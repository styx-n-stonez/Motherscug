using System;
using System.Collections.Generic;
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
            internal int DecideTick;
            internal int LastSeenTick;
            internal int ImpactTicks;
            internal int DodgeTick;
            internal string Move;
            internal float Roll;
        }

        private const int EVADE_DONE = 0;
        private const int EVADE_ARMED = 1;
        private const int EVADE_DODGED = 2;

        private const int EVADE_MAX_RECS = 16;

        private const float SPEAR_THROWN_LIFT = 0.45f;

        private const int EVADE_MAX_TRACE_TICKS = 40;

        private List<EvadeRec> evadeRecs;

        private int evadeStaggerUntil = int.MinValue / 2;
        private int evadeCooldownUntil = int.MinValue / 2;

        private Vector2[] evadeBodyPos;
        private float[] evadeRise;

        internal bool EvadeStaggering { get { return GameTick < evadeStaggerUntil; } }

        private struct EvadeTrace
        {
            internal int HitTick;
            internal float MinClear;
            internal int MinClearTick;
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
                if (rec.State == EVADE_ARMED) EvadeArmedTick(p, rec);
                return;
            }

            int horizon = Math.Max(1, p.EvasionReactionMaxTicks);
            PrepareEvadeBody(false, 0f, 0f);
            EvadeTrace t = TraceSpear(weapon, 0, horizon);
            float clearNow = SpearClearNow(weapon);
            bool hits = t.HitTick > 0;
            bool passes = !hits && t.MinClear < clearNow && (t.MinClearTick < horizon || t.StopTick > 0);
            if (!hits && !passes) return;

            rec = new EvadeRec
            {
                Spear = weapon,
                Thrower = weapon.thrownBy,
                ThrownPos = weapon.thrownPos,
                State = EVADE_DONE,
                DecideTick = now,
                LastSeenTick = now,
                ImpactTicks = t.HitTick,
                Move = "none",
            };
            if (evadeRecs.Count >= EVADE_MAX_RECS) DropOldestEvadeRec();
            evadeRecs.Add(rec);

            string busy = EvadeBusy();
            if (busy != null)
            {
                return;
            }
            if (now < evadeCooldownUntil)
            {
                return;
            }
            if (AI == null || !AI.VisualContact(weapon.firstChunk))
            {
                return;
            }
            if (!hits)
            {
                return;
            }
            if (t.HitTick < p.EvasionReactionMinTicks)
            {
                return;
            }
            float roll = UnityEngine.Random.value;
            rec.Roll = roll;
            if (p.EvasionChance < 1f && roll >= p.EvasionChance)
            {
                return;
            }
            rec.State = EVADE_ARMED;
            EvadeArmedTick(p, rec);
        }

        private void EvadeArmedTick(FoxBreedParams p, EvadeRec rec)
        {
            int now = GameTick;
            Weapon w = rec.Spear;
            int horizon = Math.Max(1, p.EvasionReactionMaxTicks);
            int min = p.EvasionReactionMinTicks;

            PrepareEvadeBody(false, 0f, 0f);
            EvadeTrace t = TraceSpear(w, 0, horizon);
            if (t.HitTick <= 0)
            {
                rec.State = EVADE_DONE;
                return;
            }
            string busy = EvadeBusy();
            if (busy != null)
            {
                rec.State = EVADE_DONE;
                return;
            }
            if (now < evadeCooldownUntil)
            {
                rec.State = EVADE_DONE;
                return;
            }
            if (t.HitTick < min)
            {
                rec.State = EVADE_DONE;
                return;
            }

            int traceTicks = Math.Min(EVADE_MAX_TRACE_TICKS, t.HitTick + horizon);
            string duckWhy, hopWhy;
            float duckClear = 0f, hopClear = 0f, dropPx = 0f, impulse = 0f;
            string chosen = null;

            if (!Grounded)
            {
                duckWhy = hopWhy = Swimming ? "swimming" : "airborne";
            }
            else
            {
                dropPx = PrepareDuckBody();
                if (dropPx < 0.5f) duckWhy = "no_drop";
                else
                {
                    EvadeTrace d = TraceSpear(w, 0, traceTicks);
                    duckClear = d.MinClear;
                    duckWhy = d.HitTick > 0 ? "still_hit" : "clears";
                    if (d.HitTick <= 0) chosen = "duck";
                }

                float g = Mathf.Max(0.05f, gravity);
                impulse = FoxStepUpAction.HopImpulse(p, g);
                if (chosen != null) hopWhy = "not_tried";
                else if (!HopHeadroom(impulse, g)) hopWhy = "ceiling";
                else
                {
                    float vy0 = Mathf.Max(MaxChunkVelY(), impulse);
                    PrepareEvadeBody(true, vy0, g);
                    EvadeTrace h = TraceSpear(w, 1, traceTicks);
                    hopClear = h.MinClear;
                    hopWhy = h.HitTick > 0 ? "still_hit" : "clears";
                    if (h.HitTick <= 0) chosen = "hop";
                }
            }

            if (chosen == null)
            {
                if (t.HitTick - 1 >= min) return;
                rec.State = EVADE_DONE;
                return;
            }

            if (move != null && move.Charge.Running)
            {
                move.Charge.End(p, "evaded");
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
                    bodyChunks[i].vel.y = Mathf.Max(bodyChunks[i].vel.y, impulse);
            }
            int stagger = Math.Max(0, p.EvasionStaggerTicks);
            int cooldown = Math.Max(0, p.EvasionCooldownTicks);
            evadeStaggerUntil = now + stagger;
            evadeCooldownUntil = now + cooldown;
            rec.State = EVADE_DODGED;
            rec.DodgeTick = now;
            rec.ImpactTicks = t.HitTick;
            rec.Move = chosen;
        }

        private void TickEvasion()
        {
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
                if (r.State == EVADE_ARMED && (!flying || now - r.LastSeenTick > 2))
                {
                    evadeRecs.RemoveAt(i);
                    continue;
                }
                if (r.State == EVADE_DONE && !flying) evadeRecs.RemoveAt(i);
            }
        }

        private void EvadeNoteHit(BodyChunk source)
        {
            Weapon w = source != null ? source.owner as Weapon : null;
            if (w == null) return;
            EvadeRec r = FindEvadeRec(w);
            if (r == null) return;
            if (r.State == EVADE_DODGED)
            {
            }
            else if (r.State == EVADE_ARMED)
            {
            }
            else return;
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
            if (pounce != PounceState.None || GatherIsActive || PounceFlight > 0f) return "pounce";
            if (PoleHeld || PoleOwnsGravity) return "pole";
            if (WallHeld || WallOwnsGravity) return "wall";
            if (ClimbOwnsGravity) return "climb";
            if (EngineOwnsBody || inShortcut) return "shortcut";
            string action = move != null ? move.RunningActionNameExcept(move.Charge) : null;
            if (action != null) return "action_" + action;
            return null;
        }

        private void PrepareEvadeBody(bool ballistic, float vy0, float g)
        {
            int n = bodyChunks.Length;
            if (evadeBodyPos == null || evadeBodyPos.Length != n) evadeBodyPos = new Vector2[n];
            for (int i = 0; i < n; i++) evadeBodyPos[i] = bodyChunks[i].pos;
            if (evadeRise == null || evadeRise.Length < EVADE_MAX_TRACE_TICKS + 1)
                evadeRise = new float[EVADE_MAX_TRACE_TICKS + 1];

            if (!ballistic && !Grounded)
            {
                ballistic = true;
                vy0 = bodyChunks[0].vel.y;
                g = Mathf.Max(0.05f, gravity);
            }
            evadeRise[0] = 0f;
            float vy = vy0, y = 0f;
            for (int k = 1; k <= EVADE_MAX_TRACE_TICKS; k++)
            {
                if (ballistic)
                {
                    vy -= g;
                    vy *= airFriction;
                    y += vy;
                }
                evadeRise[k] = y;
            }
        }

        private float PrepareDuckBody()
        {
            PrepareEvadeBody(false, 0f, 0f);
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
            for (int k = 0; k < evadeRise.Length; k++) evadeRise[k] = 0f;
            return maxDrop;
        }

        private EvadeTrace TraceSpear(Weapon w, int lag, int ticks)
        {
            EvadeTrace t;
            t.HitTick = -1;
            t.MinClear = float.MaxValue;
            t.MinClearTick = -1;
            t.StopTick = -1;

            BodyChunk sc = w.firstChunk;
            Vector2 s = sc.pos;
            Vector2 v = sc.vel;
            float g = w.gravity;
            float lift = (w is Spear) ? SPEAR_THROWN_LIFT : 0f;
            float af = w.airFriction;
            float projRad = SpearHitRad(w);
            ticks = Math.Min(ticks, EVADE_MAX_TRACE_TICKS);

            for (int k = 1; k <= ticks; k++)
            {
                Vector2 prev = s;
                v.y -= g;
                v *= af;
                s += v;
                v.y += lift;

                for (int l = 0; l <= lag; l++)
                {
                    int kb = Math.Max(0, k - l);
                    for (int i = 0; i < bodyChunks.Length; i++)
                    {
                        Vector2 c = new Vector2(evadeBodyPos[i].x + bodyChunks[i].vel.x * kb,
                                                evadeBodyPos[i].y + evadeRise[kb]);
                        float clear = Vector2.Distance(Custom.ClosestPointOnLineSegment(prev, s, c), c)
                                      - (bodyChunks[i].rad + projRad);
                        if (clear < t.MinClear) { t.MinClear = clear; t.MinClearTick = k; }
                        if (clear < 0f && t.HitTick < 0) t.HitTick = k;
                    }
                }
                if (t.HitTick > 0) return t;

                Room.Tile.TerrainType tt = room.GetTile(s).Terrain;
                if (tt == Room.Tile.TerrainType.Solid || tt == Room.Tile.TerrainType.Slope)
                {
                    t.StopTick = k;
                    return t;
                }
            }
            return t;
        }

        private float SpearClearNow(Weapon w)
        {
            float best = float.MaxValue;
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                float clear = Vector2.Distance(w.firstChunk.pos, bodyChunks[i].pos) - (bodyChunks[i].rad + SpearHitRad(w));
                if (clear < best) best = clear;
            }
            return best;
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

        private bool HopHeadroom(float impulse, float g)
        {
            float apex = impulse * impulse / (2f * g);
            for (int i = 0; i < bodyChunks.Length; i++)
            {
                BodyChunk c = bodyChunks[i];
                for (float dy = c.rad; dy <= apex + c.rad; dy += 10f)
                {
                    Room.Tile.TerrainType tt = room.GetTile(c.pos + new Vector2(0f, dy)).Terrain;
                    if (tt == Room.Tile.TerrainType.Solid || tt == Room.Tile.TerrainType.Slope) return false;
                }
                Room.Tile.TerrainType top = room.GetTile(c.pos + new Vector2(0f, apex + c.rad)).Terrain;
                if (top == Room.Tile.TerrainType.Solid || top == Room.Tile.TerrainType.Slope) return false;
            }
            return true;
        }
    }
}
