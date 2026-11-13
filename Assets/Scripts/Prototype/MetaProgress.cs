using System;
using UnityEngine;

namespace SteamGame.Prototype
{
    /// <summary>거점에서 사는 영구 강화 하나. 단계마다 값이 조금씩 오른다.</summary>
    public sealed class MetaUpgrade
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Description;
        /// <summary>단계별 가격. 길이가 최대 단계다.</summary>
        public readonly int[] Costs;
        private readonly Action<RunStats> _applyOnce;

        public MetaUpgrade(string id, string title, string description, int[] costs, Action<RunStats> applyOnce)
        {
            Id = id;
            Title = title;
            Description = description;
            Costs = costs;
            _applyOnce = applyOnce;
        }

        public int MaxLevel => Costs.Length;

        /// <summary>한 단계만큼의 효과를 수치에 더한다.</summary>
        public void ApplyOnce(RunStats stats)
        {
            _applyOnce(stats);
        }
    }

    /// <summary>
    /// 죽어도 남는 진행. 그림자 조각과 영구 강화 단계를 PlayerPrefs에 저장한다.
    /// 실력으로 깨는 게임이라 영구 강화는 폭을 작게 잡았다.
    /// </summary>
    public static class MetaProgress
    {
        public const string CurrencyName = "그림자 조각";

        private const string ShardsKey = "meta.shards";
        private const string LevelKeyPrefix = "meta.level.";

        public static readonly MetaUpgrade[] Upgrades =
        {
            new MetaUpgrade("health", "튼튼한 몸", "최대 체력 +1", new[] { 10, 20, 35 }, s => { s.MaxHealth += 1; }),
            new MetaUpgrade("damage", "잘 벼린 칼", "공격 피해 +1", new[] { 10, 20, 35 }, s => { s.PlayerDamage += 1f; }),
            new MetaUpgrade("purse", "여비", "시작 골드 +15", new[] { 8, 15, 25 }, s => { s.StartGold += 15; }),
            new MetaUpgrade("anchor", "익숙한 그림자", "분신 고정 쿨타임 -1초", new[] { 10, 20, 35 }, s => { s.AnchorCooldown -= 1f; }),
        };

        public static int Shards
        {
            get { return Mathf.Max(0, PlayerPrefs.GetInt(ShardsKey, 0)); }
            set { PlayerPrefs.SetInt(ShardsKey, Mathf.Max(0, value)); }
        }

        public static int LevelOf(MetaUpgrade upgrade)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(LevelKeyPrefix + upgrade.Id, 0), 0, upgrade.MaxLevel);
        }

        public static void SetLevel(MetaUpgrade upgrade, int level)
        {
            PlayerPrefs.SetInt(LevelKeyPrefix + upgrade.Id, Mathf.Clamp(level, 0, upgrade.MaxLevel));
        }

        /// <summary>새 판을 시작할 때 지금까지 산 영구 강화를 모두 수치에 반영한다.</summary>
        public static void ApplyAll(RunStats stats)
        {
            for (int i = 0; i < Upgrades.Length; i++)
            {
                int level = LevelOf(Upgrades[i]);
                for (int n = 0; n < level; n++)
                {
                    Upgrades[i].ApplyOnce(stats);
                }
            }
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>그림자 조각과 영구 강화를 모두 지운다. 테스트용.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(ShardsKey);
            for (int i = 0; i < Upgrades.Length; i++)
            {
                PlayerPrefs.DeleteKey(LevelKeyPrefix + Upgrades[i].Id);
            }
            PlayerPrefs.Save();
        }
    }
}
