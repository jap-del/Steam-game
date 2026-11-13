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

    /// <summary>방의 종류. 한 스테이지는 방 여섯 개로 이루어진다.</summary>
    public enum RoomType
    {
        Mob = 0,
        MidBoss = 1,
        Shop = 2,
        Puzzle = 3,
        Boss = 4,
    }

    /// <summary>만들어진 방의 정보.</summary>
    public sealed class StageLayout
    {
        public RoomType Type;
        public float Width;
        public Vector2 PlayerSpawn;
        public Vector2 ExitPosition;
        public bool HasDummy;
        public Vector2 DummyFeet;
        public readonly List<EnemySpawn> Enemies = new List<EnemySpawn>();
        /// <summary>상점 물건이 놓일 자리 (발 위치).</summary>
        public readonly List<Vector2> ShopSlots = new List<Vector2>();
        /// <summary>퍼즐방의 발판 자리 (윗면 가운데).</summary>
        public readonly List<Vector2> Plates = new List<Vector2>();
    }

    /// <summary>
    /// 방을 만든다. 전투방은 미리 정해 둔 구간(평지, 계단, 구덩이, 언덕, 탑, 섬)을
    /// 무작위 순서로 이어 붙이고, 보스방과 상점, 퍼즐방은 정해진 모양으로 만든다.
    /// </summary>
    public static class StageBuilder
    {
        public const float GroundTop = -2.5f;
        /// <summary>이 높이 아래로 떨어지면 구덩이에 빠진 것으로 본다.</summary>
        public const float KillY = GroundTop - 9f;

        private const float GroundDepth = 7f;
        private const float PlatformThickness = 0.5f;
        /// <summary>밟을 수 있는 윗면에 얹는 이끼 띠의 두께. 그림에서 4픽셀이다.</summary>
        private const float CapHeight = 0.25f;

        private static readonly Color SolidColor = new Color(0.32f, 0.34f, 0.42f, 1f);
        private static readonly Color BackdropColor = new Color(0.13f, 0.14f, 0.2f, 1f);
        private static readonly Color CapColor = new Color(0.36f, 0.58f, 0.55f, 1f);
        private static readonly Color FarWallColor = new Color(0.105f, 0.115f, 0.165f, 1f);

        private delegate float ChunkBuilder(Transform parent, float x, int stage, StageLayout layout);

        // 바닥 조각. 맞닿은 조각은 하나의 충돌체로 합쳐서 이음매에 걸리지 않게 한다.
        private static readonly List<Vector2> GroundSpans = new List<Vector2>();

        public static StageLayout Build(Transform parent, RoomType type, int stage, bool withDummy)
        {
            var layout = new StageLayout();
            layout.Type = type;
            GroundSpans.Clear();

            switch (type)
            {
                case RoomType.MidBoss:
                    BuildBossRoom(parent, layout, EnemyKind.MidBoss);
                    break;
                case RoomType.Boss:
                    BuildBossRoom(parent, layout, EnemyKind.Boss);
                    break;
                case RoomType.Shop:
                    BuildShopRoom(layout);
                    break;
                case RoomType.Puzzle:
                    BuildPuzzleRoom(parent, layout);
                    break;
                default:
                    BuildMobRoom(parent, layout, stage, withDummy);
                    break;
            }

            for (int i = 0; i < GroundSpans.Count; i++)
            {
                float left = GroundSpans[i].x;
                float right = GroundSpans[i].y;
                Solid(parent, "Ground", new Vector2((left + right) * 0.5f, GroundTop - GroundDepth * 0.5f), new Vector2(right - left, GroundDepth));
                Cap(parent, left, right, GroundTop);
            }
            GroundSpans.Clear();

            Solid(parent, "WallLeft", new Vector2(-0.5f, GroundTop + 8f), new Vector2(1f, 34f));
            Solid(parent, "WallRight", new Vector2(layout.Width + 0.5f, GroundTop + 8f), new Vector2(1f, 34f));

            // 방 뒤쪽을 덮는 먼 벽. 타일을 두 배 크기로 깔아서 멀리 있는 것처럼 보이게 한다.
            PixelSprites.CreateTiled("FarWall", parent, new Vector2(layout.Width * 0.5f, GroundTop + 6f), new Vector2(layout.Width + 40f, 40f), "Stone", PixelSprites.PixelsPerUnit * 0.5f, FarWallColor, -20);
            return layout;
        }

        // ---- 방 종류 ----

        /// <summary>전투방: 안전한 시작 구간, 무작위 구간 몇 개, 출구 구간.</summary>
        private static void BuildMobRoom(Transform parent, StageLayout layout, int stage, bool withDummy)
        {
            float x = 0f;
            float startWidth = withDummy ? 13f : 8f;
            Ground(x, x + startWidth);
            layout.PlayerSpawn = new Vector2(x + 3f, GroundTop);
            if (withDummy)
            {
                layout.HasDummy = true;
                layout.DummyFeet = new Vector2(x + 9f, GroundTop);
            }
            x += startWidth;

            ChunkBuilder[] chunks = { Flat, Steps, Pit, Terrace, Tower, Islands };
            int count = stage >= 3 ? 3 : 2;
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

            Ground(x, x + 10f);
            layout.ExitPosition = new Vector2(x + 6.5f, GroundTop);
            layout.Width = x + 10f;
        }

        /// <summary>보스방: 구덩이 없는 넓은 평지와 발판 두 개.</summary>
        private static void BuildBossRoom(Transform parent, StageLayout layout, EnemyKind boss)
        {
            const float width = 40f;
            Ground(0f, width);
            Platform(parent, 11f, 2.2f, 4f);
            Platform(parent, 29f, 2.2f, 4f);
            Decorate(parent, 0f, width);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(boss, new Vector2(27f, GroundTop)));
            layout.ExitPosition = new Vector2(37f, GroundTop);
            layout.Width = width;
        }

        /// <summary>상점: 물건 자리 세 개가 있는 작은 방.</summary>
        private static void BuildShopRoom(StageLayout layout)
        {
            const float width = 30f;
            Ground(0f, width);
            layout.PlayerSpawn = new Vector2(3f, GroundTop);
            layout.ShopSlots.Add(new Vector2(9f, GroundTop));
            layout.ShopSlots.Add(new Vector2(14f, GroundTop));
            layout.ShopSlots.Add(new Vector2(19f, GroundTop));
            layout.ExitPosition = new Vector2(26f, GroundTop);
            layout.Width = width;
        }

        /// <summary>퍼즐방: 떨어져 있는 발판 두 개를 동시에 밟아야 한다. 분신을 써야 풀 수 있다.</summary>
        private static void BuildPuzzleRoom(Transform parent, StageLayout layout)
        {
            const float width = 44f;
            Ground(0f, width);
            Decorate(parent, 0f, width);
            layout.PlayerSpawn = new Vector2(3f, GroundTop);
            layout.Plates.Add(new Vector2(12f, GroundTop));

            if (Random.value < 0.5f)
            {
                // 두 번째 발판이 높은 곳에 있는 변형.
                Platform(parent, 25f, 1.8f, 3f);
                Platform(parent, 31f, 3.4f, 4f);
                layout.Plates.Add(new Vector2(31f, GroundTop + 3.4f));
            }
            else
            {
                layout.Plates.Add(new Vector2(30f, GroundTop));
            }

            layout.ExitPosition = new Vector2(40f, GroundTop);
            layout.Width = width;
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
            Cap(parent, x + 9f, x + 21f, GroundTop + 2f);
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

        /// <summary>
        /// 바닥에서 height만큼 높은 곳에 윗면이 오는 발판.
        /// 아래에서는 뚫고 올라갈 수 있고 위에서는 밟고 설 수 있다. 적도 마찬가지다.
        /// </summary>
        private static void Platform(Transform parent, float centerX, float height, float width)
        {
            BoxCollider2D collider = Solid(parent, "Platform", new Vector2(centerX, GroundTop + height - PlatformThickness * 0.5f), new Vector2(width, PlatformThickness));
            var effector = collider.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            collider.usedByEffector = true;
            Cap(parent, centerX - width * 0.5f, centerX + width * 0.5f, GroundTop + height);
        }

        private static BoxCollider2D Solid(Transform parent, string name, Vector2 center, Vector2 size)
        {
            SpriteRenderer renderer = PixelSprites.CreateTiled(name, parent, center, size, "Stone", PixelSprites.PixelsPerUnit, SolidColor, 0);
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            // 타일로 그린 스프라이트는 크기를 늘리지 않으므로 충돌체 크기를 직접 정한다.
            collider.size = size;
            collider.offset = Vector2.zero;
            return collider;
        }

        /// <summary>밟을 수 있는 윗면에 이끼 띠를 얹는다. 충돌하지 않는다.</summary>
        private static void Cap(Transform parent, float left, float right, float top)
        {
            PixelSprites.CreateTiled("Cap", parent, new Vector2((left + right) * 0.5f, top - CapHeight * 0.5f), new Vector2(right - left, CapHeight), "Cap", PixelSprites.PixelsPerUnit, CapColor, 1);
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
                PixelSprites.CreateTiled("Backdrop", parent, new Vector2(centerX, GroundTop + pillarHeight * 0.5f), new Vector2(pillarWidth, pillarHeight), "Stone", PixelSprites.PixelsPerUnit, BackdropColor, -10);
            }
        }
    }
}
