using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 攻击范围的 Trigger 包装。只回答“玩家在不在范围内”，
    /// 实际命中由 MeleeEnemy / EnemyProjectile 的显式重叠查询完成。
    /// </summary>
    public sealed class EnemyAttackRange : MonoBehaviour
    {
        [SerializeField] private PlayerTriggerZone m_zone;

        public PlayerTriggerZone Zone => m_zone;

        public bool HasPlayer => m_zone != null && m_zone.HasPlayer;

        public Transform Player => m_zone != null ? m_zone.Player : null;

        private void Awake()
        {
            if (m_zone == null)
            {
                m_zone = GetComponent<PlayerTriggerZone>();
            }
        }

        /// <summary>
        /// 攻击范围 Trigger 跟着朝向镜像。ownerRoot 是敌人根节点。
        /// </summary>
        public void SetFacing(int facing, Transform ownerRoot)
        {
            if (m_zone == null)
            {
                m_zone = GetComponent<PlayerTriggerZone>();
            }

            if (m_zone == null || ownerRoot == null)
            {
                return;
            }

            Transform zoneTransform = m_zone.transform;
            if (zoneTransform == ownerRoot || !zoneTransform.IsChildOf(ownerRoot))
            {
                return;
            }

            m_zone.SetFacing(facing);
        }
    }
}
