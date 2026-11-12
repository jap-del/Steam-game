using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>대상을 부드럽게 따라가는 카메라.</summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector2 Offset = new Vector2(0f, 1.5f);
        public float Smoothing = 8f;
        public float MinX = -100f;
        public float MaxX = 100f;

        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = Goal();
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            float t = 1f - Mathf.Exp(-Smoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, Goal(), t);
        }

        private Vector3 Goal()
        {
            float x = Mathf.Clamp(Target.position.x + Offset.x, MinX, MaxX);
            return new Vector3(x, Target.position.y + Offset.y, transform.position.z);
        }
    }
}
