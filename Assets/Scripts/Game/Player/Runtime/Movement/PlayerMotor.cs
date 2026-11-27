using System;
using UnityEngine;

namespace Game.Player.Movement
{
    /// <summary>
    /// 角色移动：左右匀速、跳跃、脚底地面检测、朝向。
    /// 只驱动 Rigidbody2D 的速度，不做输入采样（见 PlayerInputReader），也不含任何战斗逻辑。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("左右移动")]
        [SerializeField, Min(0f)] private float moveSpeed = 6f;
        [SerializeField, Range(0f, 1f)] private float airControl = 0.7f;

        [Header("跳跃")]
        [SerializeField, Min(0f)] private float jumpVelocity = 14.7f;
        [SerializeField, Min(0.01f)] private float gravityScale = 5f;

        [Header("地面检测")]
        [SerializeField, Min(0.01f)] private float groundCheckHeight = 0.08f;
        [SerializeField, Range(0.1f, 1f)] private float groundCheckWidthRatio = 0.9f;
        [SerializeField] private LayerMask groundMask = ~0;

        private readonly Collider2D[] groundHits = new Collider2D[8];

        private Rigidbody2D body;
        private BoxCollider2D bodyCollider;

        private float moveAxis;
        private bool jumpQueued;
        private int facing = 1;

        /// <summary>朝向变化：1 向右，-1 向左。表现层据此翻转。</summary>
        public event Action<int> FacingChanged;

        public bool IsGrounded { get; private set; }
        public int Facing => facing;
        public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<BoxCollider2D>();

            body.gravityScale = gravityScale;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void OnValidate()
        {
            if (body != null) body.gravityScale = gravityScale;
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();

            Vector2 velocity = body.velocity;
            velocity.x = moveAxis * moveSpeed * (IsGrounded ? 1f : airControl);

            if (jumpQueued)
            {
                jumpQueued = false;
                if (IsGrounded) velocity.y = jumpVelocity;
            }

            body.velocity = velocity;
        }

        /// <summary>每帧由输入层调用，范围 [-1, 1]。</summary>
        public void SetMoveAxis(float axis)
        {
            moveAxis = Mathf.Clamp(axis, -1f, 1f);
            if (Mathf.Abs(moveAxis) < 0.01f) return;

            int next = moveAxis > 0f ? 1 : -1;
            if (next == facing) return;

            facing = next;
            FacingChanged?.Invoke(facing);
        }

        /// <summary>请求跳跃；在下一个物理帧生效，落地状态下才执行。</summary>
        public void RequestJump() => jumpQueued = true;

        private bool CheckGrounded()
        {
            Bounds bounds = bodyCollider.bounds;
            Vector2 center = new Vector2(
                bounds.center.x,
                bounds.min.y + groundCheckHeight * 0.5f - 0.01f);
            Vector2 size = new Vector2(
                Mathf.Max(0.01f, bounds.size.x * groundCheckWidthRatio),
                groundCheckHeight);

            int count = Physics2D.OverlapBoxNonAlloc(center, size, 0f, groundHits, groundMask);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = groundHits[i];
                if (hit == null || hit.isTrigger) continue;
                if (hit.attachedRigidbody == body) continue;   // 排除自己
                return true;
            }

            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (bodyCollider == null) bodyCollider = GetComponent<BoxCollider2D>();
            if (bodyCollider == null) return;

            Bounds bounds = bodyCollider.bounds;
            Vector3 center = new Vector3(bounds.center.x, bounds.min.y + groundCheckHeight * 0.5f - 0.01f, 0f);
            Vector3 size = new Vector3(bounds.size.x * groundCheckWidthRatio, groundCheckHeight, 0f);

            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(center, size);
        }
    }
}
