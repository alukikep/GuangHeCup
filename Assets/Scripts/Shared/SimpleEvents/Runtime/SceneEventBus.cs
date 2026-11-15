using UnityEngine;

namespace ModuleFactory.SimpleEvents
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Module Factory/Simple Events/Scene Event Bus")]
    public sealed class SceneEventBus : MonoBehaviour
    {
        public EventBus Bus { get; } = new EventBus();
    }
}
