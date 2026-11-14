using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 죽어도 남는 진행. 지금은 적을 잡아 모으는 그림자 조각뿐이고 PlayerPrefs에 저장한다.
    /// 그림자 조각을 쓰는 곳은 아직 없다.
    /// </summary>
    public static class MetaProgress
    {
        public const string CurrencyName = "그림자 조각";

        private const string ShardsKey = "meta.shards";

        public static int Shards
        {
            get { return Mathf.Max(0, PlayerPrefs.GetInt(ShardsKey, 0)); }
            set { PlayerPrefs.SetInt(ShardsKey, Mathf.Max(0, value)); }
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>그림자 조각과 고른 난이도를 모두 지운다. 테스트용.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ShardsKey);
            Difficulty.Reset();
            PlayerPrefs.Save();
        }
    }
}
