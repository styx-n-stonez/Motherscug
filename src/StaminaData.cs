using System.Collections.Generic;
using RWCustom;
using SlugBase.SaveData;
using UnityEngine;

namespace MotherMod
{
    public class StaminaData
    {
        public float stamina;
        public bool exhausted = false;
        public readonly MotherConfig config;

        private MotherConfig live;

        private Player.AnimationIndex lastAnimation = Player.AnimationIndex.None;
        private int rechargeDelayTimer;
        private int framesSinceJump = 99999;
        private int chainCount;
        private bool dangerouslyExhausted;
        private int survivalTimer;
        private bool pendingActDeath;
        private bool wasMovingWhileCollapsed;

        private bool forcedStun;
        private int stunFramesRemaining;
        private CreatureSpasmer spasmer;

        private int earlyBirthCount;
        private bool earlyBirthTriggered;

        private string emptyDeferReason;

        private string activeDrainSig;
        private float activeDrainRate;
        private int vineGraceFrames;
        private string activeGainTag;
        private float activeGainRate;
        private bool wasZeroG;

        private Room cachedOracleRoom;
        private readonly List<Oracle> cachedOracles = new List<Oracle>();
        private int oracleSweepTimer;
        private const int ORACLE_SWEEP_FRAMES = 400;

        private string sigLocoTag;
        private int sigCarryTier;
        private bool sigPup;
        private string cachedSig;

        public bool suppressWallJumpDrain;

        private int feelTier;
        private ChunkSoundEmitter heartEmitter;
        private ChunkSoundEmitter breathEmitter;
        private string heartPlayingName;
        private string breathPlayingName;
        private int lastSlowStep = -1;
        private int hitCooldownFrames;
        private static readonly Dictionary<string, SoundID> soundCache = new Dictionary<string, SoundID>();

        private bool heartLockedToAudio;
        private string heartClipLogged;
        private static readonly HashSet<string> warnedSounds = new HashSet<string>();

        public float HeartPhase { get; private set; }

        public float OverexertDepth => exhausted
            ? Mathf.Clamp01(1f - stamina / Mathf.Max(1f, config.OverexertEnter))
            : 0f;

        public float SpeedMult => OverexertSlowActive
            ? Mathf.Lerp(config.OverexertSpeedMult, config.OverexertSpeedMultMin, OverexertDepth)
            : 1f;

        public float ThrowMult => (config.OverexertSlowThrow && OverexertSlowActive) ? SpeedMult : 1f;

        private const int VINE_GRACE_FRAMES = 10;

        public bool DangerouslyExhausted => dangerouslyExhausted || forcedStun;

        public bool ForcedStunActive => forcedStun;

        public bool OverexertSlowActive => exhausted && !dangerouslyExhausted && !forcedStun;

        public float ExhaustionIntensity
        {
            get
            {
                if (forcedStun) return 1f;
                if (!dangerouslyExhausted) return 0f;
                float timeDanger = 1f - (float)survivalTimer / Mathf.Max(1, live.ExhaustionSurvivalTime);
                float staminaDanger = 1f - Mathf.Clamp01(stamina / Mathf.Max(0.0001f, live.ExhaustionExit));

                return Mathf.Clamp01(live.IntensityTimeWeight * timeDanger + live.IntensityStaminaWeight * staminaDanger);
            }
        }

        public StaminaData(MotherConfig config)
        {
            this.config = config;
            this.live = config;
            this.stamina = config.StaminaMax;
        }

        private static float PerFrame(float ratePerSecond) => ratePerSecond / MotherConfig.TICKS_PER_SECOND;

        public void Update(Player self, bool boostSwimEdge)
        {
            if (self.dead)
            {
                StopContinuousLogs("death");

                if (emptyDeferReason != null)
                {
                    Plugin.Log("Exh2", $"stun deferral cleared reason=death mode={config.ExhaustionEmptyMode}");
                    emptyDeferReason = null;
                }

                if (forcedStun || dangerouslyExhausted)
                {
                    forcedStun = false;
                    dangerouslyExhausted = false;
                    if (spasmer != null && !spasmer.slatedForDeletetion) spasmer.Destroy();
                    spasmer = null;
                    Plugin.Log("Exh2", "danger state cleared reason=death");
                }

                StopFeelAudio("death");
                feelTier = 0;
                HeartPhase = 0f;
                return;
            }

            if (cachedOracleRoom != null && self.room != cachedOracleRoom)
            {
                cachedOracleRoom = null;
                cachedOracles.Clear();
            }

            if (Plugin.Stamina.TryGet(self, out var lc)) live = lc;
            Plugin.DebugLogging = live.Debug;

            if (hitCooldownFrames > 0) hitCooldownFrames--;

            UpdateFeel(self);

            if (forcedStun)
            {
                UpdateForcedStun(self);
                return;
            }

            bool zeroG = self.EffectiveRoomGravity < config.ZeroGThreshold;
            if (zeroG != wasZeroG)
            {
                Plugin.Log("Exh2", $"zero-g {(zeroG ? "enter" : "leave")} gravity={self.EffectiveRoomGravity:0.###} threshold={config.ZeroGThreshold}");
                wasZeroG = zeroG;
            }

            if (boostSwimEdge) DrainAction("boostSwim", config.BoostSwimCost, isAct: false);

            var anim = self.animation;
            if (anim == Player.AnimationIndex.BellySlide && lastAnimation != Player.AnimationIndex.BellySlide)
                DrainAction("slide", config.SlideCost, isAct: true);

            if (anim == Player.AnimationIndex.Roll && lastAnimation != Player.AnimationIndex.Roll
                && dangerouslyExhausted)
                DrainAction("roll", 0f, isAct: true);
            lastAnimation = anim;

            if (framesSinceJump < 100000) framesSinceJump++;

            if (!exhausted && stamina <= config.OverexertEnter)
            {
                exhausted = true;
                Plugin.Log("Exh2", $"zone enter stamina={stamina:0.#} enter={config.OverexertEnter} " +
                                   $"speedMult={config.OverexertSpeedMult}->{config.OverexertSpeedMultMin} " +
                                   $"slow[climb={config.OverexertSlowClimb} swim={config.OverexertSlowSwim} throw={config.OverexertSlowThrow}]");
            }
            else if (exhausted && stamina >= config.OverexertExit)
            {
                exhausted = false;
                Plugin.Log("Exh2", $"zone leave stamina={stamina:0.#} exit={config.OverexertExit}");
            }

            if (dangerouslyExhausted)
            {
                for (int i = 0; i < self.bodyChunks.Length; i++)
                    self.bodyChunks[i].vel *= config.ExhaustionMovePenalty;

                survivalTimer--;

                bool movingWhileCollapsed = self.input[0].x != 0 || self.input[0].y != 0;
                if (movingWhileCollapsed)
                {
                    stamina = Mathf.Max(0f, stamina - config.ExhaustionMoveDrainRate);
                }
                else
                {
                    stamina = Mathf.Min(config.StaminaMax, stamina + config.ExhaustionRecoveryRate);
                }

                if (movingWhileCollapsed != wasMovingWhileCollapsed)
                {
                    if (movingWhileCollapsed)
                        Plugin.Log("Exhaustion", $"collapse-move start action=move drain={config.ExhaustionMoveDrainRate} stamina={stamina:0.#}");
                    else
                        Plugin.Log("Exhaustion", $"collapse-move stop reason=holding-still stamina={stamina:0.#}");
                    wasMovingWhileCollapsed = movingWhileCollapsed;
                }

                ApplyShake(self, ExhaustionIntensity * live.ShakeMax);

                if (stamina >= config.ExhaustionExit)
                {
                    dangerouslyExhausted = false;
                    pendingActDeath = false;
                    wasMovingWhileCollapsed = false;
                    Plugin.Log("Exhaustion", $"survive stand-up stamina={stamina:0.#} exit={config.ExhaustionExit} survivalTimer={survivalTimer}");
                }
                else if (pendingActDeath || survivalTimer <= 0)
                {
                    dangerouslyExhausted = false;
                    Plugin.Log("Exhaustion", $"death path={(pendingActDeath ? "act-while-empty" : "timeout")} survivalTimer={survivalTimer} stamina={stamina:0.#}");
                    pendingActDeath = false;
                    wasMovingWhileCollapsed = false;
                    self.Die();
                }
                return;
            }

            UpdateContinuousDrains(self, suspended: zeroG || self.inShortcut);

            if (stamina <= 0f && !zeroG)
            {
                string defer = self.inShortcut ? "shortcut"
                    : (config.ExhaustionEmptyMode != "collapse" && self.submerged) ? "submerged"
                    : null;
                if (defer != null)
                {
                    if (emptyDeferReason != defer)
                    {
                        if (emptyDeferReason != null)
                            Plugin.Log("Exh2", $"stun deferral cleared reason=gating-changed mode={config.ExhaustionEmptyMode}");
                        Plugin.Log("Exh2", $"stun deferred reason={defer} mode={config.ExhaustionEmptyMode} stamina={stamina:0.#}");
                        emptyDeferReason = defer;
                    }
                }
                else
                {
                    if (emptyDeferReason != null)
                    {
                        Plugin.Log("Exh2", "stun deferral cleared " +
                            $"reason={(emptyDeferReason == "submerged" ? "surfaced" : "exited-shortcut")} mode={config.ExhaustionEmptyMode}");
                        emptyDeferReason = null;
                    }

                    if (!earlyBirthTriggered) EnterEmptyState(self);
                    return;
                }
            }
            else if (emptyDeferReason != null)
            {
                Plugin.Log("Exh2", stamina > 0f
                    ? $"stun deferral cleared reason=recovered mode={config.ExhaustionEmptyMode} stamina={stamina:0.#}"
                    : $"stun deferral cleared reason=gating-changed mode={config.ExhaustionEmptyMode}");
                emptyDeferReason = null;
            }

            UpdatePassiveGains(self, zeroG);
        }

        private void EnterEmptyState(Player self)
        {
            earlyBirthCount++;
            int remaining = config.EarlyBirthLimit - earlyBirthCount;
            Plugin.Log("Exh2", $"early-birth counter={earlyBirthCount} limit={config.EarlyBirthLimit} remaining={remaining}");

            if (earlyBirthCount >= config.EarlyBirthLimit)
            {
                earlyBirthTriggered = true;
                TriggerEarlyBirth(self);
                return;
            }

            if (config.ExhaustionEmptyMode == "collapse")
            {
                dangerouslyExhausted = true;
                survivalTimer = config.ExhaustionSurvivalTime;
                pendingActDeath = false;
                wasMovingWhileCollapsed = false;
                StopContinuousLogs("suspended");
                Plugin.Log("Exhaustion", $"enter DANGEROUS exhaustion stamina={stamina:0.#} survivalTimer={survivalTimer}");
            }
            else
            {
                forcedStun = true;
                stunFramesRemaining = Mathf.RoundToInt(config.ExhaustionStunSeconds * MotherConfig.TICKS_PER_SECOND);

                framesSinceJump = 99999;
                chainCount = 0;
                StopContinuousLogs("suspended");
                Plugin.Log("Exh2", $"stun enter frames={stunFramesRemaining} feed={config.StunFeedPerFrame}");

                self.Stun(config.StunFeedPerFrame);
                UpdateForcedStun(self);
            }
        }

        private void UpdateForcedStun(Player self)
        {
            stunFramesRemaining--;

            if (self.stun < config.StunFeedPerFrame) self.stun = config.StunFeedPerFrame;

            if (self.room != null
                && (spasmer == null || spasmer.slatedForDeletetion || spasmer.room != self.room))
            {
                if (spasmer != null && !spasmer.slatedForDeletetion) spasmer.Destroy();
                spasmer = new CreatureSpasmer(self, allowDead: false, stunFramesRemaining);
                self.room.AddObject(spasmer);
            }

            ApplyShake(self, live.ShakeMax);

            if (stunFramesRemaining <= 0)
            {
                forcedStun = false;

                stamina = Mathf.Min(config.StaminaMax, config.PostStunRefill);
                rechargeDelayTimer = 0;
                if (spasmer != null && !spasmer.slatedForDeletetion) spasmer.Destroy();
                spasmer = null;
                Plugin.Log("Exh2", $"stun exit refill={stamina:0.#}");

                if (exhausted && stamina >= config.OverexertExit)
                {
                    exhausted = false;
                    Plugin.Log("Exh2", $"zone leave stamina={stamina:0.#} exit={config.OverexertExit}");
                }
            }
        }

        private void TriggerEarlyBirth(Player self)
        {
            Plugin.Log("Exh2", $"EARLY BIRTH triggered count={earlyBirthCount} limit={config.EarlyBirthLimit}");
            forcedStun = false;
            dangerouslyExhausted = false;

            var game = self.abstractCreature?.world?.game;
            if (game != null && game.IsStorySession)
            {
                var dpsd = game.GetStorySession.saveState?.deathPersistentSaveData;
                if (dpsd != null)
                {
                    dpsd.GetSlugBaseData().Set("motherEarlyBirth", true);
                    Plugin.Log("Exh2", "early-birth flag motherEarlyBirth=true (death-persistent)");
                }
                else
                {
                    Plugin.Log("Exh2", "early-birth flag skipped reason=no-death-persistent-data");
                }
            }
            else
            {
                Plugin.Log("Exh2", "early-birth flag skipped reason=not-story");
            }

            self.Die();
        }

        private void UpdateContinuousDrains(Player self, bool suspended)
        {
            string locoSig = null;
            int carryTier = 0;
            bool pupCarry = false;
            float rate = 0f;

            if (!suspended && self.room != null)
            {
                string locoTag = null;
                float locoRate = 0f;
                bool moving = false;

                var anim = self.animation;
                var body = self.bodyMode;
                bool inputDir = self.input[0].x != 0 || self.input[0].y != 0;

                if (anim == Player.AnimationIndex.Roll)
                {
                    locoTag = "roll"; locoRate = config.RollDrainPerSecond; moving = true;
                }
                else if (anim == Player.AnimationIndex.BellySlide || anim == Player.AnimationIndex.RocketJump)
                {
                }
                else if (anim == Player.AnimationIndex.VineGrab)
                {
                    bool climbing = self.SwimDir(normalize: false).magnitude > 0.1f;
                    locoTag = climbing ? "vineClimb" : "vineHold";
                    locoRate = climbing ? config.VineClimbDrainPerSecond : config.VineHoldDrainPerSecond;
                    moving = climbing;
                }
                else if (body == Player.BodyModeIndex.ClimbingOnBeam)
                {
                    bool climbing;
                    bool onBeam = true;
                    if (anim == Player.AnimationIndex.HangFromBeam || anim == Player.AnimationIndex.StandOnBeam)
                        climbing = self.input[0].x != 0;
                    else if (anim == Player.AnimationIndex.ClimbOnBeam)
                        climbing = self.input[0].y != 0;
                    else { climbing = false; onBeam = false; }

                    if (onBeam)
                    {
                        locoTag = climbing ? "poleClimb" : "poleHold";
                        locoRate = climbing ? config.PoleClimbDrainPerSecond : config.PoleHoldDrainPerSecond;
                        moving = climbing;
                    }
                }
                else if (anim == Player.AnimationIndex.DeepSwim)
                {
                    locoTag = "swim"; locoRate = config.SwimDrainPerSecond; moving = true;
                }
                else if (anim == Player.AnimationIndex.SurfaceSwim)
                {
                    bool paddling = self.input[0].x != 0 || self.input[0].jmp;
                    locoTag = paddling ? "swim" : "float";
                    locoRate = paddling ? config.SwimDrainPerSecond : config.FloatDrainPerSecond;
                    moving = paddling;
                }
                else if ((body == Player.BodyModeIndex.Default || body == Player.BodyModeIndex.Crawl)
                         && self.input[0].x != 0 && self.canJump > 0)
                {
                    locoTag = "walk"; locoRate = config.WalkDrainPerSecond; moving = true;
                }

                if (locoRate > 0f) { locoSig = locoTag; rate = locoRate; }

                if (moving)
                {
                    var held = self.grasps != null && self.grasps.Length > 0 ? self.grasps[0]?.grabbed : null;
                    if (held is Creature && !(held is Cicada))
                    {
                        bool heavy = self.HeavyCarry(held);
                        float carryRate = heavy ? config.CarryHeavyDrainPerSecond : config.CarryLightDrainPerSecond;
                        if (carryRate > 0f)
                        {
                            carryTier = heavy ? 2 : 1;
                            rate += carryRate;
                        }
                    }

                    bool pup = self.slugOnBack != null && self.slugOnBack.HasASlug && self.slugOnBack.slugcat.isSlugpup;
                    if (!pup && self.grasps != null)
                    {
                        for (int i = 0; i < self.grasps.Length; i++)
                        {
                            if (self.grasps[i]?.grabbed is Player p && p.isSlugpup) { pup = true; break; }
                        }
                    }
                    if (pup && config.SlugpupCarryDrainPerSecond > 0f)
                    {
                        pupCarry = true;
                        rate += config.SlugpupCarryDrainPerSecond;
                    }
                }
            }

            if (rate > 0f)
                stamina = Mathf.Max(0f, stamina - PerFrame(rate));

            LogDrainEdges(ComposeDrainSig(locoSig, carryTier, pupCarry), rate);
        }

        private string ComposeDrainSig(string locoTag, int carryTier, bool pup)
        {
            if (locoTag != sigLocoTag || carryTier != sigCarryTier || pup != sigPup)
            {
                sigLocoTag = locoTag;
                sigCarryTier = carryTier;
                sigPup = pup;
                if (locoTag == null && carryTier == 0 && !pup)
                {
                    cachedSig = null;
                }
                else
                {
                    string s = locoTag ?? "";
                    if (carryTier != 0)
                        s = (s.Length == 0 ? "" : s + "+") + (carryTier == 2 ? "carryHeavy" : "carryLight");
                    if (pup)
                        s = (s.Length == 0 ? "" : s + "+") + "pup";
                    cachedSig = s;
                }
            }
            return cachedSig;
        }

        private void LogDrainEdges(string sig, float rate)
        {
            if (sig == activeDrainSig)
            {
                vineGraceFrames = 0;
                if (sig != null && !Mathf.Approximately(rate, activeDrainRate))
                {
                    Plugin.Log("Exh2", $"drain-rate change source={sig} rate/s={rate:0.###} stamina={stamina:0.#}");
                    activeDrainRate = rate;
                }
                return;
            }

            bool wasVine = activeDrainSig != null && activeDrainSig.StartsWith("vine");
            if (sig == null && wasVine && vineGraceFrames < VINE_GRACE_FRAMES)
            {
                vineGraceFrames++;
                return;
            }

            if (activeDrainSig != null)
                Plugin.Log("Exh2", $"drain stop source={activeDrainSig} stamina={stamina:0.#}");
            if (sig != null)
                Plugin.Log("Exh2", $"drain start source={sig} rate/s={rate:0.###} stamina={stamina:0.#}");
            activeDrainSig = sig;
            activeDrainRate = rate;
            vineGraceFrames = 0;
        }

        private void StopContinuousLogs(string reason)
        {
            if (activeDrainSig != null)
            {
                Plugin.Log("Exh2", $"drain stop source={activeDrainSig} reason={reason} stamina={stamina:0.#}");
                activeDrainSig = null;
                activeDrainRate = 0f;
            }
            if (activeGainTag != null)
            {
                Plugin.Log("Exh2", $"gain stop source={activeGainTag} reason={reason} stamina={stamina:0.#}");
                activeGainTag = null;
            }
            vineGraceFrames = 0;
        }

        private void UpdatePassiveGains(Player self, bool zeroG)
        {
            string tag = null;
            float rate = 0f;

            if (self.stun != 0)
            {
            }
            else if (rechargeDelayTimer > 0)
            {
                rechargeDelayTimer--;
            }
            else if (activeDrainSig == null && self.input[0].x == 0 && self.input[0].y == 0 && !self.input[0].jmp)
            {
                if (zeroG)
                {
                    if (Custom.DistLess(self.mainBodyChunk.lastLastPos, self.mainBodyChunk.lastPos, 14f))
                    {
                        rate = config.StandGainPerSecond; tag = "stand";
                    }
                }
                else if (self.canJump > 0)
                {
                    rate = config.StandGainPerSecond; tag = "stand";

                    bool crouched = self.bodyMode == Player.BodyModeIndex.Crawl
                                    || (self.bodyMode == Player.BodyModeIndex.Default && !self.standing);
                    if (crouched && config.LayGainPerSecond > rate)
                    {
                        rate = config.LayGainPerSecond; tag = "lay";
                    }
                }

                if (config.IteratorGainPerSecond > rate
                    && Custom.DistLess(self.mainBodyChunk.lastLastPos, self.mainBodyChunk.lastPos, 14f)
                    && IteratorNearby(self))
                {
                    rate = config.IteratorGainPerSecond; tag = "iterator";
                }

                if (rate > 0f)
                {
                    float applied = exhausted ? rate * config.ExhaustedRechargeMult : rate;
                    stamina = Mathf.Min(config.StaminaMax, stamina + PerFrame(applied));
                    if (tag != activeGainTag)
                    {
                        if (activeGainTag != null)
                            Plugin.Log("Exh2", $"gain stop source={activeGainTag} stamina={stamina:0.#}");
                        Plugin.Log("Exh2", $"gain start source={tag} rate/s={applied:0.###} throttled={exhausted} stamina={stamina:0.#}");
                        activeGainTag = tag;
                        activeGainRate = applied;
                    }
                    else if (!Mathf.Approximately(applied, activeGainRate))
                    {
                        Plugin.Log("Exh2", $"gain-rate change source={tag} rate/s={applied:0.###} throttled={exhausted} stamina={stamina:0.#}");
                        activeGainRate = applied;
                    }
                    return;
                }
            }

            if (activeGainTag != null)
            {
                Plugin.Log("Exh2", $"gain stop source={activeGainTag} stamina={stamina:0.#}");
                activeGainTag = null;
            }
        }

        private bool IteratorNearby(Player self)
        {
            if (self.room == null) return false;

            if (self.room != cachedOracleRoom || --oracleSweepTimer <= 0)
            {
                oracleSweepTimer = ORACLE_SWEEP_FRAMES;
                cachedOracleRoom = self.room;
                cachedOracles.Clear();

                var lists = self.room.physicalObjects;
                if (lists != null)
                {
                    for (int i = 0; i < lists.Length; i++)
                    {
                        for (int j = 0; j < lists[i].Count; j++)
                        {
                            if (lists[i][j] is Oracle oracle) cachedOracles.Add(oracle);
                        }
                    }
                }
            }

            for (int i = 0; i < cachedOracles.Count; i++)
            {
                var oracle = cachedOracles[i];
                if (oracle == null || oracle.slatedForDeletetion) continue;
                if (!OracleQualifies(oracle)) continue;
                if (Custom.DistLess(self.mainBodyChunk.pos, oracle.firstChunk.pos, config.IteratorRestRadius))
                    return true;
            }
            return false;
        }

        private bool OracleQualifies(Oracle oracle)
        {
            if (config.IteratorRestOracles.Length == 0) return true;
            string id = oracle.ID?.value;
            for (int i = 0; i < config.IteratorRestOracles.Length; i++)
            {
                if (config.IteratorRestOracles[i] == id) return true;
            }
            return false;
        }

        public void NotifyJump(Player self)
        {
            if (ForcedStunActive)
            {
                Plugin.Log("Exh2", "jump skipped reason=stunned");
                return;
            }

            bool poleOrWall =
                self.bodyMode == Player.BodyModeIndex.WallClimb ||
                self.animation == Player.AnimationIndex.LedgeGrab ||
                self.animation == Player.AnimationIndex.ClimbOnBeam;
            DrainJump(poleOrWall ? "poleWallJump" : "jump", poleOrWall ? config.PoleWallJumpCost : config.JumpCost);
        }

        public void NotifyWallJump(Player self)
        {
            if (ForcedStunActive)
            {
                Plugin.Log("Exh2", "jump skipped reason=stunned");
                return;
            }
            DrainJump("wallJump", config.PoleWallJumpCost);
        }

        public void NotifyThrow(Player self, PhysicalObject thrown, float throwMult)
        {
            if (throwMult < 0.999f)
                Plugin.Log("Exh2", $"throw weakened mult={throwMult:0.###} depth={OverexertDepth:0.##} type={thrown.GetType().Name}");

            if (ForcedStunActive)
            {
                Plugin.Log("Exh2", $"throw skipped reason=stunned type={thrown.GetType().Name}");
                return;
            }
            if (thrown is Spear) DrainAction("spearThrow", config.SpearThrowCost, isAct: false);
            else if (thrown is Rock) DrainAction("rockThrow", config.RockThrowCost, isAct: false);
            else Plugin.Log("Exh2", $"throw uncharged type={thrown.GetType().Name}");
        }

        public void NotifyEat(Player self, IPlayerEdible edible)
        {
            if (DangerouslyExhausted)
            {
                Plugin.Log("Exh2", $"eat skipped reason={(forcedStun ? "stunned" : "collapsed")} type={edible.GetType().Name}");
                return;
            }

            if (edible is DangleFruit rottenCheck && rottenCheck.AbstrConsumable.rotted)
            {
                Plugin.Log("Exh2", "eat uncredited reason=rotten type=DangleFruit");
                return;
            }

            int nourishment = SlugcatStats.NourishmentOfObjectEaten(self.SlugCatClass, edible);
            if (nourishment <= 0)
            {
                Plugin.Log("Exh2", $"eat uncredited reason=no-nourishment type={edible.GetType().Name} nourishment={nourishment}");
                return;
            }

            float credit = nourishment * (config.EatGainPerPip / 4f);
            stamina = Mathf.Min(config.StaminaMax, stamina + credit);
            Plugin.Log("Exh2", $"gain action=eat amount={credit:0.#} pips={nourishment / 4f:0.##} type={edible.GetType().Name} stamina={stamina:0.#}");
        }

        public void NotifyHit(Player self, Creature.DamageType type, float damage, float stunBonus)
        {
            if (config.HitStaminaCost <= 0f) return;
            if (damage <= 0f && stunBonus <= 0f) return;
            if (forcedStun)
            {
                Plugin.Log("Exh2", $"hit uncharged reason=stunned type={type}");
                return;
            }
            if (hitCooldownFrames > 0) return;
            hitCooldownFrames = config.HitCostCooldown;
            stamina = Mathf.Max(0f, stamina - config.HitStaminaCost);
            Plugin.Log("Exh2", $"drain action=hit cost={config.HitStaminaCost:0.#} type={type} damage={damage:0.##} stun={stunBonus:0.#} stamina={stamina:0.#}");
        }

        private void UpdateFeel(Player self)
        {
            int tier = DangerouslyExhausted ? 2 : (exhausted ? 1 : 0);

            if (tier != feelTier)
            {
                Plugin.Log("Exh2", $"feel tier={TierName(tier)} from={TierName(feelTier)} stamina={stamina:0.#}");
                feelTier = tier;
                lastSlowStep = -1;
            }

            if (tier == 1)
            {
                int step = Mathf.Min(3, (int)(OverexertDepth * 4f));
                if (step != lastSlowStep)
                {
                    lastSlowStep = step;
                    Plugin.Log("Exh2", $"slowdown depth={OverexertDepth:0.##} speedMult={SpeedMult:0.###} stamina={stamina:0.#}");
                }
            }

            UpdateFeelAudio(self, tier);
            UpdateHeartClock(tier);
        }

        private void UpdateHeartClock(int tier)
        {
            if (tier == 0)
            {
                HeartPhase = 0f;
                heartLockedToAudio = false;
                return;
            }

            if (TryAudioHeartPhase(out float audioPhase))
            {
                if (!heartLockedToAudio)
                {
                    heartLockedToAudio = true;
                    Plugin.Log("Exh2", $"heartbeat clock=audio-locked sound={heartPlayingName} tier={TierName(tier)}");
                }

                HeartPhase = Mathf.Repeat(audioPhase + live.HeartbeatPhaseOffset, 1f);
                return;
            }

            if (heartLockedToAudio)
            {
                heartLockedToAudio = false;
                Plugin.Log("Exh2", $"heartbeat clock=free reason=no-playing-clip tier={TierName(tier)}");
            }
            float period = tier == 2
                ? Mathf.Lerp(live.HeartbeatSlow, live.HeartbeatFast, ExhaustionIntensity)
                : live.HeartbeatSlow;
            HeartPhase += 1f / Mathf.Max(1f, period);
            if (HeartPhase >= 1f) HeartPhase -= 1f;
        }

        private bool TryAudioHeartPhase(out float phase)
        {
            phase = 0f;
            if (!live.HeartbeatSyncAudio || heartEmitter == null) return false;

            var src = heartEmitter.currentSoundObject?.audioSource;
            if (src == null || !src.isPlaying) return false;

            var clip = src.clip;
            if (clip == null || clip.length <= 0f) return false;

            if (heartClipLogged != heartPlayingName)
            {
                heartClipLogged = heartPlayingName;
                Plugin.Log("Exh2", $"heartbeat clip id={heartPlayingName} clipLen={clip.length:0.###}s " +
                    $"beatsPerLoop={live.HeartbeatBeatsPerLoop:0.##} beatLen={clip.length / Mathf.Max(0.01f, live.HeartbeatBeatsPerLoop):0.###}s " +
                    $"offset={live.HeartbeatPhaseOffset:0.###}");
            }

            float beatLen = clip.length / Mathf.Max(0.01f, live.HeartbeatBeatsPerLoop);
            phase = Mathf.Repeat(src.time / beatLen, 1f);
            return true;
        }

        private static string TierName(int tier) => tier == 2 ? "down" : (tier == 1 ? "zone" : "none");

        private void UpdateFeelAudio(Player self, int tier)
        {
            if (tier == 0 || self.room == null)
            {
                StopFeelAudio(tier == 0 ? "tier-none" : "no-room");
                return;
            }
            bool down = tier == 2;
            heartEmitter = MaintainLoop(self, heartEmitter, ref heartPlayingName,
                down ? live.FeelHeartbeatStunSound : live.FeelHeartbeatZoneSound,
                down ? live.FeelHeartbeatVolStun : live.FeelHeartbeatVolZone, "heartbeat");
            breathEmitter = MaintainLoop(self, breathEmitter, ref breathPlayingName,
                live.FeelBreathSound,
                down ? live.FeelBreathVolStun : live.FeelBreathVolZone, "breath");
        }

        private ChunkSoundEmitter MaintainLoop(Player self, ChunkSoundEmitter em, ref string playingName,
            string wantName, float vol, string tag)
        {
            bool wantSilent = string.IsNullOrEmpty(wantName) || wantName == "None";
            bool stale = em != null && (em.slatedForDeletetion || em.room != self.room || playingName != wantName || wantSilent);
            if (stale)
            {
                em.alive = false;
                Plugin.Log("Exh2", $"feel-audio stop sound={tag} id={playingName}");
                em = null;
                playingName = null;
            }
            if (wantSilent) return null;
            if (em == null)
            {
                SoundID id = ResolveSound(wantName, tag);
                if (id == null) return null;

                var loader = self.room.game?.rainWorld?.processManager?.soundLoader;
                if (loader?.workingTriggers != null && id.Index >= 0 && id.Index < loader.workingTriggers.Length
                    && !loader.workingTriggers[id.Index] && warnedSounds.Add("loaded:" + wantName))
                {
                    Plugin.LogSource?.LogWarning($"[Mother][Exh2] feel-audio {tag} sound \"{wantName}\" has no loaded audio (no Sounds.txt entry or file) — it will be silent");
                }
                em = self.room.PlaySound(id, self.mainBodyChunk, loop: true, vol, 1f);
                em.requireActiveUpkeep = true;
                playingName = wantName;
                Plugin.Log("Exh2", $"feel-audio start sound={tag} id={wantName} vol={vol:0.##}");
            }
            em.alive = true;
            em.volume = vol;
            return em;
        }

        private void StopFeelAudio(string reason)
        {
            if (heartEmitter != null)
            {
                heartEmitter.alive = false;
                Plugin.Log("Exh2", $"feel-audio stop sound=heartbeat id={heartPlayingName} reason={reason}");
                heartEmitter = null;
                heartPlayingName = null;
            }
            if (breathEmitter != null)
            {
                breathEmitter.alive = false;
                Plugin.Log("Exh2", $"feel-audio stop sound=breath id={breathPlayingName} reason={reason}");
                breathEmitter = null;
                breathPlayingName = null;
            }
        }

        private static SoundID ResolveSound(string name, string tag)
        {
            if (soundCache.TryGetValue(name, out var cached)) return cached;
            if (ExtEnumBase.TryParse(typeof(SoundID), name, false, out ExtEnumBase parsed))
            {
                var id = (SoundID)parsed;
                soundCache[name] = id;
                return id;
            }
            if (warnedSounds.Add(name))
                Plugin.LogSource?.LogWarning($"[Mother][Exh2] feel-audio {tag} sound \"{name}\" is not a registered SoundID — staying silent");
            return null;
        }

        private void DrainJump(string action, float baseCost)
        {
            chainCount = (framesSinceJump <= config.ChainJumpWindow) ? chainCount + 1 : 0;
            framesSinceJump = 0;
            float mult = 1f + chainCount * config.ChainJumpMultiplier;
            if (chainCount > 0)
                Plugin.Log("Exh2", $"chain-jump escalation action={action} chainCount={chainCount} mult={mult:0.###}");
            DrainAction(action, baseCost * mult, isAct: true);
        }

        private void DrainAction(string action, float amount, bool isAct)
        {
            stamina = Mathf.Max(0f, stamina - amount);
            rechargeDelayTimer = config.RechargeDelay;
            Plugin.Log("Exh2", $"drain action={action} cost={amount:0.#} stamina={stamina:0.#}");

            if (dangerouslyExhausted && isAct)
            {
                if (config.ExhaustionActFatal)
                {
                    pendingActDeath = true;
                    Plugin.Log("Exhaustion", $"act-while-empty FATAL action={action} (act_fatal=true)");
                }
                else
                {
                    survivalTimer -= config.ExhaustionActPenalty;
                    Plugin.Log("Exhaustion", $"act-while-empty penalty action={action} framesRemoved={config.ExhaustionActPenalty} survivalTimer={survivalTimer}");
                }
            }
        }

        private static void ApplyShake(Player self, float mag)
        {
            var game = self.room != null ? self.room.game : null;
            if (game == null || game.cameras == null) return;
            for (int i = 0; i < game.cameras.Length; i++)
            {
                var cam = game.cameras[i];
                if (cam != null && cam.room == self.room)
                    cam.ScreenMovement(null, Vector2.zero, mag);
            }
        }
    }
}
