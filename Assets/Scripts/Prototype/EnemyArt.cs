using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 움직이는 그림 판(PixelRig)을 쓰는 적의 겉모습. 상태마다 움직임을 고르고, 예전 그림의 색 신호를
    /// 같은 뜻으로 옮긴다. 공격 예고는 노란 깜빡임, 공격 중은 노란 빛, 빈틈은 어둡게, 쓰러짐은 아주 어둡게,
    /// 멈춘 태엽 기사는 푸르게, 순간 베기 예고는 흐려진다. 마지막 단계의 보스는 색이 어두워진 만큼 그림도 물든다.
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>맞았거나 막았을 때 실루엣 번쩍임 세기.</summary>
        private const float HitFlashAlpha = 0.85f;
        /// <summary>공격 예고 깜빡임 세기. 그림이 거의 노랗게 덮여야 예전처럼 한눈에 보인다.</summary>
        private const float WarningFlashAlpha = 0.65f;
        /// <summary>공격 중 노란 빛 세기.</summary>
        private const float AttackFlashAlpha = 0.5f;
        /// <summary>멈춘 태엽 기사에 덮는 푸른 빛 세기.</summary>
        private const float FrozenFlashAlpha = 0.35f;
        private const float RecoverShade = 0.55f;
        private static readonly Color DownedShade = new Color(0.3f, 0.3f, 0.35f, 1f);

        private PixelRig _rig;
        private int _stateSerial;
        private int _rigSerial = -1;
        /// <summary>보스 등장 연출 동안 보여 줄 움직임. null이면 평소대로 상태를 따른다.</summary>
        private string _introPose;
        private Color _introGlow;

        /// <summary>
        /// 보스 등장 연출에서 자세를 정한다. 게임이 멈춰 있어도 실제 시간으로 움직이고, glow 색의 빛을 덮는다.
        /// clip이 null이면 평소로 돌아간다. 예전 그림이면 아무것도 하지 않는다.
        /// </summary>
        public void SetIntroPose(string clip, Color glow)
        {
            _introPose = clip;
            _introGlow = glow;
            if (_rig != null) _rig.UnscaledTime = clip != null;
        }

        private void UpdateRigLook()
        {
            if (_introPose != null)
            {
                _sprite.color = PhaseTint();
                _rig.SetFlash(_introGlow);
                _rig.Play(_introPose);
                return;
            }

            Color tint = PhaseTint();
            Color flash = Color.clear;
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                flash = WithAlpha(_flashColor, HitFlashAlpha);
            }
            else if (_state == State.Windup)
            {
                // 공격 예고: 빠르게 깜빡인다.
                if (Mathf.FloorToInt(Time.time * 14f) % 2 == 0) flash = WithAlpha(WarningColor, WarningFlashAlpha);
            }
            else if (_state == State.Attack)
            {
                flash = WithAlpha(WarningColor, AttackFlashAlpha);
            }
            else if (_state == State.Recover)
            {
                // 빈틈: 어둡게 보인다.
                tint = new Color(tint.r * RecoverShade, tint.g * RecoverShade, tint.b * RecoverShade, tint.a);
            }

            if (_downed)
            {
                tint = DownedShade;
                flash = Color.clear;
            }
            else
            {
                tint = ClockTint(tint);
                if (Kind == EnemyKind.ClockKnight && _knightFrozen > 0f && _staggerTimer <= 0f && flash.a <= 0f)
                {
                    flash = WithAlpha(FrozenTint, FrozenFlashAlpha);
                }
            }

            _sprite.color = tint;
            _rig.SetFlash(flash);

            string clip = RigClip();
            if (_rigSerial != _stateSerial && (clip == "windup" || clip == "attack" || clip == "recover"))
            {
                // 같은 동작을 연달아 해도 처음부터 다시 보여 준다.
                _rig.Restart(clip);
            }
            else
            {
                _rig.Play(clip);
            }
            _rigSerial = _stateSerial;
        }

        private string RigClip()
        {
            if (_downed) return "down";
            switch (_state)
            {
                case State.Windup: return "windup";
                case State.Attack: return "attack";
                case State.Recover: return "recover";
            }
            if (_body != null && Mathf.Abs(_body.linearVelocity.x) > 0.25f) return "walk";
            return "idle";
        }

        /// <summary>단계가 바뀌며 바탕색이 어두워졌으면, 어두워진 비율만큼 그림을 물들인다.</summary>
        private Color PhaseTint()
        {
            Color original = ColorOf(Kind);
            return new Color(Ratio(_baseColor.r, original.r), Ratio(_baseColor.g, original.g), Ratio(_baseColor.b, original.b), 1f);
        }

        private static float Ratio(float now, float was)
        {
            return was > 0.01f ? Mathf.Clamp01(now / was) : 1f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
