using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>멈춘 시계탑에 쓰는 그림. 배경의 큰 톱니바퀴는 그림 파일 없이 Play 때 만든다.</summary>
    public static class ClockArt
    {
        private static Sprite _gear;

        /// <summary>배경에 쓰는 매끈한 톱니바퀴. 지름이 1유닛이다.</summary>
        public static Sprite BackgroundGear
        {
            get
            {
                if (_gear == null) _gear = BuildGear();
                return _gear;
            }
        }

        /// <summary>천천히 도는 배경 톱니바퀴를 하나 놓는다.</summary>
        public static void CreateBackgroundGear(Transform parent, Vector2 position, float diameter, Color color, float degreesPerSecond)
        {
            SpriteRenderer renderer = RaidArt.Create("Gear", parent, position, new Vector2(diameter, diameter), BackgroundGear, color, -14);
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            renderer.gameObject.AddComponent<GearSpin>().Speed = degreesPerSecond;
        }

        private static Sprite BuildGear()
        {
            const int size = 128;
            const int teeth = 10;
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
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float outer = Mathf.Cos(angle * teeth) > 0.2f ? 1f : 0.82f;
                    // 바퀴살 넷 사이로 구멍이 뚫려 있다.
                    bool window = radius > 0.45f && radius < 0.62f && Mathf.Cos(angle * 4f) < 0.3f;
                    bool filled = radius <= outer && radius >= 0.22f && !window;
                    float edge = filled ? Mathf.Clamp01((outer - radius) * size * 0.5f) : 0f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * edge));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = "BackgroundGear";
            return sprite;
        }
    }

    /// <summary>제자리에서 돈다. 배경 톱니바퀴와 굴러오는 톱니바퀴에 쓴다.</summary>
    public sealed class GearSpin : MonoBehaviour
    {
        /// <summary>1초에 도는 각도. 음수면 시계 방향으로 돈다.</summary>
        public float Speed = 20f;

        private void Update()
        {
            transform.Rotate(0f, 0f, Speed * Time.deltaTime);
        }
    }

    /// <summary>
    /// 멈춘 시계탑의 바닥 가시. 정해진 박자에 맞춰 솟았다가 들어간다.
    /// 솟기 전에 끝이 살짝 올라와 떨리므로 보고 피할 수 있다. 플레이어만 다치고 적은 다치지 않는다.
    /// 대시 중이거나 방금 맞아서 무적이면 그냥 지나간다.
    /// </summary>
    public sealed class SpikeTrap : MonoBehaviour
    {
        /// <summary>한 번 솟았다가 다시 솟을 때까지의 시간, 그 안에서 예고하는 시간과 솟아 있는 시간(초).</summary>
        public const float Period = 2.8f;
        private const float TellTime = 0.7f;
        private const float UpTime = 0.8f;
        private const float Height = 0.5f;
        /// <summary>이 거리 안에 플레이어가 있을 때만 소리를 낸다.</summary>
        private const float HearingRange = 12f;

        private static readonly Color SpikeColor = new Color(0.72f, 0.74f, 0.8f, 1f);
        private static readonly Color SlotColor = new Color(0.08f, 0.06f, 0.05f, 1f);

        private SpriteRenderer _spikes;
        private float _left;
        private float _right;
        private float _top;
        private float _time;
        private int _stage = -1;

        /// <summary>left에서 right까지 top 높이의 바닥에 가시를 깐다. offset만큼 박자를 늦춘다.</summary>
        public static SpikeTrap Create(Transform parent, float left, float right, float top, float offset)
        {
            var go = new GameObject("SpikeTrap");
            go.transform.SetParent(parent, false);
            var trap = go.AddComponent<SpikeTrap>();
            trap._left = left;
            trap._right = right;
            trap._top = top;
            trap._time = offset;

            float center = (left + right) * 0.5f;
            float width = right - left;
            // 바닥에 난 홈. 가시가 숨어 있어도 어디서 솟는지 보인다.
            PrototypeArt.CreateBox("SpikeSlot", go.transform, new Vector2(center, top - 0.05f), new Vector2(width, 0.1f), SlotColor, 2);
            // 땅 그림보다 뒤에 그려서, 들어가 있을 때는 땅에 가려진다.
            trap._spikes = PixelSprites.CreateTiled("Spikes", go.transform, new Vector2(center, top - Height * 0.5f), new Vector2(width, Height), "Spikes", PixelSprites.PixelsPerUnit, SpikeColor, -1);
            return trap;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Repeat(_time, Period);
            float downTime = Period - TellTime - UpTime;

            int stage;
            float lift;
            if (t < downTime)
            {
                stage = 0;
                lift = 0f;
            }
            else if (t < downTime + TellTime)
            {
                // 예고: 끝이 조금 올라와 떨린다.
                stage = 1;
                lift = 0.3f + 0.05f * Mathf.Sin(Time.time * 60f);
            }
            else
            {
                stage = 2;
                lift = 1f;
            }

            Vector3 position = _spikes.transform.localPosition;
            position.y = _top - Height * 0.5f - Height * (1f - lift);
            _spikes.transform.localPosition = position;

            PrototypeGame game = PrototypeGame.Current;
            PlayerController player = game != null ? game.Player : null;
            if (stage != _stage)
            {
                _stage = stage;
                if (player != null && Mathf.Abs(player.Position.x - (_left + _right) * 0.5f) < HearingRange)
                {
                    if (stage == 1) Sfx.Play(Sound.Tick, 0.5f);
                    else if (stage == 2) Sfx.Play(Sound.Spike, 0.6f);
                }
            }

            if (stage != 2 || player == null) return;

            Vector2 feet = player.Feet;
            bool over = feet.x >= _left - 0.3f && feet.x <= _right + 0.3f;
            bool low = feet.y <= _top + Height + 0.1f && feet.y >= _top - 0.5f;
            if (over && low) player.TakeDamage(1, feet.x);
        }
    }

    /// <summary>
    /// 시계 감옥. 플레이어 둘레에 세워지는 높은 벽 두 개와 위에 걸린 시계판이다.
    /// 벽은 플레이어만 막고 감옥을 세운 보스는 막지 않는다. 걷거나 대시해서는 나갈 수 없고,
    /// 자리 바꾸기처럼 순간이동해야 빠져나온다. 시간이 다 되면 어떻게 될지는 보스가 정한다(EnemyClock.cs).
    /// </summary>
    public sealed class ClockCage : MonoBehaviour
    {
        public const float HalfWidth = 2.6f;
        private const float WallHeight = 14f;
        private const float BarHeight = 6f;
        private const float DialHeight = 6.8f;
        private const float DialSize = 1.6f;

        private static readonly Color BarColor = new Color(0.6f, 0.85f, 1f, 0.85f);
        private static readonly Color FillColor = new Color(0.55f, 0.8f, 1f, 0.07f);
        private static readonly Color DialColor = new Color(0.75f, 0.92f, 1f, 0.9f);
        private static readonly Color FrozenColor = new Color(0.75f, 0.95f, 1f, 0.22f);

        private BoxCollider2D[] _walls;
        private SpriteRenderer[] _bars;
        private SpriteRenderer _fill;
        private Transform _hand;
        private float _duration;
        private float _remaining;
        private float _fade = -1f;
        private bool _frozen;

        public float CenterX => transform.position.x;
        public float Remaining => Mathf.Max(0f, _remaining);

        /// <summary>centerX를 가운데로 감옥을 세운다. owner는 벽에 막히지 않는다.</summary>
        public static ClockCage Create(Transform parent, float centerX, float groundTop, float duration, Collider2D owner)
        {
            var go = new GameObject("ClockCage");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centerX, groundTop, 0f);
            var cage = go.AddComponent<ClockCage>();
            cage.Build(groundTop, duration, owner);
            return cage;
        }

        private void Build(float groundTop, float duration, Collider2D owner)
        {
            _duration = duration;
            _remaining = duration;
            float centerX = transform.position.x;

            _walls = new BoxCollider2D[2];
            _bars = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                float x = centerX + (i == 0 ? -HalfWidth : HalfWidth);
                var wall = new GameObject("CageWall");
                wall.transform.SetParent(transform, false);
                wall.transform.position = new Vector3(x, groundTop + WallHeight * 0.5f, 0f);
                BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.3f, WallHeight);
                if (owner != null) Physics2D.IgnoreCollision(collider, owner);
                _walls[i] = collider;

                _bars[i] = PrototypeArt.CreateBox("CageBar", transform, Vector2.zero, new Vector2(0.18f, BarHeight), BarColor, 12);
                _bars[i].transform.position = new Vector3(x, groundTop + BarHeight * 0.5f, 0f);
            }

            _fill = PrototypeArt.CreateBox("CageFill", transform, Vector2.zero, new Vector2(HalfWidth * 2f, BarHeight), FillColor, 3);
            _fill.transform.position = new Vector3(centerX, groundTop + BarHeight * 0.5f, 0f);

            // 위에 걸린 시계판. 바늘이 한 바퀴 돌면 시간이 멈춘다.
            var dialPosition = new Vector2(centerX, groundTop + DialHeight);
            RaidArt.Create("CageDial", transform, dialPosition, new Vector2(DialSize, DialSize), RaidArt.Ring, DialColor, 12);
            SpriteRenderer hand = PrototypeArt.CreateBox("CageHand", transform, Vector2.zero, new Vector2(0.1f, DialSize * 0.42f), DialColor, 13);
            var pivot = new GameObject("CageHandPivot");
            pivot.transform.SetParent(transform, false);
            pivot.transform.position = new Vector3(dialPosition.x, dialPosition.y, 0f);
            hand.transform.SetParent(pivot.transform, false);
            hand.transform.localPosition = new Vector3(0f, DialSize * 0.21f, 0f);
            _hand = pivot.transform;
        }

        /// <summary>그 자리가 감옥 안인지.</summary>
        public bool Contains(Vector2 point)
        {
            return Mathf.Abs(point.x - CenterX) < HalfWidth;
        }

        /// <summary>시간이 멈추며 감옥 안을 벴다. 감옥은 번쩍이고 사라진다.</summary>
        public void Strike()
        {
            OpenWalls();
            _fill.color = new Color(1f, 1f, 1f, 0.6f);
            _fade = 0.3f;
        }

        /// <summary>감옥을 세운 보스가 자기 감옥에 갇혔다. 벽은 열리고 얼어붙은 빛만 seconds 동안 남는다.</summary>
        public void Freeze(float seconds)
        {
            OpenWalls();
            _frozen = true;
            _fill.color = FrozenColor;
            _fade = seconds;
        }

        private void OpenWalls()
        {
            for (int i = 0; i < _walls.Length; i++)
            {
                if (_walls[i] != null) _walls[i].enabled = false;
            }
        }

        private void Update()
        {
            if (_fade >= 0f)
            {
                _fade -= Time.deltaTime;
                if (!_frozen)
                {
                    Color fill = _fill.color;
                    fill.a = Mathf.Max(0f, fill.a - Time.deltaTime * 2f);
                    _fill.color = fill;
                }
                if (_fade <= 0f) Destroy(gameObject);
                return;
            }

            _remaining -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(_remaining / _duration);
            _hand.localRotation = Quaternion.Euler(0f, 0f, -360f * progress);

            // 시간이 멈출 때가 가까우면 창살이 빠르게 깜빡인다.
            float blink = _remaining < 1f && Mathf.FloorToInt(Time.time * 14f) % 2 == 0 ? 0.45f : 1f;
            for (int i = 0; i < _bars.Length; i++)
            {
                Color bar = BarColor;
                bar.a *= blink;
                _bars[i].color = bar;
            }
        }
    }
}
