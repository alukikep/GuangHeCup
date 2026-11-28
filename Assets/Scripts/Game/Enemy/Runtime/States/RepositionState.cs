using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 在最后可见位置附近来回巡逻。重新看到玩家立刻回 AlertHigh；
    /// 搜索超时后回 Patrol，避免敌人永远停在警戒状态。
    /// </summary>
    public sealed class RepositionState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        private Vector2 m_anchor;
        private Vector2 m_target;
        private float m_elapsed;
        private float m_idleRemaining;

        public RepositionState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_anchor = m_context.LastSeenPosition;
            m_elapsed = 0f;
            m_context.Stop();
            PickNextPoint();
        }

        public void OnTick()
        {
            if (m_context.CanSeePlayer)
            {
                m_context.ChangeState(EnemyStateId.AlertHigh);
                return;
            }

            m_elapsed += m_context.DeltaTime;
            if (m_elapsed >= m_context.Definition.repositionDuration)
            {
                m_context.ChangeState(EnemyStateId.Patrol);
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
            float radius = Mathf.Max(0f, m_context.Definition.repositionRadius);
            float x = m_anchor.x + Random.Range(-radius, radius);
            m_target = new Vector2(x, m_context.Position.y);
            m_idleRemaining = 0f;

            if (Mathf.Abs(m_target.x - m_context.Position.x) > 0.01f)
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
