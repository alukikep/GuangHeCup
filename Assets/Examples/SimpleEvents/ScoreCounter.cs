using UnityEngine;

namespace ModuleFactory.SimpleEvents.Sample
{
    public readonly struct ScoreChanged
    {
        public int NewScore { get; }

        public ScoreChanged(int newScore) => NewScore = newScore;
    }

    public sealed class ScoreCounter : MonoBehaviour
    {
        [SerializeField] private SceneEventBus sceneEvents;

        public int CurrentScore { get; private set; }

        public void AddScore()
        {
            CurrentScore++;
            sceneEvents.Bus.Publish(new ScoreChanged(CurrentScore));
        }
    }
}
