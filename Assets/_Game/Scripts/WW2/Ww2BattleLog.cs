using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.WW2
{
    /// <summary>
    /// WWII 시험 전투 기록(패치 B 검증 기준 "피격 원인·무기별 피해·명중·회피를 기록"). 판을 다시 열면 비워진다.
    ///   - 공격 종류별: 시도(방향 확정) · 투하/발사 · 투하 전 격추로 취소 · 명중 · 빗나감(회피)
    ///   - 무기별 피해·격침은 RunReport가, 선체 피해 출처는 ShipController가 이미 모은다 — 여기서는 공격 단계만 센다.
    /// </summary>
    public static class Ww2BattleLog
    {
        public sealed class Attack
        {
            public string Name;
            public int Committed, Released, Cancelled, Hits, Misses;
        }

        private static readonly Dictionary<string, Attack> s_attacks = new();
        private static readonly List<string> s_order = new();

        public const string TorpedoBomber = "뇌격기 어뢰";
        public const string DiveBomber = "급강하 폭탄";
        public const string PlayerTorpedo = "현측 어뢰";
        public const string BossSalvo = "순양함 주포";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_attacks.Clear(); s_order.Clear(); }

        public static Attack Get(string name)
        {
            if (!s_attacks.TryGetValue(name, out var a))
            {
                s_attacks[name] = a = new Attack { Name = name };
                s_order.Add(name);
            }
            return a;
        }

        public static void Committed(string name) => Get(name).Committed++;
        public static void Released(string name) => Get(name).Released++;
        public static void Cancelled(string name) => Get(name).Cancelled++;
        public static void Hit(string name) => Get(name).Hits++;
        public static void Miss(string name) => Get(name).Misses++;

        public static IEnumerable<Attack> All { get { foreach (var n in s_order) yield return s_attacks[n]; } }

        public static string Describe()
        {
            var sb = new StringBuilder();
            foreach (var a in All)
                sb.AppendLine($"{a.Name}: 시도 {a.Committed} · 발사/투하 {a.Released} · 투하 전 격추 {a.Cancelled} · 명중 {a.Hits} · 빗나감 {a.Misses}");
            return sb.ToString().TrimEnd();
        }
    }
}
