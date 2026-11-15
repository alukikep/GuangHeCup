using UnityEngine;

namespace ModuleFactory.SimplePooling.Tests
{
    public sealed class SimplePoolProbe : MonoBehaviour, IPoolable
    {
        public int Value, ValueOnEnable, Rents, Returns;
        public void OnSpawn() { Value = 0; Rents++; }
        public void OnDespawn() { Value = 0; Returns++; }
        private void OnEnable() => ValueOnEnable = Value;
    }
}
