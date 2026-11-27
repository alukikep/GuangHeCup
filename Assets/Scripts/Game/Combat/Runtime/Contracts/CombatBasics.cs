using UnityEngine;

namespace Game.Combat
{
    public enum CombatFaction { Player, Enemy, Neutral }

    public enum DamageType { Melee, Projectile, Dash, Environment }

    public readonly struct DamageInfo
    {
        public readonly int Amount;
        public readonly Vector2 SourcePosition;
        public readonly Vector2 HitPoint;
        public readonly bool Heavy;
        public readonly DamageType Type;
        public readonly CombatFaction SourceFaction;
        public readonly object Source;

        public DamageInfo(int amount, Vector2 sourcePosition, Vector2 hitPoint,
                          bool heavy, DamageType type, CombatFaction sourceFaction, object source = null)
        {
            Amount = amount;
            SourcePosition = sourcePosition;
            HitPoint = hitPoint;
            Heavy = heavy;
            Type = type;
            SourceFaction = sourceFaction;
            Source = source;
        }
    }

    public interface IHealth
    {
        int Current { get; }
        int Max { get; }
        bool IsAlive { get; }
        event System.Action<int, int> Changed;
        event System.Action Died;
        void Heal(int amount);
    }

    public interface IDamageable
    {
        CombatFaction Faction { get; }
        Transform Transform { get; }
        IHealth Health { get; }
        void TakeDamage(in DamageInfo info);
    }

    public readonly struct BlockContext
    {
        public readonly Vector2 SourcePosition;
        public readonly bool Ranged;
        public readonly IDamageable Attacker;

        public BlockContext(Vector2 sourcePosition, bool ranged, IDamageable attacker)
        {
            SourcePosition = sourcePosition;
            Ranged = ranged;
            Attacker = attacker;
        }
    }

    public interface IBlockable
    {
        bool TryBlock(in BlockContext context);
    }
}
