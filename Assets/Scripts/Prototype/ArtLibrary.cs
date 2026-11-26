using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>움직임 묶음 하나. 그림 판의 Start 칸부터 Count 칸을 1초에 Fps 칸씩 넘긴다.</summary>
    public readonly struct ArtClip
    {
        public readonly string Name;
        public readonly int Start;
        public readonly int Count;
        public readonly float Fps;
        public readonly bool Loop;

        public ArtClip(string name, int start, int count, float fps, bool loop)
        {
            Name = name;
            Start = start;
            Count = Mathf.Max(1, count);
            Fps = Mathf.Max(0.1f, fps);
            Loop = loop;
        }
    }

    /// <summary>
    /// 그림 판 하나의 생김새. 칸은 왼쪽 위부터 오른쪽으로, 줄이 차면 다음 줄로 놓인다.
    /// 칸마다 Padding만큼 투명한 테두리가 있다. 색 그림 칸 뒤에는 같은 모양의 흰 실루엣 칸이 같은 수만큼 이어진다.
    /// 기준점은 칸의 왼쪽 아래에서 잰 픽셀 위치다. 캐릭터는 발밑, 물건과 타일은 가운데다.
    /// </summary>
    public sealed class ArtSheetInfo
    {
        public readonly string Name;
        public readonly int Width;
        public readonly int Height;
        public readonly int Padding;
        public readonly int Columns;
        public readonly int FrameCount;
        public readonly bool HasSilhouettes;
        public readonly float PivotX;
        public readonly float PivotY;
        public readonly ArtClip[] Clips;

        public ArtSheetInfo(string name, int width, int height, int padding, int columns, int frameCount, bool hasSilhouettes, float pivotX, float pivotY, ArtClip[] clips)
        {
            Name = name;
            Width = width;
            Height = height;
            Padding = padding;
            Columns = Mathf.Max(1, columns);
            FrameCount = Mathf.Max(1, frameCount);
            HasSilhouettes = hasSilhouettes;
            PivotX = pivotX;
            PivotY = pivotY;
            Clips = clips ?? new ArtClip[0];
        }

        public int TotalFrames => HasSilhouettes ? FrameCount * 2 : FrameCount;
        public int TextureWidth => Columns * (Width + Padding * 2);
        public int TextureHeight => ((TotalFrames + Columns - 1) / Columns) * (Height + Padding * 2);
    }

    /// <summary>읽어 들여 칸마다 스프라이트로 자른 그림 판.</summary>
    public sealed class ArtSheet
    {
        public readonly ArtSheetInfo Info;
        private readonly Texture2D _texture;
        private readonly Sprite[] _frames;
        private readonly Sprite[] _silhouettes;

        public ArtSheet(ArtSheetInfo info, Texture2D texture, float pixelsPerUnit)
        {
            Info = info;
            _texture = texture;
            _frames = new Sprite[info.FrameCount];
            for (int i = 0; i < _frames.Length; i++)
            {
                _frames[i] = Slice(i, pixelsPerUnit);
            }
            if (info.HasSilhouettes)
            {
                _silhouettes = new Sprite[info.FrameCount];
                for (int i = 0; i < _silhouettes.Length; i++)
                {
                    _silhouettes[i] = Slice(info.FrameCount + i, pixelsPerUnit);
                }
            }
        }

        public int FrameCount => _frames.Length;
        public bool HasSilhouettes => _silhouettes != null;

        /// <summary>플레이를 멈췄다 다시 켜는 사이에 텍스처나 스프라이트가 사라졌으면 false.</summary>
        public bool IsAlive => _texture != null && _frames.Length > 0 && _frames[0] != null;

        public Sprite Frame(int index)
        {
            return _frames[Mathf.Clamp(index, 0, _frames.Length - 1)];
        }

        public Sprite Silhouette(int index)
        {
            if (_silhouettes == null) return null;
            return _silhouettes[Mathf.Clamp(index, 0, _silhouettes.Length - 1)];
        }

        public bool TryGetClip(string name, out ArtClip clip)
        {
            ArtClip[] clips = Info.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].Name == name)
                {
                    clip = clips[i];
                    return true;
                }
            }
            clip = default;
            return false;
        }

        private Sprite Slice(int index, float pixelsPerUnit)
        {
            ArtSheetInfo info = Info;
            int cellWidth = info.Width + info.Padding * 2;
            int cellHeight = info.Height + info.Padding * 2;
            int column = index % info.Columns;
            int row = index / info.Columns;
            float x = column * cellWidth + info.Padding;
            // 그림 파일은 위에서부터 칸을 놓지만 텍스처 좌표는 아래에서부터 잰다.
            float y = _texture.height - (row * cellHeight + info.Padding) - info.Height;
            var rect = new Rect(x, y, info.Width, info.Height);
            var pivot = new Vector2(info.PivotX / info.Width, info.PivotY / info.Height);
            Sprite sprite = Sprite.Create(_texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = info.Name + "_" + index;
            return sprite;
        }
    }

    /// <summary>
    /// Assets/Resources/Art의 그림 판을 읽어 둔다. 그림 파일이 없거나 크기가 목록과 다르면 null을 돌려주고,
    /// 부르는 쪽은 예전 글자 격자 그림(PixelSprites)을 쓴다.
    /// </summary>
    public static class ArtLibrary
    {
        /// <summary>그림 판의 기준 해상도. 1유닛에 32픽셀. 예전 글자 격자 그림의 두 배다.</summary>
        public const float PixelsPerUnit = 32f;
        private const string Folder = "Art/";

        private static readonly Dictionary<string, ArtSheet> Cache = new Dictionary<string, ArtSheet>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            Missing.Clear();
        }

        public static ArtSheet Get(string name)
        {
            return Get(name, PixelsPerUnit);
        }

        /// <summary>pixelsPerUnit을 낮추면 같은 그림이 크게 보인다. 먼 배경 벽 타일에 쓴다.</summary>
        public static ArtSheet Get(string name, float pixelsPerUnit)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string key = name + "|" + Mathf.RoundToInt(pixelsPerUnit);
            ArtSheet sheet;
            if (Cache.TryGetValue(key, out sheet) && sheet != null && sheet.IsAlive) return sheet;
            if (Missing.Contains(name)) return null;

            ArtSheetInfo info = ArtCatalog.Find(name);
            Texture2D texture = info != null ? Resources.Load<Texture2D>(Folder + name) : null;
            if (texture == null)
            {
                Missing.Add(name);
                return null;
            }
            if (texture.width != info.TextureWidth || texture.height != info.TextureHeight)
            {
                Debug.LogWarning("그림 판 크기가 목록과 다릅니다: " + name + " " + texture.width + "x" + texture.height
                    + " (목록 " + info.TextureWidth + "x" + info.TextureHeight + "). 예전 그림을 씁니다.");
                Missing.Add(name);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sheet = new ArtSheet(info, texture, pixelsPerUnit);
            Cache[key] = sheet;
            return sheet;
        }
    }
}
