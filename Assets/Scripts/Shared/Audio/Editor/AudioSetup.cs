using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

namespace ModuleFactory.Audio.Editor
{
    public static class AudioSetup
    {
        private const string Folder = "Assets/Audio";
        private const string Template = "Assets/Scripts/Shared/Audio/Editor/Templates/DefaultAudio.mixer";
        [MenuItem("Tools/Audio/Quick Setup")]
        public static void QuickSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[Audio] Run Quick Setup outside Play mode."); return; }
            var settings = CreateDefaultSettings(Folder);
            var roots = Object.FindObjectsOfType<AudioRoot>(true);
            AudioRoot root;
            if (roots.Length == 0)
            {
                var go = new GameObject("AudioRoot");
                Undo.RegisterCreatedObjectUndo(go, "Create AudioRoot");
                root = Undo.AddComponent<AudioRoot>(go);
            }
            else
            {
                root = roots[0];
                if (roots.Length > 1) Debug.LogWarning("[Audio] Multiple AudioRoots found. Keep one enabled root.");
            }
            var serialized = new SerializedObject(root);
            if (serialized.FindProperty("settings").objectReferenceValue == null)
            {
                serialized.FindProperty("settings").objectReferenceValue = settings;
                serialized.ApplyModifiedProperties();
            }
            Selection.activeObject = root.gameObject;
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            if (Object.FindObjectsOfType<AudioListener>().Length != 1)
                Debug.LogWarning("[Audio] Ensure the scene has exactly one enabled AudioListener on its camera.");
            Debug.Log("[Audio] Ready. Create a Cue via Assets > Create > Module Factory > Audio > Cue. " +
                "Assign clips, then use an AudioEmitter or GameAudio.Service. Save your scene.");
        }

        public static AudioSettings CreateDefaultSettings(string folder)
        {
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string settingsPath = folder + "/AudioSettings.asset";
            var existing = AssetDatabase.LoadAssetAtPath<AudioSettings>(settingsPath);
            if (existing != null) return existing;
            string mixerPath = folder + "/AudioMixer.mixer";
            if (AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath) == null &&
                !AssetDatabase.CopyAsset(Template, mixerPath))
                throw new IOException("Cannot copy default audio mixer template.");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            var settings = ScriptableObject.CreateInstance<AudioSettings>();
            var so = new SerializedObject(settings);
            var policies = so.FindProperty("busPolicies");
            policies.arraySize = 3;
            SetPolicy(policies.GetArrayElementAtIndex(0), AudioBus.SFX, 24, 0, 48);
            SetPolicy(policies.GetArrayElementAtIndex(1), AudioBus.UI, 4, 2, 8);
            SetPolicy(policies.GetArrayElementAtIndex(2), AudioBus.Voice, 4, 2, 8);
            var routing = so.FindProperty("routing");
            routing.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                string bus = ((AudioBus)i).ToString();
                var element = routing.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("bus").enumValueIndex = i;
                AudioMixerGroup group = null;
                foreach (var candidate in mixer.FindMatchingGroups(string.Empty))
                    if (candidate.name == bus) { group = candidate; break; }
                if (group == null) throw new IOException("Missing mixer group: " + bus);
                element.FindPropertyRelative("group").objectReferenceValue = group;
                element.FindPropertyRelative("volumeParameter").stringValue = bus + "Volume";
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, settingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
        private static void SetPolicy(SerializedProperty item, AudioBus bus, int max, int reserved, int requests)
        {
            item.FindPropertyRelative("bus").enumValueIndex = (int)bus;
            item.FindPropertyRelative("maxVoices").intValue = max;
            item.FindPropertyRelative("reservedVoices").intValue = reserved;
            item.FindPropertyRelative("maxRequestsPerFrame").intValue = requests;
        }

    }
}
