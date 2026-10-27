using System;
using UnityEngine;

namespace MotherMod
{
    public sealed class FoxPheromone : AIModule
    {
        private readonly FoxAI ai;
        private readonly Fox fox;

        public int Communicating { get; private set; }
        public float Flicker { get; private set; }

        private bool wasEnabled;
        private int lastHeartbeatTick = -100000;
        private bool flaring;
        private string warnedResponse, warnedShare;

        private const float TilePx = 20f;

        public FoxPheromone(FoxAI ai) : base(ai)
        {
            this.ai = ai;
            this.fox = ai.fox;
        }

        public override void Update()
        {
            FoxBreedParams p = fox.P;
            if (p != null) Tick(p);
        }

        private int Clock
        {
            get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; }
        }

        private void Tick(FoxBreedParams p)
        {
            if (!p.PheromoneEnabled)
            {
                if (wasEnabled)
                {
                    wasEnabled = false;
                    Communicating = 0;
                    Flicker = 0f;
                    flaring = false;
                }
                return;
            }
            wasEnabled = true;
            ValidateKeys(p);

            TickSignal(p);

            Room room = fox.room;
            if (room != null && ai.tracker != null && fox.Consious && room.abstractRoom != null)
            {
                var crits = room.abstractRoom.creatures;
                for (int i = 0; i < crits.Count; i++)
                {
                    AbstractCreature ac = crits[i];
                    if (ac == null || ac == fox.abstractCreature) continue;
                    Fox mate = ac.realizedCreature as Fox;
                    if (!IsPackmate(mate, ac)) continue;

                    float mateDist = Vector2.Distance(mate.mainBodyChunk.pos, fox.mainBodyChunk.pos);
                    if (p.PheromoneRangeTiles > 0f && mateDist > p.PheromoneRangeTiles * TilePx)
                    {
                        continue;
                    }
                    if (p.PheromoneReceiverNeedsLos)
                    {
                        if (!room.VisualContact(mate.mainBodyChunk.pos, fox.mainBodyChunk.pos))
                        {
                            continue;
                        }
                    }

                    Tracker mt = mate.AI.tracker;
                    for (int k = 0; k < mt.CreaturesCount; k++)
                    {
                        ConsiderSighting(p, mate, mt.GetRep(k), mateDist);
                    }
                }
            }

            Heartbeat(p);
        }

        private bool IsPackmate(Fox mate, AbstractCreature ac)
        {
            if (mate == null || mate.AI == null || mate.AI.tracker == null || mate.room != fox.room) return false;
            if (!mate.Consious || mate.mainBodyChunk == null) return false;
            if (ac.rippleLayer != fox.abstractCreature.rippleLayer && !ac.rippleBothSides
                && !fox.abstractCreature.rippleBothSides) return false;
            if (ac.creatureTemplate.CreatureRelationship(fox.Template).type != CreatureTemplate.Relationship.Type.Pack)
                return false;
            FoxBreedParams mp = mate.P;
            return mp != null && mp.PheromoneEnabled;
        }

        private void ConsiderSighting(FoxBreedParams p, Fox mate, Tracker.CreatureRepresentation rep, float mateDist)
        {
            if (rep == null || rep.representedCreature == null) return;
            Creature target = rep.representedCreature.realizedCreature;
            if (target == null || target == fox) return;
            if (!rep.VisualContact) return;

            if (!PassesShareFilter(p, rep.representedCreature)) { return; }

            Tracker.CreatureRepresentation mine = ai.tracker.RepresentationForCreature(rep.representedCreature, false);
            bool hadRep = mine != null;
            string saw = "rep";
            if (hadRep)
            {
                if (mine.VisualContact) return;
                bool senderSees = false;
                BodyChunk[] chunks = target.bodyChunks;
                for (int c = 0; chunks != null && c < chunks.Length; c++)
                {
                    if (mate.AI.VisualContact(chunks[c])) { senderSees = true; break; }
                }
                if (!senderSees) return;
                saw = "chunk";
            }
            Receive(p, mate, rep, target, hadRep, saw, mateDist);
        }

        private void Receive(FoxBreedParams p, Fox mate, Tracker.CreatureRepresentation rep, Creature target,
                             bool hadRep, string saw, float mateDist)
        {
            ai.tracker.SeeCreature(rep.representedCreature);

            float intensity = rep.dynamicRelationship != null
                ? rep.dynamicRelationship.currentRelationship.intensity
                : mate.AI.StaticRelationship(rep.representedCreature).intensity;
            bool senderWasQuiet = mate.AI.pheromone.Communicating <= 0;
            FoxBreedParams mp = mate.P;
            Raise(p, intensity);
            mate.AI.pheromone.Raise(mp, intensity);
            mate.AI.pheromone.NoteSent(fox);

            if (senderWasQuiet && !string.IsNullOrEmpty(mp.VoicePheromone) && mate.voice != null)
                mate.voice.Pheromone();

            ai.tracker.RepresentationForCreature(rep.representedCreature, false);
        }

        internal int Raise(FoxBreedParams p, float intensity)
        {
            int lo = Mathf.Max(0, p.PheromoneCommMinTicks);
            int hi = Mathf.Max(lo, (int)Mathf.Lerp(lo, Mathf.Max(lo, p.PheromoneCommMaxTicks), Mathf.Clamp01(intensity)));
            int dur = UnityEngine.Random.Range(lo, hi);
            if (dur > Communicating)
            {
                Communicating = dur;
                flaring = true;
            }
            return dur;
        }

        private void NoteSent(Fox receiver)
        {
            SentSerial++;
            int slot = SentSerial % SentRingSize;
            sentRing[slot] = receiver;
            sentRingSerial[slot] = SentSerial;
            LastSentTick = Clock;
        }

        internal const int SentRingSize = 8;
        private readonly Fox[] sentRing = new Fox[SentRingSize];
        private readonly int[] sentRingSerial = new int[SentRingSize];
        public int SentSerial { get; private set; }
        public int LastSentTick { get; private set; } = -100000;

        internal Fox SentReceiver(int serial)
        {
            if (serial <= 0) return null;
            int slot = serial % SentRingSize;
            return sentRingSerial[slot] == serial ? sentRing[slot] : null;
        }

        private void TickSignal(FoxBreedParams p)
        {
            if (Communicating > 0) Communicating--;
            float target = Mathf.InverseLerp(0f, Mathf.Max(1, p.PheromoneFlickerFullTicks), Communicating);
            if (Flicker < target) Flicker = Mathf.Min(target, Flicker + Mathf.Max(0f, p.PheromoneFlickerRise));
            else Flicker = Mathf.Max(target, Flicker - Mathf.Max(0f, p.PheromoneFlickerFall));
            if (flaring && Communicating <= 0 && Flicker <= 0f)
            {
                flaring = false;
            }
        }

        private bool PassesShareFilter(FoxBreedParams p, AbstractCreature target)
        {
            string mode = p.PheromoneShare;
            if (string.IsNullOrEmpty(mode) || mode == "all") return true;
            if (mode != "prey" && mode != "threats") return true;
            CreatureTemplate.Relationship rel = ai.AmbushRelationship(target, out _);
            FoxAI.RelRoute route = FoxAI.RouteFor(rel.type, p);
            return mode == "prey" ? route == FoxAI.RelRoute.Prey : route == FoxAI.RelRoute.Threat;
        }

        private void ValidateKeys(FoxBreedParams p)
        {
            string resp = p.PheromoneReceiverResponse ?? "";
            if (resp != "none" && resp != warnedResponse)
            {
                warnedResponse = resp;
            }
            string share = p.PheromoneShare ?? "";
            if (share != "all" && share != "prey" && share != "threats" && share != warnedShare)
            {
                warnedShare = share;
            }
        }

        private void Heartbeat(FoxBreedParams p)
        {
            int now = Clock;
            int cadence = Mathf.Max(1, p.PheromoneLogTicks);
            if (now - lastHeartbeatTick < cadence) return;
            lastHeartbeatTick = now;
        }
    }
}
