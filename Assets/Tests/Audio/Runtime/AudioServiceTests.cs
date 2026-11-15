using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ModuleFactory.Audio.Tests
{
    public sealed class AudioServiceTests
    {
        private sealed class PlaybackStub : IAudioPlayback, IAudioVoiceControl
        {
            public AudioCue LastCue { get; private set; }
            private bool playing;
            private bool paused;

            public AudioHandle Play(AudioCue cue, AudioPlayOptions options = default)
            {
                LastCue = cue;
                playing = true;
                return new AudioHandle(this, 0, 1);
            }

            public AudioHandle PlayAt(AudioCue cue, Vector3 position, AudioPlayOptions options = default) => Play(cue, options);
            public AudioHandle PlayAttached(AudioCue cue, Transform target, AudioPlayOptions options = default) => Play(cue, options);
            public bool IsValid(int slot, ulong version) => playing && slot == 0 && version == 1;
            public bool IsPaused(int slot, ulong version) => IsValid(slot, version) && paused;
            public void Stop(int slot, ulong version, float fadeOutSeconds)
            {
                if (IsValid(slot, version))
                    playing = false;
            }

            public void Pause(int slot, ulong version, bool paused)
            {
                if (IsValid(slot, version))
                    this.paused = paused;
            }

            public void SetVolume(int slot, ulong version, float multiplier) { }
        }

        private GameObject host;
        private AudioSettings settings;
        private AudioService service;
        private AudioClip clip;
        private readonly List<AudioCue> cues = new List<AudioCue>();
        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private AudioCue Cue(AudioBus bus = AudioBus.SFX)
        {
            var cue = AudioCue.CreateTransient(new[] { clip }, bus, true);
            cues.Add(cue);
            return cue;
        }
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Audio test");
            host.AddComponent<AudioListener>();
            settings = ScriptableObject.CreateInstance<AudioSettings>();
            Set(settings, "prewarmVoices", 0);
            Set(settings, "maxVoices", 2);
            service = new AudioService(settings, host.transform);
            clip = AudioClip.Create("Test", 44100 * 2, 1, 44100, false);
        }
        [TearDown]
        public void TearDown()
        {
            service.Dispose();
            foreach (var cue in cues) Object.DestroyImmediate(cue);
            cues.Clear();
            Object.DestroyImmediate(clip);
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(host);
        }
        [Test]
        public void EmitterCanUsePlaybackWithItsOwnVoiceHandle()
        {
            var emitter = host.AddComponent<AudioEmitter>();
            var cue = Cue();
            Set(emitter, "cue", cue);
            var playback = new PlaybackStub();
            emitter.SetPlayback(playback);

            emitter.Play();
            Assert.AreSame(cue, playback.LastCue);
            Assert.IsTrue(emitter.Handle.IsValid);
            emitter.Pause();
            Assert.IsTrue(emitter.Handle.IsPaused);
            emitter.Resume();
            Assert.IsFalse(emitter.Handle.IsPaused);
            emitter.Stop();
            Assert.IsFalse(emitter.Handle.IsValid);
        }

        [Test]
        public void ReusedSlotCannotBeStoppedByOldHandle()
        {
            var cue = Cue();
            var old = service.Play(cue);
            old.Stop();
            var current = service.Play(cue);
            old.Stop(); old.Pause(); old.SetVolume(0);
            Assert.IsFalse(old.IsValid);
            Assert.IsTrue(current.IsValid);
            Assert.IsFalse(current.IsPaused);
        }
        [Test]
        public void DefaultOptionsAndDefaultHandleAreSafe()
        {
            default(AudioHandle).Stop(); default(AudioHandle).Pause();
            Assert.IsTrue(service.Play(Cue()).IsValid);
            AudioSource playing = null;
            foreach (var source in host.GetComponentsInChildren<AudioSource>())
                if (source.clip == clip) playing = source;
            Assert.IsNotNull(playing);
            Assert.AreEqual(1, playing.volume);
        }
        [Test]
        public void CueLimitRejectsWithoutStoppingExistingVoice()
        {
            var cue = Cue(); Set(cue, "maxInstances", 1);
            var first = service.Play(cue);
            Assert.IsFalse(service.Play(cue).IsValid);
            Assert.IsTrue(first.IsValid);
            Assert.AreEqual(1, service.RejectedRequests);
        }
        [Test]
        public void CueOverflowStealsOldestHandle()
        {
            var cue = Cue(); Set(cue, "maxInstances", 1); Set(cue, "overflow", VoiceOverflow.StealOldest);
            var old = service.Play(cue);
            var next = service.Play(cue);
            Assert.IsFalse(old.IsValid); Assert.IsTrue(next.IsValid);
            Assert.AreEqual(1, service.ActiveVoices);
        }
        [Test]
        public void PoolProtectsHigherPriorityVoices()
        {
            var important = Cue(); Set(important, "priority", 200);
            var low = Cue(); Set(low, "priority", 10);
            var a = service.Play(important); var b = service.Play(important);
            Assert.IsFalse(service.Play(low).IsValid);
            Assert.IsTrue(a.IsValid && b.IsValid);
            Assert.AreEqual(2, service.AllocatedVoices);
        }
        [Test]
        public void SourceFallbackAppliesMasterAndCategoryExactlyOnce()
        {
            var handle = service.Play(Cue());
            handle.Pause();
            service.SetVolume(AudioBus.Master, 0.5f);
            service.SetVolume(AudioBus.SFX, 0.4f);
            service.Tick(0);
            AudioSource playing = null;
            foreach (var source in host.GetComponentsInChildren<AudioSource>())
                if (source.clip == clip) playing = source;
            Assert.That(playing.volume, Is.EqualTo(0.2f).Within(0.0001f));
            service.SetMuted(AudioBus.SFX, true); service.Tick(0);
            Assert.AreEqual(0, playing.volume);
            service.SetMuted(AudioBus.SFX, false); service.Tick(0);
            Assert.That(playing.volume, Is.EqualTo(0.2f).Within(0.0001f));
        }
        [Test]
        public void HigherPriorityRequestStealsLowestPriorityVoice()
        {
            var low = Cue(); Set(low, "priority", 10);
            var medium = Cue(); Set(medium, "priority", 100);
            var high = Cue(); Set(high, "priority", 200);
            var a = service.Play(low); var b = service.Play(medium);
            var c = service.Play(high);
            Assert.IsFalse(a.IsValid); Assert.IsTrue(b.IsValid && c.IsValid);
            Assert.AreEqual(2, service.ActiveVoices);
        }
        [Test]
        public void MissingClipNeverStealsExistingVoice()
        {
            var a = service.Play(Cue()); var b = service.Play(Cue());
            var empty = AudioCue.CreateTransient(null); cues.Add(empty);
            Assert.IsFalse(service.Play(empty).IsValid);
            Assert.IsTrue(a.IsValid && b.IsValid);
        }
        [Test]
        public void CooldownDoesNotConsumeAnotherVoice()
        {
            var cue = Cue(); Set(cue, "cooldown", 10f);
            Assert.IsTrue(service.Play(cue).IsValid);
            Assert.IsFalse(service.Play(cue).IsValid);
            Assert.AreEqual(1, service.ActiveVoices);
        }
        [Test]
        public void PauseAndWorldPauseAreIndependent()
        {
            var handle = service.Play(Cue()); handle.Pause();
            service.SetWorldPaused(true); service.SetWorldPaused(false);
            service.Tick(1);
            Assert.IsTrue(handle.IsValid && handle.IsPaused);
            handle.Resume(); Assert.IsFalse(handle.IsPaused);
        }
        [Test]
        public void UiIsNotPausedWithWorld()
        {
            service.SetWorldPaused(true);
            Assert.IsFalse(service.Play(Cue(AudioBus.UI)).IsPaused);
            Assert.IsTrue(service.Play(Cue()).IsPaused);
        }
        [Test]
        public void StopFadeCompletesWhilePaused()
        {
            var handle = service.Play(Cue()); handle.Pause(); handle.Stop(0.2f);
            service.Tick(0.3f);
            Assert.IsFalse(handle.IsValid);
        }
        [Test]
        public void DestroyedTargetStopsFollowingVoice()
        {
            var target = new GameObject("Target");
            var handle = service.PlayAttached(Cue(), target.transform);
            Object.DestroyImmediate(target); service.Tick(0);
            Assert.IsFalse(handle.IsValid);
        }
        [Test]
        public void MusicKeepsAtMostTwoTracksDuringRapidSwitches()
        {
            for (int i = 0; i < 10; i++) service.PlayMusic(Cue(AudioBus.Music), 1);
            Assert.AreEqual(2, service.ActiveMusicTracks);
            service.PauseMusic(); service.StopMusic(0.2f); service.Tick(0.3f);
            Assert.AreEqual(0, service.ActiveMusicTracks);
        }
        [Test]
        public void DisposalInvalidatesHandlesAndIsIdempotent()
        {
            var handle = service.Play(Cue()); service.Dispose(); service.Dispose();
            handle.Stop(); Assert.IsFalse(handle.IsValid);
            Assert.IsFalse(service.Play(Cue()).IsValid);
        }
        [UnityTest]
        public IEnumerator SceneUnloadStopsOnlySceneOwnedVoices()
        {
            var scene = SceneManager.CreateScene("Audio owned scene");
            var cue = Cue();
            var local = service.Play(cue, new AudioPlayOptions { OwnerScene = scene });
            var global = service.Play(cue, new AudioPlayOptions { Scope = AudioScope.Global });
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.IsFalse(local.IsValid); Assert.IsTrue(global.IsValid);
        }
        [UnityTest]
        public IEnumerator OneShotNaturallyReturnsToPool()
        {
            var cue = Cue(); Set(cue, "loop", false);
            var shortClip = AudioClip.Create("Short", 441, 1, 44100, false);
            Set(cue, "clips", new[] { shortClip });
            var handle = service.Play(cue);
            yield return new WaitForSecondsRealtime(0.3f);
            service.Tick(0);
            Assert.IsFalse(handle.IsValid);
            Assert.AreEqual(0, service.ActiveVoices);
            Object.DestroyImmediate(shortClip);
        }
    }
}
