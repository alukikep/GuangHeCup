using UnityEngine;

namespace ModuleFactory.Audio
{
    internal sealed class MusicPlayer
    {
        private sealed class Track
        {
            public AudioSource Source;
            public AudioCue Cue;
            public float Gain, From, To, Time, Duration, BaseVolume;
            public void Fade(float to, float seconds)
            {
                From = Gain; To = to; Time = 0; Duration = Mathf.Max(0, seconds);
                if (Duration == 0) Gain = to;
            }
            public void Clear() { Source.Stop(); Source.clip = null; Cue = null; Gain = 0; Duration = 0; }
        }
        private readonly Track[] tracks = new Track[2];
        private readonly AudioVolumeController volume;
        private readonly AudioSettings settings;
        private readonly IAudioClipLoader loader;
        private AudioCue pendingCue;
        private AudioClip pendingClip;
        private Track starting;
        private float pendingFade, elapsed;
        private bool paused, pending;
        public MusicRequestStatus Status { get; private set; }
        public string LastError { get; private set; }
        public AudioCue PendingCue => pending ? pendingCue : null;
        public int ActiveCount => (tracks[0].Cue != null ? 1 : 0) + (tracks[1].Cue != null ? 1 : 0);

        public MusicPlayer(Transform parent, AudioVolumeController volume, AudioSettings settings, IAudioClipLoader loader)
        {
            this.volume = volume; this.settings = settings; this.loader = loader;
            for (int i = 0; i < tracks.Length; i++)
            {
                var go = new GameObject("Music " + i);
                go.transform.SetParent(parent, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.ignoreListenerPause = true;
                tracks[i] = new Track { Source = source };
            }
        }
        private void CancelPending()
        {
            // Cancellation detaches interest; Unity's shared clip load is not aborted or unloaded.
            if (starting != null) starting.Clear();
            starting = null; pendingCue = null; pendingClip = null; pending = false;
        }
        private void Fail(string error)
        { CancelPending(); Status = MusicRequestStatus.Failed; LastError = error; }
        public void Reject(string error) => Fail(error);

        public void Play(AudioCue cue, AudioClip clip, float seconds)
        {
            if (pending && pendingCue == cue) return; // Do not restart an in-flight load/timeout.
            CancelPending(); LastError = null;
            foreach (var track in tracks)
                if (track.Cue == cue && track.Source.clip != null && (paused || track.Source.isPlaying))
                {
                    track.Fade(1, seconds);
                    foreach (var other in tracks) if (other != track) other.Fade(0, seconds);
                    Status = MusicRequestStatus.Playing;
                    return;
                }
            pending = true; pendingCue = cue; pendingClip = clip;
            pendingFade = seconds; elapsed = 0; Status = MusicRequestStatus.Loading;
            if (!loader.RequestLoad(clip, settings.AllowSynchronousLoads))
            { Fail("Cannot prepare music. Enable Load In Background or preload its data during a loading screen."); return; }
            Prepare();
        }
        private void Prepare()
        {
            if (!pending || starting != null) return;
            if (pendingCue == null || pendingClip == null) { Fail("Music asset was destroyed."); return; }
            var state = loader.GetState(pendingClip);
            if (state == AudioDataLoadState.Failed) { Fail("Music data loading failed."); return; }
            if (state != AudioDataLoadState.Loaded || paused) return;
            // Preserve the louder track during rapid switches. At most two music sources exist.
            var next = tracks[0].Cue == null ? tracks[0] : tracks[1].Cue == null ? tracks[1] :
                tracks[0].Gain <= tracks[1].Gain ? tracks[0] : tracks[1];
            next.Clear(); next.Cue = pendingCue; next.BaseVolume = pendingCue.SampleVolume();
            next.Source.clip = pendingClip; next.Source.loop = pendingCue.Loop;
            next.Source.pitch = pendingCue.SamplePitch();
            next.Source.outputAudioMixerGroup = volume.Group(AudioBus.Music);
            next.Source.volume = 0;
            starting = next; Status = MusicRequestStatus.Starting;
            next.Source.Play();
            CommitIfStarted();
        }
        private void CommitIfStarted()
        {
            if (starting == null || paused || !starting.Source.isPlaying) return;
            // The outgoing track is untouched until playback actually starts.
            var incoming = starting;
            foreach (var track in tracks) if (track != incoming) track.Fade(0, pendingFade);
            incoming.Fade(1, pendingFade);
            starting = null; pending = false; pendingCue = null; pendingClip = null;
            Status = MusicRequestStatus.Playing;
        }
        public void Stop(float seconds)
        {
            CancelPending(); Status = MusicRequestStatus.Cancelled; LastError = null;
            foreach (var track in tracks)
                if (seconds <= 0) track.Clear(); else track.Fade(0, seconds);
        }
        public void Pause(bool value)
        {
            if (paused == value) return;
            paused = value;
            foreach (var track in tracks) if (track.Cue != null)
            { if (paused) track.Source.Pause(); else track.Source.UnPause(); }
        }
        public void Tick(float delta)
        {
            if (pending)
            {
                if (pendingCue == null || pendingClip == null) Fail("Music asset was destroyed.");
                else
                {
                    // User pause suspends the timeout and defers starting a new track.
                    if (!paused) elapsed += delta;
                    if (elapsed >= settings.MusicLoadTimeout) Fail("Music preparation timed out; previous track retained.");
                    else if (loader.GetState(pendingClip) == AudioDataLoadState.Failed) Fail("Music data loading failed.");
                    else { Prepare(); CommitIfStarted(); }
                }
            }
            foreach (var track in tracks)
            {
                if (track == starting) continue; // isPlaying == false is not completion while starting.
                if (track.Cue == null)
                { if (track.Source.clip != null) track.Clear(); continue; }
                if (track.Duration > 0 && (!paused || track.To == 0))
                {
                    track.Time += delta;
                    track.Gain = Mathf.Lerp(track.From, track.To, track.Time / track.Duration);
                    if (track.Time >= track.Duration) track.Duration = 0;
                }
                if ((track.To == 0 && track.Duration == 0) || (!paused && !track.Source.isPlaying))
                { track.Clear(); continue; }
                track.Source.volume = track.BaseVolume * track.Gain * volume.SourceGain(AudioBus.Music);
            }
        }
        public void Dispose()
        {
            CancelPending(); Status = MusicRequestStatus.Cancelled;
            foreach (var track in tracks) { track.Clear(); AudioObjects.Destroy(track.Source.gameObject); }
        }
    }
}
