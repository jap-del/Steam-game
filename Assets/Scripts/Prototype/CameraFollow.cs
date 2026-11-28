using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 대상을 부드럽게 따라가는 카메라. 흔들림은 따라가는 위치 위에 잠깐 얹었다가 걷어 낸다.
    /// 게임 시간이 멈춰 있어도(강화 고르기, 보스 등장) 움직이도록 실제 시간을 쓴다.
    /// 보스 등장 연출에서는 화면을 부드럽게 확대했다가 되돌린다(ZoomTo).
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector2 Offset = new Vector2(0f, 1.5f);
        public float Smoothing = 8f;
        public float MinX = -100f;
        public float MaxX = 100f;

        private Vector3 _position;
        private bool _hasPosition;
        private float _shakeAmplitude;
        private float _shakeDuration;
        private float _shakeRemaining;
        private Camera _camera;
        private float _baseSize = -1f;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private float _zoomSpeed = 2f;

        /// <summary>
        /// 화면 크기를 원래의 ratio배로 바꾼다. 1보다 작으면 가까이 다가간다. speed가 클수록 빨리 바뀐다.
        /// </summary>
        public void ZoomTo(float ratio, float speed)
        {
            _zoomTarget = Mathf.Clamp(ratio, 0.3f, 2f);
            _zoomSpeed = Mathf.Max(0.1f, speed);
        }

        /// <summary>방이 바뀌었다. 확대를 바로 풀고, 지금 카메라 크기를 원래 크기로 기억한다.</summary>
        public void ResetZoom(float baseSize)
        {
            _baseSize = baseSize;
            _zoom = 1f;
            _zoomTarget = 1f;
        }

        public void SnapToTarget()
        {
            if (Target == null) return;
            _position = Goal();
            _hasPosition = true;
            _shakeRemaining = 0f;
            transform.position = _position;
        }

        /// <summary>화면을 흔든다. 더 센 흔들림이 진행 중이면 그대로 둔다.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (_shakeRemaining > 0f && _shakeAmplitude * (_shakeRemaining / _shakeDuration) > amplitude) return;

            _shakeAmplitude = amplitude;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeRemaining = _shakeDuration;
        }

        private void LateUpdate()
        {
            if (Target == null) return;

            if (!_hasPosition)
            {
                _position = transform.position;
                _hasPosition = true;
            }

            float delta = Time.unscaledDeltaTime;
            UpdateZoom(delta);
            float t = 1f - Mathf.Exp(-Smoothing * delta);
            _position = Vector3.Lerp(_position, Goal(), t);

            Vector3 shake = Vector3.zero;
            if (_shakeRemaining > 0f)
            {
                _shakeRemaining -= delta;
                float strength = _shakeAmplitude * Mathf.Clamp01(_shakeRemaining / _shakeDuration);
                Vector2 offset = Random.insideUnitCircle * strength;
                shake = new Vector3(offset.x, offset.y, 0f);
            }
            transform.position = _position + shake;
        }

        private void UpdateZoom(float delta)
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera == null) return;
            if (_baseSize <= 0f) _baseSize = _camera.orthographicSize;

            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-_zoomSpeed * delta));
            if (Mathf.Abs(_zoom - _zoomTarget) < 0.0005f) _zoom = _zoomTarget;
            _camera.orthographicSize = _baseSize * _zoom;
        }

        private Vector3 Goal()
        {
            float x = Mathf.Clamp(Target.position.x + Offset.x, MinX, MaxX);
            return new Vector3(x, Target.position.y + Offset.y, transform.position.z);
        }
    }
}
