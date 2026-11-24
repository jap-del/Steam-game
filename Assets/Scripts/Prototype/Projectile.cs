using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 적이 쏘는 탄. 곧게 날아가며 벽에 닿으면 사라진다. 대시로 통과할 수 있다.
    /// 보스의 탄은 속도가 바뀌거나(되돌아오는 초승달), 멈춰 있다가 날아가기도 한다(멈춘 탄막).
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        private PlayerController _player;
        private Vector2 _velocity;
        private Vector2 _acceleration;
        private float _hold;
        private float _aimSpeed;
        private float _life = 5f;

        public void Launch(PlayerController player, Vector2 velocity)
        {
            _player = player;
            _velocity = velocity;
        }

        /// <summary>속도가 1초에 이만큼씩 바뀐다. 처음 방향의 반대로 주면 날아갔다가 되돌아온다.</summary>
        public void SetAcceleration(Vector2 acceleration)
        {
            _acceleration = acceleration;
        }

        /// <summary>
        /// seconds 동안 그 자리에 멈춰 있다가, 그때 플레이어가 있는 쪽으로 speed로 날아간다.
        /// 멈춰 있는 동안에는 닿아도 다치지 않는다.
        /// </summary>
        public void HoldThenAim(float seconds, float speed)
        {
            _hold = Mathf.Max(0.01f, seconds);
            _aimSpeed = speed;
            _velocity = Vector2.zero;
            _life = _hold + 5f;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_hold > 0f)
            {
                _hold -= Time.deltaTime;
                if (_hold > 0f) return;
                Vector2 toPlayer = _player != null ? _player.Position - (Vector2)transform.position : Vector2.down;
                _velocity = (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector2.down) * _aimSpeed;
            }

            _velocity += _acceleration * Time.deltaTime;
            Vector2 position = (Vector2)transform.position + _velocity * Time.deltaTime;
            transform.position = new Vector3(position.x, position.y, 0f);

            if (_player != null && _player.Collider.OverlapPoint(position))
            {
                // 대시 중이거나 방금 맞아서 무적이면 그냥 지나간다.
                if (_player.TakeDamage(1, position.x - _velocity.x))
                {
                    Destroy(gameObject);
                }
                return;
            }

            if (Combat.IsSolidAt(position))
            {
                Destroy(gameObject);
            }
        }
    }
}
