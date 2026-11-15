using ModuleFactory.Audio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Audio;

namespace ModuleFactory.Audio.Tests
{
    public sealed class AudioSetupTests
    {
        private const string Folder = "Assets/AudioPackageSetupTest";
        [Test]
        public void SetupCreatesIndependentMixerAndPreservesExistingSettings()
        {
            try
            {
                var settings = AudioSetup.CreateDefaultSettings(Folder);
                Assert.AreEqual(settings, AudioSetup.CreateDefaultSettings(Folder));
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Folder + "/AudioMixer.mixer");
                Assert.IsNotNull(mixer);
                Assert.IsTrue(settings.Validate(out _));
                Assert.AreEqual(2, settings.FindPolicy(AudioBus.UI).reservedVoices);
                Assert.AreEqual(2, settings.FindPolicy(AudioBus.Voice).reservedVoices);
                for (int i = 0; i < 5; i++)
                {
                    var bus = (AudioBus)i;
                    var route = settings.FindRouting(bus);
                    Assert.IsNotNull(route.group);
                    Assert.AreEqual(mixer, route.group.audioMixer);
                    Assert.IsTrue(mixer.GetFloat(route.volumeParameter, out _), route.volumeParameter);
                }
            }
            finally { AssetDatabase.DeleteAsset(Folder); }
        }
    }
}
