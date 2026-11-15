using System;
using System.Collections;
using ModuleFactory.SimpleUI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ModuleFactory.SceneFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIScreenFade))]
    [AddComponentMenu("Module Factory/Scene Flow/Scene Flow Loader")]
    public sealed class SceneFlowLoader : MonoBehaviour
    {
        [SerializeField, Min(0)] private float fadeSeconds = 0.25f;

        private static SceneFlowLoader owner;
        private static AsyncOperation pendingLoad;
        private bool carriedAcrossScenes;

        /// <summary>True while a transition owns the fade or an issued scene load is still running.</summary>
        public static bool IsBusy => owner != null || (pendingLoad != null && !pendingLoad.isDone);
        public UIScreenFade Fade { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            owner = null;
            pendingLoad = null;
        }

        private void Awake() => Fade = GetComponent<UIScreenFade>();

        // Void entry point for Inspector button bindings.
        public void LoadScene(string scenePath) => TryLoad(scenePath);

        /// <summary>Returns whether the request was accepted. Loading completes asynchronously.</summary>
        /// <remarks>Disabling the loader clears its fade. An already issued Unity load continues.</remarks>
        public bool TryLoad(string scenePath)
        {
            if (!Application.isPlaying || IsBusy || !isActiveAndEnabled ||
                Fade == null || !Fade.isActiveAndEnabled)
                return false;

            if (transform.parent != null)
            {
                Debug.LogWarning("Scene Flow: place the loader on a dedicated root GameObject.", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(scenePath) || !Application.CanStreamedLevelBeLoaded(scenePath))
            {
                Debug.LogWarning($"Scene Flow: target scene '{scenePath}' must be enabled in Build Settings.", this);
                Fade.SetImmediate(0);
                return false;
            }

            if (float.IsNaN(fadeSeconds) || float.IsInfinity(fadeSeconds))
            {
                Debug.LogWarning("Scene Flow: fade duration must be finite.", this);
                Fade.SetImmediate(0);
                return false;
            }

            owner = this;
            StartCoroutine(Transition(scenePath, fadeSeconds));
            return true;
        }

        private IEnumerator Transition(string scenePath, float duration)
        {
            try
            {
                var blackout = Fade.FadeOut(duration);
                yield return blackout;
                if (blackout.Status != FadeStatus.Completed || Fade == null || !Fade.isActiveAndEnabled)
                    yield break;

                var operation = StartSceneLoad(scenePath);
                if (operation == null)
                    yield break;

                pendingLoad = operation;
                operation.completed += ClearPendingLoad;
                DontDestroyOnLoad(gameObject);
                carriedAcrossScenes = true;
                yield return operation;

                // Let the destination render a frame under the blackout.
                yield return null;
                if (Fade != null && Fade.isActiveAndEnabled)
                    yield return Fade.FadeIn(duration);
            }
            finally
            {
                FinishTransition();
            }
        }

        private AsyncOperation StartSceneLoad(string scenePath)
        {
            try
            {
                return SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Scene Flow: could not load '{scenePath}'. {exception.Message}", this);
                return null;
            }
        }

        private static void ClearPendingLoad(AsyncOperation operation)
        {
            if (pendingLoad == operation)
                pendingLoad = null;
        }

        private void OnDisable()
        {
            if (!ReferenceEquals(owner, this))
                return;

            StopAllCoroutines();
            FinishTransition();
        }

        private void FinishTransition()
        {
            if (!ReferenceEquals(owner, this))
                return;

            if (Fade != null)
                Fade.SetImmediate(0);
            owner = null;

            // An issued Unity load continues even when this component is disabled.
            if (carriedAcrossScenes)
                Destroy(gameObject);
        }
    }
}
