using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxPounceAction : FoxMoveAction
    {
        internal FoxPounceAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "pounce"; } }

        internal override FoxBodyClaim Claims { get { return FoxBodyClaim.None; } }

        protected override string TimeoutReason { get { return "overran"; } }

        internal Fox.PounceState Stage { get; private set; }

        private Vector2 pounceDir;
        private Vector2 targetPos;
        private int counter;
        private int total;
        private bool isLedge;
        private bool prepared;
        private int boostLeft;
        private float launchFrac = 1f;
        private float wantDist;
        private float wantRise;
        private int gatherFramesUsed = -1;
        private int gateCooldown;
        private int endTick = -1000000;

        private bool planRouted;
        private Vector2 planVel;

        private int gatherRelease;
        private int gatherReleaseTotal;
        private float gatherReleaseFrom;

        internal int StageTicks { get { return counter; } }

        internal int GatherRelease { get { return gatherRelease; } }

        internal bool GatherActive { get { return fox.PounceGather > 0f && gatherRelease <= 0; } }

        internal int EndTick { get { return endTick; } }

        internal override void TickAuxCooldowns()
        {
            if (gateCooldown > 0) gateCooldown--;
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            if (gateCooldown > 0)
            {
                string m = null;
                return FoxMoveBid.No("gate_backoff", m);
            }

            Vector2 targetPosition = s.Target;
            string gate = null;

            if (!p.PounceEnabled) gate = "disabled";
            else if (!p.PounceAttackEnabled) gate = "attack_disabled";
            else if (Stage != Fox.PounceState.None) gate = "already_pouncing";
            else if (Cooldown > 0) gate = "cooldown";
            else if (fox.WallBarsLaunch) gate = "wall_hold";
            else if (!fox.Grounded) gate = "airborne";
            else
            {
                float d = Vector2.Distance(fox.mainBodyChunk.pos, targetPosition);
                if (d > p.PounceDistance) gate = "too_far";
                else if (d < p.PounceMinDistance) gate = "too_close";
                else if (fox.LaunchTooSteep(p, fox.LaunchAngleDeg(targetPosition))) gate = "too_steep";
                else if (!s.NoChanceRoll && UnityEngine.Random.value > p.PounceChance) gate = "roll_failed";
            }

            if (gate != null)
            {
                if (gate == "roll_failed")
                {
                    Cooldown = Math.Max(Cooldown, p.PounceRetryDelay);
                }
                else if (gate == "cooldown")
                {
                    gateCooldown = Cooldown;
                }
                else if (gate == "disabled" || gate == "attack_disabled")
                {
                    gateCooldown = Fox.GATE_PAUSE_WHEN_DISABLED;
                }
                else
                {
                    gateCooldown = p.PounceGateRetryDelay;
                }
                string mNo = null;
                return FoxMoveBid.No(gate, mNo);
            }

            string mYes = null;
            return FoxMoveBid.Yes("prey", mYes);
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.AttackStateCeilingTicks) + 1;
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            string reason = string.IsNullOrEmpty(s.Why) ? bid.Reason : s.Why;
            Stage = Fox.PounceState.Crouch;
            isLedge = reason == "ledge_climb";
            counter = 0;
            total = 0;
            pounceDir = Fox.Dir(fox.mainBodyChunk.pos, s.Target);
            targetPos = s.Target;
            fox.SolveLaunchScaling(p, pounceDir, targetPos, out launchFrac, out wantDist, out wantRise);
            gatherFramesUsed = fox.GatherFramesFor(p, launchFrac);
            prepared = s.Prepared;
            if (prepared) gatherFramesUsed = 0;
            LatchRoutedArc();
            gatherRelease = 0;
            fox.PounceGather = 0f;
            fox.PounceFlight = 0f;
            fox.PounceLand = 0f;
            boostLeft = 0;
            if (fox.AI != null) fox.AI.ReportHuntProgress("pounce_started");
        }

        private void LatchRoutedArc()
        {
            planRouted = false;
            planVel = Vector2.zero;

            if (!isLedge || fox.AI == null) return;

            FoxAscentPlan plan;
            try
            {
                plan = fox.AI.AscentPlan;
            }
            catch (Exception)
            {
                return;
            }

            if (plan == null || plan.Kind != FoxAscentKind.Pounce) return;
            if (plan.LaunchVel.sqrMagnitude <= 0.0001f) return;
            if (!plan.IsUsable(fox.AI.AscentPathGeneration, fox.MoveTick)) return;

            planRouted = true;
            planVel = plan.LaunchVel;
            if (plan.LaunchFrom.TileDefined && fox.RoomRef != null)
            {
                fox.RoomRef.MiddleOfTile(plan.LaunchFrom.Tile);
            }
        }

        protected override void OnTick(FoxBreedParams p)
        {
            counter++;
            total++;

            fox.PounceGatherReady = Mathf.Max(0f, fox.PounceGatherReady - 0.15f);

            switch (Stage)
            {
                case Fox.PounceState.Crouch:
                    UpdateGather(p);
                    break;

                case Fox.PounceState.Propel:
                    UpdateFlight(p);
                    break;

                case Fox.PounceState.Recover:
                    fox.runSpeed *= 0.1f;

                    if (p.PounceLandFrames > 0)
                    {
                        fox.PounceLand = Mathf.Max(0f, fox.PounceLand - 1f / p.PounceLandFrames);
                    }
                    else fox.PounceLand = 0f;

                    if (counter >= p.PounceRecoveryStun) End(p, "recovered");
                    break;
            }
        }

        private void UpdateGather(FoxBreedParams p)
        {
            int totalFrames = GatherFramesTotal(p);
            int frames = Math.Max(1, totalFrames);
            float was = fox.PounceGather;
            fox.PounceGather = Mathf.Clamp01((float)counter / Math.Max(1, frames - 1));
            fox.PounceFlight = 0f;

            if (fox.Grounded)
            {
                BodyChunk[] c = fox.Chunks;
                for (int i = 0; i < c.Length; i++) c[i].vel -= pounceDir * p.PounceGatherPull;
            }

            Creature prey = fox.chaseTarget;
            if (!isLedge && prey != null && prey.room == fox.RoomRef)
            {
                pounceDir = Fox.Dir(fox.mainBodyChunk.pos, prey.mainBodyChunk.pos);
                targetPos = prey.mainBodyChunk.pos;
            }

            if (was < 1f && fox.PounceGather >= 1f) fox.PounceGatherReady = 1f;

            if (counter >= totalFrames) Launch(p);
        }

        private void Launch(FoxBreedParams p)
        {
            Vector2 dir = pounceDir;
            BodyChunk[] c = fox.Chunks;

            fox.SolveLaunchScaling(p, dir, targetPos, out launchFrac, out wantDist, out wantRise);
            float speed = fox.EffectiveLaunchSpeed(p) * launchFrac;
            Vector2 v = dir * speed;
            v.y += speed * p.PounceLaunchLift;

            if (planRouted)
            {
                if (p.PounceRouteFlyLaunchVel)
                {
                    v = planVel;
                }
            }

            Vector2 predicted;
            if (p.PounceRoomClamp && fox.RoomRef != null && !fox.PredictedLandingInRoom(p, v, out predicted))
            {
                End(p, "launch_refused");
                return;
            }

            if (p.PounceRequireLanding && fox.RoomRef != null)
            {
                Vector2 landAt;
                IntVector2 landTile;
                string landWhy;
                string landEntry;
                bool landOk = fox.PredictedLandingSurvivable(p, v, out landAt, out landTile,
                                                            out landWhy, out landEntry);
                if (!landOk)
                {
                    End(p, "launch_refused");
                    return;
                }
            }

            if (p.PounceLaunchPop != 0f) c[0].pos.y += p.PounceLaunchPop;

            float[] share = { 1f + p.PounceHeadLead, 1f, 1f - p.PounceRearLag };
            for (int i = 0; i < c.Length; i++) c[i].vel = v * share[Math.Min(i, share.Length - 1)];

            Vector2 perp = new Vector2(-dir.y, dir.x) * (dir.x >= 0f ? 1f : -1f);
            c[0].vel += perp * p.PouncePitchCouple;
            c[2].vel -= perp * p.PouncePitchCouple;

            fox.AssertLaunchTraction();

            boostLeft = Math.Max(0, p.PounceBoostFrames);
            Stage = Fox.PounceState.Propel;
            counter = 0;
            fox.PounceGather = 1f;
            fox.PounceFlight = 1f;
            fox.PounceLand = 0f;

            if (prepared) fox.BreakCamo("bait_pounce");

            fox.voice.Attack();
        }

        private void UpdateFlight(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            fox.PounceGather = Mathf.Max(0f, fox.PounceGather - 0.25f);
            fox.PounceFlight = 1f;

            if (boostLeft > 0 && p.PounceBoostFrames > 0)
            {
                float decay = (float)boostLeft / p.PounceBoostFrames;
                float b = p.PounceBoost * decay;
                for (int i = 0; i < c.Length; i++) c[i].vel.y += b;
                boostLeft--;
            }

            fox.ClearGoThroughFloors();

            fox.AlignBodyToVelocity(p, fox.BodyFlightVelocity(), "pounce");

            fox.TryBite(p);

            if (counter > 2 && fox.Grounded)
            {
                LandPounce(p, "ground");
                return;
            }

            if (counter > p.PounceFlightMaxFrames)
            {
                EnterRecovery();
            }
        }

        private void LandPounce(FoxBreedParams p, string reason)
        {
            BodyChunk[] c = fox.Chunks;
            float comp = p.PounceLandCompress;
            if (comp != 0f)
            {
                c[2].vel.y += comp;
                c[2].pos.y += comp;
                c[0].vel.y -= comp;
                c[0].pos.y -= comp;
            }

            float damp = Mathf.Clamp01(p.PounceLandDamp);
            for (int i = 0; i < c.Length; i++) c[i].vel.x *= damp;

            fox.PounceLand = 1f;
            fox.PounceFlight = 0f;
            ReleaseGather(p, "landed");

            EnterRecovery();
        }

        private void EnterRecovery()
        {
            Stage = Fox.PounceState.Recover;
            counter = 0;
            boostLeft = 0;
            fox.PounceFlight = 0f;
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            if (handover || reason == "knocked_out") return 0;
            return Math.Max(0, p.PounceCooldown);
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            if (!wasRunning) return false;

            bool forced = reason == "overran" || reason == "overran_bite_gate";

            Stage = Fox.PounceState.None;
            counter = 0;
            total = 0;
            boostLeft = 0;
            fox.PounceFlight = 0f;

            if (handover)
            {
                gatherRelease = 0;
                fox.PounceGather = 0f;
                fox.PounceLand = 0f;
                fox.PounceGatherReady = 0f;
                return true;
            }

            endTick = fox.MoveTick;

            if (reason == "knocked_out")
            {
                if (fox.dead) { gatherRelease = 0; fox.PounceGather = 0f; }
                else ReleaseGather(p, "knocked_out");
                fox.PounceGatherReady = 0f;
                fox.RestoreAfterCommittedState(p, "pounce_knocked_out");
                return true;
            }

            if (reason == "launch_refused")
            {
                ReleaseGather(p, "launch_refused");
                return true;
            }

            if (forced)
            {
                fox.PounceLand = 0f;
                fox.PounceGatherReady = 0f;
                ReleaseGather(p, "forced_end");
                fox.RestoreAfterCommittedState(p, "pounce_forced");
                return true;
            }

            ReleaseGather(p, reason);
            fox.RestoreAfterCommittedState(p, "pounce");
            return true;
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }

        private void ReleaseGather(FoxBreedParams p, string reason)
        {
            int ticks = Math.Max(0, p.PounceGatherReleaseTicks);
            if (fox.PounceGather <= 0f || ticks <= 0)
            {
                gatherRelease = 0;
                fox.PounceGather = 0f;
                return;
            }
            gatherReleaseFrom = fox.PounceGather;
            gatherReleaseTotal = ticks;
            gatherRelease = ticks;
        }

        internal void TickGatherRelease(FoxBreedParams p)
        {
            if (gatherRelease <= 0) return;
            gatherRelease--;
            float k = (float)gatherRelease / Math.Max(1, gatherReleaseTotal);
            fox.PounceGather = Mathf.Clamp01(gatherReleaseFrom * (k * k * (3f - 2f * k)));
            if (gatherRelease <= 0)
            {
                fox.PounceGather = 0f;
            }
        }

        internal int GatherFramesTotal(FoxBreedParams p)
        {
            return (gatherFramesUsed >= 0) ? gatherFramesUsed : Math.Max(0, p.PounceGatherFrames);
        }
    }

    internal sealed class FoxChargeAction : FoxMoveAction
    {
        internal FoxChargeAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "charge"; } }
        internal override FoxBodyClaim Claims { get { return FoxBodyClaim.None; } }
        protected override string TimeoutReason { get { return "overran"; } }

        internal Fox.ChargeState Stage { get; private set; }

        private Vector2 chargeDir;
        private int counter;
        private int totalActive;
        private int recoverTicks;
        private int gateCooldown;

        internal Vector2 Dir { get { return chargeDir; } }

        internal override void TickAuxCooldowns()
        {
            if (gateCooldown > 0) gateCooldown--;
        }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            if (!p.ChargeEnabled)
            {
                return FoxMoveBid.No("charge_disabled", "");
            }
            if (gateCooldown > 0)
            {
                string m = null;
                return FoxMoveBid.No("gate_backoff", m);
            }

            BodyChunk[] c = fox.Chunks;
            string gate = null;
            float d = Vector2.Distance(fox.mainBodyChunk.pos, s.Target);
            Vector2 dir = Fox.Dir(fox.mainBodyChunk.pos, s.Target);
            Vector2 facing = (c[0].pos - c[2].pos).normalized;
            float facingDot = Vector2.Dot(facing, dir);

            if (Stage != Fox.ChargeState.None) gate = "already_charging";
            else if (fox.pounce != Fox.PounceState.None) gate = "pouncing";
            else if (Cooldown > 0) gate = "cooldown";
            else if (!fox.Grounded) gate = "airborne";
            else if (d > p.ChargeDistance) gate = "too_far";
            else if (d < p.ChargeMinDistance) gate = "too_close";
            else if (facingDot < p.ChargeFacingDot) gate = "not_facing";
            else if (!fox.ChargeEndsInRoom(p, dir, d)) gate = "leaves_room";
            else if (!ChargeLandingOk(p, dir, d)) gate = "no_landing";
            else if (UnityEngine.Random.value > p.ChargeChance) gate = "roll_failed";

            if (gate != null)
            {
                gateCooldown = (gate == "cooldown") ? Cooldown : Math.Max(1, p.ChargeGateRetryDelay);
                string m = null;
                return FoxMoveBid.No(gate, m);
            }

            chargeDir = dir;
            string mOk = null;
            return FoxMoveBid.Yes("prey", mOk);
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.AttackStateCeilingTicks) + 1;
        }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            Stage = Fox.ChargeState.Windup;
            counter = 0;
            totalActive = 0;
            recoverTicks = 0;
            fox.ChargeWindup = 0f;
            fox.ChargeRush = 0f;
            if (fox.AI != null) fox.AI.ReportHuntProgress("charge_started");
        }

        protected override void OnTick(FoxBreedParams p)
        {
            counter++;

            if (!fox.Consious) { EnterRecovery(p, "unconscious"); return; }
            if (fox.RoomRef == null) { EnterRecovery(p, "no_room"); return; }

            if (Stage != Fox.ChargeState.Recover)
            {
                totalActive++;
                if (totalActive > Math.Max(1, p.ChargeMaxFrames)) { EnterRecovery(p, "max_frames"); return; }
            }

            switch (Stage)
            {
                case Fox.ChargeState.Windup:
                    UpdateWindup(p);
                    break;
                case Fox.ChargeState.Rush:
                    UpdateRush(p);
                    break;
                case Fox.ChargeState.Recover:
                    fox.runSpeed *= 0.1f;
                    fox.ChargeRush = Mathf.Max(0f, fox.ChargeRush - 0.2f);

                    recoverTicks--;

                    if (recoverTicks <= 0) End(p, "recovered");
                    break;
            }
        }

        private void UpdateWindup(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            int frames = Math.Max(1, p.ChargeWindupFrames);
            fox.ChargeWindup = Mathf.Clamp01((float)counter / Math.Max(1, frames - 1));
            fox.ChargeRush = 0f;

            if (fox.Grounded)
            {
                for (int i = 0; i < c.Length; i++) c[i].vel -= chargeDir * p.ChargeWindupPull;
            }

            Creature prey = fox.chaseTarget;
            if (prey != null && prey.room == fox.RoomRef && p.ChargeAimTrack > 0f)
            {
                chargeDir = Vector2.Lerp(chargeDir, Fox.Dir(fox.mainBodyChunk.pos, prey.mainBodyChunk.pos),
                                         Mathf.Clamp01(p.ChargeAimTrack)).normalized;
            }

            if (counter >= frames)
            {
                Stage = Fox.ChargeState.Rush;
                counter = 0;
                fox.ChargeWindup = 1f;
                fox.ChargeRush = 1f;
                fox.voice.Attack();
            }
        }

        private void UpdateRush(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            fox.ChargeRush = 1f;
            fox.ChargeWindup = Mathf.Max(0f, fox.ChargeWindup - 0.15f);

            fox.ClearGoThroughFloors();

            bool thrusting = counter <= Math.Max(0, p.ChargeThrustFrames)
                             && (!p.ChargeRequireGrounded || fox.Grounded);
            if (thrusting)
            {
                for (int k = 0; k < c.Length; k++) c[k].vel += chargeDir * (p.ChargeThrust / (k + 1));
                float cap = Mathf.Max(1f, p.ChargeMaxSpeed);
                for (int k = 0; k < c.Length; k++)
                {
                    float sp = c[k].vel.magnitude;
                    if (sp > cap) c[k].vel *= cap / sp;
                }
            }

            fox.AlignBodyToVelocity(p, chargeDir * Mathf.Max(p.PounceAlignMinSpeed + 1f,
                                                             fox.BodyFlightVelocity().magnitude), "charge");

            fox.TryBite(p);

            if (!fox.InRoomBounds(fox.mainBodyChunk.pos, 0f)) { EnterRecovery(p, "left_room"); return; }

            if (p.ChargeStallSpeed > 0f && counter > Math.Max(0, p.ChargeStallGrace)
                && Vector2.Dot(fox.BodyFlightVelocity(), chargeDir) < p.ChargeStallSpeed)
            {
                EnterRecovery(p, "stalled");
                return;
            }

            if (counter > Math.Max(1, p.ChargeThrustFrames) + 4) EnterRecovery(p, "thrust_spent");
        }

        private void EnterRecovery(FoxBreedParams p, string reason)
        {
            if (Stage == Fox.ChargeState.Recover) { End(p, reason); return; }

            Stage = Fox.ChargeState.Recover;
            counter = 0;
            recoverTicks = Math.Max(1, p.ChargeRecoveryStun);
            fox.ChargeRush = 0f;
            fox.ChargeWindup = 0f;
        }

        protected override int CooldownOnEnd(FoxBreedParams p, string reason, bool handover)
        {
            return handover ? 0 : Math.Max(0, p.ChargeCooldown);
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            if (!wasRunning) return false;

            Stage = Fox.ChargeState.None;
            counter = 0;
            totalActive = 0;
            recoverTicks = 0;
            fox.ChargeWindup = 0f;
            fox.ChargeRush = 0f;

            if (handover) return true;

            if (reason == "knocked_out")
            {
                fox.RestoreAfterCommittedState(p, "charge_knocked_out");
                return true;
            }

            if (reason == "overran" || reason == "overran_bite_gate")
            {
                fox.RestoreAfterCommittedState(p, "charge_forced");
                return true;
            }

            fox.RestoreAfterCommittedState(p, "charge");
            return true;
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }

        private bool ChargeLandingOk(FoxBreedParams p, UnityEngine.Vector2 dir, float dist)
        {
            int sample;
            RWCustom.IntVector2 tile;
            string why;
            bool ok = fox.ChargeLandingSurvivable(p, dir, dist, out sample, out tile, out why);
            return ok;
        }
    }

    internal sealed class FoxLungeAction : FoxMoveAction
    {
        internal FoxLungeAction(Fox fox) : base(fox) { }

        internal override string Name { get { return "lunge"; } }
        internal override FoxBodyClaim Claims { get { return FoxBodyClaim.None; } }

        private Vector2 toPrey;

        internal bool Lunging { get { return Running; } }

        internal override FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s)
        {
            string m = null;
            if (p.LungeSpeed <= 0f)
            {
                return FoxMoveBid.No("disabled", m);
            }
            return FoxMoveBid.Yes("in_band_out_of_contact", m);
        }

        protected override int BudgetFor(FoxBreedParams p, FoxMoveSituation s)
        {
            return Math.Max(1, p.AttackStateCeilingTicks) + 1;
        }

        protected override string TimeoutReason { get { return "overran"; } }

        protected override void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
        }

        internal void Drive(FoxBreedParams p, Creature target, Vector2 dirToPrey, float dist, float reach)
        {
            toPrey = dirToPrey;

            if (!Running)
            {
                FoxMoveBid bid = Ask(p, default(FoxMoveSituation));
                if (!bid.Ok) { End(p, bid.Reason); return; }
                Begin(p, default(FoxMoveSituation), bid);
            }
            fox.Move.Guarded(p, this);
        }

        protected override void OnTick(FoxBreedParams p)
        {
            BodyChunk[] c = fox.Chunks;
            float closing = Vector2.Dot(c[0].vel, toPrey);
            if (closing < p.LungeMaxSpeed)
            {
                c[0].vel += toPrey * (3f * p.LungeSpeed);
            }
            for (int i = 1; i < c.Length; i++)
            {
                c[i].vel -= toPrey * p.LungeSpeed;
            }
        }

        protected override bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover)
        {
            if (!wasRunning) { return false; }
            return true;
        }

        protected override string Detail(FoxBreedParams p)
        {
            return null;
        }
    }
}
