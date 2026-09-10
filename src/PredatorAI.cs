namespace MotherMod
{
    public static class PredatorAI
    {
        public static void Register()
        {
            On.ArtificialIntelligence.DynamicRelationship_CreatureRepresentation_AbstractCreature
                += DynamicRelationship;
        }

        private static CreatureTemplate.Relationship DynamicRelationship(
            On.ArtificialIntelligence.orig_DynamicRelationship_CreatureRepresentation_AbstractCreature orig,
            ArtificialIntelligence self, Tracker.CreatureRepresentation rep, AbstractCreature absCrit)
        {
            var rel = orig(self, rep, absCrit);

            AbstractCreature target = absCrit ?? rep?.representedCreature;
            if (target?.realizedCreature is Player player && StaminaSystem.TryGet(player, out var d))
            {
                if (d.exhausted)
                {
                }
            }

            return rel;
        }
    }
}
