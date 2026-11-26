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
        /// <summary>1스테이지 중간 보스 성채의 문지기. 돌진, 땅울림, 낙석을 쓰고 체력 60%부터 철갑을 두른다.</summary>
        MidBoss = 3,
        /// <summary>1스테이지 보스 봉인의 수호자. 봉인 파동, 사슬, 봉인진을 쓰고 체력 60%부터 봉인석의 순서를 풀어야 한다.</summary>
        Boss = 4,
        /// <summary>방패병. 돌진형처럼 싸우지만 앞에서 때리면 막는다. 뒤를 치거나 빈틈을 노려야 한다.</summary>
        Shield = 5,
        /// <summary>폭탄병. 내가 있는 자리로 폭탄을 던진다. 떨어질 자리가 미리 표시된다.</summary>
        Bomber = 6,
        /// <summary>날아다니는 적. 지형을 무시하고 위에서 맴돌다가 번쩍인 뒤 내리꽂는다.</summary>
        Flyer = 7,
        /// <summary>2스테이지 중간 보스인 쌍둥이 중 해. 불꽃을 남기며 달리고 뛰어올라 덮친다. 내 공격만 받는다.</summary>
        TwinSun = 8,
        /// <summary>2스테이지 중간 보스인 쌍둥이 중 달. 걷지 않고 순간이동하며 초승달과 폭탄을 던진다. 분신의 공격만 받는다.</summary>
        TwinMoon = 9,
        /// <summary>2스테이지 보스 백광의 군주. 세 단계로 싸우고, 단계마다 풀어야 할 기믹이 있다.</summary>
        LightLord = 10,
        /// <summary>3스테이지 중간 보스 태엽 기사. 순간 베기, 진자, 초침을 쓰고 플레이어 둘레에 시계 감옥을 세운다.</summary>
        ClockKnight = 11,
        /// <summary>3스테이지 보스이자 마지막 보스인 시계탑의 주인. 등 뒤만 뚫리는 시계 방패와 체력을 되돌리는 역행을 쓴다.</summary>
        ClockMaster = 12,
    }

    /// <summary>
    /// 적. 모든 공격 전에 번쩍이는 예고 동작이 있어서 보고 피할 수 있다.
    /// 예고와 공격 중에는 맞아도 밀려나지 않으므로, 계속 때리기만 해서는 막을 수 없다.
    /// 보스의 움직임과 공격 고르기는 EnemyBoss.cs에, 기믹이 함께 쓰는 것은 EnemyRaid.cs에,
    /// 보스마다의 공격과 기믹은 EnemyCitadel.cs(1스테이지), EnemySanctum.cs(2스테이지), EnemyClock.cs(3스테이지)에 있다.
    /// </summary>
    public sealed partial class Enemy : MonoBehaviour
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
        private const float BruteHopSpeed = 13.5f;
        private const float BruteGapSpeed = 7.5f;
        private const float BruteLeapWindupTime = 0.35f;

        private const float ShooterRange = 14f;
        private const float ShooterWindupTime = 0.5f;
        private const float ShooterInterval = 2.2f;
        private const float ProjectileSpeed = 7.5f;

        private const float ShieldTurnTime = 0.6f;

        private const float BomberWindupTime = 0.6f;
        private const float BomberInterval = 2.8f;
        private const float BombFlightTime = 0.9f;
        private const float BombRadius = 1.5f;

        private const float FlyerNoticeRange = 11f;
        private const float FlyerHoverSpeed = 4f;
        private const float FlyerWindupTime = 0.5f;
        private const float FlyerDiveSpeed = 13f;
        private const float FlyerDiveTime = 0.55f;
        private const float FlyerRecoverTime = 0.8f;
        private const float FlyerInterval = 1.6f;

        private const float BossGravityScale = 3.5f;

        private static readonly Color WarningColor = new Color(1f, 0.95f, 0.3f, 1f);
        private static readonly Color BossCalloutColor = new Color(1f, 0.52f, 0.34f, 1f);

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
        private float _jumpCooldown;
        private float _gapTimer;
        private int _gapDirection = 1;
        private float _stuckTimer;
        private float _lastX;
        private float _turnTimer;
        private Vector2 _diveDirection;
        private float _hoverSide = 1f;
        private float _diveFloorY;
        private SpriteRenderer _shieldVisual;
        private readonly float[] _lastHitTime = { -999f, -999f };

        public EnemyKind Kind { get; private set; }
        public Collider2D Collider => _collider;
        public bool IsBoss => Kind == EnemyKind.MidBoss || Kind == EnemyKind.Boss || Kind == EnemyKind.TwinSun || Kind == EnemyKind.TwinMoon || Kind == EnemyKind.LightLord || Kind == EnemyKind.ClockKnight || Kind == EnemyKind.ClockMaster;
        /// <summary>스테이지 끝의 보스인지. 중간 보스가 아니다.</summary>
        public bool IsFinalBoss => Kind == EnemyKind.Boss || Kind == EnemyKind.LightLord || Kind == EnemyKind.ClockMaster;
        public float HealthRatio => Mathf.Clamp01(_health / MaxHealth);
        /// <summary>몸의 기본 색. 조각이 튈 때 이 색을 쓴다.</summary>
        public Color BaseColor => _baseColor;

        /// <summary>처치했을 때 주는 골드.</summary>
        public int GoldValue
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.MidBoss: return 30;
                    case EnemyKind.Boss: return 60;
                    case EnemyKind.TwinSun:
                    case EnemyKind.TwinMoon: return 20;
                    case EnemyKind.LightLord: return 80;
                    case EnemyKind.ClockKnight: return 40;
                    case EnemyKind.ClockMaster: return 100;
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
                case EnemyKind.Shield: return new Vector2(1f, 1.4f);
                case EnemyKind.Bomber: return new Vector2(0.9f, 1f);
                case EnemyKind.Flyer: return new Vector2(0.9f, 0.6f);
                case EnemyKind.TwinSun: return new Vector2(1.4f, 2f);
                case EnemyKind.TwinMoon: return new Vector2(1.3f, 1.9f);
                case EnemyKind.LightLord: return new Vector2(2.2f, 3f);
                case EnemyKind.ClockKnight: return new Vector2(1.6f, 2.2f);
                case EnemyKind.ClockMaster: return new Vector2(2.4f, 3.2f);
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
                case EnemyKind.Shield: return new Color(0.45f, 0.6f, 0.8f, 1f);
                case EnemyKind.Bomber: return new Color(0.5f, 0.7f, 0.3f, 1f);
                case EnemyKind.Flyer: return new Color(0.85f, 0.4f, 0.8f, 1f);
                case EnemyKind.TwinSun: return new Color(0.95f, 0.7f, 0.25f, 1f);
                case EnemyKind.TwinMoon: return new Color(0.55f, 0.5f, 0.95f, 1f);
                case EnemyKind.LightLord: return new Color(0.9f, 0.8f, 0.5f, 1f);
                case EnemyKind.ClockKnight: return new Color(0.55f, 0.6f, 0.68f, 1f);
                case EnemyKind.ClockMaster: return new Color(0.62f, 0.5f, 0.36f, 1f);
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
            // 움직이는 그림 판이 있으면 그것을, 없으면 예전 픽셀 그림을 쓴다.
            _rig = PixelRig.Attach(_sprite, kind.ToString(), _size);
            if (_rig == null) PixelSprites.Dress(_sprite, kind.ToString(), _size);

            // 새 보스 그림은 뿔, 후광, 모자가 몸보다 위로 솟아 있어서 체력 막대를 조금 더 올린다.
            float barY = _size.y * 0.5f + (_rig != null && IsBoss ? 0.7f : 0.3f);
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

            if (kind == EnemyKind.Flyer)
            {
                // 날아다니는 적은 중력을 받지 않고 지형을 통과한다. 공격에는 그대로 맞는다.
                _body.gravityScale = 0f;
                _collider.isTrigger = true;
            }

            if (kind == EnemyKind.Shield)
            {
                // 방패가 어느 쪽을 막고 있는지 보여 주는 밝은 막대.
                _shieldVisual = PrototypeArt.CreateBox("Shield", transform, Vector2.zero, new Vector2(0.16f, _size.y * 0.9f), new Color(0.85f, 0.95f, 1f, 1f), 9);
                if (_rig != null)
                {
                    // 새 그림에는 방패가 그려져 있으므로, 막는 쪽을 알리는 막대는 얇고 은은한 빛으로 둔다.
                    _shieldVisual.transform.localScale = new Vector3(0.08f, _size.y * 0.8f, 1f);
                    _shieldVisual.color = new Color(0.8f, 0.95f, 1f, 0.45f);
                }
            }

            BuildRaid();
        }

        /// <summary>
        /// 방패병이 지금 그쪽을 막고 있는지. 공격 뒤 빈틈에는 방패를 내린다.
        /// 시계 방패를 든 시계탑의 주인은 빈틈에도 막고, 무너졌을 때만 내린다.
        /// </summary>
        private bool IsGuarding => (Kind == EnemyKind.Shield && _state != State.Recover) || IsClockGuarding;

        private void Update()
        {
            PixelSprites.Face(_sprite, _facing);

            if (_rig != null)
            {
                UpdateRigLook();
            }
            else
            {
                UpdateBoxLook();
            }
            UpdateRaidVisuals();

            if (_shieldVisual != null)
            {
                _shieldVisual.enabled = IsGuarding;
                _shieldVisual.transform.localPosition = new Vector3(_facing * (_size.x * 0.5f + 0.1f), 0f, 0f);
            }
        }

        /// <summary>예전 픽셀 그림일 때의 색. 그림 전체를 신호 색으로 칠한다.</summary>
        private void UpdateBoxLook()
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
            _sprite.color = RaidTint(color);
        }

        private void FixedUpdate()
        {
            if (Kind == EnemyKind.Dummy || _player == null || _dead) return;

            if (_body.position.y < StageBuilder.KillY)
            {
                Die();
                return;
            }

            // 쓰러진 쌍둥이는 일어날 때까지 아무것도 하지 않는다.
            if (TickRaid()) return;

            bool stunned = _stunTimer > 0f;
            if (stunned) _stunTimer -= Time.fixedDeltaTime;

            Vector2 toPlayer = _player.Position - _body.position;
            if (Kind == EnemyKind.Flyer)
            {
                // 날아다니는 적은 위아래로도 스스로 움직이므로 따로 처리한다.
                Vector2 velocity = TickFlyer(toPlayer);
                if (!stunned) _body.linearVelocity = velocity;
                return;
            }

            float horizontal;
            if (IsBoss) horizontal = TickBoss(toPlayer);
            else if (Kind == EnemyKind.Brute || Kind == EnemyKind.Shield) horizontal = TickBrute(toPlayer);
            else horizontal = TickShooter(toPlayer);

            if (!stunned)
            {
                _body.linearVelocity = new Vector2(horizontal, _body.linearVelocity.y);
            }
        }

        /// <summary>
        /// 돌진형: 다가간다, 번쩍인다, 공격한다, 잠깐 멈춘다.
        /// 같은 높이에서는 돌진하고, 플레이어가 위에 있으면 뛰어올라 덮친다.
        /// 쫓아올 때는 턱을 뛰어넘고, 낭떠러지를 내려오고, 구덩이를 건넌다.
        /// </summary>
        private float TickBrute(Vector2 toPlayer)
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            float distanceY = Mathf.Abs(toPlayer.y);
            if (_jumpCooldown > 0f) _jumpCooldown -= Time.fixedDeltaTime;

            switch (_state)
            {
                case State.Idle:
                    if (distanceX < BruteNoticeRange && distanceY < 7f) _state = State.Chase;
                    return 0f;

                case State.Chase:
                    return ChaseAsBrute(toPlayer, distanceX, distanceY);

                case State.Windup:
                    // 공격 방향은 예고를 시작할 때 정해진다. 그래서 뒤로 넘어가면 피할 수 있다.
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer > 0f) return 0f;

                    if (_pattern == Pattern.Slam)
                    {
                        StartLeap(toPlayer);
                        Enter(State.Attack, 2.5f);
                        return _slamVelocityX;
                    }
                    Enter(State.Attack, BruteLungeTime);
                    return 0f;

                case State.Attack:
                    if (_collider.bounds.Intersects(_player.Collider.bounds))
                    {
                        _player.TakeDamage(1, _body.position.x);
                    }
                    _stateTimer -= Time.fixedDeltaTime;

                    if (_pattern == Pattern.Slam)
                    {
                        _airTime += Time.fixedDeltaTime;
                        bool landed = _airTime > 0.25f && _body.linearVelocity.y <= 0.1f && IsStandingOnGround();
                        if (landed || _stateTimer <= 0f)
                        {
                            Enter(State.Recover, BruteRecoverTime);
                            return 0f;
                        }
                        return _slamVelocityX;
                    }

                    if (_stateTimer <= 0f) Enter(State.Recover, BruteRecoverTime);
                    return HasGroundAhead() ? _facing * BruteLungeSpeed : 0f;

                default:
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) _state = State.Chase;
                    return 0f;
            }
        }

        private float ChaseAsBrute(Vector2 toPlayer, float distanceX, float distanceY)
        {
            if (distanceX > BruteForgetRange)
            {
                _state = State.Idle;
                return 0f;
            }

            bool grounded = IsStandingOnGround();
            if (!grounded)
            {
                // 공중: 구덩이를 건너는 중이면 그 방향으로, 아니면 플레이어 쪽으로 움직인다.
                if (_gapTimer > 0f)
                {
                    _gapTimer -= Time.fixedDeltaTime;
                    return _gapDirection * BruteGapSpeed;
                }
                TurnToward(toPlayer.x);
                return distanceX > 0.4f ? _facing * MoveSpeed : 0f;
            }

            _gapTimer = 0f;
            // 방패병은 돌아서는 데 시간이 걸린다. 돌아서는 동안에는 제자리에 서 있다.
            if (!TurnToward(toPlayer.x)) return 0f;
            Vector2 warningPosition = _body.position + Vector2.up * (_size.y * 0.5f + 0.9f);

            // 같은 높이에서 가까우면 돌진을 예고한다.
            if (distanceX <= BruteAttackRange && distanceY < 1.6f)
            {
                _pattern = Pattern.Lunge;
                Enter(State.Windup, BruteWindupTime);
                Warn(warningPosition, "!");
                return 0f;
            }

            // 플레이어가 위에 있으면 뛰어올라 덮치는 공격을 예고한다.
            if (toPlayer.y > 1.2f && distanceX < 5f && _jumpCooldown <= 0f)
            {
                _pattern = Pattern.Slam;
                _jumpCooldown = 1.5f;
                Enter(State.Windup, BruteLeapWindupTime);
                Warn(warningPosition, "!");
                return 0f;
            }

            if (distanceX <= 0.4f) return 0f;

            // 앞에 턱이나 벽이 있으면 뛰어넘는다.
            if (IsWallAhead() && _jumpCooldown <= 0f)
            {
                Hop();
                return _facing * MoveSpeed;
            }

            if (HasGroundAhead())
            {
                // 걷고 있는데 제자리라면 어딘가에 걸린 것이다. 뛰어서 빠져나온다.
                bool stuck = Mathf.Abs(_body.position.x - _lastX) < 0.01f;
                _lastX = _body.position.x;
                _stuckTimer = stuck ? _stuckTimer + Time.fixedDeltaTime : 0f;
                if (_stuckTimer > 0.4f && _jumpCooldown <= 0f)
                {
                    _stuckTimer = 0f;
                    Hop();
                }
                return _facing * MoveSpeed;
            }

            // 앞이 낭떠러지다. 아래에 땅이 있으면 그냥 내려간다.
            Vector2 ahead = _body.position + new Vector2(_facing * (_size.x * 0.5f + 0.35f), -_size.y * 0.5f + 0.1f);
            if (Combat.SolidDistanceBelow(ahead, 12f) >= 0f)
            {
                return _facing * MoveSpeed;
            }

            // 구덩이다. 건너편에 땅이 있으면 뛰어서 건넌다.
            if (_jumpCooldown <= 0f && HasLandingAcross())
            {
                Hop();
                _gapTimer = 0.9f;
                _gapDirection = _facing;
                return _facing * BruteGapSpeed;
            }
            return 0f;
        }

        /// <summary>
        /// 플레이어 쪽을 본다. 방패병은 바로 돌지 못하고 잠깐 걸린다.
        /// 지금 플레이어 쪽을 보고 있으면 true.
        /// </summary>
        private bool TurnToward(float deltaX)
        {
            int desired = deltaX >= 0f ? 1 : -1;
            if (desired == _facing)
            {
                _turnTimer = 0f;
                return true;
            }

            if (Kind != EnemyKind.Shield)
            {
                _facing = desired;
                return true;
            }

            _turnTimer += Time.fixedDeltaTime;
            if (_turnTimer >= ShieldTurnTime)
            {
                _turnTimer = 0f;
                _facing = desired;
                return true;
            }
            return false;
        }

        private void Hop()
        {
            _body.linearVelocity = new Vector2(_body.linearVelocity.x, BruteHopSpeed);
            _jumpCooldown = 0.7f;
        }

        /// <summary>플레이어가 지금 있는 곳을 향해 포물선으로 뛰어오른다.</summary>
        private void StartLeap(Vector2 toPlayer)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * BossGravityScale;
            float rise = Mathf.Max(toPlayer.y, 0f) + 1.2f;
            float jumpSpeed = Mathf.Clamp(Mathf.Sqrt(2f * gravity * rise), 10f, 18f);
            float airTime = 1.5f * jumpSpeed / gravity;
            _slamVelocityX = Mathf.Clamp(toPlayer.x / airTime, -9f, 9f);
            _body.linearVelocity = new Vector2(_slamVelocityX, jumpSpeed);
            _airTime = 0f;
        }

        /// <summary>발 앞, 정강이 높이에 지형이 있는지 본다.</summary>
        private bool IsWallAhead()
        {
            Vector2 probe = _body.position + new Vector2(_facing * (_size.x * 0.5f + 0.25f), -_size.y * 0.5f + 0.35f);
            return Combat.IsSolidAt(probe);
        }

        /// <summary>구덩이 건너편, 뛰어서 닿을 거리에 땅이 있는지 본다.</summary>
        private bool HasLandingAcross()
        {
            float feetY = _body.position.y - _size.y * 0.5f - 0.3f;
            float edgeX = _body.position.x + _facing * (_size.x * 0.5f);
            for (float distance = 2.5f; distance <= 5.5f; distance += 0.5f)
            {
                if (Combat.IsSolidAt(new Vector2(edgeX + _facing * distance, feetY))) return true;
            }
            return false;
        }

        /// <summary>원거리형: 제자리에서 번쩍인 뒤 플레이어가 있던 쪽으로 탄을 쏜다.</summary>
        private float TickShooter(Vector2 toPlayer)
        {
            if (_state == State.Windup)
            {
                _stateTimer -= Time.fixedDeltaTime;
                if (_stateTimer <= 0f)
                {
                    if (Kind == EnemyKind.Bomber)
                    {
                        // 폭탄은 지금 플레이어가 있는 자리에 떨어진다.
                        _game.SpawnBomb(_body.position, _player.Position, BombFlightTime, BombRadius);
                        _cooldown = BomberInterval;
                    }
                    else
                    {
                        Vector2 direction = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : new Vector2(_facing, 0f);
                        _game.SpawnProjectile(_body.position, direction * ProjectileSpeed, 0.35f);
                        _cooldown = ShooterInterval;
                    }
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
                    Enter(State.Windup, Kind == EnemyKind.Bomber ? BomberWindupTime : ShooterWindupTime);
                    Warn(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), "!");
                }
            }
            return 0f;
        }

        /// <summary>
        /// 날아다니는 적: 플레이어의 비스듬한 위쪽에서 맴돌다가, 번쩍인 뒤 그때 정한 방향으로 곧게 내리꽂는다.
        /// 내리꽂은 뒤에는 잠깐 멈춰서 빈틈이 생긴다.
        /// </summary>
        private Vector2 TickFlyer(Vector2 toPlayer)
        {
            switch (_state)
            {
                case State.Idle:
                    if (toPlayer.magnitude < FlyerNoticeRange)
                    {
                        _state = State.Chase;
                        _hoverSide = toPlayer.x >= 0f ? -1f : 1f;
                    }
                    return Vector2.zero;

                case State.Chase:
                    _facing = toPlayer.x >= 0f ? 1 : -1;
                    Vector2 hoverPoint = _player.Position + new Vector2(_hoverSide * 3f, 3.2f);
                    Vector2 toHover = hoverPoint - _body.position;
                    _cooldown -= Time.fixedDeltaTime;
                    if (_cooldown <= 0f && toHover.magnitude < 2f)
                    {
                        _diveDirection = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector2.down;
                        _diveFloorY = _player.Position.y;
                        Enter(State.Windup, FlyerWindupTime);
                        Warn(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), "!");
                        return Vector2.zero;
                    }
                    return toHover.magnitude > 0.3f ? toHover.normalized * FlyerHoverSpeed : Vector2.zero;

                case State.Windup:
                    // 내리꽂는 방향은 예고를 시작할 때 정해진다. 그 자리를 벗어나면 피할 수 있다.
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f) Enter(State.Attack, FlyerDiveTime);
                    return Vector2.zero;

                case State.Attack:
                    if (_collider.bounds.Intersects(_player.Collider.bounds))
                    {
                        _player.TakeDamage(1, _body.position.x);
                    }
                    // 플레이어가 있던 높이까지만 내려온다. 그래서 내리꽂은 뒤에는 손이 닿는 곳에 멈춰 있다.
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f || _body.position.y <= _diveFloorY)
                    {
                        Enter(State.Recover, FlyerRecoverTime);
                        return Vector2.zero;
                    }
                    return _diveDirection * FlyerDiveSpeed;

                default:
                    _stateTimer -= Time.fixedDeltaTime;
                    if (_stateTimer <= 0f)
                    {
                        _state = State.Chase;
                        _cooldown = FlyerInterval;
                        // 내리꽂고 나면 반대편으로 넘어가 있으니, 지금 있는 쪽에서 다시 맴돈다.
                        _hoverSide = toPlayer.x >= 0f ? -1f : 1f;
                    }
                    return Vector2.zero;
            }
        }

        /// <summary>공격 예고를 글자와 소리로 알린다.</summary>
        private void Warn(Vector2 position, string text)
        {
            // 보스는 공격 이름을 붉은 판에 담아 띄우고, 잡몹은 느낌표만 띄운다.
            if (IsBoss) _game.ShowCallout(position, text, BossCalloutColor);
            else _game.ShowPopup(position, text, WarningColor);
            Sfx.Play(Sound.Warn);
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
            _stateSerial++;
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
            // 쓰러진 쌍둥이와, 철갑이나 보호막이나 갑옷을 두른 보스는 피해를 받지 않는다. 쌍둥이는 맞는 쪽이 정해져 있다.
            if (BlockedByRaid(source)) return;

            // 방패병은 앞에서 온 공격을 막는다. 밀려나는 방향의 반대쪽이 공격자가 있는 쪽이다.
            int attackerSide = knockback.x >= 0f ? -1 : 1;
            if (IsGuarding && attackerSide == _facing)
            {
                _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 0.6f), "막힘", new Color(0.8f, 0.9f, 1f, 1f));
                _flashColor = new Color(0.8f, 0.9f, 1f, 1f);
                _flashTimer = 0.08f;
                _game.Feel.BlockedHit((Vector2)transform.position + new Vector2(_facing * _size.x * 0.5f, 0f));
                if (IsClockGuarding) OnClockGuardBlocked();
                return;
            }
            // 시계 방패를 든 보스의 등 뒤를 쳤다. 피해도 주고 방패에 금도 간다.
            if (attackerSide != _facing) OnClockBackHit();

            int self = (int)source;
            int other = 1 - self;
            // 본체와 분신의 공격이 정해진 시간 안에 겹치면 협공이다.
            bool pincer = Time.time - _lastHitTime[other] <= _game.Stats.PincerWindow;
            _lastHitTime[self] = Time.time;

            if (pincer) damage *= _game.Stats.PincerMultiplier;
            // 멈춘 태엽 기사는 협공에 태엽이 풀린다. 피해를 정하기 전에 알려 준다.
            OnRaidHit(pincer);
            // 기믹을 풀어 보스가 무너져 있는 동안에는 더 아프게, 태엽 갑옷을 입은 동안에는 덜 아프게 맞는다.
            damage *= DamageTakenScale(source);
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
            _game.Feel.HitEnemy(transform.position, pincer, source == HitSource.Shadow);

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
                else if (TryGoDown())
                {
                    // 쌍둥이는 바로 죽지 않고 쓰러진다. 짝이 이미 쓰러져 있었다면 둘 다 끝났다.
                    return;
                }
                else
                {
                    Die();
                    return;
                }
            }

            CheckRaidPhases();

            UpdateHealthBar();
        }

        private void UpdateHealthBar()
        {
            float ratio = Mathf.Clamp01(_health / MaxHealth);
            _healthFill.localScale = new Vector3(ratio, 0.08f, 1f);
            _healthFill.localPosition = new Vector3(-(1f - ratio) * 0.5f, _healthFill.localPosition.y, 0f);
        }

        private void Die()
        {
            if (_dead) return;
            _dead = true;
            ClearRaidObjects();
            _game.OnEnemyDied(this);
            Destroy(gameObject);
        }
    }
}
