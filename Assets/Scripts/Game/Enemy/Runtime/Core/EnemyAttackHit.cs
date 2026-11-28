using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 敌人一次命中的事实。表现层订阅它做特效/音效；
    /// 伤害由 EnemyBrain 通过 Combat.IDamageable 结算。
    /// </summary>
    public readonly struct EnemyAttackHit
    {
        public readonly Collider2D Target;
        public readonly int Damage;
        public readonly Vector2 Point;

        public EnemyAttackHit(Collider2D target, int damage, Vector2 point)
        {
            Target = target;
            Damage = damage;
            Point = point;
        }
    }
}
