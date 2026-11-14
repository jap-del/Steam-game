using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>난이도 하나. 거점의 난이도 문에서 고른다.</summary>
    public sealed class DifficultyLevel
    {
        public readonly string Title;
        public readonly string Description;
        public readonly float EnemyHealthScale;
        public readonly float EnemySpeedScale;
        /// <summary>기본 최대 체력에 더하는 값.</summary>
        public readonly int MaxHealthBonus;
        /// <summary>적을 잡을 때 얻는 그림자 조각에 곱하는 값.</summary>
        public readonly float ShardScale;
        /// <summary>거점의 난이도 문에 칠하는 색.</summary>
        public readonly Color DoorColor;

        public DifficultyLevel(string title, string description, float enemyHealthScale, float enemySpeedScale, int maxHealthBonus, float shardScale, Color doorColor)
        {
            Title = title;
            Description = description;
            EnemyHealthScale = enemyHealthScale;
            EnemySpeedScale = enemySpeedScale;
            MaxHealthBonus = maxHealthBonus;
            ShardScale = shardScale;
            DoorColor = doorColor;
        }
    }

    /// <summary>난이도 목록과 지금 고른 난이도. 고른 난이도는 PlayerPrefs에 저장된다.</summary>
    public static class Difficulty
    {
        private const string SelectedKey = "meta.difficulty";
        private const int DefaultIndex = 1;

        public static readonly DifficultyLevel[] Levels =
        {
            new DifficultyLevel("쉬움", "적 체력 80%. 최대 체력 +2.", 0.8f, 1f, 2, 1f, new Color(0.45f, 0.75f, 1f, 1f)),
            new DifficultyLevel("보통", "기본 난이도.", 1f, 1f, 0, 1f, new Color(1f, 0.85f, 0.4f, 1f)),
            new DifficultyLevel("어려움", "적 체력 130%, 적이 15% 빠르다. 최대 체력 -1. 그림자 조각 1.5배.", 1.3f, 1.15f, -1, 1.5f, new Color(0.95f, 0.35f, 0.35f, 1f)),
        };

        public static int SelectedIndex
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, DefaultIndex), 0, Levels.Length - 1); }
            set { PlayerPrefs.SetInt(SelectedKey, Mathf.Clamp(value, 0, Levels.Length - 1)); }
        }

        public static DifficultyLevel Selected
        {
            get { return Levels[SelectedIndex]; }
        }

        /// <summary>저장된 난이도를 지우고 보통으로 돌린다.</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(SelectedKey);
        }
    }
}
