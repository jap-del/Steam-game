using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 죽어도 남는 진행. 적을 잡아 모으는 그림자 조각과 한 판 클리어 기록을 PlayerPrefs에 저장한다.
    /// </summary>
    public static class MetaProgress
    {
        public const string CurrencyName = "그림자 조각";

        private const string ShardsKey = "meta.shards";
        private const string ClearsKey = "meta.clears";
        private const string BestClearKey = "meta.bestClearSeconds";

        public static int Shards
        {
            get { return Mathf.Max(0, PlayerPrefs.GetInt(ShardsKey, 0)); }
            set { PlayerPrefs.SetInt(ShardsKey, Mathf.Max(0, value)); }
        }

        /// <summary>지금까지 한 판을 클리어한 횟수.</summary>
        public static int Clears => Mathf.Max(0, PlayerPrefs.GetInt(ClearsKey, 0));

        /// <summary>가장 빨리 클리어한 시간(초). 아직 클리어한 적이 없으면 0.</summary>
        public static float BestClearSeconds => Mathf.Max(0f, PlayerPrefs.GetFloat(BestClearKey, 0f));

        /// <summary>한 판 클리어를 기록한다. 최고 기록이면 true.</summary>
        public static bool RecordClear(float seconds)
        {
            PlayerPrefs.SetInt(ClearsKey, Clears + 1);
            float best = BestClearSeconds;
            bool newBest = best <= 0f || seconds < best;
            if (newBest) PlayerPrefs.SetFloat(BestClearKey, seconds);
            return newBest;
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>그림자 조각, 클리어 기록, 고른 난이도를 모두 지운다. 테스트용.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ShardsKey);
            PlayerPrefs.DeleteKey(ClearsKey);
            PlayerPrefs.DeleteKey(BestClearKey);
            Difficulty.Reset();
            PlayerPrefs.Save();
        }
    }
}
