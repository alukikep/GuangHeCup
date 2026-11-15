using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModuleFactory.SimplePooling
{
    /// <summary>Scene-local by default. All APIs are main-thread only; no static singleton is required.</summary>
    [DisallowMultipleComponent]
    public sealed class SimplePool : MonoBehaviour
    {
        [SerializeField, Min(1)] private int defaultCapacity = 64;
        [SerializeField] private bool persistAcrossScenes;
        private readonly Dictionary<GameObject, PrefabPool> pools = new Dictionary<GameObject, PrefabPool>();
        private Transform storage;
        private bool busy, destroyed;
        private void Awake()
        {
            if (persistAcrossScenes && transform.parent == null) DontDestroyOnLoad(gameObject);
        }
        private bool Enter() { if (busy || destroyed || !isActiveAndEnabled) return false; busy = true; return true; }
        private PrefabPool Find(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponentInChildren<PooledObject>(true) != null) return null;
            if (!pools.TryGetValue(prefab, out var pool))
            {
                pool = new PrefabPool { Prefab = prefab, Capacity = Mathf.Max(1, defaultCapacity), Scale = prefab.transform.localScale };
                pools.Add(prefab, pool);
            }
            pool.Prune(); return pool;
        }
        private PooledObject Create(PrefabPool pool)
        {
            if (pool.Items.Count >= pool.Capacity) return null;
            if (storage == null)
            {
                var go = new GameObject("Pool Storage"); go.SetActive(false);
                go.transform.SetParent(transform, false); storage = go.transform;
            }
            var instance = Instantiate(pool.Prefab, storage, false); instance.SetActive(false);
            var item = instance.AddComponent<PooledObject>(); item.Owner = this; item.Pool = pool;
            item.Callbacks = instance.GetComponentsInChildren<MonoBehaviour>(true); pool.Items.Add(item); return item;
        }
        /// <summary>Configure before use. Cannot shrink below existing instances, including destroy-pending ones.</summary>
        public bool Configure(GameObject prefab, int capacity, int prewarm = 0)
        {
            if (capacity < 1 || prewarm < 0 || prewarm > capacity || !Enter()) return false;
            try
            {
                var pool = Find(prefab); if (pool == null || pool.Items.Count > capacity) return false;
                pool.Capacity = capacity;
                while (pool.Items.Count < prewarm) pool.Free.Add(Create(pool));
                return true;
            }
            finally { busy = false; }
        }
        /// <summary>Ensure at least count total clones exist, up to the configured capacity. Synchronous.</summary>
        public bool Prewarm(GameObject prefab, int count)
        {
            if (count < 0 || !Enter()) return false;
            try
            {
                var pool = Find(prefab); if (pool == null || count > pool.Capacity) return false;
                while (pool.Items.Count < count) pool.Free.Add(Create(pool));
                return true;
            }
            finally { busy = false; }
        }
        public GameObject Spawn(GameObject prefab, Vector3 position = default, Quaternion? rotation = null,
            Transform parent = null, Action<GameObject> initialize = null)
        {
            if (!Enter()) return null;
            PooledObject item = null;
            try
            {
                if (!ReferenceEquals(parent, null) && parent == null) return null;
                var pool = Find(prefab); if (pool == null) return null;
                item = pool.Take() ?? Create(pool); if (item == null) return null;
                item.InUse = true; item.Version++; item.HasTimer = false;
                var go = item.gameObject; go.SetActive(false);
                go.transform.SetParent(parent != null ? parent : transform, false);
                go.transform.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
                go.transform.localScale = pool.Scale;
                if (!Notify(item, true)) { Discard(item); return null; }
                initialize?.Invoke(go);
                if (item == null || item.Destroying || destroyed) { if (item != null) Discard(item); return null; }
                // OnSpawn and initialization must not activate or destroy the object themselves.
                if (go.activeSelf) { Discard(item); return null; }
                go.SetActive(true);
                if (item == null || destroyed || !isActiveAndEnabled) { if (item != null) Discard(item); return null; }
                return go;
            }
            catch (Exception e)
            { Debug.LogError("[SimplePool] Spawn failed: " + e, this); if (item != null) Discard(item); return null; }
            finally { busy = false; }
        }
        private bool Owned(GameObject instance, out PooledObject item)
        {
            item = instance != null ? instance.GetComponent<PooledObject>() : null;
            return item != null && item.Owner == this && item.InUse && !item.Destroying;
        }
        public bool Despawn(GameObject instance)
        {
            if (!Enter()) return false;
            try { return Owned(instance, out var item) && Return(item); }
            finally { busy = false; }
        }
        private bool Notify(PooledObject item, bool spawning)
        {
            bool success = true;
            foreach (var component in item.Callbacks)
            {
                if (component == null || !(component is IPoolable callback)) continue;
                try { if (spawning) callback.OnSpawn(); else callback.OnDespawn(); }
                catch (Exception e) { success = false; Debug.LogError("[SimplePool] Reset callback failed: " + e, this); }
            }
            return success && item != null && !destroyed;
        }
        private bool Return(PooledObject item, bool reparent = true)
        {
            item.InUse = false; item.Version++; item.HasTimer = false;
            item.gameObject.SetActive(false);
            bool clean = Notify(item, false);
            if (item == null) return false;
            if (!clean) { Discard(item); return false; }
            // Unity forbids reparenting descendants while their parent is being deactivated/destroyed.
            // Keep them inactive in place; the next Spawn assigns their parent normally.
            if (reparent && gameObject.activeInHierarchy) item.transform.SetParent(storage, false);
            item.transform.localPosition = Vector3.zero; item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = item.Pool.Scale; item.Pool.Free.Add(item); return true;
        }
        /// <summary>Replaces the pool-owned timer for this spawn. It cannot return a later reuse.</summary>
        public bool DespawnAfter(GameObject instance, float seconds, bool unscaledTime = false)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || !Enter()) return false;
            try
            {
                if (!Owned(instance, out var item)) return false;
                item.HasTimer = true; item.TimerVersion = item.Version;
                item.Remaining = Mathf.Max(0, seconds); item.Unscaled = unscaledTime; return true;
            }
            finally { busy = false; }
        }
        private void Update()
        {
            if (busy) return; busy = true;
            try
            {
                foreach (var pool in pools.Values)
                {
                    pool.Prune();
                    for (int i = pool.Items.Count - 1; i >= 0; i--)
                    {
                        var item = pool.Items[i];
                        if (!item.InUse || !item.HasTimer || item.TimerVersion != item.Version) continue;
                        item.Remaining -= item.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                        if (item.Remaining <= 0) Return(item);
                    }
                }
            }
            finally { busy = false; }
        }
        public int CountAll(GameObject prefab) => prefab != null && pools.TryGetValue(prefab, out var pool) ? Count(pool, false) : 0;
        public int CountActive(GameObject prefab) => prefab != null && pools.TryGetValue(prefab, out var pool) ? Count(pool, true) : 0;
        private static int Count(PrefabPool pool, bool active)
        {
            int count = 0;
            foreach (var item in pool.Items) if (item != null && (!active || item.InUse)) count++;
            return count;
        }
        private void Discard(PooledObject item)
        {
            if (item == null || item.Destroying) return;
            item.Destroying = true; item.InUse = false; item.HasTimer = false; item.Version++;
            item.gameObject.SetActive(false); Destroy(item.gameObject);
        }
        public bool Clear(GameObject prefab, bool includeActive = true)
        {
            if (!Enter()) return false;
            try
            {
                if (prefab == null || !pools.TryGetValue(prefab, out var pool)) return false;
                ClearPool(pool, includeActive); return true;
            }
            finally { busy = false; }
        }
        public bool ClearAll(bool includeActive = true)
        {
            if (!Enter()) return false;
            try { foreach (var pool in pools.Values) ClearPool(pool, includeActive); return true; }
            finally { busy = false; }
        }
        public bool DespawnAll()
        {
            if (!Enter()) return false;
            try { ReturnAll(); return true; }
            finally { busy = false; }
        }
        private void ReturnAll(bool reparent = true)
        { foreach (var pool in pools.Values) foreach (var item in pool.Items) if (item != null && item.InUse) Return(item, reparent); }
        private void ClearPool(PrefabPool pool, bool includeActive)
        {
            foreach (var item in pool.Items)
                if (item != null && !item.Destroying && (includeActive || !item.InUse))
                { if (item.InUse) { item.InUse = false; item.gameObject.SetActive(false); Notify(item, false); } Discard(item); }
            pool.Prune();
        }
        private void OnDisable()
        {
            if (busy || destroyed) return; busy = true;
            try { ReturnAll(false); }
            finally { busy = false; }
        }
        private void OnDestroy()
        {
            destroyed = true;
            foreach (var pool in pools.Values) ClearPool(pool, true);
            pools.Clear();
        }
    }
}
