using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class Fox : Weapon.INotifyOfFlyingWeapons
    {
        private sealed class EvadeRec
        {
            internal Weapon Spear;
            internal Creature Thrower;
            internal Vector2 ThrownPos;
            internal int State;
            internal int FirstTick;
            internal int LastSeenTick;
            internal int Gate = -1;
            internal string GateDetail;
            internal readonly int[] GateTicks = new int[EVADE_GATE_NAMES.Length];
            internal int ImpactTicks;
            internal int DodgeTick;
            internal string Move;
            internal bool Rolled;
            internal float Roll;
            internal string SkipReason;
        }

        private const int EVADE_DONE = 0;
        private const int EVADE_OPEN = 1;
        private const int EVADE_DODGED = 2;

        private const int EVADE_GATE_OFF_PATH = 0;
        private const int EVADE_GATE_BUSY = 1;
        private const int EVADE_GATE_COOLDOWN = 2;
        private const int EVADE_GATE_UNSEEN = 3;
        private const int EVADE_GATE_NO_MOVE = 4;
        private static readonly string[] EVADE_GATE_NAMES = { "off_path", "busy", "cooldown", "unseen", "no_move" };

        private const int EVADE_MAX_RECS = 16;

        private const float SPEAR_THROWN_LIFT = 0.45f;

        private const int EVADE_MAX_TRACE_TICKS = 40;

        private const int EVADE_LIFT_TRIES = 3;

        private const float EVADE_LIFT_MARGIN = 1f;

        private const int EVADE_LAND_GRACE_TICKS = 4;

        private const int EVADE_FLOOR_SCAN_ROWS = 12;

        private List<EvadeRec> evadeRecs;

        private int evadeStaggerUntil = int.MinValue / 2;
        private int evadeCooldownUntil = int.MinValue / 2;

        private bool evadeDodgeOpen;
        private int evadeDodgeTick;
        private int evadeLandBy;
        private bool evadeResumePending;

        private Vector2[] evadeBodyPos;
        private float[] evadeRise;

        private static readonly Func<Weapon, IntVector2> evadeThrowDirGetter = BuildThrowDirGetter();

        private static Func<Weapon, IntVector2> BuildThrowDirGetter()
        {
            try
            {
                FieldInfo f = typeof(Weapon).GetField("throwDir", BindingFlags.Instance | BindingFlags.NonPublic);
                if (f == null || f.FieldType != typeof(IntVector2)) return null;
                ParameterExpression w = Expression.Parameter(typeof(Weapon), "w");
                return Expression.Lambda<Func<Weapon, IntVector2>>(Expression.Field(w, f), w).Compile();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IntVector2 ThrowDirOf(Weapon w)
        {
            if (evadeThrowDirGetter != null) return evadeThrowDirGetter(w);
            Vector2 v = w.firstChunk.vel;
            if (Mathf.Abs(v.x) >= Mathf.Abs(v.y)) return new IntVector2(v.x >= 0f ? 1 : -1, 0);
            return new IntVector2(0, v.y >= 0f ? 1 : -1);
        }

        internal bool EvadeStaggering { get { return GameTick < evadeStaggerUntil; } }

        internal bool EvadeLandingOwned { get { return evadeDodgeOpen && GameTick <= evadeLandBy; } }

        private struct EvadeTrace
        {
            internal int HitTick;
            internal float MinClear;
            internal float AimDy;
            internal int StopTick;
        }

        public void FlyingWeapon(Weapon weapon)
        {
            FoxBreedParams p = P;
            if (!p.EvasionEnabled) return;
            if (!(weapon is Spear) || disabled || room == null || weapon.room != room) return;
            if (bodyChunks == null || bodyChunks.Length == 0) return;
            try
            {
                EvadeConsider(p, weapon);
            }
            catch (Exception e)
            {
                FoxFault.Warn($"fox_evade_fault variant={VariantId} id={abstractCreature.ID} " +
                              $"reason={e.GetType().Name} msg={e.Message} tick={GameTick}");
            }
        }

        private void EvadeConsider(FoxBreedParams p, Weapon weapon)
        {
            int now = GameTick;
            if (evadeRecs == null) evadeRecs = new List<EvadeRec>();

            EvadeRec rec = FindEvadeRec(weapon);
            if (rec != null)
            {
                rec.LastSeenTick = now;
                if (rec.State != EVADE_OPEN) return;
            }

            int horizon = Math.Max(1, p.EvasionReactionMaxTicks);
            int min = p.EvasionReactionMinTicks;
            float pad = Mathf.Max(0f, p.EvasionPadPx);
            bool air = !Grounded;
            PrepareEvadeBody(0f, air, air ? bodyChunks[0].vel.y : 0f, Mathf.Max(0.05f, gravity));
            EvadeTrace t = TraceSpear(weapon, 0, horizon, pad);
            bool threat = t.HitTick > 0;

            if (rec == null)
            {
                if (!threat) return;
                rec = new EvadeRec
                {
                    Spear = weapon,
                    Thrower = weapon.thrownBy,
                    ThrownPos = weapon.thrownPos,
                    State = EVADE_OPEN,
                    FirstTick = now,
                    LastSeenTick = now,
                    ImpactTicks = t.HitTick,
                    Move = "none",
                    SkipReason = "none",
                };
                if (evadeRecs.Count >= EVADE_MAX_RECS) DropOldestEvadeRec();
                evadeRecs.Add(rec);
            }
            if (threat) rec.ImpactTicks = t.HitTick;

            int gate = -1;
            string detail = null;
            if (!threat) gate = EVADE_GATE_OFF_PATH;
            else if ((detail = EvadeBusy()) != null) gate = EVADE_GATE_BUSY;
            else if (now < evadeCooldownUntil) gate = EVADE_GATE_COOLDOWN;
            else if (AI == null || !AI.VisualContact(weapon.firstChunk)) gate = EVADE_GATE_UNSEEN;

            bool closed = threat && t.HitTick < min;

            if (gate >= 0)
            {
                EvadeGate(rec, gate, detail);
                if (closed) EvadeClose(rec, "window_closed");
                return;
            }
            if (closed)
            {
                EvadeClose(rec, rec.Gate < 0 ? "inside_min" : "window_closed");
                return;
            }

            if (!rec.Rolled)
            {
                rec.Rolled = true;
                rec.Roll = UnityEngine.Random.value;
                if (p.EvasionChance < 1f && rec.Roll >= p.EvasionChance)
                {
                    rec.State = EVADE_DONE;
                    rec.SkipReason = "roll";
                    return;
                }
            }

            EvadeTryMove(p, rec, t, pad);
        }

        private static bool EvadeGate(EvadeRec rec, int gate, string detail)
        {
            rec.GateTicks[gate]++;
            bool changed = rec.Gate != gate || rec.GateDetail != detail;
            rec.Gate = gate;
            rec.GateDetail = detail;
            return changed;
        }

        private void EvadeClose(EvadeRec rec, string why)
        {
            rec.State = EVADE_DONE;
            rec.SkipReason = rec.Gate < 0 ? "point_blank" : EVADE_GATE_NAMES[rec.Gate];
        }

        private void EvadeTryMove(FoxBreedParams p, EvadeRec rec, EvadeTrace t, float pad)
        {
            int now = GameTick;
            Weapon w = rec.Spear;
            int horizon = Math.Max(1, p.EvasionReactionMaxTicks);
            int traceTicks = Math.Min(EVADE_MAX_TRACE_TICKS, t.HitTick + horizon);
            float g = Mathf.Max(0.05f, gravity);
            float pop = Mathf.Max(0f, p.EvasionDodgePop);
            float maxLift = Mathf.Max(0f, p.EvasionDodgeMaxPx);

            bool duckFirst = t.AimDy > 0f;
            string upWhy = "not_tried", duckWhy = "not_tried";
            float lift = 0f, upClear = 0f, dropPx = 0f, duckClear = 0f;
            int landTicks = 0;
            string chosen = null;
            for (int pass = 0; pass < 2 && chosen == null; pass++)
            {
                if ((pass == 0) == duckFirst)
                {
                    duckWhy = EvadeTryDuck(w, traceTicks, pad, out dropPx, out duckClear);
                    if (duckWhy == "clears") chosen = "duck";
                }
                else
                {
                    upWhy = EvadeTryUp(w, traceTicks, pad, pop, maxLift, g, out lift, out upClear, out landTicks);
                    if (upWhy == "clears") chosen = "up";
                }
            }

            if (chosen == null)
            {
                EvadeGate(rec, EVADE_GATE_NO_MOVE, "up_" + upWhy + "+duck_" + duckWhy);
                return;
            }

            if (move != null && move.Charge.Running)
            {
                move.Charge.End(p, "evaded");
            }
            if (move != null && move.Pounce.Running)
            {
                move.Pounce.End(p, "evaded");
            }
            if (move != null && move.Turn.Running)
            {
                move.Turn.End(p, "evaded");
            }

            if (chosen == "duck")
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].pos.y = evadeBodyPos[i].y;
                    bodyChunks[i].vel.y = Mathf.Min(bodyChunks[i].vel.y, 0f);
                }
            }
            else
            {
                for (int i = 0; i < bodyChunks.Length; i++)
                {
                    bodyChunks[i].pos.y += lift;
                    bodyChunks[i].vel.y = Mathf.Max(bodyChunks[i].vel.y, pop);
                }
            }
            int stagger = Math.Max(0, p.EvasionStaggerTicks);
            int cooldown = Math.Max(0, p.EvasionCooldownTicks);
            evadeStaggerUntil = now + stagger;
            evadeCooldownUntil = now + cooldown;
            evadeDodgeOpen = true;
            evadeDodgeTick = now;
            evadeLandBy = now + landTicks + EVADE_LAND_GRACE_TICKS;
            evadeResumePending = true;
            rec.State = EVADE_DODGED;
            rec.DodgeTick = now;
            rec.ImpactTicks = t.HitTick;
            rec.Move = chosen;
            rec.SkipReason = "dodged";
        }

        private string EvadeTryUp(Weapon w, int traceTicks, float pad, float pop, float maxLift,
                                  float g, out float lift, out float clear, out int landTicks)
        {
            lift = 0f;
            clear = 0f;
            landTicks = 0;
            for (int i = 0; ; i++)
            {
                PrepareEvadeBody(lift, false, 0f, g);
                EvadeTrace held = TraceSpear(w, 1, traceTicks, pad);
                if (held.HitTick <= 0) break;
                if (i >= EVADE_LIFT_TRIES) return "still_hit";
                lift += -held.MinClear + EVADE_LIFT_MARGIN;
                if (lift > maxLift) return "too_far";
                if (!EvadeHeadroom(lift, pop, g)) return "ceiling";
            }
            if (lift <= 0f) return "no_overlap";
            landTicks = PrepareEvadeBody(lift, true, Mathf.Max(MaxChunkVelY(), pop), g);
            EvadeTrace arc = TraceSpear(w, 1, traceTicks, pad);
            clear = arc.MinClear;
            return arc.HitTick > 0 ? "falls_back" : "clears";
        }

        private string EvadeTryDuck(Weapon w, int traceTicks, float pad, out float dropPx, out float clear)
        {
            clear = 0f;
            dropPx = PrepareDuckBody();
            if (dropPx < 0.5f) return "no_drop";
            EvadeTrace d = TraceSpear(w, 0, traceTicks, pad);
            clear = d.MinClear;
            return d.HitTick > 0 ? "still_hit" : "clears";
        }

        private void TickEvasion()
        {
            if (evadeDodgeOpen || evadeResumePending) TickEvadeBody();
            if (evadeRecs == null || evadeRecs.Count == 0) return;
            int now = GameTick;
            int window = Math.Max(1, P.EvasionReactionMaxTicks);
            for (int i = evadeRecs.Count - 1; i >= 0; i--)
            {
                EvadeRec r = evadeRecs[i];
                bool flying = SameThrowStillFlying(r);
                if (r.State == EVADE_DODGED)
                {
                    bool late = now > r.DodgeTick + r.ImpactTicks + window;
                    if (!flying || late)
                    {
                        evadeRecs.RemoveAt(i);
                    }
                    continue;
                }
                if (r.State == EVADE_OPEN && (!flying || now - r.LastSeenTick > 2))
                {
                    EvadeClose(r, flying ? "lost_spear" : "stopped_flying");
                    evadeRecs.RemoveAt(i);
                    continue;
                }
                if (r.State == EVADE_DONE && !flying) evadeRecs.RemoveAt(i);
            }
        }

        private void TickEvadeBody()
        {
            int now = GameTick;
            if (evadeDodgeOpen && now >= evadeDodgeTick + 2)
            {
                string how = null;
                if (dead || disabled || room == null || bodyChunks == null) how = "ended";
                else if (inShortcut || EngineOwnsBody) how = "shortcut";
                else if (PoleHeld || WallHeld) how = "surface";
                else if (Submersion > 0f) how = "water";
                else if (Grounded) how = "ground";
                if (how != null)
                {
                    evadeDodgeOpen = false;
                }
            }
            if (evadeResumePending && !evadeDodgeOpen && !EvadeStaggering)
            {
                evadeResumePending = false;
            }
        }

        private void EvadeNoteHit(BodyChunk source)
        {
            Weapon w = source != null ? source.owner as Weapon : null;
            if (w == null || !(w is Spear)) return;
            EvadeRec r = FindEvadeRec(w);
            if (r == null)
            {
                return;
            }
            if (r.State == EVADE_DODGED)
            {
                r.SkipReason = "hit_after_dodge";
            }
            else
            {
                bool open = r.State == EVADE_OPEN;
                if (open) r.SkipReason = r.Gate < 0 ? "none" : EVADE_GATE_NAMES[r.Gate];
            }
            r.State = EVADE_DONE;
        }

        private EvadeRec FindEvadeRec(Weapon w)
        {
            if (evadeRecs == null) return null;
            for (int i = 0; i < evadeRecs.Count; i++)
            {
                EvadeRec r = evadeRecs[i];
                if (!ReferenceEquals(r.Spear, w)) continue;
                if (ReferenceEquals(r.Thrower, w.thrownBy) && r.ThrownPos == w.thrownPos) return r;
                if (r.State == EVADE_DODGED)
                {
                }
                else if (r.State == EVADE_OPEN) EvadeClose(r, "rethrown");
                evadeRecs.RemoveAt(i);
                return null;
            }
            return null;
        }

        private bool SameThrowStillFlying(EvadeRec r)
        {
            Weapon w = r.Spear;
            return w != null && w.mode == Weapon.Mode.Thrown && w.room == room
                   && ReferenceEquals(w.thrownBy, r.Thrower) && w.thrownPos == r.ThrownPos;
        }

        private void DropOldestEvadeRec()
        {
            for (int i = 0; i < evadeRecs.Count; i++)
            {
                if (evadeRecs[i].State == EVADE_DONE) { evadeRecs.RemoveAt(i); return; }
            }
            evadeRecs.RemoveAt(0);
        }

        private string EvadeBusy()
        {
            if (dead) return "dead";
            if (!Consious) return "stunned";
            if (grabbedBy != null && grabbedBy.Count > 0) return "held";
            if (pounce == PounceState.Propel || PounceFlight > 0f) return "pounce";
            if (PoleHeld || PoleOwnsGravity) return "pole";
            if (WallHeld || WallOwnsGravity) return "wall";
            if (ClimbOwnsGravity) return "climb";
            if (EngineOwnsBody || inShortcut) return "shortcut";
            if (evadeDodgeOpen) return "dodging";
            if (Swimming) return "swimming";
            if (!Grounded) return "airborne";
            string action = move != null ? move.RunningActionNameExcept(move.Charge, move.Pounce, move.Turn) : null;
            if (action != null) return "action_" + action;
            return null;
        }

        private int PrepareEvadeBody(float lift, bool arc, float vy0, float g)
        {
            int n = bodyChunks.Length;
            if (evadeBodyPos == null || evadeBodyPos.Length != n) evadeBodyPos = new Vector2[n];
            for (int i = 0; i < n; i++) evadeBodyPos[i] = bodyChunks[i].pos;
            if (evadeRise == null || evadeRise.Length < EVADE_MAX_TRACE_TICKS + 1)
                evadeRise = new float[EVADE_MAX_TRACE_TICKS + 1];

            evadeRise[0] = lift;
            if (!arc)
            {
                for (int k = 1; k <= EVADE_MAX_TRACE_TICKS; k++) evadeRise[k] = lift;
                return 0;
            }
            float ground = -EvadeFloorGap();
            float vy = vy0, y = lift;
            int land = -1;
            for (int k = 1; k <= EVADE_MAX_TRACE_TICKS; k++)
            {
                if (land < 0)
                {
                    vy -= g;
                    vy *= airFriction;
                    y += vy;
                    if (y <= ground)
                    {
                        y = ground;
                        land = k;
                    }
                }
                evadeRise[k] = y;
            }
            return land < 0 ? EVADE_MAX_TRACE_TICKS : land;
        }

        private float EvadeFloorGap()
        {
            float best = float.MaxValue;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                BodyChunk c = bodyChunks[i];
                int col = room.GetTilePosition(c.pos).x;
                int bottomRow = room.GetTilePosition(new Vector2(c.pos.x, c.pos.y - c.rad)).y;
                for (int supportRow = bottomRow; supportRow >= bottomRow - EVADE_FLOOR_SCAN_ROWS; supportRow--)
                {
                    IntVector2 tile = new IntVector2(col, supportRow);
                    Room.Tile.TerrainType tt = room.GetTile(tile).Terrain;
                    float restY;
                    if (tt == Room.Tile.TerrainType.Solid || tt == Room.Tile.TerrainType.Floor)
                    {
                        restY = (supportRow + 1) * 20f + c.rad;
                    }
                    else if (tt == Room.Tile.TerrainType.Slope)
                    {
                        Room.SlopeDirection sd = room.IdentifySlope(tile);
                        if (sd != Room.SlopeDirection.UpLeft && sd != Room.SlopeDirection.UpRight) continue;
                        float u = Mathf.Clamp(c.pos.x - col * 20f, 0f, 20f);
                        restY = supportRow * 20f + (sd == Room.SlopeDirection.UpLeft ? u : 20f - u) + 2f * c.rad;
                    }
                    else continue;
                    best = Mathf.Min(best, Mathf.Max(0f, c.pos.y - restY));
                    break;
                }
            }
            return best;
        }

        private float PrepareDuckBody()
        {
            PrepareEvadeBody(0f, false, 0f, 0f);
            float maxDrop = 0f;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                BodyChunk c = bodyChunks[i];
                float bottomY = c.pos.y - c.rad;
                int col = room.GetTilePosition(c.pos).x;
                int bottomRow = room.GetTilePosition(new Vector2(c.pos.x, bottomY)).y;
                float lowered = c.pos.y;
                for (int supportRow = bottomRow; supportRow >= bottomRow - 1; supportRow--)
                {
                    Room.Tile.TerrainType tt = room.GetTile(new IntVector2(col, supportRow)).Terrain;
                    if (tt == Room.Tile.TerrainType.Solid || tt == Room.Tile.TerrainType.Floor)
                    {
                        float floorTop = (supportRow + 1) * 20f;
                        if (floorTop <= bottomY) lowered = floorTop + c.rad;
                        break;
                    }
                    if (tt == Room.Tile.TerrainType.Slope) break;
                }
                float drop = Mathf.Max(0f, c.pos.y - lowered);
                evadeBodyPos[i].y = c.pos.y - drop;
                if (drop > maxDrop) maxDrop = drop;
            }
            return maxDrop;
        }

        private EvadeTrace TraceSpear(Weapon w, int lag, int ticks, float pad)
        {
            EvadeTrace t;
            t.HitTick = -1;
            t.MinClear = float.MaxValue;
            t.AimDy = 0f;
            t.StopTick = -1;

            BodyChunk sc = w.firstChunk;
            Vector2 s = sc.pos;
            Vector2 v = sc.vel;
            float g = w.gravity;
            float lift = (w is Spear) ? SPEAR_THROWN_LIFT : 0f;
            float af = w.airFriction;
            float projRad = SpearHitRad(w) + pad;
            ticks = Math.Min(ticks, EVADE_MAX_TRACE_TICKS);

            IntVector2 dir = ThrowDirOf(w);
            IntVector2 contact = sc.ContactPoint;
            int bounceFrames = w.floorBounceFrames;
            int modeFrames = w.throwModeFrames;
            float exitSpeed = w.overrideExitThrownSpeed > 0f ? w.overrideExitThrownSpeed : w.exitThrownModeSpeed;
            float terrRad = Mathf.Max(sc.rad * sc.terrainSqueeze, 1f);
            bool collide = sc.collideWithTerrain;

            for (int k = 1; k <= ticks; k++)
            {
                if (k > 1 && bounceFrames > 0 && dir.y == 0)
                {
                    bounceFrames--;
                    if (contact.y != 0 && Mathf.Abs(v.x) > 5f)
                    {
                        v.y = (Mathf.Abs(v.y) + 1f) * -contact.y + (contact.y < 0 ? 4.5f : 0f);
                        v.x = Mathf.Max(exitSpeed + 1f, Mathf.Abs(v.x)) * Mathf.Sign(v.x);
                        bounceFrames /= 2;
                    }
                }
                if (modeFrames > 0) modeFrames--;
                if ((v.magnitude < exitSpeed && !w.doNotTumbleAtLowSpeed) || modeFrames == 0)
                {
                    EvadeStop(ref t, k - 1, modeFrames == 0 ? "frames" : "slow", s);
                    return t;
                }
                if (bounceFrames > 0 && dir.x != 0 && (contact.x != 0 || contact.y != 0)
                    && room.GetTile(s).Terrain == Room.Tile.TerrainType.Slope)
                {
                    EvadeStop(ref t, k - 1, "redirect", s);
                    return t;
                }
                if (contact == dir)
                {
                    EvadeStop(ref t, k - 1, "wall", s + dir.ToVector2() * (terrRad + 1f));
                    return t;
                }

                Vector2 prev = s;
                v.y -= g;
                v *= af;
                s += v;
                IntVector2 hitTile = new IntVector2(-1, -1);
                if (collide) contact = SpearCollide(prev, ref s, ref v, terrRad, w.bounce, w.surfaceFriction,
                                                    g, !sc.goThroughFloors, sc.collideWithSlopes, out hitTile);
                else contact = new IntVector2(0, 0);
                v.y += lift;

                for (int l = 0; l <= lag; l++)
                {
                    int kb = Math.Max(0, k - l);
                    for (int i = 0; i < bodyChunks.Length; i++)
                    {
                        Vector2 c = new Vector2(evadeBodyPos[i].x + bodyChunks[i].vel.x * kb,
                                                evadeBodyPos[i].y + evadeRise[kb]);
                        Vector2 near = Custom.ClosestPointOnLineSegment(prev, s, c);
                        float clear = Vector2.Distance(near, c) - (bodyChunks[i].rad + projRad);
                        if (clear < t.MinClear)
                        {
                            t.MinClear = clear;
                            t.AimDy = near.y - c.y;
                        }
                        if (clear < 0f && t.HitTick < 0) t.HitTick = k;
                    }
                }

                if (contact == dir)
                {
                    t.StopTick = k;
                    return t;
                }
            }
            return t;
        }

        private void EvadeStop(ref EvadeTrace t, int tick, string reason, Vector2 at)
        {
            t.StopTick = tick;
        }

        private IntVector2 SpearCollide(Vector2 last, ref Vector2 s, ref Vector2 v, float r, float bounce,
                                        float fric, float g, bool floors, bool slopes, out IntVector2 hitTile)
        {
            IntVector2 c = new IntVector2(0, 0);
            hitTile = new IntVector2(-1, -1);
            float rest = 1f + 9f * (1f - bounce);
            float along = Mathf.Clamp01(fric * 2f);
            int colL = TileIndex(s.x - r + 1f), colR = TileIndex(s.x + r - 1f);

            if (s.y < last.y)
            {
                int from = TileIndex(last.y - r), to = TileIndex(s.y - r - 0.01f);
                for (int row = from; row >= to && c.y == 0; row--)
                {
                    float top = (row + 1) * 20f;
                    if (last.y - r < top - 0.01f) continue;
                    for (int col = colL; col <= colR; col++)
                    {
                        if (!SpearFloorAt(col, row, floors) || SpearFloorAt(col, row + 1, floors)) continue;
                        s.y = top + r;
                        c.y = -1;
                        hitTile = new IntVector2(col, row);
                        v.y = Mathf.Abs(v.y) * bounce;
                        if (v.y < g || v.y < rest) v.y = 0f;
                        v.x *= along;
                        break;
                    }
                }
            }
            else if (s.y > last.y)
            {
                int from = TileIndex(last.y + r), to = TileIndex(s.y + r + 0.01f);
                for (int row = from; row <= to && c.y == 0; row++)
                {
                    float bottom = row * 20f;
                    if (last.y + r > bottom + 0.01f) continue;
                    for (int col = colL; col <= colR; col++)
                    {
                        if (!SpearSolidAt(col, row) || SpearSolidAt(col, row - 1)) continue;
                        s.y = bottom - r;
                        c.y = 1;
                        hitTile = new IntVector2(col, row);
                        v.y = -Mathf.Abs(v.y) * bounce;
                        if (Mathf.Abs(v.y) < rest) v.y = 0f;
                        v.x *= along;
                        break;
                    }
                }
            }

            if (slopes && c.y == 0)
            {
                IntVector2 tile;
                Room.SlopeDirection sd = SpearSlopeNear(s, r, out tile);
                if (sd != Room.SlopeDirection.Broken)
                {
                    float left = tile.x * 20f, bottom = tile.y * 20f;
                    float u = Mathf.Clamp(s.x - left, 0f, 20f);
                    if (sd == Room.SlopeDirection.UpLeft || sd == Room.SlopeDirection.UpRight)
                    {
                        float surf = bottom + (sd == Room.SlopeDirection.UpLeft ? u : 20f - u);
                        if (s.y <= surf + 2f * r)
                        {
                            s.y = surf + 2f * r;
                            c.y = -1;
                            hitTile = tile;
                            v.y = 0f;
                            v.x *= 1f - fric;
                        }
                    }
                    else
                    {
                        float surf = bottom + (sd == Room.SlopeDirection.DownLeft ? 20f - u : u);
                        if (s.y >= surf - 2f * r)
                        {
                            s.y = surf - 2f * r;
                            c.y = 1;
                            hitTile = tile;
                            v.y = 0f;
                            v.x *= 1f - fric;
                        }
                    }
                }
            }

            if (v.x != 0f && s.x != last.x)
            {
                int sgn = s.x > last.x ? 1 : -1;
                int lastCol = TileIndex(last.x);
                int from = TileIndex(last.x + sgn * r), to = TileIndex(s.x + sgn * (r + 0.01f));
                int rowB = TileIndex(s.y - r + 1f), rowT = TileIndex(s.y + r - 1f);
                bool done = false;
                for (int col = from; !done && (sgn > 0 ? col <= to : col >= to); col += sgn)
                {
                    if (sgn > 0 ? col <= lastCol : col >= lastCol) continue;
                    for (int row = rowB; row <= rowT; row++)
                    {
                        if (!SpearSolidAt(col, row) || SpearSolidAt(col - sgn, row)) continue;
                        s.x = sgn > 0 ? col * 20f - r : (col + 1) * 20f + r;
                        c.x = sgn;
                        hitTile = new IntVector2(col, row);
                        v.x = -sgn * Mathf.Abs(v.x) * bounce;
                        if (Mathf.Abs(v.x) < rest) v.x = 0f;
                        v.y *= along;
                        done = true;
                        break;
                    }
                }
            }
            return c;
        }

        private static int TileIndex(float px) { return Mathf.FloorToInt(px / 20f); }

        private bool SpearSolidAt(int x, int y)
        {
            return room.GetTile(x, y).Terrain == Room.Tile.TerrainType.Solid;
        }

        private bool SpearFloorAt(int x, int y, bool floors)
        {
            Room.Tile.TerrainType tt = room.GetTile(x, y).Terrain;
            return tt == Room.Tile.TerrainType.Solid || (floors && tt == Room.Tile.TerrainType.Floor);
        }

        private Room.SlopeDirection SpearSlopeNear(Vector2 s, float r, out IntVector2 tile)
        {
            IntVector2 here = room.GetTilePosition(s);
            tile = here;
            Room.SlopeDirection sd = room.IdentifySlope(here);
            if (sd != Room.SlopeDirection.Broken) return sd;
            if (SlopeProbe(here, new IntVector2(here.x, TileIndex(s.y - r)), ref tile, ref sd)) return sd;
            if (SlopeProbe(here, new IntVector2(here.x, TileIndex(s.y + r)), ref tile, ref sd)) return sd;
            if (SlopeProbe(here, new IntVector2(TileIndex(s.x - r), here.y), ref tile, ref sd)) return sd;
            if (SlopeProbe(here, new IntVector2(TileIndex(s.x + r), here.y), ref tile, ref sd)) return sd;
            return Room.SlopeDirection.Broken;
        }

        private bool SlopeProbe(IntVector2 here, IntVector2 probe, ref IntVector2 tile, ref Room.SlopeDirection sd)
        {
            if (probe == here) return false;
            Room.SlopeDirection d = room.IdentifySlope(probe);
            if (d == Room.SlopeDirection.Broken) return false;
            tile = probe;
            sd = d;
            return true;
        }

        private static float SpearHitRad(Weapon w)
        {
            return w.firstChunk.rad + ((w.thrownBy != null && w.thrownBy is Player) ? 5f : 0f);
        }

        private float MaxChunkVelY()
        {
            float m = float.MinValue;
            for (int i = 0; i < bodyChunks.Length; i++) m = Mathf.Max(m, bodyChunks[i].vel.y);
            return m;
        }

        private bool EvadeHeadroom(float lift, float pop, float g)
        {
            float reach = lift + pop * pop / (2f * g);
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                BodyChunk c = bodyChunks[i];
                float top = reach + c.rad;
                float side = Mathf.Max(0f, c.rad - 1f);
                for (float dy = c.rad; ; dy += 10f)
                {
                    if (dy > top) dy = top;
                    for (int sx = -1; sx <= 1; sx++)
                    {
                        if (EvadePointInTerrain(c.pos + new Vector2(sx * side, dy))) return false;
                    }
                    if (dy >= top) break;
                }
            }
            return true;
        }

        private bool EvadePointInTerrain(Vector2 pt)
        {
            IntVector2 tile = room.GetTilePosition(pt);
            Room.Tile.TerrainType tt = room.GetTile(tile).Terrain;
            if (tt == Room.Tile.TerrainType.Solid) return true;
            if (tt != Room.Tile.TerrainType.Slope) return false;
            float u = Mathf.Clamp(pt.x - tile.x * 20f, 0f, 20f);
            float bottom = tile.y * 20f;
            Room.SlopeDirection sd = room.IdentifySlope(tile);
            if (sd == Room.SlopeDirection.UpLeft) return pt.y < bottom + u;
            if (sd == Room.SlopeDirection.UpRight) return pt.y < bottom + 20f - u;
            if (sd == Room.SlopeDirection.DownLeft) return pt.y > bottom + 20f - u;
            if (sd == Room.SlopeDirection.DownRight) return pt.y > bottom + u;
            return true;
        }
    }
}
