using System;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MotherMod
{
    public partial class FoxGraphics
    {
        private const float PherTicksPerSecond = 40f;

        private const int PherFreshTicks = 2;

        private int pherSeenSent;
        private int pherRelSerial;
        private int pherRelTicks;
        private int pherRelLeft;
        private int pherRelCount;
        private int pherRelDone;
        private float pherEnv;
        private int pherWt;
        private bool pherDelaySniffSent;
        private readonly List<Fox> pherRelRx = new List<Fox>(4);
        private FoxSpines pherHorns;
        private bool pherHornsLooked;

        private int pherSniffLeft;
        private bool pherSniffHasKey;
        private EntityID pherSniffSender;
        private int pherSniffRelease;
        private float pherSniffPeak;

        private float pherEnvPeak;

        internal float PheromoneWiggleDeg(int i)
        {
            if (pherEnv <= 0f) return 0f;
            FoxBreedParams p = fox.P;
            if (p == null) return 0f;
            return p.PheromoneWiggleDeg * pherEnv
                   * Mathf.Sin(2f * Mathf.PI * p.PheromoneWiggleHz * pherWt / PherTicksPerSecond
                               - i * p.PheromoneWiggleLagDeg * Mathf.Deg2Rad);
        }

        private FoxSpines PheromoneHorns()
        {
            if (!pherHornsLooked)
            {
                pherHornsLooked = true;
                for (int i = 0; i < cosmetics.Count; i++)
                {
                    if (cosmetics[i] is FoxSpines s) { pherHorns = s; break; }
                }
            }
            return pherHorns;
        }

        private void UpdatePheromoneLook(FoxBreedParams p)
        {
            FoxPheromone mod = fox.AI != null ? fox.AI.pheromone : null;
            if (p == null || mod == null || !p.PheromoneEnabled)
            {
                if (pherRelLeft > 0 || pherEnv > 0f) StopPheromoneLook("disabled");
                if (mod != null) pherSeenSent = mod.SentSerial;
                return;
            }
            int style = Mathf.Clamp(p.PheromoneStyle, 1, 3);
            int now = RigClock;

            if (mod.SentSerial != pherSeenSent)
            {
                bool fresh = now - mod.LastSentTick <= PherFreshTicks;
                if (fresh && style >= 2 && fox.Consious)
                {
                    bool starting = pherRelLeft <= 0;
                    if (starting) BeginPheromoneRelease(p, now);
                    int from = Math.Max(pherSeenSent + 1, mod.SentSerial - FoxPheromone.SentRingSize + 1);
                    for (int s = from; s <= mod.SentSerial; s++)
                    {
                        Fox r = mod.SentReceiver(s);
                        if (r != null && r != fox && !pherRelRx.Contains(r)) { pherRelRx.Add(r); }
                    }
                }
                pherSeenSent = mod.SentSerial;
            }

            if (pherRelLeft > 0)
            {
                if (!fox.Consious) EndPheromoneRelease("cut_unconscious", now);
                else if (style < 2) EndPheromoneRelease("cut_style", now);
            }

            if (pherRelLeft > 0)
            {
                int done = pherRelTicks - pherRelLeft + 1;
                int want = Mathf.FloorToInt(pherRelCount * (float)done / pherRelTicks + 0.5f);
                if (style == 2)
                {
                    while (pherRelDone < want)
                    {
                        pherRelDone++;
                        SpawnPheromoneMist(p, now);
                    }
                }
                pherRelLeft--;
                if (style == 3 && !pherDelaySniffSent && pherRelLeft > 0
                    && pherRelTicks - pherRelLeft >= Math.Max(0, p.PheromoneSniffDelayTicks))
                {
                    pherDelaySniffSent = true;
                    for (int i = 0; i < pherRelRx.Count; i++)
                    {
                        Fox r = pherRelRx[i];
                        if (r != null && r.room == fox.room && r.graphicsModule is FoxGraphics rg)
                            rg.RequestPheromoneSniff(fox, pherRelSerial, "delay");
                    }
                }
            }

            if (style < 2) pherEnv = 0f;
            else if (pherRelLeft > 0) pherEnv = Mathf.Min(1f, pherEnv + 1f / Math.Max(1, p.PheromoneWiggleRiseTicks));
            else pherEnv = Mathf.Max(0f, pherEnv - 1f / Math.Max(1, p.PheromoneWiggleFallTicks));
            if (pherEnv > 0f || pherRelLeft > 0) pherWt++;
            if (pherEnv > pherEnvPeak) pherEnvPeak = pherEnv;
        }

        private void BeginPheromoneRelease(FoxBreedParams p, int now)
        {
            pherRelSerial++;
            pherRelTicks = Math.Max(1, p.PheromoneReleaseTicks);
            pherRelLeft = pherRelTicks;
            pherRelCount = Mathf.Clamp(p.PheromoneMistCount, 0, PherMaxPuffsPerRelease);
            pherRelDone = 0;
            pherWt = 0;
            pherDelaySniffSent = false;
            pherRelRx.Clear();
            pherEnvPeak = pherEnv;
        }

        private const int PherMaxPuffsPerRelease = 64;

        private void EndPheromoneRelease(string reason, int now)
        {
            pherRelLeft = 0;
        }

        private void StopPheromoneLook(string reason)
        {
            int now = RigClock;
            if (pherRelLeft > 0) EndPheromoneRelease(reason, now);
            pherEnv = 0f;
        }

        private void SpawnPheromoneMist(FoxBreedParams p, int now)
        {
            Room room = fox.room;
            if (room == null) return;

            FoxSpines horns = PheromoneHorns();
            int horn = -1;
            Vector2 at;
            if (horns != null && horns.QuillCount > 0)
            {
                horn = UnityEngine.Random.Range(0, horns.QuillCount);
                at = horns.QuillTip(horn);
            }
            else
            {
                Vector2 outward;
                at = BackPoint(0.25f, 1f, out outward);
            }

            Fox rx = PickPheromoneReceiver();
            Vector2 nose = Vector2.zero;
            bool hasNose = rx != null && TryPheromoneNose(rx, out nose);
            if (!hasNose) nose = at;
            float aim = hasNose ? Mathf.Clamp01(p.PheromoneMistAim) : 0f;

            float size = Mathf.Max(0.01f, p.PheromoneMistSize);
            Vector2 vel = Custom.RNV() * (UnityEngine.Random.value * 1.5f);
            Vector2 vanillaTo = at + new Vector2(Mathf.Lerp(-50f, 50f, UnityEngine.Random.value),
                                                 Mathf.Lerp(-100f, 400f, UnityEngine.Random.value));
            Vector2 noseTo = nose + new Vector2(Mathf.Lerp(-15f, 15f, UnityEngine.Random.value),
                                                Mathf.Lerp(-5f, 25f, UnityEngine.Random.value));
            Vector2 getTo = vanillaTo * (1f - aim) + noseTo * aim;
            Vector2 pos = at + vel.normalized * (60f * size * UnityEngine.Random.value);
            float rad = Mathf.Lerp(0.6f, 1.5f, UnityEngine.Random.value) * size;
            float lifeTime = Mathf.Max(1f, p.PheromoneMistLifetimeTicks * Mathf.Lerp(0.7f, 1.3f, UnityEngine.Random.value));

            var mist = new FoxPheromoneMist(fox, rx, pherRelSerial, pherRelDone, pos, vel, getTo, rad, lifeTime,
                                            Mathf.Clamp01(p.PheromoneMistOpacity), p.PheromoneMistHueDeg,
                                            Mathf.Clamp01(p.PheromoneMistSaturation),
                                            Mathf.Clamp01(p.PheromoneMistLightness), Mathf.Clamp01(p.PheromoneMistFog),
                                            Mathf.Max(0f, p.PheromoneMistDrift),
                                            Mathf.Max(0f, p.PheromoneSniffReachPx), Mathf.Max(0f, p.PheromoneSniffReachFac));
            room.AddObject(mist);
        }

        private Fox PickPheromoneReceiver()
        {
            int n = 0;
            for (int i = 0; i < pherRelRx.Count; i++) if (PheromoneReceiverHere(pherRelRx[i])) n++;
            if (n == 0) return null;
            int pick = UnityEngine.Random.Range(0, n);
            for (int i = 0; i < pherRelRx.Count; i++)
            {
                if (!PheromoneReceiverHere(pherRelRx[i])) continue;
                if (pick-- == 0) return pherRelRx[i];
            }
            return null;
        }

        private bool PheromoneReceiverHere(Fox r)
        {
            return r != null && !r.slatedForDeletetion && r.room != null && r.room == fox.room;
        }

        internal static bool TryPheromoneNose(Fox f, out Vector2 nose)
        {
            nose = Vector2.zero;
            if (f == null || f.slatedForDeletetion || f.room == null || f.mainBodyChunk == null) return false;
            if (f.graphicsModule is FoxGraphics g)
            {
                Vector2 n = g.DrawNosePos;
                if (n != Vector2.zero && (n - f.mainBodyChunk.pos).sqrMagnitude < 100f * 100f) { nose = n; return true; }
                if (g.head != null) { nose = g.head.pos; return true; }
            }
            nose = f.bodyChunks[0].pos;
            return true;
        }

        internal void RequestPheromoneSniff(Fox sender, int release, string why)
        {
            FoxBreedParams p = fox.P;
            if (sender == null || p == null) return;
            EntityID sid = sender.abstractCreature.ID;
            if (pherSniffHasKey && pherSniffSender == sid && pherSniffRelease == release) return;
            pherSniffHasKey = true;
            pherSniffSender = sid;
            pherSniffRelease = release;

            string skip = null;
            if (!p.PheromoneReceiverSniff) skip = "key_off";
            else if (!partsRigActive) skip = "no_parts_rig";
            else if (fox.dead || !fox.Consious) skip = "unconscious";
            else if (pherSniffLeft > 0) skip = "already_sniffing";
            if (skip != null)
            {
                return;
            }
            pherSniffLeft = Math.Max(1, p.ScentSniffTicks);
            pherSniffPeak = headPitchDeg;
        }

        private bool PheromoneSniffWanted { get { return pherSniffLeft > 0; } }

        private void TickPheromoneSniff()
        {
            if (headPitchDeg > pherSniffPeak) pherSniffPeak = headPitchDeg;
            pherSniffLeft--;
            if (pherSniffLeft <= 0)
            {
                pherSniffLeft = 0;
            }
        }

        private void CancelPheromoneSniff(string reason)
        {
            if (pherSniffLeft <= 0) return;
            pherSniffLeft = 0;
        }
    }

    public sealed class FoxPheromoneMist : CosmeticSprite
    {
        private readonly Fox sender;
        private readonly Fox receiver;
        private readonly int release;
        private readonly int puff;
        private readonly float lifeTime;
        private readonly float rad;
        private readonly Vector2 getTo;
        private readonly float opacity;
        private readonly float hueDeg, saturation, lightness, fog;
        private readonly float drift;
        private readonly float reachPx, reachFac;
        private float life = 1f;
        private float lastLife = 1f;
        private bool reached;

        public FoxPheromoneMist(Fox sender, Fox receiver, int release, int puff, Vector2 pos, Vector2 vel,
                                Vector2 getTo, float rad, float lifeTime, float opacity, float hueDeg,
                                float saturation, float lightness, float fog, float drift, float reachPx, float reachFac)
        {
            this.sender = sender;
            this.receiver = receiver;
            this.release = release;
            this.puff = puff;
            this.pos = pos;
            this.lastPos = pos;
            this.vel = vel;
            this.getTo = getTo;
            this.rad = rad;
            this.lifeTime = Mathf.Max(1f, lifeTime);
            this.opacity = opacity;
            this.hueDeg = hueDeg;
            this.saturation = saturation;
            this.lightness = lightness;
            this.fog = fog;
            this.drift = drift;
            this.reachPx = reachPx;
            this.reachFac = reachFac;
        }

        public override void Update(bool eu)
        {
            vel *= 0.9f;
            vel += (getTo - pos).normalized * (UnityEngine.Random.value * 0.04f * drift);
            lastLife = life;
            life -= 1f / lifeTime;
            BounceOffTerrain();
            if (lastLife <= 0f)
            {
                Destroy();
                return;
            }
            TestReach();
            base.Update(eu);
        }

        private void BounceOffTerrain()
        {
            if (room == null) return;
            if (!room.GetTile(pos).Solid || room.GetTile(lastPos).Solid) return;
            IntVector2 from = room.GetTilePosition(lastPos);
            IntVector2 to = room.GetTilePosition(pos);
            pos = lastPos;
            if (from.x != to.x) vel.x = -vel.x;
            if (from.y != to.y) vel.y = -vel.y;
        }

        private static float Grow(float l)
        {
            return l > 0.5f ? Custom.LerpMap(l, 1f, 0.5f, 0.5f, 1f) : Mathf.Sin(Mathf.Max(0f, l) * Mathf.PI);
        }

        private float Diameter(float l)
        {
            return 16f * 7f * rad * Grow(l);
        }

        private void TestReach()
        {
            if (reached || life <= 0.05f || receiver == null || room == null) return;
            if (receiver.room != room || receiver.slatedForDeletetion) { reached = true; return; }
            FoxGraphics rg = receiver.graphicsModule as FoxGraphics;
            if (rg == null) return;
            Vector2 nose;
            if (!FoxGraphics.TryPheromoneNose(receiver, out nose)) return;
            float dist = Vector2.Distance(pos, nose);
            float reach = reachPx + Diameter(life) * reachFac;
            if (dist >= reach) return;
            reached = true;
            rg.RequestPheromoneSniff(sender, release, "mist");
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];
            sLeaser.sprites[0] = new FSprite("Futile_White");
            sLeaser.sprites[0].shader = rCam.game.rainWorld.Shaders["FlatLightNoisy"];
            AddToContainer(sLeaser, rCam, null);
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            base.AddToContainer(sLeaser, rCam, newContatiner ?? rCam.ReturnFContainer("Items"));
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[0];
            Vector2 at = Vector2.Lerp(lastPos, pos, timeStacker);
            s.x = at.x - camPos.x;
            s.y = at.y - camPos.y;
            float l = Mathf.Lerp(lastLife, life, timeStacker);
            s.scale = 7f * rad * Grow(l);
            s.alpha = Mathf.Pow(Mathf.Max(0f, l), 1.2f) * opacity;
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            Color c = Color.Lerp(Hsl(hueDeg, saturation, lightness), palette.fogColor, fog);
            c.a = 1f;
            sLeaser.sprites[0].color = c;
        }

        internal static Color Hsl(float hueDeg, float s, float l)
        {
            float h = (((hueDeg % 360f) + 360f) % 360f) / 360f;
            float a = s * Mathf.Min(l, 1f - l);
            float Channel(float n)
            {
                float k = (n + h * 12f) % 12f;
                return l - a * Mathf.Max(-1f, Mathf.Min(Mathf.Min(k - 3f, 9f - k), 1f));
            }
            return new Color(Channel(0f), Channel(8f), Channel(4f), 1f);
        }
    }
}
