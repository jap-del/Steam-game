using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 2스테이지 "빛의 성소"의 보스들. 화면에는 풀이를 알려 주지 않는다. 아래는 개발자용 정답이다.
    ///
    /// 쌍둥이 수문장 (2-3 중간 보스)
    ///   해의 공격: 불꽃 질주(지나간 자리에 불길이 남는다), 태양 낙하(뛰어올라 덮치고 불꽃을 흩뿌린다).
    ///   달의 공격: 초승달(날아갔다가 되돌아오는 탄 두 개), 월광탄(폭탄 세 개). 달은 걷지 않고, 가까이 가면
    ///     멀어지는 쪽으로 순간이동한다(6초에 한 번).
    ///   기믹: 셋을 모두 알아내야 한다.
    ///     1. 빛의 끈: 둘이 가까이 있으면 끈으로 이어져 둘 다 어떤 공격도 받지 않는다.
    ///     2. 해와 그림자: 해는 내 공격만, 달은 분신의 공격만 받는다(달은 분신에게 두 배로 아프게 맞는다).
    ///        다른 쪽 공격은 흡수된다.
    ///     3. 함께 쓰러뜨리기: 하나만 쓰러뜨리면 짝이 살아 있는 동안 몇 초 뒤 다시 일어난다.
    ///   풀이: 달 곁에서 분신을 고정해 두고, 해를 달에게서 멀리 끌고 가서 친다. 고정된 분신이 같은 순간 달을 친다.
    ///
    /// 백광의 군주 (2-6 보스)
    ///   공격: 광선(내 높이로 방을 가로지른다), 빛기둥(일정한 간격으로 내리꽂는다), 빛의 창(천장에서 나를 향해 날아온다).
    ///   기믹, 빛의 갑옷(체력 65%부터): 보통 공격이 통하지 않는다. 군주가 내게 빛의 표식을 붙이고, 표식은 몇 초 뒤
    ///     터진다. 내게서 터지면 크게 다친다. 자리를 바꾸면(K) 표식이 나와 분신 사이에서 옮겨 붙는다.
    ///     표식이 분신에게서, 군주 곁에서 터지면 갑옷이 깨지고 군주가 한동안 무너진다. 갑옷은 얼마 뒤 다시 생긴다.
    ///   기믹, 일식(체력 30%부터): 긴 예고 뒤 방 전체에 심판의 빛이 내린다. 분신과 겹쳐 서 있어야(분신의 그림자 속)
    ///     피한다. 심판 뒤에는 그 그림자에 빛이 고였다가 잠시 뒤 터지므로, 곧바로 그 자리를 벗어나야 한다.
    /// </summary>
    public sealed partial class Enemy
    {
        // ---- 쌍둥이 수문장 ----
        private const float TwinReviveTime = 5f;
        private const float TwinReviveHealth = 0.5f;
        /// <summary>쌍둥이가 이 거리(가로) 안에 있으면 빛의 끈으로 이어진다.</summary>
        private const float TwinTetherRange = 8f;
        private const float MoonStepRange = 3.5f;
        /// <summary>달이 순간이동으로 물러나는 거리. 그쪽이 막혀 있으면 플레이어를 넘어 반대쪽으로 간다.</summary>
        private const float MoonStepDistance = 9f;
        private const float MoonStepCooldown = 6f;
        private const float MoonShadowDamageScale = 2f;
        private const float FlameDashTime = 0.5f;
        private const float FlameDashSpeed = 13f;
        private const float FlameTrailInterval = 0.07f;
        private const float FlameLife = 1.6f;
        private const float CrescentSpeed = 11f;
        private const float CrescentReturn = 9f;
        private const float MoonBombRadius = 1.5f;

        // ---- 백광의 군주 ----
        private const float LordArmorAt = 0.65f;
        private const float LordEclipseAt = 0.3f;
        private const float MarkFuse = 5f;
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
        /// <summary>심판 뒤 그림자에 고인 빛이 터질 때까지의 시간과 그 범위(가로 반폭).</summary>
        private const float AfterglowTime = 1.3f;
        private const float AfterglowHalfWidth = 1.3f;
        private const float PillarWarnTime = 0.9f;
        private const float PillarSpacing = 4.5f;
        private const float SecondWaveDelay = 0.8f;
        private const float BeamWarnTime = 0.85f;
        private const float SpearSpeed = 12f;

        private static readonly Color TetherColor = new Color(1f, 0.92f, 0.7f, 0.8f);
        private static readonly Color TetherTextColor = new Color(1f, 0.92f, 0.7f, 1f);
        private static readonly Color AbsorbTextColor = new Color(0.8f, 0.75f, 1f, 1f);
        private static readonly Color FlameColor = new Color(1f, 0.55f, 0.15f, 1f);
        private static readonly Color MoonColor = new Color(0.7f, 0.65f, 1f, 1f);
        private static readonly Color ArmorAuraColor = new Color(1f, 0.9f, 0.5f, 0.35f);
        private static readonly Color ArmorTextColor = new Color(1f, 0.92f, 0.6f, 1f);
        private static readonly Color MarkRingColor = new Color(1f, 0.95f, 0.65f, 0.8f);
        private static readonly Color MarkFillColor = new Color(1f, 0.95f, 0.65f, 0.12f);
        private static readonly Color ShadowPoolColor = new Color(0.12f, 0.04f, 0.22f, 0.85f);
        private static readonly Color AfterglowColor = new Color(1f, 0.97f, 0.8f, 0.9f);
        private static readonly Color BeamColor = new Color(1f, 0.95f, 0.65f, 1f);
        private static readonly Color SpearColor = new Color(1f, 0.97f, 0.8f, 1f);

        private static readonly Pattern[] LordCycle = { Pattern.Beam, Pattern.Pillars, Pattern.Spears };

        private Enemy _twin;
        private bool _downed;
        private float _downTimer;
        private float _moonStepCooldown;
        private float _flameTrailTimer;
        /// <summary>불꽃 질주를 예고할 때 정한 방향. 예고 중에 플레이어가 넘어가도 바뀌지 않는다.</summary>
        private int _flameDirection = 1;
        private SpriteRenderer _tether;

        private bool _armored;
        private float _armorDownTimer;
        private float _markCooldown;
        private bool _markArmed;
        private bool _markOnShadow;
        private float _markFuse;
        private SpriteRenderer _markRing;
        private SpriteRenderer _markFill;
        private float _judgmentCooldown;
        private SpriteRenderer _shadowPool;
        private float _afterglowTimer;
        private float _secondWaveTimer;
        private float _secondWaveCenter;

        // ---- 화면 표시에 쓰는 정보 ----

        public bool HasMark => _markArmed && _markRing != null;
        /// <summary>표식이 붙어 있는 자리. 나나 분신을 따라다닌다.</summary>
        public Vector2 MarkPosition => MarkHolderPosition;
        public float MarkRemaining => _markArmed ? Mathf.Max(0f, _markFuse) : 0f;
        /// <summary>심판까지 남은 시간. 심판을 준비하고 있지 않으면 0.</summary>
        public float JudgmentRemaining => _state == State.Windup && _pattern == Pattern.Judgment ? Mathf.Max(0f, _stateTimer) : 0f;

        private void TickSanctum(float dt)
        {
            if (Kind == EnemyKind.TwinMoon) TickMoon(dt);
            else if (Kind == EnemyKind.LightLord) TickLord(dt);
        }

        private void ClearSanctumObjects()
        {
            ClearMark();
            ClearShadowPool();
            if (_tether != null) Destroy(_tether.gameObject);
            _tether = null;
        }

        // ---- 쌍둥이 수문장 ----

        /// <summary>두 쌍둥이를 서로의 짝으로 묶는다.</summary>
        public static void LinkTwins(Enemy a, Enemy b)
        {
            if (a == null || b == null || a == b) return;
            a._twin = b;
            b._twin = a;
        }

        /// <summary>둘 다 서 있고 가까이 있으면 빛의 끈으로 이어진다. 이어진 동안에는 둘 다 공격을 받지 않는다.</summary>
        private bool IsTethered
        {
            get
            {
                if (_twin == null || _twin._dead || _dead || _downed || _twin._downed) return false;
                return Mathf.Abs(_twin.transform.position.x - transform.position.x) < TwinTetherRange;
            }
        }

        private bool BlockedByTwins(HitSource source)
        {
            if (Kind != EnemyKind.TwinSun && Kind != EnemyKind.TwinMoon) return false;
            if (IsTethered) return Block("연결", TetherTextColor);
            // 해는 내 공격만, 달은 분신의 공격만 받는다.
            bool wrong = Kind == EnemyKind.TwinSun ? source == HitSource.Shadow : source == HitSource.Player;
            return wrong && Block("흡수", AbsorbTextColor);
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

        /// <summary>달은 걷지 않는다. 플레이어가 가까이 오면 플레이어에게서 멀어지는 쪽으로 순간이동한다.</summary>
        private void TickMoon(float dt)
        {
            if (_moonStepCooldown > 0f) _moonStepCooldown -= dt;
            if (_moonStepCooldown > 0f || _staggerTimer > 0f) return;
            if (_state != State.Chase && _state != State.Recover) return;

            Vector2 toPlayer = _player.Position - _body.position;
            if (Mathf.Abs(toPlayer.x) > MoonStepRange || Mathf.Abs(toPlayer.y) > 3f) return;

            float width = _game.RoomWidth;
            if (width <= 8f) return;
            int away = _body.position.x >= _player.Position.x ? 1 : -1;
            float x = _body.position.x + away * MoonStepDistance;
            if (x < 2f || x > width - 2f) x = _player.Position.x - away * MoonStepDistance;
            x = Mathf.Clamp(x, 2f, width - 2f);
            var destination = new Vector2(x, StageBuilder.GroundTop + _size.y * 0.5f + 0.05f);
            _game.Feel.Sparks(_body.position, MoonColor, 8, 5f);
            _body.position = destination;
            transform.position = new Vector3(destination.x, destination.y, 0f);
            _body.linearVelocity = Vector2.zero;
            _game.Feel.Sparks(destination, MoonColor, 8, 5f);
            Sfx.Play(Sound.Swap, 0.8f);
            _moonStepCooldown = MoonStepCooldown;
        }

        private void UpdateTwinVisuals()
        {
            // 빛의 끈은 해가 그린다.
            if (Kind != EnemyKind.TwinSun) return;

            bool show = IsTethered;
            if (show && _tether == null)
            {
                Transform root = _game.RoomRoot;
                if (root == null) return;
                _tether = PrototypeArt.CreateBox("Tether", root, transform.position, Vector2.one, TetherColor, 6);
            }
            if (_tether == null) return;
            _tether.enabled = show;
            if (!show) return;

            Vector2 from = transform.position;
            Vector2 to = _twin.transform.position;
            Vector2 span = to - from;
            _tether.transform.position = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0f);
            _tether.transform.localScale = new Vector3(span.magnitude, 0.12f, 1f);
            _tether.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            Color color = TetherColor;
            color.a *= 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            _tether.color = color;
        }

        /// <summary>달려갈 방향을 정하고 불길이 지나갈 길을 바닥에 그린다.</summary>
        private void AimFlameDash()
        {
            _flameDirection = _player.Position.x >= _body.position.x ? 1 : -1;
            _facing = _flameDirection;
            float reach = DashReach(_flameDirection, FlameDashSpeed, FlameDashTime);
            ShowDashPath(_flameDirection, reach, WindupFor(Pattern.FlameDash), FlameColor);
        }

        private float StartFlameDash(Vector2 toPlayer)
        {
            _facing = _flameDirection;
            _flameTrailTimer = 0f;
            Enter(State.Attack, FlameDashTime);
            Sfx.Play(Sound.Dash);
            return _facing * FlameDashSpeed;
        }

        /// <summary>달리며 지나간 자리에 불길을 남긴다. 불길은 잠시 타오른다.</summary>
        private float TickFlameDash()
        {
            float dt = Time.fixedDeltaTime;
            _stateTimer -= dt;
            TouchPlayer(1);

            _flameTrailTimer -= dt;
            if (_flameTrailTimer <= 0f)
            {
                _flameTrailTimer = FlameTrailInterval;
                Vector2 feet = _body.position + new Vector2(0f, -_size.y * 0.5f + 0.35f);
                Hazard.Spawn(feet, new Vector2(1.1f, 0.7f), 0.05f, FlameLife, FlameColor);
            }

            if (_stateTimer <= 0f || DashBlocked(_facing)) return Rest(0.6f);
            return _facing * FlameDashSpeed;
        }

        /// <summary>뛰어올라 덮친다. 땅에 닿으면 위쪽 반원으로 불꽃을 흩뿌린다.</summary>
        private float TickSunDive()
        {
            TouchPlayer(1);
            if (!LandedFromLeap()) return _slamVelocityX;

            Vector2 feet = _body.position + new Vector2(0f, -_size.y * 0.5f + 0.4f);
            Bounds reach = _collider.bounds;
            reach.Expand(new Vector3(1.2f, 0.4f, 0f));
            if (reach.Intersects(_player.Collider.bounds)) _player.TakeDamage(1, _body.position.x);
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Lerp(15f, 165f, i / 6f) * Mathf.Deg2Rad;
                _game.SpawnShot(feet, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 7f, 0.4f, FlameColor);
            }
            Sfx.Play(Sound.Shoot, 0.8f);
            _game.ShowBlast(feet, _size.x * 0.5f + 0.8f, new Color(1f, 0.6f, 0.2f, 0.35f));
            _game.Feel.BossSlam(feet);
            return Rest(IsBossEnraged ? 0.5f : 0.9f);
        }

        /// <summary>날아갔다가 되돌아오는 초승달 두 개. 하나는 플레이어를, 하나는 조금 위를 노린다.</summary>
        private void ThrowCrescents(Vector2 toPlayer)
        {
            Vector2 aim = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : new Vector2(_facing, 0f);
            float baseAngle = Mathf.Atan2(aim.y, aim.x);
            float tilt = (aim.x >= 0f ? 1f : -1f) * 20f * Mathf.Deg2Rad;
            for (int i = 0; i < 2; i++)
            {
                float angle = baseAngle + i * tilt;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Projectile shot = _game.SpawnShot(_body.position, direction * CrescentSpeed, 0.55f, MoonColor);
                if (shot != null) shot.SetAcceleration(-direction * CrescentReturn);
                // 초승달이 가장 멀리 날아갈 자리까지 줄을 긋는다. 그 뒤 같은 줄로 되돌아온다.
                float farthest = CrescentSpeed * CrescentSpeed / (2f * CrescentReturn);
                PathMarker.Aim(_body.position, _body.position + direction * farthest, 0.45f, MoonColor);
            }
            Sfx.Play(Sound.Shoot, 0.8f);
        }

        /// <summary>플레이어 자리와 그 양옆에 폭탄 세 개를 던진다.</summary>
        private void ThrowBombs()
        {
            for (int i = -1; i <= 1; i++)
            {
                Vector2 target = _player.Position + new Vector2(i * 2.6f, 0f);
                _game.SpawnBomb(_body.position + Vector2.up * (_size.y * 0.3f), target, 1f, MoonBombRadius);
            }
        }

        // ---- 백광의 군주 ----

        private Pattern NextLordPattern(bool high)
        {
            // 기믹 공격은 때가 되면 다른 공격보다 먼저 쓴다. 분신이 없으면 기다린다.
            Vector2 shadow;
            bool hasShadow = _player.TryGetShadow(out shadow);
            if (_phase == 2 && _armored && !_markArmed && _markCooldown <= 0f && hasShadow) return Pattern.Mark;
            if (_phase >= 3 && _judgmentCooldown <= 0f && _afterglowTimer <= 0f && hasShadow) return Pattern.Judgment;

            if (high) return _patternIndex % 2 == 0 ? Pattern.Pillars : Pattern.Beam;
            return LordCycle[_patternIndex % LordCycle.Length];
        }

        private void CheckLordPhases()
        {
            if (Kind != EnemyKind.LightLord) return;

            if (_phase == 1 && HealthRatio <= LordArmorAt)
            {
                _phase = 2;
                _armored = true;
                _markCooldown = 2f;
                _game.OnBossPhase(this, "빛의 갑옷!");
            }
            else if (_phase == 2 && HealthRatio <= LordEclipseAt)
            {
                _phase = 3;
                _armored = false;
                ClearMark();
                _judgmentCooldown = 3f;
                _baseColor = Color.Lerp(_baseColor, new Color(0.35f, 0.25f, 0.45f, 1f), 0.5f);
                _game.OnBossPhase(this, "일식!");
            }
        }

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
                        Sfx.Play(Sound.Mark);
                    }
                }
            }

            bool judging = _state == State.Windup && _pattern == Pattern.Judgment;
            if (_phase >= 3 && _judgmentCooldown > 0f && !judging) _judgmentCooldown -= dt;

            if (_afterglowTimer > 0f)
            {
                _afterglowTimer -= dt;
                if (_afterglowTimer <= 0f) BurstAfterglow();
            }

            if (_markArmed)
            {
                float before = _markFuse;
                _markFuse -= dt;
                // 1초마다 똑딱인다.
                if (_markFuse > 0f && Mathf.CeilToInt(_markFuse) < Mathf.CeilToInt(before)) Sfx.Play(Sound.Warn);
                if (_markFuse <= 0f) DetonateMark();
            }
        }

        private void UpdateLordVisuals()
        {
            if (_markRing != null)
            {
                // 표식은 붙은 쪽(나나 분신)을 따라다닌다. 터질 때가 가까워질수록 빨리 깜빡인다.
                Vector2 at = MarkHolderPosition;
                var position = new Vector3(at.x, at.y, 0f);
                _markRing.transform.position = position;
                _markFill.transform.position = position;
                float speed = _markFuse < 1.5f ? 18f : 6f;
                Color ring = MarkRingColor;
                ring.a *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * speed));
                _markRing.color = ring;
            }

            if (_shadowPool == null) return;
            if (_afterglowTimer > 0f)
            {
                // 빛이 고인 그림자는 그 자리에 머물며 깜빡인다.
                Color glow = AfterglowColor;
                glow.a *= Mathf.FloorToInt(Time.time * (_afterglowTimer < 0.5f ? 16f : 8f)) % 2 == 0 ? 1f : 0.55f;
                _shadowPool.color = glow;
                return;
            }
            Vector2 shadow;
            if (_player.TryGetShadow(out shadow))
            {
                _shadowPool.transform.position = new Vector3(shadow.x, shadow.y - PlayerController.BodySize.y * 0.5f + 0.05f, 0f);
            }
        }

        /// <summary>내 높이로 방을 가로지르는 광선. 다음 단계에서는 그 위로 하나 더 지나간다.</summary>
        private void FireBeam()
        {
            float width = _game.RoomWidth;
            if (width <= 0f) return;
            float y = _player.Position.y;
            Hazard.Spawn(new Vector2(width * 0.5f, y), new Vector2(width, 1f), BeamWarnTime, 0.3f, BeamColor, 1, default(Vector2), true);
            if (_phase >= 3)
            {
                Hazard.Spawn(new Vector2(width * 0.5f, y + 2.6f), new Vector2(width, 1f), BeamWarnTime + 0.5f, 0.3f, BeamColor, 1, default(Vector2), true);
            }
        }

        /// <summary>천장 다섯 곳에서 지금 플레이어가 있는 자리를 향해 빛의 창을 던진다.</summary>
        private void ThrowSpears()
        {
            Vector2 target = _player.Position;
            for (int i = -2; i <= 2; i++)
            {
                var from = new Vector2(target.x + i * 2.4f, StageBuilder.GroundTop + 10f);
                Vector2 direction = (target - from).normalized;
                _game.SpawnShot(from, direction * SpearSpeed, 0.45f, SpearColor);
                PathMarker.Aim(from, target + direction * 3f, 0.4f, SpearColor);
            }
            Sfx.Play(Sound.Beam, 0.6f);
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

        // ---- 빛의 표식 ----

        /// <summary>표식이 붙어 있는 쪽의 자리. 분신이 사라졌으면 나에게 붙은 것으로 본다.</summary>
        private Vector2 MarkHolderPosition
        {
            get
            {
                Vector2 shadow;
                if (_markOnShadow && _player != null && _player.TryGetShadow(out shadow)) return shadow;
                return _player != null ? _player.Position : (Vector2)transform.position;
            }
        }

        /// <summary>표식을 나에게 붙인다.</summary>
        private void PlaceMark()
        {
            // 예고 중에 일식으로 넘어갔으면 표식을 붙이지 않는다.
            if (_phase != 2) return;

            Transform root = _game.RoomRoot;
            if (root == null)
            {
                _markCooldown = 1f;
                return;
            }

            ClearMark();
            _markArmed = true;
            _markOnShadow = false;
            _markFuse = MarkFuse;
            Vector2 at = _player.Position;
            Vector2 diameter = Vector2.one * MarkRadius * 2f;
            _markFill = RaidArt.Create("MarkFill", root, at, diameter, RaidArt.Disc, MarkFillColor, 5);
            _markRing = RaidArt.Create("MarkRing", root, at, diameter, RaidArt.Ring, MarkRingColor, 6);
            Sfx.Play(Sound.Mark);
        }

        /// <summary>자리를 바꾸면 표식이 나와 분신 사이에서 옮겨 붙는다.</summary>
        private void OnLordSwap()
        {
            if (!_markArmed) return;
            _markOnShadow = !_markOnShadow;
            Sfx.Play(Sound.Mark, 0.6f);
            _game.Feel.Sparks(MarkHolderPosition, MarkRingColor, 8, 4f);
        }

        private void DetonateMark()
        {
            Vector2 at = MarkHolderPosition;
            bool onShadow = _markOnShadow;
            ClearMark();
            _markCooldown = MarkInterval;

            _game.ShowBlast(at, MarkRadius, new Color(1f, 0.95f, 0.7f, 0.55f));
            _game.Feel.Explosion(at);
            if (!onShadow)
            {
                // 몸에 붙은 표식은 피할 수 없다.
                _player.TakeDamage(MarkDamage, at.x, true);
                return;
            }
            if ((_player.Position - at).magnitude <= MarkRadius + 0.3f) _player.TakeDamage(MarkDamage, at.x);
            if (!_armored) return;

            Vector2 offset = _body.position - at;
            bool caught = Mathf.Abs(offset.x) <= MarkRadius + _size.x * 0.5f && Mathf.Abs(offset.y) <= MarkRadius + _size.y * 0.5f;
            if (!caught) return;

            _armored = false;
            _armorDownTimer = ArmorDownTime;
            Stagger(ArmorStunTime);
            _game.OnGimmickSolved(this, "갑옷이 깨졌다!");
        }

        private void ClearMark()
        {
            _markArmed = false;
            _markOnShadow = false;
            if (_markRing != null) Destroy(_markRing.gameObject);
            if (_markFill != null) Destroy(_markFill.gameObject);
            _markRing = null;
            _markFill = null;
        }

        // ---- 심판 ----

        /// <summary>심판의 예고를 시작한다. 분신 발밑에 짙은 그림자가 드리운다.</summary>
        private void BeginJudgment()
        {
            ClearShadowPool();
            Sfx.Play(Sound.BossIntro, 0.8f);

            Vector2 shadow;
            Transform root = _game.RoomRoot;
            if (root == null || !_player.TryGetShadow(out shadow)) return;
            Vector2 feet = shadow + new Vector2(0f, -PlayerController.BodySize.y * 0.5f + 0.05f);
            _shadowPool = RaidArt.Create("ShadowPool", root, feet, new Vector2(1.9f, 0.5f), RaidArt.Disc, ShadowPoolColor, 3);
        }

        /// <summary>방 전체에 빛이 내린다. 분신과 겹쳐 서 있으면 피한다. 그 그림자에는 빛이 고인다.</summary>
        private void Judge()
        {
            _judgmentCooldown = JudgmentInterval;
            Sfx.Play(Sound.Judgment);
            _game.Feel.Flash(new Color(1f, 0.97f, 0.85f, 1f), 0.85f, 0.7f);
            _game.Feel.Shake(0.4f, 0.4f);

            // 방 전체를 덮는 빛이라 대시나 무적 시간으로는 피할 수 없다.
            if (!_player.IsInShadow()) _player.TakeDamage(JudgmentDamage, _body.position.x, true);

            if (_shadowPool != null) _afterglowTimer = AfterglowTime;
        }

        /// <summary>그림자에 고였던 빛이 터진다. 아직 그 자리에 서 있으면 다친다.</summary>
        private void BurstAfterglow()
        {
            if (_shadowPool == null) return;
            Vector2 at = _shadowPool.transform.position;
            ClearShadowPool();

            _game.ShowBlast(at + Vector2.up * 0.8f, AfterglowHalfWidth, new Color(1f, 0.97f, 0.8f, 0.6f));
            Sfx.Play(Sound.Beam);
            Vector2 feet = _player.Feet;
            if (Mathf.Abs(feet.x - at.x) <= AfterglowHalfWidth && Mathf.Abs(feet.y - at.y) <= 1f)
            {
                _player.TakeDamage(JudgmentDamage, at.x, true);
            }
        }

        private void ClearShadowPool()
        {
            _afterglowTimer = 0f;
            if (_shadowPool != null) Destroy(_shadowPool.gameObject);
            _shadowPool = null;
        }
    }
}
