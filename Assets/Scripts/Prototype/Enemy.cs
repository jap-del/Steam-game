using UnityEngine;

namespace SteamGame.Prototype
{
    public enum EnemyKind
    {
        /// <summary>움직이지 않는 연습용 허수아비. 체력이 다 닳으면 다시 찬다.</summary>
        Dummy = 0,
        /// <summary>다가와서 잠깐 번쩍인 뒤 돌진한다. 돌진을 피하면 빈틈이 생긴다.</summary>
        Brute = 1,
        /// <summary>제자리에서 번쩍인 뒤 탄을 쏜다.</summary>
        Shooter = 2,
        /// <summary>중간 보스. 돌진과 세 갈래 탄을 번갈아 쓴다.</summary>
        MidBoss = 3,
        /// <summary>보스. 돌진, 다섯 갈래 탄, 뛰어올라 내려찍기를 차례로 쓴다.</summary>
        Boss = 4,
    }

    /// <summary>
    /// 적. 모든 공격 전에 번쩍이는 예고 동작이 있어서 보고 피할 수 있다.
    /// 예고와 공격 중에는 맞아도 밀려나지 않으므로, 계속 때리기만 해서는 막을 수 없다.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        private enum State
        {
            Idle,
            Chase,
            Windup,
            Attack,
            Recover,
        }

        public float MaxHealth = 30f;
        public float MoveSpeed = 2.6f;

        private const float BruteNoticeRange = 10f;
        private const float BruteForgetRange = 18f;
        private const float BruteAttackRange = 2.4f;
        private const float BruteWindupTime = 0.45f;
        private const float BruteLungeTime = 0.28f;
        private const float BruteLungeSpeed = 11f;
        private const float BruteRecoverTime = 0.7f;

        private const float ShooterRange = 14f;
        private const float ShooterWindupTime = 0.5f;
        private const float ShooterInterval = 2.2f;
        private const float ProjectileSpeed = 7.5f;

        private const float BossNoticeRange = 18f;
        private const float BossWindupTime = 0.6f;
        private const float BossLungeTime = 0.4f;
        private const float BossLungeSpeed = 14f;
        private const float BossSlamAirTime = 0.8f;
        private const float BossGravityScale = 3.5f;
        private const float WaveSpeed = 9f;

        /// <summary>보스가 쓰는 공격 종류.</summary>
        private enum Pattern
        {
            Lunge,
            Spread,
            Slam,
        }

        private static readonly Color WarningColor = new Color(1f, 0.95f, 0.3f, 1f);

        private PrototypeGame _game;
        private PlayerController _player;
        private Rigidbody2D _body;
        private BoxCollider2D _collider;
        private SpriteRenderer _sprite;
        private Transform _healthFill;
        private Color _baseColor;
        private Color _flashColor;
        private Vector2 _size;
        private State _state = State.Idle;
        private float _stateTimer;
        private float _cooldown;
        private int _facing = 1;
        private float _health;
        private float _stunTimer;
        private float _flashTimer;
        private bool _dead;
        private Pattern _pattern;
        private int _patternIndex;
        private float _slamVelocityX;
        private float _airTime;
        private readonly float[] _lastHitTime = { -999f, -999f };

        public EnemyKind Kind { get; private set; }
        public Collider2D Collider => _collider;
        public bool IsBoss => Kind == EnemyKind.MidBoss || Kind == EnemyKind.Boss;
        public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);

        /// <summary>처치했을 때 주는 골드.</summary>
        public int GoldValue
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.MidBoss: return 30;
                    case EnemyKind.Boss: return 60;
                    case EnemyKind.Dummy: return 0;
                    default: return 5;
                }
            }
        }

        public static Vector2 SizeOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Dummy: return new Vector2(1f, 1.8f);
                case EnemyKind.Shooter: return new Vector2(0.8f, 1f);
                case EnemyKind.MidBoss: return new Vector2(1.5f, 2f);
                case EnemyKind.Boss: return new Vector2(2f, 2.6f);
                default: return new Vector2(0.9f, 1.2f);
            }
        }

        private static Color ColorOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Dummy: return new Color(0.8f, 0.65f, 0.35f, 1f);
                case EnemyKind.Shooter: return new Color(0.95f, 0.5f, 0.15f, 1f);
                case EnemyKind.MidBoss: return new Color(0.75f, 0.25f, 0.6f, 1f);
                case EnemyKind.Boss: return new Color(0.65f, 0.12f, 0.18f, 1f);
                default: return new Color(0.85f, 0.3f, 0.35f, 1f);
            }
        }

        public void Build(PrototypeGame game, PlayerController player, EnemyKind kind)
        {
            _game = game;
            _player = player;
            Kind = kind;
            _size = SizeOf(kind);
            _baseColor = ColorOf(kind);
            _health = MaxHealth;
            _cooldown = Random.Range(0.4f, 1.4f);

            _sprite = PrototypeArt.CreateBox("Body", transform, Vector2.zero, _size, _baseColor, 8);

            float barY = _size.y * 0.5f + 0.3f;
            PrototypeArt.CreateBox("HealthBack", transform, new Vector2(0f, barY), new Vector2(1.1f, 0.14f), new Color(0f, 0f, 0f, 0.7f), 13);
            _healthFill = PrototypeArt.CreateBox("HealthFill", transform, new Vector2(0f, barY), new Vector2(1f, 0.08f), new Color(0.9f, 0.25f, 0.25f, 1f), 14).transform;

            _collider = gameObject.AddComponent<BoxCollider2D>();
            _collider.size = _size;

            if (kind != EnemyKind.Dummy)
            {
                _body = gameObject.AddComponent<Rigidbody2D>();
                _body.gravityScale = BossGravityScale;
                _body.freezeRotation = true;
            }
        }

        private void Update()
        {
            Color color = _baseColor;
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                color = _flashColor;
            }
            else if (_state == State.Windup)
            {
                // 공격 예고: 빠르게 깜빡인다.
                color = Mathf.FloorToInt(Time.time * 14f) % 2 == 0 ? WarningColor : _baseColor;
            }
            else if (_state == State.Attack)
            {
                color = WarningColor;
            }
            else if (_state == State.Recover)
            {
                // 빈틈: 어둡게 보인다.
                color = new Color(_baseColor.r * 0.55f, _baseColor.g * 0.55f, _baseColor.b * 0.55f, 1f);
            }
            _sprite.color = color;
        }

        private void FixedUpdate()
        {
            if (Kind == EnemyKind.Dummy || _player == null || _dead) return;

            if (_body.position.y < StageBuilder.KillY)
            {
                Die();
                return;
            }

            bool stunned = _stunTimer > 0f;
            if (stunned) _stunTimer -= Time.fixedDeltaTime;

            Vector2 toPlayer = _player.Position - _body.position;
            float horizontal;
            if (IsBoss) horizontal = TickBoss(toPlayer);
            else if (Kind == EnemyKind.Brute) horizontal = TickBrute(toPlayer);
            else horizontal = TickShooter(toPlayer);

            if (!stunned)
            {
                _body.linearVelocity = new Vector2(horizontal, _body.linearVelocity.y);
            }
        }

        /// <summary>돌진형: 다가간다, 번쩍인다, 돌진한다, 잠깐 멈춘다.</summary>
        private float TickBrute(Vector2 toPlayer)
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            float distanceY = Mathf.Abs(toPlayer.y);

            switch (_state)
            {
                case State.Idle:
                    if (distanceX < BruteNoticeRange && distanceY < 3.5f) _state = State.Chase;
                    return 0f;

                case State.Chase:
                    _facing = toPlayer.x >= 0f ? 1 : -1;
                    if (distanceX > BruteForgetRange)
                    {
                        _state = State.Idle;
                        return 0f;
                    }
                    if (distanceX <= BruteAttackRange && distanceY < 1.6f)
                    {
                        Enter(State.Windup, BruteWindupTime);
                        _game.ShowPopup(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), "!", WarningColor);
                        return 0f;
                    }
                    return distanceX > 0.4f && HasGroundAhead() ? _facing * MoveSpeed : 0f;

                case State.Windup:
                    // 돌진 방향은 예고를 시작할 때 정해진다. 그래서 뒤로 넘어가면 피할 수 있다.
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) Enter(State.Attack, BruteLungeTime);
                    return 0f;

                case State.Attack:
                    if (_collider.bounds.Intersects(_player.Collider.bounds))
                    {
                        _player.TakeDamage(1, _body.position.x);
                    }
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) Enter(State.Recover, BruteRecoverTime);
                    return HasGroundAhead() ? _facing * BruteLungeSpeed : 0f;

                default:
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) _state = State.Chase;
                    return 0f;
            }
        }

        /// <summary>원거리형: 제자리에서 번쩍인 뒤 플레이어가 있던 쪽으로 탄을 쏜다.</summary>
        private float TickShooter(Vector2 toPlayer)
        {
            if (_state == State.Windup)
            {
                _stateTimer -= Time.fixedDeltaTime;
                if (_stateTimer <= 0f)
                {
                    Vector2 direction = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : new Vector2(_facing, 0f);
                    _game.SpawnProjectile(_body.position, direction * ProjectileSpeed, 0.35f);
                    _cooldown = ShooterInterval;
                    _state = State.Idle;
                }
                return 0f;
            }

            _facing = toPlayer.x >= 0f ? 1 : -1;
            if (Mathf.Abs(toPlayer.x) < ShooterRange && Mathf.Abs(toPlayer.y) < 7f)
            {
                _cooldown -= Time.fixedDeltaTime;
                if (_cooldown <= 0f)
                {
                    Enter(State.Windup, ShooterWindupTime);
                    _game.ShowPopup(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), "!", WarningColor);
                }
            }
            return 0f;
        }

        /// <summary>
        /// 보스: 거리를 좁히다가 정해진 순서대로 공격을 고른다.
        /// 모든 공격은 번쩍이는 예고 뒤에 나오고, 끝나면 빈틈이 생긴다.
        /// </summary>
        private float TickBoss(Vector2 toPlayer)
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            bool enraged = HealthRatio < 0.5f;

            switch (_state)
            {
                case State.Idle:
                    if (distanceX < BossNoticeRange)
                    {
                        _state = State.Chase;
                        _cooldown = 1f;
                    }
                    return 0f;

                case State.Chase:
                    _facing = toPlayer.x >= 0f ? 1 : -1;
                    _cooldown -= Time.fixedDeltaTime;
                    if (_cooldown <= 0f)
                    {
                        _pattern = NextPattern();
                        Enter(State.Windup, BossWindupTime);
                        _game.ShowPopup(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), PatternHint(_pattern), WarningColor);
                        return 0f;
                    }
                    return distanceX > 3f && HasGroundAhead() ? _facing * MoveSpeed : 0f;

                case State.Windup:
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer > 0f) return 0f;

                    if (_pattern == Pattern.Spread)
                    {
                        FireSpread(toPlayer, Kind == EnemyKind.Boss ? 5 : 3);
                        Enter(State.Recover, 0.9f);
                        return 0f;
                    }
                    if (_pattern == Pattern.Slam)
                    {
                        // 플레이어가 지금 있는 자리로 뛰어오른다.
                        float gravity = Mathf.Abs(Physics2D.gravity.y) * BossGravityScale;
                        _slamVelocityX = Mathf.Clamp(toPlayer.x / BossSlamAirTime, -12f, 12f);
                        _body.linearVelocity = new Vector2(_slamVelocityX, gravity * BossSlamAirTime * 0.5f);
                        _airTime = 0f;
                        Enter(State.Attack, 3f);
                        return _slamVelocityX;
                    }
                    Enter(State.Attack, BossLungeTime);
                    return 0f;

                case State.Attack:
                    if (_pattern == Pattern.Slam)
                    {
                        _airTime += Time.fixedDeltaTime;
                        _stateTimer -= Time.fixedDeltaTime;
                        bool landed = _airTime > 0.3f && _body.linearVelocity.y <= 0.1f && IsStandingOnGround();
                        if (landed || _stateTimer <= 0f)
                        {
                            Land();
                            Enter(State.Recover, enraged ? 0.9f : 1.2f);
                            return 0f;
                        }
                        return _slamVelocityX;
                    }

                    if (_collider.bounds.Intersects(_player.Collider.bounds))
                    {
                        _player.TakeDamage(1, _body.position.x);
                    }
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) Enter(State.Recover, enraged ? 0.8f : 1.1f);
                    return HasGroundAhead() ? _facing * BossLungeSpeed : 0f;

                default:
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f)
                    {
                        _state = State.Chase;
                        _cooldown = enraged ? 0.6f : 1.1f;
                    }
                    return 0f;
            }
        }

        private Pattern NextPattern()
        {
            Pattern[] cycle = Kind == EnemyKind.Boss
                ? new[] { Pattern.Lunge, Pattern.Spread, Pattern.Slam }
                : new[] { Pattern.Lunge, Pattern.Spread };
            Pattern pattern = cycle[_patternIndex % cycle.Length];
            _patternIndex++;
            return pattern;
        }

        private static string PatternHint(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Spread: return "탄!";
                case Pattern.Slam: return "내려찍기!";
                default: return "돌진!";
            }
        }

        /// <summary>플레이어 쪽을 중심으로 부채꼴로 탄을 쏜다.</summary>
        private void FireSpread(Vector2 toPlayer, int count)
        {
            Vector2 aim = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : new Vector2(_facing, 0f);
            float baseAngle = Mathf.Atan2(aim.y, aim.x);
            float step = 16f * Mathf.Deg2Rad;
            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + (i - (count - 1) * 0.5f) * step;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _game.SpawnProjectile(_body.position, direction * ProjectileSpeed, 0.35f);
            }
        }

        /// <summary>내려찍기 착지: 바로 옆에 있으면 피해를 주고, 바닥을 따라 양쪽으로 충격파를 보낸다.</summary>
        private void Land()
        {
            Bounds reach = _collider.bounds;
            reach.Expand(new Vector3(1.2f, 0.4f, 0f));
            if (reach.Intersects(_player.Collider.bounds))
            {
                _player.TakeDamage(1, _body.position.x);
            }

            Vector2 feet = _body.position + new Vector2(0f, -_size.y * 0.5f + 0.35f);
            _game.SpawnProjectile(feet + new Vector2(_size.x * 0.5f, 0f), new Vector2(WaveSpeed, 0f), 0.6f);
            _game.SpawnProjectile(feet - new Vector2(_size.x * 0.5f, 0f), new Vector2(-WaveSpeed, 0f), 0.6f);
            _game.ShowBlast(feet, _size.x * 0.5f + 0.8f, new Color(1f, 0.95f, 0.3f, 0.35f));
        }

        private bool IsStandingOnGround()
        {
            Vector2 below = _body.position + new Vector2(0f, -_size.y * 0.5f - 0.15f);
            return Combat.IsSolidAt(below);
        }

        private void Enter(State state, float duration)
        {
            _state = state;
            _stateTimer = duration;
        }

        /// <summary>발 앞쪽에 바닥이 있는지 본다. 없으면 낭떠러지라서 멈춘다.</summary>
        private bool HasGroundAhead()
        {
            Vector2 probe = _body.position + new Vector2(_facing * (_size.x * 0.5f + 0.35f), -_size.y * 0.5f - 0.3f);
            return Combat.IsSolidAt(probe);
        }

        public void TakeHit(float damage, HitSource source, Vector2 knockback)
        {
            if (_dead) return;

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
                _game.ShowPopup(popupPosition, "협공! " + Mathf.RoundToInt(damage), WarningColor);
                _flashColor = new Color(1f, 0.9f, 0.3f, 1f);
            }
            else
            {
                Color popupColor = source == HitSource.Shadow ? new Color(0.75f, 0.6f, 1f, 1f) : Color.white;
                _game.ShowPopup(popupPosition, Mathf.RoundToInt(damage).ToString(), popupColor);
                _flashColor = Color.white;
            }
            _flashTimer = 0.1f;

            // 예고와 공격 중에는 밀려나지 않는다. 보스는 언제나 밀려나지 않는다.
            bool armored = IsBoss || _state == State.Windup || _state == State.Attack;
            if (_body != null && !armored)
            {
                _body.linearVelocity = knockback;
                _stunTimer = 0.25f;
                if (_state == State.Idle && Kind == EnemyKind.Brute) _state = State.Chase;
            }

            if (_health <= 0f)
            {
                if (Kind == EnemyKind.Dummy)
                {
                    _health = MaxHealth;
                }
                else
                {
                    Die();
                    return;
                }
            }

            float ratio = Mathf.Clamp01(_health / MaxHealth);
            _healthFill.localScale = new Vector3(ratio, 0.08f, 1f);
            _healthFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, _healthFill.localPosition.y, 0f);
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            _game.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
