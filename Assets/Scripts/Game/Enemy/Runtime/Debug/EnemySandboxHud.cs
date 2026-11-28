using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 手动测试用 HUD。显示所有敌人的状态机事实，并提供锁定、攻击、击杀、治疗入口。
    /// 不引用 Player 类型：玩家只通过 Tag 和 Game.Combat 契约访问。
    /// </summary>
    public sealed class EnemySandboxHud : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField, Min(1f)] private float m_lockRadius = 6f;
        [SerializeField, Min(1f)] private float m_attackRadius = 2.5f;
        [SerializeField, Min(1f)] private float m_killRadius = 10f;
        [SerializeField, Min(1)] private int m_attackDamage = 5;
        [SerializeField, Min(1)] private int m_killDamage = 999;
        [SerializeField, Min(1)] private int m_healAmount = 100;

        [Header("Keys")]
        [SerializeField] private KeyCode m_lockKey = KeyCode.L;
        [SerializeField] private KeyCode m_attackKey = KeyCode.J;
        [SerializeField] private KeyCode m_killKey = KeyCode.K;
        [SerializeField] private KeyCode m_healKey = KeyCode.P;
        [SerializeField] private KeyCode m_helpKey = KeyCode.H;

        private readonly List<EnemyBrain> m_enemies = new List<EnemyBrain>();

        private EnemyBrain m_lockedEnemy;
        private Transform m_player;
        private IHealth m_playerHealth;
        private float m_nextScan;
        private bool m_showHelp = true;

        private void Start()
        {
            CachePlayer();
            RescanEnemies();
        }

        private void Update()
        {
            if (Time.time >= m_nextScan)
            {
                m_nextScan = Time.time + 0.5f;
                RescanEnemies();
            }

            if (m_player == null)
            {
                CachePlayer();
            }

            if (Input.GetKeyDown(m_lockKey))
            {
                ToggleLock();
            }

            if (Input.GetKeyDown(m_attackKey))
            {
                AttackNearest();
            }

            if (Input.GetKeyDown(m_killKey))
            {
                KillNearest();
            }

            if (Input.GetKeyDown(m_healKey))
            {
                HealPlayer();
            }

            if (Input.GetKeyDown(m_helpKey))
            {
                m_showHelp = !m_showHelp;
            }
        }

        private void OnDisable()
        {
            SetLocked(null);
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 620f, 460f), GUI.skin.box);
            GUILayout.Label("ENEMY SANDBOX");

            if (m_playerHealth != null)
            {
                GUILayout.Label($"Player HP: {m_playerHealth.Current}/{m_playerHealth.Max}");
            }
            else
            {
                GUILayout.Label("Player HP: no IHealth (add PlayerSandboxHealth)");
            }

            GUILayout.Label($"Locked: {(m_lockedEnemy != null ? m_lockedEnemy.name : "-")}");
            GUILayout.Space(4f);

            if (m_enemies.Count == 0)
            {
                GUILayout.Label("No EnemyBrain found.");
            }

            foreach (EnemyBrain enemy in m_enemies)
            {
                if (enemy == null)
                {
                    continue;
                }

                string locked = enemy.IsLockedByPlayer ? "LOCK" : "-";
                string trigger = enemy.HasPlayer ? "Y" : "N";
                string distance = m_player != null
                    ? Vector2.Distance(m_player.position, enemy.transform.position).ToString("F2")
                    : "-";
                GUILayout.Label($"{enemy.name}  state:{enemy.CurrentState}  HP:{enemy.Health}/{enemy.MaxHealth}  " +
                    $"facing:{enemy.Facing}  dist:{distance}  lock:{locked}  trig:{trigger}  " +
                    $"see:{enemy.CanSeePlayer}  range:{enemy.PlayerInAttackRange}");
            }

            if (m_showHelp)
            {
                GUILayout.Space(6f);
                GUILayout.Label("L lock nearest 6m | J hit nearest 2.5m | K execute nearest 10m");
                GUILayout.Label("P heal player | H toggle help");
                GUILayout.Label("AlertLow guards the first J; wait for guard to end, then J to damage.");
                GUILayout.Label("Colors: green Patrol | yellow AlertLow | orange AlertHigh");
                GUILayout.Label("red Attack | blue Guard | purple Reposition | gray Dead");
            }

            GUILayout.EndArea();
        }

        private void CachePlayer()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                return;
            }

            m_player = playerObject.transform;
            m_playerHealth = playerObject.GetComponent<IHealth>();
        }

        private void RescanEnemies()
        {
            m_enemies.Clear();
            m_enemies.AddRange(FindObjectsOfType<EnemyBrain>());
            m_enemies.RemoveAll(enemy => enemy == null);
        }

        private void ToggleLock()
        {
            EnemyBrain nearest = FindNearest(m_lockRadius);
            if (nearest == null)
            {
                SetLocked(null);
                Debug.Log("No enemy in lock range.", this);
                return;
            }

            if (m_lockedEnemy == nearest)
            {
                SetLocked(null);
            }
            else
            {
                SetLocked(nearest);
            }
        }

        private void SetLocked(EnemyBrain target)
        {
            if (m_lockedEnemy != null && m_lockedEnemy != target)
            {
                m_lockedEnemy.SetLocked(false);
            }

            m_lockedEnemy = target;
            if (m_lockedEnemy != null)
            {
                m_lockedEnemy.SetLocked(true);
                Debug.Log($"Locked {m_lockedEnemy.name}.", this);
            }
        }

        private void AttackNearest()
        {
            EnemyBrain target = FindNearest(m_attackRadius);
            if (target == null)
            {
                Debug.Log("No enemy in attack range.", this);
                return;
            }

            DamageNearest(target, m_attackDamage);
        }

        private void KillNearest()
        {
            EnemyBrain target = FindNearest(m_killRadius);
            if (target == null)
            {
                Debug.Log("No enemy in kill range.", this);
                return;
            }

            DamageNearest(target, m_killDamage);
        }

        private void DamageNearest(EnemyBrain target, int damage)
        {
            target.NotifyPlayerAttacking();
            Vector2 source = m_player != null ? (Vector2)m_player.position : target.Position;
            DamageInfo info = new DamageInfo(damage, source, target.Position, false, DamageType.Melee,
                CombatFaction.Player, m_player != null ? m_player.gameObject : gameObject);
            target.TakeDamage(info);
            Debug.Log($"Hit {target.name} for {damage}. HP {target.Health}/{target.MaxHealth}.", this);
        }

        private void HealPlayer()
        {
            if (m_playerHealth == null)
            {
                Debug.Log("Player has no IHealth.", this);
                return;
            }

            m_playerHealth.Heal(m_healAmount);
            Debug.Log($"Player healed to {m_playerHealth.Current}/{m_playerHealth.Max}.", this);
        }

        private EnemyBrain FindNearest(float maxDistance)
        {
            if (m_player == null)
            {
                return null;
            }

            float bestSqr = maxDistance * maxDistance;
            EnemyBrain best = null;
            Vector2 playerPosition = m_player.position;
            foreach (EnemyBrain enemy in m_enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                float sqr = (enemy.Position - playerPosition).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = enemy;
                }
            }

            return best;
        }
    }
}
