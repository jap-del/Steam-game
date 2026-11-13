using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>임시 그래픽. 아트 없이 색칠한 사각형만으로 화면을 구성한다.</summary>
    public static class PrototypeArt
    {
        private static Sprite _square;
        private static Material _material;

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var pixels = new Color32[16];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = new Color32(255, 255, 255, 255);
                    }
                    texture.SetPixels32(pixels);
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    texture.Apply();
                    _square = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                    _square.name = "PrototypeSquare";
                }
                return _square;
            }
        }

        /// <summary>조명 설정과 상관없이 색이 그대로 보이는 스프라이트 재질.</summary>
        public static Material UnlitMaterial
        {
            get
            {
                if (_material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (shader == null)
                    {
                        shader = Shader.Find("Sprites/Default");
                    }
                    if (shader != null)
                    {
                        _material = new Material(shader);
                    }
                }
                return _material;
            }
        }

        /// <summary>색칠한 사각형 하나를 만든다. size는 월드 단위 크기.</summary>
        public static SpriteRenderer CreateBox(string name, Transform parent, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Square;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            Material material = UnlitMaterial;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            return renderer;
        }
    }
}
