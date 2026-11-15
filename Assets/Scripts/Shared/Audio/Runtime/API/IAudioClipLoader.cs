using UnityEngine;

namespace ModuleFactory.Audio
{
    /// <summary>Data readiness for already-referenced clips. Does not own or unload assets.</summary>
    public interface IAudioClipLoader
    {
        AudioDataLoadState GetState(AudioClip clip);
        bool RequestLoad(AudioClip clip, bool allowSynchronous);
    }

    public sealed class UnityAudioClipLoader : IAudioClipLoader
    {
        public AudioDataLoadState GetState(AudioClip clip) => clip == null ? AudioDataLoadState.Failed : clip.loadState;
        public bool RequestLoad(AudioClip clip, bool allowSynchronous)
        {
            if (clip == null) return false;
            var state = clip.loadState;
            if (state == AudioDataLoadState.Loaded || state == AudioDataLoadState.Loading) return true;
            if (state == AudioDataLoadState.Failed) return false;
            // Do not silently introduce a synchronous decode on the gameplay thread.
            if (!clip.loadInBackground && !allowSynchronous) return false;
            return clip.LoadAudioData();
        }
    }

    public enum MusicRequestStatus { None, Loading, Starting, Playing, Failed, Cancelled }
}
