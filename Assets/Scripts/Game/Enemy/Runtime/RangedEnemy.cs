using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 远程敌人。攻击帧到达时发射一枚投射物，命中判定在 EnemyProjectile 里做。
    /// </summary>
    public sealed class RangedEnemy : EnemyBrain
    {
        [SerializeField] private EnemyProjectile m_projectilePrefab;
        [SerializeField] private float m_projectileSpeed = 8f;
        [SerializeField] private float m_projectileLifetime = 3f;
        [SerializeField] private float m_projectileRadius = 0.12f;

        protected override void OnAttackImpact()
        {
            if (m_projectilePrefab == null)
            {
                Debug.LogWarning("RangedEnemy 没有配置投射物 Prefab。", this);
                return;
            }

            EnemyProjectile projectile = Instantiate(m_projectilePrefab, AttackOrigin, Quaternion.identity);
            if (projectile == null)
            {
                Debug.LogWarning("RangedEnemy 的投射物 Prefab 上没有 EnemyProjectile。", this);
                return;
            }

            projectile.Launch(this, new Vector2(Facing, 0f), m_projectileSpeed, m_projectileLifetime,
                m_projectileRadius, PlayerTag);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(AttackOrigin, 0.1f);
        }
    }
}
