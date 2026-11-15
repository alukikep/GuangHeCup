using ModuleFactory.Audio;
using UnityEngine;

/// <summary>Add to an empty GameObject after Quick Setup. No external sound assets required.</summary>
public sealed class AudioPlayground : MonoBehaviour
{
    private AudioCue click, musicA, musicB;
    private AudioClip clickClip, aClip, bClip;
    private AudioHandle loop;
    private void Awake()
    {
        clickClip = Tone("Click", 880, 0.12f);
        aClip = Tone("Music A", 220, 2);
        bClip = Tone("Music B", 330, 2);
        click = AudioCue.CreateTransient(new[] { clickClip }, AudioBus.UI);
        musicA = AudioCue.CreateTransient(new[] { aClip }, AudioBus.Music, true);
        musicB = AudioCue.CreateTransient(new[] { bClip }, AudioBus.Music, true);
    }
    private static AudioClip Tone(string label, float frequency, float seconds)
    {
        const int rate = 44100;
        var data = new float[(int)(rate * seconds)];
        for (int i = 0; i < data.Length; i++)
        {
            float envelope = Mathf.Min(1, i / 220f) * Mathf.Min(1, (data.Length - 1 - i) / 220f);
            data[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * 0.12f * envelope;
        }
        var clip = AudioClip.Create(label, data.Length, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, 380, 650), GUI.skin.box);
        GUILayout.Label("Module Factory Audio - Quick Start");
        var audio = GameAudio.Service;
        if (audio == null) GUILayout.Label("Use Tools > Audio > Quick Setup first.");
        else
        {
            if (GUILayout.Button("UI click")) audio.Play(click);
            if (GUILayout.Button("World-position click")) audio.PlayAt(click, transform.position);
            if (GUILayout.Button("Loop following this object"))
            { loop.Stop(); loop = audio.PlayAttached(musicA, transform); }
            if (GUILayout.Button("Stop following loop")) loop.Stop(0.3f);
            if (GUILayout.Button("Music A")) audio.PlayMusic(musicA, 1);
            if (GUILayout.Button("Music B")) audio.PlayMusic(musicB, 1);
            if (GUILayout.Button("Pause music")) audio.PauseMusic();
            if (GUILayout.Button("Resume music")) audio.ResumeMusic();
            if (GUILayout.Button("Stop music")) audio.StopMusic(0.5f);
            if (GUILayout.Button("Burst: 1,000 UI requests"))
                for (int i = 0; i < 1000; i++) audio.Play(click);
            if (GUILayout.Button("Pause world sounds")) audio.SetWorldPaused(true);
            if (GUILayout.Button("Resume world sounds")) audio.SetWorldPaused(false);
            GUILayout.Label("Music request: " + audio.MusicStatus);
            if (!string.IsNullOrEmpty(audio.LastMusicError)) GUILayout.Label(audio.LastMusicError);
            if (audio is AudioService service)
            {
                GUILayout.Label($"Voices: {service.ActiveVoices}, fade tails: {service.ActiveTailVoices}");
                GUILayout.Label($"Budget rejections: {service.BudgetRejectedRequests}");
            }
            GUILayout.Label("Master volume");
            audio.SetVolume(AudioBus.Master, GUILayout.HorizontalSlider(audio.GetVolume(AudioBus.Master), 0, 1));
            if (GUILayout.Button("Save volume")) audio.SavePreferences();
        }
        GUILayout.EndArea();
    }
    private void OnDestroy()
    {
        // The demo owns transient cues; stop playback before releasing those assets.
        loop.Stop();
        GameAudio.Service?.StopBus(AudioBus.Music);
        GameAudio.Service?.StopBus(AudioBus.UI);
        Destroy(click); Destroy(musicA); Destroy(musicB);
        Destroy(clickClip); Destroy(aClip); Destroy(bClip);
    }
}
