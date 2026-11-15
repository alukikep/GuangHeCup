using UnityEngine;

namespace ModuleFactory.Audio
{
    public enum AudioBus { Master, Music, SFX, UI, Voice }
    public enum ClipSelection { RandomNoRepeat, Random, Sequential }
    public enum VoiceOverflow { Reject, StealOldest }
    public enum AudioScope { Scene, Global }
    public enum TargetLostBehaviour { Stop, ContinueAtLastPosition }

    [CreateAssetMenu(menuName = "Module Factory/Audio/Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [SerializeField] private ClipSelection selection = ClipSelection.RandomNoRepeat;
        [SerializeField] private AudioBus bus = AudioBus.SFX;
        [SerializeField] private Vector2 volume = Vector2.one;
        [SerializeField] private Vector2 pitch = Vector2.one;
        [SerializeField] private bool loop;
        [SerializeField, Range(0, 1)] private float spatialBlend = 1;
        [SerializeField, Min(0.01f)] private float minDistance = 1;
        [SerializeField, Min(0.01f)] private float maxDistance = 30;
        [SerializeField] private AudioRolloffMode rolloff = AudioRolloffMode.Logarithmic;
        [Tooltip("Higher values are more important for pool stealing.")]
        [SerializeField, Range(0, 255)] private int priority = 128;
        [SerializeField, Min(1)] private int maxInstances = 8;
        [SerializeField, Min(0)] private float cooldown;
        [SerializeField] private VoiceOverflow overflow = VoiceOverflow.Reject;

        public AudioBus Bus => bus;
        public bool Loop => loop;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => Mathf.Max(0.01f, minDistance);
        public float MaxDistance => Mathf.Max(MinDistance, maxDistance);
        public AudioRolloffMode Rolloff => rolloff;
        public int Priority => priority;
        public int MaxInstances => Mathf.Max(1, maxInstances);
        public float Cooldown => Mathf.Max(0, cooldown);
        public VoiceOverflow Overflow => overflow;
        internal AudioClip[] Clips => clips;
        /// <summary>Creates an owned transient cue. Stop its voices before destroying it.</summary>
        public static AudioCue CreateTransient(AudioClip[] audioClips, AudioBus output = AudioBus.SFX, bool looping = false)
        {
            var cue = CreateInstance<AudioCue>();
            cue.clips = audioClips == null ? new AudioClip[0] : (AudioClip[])audioClips.Clone();
            cue.bus = output;
            cue.loop = looping;
            return cue;
        }
        public float SampleVolume() => Mathf.Clamp01(Random.Range(volume.x, volume.y));
        public float SamplePitch() => Mathf.Clamp(Random.Range(pitch.x, pitch.y), 0.01f, 3f);

        // Selection history belongs to the service, never the asset.
        internal AudioClip Select(ref int previous)
        {
            if (clips == null || clips.Length == 0) return null;
            int count = 0;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) count++;
            if (count == 0) return null;
            int index;
            if (selection == ClipSelection.Sequential)
            {
                index = previous;
                do { index = (index + 1) % clips.Length; } while (clips[index] == null);
            }
            else
            {
                bool exclude = selection == ClipSelection.RandomNoRepeat && count > 1 &&
                    previous >= 0 && previous < clips.Length && clips[previous] != null;
                int choice = Random.Range(0, count - (exclude ? 1 : 0));
                index = 0;
                for (; index < clips.Length; index++)
                    if (clips[index] != null && (!exclude || index != previous) && choice-- == 0) break;
            }
            previous = index;
            return clips[index];
        }
    }
}
