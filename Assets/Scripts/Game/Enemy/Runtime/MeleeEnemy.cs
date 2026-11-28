using System.Collections.Generic;
using Game.Combat;
using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 近战敌人。攻击帧到达时做一次显式圆形重叠查询，
    /// Trigger 只负责攻击范围提示，不参与命中。
    /// </summary>
    public sealed class MeleeEnemy : EnemyBrain
    {
        [SerializeField] private float m_attackRadius = 0.9f;
        [SerializeField] private int m_maxTargets = 4;

        private readonly Collider2D[] m_hitBuffer = new Collider2D[8];
        private readonly HashSet<Transform> m_hitRoots = new HashSet<Transform>();

        protected override void OnAttackImpact()
        {
            Vector2 center = AttackOrigin;
            int count = Physics2D.OverlapCircleNonAlloc(center, m_attackRadius, m_hitBuffer, Physics2D.AllLayers);
            int limit = Mathf.Clamp(m_maxTargets, 1, m_hitBuffer.Length);
            m_hitRoots.Clear();

            for (int i = 0; i < count && m_hitRoots.Count < limit; i++)
            {
                Collider2D hit = m_hitBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }

                Transform root = ResolvePlayerRoot(hit);
                if (root == null || !m_hitRoots.Add(root))
                {
                    continue;
                }

                ResolveHit(hit, center, DamageType.Melee);
            }
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(AttackOrigin, m_attackRadius);
        }
    }
}
