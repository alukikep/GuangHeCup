using System;
using UnityEngine;
using UnityEngine.UI;

namespace ModuleFactory.SimpleEvents.Sample
{
    public sealed class ScoreEventLog : MonoBehaviour
    {
        [SerializeField] private SceneEventBus sceneEvents;
        [SerializeField] private Text label;
        private IDisposable subscription;
        private int eventCount;

        private void OnEnable()
        {
            subscription = sceneEvents.Bus.Subscribe<ScoreChanged>(OnScoreChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
        }

        private void OnScoreChanged(ScoreChanged message)
        {
            eventCount++;
            label.text = $"Events received: {eventCount}   |   Last score: {message.NewScore}";
        }
    }
}
