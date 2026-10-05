using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>적 미사일이 어떻게 끝났는가.</summary>
    public enum ThreatOutcome { Intercepted, HitShip, Lost }

    /// <summary>
    /// 개발용 전투 통계. 무기별 발사·명중·요격 횟수와, 명중이 실제 카메라 화면 안에서 일어났는지(화면 내 명중 비율),
    /// 적 미사일이 화면에 들어온 뒤 끝나기까지 걸린 시간을 모은다.
    /// 일반 UI에는 나오지 않고 개발용 오버레이(F3)와 자동 검증만 읽는다.
    /// </summary>
    public static class CombatStats
    {
        public class WeaponEntry
        {
            public int Fired, Hits, OnScreenHits, Intercepts;
        }

        public class ThreatEntry
        {
            public int Count, Seen;
            public float SumSeconds, MinSeconds = float.MaxValue, MaxSeconds;
        }

        private static readonly Dictionary<string, WeaponEntry> s_weapons = new();
        private static readonly Dictionary<ThreatOutcome, ThreatEntry> s_threats = new();

        public static IReadOnlyDictionary<string, WeaponEntry> Weapons => s_weapons;
        public static IReadOnlyDictionary<ThreatOutcome, ThreatEntry> Threats => s_threats;

        /// <summary>풀 인스턴스 이름으로 무기를 알아낸다. 적 탄이면 null.</summary>
        public static string KeyFor(GameObject go)
        {
            if (go == null) return null;
            string n = go.name;
            if (n.StartsWith("PRJ_Autocannon")) return "기관포";
            if (n.StartsWith("PRJ_Ciws")) return "CIWS";
            if (n.StartsWith("PRJ_Gun76")) return "76mm";
            if (n.StartsWith("MIS_PlayerRocket")) return "유도로켓";
            if (n.StartsWith("MIS_PlayerVls")) return "VLS";
            if (n.StartsWith("MIS_PlayerSam")) return "SAM";
            if (n.StartsWith("DC_Player")) return "폭뢰";
            return null;
        }

        private static WeaponEntry Get(string key)
        {
            if (!s_weapons.TryGetValue(key, out var e)) s_weapons[key] = e = new WeaponEntry();
            return e;
        }

        public static void RecordFire(string key, int count = 1)
        {
            if (string.IsNullOrEmpty(key)) return;
            Get(key).Fired += count;
        }

        public static void RecordHit(string key, Vector3 position)
        {
            if (string.IsNullOrEmpty(key)) return;
            var e = Get(key);
            e.Hits++;
            if (IsOnScreen(position)) e.OnScreenHits++;
        }

        public static void RecordIntercept(string key, Vector3 position)
        {
            if (string.IsNullOrEmpty(key)) return;
            var e = Get(key);
            e.Intercepts++;
            e.Hits++;
            if (IsOnScreen(position)) e.OnScreenHits++;
        }

        /// <summary>적 미사일 종료. screenEnteredAt이 음수면 화면에 한 번도 들어오지 않은 것.</summary>
        public static void RecordThreatEnd(ThreatOutcome outcome, float screenEnteredAt)
        {
            if (!s_threats.TryGetValue(outcome, out var e)) s_threats[outcome] = e = new ThreatEntry();
            e.Count++;
            if (screenEnteredAt < 0f) return;

            float seconds = Time.time - screenEnteredAt;
            e.Seen++;
            e.SumSeconds += seconds;
            e.MinSeconds = Mathf.Min(e.MinSeconds, seconds);
            e.MaxSeconds = Mathf.Max(e.MaxSeconds, seconds);
        }

        /// <summary>실제 카메라 투영으로 화면 안인지 판정한다.</summary>
        public static bool IsOnScreen(Vector3 world)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f;
        }

        public static void Reset()
        {
            s_weapons.Clear();
            s_threats.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
