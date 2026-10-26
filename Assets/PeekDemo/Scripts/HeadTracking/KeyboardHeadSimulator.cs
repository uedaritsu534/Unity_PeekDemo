using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace PeekDemo
{
    /// <summary>
    /// Fake head for testing without a webcam. Arrow keys lean left/right/up/down,
    /// Page Up/Down lean toward/away from the screen. Releasing a key returns to center.
    /// </summary>
    public class KeyboardHeadSimulator : HeadPoseSource
    {
        [Tooltip("Maximum simulated lean in physical meters.")]
        public Vector3 range = new Vector3(0.25f, 0.15f, 0.2f);
        [Tooltip("Meters per second.")]
        public float speed = 0.6f;

        Vector3 position;

        public override bool IsTracking => true;
        public override Vector3 RawPosition => position;
        public override string DisplayName => "Keyboard simulator";

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            var target = new Vector3(
                Axis(kb.leftArrowKey, kb.rightArrowKey) * range.x,
                Axis(kb.downArrowKey, kb.upArrowKey) * range.y,
                Axis(kb.pageDownKey, kb.pageUpKey) * range.z);
            position = Vector3.MoveTowards(position, target, speed * Time.deltaTime);
        }

        static float Axis(KeyControl negative, KeyControl positive) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);
    }
}
