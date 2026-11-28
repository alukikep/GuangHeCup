using Game.Data;

namespace Game.Enemy
{
    /// <summary>
    /// 一次攻击 = 前摇 + 判定 + 后摇。前摇结束的那一帧触发实际判定。
    /// 攻击一旦开始就不会被普通受击打断，结束后立即回到 AlertHigh。
    /// </summary>
    public sealed class AttackState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        private float m_elapsed;
        private bool m_impactDone;

        public AttackState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_context.Stop();
            m_context.FaceTowards(m_context.HasPlayer ? m_context.PlayerPosition : m_context.LastSeenPosition);
            m_elapsed = 0f;
            m_impactDone = false;
        }

        public void OnTick()
        {
            m_elapsed += m_context.DeltaTime;

            EnemyAttackData attack = m_context.Definition.attack;
            if (!m_impactDone && m_elapsed >= attack.windup)
            {
                m_impactDone = true;
                m_context.PerformAttackImpact();
            }

            if (m_elapsed >= attack.TotalDuration)
            {
                m_context.CompleteAttack();
                m_context.ChangeState(EnemyStateId.AlertHigh);
            }
        }

        public void OnExit()
        {
            m_context.Stop();
        }
    }
}
