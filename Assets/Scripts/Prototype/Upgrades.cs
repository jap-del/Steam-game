using System;
using System.Collections.Generic;

namespace SteamGame.Prototype
{
    /// <summary>웨이브를 깬 뒤 고르는 강화 하나.</summary>
    public sealed class Upgrade
    {
        public readonly string Title;
        public readonly string Description;
        private readonly Func<RunStats, bool> _available;
        private readonly Action<RunStats, PlayerController> _apply;

        public Upgrade(string title, string description, Func<RunStats, bool> available, Action<RunStats, PlayerController> apply)
        {
            Title = title;
            Description = description;
            _available = available;
            _apply = apply;
        }

        public bool IsAvailable(RunStats stats)
        {
            return _available == null || _available(stats);
        }

        public void Apply(RunStats stats, PlayerController player)
        {
            _apply(stats, player);
        }
    }

    public static class UpgradePool
    {
        private static readonly Upgrade[] All =
        {
            new Upgrade("빠른 그림자", "분신이 따라 하는 지연 시간이 1초 줄어든다.",
                s => s.ShadowDelaySeconds > 1.01f,
                (s, p) => { s.ShadowDelaySeconds -= 1f; p.RebuildShadows(); }),

            new Upgrade("두 번째 그림자", "분신이 하나 더 생긴다. 첫 분신보다 두 배 늦게 따라 한다.",
                s => s.ShadowCount < 2,
                (s, p) => { s.ShadowCount += 1; p.RebuildShadows(); }),

            new Upgrade("짙은 그림자", "분신의 피해가 본체 피해의 25%만큼 늘어난다.",
                s => s.ShadowDamageMultiplier < 1.5f,
                (s, p) => { s.ShadowDamageMultiplier += 0.25f; }),

            new Upgrade("큰 그림자", "분신의 공격 범위가 40% 넓어진다.",
                s => s.ShadowSlashScale < 2.2f,
                (s, p) => { s.ShadowSlashScale += 0.4f; p.RebuildShadows(); }),

            new Upgrade("협공 강화", "협공 피해 배율이 0.5 늘어난다.",
                null,
                (s, p) => { s.PincerMultiplier += 0.5f; }),

            new Upgrade("느긋한 협공", "협공으로 인정되는 시간이 0.15초 길어진다.",
                s => s.PincerWindow < 0.8f,
                (s, p) => { s.PincerWindow += 0.15f; }),

            new Upgrade("빠른 교대", "자리 바꾸기 대기시간이 1초 줄어든다.",
                s => s.SwapCooldown > 1.01f,
                (s, p) => { s.SwapCooldown -= 1f; }),

            new Upgrade("교대 충격파", "자리를 바꿀 때 떠난 자리와 도착한 자리 주변에 피해 15를 준다.",
                null,
                (s, p) => { s.SwapShockwaveDamage += 15f; }),

            new Upgrade("날 선 칼", "본체 공격 피해가 3 늘어난다. 분신 피해도 함께 오른다.",
                null,
                (s, p) => { s.PlayerDamage += 3f; }),

            new Upgrade("재빠른 발", "대시 대기시간이 0.2초 줄어든다.",
                s => s.DashCooldown > 0.41f,
                (s, p) => { s.DashCooldown -= 0.2f; }),

            new Upgrade("단단한 몸", "최대 체력이 1 늘고 체력을 2 회복한다.",
                null,
                (s, p) => { s.MaxHealth += 1; p.Heal(2); }),
        };

        /// <summary>지금 고를 수 있는 강화 중에서 겹치지 않게 count개를 무작위로 뽑는다.</summary>
        public static List<Upgrade> Pick(RunStats stats, int count)
        {
            var candidates = new List<Upgrade>();
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].IsAvailable(stats)) candidates.Add(All[i]);
            }

            var picked = new List<Upgrade>();
            while (picked.Count < count && candidates.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                picked.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            return picked;
        }
    }
}
