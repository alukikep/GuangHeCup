using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 视野 = Trigger 内的玩家 + 可选的障碍线检。Trigger 只负责“玩家在不在范围内”，
    /// 不做命中判定。
    /// </summary>
    public sealed class EnemyVision : MonoBehaviour
    {
        [SerializeField] private PlayerTriggerZone m_zone;
        [SerializeField] private LayerMask m_obstacleMask;
        [SerializeField] private float m_eyeHeight = 1f;
        [SerializeField] private float m_targetHeight = 0.8f;

        private const float k_minSightDistance = 0.01f;

        private readonly RaycastHit2D[] m_hitBuffer = new RaycastHit2D[16];

        public PlayerTriggerZone Zone => m_zone;

        public bool HasPlayer => m_zone != null && m_zone.HasPlayer;

        public Transform Player => m_zone != null ? m_zone.Player : null;

        public bool CanSeePlayer
        {
            get
            {
                if (!HasPlayer)
                {
                    return false;
                }

                if (m_obstacleMask.value == 0)
                {
                    return true;
                }

                Vector2 from = (Vector2)transform.position + Vector2.up * m_eyeHeight;
                Vector2 to = (Vector2)m_zone.Player.position + Vector2.up * m_targetHeight;
                int count = Physics2D.LinecastNonAlloc(from, to, m_hitBuffer, m_obstacleMask);

                // 只让最近的“非自身、非 Trigger”碰撞体决定可见性。
                // 自身视野/攻击范围是 Trigger，不参与遮挡；自身身体也不能挡自己的视线。
                float nearest = float.PositiveInfinity;
                bool blocked = false;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit2D hit = m_hitBuffer[i];
                    Collider2D collider = hit.collider;
                    if (collider == null || collider.isTrigger)
                    {
                        continue;
                    }

                    // 眼睛本身嵌在平台/天花板里时，不能把这块几何当成视线障碍。
                    if (hit.distance <= k_minSightDistance || collider.OverlapPoint(from))
                    {
                        continue;
                    }

                    Transform hitTransform = collider.transform;
                    if (hitTransform == transform.root || hitTransform.IsChildOf(transform.root))
                    {
                        continue;
                    }

                    if (hit.distance >= nearest)
                    {
                        continue;
                    }

                    nearest = hit.distance;
                    blocked = hitTransform != m_zone.Player && !hitTransform.IsChildOf(m_zone.Player);
                }

                return !blocked;
            }
        }

        private void Awake()
        {
            if (m_zone == null)
            {
                m_zone = GetComponent<PlayerTriggerZone>();
            }
        }

        private void OnDrawGizmos()
        {
            PolygonCollider2D polygon = GetComponent<PolygonCollider2D>();
            if (polygon == null)
            {
                return;
            }

            bool seesPlayer = Application.isPlaying && CanSeePlayer;
            Gizmos.color = seesPlayer ? Color.green : new Color(1f, 0.8f, 0.2f, 0.7f);
            for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
            {
                Vector2[] points = polygon.GetPath(pathIndex);
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 from = polygon.transform.TransformPoint(points[i]);
                    Vector3 to = polygon.transform.TransformPoint(points[(i + 1) % points.Length]);
                    Gizmos.DrawLine(from, to);
                }
            }

            if (!Application.isPlaying || m_zone == null || m_zone.Player == null)
            {
                return;
            }

            Vector2 eye = (Vector2)transform.position + Vector2.up * m_eyeHeight;
            Vector2 target = (Vector2)m_zone.Player.position + Vector2.up * m_targetHeight;
            Gizmos.color = seesPlayer ? Color.green : Color.red;
            Gizmos.DrawLine(eye, target);
        }

        /// <summary>
        /// 让视野 Trigger 跟着朝向镜像。ownerRoot 是敌人根节点，
        /// 根节点自己的 Trigger 不移动，避免把整个敌人挪走。
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
