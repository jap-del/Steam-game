namespace SteamGame.Prototype
{
    /// <summary>
    /// 픽셀 그림의 원본. 글자 하나가 픽셀 하나다. 모든 캐릭터는 오른쪽을 보고 있다.
    /// 바탕 층은 밝기만 있고 색은 스프라이트의 색으로 입힌다:
    ///   .  투명    #  윤곽선    d  어두움    m  중간    l  밝음
    /// 덧그림 층은 색이 정해져 있고 스프라이트의 색을 따르지 않는다:
    ///   o  흰 빛    y  노란 빛    c  청록    r  붉은 빛
    /// </summary>
    internal static class PixelSpriteData
    {
        public static string[] Rows(string name)
        {
            switch (name)
            {
                case "Player": return Player;
                case "Brute": return Brute;
                case "Shooter": return Shooter;
                case "MidBoss": return MidBoss;
                case "Boss": return Boss;
                case "Dummy": return Dummy;
                case "Shield": return Shield;
                case "Bomber": return Bomber;
                case "Flyer": return Flyer;
                case "Potion": return Potion;
                case "Crystal": return Crystal;
                case "Altar": return Altar;
                case "Bomb": return Bomb;
                case "Door": return Door;
                case "Slash": return Slash;
                case "Orb": return Orb;
                case "Stone": return Stone;
                case "Cap": return Cap;
                default: return null;
            }
        }

        private static readonly string[] Player =
        {
            "....#####....",
            "...#lllll#...",
            "..#lllllll#..",
            ".#llmmmmmll#.",
            ".#lm######l#.",
            ".#lm##l#l#m#.",
            ".#lmm#####m#.",
            "..#mmmmmmm#..",
            "ccccccccccc#.",
            "cc.#cccccccc.",
            "c..#lllllm#..",
            "...#lmmmmm#.#",
            "..#llmmmmml#l",
            "..#l#mmmm#l##",
            "...##mmmm##..",
            "...#mmmmmm#..",
            "...#mmddmm#..",
            "...#mm##mm#..",
            "...#md#.#md#.",
            "...#md#.#md#.",
            "..##dd#.#dd##",
            "..####..#####",
        };

        private static readonly string[] Brute =
        {
            "#.........#...",
            "##.......##...",
            "#l#.....#l#...",
            ".#l#####l#....",
            ".#lllllllll#..",
            "#lmmmmmmmmml#.",
            "#lmm#o#m#o#m#.",
            "#lmmmmmmmmmm#.",
            "#lmm#######m#.",
            "#lmmm#l#l#mm#.",
            ".#mmmmmmmmm##.",
            "##mmmmmmmmm#l#",
            "#l#mmmmmmm#ll#",
            "#ll#mmmmmm#ll#",
            "#ll#mmddmm#l#.",
            ".###mm##mm##..",
            "..#mm#..#mm#..",
            "..#dd#..#dd#..",
            ".####..####...",
        };

        private static readonly string[] Shooter =
        {
            "....#####....",
            "..##lllll##..",
            ".#lllllllll#.",
            ".#lmmmmmmml#.",
            "#lmm#####mml#",
            "#lm#ooooo#ml#",
            "#lm#oo##o#ml#",
            "#lm#oo##o#ml#",
            "#lm#ooooo#ml#",
            "#lmm#####mmm#",
            ".#mmmmmmmmm#.",
            ".#dmmmmmmmd#.",
            "..##ddddd##..",
            "...#d#.#d#...",
            "..##d#.#d##..",
            "..###..####..",
        };

        private static readonly string[] MidBoss =
        {
            "...#................#...",
            "..#l#..............#l#..",
            "..#l#..............#l#..",
            "..#ll#...######...#ll#..",
            "...#ll###llllll###ll#...",
            "....#llllllllllllll#....",
            "....#lmmmmmmmmmmmml#....",
            "....#lmmmmmmmmmmmml#....",
            "....#l####mmmm####l#....",
            "....#l#oo#mmmm#oo#l#....",
            "....#l####mmmm####l#....",
            "....#lmmmm#mm#mmmml#....",
            "....#mmmmm#mm#mmmmm#....",
            ".....#mmmmmmmmmmmm#.....",
            "..####dddddddddddd####..",
            ".#llll############llll#.",
            "#lllmml#llllllll#lmmlll#",
            "#lmmmml#lmmmmmml#lmmmml#",
            "#lmmmm#lmmmmmmmml#mmmml#",
            "#lmmm##lmmmmmmmml##mmml#",
            ".#mm#.#lmmmmmmmml#.#mm#.",
            ".#mm#.#mmmmmmmmmm#.#mm#.",
            ".#mm#.#mmmmddmmmm#.#mm#.",
            ".#ll#.#dddddddddd#.#ll#.",
            ".#ll#.#mmmmmmmmmm#.#ll#.",
            "..##..#mmmm##mmmm#..##..",
            "......#mmm#..#mmm#......",
            "......#mmm#..#mmm#......",
            "......#mmm#..#mmm#......",
            "......#mmd#..#dmm#......",
            ".....##ddd#..#ddd##.....",
            ".....######..######.....",
        };

        private static readonly string[] Boss =
        {
            "..#..........................#..",
            ".#l#........................#l#.",
            ".#l#........................#l#.",
            ".#ll#.......#......#.......#ll#.",
            "..#ll#.....#l#....#l#.....#ll#..",
            "..#lll#....#l#....#l#....#lll#..",
            "...#lll####ll######ll####lll#...",
            "....#llllllllllllllllllllll#....",
            "....#lmmmmmmmmmmmmmmmmmmmml#....",
            "...#lmmmmmmmmmmmmmmmmmmmmmml#...",
            "...#lmm#####mmmmmmmm#####mml#...",
            "...#lmm#rrr#mmmmmmmm#rrr#mml#...",
            "...#lmmm####mmmmmmmm####mmml#...",
            "...#lmmmmmmmmmm##mmmmmmmmmml#...",
            "...#lmmm################mmml#...",
            "....#mmm#l#l#l#ll#l#l#l#mmm#....",
            "....#mmmm##############mmmm#....",
            ".....#mmmmmmmmmmmmmmmmmmmm#.....",
            "..####dddddddddddddddddddd####..",
            ".#llll#mmmmmmmmmmmmmmmmmm#llll#.",
            "#lllllm#mmmmmmmmmmmmmmmm#mlllll#",
            "#llmmmmm#mmmmm####mmmmm#mmmmmll#",
            "#lmmmmmm#mmmm#yyyy#mmmm#mmmmmml#",
            "#lmmmmmm#mmm#yyyyyy#mmm#mmmmmml#",
            "#lmmmmm#mmmm#yyyyyy#mmmm#mmmmml#",
            "#lmmmmm#mmmmm#yyyy#mmmmm#mmmmml#",
            ".#mmmm#.#mmmmm####mmmmm#.#mmmm#.",
            ".#mmmm#.#mmmmmmmmmmmmmm#.#mmmm#.",
            ".#mmmm#.#mmmmmmmmmmmmmm#.#mmmm#.",
            ".#mmmm#.#dddddddddddddd#.#mmmm#.",
            "#llmmml##mmmmmmmmmmmmmm##lmmmll#",
            "#lllmml##mmmmmmmmmmmmmm##lmmlll#",
            "#lmmmmm##mmmmmm##mmmmmm##mmmmml#",
            ".#mmmm#.#mmmmm#..#mmmmm#.#mmmm#.",
            "..####..#mmmmm#..#mmmmm#..####..",
            "........#mmmmm#..#mmmmm#........",
            "........#mmmmm#..#mmmmm#........",
            "........#mmmmd#..#dmmmm#........",
            "........#mmmdd#..#ddmmm#........",
            ".......##ddddd#..#ddddd##.......",
            "......#ddddddd#..#ddddddd#......",
            "......#########..#########......",
        };

        private static readonly string[] Dummy =
        {
            ".....######.....",
            "....#llllll#....",
            "...#llllllll#...",
            "...#lmmmmmml#...",
            "...#lm#mm#ml#...",
            "...#lmmmmmml#...",
            "...#mmmmmmmm#...",
            "....#mmmmmm#....",
            ".....######.....",
            "......#mm#......",
            "################",
            "#llllllllllllll#",
            "#mmmmmmmmmmmmmm#",
            "################",
            "...#lmmmmmml#...",
            "...#lmmmmmml#...",
            "...#lmmmmmml#...",
            "...#lmdmmdml#...",
            "...#lmmmmmml#...",
            "...#mmmmmmmm#...",
            "...#mmmmmmmm#...",
            "....########....",
            "......#mm#......",
            "......#mm#......",
            "......#mm#......",
            "......#mm#......",
            "......#mm#......",
            "....###dd###....",
            "...##########...",
        };

        private static readonly string[] Shield =
        {
            ".....######.....",
            "....#llllll#....",
            "...#llmmmmml#...",
            "...#lmmmmmmm#...",
            "...#lm######m#..",
            "...#lm#o#o##m#..",
            "...#lmm####mm#..",
            "....#mmmmmmm#...",
            ".....#######....",
            "...##lllllll##..",
            "..#llmmmmmmmml#.",
            "..#lmmmmmmmmmm#.",
            "..#lm#mmmmmm#m#.",
            "..#lm#mmmmmm#m#.",
            "..#ll#mmmmmm#l#.",
            "...###dddddd##..",
            ".....#mmmmmm#...",
            ".....#mm##mm#...",
            ".....#mm#.#mm#..",
            ".....#md#.#md#..",
            "....##dd#.#dd##.",
            "....####..#####.",
        };

        private static readonly string[] Bomber =
        {
            "..#.......#...",
            ".#l#.....#l#..",
            ".#ll#####ll#..",
            "#lllllllllll#.",
            "#lmmmmmmmmml#.",
            "#lmm#o#m#o#m#.",
            "#lmmmmmmmmmm#.",
            "#lmm##mmm##m#.",
            "#lmmm#####mm#.",
            ".#mmmmmmmmm#..",
            ".#mmmmmmmmm##.",
            "#l#mmmmmmm#ll#",
            "#l#mmmmmmm#ll#",
            ".##mm###mm###.",
            "..#dd#.#dd#...",
            ".####.####....",
        };

        private static readonly string[] Flyer =
        {
            "#............#",
            "##....##....##",
            "#l#..#ll#..#l#",
            "#ll##llll##ll#",
            "#lllmmmmmmlll#",
            "#lmm#ommo#mml#",
            ".#mmmmmmmmmm#.",
            ".#m#mmmmmm#m#.",
            "..#.#mmmm#.#..",
            ".....####.....",
        };

        private static readonly string[] Potion =
        {
            "................",
            ".....######.....",
            ".....#mmmm#.....",
            ".....######.....",
            "......#ll#......",
            "......#ll#......",
            ".....#llll#.....",
            "...##llllll##...",
            "..#llllllllll#..",
            ".#llmmmmmmmmll#.",
            ".#lmmmmmmmmmml#.",
            "#llmmmmmmmmmmll#",
            "#lmmmmmmmmmmmml#",
            "#lmmmmmddmmmmml#",
            ".#mmmddddddmmm#.",
            "..############..",
        };

        private static readonly string[] Crystal =
        {
            "................",
            ".......##.......",
            "......#ll#......",
            ".....#llll#.....",
            "....#llllll#....",
            "...#llllllll#...",
            "..#llllmmllll#..",
            ".#lllmmmmmmlll#.",
            "#lllmmmmmmmmlll#",
            ".#lmmmmmmmmmml#.",
            "..#mmmmddmmmm#..",
            "...#mmddddmm#...",
            "....#mddddm#....",
            ".....#dddd#.....",
            "......#dd#......",
            ".......##.......",
        };

        private static readonly string[] Altar =
        {
            ".........##.........",
            "........#ll#........",
            ".......#llll#.......",
            "......#llllll#......",
            "......#llmmll#......",
            ".......#mmmm#.......",
            "........#mm#........",
            ".........##.........",
            "....................",
            "..################..",
            ".#llllllllllllllll#.",
            ".#mmmmmmmmmmmmmmmm#.",
            "..################..",
            "....#lmmmmmmmml#....",
            "....#lmmmmmmmml#....",
            "....#lmdmmmmdml#....",
            "....#lmmmmmmmml#....",
            "..###mmmmmmmmmm###..",
            ".#llllllllllllllll#.",
            ".##################.",
        };

        private static readonly string[] Bomb =
        {
            "....#y..",
            "...#....",
            "..####..",
            ".#lmmm#.",
            "#lmmmmm#",
            "#mmmmmm#",
            ".#mmmm#.",
            "..####..",
        };

        private static readonly string[] Door =
        {
            ".........########.........",
            ".......###lllmmm###.......",
            ".....##llllllmmmmmm##.....",
            "....##lllllllmmmmmmm##....",
            "...##lllll######mmmmm##...",
            "..##llll##dddddd##mmmm##..",
            "..#llll#dddd##dddd#mmmm#..",
            ".#llll#ddddd##ddddd#mmmm#.",
            ".#lll#dddddd##dddddd#mmm#.",
            "##lll#dddddd##dddddd#mmm##",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#d#####d##d#####d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#####d##d#####d#mmm#",
            "#lll#dddddld##dlddddd#mmm#",
            "#lll#dddddld##dlddddd#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#d#####d##d#####d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#mmm#d##d#mmm#d#mmm#",
            "#lll#d#####d##d#####d#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "#lll#ddddddd##ddddddd#mmm#",
            "##########################",
        };

        private static readonly string[] Slash =
        {
            ".........mmmlllmmmm........",
            "............mmllllmm.......",
            ".............mmlllllmm.....",
            "...............mlllllmm....",
            "................mlllllmm...",
            "................mmlllllmm..",
            ".................mllllllm..",
            ".................mmlllllm..",
            "..................mllllllm.",
            "..................mllllllm.",
            "..................mllllllm.",
            "..................mllllllm.",
            "..................mllllllm.",
            ".................mmlllllm..",
            ".................mllllllm..",
            "................mmlllllmm..",
            "................mlllllmm...",
            "...............mlllllmm....",
            ".............mmlllllmm.....",
            "............mmllllmm.......",
            ".........mmmlllmmmm........",
        };

        private static readonly string[] Orb =
        {
            "..mmmm..",
            ".mmmmmm.",
            "mmllllmm",
            "mmllllmm",
            "mmllllmm",
            "mmllllmm",
            ".mmmmmm.",
            "..mmmm..",
        };

        private static readonly string[] Stone =
        {
            "llllllllllllllld",
            "lllllllllllllmld",
            "lllmllllllllllld",
            "llllllllllllllld",
            "lllllllllmlllmmd",
            "llllllllllllmmmd",
            "mmmmmmmmmmmmmmmd",
            "dddddddddddddddd",
            "llllllldllllllll",
            "lmlllmldllllllll",
            "llllllldlllllmll",
            "llllllldllllllll",
            "lllllmmdllmlllll",
            "llllmmmdllllllll",
            "mmmmmmmdmmmmmmmm",
            "dddddddddddddddd",
        };

        private static readonly string[] Cap =
        {
            "llllllllllllllll",
            "lmllllmlllllmlll",
            "mmlmmmmmlmmmmmml",
            "m.mm.mmm.mm.mmm.",
        };
    }
}
