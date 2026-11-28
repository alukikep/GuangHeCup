using System.Collections.Generic;
using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 记录当前待在 Trigger 内的玩家碰撞体。一个玩家可能有多个碰撞体，
    /// 所以按碰撞体计数，而不是简单的 bool。
    /// </summary>
    public sealed class PlayerTriggerZone : MonoBehaviour
    {
        [SerializeField] private string m_playerTag = "Player";

        private readonly HashSet<Collider2D> m_colliders = new HashSet<Collider2D>();
        private readonly List<PolygonCollider2D> m_polygons = new List<PolygonCollider2D>();
        private readonly List<Vector2[][]> m_polygonPoints = new List<Vector2[][]>();
        private readonly List<EdgeCollider2D> m_edges = new List<EdgeCollider2D>();
        private readonly List<Vector2[]> m_edgePoints = new List<Vector2[]>();
        private readonly List<Collider2D> m_offsetColliders = new List<Collider2D>();
        private readonly List<Vector2> m_baseOffsets = new List<Vector2>();
        private readonly Collider2D[] m_overlapBuffer = new Collider2D[16];

        private ContactFilter2D m_overlapFilter;
        private Collider2D m_zoneCollider;
        private Transform m_player;
        private bool m_geometryCaptured;
        private int m_appliedFacing;

        public bool HasPlayer => m_player != null;

        public Transform Player => m_player;

        public string PlayerTag => m_playerTag;

        private void Reset()
        {
            Collider2D zone = GetComponent<Collider2D>();
            if (zone != null)
            {
                zone.isTrigger = true;
            }
        }

        private void Awake()
        {
            m_zoneCollider = GetComponent<Collider2D>();
            if (m_zoneCollider == null)
            {
                Debug.LogError("PlayerTriggerZone 需要同物体上的 Collider2D。", this);
            }
            else if (!m_zoneCollider.isTrigger)
            {
                Debug.LogWarning("PlayerTriggerZone 的 Collider2D 不是 Trigger。", this);
            }

            m_overlapFilter = new ContactFilter2D();
            m_overlapFilter.useTriggers = true;
            m_overlapFilter.useLayerMask = false;
        }

        /// <summary>
        /// 不依赖 OnTriggerEnter 回调，每物理帧直接查询 Trigger 自身重叠到的碰撞体。
        /// 这样即使 Trigger 在玩家已经位于其中时才启用，也能立即得到 HasPlayer。
        /// </summary>
        private void FixedUpdate()
        {
            if (m_zoneCollider == null)
            {
                return;
            }

            int count = m_zoneCollider.OverlapCollider(m_overlapFilter, m_overlapBuffer);
            m_colliders.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider2D other = m_overlapBuffer[i];
                if (other == null || other == m_zoneCollider)
                {
                    continue;
                }

                if (ResolvePlayerRoot(other) != null)
                {
                    m_colliders.Add(other);
                }
            }

            RefreshPlayer();
        }

        private void OnDisable()
        {
            m_colliders.Clear();
            m_player = null;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (ResolvePlayerRoot(other) == null)
            {
                return;
            }

            m_colliders.Add(other);
            RefreshPlayer();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (m_colliders.Contains(other))
            {
                return;
            }

            if (ResolvePlayerRoot(other) == null)
            {
                return;
            }

            m_colliders.Add(other);
            RefreshPlayer();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (m_colliders.Remove(other))
            {
                RefreshPlayer();
            }
        }

        /// <summary>
        /// 让 Trigger 的碰撞体几何按敌人朝向镜像。只应作用于敌人的子物体，
        /// 由调用方保证不会传敌人根节点。不要移动 transform：子物体的 localPosition
        /// 通常是 0，真正决定形状的是碰撞体自己的 points / offset。
        /// </summary>
        public void SetFacing(int facing)
        {
            if (transform.parent == null)
            {
                return;
            }

            if (!m_geometryCaptured)
            {
                CaptureGeometry();
            }

            int sign = facing < 0 ? -1 : 1;
            if (m_appliedFacing == sign)
            {
                return;
            }

            m_appliedFacing = sign;
            ApplyPolygonFacing(sign);
            ApplyEdgeFacing(sign);
            ApplyOffsetFacing(sign);
        }

        /// <summary>
        /// 从碰撞体向上找带 Player Tag 的根。找不到返回 null。
        /// </summary>
        public Transform ResolvePlayerRoot(Collider2D other)
        {
            if (other == null)
            {
                return null;
            }

            Transform current = other.transform;
            while (current != null)
            {
                if (current.CompareTag(m_playerTag))
                {
                    return current;
                }

                current = current.parent;
            }

            return null;
        }

        private void CaptureGeometry()
        {
            m_geometryCaptured = true;
            m_polygons.Clear();
            m_polygonPoints.Clear();
            m_edges.Clear();
            m_edgePoints.Clear();
            m_offsetColliders.Clear();
            m_baseOffsets.Clear();

            Collider2D[] colliders = GetComponents<Collider2D>();
            foreach (Collider2D collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                m_offsetColliders.Add(collider);
                m_baseOffsets.Add(collider.offset);

                if (collider is PolygonCollider2D polygon)
                {
                    m_polygons.Add(polygon);
                    Vector2[][] paths = new Vector2[polygon.pathCount][];
                    for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
                    {
                        paths[pathIndex] = polygon.GetPath(pathIndex);
                    }

                    m_polygonPoints.Add(paths);
                }
                else if (collider is EdgeCollider2D edge)
                {
                    m_edges.Add(edge);
                    m_edgePoints.Add(edge.points);
                }
            }
        }

        private void ApplyPolygonFacing(int sign)
        {
            for (int i = 0; i < m_polygons.Count; i++)
            {
                PolygonCollider2D polygon = m_polygons[i];
                if (polygon == null)
                {
                    continue;
                }

                Vector2[][] paths = m_polygonPoints[i];
                polygon.pathCount = paths.Length;
                for (int pathIndex = 0; pathIndex < paths.Length; pathIndex++)
                {
                    polygon.SetPath(pathIndex, FlipPoints(paths[pathIndex], sign));
                }
            }
        }

        private void ApplyEdgeFacing(int sign)
        {
            for (int i = 0; i < m_edges.Count; i++)
            {
                EdgeCollider2D edge = m_edges[i];
                if (edge == null)
                {
                    continue;
                }

                edge.points = FlipPoints(m_edgePoints[i], sign);
            }
        }

        private void ApplyOffsetFacing(int sign)
        {
            for (int i = 0; i < m_offsetColliders.Count; i++)
            {
                Collider2D collider = m_offsetColliders[i];
                if (collider == null)
                {
                    continue;
                }

                Vector2 offset = m_baseOffsets[i];
                collider.offset = new Vector2(offset.x * sign, offset.y);
            }
        }

        private static Vector2[] FlipPoints(Vector2[] source, int sign)
        {
            Vector2[] flipped = new Vector2[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                flipped[i] = new Vector2(source[i].x * sign, source[i].y);
            }

            return flipped;
        }

        private void RefreshPlayer()
        {
            m_colliders.RemoveWhere(collider => collider == null);
            m_player = null;

            foreach (Collider2D collider in m_colliders)
            {
                Transform root = ResolvePlayerRoot(collider);
                if (root != null)
                {
                    m_player = root;
                    return;
                }
            }
        }
    }
}
