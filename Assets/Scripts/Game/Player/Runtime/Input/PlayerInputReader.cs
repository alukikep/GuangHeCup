using Game.Player.Movement;
using ModuleFactory.KeyboardInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player.Input
{
    /// <summary>
    /// 读取 KeyboardInput 的 Gameplay 映射：
    /// Move.x 是左右（A/D），Move.y 的上升沿临时当跳跃（W）。
    /// 接真实控件时把跳跃换成独立的 Jump 按钮即可，调用方不用改。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("KeyboardActions.inputactions 模板，KeyboardActionSet 会克隆一份自用。")]
        [SerializeField] private InputActionAsset actionsTemplate;

        [SerializeField] private string moveActionName = "Move";

        [Tooltip("Move.y 超过该值视为跳跃按下。")]
        [SerializeField, Range(0.1f, 0.9f)] private float jumpAxisThreshold = 0.5f;

        private KeyboardActionSet actions;
        private KeyboardActionSet.KeyboardAction move;
        private PlayerMotor motor;
        private bool jumpHeld;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
        }

        private void OnEnable()
        {
            if (actionsTemplate == null)
            {
                Debug.LogError("[PlayerInputReader] 未指定 InputActionAsset，移动输入不会生效。", this);
                return;
            }

            actions = new KeyboardActionSet(actionsTemplate);
            move = actions.GetGameplayAction(moveActionName);
        }

        private void OnDisable()
        {
            actions?.Dispose();
            actions = null;
            move = null;
            jumpHeld = false;
        }

        private void Update()
        {
            if (move == null) return;

            Vector2 value = move.ReadValue<Vector2>();
            motor.SetMoveAxis(value.x);

            bool jumpDown = value.y >= jumpAxisThreshold;
            if (jumpDown && !jumpHeld) motor.RequestJump();
            jumpHeld = jumpDown;
        }
    }
}
