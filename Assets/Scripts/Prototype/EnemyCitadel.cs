using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 1스테이지 "무너진 성채"의 보스 둘. 화면에는 풀이를 알려 주지 않는다. 아래는 개발자용 정답이다.
    ///
    /// 성채의 문지기 (1-3 중간 보스)
    ///   공격: 성문 돌진(벽이나 바위에 부딪힐 때까지 달린다), 땅울림(발밑에서 양쪽으로 땅이 차례로 솟는다),
    ///         낙석(천장에서 바위 세 개가 떨어진다).
    ///   기믹, 철갑(체력 60%부터): 모든 공격을 튕겨 낸다. 이때부터 떨어진 바위가 바닥에 남고,
    ///     성문 돌진은 내가 아니라 내 분신을 향한다. 돌진하다 바위에 부딪히면 철갑에 금이 가고,
    ///     금이 세 번 가면 철갑이 깨져 한동안 무너진다. 철갑은 얼마 뒤 다시 굳는다.
    ///     풀이: 낙석을 유도해 바위를 깔고, 문지기와 분신 사이에 바위가 오도록 분신을 둔다(분신 고정이 쉽다).
    ///
    /// 봉인의 수호자 (1-6 보스)
    ///   공격: 봉인 파동(사방으로 퍼지는 구슬), 사슬(낮게 한 번, 조금 뒤 높게 한 번 휘두른다),
    ///         봉인진(바닥 여러 곳이 솟는다).
    ///   기믹, 봉인(체력 60%부터): 보호막을 치고 봉인석 셋(왼쪽 발판, 가운데 바닥, 오른쪽 발판)을 세운다.
    ///     봉인석이 하나씩 차례로 번쩍이며 순서를 보여 준다. 봉인석은 분신의 공격에만 반응하고 내 공격은 튕긴다.
    ///     분신이 보여 준 순서대로 쳐야 하나씩 켜지고, 셋이 다 켜지면 보호막이 깨진다. 순서가 틀리면 모두 꺼지고
    ///     봉인 파동이 터지며, 새 순서를 다시 보여 준다. 보호막은 깨지고 얼마 뒤 새 순서로 다시 선다.
    ///     풀이: 보여 준 순서대로 봉인석을 치며 지나가면 2초 뒤 분신이 같은 순서로 친다.
    /// </summary>
    public sealed partial class Enemy
    {
        // ---- 성채의 문지기 ----
        private const float GateIronAt = 0.6f;
        private const float RamSpeed = 15f;
        private const float RamMaxTime = 1.8f;
        private const float RamWallDaze = 1.2f;
        private const float RamBoulderDaze = 1.8f;
        private const int IronCracksToBreak = 3;
        private const float IronBreakStunTime = 4.5f;
        /// <summary>철갑이 깨진 뒤 다시 굳을 때까지의 시간. 무너져 있는 시간은 빼고 센다.</summary>
        private const float IronDownTime = 12f;
        private const int QuakeSteps = 9;
        private const float QuakeStepTime = 0.08f;
        private const float QuakeSpacing = 1.5f;
        private const float RubbleFallTime = 0.9f;
        private const float RubbleSpread = 3.5f;
        private const float BoulderLife = 18f;
        private const int MaxBoulders = 4;

        // ---- 봉인의 수호자 ----
        private const float SealAt = 0.6f;
        private const float SealRevealDelay = 0.8f;
        private const float SealRevealStep = 0.8f;
        private const float SealRetryDelay = 1.4f;
        private const float SealStunTime = 4.5f;
        /// <summary>보호막이 깨진 뒤 다시 설 때까지의 시간. 무너져 있는 시간은 빼고 센다.</summary>
        private const float SealDownTime = 14f;
        private const int SealWaveCount = 12;
        private const float SealWaveSpeed = 5f;
        private const float SealSecondRingDelay = 0.45f;
        private const float ChainLength = 15f;
        private const float RuneSpacing = 5f;

        private static readonly Color IronAuraColor = new Color(0.75f, 0.75f, 0.8f, 0.3f);
        private static readonly Color IronTextColor = new Color(0.85f, 0.85f, 0.9f, 1f);
        private static readonly Color RamTargetColor = new Color(1f, 0.3f, 0.25f, 0.85f);
        private static readonly Color QuakeColor = new Color(0.85f, 0.65f, 0.4f, 1f);
        private static readonly Color RubbleColor = new Color(0.8f, 0.7f, 0.55f, 1f);
        private static readonly Color SealAuraColor = new Color(0.5f, 0.95f, 1f, 0.3f);
        private static readonly Color SealTextColor = new Color(0.6f, 1f, 1f, 1f);
        private static readonly Color SealOrbColor = new Color(0.55f, 0.95f, 1f, 1f);
        private static readonly Color ChainColor = new Color(0.7f, 0.8f, 0.95f, 1f);
        private static readonly Color RuneColor = new Color(0.45f, 0.9f, 1f, 1f);

        private static readonly Pattern[] GateCycle = { Pattern.Ram, Pattern.Rubble, Pattern.Quake };
        private static readonly Pattern[] GateIronCycle = { Pattern.Rubble, Pattern.Ram, Pattern.Quake, Pattern.Ram };
        private static readonly Pattern[] GuardianCycle = { Pattern.Chains, Pattern.SealWave, Pattern.Runes };

        private bool _ironclad;
        private bool _hasIronPhase;
        private float _ironDownTimer;
        private int _ironCracks;
        private int _ramDirection = 1;
        private int _quakeLeft;
        private int _quakeStep;
        private float _quakeTimer;
        private float _quakeOriginX;
        private List<Boulder> _boulders;
        /// <summary>떨어지는 중인 바위. x는 떨어질 자리, y는 바닥에 닿을 때까지 남은 시간.</summary>
        private List<Vector2> _fallingRocks;

        private bool _sealed;
        private bool _hasSealPhase;
        private float _sealDownTimer;
        private SealStone[] _seals;
        private int[] _sealOrder;
        private int _sealProgress;
        private int _revealStep;
        private float _revealTimer;
        private float _secondRingTimer;

        private void BuildCitadel()
        {
            if (Kind != EnemyKind.MidBoss) return;
            _boulders = new List<Boulder>();
            _fallingRocks = new List<Vector2>();
        }

        private void TickCitadel(float dt)
        {
            if (Kind == EnemyKind.MidBoss) TickGatekeeper(dt);
            else if (Kind == EnemyKind.Boss) TickGuardian(dt);
        }

        private void CheckCitadelPhases()
        {
            if (_phase != 1) return;

            if (Kind == EnemyKind.MidBoss && HealthRatio <= GateIronAt)
            {
                _phase = 2;
                _hasIronPhase = true;
                HardenIron();
                _game.OnBossPhase(this, "철갑!");
            }
            else if (Kind == EnemyKind.Boss && HealthRatio <= SealAt)
            {
                _phase = 2;
                _hasSealPhase = true;
                RaiseSeal();
                _game.OnBossPhase(this, "봉인!");
            }
        }

        private void ClearCitadelObjects()
        {
            if (_boulders != null)
            {
                for (int i = 0; i < _boulders.Count; i++)
                {
                    if (_boulders[i] != null) Destroy(_boulders[i].gameObject);
                }
                _boulders.Clear();
            }
            ClearSeals(false);
        }

        // ---- 성채의 문지기 ----

        private Pattern NextGatekeeperPattern(bool high)
        {
            if (high) return _patternIndex % 2 == 0 ? Pattern.Rubble : Pattern.Quake;
            Pattern[] cycle = _phase >= 2 ? GateIronCycle : GateCycle;
            return cycle[_patternIndex % cycle.Length];
        }

        private void TickGatekeeper(float dt)
        {
            if (_quakeLeft > 0)
            {
                _quakeTimer -= dt;
                if (_quakeTimer <= 0f)
                {
                    _quakeTimer = QuakeStepTime;
                    _quakeStep++;
                    _quakeLeft--;
                    float offset = _size.x * 0.5f + (_quakeStep - 0.5f) * QuakeSpacing;
                    QuakeAt(_quakeOriginX + offset);
                    QuakeAt(_quakeOriginX - offset);
                }
            }

            for (int i = _fallingRocks.Count - 1; i >= 0; i--)
            {
                Vector2 rock = _fallingRocks[i];
                rock.y -= dt;
                if (rock.y > 0f)
                {
                    _fallingRocks[i] = rock;
                    continue;
                }
                _fallingRocks.RemoveAt(i);
                PlaceBoulder(rock.x);
            }

            // 철갑은 깨진 뒤 무너짐에서 일어나고 얼마 지나면 다시 굳는다.
            if (_hasIronPhase && !_ironclad && _staggerTimer <= 0f)
            {
                _ironDownTimer -= dt;
                if (_ironDownTimer <= 0f)
                {
                    HardenIron();
                    Sfx.Play(Sound.Block);
                    _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.2f), "철갑", IronTextColor);
                }
            }
        }

        private void HardenIron()
        {
            _ironclad = true;
            _ironCracks = 0;
        }

        /// <summary>
        /// 돌진할 방향을 정한다. 철갑을 두른 동안에는 플레이어가 아니라 분신을 노리고, 분신 자리에 조준 표시가 생긴다.
        /// 달려갈 길과 멈출 자리를 바닥에 그린다.
        /// </summary>
        private void AimRam()
        {
            Vector2 target = _player.Position;
            Vector2 shadow;
            float windup = WindupFor(Pattern.Ram);
            if (_ironclad && _player.TryGetShadow(out shadow))
            {
                target = shadow;
                PathMarker.Reticle(shadow, 1.5f, windup + 0.15f, RamTargetColor);
            }
            _ramDirection = target.x >= _body.position.x ? 1 : -1;
            _facing = _ramDirection;

            float reach = RamReach(_ramDirection, RamSpeed * RamMaxTime);
            Vector2 front = _body.position + new Vector2(_ramDirection * _size.x * 0.5f, 0f);
            PathMarker.Lane(front, front + new Vector2(_ramDirection * reach, 0f), _size.y, windup, RamTargetColor);
        }

        /// <summary>그쪽으로 돌진하면 어디까지 가는지 어림한다. 벽, 낭떠러지, 바위에서 멈춘다.</summary>
        private float RamReach(int direction, float maxDistance)
        {
            float feet = _body.position.y - _size.y * 0.5f;
            float halfWidth = _size.x * 0.5f;
            const float step = 0.25f;
            for (float distance = 0f; distance < maxDistance; distance += step)
            {
                float center = _body.position.x + direction * distance;
                Vector2 chest = new Vector2(center + direction * (halfWidth + 0.3f), _body.position.y);
                if (Combat.IsSolidAt(chest)) return distance;
                Vector2 ground = new Vector2(center + direction * (halfWidth + 0.35f), feet - 0.3f);
                if (!Combat.IsSolidAt(ground)) return distance;
                float front = center + direction * halfWidth;
                for (int i = 0; i < _boulders.Count; i++)
                {
                    Boulder boulder = _boulders[i];
                    if (boulder == null) continue;
                    Vector2 at = boulder.transform.position;
                    if (feet > at.y + Boulder.Height * 0.5f) continue;
                    if (Mathf.Abs(at.x - front) <= Boulder.HalfWidth + 0.15f) return distance;
                }
            }
            return maxDistance;
        }

        private float StartRam()
        {
            _facing = _ramDirection;
            Enter(State.Attack, RamMaxTime);
            Sfx.Play(Sound.Dash);
            return _ramDirection * RamSpeed;
        }

        /// <summary>벽이나 바위에 부딪힐 때까지 달린다. 낭떠러지 앞에서는 멈춘다.</summary>
        private float TickRam()
        {
            _stateTimer -= Time.fixedDeltaTime;
            TouchPlayer(1);

            Boulder boulder = BoulderAhead();
            if (boulder != null)
            {
                CrashIntoBoulder(boulder);
                return 0f;
            }
            Vector2 chest = _body.position + new Vector2(_ramDirection * (_size.x * 0.5f + 0.3f), 0f);
            if (Combat.IsSolidAt(chest))
            {
                Crash(RamWallDaze);
                return 0f;
            }
            if (_stateTimer <= 0f || !HasGroundToward(_ramDirection)) return Rest(0.7f);
            return _ramDirection * RamSpeed;
        }

        private Boulder BoulderAhead()
        {
            float front = _body.position.x + _ramDirection * _size.x * 0.5f;
            float feet = _body.position.y - _size.y * 0.5f;
            for (int i = _boulders.Count - 1; i >= 0; i--)
            {
                Boulder boulder = _boulders[i];
                if (boulder == null)
                {
                    _boulders.RemoveAt(i);
                    continue;
                }
                Vector2 at = boulder.transform.position;
                // 발판 위를 달리는 중이면 바닥의 바위에 닿지 않는다.
                if (feet > at.y + Boulder.Height * 0.5f) continue;
                if (Mathf.Abs(at.x - front) <= Boulder.HalfWidth + 0.15f) return boulder;
            }
            return null;
        }

        /// <summary>부딪혀 멈췄다. 잠깐 휘청인다.</summary>
        private void Crash(float daze)
        {
            Vector2 front = _body.position + new Vector2(_ramDirection * _size.x * 0.5f, 0f);
            _game.Feel.Shake(0.3f, 0.25f);
            _game.Feel.Sparks(front, new Color(0.9f, 0.85f, 0.7f, 1f), 10, 6f);
            Sfx.Play(Sound.BossSlam, 0.8f);
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            Enter(State.Recover, daze);
        }

        private void CrashIntoBoulder(Boulder boulder)
        {
            _boulders.Remove(boulder);
            boulder.Shatter();
            Crash(RamBoulderDaze);
            if (!_ironclad) return;

            _ironCracks++;
            _game.ShowPopup((Vector2)transform.position + Vector2.up * (_size.y * 0.5f + 1.1f), "균열 " + _ironCracks, IronTextColor);
            if (_ironCracks < IronCracksToBreak) return;

            _ironclad = false;
            _ironDownTimer = IronDownTime;
            Sfx.Play(Sound.Shatter);
            _game.Feel.Sparks(transform.position, IronTextColor, 16, 8f);
            Stagger(IronBreakStunTime);
            _game.OnGimmickSolved(this, "철갑이 깨졌다!");
        }

        private void StartQuake()
        {
            _quakeOriginX = _body.position.x;
            _quakeStep = 0;
            _quakeLeft = QuakeSteps;
            _quakeTimer = 0f;
            _game.Feel.Shake(0.25f, 0.2f);
            _game.Feel.BossSlam(_body.position + new Vector2(0f, -_size.y * 0.5f));
        }

        /// <summary>땅울림 한 칸. 바닥이 잠깐 솟는다. 뛰어넘는다.</summary>
        private void QuakeAt(float x)
        {
            if (x < 0.3f || x > _game.RoomWidth - 0.3f) return;
            Hazard.Spawn(new Vector2(x, StageBuilder.GroundTop + 0.65f), new Vector2(1.4f, 1.3f), 0.12f, 0.18f, QuakeColor);
        }

        /// <summary>플레이어 자리와 양옆 천장에서 바위가 떨어진다. 철갑을 두른 동안에는 떨어진 바위가 바닥에 남는다.</summary>
        private void DropRubble()
        {
            Transform root = _game.RoomRoot;
            float width = _game.RoomWidth;
            if (root == null || width <= 3f) return;

            float center = _player.Position.x;
            for (int i = -1; i <= 1; i++)
            {
                float x = Mathf.Clamp(center + i * RubbleSpread + Random.Range(-0.6f, 0.6f), 1.5f, width - 1.5f);
                // 떨어지는 길 전체가 위험하다. 발판 위에 있어도 맞는다.
                Hazard.Spawn(new Vector2(x, StageBuilder.GroundTop + 4f), new Vector2(1.4f, 8f), RubbleFallTime, 0.15f, RubbleColor);
                FallingRock.Spawn(root, x, StageBuilder.GroundTop + 11f, StageBuilder.GroundTop + 0.5f, RubbleFallTime);
                if (_ironclad) _fallingRocks.Add(new Vector2(x, RubbleFallTime));
            }
            _game.Feel.Shake(0.15f, 0.2f);
            Sfx.Play(Sound.BossSlam, 0.5f);
        }

        private void PlaceBoulder(float x)
        {
            Transform root = _game.RoomRoot;
            if (root == null) return;

            _boulders.RemoveAll(b => b == null);
            if (_boulders.Count >= MaxBoulders)
            {
                // 가장 오래된 바위부터 부서진다.
                Boulder oldest = _boulders[0];
                _boulders.RemoveAt(0);
                if (oldest != null) oldest.Shatter();
            }
            _boulders.Add(Boulder.Create(root, x, StageBuilder.GroundTop, BoulderLife));
            _game.Feel.Shake(0.1f, 0.1f);
        }

        // ---- 봉인의 수호자 ----

        private Pattern NextGuardianPattern(bool high)
        {
            if (high) return _patternIndex % 2 == 0 ? Pattern.Runes : Pattern.SealWave;
            return GuardianCycle[_patternIndex % GuardianCycle.Length];
        }

        private void StartSealWave()
        {
            FireSealRing(0f);
            if (_phase >= 2) _secondRingTimer = SealSecondRingDelay;
        }

        /// <summary>몸에서 사방으로 구슬을 쏜다. offset만큼 돌려서 앞 고리의 틈을 메운다.</summary>
        private void FireSealRing(float offset)
        {
            for (int i = 0; i < SealWaveCount; i++)
            {
                float angle = (i + offset) * Mathf.PI * 2f / SealWaveCount;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _game.SpawnShot(_body.position, direction * SealWaveSpeed, 0.4f, SealOrbColor);
            }
            Sfx.Play(Sound.Shoot, 0.8f);
        }

        /// <summary>플레이어 쪽으로 사슬을 낮게 휘두르고, 조금 뒤 높게 휘두른다. 뛰어넘고, 내려와 있어야 한다.</summary>
        private void SweepChains(Vector2 toPlayer)
        {
            int side = toPlayer.x >= 0f ? 1 : -1;
            float centerX = _body.position.x + side * ChainLength * 0.5f;
            var size = new Vector2(ChainLength, 0.8f);
            Hazard.Spawn(new Vector2(centerX, StageBuilder.GroundTop + 0.6f), size, 0.7f, 0.2f, ChainColor, 1, default(Vector2), true);
            Hazard.Spawn(new Vector2(centerX, StageBuilder.GroundTop + 2.5f), size, 1.75f, 0.2f, ChainColor, 1, default(Vector2), true);
        }

        /// <summary>플레이어 자리와 일정한 간격으로 바닥이 솟는다. 다음 단계에서는 더 많이 솟는다.</summary>
        private void CallRunes()
        {
            float width = _game.RoomWidth;
            int count = _phase >= 2 ? 5 : 3;
            float center = _player.Position.x;
            for (int i = 0; i < count; i++)
            {
                // 0, -1, +1, -2, +2 칸
                int step = (i + 1) / 2 * (i % 2 == 0 ? 1 : -1);
                float x = center + step * RuneSpacing;
                if (x < 0.5f || x > width - 0.5f) continue;
                Hazard.Spawn(new Vector2(x, StageBuilder.GroundTop + 1.3f), new Vector2(2.4f, 2.6f), 1f, 0.25f, RuneColor, 1, default(Vector2), i == 0);
            }
        }

        private void TickGuardian(float dt)
        {
            if (_secondRingTimer > 0f)
            {
                _secondRingTimer -= dt;
                if (_secondRingTimer <= 0f) FireSealRing(0.5f);
            }

            // 보호막은 깨진 뒤 무너짐에서 일어나고 얼마 지나면 새 순서로 다시 선다.
            if (_hasSealPhase && !_sealed && _staggerTimer <= 0f)
            {
                _sealDownTimer -= dt;
                if (_sealDownTimer <= 0f)
                {
                    RaiseSeal();
                    Sfx.Play(Sound.Mark);
                }
            }

            if (!_sealed || !IsRevealing) return;
            _revealTimer -= dt;
            if (_revealTimer > 0f) return;

            SealStone stone = _seals[_sealOrder[_revealStep]];
            if (stone != null) stone.Flash();
            _revealStep++;
            _revealTimer = SealRevealStep;
        }

        /// <summary>봉인석이 순서를 보여 주는 중인지. 그동안에는 쳐도 반응하지 않는다.</summary>
        private bool IsRevealing => _seals != null && _sealOrder != null && _revealStep < _sealOrder.Length;

        private void RaiseSeal()
        {
            IList<Vector2> points = _game.GimmickPoints;
            Transform root = _game.RoomRoot;
            if (points == null || points.Count < 3 || root == null) return;

            ClearSeals(false);
            _sealed = true;
            _seals = new SealStone[3];
            for (int i = 0; i < _seals.Length; i++)
            {
                _seals[i] = SealStone.Create(root, points[i], this, i);
            }
            ShuffleSealOrder();
            BeginReveal(SealRevealDelay);
        }

        private void ShuffleSealOrder()
        {
            if (_sealOrder == null) _sealOrder = new int[3];
            for (int i = 0; i < _sealOrder.Length; i++) _sealOrder[i] = i;
            for (int i = _sealOrder.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int swap = _sealOrder[i];
                _sealOrder[i] = _sealOrder[j];
                _sealOrder[j] = swap;
            }
        }

        /// <summary>봉인석을 모두 끄고, delay 뒤부터 순서를 하나씩 보여 준다.</summary>
        private void BeginReveal(float delay)
        {
            _sealProgress = 0;
            _revealStep = 0;
            _revealTimer = delay;
            for (int i = 0; i < _seals.Length; i++)
            {
                if (_seals[i] != null) _seals[i].SetLit(false);
            }
        }

        /// <summary>봉인석이 맞았다. SealStone이 부른다. 분신의 공격만 센다.</summary>
        public void OnSealStoneHit(SealStone stone, HitSource source)
        {
            if (!_sealed || _seals == null || IsRevealing || stone.IsLit) return;

            if (source == HitSource.Player)
            {
                stone.Dull();
                return;
            }

            if (stone.Index == _sealOrder[_sealProgress])
            {
                stone.SetLit(true);
                _sealProgress++;
                if (_sealProgress >= _seals.Length) BreakSeal();
                return;
            }

            // 순서가 틀렸다. 모두 꺼지고 봉인 파동이 터진다. 새 순서를 다시 보여 준다.
            stone.Reject();
            FireSealRing(0.25f);
            ShuffleSealOrder();
            BeginReveal(SealRetryDelay);
        }

        private void BreakSeal()
        {
            _sealed = false;
            _sealDownTimer = SealDownTime;
            ClearSeals(true);
            Stagger(SealStunTime);
            _game.OnGimmickSolved(this, "봉인이 깨졌다!");
        }

        private void ClearSeals(bool shatter)
        {
            if (_seals == null) return;
            for (int i = 0; i < _seals.Length; i++)
            {
                if (_seals[i] == null) continue;
                if (shatter) _seals[i].Shatter();
                else Destroy(_seals[i].gameObject);
            }
            _seals = null;
        }
    }
}
