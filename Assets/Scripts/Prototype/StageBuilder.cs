using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    public struct EnemySpawn
    {
        public EnemyKind Kind;
        /// <summary>적이 설 발 위치.</summary>
        public Vector2 Feet;

        public EnemySpawn(EnemyKind kind, Vector2 feet)
        {
            Kind = kind;
            Feet = feet;
        }
    }

    /// <summary>만들어진 스테이지의 정보.</summary>
    public sealed class StageLayout
    {
        public float Width;
        public Vector2 PlayerSpawn;
        public Vector2 ExitPosition;
        public bool HasDummy;
        public Vector2 DummyFeet;
        public readonly List<EnemySpawn> Enemies = new List<EnemySpawn>();
    }

    /// <summary>
    /// 미리 정해 둔 구간(평지, 계단, 구덩이, 언덕, 탑, 섬)을 무작위 순서로 이어 붙여
    /// 가로로 긴 스테이지를 만든다. 스테이지 번호가 클수록 구간이 많아진다.
    /// </summary>
    public static class StageBuilder
    {
        public const float GroundTop = -2.5f;
        /// <summary>이 높이 아래로 떨어지면 구덩이에 빠진 것으로 본다.</summary>
        public const float KillY = GroundTop - 9f;

        private const float GroundDepth = 7f;
        private const float PlatformThickness = 0.5f;

        private static readonly Color SolidColor = new Color(0.32f, 0.34f, 0.42f, 1f);
        private static readonly Color BackdropColor = new Color(0.13f, 0.14f, 0.2f, 1f);

        private delegate float ChunkBuilder(Transform parent, float x, int stage, StageLayout layout);

        // 바닥 조각. 맞닿은 조각은 하나의 충돌체로 합쳐서 이음매에 걸리지 않게 한다.
        private static readonly List<Vector2> GroundSpans = new List<Vector2>();

        public static StageLayout Build(Transform parent, int stage)
        {
            var layout = new StageLayout();
            GroundSpans.Clear();
            float x = 0f;

            // 시작 구간: 적이 없는 안전한 곳.
            Ground(x, x + 14f);
            layout.PlayerSpawn = new Vector2(x + 4f, GroundTop);
            if (stage == 1)
            {
                layout.HasDummy = true;
                layout.DummyFeet = new Vector2(x + 10f, GroundTop);
            }
            x += 14f;

            ChunkBuilder[] chunks = { Flat, Steps, Pit, Terrace, Tower, Islands };
            int count = Mathf.Min(6 + (stage - 1) * 2, 14);
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, chunks.Length);
                if (pick == previous) pick = (pick + 1) % chunks.Length;
                previous = pick;

                float width = chunks[pick](parent, x, stage, layout);
                Decorate(parent, x, width);
                x += width;
            }

            // 끝 구간: 출구 문.
            Ground(x, x + 16f);
            layout.ExitPosition = new Vector2(x + 11f, GroundTop);
            x += 16f;
            layout.Width = x;

            for (int i = 0; i < GroundSpans.Count; i++)
            {
                float left = GroundSpans[i].x;
                float right = GroundSpans[i].y;
                Solid(parent, "Ground", new Vector2((left + right) * 0.5f, GroundTop - GroundDepth * 0.5f), new Vector2(right - left, GroundDepth));
            }
            GroundSpans.Clear();

            Solid(parent, "WallLeft", new Vector2(-0.5f, GroundTop + 8f), new Vector2(1f, 34f));
            Solid(parent, "WallRight", new Vector2(layout.Width + 0.5f, GroundTop + 8f), new Vector2(1f, 34f));
            return layout;
        }

        // ---- 구간들. 각 함수는 구간의 너비를 돌려준다. ----

        private static float Flat(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 26f);
            Spawn(layout, EnemyKind.Brute, x + 11f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 18f, 0f);
            if (stage >= 2) Spawn(layout, EnemyKind.Shooter, x + 23f, 0f);
            return 26f;
        }

        private static float Steps(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 30f);
            Platform(parent, x + 7f, 1.8f, 4f);
            Platform(parent, x + 13.5f, 3.6f, 4f);
            Platform(parent, x + 21f, 1.8f, 5f);
            Spawn(layout, EnemyKind.Shooter, x + 13.5f, 3.6f);
            Spawn(layout, EnemyKind.Brute, x + 17f, 0f);
            if (stage >= 2) Spawn(layout, EnemyKind.Brute, x + 21f, 1.8f);
            return 30f;
        }

        private static float Pit(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 10f);
            Ground(x + 15f, x + 28f);
            Platform(parent, x + 12.5f, 1.6f, 2f);
            Spawn(layout, EnemyKind.Brute, x + 20f, 0f);
            Spawn(layout, EnemyKind.Shooter, x + 25f, 0f);
            return 28f;
        }

        private static float Terrace(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 30f);
            Solid(parent, "Terrace", new Vector2(x + 15f, GroundTop + 1f), new Vector2(12f, 2f));
            Spawn(layout, EnemyKind.Brute, x + 12.5f, 2f);
            Spawn(layout, EnemyKind.Brute, x + 18f, 2f);
            Spawn(layout, EnemyKind.Shooter, x + 26f, 0f);
            return 30f;
        }

        private static float Tower(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 26f);
            Platform(parent, x + 6f, 2f, 4f);
            Platform(parent, x + 12f, 4f, 4f);
            Platform(parent, x + 18f, 6f, 4f);
            Spawn(layout, EnemyKind.Shooter, x + 18f, 6f);
            Spawn(layout, EnemyKind.Brute, x + 10f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 22f, 0f);
            if (stage >= 3) Spawn(layout, EnemyKind.Shooter, x + 6f, 2f);
            return 26f;
        }

        private static float Islands(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 8f);
            Ground(x + 12f, x + 20f);
            Ground(x + 24f, x + 32f);
            Spawn(layout, EnemyKind.Brute, x + 16f, 0f);
            Spawn(layout, EnemyKind.Shooter, x + 28.5f, 0f);
            return 32f;
        }

        // ---- 재료 ----

        private static void Ground(float left, float right)
        {
            int last = GroundSpans.Count - 1;
            if (last >= 0 && Mathf.Abs(GroundSpans[last].y - left) < 0.001f)
            {
                GroundSpans[last] = new Vector2(GroundSpans[last].x, right);
            }
            else
            {
                GroundSpans.Add(new Vector2(left, right));
            }
        }

        /// <summary>바닥에서 height만큼 높은 곳에 윗면이 오는 발판.</summary>
        private static void Platform(Transform parent, float centerX, float height, float width)
        {
            Solid(parent, "Platform", new Vector2(centerX, GroundTop + height - PlatformThickness * 0.5f), new Vector2(width, PlatformThickness));
        }

        private static void Solid(Transform parent, string name, Vector2 center, Vector2 size)
        {
            SpriteRenderer renderer = PrototypeArt.CreateBox(name, parent, center, size, SolidColor, 0);
            renderer.gameObject.AddComponent<BoxCollider2D>();
        }

        private static void Spawn(StageLayout layout, EnemyKind kind, float x, float heightAboveGround)
        {
            layout.Enemies.Add(new EnemySpawn(kind, new Vector2(x, GroundTop + heightAboveGround)));
        }

        /// <summary>움직임이 눈에 보이도록 배경에 어두운 기둥을 세운다. 충돌하지 않는다.</summary>
        private static void Decorate(Transform parent, float x, float width)
        {
            for (int i = 0; i < 3; i++)
            {
                float pillarWidth = Random.Range(1f, 3f);
                float pillarHeight = Random.Range(3f, 10f);
                float centerX = x + Random.Range(1f, width - 1f);
                PrototypeArt.CreateBox("Backdrop", parent, new Vector2(centerX, GroundTop + pillarHeight * 0.5f), new Vector2(pillarWidth, pillarHeight), BackdropColor, -10);
            }
        }
    }
}
