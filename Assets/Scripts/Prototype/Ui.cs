using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 화면 표시(IMGUI)를 그리는 도구. 픽셀 글꼴(갈무리11), 둥근 판, 외곽선 글자, 픽셀 아이콘, 키 모양 표시를 만든다.
    /// 크기는 세로 540픽셀 화면을 기준으로 적고 U()로 실제 화면 크기에 맞춘다. 1080p에서는 두 배가 된다.
    /// 매 OnGUI의 처음에 Begin()을 불러야 한다.
    /// </summary>
    public static class Ui
    {
        // ---- 색 ----

        /// <summary>글자 외곽선과 그림자. 아주 어두운 보라.</summary>
        public static readonly Color Ink = new Color(0.035f, 0.025f, 0.07f, 1f);
        public static readonly Color PanelFill = new Color(0.06f, 0.045f, 0.1f, 0.82f);
        public static readonly Color PanelEdge = new Color(0.8f, 0.72f, 1f, 0.16f);
        /// <summary>그림자 분신의 보라. 이 게임의 대표 색이다.</summary>
        public static readonly Color Accent = new Color(0.74f, 0.58f, 1f, 1f);
        public static readonly Color TextColor = new Color(0.97f, 0.95f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.72f, 0.68f, 0.84f, 1f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.32f, 1f);
        public static readonly Color Shard = new Color(0.76f, 0.64f, 1f, 1f);
        public static readonly Color Danger = new Color(1f, 0.33f, 0.38f, 1f);
        public static readonly Color Good = new Color(0.52f, 1f, 0.7f, 1f);
        public static readonly Color Heart = new Color(1f, 0.3f, 0.38f, 1f);

        private static readonly float[] OutlineX = { -1f, 0f, 1f, -1f, 1f, -1f, 0f, 1f };
        private static readonly float[] OutlineY = { -1f, -1f, -1f, 0f, 0f, 1f, 1f, 1f };

        private static readonly Dictionary<int, GUIStyle> Styles = new Dictionary<int, GUIStyle>();
        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static readonly GUIContent Content = new GUIContent();
        private static Font _regular;
        private static Font _bold;
        private static bool _fontsTried;
        private static Texture2D _fadeDown;
        private static Texture2D _band;
        private static Texture2D _glow;

        /// <summary>기준 화면(세로 540) 1픽셀이 실제 화면에서 몇 픽셀인지.</summary>
        public static float Scale { get; private set; } = 1f;

        /// <summary>픽셀 아이콘의 한 칸이 실제 화면에서 몇 픽셀인지. 흐려지지 않게 정수로 맞춘다.</summary>
        public static int IconPx { get; private set; } = 1;

        /// <summary>갈무리 글꼴을 불러왔는지. 못 불러오면 유니티 기본 글꼴로 그린다.</summary>
        public static bool HasPixelFont => _regular != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Styles.Clear();
            Icons.Clear();
            _regular = null;
            _bold = null;
            _fontsTried = false;
            _fadeDown = null;
            _band = null;
            _glow = null;
        }

        /// <summary>OnGUI의 처음에 부른다. 화면 크기에 맞춰 배율을 정하고 글꼴을 불러온다.</summary>
        public static void Begin()
        {
            Scale = Mathf.Max(0.6f, Screen.height / 540f);
            IconPx = Mathf.Max(1, Mathf.RoundToInt(Scale * 1.5f));
            if (!_fontsTried)
            {
                _fontsTried = true;
                _regular = Resources.Load<Font>("Fonts/Galmuri11");
                _bold = Resources.Load<Font>("Fonts/Galmuri11-Bold");
                if (_bold == null) _bold = _regular;
            }
        }

        /// <summary>기준 화면의 길이를 실제 화면의 픽셀로 바꾼다.</summary>
        public static float U(float value)
        {
            return value * Scale;
        }

        public static Color Alpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        // ---- 글자 ----

        /// <summary>
        /// 글자 크기(기준 화면 픽셀)를 실제 픽셀로 바꾼다. 갈무리11은 12의 배수에서 가장 또렷하므로,
        /// 가까우면 12의 배수로 맞춘다.
        /// </summary>
        public static int FontPx(float size)
        {
            float raw = Mathf.Max(8f, size * Scale);
            if (!HasPixelFont) return Mathf.RoundToInt(raw);
            float snapped = Mathf.Max(12f, Mathf.Round(raw / 12f) * 12f);
            if (Mathf.Abs(snapped - raw) / raw <= 0.22f) return Mathf.RoundToInt(snapped);
            return Mathf.RoundToInt(raw);
        }

        public static GUIStyle Style(float size, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            int px = FontPx(size);
            int key = px * 64 + (bold ? 32 : 0) + (wrap ? 16 : 0) + (int)anchor;
            GUIStyle style;
            if (Styles.TryGetValue(key, out style) && style != null) return style;

            style = new GUIStyle(GUI.skin.label);
            if (HasPixelFont)
            {
                style.font = bold ? _bold : _regular;
            }
            else if (bold)
            {
                style.fontStyle = FontStyle.Bold;
            }
            style.fontSize = px;
            style.alignment = anchor;
            style.wordWrap = wrap;
            style.richText = false;
            style.clipping = TextClipping.Overflow;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.normal.textColor = Color.white;
            Styles[key] = style;
            return style;
        }

        public static Vector2 Measure(string text, GUIStyle style)
        {
            Content.text = text;
            return style.CalcSize(Content);
        }

        public static float WrappedHeight(string text, GUIStyle style, float width)
        {
            Content.text = text;
            return style.CalcHeight(Content, width);
        }

        /// <summary>외곽선을 두른 글자. 밝은 배경 위에서도 읽힌다. outline이 0이면 외곽선 없이 그린다.</summary>
        public static void Label(Rect rect, string text, GUIStyle style, Color color, float outline = 1f)
        {
            if (string.IsNullOrEmpty(text) || color.a <= 0.01f) return;
            Color previous = GUI.color;
            if (outline > 0f)
            {
                // 글꼴 한 칸 굵기만큼 둘러서 픽셀 글자와 어울리게 한다.
                float o = Mathf.Max(1f, Mathf.Round(style.fontSize / 12f)) * outline;
                GUI.color = new Color(Ink.r, Ink.g, Ink.b, color.a * 0.92f);
                GUI.Label(new Rect(rect.x, rect.y + o * 2f, rect.width, rect.height), text, style);
                for (int i = 0; i < OutlineX.Length; i++)
                {
                    GUI.Label(new Rect(rect.x + OutlineX[i] * o, rect.y + OutlineY[i] * o, rect.width, rect.height), text, style);
                }
            }
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        /// <summary>한 줄 글자를 (x, y)에 쓴다. 쓴 너비를 돌려준다.</summary>
        public static float Text(float x, float y, string text, float size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            GUIStyle style = Style(size, bold, anchor);
            Vector2 measured = Measure(text, style);
            float left = x;
            if (anchor == TextAnchor.UpperCenter || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.LowerCenter) left = x - measured.x * 0.5f;
            else if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight) left = x - measured.x;
            float top = y;
            if (anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.MiddleRight) top = y - measured.y * 0.5f;
            else if (anchor == TextAnchor.LowerLeft || anchor == TextAnchor.LowerCenter || anchor == TextAnchor.LowerRight) top = y - measured.y;
            Label(new Rect(Mathf.Round(left), Mathf.Round(top), measured.x + 2f, measured.y), text, Style(size, bold, TextAnchor.UpperLeft), color);
            return measured.x;
        }

        // ---- 판과 선 ----

        public static void Fill(Rect rect, Color color, float radius = 0f)
        {
            if (color.a <= 0.003f || rect.width <= 0f || rect.height <= 0f) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f));
        }

        public static void Border(Rect rect, Color color, float width, float radius = 0f)
        {
            if (color.a <= 0.003f || rect.width <= 0f || rect.height <= 0f) return;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, Mathf.Max(1f, width), Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f));
        }

        /// <summary>어두운 반투명 판. 위쪽 가장자리에 얇은 빛을 둔다.</summary>
        public static void Panel(Rect rect, float alpha = 1f, Color? edge = null)
        {
            float radius = U(4f);
            Fill(new Rect(rect.x, rect.y + U(2f), rect.width, rect.height), Alpha(Ink, 0.45f * alpha), radius);
            Fill(rect, Alpha(PanelFill, alpha), radius);
            Border(rect, Alpha(edge ?? PanelEdge, alpha), Mathf.Max(1f, Mathf.Round(U(1f))), radius);
            Fill(new Rect(rect.x + radius, rect.y + Mathf.Max(1f, Mathf.Round(U(1f))), rect.width - radius * 2f, Mathf.Max(1f, Mathf.Round(U(1f)))), new Color(1f, 1f, 1f, 0.07f * alpha));
        }

        /// <summary>둥근 끝의 알약 모양 판.</summary>
        public static void Pill(Rect rect, Color fill, Color border)
        {
            float radius = rect.height * 0.5f;
            Fill(rect, fill, radius);
            Border(rect, border, Mathf.Max(1f, Mathf.Round(U(1f))), radius);
        }

        /// <summary>위가 진하고 아래로 갈수록 투명해지는 띠. 화면 위쪽 글자를 받쳐 준다.</summary>
        public static void FadeDown(Rect rect, Color color)
        {
            if (_fadeDown == null) _fadeDown = BuildGradient(false);
            DrawTinted(rect, _fadeDown, color);
        }

        /// <summary>가운데가 진하고 양옆으로 갈수록 투명해지는 가로 띠.</summary>
        public static void Band(Rect rect, Color color)
        {
            if (_band == null) _band = BuildGradient(true);
            DrawTinted(rect, _band, color);
        }

        /// <summary>가장자리가 부드럽게 퍼지는 빛.</summary>
        public static void Glow(Rect rect, Color color)
        {
            if (_glow == null) _glow = BuildGlow();
            DrawTinted(rect, _glow, color);
        }

        private static void DrawTinted(Rect rect, Texture texture, Color color)
        {
            if (color.a <= 0.003f) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        /// <summary>가로 막대. 모서리가 살짝 둥글다.</summary>
        public static void Bar(Rect rect, float ratio, Color fill, Color back)
        {
            float radius = Mathf.Min(U(2f), rect.height * 0.5f);
            Fill(rect, back, radius);
            ratio = Mathf.Clamp01(ratio);
            if (ratio <= 0f) return;
            var filled = new Rect(rect.x, rect.y, Mathf.Max(radius * 2f, rect.width * ratio), rect.height);
            Fill(filled, fill, radius);
            // 위쪽에 밝은 줄을 그어 입체감을 준다.
            Fill(new Rect(filled.x + radius, filled.y, filled.width - radius * 2f, Mathf.Max(1f, Mathf.Round(rect.height * 0.3f))), new Color(1f, 1f, 1f, 0.22f * fill.a));
        }

        // ---- 키 모양 ----

        /// <summary>키보드 키 모양을 그리고 너비를 돌려준다. height는 실제 픽셀.</summary>
        public static float Keycap(float x, float y, string key, float height, float alpha = 1f, bool lit = false)
        {
            GUIStyle style = Style(9f, true, TextAnchor.MiddleCenter);
            float width = Mathf.Max(height, Measure(key, style).x + U(7f));
            var rect = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(width), Mathf.Round(height));
            float radius = U(3f);
            float edge = Mathf.Max(1f, Mathf.Round(U(1f)));
            // 아래쪽 두께를 두어 눌리는 키처럼 보이게 한다.
            Fill(new Rect(rect.x, rect.y + edge * 2f, rect.width, rect.height), Alpha(Ink, 0.85f * alpha), radius);
            Fill(rect, lit ? new Color(0.32f, 0.24f, 0.5f, 0.98f * alpha) : new Color(0.16f, 0.13f, 0.24f, 0.95f * alpha), radius);
            Border(rect, lit ? Alpha(Accent, alpha) : new Color(1f, 1f, 1f, 0.32f * alpha), edge, radius);
            Label(new Rect(rect.x, rect.y - edge * 0.5f, rect.width, rect.height), key, style, Alpha(TextColor, alpha), 0f);
            return rect.width;
        }

        // ---- 픽셀 아이콘 ----

        /// <summary>픽셀 아이콘의 실제 크기.</summary>
        public static Vector2 IconSize(string name, int px)
        {
            Texture2D texture = IconTexture(name);
            if (texture == null) return Vector2.zero;
            return new Vector2(texture.width * px, texture.height * px);
        }

        /// <summary>픽셀 아이콘을 왼쪽 위 (x, y)에 tint 색으로 그린다. 외곽선은 어두운 채로 남는다.</summary>
        public static void Icon(string name, float x, float y, int px, Color tint)
        {
            Texture2D texture = IconTexture(name);
            if (texture == null || tint.a <= 0.01f) return;
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(Mathf.Round(x), Mathf.Round(y), texture.width * px, texture.height * px), texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        /// <summary>가운데가 (cx, cy)가 되게 아이콘을 그린다.</summary>
        public static void IconCentered(string name, float cx, float cy, int px, Color tint)
        {
            Vector2 size = IconSize(name, px);
            Icon(name, cx - size.x * 0.5f, cy - size.y * 0.5f, px, tint);
        }

        private static Texture2D IconTexture(string name)
        {
            Texture2D texture;
            if (Icons.TryGetValue(name, out texture) && texture != null) return texture;
            string[] rows = IconRows(name);
            if (rows == null) return null;
            texture = BuildIcon(rows);
            Icons[name] = texture;
            return texture;
        }

        /// <summary>
        /// 아이콘 그림. o는 외곽선, #은 본색, +는 밝은 곳, -는 어두운 곳이다.
        /// 본색은 흰색에 가깝게 칠해 두고 그릴 때 색을 입힌다.
        /// </summary>
        private static string[] IconRows(string name)
        {
            switch (name)
            {
                case "Heart":
                    return new[]
                    {
                        ".ooo...ooo.",
                        "o+##o.o###o",
                        "o+###o####o",
                        "o#########o",
                        "o########-o",
                        ".o######-o.",
                        "..o####-o..",
                        "...o##-o...",
                        "....o-o....",
                        ".....o.....",
                    };
                case "Coin":
                    return new[]
                    {
                        "..ooooo..",
                        ".o+###-o.",
                        "o+##o##-o",
                        "o#+#o##-o",
                        "o##+o##-o",
                        "o###o#--o",
                        ".o-----o.",
                        "..ooooo..",
                    };
                case "Shard":
                    return new[]
                    {
                        "....o....",
                        "...o+o...",
                        "..o+#-o..",
                        ".o+##--o.",
                        "o+###---o",
                        ".o+##--o.",
                        "..o+#-o..",
                        "...o+o...",
                        "....o....",
                    };
                case "Dash":
                    return new[]
                    {
                        "oo...oo....",
                        "o+o..o+o...",
                        ".o#o..o#o..",
                        "..o#o..o#o.",
                        "...o#o..o#o",
                        "..o#o..o#o.",
                        ".o#o..o#o..",
                        "o-o..o-o...",
                        "oo...oo....",
                    };
                case "Swap":
                    return new[]
                    {
                        "......oo...",
                        "......o+o..",
                        "oooooooo#o.",
                        "o+#######-o",
                        "oooooooo-o.",
                        "......o-o..",
                        "..oo..oo...",
                        ".o+o.......",
                        "o+#oooooooo",
                        "o+#######-o",
                        ".o#oooooooo",
                        "..o-o......",
                        "...oo......",
                    };
                case "Anchor":
                    return new[]
                    {
                        "..ooooo..",
                        "..o+#-o..",
                        "..o+#-o..",
                        ".oo+#-oo.",
                        "o+#####-o",
                        "ooooooooo",
                        "...o#o...",
                        "...o#o...",
                        "....o....",
                    };
                case "Skull":
                    return new[]
                    {
                        ".ooooo.",
                        "o+####o",
                        "o#o#o#o",
                        "o#####o",
                        ".o###o.",
                        ".o#o#o.",
                        "..ooo..",
                    };
                case "Diamond":
                    return new[]
                    {
                        "...o...",
                        "..o+o..",
                        ".o+##o.",
                        "o+###-o",
                        ".o#--o.",
                        "..o-o..",
                        "...o...",
                    };
                case "Dot":
                    return new[]
                    {
                        ".ooo.",
                        "o+##o",
                        "o###o",
                        "o#--o",
                        ".ooo.",
                    };
                case "Question":
                    return new[]
                    {
                        ".ooooo.",
                        "o+###-o",
                        "oooo#-o",
                        "..o#-o.",
                        "..o-o..",
                        "..ooo..",
                        "..o#o..",
                        "..ooo..",
                    };
                case "Door":
                    return new[]
                    {
                        ".ooooo.",
                        "o+###-o",
                        "o#####o",
                        "o###o#o",
                        "o#####o",
                        "o####-o",
                        "ooooooo",
                    };
                default:
                    return null;
            }
        }

        private static Texture2D BuildIcon(string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                string row = rows[height - 1 - y];
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    Color32 color;
                    switch (c)
                    {
                        case 'o': color = new Color32(9, 6, 18, 255); break;
                        case '#': color = new Color32(222, 222, 222, 255); break;
                        case '+': color = new Color32(255, 255, 255, 255); break;
                        case '-': color = new Color32(142, 142, 152, 255); break;
                        default: color = new Color32(0, 0, 0, 0); break;
                    }
                    pixels[y * width + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>horizontal이면 가운데가 진한 가로 띠, 아니면 위가 진한 세로 띠.</summary>
        private static Texture2D BuildGradient(bool horizontal)
        {
            const int size = 64;
            var texture = new Texture2D(horizontal ? size : 1, horizontal ? 1 : size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size];
            for (int i = 0; i < size; i++)
            {
                float t = (i + 0.5f) / size;
                float alpha;
                if (horizontal)
                {
                    float d = Mathf.Abs(t * 2f - 1f);
                    alpha = 1f - d * d;
                }
                else
                {
                    // 텍스처의 아래쪽이 화면의 아래쪽이다. 위로 갈수록 진하다.
                    alpha = t * t;
                }
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(alpha)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D BuildGlow()
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
                    float dx = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    float dy = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    float d = Mathf.Clamp01(Mathf.Max(dx, dy));
                    float alpha = 1f - d;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * alpha * alpha));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
