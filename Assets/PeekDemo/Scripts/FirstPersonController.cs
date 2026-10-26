using UnityEngine;
using UnityEngine.InputSystem;

namespace PeekDemo
{
    /// <summary>
    /// Plain WASD + mouse-look controller. Yaw rotates this transform, pitch rotates <see cref="pitchPivot"/>.
    /// Head tracking is layered on top by <see cref="OffAxisWindowCamera"/> on a child of the pivot.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        public Transform pitchPivot;
        public float walkSpeed = 4f;
        public float sprintSpeed = 7f;
        public float jumpHeight = 1f;
        public float gravity = -20f;
        [Tooltip("Degrees per pixel of mouse movement.")]
        public float mouseSensitivity = 0.08f;
        public float maxPitch = 85f;

        CharacterController controller;
        float pitch;
        float verticalVelocity;

        void Awake() => controller = GetComponent<CharacterController>();

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked(true);
            if (kb.escapeKey.wasPressedThisFrame) SetCursorLocked(false);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
                if (pitchPivot) pitchPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            var input = new Vector2(
                (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
                (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
            input = Vector2.ClampMagnitude(input, 1f);

            float speed = kb.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (kb.spaceKey.wasPressedThisFrame) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            verticalVelocity += gravity * Time.deltaTime;
            move.y = verticalVelocity;

            controller.Move(move * Time.deltaTime);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
