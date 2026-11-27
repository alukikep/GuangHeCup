using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    public enum EnemyState { Patrol, Alert, Chase, Charge, Search, Return, Dead }

    public interface IEnemyCombatInfo
    {
        Transform Transform { get; }
        bool IsDead { get; }
        IHealth Health { get; }
        EnemyState State { get; }
        float DistanceToPlayer { get; }

        /// <summary>近战命中前剩余时间，-1 表示没有。</summary>
        float MeleeTimeToHit { get; }

        bool IsCharging { get; }
        void ApplyBlockRecoil(float direction);
    }

    public interface IEnemyRegistry
    {
        IReadOnlyList<IEnemyCombatInfo> All { get; }
        IEnemyCombatInfo NearestTo(Vector2 point, float maxDistance);

        /// <summary>最近的近战命中剩余时间，-1 表示没有。</summary>
        float NearestMeleeTimeToHit();

        bool AnyLiving { get; }

        event System.Action<IEnemyCombatInfo> Registered;
        event System.Action<IEnemyCombatInfo> Died;

        void Refresh();
    }
}
