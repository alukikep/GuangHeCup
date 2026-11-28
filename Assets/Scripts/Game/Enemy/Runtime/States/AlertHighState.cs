using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 已被锁定且看到玩家：主动追击，进入攻击范围后出招。
    /// 丢失视野后先走向最后可见位置，超过丢失时间转入 Reposition。
    /// </summary>
    public sealed class AlertHighState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        private float m_lostTimer;

        public AlertHighState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_lostTimer = 0f;
        }

        public void OnTick()
        {
            if (m_context.CanSeePlayer)
            {
                m_lostTimer = 0f;
                m_context.FaceTowards(m_context.PlayerPosition);

                if (m_context.PlayerInAttackRange)
                {
                    m_context.Stop();
                    if (m_context.CanAttack)
                    {
                        m_context.ChangeState(EnemyStateId.Attack);
                    }

                    return;
                }

                m_context.MoveTowards(m_context.PlayerPosition, m_context.Definition.chaseSpeed);
                return;
            }

            m_lostTimer += m_context.DeltaTime;
            if (m_lostTimer >= m_context.Definition.loseSightTime)
            {
                m_context.ChangeState(EnemyStateId.Reposition);
                return;
            }

            Vector2 lastSeen = m_context.LastSeenPosition;
            if (Mathf.Abs(m_context.Position.x - lastSeen.x) > m_context.Definition.arriveDistance)
            {
                m_context.MoveTowards(lastSeen, m_context.Definition.chaseSpeed);
            }
            else
            {
                m_context.Stop();
                m_context.FaceTowards(lastSeen);
            }
        }

        public void OnExit()
        {
            m_context.Stop();
        }
    }
}
