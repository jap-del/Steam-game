using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 빛의 성소의 유리 발판. 플레이어가 올라서면 떨리며 금이 가다가 잠시 뒤 깨지고, 조금 지나면 다시 생긴다.
    /// 적은 밟아도 깨지지 않는다. 플레이어는 유리 위를 안전한 자리로 기억하지 않으므로
    /// 깨진 뒤 구덩이에 빠져도 유리가 없는 곳으로 돌아간다.
    /// </summary>
    public sealed class GlassPlatform : MonoBehaviour
    {
        /// <summary>올라선 뒤 깨질 때까지의 시간과, 깨진 뒤 다시 생길 때까지의 시간(초).</summary>
        private const float CrackTime = 0.55f;
        private const float RespawnTime = 2.5f;

        private BoxCollider2D _collider;
        private SpriteRenderer[] _renderers;
        private Color[] _colors;
        private Vector3[] _positions;
        private float _crack = -1f;
        private float _gone;

        public void Build(BoxCollider2D collider, SpriteRenderer[] renderers)
        {
            _collider = collider;
            _renderers = renderers;
            _colors = new Color[renderers.Length];
            _positions = new Vector3[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                _colors[i] = renderers[i].color;
                _positions[i] = renderers[i].transform.localPosition;
            }
        }

        private void FixedUpdate()
        {
            if (_collider == null || _gone > 0f || _crack >= 0f) return;

            // 윗면 바로 위를 살펴서 플레이어가 올라서 있으면 금이 가기 시작한다.
            Bounds bounds = _collider.bounds;
            var center = new Vector2(bounds.center.x, bounds.max.y + 0.08f);
            var size = new Vector2(Mathf.Max(0.1f, bounds.size.x - 0.1f), 0.12f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponent<PlayerController>() != null)
                {
                    _crack = 0f;
                    Sfx.Play(Sound.Block, 0.35f);
                    return;
                }
            }
        }

        private void Update()
        {
            if (_renderers == null) return;

            if (_crack >= 0f)
            {
                _crack += Time.deltaTime;
                // 금이 가는 동안 떨리고 하얗게 질린다.
                float t = Mathf.Clamp01(_crack / CrackTime);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;
                    _renderers[i].color = Color.Lerp(_colors[i], Color.white, t * 0.7f);
                    _renderers[i].transform.localPosition = _positions[i] + new Vector3(Random.Range(-0.04f, 0.04f) * t, 0f, 0f);
                }
                if (_crack >= CrackTime) Shatter();
                return;
            }

            if (_gone > 0f)
            {
                _gone -= Time.deltaTime;
                if (_gone <= 0f) Restore();
            }
        }

        /// <summary>금이 다 가면 한 번에 사라진다. 충돌도 함께 꺼진다.</summary>
        private void Shatter()
        {
            _crack = -1f;
            _gone = RespawnTime;
            _collider.enabled = false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].enabled = false;
                _renderers[i].transform.localPosition = _positions[i];
            }

            Sfx.Play(Sound.Shatter);
            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null)
            {
                game.Feel.Sparks(_collider.transform.position, new Color(0.8f, 0.97f, 1f, 1f), 8, 4f);
            }
        }

        private void Restore()
        {
            _gone = 0f;
            _collider.enabled = true;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].enabled = true;
                _renderers[i].color = _colors[i];
            }
        }
    }
}
