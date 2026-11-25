using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 보스의 움직임과 공격 고르기. 보스마다 자기만의 공격을 쓰고, 다른 보스와 같은 공격은 쓰지 않는다.
    /// 모든 공격은 번쩍이는 예고와 이름 뒤에 나오고, 끝나면 빈틈이 생긴다.
    ///
    ///   성채의 문지기: 성문 돌진, 땅울림, 낙석
    ///   봉인의 수호자: 봉인 파동, 사슬, 봉인진
    ///   해의 수문장: 불꽃 질주, 태양 낙하 / 달의 수문장: 초승달, 월광탄
    ///   백광의 군주: 광선, 빛기둥, 빛의 창 (기믹: 표식, 심판)
    ///   태엽 기사: 순간 베기, 진자, 초침 (기믹: 시계 감옥)
    ///   시계탑의 주인: 톱니바퀴, 멈춘 탄막, 되감기 돌진, 시한 지뢰 (기믹: 역행)
    ///
    /// 공격과 기믹은 스테이지별로 EnemyCitadel.cs, EnemySanctum.cs, EnemyClock.cs에 있다.
    /// 기믹의 풀이는 화면에 알려 주지 않는다.
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>적이 쓰는 공격 종류. Lunge와 Slam은 잡몹(돌진형)만 쓴다.</summary>
        private enum Pattern
        {
            Lunge,
            Slam,
            // 성채의 문지기
            Ram,
            Quake,
            Rubble,
            // 봉인의 수호자
            SealWave,
            Chains,
            Runes,
            // 해의 수문장, 달의 수문장
            FlameDash,
            SunDive,
            Crescent,
            MoonBombs,
            // 백광의 군주
            Beam,
            Pillars,
            Spears,
            Mark,
            Judgment,
            // 태엽 기사
            Blink,
            Pendulum,
            SecondHand,
            Cage,
            // 시계탑의 주인
            Gears,
            Barrage,
            RewindDash,
            Mines,
            Reversal,
        }

        private const float BossNoticeRange = 18f;
        /// <summary>보스가 뛰어올라 덮칠 때 평지에서 공중에 떠 있는 시간.</summary>
        private const float LeapAirTime = 0.8f;
        /// <summary>스테이지 끝 보스의 몸에 부딪히면 잃는 체력. 다른 보스는 1이다.</summary>
        private const int FinalBossHeavyDamage = 2;
        private static readonly Color LeapMarkColor = new Color(1f, 0.55f, 0.2f, 1f);

        /// <summary>
        /// 보스: 거리를 좁히다가 자기 순서대로 공격을 고른다.
        /// 예고가 끝나면 공격을 시작하고, 몸이 움직이는 공격(돌진, 도약, 감옥, 역행)은 Attack 상태에서 이어 간다.
        /// </summary>
        private float TickBoss(Vector2 toPlayer)
        {
            float dt = Time.fixedDeltaTime;
            switch (_state)
            {
                case State.Idle:
                    if (Mathf.Abs(toPlayer.x) < BossNoticeRange)
                    {
                        _state = State.Chase;
                        _cooldown = 1f;
                    }
                    return 0f;

                case State.Chase:
                    // 시계 방패를 든 시계탑의 주인은 천천히 돌아선다. 돌아서는 동안에는 걷지도 공격하지도 않는다.
                    bool ready = TurnBossToward(toPlayer);
                    _cooldown -= dt;
                    if (_cooldown <= 0f && ready)
                    {
                        _patternIndex++;
                        _pattern = ChoosePattern(toPlayer);
                        Enter(State.Windup, WindupFor(_pattern));
                        Warn(_body.position + Vector2.up * (_size.y * 0.5f + 0.9f), PatternName(_pattern));
                        OnPatternWindup(_pattern);
                        return 0f;
                    }
                    return ready ? ChaseSpeed(toPlayer) : 0f;

                case State.Windup:
                    _stateTimer -= dt;
                    if (_stateTimer > 0f) return 0f;
                    return StartPattern(toPlayer);

                case State.Attack:
                    return TickPattern();

                default:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f)
                    {
                        _state = State.Chase;
                        _cooldown = BossCooldown();
                    }
                    return 0f;
            }
        }

        private Pattern ChoosePattern(Vector2 toPlayer)
        {
            // 플레이어가 발판 위에 있으면 바닥을 따라가는 공격은 닿지 않으므로 다른 공격을 고른다.
            bool high = toPlayer.y > 1.5f;
            switch (Kind)
            {
                case EnemyKind.MidBoss: return NextGatekeeperPattern(high);
                case EnemyKind.Boss: return NextGuardianPattern(high);
                case EnemyKind.TwinSun: return high || _patternIndex % 2 == 0 ? Pattern.SunDive : Pattern.FlameDash;
                case EnemyKind.TwinMoon: return _patternIndex % 2 == 0 ? Pattern.MoonBombs : Pattern.Crescent;
                case EnemyKind.LightLord: return NextLordPattern(high);
                case EnemyKind.ClockKnight: return NextKnightPattern(high);
                case EnemyKind.ClockMaster: return NextMasterPattern(high);
                default: return Pattern.Lunge;
            }
        }

        /// <summary>공격 예고 위에 띄우는 이름.</summary>
        private static string PatternName(Pattern pattern)
        {
            switch (pattern)
            {
                case Pattern.Ram: return "성문 돌진!";
                case Pattern.Quake: return "땅울림!";
                case Pattern.Rubble: return "낙석!";
                case Pattern.SealWave: return "봉인 파동!";
                case Pattern.Chains: return "사슬!";
                case Pattern.Runes: return "봉인진!";
                case Pattern.FlameDash: return "불꽃 질주!";
                case Pattern.SunDive: return "태양 낙하!";
                case Pattern.Crescent: return "초승달!";
                case Pattern.MoonBombs: return "월광탄!";
                case Pattern.Beam: return "광선!";
                case Pattern.Pillars: return "빛기둥!";
                case Pattern.Spears: return "빛의 창!";
                case Pattern.Mark: return "표식!";
                case Pattern.Judgment: return "심판!";
                case Pattern.Blink: return "순간 베기!";
                case Pattern.Pendulum: return "진자!";
                case Pattern.SecondHand: return "초침!";
                case Pattern.Cage: return "시계 감옥!";
                case Pattern.Gears: return "톱니바퀴!";
                case Pattern.Barrage: return "멈춘 탄막!";
                case Pattern.RewindDash: return "되감기 돌진!";
                case Pattern.Mines: return "시한 지뢰!";
                case Pattern.Reversal: return "역행!";
                default: return "!";
            }
        }

        private float WindupFor(Pattern pattern)
        {
            float windup;
            switch (pattern)
            {
                case Pattern.Judgment: return JudgmentCastTime;
                case Pattern.Ram: windup = 0.65f; break;
                case Pattern.Mark:
                case Pattern.Cage: windup = 0.7f; break;
                case Pattern.Quake:
                case Pattern.SealWave:
                case Pattern.MoonBombs:
                case Pattern.RewindDash: windup = 0.55f; break;
                case Pattern.Chains:
                case Pattern.Runes:
                case Pattern.Beam:
                case Pattern.Pillars:
                case Pattern.Blink:
                case Pattern.Barrage:
                case Pattern.Reversal: windup = 0.45f; break;
                case Pattern.SecondHand: windup = 0.35f; break;
                default: windup = 0.5f; break;
            }
            return IsBossEnraged ? windup * 0.85f : windup;
        }

        private float BossCooldown()
        {
            bool enraged = IsBossEnraged;
            switch (Kind)
            {
                case EnemyKind.TwinSun:
                case EnemyKind.TwinMoon: return enraged ? 0.3f : 0.8f;
                case EnemyKind.LightLord:
                case EnemyKind.ClockMaster: return enraged ? 0.42f : 0.7f;
                default: return enraged ? 0.55f : 0.85f;
            }
        }

        /// <summary>보스가 다음 단계로 넘어가 빨라졌는지. 쌍둥이는 짝이 쓰러져 있는 동안 몰아친다.</summary>
        private bool IsBossEnraged
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.TwinSun:
                    case EnemyKind.TwinMoon: return _twin != null && _twin._downed;
                    case EnemyKind.LightLord:
                    case EnemyKind.ClockMaster: return _phase >= 3;
                    default: return _phase >= 2;
                }
            }
        }

        /// <summary>예고를 시작할 때 할 일. 돌진할 방향과 길을 정해 보여 주거나, 심판의 그림자를 드리운다.</summary>
        private void OnPatternWindup(Pattern pattern)
        {
            if (pattern == Pattern.Ram) AimRam();
            else if (pattern == Pattern.FlameDash) AimFlameDash();
            else if (pattern == Pattern.RewindDash) AimRewindDash();
            else if (pattern == Pattern.Judgment) BeginJudgment();
        }

        /// <summary>
        /// 그쪽으로 speed로 seconds 동안 달리면 어디까지 가는지 어림한다. 벽이나 낭떠러지에서 멈춘다.
        /// 달려갈 길을 미리 그릴 때 쓴다.
        /// </summary>
        private float DashReach(int direction, float speed, float seconds)
        {
            float maxDistance = speed * seconds;
            float feet = _body.position.y - _size.y * 0.5f;
            float halfWidth = _size.x * 0.5f;
            const float step = 0.25f;
            for (float distance = 0f; distance < maxDistance; distance += step)
            {
                float center = _body.position.x + direction * distance;
                if (Combat.IsSolidAt(new Vector2(center + direction * (halfWidth + 0.3f), _body.position.y))) return distance;
                if (!Combat.IsSolidAt(new Vector2(center + direction * (halfWidth + 0.35f), feet - 0.3f))) return distance;
            }
            return maxDistance;
        }

        /// <summary>몸 앞에서 distance만큼 달려갈 길을 바닥에 그린다.</summary>
        private void ShowDashPath(int direction, float distance, float seconds, Color color)
        {
            Vector2 front = _body.position + new Vector2(direction * _size.x * 0.5f, 0f);
            PathMarker.Lane(front, front + new Vector2(direction * distance, 0f), _size.y, seconds, color);
        }

        /// <summary>예고가 끝났다. 공격을 시작한다. 돌려주는 값은 이번 틱의 가로 속도다.</summary>
        private float StartPattern(Vector2 toPlayer)
        {
            switch (_pattern)
            {
                case Pattern.Ram: return StartRam();
                case Pattern.Quake: StartQuake(); return Rest(0.9f);
                case Pattern.Rubble: DropRubble(); return Rest(0.8f);
                case Pattern.SealWave: StartSealWave(); return Rest(0.8f);
                case Pattern.Chains: SweepChains(toPlayer); return Rest(1.8f);
                case Pattern.Runes: CallRunes(); return Rest(0.9f);
                case Pattern.FlameDash: return StartFlameDash(toPlayer);
                case Pattern.SunDive: return StartBossLeap(toPlayer);
                case Pattern.Crescent: ThrowCrescents(toPlayer); return Rest(0.7f);
                case Pattern.MoonBombs: ThrowBombs(); return Rest(0.75f);
                case Pattern.Beam: FireBeam(); return Rest(1f);
                case Pattern.Pillars: CallPillars(); return Rest(IsBossEnraged ? 0.7f : 1f);
                case Pattern.Spears: ThrowSpears(); return Rest(0.8f);
                case Pattern.Mark: PlaceMark(); return Rest(0.5f);
                case Pattern.Judgment: Judge(); return Rest(1.6f);
                case Pattern.Blink: BlinkStrike(); return Rest(0.9f);
                case Pattern.Pendulum: SwingPendulum(); return Rest(1f);
                case Pattern.SecondHand: StartSecondHand(); return Rest(1.9f);
                case Pattern.Cage:
                    // 감옥이 서 있는 동안 기사는 제자리에서 바늘이 돌아가는 것을 지켜본다.
                    if (!SpawnCages()) return Rest(0.3f);
                    Enter(State.Attack, CageTime);
                    return 0f;
                case Pattern.Gears: StartGears(); return Rest(IsBossEnraged ? 0.6f : 0.8f);
                case Pattern.Barrage: FireFrozenBarrage(); return Rest(0.9f);
                case Pattern.RewindDash: return StartRewindDash();
                case Pattern.Mines: PlaceMines(); return Rest(0.8f);
                case Pattern.Reversal:
                    BeginReversal();
                    Enter(State.Attack, ReversalCastTime);
                    return 0f;
                default: return Rest(0.5f);
            }
        }

        /// <summary>몸이 움직이는 공격을 이어 간다.</summary>
        private float TickPattern()
        {
            switch (_pattern)
            {
                case Pattern.Ram: return TickRam();
                case Pattern.FlameDash: return TickFlameDash();
                case Pattern.SunDive: return TickSunDive();
                case Pattern.Cage: return TickCage();
                case Pattern.RewindDash: return TickRewindDash();
                case Pattern.Reversal: return TickReversal();
                default: return Rest(0.5f);
            }
        }

        /// <summary>공격을 마치고 seconds 동안 숨을 고른다. 이때가 빈틈이다.</summary>
        private float Rest(float seconds)
        {
            Enter(State.Recover, seconds);
            return 0f;
        }

        // ---- 여러 보스가 함께 쓰는 몸놀림 ----

        /// <summary>스테이지 끝 보스의 몸에 부딪히면 더 아프다.</summary>
        private int HeavyHitDamage => IsFinalBoss ? FinalBossHeavyDamage : 1;

        /// <summary>보스 몸이 플레이어에 닿았으면 다치게 한다. 돌진처럼 몸으로 하는 공격 중에 부른다.</summary>
        private void TouchPlayer(int damage)
        {
            if (_collider.bounds.Intersects(_player.Collider.bounds))
            {
                _player.TakeDamage(damage, _body.position.x);
            }
        }

        /// <summary>그쪽으로 달리면 벽에 부딪히거나 낭떠러지로 떨어지는지.</summary>
        private bool DashBlocked(int direction)
        {
            Vector2 chest = _body.position + new Vector2(direction * (_size.x * 0.5f + 0.3f), 0f);
            return Combat.IsSolidAt(chest) || !HasGroundToward(direction);
        }

        /// <summary>플레이어가 지금 있는 자리를 향해 포물선으로 뛰어오른다. 위에 있으면 더 높이 뛴다.</summary>
        private float StartBossLeap(Vector2 toPlayer)
        {
            float gravity = Mathf.Abs(Physics2D.gravity.y) * BossGravityScale;
            float flatSpeed = gravity * LeapAirTime * 0.5f;
            float reachSpeed = Mathf.Sqrt(2f * gravity * (Mathf.Max(toPlayer.y, 0f) + 1.5f));
            float jumpSpeed = Mathf.Clamp(Mathf.Max(flatSpeed, reachSpeed), flatSpeed, 19f);
            float airTime = 2f * jumpSpeed / gravity * 0.85f;
            _slamVelocityX = Mathf.Clamp(toPlayer.x / airTime, -12f, 12f);
            _body.linearVelocity = new Vector2(_slamVelocityX, jumpSpeed);

            // 떨어질 자리에 고리를 그린다. 플레이어가 있던 높이로 떨어진다고 어림한다.
            float landX = _body.position.x + _slamVelocityX * airTime;
            float landY = Mathf.Abs(toPlayer.y) > 1f ? _player.Feet.y : _body.position.y - _size.y * 0.5f;
            PathMarker.Landing(new Vector2(landX, landY), _size.x * 0.5f + 1.2f, airTime, LeapMarkColor);
            _airTime = 0f;
            Enter(State.Attack, 3f);
            return _slamVelocityX;
        }

        /// <summary>뛰어오른 뒤 땅에 닿았으면 true.</summary>
        private bool LandedFromLeap()
        {
            _airTime += Time.fixedDeltaTime;
            _stateTimer -= Time.fixedDeltaTime;
            bool landed = _airTime > 0.3f && _body.linearVelocity.y <= 0.1f && IsStandingOnGround();
            return landed || _stateTimer <= 0f;
        }
    }
}
