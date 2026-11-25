using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 3스테이지 "멈춘 시계탑"의 보스 둘. 화면에는 풀이를 알려 주지 않는다. 아래는 개발자용 정답이다.
    ///
    /// 태엽 기사 (3-3 중간 보스)
    ///   공격: 순간 베기(사라졌다가 내 등 뒤에 나타나 벤다), 진자(방 끝에서 끝까지 낮게 지나가는 칼날),
    ///         초침(내 자리를 세 번 연달아 내리친다).
    ///   기믹: 태엽 갑옷 때문에 평소에는 피해가 30%만 들어간다. 가끔 내 둘레에 시계 감옥을 세우는데, 벽은 걷거나 대시해서는
    ///     지나갈 수 없고 바늘이 한 바퀴 돌면 감옥 안을 벤다. 감옥 밖에 있는 분신과 자리를 바꿔(K) 빠져나가면
    ///     기사가 빈 감옥에 뛰어들었다가 멈춘 시간에 갇힌다. 멈춰 있는 기사를 나와 분신이 함께 치면(협공)
    ///     태엽이 풀려 갑옷이 벗겨지고 한동안 더 아프게 맞는다.
    ///   체력 50%부터는 분신 둘레에도 더 빨리 닫히는 감옥이 서고, 감옥이 서 있는 동안 분신이 그 자리에 묶인다.
    ///     분신의 감옥이 먼저 닫혀 사라진 뒤, 내 감옥이 닫히기 전에 자리를 바꿔야 한다. 너무 일찍 바꾸면 분신의 감옥에 베인다.
    ///
    /// 시계탑의 주인 (3-6 보스, 마지막 보스)
    ///   공격: 톱니바퀴(발밑에서 양쪽으로 굴린다), 멈춘 탄막(내 둘레에 멈춰 있던 탄이 차례로 날아온다),
    ///         되감기 돌진(달려갔다가 같은 길로 되돌아온다), 시한 지뢰(내 주변 바닥에 시계 지뢰를 깐다).
    ///   기믹, 시계 방패(체력 70%부터): 보고 있는 쪽에서 오는 공격은 막는다. 나와 분신 중 가까운 쪽을 천천히 바라본다.
    ///     등 뒤를 연달아 여섯 번 치면 방패가 깨지는데, 잠깐이라도 멈추면 금이 메워진다.
    ///     풀이: 분신을 주인 바로 앞에 고정해 미끼로 쓰고, 나는 멀찍이 등 뒤로 돌아가 몰아친다.
    ///   기믹, 역행(체력 35%부터, 자정): 가끔 시계판을 띄우고 시간을 거꾸로 돌려, 끝나면 몇 초 전의 체력으로 돌아간다.
    ///     시계판이 다 돌 때 고정한 분신이 주인 곁에 있으면 시간이 붙잡혀 역행이 실패하고 주인이 무너진다.
    /// </summary>
    public sealed partial class Enemy
    {
        // ---- 태엽 기사 ----
        /// <summary>감옥이 선 뒤 시간이 멈출 때까지(초). 화면의 카운트다운도 이 값을 쓴다.</summary>
        public const float CageTime = 3.4f;
        /// <summary>체력 50%부터 분신 둘레에 서는 감옥이 닫히는 시간.</summary>
        private const float ShadowCageTime = 2f;
        private const float CageFirstDelay = 5f;
        private const float KnightCageInterval = 12f;
        private const float KnightSecondPhaseAt = 0.5f;
        /// <summary>자리 바꾸기를 이만큼 안에 다시 쓸 수 있을 때만 감옥을 세운다. 빠져나갈 길이 없는 감옥은 세우지 않는다.</summary>
        private const float CageSwapReadiness = 1f;
        private const float KnightFreezeTime = 5f;
        /// <summary>태엽 갑옷을 입은 기사가 받는 피해의 비율.</summary>
        private const float KnightArmorScale = 0.3f;
        /// <summary>태엽이 풀린 뒤, 멈춰 있던 남은 시간에 더해 무너져 있는 시간.</summary>
        private const float KnightUnlockBonus = 2.5f;
        private const float BlinkDistance = 2.2f;
        private const float BlinkSlashWarn = 0.35f;
        private const float PendulumSpeed = 13f;
        private const float PendulumWarn = 0.55f;
        private const int SecondHandStrikes = 3;
        private const float SecondHandInterval = 0.55f;

        // ---- 시계탑의 주인 ----
        private const float MasterGuardAt = 0.7f;
        private const float MasterMidnightAt = 0.35f;
        /// <summary>방패를 든 동안 바라볼 쪽이 바뀌면 이만큼 걸려 돌아선다.</summary>
        private const float GuardTurnTime = 0.6f;
        private const int GuardCracksToBreak = 6;
        /// <summary>등 뒤를 친 뒤 이만큼 안에 다시 치지 않으면 금이 메워진다.</summary>
        private const float GuardCrackDecay = 2.5f;
        private const float GuardBreakStunTime = 4f;
        /// <summary>방패가 깨진 뒤 다시 들 때까지의 시간. 무너져 있는 시간을 포함한다.</summary>
        private const float GuardDownTime = 11f;
        private const float GearSpeed = 7.5f;
        private const float GearSize = 0.75f;
        private const float GearSecondWaveDelay = 0.6f;
        private const int BarrageCount = 8;
        private const float BarrageRadius = 6f;
        private const float BarrageHold = 1.1f;
        private const float BarrageStagger = 0.07f;
        private const float BarrageSpeed = 10f;
        private const float MineFuse = 2.2f;
        private const float MineSize = 3f;
        private const float MineSpacing = 3.4f;
        private const float RewindOutTime = 0.42f;
        private const float RewindPause = 0.25f;
        private const float RewindSpeed = 15f;
        /// <summary>역행의 시계판이 한 바퀴 도는 시간. 화면의 카운트다운도 이 값을 쓴다.</summary>
        public const float ReversalCastTime = 3f;
        private const float ReversalFirstDelay = 3f;
        private const float ReversalInterval = 14f;
        /// <summary>역행이 끝나면 이만큼 전의 체력으로 돌아간다.</summary>
        private const float ReversalLookback = 6f;
        /// <summary>고정한 분신이 주인 몸에서 이만큼(가로) 안에 있으면 역행을 붙잡는다.</summary>
        private const float ReversalAnchorRange = 2.5f;
        private const float ReversalStunTime = 4.5f;
        private const int HistorySize = 40;
        private const float HistoryStep = 0.25f;

        private static readonly Color CageTextColor = new Color(0.6f, 0.85f, 1f, 1f);
        private static readonly Color FrozenTint = new Color(0.55f, 0.8f, 1f, 1f);
        private static readonly Color BlinkSlashColor = new Color(0.75f, 0.85f, 1f, 1f);
        private static readonly Color PendulumColor = new Color(0.85f, 0.88f, 0.95f, 1f);
        private static readonly Color SecondHandColor = new Color(1f, 0.85f, 0.5f, 1f);
        private static readonly Color GuardTextColor = new Color(1f, 0.75f, 0.4f, 1f);
        private static readonly Color GuardColor = new Color(1f, 0.78f, 0.4f, 0.85f);
        private static readonly Color BarrageColor = new Color(0.65f, 0.9f, 1f, 1f);
        private static readonly Color MineColor = new Color(1f, 0.6f, 0.3f, 1f);
        private static readonly Color RewindPathColor = new Color(1f, 0.68f, 0.32f, 1f);
        private static readonly Color MineDialColor = new Color(1f, 0.8f, 0.5f, 0.9f);
        private static readonly Color ReversalColor = new Color(0.7f, 0.95f, 1f, 0.9f);
        private static readonly Color HealTextColor = new Color(0.5f, 1f, 0.6f, 1f);

        private static readonly Pattern[] KnightCycle = { Pattern.Blink, Pattern.Pendulum, Pattern.SecondHand };
        private static readonly Pattern[] MasterCycle = { Pattern.Gears, Pattern.Barrage, Pattern.RewindDash, Pattern.Mines };

        private ClockCage _cage;
        private ClockCage _shadowCage;
        private float _shadowCageTimer;
        private bool _cageFailed;
        private float _cageCooldown;
        private float _knightFrozen;
        private int _secondHandLeft;
        private float _secondHandTimer;

        private bool _hasGuardPhase;
        private bool _guardUp;
        private float _guardDownTimer;
        private int _guardCracks;
        private float _crackDecayTimer;
        private float _guardTurnTimer;
        private float _gearWaveTimer;
        private int _rewindDirection = 1;
        private float _rewindTimer;
        private float _reversalCooldown;
        private SpriteRenderer _reversalDial;
        private Transform _reversalHand;
        private float[] _healthHistory;
        private int _historyIndex;
        private int _historyCount;
        private float _historyTimer;

        private bool IsClockBoss => Kind == EnemyKind.ClockKnight || Kind == EnemyKind.ClockMaster;

        /// <summary>시계탑의 주인이 방패를 들고 있는지. 무너져 있는 동안에는 막지 못한다.</summary>
        private bool IsClockGuarding => _guardUp && _staggerTimer <= 0f && !_dead;

        private bool IsCaging => _pattern == Pattern.Cage && _state == State.Attack && _cage != null;

        private bool IsReversing => _pattern == Pattern.Reversal && _state == State.Attack;

        /// <summary>시간이 멈출 때까지 남은 시간. 감옥이 서 있지 않으면 0.</summary>
        public float CageRemaining => IsCaging ? Mathf.Max(0f, _stateTimer) : 0f;

        /// <summary>역행이 끝날 때까지 남은 시간. 역행 중이 아니면 0.</summary>
        public float ReversalRemaining => IsReversing ? Mathf.Max(0f, _stateTimer) : 0f;

        private string ClockBarStatus
        {
            get
            {
                if (!IsClockBoss) return null;
                if (IsCaging) return "시계 감옥";
                if (_knightFrozen > 0f) return "정지";
                if (IsReversing) return "역행";
                if (IsClockGuarding) return "시계 방패";
                if (Kind == EnemyKind.ClockMaster && _phase >= 3) return "자정";
                return null;
            }
        }

        // ---- EnemyRaid.cs에서 부르는 것 ----

        private void BuildClock()
        {
            if (Kind == EnemyKind.ClockKnight)
            {
                _cageCooldown = CageFirstDelay;
            }
            else if (Kind == EnemyKind.ClockMaster)
            {
                _healthHistory = new float[HistorySize];
                // 보고 있는 쪽에 드는 시계 방패. 방패병처럼 Update에서 보이고 숨기고 앞쪽으로 옮긴다.
                _shieldVisual = RaidArt.Create("ClockShield", transform, transform.position, new Vector2(0.5f, _size.y * 0.9f), RaidArt.Disc, GuardColor, 9);
                _shieldVisual.enabled = false;
            }
        }

        private void TickClock(float dt)
        {
            if (Kind == EnemyKind.ClockKnight) TickKnight(dt);
            else if (Kind == EnemyKind.ClockMaster) TickMaster(dt);
        }

        private void CheckClockPhases()
        {
            if (Kind == EnemyKind.ClockKnight)
            {
                if (_phase == 1 && HealthRatio <= KnightSecondPhaseAt)
                {
                    _phase = 2;
                    _game.OnBossPhase(this, "가속!");
                }
                return;
            }
            if (Kind != EnemyKind.ClockMaster) return;

            if (_phase == 1 && HealthRatio <= MasterGuardAt)
            {
                _phase = 2;
                _hasGuardPhase = true;
                RaiseGuard();
                _game.OnBossPhase(this, "시계 방패!");
            }
            else if (_phase == 2 && HealthRatio <= MasterMidnightAt)
            {
                _phase = 3;
                _reversalCooldown = ReversalFirstDelay;
                _baseColor = Color.Lerp(_baseColor, new Color(0.3f, 0.25f, 0.4f, 1f), 0.5f);
                _game.OnBossPhase(this, "자정!");
            }
        }

        private void ClearClockObjects()
        {
            if (_cage != null) Destroy(_cage.gameObject);
            if (_shadowCage != null) Destroy(_shadowCage.gameObject);
            _cage = null;
            _shadowCage = null;
            ClearReversalDial();
        }

        /// <summary>멈춘 기사는 푸르게, 순간 베기를 준비하는 기사는 흐려진다.</summary>
        private Color ClockTint(Color color)
        {
            if (Kind != EnemyKind.ClockKnight) return color;
            if (_knightFrozen > 0f && _staggerTimer <= 0f) return Color.Lerp(color, FrozenTint, 0.6f);
            if (_state == State.Windup && _pattern == Pattern.Blink) color.a = Mathf.Clamp01(_stateTimer / 0.45f);
            return color;
        }

        private void UpdateClockVisuals()
        {
            if (_reversalHand == null || !IsReversing) return;
            // 역행의 바늘은 거꾸로 돈다.
            float progress = 1f - Mathf.Clamp01(_stateTimer / ReversalCastTime);
            _reversalHand.localRotation = Quaternion.Euler(0f, 0f, 360f * progress);
            _reversalDial.transform.position = transform.position + Vector3.up * (_size.y * 0.5f + 1.4f);
        }

        /// <summary>
        /// 쫓는 동안 바라볼 쪽으로 돈다. 방패를 든 시계탑의 주인은 나와 분신 중 가까운 쪽을 천천히 바라보고,
        /// 돌아서는 동안에는 걷지도 공격하지도 않는다. 다 돌아섰으면 true.
        /// </summary>
        private bool TurnBossToward(Vector2 toPlayer)
        {
            Vector2 toTarget = toPlayer;
            Vector2 shadow;
            if (IsClockGuarding && _player.TryGetShadow(out shadow))
            {
                Vector2 toShadow = shadow - _body.position;
                if (Mathf.Abs(toShadow.x) < Mathf.Abs(toPlayer.x)) toTarget = toShadow;
            }

            int want = toTarget.x >= 0f ? 1 : -1;
            if (want == _facing || !IsClockGuarding)
            {
                _facing = want;
                _guardTurnTimer = 0f;
                return true;
            }

            _guardTurnTimer += Time.fixedDeltaTime;
            if (_guardTurnTimer < GuardTurnTime) return false;
            _guardTurnTimer = 0f;
            _facing = want;
            return true;
        }

        // ---- 태엽 기사 ----

        private Pattern NextKnightPattern(bool high)
        {
            if (ReadyToCage()) return Pattern.Cage;
            if (high) return _patternIndex % 2 == 0 ? Pattern.SecondHand : Pattern.Blink;
            return KnightCycle[_patternIndex % KnightCycle.Length];
        }

        private void TickKnight(float dt)
        {
            if (_knightFrozen > 0f) _knightFrozen -= dt;
            if (_cageCooldown > 0f && !IsCaging && _knightFrozen <= 0f && _staggerTimer <= 0f) _cageCooldown -= dt;

            if (_secondHandLeft > 0)
            {
                _secondHandTimer -= dt;
                if (_secondHandTimer <= 0f)
                {
                    _secondHandTimer = SecondHandInterval;
                    _secondHandLeft--;
                    StrikeSecondHand();
                }
            }
        }

        /// <summary>멈춰 있는 기사를 나와 분신이 함께 치면 태엽이 풀린다.</summary>
        private void OnKnightHit(bool pincer)
        {
            if (Kind != EnemyKind.ClockKnight || !pincer || _knightFrozen <= 0f || _staggerTimer > 0f) return;
            Stagger(_knightFrozen + KnightUnlockBonus);
            _knightFrozen = 0f;
            Sfx.Play(Sound.Shatter);
            _game.Feel.Sparks(transform.position, CageTextColor, 14, 7f);
            _game.OnGimmickSolved(this, "태엽이 풀렸다!");
        }

        private bool ReadyToCage()
        {
            if (_cageCooldown > 0f || _cage != null) return false;
            // 분신이 없거나 자리 바꾸기를 곧 쓸 수 없으면 빠져나갈 길이 없으므로 기다린다.
            Vector2 shadow;
            if (!_player.TryGetShadow(out shadow)) return false;
            return _player.SwapCooldownRemaining <= CageSwapReadiness;
        }

        /// <summary>감옥이 닫힐 때 안에 있으면 잃는 체력. 피할 수 없다.</summary>
        private int CageDamage => _phase >= 2 ? 2 : 1;

        /// <summary>사라졌다가 플레이어의 등 뒤에 나타나 벤다.</summary>
        private void BlinkStrike()
        {
            float width = _game.RoomWidth;
            Vector2 target = _player.Position;
            int behind = -_player.Facing;
            float x = target.x + behind * BlinkDistance;
            if (width > 2f && (x < 1f || x > width - 1f)) x = target.x - behind * BlinkDistance;
            float feetY = _player.Feet.y;
            var destination = new Vector2(x, feetY + _size.y * 0.5f + 0.05f);

            _game.Feel.Sparks(_body.position, CageTextColor, 8, 5f);
            _body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, 0f);
            _body.linearVelocity = Vector2.zero;
            _facing = target.x >= x ? 1 : -1;
            _game.Feel.Sparks(destination, CageTextColor, 8, 5f);
            Sfx.Play(Sound.Swap);

            var center = new Vector2((x + target.x) * 0.5f, feetY + 1.1f);
            Hazard.Spawn(center, new Vector2(BlinkDistance + 2.2f, 2.2f), BlinkSlashWarn, 0.2f, BlinkSlashColor, 1, default(Vector2), true);
        }

        /// <summary>방 끝에서 끝까지 낮게 지나가는 진자 칼날. 먼저 지나갈 길을 보여 준다. 뛰어넘는다.</summary>
        private void SwingPendulum()
        {
            float width = _game.RoomWidth;
            if (width <= 2f) return;
            int direction = _player.Position.x >= _body.position.x ? 1 : -1;
            float startX = direction > 0 ? 0.7f : width - 0.7f;
            float travelTime = (width - 1.4f) / PendulumSpeed;
            float y = StageBuilder.GroundTop + 0.75f;
            Hazard lane = Hazard.Spawn(new Vector2(width * 0.5f, y), new Vector2(width, 1.5f), PendulumWarn, 0.01f, PendulumColor, 0);
            if (lane != null) lane.ShowDirection(direction);
            Hazard.Spawn(new Vector2(startX, y), new Vector2(1.4f, 1.5f), PendulumWarn, travelTime, PendulumColor, 1, new Vector2(direction * PendulumSpeed, 0f), true);
        }

        private void StartSecondHand()
        {
            _secondHandLeft = SecondHandStrikes;
            _secondHandTimer = 0f;
        }

        /// <summary>지금 플레이어가 있는 자리를 내리친다.</summary>
        private void StrikeSecondHand()
        {
            float x = _player.Position.x;
            Hazard.Spawn(new Vector2(x, StageBuilder.GroundTop + 4f), new Vector2(1.6f, 8f), 0.5f, 0.15f, SecondHandColor);
            Sfx.Play(Sound.Tick);
        }

        /// <summary>
        /// 예고가 끝났다. 지금 플레이어가 서 있는 자리를 가운데로 감옥을 세운다.
        /// 체력 50%부터는 분신 둘레에도 더 빨리 닫히는 감옥을 세우고, 감옥이 서 있는 동안 분신을 묶어 둔다.
        /// </summary>
        private bool SpawnCages()
        {
            Transform root = _game.RoomRoot;
            if (root == null) return false;

            float x = CageCenterFor(_player.Position.x);
            _cage = ClockCage.Create(root, x, StageBuilder.GroundTop, CageTime, _collider);
            _cageFailed = false;
            Sfx.Play(Sound.Cage);
            _game.Feel.Shake(0.2f, 0.2f);

            Vector2 shadow;
            if (_phase >= 2 && _player.TryGetShadow(out shadow))
            {
                float shadowX = CageCenterFor(shadow.x);
                if (Mathf.Abs(shadowX - x) >= ClockCage.HalfWidth * 2f + 0.2f)
                {
                    _shadowCage = ClockCage.Create(root, shadowX, StageBuilder.GroundTop, ShadowCageTime, _collider);
                    _shadowCageTimer = ShadowCageTime;
                    _player.HoldShadows(CageTime);
                }
            }
            return true;
        }

        /// <summary>감옥이 방 밖으로 삐져나가지 않게 가운데를 정한다.</summary>
        private float CageCenterFor(float x)
        {
            float margin = ClockCage.HalfWidth + 0.6f;
            float width = _game.RoomWidth;
            return width > margin * 2f ? Mathf.Clamp(x, margin, width - margin) : x;
        }

        /// <summary>감옥이 서 있는 동안. 기사는 바늘이 돌아가는 것을 지켜보며 1초마다 똑딱인다.</summary>
        private float TickCage()
        {
            float dt = Time.fixedDeltaTime;
            float before = _stateTimer;
            _stateTimer -= dt;
            if (_stateTimer > 0f && Mathf.CeilToInt(_stateTimer) < Mathf.CeilToInt(before)) Sfx.Play(Sound.Tick);

            if (_shadowCage != null)
            {
                _shadowCageTimer -= dt;
                if (_shadowCageTimer <= 0f) ResolveShadowCage();
            }
            if (_stateTimer <= 0f) ResolveCage();
            return 0f;
        }

        /// <summary>분신의 감옥이 닫혔다. 그 안에 내가 있으면 베인다.</summary>
        private void ResolveShadowCage()
        {
            ClockCage cage = _shadowCage;
            _shadowCage = null;
            if (cage == null) return;

            cage.Strike();
            Sfx.Play(Sound.Freeze, 0.7f);
            if (!cage.Contains(_player.Position)) return;
            _player.TakeDamage(CageDamage, cage.CenterX, true);
            _cageFailed = true;
        }

        /// <summary>내 감옥이 닫혔다. 아직 안에 있으면 베이고, 제대로 빠져나갔으면 기사가 자기 감옥에 갇힌다.</summary>
        private void ResolveCage()
        {
            _cageCooldown = KnightCageInterval;
            if (_shadowCage != null) ResolveShadowCage();
            ClockCage cage = _cage;
            _cage = null;
            if (cage == null)
            {
                Rest(0.5f);
                return;
            }

            Sfx.Play(Sound.Freeze);
            _game.Feel.Flash(new Color(0.6f, 0.85f, 1f, 1f), 0.55f, 0.5f);
            _game.Feel.Shake(0.3f, 0.3f);

            if (cage.Contains(_player.Position))
            {
                cage.Strike();
                _game.ShowBlast(new Vector2(cage.CenterX, StageBuilder.GroundTop + 1.5f), ClockCage.HalfWidth, new Color(0.7f, 0.9f, 1f, 0.55f));
                // 멈춘 시간 속에서 베는 것이라 대시나 무적 시간으로 피할 수 없다.
                _player.TakeDamage(CageDamage, cage.CenterX, true);
                Rest(0.9f);
                return;
            }
            if (_cageFailed)
            {
                cage.Strike();
                Rest(0.6f);
                return;
            }

            // 플레이어는 빠져나갔다. 기사가 빈 감옥 한가운데로 뛰어들었다가 멈춘 시간에 갇힌다.
            var inside = new Vector2(cage.CenterX, StageBuilder.GroundTop + _size.y * 0.5f + 0.05f);
            _game.Feel.Sparks(_body.position, CageTextColor, 8, 5f);
            _body.position = inside;
            transform.position = new Vector3(inside.x, inside.y, 0f);
            _body.linearVelocity = Vector2.zero;
            cage.Freeze(KnightFreezeTime);
            _knightFrozen = KnightFreezeTime;
            _secondHandLeft = 0;
            Rest(KnightFreezeTime);
        }

        // ---- 시계탑의 주인 ----

        private Pattern NextMasterPattern(bool high)
        {
            if (ReadyToReverse()) return Pattern.Reversal;
            if (high) return _patternIndex % 2 == 0 ? Pattern.Barrage : Pattern.Mines;
            return MasterCycle[_patternIndex % MasterCycle.Length];
        }

        private void TickMaster(float dt)
        {
            _historyTimer -= dt;
            if (_historyTimer <= 0f)
            {
                _historyTimer = HistoryStep;
                _healthHistory[_historyIndex] = _health;
                _historyIndex = (_historyIndex + 1) % HistorySize;
                _historyCount = Mathf.Min(_historyCount + 1, HistorySize);
            }

            if (_gearWaveTimer > 0f)
            {
                _gearWaveTimer -= dt;
                if (_gearWaveTimer <= 0f) SpawnGearWave();
            }

            if (_phase >= 3 && _reversalCooldown > 0f && !IsReversing) _reversalCooldown -= dt;

            if (_guardCracks > 0 && IsClockGuarding)
            {
                _crackDecayTimer -= dt;
                if (_crackDecayTimer <= 0f)
                {
                    _guardCracks = 0;
                    _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.1f), "복구", GuardTextColor);
                }
            }

            if (_hasGuardPhase && !_guardUp)
            {
                _guardDownTimer -= dt;
                if (_guardDownTimer <= 0f)
                {
                    RaiseGuard();
                    Sfx.Play(Sound.Cage, 0.6f);
                }
            }
        }

        private void RaiseGuard()
        {
            _guardUp = true;
            _guardCracks = 0;
            _guardTurnTimer = 0f;
        }

        /// <summary>보고 있는 쪽에서 온 공격을 방패로 막았다. 따로 하는 일은 없다.</summary>
        private void OnClockGuardBlocked()
        {
            Sfx.Play(Sound.Block, 0.6f);
        }

        /// <summary>방패를 든 보스의 등 뒤를 쳤다. 금이 쌓이면 방패가 깨진다. 잠깐이라도 멈추면 금이 메워진다.</summary>
        private void OnClockBackHit()
        {
            if (!IsClockGuarding) return;

            _guardCracks++;
            _crackDecayTimer = GuardCrackDecay;
            Vector2 above = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.1f);
            _game.ShowPopup(above, "균열 " + _guardCracks, GuardTextColor);
            if (_guardCracks < GuardCracksToBreak) return;

            _guardUp = false;
            _guardCracks = 0;
            _guardDownTimer = GuardDownTime;
            Sfx.Play(Sound.Shatter);
            _game.Feel.Sparks(transform.position, GuardColor, 14, 7f);
            Stagger(GuardBreakStunTime);
            _game.OnGimmickSolved(this, "방패가 깨졌다!");
        }

        /// <summary>보스 발밑에서 양쪽으로 톱니바퀴를 굴린다. 뛰어넘거나 대시로 지나간다.</summary>
        private void StartGears()
        {
            SpawnGearWave();
            if (_phase >= 3) _gearWaveTimer = GearSecondWaveDelay;
        }

        private void SpawnGearWave()
        {
            if (_body == null) return;
            Vector2 feet = _body.position + new Vector2(0f, -_size.y * 0.5f + GearSize * 0.5f + 0.05f);
            _game.SpawnGear(feet + new Vector2(_size.x * 0.5f, 0f), new Vector2(GearSpeed, 0f), GearSize);
            _game.SpawnGear(feet - new Vector2(_size.x * 0.5f, 0f), new Vector2(-GearSpeed, 0f), GearSize);
        }

        /// <summary>플레이어 둘레에 멈춘 탄을 둥글게 세운다. 탄은 차례로 그때 플레이어가 있는 쪽으로 날아온다.</summary>
        private void FireFrozenBarrage()
        {
            Vector2 center = _player.Position;
            float spin = Random.Range(0f, 360f);
            for (int i = 0; i < BarrageCount; i++)
            {
                float angle = (spin + i * 360f / BarrageCount) * Mathf.Deg2Rad;
                Vector2 at = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * BarrageRadius;
                at.y = Mathf.Max(at.y, StageBuilder.GroundTop + 0.5f);
                Projectile shot = _game.SpawnShot(at, Vector2.zero, 0.45f, BarrageColor);
                if (shot != null) shot.HoldThenAim(BarrageHold + i * BarrageStagger, BarrageSpeed);
            }
            Sfx.Play(Sound.Freeze, 0.6f);
        }

        /// <summary>달려갈 방향을 바라보는 쪽으로 정하고, 갔다가 되돌아올 길을 그린다.</summary>
        private void AimRewindDash()
        {
            _rewindDirection = _facing;
            float reach = DashReach(_rewindDirection, RewindSpeed, RewindOutTime);
            ShowDashPath(_rewindDirection, reach, WindupFor(Pattern.RewindDash), RewindPathColor);
        }

        /// <summary>바라보는 쪽으로 달려갔다가, 잠깐 멈춘 뒤 같은 길로 되돌아온다.</summary>
        private float StartRewindDash()
        {
            _facing = _rewindDirection;
            _rewindTimer = 0f;
            Enter(State.Attack, RewindOutTime * 2f + RewindPause);
            Sfx.Play(Sound.Dash);
            return DashBlocked(_rewindDirection) ? 0f : _rewindDirection * RewindSpeed;
        }

        private float TickRewindDash()
        {
            float dt = Time.fixedDeltaTime;
            _rewindTimer += dt;
            _stateTimer -= dt;
            TouchPlayer(HeavyHitDamage);
            if (_stateTimer <= 0f) return Rest(0.8f);
            if (_rewindTimer < RewindOutTime) return DashBlocked(_rewindDirection) ? 0f : _rewindDirection * RewindSpeed;
            if (_rewindTimer - dt < RewindOutTime)
            {
                // 멈춘 순간에 되돌아갈 길을 보여 준다.
                float back = DashReach(-_rewindDirection, RewindSpeed, RewindOutTime);
                ShowDashPath(-_rewindDirection, back, RewindPause, RewindPathColor);
            }
            if (_rewindTimer < RewindOutTime + RewindPause) return 0f;
            return DashBlocked(-_rewindDirection) ? 0f : -_rewindDirection * RewindSpeed;
        }

        /// <summary>플레이어 주변 바닥에 시계 지뢰를 깐다. 바늘이 돌고 나면 터진다. 범위 밖으로 벗어난다.</summary>
        private void PlaceMines()
        {
            Transform root = _game.RoomRoot;
            float width = _game.RoomWidth;
            if (root == null || width <= 3f) return;

            int count = _phase >= 3 ? 4 : 3;
            float center = _player.Position.x;
            for (int i = 0; i < count; i++)
            {
                float x = Mathf.Clamp(center + (i - (count - 1) * 0.5f) * MineSpacing, 1.5f, width - 1.5f);
                var at = new Vector2(x, StageBuilder.GroundTop + MineSize * 0.5f);
                Hazard.Spawn(at, new Vector2(MineSize, MineSize), MineFuse, 0.2f, MineColor, HeavyHitDamage, default(Vector2), i == 0);
                SpriteRenderer dial = RaidArt.Create("MineDial", root, new Vector2(x, StageBuilder.GroundTop + 0.7f), new Vector2(1.2f, 1.2f), RaidArt.Ring, MineDialColor, 6);
                Destroy(dial.gameObject, MineFuse);
            }
            Sfx.Play(Sound.Tick);
        }

        // ---- 역행 ----

        private bool ReadyToReverse()
        {
            if (_phase < 3 || _reversalCooldown > 0f) return false;
            // 고정할 분신이 없거나 고정을 곧 쓸 수 없으면 막을 길이 없으므로 기다린다.
            Vector2 shadow;
            if (!_player.TryGetShadow(out shadow)) return false;
            return _player.AreShadowsAnchored || _player.AnchorCooldownRemaining <= ReversalCastTime - 1f;
        }

        /// <summary>머리 위에 시계판을 띄운다. 바늘이 거꾸로 돈다.</summary>
        private void BeginReversal()
        {
            ClearReversalDial();
            Transform root = _game.RoomRoot;
            if (root != null)
            {
                Vector2 at = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.4f);
                _reversalDial = RaidArt.Create("ReversalDial", root, at, new Vector2(1.8f, 1.8f), RaidArt.Ring, ReversalColor, 12);
                SpriteRenderer hand = PrototypeArt.CreateBox("ReversalHand", _reversalDial.transform, new Vector2(0f, 0.22f), new Vector2(0.06f, 0.44f), ReversalColor, 13);
                var pivot = new GameObject("ReversalPivot");
                pivot.transform.SetParent(_reversalDial.transform, false);
                hand.transform.SetParent(pivot.transform, false);
                _reversalHand = pivot.transform;
            }
            Sfx.Play(Sound.Cage);
        }

        /// <summary>시계판이 다 돌 때까지 제자리에서 1초마다 똑딱인다.</summary>
        private float TickReversal()
        {
            float before = _stateTimer;
            _stateTimer -= Time.fixedDeltaTime;
            if (_stateTimer > 0f && Mathf.CeilToInt(_stateTimer) < Mathf.CeilToInt(before)) Sfx.Play(Sound.Tick);
            if (_stateTimer > 0f) return 0f;

            ClearReversalDial();
            _reversalCooldown = ReversalInterval;

            Vector2 shadow;
            bool caught = _player.AreShadowsAnchored && _player.TryGetShadow(out shadow)
                && Mathf.Abs(shadow.x - _body.position.x) <= ReversalAnchorRange + _size.x * 0.5f
                && Mathf.Abs(shadow.y - _body.position.y) <= _size.y;
            if (caught)
            {
                Sfx.Play(Sound.Shatter);
                Stagger(ReversalStunTime);
                _game.OnGimmickSolved(this, "시간이 붙잡혔다!");
                return 0f;
            }

            Sfx.Play(Sound.Freeze);
            _game.Feel.Flash(new Color(0.7f, 0.95f, 1f, 1f), 0.5f, 0.5f);
            float before6 = HealthAgo(ReversalLookback);
            if (before6 > _health)
            {
                float healed = Mathf.Min(MaxHealth, before6) - _health;
                _health += healed;
                UpdateHealthBar();
                _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.1f), "되감기 +" + Mathf.RoundToInt(healed), HealTextColor);
            }
            return Rest(0.8f);
        }

        /// <summary>seconds 전의 체력. 기록이 모자라면 가장 오래된 기록을 쓴다.</summary>
        private float HealthAgo(float seconds)
        {
            if (_healthHistory == null || _historyCount == 0) return _health;
            int steps = Mathf.Min(Mathf.RoundToInt(seconds / HistoryStep), _historyCount - 1);
            int index = (_historyIndex - 1 - steps + HistorySize * 2) % HistorySize;
            return _healthHistory[index];
        }

        private void ClearReversalDial()
        {
            if (_reversalDial != null) Destroy(_reversalDial.gameObject);
            _reversalDial = null;
            _reversalHand = null;
        }
    }
}
