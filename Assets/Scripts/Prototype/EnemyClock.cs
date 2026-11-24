using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 3스테이지 "멈춘 시계탑"의 보스 기믹. 아직 기믹에 쓰지 않았던 자리 바꾸기(K)가 열쇠다.
    ///
    /// 태엽 기사 (3스테이지 중간 보스): 가끔 플레이어 둘레에 시계 감옥을 세운다. 감옥의 벽은 걷거나 대시해서는
    ///   지나갈 수 없고, 시계판의 바늘이 한 바퀴 돌면 시간이 멈추며 기사가 감옥 안을 벤다.
    ///   감옥 밖에 있는 분신과 자리를 바꾸면 빠져나오고, 그러면 기사가 빈 감옥에 뛰어들었다가 멈춘 시간에 갇혀 무너진다.
    ///   분신은 2초 늦게 따라오므로 갇히자마자 바꿔야 한다. 미리 분신을 멀리 고정해 두어도 된다.
    /// 시계탑의 주인 (3스테이지 보스, 마지막 보스): 세 단계로 싸운다.
    ///   1단계: 돌진, 톱니바퀴, 탄, 내려찍기 사이에 시계 감옥을 쓴다.
    ///   2단계 (체력 70%): 시계 방패. 보고 있는 쪽에서 오는 공격은 모두 막는다. 천천히 돌아서므로
    ///     등 뒤를 노린다. 등 뒤를 여러 번 치면 방패가 깨지고 잠깐 무너진다. 얼마 뒤 방패를 다시 든다.
    ///     자리 바꾸기로 순식간에 등 뒤로 가거나, 분신을 등 뒤에 고정하고 공격한다.
    ///   3단계 (체력 35%, 자정): 방패와 감옥을 함께 쓰고, 공격이 빨라지고, 톱니바퀴가 두 번 굴러온다.
    /// </summary>
    public sealed partial class Enemy
    {
        // ---- 시계 감옥 ----
        /// <summary>감옥이 선 뒤 시간이 멈출 때까지(초). 화면의 카운트다운도 이 값을 쓴다.</summary>
        public const float CageTime = 3.4f;
        private const float CageWindup = 0.7f;
        private const float CageFirstDelay = 5f;
        private const float KnightCageInterval = 13f;
        private const float MasterCageInterval = 15f;
        private const float MidnightCageInterval = 11f;
        private const float CageStunTime = 4f;
        /// <summary>자리 바꾸기를 이만큼 안에 다시 쓸 수 있을 때만 감옥을 세운다. 빠져나갈 길이 없는 감옥은 세우지 않는다.</summary>
        private const float CageSwapReadiness = 1f;

        // ---- 시계 방패 ----
        private const float MasterGuardAt = 0.7f;
        private const float MasterMidnightAt = 0.35f;
        /// <summary>방패를 든 동안 플레이어가 등 뒤에 이만큼 있어야 돌아선다.</summary>
        private const float GuardTurnTime = 0.7f;
        private const int GuardCracksToBreak = 5;
        private const float GuardBreakStunTime = 3.5f;
        /// <summary>방패가 깨진 뒤 다시 들 때까지의 시간. 무너져 있는 시간을 포함한다.</summary>
        private const float GuardDownTime = 10f;

        // ---- 톱니바퀴 ----
        private const float GearSpeed = 7.5f;
        private const float GearSize = 0.75f;
        private const float GearSecondWaveDelay = 0.6f;

        private static readonly Color CageTextColor = new Color(0.6f, 0.85f, 1f, 1f);
        private static readonly Color GuardTextColor = new Color(1f, 0.75f, 0.4f, 1f);
        private static readonly Color GuardColor = new Color(1f, 0.78f, 0.4f, 0.85f);
        private static readonly float[] MasterMarks = { MasterGuardAt, MasterMidnightAt };

        private ClockCage _cage;
        private float _cageCooldown;
        private bool _cageIntroduced;
        private int _failedCages;

        private bool _hasGuardPhase;
        private bool _guardUp;
        private float _guardDownTimer;
        private int _guardCracks;
        private int _guardBlocks;
        private float _guardTurnTimer;
        private float _gearWaveTimer;

        private bool IsClockBoss => Kind == EnemyKind.ClockKnight || Kind == EnemyKind.ClockMaster;

        /// <summary>시계탑의 주인이 방패를 들고 있는지. 무너져 있는 동안에는 막지 못한다.</summary>
        private bool IsClockGuarding => _guardUp && _staggerTimer <= 0f && !_dead;

        private bool IsCaging => _pattern == Pattern.Cage && _state == State.Attack && _cage != null;

        /// <summary>시간이 멈출 때까지 남은 시간. 감옥이 서 있지 않으면 0.</summary>
        public float CageRemaining => IsCaging ? Mathf.Max(0f, _stateTimer) : 0f;

        /// <summary>감옥의 가운데. 감옥이 없으면 보스 자리.</summary>
        public Vector2 CageCenter => _cage != null ? new Vector2(_cage.CenterX, StageBuilder.GroundTop) : (Vector2)transform.position;

        private string ClockBarStatus
        {
            get
            {
                if (!IsClockBoss) return null;
                if (IsCaging) return "시계 감옥";
                if (IsClockGuarding) return "시계 방패 " + _guardCracks + "/" + GuardCracksToBreak;
                if (Kind == EnemyKind.ClockMaster && _phase >= 3) return "자정";
                return null;
            }
        }

        private float[] ClockPhaseMarks => Kind == EnemyKind.ClockMaster ? MasterMarks : (Kind == EnemyKind.ClockKnight ? HalfMark : null);

        // ---- Enemy.cs와 EnemyRaid.cs에서 부르는 것 ----

        private void BuildClock()
        {
            if (!IsClockBoss) return;

            _cageCooldown = CageFirstDelay;
            if (Kind == EnemyKind.ClockMaster)
            {
                // 보고 있는 쪽에 드는 시계 방패. 방패병처럼 Update에서 보이고 숨기고 앞쪽으로 옮긴다.
                _shieldVisual = RaidArt.Create("ClockShield", transform, transform.position, new Vector2(0.5f, _size.y * 0.9f), RaidArt.Disc, GuardColor, 9);
                _shieldVisual.enabled = false;
            }
        }

        private void TickClock(float dt)
        {
            if (!IsClockBoss) return;

            if (_cageCooldown > 0f && !IsCaging && CageAllowed) _cageCooldown -= dt;

            if (_gearWaveTimer > 0f)
            {
                _gearWaveTimer -= dt;
                if (_gearWaveTimer <= 0f) SpawnGearWave();
            }

            if (_hasGuardPhase && !_guardUp)
            {
                _guardDownTimer -= dt;
                if (_guardDownTimer <= 0f)
                {
                    RaiseGuard();
                    _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.2f), "방패를 다시 든다", GuardTextColor);
                    Sfx.Play(Sound.Cage, 0.6f);
                }
            }
        }

        /// <summary>시계탑의 주인은 2단계에서는 방패에만 집중하고 감옥을 쓰지 않는다.</summary>
        private bool CageAllowed => Kind == EnemyKind.ClockKnight || _phase != 2;

        private void CheckClockPhases()
        {
            if (Kind != EnemyKind.ClockMaster) return;

            if (_phase == 1 && HealthRatio <= MasterGuardAt)
            {
                _phase = 2;
                _hasGuardPhase = true;
                RaiseGuard();
                _game.OnBossPhase(this, "시계 방패!", "시계 방패: 보고 있는 쪽에서 오는 공격은 막는다");
            }
            else if (_phase == 2 && HealthRatio <= MasterMidnightAt)
            {
                _phase = 3;
                _cageCooldown = 3f;
                if (!_guardUp) RaiseGuard();
                _baseColor = Color.Lerp(_baseColor, new Color(0.3f, 0.25f, 0.4f, 1f), 0.5f);
                _game.OnBossPhase(this, "자정!", "자정: 시계 방패를 든 채로 시계 감옥도 쓴다. 등 뒤를 노리고, 감옥에서는 자리를 바꿔 빠져나오자");
            }
        }

        private void ClearClockObjects()
        {
            if (_cage != null) Destroy(_cage.gameObject);
            _cage = null;
        }

        /// <summary>
        /// 쫓는 동안 플레이어 쪽으로 돈다. 방패를 든 시계탑의 주인은 천천히 돌아서서, 그동안은 걷지도 공격하지도 않는다.
        /// 플레이어 쪽을 보고 있으면 true.
        /// </summary>
        private bool TurnBossToward(Vector2 toPlayer)
        {
            int want = toPlayer.x >= 0f ? 1 : -1;
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

        private Pattern NextClockPattern(Vector2 toPlayer)
        {
            if (ReadyToCage()) return Pattern.Cage;

            if (toPlayer.y > 1.5f) return _patternIndex % 2 == 0 ? Pattern.Slam : Pattern.Spread;
            Pattern[] cycle = Kind == EnemyKind.ClockMaster
                ? new[] { Pattern.Lunge, Pattern.Gears, Pattern.Spread, Pattern.Slam }
                : new[] { Pattern.Lunge, Pattern.Spread, Pattern.Slam };
            return cycle[_patternIndex % cycle.Length];
        }

        private bool ReadyToCage()
        {
            if (_cageCooldown > 0f || !CageAllowed || _cage != null) return false;
            // 분신이 없거나 자리 바꾸기를 곧 쓸 수 없으면 빠져나갈 길이 없으므로 기다린다.
            Vector2 shadow;
            if (!_player.TryGetShadow(out shadow)) return false;
            return _player.SwapCooldownRemaining <= CageSwapReadiness;
        }

        private float CageInterval
        {
            get
            {
                if (Kind == EnemyKind.ClockKnight) return KnightCageInterval;
                return _phase >= 3 ? MidnightCageInterval : MasterCageInterval;
            }
        }

        /// <summary>감옥이 지금 다 서 있는 동안 돌아갈 때 받는 피해. 중간 보스는 가르쳐 주는 쪽이라 덜 아프다.</summary>
        private int CageDamage => IsFinalBoss ? 2 : 1;

        // ---- 시계 감옥 ----

        /// <summary>예고가 끝났다. 지금 플레이어가 서 있는 자리를 가운데로 감옥을 세운다.</summary>
        private bool SpawnCage()
        {
            Transform root = _game.RoomRoot;
            if (root == null) return false;

            float margin = ClockCage.HalfWidth + 0.6f;
            float width = _game.RoomWidth;
            float x = width > margin * 2f ? Mathf.Clamp(_player.Position.x, margin, width - margin) : _player.Position.x;
            _cage = ClockCage.Create(root, x, StageBuilder.GroundTop, CageTime, _collider);
            Sfx.Play(Sound.Cage);
            _game.Feel.Shake(0.2f, 0.2f);
            _game.ShowPopup(new Vector2(x, StageBuilder.GroundTop + 7.8f), "시계 감옥!", CageTextColor);

            if (!_cageIntroduced)
            {
                _cageIntroduced = true;
                if (_failedCages == 0) _game.SetBossHint("시계 감옥: 시간이 멈추기 전에 감옥에서 빠져나가야 한다");
            }
            return true;
        }

        /// <summary>감옥이 서 있는 동안. 보스는 바늘이 돌아가는 것을 지켜보며 1초마다 똑딱인다.</summary>
        private float TickCage()
        {
            float before = _stateTimer;
            _stateTimer -= Time.fixedDeltaTime;
            if (_stateTimer > 0f && Mathf.CeilToInt(_stateTimer) < Mathf.CeilToInt(before)) Sfx.Play(Sound.Tick);
            if (_stateTimer <= 0f) ResolveCage();
            return 0f;
        }

        /// <summary>시간이 멈췄다. 플레이어가 아직 감옥 안이면 베이고, 빠져나갔으면 보스가 자기 감옥에 갇힌다.</summary>
        private void ResolveCage()
        {
            _cageCooldown = CageInterval;
            if (_cage == null)
            {
                Enter(State.Recover, 0.5f);
                return;
            }

            ClockCage cage = _cage;
            _cage = null;
            Sfx.Play(Sound.Freeze);
            _game.Feel.Flash(new Color(0.6f, 0.85f, 1f, 1f), 0.55f, 0.5f);
            _game.Feel.Shake(0.3f, 0.3f);

            if (cage.Contains(_player.Position))
            {
                cage.Strike();
                _game.ShowBlast(new Vector2(cage.CenterX, StageBuilder.GroundTop + 1.5f), ClockCage.HalfWidth, new Color(0.7f, 0.9f, 1f, 0.55f));
                // 멈춘 시간 속에서 베는 것이라 대시나 무적 시간으로 피할 수 없다.
                _player.TakeDamage(CageDamage, cage.CenterX, true);
                Enter(State.Recover, 0.9f);

                _failedCages++;
                if (_failedCages == 1) _game.SetBossHint("시계 감옥: 걸어서도, 대시로도 벽을 지나갈 수 없다");
                else if (_failedCages == 2) _game.SetBossHint("시계 감옥: 감옥 밖에 있는 무언가와 자리를 바꿀 수 있다면…");
                else if (_failedCages == 3) _game.SetBossHint("시계 감옥: 갇히자마자 K로 분신과 자리를 바꾸자. 분신은 2초 늦게 따라오니 감옥에 따라 들어오기 전에. S로 분신을 멀리 고정해 둬도 된다");
                return;
            }

            // 플레이어는 이미 빠져나갔다. 보스가 빈 감옥 한가운데로 뛰어들었다가 멈춘 시간에 갇힌다.
            if (_body != null)
            {
                var inside = new Vector2(cage.CenterX, StageBuilder.GroundTop + _size.y * 0.5f + 0.05f);
                _game.Feel.Sparks(_body.position, CageTextColor, 8, 5f);
                _body.position = inside;
                transform.position = new Vector3(inside.x, inside.y, 0f);
                _body.linearVelocity = Vector2.zero;
            }
            cage.Freeze(CageStunTime);
            Stagger(CageStunTime);
            _game.OnGimmickSolved(this, "시간에 갇혔다!");
        }

        // ---- 시계 방패 ----

        private void RaiseGuard()
        {
            _guardUp = true;
            _guardCracks = 0;
            _guardTurnTimer = 0f;
        }

        /// <summary>보고 있는 쪽에서 온 공격을 방패로 막았다. 막힐수록 힌트가 구체적으로 바뀐다.</summary>
        private void OnClockGuardBlocked()
        {
            _guardBlocks++;
            if (_guardBlocks == 5) _game.SetBossHint("시계 방패: 등 뒤는 막지 못한다. 시계탑의 주인은 천천히 돌아선다");
            else if (_guardBlocks == 12) _game.SetBossHint("시계 방패: 등 뒤에 있는 분신과 K로 자리를 바꾸면 순식간에 뒤로 간다. 분신을 등 뒤에 S로 고정하고 공격해도 된다");
        }

        /// <summary>방패를 든 보스의 등 뒤를 쳤다. 금이 쌓이면 방패가 깨진다.</summary>
        private void OnClockBackHit()
        {
            if (!IsClockGuarding) return;

            _guardCracks++;
            Vector2 above = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.1f);
            _game.ShowPopup(above, "균열 " + _guardCracks + "/" + GuardCracksToBreak, GuardTextColor);
            if (_guardCracks < GuardCracksToBreak) return;

            _guardUp = false;
            _guardDownTimer = GuardDownTime;
            Sfx.Play(Sound.Shatter);
            _game.Feel.Sparks(transform.position, GuardColor, 14, 7f);
            Stagger(GuardBreakStunTime);
            _game.OnGimmickSolved(this, "방패가 깨졌다!");
        }

        // ---- 톱니바퀴 ----

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
    }
}
