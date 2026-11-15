using UnityEngine;

namespace ModuleFactory.Audio
{
    public enum AudioEmitterMode { TwoDimensional, WorldPosition, FollowTransform }

    [DisallowMultipleComponent]
    public sealed class AudioEmitter : MonoBehaviour
    {
        [SerializeField] private AudioCue cue;
        [SerializeField] private AudioEmitterMode mode;
        [SerializeField] private AudioScope scope;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool stopOnDisable = true;
        [SerializeField, Min(0)] private float fadeInSeconds;
        private IAudioPlayback playbackOverride;
        private AudioHandle handle;
        public AudioHandle Handle => handle;
        /// <summary>Overrides playback for subsequent calls. Pass null to use GameAudio.</summary>
        public void SetPlayback(IAudioPlayback playback) => playbackOverride = playback;

        private void OnEnable() { if (playOnEnable) Play(); }
        // Public parameterless methods work with UnityEvents, including UI Button.onClick.
        public void Play()
        {
            var playback = playbackOverride ?? GameAudio.Service;
            if (playback == null)
            { Debug.LogWarning("[Audio] Initialize an AudioRoot before playing an emitter.", this); return; }
            // Each emitter owns one voice. Use the service directly for overlapping requests.
            handle.Stop();
            var options = new AudioPlayOptions
            { Scope = scope, OwnerScene = gameObject.scene, FadeInSeconds = fadeInSeconds };
            switch (mode)
            {
                case AudioEmitterMode.WorldPosition:
                    handle = playback.PlayAt(cue, transform.position, options); break;
                case AudioEmitterMode.FollowTransform:
                    handle = playback.PlayAttached(cue, transform, options); break;
                default: handle = playback.Play(cue, options); break;
            }
        }
        public void Stop() => handle.Stop();
        public void Pause() => handle.Pause();
        public void Resume() => handle.Resume();
        private void OnDisable() { if (stopOnDisable) Stop(); }
    }
}
