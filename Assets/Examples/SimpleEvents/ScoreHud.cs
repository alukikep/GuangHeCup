using System;
using UnityEngine;
using UnityEngine.UI;

namespace ModuleFactory.SimpleEvents.Sample
{
    public sealed class ScoreHud : MonoBehaviour
    {
        [SerializeField] private SceneEventBus sceneEvents;
        [SerializeField] private ScoreCounter score;
        [SerializeField] private Text label;
        private IDisposable subscription;

        private void OnEnable()
        {
            subscription = sceneEvents.Bus.Subscribe<ScoreChanged>(OnScoreChanged);
            RenderScore(score.CurrentScore);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void OnScoreChanged(ScoreChanged message) => RenderScore(message.NewScore);

        private void RenderScore(int value) => label.text = $"HUD score: {value}";
    }
}
