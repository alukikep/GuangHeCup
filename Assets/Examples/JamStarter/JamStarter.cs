using System.Collections;
using ModuleFactory.Audio;
using ModuleFactory.SimplePooling;
using ModuleFactory.SimpleUI;
using UnityEngine;
using UnityEngine.UI;

namespace ModuleFactory.GameJam
{
    public sealed class JamStarter : MonoBehaviour
    {
        [SerializeField] private UIManager ui;
        [SerializeField] private UIPanel menu;
        [SerializeField] private UIPanel game;
        [SerializeField] private UIPanel pause;
        [SerializeField] private UIPanel results;
        [SerializeField] private UIScreenFade fade;
        [SerializeField] private SimplePool pool;
        [SerializeField] private Button targetPrefab;
        [SerializeField] private RectTransform playArea;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private AudioCue collectSound;
        [SerializeField, Min(1)] private int goal = 3;
        [SerializeField, Min(0.1f)] private float targetLifetime = 3f;

        private int score;
        private bool transitioning;

        private bool IsPlaying => !transitioning && game.IsOpen && !pause.IsOpen && !results.IsOpen;

        private void Start()
        {
            pool.Configure(targetPrefab.gameObject, capacity: 1, prewarm: 1);
        }

        public void StartRound()
        {
            if (!transitioning && (menu.IsOpen || results.IsOpen))
                StartCoroutine(SwitchTo(game));
        }

        public void SpawnTarget()
        {
            if (!IsPlaying) return;

            var target = pool.Spawn(targetPrefab.gameObject, parent: playArea, initialize: InitializeTarget);
            if (target != null) pool.DespawnAfter(target, targetLifetime);
        }

        private void InitializeTarget(GameObject target)
        {
            var rect = (RectTransform)target.transform;
            var anchor = new Vector2(Random.Range(0.18f, 0.82f), Random.Range(0.22f, 0.78f));
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
            var button = target.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Collect(target));
        }

        private void Collect(GameObject target)
        {
            if (!IsPlaying || !pool.Despawn(target)) return;

            GameAudio.Play(collectSound);
            score++;
            UpdateScore();
            if (score >= goal) ui.Open(results);
        }

        public void PauseRound()
        {
            if (!IsPlaying || !ui.Open(pause)) return;
            Time.timeScale = 0f;
            GameAudio.Service?.SetWorldPaused(true);
        }

        public void ResumeRound()
        {
            if (!transitioning) ui.Close(pause);
        }

        // Bound to the pause panel's Hidden event, including dismissal by UIManager.
        public void RestoreTime()
        {
            Time.timeScale = 1f;
            GameAudio.Service?.SetWorldPaused(false);
        }

        public void ReturnToMenu()
        {
            if (!transitioning && (pause.IsOpen || results.IsOpen))
                StartCoroutine(SwitchTo(menu));
        }

        private IEnumerator SwitchTo(UIPanel destination)
        {
            transitioning = true;
            var blackout = fade.FadeOut(0.2f);
            yield return blackout;
            if (blackout.Status == FadeStatus.Completed)
            {
                ui.CloseAll();
                pool.DespawnAll();
                score = 0;
                UpdateScore();
                ui.Open(destination);
                yield return fade.FadeIn(0.2f);
            }
            transitioning = false;
        }

        private void UpdateScore() => scoreLabel.text = $"Targets: {score} / {goal}";

        private void OnDisable()
        {
            StopAllCoroutines();
            if (transitioning) fade.SetImmediate(0f);
            transitioning = false;
            if (pause != null && pause.IsOpen) RestoreTime();
        }
    }
}
