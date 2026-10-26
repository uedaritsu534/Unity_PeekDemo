using UnityEngine;

namespace PeekDemo
{
    /// <summary>
    /// Supplies the viewer's head position in physical meters, in screen space as the viewer sees it:
    /// +x = right, +y = up, +z = toward the screen. Absolute origin doesn't matter; the camera recenters.
    /// </summary>
    public abstract class HeadPoseSource : MonoBehaviour
    {
        public abstract bool IsTracking { get; }
        public abstract Vector3 RawPosition { get; }
        public virtual string DisplayName => GetType().Name;
    }
}
