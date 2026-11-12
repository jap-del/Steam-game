using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 연습용 적. 플레이어를 향해 걸어와 닿으면 피해를 준다.
    /// IsDummy가 켜져 있으면 움직이지 않는 허수아비로, 체력이 다 닳으면 다시 채워진다.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        public float MaxHealth = 30f;
        public float MoveSpeed = 2.4f;
        public float JumpSpeed = 13.5f;
        public bool IsDummy;

        private PrototypeGame _game;
        private PlayerController _player;
        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private SpriteRenderer _sprite;
        private Transform _healthFill;
        private Color _baseColor;
        private Vector2 _size;
        private float _health;
        private float _stunTimer;
        private float _flashTimer;
        private float _jumpTimer;
        private readonly float[] _lastHitTime = { -999f, -999f };

        public Collider2D Collider => _collider;

        public void Build(PrototypeGame game, PlayerController player, Vector2 size, Color color, bool isDummy)
        {
            _game = game;
            _player = player;
            _size = size;
            _baseColor = color;
            IsDummy = isDummy;
            _health = MaxHealth;

            _sprite = PrototypeArt.CreateBox("Body", transform, Vector2.zero, size, color, 8);

            float barY = size.y * 0.5f + 0.3f;
            PrototypeArt.CreateBox("HealthBack", transform, new Vector2(0f, barY), new Vector2(1.1f, 0.14f), new Color(0f, 0f, 0f, 0.7f), 13);
            _healthFill = PrototypeArt.CreateBox("HealthFill", transform, new Vector2(0f, barY), new Vector2(1f, 0.08f), new Color(0.9f, 0.25f, 0.25f, 1f), 14).transform;

            _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = size;

            if (!isDummy)
            {
                _body = gameObject.AddComponent<Rigidbody2D>();
                _body.gravityScale = 3.5f;
                _body.freezeRotation = true;
            }
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) _sprite.color = _baseColor;
            }
        }

        private void FixedUpdate()
        {
            if (IsDummy || _player == null) return;
            if (_jumpTimer > 0f) _jumpTimer -= Time.fixedDeltaTime;

            if (_stunTimer > 0f)
            {
                _stunTimer -= Time.fixedDeltaTime;
            }
            else
            {
                float dx = _player.Position.x - _body.position.x;
                float speed = Mathf.Abs(dx) > 0.3f ? Mathf.Sign(dx) * MoveSpeed : 0f;
                float vertical = _body.linearVelocity.y;

                // 플레이어가 가까운 위쪽에 있으면 뛰어오른다. 발판 위에 숨을 수 없게 하기 위함이다.
                bool playerAbove = _player.Position.y > _body.position.y + 1f;
                if (playerAbove && Mathf.Abs(dx) < 4f && Mathf.Abs(vertical) < 0.01f && _jumpTimer <= 0f)
                {
                    vertical = JumpSpeed;
                    _jumpTimer = 1.2f;
                }
                _body.linearVelocity = new Vector2(speed, vertical);
            }

            if (_collider.bounds.Intersects(_player.Collider.bounds))
            {
                _player.TakeDamage(1, _body.position.x);
            }
        }

        public void TakeHit(float damage, HitSource source, Vector2 knockback)
        {
            if (_health <= 0f) return;

            int self = (int)source;
            int other = 1 - self;
            // 본체와 분신의 공격이 정해진 시간 안에 겹치면 협공이다.
            bool pincer = Time.time - _lastHitTime[other] <= _game.Stats.PincerWindow;
            _lastHitTime[self] = Time.time;

            if (pincer) damage *= _game.Stats.PincerMultiplier;
            _health -= damage;

            Vector2 popupPosition = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 0.6f);
            if (pincer)
            {
                _game.CountPincer();
                _game.ShowPopup(popupPosition, "협공! " + Mathf.RoundToInt(damage), new Color(1f, 0.85f, 0.2f, 1f));
                _sprite.color = new Color(1f, 0.9f, 0.3f, 1f);
            }
            else
            {
                Color popupColor = source == HitSource.Shadow ? new Color(0.75f, 0.6f, 1f, 1f) : Color.white;
                _game.ShowPopup(popupPosition, Mathf.RoundToInt(damage).ToString(), popupColor);
                _sprite.color = Color.white;
            }
            _flashTimer = 0.1f;

            if (_body != null)
            {
                _body.linearVelocity = knockback;
                _stunTimer = 0.25f;
            }

            if (_health <= 0f)
            {
                if (IsDummy)
                {
                    _health = MaxHealth;
                }
                else
                {
                    _game.OnEnemyDied(this);
                    Destroy(gameObject);
                    return;
                }
            }

            float ratio = Mathf.Clamp01(_health / MaxHealth);
            _healthFill.localScale = new Vector3(ratio, 0.08f, 1f);
            _healthFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, _healthFill.localPosition.y, 0f);
        }
    }
}
