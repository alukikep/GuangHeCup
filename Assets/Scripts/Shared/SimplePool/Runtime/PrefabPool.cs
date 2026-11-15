using System.Collections.Generic;
using UnityEngine;

namespace ModuleFactory.SimplePooling
{
    // One small record per prefab; capacity includes available and currently spawned instances.
    internal sealed class PrefabPool
    {
        internal GameObject Prefab;
        internal Vector3 Scale;
        internal int Capacity;
        internal readonly List<PooledObject> Items = new List<PooledObject>();
        internal readonly List<PooledObject> Free = new List<PooledObject>();
        internal void Prune()
        {
            for (int i = Items.Count - 1; i >= 0; i--) if (Items[i] == null) Items.RemoveAt(i);
            for (int i = Free.Count - 1; i >= 0; i--)
                if (Free[i] == null || Free[i].Destroying || Free[i].InUse) Free.RemoveAt(i);
        }
        internal PooledObject Take()
        {
            if (Free.Count == 0) return null;
            int last = Free.Count - 1; var item = Free[last]; Free.RemoveAt(last); return item;
        }
    }
}
