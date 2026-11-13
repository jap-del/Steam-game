using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>원거리 적이 쏘는 탄. 곧게 날아가며 벽에 닿으면 사라진다. 대시로 통과할 수 있다.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        private PlayerController _player;
        private Vector2 _velocity;
        private float _life = 5f;

        public void Launch(PlayerController player, Vector2 velocity)
        {
            _player = player;
            _velocity = velocity;
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }

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
