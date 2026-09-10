using System.Globalization;
using System.Text;
using SlugBase;
using UnityEngine;

namespace MotherMod
{
    public sealed class MotherConfig
    {
        public const float TICKS_PER_SECOND = 40f;

        public bool Debug = false;

        public float StaminaMax = 1000f;

        public float SlideCost = 100f;
        public float JumpCost = 30f;
        public float PoleWallJumpCost = 50f;
        public int   ChainJumpWindow = 20;
        public float ChainJumpMultiplier = 0.5f;
        public float SpearThrowCost = 500f;
        public float RockThrowCost = 25f;
        public float BoostSwimCost = 50f;

        public float RollDrainPerSecond = 0.5f;
        public float WalkDrainPerSecond = 1f;
        public float SwimDrainPerSecond = 0.75f;
        public float FloatDrainPerSecond = 0.25f;
        public float PoleHoldDrainPerSecond = 0.25f;
        public float PoleClimbDrainPerSecond = 0.5f;
        public float VineHoldDrainPerSecond = 0.5f;
        public float VineClimbDrainPerSecond = 1f;
        public float CarryLightDrainPerSecond = 1f;
        public float CarryHeavyDrainPerSecond = 2f;
        public float SlugpupCarryDrainPerSecond = 0.25f;

        public float EatGainPerPip = 100f;
        public float StandGainPerSecond = 1f;
        public float LayGainPerSecond = 2f;
        public float IteratorGainPerSecond = 5f;
        public float IteratorRestRadius = 300f;
        public string[] IteratorRestOracles = new string[0];
        public int   RechargeDelay = 60;
        public float ExhaustedRechargeMult = 0.35f;

        public float OverexertEnter = 300f;
        public float OverexertExit = 400f;

        public float OverexertSpeedMult = 0.85f;
        public float OverexertSpeedMultMin = 0.6f;
        public bool  OverexertSlowClimb = true;
        public bool  OverexertSlowSwim = true;
        public bool  OverexertSlowThrow = true;

        public float HitStaminaCost = 0f;
        public int   HitCostCooldown = 20;

        public string FeelHeartbeatZoneSound = "HeartbeatMed_LOOP";
        public string FeelHeartbeatStunSound = "HeartbeatHard_LOOP";
        public float  FeelHeartbeatVolZone = 0.25f;
        public float  FeelHeartbeatVolStun = 0.8f;
        public string FeelBreathSound = "Mother_Breath_LOOP";
        public float  FeelBreathVolZone = 0.3f;
        public float  FeelBreathVolStun = 0.8f;
        public float  HudPulseScale = 0.15f;

        public string ExhaustionEmptyMode = "stun";
        public float ExhaustionStunSeconds = 10f;
        public int   StunFeedPerFrame = 15;
        public float PostStunRefill = 400f;
        public float StunBiteDeathMult = 2f;
        public int   EarlyBirthLimit = 5;

        public float ZeroGThreshold = 0.1f;

        public int   ExhaustionSurvivalTime = 160;
        public float ExhaustionRecoveryRate = 6f;
        public float ExhaustionExit = 250f;
        public int   ExhaustionActPenalty = 80;
        public bool  ExhaustionActFatal = false;
        public float ExhaustionMovePenalty = 0.6f;
        public float ExhaustionMoveDrainRate = 8f;

        public float VignetteMaxAlpha = 0.85f;
        public Color VignetteColor = new Color(0.10f, 0f, 0f);
        public int   HeartbeatSlow = 45;
        public int   HeartbeatFast = 18;
        public float ShakeMax = 1.2f;

        public float IntensityTimeWeight = 0.5f;
        public float IntensityStaminaWeight = 0.5f;

        public float PulseBase = 0.30f;
        public float PulseDepth = 0.70f;

        public string PulseShape = "punch";
        public float  PulseAttack = 0.04f;
        public float  PulseDecay = 5f;
        public float  PulseSustain = 0f;

        public float HeartPulse01(float phase)
        {
            float p = Mathf.Repeat(phase, 1f);
            if (PulseShape == "sine") return 0.5f + 0.5f * Mathf.Sin(p * 2f * Mathf.PI);

            float attack = Mathf.Clamp(PulseAttack, 0.001f, 0.95f);
            float sustain = Mathf.Clamp01(PulseSustain);

            if (p < attack) return Mathf.Lerp(sustain, 1f, p / attack);
            float d = (p - attack) / Mathf.Max(0.001f, 1f - attack);
            return sustain + (1f - sustain) * Mathf.Exp(-Mathf.Max(0f, PulseDecay) * d);
        }

        public bool  HeartbeatSyncAudio = true;
        public float HeartbeatBeatsPerLoop = 1f;

        public float HeartbeatPhaseOffset = 0f;

        public float HudShowBelow = 500f;
        public float HudYOffset = -18f;
        public Color HudFillColor = new Color(230f / 255f, 226f / 255f, 41f / 255f);
        public Color HudZoneColor = new Color(1f, 0.25f, 0.25f);
        public int   HudPipCount = 10;
        public float HudPipSpacing = 15f;
        public float HudPipScale = 0.75f;
        public int   HudRevealFrames = 120;
        public float HudEmptyDim = 0.4f;
        public float HudFadeIn = 0.05f;
        public float HudFadeOut = 0.025f;

        public static MotherConfig Default => new MotherConfig();

        private static string warnedBandClamp;
        private static string warnedEmptyMode;
        private static string warnedStunFeedClamp;

        public static MotherConfig FromJson(JsonAny json)
        {
            var o = json.AsObject();
            var c = new MotherConfig();

            c.Debug = B(o, "debug", c.Debug);

            c.StaminaMax = F(o, "stamina_max", c.StaminaMax);

            c.SlideCost = F(o, "slide_cost", c.SlideCost);
            c.JumpCost = F(o, "jump_cost", c.JumpCost);
            c.PoleWallJumpCost = F(o, "pole_wall_jump_cost", c.PoleWallJumpCost);
            c.ChainJumpWindow = I(o, "chain_jump_window", c.ChainJumpWindow);
            c.ChainJumpMultiplier = F(o, "chain_jump_multiplier", c.ChainJumpMultiplier);
            c.SpearThrowCost = F(o, "spear_throw_cost", c.SpearThrowCost);
            c.RockThrowCost = F(o, "rock_throw_cost", c.RockThrowCost);
            c.BoostSwimCost = F(o, "boost_swim_cost", c.BoostSwimCost);

            c.RollDrainPerSecond = F(o, "roll_drain_per_second", c.RollDrainPerSecond);
            c.WalkDrainPerSecond = F(o, "walk_drain_per_second", c.WalkDrainPerSecond);
            c.SwimDrainPerSecond = F(o, "swim_drain_per_second", c.SwimDrainPerSecond);
            c.FloatDrainPerSecond = F(o, "float_drain_per_second", c.FloatDrainPerSecond);
            c.PoleHoldDrainPerSecond = F(o, "pole_hold_drain_per_second", c.PoleHoldDrainPerSecond);
            c.PoleClimbDrainPerSecond = F(o, "pole_climb_drain_per_second", c.PoleClimbDrainPerSecond);
            c.VineHoldDrainPerSecond = F(o, "vine_hold_drain_per_second", c.VineHoldDrainPerSecond);
            c.VineClimbDrainPerSecond = F(o, "vine_climb_drain_per_second", c.VineClimbDrainPerSecond);
            c.CarryLightDrainPerSecond = F(o, "carry_light_drain_per_second", c.CarryLightDrainPerSecond);
            c.CarryHeavyDrainPerSecond = F(o, "carry_heavy_drain_per_second", c.CarryHeavyDrainPerSecond);
            c.SlugpupCarryDrainPerSecond = F(o, "slugpup_carry_drain_per_second", c.SlugpupCarryDrainPerSecond);

            c.EatGainPerPip = F(o, "eat_gain_per_pip", c.EatGainPerPip);
            c.StandGainPerSecond = F(o, "stand_gain_per_second", c.StandGainPerSecond);
            c.LayGainPerSecond = F(o, "lay_gain_per_second", c.LayGainPerSecond);
            c.IteratorGainPerSecond = F(o, "iterator_gain_per_second", c.IteratorGainPerSecond);
            c.IteratorRestRadius = F(o, "iterator_rest_radius", c.IteratorRestRadius);
            c.IteratorRestOracles = SList(o, "iterator_rest_oracles", c.IteratorRestOracles);
            c.RechargeDelay = I(o, "recharge_delay", c.RechargeDelay);
            c.ExhaustedRechargeMult = F(o, "exhausted_recharge_mult", c.ExhaustedRechargeMult);

            c.OverexertEnter = F(o, "overexert_enter", c.OverexertEnter);
            c.OverexertExit = F(o, "overexert_exit", c.OverexertExit);

            if (c.OverexertExit < c.OverexertEnter + 1f)
            {
                string offending = $"{c.OverexertEnter}/{c.OverexertExit}";
                c.OverexertExit = c.OverexertEnter + 1f;
                if (warnedBandClamp != offending)
                {
                    warnedBandClamp = offending;
                    Plugin.LogSource?.LogWarning(
                        $"[Mother][Config] overexert_exit clamped to {c.OverexertExit} (enter/exit was {offending}; exit must be > enter)");
                }
            }
            c.OverexertSpeedMult = F(o, "overexert_speed_mult", c.OverexertSpeedMult);
            c.OverexertSpeedMultMin = F(o, "overexert_speed_mult_min", c.OverexertSpeedMultMin);

            if (c.OverexertSpeedMultMin > c.OverexertSpeedMult) c.OverexertSpeedMultMin = c.OverexertSpeedMult;
            c.OverexertSlowClimb = B(o, "overexert_slow_climb", c.OverexertSlowClimb);
            c.OverexertSlowSwim = B(o, "overexert_slow_swim", c.OverexertSlowSwim);
            c.OverexertSlowThrow = B(o, "overexert_slow_throw", c.OverexertSlowThrow);

            c.HitStaminaCost = F(o, "hit_stamina_cost", c.HitStaminaCost);
            c.HitCostCooldown = I(o, "hit_cost_cooldown", c.HitCostCooldown);

            c.FeelHeartbeatZoneSound = S(o, "feel_heartbeat_zone_sound", c.FeelHeartbeatZoneSound);
            c.FeelHeartbeatStunSound = S(o, "feel_heartbeat_stun_sound", c.FeelHeartbeatStunSound);
            c.FeelHeartbeatVolZone = F(o, "feel_heartbeat_vol_zone", c.FeelHeartbeatVolZone);
            c.FeelHeartbeatVolStun = F(o, "feel_heartbeat_vol_stun", c.FeelHeartbeatVolStun);
            c.FeelBreathSound = S(o, "feel_breath_sound", c.FeelBreathSound);
            c.FeelBreathVolZone = F(o, "feel_breath_vol_zone", c.FeelBreathVolZone);
            c.FeelBreathVolStun = F(o, "feel_breath_vol_stun", c.FeelBreathVolStun);
            c.HudPulseScale = F(o, "hud_pulse_scale", c.HudPulseScale);

            string rawMode = S(o, "exhaustion_empty_mode", c.ExhaustionEmptyMode);
            if (string.Equals(rawMode, "collapse", System.StringComparison.OrdinalIgnoreCase))
            {
                c.ExhaustionEmptyMode = "collapse";
            }
            else
            {
                if (!string.IsNullOrEmpty(rawMode)
                    && !string.Equals(rawMode, "stun", System.StringComparison.OrdinalIgnoreCase)
                    && warnedEmptyMode != rawMode)
                {
                    warnedEmptyMode = rawMode;
                    Plugin.LogSource?.LogWarning(
                        $"[Mother][Config] exhaustion_empty_mode \"{rawMode}\" not recognized — using \"stun\" (expected \"stun\" or \"collapse\")");
                }
                c.ExhaustionEmptyMode = "stun";
            }
            c.ExhaustionStunSeconds = F(o, "exhaustion_stun_seconds", c.ExhaustionStunSeconds);
            c.StunFeedPerFrame = I(o, "stun_feed_per_frame", c.StunFeedPerFrame);

            if (c.StunFeedPerFrame < 11)
            {
                string offendingFeed = c.StunFeedPerFrame.ToString();
                c.StunFeedPerFrame = 11;
                if (warnedStunFeedClamp != offendingFeed)
                {
                    warnedStunFeedClamp = offendingFeed;
                    Plugin.LogSource?.LogWarning(
                        $"[Mother][Config] stun_feed_per_frame clamped to 11 (was {offendingFeed}; the Stunned threshold is stun >= 10)");
                }
            }
            c.PostStunRefill = Mathf.Min(F(o, "post_stun_refill", c.PostStunRefill), c.StaminaMax);
            c.StunBiteDeathMult = F(o, "stun_bite_death_mult", c.StunBiteDeathMult);
            c.EarlyBirthLimit = I(o, "early_birth_limit", c.EarlyBirthLimit);

            c.ZeroGThreshold = F(o, "zero_g_threshold", c.ZeroGThreshold);

            c.ExhaustionSurvivalTime = I(o, "exhaustion_survival_time", c.ExhaustionSurvivalTime);
            c.ExhaustionRecoveryRate = F(o, "exhaustion_recovery_rate", c.ExhaustionRecoveryRate);
            c.ExhaustionExit = F(o, "exhaustion_exit", c.ExhaustionExit);
            c.ExhaustionActPenalty = I(o, "exhaustion_act_penalty", c.ExhaustionActPenalty);
            c.ExhaustionActFatal = B(o, "exhaustion_act_fatal", c.ExhaustionActFatal);
            c.ExhaustionMovePenalty = F(o, "exhaustion_move_penalty", c.ExhaustionMovePenalty);
            c.ExhaustionMoveDrainRate = F(o, "exhaustion_move_drain_rate", c.ExhaustionMoveDrainRate);

            c.VignetteMaxAlpha = F(o, "vignette_max_alpha", c.VignetteMaxAlpha);
            c.VignetteColor = C(o, "vignette_color", c.VignetteColor);
            c.HeartbeatSlow = I(o, "heartbeat_slow", c.HeartbeatSlow);
            c.HeartbeatFast = I(o, "heartbeat_fast", c.HeartbeatFast);
            c.ShakeMax = F(o, "shake_max", c.ShakeMax);
            c.IntensityTimeWeight = F(o, "intensity_time_weight", c.IntensityTimeWeight);
            c.IntensityStaminaWeight = F(o, "intensity_stamina_weight", c.IntensityStaminaWeight);
            c.PulseBase = F(o, "pulse_base", c.PulseBase);
            c.PulseDepth = F(o, "pulse_depth", c.PulseDepth);
            c.PulseShape = S(o, "pulse_shape", c.PulseShape);
            c.PulseAttack = F(o, "pulse_attack", c.PulseAttack);
            c.PulseDecay = F(o, "pulse_decay", c.PulseDecay);
            c.PulseSustain = F(o, "pulse_sustain", c.PulseSustain);
            c.HeartbeatSyncAudio = B(o, "heartbeat_sync_audio", c.HeartbeatSyncAudio);
            c.HeartbeatBeatsPerLoop = F(o, "heartbeat_beats_per_loop", c.HeartbeatBeatsPerLoop);
            c.HeartbeatPhaseOffset = F(o, "heartbeat_phase_offset", c.HeartbeatPhaseOffset);

            c.HudShowBelow = F(o, "hud_show_below", c.HudShowBelow);
            c.HudYOffset = F(o, "hud_y_offset", c.HudYOffset);
            c.HudFillColor = C(o, "hud_fill_color", c.HudFillColor);
            c.HudZoneColor = C(o, "hud_zone_color", c.HudZoneColor);
            c.HudPipCount = I(o, "hud_pip_count", c.HudPipCount);
            c.HudPipSpacing = F(o, "hud_pip_spacing", c.HudPipSpacing);
            c.HudPipScale = F(o, "hud_pip_scale", c.HudPipScale);
            c.HudRevealFrames = I(o, "hud_reveal_frames", c.HudRevealFrames);
            c.HudEmptyDim = F(o, "hud_empty_dim", c.HudEmptyDim);
            c.HudFadeIn = F(o, "hud_fade_in", c.HudFadeIn);
            c.HudFadeOut = F(o, "hud_fade_out", c.HudFadeOut);

            return c;
        }

        private static float F(JsonObject o, string key, float fallback)
        {
            var v = o.TryGet(key);
            return v.HasValue ? v.Value.AsFloat() : fallback;
        }

        private static int I(JsonObject o, string key, int fallback)
        {
            var v = o.TryGet(key);
            return v.HasValue ? v.Value.AsInt() : fallback;
        }

        private static bool B(JsonObject o, string key, bool fallback)
        {
            var v = o.TryGet(key);
            return v.HasValue ? v.Value.AsBool() : fallback;
        }

        private static string S(JsonObject o, string key, string fallback)
        {
            var v = o.TryGet(key);
            return v.HasValue ? v.Value.AsString() : fallback;
        }

        private static string[] SList(JsonObject o, string key, string[] fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            var list = v.Value.AsList();
            var arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list.GetString(i);
            return arr;
        }

        private static Color C(JsonObject o, string key, Color fallback)
        {
            var v = o.TryGet(key);
            if (!v.HasValue) return fallback;
            string hex = v.Value.AsString().TrimStart('#');
            if (hex.Length >= 6
                && int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r)
                && int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g)
                && int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
            {
                return new Color(r / 255f, g / 255f, b / 255f);
            }
            return fallback;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"MotherConfig(debug={Debug}, staminaMax={StaminaMax}, ");
            sb.Append($"discrete[slide={SlideCost} jump={JumpCost} poleWall={PoleWallJumpCost} chain[{ChainJumpWindow}f x{ChainJumpMultiplier}] spear={SpearThrowCost} rock={RockThrowCost} boostSwim={BoostSwimCost}], ");
            sb.Append($"drains/s[roll={RollDrainPerSecond} walk={WalkDrainPerSecond} swim={SwimDrainPerSecond} float={FloatDrainPerSecond} poleHold={PoleHoldDrainPerSecond} poleClimb={PoleClimbDrainPerSecond} vineHold={VineHoldDrainPerSecond} vineClimb={VineClimbDrainPerSecond} carryLight={CarryLightDrainPerSecond} carryHeavy={CarryHeavyDrainPerSecond} pup={SlugpupCarryDrainPerSecond}], ");
            sb.Append($"gains[eatPerPip={EatGainPerPip} stand/s={StandGainPerSecond} lay/s={LayGainPerSecond} iterator/s={IteratorGainPerSecond} iterRadius={IteratorRestRadius} iterOracles=[{string.Join(",", IteratorRestOracles)}] delay={RechargeDelay} zoneMult={ExhaustedRechargeMult}], ");
            sb.Append($"zone[enter={OverexertEnter} exit={OverexertExit} speedMult={OverexertSpeedMult}->{OverexertSpeedMultMin} slow[climb={OverexertSlowClimb} swim={OverexertSlowSwim} throw={OverexertSlowThrow}]], ");
            sb.Append($"hit[cost={HitStaminaCost} cooldown={HitCostCooldown}], ");
            sb.Append($"feel[heart zone={FeelHeartbeatZoneSound}@{FeelHeartbeatVolZone} stun={FeelHeartbeatStunSound}@{FeelHeartbeatVolStun} breath={FeelBreathSound}@{FeelBreathVolZone}/{FeelBreathVolStun} hudPulse={HudPulseScale}], ");
            sb.Append($"empty[mode={ExhaustionEmptyMode} stunSec={ExhaustionStunSeconds} stunFeed={StunFeedPerFrame} refill={PostStunRefill} biteMult={StunBiteDeathMult} earlyBirthLimit={EarlyBirthLimit} zeroG<{ZeroGThreshold}], ");
            sb.Append($"collapse[t{ExhaustionSurvivalTime} rec{ExhaustionRecoveryRate} exit{ExhaustionExit} act{ExhaustionActPenalty} fatal{ExhaustionActFatal} movePen{ExhaustionMovePenalty} moveDrain{ExhaustionMoveDrainRate}], ");
            sb.Append($"intensityW[t{IntensityTimeWeight} s{IntensityStaminaWeight}], pulse[shape{PulseShape} base{PulseBase} depth{PulseDepth} atk{PulseAttack} dec{PulseDecay} sus{PulseSustain}], ");
            sb.Append($"vignette[maxA{VignetteMaxAlpha} col{VignetteColor}], heartbeat[{HeartbeatSlow}->{HeartbeatFast} sync={HeartbeatSyncAudio} beats/loop={HeartbeatBeatsPerLoop} offset={HeartbeatPhaseOffset}], shake{ShakeMax}, ");
            sb.Append($"hud[pips={HudPipCount} sp={HudPipSpacing} sc={HudPipScale} showBelow={HudShowBelow} y={HudYOffset} fill={HudFillColor} zoneCol={HudZoneColor} reveal={HudRevealFrames} dim={HudEmptyDim} fade[{HudFadeIn}/{HudFadeOut}]])");
            return sb.ToString();
        }
    }
}
