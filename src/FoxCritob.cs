using System.Collections.Generic;
using Fisobs.Core;
using Fisobs.Creatures;
using Fisobs.Sandbox;
using UnityEngine;
using CreatureType = CreatureTemplate.Type;

namespace MotherMod
{
    sealed class FoxCritob : Critob
    {
        public static readonly CreatureType MotherFoxTofu = new CreatureType("MotherFoxTofu", true);

        public static readonly CreatureType MotherFoxMilk = new CreatureType("MotherFoxMilk", true);

        public static readonly CreatureType MotherFoxPopcorn = new CreatureType("MotherFoxPopcorn", true);

        public static readonly CreatureType MotherFoxCorn = new CreatureType("MotherFoxCorn", true);

        public static readonly CreatureType MotherFoxCherry = new CreatureType("MotherFoxCherry", true);

        public static readonly CreatureType MotherFoxMushroom = new CreatureType("MotherFoxMushroom", true);

        public static readonly CreatureType MotherFoxPotato = new CreatureType("MotherFoxPotato", true);

        public static readonly CreatureCommunities.CommunityID FoxCommunity =
            new CreatureCommunities.CommunityID("MotherFoxes", true);

        public static Critob[] AllVariants()
        {
            return new Critob[]
            {
                new FoxCritob(MotherFoxTofu, variantId: "tofu", creatureName: "Tofu Fox"),

                new FoxCritob(MotherFoxMilk, variantId: "milk", creatureName: "Milk Fox"),

                new FoxCritob(MotherFoxPopcorn,  variantId: "popcorn",  creatureName: "Popcorn Fox"),
                new FoxCritob(MotherFoxCorn,     variantId: "corn",     creatureName: "Corn Fox"),
                new FoxCritob(MotherFoxCherry,   variantId: "cherry",   creatureName: "Cherry Fox"),
                new FoxCritob(MotherFoxMushroom, variantId: "mushroom", creatureName: "Mushroom Fox"),
                new FoxCritob(MotherFoxPotato,   variantId: "potato",   creatureName: "Potato Fox"),
            };
        }

        private static readonly Dictionary<CreatureType, string> FoxTypes = new Dictionary<CreatureType, string>();

        public static bool IsFoxType(CreatureType type)
        {
            return type != null && FoxTypes.ContainsKey(type);
        }

        public static string VariantIdFor(CreatureType type)
        {
            if (type != null && FoxTypes.TryGetValue(type, out string id)) return id;
            return type == null ? "unknown" : type.value;
        }

        public static IEnumerable<CreatureType> AllFoxTypes()
        {
            return FoxTypes.Keys;
        }

        public readonly string VariantId;

        private FoxCritob(CreatureType type, string variantId, string creatureName) : base(type)
        {
            VariantId = variantId;
            CreatureName = creatureName;
            FoxTypes[type] = variantId;

            ShelterDanger = ShelterDanger.Hostile;

            RegisterUnlock(KillScore.Configurable(4),
                           new MultiplayerUnlocks.SandboxUnlockID(type.value, true),
                           parent: MultiplayerUnlocks.SandboxUnlockID.Slugcat);
        }

        public FoxBreedParams Params => FoxParams.For(VariantId);

        public override void LoadResources(RainWorld rainWorld)
        {
            string iconName = Ext.IconAtlasName(Type.value);
            Icon = new SimpleIcon(iconName, Params.IconColor);
        }

        public override CreatureTemplate CreateTemplate()
        {
            FoxBreedParams p = Params;

            CreatureTemplate t = new CreatureFormula(CreatureType.LizardTemplate, this)
            {
                HasAI = true,
                DefaultRelationship = new CreatureTemplate.Relationship(
                    CreatureTemplate.Relationship.Type.Ignores, 0f),

                Pathing = PreBakedPathing.Ancestral(CreatureType.GreenLizard),

                TileResistances = BuildTileResist(p),
                ConnectionResistances = BuildConnectionResist(p),

                DamageResistances = new AttackResist { Base = p.Toughness * 2f, Bite = p.BiteResistance },
                StunResistances = new AttackResist { Base = p.StunToughness, Bite = p.BiteStunResistance },
            }.IntoTemplate();

            t.ancestor = null;

            t.virtualCreature = false;

            t.offScreenSpeed = p.OffScreenSpeed;
            t.abstractedLaziness = p.AbstractedLaziness;
            t.roamInRoomChance = p.RoamInRoomChance;
            t.roamBetweenRoomsChance = p.RoamBetweenRoomsChance;
            t.bodySize = p.BodySize;
            t.scaryness = p.Scaryness;
            t.meatPoints = p.MeatPoints;
            t.dangerousToPlayer = p.DangerousToPlayer;
            t.grasps = p.Grasps;
            t.shortcutSegments = p.ShortcutSegments;
            t.stowFoodInDen = p.StowFoodInDen;
            t.visualRadius = p.VisualRadius;
            t.waterVision = p.WaterVision;
            t.throughSurfaceVision = p.ThroughSurfaceVision;
            t.movementBasedVision = p.MovementBasedVision;
            t.canSwim = p.CanSwim;
            t.waterPathingResistance = p.WaterPathingResistance;
            t.waterRelationship = ParseWaterRelationship(p.WaterRelationship);
            t.canFly = false;
            t.quickDeath = true;
            t.saveCreature = true;
            t.smallCreature = false;

            t.SetNodeType(AbstractRoomNode.Type.Exit, true);
            t.SetNodeType(AbstractRoomNode.Type.Den, true);

            t.communityID = FoxCommunity;

            t.communityInfluence = p.RepCommunityInfluence;

            LoadedPerformanceCost = p.LoadedPerformanceCost;

            CheckPoleConfig(t, p);
            LogTemplate(t, p);
            return t;
        }

        private void CheckPoleConfig(CreatureTemplate t, FoxBreedParams p)
        {
            bool wallAllowed = t.AccessibilityResistance(AItile.Accessibility.Wall).Allowed;

            FoxWallAccess.Register(t.type, VariantId, wallAllowed);
        }

        private void LogTemplate(CreatureTemplate t, FoxBreedParams p)
        {
            var nodes = new List<string>();
            for (int i = 0; i < t.mappedNodeTypes.Length; i++)
            {
                if (t.mappedNodeTypes[i]) nodes.Add(i.ToString());
            }

            var tiles = new List<string>();
            foreach (AItile.Accessibility acc in System.Enum.GetValues(typeof(AItile.Accessibility)))
            {
                PathCost c = t.AccessibilityResistance(acc);
                tiles.Add($"{acc}={c.resistance}/{c.legality}");
            }
        }

        private static TileResist BuildTileResist(FoxBreedParams p)
        {
            var r = new TileResist();
            foreach (var kv in p.Tiles)
            {
                switch (kv.Key)
                {
                    case AItile.Accessibility.OffScreen: r.OffScreen = kv.Value; break;
                    case AItile.Accessibility.Floor:     r.Floor = kv.Value; break;
                    case AItile.Accessibility.Corridor:  r.Corridor = kv.Value; break;
                    case AItile.Accessibility.Climb:     r.Climb = kv.Value; break;
                    case AItile.Accessibility.Wall:      r.Wall = kv.Value; break;
                    case AItile.Accessibility.Ceiling:   r.Ceiling = kv.Value; break;
                    case AItile.Accessibility.Air:       r.Air = kv.Value; break;
                    case AItile.Accessibility.Solid:     r.Solid = kv.Value; break;
                    default:
                        break;
                }
            }
            return r;
        }

        private static ConnectionResist BuildConnectionResist(FoxBreedParams p)
        {
            var r = new ConnectionResist();
            foreach (var kv in p.Connections)
            {
                switch (kv.Key)
                {
                    case MovementConnection.MovementType.Standard:          r.Standard = kv.Value; break;
                    case MovementConnection.MovementType.ReachOverGap:      r.ReachOverGap = kv.Value; break;
                    case MovementConnection.MovementType.ReachUp:           r.ReachUp = kv.Value; break;
                    case MovementConnection.MovementType.DoubleReachUp:     r.DoubleReachUp = kv.Value; break;
                    case MovementConnection.MovementType.ReachDown:         r.ReachDown = kv.Value; break;
                    case MovementConnection.MovementType.SemiDiagonalReach: r.SemiDiagonalReach = kv.Value; break;
                    case MovementConnection.MovementType.DropToFloor:       r.DropToFloor = kv.Value; break;
                    case MovementConnection.MovementType.DropToClimb:       r.DropToClimb = kv.Value; break;
                    case MovementConnection.MovementType.DropToWater:       r.DropToWater = kv.Value; break;
                    case MovementConnection.MovementType.LizardTurn:        r.LizardTurn = kv.Value; break;
                    case MovementConnection.MovementType.OpenDiagonal:      r.OpenDiagonal = kv.Value; break;
                    case MovementConnection.MovementType.Slope:             r.Slope = kv.Value; break;
                    case MovementConnection.MovementType.CeilingSlope:      r.CeilingSlope = kv.Value; break;
                    case MovementConnection.MovementType.ShortCut:          r.ShortCut = kv.Value; break;
                    case MovementConnection.MovementType.NPCTransportation: r.NPCTransportation = kv.Value; break;
                    case MovementConnection.MovementType.BetweenRooms:      r.BetweenRooms = kv.Value; break;
                    case MovementConnection.MovementType.OffScreenMovement: r.OffScreenMovement = kv.Value; break;
                    case MovementConnection.MovementType.BigCreatureShortCutSqueeze: r.BigCreatureShortCutSqueeze = kv.Value; break;
                    case MovementConnection.MovementType.OffScreenUnallowed: r.OffScreenUnallowed = kv.Value; break;
                    case MovementConnection.MovementType.OutsideRoom:       r.OutsideRoom = kv.Value; break;
                    case MovementConnection.MovementType.SideHighway:       r.SideHighway = kv.Value; break;
                    case MovementConnection.MovementType.SkyHighway:        r.SkyHighway = kv.Value; break;
                    case MovementConnection.MovementType.SeaHighway:        r.SeaHighway = kv.Value; break;
                    case MovementConnection.MovementType.RegionTransportation: r.RegionTransportation = kv.Value; break;
                    default:
                        break;
                }
            }
            return r;
        }

        private CreatureTemplate.WaterRelationship ParseWaterRelationship(string name)
        {
            switch (name)
            {
                case "AirOnly": return CreatureTemplate.WaterRelationship.AirOnly;
                case "AirAndSurface": return CreatureTemplate.WaterRelationship.AirAndSurface;
                case "Amphibious": return CreatureTemplate.WaterRelationship.Amphibious;
                case "WaterOnly": return CreatureTemplate.WaterRelationship.WaterOnly;
                default:
                    return CreatureTemplate.WaterRelationship.AirAndSurface;
            }
        }

        public override void EstablishRelationships()
        {
            FoxBreedParams p = Params;
            Relationships self = new Relationships(Type);

            foreach (var template in StaticWorld.creatureTemplates)
            {
                if (template.quantified)
                {
                    self.Ignores(template.type);
                    self.IgnoredBy(template.type);
                }
            }

            List<FoxRelationship> ordered = new List<FoxRelationship>(p.Relationships);
            ordered.Sort((a, b) => AncestorDepth(a.Other).CompareTo(AncestorDepth(b.Other)));

            int applied = 0, skipped = 0;
            var declared = new List<KeyValuePair<CreatureType, FoxRelationship>>();

            foreach (FoxRelationship r in ordered)
            {
                if (!TryParseCreatureType(r.Other, out CreatureType other))
                {
                    skipped++;
                    continue;
                }

                bool ok = ApplyOneDirection(Type, other, r.Type, r.Intensity, r.Other, "forward");
                ok |= ApplyOneDirection(other, Type, r.ReverseType, r.ReverseIntensity, r.Other, "reverse");
                if (ok)
                {
                    applied++;
                    declared.Add(new KeyValuePair<CreatureType, FoxRelationship>(other, r));
                }
                else skipped++;
            }

            int mismatches = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                mismatches = 0;
                for (int k = 0; k < declared.Count; k++)
                {
                    CreatureType other = declared[k].Key;
                    FoxRelationship r = declared[k].Value;
                    if (!ReadBackMatches(Type, other, r.Type, r.Intensity))
                    {
                        mismatches++;
                        if (pass == 0) ApplyOneDirection(Type, other, r.Type, r.Intensity, r.Other, "repair");
                    }
                    if (!ReadBackMatches(other, Type, r.ReverseType, r.ReverseIntensity))
                    {
                        mismatches++;
                        if (pass == 0) ApplyOneDirection(other, Type, r.ReverseType, r.ReverseIntensity, r.Other, "repair_reverse");
                    }
                }
                if (mismatches == 0) break;
            }

            int routePrey = 0, routeThreat = 0, routeDiscomfort = 0, routeInert = 0;
            for (int k = 0; k < declared.Count; k++)
            {
                CreatureType other = declared[k].Key;
                FoxRelationship r = declared[k].Value;
                var fwd = ReadBack(Type, other);
                string route = RouteName(fwd.type, p);
                if (route == "prey") routePrey++;
                else if (route == "threat") routeThreat++;
                else if (route == "discomfort") routeDiscomfort++;
                else if (fwd.type != CreatureTemplate.Relationship.Type.Ignores) routeInert++;

                if (r.Type == "Ignores" && r.ReverseType == null) continue;
                ReadBack(other, Type);
            }

            foreach (CreatureType foxType in AllFoxTypes())
            {
                bool mentioned = false;
                for (int k = 0; k < declared.Count; k++)
                {
                    if (declared[k].Key == foxType) { mentioned = true; break; }
                }
                if (mentioned) continue;
                StaticWorld.EstablishRelationship(Type, foxType,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f));
                StaticWorld.EstablishRelationship(foxType, Type,
                    new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f));
            }
        }

        private static string RouteName(CreatureTemplate.Relationship.Type type, FoxBreedParams p)
        {
            switch (FoxAI.RouteFor(type, p))
            {
                case FoxAI.RelRoute.Prey: return "prey";
                case FoxAI.RelRoute.Threat: return "threat";
                case FoxAI.RelRoute.Discomfort: return "discomfort";
                default: return "inert";
            }
        }

        private static int AncestorDepth(string name)
        {
            if (!TryParseCreatureType(name, out CreatureType type)) return 0;
            CreatureTemplate t = StaticWorld.GetCreatureTemplate(type);
            int depth = 0;
            while (t != null && t.ancestor != null && depth < 16)
            {
                t = t.ancestor;
                depth++;
            }
            return depth;
        }

        private static CreatureTemplate.Relationship ReadBack(CreatureType from, CreatureType to)
        {
            CreatureTemplate t = StaticWorld.GetCreatureTemplate(from);
            if (t == null || t.relationships == null || to == null || to.Index < 0
                || to.Index >= t.relationships.Length)
            {
                return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Ignores, 0f);
            }
            return t.relationships[to.Index];
        }

        private static bool ReadBackMatches(CreatureType from, CreatureType to, string typeName, float intensity)
        {
            if (string.IsNullOrEmpty(typeName)) return true;
            if (!TryParseRelationshipType(typeName, out var want)) return true;
            var have = ReadBack(from, to);
            return have.type == want && Mathf.Abs(have.intensity - intensity) < 0.001f;
        }

        private bool ApplyOneDirection(CreatureType from, CreatureType to, string typeName,
                                       float intensity, string otherName, string direction)
        {
            if (string.IsNullOrEmpty(typeName)) return true;

            if (!TryParseRelationshipType(typeName, out var relType))
            {
                return false;
            }

            StaticWorld.EstablishRelationship(from, to, new CreatureTemplate.Relationship(relType, intensity));
            return true;
        }

        private static bool TryParseCreatureType(string name, out CreatureType type)
        {
            type = null;
            if (string.IsNullOrEmpty(name)) return false;
            if (!ExtEnumBase.TryParse(typeof(CreatureType), name, true, out ExtEnumBase parsed)) return false;
            type = (CreatureType)parsed;
            return type.Index != -1;
        }

        private static bool TryParseRelationshipType(string name, out CreatureTemplate.Relationship.Type type)
        {
            type = null;
            if (string.IsNullOrEmpty(name)) return false;
            if (!ExtEnumBase.TryParse(typeof(CreatureTemplate.Relationship.Type), name, true, out ExtEnumBase parsed)) return false;
            type = (CreatureTemplate.Relationship.Type)parsed;
            return type.Index != -1;
        }

        public override CreatureState CreateState(AbstractCreature acrit)
        {
            return new FoxState(acrit);
        }

        public override Creature CreateRealizedCreature(AbstractCreature acrit)
        {
            return new Fox(acrit, VariantId);
        }

        public override ArtificialIntelligence CreateRealizedAI(AbstractCreature acrit)
        {
            if (acrit.realizedCreature is Fox fox) return new FoxAI(acrit, fox);

            return new InertAI(acrit);
        }

        public override IEnumerable<string> WorldFileAliases()
        {
            yield return CreatureName.Replace(" ", "").ToLowerInvariant();
            yield return Type.value.ToLowerInvariant();
        }

        public override IEnumerable<DevInterface.RoomAttractivenessPanel.Category> DevtoolsRoomAttraction()
        {
            foreach (string name in Params.RoomAttraction)
            {
                var cat = new DevInterface.RoomAttractivenessPanel.Category(name);
                if (cat.Index < 0)
                {
                    continue;
                }
                yield return cat;
            }
        }

        public override string DevtoolsMapName(AbstractCreature acrit)
        {
            return VariantId;
        }

        public override Color DevtoolsMapColor(AbstractCreature acrit)
        {
            return Params.ColorTail;
        }
    }

    sealed class InertAI : ArtificialIntelligence
    {
        public InertAI(AbstractCreature acrit) : base(acrit, acrit.world) { }
    }
}
