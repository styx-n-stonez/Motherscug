using System;
using UnityEngine;

namespace MotherMod
{
    [Flags]
    internal enum FoxBodyClaim
    {
        None = 0,
        Gravity = 1,
        SpineElasticity = 2,
        SpineLinks = 4,
        TerrainSqueeze = 8,
        Drive = 16,
        ThroughFloors = 32,
    }

    internal struct FoxMoveBid
    {
        public bool Ok;
        public string Reason;
        public string Measure;

        internal static FoxMoveBid Yes(string why, string measure)
        {
            return new FoxMoveBid { Ok = true, Reason = why ?? "ok", Measure = measure ?? "" };
        }

        internal static FoxMoveBid No(string why, string measure)
        {
            return new FoxMoveBid { Ok = false, Reason = why ?? "no", Measure = measure ?? "" };
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Measure) ? Reason : Reason + "(" + Measure + ")";
        }
    }

    internal struct FoxMoveSituation
    {
        public MovementConnection Conn;
        public Vector2 ToTarget;
        public Vector2 Desired;
        public Vector2 Target;
        public string Why;
        public bool Prepared;
        public bool NoChanceRoll;
    }

    internal abstract class FoxMoveAction
    {
        protected readonly Fox fox;

        protected FoxMoveAction(Fox fox) { this.fox = fox; }

        internal abstract string Name { get; }

        internal virtual FoxBodyClaim Claims { get { return FoxBodyClaim.None; } }

        internal virtual bool Persistent { get { return true; } }

        internal virtual bool CooldownTicksLate { get { return false; } }

        internal virtual bool CooldownTickedExternally { get { return false; } }

        internal int Ticks;
        internal int Budget;
        internal int Cooldown;

        internal virtual void TickAuxCooldowns() { }
        internal string LastEndReason = "none";

        internal bool Running { get { return Ticks > 0; } }

        internal int LiveTicks { get { return Math.Max(0, Ticks - 1); } }

        private int lastHeartbeatTick = -1000000;

        internal abstract FoxMoveBid Ask(FoxBreedParams p, FoxMoveSituation s);

        internal bool Begin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid)
        {
            if (Running) return true;

            Ticks = 1;
            Budget = Math.Max(1, BudgetFor(p, s));
            lastHeartbeatTick = fox.MoveTick;
            OnBegin(p, s, bid);

            if (!Persistent) End(p, "applied");
            return true;
        }

        internal void Tick(FoxBreedParams p)
        {
            if (!Running)
            {
                if (CooldownTicksLate && !CooldownTickedExternally && Cooldown > 0) Cooldown--;
                if (LeakedClaim(p)) End(p, OrphanReason);
                return;
            }

            string abort = ShouldAbort(p);
            if (abort != null) { End(p, abort); return; }

            string done = IsComplete(p);
            if (done != null) { End(p, done); return; }

            if (Ticks >= Budget) { End(p, TimeoutReason); return; }
            Ticks++;

            OnTick(p);

            if (!Running) return;

            int every = Math.Max(1, HeartbeatTicks(p));
            if (fox.MoveTick - lastHeartbeatTick >= every)
            {
                lastHeartbeatTick = fox.MoveTick;
            }
        }

        internal void End(FoxBreedParams p, string reason, bool handover = false)
        {
            bool wasRunning = Ticks > 0;
            int had = Ticks;

            Ticks = 0;
            Budget = 0;

            bool restored = OnEnd(p, reason, had, wasRunning, handover);

            if (wasRunning)
            {
                int cd = CooldownOnEnd(p, reason, handover);
                if (cd > 0) Cooldown = Math.Max(Cooldown, cd);
            }

            if (!handover) fox.Move.AssertStandingBody(p, Name + "_end", false);

            if (!wasRunning && !restored) return;
            LastEndReason = reason;
        }

        protected virtual int BudgetFor(FoxBreedParams p, FoxMoveSituation s) { return 1; }

        protected virtual string TimeoutReason { get { return "timeout"; } }

        protected abstract void OnBegin(FoxBreedParams p, FoxMoveSituation s, FoxMoveBid bid);

        protected virtual void OnTick(FoxBreedParams p) { }

        protected virtual string ShouldAbort(FoxBreedParams p) { return null; }

        protected virtual string IsComplete(FoxBreedParams p) { return null; }

        protected virtual bool OnEnd(FoxBreedParams p, string reason, int ran, bool wasRunning, bool handover) { return false; }

        protected virtual bool LeakedClaim(FoxBreedParams p) { return false; }

        protected virtual string OrphanReason { get { return "orphaned_claim"; } }

        protected virtual int CooldownOnEnd(FoxBreedParams p, string reason, bool handover) { return 0; }

        protected virtual string Detail(FoxBreedParams p) { return ""; }

        protected virtual int HeartbeatTicks(FoxBreedParams p) { return p.MoveActionLogTicks; }
    }
}
