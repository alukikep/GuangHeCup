using UnityEditor;
using UnityEngine;

namespace ModuleFactory.Audio.Editor
{
    public sealed class AudioDiagnostics : EditorWindow
    {
        [MenuItem("Tools/Audio/Diagnostics")]
        private static void Open() => GetWindow<AudioDiagnostics>("Audio Diagnostics");
        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }
        private void OnGUI()
        {
            var service = GameAudio.Service as AudioService;
            if (!EditorApplication.isPlaying || service == null)
            { EditorGUILayout.HelpBox("Enter Play mode with an AudioRoot to inspect audio.", MessageType.Info); return; }
            EditorGUILayout.LabelField("Voices", $"{service.ActiveVoices} active / {service.AllocatedVoices} allocated");
            EditorGUILayout.LabelField("Music tracks", service.ActiveMusicTracks.ToString());
            EditorGUILayout.LabelField("Steal tails", $"{service.ActiveTailVoices} / {service.TailCapacity}");
            EditorGUILayout.LabelField("Music request", service.MusicStatus.ToString());
            if (service.PendingMusicCue != null) EditorGUILayout.ObjectField("Pending music", service.PendingMusicCue, typeof(AudioCue), false);
            if (!string.IsNullOrEmpty(service.LastMusicError)) EditorGUILayout.HelpBox(service.LastMusicError, MessageType.Warning);
            EditorGUILayout.LabelField("Budget rejections", service.BudgetRejectedRequests.ToString());
            EditorGUILayout.LabelField("Rejected requests", service.RejectedRequests.ToString());
            EditorGUILayout.LabelField("Last rejection", service.LastRejection ?? "None");
            for (int i = 0; i < 5; i++)
            {
                var bus = (AudioBus)i;
                EditorGUILayout.LabelField(bus + " active voices", service.GetActiveVoices(bus).ToString());
                EditorGUI.BeginChangeCheck();
                float value = EditorGUILayout.Slider(bus.ToString(), service.GetVolume(bus), 0, 1);
                bool mute = EditorGUILayout.Toggle("Mute " + bus, service.IsMuted(bus));
                if (EditorGUI.EndChangeCheck()) { service.SetVolume(bus, value); service.SetMuted(bus, mute); }
            }
            if (GUILayout.Button("Stop all")) service.StopBus(AudioBus.Master);
            if (GUILayout.Button("Save volume preferences")) service.SavePreferences();
        }
    }
}
