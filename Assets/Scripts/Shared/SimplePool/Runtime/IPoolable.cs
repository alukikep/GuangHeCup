namespace ModuleFactory.SimplePooling
{
    /// <summary>Called while inactive. Reset your own gameplay state here; Awake may not have run yet.</summary>
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }
}
