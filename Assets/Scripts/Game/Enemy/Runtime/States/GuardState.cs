namespace Game.Enemy
{
    /// <summary>
    /// 格挡是短暂状态：不受伤，时间到后立刻进入 AlertHigh。
    /// 格挡期间不再接受伤害，具体判定在 EnemyBrain.TakeDamage。
    /// </summary>
    public sealed class GuardState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        private float m_elapsed;

        public GuardState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_elapsed = 0f;
            m_context.Stop();
            m_context.FaceTowards(m_context.HasPlayer ? m_context.PlayerPosition : m_context.LastSeenPosition);
            m_context.BeginGuard();
        }

        public void OnTick()
        {
            m_context.Stop();
            m_elapsed += m_context.DeltaTime;
            if (m_elapsed >= m_context.Definition.guardDuration)
            {
                m_context.EndGuard();
                m_context.ChangeState(EnemyStateId.AlertHigh);
            }
        }

        public void OnExit()
        {
            m_context.EndGuard();
        }
    }
}
