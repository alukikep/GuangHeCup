using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModuleFactory.Audio
{
    public struct AudioPlayOptions
    {
        // Nullable multipliers make default(AudioPlayOptions) mean 1, not 0.
        public float? VolumeMultiplier;
        public float? PitchMultiplier;
        public float FadeInSeconds;
        public AudioScope Scope;
        public Scene? OwnerScene;
        public TargetLostBehaviour TargetLost;
    }

    public interface IAudioPlayback
    {
        AudioHandle Play(AudioCue cue, AudioPlayOptions options = default);
        AudioHandle PlayAt(AudioCue cue, Vector3 position, AudioPlayOptions options = default);
        AudioHandle PlayAttached(AudioCue cue, Transform target, AudioPlayOptions options = default);
    }

    public interface IAudioService : IAudioPlayback
    {
        MusicRequestStatus MusicStatus { get; }
        string LastMusicError { get; }
        AudioCue PendingMusicCue { get; }
        AudioDataLoadState Preload(AudioCue cue, bool allowSynchronous = false);
        void PlayMusic(AudioCue cue, float crossFadeSeconds = 0.5f);
        void StopMusic(float fadeOutSeconds = 0.5f);
        void PauseMusic();
        void ResumeMusic();
        void SetVolume(AudioBus bus, float normalizedVolume);
        float GetVolume(AudioBus bus);
        void SetMuted(AudioBus bus, bool muted);
        bool IsMuted(AudioBus bus);
        void SetWorldPaused(bool paused);
        void StopBus(AudioBus bus, float fadeOutSeconds = 0f);
        void LoadPreferences();
        void SavePreferences();
    }

    /// <summary>Controls voices identified by a slot and playback version.</summary>
    /// <remarks>Implementations must ignore commands for stale or invalid slot/version pairs.</remarks>
    public interface IAudioVoiceControl
    {
        bool IsValid(int slot, ulong version);
        bool IsPaused(int slot, ulong version);
        void Stop(int slot, ulong version, float fadeOutSeconds);
        void Pause(int slot, ulong version, bool paused);
        void SetVolume(int slot, ulong version, float multiplier);
    }

    public readonly struct AudioHandle
    {
        private readonly IAudioVoiceControl voiceControl;
        private readonly int slot;
        private readonly ulong version;
        public AudioHandle(IAudioVoiceControl voiceControl, int slot, ulong version)
        { this.voiceControl = voiceControl; this.slot = slot; this.version = version; }
        public bool IsValid => voiceControl != null && voiceControl.IsValid(slot, version);
        public bool IsPaused => IsValid && voiceControl.IsPaused(slot, version);
        public void Stop(float fadeOutSeconds = 0) => voiceControl?.Stop(slot, version, fadeOutSeconds);
        public void Pause() => voiceControl?.Pause(slot, version, true);
        public void Resume() => voiceControl?.Pause(slot, version, false);
        public void SetVolume(float multiplier) => voiceControl?.SetVolume(slot, version, multiplier);
    }

    public static class GameAudio
    {
        public static IAudioService Service { get; internal set; }
        public static bool IsReady => Service != null;
        // Early calls fail safely; no implicit creation of hidden managers.
        public static AudioHandle Play(AudioCue cue, AudioPlayOptions options = default) =>
            Service == null ? default : Service.Play(cue, options);
        public static AudioHandle PlayAt(AudioCue cue, Vector3 position, AudioPlayOptions options = default) =>
            Service == null ? default : Service.PlayAt(cue, position, options);
        public static AudioHandle PlayAttached(AudioCue cue, Transform target, AudioPlayOptions options = default) =>
            Service == null ? default : Service.PlayAttached(cue, target, options);
    }
}
