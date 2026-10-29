using System;
using System.Text;
using UnityEngine;

namespace MotherMod
{
    internal enum FoxMoveHandler
    {
        None,
        Walk,
        Reach,
        Shortcut,
        Turn,
        AbstractOnly,
    }

    internal sealed class FoxMoveEngine
    {
        private readonly Fox fox;

        internal readonly FoxReachAction Reach;
        internal readonly FoxStepUpAction StepUp;
        internal readonly FoxLedgePounceAction LedgePounce;
        internal readonly FoxMantleAction Mantle;
        internal readonly FoxTurnAction Turn;
        internal readonly FoxPounceAction Pounce;
        internal readonly FoxChargeAction Charge;
        internal readonly FoxLungeAction Lunge;

        internal readonly FoxEnterAction Enter;

        private readonly FoxMoveAction[] ticked;

        private readonly FoxMoveAction[] all;

        private int lastAssertLogTick = -1000000;

        internal FoxMoveEngine(Fox fox)
        {
            this.fox = fox;
            Reach = new FoxReachAction(fox);
            StepUp = new FoxStepUpAction(fox);
            LedgePounce = new FoxLedgePounceAction(fox);
            Mantle = new FoxMantleAction(fox);
            Turn = new FoxTurnAction(fox);
            Pounce = new FoxPounceAction(fox);
            Charge = new FoxChargeAction(fox);
            Lunge = new FoxLungeAction(fox);
            Enter = new FoxEnterAction(fox);
            ticked = new FoxMoveAction[] { Mantle, Reach, StepUp, LedgePounce, Enter };
            all = new FoxMoveAction[] { Turn, Mantle, Reach, StepUp, LedgePounce, Charge, Pounce, Lunge, Enter };
        }

        internal FoxMoveAction DriveOwner
        {
            get
            {
                for (int i = 0; i < ticked.Length; i++)
                {
                    if (ticked[i].Running && (ticked[i].Claims & FoxBodyClaim.Drive) != 0) return ticked[i];
                }
                return null;
            }
        }

        internal bool AnyRunning
        {
            get
            {
                for (int i = 0; i < all.Length; i++) if (all[i].Running) return true;
                return false;
            }
        }

        internal string RunningActionName
        {
            get
            {
                for (int i = 0; i < all.Length; i++) if (all[i].Running) return all[i].Name;
                return null;
            }
        }

        internal string RunningActionNameExcept(FoxMoveAction skip)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (ReferenceEquals(all[i], skip)) continue;
                if (all[i].Running) return all[i].Name;
            }
            return null;
        }

        internal void TickCooldownsEarly()
        {
            for (int i = 0; i < all.Length; i++)
            {
                all[i].TickAuxCooldowns();
                if (all[i].CooldownTicksLate || all[i].CooldownTickedExternally) continue;
                if (all[i].Cooldown > 0) all[i].Cooldown--;
            }
        }

        internal void TickActions(FoxBreedParams p)
        {
            if (fox.EngineOwnsBody)
            {
                StandDownActions(p, fox.dead ? "died" : "engine_owns");
                return;
            }

            for (int i = 0; i < ticked.Length; i++) Guarded(p, ticked[i]);
        }

        internal void Guarded(FoxBreedParams p, FoxMoveAction a)
        {
            try
            {
                a.Tick(p);
            }
            catch (Exception e)
            {
                FoxFault.Warn($"fox_move_action variant={fox.VariantId} id={fox.abstractCreature.ID} " +
                            $"action={a.Name} event=exception reason={e.GetType().Name} " +
                            $"msg={e.Message} tick={fox.MoveTick}");
                try { a.End(p, "exception"); }
                catch (Exception) { }
                AssertStandingBody(p, "exception", true);
            }
        }

        internal string StandDownActions(FoxBreedParams p, string reason)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < all.Length; i++)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(all[i].Name).Append("_ticks=").Append(all[i].Ticks);
                all[i].End(p, reason, true);
            }
            return sb.ToString();
        }

        private FoxBodyClaim StandingClaims(FoxBreedParams p)
        {
            FoxBodyClaim c = FoxBodyClaim.None;
            bool live = !fox.EngineOwnsBody;
            if (p == null) return c;

            if (live && p.SqueezeEnabled) c |= FoxBodyClaim.TerrainSqueeze;
            if (live && p.SqueezeEnabled && p.SqueezeSpinePull) c |= FoxBodyClaim.SpineLinks;
            if (live && fox.SpineRecovering) c |= FoxBodyClaim.SpineElasticity;
            if (live && fox.Consious && fox.ClimbOwnsGravity) c |= FoxBodyClaim.Gravity;
            if (live && fox.PoleOwnsGravity) c |= FoxBodyClaim.Gravity;
            if (live && fox.WallOwnsGravity) c |= FoxBodyClaim.Gravity;
            return c;
        }

        private FoxBodyClaim AllClaims(FoxBreedParams p)
        {
            FoxBodyClaim c = StandingClaims(p);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].Running) c |= all[i].Claims;
            }
            return c;
        }

        internal void AssertStandingBody(FoxBreedParams p, string tag, bool hard)
        {
            if (p == null) return;
            FoxBodyClaim claims = AllClaims(p);
            string violated = null;

            if ((claims & FoxBodyClaim.SpineElasticity) == 0)
            {
                if (hard)
                {
                    fox.RestoreCommittedStateBody(p, tag);
                }
                else if (fox.SpineRelaxedNow)
                {
                    violated = Append(violated, "spine_relaxed_unclaimed");
                }
            }

            if ((claims & (FoxBodyClaim.TerrainSqueeze | FoxBodyClaim.SpineLinks)) == 0)
            {
                if (fox.BodyDeflated) violated = Append(violated, "deflated_unclaimed");
                fox.AssertAsBuiltBodyNow();
            }

            if ((claims & FoxBodyClaim.Gravity) == 0 && !fox.applyGravity)
            {
                violated = Append(violated, "weightless_unclaimed");
                fox.SetWeightApplied(p, true, "assert_" + tag);
            }

            if ((claims & FoxBodyClaim.ThroughFloors) == 0 && fox.ThroughFloorsOwned)
            {
                violated = Append(violated, "through_floors_unclaimed");
                fox.SetGoThroughFloors(false, "assert_" + tag);
            }

            if (violated != null && fox.MoveTick - lastAssertLogTick >= Math.Max(1, p.MoveActionLogTicks))
            {
                lastAssertLogTick = fox.MoveTick;
            }
        }

        private static string Append(string s, string token)
        {
            return string.IsNullOrEmpty(s) ? token : s + "," + token;
        }

        internal bool SelectClimb(FoxBreedParams p, MovementConnection conn, Vector2 toTarget, Vector2 desired)
        {
            var s = new FoxMoveSituation { Conn = conn, ToTarget = toTarget, Desired = desired };

            if (Mantle.Running) return true;

            if (LedgePounce.ConsiderApproach(p, s))
            {
                return true;
            }

            FoxMoveBid mantle = Mantle.Ask(p, s);
            if (mantle.Ok)
            {
                Mantle.Begin(p, s, mantle);
                return true;
            }

            FoxMoveBid leap = LedgePounce.Ask(p, s);
            if (leap.Ok)
            {
                LedgePounce.Begin(p, s, leap);
                return true;
            }

            if (p.StepUpRampConnWalks
                && (conn.type == MovementConnection.MovementType.Slope
                    || conn.type == MovementConnection.MovementType.CeilingSlope))
            {
                return false;
            }

            bool fromConn = conn.destinationCoord.y > conn.startCoord.y;
            bool fromTarget = toTarget.y > p.StepUpHeightThreshold;
            if (!fromConn && !fromTarget)
            {
                return false;
            }

            FoxMoveBid hop = StepUp.Ask(p, s);
            if (hop.Ok)
            {
                StepUp.Begin(p, s, hop);
                return true;
            }

            return false;
        }

        internal bool SelectEnter(FoxBreedParams p)
        {
            if (Enter.Running) return true;
            var s = new FoxMoveSituation { Why = "enter" };
            FoxMoveBid bid = Enter.Ask(p, s);
            if (!bid.Ok) return false;
            Enter.Begin(p, s, bid);
            return true;
        }

        internal static FoxMoveHandler HandlerFor(MovementConnection.MovementType t)
        {
            switch (t)
            {
                case MovementConnection.MovementType.ReachUp:
                case MovementConnection.MovementType.DoubleReachUp:
                case MovementConnection.MovementType.ReachOverGap:
                case MovementConnection.MovementType.SemiDiagonalReach:
                    return FoxMoveHandler.Reach;

                case MovementConnection.MovementType.ShortCut:
                case MovementConnection.MovementType.NPCTransportation:
                    return FoxMoveHandler.Shortcut;

                case MovementConnection.MovementType.BigCreatureShortCutSqueeze:
                    return FoxMoveHandler.None;

                case MovementConnection.MovementType.LizardTurn:
                    return FoxMoveHandler.Turn;

                case MovementConnection.MovementType.Standard:
                case MovementConnection.MovementType.OpenDiagonal:
                case MovementConnection.MovementType.Slope:
                case MovementConnection.MovementType.CeilingSlope:
                case MovementConnection.MovementType.ReachDown:
                case MovementConnection.MovementType.DropToFloor:
                case MovementConnection.MovementType.DropToClimb:
                case MovementConnection.MovementType.DropToWater:
                    return FoxMoveHandler.Walk;

                case MovementConnection.MovementType.BetweenRooms:
                case MovementConnection.MovementType.OffScreenMovement:
                case MovementConnection.MovementType.OffScreenUnallowed:
                case MovementConnection.MovementType.OutsideRoom:
                case MovementConnection.MovementType.SideHighway:
                case MovementConnection.MovementType.SkyHighway:
                case MovementConnection.MovementType.SeaHighway:
                case MovementConnection.MovementType.RegionTransportation:
                    return FoxMoveHandler.AbstractOnly;

                default:
                    return FoxMoveHandler.None;
            }
        }
    }
}
