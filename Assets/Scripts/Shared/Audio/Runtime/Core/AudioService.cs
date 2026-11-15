using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModuleFactory.Audio
{
    /// <summary>Main-thread service. AudioRoot normally owns its Tick and Dispose lifecycle.</summary>
    public sealed class AudioService : IAudioService, IAudioVoiceControl, IDisposable
    {
        private sealed class CueState { public int Previous = -1; public double NextAllowed; }
        private readonly Dictionary<AudioCue, CueState> states = new Dictionary<AudioCue, CueState>();
        private readonly AudioVoicePool pool;
        private readonly AudioVolumeController volume;
        private readonly MusicPlayer music;
        private readonly AudioSettings settings;
        private readonly IAudioClipLoader loader;
        private readonly int[] busCounts = new int[5];
        private readonly int[] busRequests = new int[5];
        private int budgetFrame = -1, frameRequests;
        private bool disposed, worldPaused;
        private long order;
        public bool IsDisposed => disposed;
        public int ActiveVoices { get; private set; }
        public int AllocatedVoices => pool.Voices.Count;
        public int ActiveMusicTracks => music.ActiveCount;
        public int ActiveTailVoices
        {
            get { int count = 0; foreach (var tail in pool.Tails) if (tail.Active) count++; return count; }
        }
        public int TailCapacity => pool.Tails.Count;
        public int BudgetRejectedRequests { get; private set; }
        public MusicRequestStatus MusicStatus => music.Status;
        public string LastMusicError => music.LastError;
        public AudioCue PendingMusicCue => music.PendingCue;
        public int RejectedRequests { get; private set; }
        public string LastRejection { get; private set; }

        public AudioService(AudioSettings settings, Transform parent, IAudioClipLoader clipLoader = null)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!settings.Validate(out var error)) throw new ArgumentException(error, nameof(settings));
            this.settings = settings;
            loader = clipLoader ?? new UnityAudioClipLoader();
            pool = new AudioVoicePool(parent, settings.PrewarmVoices, settings.MaxVoices, settings.MaxStealTails);
            volume = new AudioVolumeController(settings);
            music = new MusicPlayer(parent, volume, settings, loader);
            SceneManager.sceneUnloaded += SceneUnloaded;
        }
        private CueState State(AudioCue cue)
        {
            if (!states.TryGetValue(cue, out var state)) states.Add(cue, state = new CueState());
            return state;
        }
        private AudioHandle Reject(string reason)
        { RejectedRequests++; LastRejection = reason; return default; }
        private bool ConsumeBudget(AudioBus bus)
        {
            if (budgetFrame != Time.frameCount)
            {
                budgetFrame = Time.frameCount; frameRequests = 0;
                Array.Clear(busRequests, 0, busRequests.Length);
            }
            var policy = settings.FindPolicy(bus);
            if (frameRequests >= settings.MaxRequestsPerFrame ||
                (policy != null && policy.maxRequestsPerFrame > 0 && busRequests[(int)bus] >= policy.maxRequestsPerFrame))
            {
                BudgetRejectedRequests++; Reject("Per-frame request budget"); return false;
            }
            frameRequests++; busRequests[(int)bus]++; return true;
        }
        public AudioDataLoadState Preload(AudioCue cue, bool allowSynchronous = false)
        {
            if (disposed || cue == null || cue.Clips == null || cue.Clips.Length == 0) return AudioDataLoadState.Failed;
            var result = AudioDataLoadState.Loaded;
            bool any = false;
            foreach (var clip in cue.Clips)
            {
                if (clip == null) continue;
                any = true;
                if (!loader.RequestLoad(clip, allowSynchronous || settings.AllowSynchronousLoads)) return AudioDataLoadState.Failed;
                var state = loader.GetState(clip);
                if (state == AudioDataLoadState.Failed) return state;
                if (state != AudioDataLoadState.Loaded) result = AudioDataLoadState.Loading;
            }
            return any ? result : AudioDataLoadState.Failed;
        }
        public int GetActiveVoices(AudioBus bus) => busCounts[(int)bus];
        private bool CanUseFreeSlot(AudioBus bus)
        {
            int protectedSlots = 0;
            for (int i = 0; i < busCounts.Length; i++)
                if (i != (int)bus)
                    protectedSlots += Mathf.Max(0, (settings.FindPolicy((AudioBus)i)?.reservedVoices ?? 0) - busCounts[i]);
            return settings.MaxVoices - ActiveVoices > protectedSlots;
        }
        private bool CanSteal(AudioVoice voice, AudioCue cue, bool sameBusOnly)
        {
            if (!voice.Active || voice.Priority > cue.Priority) return false;
            if (sameBusOnly && voice.Bus != cue.Bus) return false;
            return voice.Bus == cue.Bus || busCounts[(int)voice.Bus] >
                (settings.FindPolicy(voice.Bus)?.reservedVoices ?? 0);
        }
        public AudioHandle Play(AudioCue cue, AudioPlayOptions options = default) =>
            PlayInternal(cue, Vector3.zero, null, false, options);
        public AudioHandle PlayAt(AudioCue cue, Vector3 position, AudioPlayOptions options = default) =>
            PlayInternal(cue, position, null, true, options);
        public AudioHandle PlayAttached(AudioCue cue, Transform target, AudioPlayOptions options = default)
        {
            if (target == null) return Reject("Missing follow target");
            if (!options.OwnerScene.HasValue) options.OwnerScene = target.gameObject.scene;
            return PlayInternal(cue, target.position, target, true, options);
        }
        private AudioHandle PlayInternal(AudioCue cue, Vector3 position, Transform target,
            bool spatial, AudioPlayOptions options)
        {
            if (disposed) return default;
            if (cue == null) return Reject("Missing cue");
            if (!ConsumeBudget(cue.Bus)) return default;
            var state = State(cue);
            if (Time.realtimeSinceStartupAsDouble < state.NextAllowed) return Reject("Cue cooldown");
            Scene scene = options.OwnerScene ?? SceneManager.GetActiveScene();
            if (options.Scope == AudioScope.Scene && (!scene.IsValid() || !scene.isLoaded))
                return Reject("Owner scene is not loaded");
            // Resolve the clip before stealing an existing voice. Failed requests do not consume history.
            int previous = state.Previous;
            var clip = cue.Select(ref previous);
            if (clip == null) return Reject("Cue contains no clips");
            if (loader.GetState(clip) != AudioDataLoadState.Loaded)
                return Reject("Audio data not ready; call Preload before gameplay");
            int count = 0;
            AudioVoice oldestSame = null;
            foreach (var item in pool.Voices)
                if (item.Active && item.Cue == cue)
                { count++; if (oldestSame == null || item.Order < oldestSame.Order) oldestSame = item; }
            AudioVoice voice;
            if (count >= cue.MaxInstances)
            {
                if (cue.Overflow == VoiceOverflow.Reject) return Reject("Cue instance limit");
                voice = oldestSame;
            }
            else
            {
                var policy = settings.FindPolicy(cue.Bus);
                bool atBusLimit = policy != null && policy.maxVoices > 0 && busCounts[(int)cue.Bus] >= policy.maxVoices;
                voice = !atBusLimit && CanUseFreeSlot(cue.Bus) ? pool.Acquire() : null;
            }
            if (voice == null)
            {
                var policy = settings.FindPolicy(cue.Bus);
                bool sameBusOnly = policy != null && policy.maxVoices > 0 && busCounts[(int)cue.Bus] >= policy.maxVoices;
                foreach (var item in pool.Voices)
                    if (CanSteal(item, cue, sameBusOnly) && (voice == null ||
                        item.Priority < voice.Priority ||
                        (item.Priority == voice.Priority && item.Order < voice.Order))) voice = item;
                if (voice == null) return Reject("Voice capacity, bus reservation or priority limit");
            }
            if (voice.Active)
            {
                if (!pool.BeginSteal(voice, settings.StealFadeSeconds)) return Reject("Steal tail capacity reached");
                Release(voice);
            }
            voice.Version++;
            voice.Active = true;
            voice.Cue = cue;
            voice.Bus = cue.Bus;
            voice.Priority = cue.Priority;
            voice.Order = ++order;
            voice.Target = target;
            voice.Following = target != null;
            voice.TargetLost = options.TargetLost;
            voice.Scope = options.Scope;
            voice.SceneHandle = scene.handle;
            voice.UserPaused = false;
            voice.WorldPaused = worldPaused && cue.Bus != AudioBus.UI;
            voice.Stopping = false;
            voice.BaseVolume = cue.SampleVolume();
            voice.Multiplier = Mathf.Clamp01(options.VolumeMultiplier ?? 1);
            voice.Gain = options.FadeInSeconds > 0 ? 0 : 1;
            voice.Fade(1, options.FadeInSeconds);
            var source = voice.Source;
            source.transform.position = position;
            source.clip = clip;
            source.loop = cue.Loop;
            source.pitch = Mathf.Clamp(cue.SamplePitch() * (options.PitchMultiplier ?? 1), 0.01f, 3);
            source.spatialBlend = spatial ? cue.SpatialBlend : 0;
            source.minDistance = cue.MinDistance;
            source.maxDistance = cue.MaxDistance;
            source.rolloffMode = cue.Rolloff;
            source.priority = 255 - cue.Priority;
            source.ignoreListenerPause = cue.Bus == AudioBus.UI;
            source.outputAudioMixerGroup = volume.Group(cue.Bus);
            source.volume = voice.BaseVolume * voice.Multiplier * voice.Gain * volume.SourceGain(cue.Bus);
            source.Play();
            if (voice.Paused) source.Pause();
            ActiveVoices++;
            busCounts[(int)voice.Bus]++;
            state.Previous = previous;
            state.NextAllowed = Time.realtimeSinceStartupAsDouble + cue.Cooldown;
            return new AudioHandle(this, pool.Voices.IndexOf(voice), voice.Version);
        }
        private void Release(AudioVoice voice)
        { busCounts[(int)voice.Bus]--; pool.Release(voice); ActiveVoices--; }
        public void Tick(float unscaledDeltaTime)
        {
            if (disposed) return;
            volume.Apply();
            float delta = Mathf.Max(0, unscaledDeltaTime);
            foreach (var voice in pool.Voices)
            {
                if (!voice.Active) continue;
                if (!TickVoice(voice, delta)) Release(voice);
            }
            foreach (var tail in pool.Tails) if (tail.Active && !TickVoice(tail, delta)) pool.Release(tail);
            music.Tick(delta);
        }
        private bool TickVoice(AudioVoice voice, float delta)
        {
            if (voice.Cue == null) return false;
            if (voice.Following)
            {
                if (voice.Target != null) voice.Source.transform.position = voice.Target.position;
                else if (voice.TargetLost == TargetLostBehaviour.Stop) return false;
                else voice.Following = false;
            }
            if (!voice.Paused || voice.Stopping) voice.TickFade(delta);
            if ((voice.Stopping && voice.FadeDuration == 0) ||
                (!voice.Paused && !(AudioListener.pause && !voice.Source.ignoreListenerPause) && !voice.Source.isPlaying)) return false;
            voice.Source.volume = voice.BaseVolume * voice.Multiplier * voice.Gain * volume.SourceGain(voice.Bus);
            return true;
        }
        private bool IsValid(int slot, ulong version) => !disposed && slot >= 0 && slot < pool.Voices.Count &&
            pool.Voices[slot].Active && pool.Voices[slot].Version == version;
        bool IAudioVoiceControl.IsValid(int slot, ulong version) => IsValid(slot, version);
        bool IAudioVoiceControl.IsPaused(int slot, ulong version) => IsValid(slot, version) && pool.Voices[slot].Paused;
        void IAudioVoiceControl.Stop(int slot, ulong version, float fadeOutSeconds) => Stop(slot, version, fadeOutSeconds);

        private void Stop(int slot, ulong version, float seconds)
        {
            if (!IsValid(slot, version)) return;
            var voice = pool.Voices[slot];
            if (seconds <= 0) Release(voice);
            else { voice.Stopping = true; voice.Fade(0, seconds); }
        }
        void IAudioVoiceControl.Pause(int slot, ulong version, bool paused)
        {
            if (!IsValid(slot, version)) return;
            var voice = pool.Voices[slot];
            bool before = voice.Paused;
            voice.UserPaused = paused;
            ApplyPause(voice, before);
        }
        private static void ApplyPause(AudioVoice voice, bool before)
        {
            if (before == voice.Paused) return;
            if (voice.Paused) voice.Source.Pause(); else voice.Source.UnPause();
        }
        void IAudioVoiceControl.SetVolume(int slot, ulong version, float multiplier)
        { if (IsValid(slot, version)) pool.Voices[slot].Multiplier = Mathf.Clamp01(multiplier); }
        public void SetWorldPaused(bool paused)
        {
            if (disposed) return;
            worldPaused = paused;
            foreach (var voice in pool.Voices)
            {
                if (!voice.Active) continue;
                bool before = voice.Paused;
                voice.WorldPaused = paused && voice.Bus != AudioBus.UI;
                ApplyPause(voice, before);
            }
            foreach (var tail in pool.Tails)
            {
                if (!tail.Active) continue;
                bool before = tail.Paused;
                tail.WorldPaused = paused && tail.Bus != AudioBus.UI;
                ApplyPause(tail, before);
            }
        }
        public void StopBus(AudioBus bus, float fadeOutSeconds = 0)
        {
            if (disposed) return;
            for (int i = 0; i < pool.Voices.Count; i++)
            {
                var voice = pool.Voices[i];
                if (voice.Active && (bus == AudioBus.Master || voice.Bus == bus)) Stop(i, voice.Version, fadeOutSeconds);
            }
            foreach (var tail in pool.Tails)
                if (tail.Active && (bus == AudioBus.Master || tail.Bus == bus))
                {
                    if (fadeOutSeconds <= 0) pool.Release(tail);
                    else tail.Fade(0, Mathf.Min(fadeOutSeconds, tail.FadeDuration));
                }
            if (bus == AudioBus.Master || bus == AudioBus.Music) music.Stop(fadeOutSeconds);
        }
        public void PlayMusic(AudioCue cue, float crossFadeSeconds = 0.5f)
        {
            if (disposed) return;
            if (cue == null) { music.Reject("Missing music cue"); Reject("Missing music cue"); return; }
            if (!ConsumeBudget(AudioBus.Music)) return;
            var state = State(cue);
            int previous = state.Previous;
            var clip = cue.Select(ref previous);
            if (clip == null) { music.Reject("Music cue contains no clips"); Reject("Music cue contains no clips"); return; }
            state.Previous = previous;
            music.Play(cue, clip, Mathf.Max(0, crossFadeSeconds));
        }
        public void StopMusic(float fadeOutSeconds = 0.5f) { if (!disposed) music.Stop(fadeOutSeconds); }
        public void PauseMusic() { if (!disposed) music.Pause(true); }
        public void ResumeMusic() { if (!disposed) music.Pause(false); }
        public void SetVolume(AudioBus bus, float normalizedVolume) { if (!disposed) volume.Set(bus, normalizedVolume); }
        public float GetVolume(AudioBus bus) => volume.Get(bus);
        public void SetMuted(AudioBus bus, bool muted) { if (!disposed) volume.Mute(bus, muted); }
        public bool IsMuted(AudioBus bus) => volume.IsMuted(bus);
        public void LoadPreferences() { if (!disposed) volume.Load(); }
        public void SavePreferences() { if (!disposed) volume.Save(); }
        private void SceneUnloaded(Scene scene)
        {
            foreach (var voice in pool.Voices)
                if (voice.Active && voice.Scope == AudioScope.Scene && voice.SceneHandle == scene.handle) Release(voice);
            foreach (var tail in pool.Tails)
                if (tail.Active && tail.Scope == AudioScope.Scene && tail.SceneHandle == scene.handle) pool.Release(tail);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SceneManager.sceneUnloaded -= SceneUnloaded;
            music.Dispose();
            pool.Dispose();
            states.Clear();
            ActiveVoices = 0;
            Array.Clear(busCounts, 0, busCounts.Length);
        }
    }
}
