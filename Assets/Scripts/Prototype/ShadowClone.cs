using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 플레이어의 몇 초 전 행동을 같은 자리에서 그대로 재현하는 분신.
    /// 충돌체가 없어서 피해를 받지 않고, 공격만 적에게 닿는다.
    /// </summary>
    public sealed class ShadowClone : MonoBehaviour
    {
        private SpriteRenderer _body;
        private SpriteRenderer _slash;
        private Vector2 _previous;
        private Vector2 _target;
        private float _slashTimer;
        private bool _active;
        private float _slashScale = 1f;

        public bool IsActive => _active;
        public Vector2 Position => _target;

        public void Build(Vector2 bodySize, float slashScale, float alpha)
        {
            _slashScale = slashScale;
            _body = PrototypeArt.CreateBox("Body", transform, Vector2.zero, bodySize, new Color(0.45f, 0.25f, 0.85f, alpha), 9);
            _slash = PrototypeArt.CreateBox("Slash", transform, Vector2.zero, Combat.SlashSize * slashScale, new Color(0.7f, 0.5f, 1f, 0.6f), 11);
            _slash.enabled = false;
            SetVisible(false);
        }

        /// <summary>플레이어가 매 물리 틱마다 호출한다. frame은 지연 시간만큼 과거의 플레이어 상태다.</summary>
        public void Replay(ShadowFrame frame, float damage)
        {
            if (!_active)
            {
                _active = true;
                _previous = frame.Position;
                SetVisible(true);
            }
            else
            {
                _previous = _target;
            }
            _target = frame.Position;

            if (frame.Attacked)
            {
                Combat.Slash(frame.Position, frame.Facing, damage, HitSource.Shadow, _slashScale);
                _slash.transform.localPosition = new Vector3(frame.Facing * Combat.SlashReach * _slashScale, 0f, 0f);
                _slash.enabled = true;
                _slashTimer = 0.12f;
            }
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            // 물리 틱 사이를 부드럽게 이어서 보여 준다.
            float t = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            Vector2 shown = Vector2.Lerp(_previous, _target, t);
            transform.position = new Vector3(shown.x, shown.y, 0f);

            if (_slashTimer > 0f)
            {
                _slashTimer -= Time.deltaTime;
                if (_slashTimer <= 0f)
                {
                    _slash.enabled = false;
                }
            }
        }

        private void SetVisible(bool visible)
        {
            if (_body != null)
            {
                _body.enabled = visible;
            }
        }
    }
}
