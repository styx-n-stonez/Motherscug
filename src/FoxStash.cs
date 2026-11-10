using System;

namespace MotherMod
{
    internal static class FoxStash
    {
        public static void Register()
        {
            On.AbstractCreature.IsEnteringDen += AbstractCreature_IsEnteringDen;
        }

        private static void AbstractCreature_IsEnteringDen(On.AbstractCreature.orig_IsEnteringDen orig,
                                                           AbstractCreature self, WorldCoordinate den)
        {
            if (!FoxCritob.IsFoxType(self.creatureTemplate.type))
            {
                orig(self, den);
                return;
            }

            AbstractCreature carried = null;
            try
            {
                for (int i = 0; i < self.stuckObjects.Count; i++)
                {
                    var stick = self.stuckObjects[i] as AbstractPhysicalObject.CreatureGripStick;
                    if (stick != null && stick.A == self && stick.B is AbstractCreature prey)
                    {
                        carried = prey;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                FoxFault.Warn($"stash_scan_failed id={self.ID} err={e.GetType().Name} msg=\"{e.Message}\"");
            }

            orig(self, den);

            if (carried == null) return;

            try
            {
                NoteStash(self, den, carried);
            }
            catch (Exception e)
            {
                FoxFault.Warn($"stash_credit_failed id={self.ID} err={e.GetType().Name} " +
                            $"msg=\"{e.Message}\" action=stash_not_noted");
            }
        }

        private static void NoteStash(AbstractCreature self, WorldCoordinate den, AbstractCreature carried)
        {
            string variantId = FoxCritob.VariantIdFor(self.creatureTemplate.type);
            FoxBreedParams p = FoxParams.For(variantId);

            (self.abstractAI?.RealAI as FoxAI)?.Fed(p.DenRestFrames);
        }
    }
}
