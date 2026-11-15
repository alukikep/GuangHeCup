using UnityEditor;
using UnityEngine;

namespace ModuleFactory.Audio.Editor
{
    [CustomEditor(typeof(AudioSettings))]
    public sealed class AudioSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var settings = (AudioSettings)target;
            if (!settings.Validate(out var error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.HelpBox($"Maximum sources: {settings.MaxVoices} voices + {settings.MaxStealTails} steal tails + 2 music. " +
                "Voice and request reservations are different: the global frame request budget is shared. Restart AudioRoot after changing capacity.", MessageType.Info);
        }
    }

    [CustomEditor(typeof(AudioCue))]
    public sealed class AudioCueInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var clips = serializedObject.FindProperty("clips");
            for (int i = 0; i < clips.arraySize; i++)
            {
                var clip = clips.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                if (clip == null) continue;
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) as AudioImporter;
                if (importer == null) continue;
                if (!importer.loadInBackground && !importer.defaultSampleSettings.preloadAudioData)
                    EditorGUILayout.HelpBox(clip.name + ": neither background load nor default-platform preload is enabled. " +
                        "Enable background loading in its import settings, or explicitly preload during a loading screen. " +
                        "Check target platform overrides as well.", MessageType.Warning);
            }
        }
    }
}
