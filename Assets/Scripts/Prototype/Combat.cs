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

        private static readonly HashSet<Enemy> HitThisAttack = new HashSet<Enemy>();

        /// <summary>origin에서 facing 방향으로 근접 공격을 한 번 한다. 맞은 적의 수를 돌려준다.</summary>
        public static int Slash(Vector2 origin, int facing, float damage, HitSource source, float scale = 1f)
        {
            Vector2 center = origin + new Vector2(facing * SlashReach * scale, 0f);
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, SlashSize * scale, 0f);
            return Apply(hits, damage, source, origin);
        }

        /// <summary>center 주변 원 안의 모든 적에게 피해를 준다.</summary>
        public static int Burst(Vector2 center, float radius, float damage, HitSource source)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
            return Apply(hits, damage, source, center);
        }

        /// <summary>그 지점이 바닥이나 벽 같은 지형 안인지 확인한다. 적과 플레이어는 지형으로 치지 않는다.</summary>
        public static bool IsSolidAt(Vector2 point)
        {
            Collider2D[] hits = Physics2D.OverlapPointAll(point);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (!hit.isTrigger && hit.attachedRigidbody == null && hit.GetComponentInParent<Enemy>() == null)
                {
                    return true;
                }
            }
            return false;
        }

        private static int Apply(Collider2D[] hits, float damage, HitSource source, Vector2 origin)
        {
            HitThisAttack.Clear();
            for (int i = 0; i < hits.Length; i++)
            {
                Enemy enemy = hits[i].GetComponentInParent<Enemy>();
                if (enemy == null || !HitThisAttack.Add(enemy))
                {
                    continue;
                }
                float direction = enemy.transform.position.x >= origin.x ? 1f : -1f;
                enemy.TakeHit(damage, source, new Vector2(direction * 6f, 3f));
            }

            int count = HitThisAttack.Count;
            HitThisAttack.Clear();
            return count;
        }
    }
}
