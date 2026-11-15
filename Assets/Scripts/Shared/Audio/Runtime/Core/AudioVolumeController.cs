using UnityEngine;
using UnityEngine.Audio;

namespace ModuleFactory.Audio
{
    internal sealed class AudioVolumeController
    {
        private readonly AudioSettings settings;
        private readonly float[] volumes = { 1, 1, 1, 1, 1 };
        private readonly bool[] muted = new bool[5];
        private readonly bool[] mixerControlled = new bool[5];
        private readonly bool[] warned = new bool[5];
        private bool dirty = true;
        public AudioVolumeController(AudioSettings settings) { this.settings = settings; }
        public float Get(AudioBus bus) => volumes[(int)bus];
        public bool IsMuted(AudioBus bus) => muted[(int)bus];
        public void Set(AudioBus bus, float value) { volumes[(int)bus] = Mathf.Clamp01(value); dirty = true; }
        public void Mute(AudioBus bus, bool value) { muted[(int)bus] = value; dirty = true; }
        public AudioMixerGroup Group(AudioBus bus) => settings.FindRouting(bus)?.group;
        private float Linear(int index) => muted[index] ? 0 : volumes[index];
        public float SourceGain(AudioBus bus)
        {
            float master = mixerControlled[0] ? 1 : Linear(0);
            return bus == AudioBus.Master ? master : master * (mixerControlled[(int)bus] ? 1 : Linear((int)bus));
        }
        // Called from Update, not Awake: Mixer.SetFloat is not safe during Awake.
        public void Apply()
        {
            if (!dirty) return;
            dirty = false;
            for (int i = 0; i < volumes.Length; i++)
            {
                var route = settings.FindRouting((AudioBus)i);
                mixerControlled[i] = false;
                if (route?.group == null || string.IsNullOrEmpty(route.volumeParameter)) continue;
                float gain = Linear(i);
                mixerControlled[i] = route.group.audioMixer.SetFloat(route.volumeParameter,
                    gain <= 0.0001f ? -80 : Mathf.Log10(gain) * 20);
                if (!mixerControlled[i] && !warned[i])
                {
                    warned[i] = true;
                    Debug.LogWarning($"[Audio] Expose mixer parameter '{route.volumeParameter}'. Using source gain instead.");
                }
            }
        }
        public void Load()
        {
            for (int i = 0; i < volumes.Length; i++)
            {
                string key = settings.PreferencesKey + "." + (AudioBus)i;
                volumes[i] = Mathf.Clamp01(PlayerPrefs.GetFloat(key + ".Volume", 1));
                muted[i] = PlayerPrefs.GetInt(key + ".Muted", 0) != 0;
            }
            dirty = true;
        }
        public void Save()
        {
            for (int i = 0; i < volumes.Length; i++)
            {
                string key = settings.PreferencesKey + "." + (AudioBus)i;
                PlayerPrefs.SetFloat(key + ".Volume", volumes[i]);
                PlayerPrefs.SetInt(key + ".Muted", muted[i] ? 1 : 0);
            }
            PlayerPrefs.Save();
        }
    }
}
