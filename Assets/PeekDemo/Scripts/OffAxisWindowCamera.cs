using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PeekDemo
{
    /// <summary>
    /// Turns the monitor into a window. The parent transform is the neutral eye (it gets mouse look);
    /// a virtual screen rectangle sits a fixed distance in front of it. Each frame the camera is moved
    /// to the tracked eye position and given an asymmetric frustum through that rectangle, so the
    /// image shifts the way the view through a real window would.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OffAxisWindowCamera : MonoBehaviour
    {
        [Tooltip("Preferred source (e.g. OpenTrack). Used whenever it is tracking.")]
        public HeadPoseSource primarySource;
        [Tooltip("Used when the primary source has no data (e.g. keyboard simulator).")]
        public HeadPoseSource fallbackSource;

        [Header("Physical setup")]
        [Tooltip("Height of the visible image on your monitor, in meters. 24\" 16:9 ≈ 0.30, 27\" ≈ 0.34.")]
        public float screenHeightMeters = 0.30f;
        [Tooltip("Distance from your eyes to the screen when sitting normally, in meters.")]
        public float viewingDistanceMeters = 0.60f;

        [Header("Feel")]
        [Tooltip("Vertical FOV at the neutral head position. 0 = physically correct (usually ~28°, feels zoomed in).")]
        [Range(0f, 100f)] public float neutralVerticalFov = 60f;
        [Tooltip("1 = the view angle changes exactly as through a real window of your monitor's size. Higher exaggerates peeking.")]
        public float parallaxGain = 2.5f;
        [Tooltip("Clamp on physical head offset from the recentered position, in meters.")]
        public Vector3 maxHeadOffset = new Vector3(0.30f, 0.20f, 0.25f);
        [Tooltip("Higher = snappier, lower = smoother (1/seconds).")]
        public float smoothing = 18f;
        [Tooltip("Stops the eye from being pushed through walls when leaning. 0 disables.")]
        public float eyeCollisionRadius = 0.08f;
        public LayerMask eyeCollisionMask = Physics.DefaultRaycastLayers;

        [Header("Controls")]
        public Key recenterKey = Key.C;
        public Key toggleKey = Key.H;
        public bool showHud = true;

        public bool HeadTrackingEnabled { get; set; } = true;
        public Vector3 HeadOffset => smoothedOffset;

        Camera cam;
        HeadPoseSource active;
        readonly Dictionary<HeadPoseSource, Vector3> neutrals = new Dictionary<HeadPoseSource, Vector3>();
        Vector3 smoothedOffset;

        void Awake() => cam = GetComponent<Camera>();

        void OnDisable()
        {
            if (cam) cam.ResetProjectionMatrix();
            transform.localPosition = Vector3.zero;
        }

        /// <summary>Treat the current head position as "sitting normally".</summary>
        public void Recenter()
        {
            if (active) neutrals[active] = active.RawPosition;
        }

        // LateUpdate so mouse look and movement (Update) have already placed the parent this frame.
        void LateUpdate()
        {
            active = primarySource && primarySource.IsTracking ? primarySource
                   : fallbackSource && fallbackSource.IsTracking ? fallbackSource
                   : null;
            // First time a source delivers data, its current position becomes neutral.
            if (active && !neutrals.ContainsKey(active)) Recenter();

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[recenterKey].wasPressedThisFrame) Recenter();
                if (kb[toggleKey].wasPressedThisFrame) HeadTrackingEnabled = !HeadTrackingEnabled;
            }

            Vector3 target = Vector3.zero;
            if (HeadTrackingEnabled && active)
            {
                target = active.RawPosition - neutrals[active];
                target.x = Mathf.Clamp(target.x, -maxHeadOffset.x, maxHeadOffset.x);
                target.y = Mathf.Clamp(target.y, -maxHeadOffset.y, maxHeadOffset.y);
                target.z = Mathf.Clamp(target.z, -maxHeadOffset.z, maxHeadOffset.z);
            }
            smoothedOffset = Vector3.Lerp(smoothedOffset, target, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            ApplyWindowProjection();
        }

        void ApplyWindowProjection()
        {
            float halfH = screenHeightMeters * 0.5f;
            float halfW = halfH * cam.aspect;
            float screenDist = neutralVerticalFov > 0.01f
                ? halfH / Mathf.Tan(neutralVerticalFov * 0.5f * Mathf.Deg2Rad)
                : viewingDistanceMeters;

            // The virtual screen may be closer than the real one (wider FOV), so scale head motion by the same
            // ratio to keep the view angle through it matching the real monitor; then exaggerate by the gain.
            Vector3 eye = smoothedOffset * (parallaxGain * screenDist / viewingDistanceMeters);
            eye.z = Mathf.Min(eye.z, screenDist - 0.02f);
            eye = ResolveEyeCollision(eye);

            transform.localPosition = eye;
            transform.localRotation = Quaternion.identity;

            // Screen rectangle is centered at (0, 0, screenDist) in parent space; project its edges onto the near plane.
            float near = cam.nearClipPlane;
            float k = near / (screenDist - eye.z);
            cam.projectionMatrix = Matrix4x4.Frustum(
                (-halfW - eye.x) * k, (halfW - eye.x) * k,
                (-halfH - eye.y) * k, (halfH - eye.y) * k,
                near, cam.farClipPlane);
        }

        Vector3 ResolveEyeCollision(Vector3 eye)
        {
            var parent = transform.parent;
            if (!parent || eyeCollisionRadius <= 0f) return eye;

            Vector3 origin = parent.position;
            Vector3 delta = parent.TransformPoint(eye) - origin;
            float length = delta.magnitude;
            if (length < 1e-4f) return eye;

            Vector3 dir = delta / length;
            if (Physics.SphereCast(origin, eyeCollisionRadius, dir, out var hit, length, eyeCollisionMask, QueryTriggerInteraction.Ignore))
                return parent.InverseTransformPoint(origin + dir * hit.distance);
            return eye;
        }

        void OnGUI()
        {
            if (!showHud) return;
            string source = active ? active.DisplayName : "none";
            string state = HeadTrackingEnabled ? "on" : "off";
            Vector3 cm = smoothedOffset * 100f;
            GUI.Label(new Rect(10, 10, 700, 80),
                $"Head: {source}   tracking {state}   offset (cm) x {cm.x:F1}  y {cm.y:F1}  z {cm.z:F1}\n" +
                $"[{recenterKey}] recenter   [{toggleKey}] toggle head tracking   arrows / PgUp / PgDn = simulate head\n" +
                "WASD move   mouse look   Shift sprint   Space jump   click to lock cursor, Esc to release");
        }
    }
}
