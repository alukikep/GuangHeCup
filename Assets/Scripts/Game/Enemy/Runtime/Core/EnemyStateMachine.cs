using System;
using System.Collections.Generic;

namespace Game.Enemy
{
    /// <summary>
    /// 纯 C# 状态机。没有 MonoBehaviour、没有 Time，Tick 由外部驱动。
    /// </summary>
    public sealed class EnemyStateMachine
    {
        private readonly Dictionary<EnemyStateId, IEnemyState> m_states =
            new Dictionary<EnemyStateId, IEnemyState>();

        private readonly IEnemyContext m_context;
        private IEnemyState m_current;

        public EnemyStateMachine(IEnemyContext context)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public EnemyStateId CurrentId { get; private set; }

        public bool IsRunning => m_current != null;

        public event Action<EnemyStateId, EnemyStateId> StateChanged;

        public void Register(EnemyStateId id, IEnemyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (m_states.ContainsKey(id))
            {
                throw new InvalidOperationException($"State {id} is already registered.");
            }

            m_states.Add(id, state);
        }

        public void Initialize(EnemyStateId id)
        {
            m_current = Get(id);
            CurrentId = id;
            m_current.OnEnter();
            StateChanged?.Invoke(id, id);
        }

        public void Tick()
        {
            m_current?.OnTick();
        }

        public void ChangeState(EnemyStateId next)
        {
            if (m_current == null)
            {
                Initialize(next);
                return;
            }

            if (CurrentId == next)
            {
                return;
            }

            EnemyStateId previous = CurrentId;
            m_current.OnExit();
            m_current = Get(next);
            CurrentId = next;
            m_current.OnEnter();
            StateChanged?.Invoke(previous, next);
        }

        private IEnemyState Get(EnemyStateId id)
        {
            if (!m_states.TryGetValue(id, out IEnemyState state))
            {
                throw new InvalidOperationException($"State {id} is not registered.");
            }

            return state;
        }
    }
}
