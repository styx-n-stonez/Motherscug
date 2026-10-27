using UnityEngine;

namespace MotherMod
{
    public class FoxVoice
    {
        private readonly Fox fox;

        private int cooldown;

        public FoxVoice(Fox fox)
        {
            this.fox = fox;
        }

        public void Update()
        {
            if (cooldown > 0) cooldown--;
        }

        public void Hunt() { Play("hunt", fox.P.VoiceHunt, false); }

        public void Alert() { Play("alert", fox.P.VoiceAlert, false); }

        public void Attack() { Play("attack", fox.P.VoiceAttack, false); }

        public void Hurt() { Play("hurt", fox.P.VoiceHurt, false); }

        public void Pheromone() { Play("pheromone", fox.P.VoicePheromone, false); }

        public void Death() { Play("death", fox.P.VoiceDeath, true); }

        private void Play(string ev, string soundName, bool ignoreCooldown)
        {
            if (fox.room == null || fox.mainBodyChunk == null) return;

            if (!ignoreCooldown && cooldown > 0)
            {
                return;
            }

            if (string.IsNullOrEmpty(soundName))
            {
                return;
            }

            if (!ExtEnumBase.TryParse(typeof(SoundID), soundName, false, out ExtEnumBase parsed)
                || parsed.Index == -1)
            {
                return;
            }

            FoxBreedParams p = fox.P;
            fox.room.PlaySound((SoundID)parsed, fox.mainBodyChunk, false,
                               Mathf.Max(0f, p.VoiceVolume), Mathf.Max(0.01f, p.VoicePitch));

            cooldown = p.VoiceCooldownFrames;
        }
    }
}
