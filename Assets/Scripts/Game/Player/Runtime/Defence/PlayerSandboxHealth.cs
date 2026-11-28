using System;
using Game.Combat;
using UnityEngine;

namespace Game.Player.Defence
{
    /// <summary>
    /// 测试用玩家受击体。只负责扣血与受击闪色，不做防御或无敌帧。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSandboxHealth : MonoBehaviour, IDamageable, IHealth
    {
        [SerializeField] [Min(1)] private int m_maxHealth = 100;
        [SerializeField] private SpriteRenderer m_visual;
        [SerializeField] private Color m_hitColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] [Min(0.01f)] private float m_hitFlashDuration = 0.12f;

        private int m_currentHealth;
        private Color m_baseColor = Color.white;
        private float m_flashUntil;
        private bool m_isDead;

        public event Action<int, int> Changed;

        public event Action Died;

        public int Current => m_currentHealth;

        public int Max => m_maxHealth;

        public bool IsAlive => !m_isDead && m_currentHealth > 0;

        public CombatFaction Faction => CombatFaction.Player;

        public IHealth Health => this;

        Transform IDamageable.Transform => transform;

        private void Awake()
        {
            m_currentHealth = Mathf.Max(1, m_maxHealth);
            if (m_visual == null)
            {
                m_visual = GetComponentInChildren<SpriteRenderer>();
            }

            if (m_visual != null)
            {
                m_baseColor = m_visual.color;
            }
        }

        private void Update()
        {
            if (m_flashUntil > 0f && Time.time >= m_flashUntil)
            {
                m_flashUntil = 0f;
                if (m_visual != null)
                {
                    m_visual.color = m_baseColor;
                }
            }
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0)
            {
                return;
            }

            m_currentHealth = Mathf.Max(0, m_currentHealth - info.Amount);
            Changed?.Invoke(m_currentHealth, m_maxHealth);

            if (m_visual != null)
            {
                m_visual.color = m_hitColor;
                m_flashUntil = Time.time + Mathf.Max(0.01f, m_hitFlashDuration);
            }

            Debug.Log($"Player took {info.Amount} {info.Type} damage. HP {m_currentHealth}/{m_maxHealth}.", this);

            if (m_currentHealth <= 0)
            {
                m_isDead = true;
                Died?.Invoke();
                Debug.Log("Player died.", this);
            }
        }

        public void Heal(int amount)
        {
            if (!IsAlive || amount <= 0)
            {
                return;
            }

            m_currentHealth = Mathf.Min(m_maxHealth, m_currentHealth + amount);
            Changed?.Invoke(m_currentHealth, m_maxHealth);
        }
    }
}
