using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 보스 공격에 쓰는 사각형 범위. 먼저 흐리게 깜빡이며 자리를 보여 주고(예고), 잠깐 밝게 켜지며(발동)
    /// 그 안에 있는 플레이어를 한 번 다치게 한다. 대시 중이거나 방금 맞아 무적이면 그냥 지나가고,
    /// 발동이 끝날 때까지 다시 판정한다. 발동한 뒤 옆으로 움직이게 할 수도 있다(진자 칼날).
    /// 피해가 0이면 길만 보여 주는 예고 표시다.
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        private PlayerController _player;
        private SpriteRenderer _renderer;
        private Color _color;
        private Vector2 _size;
        private Vector2 _velocity;
        private float _warn;
        private float _active;
        private float _activeTime;
        private int _damage;
        private bool _loud;
        private bool _unavoidable;
        private bool _fired;
        private bool _hitDone;

        /// <summary>
        /// center를 가운데로 size 크기의 범위를 만든다. warnTime 동안 예고하고 activeTime 동안 발동한다.
        /// velocity를 주면 발동한 뒤 그 속도로 움직인다. 방이 없으면 null.
        /// </summary>
        public static Hazard Spawn(Vector2 center, Vector2 size, float warnTime, float activeTime, Color color, int damage = 1, Vector2 velocity = default(Vector2), bool loud = false, bool unavoidable = false)
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game == null || game.RoomRoot == null) return null;

            Color warn = color;
            warn.a = 0.18f;
            SpriteRenderer renderer = PrototypeArt.CreateBox("Hazard", game.RoomRoot, center, size, warn, 4);
            var hazard = renderer.gameObject.AddComponent<Hazard>();
            hazard._player = game.Player;
            hazard._renderer = renderer;
            hazard._color = color;
            hazard._size = size;
            hazard._velocity = velocity;
            hazard._warn = warnTime;
            hazard._activeTime = Mathf.Max(0.01f, activeTime);
            hazard._damage = damage;
            hazard._loud = loud;
            hazard._unavoidable = unavoidable;
            return hazard;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!_fired)
            {
                _warn -= dt;
                // 발동하기 직전에는 더 진하게 깜빡인다.
                bool late = _warn < 0.25f && Mathf.FloorToInt(Time.time * 16f) % 2 == 0;
                Color warn = _color;
                warn.a = late ? 0.4f : 0.18f;
                _renderer.color = warn;
                if (_warn > 0f) return;

                if (_damage <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
                _fired = true;
                _active = _activeTime;
                if (_loud) Sfx.Play(Sound.Beam, 0.6f);
            }

            if (_velocity != Vector2.zero)
            {
                transform.position += new Vector3(_velocity.x, _velocity.y, 0f) * dt;
            }

            if (!_hitDone && _player != null && Overlaps(_player.Position))
            {
                if (_player.TakeDamage(_damage, transform.position.x, _unavoidable)) _hitDone = true;
            }

            _active -= dt;
            Color hot = Color.Lerp(_color, Color.white, 0.3f);
            hot.a = 0.3f + 0.6f * Mathf.Clamp01(_active / _activeTime);
            _renderer.color = hot;
            if (_active <= 0f) Destroy(gameObject);
        }

        private bool Overlaps(Vector2 playerCenter)
        {
            Vector2 half = (_size + PlayerController.BodySize) * 0.5f;
            Vector2 offset = playerCenter - (Vector2)transform.position;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
        }
    }

    /// <summary>천장에서 떨어지는 바위 그림. 부딪힘은 따로 만든 Hazard가 맡는다. 바닥에 닿으면 부서진다.</summary>
    public sealed class FallingRock : MonoBehaviour
    {
        private static readonly Color RockColor = new Color(0.55f, 0.5f, 0.44f, 1f);

        private float _x;
        private float _fromY;
        private float _toY;
        private float _time;
        private float _elapsed;

        public static void Spawn(Transform parent, float x, float fromY, float toY, float time)
        {
            SpriteRenderer renderer = PrototypeArt.CreateBox("FallingRock", parent, new Vector2(x, fromY), new Vector2(1.1f, 0.9f), RockColor, 14);
            var rock = renderer.gameObject.AddComponent<FallingRock>();
            rock._x = x;
            rock._fromY = fromY;
            rock._toY = toY;
            rock._time = Mathf.Max(0.05f, time);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _time);
            // 점점 빨라지며 떨어진다.
            float y = Mathf.Lerp(_fromY, _toY, t * t);
            transform.position = new Vector3(_x, y, 0f);
            if (t < 1f) return;

            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null) game.Feel.Sparks(transform.position, RockColor, 6, 5f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 철갑을 두른 성채의 문지기가 떨어뜨린 바위가 바닥에 남은 것. 몸으로 길을 막지 않는다.
    /// 문지기가 돌진하다 부딪히면 부서진다. 이 판정은 문지기가 한다(EnemyCitadel.cs).
    /// </summary>
    public sealed class Boulder : MonoBehaviour
    {
        public const float HalfWidth = 0.7f;
        public const float Height = 1.1f;

        private static readonly Color BodyColor = new Color(0.5f, 0.46f, 0.4f, 1f);
        private static readonly Color TopColor = new Color(0.66f, 0.62f, 0.55f, 1f);

        private SpriteRenderer _body;
        private float _life;

        public static Boulder Create(Transform parent, float x, float groundTop, float life)
        {
            var go = new GameObject("Boulder");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, groundTop + Height * 0.5f, 0f);
            var boulder = go.AddComponent<Boulder>();
            boulder._body = PrototypeArt.CreateBox("Body", go.transform, Vector2.zero, new Vector2(HalfWidth * 2f, Height), BodyColor, 6);
            PrototypeArt.CreateBox("Top", go.transform, new Vector2(0f, Height * 0.5f - 0.12f), new Vector2(HalfWidth * 1.6f, 0.24f), TopColor, 7);
            boulder._life = life;
            return boulder;
        }

        /// <summary>문지기의 돌진에 부서진다.</summary>
        public void Shatter()
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null) game.Feel.Sparks(transform.position, TopColor, 14, 7f);
            Sfx.Play(Sound.Shatter, 0.7f);
            Destroy(gameObject);
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            // 사라지기 전에 깜빡인다.
            _body.enabled = _life > 2f || Mathf.FloorToInt(Time.time * 8f) % 2 == 0;
        }
    }
}
