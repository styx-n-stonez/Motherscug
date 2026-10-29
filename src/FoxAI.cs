using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;
using static CreatureTemplate.Relationship.Type;

namespace MotherMod
{
    public class FoxAI : ArtificialIntelligence, IUseARelationshipTracker, IReactToSocialEvents, IUseItemTracker
    {
        private readonly float repRoll;

        public enum Behavior
        {
            Idle,
            Wander,
            Hunt,
            Flee,
            EscapeRain,
            ReturnToDen,
            ReturnPrey,
            Eat,
            Stalk,
            Bait
        }

        public readonly Fox fox;

        public Behavior CurrentBehavior { get { return behavior; } }

        private Behavior behavior = Behavior.Idle;
        private int behaviorCounter;
        private int huntCounter;

        private int huntLatchTicks;
        private AbstractCreature huntLatchPrey;
        private bool huntLatchHolding;
        private bool huntBlindLogged;

        private int omniCounter;
        private int omniSweep;

        internal FoxPheromone pheromone;

        private int scentCounter;
        private int scentSweep;
        private bool scentWasEnabled;
        private int sniffArmedSerial;
        private int sniffSeenClosed;
        private int sniffCooldownUntil;
        private int sniffNoGfxTicks;
        private int sniffUnstartedTicks;

        private const float TilePx = 20f;
        private const int ScentRefreshLogTicks = 200;
        private const int SniffStaleFactor = 20;

        private int eatCounter;
        private Creature eatTarget;
        private int denRestCounter;
        private string denReason = "none";

        private bool denWatchOn;
        private int denNoProgressTicks;
        private float denBestCost = float.MaxValue;
        private int denBestGen = -1;
        private AbstractCreature denResumePrey;
        private bool denGaveUp;
        private float denGaveUpHealth = -1f;

        private struct ForgottenPrey
        {
            public AbstractCreature Creature;
            public int Tick;
        }
        private readonly List<ForgottenPrey> forgottenPrey = new List<ForgottenPrey>();
        private int lastSpacingLogTick = -1000000;

        private AbstractCreature lastPreyTarget;

        private readonly FoxPounceRouter pounceRouter;

        private readonly FoxBait bait;

        internal FoxBait Bait { get { return bait; } }

        internal FoxAscentPlan AscentPlan
        {
            get { return pounceRouter != null ? pounceRouter.Plan : null; }
        }

        internal int AscentPathGeneration
        {
            get { return pounceRouter != null ? pounceRouter.PathGeneration : -1; }
        }

        internal string WalkedRoute
        {
            get { return pounceRouter != null ? pounceRouter.WalkedRoute : "none"; }
        }

        internal FoxAscentPlan RequestAscent(Vector2 measuredLip, float runUpPx)
        {
            if (pounceRouter == null) return null;
            return pounceRouter.RequestAscent(fox.P, measuredLip, runUpPx);
        }

        private bool obstacleReporting;

        private bool obstacleMuted;

        private float huntBestDistance = float.MaxValue;
        private bool huntProgressEvent;

        private bool stalkLatched;
        private AbstractCreature stalkLatchedOn;

        private int GameTick
        {
            get { return (fox.room != null && fox.room.game != null) ? fox.room.game.clock : 0; }
        }

        public FoxAI(AbstractCreature acrit, Fox fox) : base(acrit, acrit.world)
        {
            this.fox = fox;
            fox.AI = this;

            unchecked
            {
                uint h = (uint)acrit.ID.RandomSeed * 2654435761u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                repRoll = (h % 20001u) / 10000f - 1f;
            }

            FoxBreedParams p = fox.P;

            AddModule(new StandardPather(this, acrit.world, acrit));
            pathFinder.stepsPerFrame = p.PathStepsPerFrame;

            AddModule(new Tracker(this, 10, 10, p.RememberCreatureFrames, 0.35f, 5, 5, 10));
            AddModule(new ThreatTracker(this, 3));
            AddModule(new RainTracker(this));
            AddModule(new DenFinder(this, acrit));
            AddModule(new NoiseTracker(this, tracker));
            AddModule(new PreyTracker(this, 5, p.PreyPersistenceBias, p.PreySureToGetDistance, p.PreySureToLoseDistance, 0.5f));
            preyTracker.giveUpOnUnreachablePrey = p.PreyGiveUpUnreachable;
            preyTracker.giveUpOnGhostGeneration = Math.Max(2, p.PreyGiveUpGhostGeneration);
            AddModule(new UtilityComparer(this));
            pheromone = new FoxPheromone(this);
            AddModule(pheromone);
            AddModule(new RelationshipTracker(this, tracker));
            AddModule(new StuckTracker(this, trackPastPositions: true, trackNotFollowingCurrentGoal: true));

            if (p.DiscomfortEnabled)
            {
                AddModule(new DiscomfortTracker(this, tracker, 0f));
            }

            if (p.RouteObstacleEnabled)
            {
                AddModule(new ObstacleTracker(this, trackObjects: false, trackTiles: true,
                                              mapDecayPerReport: 2,
                                              objectsReportsToNoticeCreatures: 3,
                                              objectsMaxTrackedCreatures: 5));
            }

            if (p.BaitEnabled)
            {
                AddModule(new ItemTracker(this, FoxBait.SeeAroundCorners, p.BaitTrackMaxItems,
                                          p.BaitTrackRememberTicks, p.BaitTrackForgetTiles,
                                          stopTrackingCarried: true));
                bait = new FoxBait(this, fox);
            }

            var smoother = new FloatTweener.FloatTweenUpAndDown(
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Lerp, 0.5f),
                new FloatTweener.FloatTweenBasic(FloatTweener.TweenType.Tick, 0.005f));
            utilityComparer.AddComparedModule(threatTracker, smoother, p.WeightThreat, 1.1f);
            utilityComparer.AddComparedModule(preyTracker, null, p.WeightPrey, 1.1f);
            utilityComparer.AddComparedModule(rainTracker, null, p.WeightRain, 1.1f);

            noiseTracker.hearingSkill = p.HearingSkill;

            pounceRouter = new FoxPounceRouter(this, fox);
        }

        public override void Update()
        {
            if (fox.room != null)
            {
                FoxBreedParams p = fox.P;
                UpdateAppetite(p);
                UpdateSocialMemoryDecay(p);
                UpdateScentSense(p);
                UpdateOmniSense(p);
                if (bait != null) bait.Update(p);
                DetermineBehavior();
                ActOnBehavior();

                if (pounceRouter != null) pounceRouter.Update(p);

                FeedObstacleTracker(p);
            }

            base.Update();
        }

        private void DetermineBehavior()
        {
            FoxBreedParams p = fox.P;
            Behavior previous = behavior;
            if (forgottenPrey.Count > 0) TickForgottenPrey(p);
            float utility = utilityComparer.HighestUtility();

            if (utility < p.IdleUtilityThreshold)
            {
                if (behavior != Behavior.Idle && behavior != Behavior.Wander)
                {
                    behavior = Behavior.Idle;
                    behaviorCounter = 0;
                }
                else if (behaviorCounter <= 0)
                {
                    behavior = (UnityEngine.Random.value < p.IdleStayChance) ? Behavior.Idle : Behavior.Wander;
                    behaviorCounter = UnityEngine.Random.Range(p.IdleSwitchMinFrames, p.IdleSwitchMaxFrames);
                }
            }
            else
            {
                AIModule top = utilityComparer.HighestUtilityModule();
                if (top is ThreatTracker) behavior = Behavior.Flee;
                else if (top is RainTracker) behavior = Behavior.EscapeRain;
                else if (top is PreyTracker) behavior = Behavior.Hunt;
            }

            if (behavior == Behavior.Hunt || behavior == Behavior.Stalk)
            {
                var latchOn = preyTracker.MostAttractivePrey?.representedCreature;
                if (!ReferenceEquals(latchOn, huntLatchPrey))
                {
                    huntLatchPrey = latchOn;
                    huntLatchHolding = false;
                }
                huntLatchTicks = Math.Max(0, p.HuntPersistFrames);
            }
            else if (huntLatchTicks > 0)
            {
                huntLatchTicks--;
                var stillTracked = preyTracker.MostAttractivePrey;
                bool eligible = behavior != Behavior.Flee && behavior != Behavior.EscapeRain
                                && behavior != Behavior.ReturnPrey && behavior != Behavior.Eat
                                && fox.HeldPrey == null
                                && stillTracked != null
                                && ReferenceEquals(stillTracked.representedCreature, huntLatchPrey);
                if (eligible)
                {
                    behavior = Behavior.Hunt;
                    if (!huntLatchHolding)
                    {
                        huntLatchHolding = true;
                    }
                }
                else
                {
                    if (huntLatchHolding)
                    {
                        huntLatchHolding = false;
                    }
                    huntLatchTicks = 0;
                }
            }
            else if (huntLatchHolding)
            {
                huntLatchHolding = false;
            }

            Behavior beforeDen = behavior;
            if (behavior != Behavior.Flee && behavior != Behavior.EscapeRain)
            {
                string reason = null;
                float health = creature.state is HealthState hs0 ? hs0.health : 1f;
                bool injured = creature.state is HealthState && health < p.DenHealthThreshold;
                if (denGaveUp) UpdateDenGiveUpWound(health, injured);
                if (injured && !denGaveUp) reason = "injury";
                else if (p.DenFedEnabled && denRestCounter > 0 && utility < p.DenFedPriority) reason = "fed";

                if (reason != null)
                {
                    behavior = Behavior.ReturnToDen;
                    denReason = reason;
                }
            }

            if (behavior == Behavior.ReturnToDen && denReason == "injury")
            {
                if (!denWatchOn) BeginDenWatch(previous);
                else if (DenRetreatStalled(p))
                {
                    behavior = GiveUpDen(p, beforeDen);
                }
            }
            else denWatchOn = false;

            bool ambushHolds = bait != null && bait.HoldsHunt(p);

            if (behavior == Behavior.Hunt && p.StalkEnabled && !ambushHolds) behavior = StalkOrHunt(p, behavior, previous);
            else if (behavior != Behavior.Stalk) fox.StalkCrouch = 0f;

            if (p.EatInPlaceEnabled
                && (behavior == Behavior.Hunt || behavior == Behavior.Stalk || behavior == Behavior.Eat))
            {
                behavior = EatOrHunt(p, behavior);
            }
            else if (eatTarget != null)
            {
                ClearEat();
            }
            if (!p.EatInPlaceEnabled && behavior == Behavior.Eat)
            {
                behavior = Behavior.Hunt;
            }

            if (fox.HeldPrey != null && behavior != Behavior.Flee)
            {
                if (behavior != Behavior.ReturnPrey && eatTarget != null)
                {
                    ClearEat();
                }
                behavior = Behavior.ReturnPrey;
            }
            else if (behavior == Behavior.ReturnPrey)
            {
                behavior = Behavior.Idle;
            }

            if ((behavior == Behavior.Hunt || behavior == Behavior.Stalk) && !ambushHolds)
            {
                bool progressed = HuntProgressed(p);
                var watched = preyTracker.MostAttractivePrey;
                bool seen = watched != null && watched.VisualContact;
                bool reachable = watched != null && PreyReachable(watched);
                if (progressed) huntCounter = 0;
                else if (!(seen && reachable)) huntCounter++;
                if (huntCounter > p.HuntForgetFrames)
                {
                    var giveUp = preyTracker.MostAttractivePrey;
                    huntCounter = 0;
                    if (giveUp != null)
                    {
                        preyTracker.ForgetPrey(giveUp.representedCreature);
                        RememberForgottenPrey(giveUp.representedCreature);
                    }
                    behavior = Behavior.Idle;
                }
            }
            else
            {
                huntCounter = 0;
                huntBestDistance = float.MaxValue;
                stalkLatched = false;
                stalkLatchedOn = null;
            }

            if (bait != null) behavior = bait.Arbitrate(p, behavior);

            if (behavior != Behavior.ReturnToDen) denWatchOn = false;

            behaviorCounter--;

            if (behavior != previous)
            {
                if (behavior == Behavior.Hunt || behavior == Behavior.Stalk) fox.voice.Hunt();
            }
        }

        public void ReportHuntProgress(string reason)
        {
            huntProgressEvent = true;
            if (bait != null) bait.NoteHuntProgress(reason);
        }

        internal CreatureTemplate.Relationship AmbushRelationship(AbstractCreature other, out string src)
        {
            return ScentRelationship(other, tracker.RepresentationForCreature(other, false), fox.P, out src);
        }

        internal bool BeginAmbushChase(Creature target, FoxBreedParams p, ref string detail)
        {
            if (target == null || target.abstractCreature == null || tracker == null || preyTracker == null)
            {
                return false;
            }

            AbstractCreature ac = target.abstractCreature;
            tracker.SeeCreature(ac);
            Tracker.CreatureRepresentation rep = tracker.RepresentationForCreature(ac, false);
            if (rep == null)
            {
                return false;
            }

            CreatureTemplate.Relationship rel = rep.dynamicRelationship != null
                ? rep.dynamicRelationship.currentRelationship : StaticRelationship(ac);
            if (RouteFor(rel.type, p) != RelRoute.Prey)
            {
                return false;
            }

            preyTracker.AddPrey(rep);

            huntLatchPrey = ac;
            huntLatchTicks = Math.Max(0, p.HuntPersistFrames);
            huntLatchHolding = false;
            huntCounter = 0;
            huntBestDistance = float.MaxValue;
            stalkLatched = true;
            stalkLatchedOn = ac;

            return true;
        }

        private bool HuntProgressed(FoxBreedParams p)
        {
            if (huntProgressEvent)
            {
                huntProgressEvent = false;
                huntBestDistance = float.MaxValue;
                return true;
            }

            var prey = preyTracker.MostAttractivePrey;
            Creature realized = prey?.representedCreature.realizedCreature;
            if (realized == null || realized.room != fox.room || realized.mainBodyChunk == null)
            {
                return false;
            }

            float d = Vector2.Distance(fox.mainBodyChunk.pos, realized.mainBodyChunk.pos);
            if (d < huntBestDistance - 1f)
            {
                huntBestDistance = d;
                return true;
            }
            return false;
        }

        private bool PreyReachable(Tracker.CreatureRepresentation rep)
        {
            if (rep == null || pathFinder == null || !pathFinder.DoneMappingAccessibility) return false;
            WorldCoordinate guess = rep.BestGuessForPosition();
            if (pathFinder.CoordinateReachableAndGetbackable(guess)) return true;
            for (int i = 0; i < 4; i++)
            {
                if (pathFinder.CoordinateReachableAndGetbackable(guess + Custom.fourDirections[i])) return true;
            }
            return false;
        }

        private void RememberForgottenPrey(AbstractCreature ac)
        {
            if (ac == null) return;
            for (int i = forgottenPrey.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(forgottenPrey[i].Creature, ac)) forgottenPrey.RemoveAt(i);
            }
            forgottenPrey.Add(new ForgottenPrey { Creature = ac, Tick = GameTick });
        }

        private void TickForgottenPrey(FoxBreedParams p)
        {
            int now = GameTick;
            int cooldown = p.HuntForgetCooldownTicks;
            for (int i = forgottenPrey.Count - 1; i >= 0; i--)
            {
                ForgottenPrey f = forgottenPrey[i];
                if (cooldown < 0)
                {
                    forgottenPrey.RemoveAt(i);
                    continue;
                }
                if (now - f.Tick < cooldown) continue;
                forgottenPrey.RemoveAt(i);

                Tracker.CreatureRepresentation rep = (tracker == null || preyTracker == null || f.Creature == null)
                    ? null : tracker.RepresentationForCreature(f.Creature, false);
                if (rep == null || rep.deleteMeNextFrame)
                {
                    continue;
                }
                CreatureTemplate.Relationship rel = rep.dynamicRelationship != null
                    ? rep.dynamicRelationship.currentRelationship : StaticRelationship(f.Creature);
                if (RouteFor(rel.type, p) != RelRoute.Prey)
                {
                    continue;
                }
                preyTracker.AddPrey(rep);
            }
        }

        private void BeginDenWatch(Behavior was)
        {
            denWatchOn = true;
            denNoProgressTicks = 0;
            denBestCost = float.MaxValue;
            denBestGen = -1;
            denResumePrey = (was == Behavior.Hunt || was == Behavior.Stalk)
                ? (preyTracker.MostAttractivePrey?.representedCreature ?? huntLatchPrey) : null;
        }

        private bool DenRetreatStalled(FoxBreedParams p)
        {
            bool progress = false;
            if (pathFinder == null) { }
            else if (!denFinder.GetDenPosition().HasValue) { }
            else
            {
                PathFinder.PathingCell cell = pathFinder.PathingCellAtWorldCoordinate(creature.pos);
                if (cell == null) { }
                else
                {
                    if (cell.generation != pathFinder.pathGeneration) { }
                    else if (!cell.costToGoal.Allowed) { }
                    else
                    {
                        float cost = cell.costToGoal.resistance;
                        if (denBestGen != cell.generation)
                        {
                            denBestGen = cell.generation;
                            denBestCost = cost;
                        }
                        else if (cost < denBestCost)
                        {
                            denBestCost = cost;
                            progress = true;
                        }
                    }
                }
            }

            denNoProgressTicks = progress ? 0 : denNoProgressTicks + 1;
            return p.DenGiveUpTicks > 0 && denNoProgressTicks >= p.DenGiveUpTicks;
        }

        private Behavior GiveUpDen(FoxBreedParams p, Behavior arbitrated)
        {
            denGaveUp = true;
            denGaveUpHealth = creature.state is HealthState hs ? hs.health : 1f;
            denWatchOn = false;
            denNoProgressTicks = 0;

            Behavior next = arbitrated;
            Tracker.CreatureRepresentation top = preyTracker.MostAttractivePrey;
            if (denResumePrey != null && top != null && ReferenceEquals(top.representedCreature, denResumePrey))
            {
                next = Behavior.Hunt;
                huntLatchPrey = denResumePrey;
                huntLatchTicks = Math.Max(0, p.HuntPersistFrames);
                huntLatchHolding = false;
            }
            if (next == Behavior.ReturnToDen) next = Behavior.Idle;

            denFinder.GetDenPosition();
            return next;
        }

        private void UpdateDenGiveUpWound(float health, bool injured)
        {
            if (!injured)
            {
                denGaveUp = false;
                return;
            }
            if (health < denGaveUpHealth)
            {
                denGaveUp = false;
                return;
            }
            denGaveUpHealth = health;
        }

        private void ActOnBehavior()
        {
            FoxBreedParams p = fox.P;

            pathFinder.walkPastPointOfNoReturn =
                stranded
                || !denFinder.GetDenPosition().HasValue
                || !pathFinder.CoordinatePossibleToGetBackFrom(denFinder.GetDenPosition().Value)
                || threatTracker.Utility() > p.PanicUtilityThreshold;

            float stuckBoost = stuckTracker != null ? stuckTracker.Utility() : 0f;

            switch (behavior)
            {
                case Behavior.Idle:
                    fox.runSpeed = Mathf.Max(p.RunSpeedIdle * p.RunSpeedIdleStillFac, stuckBoost);
                    fox.chaseTarget = null;
                    fox.StalkCrouch = 0f;
                    break;

                case Behavior.Wander:
                    fox.runSpeed = Mathf.Max(p.RunSpeedIdle, stuckBoost);
                    fox.chaseTarget = null;
                    UpdateWanderDestination(p);
                    break;

                case Behavior.Hunt:
                    fox.runSpeed = Mathf.Max(p.RunSpeedHunt, stuckBoost);
                    Hunt(p);
                    break;

                case Behavior.Flee:
                    fox.runSpeed = Mathf.Max(p.RunSpeedFlee, stuckBoost);
                    fox.chaseTarget = null;
                    creature.abstractAI.SetDestination(
                        threatTracker.FleeTo(creature.pos, p.FleeReevaluations, p.FleeMaxDistance, true));
                    break;

                case Behavior.Stalk:
                    fox.runSpeed = Mathf.Max(p.RunSpeedStalk, stuckBoost);
                    fox.StalkCrouch = Mathf.Clamp01(p.StalkCrouchFac);
                    Hunt(p, allowPounce: false);
                    break;

                case Behavior.Eat:
                    fox.runSpeed = stuckBoost;
                    UpdateEat(p);
                    break;

                case Behavior.ReturnPrey:
                    fox.runSpeed = Mathf.Max(p.RunSpeedHunt, stuckBoost);
                    fox.chaseTarget = null;
                    var preyDen = denFinder.GetDenPosition();
                    if (preyDen.HasValue) creature.abstractAI.SetDestination(preyDen.Value);
                    break;

                case Behavior.EscapeRain:
                case Behavior.ReturnToDen:
                    fox.runSpeed = Mathf.Max(p.RunSpeedFlee, stuckBoost);
                    fox.chaseTarget = null;
                    var den = denFinder.GetDenPosition();
                    if (den.HasValue) creature.abstractAI.SetDestination(den.Value);
                    if (denRestCounter > 0) denRestCounter--;
                    break;

                case Behavior.Bait:
                    if (bait == null || !bait.Striking) fox.chaseTarget = null;
                    if (bait != null) bait.Act(p, stuckBoost);
                    break;
            }

            UpdateEatPose(p);
        }

        private void UpdateEatPose(FoxBreedParams p)
        {
            float rate = Mathf.Max(0.001f, p.EatPoseRate);
            bool feeding = behavior == Behavior.Eat && eatTarget != null
                           && eatTarget.mainBodyChunk != null;
            fox.EatPose = Mathf.Clamp01(fox.EatPose + (feeding ? rate : -rate));
            if (feeding) fox.EatAimPos = eatTarget.mainBodyChunk.pos;
        }

        private void Hunt(FoxBreedParams p, bool allowPounce = true)
        {
            var prey = preyTracker.MostAttractivePrey;
            if (prey == null)
            {
                fox.chaseTarget = null;
                return;
            }

            if (!ReferenceEquals(prey.representedCreature, lastPreyTarget))
            {
                lastPreyTarget = prey.representedCreature;
            }

            WorldCoordinate guess = prey.BestGuessForPosition();
            bool blindPursuit = !prey.VisualContact;
            if (p.HuntGhostPursue || prey.VisualContact)
            {
                creature.abstractAI.SetDestination(guess);
            }
            if (blindPursuit != huntBlindLogged)
            {
                huntBlindLogged = blindPursuit;
            }

            bool corpsesCount = p.EatsCarrion;
            Creature realized = prey.representedCreature.realizedCreature;
            fox.chaseTarget = (realized != null && realized.room == fox.room
                               && (!realized.dead || corpsesCount)) ? realized : null;

            if (allowPounce && fox.chaseTarget != null && !fox.chaseTarget.dead
                && fox.pounce == Fox.PounceState.None && fox.charge == Fox.ChargeState.None)
            {
                if (!fox.TryCharge(fox.chaseTarget.mainBodyChunk.pos))
                {
                    fox.TryPounce(fox.chaseTarget.mainBodyChunk.pos);
                }
            }
        }

        private void SetIdleDestination()
        {
            IdleSample s = SampleIdleDestination(fox.P);
            if (!s.Found) return;
            creature.abstractAI.SetDestination(s.Coord);
        }

        private struct IdleSample
        {
            internal bool Found;
            internal WorldCoordinate Coord;
            internal float Score, Threat;
            internal IntVector2 From;
        }

        private IdleSample SampleIdleDestination(FoxBreedParams p)
        {
            IdleSample result = default;
            if (fox.room == null) return result;

            IntVector2 here = fox.room.GetTilePosition(fox.mainBodyChunk.pos);

            WorldCoordinate best = default;
            bool haveBest = false;
            float bestScore = float.MinValue;
            float bestThreat = 0f, bestNoise = 0f, bestDist = 0f, bestSpacing = 0f;
            int examined = 0;

            int candidates = Math.Max(1, p.RoamCandidates);
            int attempts = 0;
            int maxAttempts = candidates * Math.Max(1, p.RoamAttemptsPerCandidate);
            int radius = Math.Max(1, p.RoamRadiusTiles);
            while (examined < candidates && attempts < maxAttempts)
            {
                attempts++;
                var coord = new WorldCoordinate(
                    fox.room.abstractRoom.index,
                    Mathf.Clamp(here.x + UnityEngine.Random.Range(-radius, radius + 1),
                                0, fox.room.TileWidth - 1),
                    Mathf.Clamp(here.y + UnityEngine.Random.Range(-radius, radius + 1),
                                0, fox.room.TileHeight - 1),
                    -1);

                if (!pathFinder.CoordinateReachableAndGetbackable(coord)) continue;
                if (fox.PoleDestinationRefused(coord)) continue;
                if (fox.WallDestinationRefused(coord)) continue;
                examined++;

                float threatTerm, noiseTerm, distTerm, spacingTerm;
                float score = ScoreIdleCoordinate(coord, here, p,
                                                  out threatTerm, out noiseTerm,
                                                  out distTerm, out spacingTerm);

                if (!haveBest || score > bestScore)
                {
                    haveBest = true;
                    bestScore = score;
                    best = coord;
                    bestThreat = threatTerm;
                    bestNoise = noiseTerm;
                    bestDist = distTerm;
                    bestSpacing = spacingTerm;
                }
            }

            result.Found = haveBest;
            result.Coord = best;
            result.Score = bestScore;
            result.Threat = bestThreat;
            result.From = here;

            return result;
        }

        private float ScoreIdleCoordinate(WorldCoordinate coord, IntVector2 here, FoxBreedParams p,
                                          out float threatTerm, out float noiseTerm,
                                          out float distTerm, out float spacingTerm)
        {
            threatTerm = ThreatTermFor(coord);
            noiseTerm = NoiseTermFor(coord);
            distTerm = Mathf.InverseLerp(0f, ROAM_NORMALISE_TILES,
                                         IntVector2.ToVector2(here - coord.Tile).magnitude);
            spacingTerm = ConspecificTermFor(coord, p);

            return threatTerm * p.RoamThreatWeight
                 + noiseTerm * p.RoamNoiseWeight
                 + distTerm * p.RoamDistanceWeight
                 + spacingTerm
                 + UnityEngine.Random.value * p.RoamPickJitter;
        }

        private int idleCommitTicks;
        private int idleCommitStreak;

        private const int IDLE_ARRIVE_TILES = 5;

        private void UpdateWanderDestination(FoxBreedParams p)
        {
            if (p.IdleCommitWinStreak <= 0)
            {
                if (UnityEngine.Random.value < p.IdleWanderChance) SetIdleDestination();
                return;
            }

            WorldCoordinate held = creature.abstractAI.destination;
            bool heldUsable = held.TileDefined
                              && fox.room != null
                              && held.room == fox.room.abstractRoom.index
                              && pathFinder.CoordinateReachableAndGetbackable(held)
                              && !fox.PoleDestinationRefused(held)
                              && !fox.WallDestinationRefused(held);

            bool forced = false;
            if (!heldUsable)
            {
                if (idleCommitTicks > 0 || idleCommitStreak > 0)
                {
                    forced = true;
                }
                idleCommitTicks = 0;
                idleCommitStreak = 0;
            }
            else if (idleCommitTicks > 0)
            {
                if (fox.room != null
                    && Custom.ManhattanDistance(fox.room.GetTilePosition(fox.mainBodyChunk.pos),
                                                held.Tile) < IDLE_ARRIVE_TILES)
                {
                    idleCommitTicks = 0;
                    idleCommitStreak = 0;
                }
                else
                {
                    idleCommitTicks--;
                    return;
                }
            }

            if (!forced && UnityEngine.Random.value >= p.IdleWanderChance) return;

            IdleSample s = SampleIdleDestination(p);
            if (!s.Found) return;

            if (!heldUsable)
            {
                creature.abstractAI.SetDestination(s.Coord);
                idleCommitStreak = 0;
                return;
            }

            float dropThreat, dropNoise, dropDist, dropSpacing;
            float heldScore = ScoreIdleCoordinate(held, s.From, p,
                                                  out dropThreat, out dropNoise,
                                                  out dropDist, out dropSpacing);

            if (s.Score > heldScore)
            {
                creature.abstractAI.SetDestination(s.Coord);
                idleCommitStreak = 0;
                return;
            }

            idleCommitStreak++;
            if (idleCommitStreak > p.IdleCommitWinStreak)
            {
                int lo = Math.Max(1, p.IdleCommitLockMin);
                int hi = Math.Max(lo + 1, p.IdleCommitLockMax);
                idleCommitTicks = UnityEngine.Random.Range(lo, hi);
                idleCommitStreak = 0;
            }
        }

        internal void ForceFreshDestination(string reason)
        {
            idleCommitTicks = 0;
            idleCommitStreak = 0;
            SetIdleDestination();
        }

        private const float ROAM_NORMALISE_TILES = 40f;

        private float ThreatTermFor(WorldCoordinate coord)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                if (rep == null || rep.dynamicRelationship == null) continue;
                if (rep.dynamicRelationship.currentRelationship.type != Afraid) continue;

                float d = IntVector2.ToVector2(rep.BestGuessForPosition().Tile - coord.Tile).magnitude;
                if (d < nearest) nearest = d;
            }
            if (nearest == float.MaxValue) return 1f;
            return Mathf.InverseLerp(0f, ROAM_NORMALISE_TILES, nearest);
        }

        private float NoiseTermFor(WorldCoordinate coord)
        {
            if (noiseTracker == null || noiseTracker.sources == null || noiseTracker.sources.Count == 0) return 0f;

            float nearest = float.MaxValue;
            for (int i = 0; i < noiseTracker.sources.Count; i++)
            {
                var src = noiseTracker.sources[i];
                if (src == null) continue;
                IntVector2 tile = fox.room.GetTilePosition(src.pos);
                float d = IntVector2.ToVector2(tile - coord.Tile).magnitude;
                if (d < nearest) nearest = d;
            }
            if (nearest == float.MaxValue) return 0f;
            return 1f - Mathf.InverseLerp(0f, ROAM_NORMALISE_TILES, nearest);
        }

        public bool TryGetHeardNoise(int maxAge, out Vector2 pos, out int age)
        {
            pos = Vector2.zero;
            age = int.MaxValue;
            if (noiseTracker == null || noiseTracker.sources == null) return false;

            float bestLoud = float.MinValue;
            bool found = false;
            for (int i = 0; i < noiseTracker.sources.Count; i++)
            {
                var src = noiseTracker.sources[i];
                if (src == null || src.slatedForDeletion) continue;
                if (src.age > maxAge) continue;
                if (src.age > age) continue;
                if (src.age == age && src.LASTSOUNDSTRENGTH <= bestLoud) continue;
                age = src.age;
                bestLoud = src.LASTSOUNDSTRENGTH;
                pos = src.pos;
                found = true;
            }
            return found;
        }

        private float ConspecificTermFor(WorldCoordinate coord, FoxBreedParams p)
        {
            if (!p.ConspecificAwareness || p.ConspecificSpacing <= 0f) return 0f;

            float worst = 0f;
            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                if (rep == null) continue;
                if (!FoxCritob.IsFoxType(rep.representedCreature.creatureTemplate.type)) continue;
                if (rep.representedCreature == creature) continue;

                float d = IntVector2.ToVector2(rep.BestGuessForPosition().Tile - coord.Tile).magnitude;
                if (d >= p.ConspecificSpacing) continue;

                float penalty = 1f - d / p.ConspecificSpacing;
                if (penalty > worst)
                {
                    worst = penalty;
                    if (GameTick - lastSpacingLogTick >= 200)
                    {
                        lastSpacingLogTick = GameTick;
                    }
                }
            }
            return -worst;
        }

        public override float VisualScore(Vector2 lookAtPoint, float bonus)
        {
            float score = base.VisualScore(lookAtPoint, bonus);
            if (fox == null || fox.bodyChunks == null || fox.bodyChunks.Length < 2) return score;

            FoxBreedParams p = fox.P;
            Vector2 facing = (fox.bodyChunks[0].pos - fox.bodyChunks[1].pos).normalized;
            Vector2 toTarget = (lookAtPoint - fox.bodyChunks[1].pos).normalized;

            return score - Mathf.InverseLerp(p.VisionPerfectAngle, p.VisionPeripheralAngle,
                                             Vector2.Dot(facing, toTarget));
        }

        private void UpdateOmniSense(FoxBreedParams p)
        {
            if (fox.room == null || creature.Room == null) return;

            omniCounter++;
            if (omniCounter < Math.Max(1, p.SenseOmniIntervalTicks)) return;
            omniCounter = 0;
            omniSweep++;

            var crits = creature.Room.creatures;
            if (crits == null) return;

            for (int i = 0; i < crits.Count; i++)
            {
                AbstractCreature other = crits[i];
                if (other == null || other == creature) continue;

                Creature realized = other.realizedCreature;
                if (realized == null || realized.room != fox.room) continue;

                if (realized is Player pl && pl.IsHidden && pl.VisibilityBonus <= -1f)
                {
                    LoseOmni(other, "hidden");
                    continue;
                }

                CreatureTemplate.Relationship rel = StaticRelationship(other);
                if (rel.type == DoesntTrack || rel.type == Ignores || rel.intensity <= 0f) continue;

                Vector2 from = fox.VisionPoint;
                Vector2 to = realized.mainBodyChunk.pos;
                bool los = fox.room.VisualContact(from, to);

                if (!los && !p.SenseThroughWalls)
                {
                    LoseOmni(other, "no_line_of_sight");
                    continue;
                }

                float radius = los ? p.SenseRadius : p.SenseRadius * p.SenseWallRadiusFac;
                float cap = los ? p.SenseMaxScore : p.SenseMaxScore * p.SenseWallScoreFac;

                bool blinded = p.BlindEnabled && fox.Blinded;
                if (blinded)
                {
                    float fac = Mathf.Clamp01(p.BlindOmniFac);
                    radius *= fac;
                    cap *= fac;
                }
                float dist = Vector2.Distance(from, to);
                if (dist > radius)
                {
                    LoseOmni(other, "out_of_range");
                    continue;
                }

                var rep = tracker.RepresentationForCreature(other, true);
                if (rep == null) continue;

                string channel = los ? "omni" : "omni_wall";
                float score = rep.EstimatedChanceOfFinding;

                if (score <= cap)
                {
                    if (los && p.SenseOmniInLosFull)
                    {
                        tracker.SeeCreature(other);
                        channel = "omni_full";
                    }
                    else
                    {
                        rep.HeardThisCreature();
                    }
                }

                MarkOmni(rep, other, channel, rep.EstimatedChanceOfFinding, dist, cap);
            }

            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                var st = rep?.dynamicRelationship?.state as FoxTrackedState;
                if (st == null || st.omniChannel == null || st.omniSweep == omniSweep) continue;

                st.omniChannel = null;
            }
        }

        private void MarkOmni(Tracker.CreatureRepresentation rep, AbstractCreature other,
                              string channel, float score, float dist, float cap)
        {
            var st = rep.dynamicRelationship?.state as FoxTrackedState;
            if (st == null) return;

            st.omniSweep = omniSweep;
            if (st.omniChannel == channel) return;

            st.omniChannel = channel;
        }

        private void LoseOmni(AbstractCreature other, string reason)
        {
            var rep = tracker.RepresentationForCreature(other, false);
            var st = rep?.dynamicRelationship?.state as FoxTrackedState;
            if (st == null || st.omniChannel == null) return;

            st.omniChannel = null;
            st.omniSweep = omniSweep;
        }

        private struct ScentCandidate
        {
            public AbstractCreature other;
            public Tracker.CreatureRepresentation rep;
            public FoxTrackedState st;
            public string channel;
            public float dist, radPx, appetite, cap, passive, omniCap;
            public CreatureTemplate.Relationship rel;
            public string relSrc, omniHeld;
        }

        private void UpdateScentSense(FoxBreedParams p)
        {
            if (!p.ScentEnabled)
            {
                if (scentWasEnabled)
                {
                    scentWasEnabled = false;
                }
                return;
            }
            if (fox.room == null || creature.Room == null) return;

            int now = GameTick;
            if (!scentWasEnabled)
            {
                scentWasEnabled = true;
                sniffSeenClosed = fox.ScentSniff.ClosedSerial;
                sniffArmedSerial = 0;
            }

            PollSniffSeam(p, now);

            scentCounter++;
            if (scentCounter < Math.Max(1, p.ScentIntervalTicks)) return;
            scentCounter = 0;
            scentSweep++;

            int armed = sniffArmedSerial;
            sniffArmedSerial = 0;
            int admitted = 0, seeded = 0;

            var crits = creature.Room.creatures;
            if (crits == null) return;

            float blindFac = (p.BlindEnabled && fox.Blinded) ? Mathf.Clamp01(p.BlindOmniFac) : 1f;
            Vector2 from = fox.VisionPoint;

            AbstractCreature wantFor = null;
            string wantWhy = null;
            float wantDist = 0f, wantAppetite = -1f;

            for (int i = 0; i < crits.Count; i++)
            {
                ScentCandidate c = default(ScentCandidate);
                c.other = crits[i];
                if (c.other == null || c.other == creature) continue;

                Creature realized = c.other.realizedCreature;
                if (realized == null || realized.room != fox.room || realized.mainBodyChunk == null) continue;

                c.rep = tracker.RepresentationForCreature(c.other, false);
                c.st = c.rep?.dynamicRelationship?.state as FoxTrackedState;

                if (realized is Player pl && pl.IsHidden && pl.VisibilityBonus <= -1f)
                {
                    ColdScent(c.other, c.rep, c.st, "hidden", -1f, "");
                    continue;
                }

                c.rel = ScentRelationship(c.other, c.rep, p, out c.relSrc);
                if (c.rel.type == DoesntTrack) continue;
                c.appetite = RouteFor(c.rel.type, p) == RelRoute.Prey ? Mathf.Clamp01(c.rel.intensity) : 0f;

                Vector2 to = realized.mainBodyChunk.pos;
                c.dist = Vector2.Distance(from, to);
                float radOpen = p.ScentRadiusTiles * TilePx * Mathf.Lerp(p.ScentAppetiteRangeFac, 1f, c.appetite);
                if (c.dist > radOpen)
                {
                    ColdScent(c.other, c.rep, c.st, "out_of_range", c.dist, "");
                    continue;
                }
                bool los = fox.room.VisualContact(from, to);
                float radNoBlind = los ? radOpen : radOpen * p.ScentWallRadiusFac;
                c.radPx = radNoBlind * blindFac;
                c.channel = los ? "scent" : "scent_wall";
                if (c.dist > c.radPx)
                {
                    ColdScent(c.other, c.rep, c.st,
                              (blindFac < 1f && c.dist <= radNoBlind) ? "blinded" : "out_of_range", c.dist, "");
                    continue;
                }

                c.cap = p.ScentMaxScore * Mathf.Lerp(p.ScentAppetiteScoreFac, 1f, c.appetite)
                        * (los ? 1f : p.ScentWallScoreFac) * blindFac;
                c.passive = c.cap * p.ScentPassiveFac;
                if (c.cap <= 0f)
                {
                    string why = null;
                    ColdScent(c.other, c.rep, c.st, "no_appetite", c.dist, why);
                    continue;
                }

                c.omniHeld = OmniClaim(p, c.other, c.st, los, c.dist, blindFac, out c.omniCap);
                float pre = c.rep != null ? c.rep.EstimatedChanceOfFinding : 0f;
                if (c.omniHeld != null && (c.rep == null || pre <= c.omniCap))
                {
                    string why = null;
                    ColdScent(c.other, c.rep, c.st, "omni_won", c.dist, why);
                    continue;
                }

                if (c.rep == null)
                {
                    if (armed == 0)
                    {
                        NoteSniffWant(ref wantFor, ref wantWhy, ref wantDist, ref wantAppetite, c, "unseeded");
                        continue;
                    }
                    c.rep = tracker.RepresentationForCreature(c.other, true);
                    if (c.rep == null) continue;
                    c.st = c.rep.dynamicRelationship?.state as FoxTrackedState;
                    admitted++; seeded++;
                    MarkScent(ref c, "acquired", 0f, armed, 1);
                    continue;
                }

                if (armed != 0)
                {
                    if (pre <= c.cap)
                    {
                        c.rep.HeardThisCreature();
                        admitted++;
                        MarkScent(ref c, c.st != null && c.st.scentChannel != null ? "promoted" : "acquired",
                                  pre, armed, 0);
                    }
                    else
                    {
                        TouchScent(c.st);
                    }
                }
                else
                {
                    if (pre <= c.passive)
                    {
                        c.rep.HeardThisCreature();
                        MarkScent(ref c, c.st != null && c.st.scentChannel != null ? "refreshed" : "acquired",
                                  pre, 0, 0);
                    }
                    else
                    {
                        TouchScent(c.st);
                    }
                    if (pre < c.cap && c.passive < c.cap)
                    {
                        NoteSniffWant(ref wantFor, ref wantWhy, ref wantDist, ref wantAppetite, c, "promotable");
                    }
                }
            }

            for (int i = 0; i < tracker.CreaturesCount; i++)
            {
                var rep = tracker.GetRep(i);
                var st = rep?.dynamicRelationship?.state as FoxTrackedState;
                if (st == null || st.scentChannel == null || st.scentSweep == scentSweep) continue;
                ColdScent(rep.representedCreature, rep, st, "left_room", -1f, "");
            }

            MaybeRequestSniff(p, now, wantFor, wantWhy, wantDist, wantAppetite);
        }

        private CreatureTemplate.Relationship ScentRelationship(
            AbstractCreature other, Tracker.CreatureRepresentation rep, FoxBreedParams p, out string src)
        {
            if (rep != null && rep.dynamicRelationship != null)
            {
                src = "dynamic";
                return rep.dynamicRelationship.currentRelationship;
            }

            CreatureTemplate.Relationship rel = StaticRelationship(other);
            if (other.state != null && !other.state.alive)
            {
                string rule;
                rel = CarrionRelationship(rel, p, out rule);
                src = "carrion_" + rule;
                return rel;
            }
            if (p.RepEnabled && other.creatureTemplate.type == CreatureTemplate.Type.Slugcat)
            {
                string band;
                ReputationTerms unused;
                rel = ReputationOutcome(other, rel, p, out band, out unused);
                src = "reputation_" + band;
                return rel;
            }
            src = "static";
            return rel;
        }

        private string OmniClaim(FoxBreedParams p, AbstractCreature other, FoxTrackedState st,
                                 bool los, float dist, float blindFac, out float omniCap)
        {
            string held = st != null ? st.omniChannel : null;
            if (held == null)
            {
                CreatureTemplate.Relationship sr = StaticRelationship(other);
                bool nearWould = !(sr.type == DoesntTrack || sr.type == Ignores || sr.intensity <= 0f)
                                 && (los || p.SenseThroughWalls)
                                 && dist <= (los ? p.SenseRadius : p.SenseRadius * p.SenseWallRadiusFac) * blindFac;
                if (nearWould) held = los ? "omni_pending" : "omni_wall_pending";
            }
            if (held == null)
            {
                omniCap = 0f;
                return null;
            }
            omniCap = (los ? p.SenseMaxScore : p.SenseMaxScore * p.SenseWallScoreFac) * blindFac;
            return held;
        }

        private static void NoteSniffWant(ref AbstractCreature wantFor, ref string wantWhy, ref float wantDist,
                                          ref float wantAppetite, ScentCandidate c, string why)
        {
            if (wantFor != null && (c.appetite < wantAppetite
                                    || (c.appetite == wantAppetite && c.dist >= wantDist))) return;
            wantFor = c.other;
            wantWhy = why;
            wantDist = c.dist;
            wantAppetite = c.appetite;
        }

        private void TouchScent(FoxTrackedState st)
        {
            if (st != null && st.scentChannel != null) st.scentSweep = scentSweep;
        }

        private void MarkScent(ref ScentCandidate c, string evt, float pre, int sniffSerial, int seed)
        {
            FoxTrackedState st = c.st;
            string was = st != null ? st.scentChannel : null;
            if (st != null)
            {
                st.scentSweep = scentSweep;
                st.scentChannel = c.channel;
            }
            int now = GameTick;
            if (evt == "refreshed" && st != null && was == c.channel
                && now - st.scentLastLogTick < ScentRefreshLogTicks)
            {
                return;
            }
            if (st != null)
            {
                st.scentLastLogTick = now;
            }
        }

        private void ColdScent(AbstractCreature other, Tracker.CreatureRepresentation rep, FoxTrackedState st,
                               string reason, float dist, string detail)
        {
            if (st == null || st.scentChannel == null) return;
            st.scentChannel = null;
            st.scentSweep = scentSweep;
        }

        private void PollSniffSeam(FoxBreedParams p, int now)
        {
            Fox.ScentSniffSeam s = fox.ScentSniff;
            int cooldown = Math.Max(0, p.ScentSniffCooldownTicks);

            if (s.ClosedSerial > sniffSeenClosed)
            {
                sniffSeenClosed = s.ClosedSerial;
                sniffCooldownUntil = now + cooldown;
                if (s.ClosedCompleted)
                {
                    sniffArmedSerial = s.ClosedSerial;
                }
            }

            if (!s.Open)
            {
                sniffNoGfxTicks = 0;
                sniffUnstartedTicks = 0;
                return;
            }

            if (fox.graphicsModule == null)
            {
                sniffUnstartedTicks = 0;
                if (++sniffNoGfxTicks >= Math.Max(1, p.ScentSniffTicks))
                {
                    s.WithdrawnSerial = s.RequestSerial;
                    sniffNoGfxTicks = 0;
                    sniffArmedSerial = s.RequestSerial;
                    sniffCooldownUntil = now + cooldown;
                }
                return;
            }

            sniffNoGfxTicks = 0;
            if (s.StartedSerial == s.RequestSerial)
            {
                sniffUnstartedTicks = 0;
                return;
            }
            if (++sniffUnstartedTicks >= SniffStaleFactor * Math.Max(1, p.ScentSniffTicks))
            {
                s.WithdrawnSerial = s.RequestSerial;
                sniffUnstartedTicks = 0;
                sniffCooldownUntil = now + cooldown;
            }
        }

        private void MaybeRequestSniff(FoxBreedParams p, int now, AbstractCreature wantFor, string wantWhy,
                                       float wantDist, float wantAppetite)
        {
            if (wantFor == null) return;
            Fox.ScentSniffSeam s = fox.ScentSniff;
            if (s.Open || sniffArmedSerial != 0 || now < sniffCooldownUntil) return;

            s.RequestSerial++;
        }

        private void UpdateAppetite(FoxBreedParams p)
        {
            var ut = utilityComparer?.GetUtilityTracker(preyTracker);
            if (ut == null) return;
            ut.weight = p.WeightPrey * Mathf.Lerp(1f, p.StarveAggressionFac, Mathf.Clamp01(fox.Hunger));
        }

        private Behavior StalkOrHunt(FoxBreedParams p, Behavior current, Behavior previous)
        {
            var prey = preyTracker.MostAttractivePrey;
            Creature realized = prey?.representedCreature.realizedCreature;
            if (prey == null || realized == null || realized.room != fox.room)
            {
                fox.StalkCrouch = 0f;
                return current;
            }

            float dist = Vector2.Distance(fox.mainBodyChunk.pos, realized.mainBodyChunk.pos);
            float preySpeed = realized.mainBodyChunk.vel.magnitude;

            if (!ReferenceEquals(realized.abstractCreature, stalkLatchedOn)) stalkLatched = false;

            if (previous == Behavior.Stalk)
            {
                if (dist < p.StalkBreakDistance)
                {
                    fox.StalkCrouch = 0f;
                    return Behavior.Hunt;
                }
                if (preySpeed > p.StalkPreySpeedBreak)
                {
                    stalkLatched = true;
                    stalkLatchedOn = realized.abstractCreature;
                    fox.StalkCrouch = 0f;
                    return Behavior.Hunt;
                }
                return Behavior.Stalk;
            }

            if (dist < p.StalkDistance && dist > p.StalkBreakDistance
                && prey.VisualContact && !stalkLatched && preySpeed <= p.StalkPreySpeedBreak)
            {
                return Behavior.Stalk;
            }

            fox.StalkCrouch = 0f;
            return current;
        }

        private Behavior EatOrHunt(FoxBreedParams p, Behavior current)
        {
            Creature target = eatTarget;
            if (target == null)
            {
                var prey = preyTracker.MostAttractivePrey;
                target = prey?.representedCreature.realizedCreature;
            }

            if (target == null || target.room != fox.room || !target.dead)
            {
                if (eatTarget != null) { ClearEat(); }
                return current;
            }
            if (fox.CanCarry(target))
            {
                if (eatTarget != null) { ClearEat(); }
                return current;
            }
            if (target.State != null && target.State.meatLeft <= 0)
            {
                if (eatTarget != null) { ClearEat(); }
                preyTracker.ForgetPrey(target.abstractCreature);
                return current;
            }

            float threat = threatTracker.Utility();
            if (threat > p.EatSafeThreat)
            {
                if (eatTarget != null)
                {
                    ClearEat();
                }
                return current;
            }

            float dist = Vector2.Distance(fox.mainBodyChunk.pos, target.mainBodyChunk.pos);
            if (dist > p.EatReach)
            {
                if (eatTarget != null)
                {
                    ClearEat();
                }
                return current;
            }

            if (eatTarget != target)
            {
                eatTarget = target;
                eatCounter = 0;
            }
            return Behavior.Eat;
        }

        private void UpdateEat(FoxBreedParams p)
        {
            if (eatTarget == null) return;
            fox.chaseTarget = null;

            eatCounter++;
            if (eatCounter < p.EatFrames) return;

            fox.FinishMeal(eatTarget, p);
            denRestCounter = p.DenRestFrames;
            ClearEat();
        }

        private void ClearEat()
        {
            eatTarget = null;
            eatCounter = 0;
        }

        public void Fed(int restFrames)
        {
            denRestCounter = restFrames;
        }

        public override PathCost TravelPreference(MovementConnection connection, PathCost cost)
        {
            cost = base.TravelPreference(connection, cost);

            Room room = fox != null ? fox.RoomRef : null;
            if (room == null || room.aimap == null) return cost;

            FoxBreedParams p = fox.P;

            if (p.RouteObstacleEnabled && obstacleTracker != null)
            {
                int warnings = obstacleTracker.ObstacleWarning(connection);
                if (warnings > 0)
                {
                    float clamped = Mathf.Min(warnings, OBSTACLE_WARNING_CLAMP);
                    cost += new PathCost(
                        Mathf.Pow(clamped, p.RouteObstacleExponent) * p.RouteObstacleWeight,
                        PathCost.Legality.Allowed);
                }
            }

            string holeKind;
            if (p.RouteHoleAvoid && IsCrossedFloorHole(room, p, connection, out holeKind))
            {
                bool throughCorridor = string.Equals(holeKind, "slot_drop", StringComparison.Ordinal)
                                       || string.Equals(holeKind, "slot_through", StringComparison.Ordinal);
                float penalty = throughCorridor ? p.RouteCorridorPenalty : p.RouteHolePenalty;
                PathCost.Legality legality = throughCorridor
                    ? PathCost.Legality.Allowed
                    : ParseHoleLegality(p.RouteHoleLegality);
                cost += new PathCost(penalty, legality);
            }

            if (p.ClimbsPoles && !p.PoleHorizontalAllowed
                && IsHorizontalOnlyPole(room, connection.destinationCoord))
            {
                cost += new PathCost(0f, PathCost.Legality.Unallowed);
            }

            return cost;
        }

        internal PathCost TravelPreferenceForQuery(MovementConnection connection, PathCost cost)
        {
            int prev = rpStep;
            rpStep = 0;
            try { return TravelPreference(connection, cost); }
            finally { rpStep = prev; }
        }

        internal static bool IsHorizontalOnlyPole(Room room, WorldCoordinate c)
        {
            if (!c.TileDefined || c.room != room.abstractRoom.index) return false;
            Room.Tile tile = room.GetTile(c.x, c.y);
            if (!tile.horizontalBeam || tile.verticalBeam) return false;
            AItile ai = room.aimap.getAItile(c.x, c.y);
            return ai != null && ai.acc == AItile.Accessibility.Climb;
        }

        private const int OBSTACLE_WARNING_CLAMP = 3;

        private bool IsCrossedFloorHole(Room room, FoxBreedParams p, MovementConnection connection,
                                        out string kind)
        {
            kind = "none";

            if (connection.type != MovementConnection.MovementType.Standard)
            {
                kind = "not_standard";
                return false;
            }

            WorldCoordinate dest = connection.destinationCoord;
            if (!dest.TileDefined || dest.room != room.abstractRoom.index) { return false; }

            int dx = dest.x, dy = dest.y;
            if (dx <= 0 || dx >= room.TileWidth - 1 || dy <= 0 || dy >= room.TileHeight - 1)
            { return false; }

            Room.Tile at = room.GetTile(dx, dy);
            if (at.Solid) { return false; }

            if (room.GetTile(dx, dy + 1).Solid) { return false; }

            WorldCoordinate goal = pathFinder.GetDestination;
            bool wallLeft = room.GetTile(dx - 1, dy).Solid;
            bool wallRight = room.GetTile(dx + 1, dy).Solid;

            if (wallLeft && wallRight)
            {
                if (room.GetTile(dx - 1, dy + 1).Solid || room.GetTile(dx + 1, dy + 1).Solid)
                {
                    kind = "shaft_not_hole";
                    return false;
                }

                int scan = Math.Max(1, p.RouteHoleProbeTiles) + 1;
                int depth = 0;
                bool bottom = false;
                for (int i = 1; i <= scan; i++)
                {
                    if (room.GetTile(dx, dy - i).Solid) { bottom = true; break; }
                    depth++;
                }
                lastHoleOpenRow = dy - depth;
                bool leadsSideways = false;
                if (bottom && p.RouteCorridorDetectSideways)
                {
                    int openRow = lastHoleOpenRow;
                    leadsSideways = !room.GetTile(dx - 1, openRow).Solid
                                    || !room.GetTile(dx + 1, openRow).Solid;
                }
                kind = !bottom ? "slot_drop" : (leadsSideways ? "slot_through" : "slot_pit");

                if (goal.room == dest.room && goal.TileDefined && goal.x == dx && goal.y <= dy)
                {
                    kind = "route_goes_in";
                    return false;
                }

                return true;
            }

            Room.Tile below = room.GetTile(dx, dy - 1);
            if (below.Terrain != Room.Tile.TerrainType.ShortcutEntrance)
            {
                if (!below.Solid && room.GetTile(dx - 1, dy - 1).Solid && room.GetTile(dx + 1, dy - 1).Solid)
                {
                    kind = "bridge_not_demoted";
                }
                return false;
            }

            if (!room.GetTile(dx - 1, dy - 1).Solid || !room.GetTile(dx + 1, dy - 1).Solid)
            {
                kind = "wall_pipe";
                return false;
            }

            bool pipeMouth = true;
            kind = "floor_pipe";

            if (goal.room == dest.room && goal.TileDefined)
            {
                if (goal.x == dx && goal.y <= dy) { kind = "route_goes_in"; return false; }
            }

            if (pipeMouth && goal.room != dest.room)
            {
                kind = "pipe_transit";
                return false;
            }
            return true;
        }

        private int rpStep = 1;

        private int lastHoleOpenRow = -1;

        private static PathCost.Legality ParseHoleLegality(string key)
        {
            if (string.Equals(key, "allowed", StringComparison.OrdinalIgnoreCase))
                return PathCost.Legality.Allowed;
            if (string.Equals(key, "unallowed", StringComparison.OrdinalIgnoreCase))
                return PathCost.Legality.Unallowed;
            return PathCost.Legality.Unwanted;
        }

        private void FeedObstacleTracker(FoxBreedParams p)
        {
            if (!p.RouteObstacleEnabled || obstacleTracker == null || stuckTracker == null) return;

            bool stuck = stuckTracker.Utility() > 0f;
            if (!stuck)
            {
                if (obstacleReporting)
                {
                    obstacleReporting = false;
                }
                return;
            }

            if (p.RouteObstacleSkipWhenWedged || p.RouteObstacleEscapeMuteTicks > 0)
            {
                bool wedged = p.RouteObstacleSkipWhenWedged && fox.WedgeActive;
                int sinceEscape = GameTick - fox.WedgeEscapeTick;
                bool muted = p.RouteObstacleEscapeMuteTicks > 0
                             && sinceEscape >= 0 && sinceEscape < p.RouteObstacleEscapeMuteTicks;
                if (wedged || muted)
                {
                    if (!obstacleMuted)
                    {
                        obstacleMuted = true;
                    }
                    return;
                }
                if (obstacleMuted)
                {
                    obstacleMuted = false;
                }
            }

            if (GameTick % OBSTACLE_REPORT_EVERY_TICKS != 0) return;

            StandardPather pather = pathFinder as StandardPather;
            if (pather == null) return;

            MovementConnection blocked = FoxPathTieBreak.FollowPathUnfiltered(pather, creature.pos, false);
            if (blocked == default(MovementConnection)) return;

            obstacleTracker.ReportMovementFailure(blocked, null);

            if (!obstacleReporting)
            {
                obstacleReporting = true;
            }
        }

        private const int OBSTACLE_REPORT_EVERY_TICKS = 10;

        public override void NewRoom(Room room)
        {
            base.NewRoom(room);
            if (pounceRouter != null) pounceRouter.NewRoom();
            if (bait != null) bait.NewRoom();
        }

        public override bool WantToStayInDenUntilEndOfCycle()
        {
            FoxBreedParams p = fox.P;
            if (rainTracker.Utility() > p.DenRainUtilityThreshold) return true;
            return creature.state is HealthState hs && hs.health < p.DenHealthThreshold;
        }

        bool IUseItemTracker.TrackItem(AbstractPhysicalObject obj)
        {
            return bait != null && bait.TrackItem(obj);
        }

        void IUseItemTracker.SeeThrownWeapon(PhysicalObject obj, Creature thrower)
        {
        }

        public override Tracker.CreatureRepresentation CreateTrackerRepresentationForCreature(AbstractCreature otherCreature)
        {
            if (fox.P.ConspecificAwareness && FoxCritob.IsFoxType(otherCreature.creatureTemplate.type))
            {
                return new Tracker.ElaborateCreatureRepresentation(tracker, otherCreature, 1f, 3);
            }

            return otherCreature.creatureTemplate.smallCreature
                ? (Tracker.CreatureRepresentation)new Tracker.SimpleCreatureRepresentation(tracker, otherCreature, 0.5f, false)
                : new Tracker.ElaborateCreatureRepresentation(tracker, otherCreature, 1f, 3);
        }

        public enum RelRoute
        {
            Inert = 0,
            Prey,
            Threat,
            Discomfort
        }

        public static RelRoute RouteFor(CreatureTemplate.Relationship.Type type, FoxBreedParams p)
        {
            if (type == Eats) return RelRoute.Prey;
            if (type == Afraid) return RelRoute.Threat;
            if (type == AgressiveRival) return RelRoute.Threat;
            if (type == Attacks)
            {
                return IsRouteKey(p == null ? null : p.RelRouteAttacks, "threat")
                    ? RelRoute.Threat : RelRoute.Prey;
            }
            if (type == SocialDependent)
            {
                return IsRouteKey(p == null ? null : p.RelRouteSocialDependent, "inert")
                    ? RelRoute.Inert : RelRoute.Prey;
            }
            if (type == Uncomfortable)
            {
                return (p == null || p.DiscomfortEnabled) ? RelRoute.Discomfort : RelRoute.Inert;
            }
            return RelRoute.Inert;
        }

        internal static bool IsRouteKey(string value, string want)
        {
            return value != null && value.Trim().Equals(want, StringComparison.OrdinalIgnoreCase);
        }

        AIModule IUseARelationshipTracker.ModuleToTrackRelationship(CreatureTemplate.Relationship relationship)
        {
            switch (RouteFor(relationship.type, fox == null ? null : fox.P))
            {
                case RelRoute.Prey: return preyTracker;
                case RelRoute.Threat: return threatTracker;
                default: return null;
            }
        }

        public class FoxTrackedState : RelationshipTracker.TrackedCreatureState
        {
            public bool sawItLastTick;

            public string omniChannel;

            public int omniSweep;

            public string scentChannel;
            public int scentSweep;
            public int scentLastLogTick;

            public string carrionRule;
        }

        RelationshipTracker.TrackedCreatureState IUseARelationshipTracker.CreateTrackedCreatureState(RelationshipTracker.DynamicRelationship rel)
        {
            return new FoxTrackedState();
        }

        CreatureTemplate.Relationship IUseARelationshipTracker.UpdateDynamicRelationship(RelationshipTracker.DynamicRelationship dRelation)
        {
            bool visual = dRelation.trackerRep.VisualContact;
            if (visual)
            {
                dRelation.state.alive = dRelation.trackerRep.representedCreature.state.alive;
            }

            FoxBreedParams p = fox.P;
            CreatureTemplate.Relationship rel;
            string carrionRule = null;
            if (dRelation.state.alive)
            {
                rel = StaticRelationship(dRelation.trackerRep.representedCreature);
            }
            else
            {
                rel = CarrionRelationship(
                    StaticRelationship(dRelation.trackerRep.representedCreature), p, out carrionRule);
            }

            FoxTrackedState st = dRelation.state as FoxTrackedState;

            if (carrionRule != null && st != null && st.carrionRule != carrionRule)
            {
                st.carrionRule = carrionRule;
                AbstractCreature corpse = dRelation.trackerRep.representedCreature;
                StaticRelationship(corpse);
            }

            if (p.RepEnabled
                && dRelation.state.alive
                && dRelation.trackerRep.representedCreature.creatureTemplate.type == CreatureTemplate.Type.Slugcat)
            {
                rel = ApplyReputation(dRelation, rel, st, p);
            }

            if (st != null && st.sawItLastTick != visual)
            {
                st.sawItLastTick = visual;
            }

            return rel;
        }

        private CreatureTemplate.Relationship CarrionRelationship(
            CreatureTemplate.Relationship live, FoxBreedParams p, out string rule)
        {
            if (p == null || !p.EatsCarrion)
            {
                rule = "disabled";
                return new CreatureTemplate.Relationship(Ignores, 0f);
            }
            if (live.type == Eats)
            {
                rule = "eats";
                return new CreatureTemplate.Relationship(Eats, live.intensity * p.CarrionIntensityFac);
            }
            if (p.CarrionFromAllPrey && RouteFor(live.type, p) == RelRoute.Prey)
            {
                rule = "prey_route";
                return new CreatureTemplate.Relationship(Eats, live.intensity * p.CarrionIntensityFac);
            }
            rule = p.CarrionFromAllPrey ? "not_prey" : "not_eats";
            return new CreatureTemplate.Relationship(Ignores, 0f);
        }

        public override bool TrackerToDiscardDeadCreature(AbstractCreature crit)
        {
            bool discard = base.TrackerToDiscardDeadCreature(crit);
            if (!discard || crit == null) return discard;

            FoxBreedParams p = (fox == null) ? null : fox.P;
            if (p == null || !p.EatsCarrion || !p.CarrionFromAllPrey) return true;

            string rule;
            CreatureTemplate.Relationship carrion = CarrionRelationship(StaticRelationship(crit), p, out rule);
            if (carrion.type != Eats) return true;

            if (crit.ID.number != carrionKeepLastId || GameTick - carrionKeepLastTick >= CarrionKeepLogCooldown)
            {
                carrionKeepLastId = crit.ID.number;
                carrionKeepLastTick = GameTick;
            }
            return false;
        }

        private const int CarrionKeepLogCooldown = 400;

        private int carrionKeepLastId = int.MinValue;
        private int carrionKeepLastTick = int.MinValue / 2;

        private CreatureTemplate.Relationship ApplyReputation(
            RelationshipTracker.DynamicRelationship dRelation,
            CreatureTemplate.Relationship staticRel,
            FoxTrackedState st,
            FoxBreedParams p)
        {
            AbstractCreature player = dRelation.trackerRep.representedCreature;

            string band;
            ReputationTerms t;
            CreatureTemplate.Relationship outRel = ReputationOutcome(player, staticRel, p, out band, out t);

            return outRel;
        }

        private struct ReputationTerms
        {
            public float community, communityRaw, personality, memory, like;
            public string communitySrc, memorySrc;
            public bool forced;
        }

        private CreatureTemplate.Relationship ReputationOutcome(
            AbstractCreature player,
            CreatureTemplate.Relationship staticRel,
            FoxBreedParams p,
            out string band,
            out ReputationTerms terms)
        {
            string communitySrc;
            float communityRaw = CommunityLikeOfPlayer(player, out communitySrc);
            float community = Mathf.Clamp(communityRaw * p.RepCommunityWeight, -1f, 1f);

            float personality = Mathf.Clamp(repRoll * p.RepPersonalityAmount, -1f, 1f);

            SocialMemory sm = (creature != null && creature.state != null) ? creature.state.socialMemory : null;
            float memory = (sm == null) ? 0f : Mathf.Clamp(sm.GetTempLike(player.ID) * p.RepMemoryWeight, -1f, 1f);
            string memorySrc = (sm == null) ? "absent" : (p.RepMemoryFromAttacks ? "ok" : "ok_no_writer");

            float like = community;
            like = Mathf.Lerp(like, personality, Mathf.Abs(personality));
            like = Mathf.Lerp(like, memory, Mathf.Abs(memory));

            bool forced = p.RepDebugLike >= -1f && p.RepDebugLike <= 1f;
            if (forced) like = p.RepDebugLike;

            CreatureTemplate.Relationship outRel = staticRel;
            if (RouteFor(staticRel.type, p) != RelRoute.Prey)
            {
                band = "inert_static_" + staticRel.type;
            }
            else if (like <= p.RepHostileThreshold)
            {
                float t = Mathf.InverseLerp(p.RepHostileThreshold, -1f, like);
                t = Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.01f, p.RepAggressionExponent));
                outRel = new CreatureTemplate.Relationship(staticRel.type, Mathf.Lerp(staticRel.intensity, 1f, t));
                band = "hostile";
            }
            else if (like >= p.RepFriendlyThreshold)
            {
                outRel = new CreatureTemplate.Relationship(
                    p.RepFriendlyStayOutOfWay ? StayOutOfWay : Ignores, 0f);
                band = "friendly";
            }
            else
            {
                band = "neutral";
            }

            terms = new ReputationTerms
            {
                community = community,
                communityRaw = communityRaw,
                communitySrc = communitySrc,
                personality = personality,
                memory = memory,
                memorySrc = memorySrc,
                like = like,
                forced = forced,
            };
            return outRel;
        }

        private float CommunityLikeOfPlayer(AbstractCreature player, out string src)
        {
            src = "ok";
            if (player == null) { src = "no_player"; return 0f; }
            PlayerState ps = player.state as PlayerState;
            if (ps == null) { src = "not_player_state"; return 0f; }
            if (creature == null || creature.world == null || creature.world.game == null
                || creature.world.game.session == null) { src = "no_session"; return 0f; }

            CreatureCommunities cc = creature.world.game.session.creatureCommunities;
            if (cc == null) { src = "no_communities"; return 0f; }
            CreatureCommunities.CommunityID id = creature.creatureTemplate.communityID;
            if (id == null || id.Index <= 0) { src = "no_community_id"; return 0f; }

            float[,,] op = cc.playerOpinions;
            if (op == null) { src = "no_opinions"; return 0f; }
            int region = (cc.session is ArenaGameSession) ? -1 : creature.world.RegionNumber;
            int playerNumber = (cc.session is StoryGameSession) ? 0 : ps.playerNumber;
            if (id.Index - 1 >= op.GetLength(0)) { src = "community_unsized"; return 0f; }
            if (region + 1 < 0 || region + 1 >= op.GetLength(1)) { src = "region_oob"; return 0f; }
            if (playerNumber < 0 || playerNumber >= op.GetLength(2)) { src = "player_oob"; return 0f; }

            return cc.LikeOfPlayer(id, creature.world.RegionNumber, ps.playerNumber);
        }

        private void UpdateSocialMemoryDecay(FoxBreedParams p)
        {
            if (!p.RepEnabled || p.RepMemoryDecay <= 0f) return;
            SocialMemory sm = (creature != null && creature.state != null) ? creature.state.socialMemory : null;
            if (sm == null) return;
            sm.EvenOutAllTemps(p.RepMemoryDecay);
        }

        void IReactToSocialEvents.SocialEvent(SocialEventRecognizer.EventID ID, Creature subjectCrit,
                                              Creature objectCrit, PhysicalObject involvedItem)
        {
            FoxBreedParams p = fox.P;
            if (!p.RepEnabled || !p.RepMemoryFromAttacks) return;
            if (subjectCrit == null || objectCrit == null) return;
            if (!(subjectCrit is Player)) return;
            if (objectCrit != fox) return;

            float severity;
            if (ID == SocialEventRecognizer.EventID.NonLethalAttackAttempt) severity = 0.05f;
            else if (ID == SocialEventRecognizer.EventID.NonLethalAttack) severity = 0.15f;
            else if (ID == SocialEventRecognizer.EventID.LethalAttackAttempt) severity = 0.35f;
            else if (ID == SocialEventRecognizer.EventID.LethalAttack) severity = 1f;
            else return;

            SocialMemory sm = (creature != null && creature.state != null) ? creature.state.socialMemory : null;
            if (sm == null)
            {
                return;
            }

            float change = -severity * p.RepMemoryEventScale;
            SocialMemory.Relationship r = sm.GetOrInitiateRelationship(subjectCrit.abstractCreature.ID);
            r.InfluenceTempLike(change * 1.5f);
            r.InfluenceLike(change * 0.75f);
            r.InfluenceKnow(Mathf.Abs(change) * 0.25f);
        }
    }
}
