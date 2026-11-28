using Game.Data;
using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 状态只依赖这个上下文，不直接摸 MonoBehaviour、Rigidbody2D 或 Player。
    /// 这样状态逻辑可以脱离场景单独测试。
    /// </summary>
    public interface IEnemyContext
    {
        EnemyDefinition Definition { get; }

        float DeltaTime { get; }

        float Now { get; }

        int Facing { get; }

        bool HasPlayer { get; }

        bool CanSeePlayer { get; }

        bool PlayerInAttackRange { get; }

        bool IsLockedByPlayer { get; }

        bool PlayerIsAttacking { get; }

        bool CanAttack { get; }

        Vector2 Position { get; }

        Vector2 PlayerPosition { get; }

        Vector2 SpawnPosition { get; }

        Vector2 LastSeenPosition { get; }

        void ChangeState(EnemyStateId state);

        void MoveTowards(Vector2 target, float speed);

        void Stop();

        void FaceTowards(Vector2 target);

        void PerformAttackImpact();

        void CompleteAttack();

        void BeginGuard();

        void EndGuard();
    }
}
