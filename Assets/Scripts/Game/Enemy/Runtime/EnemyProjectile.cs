using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 远程敌人的投射物。每帧显式圆形查询，不使用 Trigger 回调做命中。
    /// Prefab 上不要挂 Collider2D，否则会打到自己。
    /// </summary>
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float m_radius = 0.12f;
        [SerializeField] private LayerMask m_hitMask = ~0;

        private readonly Collider2D[] m_hitBuffer = new Collider2D[8];

        private EnemyBrain m_owner;
        private Vector2 m_direction = Vector2.right;
        private float m_speed = 8f;
        private float m_lifetime = 3f;
        private string m_targetTag = "Player";

        private void Awake()
        {
            if (m_hitMask.value == 0)
            {
                m_hitMask = ~0;
            }
        }

        public void Launch(EnemyBrain owner, Vector2 direction, float speed, float lifetime, float radius,
            string targetTag)
        {
            m_owner = owner;
            m_direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            m_speed = Mathf.Max(0f, speed);
            m_lifetime = Mathf.Max(0.01f, lifetime);
            m_radius = Mathf.Max(0.01f, radius);
            m_targetTag = targetTag;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            m_lifetime -= deltaTime;
            if (m_lifetime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position += (Vector3)(m_direction * (m_speed * deltaTime));

            int count = Physics2D.OverlapCircleNonAlloc(transform.position, m_radius, m_hitBuffer, m_hitMask);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = m_hitBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                if (m_owner != null &&
                    (hit.transform == m_owner.transform || hit.transform.IsChildOf(m_owner.transform)))
                {
                    continue;
                }

                if (IsPlayerCollider(hit))
                {
                    if (m_owner != null)
                    {
                        m_owner.HandleProjectileHit(hit, transform.position);
                    }

                    Destroy(gameObject);
                    return;
                }

                if (!hit.isTrigger)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

        private bool IsPlayerCollider(Collider2D collider)
        {
            Transform current = collider.transform;
            while (current != null)
            {
                if (current.CompareTag(m_targetTag))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
