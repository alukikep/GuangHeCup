using System.Collections.Generic;
using UnityEngine;

namespace ModuleFactory.Audio
{
    internal sealed class AudioVoice
    {
        public AudioSource Source;
        public ulong Version;
        public AudioCue Cue;
        public AudioBus Bus;
        public int Priority;
        public Transform Target;
        public bool Following, Active, UserPaused, WorldPaused;
        public bool Paused => UserPaused || WorldPaused;
        public TargetLostBehaviour TargetLost;
        public AudioScope Scope;
        public int SceneHandle;
        public long Order;
        public float BaseVolume, Multiplier, Gain, FadeFrom, FadeTo, FadeTime, FadeDuration;
        public bool Stopping;
        public void CopyPlaybackTo(AudioVoice tail)
        {
            tail.Cue = Cue; tail.Target = Target; tail.Following = Following;
            tail.Bus = Bus; tail.Priority = Priority;
            tail.Active = true; tail.UserPaused = UserPaused; tail.WorldPaused = WorldPaused;
            tail.TargetLost = TargetLost; tail.Scope = Scope; tail.SceneHandle = SceneHandle;
            tail.BaseVolume = BaseVolume; tail.Multiplier = Multiplier; tail.Gain = Gain;
            tail.Stopping = true;
        }

        public void Fade(float to, float seconds)
        {
            FadeFrom = Gain;
            FadeTo = to;
            FadeTime = 0;
            FadeDuration = Mathf.Max(0, seconds);
            if (FadeDuration == 0) Gain = to;
        }
        public void TickFade(float delta)
        {
            if (FadeDuration <= 0) return;
            FadeTime += delta;
            Gain = Mathf.Lerp(FadeFrom, FadeTo, FadeTime / FadeDuration);
            if (FadeTime >= FadeDuration) FadeDuration = 0;
        }
    }

    internal sealed class AudioVoicePool
    {
        public readonly List<AudioVoice> Voices = new List<AudioVoice>();
        public readonly List<AudioVoice> Tails = new List<AudioVoice>();
        private readonly Transform parent;
        private readonly int limit;
        public AudioVoicePool(Transform parent, int prewarm, int limit, int tailCapacity)
        {
            this.parent = parent;
            this.limit = limit;
            for (int i = 0; i < prewarm; i++) Add();
            for (int i = 0; i < tailCapacity; i++) Tails.Add(Create("Steal tail " + i));
        }
        private AudioVoice Add()
        {
            var voice = Create("Voice " + Voices.Count);
            Voices.Add(voice);
            return voice;
        }
        private AudioVoice Create(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0;
            var voice = new AudioVoice { Source = source };
            return voice;
        }
        public bool BeginSteal(AudioVoice voice, float fadeSeconds)
        {
            if (fadeSeconds <= 0 || Tails.Count == 0 || voice.Paused || !voice.Source.isPlaying) return true;
            foreach (var tail in Tails)
            {
                if (tail.Active) continue;
                voice.CopyPlaybackTo(tail);
                // Transfer the playing source itself; do not restart/seek/copy the clip.
                var spare = tail.Source; tail.Source = voice.Source; voice.Source = spare;
                tail.Fade(0, fadeSeconds);
                return true;
            }
            return false;
        }
        public AudioVoice Acquire()
        {
            foreach (var voice in Voices) if (!voice.Active) return voice;
            return Voices.Count < limit ? Add() : null;
        }
        public void Release(AudioVoice voice)
        {
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.outputAudioMixerGroup = null;
            voice.Target = null;
            voice.Cue = null;
            voice.Active = false;
            voice.Following = false;
        }
        public void Dispose()
        {
            foreach (var voice in Voices)
                if (voice.Source != null) { voice.Source.Stop(); AudioObjects.Destroy(voice.Source.gameObject); }
            foreach (var tail in Tails)
                if (tail.Source != null) { tail.Source.Stop(); AudioObjects.Destroy(tail.Source.gameObject); }
            Voices.Clear();
            Tails.Clear();
        }
    }

    internal static class AudioObjects
    {
        public static void Destroy(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
