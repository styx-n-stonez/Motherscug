using System;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxTurnAction : FoxMoveAction
    {
        internal FoxTurnAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "turn"; } }

        internal override FoxBodyClaim Claims
        {
            get { return FoxBodyClaim.SpineElasticity | FoxBodyClaim.Drive; }
        }

        internal override bool CooldownTickedExternally { get { return true; } }

        protected override string TimeoutReason { get { return "bail_ticks"; } }

        private Vector2 pivotAxis;
        private Vector2 pivotPerp;
        private float   pivotSpan;
        private float   pivotS;
        private float   pivotMaxSpan;
        private float   pivotRestSpan;
        private int     pivotCollapseTicks;

        private Vector2 turnTravel;
        private int     lastTurnStartTick = -1000000;

        private bool    anchorRear;

        private Vector2 toTarget;
        private bool    drove;
        private float   floorPxThisTick;

        internal float Progress
        {
            get { return Running ? Mathf.Clamp01((1f - pivotS) * 0.5f) : 0f; }
        }

        internal void TickCooldown()
        {
            if (Cooldown > 0) Cooldown--;
        }

        internal bool Drive(FoxBreedParams p, Vector2 to, MovementConnection conn, bool routed)
        {
            if (Fox.TurnModeIs(p, Fox.TURN_MODE_NONE)) return false;

            if (Running && Guard(p)) return false;

            toTarget = to;

            if (!Running)
            {
                var s = new FoxMoveSituation { Conn = conn, ToTarget = to };
                FoxMoveBid bid = Ask(p, s);
                if (!bid.Ok) { return false; }
                Begin(p, s, bid);
                if (!Running) return false;
            }

            drove = false;
            fox.Move.Guarded(p, this);
            return drove;
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length < 3) return FoxMoveBid.No("no_body", "");

            Vector2 axis = Fox.Dir(c[2].pos, c[0].pos);
            Vector2 travel = s.ToTarget.sqrMagnitude > 0.01f ? s.ToTarget.normalized : axis;
            float dot = Vector2.Dot(axis, travel);
            float travelHoriz = Mathf.Abs(travel.x);
            bool tooVertical = p.TurnTriggerMinHorizontal > 0f
                               && travelHoriz < p.TurnTriggerMinHorizontal;
            string measure = null;

            bool swimTurn = fox.Swimming;
            bool waterOk = swimTurn && p.TurnInWater;
            bool noFooting = !fox.Grounded && !waterOk;

            if (dot > p.TurnTriggerDot || tooVertical || Cooldown > 0 || noFooting
                || fox.pounce != Fox.PounceState.None || fox.GatherIsActive || fox.PounceFlight > 0f
                || fox.AirTicks > 0)
            {
                return FoxMoveBid.No(
                    dot > p.TurnTriggerDot ? "already_facing"
                    : tooVertical ? "travel_vertical"
                    : Cooldown > 0 ? "turn_cooldown"
                    : (noFooting && swimTurn) ? "water"
                    : noFooting ? "airborne"
                    : fox.pounce != Fox.PounceState.None ? "pouncing"
                    : fox.GatherIsActive ? "gathering"
                    : fox.PounceFlight > 0f ? "in_flight" : "air_ticks",
                    measure);
            }

            if (p.TurnMinIntervalTicks > 0 && lastTurnStartTick != -1000000
                && fox.MoveTick - lastTurnStartTick < p.TurnMinIntervalTicks)
            {
                return FoxMoveBid.No("interval", measure);
            }

            Creature prey = fox.chaseTarget;
            if (p.TurnFocusHold && prey != null && prey.room == fox.RoomRef
                && fox.AI != null
                && (fox.AI.CurrentBehavior == FoxAI.Behavior.Hunt
                    || fox.AI.CurrentBehavior == FoxAI.Behavior.Stalk))
            {
                Vector2 toPrey = Fox.Dir(c[1].pos, prey.mainBodyChunk.pos);
                float preyDot = Vector2.Dot(axis, toPrey);
                if (preyDot >= p.TurnFocusHoldDot)
                {
                    return FoxMoveBid.No("focus", measure);
                }
            }

            Vector2 startOff = c[2].pos - c[0].pos;
            float span = startOff.magnitude;
            if (span < 1f)
            {
                Cooldown = Math.Max(Cooldown, p.TurnCooldown);
                string m = null;
                return FoxMoveBid.No("no_span", m);
            }
            Vector2 axisUnit = startOff / span;

            Room room = fox.RoomRef;
            anchorRear = false;
            Vector2 frontEnd = c[0].pos - axisUnit * span;
            Vector2 rearEnd = c[2].pos + axisUnit * span;
            if (room != null)
            {
                string probe;
                if (EndPoseClear(p.TurnBlockedProbeLevel, room, c[0].pos, frontEnd, c[2].pos, out probe))
                {
                }
                else if (p.TurnRearAnchorEnabled
                         && EndPoseClear(p.TurnBlockedProbeLevel, room, c[2].pos, rearEnd, c[0].pos, out probe))
                {
                    anchorRear = true;
                }
                else
                {
                    Cooldown = Math.Max(Cooldown, p.TurnCooldown);
                    return FoxMoveBid.No("blocked", measure);
                }
            }

            float restSpan = fox.SpineRestSpan;
            if (Mathf.Abs(span - restSpan) > Mathf.Max(1f, p.TurnPivotStretchPx))
            {
                Cooldown = Math.Max(Cooldown, p.TurnCooldown);
                string m = null;
                return FoxMoveBid.No("bad_span", m);
            }

            pivotSpan = span;
            pivotAxis = anchorRear ? -axisUnit : axisUnit;
            pivotPerp = Vector2.zero;
            pivotRestSpan = restSpan;
            pivotMaxSpan = restSpan + Mathf.Max(0f, p.TurnPivotStretchPx);
            turnTravel = travel;
            string mOk = null;
            return FoxMoveBid.Yes("body_points_wrong_way", mOk);
        }

        private static bool EndPoseClear(bool levelProbe, Room room, Vector2 anchorPos, Vector2 endPos, Vector2 swingFromPos,
                                         out string probe)
        {
            if (!room.GetTile(endPos).Solid) { probe = "axis"; return true; }
            if (room.GetTile(swingFromPos).Solid) { probe = "swing_in_solid"; return true; }
            if (levelProbe
                && !room.GetTile(new Vector2(endPos.x, anchorPos.y)).Solid)
            {
                probe = "level";
                return true;
            }
            probe = "blocked";
            return false;
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.TurnPivotMaxTicks);
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            pivotS = 1f;
            pivotCollapseTicks = 0;
            lastTurnStartTick = fox.MoveTick;

            fox.RelaxSpineForTurnNow(p);
        }

        internal bool Guard(FoxBreedParams p)
        {
            if (!Running) return false;

            string reason = null;
            BodyChunk[] c = fox.Chunks;
            if (fox.pounce != Fox.PounceState.None || fox.GatherIsActive || fox.PounceFlight > 0f)
                reason = "aborted_pounce";
            else if (fox.charge != Fox.ChargeState.None || fox.ChargeWindup > 0f || fox.ChargeRush > 0f)
                reason = "aborted_charge";
            else if ((!fox.Grounded && !(p.TurnInWater && fox.Swimming))
                     || fox.AirTicks >= Math.Max(1, p.TurnAbortAirTicks))
                reason = "aborted_airborne";
            else if (!fox.Consious) reason = "aborted_unconscious";
            else if (fox.RoomRef == null || c == null || c.Length < 3) reason = "aborted_no_body";
            if (reason == null) return false;

            End(p, reason);
            fox.SnapSpineBackNow(p);
            return true;
        }

        protected override string ShouldAbort(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length < 3) return "no_body";

            BodyChunk anchor = c[anchorRear ? 2 : 0], swing = c[anchorRear ? 0 : 2];
            BodyChunk front = c[0], rear = c[2];

            Vector2 off = swing.pos - anchor.pos;
            float span = off.magnitude;
            pivotS = Mathf.Clamp(Vector2.Dot(off, pivotAxis) / Mathf.Max(1f, pivotSpan), -1.5f, 1.5f);

            if (span > pivotMaxSpan)
            {
                if (p.TurnPivotSpanClamp && span > 0.01f)
                {
                    Vector2 outward = off / span;
                    swing.pos = anchor.pos + outward * pivotMaxSpan;
                    float radial = Vector2.Dot(swing.vel - anchor.vel, outward);
                    if (radial > 0f) swing.vel -= outward * radial;
                    off = swing.pos - anchor.pos;
                    span = off.magnitude;
                }
                return "stretch";
            }

            floorPxThisTick = Mathf.Clamp01(p.TurnPivotSpanFloorFac) * Mathf.Max(1f, pivotRestSpan);
            if (floorPxThisTick > 0f && span < floorPxThisTick)
            {
                pivotCollapseTicks++;
                if (pivotCollapseTicks > Math.Max(1, p.TurnPivotMaxTicks / 4))
                {
                    crossingStalled = true;
                    return "crossing_stalled";
                }
            }
            else pivotCollapseTicks = 0;

            longEnoughThisTick = span >= pivotSpan * Mathf.Clamp01(p.TurnPivotReleaseSpanFac);
            if (toTarget.sqrMagnitude > 0.01f)
            {
                Vector2 now = toTarget.normalized;
                neckDotThisTick = fox.NeckDotNow;
                if (longEnoughThisTick && neckDotThisTick > 0f
                    && Vector2.Dot(Fox.Dir(rear.pos, front.pos), now) > p.TurnPivotReleaseDot)
                {
                    return "aligned";
                }
                if (Vector2.Dot(turnTravel, now) < p.TurnPivotGoalDot) return "goal_moved";
            }

            spanThisTick = span;
            offThisTick = off;
            return null;
        }

        private bool  crossingStalled;
        private bool  longEnoughThisTick;
        private float neckDotThisTick = 1f;
        private float spanThisTick;
        private Vector2 offThisTick;

        protected override string IsComplete(FoxBreedParams p)
        {
            if (pivotS > -1f + Mathf.Max(0.001f, p.TurnPivotFinish)) return null;
            neckDotThisTick = fox.NeckDotNow;
            return neckDotThisTick > 0f ? "completed" : null;
        }

        protected override void OnTick(FoxBreedParams p)
        {
            drove = true;
            BodyChunk[] c = fox.Chunks;
            BodyChunk anchor = c[anchorRear ? 2 : 0], mid = c[1], swingChunk = c[anchorRear ? 0 : 2];
            Vector2 off = offThisTick;
            float span = spanThisTick;
            float pivotFloorPx = floorPxThisTick;

            float sTarget = Mathf.Max(-1f, pivotS - Mathf.Max(0.01f, p.TurnPivotLead));
            float floor = Mathf.Clamp01(p.TurnPivotCrossFac);
            if (Mathf.Abs(sTarget) < floor) sTarget = -floor;

            Vector2 swingUp = new Vector2(-pivotAxis.y, pivotAxis.x);
            if (swingUp.y < 0f) swingUp = -swingUp;
            float swing = Mathf.Max(0f, p.TurnPivotSwingPx) * (1f - Mathf.Abs(sTarget));

            Vector2 swingGoal = anchor.pos + pivotAxis * (sTarget * pivotSpan) + pivotPerp + swingUp * swing;

            if (pivotFloorPx > 0f)
            {
                Vector2 goalOff = swingGoal - anchor.pos;
                float goalLen = goalOff.magnitude;
                Vector2 outward = goalLen > 0.01f ? goalOff / goalLen
                                                  : (span > 0.01f ? off / span : pivotAxis);
                if (goalLen < pivotFloorPx) swingGoal = anchor.pos + outward * pivotFloorPx;
                if (span < pivotFloorPx)
                {
                    Vector2 chunkOut = outward;
                    bool crossed = span > 0.01f && Vector2.Dot(off, chunkOut) < 0f;
                    swingChunk.pos = anchor.pos + chunkOut * pivotFloorPx;
                    if (crossed)
                    {
                        mid.pos = (anchor.pos + swingChunk.pos) * 0.5f;
                        mid.vel = anchor.vel;
                    }
                    float radial = Vector2.Dot(swingChunk.vel - anchor.vel, chunkOut);
                    if (radial < 0f) swingChunk.vel -= chunkOut * radial;
                    off = swingChunk.pos - anchor.pos;
                    span = off.magnitude;
                }
            }

            Vector2 midGoal = (anchor.pos + swingChunk.pos) * 0.5f;

            float speedFac = Mathf.Max(0.05f, fox.runSpeed) * Mathf.Max(0.01f, p.TurnPivotSpeed);
            DrivePivotChunk(p, swingChunk, swingGoal, speedFac, -pivotAxis);
            DrivePivotChunk(p, mid, midGoal, speedFac * p.TurnPivotMidFac, Vector2.zero);

            anchor.vel.x = Mathf.Lerp(anchor.vel.x, 0f, Mathf.Clamp01(p.TurnPivotBrake));

            Vector2 carryBefore = fox.CarryPointNow(p);
            fox.CarryThroughTurnNow(p, carryBefore);

        }

        private static void DrivePivotChunk(FoxBreedParams p, BodyChunk chunk, Vector2 goal, float step,
                                            Vector2 fallback)
        {
            Vector2 d = goal - chunk.pos;
            float dist = d.magnitude;
            if (step > dist) step = dist;
            Vector2 dir = dist > 0.01f ? d / dist : fallback;

            chunk.vel *= Mathf.Clamp01(p.TurnPivotFriction);
            chunk.vel += dir * step;
            if (p.TurnPivotJitter > 0f)
            {
                float a = UnityEngine.Random.value * 360f * Mathf.Deg2Rad;
                chunk.vel += new Vector2(Mathf.Cos(a), Mathf.Sin(a))
                           * (p.TurnPivotJitter * step * UnityEngine.Random.value);
            }
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            switch (reason)
            {
                case "aligned":
                case "goal_moved":
                case "stretch":
                case "crossing_stalled":
                    return 0;
                default:
                    return p.TurnCooldown;
            }
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            if (!wasRunning) return false;

            fox.ReleaseSpineAfterTurnNow(p);

            if (crossingStalled) fox.SnapSpineBackNow(p);
            crossingStalled = false;

            pivotS = 1f;
            pivotCollapseTicks = 0;
            return true;
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }
}
