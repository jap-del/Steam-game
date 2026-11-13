namespace SteamGame.Prototype
{
    /// <summary>한 판 동안 강화로 바뀌는 수치 모음. 죽으면 처음 값으로 돌아간다.</summary>
    public sealed class RunStats
    {
        public int MaxHealth = 5;
        public float PlayerDamage = 10f;

        public int ShadowCount = 1;
        public float ShadowDelaySeconds = 2f;
        public float ShadowDamageMultiplier = 0.5f;
        public float ShadowSlashScale = 1f;

        public float PincerWindow = 0.6f;
        public float PincerMultiplier = 2f;

        public float SwapCooldown = 4f;
        public float SwapShockwaveDamage = 0f;

        public float DashCooldown = 0.8f;

        /// <summary>분신 고정이 유지되는 시간과, 풀린 뒤 다시 쓸 수 있을 때까지의 시간.</summary>
        public float AnchorDuration = 3f;
        public float AnchorCooldown = 10f;
    }
}
