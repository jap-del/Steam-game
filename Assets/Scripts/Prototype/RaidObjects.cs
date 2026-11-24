using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>보스 기믹에 쓰는 둥근 그림. 그림 파일 없이 Play 때 만든다.</summary>
    public static class RaidArt
    {
        private static Sprite _disc;
        private static Sprite _ring;

        /// <summary>속이 꽉 찬 원. 지름이 1유닛이다.</summary>
        public static Sprite Disc
        {
            get
            {
                if (_disc == null) _disc = BuildCircle("RaidDisc", 0f);
                return _disc;
            }
        }

        /// <summary>테두리만 있는 원. 지름이 1유닛이다.</summary>
        public static Sprite Ring
        {
            get
            {
                if (_ring == null) _ring = BuildCircle("RaidRing", 0.82f);
                return _ring;
            }
        }

        /// <summary>원 그림 하나를 만든다. size는 월드 단위의 가로세로 지름.</summary>
        public static SpriteRenderer Create(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Material material = PrototypeArt.UnlitMaterial;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        private static Sprite BuildCircle(string name, float inner)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    // 가장자리를 1픽셀쯤 부드럽게 한다.
                    float outer = Mathf.Clamp01((1f - distance) * size * 0.5f);
                    float hole = inner > 0f ? Mathf.Clamp01((distance - inner) * size * 0.5f) : 1f;
                    byte alpha = (byte)Mathf.RoundToInt(255f * outer * hole);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }
    }

    /// <summary>
    /// 봉인의 수호자가 보호막을 칠 때 생기는 봉인석. 맞으면 잠깐 빛난다.
    /// 두 봉인석이 동시에 빛나고 있으면 보호막이 깨진다. 이 판정은 보스가 한다.
    /// 플레이어와 분신의 공격 모두에 반응하고, 몸으로 길을 막지 않는다.
    /// </summary>
    public sealed class SealStone : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(0.9f, 1.3f);

        /// <summary>한 번 맞으면 빛나 있는 시간(초).</summary>
        public const float LitDuration = 1.2f;

        private static readonly Color DarkColor = new Color(0.35f, 0.45f, 0.6f, 1f);
        private static readonly Color LitColor = new Color(0.7f, 1f, 1f, 1f);

        private SpriteRenderer _body;
        private SpriteRenderer _glow;
        private float _lit;
        private bool _broken;

        public bool IsLit => !_broken && _lit > 0f;
        /// <summary>남은 빛의 비율. 1이면 방금 맞은 것이다.</summary>
        public float LitRatio => _broken ? 0f : Mathf.Clamp01(_lit / LitDuration);

        /// <summary>feet 위에 봉인석을 세운다. 방이 바뀌면 함께 사라지도록 방 아래에 만든다.</summary>
        public static SealStone Create(Transform parent, Vector2 feet)
        {
            var go = new GameObject("SealStone");
            go.transform.SetParent(parent, false);
            // 발판에서 조금 떠 있다.
            go.transform.position = new Vector3(feet.x, feet.y + Size.y * 0.5f + 0.1f, 0f);
            var stone = go.AddComponent<SealStone>();
            stone.Build();
            return stone;
        }

        private void Build()
        {
            _glow = RaidArt.Create("Glow", transform, transform.position, Size * 2.2f, RaidArt.Disc, new Color(0.6f, 1f, 1f, 0f), 7);
            _body = PrototypeArt.CreateBox("Body", transform, Vector2.zero, Size, DarkColor, 9);
            PixelSprites.Dress(_body, "Seal", Size, false);

            var collider = gameObject.AddComponent<BoxCollider2D>();
            collider.size = Size;
            collider.isTrigger = true;
        }

        /// <summary>공격에 맞았다. 다시 맞으면 빛나는 시간이 처음부터 다시 흐른다.</summary>
        public void Hit(HitSource source)
        {
            if (_broken) return;

            bool wasLit = _lit > 0f;
            _lit = LitDuration;
            if (!wasLit) Sfx.Play(Sound.Chime, source == HitSource.Shadow ? 0.8f : 1f);

            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null)
            {
                game.Feel.Sparks(transform.position, LitColor, 4, 3.5f);
            }
        }

        /// <summary>보호막이 깨질 때 함께 부서진다.</summary>
        public void Shatter()
        {
            if (_broken) return;
            _broken = true;

            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null)
            {
                game.Feel.Sparks(transform.position, LitColor, 14, 7f);
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            if (_broken) return;

            if (_lit > 0f) _lit -= Time.deltaTime;
            float ratio = LitRatio;
            _body.color = Color.Lerp(DarkColor, LitColor, ratio > 0f ? 0.5f + ratio * 0.5f : 0f);
            // 빛나는 동안 둘레에 빛이 퍼진다. 꺼질 때가 가까우면 깜빡인다.
            float blink = ratio > 0f && ratio < 0.35f && Mathf.FloorToInt(Time.time * 12f) % 2 == 0 ? 0.4f : 1f;
            _glow.color = new Color(0.6f, 1f, 1f, ratio > 0f ? 0.35f * blink : 0.06f + 0.04f * Mathf.Sin(Time.time * 3f));
        }
    }

    /// <summary>
    /// 백광의 군주가 내리는 빛기둥. 먼저 희미한 기둥으로 떨어질 자리를 보여 주고, 잠시 뒤 밝게 내리꽂는다.
    /// 내리꽂는 순간 기둥 안에 있으면 피해를 입는다. 대시로 통과할 수 있다.
    /// </summary>
    public sealed class LightPillar : MonoBehaviour
    {
        public const float HalfWidth = 0.7f;
        private const float ActiveTime = 0.3f;
        private const float Height = 30f;

        private PlayerController _player;
        private SpriteRenderer _renderer;
        private float _warn;
        private float _active;
        private bool _fired;
        private bool _hitDone;

        public static void Spawn(Transform parent, PlayerController player, float x, float warnTime)
        {
            SpriteRenderer renderer = PrototypeArt.CreateBox("LightPillar", parent, new Vector2(x, StageBuilder.GroundTop + Height * 0.5f - 4f), new Vector2(HalfWidth * 2f, Height), new Color(1f, 0.95f, 0.6f, 0.14f), 4);
            var pillar = renderer.gameObject.AddComponent<LightPillar>();
            pillar._player = player;
            pillar._renderer = renderer;
            pillar._warn = warnTime;
        }

        private void Update()
        {
            if (!_fired)
            {
                _warn -= Time.deltaTime;
                // 떨어지기 직전에는 더 진하게 깜빡인다.
                float pulse = _warn < 0.3f && Mathf.FloorToInt(Time.time * 16f) % 2 == 0 ? 0.32f : 0.16f;
                _renderer.color = new Color(1f, 0.95f, 0.6f, pulse);
                if (_warn > 0f) return;

                _fired = true;
                _active = ActiveTime;
                transform.localScale = new Vector3(HalfWidth * 2.4f, Height, 1f);
                _renderer.color = new Color(1f, 0.98f, 0.82f, 0.9f);
                Sfx.Play(Sound.Beam, 0.8f);
                PrototypeGame game = PrototypeGame.Current;
                if (game != null && game.Feel != null) game.Feel.Shake(0.06f, 0.08f);
            }

            if (!_hitDone && _player != null)
            {
                Vector2 position = _player.Position;
                if (Mathf.Abs(position.x - transform.position.x) <= HalfWidth + PlayerController.BodySize.x * 0.5f)
                {
                    // 대시 중이거나 방금 맞아서 무적이면 그냥 지나가고, 기둥이 사라질 때까지 다시 판정한다.
                    if (_player.TakeDamage(1, transform.position.x)) _hitDone = true;
                }
            }

            _active -= Time.deltaTime;
            Color color = _renderer.color;
            color.a = Mathf.Clamp01(_active / ActiveTime) * 0.9f;
            _renderer.color = color;
            if (_active <= 0f) Destroy(gameObject);
        }
    }
}
