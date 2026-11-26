using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 보스전 요약(2026-11-26, 1스테이지 연안 경비정부터). 결과 화면 한 줄 — "체력이 줄어서 이겼다"가 아니라
    /// "저쪽 발사관을 부수고 그쪽에 붙어서 이겼다"가 보이게: 일제사격 · 막아 낸 사격(못 돌려 거름 · 예고 중 파괴) ·
    /// 열린 발사관에 들어간 피격 · 부서진 쪽에 붙어 있던 시간 · 부위를 부순 시각.
    /// 새 판(RunReport.BeginRun)에서 비운다. 이어하기 저장에는 넣지 않는다(보스전 도중에는 저장하지 않음).
    /// </summary>
    public static class BossDuelLog
    {
        public static bool Fought { get; private set; }
        public static string Boss { get; private set; }
        public static int Salvos, Blocked, Cancelled, ExposedHits;
        public static float DeadSideSeconds;
        public static readonly List<string> PartKills = new();
        private static float s_start;

        public static void Reset()
        {
            Fought = false; Boss = null;
            Salvos = Blocked = Cancelled = ExposedHits = 0;
            DeadSideSeconds = 0f;
            PartKills.Clear();
        }

        public static void Begin(string boss)
        {
            Reset();
            Fought = true; Boss = boss; s_start = Time.time;
        }

        public static void PartKilled(string part)
        {
            if (!Fought) return;
            float t = Time.time - s_start;
            PartKills.Add($"{part} {Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}");
        }

        /// <summary>결과 화면 한 줄(싸우지 않았으면 빈 문자열).</summary>
        public static string Line()
        {
            if (!Fought) return "";
            var sb = new System.Text.StringBuilder($"{Boss}: 일제사격 {Salvos}회 · 막아 냄 {Blocked + Cancelled}회");
            if (Cancelled > 0) sb.Append($"(예고 중 파괴 {Cancelled})");
            if (ExposedHits > 0) sb.Append($" · 열린 발사관 명중 {ExposedHits}");
            if (DeadSideSeconds >= 1f) sb.Append($" · 부서진 쪽에 붙음 {DeadSideSeconds:0}초");
            if (PartKills.Count > 0) sb.Append($"\n<size=88%><color=#9fb3c0>부위 파괴: {string.Join(" · ", PartKills)}</color></size>");
            return sb.ToString();
        }
    }
}
