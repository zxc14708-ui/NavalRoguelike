#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>
    /// 자동 전투 검증 — 밸런스 비교(2026-11-21, 손상 통제 4단계): 블록이 받는 피해 몫(BalanceConfig.moduleDamageShare)을 바꿔 가며
    /// 같은 적 묶음과 실제로 싸우고, 기함이 몇 초 만에 "격침될 만큼"(원래 선체만큼) 깎이는지 · 블록이 몇 개 부서지는지 · 화재 · 침수를 잰다.
    /// 판정 없이 표만 남긴다(수치 결정용). 원본 데이터는 바꾸지 않는다(DamageResolver.DevModuleShareOverride).
    /// </summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator BalanceSimCheck()
        {
            _report.AppendLine("\n## 밸런스 비교 — 블록이 받는 피해 몫(2026-11-21)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            float hullLimit = ship.Hull != null ? ship.Hull.HullHp : ship.Config != null ? ship.Config.HullMaxHp : 100f;
            ShipFire.Enabled = true; ShipFlooding.Enabled = true;   // 실제 교전처럼(검사 환경은 꺼 둔다)
            var fire = ship.GetComponent<ShipFire>();
            var flood = ship.GetComponent<ShipFlooding>();
            var csv = new StringBuilder("share,trial,virtual_death_s,hull_lost_45s,blocks_destroyed,fires,holes,cookoffs\n");
            var shares = new[] { 0.6f, 0.45f, 0.3f };
            const float FightSeconds = 45f;
            _report.AppendLine($"- 같은 적 묶음(미사일정 2 · 고속정 3 · 자폭 드론 6 · 어뢰정 1)과 {FightSeconds:0}초 실제 교전 · 기함 무장 · 손상 통제 켬 · 선체 {hullLimit:0}만큼 깎이면 \"격침\"으로 셈");
            _report.AppendLine("| 블록 몫 | 판 | 격침까지(초) | 45초 선체 손실 | 부서진 블록 | 화재 | 구멍 | 유폭 |");
            _report.AppendLine("|---|---|---|---|---|---|---|---|");
            s_pauseModuleHealing = true;
            var summary = new List<(float share, float death, int blocks)>();
            foreach (float share in shares)
                for (int trial = 0; trial < 2; trial++)
                {
                    CombatDevTools.ClearBattlefield();
                    RestoreAllBlocks(ship);
                    fire?.ExtinguishAll(); flood?.DrainAll();
                    MakeInvulnerable(ship);
                    DamageResolver.DevModuleShareOverride = share;
                    Random.InitState(1000 + trial);
                    yield return new WaitForSeconds(0.5f);
                    int fires0 = fire != null ? fire.Ignited : 0, holes0 = flood != null ? flood.HolesMade : 0, cook0 = fire != null ? fire.CookOffs : 0;
                    float hull0 = ship.HullHp, t0 = Time.time, death = -1f;
                    CombatDevTools.SpawnRing("ene_missileboat", 2, 70f, trial * 40f);
                    CombatDevTools.SpawnRing("ene_fastboat", 3, 45f, 60f + trial * 40f);
                    CombatDevTools.SpawnRing("ene_drone", 6, 60f, 30f + trial * 40f);
                    CombatDevTools.SpawnRing("ene_torpedo_boat", 1, 50f, 200f + trial * 40f);
                    while (Time.time - t0 < FightSeconds)
                    {
                        if (death < 0f && hull0 - ship.HullHp >= hullLimit) death = Time.time - t0;
                        yield return new WaitForSeconds(0.2f);
                    }
                    float lost = hull0 - ship.HullHp;
                    int blocks = ship.Grid.Modules.Count(m => m != null && m.IsDestroyed);
                    int fires = fire != null ? fire.Ignited - fires0 : 0, holes = flood != null ? flood.HolesMade - holes0 : 0, cooks = fire != null ? fire.CookOffs - cook0 : 0;
                    string d = death < 0f ? "버팀" : $"{death:0}";
                    _report.AppendLine($"| {share:0.00} | {trial + 1} | {d} | {lost:0} | {blocks} | {fires} | {holes} | {cooks} |");
                    csv.AppendLine($"{share:0.00},{trial + 1},{(death < 0f ? FightSeconds : death):0.0},{lost:0},{blocks},{fires},{holes},{cooks}");
                    summary.Add((share, death < 0f ? FightSeconds : death, blocks));
                }
            DamageResolver.DevModuleShareOverride = -1f;
            s_pauseModuleHealing = false;
            CombatDevTools.ClearBattlefield();
            RestoreAllBlocks(ship);
            fire?.ExtinguishAll(); flood?.DrainAll();
            File.WriteAllText(Path.Combine(OutputDirectory, "balance_sim.csv"), csv.ToString(), new UTF8Encoding(false));
            foreach (var g in summary.GroupBy(s => s.share))
                _report.AppendLine($"- 블록 몫 {g.Key:0.00}: 격침까지 평균 {g.Average(x => x.death):0}초(45초 = 버팀) · 부서진 블록 평균 {g.Average(x => x.blocks):0.0}개");
            _report.AppendLine("- 원자료 balance_sim.csv");
            if (summary.Count == 0) Fail("밸런스 비교가 한 판도 돌지 않음");
        }

        /// <summary>부서진 블록을 모두 되살리고 내구를 가득(판 사이 초기화).</summary>
        private static void RestoreAllBlocks(ShipController ship)
        {
            if (ship?.Grid == null) return;
            foreach (var m in ship.Grid.Modules)
            {
                if (m == null) continue;
                if (m.IsDestroyed) m.Restore(1f);
                m.Repair(m.MaxHp);
            }
        }
    }
}
#endif
