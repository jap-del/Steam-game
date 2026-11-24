using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 보스 기믹이 함께 쓰는 것: 이름과 체력 막대 표시, 기믹을 풀었을 때의 무너짐, 공격을 막는 판정,
    /// 발판을 뚫고 내려오기, 쫓아가는 속도. 보스마다의 기믹은 스테이지별 파일에 있다.
    ///   EnemyCitadel.cs: 성채의 문지기(철갑과 바위), 봉인의 수호자(봉인석의 순서)
    ///   EnemySanctum.cs: 쌍둥이 수문장(빛의 끈과 해와 달), 백광의 군주(옮겨 붙는 표식, 두 번 내리는 심판)
    ///   EnemyClock.cs: 태엽 기사(시계 감옥과 태엽 갑옷), 시계탑의 주인(시계 방패와 역행)
    /// 화면에는 기믹의 풀이나 힌트를 띄우지 않는다. 플레이어가 보고 듣고 알아내야 한다.
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>기믹을 풀어 보스가 무너져 있는 동안 받는 피해 배율.</summary>
        private const float StaggerDamageScale = 1.5f;
        /// <summary>보스 발이 플레이어 발보다 이만큼 높이 있으면 발판을 뚫고 내려온다.</summary>
        private const float DropHeight = 1.2f;

        private static readonly Color DownedTextColor = new Color(0.7f, 0.7f, 0.75f, 1f);

        private static readonly float[] GatekeeperMarks = { GateIronAt };
        private static readonly float[] GuardianMarks = { SealAt };
        private static readonly float[] LordMarks = { LordArmorAt, LordEclipseAt };
        private static readonly float[] KnightMarks = { KnightSecondPhaseAt };
        private static readonly float[] MasterMarks = { MasterGuardAt, MasterMidnightAt };

        /// <summary>보스의 단계. 1에서 시작해 체력이 정해진 비율 아래로 내려갈 때마다 오른다.</summary>
        private int _phase = 1;
        private SpriteRenderer _aura;
        private float _staggerTimer;
        private Collider2D _dropThrough;

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
                if (_staggerTimer > 0f) return "무너짐";
                if (_ironclad) return "철갑";
                if (_sealed) return "보호막";
                if (_armored) return "빛의 갑옷";
                if (IsTethered) return "연결";
                string clock = ClockBarStatus;
                if (clock != null) return clock;
                if (Kind == EnemyKind.LightLord && _phase >= 3) return "일식";
                if (_twin != null && _twin._downed) return "격노";
                return null;
            }
        }

        /// <summary>체력 막대에 표시할 단계의 경계(체력 비율). 없으면 null.</summary>
        public float[] PhaseMarks
        {
            get
            {
                switch (Kind)
                {
                    case EnemyKind.MidBoss: return GatekeeperMarks;
                    case EnemyKind.Boss: return GuardianMarks;
                    case EnemyKind.LightLord: return LordMarks;
                    case EnemyKind.ClockKnight: return KnightMarks;
                    case EnemyKind.ClockMaster: return MasterMarks;
                    default: return null;
                }
            }
        }

        /// <summary>기믹을 풀어 보스가 무너져 있는지. 체력 막대를 밝게 깜빡인다.</summary>
        public bool IsStaggered => _staggerTimer > 0f;
        public bool IsDowned => _downed;

        // ---- Enemy.cs에서 부르는 것 ----

        private void BuildRaid()
        {
            if (!IsBoss) return;
            BuildCitadel();
            BuildClock();

            // 철갑, 보호막, 빛의 갑옷을 두르면 몸 둘레에 빛이 생긴다.
            Color aura;
            if (Kind == EnemyKind.MidBoss) aura = IronAuraColor;
            else if (Kind == EnemyKind.Boss) aura = SealAuraColor;
            else if (Kind == EnemyKind.LightLord) aura = ArmorAuraColor;
            else return;
            _aura = RaidArt.Create("Aura", transform, transform.position, _size * 1.6f, RaidArt.Disc, aura, 7);
            _aura.enabled = false;
        }

        /// <summary>물리 틱마다 부른다. true이면 이번 틱의 움직임과 공격을 건너뛴다.</summary>
        private bool TickRaid()
        {
            if (!IsBoss) return false;
            float dt = Time.fixedDeltaTime;
            if (_staggerTimer > 0f) _staggerTimer -= dt;

            if (_downed)
            {
                _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
                _downTimer -= dt;
                if (_downTimer <= 0f) Revive();
                return true;
            }

            TickCitadel(dt);
            TickSanctum(dt);
            TickClock(dt);
            TickDropThrough();
            return false;
        }

        /// <summary>체력이 단계의 경계를 넘으면 다음 단계로 간다. 한 번 맞을 때 한 단계씩만 넘어간다.</summary>
        private void CheckRaidPhases()
        {
            CheckCitadelPhases();
            CheckLordPhases();
            CheckClockPhases();
        }

        /// <summary>true이면 이 공격은 막혔다. 막힌 까닭을 짧은 글자로 보여 준다.</summary>
        private bool BlockedByRaid(HitSource source)
        {
            if (_downed) return true;
            if (_ironclad) return Block("팅", IronTextColor);
            if (_sealed) return Block("보호막", SealTextColor);
            if (_armored) return Block("갑옷", ArmorTextColor);
            return BlockedByTwins(source);
        }

        private bool Block(string text, Color color)
        {
            Vector2 above = (Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 0.6f);
            _game.ShowPopup(above, text, color);
            _flashColor = color;
            _flashTimer = 0.08f;
            _game.Feel.BlockedHit(transform.position);
            return true;
        }

        /// <summary>막히지 않은 공격이 들어왔다. 협공이었는지 알려 준다. 받는 피해를 정하기 전에 부른다.</summary>
        private void OnRaidHit(bool pincer)
        {
            OnKnightHit(pincer);
        }

        /// <summary>받는 피해에 곱하는 값. 무너져 있으면 더 아프고, 태엽 갑옷은 덜 아프다.</summary>
        private float DamageTakenScale(HitSource source)
        {
            float scale = _staggerTimer > 0f ? StaggerDamageScale : 1f;
            if (Kind == EnemyKind.ClockKnight && _staggerTimer <= 0f) scale *= KnightArmorScale;
            if (Kind == EnemyKind.TwinMoon && source == HitSource.Shadow) scale *= MoonShadowDamageScale;
            return scale;
        }

        /// <summary>기믹을 풀었다. 보스가 seconds 동안 아무것도 못 하고 더 아프게 맞는다.</summary>
        private void Stagger(float seconds)
        {
            _staggerTimer = seconds;
            Enter(State.Recover, seconds);
            if (_body != null) _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        }

        private Color RaidTint(Color color)
        {
            if (_downed) return new Color(_baseColor.r * 0.3f, _baseColor.g * 0.3f, _baseColor.b * 0.35f, 1f);
            return ClockTint(color);
        }

        private void UpdateRaidVisuals()
        {
            if (_aura != null)
            {
                bool show = _ironclad || _sealed || _armored;
                _aura.enabled = show;
                if (show)
                {
                    Color color = _ironclad ? IronAuraColor : (_sealed ? SealAuraColor : ArmorAuraColor);
                    color.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 4f);
                    _aura.color = color;
                }
            }

            UpdateTwinVisuals();
            UpdateLordVisuals();
            UpdateClockVisuals();
        }

        private void ClearRaidObjects()
        {
            ClearCitadelObjects();
            ClearSanctumObjects();
            ClearClockObjects();
        }

        /// <summary>플레이어가 분신과 자리를 바꿨다. 표식이 옮겨 붙는다.</summary>
        public void OnPlayerSwapped()
        {
            OnLordSwap();
        }

        // ---- 움직임 ----

        private float ChaseSpeed(Vector2 toPlayer)
        {
            float distanceX = Mathf.Abs(toPlayer.x);
            switch (Kind)
            {
                // 달의 수문장은 걸어 다니지 않는다. 다가오면 방 반대편으로 순간이동한다.
                case EnemyKind.TwinMoon: return 0f;
                // 백광의 군주는 멀찍이서 싸운다.
                case EnemyKind.LightLord: return distanceX > 7f && CanWalkToward(_facing) ? _facing * MoveSpeed : 0f;
                // 방패를 든 시계탑의 주인이 분신을 바라보고 있으면 걷지 않고 그 자리를 지킨다.
                case EnemyKind.ClockMaster:
                    if (IsClockGuarding && (toPlayer.x >= 0f ? 1 : -1) != _facing) return 0f;
                    return distanceX > 3f && CanWalkToward(_facing) ? _facing * MoveSpeed : 0f;
                default: return distanceX > 3f && CanWalkToward(_facing) ? _facing * MoveSpeed : 0f;
            }
        }

        private bool HasGroundToward(int direction)
        {
            Vector2 probe = _body.position + new Vector2(direction * (_size.x * 0.5f + 0.35f), -_size.y * 0.5f - 0.3f);
            return Combat.IsSolidAt(probe);
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
    }
}
