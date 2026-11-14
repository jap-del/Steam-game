using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 플레이어의 몇 초 전 행동을 같은 자리에서 그대로 재현하는 분신.
    /// 충돌체가 없어서 피해를 받지 않고, 공격만 적에게 닿는다.
    /// 고정하면 그 자리에 멈춰 서서 플레이어가 공격할 때 함께 공격한다.
    /// </summary>
    public sealed class ShadowClone : MonoBehaviour
    {
        private SpriteRenderer _body;
        private SpriteRenderer _slash;
        private Color _bodyColor;
        private Vector2 _previous;
        private Vector2 _target;
        private float _slashTimer;
        private float _slashScale = 1f;
        private bool _active;
        private bool _anchored;

        public bool IsActive => _active;
        public bool IsAnchored => _anchored;
        public Vector2 Position => _target;

        public void Build(Vector2 bodySize, float slashScale, float alpha)
        {
            _slashScale = slashScale;
            _bodyColor = new Color(0.45f, 0.25f, 0.85f, alpha);
            _body = PrototypeArt.CreateBox("Body", transform, Vector2.zero, bodySize, _bodyColor, 9);
            // 분신은 플레이어와 같은 그림을 한 가지 색으로만 칠한 실루엣이다.
            PixelSprites.Dress(_body, "Player", bodySize, false);
            _slash = PrototypeArt.CreateBox("Slash", transform, Vector2.zero, Combat.SlashSize * slashScale, new Color(0.7f, 0.5f, 1f, 0.6f), 11);
            PixelSprites.Dress(_slash, "Slash", Combat.SlashSize * slashScale);
            _slash.enabled = false;
            _body.enabled = false;
        }

        /// <summary>플레이어가 매 물리 틱마다 호출한다. frame은 지연 시간만큼 과거의 플레이어 상태다.</summary>
        public void Replay(ShadowFrame frame, float damage)
        {
            // 고정된 동안에는 과거 행동을 따라 하지 않는다.
            if (_anchored) return;

            if (!_active)
            {
                _active = true;
                _previous = frame.Position;
                _body.enabled = true;
            }
            else
            {
                _previous = _target;
            }
            _target = frame.Position;
            PixelSprites.Face(_body, frame.Facing);

            if (frame.Attacked) Attack(frame.Position, frame.Facing, damage);
        }

        /// <summary>고정하면 지금 자리에 멈추고, 풀면 다시 과거 행동을 따라 한다.</summary>
        public void SetAnchored(bool anchored)
        {
            if (!_active) return;

            _anchored = anchored;
            _previous = _target;
            _body.color = anchored ? new Color(0.75f, 0.55f, 1f, 0.9f) : _bodyColor;
        }

        /// <summary>고정된 분신이 플레이어의 공격에 맞춰 바로 공격한다.</summary>
        public void AttackNow(int facing, float damage)
        {
            if (!_active) return;
            Attack(_target, facing, damage);
        }

        public void MoveTo(Vector2 position)
        {
            _previous = position;
            _target = position;
        }

        private void Attack(Vector2 origin, int facing, float damage)
        {
            // 가까이에 적이 있으면 그쪽을 보고 공격한다. 헛손질을 줄이기 위함이다.
            facing = Combat.FacingToNearestEnemy(origin, facing, 2.6f * _slashScale);
            Combat.Slash(origin, facing, damage, HitSource.Shadow, _slashScale);
            _slash.transform.localPosition = new Vector3(facing * Combat.SlashReach * _slashScale, 0f, 0f);
            PixelSprites.Face(_slash, facing);
            PixelSprites.Face(_body, facing);
            _slash.enabled = true;
            _slashTimer = 0.12f;
        }

        private void Update()
        {
            if (!_active) return;

            // 물리 틱 사이를 부드럽게 이어서 보여 준다.
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            Vector2 shown = Vector2.Lerp(_previous, _target, t);
            transform.position = new Vector3(shown.x, shown.y, 0f);

            if (_slashTimer > 0f)
            {
                _slashTimer -= Time.deltaTime;
                if (_slashTimer <= 0f) _slash.enabled = false;
            }
        }
    }
}
