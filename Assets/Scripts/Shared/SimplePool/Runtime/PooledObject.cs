using UnityEngine;

namespace ModuleFactory.SimplePooling
{
    [DisallowMultipleComponent, AddComponentMenu("")]
    internal sealed class PooledObject : MonoBehaviour
    {
        internal SimplePool Owner;
        internal PrefabPool Pool;
        internal MonoBehaviour[] Callbacks;
        internal bool InUse, Destroying, HasTimer, Unscaled;
        internal uint Version, TimerVersion;
        internal float Remaining;
    }
}
