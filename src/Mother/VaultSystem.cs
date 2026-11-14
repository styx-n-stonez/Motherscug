using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MoreSlugcats;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public sealed class VaultSystem
    {
        private const int SPEED_WINDOW_TICKS = 10;
        private const int NEVER = 1 << 30;

        private const float SPEAR_HALF = 27f;
        private const float SWING_PACE_START = 0.55f;
        private const float SWING_PACE_END = 1.35f;
        private const float PUSH_OFF_FROM = 0.6f;

        private enum Phase { None, WindUp, Swing }

        private enum Kind { Plain, Fire, Explosive, Electric }

        private readonly float[] speedSamples = new float[SPEED_WINDOW_TICKS];
        private readonly bool[] sprintSamples = new bool[SPEED_WINDOW_TICKS];
        private int sampleIndex;

        private int groundedTicks;
        private int sinceStanding = NEVER;

        private int sinceLaunch = NEVER;

        private Phase phase;
        private int phaseTicks;
        private int vaultDir;
        private Vector2 plantPoint;
        private float swingSpeed;
        private float startRadius;
        private float startAngle;
        private float hipStartAngle;
        private Vector2 launchVel;

        private Kind kind;
        private float plantReach;
        private float releaseAngle;
        private float hipEndAngle;
        private Color effectColor;

        private struct TrailNode { public Vector2 pos; public int life; }
        private readonly List<TrailNode> trail = new List<TrailNode>();
        private Room trailRoom;

        private int poseGrasp = -1;
        private Spear poseSpear;

        private bool flightOpen;

        private bool flightSprinted;

        private sealed class ThrowMark { public bool decays; public float start, floor, decayDistance; public int lizardStun; }
        private static readonly ConditionalWeakTable<Spear, ThrowMark> throwMarks = new ConditionalWeakTable<Spear, ThrowMark>();

        public static void Register()
        {
            On.Player.GraphicsModuleUpdated += Player_GraphicsModuleUpdated;
            On.Spear.HitSomething += Spear_HitSomething;
        }

        public void VaultThrow(Spear spear, MotherConfig cfg, bool sprintThrow, bool weakened)
        {
            if (!cfg.VaultThrowEnabled || phase != Phase.None || !flightOpen) return;
            for (int i = 0; i < spear.bodyChunks.Length; i++)
                spear.bodyChunks[i].vel *= cfg.VaultThrowSpeedMult;
            bool sprint = flightSprinted || sprintThrow;
            throwMarks.Remove(spear);
            if (!weakened && sprint) spear.spearDamageBonus = cfg.SprintSpearDamage;
            throwMarks.Add(spear, new ThrowMark
            {
                decays = !weakened && !sprint,
                start = cfg.VaultThrowDamageStart,
                floor = cfg.VaultThrowDamageFloor,
                decayDistance = cfg.VaultThrowDecayDistance,
                lizardStun = cfg.VaultThrowLizardStunTicks,
            });
        }

        private static bool Spear_HitSomething(On.Spear.orig_HitSomething orig, Spear self, SharedPhysics.CollisionResult result, bool eu)
        {
            if (!throwMarks.TryGetValue(self, out ThrowMark mark)) return orig(self, result, eu);

            float flown = Vector2.Distance(self.thrownPos, self.firstChunk.pos);
            if (mark.decays)
                self.spearDamageBonus = Mathf.Lerp(mark.start, mark.floor, Mathf.InverseLerp(0f, mark.decayDistance, flown));
            bool hit = orig(self, result, eu);
            int stun = -1;
            if (hit && result.obj is Lizard lizard && !lizard.dead && mark.lizardStun > 0)
            {
                lizard.Stun(lizard.stun + mark.lizardStun);
                stun = lizard.stun;
            }
            return hit;
        }

        private static bool OnGround(Player self, MotherConfig cfg)
        {
            return self.room != null
                && (self.bodyMode == Player.BodyModeIndex.Stand || self.bodyMode == Player.BodyModeIndex.Crawl)
                && self.EffectiveRoomGravity >= cfg.ZeroGThreshold;
        }

        private static bool FindPlantPoint(Player self, float maxReach, int dir, out Vector2 point)
        {
            Room room = self.room;
            float feetY = Mathf.Min(self.bodyChunks[0].pos.y - self.bodyChunks[0].rad, self.bodyChunks[1].pos.y - self.bodyChunks[1].rad);
            for (float reach = maxReach; reach >= 0f; reach -= 10f)
            {
                float x = self.mainBodyChunk.pos.x + dir * reach;
                if (room.GetTile(new Vector2(x, feetY + 5f)).Solid) continue;
                for (float drop = 5f; drop <= 45f; drop += 20f)
                {
                    Vector2 probe = new Vector2(x, feetY - drop);
                    Room.Tile.TerrainType terrain = room.GetTile(probe).Terrain;
                    if (terrain != Room.Tile.TerrainType.Solid && terrain != Room.Tile.TerrainType.Slope && terrain != Room.Tile.TerrainType.Floor) continue;
                    point = new Vector2(x, drop <= 5f ? feetY : room.MiddleOfTile(probe).y + 10f);
                    return true;
                }
            }
            point = default;
            return false;
        }

        public bool TryVault(Player self, StaminaData d, int grasp, PhysicalObject held)
        {
            if (phase != Phase.None) return true;

            if (self.input[0].y >= 0 || self.input[0].x == 0) return false;

            MotherConfig cfg = d.config;
            bool enabled = cfg.VaultEnabled;
            Spear spear = held as Spear;
            int dir = self.input[0].x > 0 ? 1 : -1;
            bool down = self.dead || self.stun > 0 || d.DangerouslyExhausted;
            bool red = d.exhausted;
            bool cooling = sinceLaunch < cfg.VaultCooldownTicks;
            bool grounded = OnGround(self, cfg);
            bool prone = grounded && self.bodyMode == Player.BodyModeIndex.Crawl && sinceStanding > cfg.VaultProneGraceTicks;
            bool justLanded = grounded && groundedTicks < cfg.VaultLandingTicks;
            Kind spearKind = spear == null ? Kind.Plain : spear is ExplosiveSpear ? Kind.Explosive
                : spear is ElectricSpear && spear.abstractSpear.electricCharge > 0 ? Kind.Electric : spear.bugSpear ? Kind.Fire : Kind.Plain;
            float reach = spearKind == Kind.Explosive ? cfg.VaultExplosivePlantReach : cfg.VaultPlantReach;
            Vector2 point = default;
            bool floor = grounded && FindPlantPoint(self, reach, dir, out point);
            if (!enabled || spear == null || down || red || cooling || !grounded || prone || justLanded || !floor)
            {
                return false;
            }

            float speed = 0f;
            bool sprinted = false;
            for (int i = 0; i < speedSamples.Length; i++)
            {
                if (speedSamples[i] > speed) speed = speedSamples[i];
                sprinted |= sprintSamples[i];
            }
            flightSprinted = sprinted;

            float forwardMult = 1f, upMult = 1f, swingMult = 1f;
            kind = spearKind;
            plantReach = reach;
            releaseAngle = cfg.VaultReleaseAngle;
            hipEndAngle = cfg.VaultHipEndAngle;
            effectColor = Color.white;
            if (kind == Kind.Explosive)
            {
                forwardMult = cfg.VaultExplosiveForwardMult;
                upMult = cfg.VaultExplosiveUpMult;
                effectColor = (spear as ExplosiveSpear).explodeColor;
            }
            else if (kind == Kind.Electric)
            {
                forwardMult = cfg.VaultElectricForwardMult;
                upMult = cfg.VaultElectricUpMult;
                swingMult = cfg.VaultElectricSwingMult;
                releaseAngle = cfg.VaultElectricReleaseAngle;
                hipEndAngle = cfg.VaultElectricHipEndAngle;
                effectColor = new Color(0.7f, 1f, 1f);
                spear.abstractSpear.electricCharge--;
            }
            else if (kind == Kind.Fire)
            {
                forwardMult = cfg.VaultFireForwardMult * (1f + Random.Range(-cfg.VaultFireWild, cfg.VaultFireWild));
                upMult = cfg.VaultFireUpMult * (1f + Random.Range(-cfg.VaultFireWild, cfg.VaultFireWild));
                effectColor = Custom.HSL2RGB(Custom.Decimal(spear.abstractSpear.hue + EggBugGraphics.HUE_OFF), 1f, 0.5f);
            }
            if (sprinted)
            {
                forwardMult *= cfg.VaultSprintForwardMult;
                upMult *= cfg.VaultSprintUpMult;
            }
            launchVel = new Vector2((cfg.VaultLaunchBase + cfg.VaultLaunchPerSpeed * speed) * forwardMult * dir, cfg.VaultLaunchUp * upMult);
            swingSpeed = (cfg.VaultSwingBase + cfg.VaultSwingPerSpeed * speed) * swingMult;

            plantPoint = point;
            vaultDir = dir;
            phase = Phase.WindUp;
            phaseTicks = 0;
            poseGrasp = grasp;
            poseSpear = spear;
            sinceLaunch = NEVER;

            d.NotifyVault(cfg.VaultStaminaCost);

            return true;
        }

        private static float SwingAngle(Vector2 rel, int dir)
        {
            return Mathf.Atan2(rel.y, -rel.x * dir) * Mathf.Rad2Deg;
        }

        private static void HoldAirborne(Player self)
        {
            self.animation = Player.AnimationIndex.None;
            self.bodyMode = Player.BodyModeIndex.Default;
            self.canJump = 0;
            self.jumpBoost = 0f;
        }

        private void EndVault(string reason)
        {
            phase = Phase.None;
            poseSpear = null;
            poseGrasp = -1;
        }

        private void BeginSwing(Player self, MotherConfig cfg)
        {
            Vector2 rel = self.bodyChunks[0].pos - plantPoint;
            startRadius = Mathf.Max(rel.magnitude, 1f);
            startAngle = SwingAngle(rel, vaultDir);
            Vector2 hip = self.bodyChunks[1].pos - self.bodyChunks[0].pos;
            hipStartAngle = Mathf.Repeat(Mathf.Atan2(hip.y, hip.x * vaultDir) * Mathf.Rad2Deg, 360f);
            if (hipStartAngle < 135f) hipStartAngle = hipEndAngle;
            phase = Phase.Swing;
            phaseTicks = 0;
            if (cfg.VaultPlantSound)
                self.room.PlaySound(SoundID.Spear_Stick_In_Ground, plantPoint);
            if (cfg.VaultEffects) PlantEffects(self.room);
        }

        private void Sparks(Room room, Vector2 pos, int count, float minSpeed, float maxSpeed)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Custom.RNV();
                room.AddObject(new Spark(pos + dir * (Random.value * 10f), dir * Mathf.Lerp(minSpeed, maxSpeed, Random.value), effectColor, null, 4, 18));
            }
        }

        private void Arcs(Room room, Vector2 pos, int count, float reach)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Custom.DegToVec(360f * Random.value);
                room.AddObject(new MouseSpark(pos + dir * 9f, dir * (reach * Random.value), 20f, effectColor));
            }
        }

        private void PlantEffects(Room room)
        {
            if (kind == Kind.Electric)
            {
                room.AddObject(new Explosion.ExplosionLight(plantPoint, 120f, 1f, 4, effectColor));
                room.PlaySound(SoundID.Zapper_Zap, plantPoint, 1f, 1.5f + Random.value);
                Arcs(room, plantPoint, 10, 30f);
            }
            else if (kind == Kind.Fire)
            {
                room.AddObject(new ExplosionSpikes(room, plantPoint, 8, 6f, 6f, 4f, 50f, effectColor));
                room.PlaySound(SoundID.Fire_Spear_Pop, plantPoint);
                Sparks(room, plantPoint, 6, 4f, 14f);
            }
        }

        private void Detonate(Player self)
        {
            Room room = self.room;
            room.AddObject(new SootMark(room, plantPoint, 50f, bigSprite: false));
            room.AddObject(new Explosion(room, poseSpear ?? (PhysicalObject)self, plantPoint, 5, 110f, 5f, 1.1f, 60f, 0.3f, self, 0f, 0f, 0.7f));
            for (int i = 0; i < 14; i++)
                room.AddObject(new Explosion.ExplosionSmoke(plantPoint, Custom.RNV() * (5f * Random.value), 1f));
            room.AddObject(new Explosion.ExplosionLight(plantPoint, 160f, 1f, 3, effectColor));
            room.AddObject(new ExplosionSpikes(room, plantPoint, 9, 4f, 5f, 5f, 90f, effectColor));
            room.AddObject(new ShockWave(plantPoint, 60f, 0.045f, 4));
            Sparks(room, plantPoint, 20, 4f, 30f);
            room.ScreenMovement(plantPoint, default, 0.7f);
            room.AddObject(new Smoke.Smolder(room, plantPoint + Vector2.up * 3f, null, null));
            room.PlaySound(SoundID.Fire_Spear_Explode, plantPoint);
            room.InGameNoise(new Noise.InGameNoise(plantPoint, 8000f, self, 1f));

            Spear spear = poseSpear;
            int grasp = poseGrasp;
            poseSpear = null;
            poseGrasp = -1;
            if (spear != null && grasp >= 0 && self.grasps[grasp] != null && self.grasps[grasp].grabbed == spear)
            {
                self.ReleaseGrasp(grasp);
                spear.abstractPhysicalObject.LoseAllStuckObjects();
                spear.Destroy();
            }
        }

        private void LaunchEffects(Player self)
        {
            Room room = self.room;
            Vector2 at = self.bodyChunks[1].pos;
            if (kind == Kind.Electric)
            {
                room.AddObject(new Explosion.ExplosionLight(at, 200f, 1f, 4, effectColor));
                room.PlaySound(SoundID.Jelly_Fish_Tentacle_Stun, at);
                Arcs(room, at, 15, 36f);
            }
            else if (kind == Kind.Fire)
            {
                room.AddObject(new Explosion.ExplosionLight(at, 280f, 1f, 7, Color.white));
                room.AddObject(new ExplosionSpikes(room, at, 14, 15f, 9f, 5f, 90f, effectColor));
                room.PlaySound(SoundID.Firecracker_Bang, at);
                Sparks(room, at, 12, 4f, 22f);
            }
        }

        private void FlightEffects(Player self, MotherConfig cfg)
        {
            Vector2 at = self.bodyChunks[1].pos;
            if (kind == Kind.Electric && sinceLaunch <= cfg.VaultElectricTrailTicks)
            {
                if (trailRoom != self.room) trail.Clear();
                trailRoom = self.room;
                trail.Add(new TrailNode { pos = at, life = cfg.VaultElectricLingerTicks });
                if (cfg.VaultEffects) Arcs(self.room, at, 2, 14f);
            }
            else if (kind == Kind.Explosive && sinceLaunch <= 8 && cfg.VaultEffects)
            {
                self.room.AddObject(new Explosion.ExplosionSmoke(at, -launchVel * 0.2f + Custom.RNV() * 2f, 0.7f));
            }
            else if (kind == Kind.Fire && sinceLaunch <= cfg.VaultFireBurstTicks && sinceLaunch % 3 == 0)
            {
                Vector2 kick = Custom.RNV() * cfg.VaultFireBurstForce;
                for (int i = 0; i < self.bodyChunks.Length; i++)
                    self.bodyChunks[i].vel += kick;
                if (cfg.VaultEffects)
                {
                    self.room.AddObject(new Explosion.ExplosionLight(at, 60f, 1f, 2, effectColor));
                    self.room.PlaySound(SoundID.Fire_Spear_Pop, at);
                    Sparks(self.room, at, 4, 4f, 12f);
                }
            }
        }

        private void TickTrail(Player self, MotherConfig cfg)
        {
            if (trail.Count == 0) return;
            if (self.room == null || self.room != trailRoom)
            {
                trail.Clear();
                return;
            }
            Room room = trailRoom;
            for (int i = trail.Count - 1; i >= 0; i--)
            {
                TrailNode node = trail[i];
                node.life--;
                if (node.life <= 0)
                {
                    trail.RemoveAt(i);
                    continue;
                }
                trail[i] = node;
                if (cfg.VaultEffects && Random.value < 0.12f)
                {
                    Arcs(room, node.pos, 1, 16f);
                    if (Random.value < 0.25f) room.AddObject(new Explosion.ExplosionLight(node.pos, 50f, 0.6f, 3, effectColor));
                }
            }

            float radius = cfg.VaultElectricTrailRadius;
            for (int c = 0; c < room.abstractRoom.creatures.Count; c++)
            {
                Creature crit = room.abstractRoom.creatures[c].realizedCreature;
                if (crit == null || crit is Player || crit.dead || crit.room != room || crit.stun > 20) continue;
                bool touching = false;
                for (int i = 0; i < trail.Count && !touching; i++)
                    for (int b = 0; b < crit.bodyChunks.Length && !touching; b++)
                        touching = Custom.DistLess(crit.bodyChunks[b].pos, trail[i].pos, radius + crit.bodyChunks[b].rad);
                if (!touching) continue;

                crit.SetKillTag(self.abstractCreature);
                crit.Violence(self.firstChunk, null, crit.firstChunk, null, Creature.DamageType.Electric, 0.1f,
                    320f * Mathf.Lerp(crit.Template.baseStunResistance, 1f, 0.5f));
                room.AddObject(new CreatureSpasmer(crit, allowDead: false, crit.stun));
                room.PlaySound(SoundID.Jelly_Fish_Tentacle_Stun, crit.firstChunk.pos);
                room.AddObject(new Explosion.ExplosionLight(crit.firstChunk.pos, 200f, 1f, 4, effectColor));
                Arcs(room, crit.firstChunk.pos, 15, 36f);
            }
        }

        private float DriveSwing(Player self, MotherConfig cfg)
        {
            BodyChunk upper = self.bodyChunks[0], hips = self.bodyChunks[1];
            Vector2 rel = upper.pos - plantPoint;
            float angle = SwingAngle(rel, vaultDir);
            float progress = Mathf.InverseLerp(startAngle, releaseAngle, angle);

            float pace = swingSpeed * Mathf.Lerp(SWING_PACE_START, SWING_PACE_END, progress);
            float radius = Mathf.Lerp(startRadius, cfg.VaultPoleLength, Mathf.SmoothStep(0f, 1f, progress));
            float next = (angle + pace / radius * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            Vector2 target = plantPoint + new Vector2(-Mathf.Cos(next) * vaultDir, Mathf.Sin(next)) * radius;
            Vector2 arcVel = Vector2.ClampMagnitude(target - upper.pos, pace * 2f);

            float push = Mathf.InverseLerp(PUSH_OFF_FROM, 1f, progress);
            Vector2 vel = Vector2.Lerp(arcVel, launchVel, push * push);
            upper.vel = vel + Vector2.up * self.gravity;

            float hipAngle = Mathf.Lerp(hipStartAngle, hipEndAngle, progress * progress) * Mathf.Deg2Rad;
            Vector2 hipTarget = upper.pos + new Vector2(Mathf.Cos(hipAngle) * vaultDir, Mathf.Sin(hipAngle)) * self.bodyChunkConnections[0].distance;
            hips.vel = Vector2.Lerp(hips.vel, vel + (hipTarget - hips.pos) * 0.3f, 0.5f) + Vector2.up * self.gravity * 0.5f;
            return progress;
        }

        private void TickVault(Player self, MotherConfig cfg)
        {
            phaseTicks++;
            BodyChunk upper = self.bodyChunks[0];
            bool lostSpear = poseGrasp < 0 || self.grasps[poseGrasp] == null || self.grasps[poseGrasp].grabbed != poseSpear;
            string abort = self.room == null ? "no-room" : self.dead || self.stun > 0 ? "down" : lostSpear ? "lost-spear" : null;
            if (abort == null && phase == Phase.Swing)
                abort = upper.ContactPoint.x == vaultDir ? "wall" : upper.ContactPoint.y > 0 ? "ceiling" : null;
            if (abort != null)
            {
                EndVault(abort);
                return;
            }

            if (phase == Phase.WindUp)
            {
                if (FindPlantPoint(self, plantReach, vaultDir, out Vector2 ahead)) plantPoint = ahead;
                if (phaseTicks < cfg.VaultWindupTicks) return;
                BeginSwing(self, cfg);
            }

            HoldAirborne(self);
            bool noSwing = kind == Kind.Explosive || startAngle >= releaseAngle;
            float progress = noSwing ? 1f : DriveSwing(self, cfg);
            if (progress < 1f && phaseTicks < cfg.VaultSwingMaxTicks) return;

            for (int i = 0; i < self.bodyChunks.Length; i++)
                self.bodyChunks[i].vel = launchVel;
            if (kind == Kind.Fire)
            {
                self.animation = Player.AnimationIndex.Flip;
                self.slideDirection = vaultDir;
                self.flipFromSlide = false;
            }
            if (kind == Kind.Explosive) Detonate(self);
            else if (cfg.VaultEffects) LaunchEffects(self);
            phase = Phase.None;
            sinceLaunch = 0;
            flightOpen = true;
        }

        public void Tick(Player self, MotherConfig cfg, bool sprinting)
        {
            speedSamples[sampleIndex] = OnGround(self, cfg) ? Mathf.Abs(self.mainBodyChunk.vel.x) : 0f;
            sprintSamples[sampleIndex] = sprinting;
            groundedTicks = OnGround(self, cfg) ? groundedTicks + 1 : 0;
            if (self.bodyMode == Player.BodyModeIndex.Stand) sinceStanding = 0;
            else if (sinceStanding < NEVER) sinceStanding++;
            sampleIndex = (sampleIndex + 1) % speedSamples.Length;

            if (sinceLaunch < NEVER) sinceLaunch++;
            if (phase != Phase.None) TickVault(self, cfg);

            if (phase == Phase.None && poseSpear != null && sinceLaunch > cfg.VaultPlantTicks)
            {
                poseSpear = null;
                poseGrasp = -1;
            }

            if (phase == Phase.None && flightOpen && sinceLaunch >= 1 && self.room != null) FlightEffects(self, cfg);
            TickTrail(self, cfg);
            AimHands(self);
            if (phase == Phase.None && sinceLaunch >= 2 && Touching(self)) flightOpen = false;
        }

        private void AimHands(Player self)
        {
            if (phase == Phase.None || poseSpear == null || !(self.graphicsModule is PlayerGraphics g)) return;
            Vector2 upper = self.bodyChunks[0].pos;
            Vector2 alongShaft = (plantPoint - upper).normalized;
            for (int i = 0; i < g.hands.Length; i++)
            {
                g.hands[i].reachingForObject = true;
                g.hands[i].absoluteHuntPos = upper + alongShaft * (i == poseGrasp ? 5f : 14f);
                g.hands[i].huntSpeed = 20f;
                g.hands[i].quickness = 1f;
            }
        }

        private static bool Touching(Player self)
        {
            return self.bodyChunks[0].ContactPoint.y < 0 || self.bodyChunks[1].ContactPoint.y < 0;
        }

        private static void Player_GraphicsModuleUpdated(On.Player.orig_GraphicsModuleUpdated orig, Player self, bool actuallyViewed, bool eu)
        {
            orig(self, actuallyViewed, eu);
            if (!actuallyViewed || !StaminaSystem.TryGet(self, out var d)) return;
            VaultSystem v = d.vault;
            if (v.poseSpear == null || v.poseGrasp < 0 || self.grasps[v.poseGrasp] == null || self.grasps[v.poseGrasp].grabbed != v.poseSpear) return;

            float planted;
            if (v.phase == Phase.WindUp) planted = Mathf.SmoothStep(0f, 1f, (v.phaseTicks + 1f) / Mathf.Max(1, d.config.VaultWindupTicks));
            else if (v.phase == Phase.Swing) planted = 1f;
            else if (v.sinceLaunch <= d.config.VaultPlantTicks) planted = 1f - Mathf.SmoothStep(0f, 1f, v.sinceLaunch / Mathf.Max(1f, d.config.VaultPlantTicks));
            else return;

            BodyChunk chunk = v.poseSpear.firstChunk;
            Vector2 hand = chunk.pos;
            Vector2 heldDir = v.poseSpear.setRotation ?? v.poseSpear.rotation;
            Vector2 tip = Vector2.Lerp(hand + heldDir.normalized * SPEAR_HALF, v.plantPoint, planted);
            Vector2 toTip = tip - hand;
            float dist = toTip.magnitude;
            if (dist < 1f) return;
            Vector2 aim = toTip / dist;
            Vector2 centre = dist <= SPEAR_HALF * 2f - 3f ? tip - aim * SPEAR_HALF : hand + aim * (SPEAR_HALF - 3f);
            chunk.pos = centre;
            v.poseSpear.setRotation = aim;
            v.poseSpear.rotation = aim;
        }
    }
}
