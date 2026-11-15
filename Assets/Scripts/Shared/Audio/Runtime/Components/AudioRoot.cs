using UnityEngine;

namespace ModuleFactory.Audio
{
    [DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class AudioRoot : MonoBehaviour
    {
        [SerializeField] private AudioSettings settings;
        [SerializeField] private bool persistAcrossScenes = true;
        [SerializeField] private bool loadPreferencesOnStart = true;
        private AudioService service;
        public AudioService Service => service;
        public AudioSettings Settings => settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            (GameAudio.Service as System.IDisposable)?.Dispose();
            GameAudio.Service = null;
        }
        // Rebind also when both scene reload and domain reload are disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RebindRoots()
        {
            foreach (var root in FindObjectsOfType<AudioRoot>()) if (root.isActiveAndEnabled) root.Initialize();
        }
        private void Awake() => Initialize();
        private void OnEnable() { if (Application.isPlaying) Initialize(); }
        private void Initialize()
        {
            if (GameAudio.Service != null && GameAudio.Service != service)
            {
                Debug.LogWarning("[Audio] Another AudioRoot is already active. Disabling this component.", this);
                enabled = false;
                return;
            }
            if (settings == null)
            {
                Debug.LogError("[Audio] Assign AudioSettings or use Tools > Audio > Quick Setup.", this);
                enabled = false;
                return;
            }
            if (!settings.Validate(out var validationError))
            {
                Debug.LogError("[Audio] Invalid settings: " + validationError, this);
                enabled = false; return;
            }
            if (persistAcrossScenes && transform.parent != null)
            {
                Debug.LogError("[Audio] Persistent AudioRoot must be a root GameObject.", this);
                enabled = false;
                return;
            }
            if (service != null && service.IsDisposed) service = null;
            if (service == null)
            {
                service = new AudioService(settings, transform);
                if (loadPreferencesOnStart) service.LoadPreferences();
            }
            GameAudio.Service = service;
            if (persistAcrossScenes) DontDestroyOnLoad(gameObject);
        }
        private void Update() => service?.Tick(Time.unscaledDeltaTime);
        private void OnDisable()
        {
            if (GameAudio.Service == service) GameAudio.Service = null;
            service?.Dispose();
            service = null;
        }
    }
}
