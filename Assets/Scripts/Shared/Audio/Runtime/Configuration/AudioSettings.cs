using System;
using UnityEngine;
using UnityEngine.Audio;

namespace ModuleFactory.Audio
{
    [Serializable]
    public sealed class AudioBusRouting
    {
        public AudioBus bus;
        public AudioMixerGroup group;
        [Tooltip("Optional exposed dB parameter. Empty uses source-volume fallback.")]
        public string volumeParameter;
    }

    [CreateAssetMenu(menuName = "Module Factory/Audio/Settings")]
    public sealed class AudioSettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int prewarmVoices = 8;
        [SerializeField, Min(1)] private int maxVoices = 32;
        [SerializeField] private string preferencesKey = "ModuleFactory.Audio";
        [SerializeField] private AudioBusRouting[] routing = new AudioBusRouting[0];
        [SerializeField, Min(1)] private int maxRequestsPerFrame = 64;
        [SerializeField, Min(0)] private int maxStealTails = 4;
        [SerializeField, Range(0, 0.2f)] private float stealFadeSeconds = 0.02f;
        [SerializeField, Min(0.1f)] private float musicLoadTimeout = 10;
        [Tooltip("Allow LoadAudioData to block the main thread. Prefer background loading or preloading during a loading screen.")]
        [SerializeField] private bool allowSynchronousLoads;
        [SerializeField] private AudioBusPolicy[] busPolicies = new AudioBusPolicy[0];
        public int MaxVoices => Mathf.Max(1, maxVoices);
        public int PrewarmVoices => Mathf.Clamp(prewarmVoices, 0, MaxVoices);
        public int MaxRequestsPerFrame => Mathf.Max(1, maxRequestsPerFrame);
        public int MaxStealTails => Mathf.Clamp(maxStealTails, 0, MaxVoices);
        public float StealFadeSeconds => Mathf.Clamp(stealFadeSeconds, 0, 0.2f);
        public float MusicLoadTimeout => Mathf.Max(0.1f, musicLoadTimeout);
        public bool AllowSynchronousLoads => allowSynchronousLoads;
        public AudioBusPolicy FindPolicy(AudioBus bus)
        {
            if (busPolicies != null)
                foreach (var item in busPolicies) if (item != null && item.bus == bus) return item;
            return null;
        }
        public bool Validate(out string error)
        {
            int reservations = 0;
            var seen = new bool[5];
            if (busPolicies != null) foreach (var policy in busPolicies)
            {
                if (policy == null) continue;
                int index = (int)policy.bus;
                if (index < 0 || index >= 5 || seen[index])
                { error = "Bus policies must have distinct valid buses."; return false; }
                seen[index] = true;
                if (policy.reservedVoices < 0 || policy.maxVoices < 0 || policy.maxRequestsPerFrame < 0 ||
                    (policy.maxVoices > 0 && policy.reservedVoices > policy.maxVoices))
                { error = "Bus reservation must fit its voice limit; limits cannot be negative."; return false; }
                reservations += policy.reservedVoices;
            }
            if (reservations > MaxVoices)
            { error = "Sum of bus reservations exceeds Max Voices."; return false; }
            error = null; return true;
        }
        public string PreferencesKey => string.IsNullOrWhiteSpace(preferencesKey) ? "ModuleFactory.Audio" : preferencesKey;
        public AudioBusRouting FindRouting(AudioBus bus)
        {
            if (routing != null)
                foreach (var item in routing) if (item != null && item.bus == bus) return item;
            return null;
        }
    }

    [Serializable]
    public sealed class AudioBusPolicy
    {
        public AudioBus bus;
        [Tooltip("0 means no extra limit; the global pool limit still applies.")]
        [Min(0)] public int maxVoices;
        [Tooltip("Protected capacity. Other buses cannot occupy these slots while unused.")]
        [Min(0)] public int reservedVoices;
        [Tooltip("0 uses the global request budget only.")]
        [Min(0)] public int maxRequestsPerFrame;
    }
}
