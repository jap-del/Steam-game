using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 보스의 기믹. 체력만 깎으면 이기는 싸움이 아니라, 무엇을 해야 하는지 알아내고 풀어야 이기는 싸움이다.
    /// 처음에는 규칙만 짧게 알려 주고, 실패할수록 화면 위쪽의 힌트가 조금씩 구체적으로 바뀐다.
    ///
    /// 봉인의 수호자 (1스테이지 보스): 체력이 절반 아래로 내려가면 보호막을 치고 양쪽 발판에 봉인석을 세운다.
    ///   봉인석은 맞으면 잠깐 빛나고, 두 봉인석이 동시에 빛나면 보호막이 깨진다. 분신이 2초 늦게 같은 공격을
    ///   한다는 것, 또는 고정한 분신이 함께 공격한다는 것을 쓰면 풀린다.
    /// 쌍둥이 수문장 (2스테이지 중간 보스): 하나를 쓰러뜨려도 짝이 살아 있으면 몇 초 뒤 다시 일어난다.
    ///   짝이 쓰러져 있는 동안 남은 하나는 몰아친다. 둘의 체력을 맞춰 깎다가 거의 동시에 쓰러뜨려야 한다.
    /// 백광의 군주 (2스테이지 보스): 세 단계로 싸운다.
    ///   1단계: 두 번 돌진, 빛기둥, 넓은 탄, 내려찍기.
    ///   2단계 (체력 65%): 빛의 갑옷을 둘러 보통 공격이 통하지 않는다. 대신 분신에게 빛의 표식을 붙이는데,
    ///     표식이 군주 곁에서 터지면 갑옷이 깨지고 군주가 잠깐 무너진다. 무너진 동안에는 더 아프게 맞는다.
    ///     얼마 뒤 갑옷이 다시 생긴다. 표식이 터질 때 나도 가까이 있으면 크게 다친다.
    ///   3단계 (체력 30%): 일식. 공격이 빨라지고 빛기둥이 두 번 내린다. 가끔 긴 예고 뒤 방 전체에 심판의 빛을
    ///     내리는데, 내 분신과 겹쳐 서 있어야(분신의 그림자 속에 있어야) 피할 수 있다. 대시로는 피할 수 없다.
    /// </summary>
    public sealed partial class Enemy
    {
        // ---- 봉인의 수호자 ----
        private const float SealHintDelay = 20f;
        private const float SealStunTime = 3.5f;

        // ---- 쌍둥이 수문장 ----
        private const float TwinReviveTime = 4.5f;
        private const float TwinReviveHealth = 0.5f;
        private const float TwinMoonKeepAway = 5.5f;
        private const float TwinMoonApproach = 10f;

        // ---- 백광의 군주 ----
        private const float LordArmorAt = 0.65f;
        private const float LordEclipseAt = 0.3f;
        private const float MarkFuse = 4.5f;
        private const float MarkRadius = 2.4f;
        private const int MarkDamage = 2;
        private const float MarkInterval = 6f;
        private const float ArmorStunTime = 4f;
        /// <summary>갑옷이 깨진 뒤 다시 생길 때까지의 시간. 무너져 있는 시간을 포함한다.</summary>
        private const float ArmorDownTime = 10f;
        /// <summary>심판의 예고 시간. 화면의 카운트다운도 이 값을 쓴다.</summary>
        public const float JudgmentCastTime = 3.6f;
        private const float JudgmentInterval = 13f;
        private const int JudgmentDamage = 2;
        private const float PillarWarnTime = 0.9f;
        private const float PillarSpacing = 4.5f;
        private const float SecondWaveDelay = 0.8f;

        /// <summary>기믹을 풀어 보스가 무너져 있는 동안 받는 피해 배율.</summary>
        private const float StaggerDamageScale = 1.5f;

        private static readonly Color SealAuraColor = new Color(0.5f, 0.95f, 1f, 0.3f);
        private static readonly Color ArmorAuraColor = new Color(1f, 0.9f, 0.5f, 0.35f);
        private static readonly Color SealTextColor = new Color(0.6f, 1f, 1f, 1f);
        private static readonly Color ArmorTextColor = new Color(1f, 0.92f, 0.6f, 1f);
        private static readonly Color MarkRingColor = new Color(1f, 0.95f, 0.65f, 0.8f);
        private static readonly Color MarkFillColor = new Color(1f, 0.95f, 0.65f, 0.12f);
        private static readonly Color ShadowPoolColor = new Color(0.12f, 0.04f, 0.22f, 0.85f);
        private static readonly Color ShadowTextColor = new Color(0.75f, 0.6f, 1f, 1f);
        private static readonly Color DownedTextColor = new Color(0.7f, 0.7f, 0.75f, 1f);

        private static readonly float[] HalfMark = { 0.5f };
        private static readonly float[] LordMarks = { LordArmorAt, LordEclipseAt };

        private SpriteRenderer _aura;
        private float _staggerTimer;
        private int _lungesLeft;

        /// <summary>보스 발이 플레이어 발보다 이만큼 높이 있으면 발판을 뚫고 내려온다.</summary>
        private const float DropHeight = 1.2f;
        private Collider2D _dropThrough;

        private bool _sealed;
        private SealStone[] _seals;
        private float _sealTime;
        private bool _sealHintShown;

        private Enemy _twin;
        private bool _downed;
        private float _downTimer;

        private int _phase = 1;
        private bool _armored;
        private float _armorDownTimer;
        private float _markCooldown;
        private bool _markArmed;
        private float _markFuse;
        private SpriteRenderer _markRing;
        private SpriteRenderer _markFill;
        private int _failedMarks;
        private float _judgmentCooldown;
        private SpriteRenderer _shadowPool;
        private int _failedJudgments;
        private float _secondWaveTimer;
        private float _secondWaveCenter;

        // ---- 화면 표시에 쓰는 정보 ----

        /// <summary>보스 이름. 등장 연출과 체력 막대에 쓴다.</summary>
        public string BossName
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.MidBoss: return "성채의 문지기";
                    case EnemyKind.Boss: return "봉인의 수호자";
                    case EnemyKind.TwinSun: return "해의 수문장";
                    case EnemyKind.TwinMoon: return "달의 수문장";
                    case EnemyKind.LightLord: return "백광의 군주";
                    case EnemyKind.ClockKnight: return "태엽 기사";
                    case EnemyKind.ClockMaster: return "시계탑의 주인";
                    default: return "";
                }
            }
        }

        /// <summary>체력 막대 옆에 붙이는 지금 상태. 없으면 null.</summary>
        public string BarStatus
        {
            get
            {
                if (_downed) return "쓰러짐 " + Mathf.Max(0f, _downTimer).ToString("0.0");
                if (_sealed) return "보호막";
                if (_staggerTimer > 0f) return "무너짐";
                if (_armored) return "빛의 갑옷";
                string clock = ClockBarStatus;
                if (clock != null) return clock;
                if (Kind == EnemyKind.LightLord && _phase >= 3) return "일식";
                if (_twin != null && _twin._downed) return "격노";
                if (_enrageAnnounced) return "분노";
                return null;
            }
        }

        /// <summary>체력 막대에 표시할 단계의 경계(체력 비율). 없으면 null.</summary>
        public float[] PhaseMarks
        {
            get
            {
                if (Kind == EnemyKind.LightLord) return LordMarks;
                if (Kind == EnemyKind.Boss || Kind == EnemyKind.MidBoss) return HalfMark;
                return ClockPhaseMarks;
            }
        }

        /// <summary>기믹을 풀어 보스가 무너져 있는지. 체력 막대를 밝게 깜빡인다.</summary>
        public bool IsStaggered => _staggerTimer > 0f;
        public bool IsDowned => _downed;
        public bool HasMark => _markArmed && _markRing != null;
        public Vector2 MarkPosition => _markRing != null ? (Vector2)_markRing.transform.position : (Vector2)transform.position;
        public float MarkRemaining => _markArmed ? Mathf.Max(0f, _markFuse) : 0f;

        /// <summary>심판까지 남은 시간. 심판을 준비하고 있지 않으면 0.</summary>
        public float JudgmentRemaining => _state == State.Windup && _pattern == Pattern.Judgment ? Mathf.Max(0f, _stateTimer) : 0f;

        // ---- Enemy.cs에서 부르는 것 ----

        private void BuildRaid()
        {
            BuildClock();
            if (Kind != EnemyKind.Boss && Kind != EnemyKind.LightLord) return;

            // 보호막이나 갑옷을 두르면 몸 둘레에 빛이 생긴다.
            _aura = RaidArt.Create("Aura", transform, transform.position, _size * 1.6f, RaidArt.Disc, Kind == EnemyKind.Boss ? SealAuraColor : ArmorAuraColor, 7);
            _aura.enabled = false;
        }

        /// <summary>물리 틱마다 부른다. true이면 이번 틱의 움직임과 공격을 건너뛴다.</summary>
        private bool TickRaid()
        {
            float dt = Time.fixedDeltaTime;
            if (_staggerTimer > 0f) _staggerTimer -= dt;

            if (_downed)
            {
                _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
                _downTimer -= dt;
                if (_downTimer <= 0f) Revive();
                return true;
            }

            if (_sealed) TickSeal(dt);
            if (Kind == EnemyKind.LightLord) TickLord(dt);
            if (IsClockBoss) TickClock(dt);
            if (IsBoss) TickDropThrough();
            return false;
        }

        /// <summary>
        /// 보스가 발판 위에 있고 플레이어가 아래에 있으면 발판을 뚫고 내려온다.
        /// 공격 중에는 하지 않고, 쫓거나 숨을 고를 때만 한다.
        /// </summary>
        private void TickDropThrough()
        {
            if (_dropThrough != null)
            {
                _body.WakeUp();
                // 보스 몸이 발판과 완전히 떨어진 뒤에만 다시 밟을 수 있게 한다.
                // 몸이 발판에 걸친 채로 되돌리면, 물리 엔진이 가장 가까운 쪽인 발판 위로 보스를 밀어 올린다.
                // 발판 위아래로 여유를 두어, 막 떨어지기 시작한 순간에 되돌리지 않는다.
                Bounds platformBounds = _dropThrough.bounds;
                platformBounds.Expand(new Vector3(0.1f, 0.4f, 0f));
                if (_collider.bounds.Intersects(platformBounds)) return;
                // 그사이 깨진 유리 발판이면 다시 생길 때까지 기다렸다가 되돌린다.
                if (!_dropThrough.enabled) return;
                Physics2D.IgnoreCollision(_collider, _dropThrough, false);
                _dropThrough = null;
                return;
            }

            if (_state != State.Chase && _state != State.Recover) return;
            if (Mathf.Abs(_body.linearVelocity.y) > 0.5f) return;

            if (!PlayerIsBelow) return;

            Collider2D platform = OneWayPlatformUnderFeet();
            if (platform == null) return;
            Physics2D.IgnoreCollision(_collider, platform, true);
            _dropThrough = platform;
            // 가만히 서 있던 몸은 물리 엔진이 재워 두어서 중력을 받지 않는다. 깨우고 아래로 살짝 밀어 준다.
            _body.WakeUp();
            _body.linearVelocity = new Vector2(_body.linearVelocity.x, -3f);
        }

        /// <summary>플레이어의 발이 보스의 발보다 확실히 아래에 있는지.</summary>
        private bool PlayerIsBelow
        {
            get
            {
                float bossFeet = _body.position.y - _size.y * 0.5f;
                float playerFeet = _player.Position.y - PlayerController.BodySize.y * 0.5f;
                return bossFeet - playerFeet >= DropHeight;
            }
        }

        /// <summary>
        /// 그쪽으로 걸어가도 되는지. 앞에 바닥이 있으면 된다.
        /// 바닥이 없어도 플레이어가 아래에 있고 떨어질 곳에 땅이 있으면 발판 끝에서 걸어 내려간다.
        /// </summary>
        private bool CanWalkToward(int direction)
        {
            if (HasGroundToward(direction)) return true;
            if (!PlayerIsBelow) return false;
            Vector2 ahead = _body.position + new Vector2(direction * (_size.x * 0.5f + 0.35f), -_size.y * 0.5f + 0.1f);
            return Combat.SolidDistanceBelow(ahead, 12f) >= 0f;
        }

        /// <summary>발밑이 아래에서 뚫고 올라갈 수 있는 발판이면 그 발판을, 아니면 null을 준다.</summary>
        private Collider2D OneWayPlatformUnderFeet()
        {
            // 발판 끝에 걸쳐 서 있어도 찾도록 발바닥 너비만큼 살핀다.
            Vector2 below = _body.position + new Vector2(0f, -_size.y * 0.5f - 0.1f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(below, new Vector2(_size.x * 0.9f, 0.15f), 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].usedByEffector && hits[i].GetComponent<PlatformEffector2D>() != null) return hits[i];
            }
            return null;
        }

        /// <summary>true이면 이 공격은 막혔다.</summary>
        private bool BlockedByRaid()
        {
            if (_downed) return true;
            if (!_sealed && !_armored) return false;

            Vector2 above = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 0.6f);
            Color color = _sealed ? SealTextColor : ArmorTextColor;
            _game.ShowPopup(above, _sealed ? "보호막" : "갑옷", color);
            _flashColor = color;
            _flashTimer = 0.08f;
            _game.Feel.BlockedHit(transform.position);
            return true;
        }

        private float DamageTakenScale => _staggerTimer > 0f ? StaggerDamageScale : 1f;

        private Color RaidTint(Color color)
        {
            if (_downed) return new Color(_baseColor.r * 0.3f, _baseColor.g * 0.3f, _baseColor.b * 0.35f, 1f);
            return color;
        }

        private void UpdateRaidVisuals()
        {
            if (_aura != null)
            {
                bool show = _sealed || _armored;
                _aura.enabled = show;
                if (show)
                {
                    Color color = _sealed ? SealAuraColor : ArmorAuraColor;
                    color.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                    _aura.color = color;
                }
            }

            if (!_markArmed && _shadowPool == null) return;

            Vector2 shadow = Vector2.zero;
            bool hasShadow = _player != null && _player.TryGetShadow(out shadow);
            if (!hasShadow) return;

            if (_markRing != null)
            {
                // 표식은 분신을 따라다닌다. 터질 때가 가까워질수록 빨리 깜빡인다.
                var position = new Vector3(shadow.x, shadow.y, 0f);
                _markRing.transform.position = position;
                _markFill.transform.position = position;
                float speed = _markFuse < 1.5f ? 18f : 6f;
                Color ring = MarkRingColor;
                ring.a *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * speed));
                _markRing.color = ring;
            }

            if (_shadowPool != null)
            {
                _shadowPool.transform.position = new Vector3(shadow.x, shadow.y - PlayerController.BodySize.y * 0.5f + 0.05f, 0f);
            }
        }

        private void ClearRaidObjects()
        {
            ClearClockObjects();
            ClearMark();
            ClearShadowPool();
            if (_seals != null)
            {
                for (int i = 0; i < _seals.Length; i++)
                {
                    if (_seals[i] != null) Destroy(_seals[i].gameObject);
                }
                _seals = null;
            }
        }

        /// <summary>기믹을 풀었다. 보스가 seconds 동안 아무것도 못 하고 더 아프게 맞는다.</summary>
        private void Stagger(float seconds)
        {
            _staggerTimer = seconds;
            _lungesLeft = 0;
            Enter(State.Recover, seconds);
            if (_body != null) _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        }

        // ---- 공격 고르기 ----

        private bool IsBossEnraged
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.LightLord:
                    case EnemyKind.ClockMaster: return _phase >= 3;
                    // 짝이 쓰러져 있는 동안 남은 쌍둥이는 몰아친다.
                    case EnemyKind.TwinSun:
                    case EnemyKind.TwinMoon: return _twin != null && _twin._downed;
                    default: return HealthRatio < 0.5f;
                }
            }
        }

        private float WindupFor(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Judgment: return JudgmentCastTime;
                case Pattern.Mark: return 0.7f;
                case Pattern.Pillars: return 0.45f;
                case Pattern.Cage: return CageWindup;
                default: return (Kind == EnemyKind.LightLord || Kind == EnemyKind.ClockMaster) && _phase >= 3 ? 0.42f : BossWindupTime;
            }
        }

        private float BossCooldown(bool enraged)
        {
            switch (Kind)
            {
                case EnemyKind.LightLord: return _phase >= 3 ? 0.4f : 0.75f;
                case EnemyKind.ClockMaster: return _phase >= 3 ? 0.4f : 0.7f;
                case EnemyKind.TwinSun:
                case EnemyKind.TwinMoon: return enraged ? 0.3f : 0.8f;
                default: return enraged ? 0.45f : 0.9f;
            }
        }

        private int SpreadCount()
        {
            switch (Kind)
            {
                case EnemyKind.Boss:
                case EnemyKind.TwinMoon:
                case EnemyKind.ClockKnight: return 5;
                case EnemyKind.LightLord: return _phase >= 3 ? 9 : 7;
                case EnemyKind.ClockMaster: return _phase >= 3 ? 7 : 5;
                default: return 3;
            }
        }

        private float ChaseSpeed(Vector2 toPlayer)
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            if (Kind == EnemyKind.TwinMoon)
            {
                // 달은 거리를 둔다. 너무 가까우면 물러나고, 너무 멀면 다가간다.
                int away = toPlayer.x >= 0f ? -1 : 1;
                if (distanceX < TwinMoonKeepAway && CanWalkToward(away)) return away * MoveSpeed;
                if (distanceX > TwinMoonApproach && CanWalkToward(_facing)) return _facing * MoveSpeed;
                return 0f;
            }
            return distanceX > 3f && CanWalkToward(_facing) ? _facing * MoveSpeed : 0f;
        }

        private bool HasGroundToward(int direction)
        {
            Vector2 probe = _body.position + new Vector2(direction * (_size.x * 0.5f + 0.35f), -_size.y * 0.5f - 0.3f);
            return Combat.IsSolidAt(probe);
        }

        private bool TryRaidPattern(Vector2 toPlayer, out Pattern pattern)
        {
            pattern = Pattern.Lunge;
            switch (Kind)
            {
                case EnemyKind.TwinSun:
                    pattern = toPlayer.y > 1.5f || _patternIndex % 2 == 0 ? Pattern.Slam : Pattern.Lunge;
                    return true;
                case EnemyKind.TwinMoon:
                    pattern = _patternIndex % 2 == 0 ? Pattern.Bombs : Pattern.Spread;
                    return true;
                case EnemyKind.LightLord:
                    pattern = NextLordPattern(toPlayer);
                    return true;
                case EnemyKind.ClockKnight:
                case EnemyKind.ClockMaster:
                    pattern = NextClockPattern(toPlayer);
                    return true;
                default:
                    return false;
            }
        }

        private Pattern NextLordPattern(Vector2 toPlayer)
        {
            // 기믹 공격은 때가 되면 다른 공격보다 먼저 쓴다. 분신이 없으면 기다린다.
            Vector2 shadow;
            bool hasShadow = _player.TryGetShadow(out shadow);
            if (_phase == 2 && _armored && !_markArmed && _markCooldown <= 0f && hasShadow) return Pattern.Mark;
            if (_phase >= 3 && _judgmentCooldown <= 0f && hasShadow) return Pattern.Judgment;

            if (toPlayer.y > 1.5f) return _patternIndex % 2 == 0 ? Pattern.Pillars : Pattern.Slam;
            Pattern[] cycle = { Pattern.Lunge, Pattern.Pillars, Pattern.Spread, Pattern.Slam };
            return cycle[_patternIndex % cycle.Length];
        }

        private void OnPatternWindup(Pattern pattern)
        {
            // 백광의 군주의 돌진은 두 번 이어진다.
            _lungesLeft = Kind == EnemyKind.LightLord && pattern == Pattern.Lunge ? 1 : 0;
            if (pattern == Pattern.Judgment) BeginJudgment();
        }

        // ---- 공격 ----

        /// <summary>플레이어 자리와 그 양옆에 폭탄 세 개를 던진다. 옆으로 크게 벗어나거나 대시로 피한다.</summary>
        private void ThrowBombs()
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector2 target = _player.Position + new Vector2(i * 2.6f, 0f);
                _game.SpawnBomb(_body.position + Vector2.up * (_size.y * 0.3f), target, 1f, BombRadius);
            }
        }

        private void CallPillars()
        {
            float center = _player.Position.x;
            SpawnPillarWave(center);
            if (_phase >= 3)
            {
                // 일식 중에는 첫 기둥들 사이로 두 번째 기둥들이 이어서 내린다.
                _secondWaveTimer = SecondWaveDelay;
                _secondWaveCenter = center + PillarSpacing * 0.5f;
            }
        }

        private void SpawnPillarWave(float center)
        {
            Transform root = _game.RoomRoot;
            if (root == null) return;

            float width = _game.RoomWidth;
            for (int i = -3; i <= 3; i++)
            {
                float x = center + i * PillarSpacing;
                if (x < 0.8f || x > width - 0.8f) continue;
                LightPillar.Spawn(root, _player, x, PillarWarnTime);
            }
        }

        // ---- 봉인의 수호자: 보호막과 봉인석 ----

        private void RaiseSeal()
        {
            IList<Vector2> points = _game.GimmickPoints;
            Transform root = _game.RoomRoot;
            if (points == null || points.Count < 2 || root == null)
            {
                _game.OnBossEnraged(this);
                return;
            }

            _sealed = true;
            _sealTime = 0f;
            _sealHintShown = false;
            _seals = new SealStone[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                _seals[i] = SealStone.Create(root, points[i]);
            }
            _game.OnBossPhase(this, "봉인의 보호막!", "보호막: 양쪽 봉인석이 동시에 빛나야 깨진다");
        }

        private void TickSeal(float dt)
        {
            _sealTime += dt;
            if (!_sealHintShown && _sealTime >= SealHintDelay)
            {
                _sealHintShown = true;
                _game.SetBossHint("보호막: 분신은 2초 전의 내 공격을 그대로 따라 한다. S로 고정한 분신은 내가 공격할 때 함께 친다");
            }

            if (_seals == null) return;
            for (int i = 0; i < _seals.Length; i++)
            {
                if (_seals[i] == null || !_seals[i].IsLit) return;
            }
            BreakSeal();
        }

        private void BreakSeal()
        {
            _sealed = false;
            for (int i = 0; i < _seals.Length; i++)
            {
                if (_seals[i] != null) _seals[i].Shatter();
            }
            _seals = null;
            Stagger(SealStunTime);
            _game.OnGimmickSolved(this, "봉인이 깨졌다!");
        }

        // ---- 쌍둥이 수문장 ----

        /// <summary>두 쌍둥이를 서로의 짝으로 묶는다.</summary>
        public static void LinkTwins(Enemy a, Enemy b)
        {
            if (a == null || b == null || a == b) return;
            a._twin = b;
            b._twin = a;
        }

        /// <summary>체력이 다 닳았을 때 부른다. 쌍둥이면 true를 돌려주고 쓰러지거나, 짝과 함께 끝난다.</summary>
        private bool TryGoDown()
        {
            if (_twin == null || _twin._dead) return false;

            if (_twin._downed)
            {
                // 짝이 쓰러져 있는 동안 이쪽도 쓰러뜨렸다. 둘 다 끝난다.
                Enemy twin = _twin;
                _game.OnGimmickSolved(this, "함께 쓰러뜨렸다!");
                twin.Die();
                Die();
                return true;
            }

            _downed = true;
            _downTimer = TwinReviveTime;
            _health = 0f;
            _lungesLeft = 0;
            Enter(State.Recover, TwinReviveTime);
            UpdateHealthBar();
            _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 0.6f), "쓰러짐", DownedTextColor);
            Sfx.Play(Sound.EnemyDie);
            _game.Feel.Shake(0.15f, 0.15f);
            return true;
        }

        private void Revive()
        {
            _downed = false;
            _health = MaxHealth * TwinReviveHealth;
            _state = State.Chase;
            _cooldown = 0.8f;
            UpdateHealthBar();
            _game.OnTwinRevived(this);
        }

        // ---- 백광의 군주 ----

        private void TickLord(float dt)
        {
            if (_secondWaveTimer > 0f)
            {
                _secondWaveTimer -= dt;
                if (_secondWaveTimer <= 0f) SpawnPillarWave(_secondWaveCenter);
            }

            if (_phase == 2)
            {
                if (_armored)
                {
                    if (!_markArmed && _markCooldown > 0f) _markCooldown -= dt;
                }
                else
                {
                    _armorDownTimer -= dt;
                    if (_armorDownTimer <= 0f)
                    {
                        _armored = true;
                        _markCooldown = 2.5f;
                        _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.2f), "갑옷이 다시 빛난다", ArmorTextColor);
                        Sfx.Play(Sound.Mark);
                    }
                }
            }

            bool judging = _state == State.Windup && _pattern == Pattern.Judgment;
            if (_phase >= 3 && _judgmentCooldown > 0f && !judging) _judgmentCooldown -= dt;

            if (_markArmed)
            {
                float before = _markFuse;
                _markFuse -= dt;
                // 1초마다 똑딱인다.
                if (_markFuse > 0f && Mathf.CeilToInt(_markFuse) < Mathf.CeilToInt(before)) Sfx.Play(Sound.Warn);
                if (_markFuse <= 0f) DetonateMark();
            }
        }

        /// <summary>체력이 단계의 경계를 넘으면 다음 단계로 간다. 한 번 맞을 때 한 단계씩만 넘어간다.</summary>
        private void CheckRaidPhases()
        {
            CheckClockPhases();
            if (Kind != EnemyKind.LightLord) return;

            if (_phase == 1 && HealthRatio <= LordArmorAt)
            {
                _phase = 2;
                _armored = true;
                _markCooldown = 2f;
                _game.OnBossPhase(this, "빛의 갑옷!", "빛의 갑옷: 보통 공격은 통하지 않는다");
            }
            else if (_phase == 2 && HealthRatio <= LordEclipseAt)
            {
                _phase = 3;
                _armored = false;
                ClearMark();
                _judgmentCooldown = 3f;
                _baseColor = Color.Lerp(_baseColor, new Color(0.35f, 0.25f, 0.45f, 1f), 0.5f);
                _game.OnBossPhase(this, "일식!", "일식: 백광의 군주가 심판을 준비한다");
            }
        }

        private void PlaceMark()
        {
            // 예고 중에 일식으로 넘어갔으면 표식을 붙이지 않는다.
            if (_phase != 2) return;

            Vector2 at;
            Transform root = _game.RoomRoot;
            if (root == null || !_player.TryGetShadow(out at))
            {
                _markCooldown = 1f;
                return;
            }

            ClearMark();
            _markArmed = true;
            _markFuse = MarkFuse;
            Vector2 diameter = Vector2.one * MarkRadius * 2f;
            _markFill = RaidArt.Create("MarkFill", root, at, diameter, RaidArt.Disc, MarkFillColor, 5);
            _markRing = RaidArt.Create("MarkRing", root, at, diameter, RaidArt.Ring, MarkRingColor, 6);
            Sfx.Play(Sound.Mark);
            _game.ShowPopup(at + Vector2.up * 1.4f, "빛의 표식", ArmorTextColor);
        }

        private void DetonateMark()
        {
            Vector2 at = MarkPosition;
            Vector2 shadow;
            if (_player.TryGetShadow(out shadow)) at = shadow;
            ClearMark();
            _markCooldown = MarkInterval;

            _game.ShowBlast(at, MarkRadius, new Color(1f, 0.95f, 0.7f, 0.55f));
            _game.Feel.Explosion(at);
            if ((_player.Position - at).magnitude <= MarkRadius + 0.3f)
            {
                _player.TakeDamage(MarkDamage, at.x);
            }
            if (!_armored) return;

            Vector2 offset = _body.position - at;
            bool caught = Mathf.Abs(offset.x) <= MarkRadius + _size.x * 0.5f && Mathf.Abs(offset.y) <= MarkRadius + _size.y * 0.5f;
            if (caught)
            {
                BreakArmor();
                return;
            }

            // 실패할수록 힌트가 구체적으로 바뀐다.
            _failedMarks++;
            if (_failedMarks == 1) _game.SetBossHint("빛의 갑옷: 표식은 내가 아니라 분신에게 붙는다");
            else if (_failedMarks == 2) _game.SetBossHint("빛의 갑옷: 분신의 표식이 백광의 군주 곁에서 터지면…");
            else if (_failedMarks == 4) _game.SetBossHint("빛의 갑옷: 분신을 군주 곁으로 데려가고, 터지기 전에 나는 빠져나오자. S로 분신을 고정해도 된다");
        }

        private void BreakArmor()
        {
            _armored = false;
            _armorDownTimer = ArmorDownTime;
            Stagger(ArmorStunTime);
            _game.OnGimmickSolved(this, "갑옷이 깨졌다!");
        }

        private void ClearMark()
        {
            _markArmed = false;
            if (_markRing != null) Destroy(_markRing.gameObject);
            if (_markFill != null) Destroy(_markFill.gameObject);
            _markRing = null;
            _markFill = null;
        }

        /// <summary>심판의 예고를 시작한다. 분신 발밑에 짙은 그림자가 드리운다.</summary>
        private void BeginJudgment()
        {
            ClearShadowPool();
            Sfx.Play(Sound.BossIntro, 0.8f);
            _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.6f), "빛이 닿지 않는 곳으로!", ArmorTextColor);

            Vector2 shadow;
            Transform root = _game.RoomRoot;
            if (root == null || !_player.TryGetShadow(out shadow)) return;
            Vector2 feet = shadow + new Vector2(0f, -PlayerController.BodySize.y * 0.5f + 0.05f);
            _shadowPool = RaidArt.Create("ShadowPool", root, feet, new Vector2(1.9f, 0.5f), RaidArt.Disc, ShadowPoolColor, 3);
        }

        private void Judge()
        {
            ClearShadowPool();
            _judgmentCooldown = JudgmentInterval;
            Sfx.Play(Sound.Judgment);
            _game.Feel.Flash(new Color(1f, 0.97f, 0.85f, 1f), 0.85f, 0.7f);
            _game.Feel.Shake(0.4f, 0.4f);

            if (_player.IsInShadow())
            {
                _game.ShowPopup(_player.Position + Vector2.up * 1.6f, "그림자 속에 숨었다", ShadowTextColor);
                return;
            }

            // 방 전체를 덮는 빛이라 대시나 무적 시간으로는 피할 수 없다.
            _player.TakeDamage(JudgmentDamage, _body.position.x, true);
            _failedJudgments++;
            if (_failedJudgments == 1) _game.SetBossHint("일식: 심판의 빛은 그림자 속까지는 닿지 않는다");
            else if (_failedJudgments == 2) _game.SetBossHint("일식: 심판 때 바닥에 드리운 내 분신의 그림자 위에 서 있자");
        }

        private void ClearShadowPool()
        {
            if (_shadowPool != null) Destroy(_shadowPool.gameObject);
            _shadowPool = null;
        }
    }
}
