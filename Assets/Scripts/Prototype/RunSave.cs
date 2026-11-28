using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>
    /// 이어하기용으로 저장하는 한 판의 진행. 방에 들어갈 때마다, 그리고 상점이나 제단에서 사거나 퍼즐을 풀었을 때 쓴다.
    /// 이어하면 그 방에 들어온 때부터 다시 한다. 방은 같은 씨앗으로 만들어서 모양, 적, 상점 물건이 같다.
    /// </summary>
    [System.Serializable]
    public sealed class RunSaveData
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// <summary>이 판의 씨앗. 방마다 이 값과 스테이지, 방 번호로 무작위 값을 정한다.</summary>
        public int Seed;
        public int Stage;
        /// <summary>0은 거점, 1에서 6은 방.</summary>
        public int Room;
        /// <summary>그 방의 종류(RoomType 값). 메인 화면에 "2-5 퍼즐방"처럼 보여 줄 때만 쓴다.</summary>
        public int RoomKind;
        public int Health;
        public int Gold;
        public int Kills;
        public int Pincers;
        /// <summary>이번 판에 모은 그림자 조각(이미 전체 조각에 더해져 있다. 기록 표시용).</summary>
        public int RunShards;
        public int Difficulty;
        /// <summary>판을 시작하고 지난 시간(초). 클리어 시간에 이어서 더한다.</summary>
        public float Elapsed;
        public bool AltarUsed;
        public bool PuzzleSolved;
        /// <summary>상점에서 팔린 물건. 물건 순서대로 한 비트씩.</summary>
        public int SoldMask;
        /// <summary>저장한 때(사람이 읽는 글자). 메인 화면에 보여 준다.</summary>
        public string SavedAt;
        public RunStats Stats;
    }

    /// <summary>
    /// 한 판의 진행을 PlayerPrefs에 JSON으로 저장한다. 판이 끝나면(죽거나, 클리어하거나, 새 게임을 시작하면) 지운다.
    /// 그림자 조각과 클리어 기록 같은 죽어도 남는 진행은 MetaProgress가 따로 저장한다.
    /// </summary>
    public static class RunSave
    {
        private const string Key = "run.save.v1";

        public static bool Exists => Load() != null;

        /// <summary>저장된 판. 없거나 읽을 수 없으면 null.</summary>
        public static RunSaveData Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try
            {
                var data = JsonUtility.FromJson<RunSaveData>(PlayerPrefs.GetString(Key));
                if (data == null || data.Version != RunSaveData.CurrentVersion || data.Stats == null) return null;
                if (data.Stage < 1 || data.Room < 0 || data.Room > PrototypeGame.RoomsPerStage) return null;
                return data;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        public static void Write(RunSaveData data)
        {
            if (data == null) return;
            data.Version = RunSaveData.CurrentVersion;
            data.SavedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void Delete()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
