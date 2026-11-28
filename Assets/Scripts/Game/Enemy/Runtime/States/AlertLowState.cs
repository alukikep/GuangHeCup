using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 发现玩家但未被锁定：停步注视，不主动接近。
    /// 玩家进入攻击范围且没有出招时，才允许先手攻击。
    /// </summary>
    public sealed class AlertLowState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        private float m_lostTimer;

        public AlertLowState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_lostTimer = 0f;
            m_context.Stop();
        }

        public void OnTick()
        {
            if (m_context.CanSeePlayer)
            {
                m_lostTimer = 0f;
                m_context.Stop();
                m_context.FaceTowards(m_context.PlayerPosition);

                if (m_context.IsLockedByPlayer)
                {
                    m_context.ChangeState(EnemyStateId.AlertHigh);
                    return;
                }

                if (m_context.PlayerInAttackRange && !m_context.PlayerIsAttacking && m_context.CanAttack)
                {
                    m_context.ChangeState(EnemyStateId.Attack);
                }

                return;
            }

            m_context.Stop();
            m_lostTimer += m_context.DeltaTime;
            if (m_lostTimer >= m_context.Definition.loseSightTime)
            {
                m_context.ChangeState(EnemyStateId.Patrol);
            }
        }

        public void OnExit()
        {
            m_context.Stop();
        }
    }
}
