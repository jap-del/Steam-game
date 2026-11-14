using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 사각형 대신 쓰는 픽셀 그림. 그림 파일 없이 PixelSpriteData의 글자 격자로 스프라이트를 만든다.
    /// 바탕 층은 밝기만 가진 그림이라 스프라이트의 색으로 물든다. 그래서 적이 노랗게 깜빡이거나
    /// 어두워지는 신호가 사각형일 때와 똑같이 보인다. 눈이나 목도리처럼 색이 정해진 부분은
    /// 덧그림 층으로 따로 올린다.
    /// </summary>
    public static class PixelSprites
    {
        /// <summary>캐릭터 그림의 기준 해상도. 1유닛에 16픽셀.</summary>
        public const float PixelsPerUnit = 16f;

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// 사각형 스프라이트를 name의 픽셀 그림으로 바꾼다. 그림은 size(월드 단위)에 꼭 맞게 늘린다.
        /// 그 이름의 그림이 없으면 사각형을 그대로 둔다.
        /// </summary>
        public static void Dress(SpriteRenderer renderer, string name, Vector2 size, bool withOverlay = true)
        {
            if (renderer == null) return;

            Sprite sprite = Get(name, false, PixelsPerUnit);
            if (sprite == null) return;

            renderer.sprite = sprite;
            float nativeWidth = sprite.rect.width / sprite.pixelsPerUnit;
            float nativeHeight = sprite.rect.height / sprite.pixelsPerUnit;
            renderer.transform.localScale = new Vector3(size.x / nativeWidth, size.y / nativeHeight, 1f);

            if (!withOverlay) return;
            Sprite overlay = Get(name, true, PixelsPerUnit);
            if (overlay == null) return;

            // 덧그림은 바탕 그림의 자식이라 크기와 좌우 뒤집기를 그대로 따라간다.
            var go = new GameObject("Overlay");
            go.transform.SetParent(renderer.transform, false);
            var overlayRenderer = go.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = overlay;
            overlayRenderer.sortingOrder = renderer.sortingOrder + 1;
            overlayRenderer.sharedMaterial = renderer.sharedMaterial;
            go.AddComponent<SpriteOverlay>().Follow(renderer, overlayRenderer);
        }

        /// <summary>그림이 보는 방향을 맞춘다. 그림은 오른쪽을 보게 그려져 있다.</summary>
        public static void Face(SpriteRenderer renderer, int facing)
        {
            if (renderer == null) return;

            Transform target = renderer.transform;
            Vector3 scale = target.localScale;
            float x = Mathf.Abs(scale.x) * (facing < 0 ? -1f : 1f);
            if (scale.x != x)
            {
                scale.x = x;
                target.localScale = scale;
            }
        }

        /// <summary>
        /// 타일 그림을 size만큼 이어 붙인 사각형을 만든다. 지형과 배경에 쓴다.
        /// pixelsPerUnit을 낮추면 타일이 크게 보인다.
        /// </summary>
        public static SpriteRenderer CreateTiled(string name, Transform parent, Vector2 localPosition, Vector2 size, string tile, float pixelsPerUnit, Color color, int sortingOrder)
        {
            Sprite sprite = Get(tile, false, pixelsPerUnit);
            if (sprite == null)
            {
                return PrototypeArt.CreateBox(name, parent, localPosition, size, color, sortingOrder);
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.tileMode = SpriteTileMode.Continuous;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Material material = PrototypeArt.UnlitMaterial;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            return renderer;
        }

        private static Sprite Get(string name, bool overlay, float pixelsPerUnit)
        {
            string key = name + (overlay ? "|overlay|" : "|base|") + Mathf.RoundToInt(pixelsPerUnit);
            Sprite cached;
            // 플레이를 멈추면 만든 텍스처가 사라지므로, 사라진 것은 다시 만든다.
            if (Cache.TryGetValue(key, out cached) && cached != null) return cached;

            string[] rows = PixelSpriteData.Rows(name);
            if (rows == null || rows.Length == 0) return null;

            int width = rows[0].Length;
            int height = rows.Length;
            var pixels = new Color32[width * height];
            bool any = false;
            for (int row = 0; row < height; row++)
            {
                string line = rows[row];
                // 텍스처는 아래쪽 줄부터 채우므로 위아래를 뒤집어 넣는다.
                int offset = (height - 1 - row) * width;
                for (int x = 0; x < width && x < line.Length; x++)
                {
                    Color32 color;
                    if (TryColor(line[x], overlay, out color))
                    {
                        pixels[offset + x] = color;
                        any = true;
                    }
                }
            }
            if (!any) return null;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        private static bool TryColor(char symbol, bool overlay, out Color32 color)
        {
            color = new Color32(0, 0, 0, 0);
            if (symbol == '.') return false;

            Color32 fixedColor;
            bool isFixed = TryFixedColor(symbol, out fixedColor);
            if (overlay)
            {
                if (!isFixed) return false;
                color = fixedColor;
                return true;
            }

            // 덧그림 자리는 바탕 층에서 윤곽선 밝기로 채워 둔다.
            byte value = isFixed ? (byte)41 : Brightness(symbol);
            color = new Color32(value, value, value, 255);
            return true;
        }

        private static byte Brightness(char symbol)
        {
            switch (symbol)
            {
                case 'l': return 255;
                case 'm': return 191;
                case 'd': return 128;
                default: return 41;
            }
        }

        private static bool TryFixedColor(char symbol, out Color32 color)
        {
            switch (symbol)
            {
                case 'o': color = new Color32(255, 255, 255, 255); return true;
                case 'y': color = new Color32(255, 225, 120, 255); return true;
                case 'c': color = new Color32(90, 230, 240, 255); return true;
                case 'r': color = new Color32(255, 90, 70, 255); return true;
                default: color = new Color32(0, 0, 0, 0); return false;
            }
        }
    }
}
