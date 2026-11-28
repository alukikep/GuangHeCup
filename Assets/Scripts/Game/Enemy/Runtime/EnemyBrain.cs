using System;
using Game.Combat;
using Game.Data;
using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 敌人父类。只负责把“世界状态”喂给状态机，并把状态机意图变成
    /// Rigidbody2D 速度、朝向、攻击判定。具体攻击方式由子类实现。
    /// 伤害通过 Game.Combat.IDamageable 结算，不认识 Player 类型。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class EnemyBrain : MonoBehaviour, IEnemyContext, IDamageable, IHealth
    {
        [Header("Data")]
        [SerializeField] private EnemyDefinition m_definition;
        [SerializeField] private int m_initialFacing = -1;

        [Header("Sensors")]
        [SerializeField] private EnemyVision m_vision;
        [SerializeField] private EnemyAttackRange m_attackRange;

        [Header("Presentation")]
        [SerializeField] private Transform m_visual;
        [SerializeField] private Transform m_attackPoint;
        [SerializeField] private Vector2 m_defaultAttackOffset = new Vector2(0.6f, 0.6f);

        [Header("State Colors")]
        [SerializeField] private Color m_patrolColor = new Color(0.45f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color m_alertLowColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField] private Color m_alertHighColor = new Color(1f, 0.5f, 0.15f, 1f);
        [SerializeField] private Color m_attackColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField] private Color m_guardColor = new Color(0.25f, 0.6f, 1f, 1f);
        [SerializeField] private Color m_repositionColor = new Color(0.7f, 0.35f, 1f, 1f);
        [SerializeField] private Color m_deadColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private EnemyStateMachine m_machine;
        private SpriteRenderer m_visualRenderer;
        private Rigidbody2D m_body;
        private Collider2D m_bodyCollider;
        private int m_health;
        private int m_facing = 1;
        private bool m_locked;
        private bool m_isGuarding;
        private bool m_isDead;
        private float m_playerAttackUntil;
        private float m_nextAttackTime;
        private Vector2 m_spawnPosition;
        private Vector2 m_lastSeenPosition;

        public event Action<EnemyStateId, EnemyStateId> StateChanged;

        public event Action<int, int> HealthChanged;

        public event Action Died;

        public event Action Guarded;

        public event Action<EnemyAttackHit> AttackHit;

        public event Action<GameObject> LootDropped;

        public EnemyDefinition Definition => m_definition;

        // 灰盒期没有 IGameClock，先用 Unity 时间；接入 Game.Core 后替换。
        public float DeltaTime => Time.deltaTime;

        public float Now => Time.time;

        public int Facing => m_facing;

        public bool HasPlayer => m_vision != null && m_vision.HasPlayer;

        public bool CanSeePlayer => m_vision != null && m_vision.CanSeePlayer;

        public bool PlayerInAttackRange => m_attackRange != null && m_attackRange.HasPlayer;

        public bool IsLockedByPlayer => m_locked;

        public bool PlayerIsAttacking => Time.time < m_playerAttackUntil;

        public bool CanAttack => !m_isDead && !m_isGuarding && Time.time >= m_nextAttackTime;

        public Vector2 Position => m_body != null ? m_body.position : (Vector2)transform.position;

        public Vector2 PlayerPosition =>
            m_vision != null && m_vision.Player != null ? (Vector2)m_vision.Player.position : m_lastSeenPosition;

        public Vector2 SpawnPosition => m_spawnPosition;

        public Vector2 LastSeenPosition => m_lastSeenPosition;

        public EnemyStateId CurrentState => m_machine != null ? m_machine.CurrentId : EnemyStateId.Patrol;

        public int Health => m_health;

        public int MaxHealth => m_definition != null ? m_definition.maxHealth : 0;

        public string PlayerTag => m_vision != null && m_vision.Zone != null ? m_vision.Zone.PlayerTag : "Player";

        public bool IsAlive => !m_isDead && m_health > 0;

        public CombatFaction Faction => CombatFaction.Enemy;

        IHealth IDamageable.Health => this;

        Transform IDamageable.Transform => transform;

        int IHealth.Current => m_health;

        int IHealth.Max => MaxHealth;

        event Action<int, int> IHealth.Changed
        {
            add => HealthChanged += value;
            remove => HealthChanged -= value;
        }

        event Action IHealth.Died
        {
            add => Died += value;
            remove => Died -= value;
        }

        protected Vector2 AttackOrigin
        {
            get
            {
                if (m_attackPoint != null)
                {
                    return m_attackPoint.position;
                }

                return (Vector2)transform.position + new Vector2(m_facing * m_defaultAttackOffset.x, m_defaultAttackOffset.y);
            }
        }

        private void Awake()
        {
            m_body = GetComponent<Rigidbody2D>();
            m_bodyCollider = GetComponent<Collider2D>();

            if (m_vision == null)
            {
                m_vision = GetComponentInChildren<EnemyVision>();
            }

            if (m_attackRange == null)
            {
                m_attackRange = GetComponentInChildren<EnemyAttackRange>();
            }

            if (m_visual == null)
            {
                SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
                if (sprite != null && sprite.transform != transform)
                {
                    m_visual = sprite.transform;
                }
            }

            if (m_visual != null)
            {
                m_visualRenderer = m_visual.GetComponent<SpriteRenderer>();
                if (m_visualRenderer == null)
                {
                    m_visualRenderer = m_visual.GetComponentInChildren<SpriteRenderer>();
                }
            }

            if (m_definition == null)
            {
                Debug.LogError("EnemyBrain 没有配置 EnemyDefinition。", this);
                enabled = false;
                return;
            }

            if (m_vision == null)
            {
                Debug.LogWarning("EnemyBrain 找不到 EnemyVision，敌人不会发现玩家。", this);
            }

            if (m_attackRange == null)
            {
                Debug.LogWarning("EnemyBrain 找不到 EnemyAttackRange，敌人不会进入攻击范围。", this);
            }

            m_facing = m_initialFacing >= 0 ? 1 : -1;
            m_health = Mathf.Max(1, m_definition.maxHealth);
            m_spawnPosition = transform.position;
            m_lastSeenPosition = m_spawnPosition;

            if (m_body != null)
            {
                m_body.freezeRotation = true;
            }

            ApplyFacing();

            m_machine = new EnemyStateMachine(this);
            m_machine.Register(EnemyStateId.Patrol, new PatrolState(this));
            m_machine.Register(EnemyStateId.AlertLow, new AlertLowState(this));
            m_machine.Register(EnemyStateId.AlertHigh, new AlertHighState(this));
            m_machine.Register(EnemyStateId.Attack, new AttackState(this));
            m_machine.Register(EnemyStateId.Guard, new GuardState(this));
            m_machine.Register(EnemyStateId.Reposition, new RepositionState(this));
            m_machine.Register(EnemyStateId.Dead, new DeadState(this));
            m_machine.StateChanged += HandleStateChanged;
            m_machine.Initialize(EnemyStateId.Patrol);
        }

        private void Update()
        {
            if (m_isDead || m_machine == null)
            {
                return;
            }

            if (m_vision != null && m_vision.CanSeePlayer)
            {
                m_lastSeenPosition = m_vision.Player.position;
            }

            m_machine.Tick();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (m_definition == null)
            {
                return;
            }

            Vector3 patrolCenter = Application.isPlaying ? (Vector3)m_spawnPosition : transform.position;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(patrolCenter, m_definition.patrolRadius);

            Vector3 searchCenter = Application.isPlaying ? (Vector3)m_lastSeenPosition : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(searchCenter, m_definition.repositionRadius);
        }

        public void ChangeState(EnemyStateId state)
        {
            if (m_machine == null)
            {
                return;
            }

            if (m_isDead && state != EnemyStateId.Dead)
            {
                return;
            }

            m_machine.ChangeState(state);
        }

        public void MoveTowards(Vector2 target, float speed)
        {
            if (m_isDead || m_body == null)
            {
                return;
            }

            float deltaX = target.x - Position.x;
            if (Mathf.Abs(deltaX) <= m_definition.arriveDistance)
            {
                Stop();
                return;
            }

            int direction = deltaX > 0f ? 1 : -1;
            Face(direction);

            Vector2 velocity = m_body.velocity;
            velocity.x = direction * Mathf.Max(0f, speed);
            m_body.velocity = velocity;
        }

        public void Stop()
        {
            if (m_body == null)
            {
                return;
            }

            Vector2 velocity = m_body.velocity;
            velocity.x = 0f;
            m_body.velocity = velocity;
        }

        public void FaceTowards(Vector2 target)
        {
            float deltaX = target.x - Position.x;
            if (Mathf.Abs(deltaX) > 0.01f)
            {
                Face(deltaX > 0f ? 1 : -1);
            }
        }

        public void PerformAttackImpact()
        {
            if (m_isDead || m_definition == null)
            {
                return;
            }

            m_nextAttackTime = Now + m_definition.attack.cooldown;
            OnAttackImpact();
        }

        public void CompleteAttack()
        {
            if (m_isDead)
            {
                return;
            }
        }

        public void BeginGuard()
        {
            m_isGuarding = true;
        }

        public void EndGuard()
        {
            m_isGuarding = false;
        }

        /// <summary>
        /// 由 Battle 的锁定系统调用。锁定后若玩家在视野内，立刻进入 AlertHigh。
        /// </summary>
        public void SetLocked(bool locked)
        {
            m_locked = locked;
            if (!locked || m_isDead || m_machine == null || !CanSeePlayer)
            {
                return;
            }

            EnemyStateId current = CurrentState;
            if (current == EnemyStateId.Patrol || current == EnemyStateId.AlertLow || current == EnemyStateId.Reposition)
            {
                ChangeState(EnemyStateId.AlertHigh);
            }
        }

        /// <summary>
        /// 玩家出招时由 Player 侧或 Battle 调用。AlertLow 在记忆窗口内不会抢招。
        /// </summary>
        public void NotifyPlayerAttacking()
        {
            if (m_isDead || m_definition == null)
            {
                return;
            }

            m_playerAttackUntil = Now + m_definition.playerAttackMemory;
        }

        public void TakeDamage(in DamageInfo damage)
        {
            if (!IsAlive || damage.Amount <= 0 || m_machine == null)
            {
                return;
            }

            m_playerAttackUntil = Now + m_definition.playerAttackMemory;

            if (m_isGuarding || CurrentState == EnemyStateId.Guard)
            {
                Guarded?.Invoke();
                return;
            }

            if (CurrentState == EnemyStateId.AlertLow && m_definition.guardWhenAttacked)
            {
                ChangeState(EnemyStateId.Guard);
                Guarded?.Invoke();
                return;
            }

            m_health = Mathf.Max(0, m_health - damage.Amount);
            HealthChanged?.Invoke(m_health, MaxHealth);

            if (m_health <= 0)
            {
                Die();
                return;
            }

            EnemyStateId current = CurrentState;
            if (current == EnemyStateId.Patrol || current == EnemyStateId.AlertLow || current == EnemyStateId.Reposition)
            {
                ChangeState(EnemyStateId.AlertHigh);
            }
        }

        void IHealth.Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            int max = MaxHealth;
            m_health = Mathf.Clamp(m_health + amount, 0, max);
            HealthChanged?.Invoke(m_health, max);
        }

        protected abstract void OnAttackImpact();

        /// <summary>
        /// 显式重叠查询后的命中结算。子类负责找到目标，这里只负责过滤和发事件。
        /// </summary>
        protected void ResolveHit(Collider2D target, Vector2 point, DamageType damageType)
        {
            if (m_isDead || target == null || m_definition == null)
            {
                return;
            }

            int damage = m_definition.attack.damage;
            AttackHit?.Invoke(new EnemyAttackHit(target, damage, point));

            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.Health == null || !damageable.Health.IsAlive)
            {
                return;
            }

            damageable.TakeDamage(new DamageInfo(damage, Position, point, false, damageType,
                CombatFaction.Enemy, gameObject));
        }

        protected Transform ResolvePlayerRoot(Collider2D collider)
        {
            if (collider == null)
            {
                return null;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                if (current.CompareTag(PlayerTag))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        internal void HandleProjectileHit(Collider2D target, Vector2 point)
        {
            ResolveHit(target, point, DamageType.Projectile);
        }

        private void Face(int direction)
        {
            if (direction == 0)
            {
                return;
            }

            m_facing = direction > 0 ? 1 : -1;
            ApplyFacing();
        }

        private void ApplyFacing()
        {
            if (m_visual != null)
            {
                Vector3 scale = m_visual.localScale;
                scale.x = Mathf.Abs(scale.x) * m_facing;
                m_visual.localScale = scale;
            }

            if (m_vision != null)
            {
                m_vision.SetFacing(m_facing, transform);
            }

            if (m_attackRange != null)
            {
                m_attackRange.SetFacing(m_facing, transform);
            }
        }

        private void OnDisable()
        {
            if (!m_isDead)
            {
                Stop();
            }
        }

        private void HandleStateChanged(EnemyStateId previous, EnemyStateId next)
        {
            ApplyStateColor(next);
            StateChanged?.Invoke(previous, next);
        }

        private void ApplyStateColor(EnemyStateId state)
        {
            if (m_visualRenderer == null)
            {
                return;
            }

            switch (state)
            {
                case EnemyStateId.Patrol:
                    m_visualRenderer.color = m_patrolColor;
                    break;
                case EnemyStateId.AlertLow:
                    m_visualRenderer.color = m_alertLowColor;
                    break;
                case EnemyStateId.AlertHigh:
                    m_visualRenderer.color = m_alertHighColor;
                    break;
                case EnemyStateId.Attack:
                    m_visualRenderer.color = m_attackColor;
                    break;
                case EnemyStateId.Guard:
                    m_visualRenderer.color = m_guardColor;
                    break;
                case EnemyStateId.Reposition:
                    m_visualRenderer.color = m_repositionColor;
                    break;
                case EnemyStateId.Dead:
                    m_visualRenderer.color = m_deadColor;
                    break;
            }
        }

        private void Die()
        {
            if (m_isDead)
            {
                return;
            }

            m_isDead = true;
            m_health = 0;
            HealthChanged?.Invoke(0, MaxHealth);
            m_machine.ChangeState(EnemyStateId.Dead);
            Stop();

            if (m_bodyCollider != null)
            {
                m_bodyCollider.enabled = false;
            }

            Died?.Invoke();
            DropLoot();

            if (m_definition.deathDestroyDelay > 0f)
            {
                Destroy(gameObject, m_definition.deathDestroyDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void DropLoot()
        {
            if (m_definition.loot == null)
            {
                return;
            }

            foreach (LootEntry entry in m_definition.loot)
            {
                if (entry == null || entry.itemPrefab == null)
                {
                    continue;
                }

                if (UnityEngine.Random.value > entry.dropChance)
                {
                    continue;
                }

                int minAmount = Mathf.Max(0, Mathf.Min(entry.minAmount, entry.maxAmount));
                int maxAmount = Mathf.Max(0, Mathf.Max(entry.minAmount, entry.maxAmount));
                if (maxAmount <= 0)
                {
                    continue;
                }

                int amount = UnityEngine.Random.Range(minAmount, maxAmount + 1);
                if (amount <= 0)
                {
                    continue;
                }

                Vector2 scatter = UnityEngine.Random.insideUnitCircle * m_definition.lootScatterRadius;
                GameObject drop = Instantiate(entry.itemPrefab, (Vector2)transform.position + scatter, Quaternion.identity);
                EnemyLootDrop lootDrop = drop.GetComponent<EnemyLootDrop>();
                if (lootDrop != null)
                {
                    lootDrop.Amount = amount;
                }

                LootDropped?.Invoke(drop);
            }
        }
    }
}
