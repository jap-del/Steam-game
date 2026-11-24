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
    /// 봉인의 수호자가 보호막을 칠 때 세우는 봉인석. 맞으면 수호자에게 알리고, 켜질지 말지는 수호자가 정한다(EnemyCitadel.cs).
    /// 순서를 보여 줄 때 잠깐 번쩍이고, 켜지면 계속 빛나고, 틀리면 붉게 번쩍인다.
    /// 플레이어와 분신의 공격 모두에 맞지만, 몸으로 길을 막지는 않는다.
    /// </summary>
    public sealed class SealStone : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(0.9f, 1.3f);

        private const float FlashTime = 0.6f;
        private const float RejectTime = 0.6f;

        private static readonly Color DarkColor = new Color(0.35f, 0.45f, 0.6f, 1f);
        private static readonly Color LitColor = new Color(0.7f, 1f, 1f, 1f);
        private static readonly Color RejectColor = new Color(1f, 0.35f, 0.3f, 1f);
        private static readonly Color DullColor = new Color(0.6f, 0.6f, 0.65f, 1f);

        private Enemy _owner;
        private SpriteRenderer _body;
        private SpriteRenderer _glow;
        private bool _lit;
        private float _flash;
        private float _reject;
        private bool _broken;

        /// <summary>몇 번째 봉인석인지(0부터). 수호자가 순서를 맞춰 볼 때 쓴다.</summary>
        public int Index { get; private set; }
        public bool IsLit => !_broken && _lit;

        /// <summary>feet 위에 봉인석을 세운다. 방이 바뀌면 함께 사라지도록 방 아래에 만든다.</summary>
        public static SealStone Create(Transform parent, Vector2 feet, Enemy owner, int index)
        {
            var go = new GameObject("SealStone");
            go.transform.SetParent(parent, false);
            // 발판에서 조금 떠 있다.
            go.transform.position = new Vector3(feet.x, feet.y + Size.y * 0.5f + 0.1f, 0f);
            var stone = go.AddComponent<SealStone>();
            stone._owner = owner;
            stone.Index = index;
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

        /// <summary>공격에 맞았다. 수호자에게 알린다.</summary>
        public void Hit(HitSource source)
        {
            if (_broken || _owner == null) return;
            _owner.OnSealStoneHit(this, source);
        }

        /// <summary>순서를 보여 줄 때 잠깐 밝게 번쩍인다.</summary>
        public void Flash()
        {
            if (_broken) return;
            _flash = FlashTime;
            Sfx.Play(Sound.Chime);
            Sparks(LitColor, 6);
        }

        /// <summary>켜거나 끈다. 켜진 봉인석은 보호막이 깨질 때까지 계속 빛난다.</summary>
        public void SetLit(bool lit)
        {
            if (_broken) return;
            if (lit && !_lit)
            {
                Sfx.Play(Sound.Chime, 0.8f);
                Sparks(LitColor, 8);
            }
            _lit = lit;
        }

        /// <summary>순서가 틀렸다. 붉게 번쩍인다.</summary>
        public void Reject()
        {
            if (_broken) return;
            _reject = RejectTime;
            Sfx.Play(Sound.Deny);
            Sparks(RejectColor, 8);
        }

        /// <summary>반응하지 않는 공격에 맞았다. 둔하게 튕긴다.</summary>
        public void Dull()
        {
            if (_broken) return;
            Sfx.Play(Sound.Block, 0.5f);
            Sparks(DullColor, 3);
        }

        /// <summary>보호막이 깨질 때 함께 부서진다.</summary>
        public void Shatter()
        {
            if (_broken) return;
            _broken = true;
            Sparks(LitColor, 14);
            Destroy(gameObject);
        }

        private void Sparks(Color color, int count)
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null) game.Feel.Sparks(transform.position, color, count, 4f);
        }

        private void Update()
        {
            if (_broken) return;

            if (_flash > 0f) _flash -= Time.deltaTime;
            if (_reject > 0f) _reject -= Time.deltaTime;

            Color body;
            float glow;
            if (_reject > 0f)
            {
                body = RejectColor;
                glow = 0.3f;
            }
            else if (_lit)
            {
                body = LitColor;
                glow = 0.3f + 0.05f * Mathf.Sin(Time.time * 5f);
            }
            else if (_flash > 0f)
            {
                body = Color.Lerp(DarkColor, LitColor, 0.4f + 0.6f * (_flash / FlashTime));
                glow = 0.45f * (_flash / FlashTime);
            }
            else
            {
                body = DarkColor;
                glow = 0.06f + 0.04f * Mathf.Sin(Time.time * 3f);
            }
            _body.color = body;
            Color glowColor = _reject > 0f ? RejectColor : new Color(0.6f, 1f, 1f, 1f);
            glowColor.a = glow;
            _glow.color = glowColor;
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
