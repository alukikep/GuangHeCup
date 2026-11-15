using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ModuleFactory.Audio.Tests
{
    public sealed class AudioBoundaryTests
    {
        private sealed class ControlledLoader : IAudioClipLoader
        {
            public readonly Dictionary<AudioClip, AudioDataLoadState> States = new Dictionary<AudioClip, AudioDataLoadState>();
            public bool Accept = true;
            public int Loads;
            public AudioDataLoadState GetState(AudioClip clip) =>
                States.TryGetValue(clip, out var state) ? state : AudioDataLoadState.Loaded;
            public bool RequestLoad(AudioClip clip, bool allowSynchronous) { Loads++; return Accept; }
        }
        private GameObject host;
        private AudioSettings settings;
        private AudioService service;
        private ControlledLoader loader;
        private readonly List<Object> assets = new List<Object>();
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        private AudioCue Cue(AudioBus bus = AudioBus.SFX)
        {
            var clip = AudioClip.Create("Boundary clip " + assets.Count, 88200, 1, 44100, false);
            var cue = AudioCue.CreateTransient(new[] { clip }, bus, true);
            Set(cue, "maxInstances", 100);
            assets.Add(clip); assets.Add(cue);
            return cue;
        }
        private static AudioClip Clip(AudioCue cue) =>
            ((AudioClip[])typeof(AudioCue).GetField("clips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cue))[0];
        private void StartService() => service = new AudioService(settings, host.transform, loader);
        private AudioSource MusicSource(AudioCue cue)
        {
            foreach (var source in host.GetComponentsInChildren<AudioSource>())
                if (source.name.StartsWith("Music ") && source.clip == Clip(cue)) return source;
            return null;
        }
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Audio boundaries"); host.AddComponent<AudioListener>();
            settings = ScriptableObject.CreateInstance<AudioSettings>();
            Set(settings, "maxVoices", 4); Set(settings, "prewarmVoices", 4);
            Set(settings, "maxStealTails", 2); Set(settings, "musicLoadTimeout", 1f);
            loader = new ControlledLoader();
        }
        [TearDown]
        public void TearDown()
        {
            service?.Dispose();
            foreach (var asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear(); Object.DestroyImmediate(settings); Object.DestroyImmediate(host);
        }
        private AudioCue PlayOldMusic()
        {
            var old = Cue(AudioBus.Music); service.PlayMusic(old, 0); service.Tick(0);
            Assert.AreEqual(MusicRequestStatus.Playing, service.MusicStatus);
            Assert.That(MusicSource(old).volume, Is.GreaterThan(0));
            return old;
        }
        [Test]
        public void LoadingMusicKeepsOldTrackUntilNewDataIsReady()
        {
            StartService(); var old = PlayOldMusic(); var next = Cue(AudioBus.Music);
            loader.States[Clip(next)] = AudioDataLoadState.Loading;
            service.PlayMusic(next, 0.2f); service.Tick(0.2f);
            Assert.AreEqual(MusicRequestStatus.Loading, service.MusicStatus);
            Assert.IsNull(MusicSource(next)); Assert.That(MusicSource(old).volume, Is.EqualTo(1));
            loader.States[Clip(next)] = AudioDataLoadState.Loaded;
            service.Tick(0); service.Tick(0.3f);
            Assert.AreEqual(MusicRequestStatus.Playing, service.MusicStatus);
            Assert.IsNotNull(MusicSource(next)); Assert.IsNull(MusicSource(old));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void FailedOrTimedOutPreparationKeepsOldMusic(bool timeout)
        {
            StartService(); var old = PlayOldMusic(); var next = Cue(AudioBus.Music);
            loader.States[Clip(next)] = AudioDataLoadState.Loading;
            service.PlayMusic(next);
            if (!timeout) loader.States[Clip(next)] = AudioDataLoadState.Failed;
            service.Tick(timeout ? 1.1f : 0);
            Assert.AreEqual(MusicRequestStatus.Failed, service.MusicStatus);
            Assert.IsNotEmpty(service.LastMusicError); Assert.IsNull(service.PendingMusicCue);
            Assert.That(MusicSource(old).volume, Is.EqualTo(1));
        }
        [Test]
        public void RefusedLoadDoesNotFadeOutCurrentTrack()
        {
            StartService(); var old = PlayOldMusic(); loader.Accept = false;
            service.PlayMusic(Cue(AudioBus.Music)); service.Tick(0.2f);
            Assert.AreEqual(MusicRequestStatus.Failed, service.MusicStatus);
            Assert.That(MusicSource(old).volume, Is.EqualTo(1));
        }
        [Test]
        public void LatestMusicRequestWinsWhenOlderLoadingCompletesLate()
        {
            StartService(); PlayOldMusic(); var b = Cue(AudioBus.Music); var c = Cue(AudioBus.Music);
            loader.States[Clip(b)] = AudioDataLoadState.Loading;
            loader.States[Clip(c)] = AudioDataLoadState.Loading;
            service.PlayMusic(b); service.PlayMusic(c);
            loader.States[Clip(b)] = AudioDataLoadState.Loaded; service.Tick(0.1f);
            Assert.IsNull(MusicSource(b)); Assert.AreEqual(c, service.PendingMusicCue);
            loader.States[Clip(c)] = AudioDataLoadState.Loaded; service.Tick(0.1f);
            Assert.IsNotNull(MusicSource(c)); Assert.IsNull(MusicSource(b));
        }
        [Test]
        public void StopCancelsPendingMusicAndLateCompletionCannotRestartIt()
        {
            StartService(); var next = Cue(AudioBus.Music);
            loader.States[Clip(next)] = AudioDataLoadState.Loading;
            service.PlayMusic(next); service.StopMusic(0);
            loader.States[Clip(next)] = AudioDataLoadState.Loaded; service.Tick(1);
            Assert.AreEqual(0, service.ActiveMusicTracks); Assert.IsNull(service.PendingMusicCue);
            Assert.AreEqual(MusicRequestStatus.Cancelled, service.MusicStatus);
        }
        [Test]
        public void RepeatingPendingCueDoesNotRestartLoadOrTimeout()
        {
            StartService(); var next = Cue(AudioBus.Music);
            loader.States[Clip(next)] = AudioDataLoadState.Loading;
            service.PlayMusic(next); service.Tick(0.6f); service.PlayMusic(next); service.Tick(0.5f);
            Assert.AreEqual(1, loader.Loads); Assert.AreEqual(MusicRequestStatus.Failed, service.MusicStatus);
        }
        [Test]
        public void PausingDefersMusicStartAndSuspendsPreparationTimeout()
        {
            StartService(); var next = Cue(AudioBus.Music);
            service.PauseMusic(); service.PlayMusic(next); service.Tick(20);
            Assert.AreEqual(MusicRequestStatus.Loading, service.MusicStatus);
            Assert.AreEqual(0, service.ActiveMusicTracks);
            service.ResumeMusic(); service.Tick(0);
            Assert.AreEqual(MusicRequestStatus.Playing, service.MusicStatus);
        }
        [Test]
        public void UnreadyEffectCannotStealExistingVoice()
        {
            StartService(); var cue = Cue(); var handle = service.Play(cue);
            var unavailable = Cue(); loader.States[Clip(unavailable)] = AudioDataLoadState.Loading;
            Assert.IsFalse(service.Play(unavailable).IsValid);
            Assert.IsTrue(handle.IsValid); Assert.AreEqual(1, service.ActiveVoices);
        }
        [Test]
        public void PreloadReportsPendingReadyAndFailureWithoutAllocatingVoices()
        {
            StartService(); var cue = Cue(); loader.States[Clip(cue)] = AudioDataLoadState.Loading;
            Assert.AreEqual(AudioDataLoadState.Loading, service.Preload(cue));
            loader.States[Clip(cue)] = AudioDataLoadState.Loaded;
            Assert.AreEqual(AudioDataLoadState.Loaded, service.Preload(cue));
            loader.Accept = false;
            Assert.AreEqual(AudioDataLoadState.Failed, service.Preload(cue)); Assert.AreEqual(0, service.ActiveVoices);
        }
        [Test]
        public void TenThousandRequestsRemainWithinVoiceTailAndFrameBudgets()
        {
            StartService(); var cue = Cue(); var timer = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) service.Play(cue);
            timer.Stop();
            Assert.AreEqual(10000 - settings.MaxRequestsPerFrame, service.BudgetRejectedRequests);
            Assert.LessOrEqual(service.ActiveVoices, settings.MaxVoices);
            Assert.LessOrEqual(service.ActiveTailVoices, settings.MaxStealTails);
            Assert.AreEqual(settings.MaxVoices + settings.MaxStealTails + 2, host.GetComponentsInChildren<AudioSource>().Length);
            TestContext.WriteLine("10,000 same-frame requests: " + timer.Elapsed.TotalMilliseconds + " ms (editor diagnostic, not a device benchmark).");
        }
        [UnityTest]
        public IEnumerator RequestBudgetResetsOnNextUnityFrame()
        {
            Set(settings, "maxRequestsPerFrame", 1); StartService(); var cue = Cue();
            Assert.IsTrue(service.Play(cue).IsValid); Assert.IsFalse(service.Play(cue).IsValid);
            yield return null;
            Assert.IsTrue(service.Play(cue).IsValid);
        }
        [Test]
        public void SfxCannotOccupyOrStealReservedUiVoice()
        {
            Set(settings, "busPolicies", new[] { new AudioBusPolicy { bus = AudioBus.UI, reservedVoices = 1, maxVoices = 1 } });
            StartService(); var sfx = Cue(); Set(sfx, "priority", 255);
            for (int i = 0; i < 4; i++) service.Play(sfx);
            Assert.AreEqual(3, service.GetActiveVoices(AudioBus.SFX));
            var ui = Cue(AudioBus.UI); Set(ui, "priority", 0);
            var protectedHandle = service.Play(ui); Assert.IsTrue(protectedHandle.IsValid);
            service.Tick(0.1f); service.Play(sfx);
            Assert.IsTrue(protectedHandle.IsValid); Assert.AreEqual(1, service.GetActiveVoices(AudioBus.UI));
        }
        [Test]
        public void BusVoiceLimitAndBusRequestBudgetApplyIndependently()
        {
            Set(settings, "busPolicies", new[] { new AudioBusPolicy { bus = AudioBus.SFX, maxVoices = 1, maxRequestsPerFrame = 2 } });
            StartService(); var cue = Cue();
            service.Play(cue); service.Play(cue);
            Assert.AreEqual(1, service.GetActiveVoices(AudioBus.SFX));
            Assert.IsFalse(service.Play(cue).IsValid); Assert.AreEqual(1, service.BudgetRejectedRequests);
            Assert.IsTrue(service.Play(Cue(AudioBus.UI)).IsValid);
        }
        [Test]
        public void StealingFadesOldSourceWithoutRestartingAndSaturatedTailsReject()
        {
            Set(settings, "maxVoices", 1); Set(settings, "prewarmVoices", 1); Set(settings, "maxStealTails", 1);
            StartService(); var cue = Cue(); var old = service.Play(cue);
            AudioSource original = null;
            foreach (var source in host.GetComponentsInChildren<AudioSource>()) if (source.clip == Clip(cue)) original = source;
            var current = service.Play(cue);
            Assert.IsFalse(old.IsValid); Assert.IsTrue(current.IsValid);
            Assert.IsTrue(original.isPlaying); Assert.AreEqual(1, service.ActiveTailVoices);
            Assert.IsFalse(service.Play(cue).IsValid); Assert.IsTrue(current.IsValid);
            service.Tick(settings.StealFadeSeconds * 0.5f);
            Assert.That(original.volume, Is.EqualTo(0.5f).Within(0.01f));
            service.Tick(settings.StealFadeSeconds);
            Assert.AreEqual(0, service.ActiveTailVoices); Assert.IsNull(original.clip);
            Assert.IsTrue(service.Play(cue).IsValid);
        }
        [UnityTest]
        public IEnumerator SceneUnloadAndMasterStopAlsoClearStealTails()
        {
            Set(settings, "maxVoices", 1); StartService(); var cue = Cue();
            var scene = SceneManager.CreateScene("Audio tail scene");
            service.Play(cue, new AudioPlayOptions { OwnerScene = scene }); service.Play(cue);
            Assert.AreEqual(1, service.ActiveTailVoices);
            yield return SceneManager.UnloadSceneAsync(scene);
            Assert.AreEqual(0, service.ActiveTailVoices);
            service.Play(cue); Assert.AreEqual(1, service.ActiveTailVoices);
            service.StopBus(AudioBus.Master);
            Assert.AreEqual(0, service.ActiveTailVoices); Assert.AreEqual(0, service.ActiveVoices);
        }
        [Test]
        public void InvalidReservationsFailBeforeAllocatingSources()
        {
            Set(settings, "busPolicies", new[] { new AudioBusPolicy { bus = AudioBus.UI, reservedVoices = 5 } });
            Assert.Throws<ArgumentException>(() => StartService());
            Assert.AreEqual(0, host.GetComponentsInChildren<AudioSource>().Length);
        }
    }
}
