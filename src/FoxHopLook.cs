using System;
using RWCustom;
using Smoke;
using UnityEngine;

namespace MotherMod
{
    internal sealed class FoxHopLook
    {
        internal enum Phase { None, Charge, Flight, Land }

        private readonly Fox fox;
        internal FoxHopLook(Fox fox) { this.fox = fox; }

        private bool latched;
        private Phase phase = Phase.None;
        private int beganTick;
        private Vector2 start;
        private Vector2 dir;
        private Vector2 initVel;
        private Vector2 aim;
        private Vector2 landingDir;
        private bool hasLandingDir;
        private float power;
        private int chargeTicks, planned;
        private int tin, jc, spin;
        private int touchTick = -1;
        private float sink;
        private float flickerNum;
        private FoxHopSmoke smoke;

        internal bool ChargeKickPending;
        internal Vector2 ChargeKickDir, ChargeKickPerp;
        internal float ChargeKickShiver;
        internal bool SpinKickPending;
        internal Vector2 SpinKickN;

        private float sinkMaxPx;
        private float shockR;

        internal bool OwnsPose { get { return latched; } }
        internal bool OwnsAttitude { get { return latched && phase == Phase.Flight && fox.pounce == Fox.PounceState.Propel; } }
        internal bool LooksAtGoal { get { return latched && (phase == Phase.Charge || phase == Phase.Flight); } }
        internal Vector2 Goal { get { return aim; } }
        internal Phase CurrentPhase { get { return phase; } }

        internal float FlickerNum { get { return flickerNum; } }

        internal void BeginHop(FoxBreedParams p, FoxMoveSituation s)
        {
            if (latched) End(p, "next_hop");
            if (p == null || p.HopLookCyan < 1) return;
            Room room = fox.RoomRef;
            if (room == null || s.HopVel.sqrMagnitude < 0.0001f) return;

            latched = true;
            phase = Phase.Charge;
            beganTick = fox.MoveTick;
            start = room.MiddleOfTile(s.HopStartTile.x, s.HopStartTile.y);
            initVel = s.HopVel;
            dir = s.HopVel.normalized;
            aim = s.Target;
            landingDir = s.HopLandingDir;
            hasLandingDir = s.HopLandingDir.sqrMagnitude > 0.0001f;
            power = Mathf.Clamp01(s.HopPower);
            chargeTicks = Math.Max(0, s.HopChargeTicks);
            planned = Math.Max(1, s.HopFlightTicks);
            tin = 0; jc = 0; spin = 0; touchTick = -1;
            sink = 0f; flickerNum = 1f;
            ChargeKickPending = false; SpinKickPending = false;
            sinkMaxPx = 0f;
            shockR = 0f;
        }

        internal void Abandon(FoxBreedParams p, string why)
        {
            if (latched) End(p, why);
        }

        internal void ChargeTick(FoxBreedParams p, int counter)
        {
            if (!latched || phase != Phase.Charge) return;
            BodyChunk[] c = fox.Chunks;
            if (c == null || c.Length < 3) return;
            tin = Math.Max(0, counter - 1);

            if (Custom.DistLess(c[1].pos, start, 50f))
            {
                c[1].vel *= 0.5f;
                c[1].pos = Vector2.Lerp(c[1].pos, start - dir * (tin * 0.5f), 0.2f);
                float k = Mathf.Max(0f, p.HopLookChargeCouple);
                c[0].vel += dir * (1.5f * k);
                c[2].vel -= dir * (2f * k);
            }

            sink = Mathf.Lerp(sink, tin * 0.5f, 0.2f);
            float drawn = DrawSinkPx(p);
            if (drawn > sinkMaxPx) sinkMaxPx = drawn;

            ChargeKickPending = true;
            ChargeKickDir = dir;
            ChargeKickPerp = Perp(c[0].pos - c[2].pos);
            ChargeKickShiver = ((tin % 6) < 3 ? -1f : 1f) * Mathf.Max(0f, p.HopLookTailShiverPx);

            flickerNum = 0.5f + 0.5f * Mathf.InverseLerp(Math.Max(1, chargeTicks), 0f, tin);
        }

        internal float DrawSinkPx(FoxBreedParams p)
        {
            if (!latched || phase != Phase.Charge || p == null) return 0f;
            return Mathf.Min(Mathf.Max(0f, p.HopLookSinkPx), sink * Mathf.Max(0f, dir.y));
        }

        internal void Launch(FoxBreedParams p, BodyChunk[] c, Vector2 v)
        {
            if (!latched || c == null || c.Length < 3) return;
            phase = Phase.Flight;
            jc = 0;
            if (v.sqrMagnitude > 0.0001f) { initVel = v; dir = v.normalized; }

            if (p.HopLookSnap >= 1)
            {
                for (int i = 0; i < c.Length; i++) c[i].pos = start + dir * ((1 - i) * 8f);
                Vector2 shift = start + initVel - c[1].pos;
                for (int i = 0; i < c.Length; i++)
                {
                    c[i].pos += shift;
                    c[i].vel = initVel;
                }
            }

            FoxAI.Behavior b = fox.AI != null ? fox.AI.CurrentBehavior : FoxAI.Behavior.Idle;
            float exc = (b == FoxAI.Behavior.Hunt || b == FoxAI.Behavior.Flee) ? 1f : 0f;
            float run = Mathf.Clamp01(fox.runSpeed);
            float thr = Mathf.Lerp(-0.9f, -0.1f, Mathf.Pow((exc + run) * 0.5f, 0.5f));
            float dd = Vector2.Dot((c[1].pos - c[0].pos).normalized, dir);
            spin = (planned > 25 && dd < thr) ? (initVel.x >= 0f ? 1 : -1) : 0;

            Room room = fox.RoomRef;
            if (room == null || fox.graphicsModule == null) return;
            float bu = Mathf.Clamp01(p.HopLookBurst);
            int mode = Mathf.Clamp(p.HopLookAccent, 0, 2);
            Color eff = AccentColor(p);
            Color head = p.ColorHead;
            int nPuffs = Mathf.RoundToInt(7f * bu);
            if (nPuffs > 0 && EnsureSmoke(room))
            {
                for (int k = 0; k < nPuffs; k++)
                {
                    Vector2 pv = -initVel * (UnityEngine.Random.value * 0.5f)
                                 + Custom.RNV() * (Mathf.Lerp(6f, 23f, power) * Mathf.Max(0.3f, bu));
                    smoke.Emit(start, pv, true, Mathf.Lerp(30f, 140f, power) * Mathf.Max(0.2f, bu), eff, head, mode);
                }
            }
            if (p.HopLookBubbles >= 1)
            {
                Vector2 hp = c[0].pos + (c[0].pos - c[1].pos).normalized * 8f;
                for (int n = (int)(power * 10f * UnityEngine.Random.value); n >= 0; n--)
                {
                    Vector2 bp = hp + Custom.RNV() * (5f * UnityEngine.Random.value);
                    Vector2 bv = c[0].vel + Custom.RNV()
                                 * (UnityEngine.Random.value * UnityEngine.Random.value * 12f + power * 10f);
                    room.AddObject(new FoxHopBubble(bp, bv, head));
                }
            }
            if (bu > 0.005f)
            {
                var light = new FoxHopLight(start - dir * 10f, power, power * bu, eff, mode);
                room.AddObject(light);
                shockR = Mathf.Lerp(40f, 120f, power) * bu;
                room.AddObject(new ShockWave(start - dir * 15f, shockR, 0.07f, 6 + (int)(power * 4f)));
            }
        }

        internal void FlightTick(FoxBreedParams p)
        {
            if (!latched || phase != Phase.Flight) return;
            BodyChunk[] c = fox.Chunks;
            Room room = fox.RoomRef;
            if (c == null || c.Length < 3 || room == null) return;
            jc++;

            float trailLimit = 70f * power * Mathf.Clamp01(p.HopLookTrail);
            if (fox.graphicsModule != null && jc < trailLimit && EnsureSmoke(room))
            {
                Vector2 tv = c[1].vel + DirVec(c[1].pos, c[2].pos) * 16f;
                smoke.Emit(c[1].pos, tv, false, (trailLimit - jc) * 1.5f, AccentColor(p), p.ColorHead,
                           Mathf.Clamp(p.HopLookAccent, 0, 2));
            }

            float st = Mathf.Max(0f, p.HopLookSteer);
            if (hasLandingDir && jc > planned - 30)
            {
                c[0].vel += landingDir * (3f * st);
                c[2].vel -= landingDir * (3f * st);
            }
            else if (spin == 0 || p.HopLookSpin < 1)
            {
                Vector2 tow = DirVec(c[1].pos, aim) * (LerpMap(jc, 10f, 60f, 2f, 0f) * st);
                c[0].vel += tow;
                c[2].vel -= tow;
            }
            else
            {
                IntVector2 t = room.GetTilePosition(c[1].pos);
                if (room.aimap != null && room.aimap.getTerrainProximity(t.x, t.y) > 2)
                {
                    Vector2 n = (Perp(c[2].pos - c[1].pos) + Perp(c[1].pos - c[0].pos)).normalized;
                    Vector2 sv = n * (spin * LerpMap(jc, 20f, 60f, 3f, 0f));
                    c[0].vel += sv;
                    c[2].vel -= sv;
                    SpinKickPending = true;
                    SpinKickN = n * spin;
                }
            }
        }

        internal void NoteTouchdown(FoxBreedParams p)
        {
            if (!latched || phase != Phase.Flight) return;
            phase = Phase.Land;
            touchTick = fox.MoveTick;
        }

        internal bool SkipsLandCompress { get { return latched; } }

        internal void NotePounceEnded(FoxBreedParams p, string reason, bool touched, bool handover)
        {
            if (!latched) return;
            if (handover) { End(p, "handover"); return; }
            if (phase == Phase.Charge || phase == Phase.Flight) phase = Phase.None;
        }

        internal void Tick(FoxBreedParams p)
        {
            if (!latched || p == null) return;
            int now = fox.MoveTick;
            bool running = fox.Move != null && fox.Move.Pounce.Running;
            if (!running && fox.PounceGather > 0f && fox.Move != null && fox.Move.Pounce.GatherRelease <= 0)
            {
                End(p, "gather_taken");
                return;
            }
            if (phase == Phase.Land)
            {
                int s = now - touchTick;
                if (BoingDone(p, s)) { phase = Phase.None; }
            }
            if (phase == Phase.None)
            {
                if (!running && fox.PounceGather <= 0f && fox.PounceLand <= 0f)
                {
                    End(p, touchTick >= 0 ? "boing_done" : "ended_in_air");
                    return;
                }
            }
            int cap = Math.Max(1, p.AttackStateCeilingTicks) + 8 * Math.Max(2, p.HopLookLandTicks) + 200;
            if (now - beganTick > cap) End(p, "watchdog");
        }

        internal int BoingTicks()
        {
            if (!latched || phase != Phase.Land || touchTick < 0) return -1;
            return Math.Max(0, fox.MoveTick - touchTick);
        }

        private void End(FoxBreedParams p, string why)
        {
            latched = false;
            phase = Phase.None;
            ChargeKickPending = false;
            SpinKickPending = false;
            touchTick = -1;
        }

        internal static float BoingRaw(FoxBreedParams p, float s)
        {
            float sq = p.HopLookLandSquashPx;
            if (!(sq > 0f) || s < 0f) return 0f;
            float T = Mathf.Max(2f, p.HopLookLandTicks);
            float st = Mathf.Clamp(p.HopLookLandStretch, 0f, 0.95f);
            float w = 2f * Mathf.PI / T;
            float lam = st > 0.001f ? -Mathf.Log(st) * w / Mathf.PI : w * 3f;
            float s0 = Mathf.Atan2(w, lam) / w;
            float A = sq / (Mathf.Exp(-lam * s0) * Mathf.Sin(w * s0));
            float env = A * Mathf.Exp(-lam * s);
            if (s > T && env < 0.02f) return 0f;
            return -env * Mathf.Sin(w * s);
        }

        private static bool BoingDone(FoxBreedParams p, int s)
        {
            float sq = p.HopLookLandSquashPx;
            if (!(sq > 0f)) return true;
            float T = Mathf.Max(2f, p.HopLookLandTicks);
            if (s > 8f * T) return true;
            float st = Mathf.Clamp(p.HopLookLandStretch, 0f, 0.95f);
            float w = 2f * Mathf.PI / T;
            float lam = st > 0.001f ? -Mathf.Log(st) * w / Mathf.PI : w * 3f;
            float s0 = Mathf.Atan2(w, lam) / w;
            float A = sq / (Mathf.Exp(-lam * s0) * Mathf.Sin(w * s0));
            return s > T && A * Mathf.Exp(-lam * s) < 0.02f;
        }

        internal static Color AccentColor(FoxBreedParams p)
        {
            return Mathf.Clamp(p.HopLookAccent, 0, 2) == 1 ? p.ColorHead : p.ColorLegLower;
        }

        private bool EnsureSmoke(Room room)
        {
            if (smoke != null && (smoke.slatedForDeletetion || smoke.room != room)) smoke = null;
            if (smoke == null)
            {
                smoke = new FoxHopSmoke(room);
                room.AddObject(smoke);
            }
            return smoke != null;
        }

        private static Vector2 Perp(Vector2 v)
        {
            v.Normalize();
            return new Vector2(-v.y, v.x);
        }

        private static Vector2 DirVec(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            return d.sqrMagnitude > 0.000001f ? d.normalized : Vector2.zero;
        }

        private static float LerpMap(float v, float a, float b, float c, float d)
        {
            return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, v));
        }
    }

    public partial class Fox
    {
        private FoxHopLook hopLook;

        internal FoxHopLook HopLook { get { return hopLook ?? (hopLook = new FoxHopLook(this)); } }

        internal float DrawPounceGather { get { return HopLook.OwnsPose ? 0f : PounceGather; } }
        internal float DrawPounceFlight { get { return HopLook.OwnsPose ? 0f : PounceFlight; } }
        internal float DrawPounceLand { get { return HopLook.OwnsPose ? 0f : PounceLand; } }
        internal float DrawLandingPose { get { return HopLook.OwnsPose ? 0f : LandingPose; } }
    }

    public partial class FoxGraphics
    {
        private float hopBoingDepth, lastHopBoingDepth;

        private void ApplyHopLookPose(FoxBreedParams p)
        {
            lastHopBoingDepth = hopBoingDepth;
            hopBoingDepth = 0f;
            FoxHopLook look = fox.HopLook;
            if (!look.OwnsPose || p == null) return;

            float sinkPx = look.DrawSinkPx(p);
            if (sinkPx > 0f)
            {
                drawPositions[1, 0].y -= sinkPx;
                drawPositions[2, 0].y -= sinkPx * 0.8f;
            }

            int s = look.BoingTicks();
            if (s >= 0)
            {
                float x = FoxHopLook.BoingRaw(p, s);
                if (x != 0f)
                {
                    for (int i = 0; i < drawPositions.GetLength(0); i++) drawPositions[i, 0].y += x;
                }
                float sq = p.HopLookLandSquashPx;
                if (sq > 0f) hopBoingDepth = Mathf.Clamp(-x / sq, -1f, 1f);
            }
        }

        internal float HopBoingWidthFac(FoxBreedParams p, float timeStacker)
        {
            if (p == null || p.HopLookLandWiden == 0f) return 1f;
            float d = Mathf.Lerp(lastHopBoingDepth, hopBoingDepth, timeStacker);
            return d == 0f ? 1f : Mathf.Max(0.1f, 1f + p.HopLookLandWiden * d);
        }

        internal float HopBoingNodDeg(FoxBreedParams p, float timeStacker)
        {
            if (p == null) return 0f;
            float d = Mathf.Lerp(lastHopBoingDepth, hopBoingDepth, timeStacker);
            return d > 0f ? Mathf.Max(0f, p.HopLookLandRecoilDeg) * d : 0f;
        }

        private void ApplyHopLookTail()
        {
            FoxHopLook look = fox.HopLook;
            if (!look.OwnsPose) return;
            int n = tailSegmentCount;
            if (look.ChargeKickPending)
            {
                look.ChargeKickPending = false;
                for (int i = 0; i < n; i++)
                    tail[i].vel += -look.ChargeKickDir * i - look.ChargeKickPerp * look.ChargeKickShiver;
            }
            if (look.SpinKickPending)
            {
                look.SpinKickPending = false;
                for (int i = 0; i < n; i++)
                {
                    float t = n <= 1 ? 0f : (float)i / (n - 1);
                    tail[i].vel += look.SpinKickN * (Mathf.Sin(t * Mathf.PI) * 15f);
                }
            }
        }
    }

    internal sealed class FoxHopSmoke : SmokeSystem
    {
        internal static readonly SmokeType HopSmokeType = new SmokeType("MotherFoxHopSmoke", register: true);

        internal FoxHopSmoke(Room room) : base(HopSmokeType, room, 2, 0f) { }

        public override SmokeSystemParticle CreateParticle()
        {
            return new FoxHopSmokeParticle();
        }

        internal bool Emit(Vector2 pos, Vector2 vel, bool big, float maxLife, Color effect, Color head, int accentMode)
        {
            var pt = AddParticle(pos, vel, Mathf.Max(1f, maxLife * Mathf.Lerp(0.3f, 1f, UnityEngine.Random.value)))
                     as FoxHopSmokeParticle;
            if (pt == null) return false;
            pt.Big = big;
            pt.EffectColor = effect;
            pt.HeadColor = head;
            pt.AccentMode = accentMode;
            return true;
        }
    }

    internal sealed class FoxHopSmokeParticle : SpriteSmoke
    {
        internal bool Big;
        internal Color EffectColor = Color.white, HeadColor = Color.white;
        internal int AccentMode;
        private Color fadeColor = new Color(0.1f, 0.1f, 0.1f);
        private Color paletteEffectA = Color.white;
        private bool paletteKnown;
        private int counter;

        public override void Reset(SmokeSystem newOwner, Vector2 pos, Vector2 vel, float newLifeTime)
        {
            base.Reset(newOwner, pos, vel, newLifeTime);
            rad = Mathf.Lerp(28f, 46f, UnityEngine.Random.value);
            counter = 0;
        }

        public override void Update(bool eu)
        {
            base.Update(eu);
            if (resting) return;
            float m = vel.magnitude;
            if (m > 0.0001f) vel *= 0.7f + 0.3f / Mathf.Sqrt(m);
            if (room != null)
            {
                if (room.PointSubmerged(pos)) pos.y = room.FloatWaterLevel(pos);
                if (room.GetTile(pos).Solid && !room.GetTile(lastPos).Solid)
                {
                    IntVector2 from = room.GetTilePosition(lastPos);
                    IntVector2 to = room.GetTilePosition(pos);
                    pos = lastPos;
                    if (from.x != to.x) vel.x = -vel.x;
                    if (from.y != to.y) vel.y = -vel.y;
                }
            }
            counter++;
        }

        public override float Rad(int type, float useLife, float useStretched, float timeStacker)
        {
            float u = Mathf.Pow(Mathf.Max(0f, 1f - useLife), 0.2f);
            float n = type == 0 ? Mathf.Lerp(4f, rad, u + useStretched)
                    : type == 1 ? 1.5f * Mathf.Lerp(2f, rad, u)
                    : Mathf.Lerp(4f, rad, u);
            return Big ? n * (1f + Mathf.InverseLerp(0f, 10f, counter + timeStacker)) : n * 0.2f;
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            base.InitiateSprites(sLeaser, rCam);
            for (int i = 0; i < 2; i++) sLeaser.sprites[i].shader = rCam.game.rainWorld.Shaders["FireSmoke"];
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
            if (resting) return;
            float l = Mathf.Lerp(lastLife, life, timeStacker);
            Color eff = (AccentMode == 2 && paletteKnown) ? paletteEffectA : EffectColor;
            Color col = Big ? Color.Lerp(eff, fadeColor, Mathf.InverseLerp(1f, 0.25f, l))
                            : Color.Lerp(HeadColor, fadeColor, Mathf.InverseLerp(1f, 0.25f, l) * 0.5f);
            col = FoxHopFx.NoPureBlack(col);
            sLeaser.sprites[0].color = col;
            sLeaser.sprites[1].color = col;
            float bigF = Big ? (1f - 0.2f * Mathf.InverseLerp(0f, 10f, counter + timeStacker)) : 1f;
            sLeaser.sprites[0].alpha = Mathf.Pow(Mathf.Max(0f, l), 0.25f) * (1f - stretched) * bigF;
            sLeaser.sprites[1].alpha = (0.3f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(l * Mathf.PI)), 0.7f) * 0.65f
                                        * (1f - stretched)) * bigF;
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            fadeColor = Color.Lerp(palette.blackColor, palette.fogColor, 0.6f);
            if (palette.texture != null) { paletteEffectA = palette.texture.GetPixel(30, 5); paletteKnown = true; }
        }
    }

    internal sealed class FoxHopLight : CosmeticSprite
    {
        private float life = 1f, lastLife = 1f;
        private readonly float lifeTime, intensity;
        private readonly int accentMode;
        private Color drawColor;

        internal FoxHopLight(Vector2 at, float power, float intensity, Color color, int accentMode)
        {
            pos = at;
            lastPos = at;
            lifeTime = Mathf.Max(1f, Mathf.Lerp(4f, 22f, power * power));
            this.intensity = Mathf.Clamp01(intensity);
            this.accentMode = accentMode;
            drawColor = color;
        }

        private float Radius(float n)
        {
            return Mathf.Lerp(20f + 10f * intensity, 40f + 30f * intensity, n)
                   + Mathf.Lerp(50f, 90f, intensity) * Mathf.Sin(n * n * Mathf.PI);
        }

        public override void Update(bool eu)
        {
            lastLife = life;
            life -= 1f / lifeTime;
            if (lastLife <= 0f) Destroy();
            base.Update(eu);
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[3];
            for (int i = 0; i < 3; i++) sLeaser.sprites[i] = new FSprite("Futile_White");
            sLeaser.sprites[0].shader = rCam.game.rainWorld.Shaders["LightSource"];
            sLeaser.sprites[1].shader = rCam.game.rainWorld.Shaders["FlatLight"];
            sLeaser.sprites[2].shader = rCam.game.rainWorld.Shaders["FlatLight"];
            AddToContainer(sLeaser, rCam, null);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            Vector2 at = Vector2.Lerp(lastPos, pos, timeStacker);
            float n = Mathf.Lerp(lastLife, life, timeStacker);
            for (int i = 0; i < 3; i++)
            {
                sLeaser.sprites[i].x = at.x - camPos.x;
                sLeaser.sprites[i].y = at.y - camPos.y;
            }
            float r = Radius(n);
            Color c = FoxHopFx.NoPureBlack(drawColor);
            sLeaser.sprites[0].color = c;
            sLeaser.sprites[0].scale = r * 2f / 8f;
            sLeaser.sprites[0].alpha = Mathf.Pow(Mathf.InverseLerp(0f, 0.5f, n), 0.5f);
            sLeaser.sprites[1].color = c;
            sLeaser.sprites[1].scale = r / 8f;
            sLeaser.sprites[1].alpha = n * n * (0.4f + 0.4f * intensity);
            sLeaser.sprites[2].scale = r * Mathf.Lerp(0.4f, 0.8f, UnityEngine.Random.value) / 8f;
            sLeaser.sprites[2].alpha = Mathf.Pow(Mathf.InverseLerp(0.25f, 1f, n), 3f) * intensity;
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            if (accentMode == 2 && palette.texture != null) drawColor = palette.texture.GetPixel(30, 5);
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (newContatiner == null) newContatiner = rCam.ReturnFContainer("Bloom");
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                newContatiner.AddChild(sLeaser.sprites[i]);
            }
        }
    }

    internal sealed class FoxHopBubble : CosmeticSprite
    {
        private float life = 1f, lastLife = 1f;
        private readonly int lifeTime;
        private bool hollow;
        private readonly Color headColor;
        private Color black = new Color(0.02f, 0.02f, 0.02f);

        internal FoxHopBubble(Vector2 at, Vector2 v, Color headColor)
        {
            pos = at;
            lastPos = at;
            vel = v;
            this.headColor = headColor;
            lifeTime = 10 + UnityEngine.Random.Range(0, UnityEngine.Random.Range(0,
                       UnityEngine.Random.Range(0, UnityEngine.Random.Range(0, 200))));
        }

        public override void Update(bool eu)
        {
            vel = Vector3.Slerp(vel, Custom.DegToVec(UnityEngine.Random.value * 360f), 0.2f);
            lastLife = life;
            life -= 1f / Math.Max(1, lifeTime);
            if (UnityEngine.Random.value < 0.5f) hollow = UnityEngine.Random.value < 0.5f;
            if (room != null)
            {
                if (room.GetTile(pos).Solid) life = 0f;
                if (room.PointSubmerged(pos)) { vel *= 0.9f; vel.y += 4f; }
            }
            if (life <= 0f) Destroy();
            base.Update(eu);
        }

        public override void InitiateSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam)
        {
            sLeaser.sprites = new FSprite[1];
            sLeaser.sprites[0] = new FSprite("LizardBubble0");
            AddToContainer(sLeaser, rCam, null);
        }

        public override void DrawSprites(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, float timeStacker, Vector2 camPos)
        {
            FSprite s = sLeaser.sprites[0];
            float l = Mathf.Lerp(lastLife, life, timeStacker);
            float n = 0.625f * Mathf.Lerp(Mathf.Lerp(Mathf.Sin(Mathf.PI * lastLife), lastLife, 0.5f),
                                          Mathf.Lerp(Mathf.Sin(Mathf.PI * life), life, 0.5f), timeStacker);
            s.x = Mathf.Lerp(lastPos.x, pos.x, timeStacker) - camPos.x;
            s.y = Mathf.Lerp(lastPos.y, pos.y, timeStacker) - camPos.y;
            s.color = FoxHopFx.NoPureBlack(Color.Lerp(headColor, black, 1f - Mathf.Clamp01(l * 2f)));
            string el = hollow ? "LizardBubble1" : "LizardBubble0";
            if (s.element == null || s.element.name != el) s.element = Futile.atlasManager.GetElementWithName(el);
            s.scaleX = Mathf.Max(0f, n);
            s.scaleY = Mathf.Max(0f, n);
            s.rotation = 0f;
            s.anchorY = 0.5f;
            base.DrawSprites(sLeaser, rCam, timeStacker, camPos);
        }

        public override void ApplyPalette(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            black = palette.blackColor;
        }

        public override void AddToContainer(RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, FContainer newContatiner)
        {
            if (newContatiner == null) newContatiner = rCam.ReturnFContainer("Midground");
            for (int i = 0; i < sLeaser.sprites.Length; i++)
            {
                sLeaser.sprites[i].RemoveFromContainer();
                newContatiner.AddChild(sLeaser.sprites[i]);
            }
        }
    }

    internal static class FoxHopFx
    {
        internal static Color NoPureBlack(Color c)
        {
            return new Color(Mathf.Max(c.r, 0.02f), Mathf.Max(c.g, 0.02f), Mathf.Max(c.b, 0.02f), c.a);
        }
    }
}
