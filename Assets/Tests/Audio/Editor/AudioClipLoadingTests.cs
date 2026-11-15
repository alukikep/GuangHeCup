using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ModuleFactory.Audio.Tests
{
    public sealed class AudioClipLoadingTests
    {
        private const string Folder = "Assets/AudioPackageLoadingTest";
        private const string ClipPath = Folder + "/Loading.wav";
        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(Folder);
            using (var writer = new BinaryWriter(File.Create(ClipPath)))
            {
                const int bytes = 44100 * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                writer.Write(new byte[bytes]);
            }
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceSynchronousImport);
        }
        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);
        private static AudioClip Configure(bool background)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(ClipPath);
            importer.loadInBackground = background;
            var sample = importer.defaultSampleSettings;
            sample.preloadAudioData = false;
            sample.loadType = AudioClipLoadType.DecompressOnLoad;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip.loadState == AudioDataLoadState.Loaded) Assert.IsTrue(clip.UnloadAudioData());
            Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
            return clip;
        }
        [Test]
        public void DefaultLoaderRefusesUnloadedSynchronousClipWithoutStartingItsLoad()
        {
            var clip = Configure(false);
            Assert.IsFalse(new UnityAudioClipLoader().RequestLoad(clip, false));
            Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
        }
        [Test]
        public void ExplicitSynchronousOptInCanPrepareDuringLoadingScreen()
        {
            var clip = Configure(false);
            Assert.IsTrue(new UnityAudioClipLoader().RequestLoad(clip, true));
            Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
        }
        [UnityTest]
        public IEnumerator RealImportedBackgroundClipCanBePreparedWithoutSynchronousOptIn()
        {
            var clip = Configure(true);
            var loader = new UnityAudioClipLoader();
            Assert.IsTrue(loader.RequestLoad(clip, false));
            double deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (loader.GetState(clip) == AudioDataLoadState.Loading && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            Assert.AreEqual(AudioDataLoadState.Loaded, loader.GetState(clip));
        }
    }
}
