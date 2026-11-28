using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 默认状态：在出生点附近的水平范围内随机选点，走到后停顿再选下一个点。
    /// </summary>
    public sealed class PatrolState : IEnemyState
    {
        private const float k_facingDeadZone = 0.01f;

        private readonly IEnemyContext m_context;

        private Vector2 m_target;
        private float m_idleRemaining;

        public PatrolState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_context.Stop();
            PickNextPoint();
        }

        public void OnTick()
        {
            if (m_context.CanSeePlayer)
            {
                EnemyStateId next = m_context.IsLockedByPlayer ? EnemyStateId.AlertHigh : EnemyStateId.AlertLow;
                m_context.ChangeState(next);
                return;
            }

            if (m_idleRemaining > 0f)
            {
                m_context.Stop();
                m_idleRemaining -= m_context.DeltaTime;
                if (m_idleRemaining <= 0f)
                {
                    PickNextPoint();
                }

                return;
            }

            m_context.MoveTowards(m_target, m_context.Definition.patrolSpeed);
            if (Mathf.Abs(m_context.Position.x - m_target.x) <= m_context.Definition.arriveDistance)
            {
                m_context.Stop();
                m_idleRemaining = RandomIdle();
            }
        }

        public void OnExit()
        {
            m_context.Stop();
        }

        private void PickNextPoint()
        {
            float radius = Mathf.Max(0f, m_context.Definition.patrolRadius);
            float x = m_context.SpawnPosition.x + Random.Range(-radius, radius);
            m_target = new Vector2(x, m_context.Position.y);
            m_idleRemaining = 0f;

            if (Mathf.Abs(m_target.x - m_context.Position.x) > k_facingDeadZone)
            {
                m_context.FaceTowards(m_target);
            }
        }

        private float RandomIdle()
        {
            Vector2 range = m_context.Definition.patrolIdleRange;
            float min = Mathf.Min(range.x, range.y);
            float max = Mathf.Max(range.x, range.y);
            return Random.Range(min, max);
        }
    }
}
