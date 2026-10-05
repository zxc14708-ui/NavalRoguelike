using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>현재 체력을 알려 주는 표적. 표적 분배가 "이미 충분히 배정됐는가"를 판단한다.</summary>
    public interface IHasHealth
    {
        float CurrentHp { get; }
    }

    /// <summary>
    /// 유도탄(유도로켓·VLS·함대공)의 표적 분배. 날아가는 탄의 예상 피해를 표적마다 더해 두고,
    /// 이미 남은 체력만큼 배정된 표적은 다른 발사기가 건너뛰게 한다 — 한 표적에 몰리지 않는다.
    ///
    /// 규칙은 단순하다.
    ///   - 발사할 때 Claim, 탄이 명중·요격·소멸·풀 반환될 때 Release.
    ///   - 표적이 격침·요격·풀 반환되면 ReleaseTarget으로 그 표적의 배정을 모두 지운다.
    ///   - 혹시 놓친 배정은 만료 시간이 지나면 저절로 사라진다.
    /// 기관포·CIWS 같은 연사 화기는 배정하지 않는다. CIWS는 충돌이 임박하면 배정과 상관없이 추가로 쏜다.
    /// </summary>
    public static class TargetAllocator
    {
        private struct Claim
        {
            public int Id;
            public float Damage;
            public float ExpiresAt;
        }

        private static readonly Dictionary<Transform, List<Claim>> s_claims = new();
        private static readonly List<Transform> s_prune = new();
        private static int s_nextId = 1;
        private static float s_nextPrune;

        /// <summary>배정된 예상 피해 합.</summary>
        public static float AssignedDamage(ITargetable target)
        {
            if (target?.Transform == null || !s_claims.TryGetValue(target.Transform, out var list)) return 0f;

            float now = Time.time, sum = 0f;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].ExpiresAt <= now) { list.RemoveAt(i); continue; }
                sum += list[i].Damage;
            }
            return sum;
        }

        /// <summary>이 표적의 남은 체력을 이미 배정된 탄이 모두 덮는가.</summary>
        public static bool IsCovered(ITargetable target)
        {
            if (target == null) return false;
            float assigned = AssignedDamage(target);
            if (assigned <= 0f) return false;
            float hp = target is IHasHealth h ? h.CurrentHp : 1f;
            return assigned >= hp;
        }

        /// <summary>표적에 예상 피해를 배정한다. 돌려받은 번호로 나중에 Release한다.</summary>
        public static int ClaimTarget(ITargetable target, float damage, float lifetime)
        {
            if (target?.Transform == null) return 0;
            PruneOccasionally();

            if (!s_claims.TryGetValue(target.Transform, out var list))
            {
                list = new List<Claim>(4);
                s_claims[target.Transform] = list;
            }

            int id = s_nextId++;
            list.Add(new Claim { Id = id, Damage = Mathf.Max(0.01f, damage), ExpiresAt = Time.time + Mathf.Max(0.5f, lifetime) });
            return id;
        }

        /// <summary>탄 하나의 배정을 푼다(명중·요격·소멸·풀 반환).</summary>
        public static void Release(Transform target, int claimId)
        {
            if (claimId == 0 || target == null || !s_claims.TryGetValue(target, out var list)) return;
            for (int i = list.Count - 1; i >= 0; i--)
                if (list[i].Id == claimId) { list.RemoveAt(i); break; }
            if (list.Count == 0) s_claims.Remove(target);
        }

        /// <summary>표적이 사라지면 그 표적에 걸린 배정을 모두 지운다.</summary>
        public static void ReleaseTarget(Transform target)
        {
            if (target != null) s_claims.Remove(target);
        }

        private static void PruneOccasionally()
        {
            if (Time.time < s_nextPrune) return;
            s_nextPrune = Time.time + 2f;

            s_prune.Clear();
            float now = Time.time;
            foreach (var kv in s_claims)
            {
                kv.Value.RemoveAll(c => c.ExpiresAt <= now);
                if (kv.Key == null || kv.Value.Count == 0) s_prune.Add(kv.Key);
            }
            foreach (var t in s_prune) s_claims.Remove(t);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_claims.Clear();
            s_nextId = 1;
            s_nextPrune = 0f;
        }
    }
}
