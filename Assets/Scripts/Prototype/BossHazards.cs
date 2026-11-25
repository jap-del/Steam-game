using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 보스 공격에 쓰는 사각형 범위. 먼저 테두리와 함께 자리를 보여 주고(예고), 안쪽이 차오르다가
    /// 다 차면 밝게 켜지며(발동) 그 안에 있는 플레이어를 한 번 다치게 한다. 대시 중이거나 방금 맞아 무적이면
    /// 그냥 지나가고, 발동이 끝날 때까지 다시 판정한다. 발동한 뒤 옆으로 움직이게 할 수도 있다(진자 칼날).
    /// 피해가 0이면 길만 보여 주는 예고 표시다. 움직이는 방향이 있으면 안쪽에 화살표가 흐른다.
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        private const float EdgeThickness = 0.07f;
        private const float FlashTime = 0.07f;

        private PlayerController _player;
        private Color _color;
        private Vector2 _size;
        private Vector2 _velocity;
        private float _warn;
        private float _warnTime;
        private float _active;
        private float _activeTime;
        private int _damage;
        private bool _loud;
        private bool _unavoidable;
        private bool _fired;
        private bool _hitDone;

        private SpriteRenderer _fill;
        private SpriteRenderer _charge;
        private SpriteRenderer _core;
        private SpriteRenderer _glow;
        private readonly SpriteRenderer[] _edges = new SpriteRenderer[4];
        private SpriteRenderer[] _chevrons;
        private int _direction;
        private float _chevronScroll;

        /// <summary>
        /// center를 가운데로 size 크기의 범위를 만든다. warnTime 동안 예고하고 activeTime 동안 발동한다.
        /// velocity를 주면 발동한 뒤 그 속도로 움직인다. 방이 없으면 null.
        /// </summary>
        public static Hazard Spawn(Vector2 center, Vector2 size, float warnTime, float activeTime, Color color, int damage = 1, Vector2 velocity = default(Vector2), bool loud = false, bool unavoidable = false)
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game == null || game.RoomRoot == null) return null;

            var go = new GameObject("Hazard");
            go.transform.SetParent(game.RoomRoot, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var hazard = go.AddComponent<Hazard>();
            hazard._player = game.Player;
            hazard._color = color;
            hazard._size = size;
            hazard._velocity = velocity;
            hazard._warn = warnTime;
            hazard._warnTime = Mathf.Max(0.01f, warnTime);
            hazard._activeTime = Mathf.Max(0.01f, activeTime);
            hazard._damage = damage;
            hazard._loud = loud;
            hazard._unavoidable = unavoidable;
            hazard.BuildVisuals();
            if (Mathf.Abs(velocity.x) > 0.01f) hazard.ShowDirection(velocity.x > 0f ? 1 : -1);
            return hazard;
        }

        /// <summary>안쪽에 direction 쪽으로 흐르는 화살표를 띄운다. 길만 보여 주는 예고에 쓴다.</summary>
        public Hazard ShowDirection(int direction)
        {
            if (direction == 0 || _chevrons != null) return this;
            _direction = direction > 0 ? 1 : -1;
            float height = Mathf.Min(_size.y * 0.55f, 0.75f);
            float width = height * 0.7f;
            int count = Mathf.Max(1, Mathf.FloorToInt(_size.x / 1.3f));
            _chevrons = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                _chevrons[i] = HazardArt.Create("Arrow", transform, Vector2.zero, new Vector2(width * _direction, height), HazardArt.Chevron, HazardArt.WithAlpha(_color, 0f), 5);
            }
            return this;
        }

        private void BuildVisuals()
        {
            Transform root = transform;
            _fill = PrototypeArt.CreateBox("Fill", root, Vector2.zero, _size, HazardArt.WithAlpha(_color, 0.08f), 4);
            _charge = PrototypeArt.CreateBox("Charge", root, Vector2.zero, _size, HazardArt.WithAlpha(_color, 0.16f), 4);

            float t = Mathf.Min(EdgeThickness, Mathf.Min(_size.x, _size.y) * 0.25f);
            _edges[0] = PrototypeArt.CreateBox("EdgeTop", root, new Vector2(0f, (_size.y - t) * 0.5f), new Vector2(_size.x, t), _color, 5);
            _edges[1] = PrototypeArt.CreateBox("EdgeBottom", root, new Vector2(0f, -(_size.y - t) * 0.5f), new Vector2(_size.x, t), _color, 5);
            _edges[2] = PrototypeArt.CreateBox("EdgeLeft", root, new Vector2(-(_size.x - t) * 0.5f, 0f), new Vector2(t, _size.y), _color, 5);
            _edges[3] = PrototypeArt.CreateBox("EdgeRight", root, new Vector2((_size.x - t) * 0.5f, 0f), new Vector2(t, _size.y), _color, 5);

            _glow = HazardArt.Create("Glow", root, Vector2.zero, _size + new Vector2(1.2f, 1.2f), HazardArt.Soft, HazardArt.WithAlpha(_color, 0f), 4);
            _core = PrototypeArt.CreateBox("Core", root, Vector2.zero, _size, HazardArt.WithAlpha(_color, 0f), 5);
            _glow.enabled = false;
            _core.enabled = false;
            UpdateWarnVisuals();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (!_fired)
            {
                _warn -= dt;
                UpdateWarnVisuals();
                if (_warn > 0f) return;

                if (_damage <= 0)
                {
                    Destroy(gameObject);
                    return;
                }
                Fire();
            }

            if (_velocity != Vector2.zero)
            {
                transform.position += new Vector3(_velocity.x, _velocity.y, 0f) * dt;
            }

            if (!_hitDone && _player != null && Overlaps(_player.Position))
            {
                if (_player.TakeDamage(_damage, transform.position.x, _unavoidable)) _hitDone = true;
            }

            _active -= dt;
            UpdateActiveVisuals();
            if (_active <= 0f) Destroy(gameObject);
        }

        /// <summary>
        /// 예고: 희미한 바닥에 테두리를 두르고, 안쪽이 시간에 맞춰 차오른다.
        /// 가로로 긴 범위는 가운데에서 양옆으로, 세로로 긴 범위는 위에서 아래로, 나머지는 가운데에서 바깥으로 찬다.
        /// 발동하기 직전에는 테두리가 하얗게 깜빡인다.
        /// </summary>
        private void UpdateWarnVisuals()
        {
            float progress = 1f - Mathf.Clamp01(_warn / _warnTime);
            bool late = _warn < 0.25f;
            bool blink = late && Mathf.FloorToInt(Time.time * 18f) % 2 == 0;
            float appear = Mathf.Clamp01((_warnTime - _warn) / 0.08f);

            Vector2 chargeSize = _size;
            Vector2 chargeOffset = Vector2.zero;
            float ratio = _size.x / Mathf.Max(0.01f, _size.y);
            if (ratio >= 1.3f)
            {
                chargeSize.x = _size.x * progress;
            }
            else if (ratio <= 1f / 1.3f)
            {
                chargeSize.y = _size.y * progress;
                chargeOffset.y = (_size.y - chargeSize.y) * 0.5f;
            }
            else
            {
                chargeSize = _size * progress;
            }
            _charge.transform.localScale = new Vector3(Mathf.Max(0.001f, chargeSize.x), Mathf.Max(0.001f, chargeSize.y), 1f);
            _charge.transform.localPosition = chargeOffset;
            _charge.color = HazardArt.WithAlpha(_color, (late ? 0.3f : 0.16f + 0.08f * progress) * appear);
            _fill.color = HazardArt.WithAlpha(_color, 0.08f * appear);

            Color edge = blink ? Color.Lerp(_color, Color.white, 0.7f) : _color;
            bool wide = ratio >= 1f;
            for (int i = 0; i < _edges.Length; i++)
            {
                // 긴 쪽 테두리를 더 진하게 그린다.
                bool longSide = wide ? i < 2 : i >= 2;
                float alpha = (longSide ? 0.8f : 0.45f) * appear;
                if (late) alpha = Mathf.Min(1f, alpha + 0.2f);
                _edges[i].color = HazardArt.WithAlpha(edge, alpha);
            }

            UpdateChevrons(appear * (late ? 0.85f : 0.6f), Time.deltaTime * 4.5f);
        }

        private void UpdateChevrons(float alpha, float scrollStep)
        {
            if (_chevrons == null) return;
            _chevronScroll += scrollStep;
            float spacing = _size.x / _chevrons.Length;
            float half = _size.x * 0.5f;
            for (int i = 0; i < _chevrons.Length; i++)
            {
                SpriteRenderer chevron = _chevrons[i];
                if (chevron == null) continue;
                // 범위 안에서 방향 쪽으로 흘러가고, 끝에 닿으면 반대편에서 다시 나온다.
                float along = Mathf.Repeat(i * spacing + _chevronScroll, _size.x);
                float x = (along - half) * _direction;
                chevron.transform.localPosition = new Vector3(x, 0f, 0f);
                float edgeFade = Mathf.Clamp01((half - Mathf.Abs(x)) / 0.5f);
                chevron.color = HazardArt.WithAlpha(_color, alpha * edgeFade);
            }
        }

        private void Fire()
        {
            _fired = true;
            _active = _activeTime;
            _fill.enabled = false;
            _charge.enabled = false;
            _core.enabled = true;
            _glow.enabled = true;
            if (_loud) Sfx.Play(Sound.Beam, 0.6f);
            UpdateActiveVisuals();
        }

        /// <summary>발동: 하얗게 번쩍인 뒤 밝은 색으로 타오르다가 사라진다. 오래 타는 불길은 일렁인다.</summary>
        private void UpdateActiveVisuals()
        {
            float elapsed = _activeTime - _active;
            float ratio = Mathf.Clamp01(_active / _activeTime);
            bool flash = elapsed < FlashTime;
            float alpha = 0.35f + 0.6f * ratio;
            if (_activeTime > 0.6f)
            {
                // 불길처럼 오래 남는 범위는 사라질 때까지 진하게 일렁이다가 끝에서 흐려진다.
                alpha = Mathf.Lerp(0.55f, 0.85f, Mathf.PerlinNoise(Time.time * 9f, transform.position.x)) * Mathf.Clamp01(_active / 0.3f);
            }

            Color hot = Color.Lerp(_color, Color.white, 0.35f);
            _core.color = flash ? new Color(1f, 1f, 1f, 0.95f) : HazardArt.WithAlpha(hot, alpha);
            _glow.color = HazardArt.WithAlpha(_color, (flash ? 0.6f : 0.35f) * ratio + 0.1f);
            float swell = flash ? 1.06f : 1f;
            _core.transform.localScale = new Vector3(_size.x * swell, _size.y * swell, 1f);
            for (int i = 0; i < _edges.Length; i++)
            {
                _edges[i].color = HazardArt.WithAlpha(Color.Lerp(_color, Color.white, 0.6f), alpha);
            }
            UpdateChevrons(0f, 0f);
        }

        private bool Overlaps(Vector2 playerCenter)
        {
            Vector2 half = (_size + PlayerController.BodySize) * 0.5f;
            Vector2 offset = playerCenter - (Vector2)transform.position;
            return Mathf.Abs(offset.x) <= half.x && Mathf.Abs(offset.y) <= half.y;
        }
    }

    /// <summary>공격 예고에 쓰는 그림: 화살표, 부드럽게 퍼지는 빛.</summary>
    public static class HazardArt
    {
        private static Sprite _chevron;
        private static Sprite _soft;

        /// <summary>오른쪽을 가리키는 꺾쇠 화살표. 가로세로 1유닛이다. 가로 크기를 음수로 주면 왼쪽을 가리킨다.</summary>
        public static Sprite Chevron
        {
            get
            {
                if (_chevron == null) _chevron = Build("HazardChevron", ChevronAlpha);
                return _chevron;
            }
        }

        /// <summary>가운데가 꽉 차고 가장자리가 부드럽게 흐려지는 사각형. 빛 번짐에 쓴다.</summary>
        public static Sprite Soft
        {
            get
            {
                if (_soft == null) _soft = Build("HazardSoft", SoftAlpha);
                return _soft;
            }
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        /// <summary>부모 기준 위치에 그림 하나를 만든다. size는 월드 단위 크기.</summary>
        public static SpriteRenderer Create(string name, Transform parent, Vector2 localPosition, Vector2 size, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Material material = PrototypeArt.UnlitMaterial;
            if (material != null) renderer.sharedMaterial = material;
            return renderer;
        }

        private delegate float AlphaAt(float u, float v);

        private static Sprite Build(string name, AlphaAt alphaAt)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alphaAt(u, v))));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = name;
            return sprite;
        }

        private static float ChevronAlpha(float u, float v)
        {
            // 가운데 줄에서 끝이 오른쪽(u = 0.8)에 오고, 위아래 끝에서는 왼쪽(u = 0.25)으로 꺾인다.
            float fromCenter = Mathf.Abs(v * 2f - 1f);
            float line = 0.8f - 0.55f * fromCenter;
            float distance = Mathf.Abs(u - line);
            return Mathf.Clamp01((0.17f - distance) * 20f) * Mathf.Clamp01((1f - fromCenter) * 12f);
        }

        private static float SoftAlpha(float u, float v)
        {
            float d = Mathf.Max(Mathf.Abs(u * 2f - 1f), Mathf.Abs(v * 2f - 1f));
            float inside = Mathf.Clamp01((1f - d) / 0.45f);
            return inside * inside * (3f - 2f * inside);
        }
    }

    /// <summary>
    /// 보스가 움직일 길과 떨어질 자리를 미리 보여 주는 표시. 정해진 시간 동안 떠 있다가 사라지고, 부딪힘은 없다.
    ///   Lane: 돌진할 길. 흐르는 화살표와 멈출 자리의 막대.
    ///   Landing: 뛰어올라 떨어질 자리. 좁혀 오는 고리.
    ///   Aim: 탄이 날아갈 줄.
    ///   Reticle: 노리는 대상에 붙는 조준 표시.
    /// </summary>
    public sealed class PathMarker : MonoBehaviour
    {
        private enum Kind
        {
            Lane,
            Landing,
            Aim,
            Reticle,
        }

        private Kind _kind;
        private Color _color;
        private float _time;
        private float _age;
        private Vector2 _size;
        private int _direction;
        private SpriteRenderer _band;
        private SpriteRenderer _line;
        private SpriteRenderer _end;
        private SpriteRenderer _ring;
        private SpriteRenderer _inner;
        private SpriteRenderer _closing;
        private SpriteRenderer[] _parts;
        private Transform _follow;
        private Vector2 _followOffset;

        /// <summary>from에서 to까지 가로로 달려갈 길. height는 길의 높이(보스 몸 높이)다.</summary>
        public static PathMarker Lane(Vector2 from, Vector2 to, float height, float time, Color color)
        {
            float length = Mathf.Abs(to.x - from.x);
            if (length < 0.2f) return null;
            PathMarker marker = Create("DashPath", new Vector2((from.x + to.x) * 0.5f, from.y), Kind.Lane, time, color);
            if (marker == null) return null;
            marker._size = new Vector2(length, height);
            marker._direction = to.x >= from.x ? 1 : -1;
            Transform root = marker.transform;
            marker._band = PrototypeArt.CreateBox("Band", root, Vector2.zero, marker._size, HazardArt.WithAlpha(color, 0f), 4);
            marker._line = PrototypeArt.CreateBox("Line", root, new Vector2(0f, -height * 0.5f + 0.04f), new Vector2(length, 0.08f), HazardArt.WithAlpha(color, 0f), 5);
            marker._end = PrototypeArt.CreateBox("Stop", root, new Vector2(marker._direction * length * 0.5f, 0f), new Vector2(0.14f, height * 1.15f), HazardArt.WithAlpha(color, 0f), 5);
            int count = Mathf.Max(1, Mathf.FloorToInt(length / 1.1f));
            marker._parts = new SpriteRenderer[count];
            float arrowHeight = Mathf.Min(height * 0.45f, 0.8f);
            for (int i = 0; i < count; i++)
            {
                marker._parts[i] = HazardArt.Create("Arrow", root, Vector2.zero, new Vector2(arrowHeight * 0.7f * marker._direction, arrowHeight), HazardArt.Chevron, HazardArt.WithAlpha(color, 0f), 5);
            }
            return marker;
        }

        /// <summary>feet(발이 닿을 자리)에 떨어질 자리를 표시한다. 고리가 좁혀 오다가 time이 되면 닿는다.</summary>
        public static PathMarker Landing(Vector2 feet, float halfWidth, float time, Color color)
        {
            PathMarker marker = Create("LandingMark", feet, Kind.Landing, time, color);
            if (marker == null) return null;
            Transform root = marker.transform;
            marker._size = new Vector2(halfWidth * 2f, 0.55f);
            marker._line = PrototypeArt.CreateBox("Beam", root, new Vector2(0f, 3f), new Vector2(0.1f, 6f), HazardArt.WithAlpha(color, 0f), 4);
            marker._band = HazardArt.Create("Glow", root, new Vector2(0f, 0.6f), new Vector2(halfWidth * 2.2f, 1.4f), HazardArt.Soft, HazardArt.WithAlpha(color, 0f), 4);
            marker._inner = RaidArt.Create("Fill", root, feet, marker._size, RaidArt.Disc, HazardArt.WithAlpha(color, 0f), 4);
            marker._ring = RaidArt.Create("Ring", root, feet, marker._size, RaidArt.Ring, HazardArt.WithAlpha(color, 0f), 5);
            marker._closing = RaidArt.Create("Closing", root, feet, marker._size * 2f, RaidArt.Ring, HazardArt.WithAlpha(color, 0f), 5);
            return marker;
        }

        /// <summary>from에서 to로 날아갈 줄. 순식간에 뻗었다가 흐려진다.</summary>
        public static PathMarker Aim(Vector2 from, Vector2 to, float time, Color color)
        {
            Vector2 span = to - from;
            if (span.sqrMagnitude < 0.04f) return null;
            PathMarker marker = Create("AimLine", from, Kind.Aim, time, color);
            if (marker == null) return null;
            marker._size = new Vector2(span.magnitude, 0.06f);
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            Transform root = marker.transform;
            marker._line = PrototypeArt.CreateBox("Line", root, Vector2.zero, marker._size, HazardArt.WithAlpha(color, 0f), 5);
            marker._band = HazardArt.Create("Glow", root, Vector2.zero, new Vector2(marker._size.x, 0.4f), HazardArt.Soft, HazardArt.WithAlpha(color, 0f), 4);
            return marker;
        }

        /// <summary>target에 붙어 따라다니는 조준 표시. 네 귀퉁이가 좁혀 온다.</summary>
        public static PathMarker Reticle(Transform target, Vector2 offset, float diameter, float time, Color color)
        {
            if (target == null) return null;
            PathMarker marker = Create("Reticle", (Vector2)target.position + offset, Kind.Reticle, time, color);
            if (marker == null) return null;
            marker._follow = target;
            marker._followOffset = offset;
            marker._size = new Vector2(diameter, diameter);
            Transform root = marker.transform;
            marker._ring = RaidArt.Create("Ring", root, root.position, marker._size, RaidArt.Ring, HazardArt.WithAlpha(color, 0f), 12);
            marker._parts = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                marker._parts[i] = PrototypeArt.CreateBox("Tick", root, Vector2.zero, new Vector2(0.1f, 0.35f), HazardArt.WithAlpha(color, 0f), 12);
            }
            return marker;
        }

        /// <summary>world 자리에 붙는 조준 표시. 따라다니지 않는다.</summary>
        public static PathMarker Reticle(Vector2 world, float diameter, float time, Color color)
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game == null || game.RoomRoot == null) return null;
            var anchor = new GameObject("ReticleAnchor");
            anchor.transform.SetParent(game.RoomRoot, false);
            anchor.transform.position = new Vector3(world.x, world.y, 0f);
            Destroy(anchor, time + 0.1f);
            return Reticle(anchor.transform, Vector2.zero, diameter, time, color);
        }

        private static PathMarker Create(string name, Vector2 position, Kind kind, float time, Color color)
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game == null || game.RoomRoot == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(game.RoomRoot, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var marker = go.AddComponent<PathMarker>();
            marker._kind = kind;
            marker._color = color;
            marker._time = Mathf.Max(0.05f, time);
            return marker;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _time)
            {
                Destroy(gameObject);
                return;
            }

            float progress = _age / _time;
            float appear = Mathf.Clamp01(_age / 0.08f);
            // 끝나기 직전에는 깜빡여서 곧 온다는 것을 알린다.
            float late = _time - _age < 0.2f && Mathf.FloorToInt(Time.time * 18f) % 2 == 0 ? 1f : 0f;
            switch (_kind)
            {
                case Kind.Lane: UpdateLane(appear, late); break;
                case Kind.Landing: UpdateLanding(progress, appear, late); break;
                case Kind.Aim: UpdateAim(progress); break;
                case Kind.Reticle: UpdateReticle(progress, appear); break;
            }
        }

        private void UpdateLane(float appear, float late)
        {
            Color hot = Color.Lerp(_color, Color.white, late * 0.6f);
            _band.color = HazardArt.WithAlpha(_color, 0.1f * appear);
            _line.color = HazardArt.WithAlpha(hot, 0.75f * appear);
            _end.color = HazardArt.WithAlpha(hot, (0.6f + 0.4f * Mathf.Sin(Time.time * 20f)) * appear);

            // 화살표가 달려갈 쪽으로 빠르게 흐른다.
            float half = _size.x * 0.5f;
            float spacing = _size.x / _parts.Length;
            float scroll = _age * 7f;
            for (int i = 0; i < _parts.Length; i++)
            {
                float along = Mathf.Repeat(i * spacing + scroll, _size.x);
                float x = (along - half) * _direction;
                _parts[i].transform.localPosition = new Vector3(x, 0f, 0f);
                float edgeFade = Mathf.Clamp01((half - Mathf.Abs(x)) / 0.6f);
                _parts[i].color = HazardArt.WithAlpha(hot, 0.7f * appear * edgeFade);
            }
        }

        private void UpdateLanding(float progress, float appear, float late)
        {
            Color hot = Color.Lerp(_color, Color.white, late * 0.6f);
            _ring.color = HazardArt.WithAlpha(hot, 0.9f * appear);
            _inner.transform.localScale = new Vector3(_size.x * progress, _size.y * progress, 1f);
            _inner.color = HazardArt.WithAlpha(_color, 0.35f * appear);
            float closing = Mathf.Lerp(2.2f, 1f, progress * progress);
            _closing.transform.localScale = new Vector3(_size.x * closing, _size.y * closing, 1f);
            _closing.color = HazardArt.WithAlpha(hot, 0.6f * appear * progress);
            _line.color = HazardArt.WithAlpha(_color, 0.3f * appear);
            _band.color = HazardArt.WithAlpha(_color, 0.25f * appear * progress);
        }

        private void UpdateAim(float progress)
        {
            // 0.1초 만에 끝까지 뻗고, 뒤로 갈수록 흐려진다.
            float reach = Mathf.Clamp01(_age / 0.1f);
            float alpha = 1f - progress * progress;
            float length = _size.x * reach;
            _line.transform.localScale = new Vector3(length, _size.y, 1f);
            _line.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
            _line.color = HazardArt.WithAlpha(Color.Lerp(_color, Color.white, 0.4f), 0.8f * alpha);
            _band.transform.localScale = new Vector3(length, 0.4f, 1f);
            _band.transform.localPosition = new Vector3(length * 0.5f, 0f, 0f);
            _band.color = HazardArt.WithAlpha(_color, 0.25f * alpha);
        }

        private void UpdateReticle(float progress, float appear)
        {
            if (_follow != null) transform.position = (Vector3)((Vector2)_follow.position + _followOffset);
            float squeeze = Mathf.Lerp(1.5f, 1f, 1f - (1f - progress) * (1f - progress));
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 16f);
            _ring.transform.localScale = new Vector3(_size.x * squeeze, _size.y * squeeze, 1f);
            _ring.transform.localPosition = Vector3.zero;
            _ring.color = HazardArt.WithAlpha(_color, 0.85f * appear * pulse);
            float radius = _size.x * 0.5f * squeeze + 0.25f;
            for (int i = 0; i < _parts.Length; i++)
            {
                float angle = (45f + i * 90f + _age * 120f) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Transform tick = _parts[i].transform;
                tick.localPosition = direction * radius;
                tick.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
                _parts[i].color = HazardArt.WithAlpha(_color, appear * pulse);
            }
        }
    }

    /// <summary>폭발이나 충격파가 퍼지는 고리. ShowBlast가 만든다.</summary>
    public sealed class BlastRing : MonoBehaviour
    {
        private const float Life = 0.28f;

        private SpriteRenderer _disc;
        private SpriteRenderer _ring;
        private Color _color;
        private float _radius;
        private float _age;

        public static void Spawn(Transform parent, Vector2 center, float radius, Color color)
        {
            var go = new GameObject("Blast");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var blast = go.AddComponent<BlastRing>();
            blast._color = color;
            blast._radius = radius;
            float diameter = radius * 2f;
            blast._disc = RaidArt.Create("Flash", go.transform, center, new Vector2(diameter, diameter), RaidArt.Disc, color, 7);
            blast._ring = RaidArt.Create("Ring", go.transform, center, new Vector2(diameter, diameter), RaidArt.Ring, Color.white, 8);
            blast.Apply();
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            Apply();
        }

        private void Apply()
        {
            float t = Mathf.Clamp01(_age / Life);
            float ease = 1f - (1f - t) * (1f - t);
            float diameter = _radius * 2f;
            float ringSize = diameter * Mathf.Lerp(0.6f, 1.12f, ease);
            _ring.transform.localScale = new Vector3(ringSize, ringSize, 1f);
            _ring.transform.localPosition = Vector3.zero;
            _ring.color = HazardArt.WithAlpha(Color.Lerp(Color.white, _color, t), (1f - t) * 0.95f);
            float discSize = diameter * Mathf.Lerp(0.85f, 1f, ease);
            _disc.transform.localScale = new Vector3(discSize, discSize, 1f);
            _disc.transform.localPosition = Vector3.zero;
            _disc.color = HazardArt.WithAlpha(Color.Lerp(Color.white, _color, Mathf.Clamp01(t * 3f)), _color.a * (1f - t));
        }
    }

    /// <summary>
    /// 떨어질 폭탄의 자리. 둥근 범위에 고리가 좁혀 오다가 폭탄이 닿을 때 닿는다. 폭탄이 터지면 폭탄이 지운다.
    /// </summary>
    public sealed class LandingCircle : MonoBehaviour
    {
        private SpriteRenderer _fill;
        private SpriteRenderer _ring;
        private SpriteRenderer _closing;
        private Color _color;
        private float _diameter;
        private float _time;
        private float _age;

        public static GameObject Spawn(Transform parent, Vector2 center, float radius, float time, Color color)
        {
            var go = new GameObject("BombMarker");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(center.x, center.y, 0f);
            var circle = go.AddComponent<LandingCircle>();
            circle._color = color;
            circle._diameter = radius * 2f;
            circle._time = Mathf.Max(0.05f, time);
            var size = new Vector2(circle._diameter, circle._diameter);
            circle._fill = RaidArt.Create("Fill", go.transform, center, size, RaidArt.Disc, HazardArt.WithAlpha(color, 0.12f), 5);
            circle._ring = RaidArt.Create("Ring", go.transform, center, size, RaidArt.Ring, HazardArt.WithAlpha(color, 0.8f), 5);
            circle._closing = RaidArt.Create("Closing", go.transform, center, size * 2f, RaidArt.Ring, HazardArt.WithAlpha(color, 0f), 5);
            return go;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float progress = Mathf.Clamp01(_age / _time);
            bool late = _time - _age < 0.2f && Mathf.FloorToInt(Time.time * 18f) % 2 == 0;
            Color hot = late ? Color.Lerp(_color, Color.white, 0.6f) : _color;
            float closing = Mathf.Lerp(2f, 1f, progress * progress);
            _closing.transform.localScale = new Vector3(_diameter * closing, _diameter * closing, 1f);
            _closing.transform.localPosition = Vector3.zero;
            _closing.color = HazardArt.WithAlpha(hot, 0.7f * Mathf.Clamp01(progress * 3f));
            _fill.color = HazardArt.WithAlpha(_color, 0.1f + 0.2f * progress);
            _ring.color = HazardArt.WithAlpha(hot, 0.85f);
        }
    }

    /// <summary>천장에서 떨어지는 바위 그림. 부딪힘은 따로 만든 Hazard가 맡는다. 바닥에 닿으면 부서진다.</summary>
    public sealed class FallingRock : MonoBehaviour
    {
        private static readonly Color RockColor = new Color(0.55f, 0.5f, 0.44f, 1f);

        private float _x;
        private float _fromY;
        private float _toY;
        private float _time;
        private float _elapsed;

        public static void Spawn(Transform parent, float x, float fromY, float toY, float time)
        {
            SpriteRenderer renderer = PrototypeArt.CreateBox("FallingRock", parent, new Vector2(x, fromY), new Vector2(1.1f, 0.9f), RockColor, 14);
            var rock = renderer.gameObject.AddComponent<FallingRock>();
            rock._x = x;
            rock._fromY = fromY;
            rock._toY = toY;
            rock._time = Mathf.Max(0.05f, time);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _time);
            // 점점 빨라지며 떨어진다.
            float y = Mathf.Lerp(_fromY, _toY, t * t);
            transform.position = new Vector3(_x, y, 0f);
            if (t < 1f) return;

            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null) game.Feel.Sparks(transform.position, RockColor, 6, 5f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 철갑을 두른 성채의 문지기가 떨어뜨린 바위가 바닥에 남은 것. 몸으로 길을 막지 않는다.
    /// 문지기가 돌진하다 부딪히면 부서진다. 이 판정은 문지기가 한다(EnemyCitadel.cs).
    /// </summary>
    public sealed class Boulder : MonoBehaviour
    {
        public const float HalfWidth = 0.7f;
        public const float Height = 1.1f;

        private static readonly Color BodyColor = new Color(0.5f, 0.46f, 0.4f, 1f);
        private static readonly Color TopColor = new Color(0.66f, 0.62f, 0.55f, 1f);

        private SpriteRenderer _body;
        private float _life;

        public static Boulder Create(Transform parent, float x, float groundTop, float life)
        {
            var go = new GameObject("Boulder");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, groundTop + Height * 0.5f, 0f);
            var boulder = go.AddComponent<Boulder>();
            boulder._body = PrototypeArt.CreateBox("Body", go.transform, Vector2.zero, new Vector2(HalfWidth * 2f, Height), BodyColor, 6);
            PrototypeArt.CreateBox("Top", go.transform, new Vector2(0f, Height * 0.5f - 0.12f), new Vector2(HalfWidth * 1.6f, 0.24f), TopColor, 7);
            boulder._life = life;
            return boulder;
        }

        /// <summary>문지기의 돌진에 부서진다.</summary>
        public void Shatter()
        {
            PrototypeGame game = PrototypeGame.Current;
            if (game != null && game.Feel != null) game.Feel.Sparks(transform.position, TopColor, 14, 7f);
            Sfx.Play(Sound.Shatter, 0.7f);
            Destroy(gameObject);
        }

        private void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            // 사라지기 전에 깜빡인다.
            _body.enabled = _life > 2f || Mathf.FloorToInt(Time.time * 8f) % 2 == 0;
        }
    }
}
