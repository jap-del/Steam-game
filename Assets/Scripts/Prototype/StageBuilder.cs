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
        /// <summary>스테이지 앞에 머무는 거점. 첫 거점에서는 난이도를 고르고, 그 뒤의 거점에서는 강화를 산다.</summary>
        Hub = 5,
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
        /// <summary>false이면 닿아서 나가는 출구 문이 없다. 첫 거점은 난이도 문으로만 나간다.</summary>
        public bool HasExit = true;
        /// <summary>첫 거점의 난이도 문 자리 (발 위치). Difficulty.Levels와 같은 순서다.</summary>
        public readonly List<Vector2> DifficultyDoors = new List<Vector2>();
        /// <summary>첫 거점의 이벤트 문. 나중에 이벤트나 DLC에 쓴다.</summary>
        public bool HasEventDoor;
        public Vector2 EventDoor;
        /// <summary>2스테이지 거점부터 있는 강화의 제단.</summary>
        public bool HasAltar;
        public Vector2 Altar;
        /// <summary>보스 기믹에 쓰는 자리 (발 위치). 1스테이지 보스방에서는 봉인석이 놓인다.</summary>
        public readonly List<Vector2> GimmickPoints = new List<Vector2>();
    }

    /// <summary>
    /// 방을 만든다. 전투방은 미리 정해 둔 구간(평지, 계단, 구덩이, 언덕, 탑, 섬)을
    /// 무작위 순서로 이어 붙이고, 보스방과 상점, 퍼즐방은 정해진 모양으로 만든다.
    /// 스테이지마다 지역이 다르다. 1스테이지는 무너진 성채, 2스테이지는 빛의 성소, 3스테이지는 멈춘 시계탑이다.
    /// 빛의 성소에는 밟으면 금이 가서 깨지는 유리 발판이, 멈춘 시계탑에는 박자에 맞춰 솟는 바닥 가시가 있다.
    /// </summary>
    public static class StageBuilder
    {
        /// <summary>지역의 색과 장식.</summary>
        private sealed class Theme
        {
            public string Name;
            public Color Solid;
            public Color Backdrop;
            public Color Cap;
            public Color FarWall;
            public Color Background;
            /// <summary>배경에 희미한 빛줄기를 내린다.</summary>
            public bool LightShafts;
            /// <summary>배경에 천천히 도는 큰 톱니바퀴를 건다.</summary>
            public bool Gears;
        }

        private static readonly Theme Citadel = new Theme
        {
            Name = "무너진 성채",
            Solid = new Color(0.32f, 0.34f, 0.42f, 1f),
            Backdrop = new Color(0.13f, 0.14f, 0.2f, 1f),
            Cap = new Color(0.36f, 0.58f, 0.55f, 1f),
            FarWall = new Color(0.105f, 0.115f, 0.165f, 1f),
            Background = new Color(0.09f, 0.1f, 0.14f, 1f),
        };

        private static readonly Theme Sanctum = new Theme
        {
            Name = "빛의 성소",
            Solid = new Color(0.55f, 0.52f, 0.6f, 1f),
            Backdrop = new Color(0.24f, 0.22f, 0.3f, 1f),
            Cap = new Color(0.85f, 0.75f, 0.45f, 1f),
            FarWall = new Color(0.16f, 0.15f, 0.21f, 1f),
            Background = new Color(0.1f, 0.09f, 0.13f, 1f),
            LightShafts = true,
        };

        private static readonly Theme Clocktower = new Theme
        {
            Name = "멈춘 시계탑",
            Solid = new Color(0.46f, 0.38f, 0.3f, 1f),
            Backdrop = new Color(0.2f, 0.15f, 0.12f, 1f),
            Cap = new Color(0.8f, 0.55f, 0.28f, 1f),
            FarWall = new Color(0.12f, 0.095f, 0.085f, 1f),
            Background = new Color(0.075f, 0.06f, 0.055f, 1f),
            Gears = true,
        };

        private static readonly Color BackgroundGearColor = new Color(0.22f, 0.17f, 0.13f, 1f);

        private static readonly Color GlassColor = new Color(0.7f, 0.92f, 1f, 0.6f);
        private static readonly Color GlassCapColor = new Color(0.85f, 1f, 1f, 0.9f);
        // 보스의 빛기둥(노랗고 곧게 내리꽂힘)과 헷갈리지 않도록 푸른빛으로 비스듬히 비춘다.
        private static readonly Color LightShaftColor = new Color(0.8f, 0.9f, 1f, 0.05f);
        private const float LightShaftTilt = -24f;

        private static Theme _theme = Citadel;

        private static Theme ThemeFor(int stage)
        {
            if (stage >= 3) return Clocktower;
            return stage == 2 ? Sanctum : Citadel;
        }

        /// <summary>그 스테이지의 지역 이름. 화면에 보여 준다.</summary>
        public static string AreaNameFor(int stage)
        {
            return ThemeFor(stage).Name;
        }

        /// <summary>그 스테이지에서 카메라 뒤에 칠할 색.</summary>
        public static Color BackgroundFor(int stage)
        {
            return ThemeFor(stage).Background;
        }

        public const float GroundTop = -2.5f;
        /// <summary>이 높이 아래로 떨어지면 구덩이에 빠진 것으로 본다.</summary>
        public const float KillY = GroundTop - 9f;

        private const float GroundDepth = 7f;
        private const float PlatformThickness = 0.5f;
        /// <summary>밟을 수 있는 윗면에 얹는 이끼 띠의 두께. 그림에서 4픽셀이다.</summary>
        private const float CapHeight = 0.25f;

        private delegate float ChunkBuilder(Transform parent, float x, int stage, StageLayout layout);

        // 바닥 조각. 맞닿은 조각은 하나의 충돌체로 합쳐서 이음매에 걸리지 않게 한다.
        private static readonly List<Vector2> GroundSpans = new List<Vector2>();

        public static StageLayout Build(Transform parent, RoomType type, int stage, bool withDummy)
        {
            var layout = new StageLayout();
            layout.Type = type;
            GroundSpans.Clear();
            _theme = ThemeFor(stage);

            switch (type)
            {
                case RoomType.MidBoss:
                    if (stage >= 3) BuildKnightRoom(parent, layout);
                    else if (stage == 2) BuildTwinRoom(parent, layout);
                    else BuildGatekeeperRoom(parent, layout);
                    break;
                case RoomType.Boss:
                    if (stage >= 3) BuildMasterRoom(parent, layout);
                    else if (stage == 2) BuildLightLordRoom(parent, layout);
                    else BuildGuardianRoom(parent, layout);
                    break;
                case RoomType.Shop:
                    BuildShopRoom(layout);
                    break;
                case RoomType.Puzzle:
                    BuildPuzzleRoom(parent, layout);
                    break;
                case RoomType.Hub:
                    BuildHubRoom(layout, stage);
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
            PixelSprites.CreateTiled("FarWall", parent, new Vector2(layout.Width * 0.5f, GroundTop + 6f), new Vector2(layout.Width + 40f, 40f), "Stone", PixelSprites.PixelsPerUnit * 0.5f, _theme.FarWall, -20);
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

            // 빛의 성소에서는 구덩이 구간 대신 유리 발판이 있는 구간이, 멈춘 시계탑에서는 가시 구간이 나온다.
            ChunkBuilder[] chunks;
            if (_theme == Sanctum) chunks = new ChunkBuilder[] { Flat, Steps, Terrace, Tower, GlassBridge, GlassClimb, Colonnade };
            else if (_theme == Clocktower) chunks = new ChunkBuilder[] { Flat, Steps, Terrace, Tower, Islands, SpikeHall, SpikeStairs };
            else chunks = new ChunkBuilder[] { Flat, Steps, Pit, Terrace, Tower, Islands };
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

        /// <summary>1스테이지 중간 보스방: 성채의 문지기. 돌진이 벽까지 닿는 넓은 평지와, 돌진을 피할 발판 두 개.</summary>
        private static void BuildGatekeeperRoom(Transform parent, StageLayout layout)
        {
            const float width = 40f;
            Ground(0f, width);
            Platform(parent, 10f, 2.2f, 4f);
            Platform(parent, 30f, 2.2f, 4f);
            Decorate(parent, 0f, width);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.MidBoss, new Vector2(27f, GroundTop)));
            layout.ExitPosition = new Vector2(37f, GroundTop);
            layout.Width = width;
        }

        /// <summary>1스테이지 보스방: 봉인의 수호자. 양쪽 발판과 가운데 바닥에 봉인석이 설 자리가 있다.</summary>
        private static void BuildGuardianRoom(Transform parent, StageLayout layout)
        {
            const float width = 40f;
            Ground(0f, width);
            Platform(parent, 9f, 2.4f, 4f);
            Platform(parent, 31f, 2.4f, 4f);
            Decorate(parent, 0f, width);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.Boss, new Vector2(27f, GroundTop)));
            layout.ExitPosition = new Vector2(37f, GroundTop);
            layout.Width = width;
            // 봉인석이 서는 자리: 왼쪽 발판 위, 가운데 바닥, 오른쪽 발판 위. 순서는 이 차례로 0, 1, 2번이다.
            layout.GimmickPoints.Add(new Vector2(9f, GroundTop + 2.4f));
            layout.GimmickPoints.Add(new Vector2(20f, GroundTop));
            layout.GimmickPoints.Add(new Vector2(31f, GroundTop + 2.4f));
        }

        /// <summary>2스테이지 중간 보스방: 쌍둥이 수문장. 해는 가까이, 달은 멀리서 싸운다.</summary>
        private static void BuildTwinRoom(Transform parent, StageLayout layout)
        {
            const float width = 40f;
            Ground(0f, width);
            Platform(parent, 11f, 2.2f, 4f);
            Platform(parent, 29f, 2.2f, 4f);
            Decorate(parent, 0f, width, false);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.TwinSun, new Vector2(22f, GroundTop)));
            layout.Enemies.Add(new EnemySpawn(EnemyKind.TwinMoon, new Vector2(32f, GroundTop)));
            layout.ExitPosition = new Vector2(37f, GroundTop);
            layout.Width = width;
        }

        /// <summary>2스테이지 보스방: 백광의 군주. 양쪽 발판과, 거기서 대시로 건너가는 가운데의 높은 유리 발판.</summary>
        private static void BuildLightLordRoom(Transform parent, StageLayout layout)
        {
            const float width = 42f;
            Ground(0f, width);
            Platform(parent, 10f, 2.4f, 4f);
            Platform(parent, 32f, 2.4f, 4f);
            Glass(parent, 21f, 3.6f, 5f);
            Decorate(parent, 0f, width, false);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.LightLord, new Vector2(30f, GroundTop)));
            layout.ExitPosition = new Vector2(39f, GroundTop);
            layout.Width = width;
        }

        /// <summary>3스테이지 중간 보스방: 태엽 기사. 평지와 양쪽 발판. 시계 감옥은 방 어디에나 서고, 진자는 방 끝에서 끝까지 지나간다.</summary>
        private static void BuildKnightRoom(Transform parent, StageLayout layout)
        {
            const float width = 40f;
            Ground(0f, width);
            Platform(parent, 11f, 2.2f, 4f);
            Platform(parent, 29f, 2.2f, 4f);
            Decorate(parent, 0f, width, false);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.ClockKnight, new Vector2(26f, GroundTop)));
            layout.ExitPosition = new Vector2(37f, GroundTop);
            layout.Width = width;
        }

        /// <summary>3스테이지 보스방: 시계탑의 주인. 넓은 평지, 양쪽의 낮은 발판, 가운데의 높은 발판.</summary>
        private static void BuildMasterRoom(Transform parent, StageLayout layout)
        {
            const float width = 44f;
            Ground(0f, width);
            Platform(parent, 10f, 2.4f, 4f);
            Platform(parent, 34f, 2.4f, 4f);
            Platform(parent, 22f, 4.2f, 5f);
            Decorate(parent, 0f, width, false);
            layout.PlayerSpawn = new Vector2(4f, GroundTop);
            layout.Enemies.Add(new EnemySpawn(EnemyKind.ClockMaster, new Vector2(32f, GroundTop)));
            layout.ExitPosition = new Vector2(41f, GroundTop);
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

        /// <summary>
        /// 거점: 적이 없는 방. 1스테이지 거점에는 난이도마다 문이 하나씩 있고 이벤트 문이 따로 있다.
        /// 2스테이지 거점부터는 강화의 제단과 다음 스테이지로 가는 문이 있다.
        /// </summary>
        private static void BuildHubRoom(StageLayout layout, int stage)
        {
            const float width = 34f;
            Ground(0f, width);
            layout.PlayerSpawn = new Vector2(3f, GroundTop);
            layout.ExitPosition = new Vector2(30f, GroundTop);
            if (stage <= 1)
            {
                for (int i = 0; i < Difficulty.Levels.Length; i++)
                {
                    layout.DifficultyDoors.Add(new Vector2(9f + i * 5f, GroundTop));
                }
                layout.HasEventDoor = true;
                layout.EventDoor = new Vector2(28f, GroundTop);
                layout.HasExit = false;
            }
            else
            {
                layout.HasAltar = true;
                layout.Altar = new Vector2(14f, GroundTop);
            }
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

        /// <summary>빛의 성소: 넓은 구덩이 위에 유리 발판 세 개. 멈춰 서면 깨지므로 계속 건너가야 한다.</summary>
        private static float GlassBridge(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 8f);
            Glass(parent, x + 10.5f, 1f, 3f);
            Glass(parent, x + 15f, 1.6f, 3f);
            Glass(parent, x + 19.5f, 1f, 3f);
            Ground(x + 22f, x + 34f);
            Spawn(layout, EnemyKind.Brute, x + 27f, 0f);
            Spawn(layout, EnemyKind.Shooter, x + 31f, 0f);
            return 34f;
        }

        /// <summary>빛의 성소: 유리 발판을 밟고 올라가야 위쪽의 원거리 적에게 닿는다.</summary>
        private static float GlassClimb(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 28f);
            Platform(parent, x + 6f, 2f, 4f);
            Glass(parent, x + 11f, 4f, 3f);
            Glass(parent, x + 16f, 6f, 3f);
            Platform(parent, x + 22f, 4.4f, 4f);
            Spawn(layout, EnemyKind.Shooter, x + 22f, 4.4f);
            Spawn(layout, EnemyKind.Brute, x + 14f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 25f, 0f);
            return 28f;
        }

        /// <summary>빛의 성소: 낮은 기둥 세 개 사이에서 싸우고, 기둥 위로 높은 발판이 걸쳐 있다.</summary>
        private static float Colonnade(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 30f);
            for (int i = 0; i < 3; i++)
            {
                float columnX = x + 8f + i * 8f;
                Solid(parent, "Column", new Vector2(columnX, GroundTop + 1f), new Vector2(1.5f, 2f));
                Cap(parent, columnX - 0.75f, columnX + 0.75f, GroundTop + 2f);
            }
            Platform(parent, x + 16f, 4.4f, 8f);
            Spawn(layout, EnemyKind.Shooter, x + 16f, 4.4f);
            Spawn(layout, EnemyKind.Brute, x + 12f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 20f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 27f, 0f);
            return 30f;
        }

        /// <summary>멈춘 시계탑: 바닥 가시가 번갈아 솟는 복도. 가시 사이의 빈 바닥과 가운데 발판에서 싸운다.</summary>
        private static float SpikeHall(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 32f);
            Spikes(parent, x + 5f, x + 9f, 0f);
            Spikes(parent, x + 13f, x + 17f, SpikeTrap.Period * 0.5f);
            Spikes(parent, x + 21f, x + 25f, 0f);
            Platform(parent, x + 15f, 3.2f, 4f);
            Spawn(layout, EnemyKind.Brute, x + 11f, 0f);
            Spawn(layout, EnemyKind.Shooter, x + 15f, 3.2f);
            Spawn(layout, EnemyKind.Brute, x + 19f, 0f);
            Spawn(layout, EnemyKind.Brute, x + 28f, 0f);
            return 32f;
        }

        /// <summary>멈춘 시계탑: 가시가 깔린 바닥 위로 발판을 밟고 건너간다. 가시가 들어간 틈에 바닥으로 내려와도 된다.</summary>
        private static float SpikeStairs(Transform parent, float x, int stage, StageLayout layout)
        {
            Ground(x, x + 30f);
            Spikes(parent, x + 6f, x + 24f, SpikeTrap.Period * 0.25f);
            Platform(parent, x + 8f, 1.8f, 3.5f);
            Platform(parent, x + 14f, 3.2f, 3.5f);
            Platform(parent, x + 20f, 1.8f, 3.5f);
            Spawn(layout, EnemyKind.Shooter, x + 14f, 3.2f);
            Spawn(layout, EnemyKind.Shooter, x + 20f, 1.8f);
            Spawn(layout, EnemyKind.Brute, x + 27f, 0f);
            return 30f;
        }

        // ---- 재료 ----

        /// <summary>바닥에 박자 맞춰 솟는 가시를 깐다. offset만큼 박자를 늦춰서 옆의 가시와 엇갈리게 한다.</summary>
        private static void Spikes(Transform parent, float left, float right, float offset)
        {
            SpikeTrap.Create(parent, left, right, GroundTop, offset);
        }

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

        /// <summary>
        /// 유리 발판. 보통 발판처럼 아래에서 뚫고 올라갈 수 있지만, 플레이어가 올라서면 금이 가다가 깨진다.
        /// 깨진 발판은 잠시 뒤 다시 생긴다.
        /// </summary>
        private static void Glass(Transform parent, float centerX, float height, float width)
        {
            Vector2 center = new Vector2(centerX, GroundTop + height - PlatformThickness * 0.5f);
            Vector2 size = new Vector2(width, PlatformThickness);
            SpriteRenderer body = PixelSprites.CreateTiled("GlassPlatform", parent, center, size, "Glass", PixelSprites.PixelsPerUnit, GlassColor, 0);
            var collider = body.gameObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.offset = Vector2.zero;
            var effector = collider.gameObject.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 170f;
            collider.usedByEffector = true;

            float top = GroundTop + height;
            SpriteRenderer cap = PixelSprites.CreateTiled("GlassCap", parent, new Vector2(centerX, top - CapHeight * 0.25f), new Vector2(width, CapHeight * 0.5f), "Cap", PixelSprites.PixelsPerUnit, GlassCapColor, 1);
            body.gameObject.AddComponent<GlassPlatform>().Build(collider, new[] { body, cap });
        }

        private static BoxCollider2D Solid(Transform parent, string name, Vector2 center, Vector2 size)
        {
            SpriteRenderer renderer = PixelSprites.CreateTiled(name, parent, center, size, "Stone", PixelSprites.PixelsPerUnit, _theme.Solid, 0);
            var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
            // 타일로 그린 스프라이트는 크기를 늘리지 않으므로 충돌체 크기를 직접 정한다.
            collider.size = size;
            collider.offset = Vector2.zero;
            return collider;
        }

        /// <summary>밟을 수 있는 윗면에 이끼 띠를 얹는다. 충돌하지 않는다.</summary>
        private static void Cap(Transform parent, float left, float right, float top)
        {
            PixelSprites.CreateTiled("Cap", parent, new Vector2((left + right) * 0.5f, top - CapHeight * 0.5f), new Vector2(right - left, CapHeight), "Cap", PixelSprites.PixelsPerUnit, _theme.Cap, 1);
        }

        private static void Spawn(StageLayout layout, EnemyKind kind, float x, float heightAboveGround)
        {
            layout.Enemies.Add(new EnemySpawn(kind, new Vector2(x, GroundTop + heightAboveGround)));
        }

        /// <summary>
        /// 움직임이 눈에 보이도록 배경에 어두운 기둥을 세운다. 충돌하지 않는다.
        /// 보스방에서는 빛줄기를 넣지 않는다(빛기둥 공격과 헷갈리지 않게).
        /// </summary>
        private static void Decorate(Transform parent, float x, float width, bool lightShafts = true)
        {
            for (int i = 0; i < 3; i++)
            {
                float pillarWidth = Random.Range(1f, 3f);
                float pillarHeight = Random.Range(3f, 10f);
                float centerX = x + Random.Range(1f, width - 1f);
                PixelSprites.CreateTiled("Backdrop", parent, new Vector2(centerX, GroundTop + pillarHeight * 0.5f), new Vector2(pillarWidth, pillarHeight), "Stone", PixelSprites.PixelsPerUnit, _theme.Backdrop, -10);
            }

            if (_theme.Gears)
            {
                // 멈춘 시계탑: 벽 너머에서 천천히 도는 큰 톱니바퀴.
                for (int i = 0; i < 2; i++)
                {
                    float centerX = x + Random.Range(2f, width - 2f);
                    var position = new Vector2(centerX, GroundTop + Random.Range(4f, 9f));
                    float spin = Random.Range(8f, 18f) * (Random.value < 0.5f ? -1f : 1f);
                    ClockArt.CreateBackgroundGear(parent, position, Random.Range(3f, 6f), BackgroundGearColor, spin);
                }
            }

            if (!_theme.LightShafts || !lightShafts) return;
            // 빛의 성소: 높은 창에서 비스듬히 들어오는 희미한 빛줄기. 바닥까지 닿지 않는다.
            for (int i = 0; i < 2; i++)
            {
                float centerX = x + Random.Range(2f, width - 2f);
                SpriteRenderer shaft = PrototypeArt.CreateBox("LightShaft", parent, new Vector2(centerX, GroundTop + 12f), new Vector2(Random.Range(1.2f, 2.4f), 16f), LightShaftColor, -15);
                shaft.transform.localRotation = Quaternion.Euler(0f, 0f, LightShaftTilt);
            }
        }
    }
}
