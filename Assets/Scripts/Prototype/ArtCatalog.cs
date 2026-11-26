namespace SteamGame.Prototype
{
    /// <summary>
    /// Assets/Resources/Art 안 그림 판의 목록. 그림 생성 도구가 그림 파일과 함께 만든다.
    /// 칸 크기, 발 기준점, 움직임 묶음(이름, 시작 칸, 칸 수, 1초에 넘기는 칸 수, 반복 여부)을 적는다.
    /// </summary>
    public static class ArtCatalog
    {
        public static ArtSheetInfo Find(string name)
        {
            switch (name)
            {
                case "Player":
                    return new ArtSheetInfo("Player", 64, 56, 1, 31, 21, true, 30.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("run", 4, 6, 12f, true), new ArtClip("jump", 10, 2, 8f, false), new ArtClip("fall", 12, 2, 8f, true), new ArtClip("dash", 14, 2, 16f, true), new ArtClip("attack", 17, 3, 14f, false), new ArtClip("hurt", 20, 1, 8f, false) });
                case "PlayerShadow":
                    return new ArtSheetInfo("PlayerShadow", 64, 56, 1, 31, 21, true, 30.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("run", 4, 6, 12f, true), new ArtClip("jump", 10, 2, 8f, false), new ArtClip("fall", 12, 2, 8f, true), new ArtClip("dash", 14, 2, 16f, true), new ArtClip("attack", 17, 3, 14f, false), new ArtClip("hurt", 20, 1, 8f, false) });
                case "Brute":
                    return new ArtSheetInfo("Brute", 60, 48, 1, 28, 14, true, 28.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Shooter":
                    return new ArtSheetInfo("Shooter", 48, 44, 1, 28, 14, true, 23.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Shield":
                    return new ArtSheetInfo("Shield", 60, 56, 1, 28, 14, true, 27.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Bomber":
                    return new ArtSheetInfo("Bomber", 56, 48, 1, 28, 14, true, 26.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Flyer":
                    return new ArtSheetInfo("Flyer", 60, 40, 1, 28, 14, true, 30.5f, 12.9f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Dummy":
                    return new ArtSheetInfo("Dummy", 48, 64, 1, 8, 4, true, 24.5f, 1f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true) });
                case "MidBoss":
                    return new ArtSheetInfo("MidBoss", 100, 84, 1, 20, 14, true, 46.5f, 2f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Boss":
                    return new ArtSheetInfo("Boss", 120, 104, 1, 16, 14, true, 56.5f, 2f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "TwinSun":
                    return new ArtSheetInfo("TwinSun", 92, 84, 1, 21, 15, true, 42.5f, 3f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false), new ArtClip("down", 14, 1, 6f, false) });
                case "TwinMoon":
                    return new ArtSheetInfo("TwinMoon", 92, 84, 1, 21, 15, true, 42.5f, 3f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false), new ArtClip("down", 14, 1, 6f, false) });
                case "LightLord":
                    return new ArtSheetInfo("LightLord", 132, 116, 1, 15, 14, true, 64.5f, 3f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "ClockKnight":
                    return new ArtSheetInfo("ClockKnight", 100, 88, 1, 20, 14, true, 46.5f, 3f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "ClockMaster":
                    return new ArtSheetInfo("ClockMaster", 132, 124, 1, 15, 14, true, 62.5f, 3f,
                        new[] { new ArtClip("idle", 0, 4, 6f, true), new ArtClip("walk", 4, 4, 8f, true), new ArtClip("windup", 8, 2, 6f, false), new ArtClip("attack", 10, 2, 14f, false), new ArtClip("recover", 12, 2, 6f, false) });
                case "Door":
                    return new ArtSheetInfo("Door", 52, 84, 0, 1, 1, false, 26f, 42f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Potion":
                    return new ArtSheetInfo("Potion", 32, 32, 0, 1, 1, false, 16f, 16f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Crystal":
                    return new ArtSheetInfo("Crystal", 32, 32, 0, 1, 1, false, 16f, 16f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Altar":
                    return new ArtSheetInfo("Altar", 40, 40, 0, 1, 1, false, 20f, 20f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Orb":
                    return new ArtSheetInfo("Orb", 16, 16, 0, 1, 1, false, 8f, 8f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Gear":
                    return new ArtSheetInfo("Gear", 24, 24, 0, 1, 1, false, 12f, 12f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Bomb":
                    return new ArtSheetInfo("Bomb", 16, 16, 0, 1, 1, false, 8f, 8f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Seal":
                    return new ArtSheetInfo("Seal", 24, 36, 0, 1, 1, false, 12f, 18f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Slash":
                    return new ArtSheetInfo("Slash", 54, 42, 0, 1, 1, false, 27f, 21f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Stone":
                    return new ArtSheetInfo("Stone", 32, 32, 0, 1, 1, false, 16f, 16f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Cap":
                    return new ArtSheetInfo("Cap", 32, 8, 0, 1, 1, false, 16f, 4f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Glass":
                    return new ArtSheetInfo("Glass", 32, 16, 0, 1, 1, false, 16f, 8f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                case "Spikes":
                    return new ArtSheetInfo("Spikes", 32, 16, 0, 1, 1, false, 16f, 8f,
                        new[] { new ArtClip("idle", 0, 1, 1f, true) });
                default:
                    return null;
            }
        }
    }
}
