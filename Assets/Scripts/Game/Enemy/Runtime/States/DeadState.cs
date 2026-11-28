namespace Game.Enemy
{
    public sealed class DeadState : IEnemyState
    {
        private readonly IEnemyContext m_context;

        public DeadState(IEnemyContext context)
        {
            m_context = context;
        }

        public void OnEnter()
        {
            m_context.Stop();
        }

        public void OnTick()
        {
        }

        public void OnExit()
        {
        }
    }
}
