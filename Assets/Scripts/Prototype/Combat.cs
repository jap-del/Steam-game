using System.Collections.Generic;
using UnityEngine;

namespace SteamGame.Prototype
{
    public enum HitSource
    {
        Player = 0,
        Shadow = 1,
    }

    public static class Combat
    {
        public static readonly Vector2 SlashSize = new Vector2(1.7f, 1.3f);
        public const float SlashReach = 1.0f;

        private static readonly HashSet<Enemy> HitThisSlash = new HashSet<Enemy>();

        /// <summary>origin에서 facing 방향으로 근접 공격을 한 번 한다. 맞은 적의 수를 돌려준다.</summary>
        public static int Slash(Vector2 origin, int facing, float damage, HitSource source)
        {
            Vector2 center = origin + new Vector2(facing * SlashReach, 0f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, SlashSize, 0f);

            HitThisSlash.Clear();
            for (int i = 0; i < hits.Length; i++)
            {
                Enemy enemy = hits[i].GetComponentInParent<Enemy>();
                if (enemy == null || !HitThisSlash.Add(enemy))
                {
                    continue;
                }
                enemy.TakeHit(damage, source, new Vector2(facing * 6f, 3f));
            }

            int count = HitThisSlash.Count;
            HitThisSlash.Clear();
            return count;
        }
    }
}
