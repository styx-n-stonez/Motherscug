using UnityEngine;

namespace MotherMod
{
    internal enum FoxAscentKind
    {
        None = 0,
        StepUp,
        Pounce,
        Mantle,
    }

    internal sealed class FoxAscentPlan
    {
        public FoxAscentKind Kind;

        public WorldCoordinate Target;

        public WorldCoordinate LaunchFrom;

        public Vector2 AimAt;

        public Vector2 LaunchVel;

        public int Generation;

        public int ExpiresAtTick;

        public int Revalidations;

        public int HeldTicks;

        public bool RoutePriced;

        public float CostSaved;

        public string Why;

        public string PounceRuledOutBy;

        public bool IsUsable(int pathGeneration, int nowTick)
        {
            return Kind != FoxAscentKind.None
                && Generation == pathGeneration
                && nowTick < ExpiresAtTick;
        }

        public string UsabilityToken(int pathGeneration, int nowTick)
        {
            if (Kind == FoxAscentKind.None) return "no_kind";
            if (nowTick >= ExpiresAtTick) return "expired";
            if (Generation != pathGeneration)
                return HeldTicks > 0 ? "held_awaiting_flood" : "stale_generation";
            return "usable";
        }

        public string Describe()
        {
            return null;
        }
    }
}
