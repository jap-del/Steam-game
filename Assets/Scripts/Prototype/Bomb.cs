using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 폭탄병이 던지는 폭탄. 포물선으로 날아가 정해진 자리에 떨어지면 터진다.
    /// 떨어질 자리는 날아가는 동안 붉게 표시되므로 보고 벗어나면 된다. 대시 중에는 맞지 않는다.
    /// </summary>
    public sealed class Bomb : MonoBehaviour
    {
        private const float ArcHeight = 3f;

        private PrototypeGame _game;
        private PlayerController _player;
        private GameObject _marker;
        private Vector2 _from;
        private Vector2 _to;
        private float _flightTime;
        private float _radius;
        private float _elapsed;

        public void Launch(PrototypeGame game, PlayerController player, GameObject marker, Vector2 from, Vector2 to, float flightTime, float radius)
        {
            _game = game;
            _player = player;
            _marker = marker;
            _from = from;
            _to = to;
            _flightTime = Mathf.Max(0.1f, flightTime);
            _radius = radius;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _flightTime);
            Vector2 position = Vector2.Lerp(_from, _to, t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * ArcHeight);
            transform.position = new Vector3(position.x, position.y, 0f);

            if (t < 1f) return;

            if (_player != null && (_player.Position - _to).magnitude <= _radius + 0.4f)
            {
                _player.TakeDamage(1, _to.x);
            }
            _game.ShowBlast(_to, _radius, new Color(1f, 0.55f, 0.2f, 0.55f));
            _game.Feel.Explosion(_to);

            if (_marker != null) Destroy(_marker);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // 방이 바뀌어 폭탄이 사라질 때 표시도 함께 지운다.
            if (_marker != null) Destroy(_marker);
        }
    }
}
