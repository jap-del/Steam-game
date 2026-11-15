using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>맞은 자리에서 튀는 작은 조각 하나. 떨어지면서 흐려지다가 사라진다.</summary>
    public sealed class Spark : MonoBehaviour
    {
        private const float Gravity = 18f;

        private SpriteRenderer _renderer;
        private Vector2 _velocity;
        private float _life;
        private float _age;

        public void Launch(SpriteRenderer renderer, Vector2 velocity, float life)
        {
            _renderer = renderer;
            _velocity = velocity;
            _life = Mathf.Max(0.05f, life);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life)
            {
                Destroy(gameObject);
                return;
            }

            _velocity.y -= Gravity * Time.deltaTime;
            transform.position += new Vector3(_velocity.x, _velocity.y, 0f) * Time.deltaTime;

            if (_renderer != null)
            {
                Color color = _renderer.color;
                color.a = 1f - _age / _life;
                _renderer.color = color;
            }
        }
    }
}
