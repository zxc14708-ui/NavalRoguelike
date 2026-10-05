#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Progression;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 명세 시험 A~G와 위협 우선순위 검사.</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 명세 18번 시험 A~G. 한 가지 무기·방어로는 모든 위협을 안정적으로 막지 못하는지 본다.
        /// 선체 피해는 검증용으로 늘린 선체(100000)에서 잰 실제 피해량이다.
        /// </summary>
        private IEnumerator SpecTests()
        {
            _report.AppendLine("\n## 명세 시험 A~G (4차)");
            var results = new List<EngageResult>();
            EngageResult Last() => results[results.Count - 1];

            // A. 기관포만
            _report.AppendLine($"\n### A. 기관포 1문만 (설치 {ResetLoadout(M("mod_autocannon"))})");
            TableHeader(_report);
            yield return Engage("A1 고속정 8", 45f, () => CombatDevTools.SpawnRing("ene_fastboat", 8, 40f), results, AllDead);
            var a1 = Last();
            yield return Engage("A2 미사일정 3", 40f, () => CombatDevTools.SpawnRing("ene_missileboat", 3, 44f), results);
            var a2 = Last();
            if (a1.Alive > 0) Fail("A: 기관포가 고속정 무리를 처리하지 못함");
            if (a2.Alive == 0) Fail("A: 기관포만으로 미사일정을 모두 잡음(한계가 없음)");

            // B. VLS 위주
            _report.AppendLine($"\n### B. VLS 3문 + 레이더 (설치 {ResetLoadout(M("mod_vls"), M("mod_vls"), M("mod_vls"), M("mod_radar"))})");
            TableHeader(_report);
            yield return Engage("B1 보스 1", 45f, () => CombatDevTools.SpawnRing("ene_boss", 1, 42f), results, AllDead);
            var b1 = Last();
            yield return Engage("B2 고속정 10", 30f, () => CombatDevTools.SpawnRing("ene_fastboat", 10, 40f), results);
            var b2 = Last();
            int vlsAtSmall = CombatStats.Weapons.TryGetValue("VLS", out var bv) ? bv.Fired : 0;
            _report.AppendLine($"- B2에서 VLS가 고속정에 쏜 수: {vlsAtSmall}");
            int minCells = int.MaxValue;
            var vlsList = AmmoUsers().FindAll(x => x.user is VlsModule);
            yield return Engage("B3 미사일정 12 (장기전)", 60f, () => CombatDevTools.SpawnRing("ene_missileboat", 12, 46f), results,
                                () => { foreach (var v in vlsList) minCells = Mathf.Min(minCells, v.user.Ammo.Current); return false; });
            var b3 = Last();
            _report.AppendLine($"- B3 VLS 최저 셀: {(minCells == int.MaxValue ? -1 : minCells)} (문당 8셀)");
            if (b1.Alive > 0) Fail("B: VLS가 보스를 제거하지 못함");
            if (vlsAtSmall > 0) Fail("B: VLS가 고속정에 미사일을 낭비함");
            if (b2.Alive == 0 && b2.HullDamage <= 0f) Fail("B: VLS 위주 함선이 고속정 무리에 아무 압박도 받지 않음");
            if (minCells > 2) _report.AppendLine("  - 참고: 장기전에서 VLS 셀이 예비선까지 줄지 않음");

            // C. CIWS 1문
            _report.AppendLine($"\n### C. CIWS 1문만 (설치 {ResetLoadout(M("mod_ciws", 2, 0))})");
            TableHeader(_report);
            yield return Engage("C1 미사일 2", 12f, () => FrontVolley(2, 20f), results, NoMissiles);
            var c1 = Last();
            yield return Engage("C2 미사일 6 포화", 12f, () => FrontVolley(6, 70f), results, NoMissiles);
            var c2 = Last();
            if (c1.MissilesIntercepted < 2) Fail($"C: CIWS 1문이 미사일 2발을 다 막지 못함({c1.MissilesIntercepted}/2)");
            if (c2.MissilesHit == 0) Fail("C: CIWS 1문이 6발 포화를 모두 막음(포화 돌파가 없음)");

            // D. CIWS 여러 문(서로 다른 곳)
            int dInstalled = ResetLoadout(M("mod_ciws", 2, 0), M("mod_ciws", 0, 2), M("mod_ciws", 0, -2), M("mod_ciws", -3, 0));
            int slots = GameManager.Instance.Player.Grid.OccupiedCells.Count;
            _report.AppendLine($"\n### D. CIWS {dInstalled}문 분산 (함선 칸 {slots}, 다른 모듈을 넣을 자리를 CIWS가 차지)");
            TableHeader(_report);
            yield return Engage("D1 미사일 6 포화", 12f, () => FrontVolley(6, 70f), results, NoMissiles);
            var d1 = Last();
            yield return Engage("D2 미사일 8 전방위", 12f, () => CombatDevTools.MissileVolley(8, 30f, 0f, damageOverride: 0.2f), results, NoMissiles);
            if (d1.MissilesIntercepted < c2.MissilesIntercepted) Fail($"D: CIWS를 늘렸는데 요격이 줄어듦({c2.MissilesIntercepted} → {d1.MissilesIntercepted})");

            // E. CIWS가 드론에 탄을 쓴 뒤 포화
            _report.AppendLine($"\n### E. CIWS 1문, 드론 뒤 포화 (설치 {ResetLoadout(M("mod_ciws", 2, 0))})");
            TableHeader(_report);
            var ciws = AmmoUsers().Find(x => x.user is CiwsModule).user;
            int ammoAtVolley = -1;
            bool reloadingAtVolley = false;
            bool volley = false;
            float eStart = 0f;
            yield return Engage("E1 드론 16 → 10초 뒤 미사일 6", 22f, () =>
            {
                CombatDevTools.SpawnRing("ene_drone", 16, 26f);
                eStart = Time.time;
            }, results, () =>
            {
                if (!volley && Time.time - eStart > 10f)
                {
                    volley = true;
                    if (ciws != null) { ammoAtVolley = ciws.Ammo.Current; reloadingAtVolley = ciws.Ammo.IsReloading; }
                    FrontVolley(6, 70f);
                }
                return false;
            });
            var e1 = Last();
            _report.AppendLine($"- 포화 순간 CIWS 탄약 {ammoAtVolley}/{(ciws != null ? ciws.Ammo.Capacity : 0)}, 재장전 중 {(reloadingAtVolley ? "예" : "아니오")} → 요격 {e1.MissilesIntercepted} · 피격 {e1.MissilesHit} (C2 대비 {c2.MissilesIntercepted}/{c2.MissilesHit})");
            if (ciws != null && ammoAtVolley >= ciws.Ammo.Capacity) Fail("E: 드론을 상대하고도 CIWS 탄약이 줄지 않음");

            // F. 탄약고: 3차 검사를 그대로 쓴다
            _report.AppendLine("\n### F. 탄약고 배치·유폭");
            ResetLoadout(M("mod_autocannon"), M("mod_ciws", 2, 0));
            yield return LogisticsCheck();

            // G. 혼합 웨이브: 한 가지 체계 vs 균형 편성
            _report.AppendLine("\n### G. 혼합 위협(고속정 8 · 미사일정 3 · 잠수함 2)");
            TableHeader(_report);
            var builds = new (string name, (string, GridCoord?)[] mods)[]
            {
                ("기관포 3", new[] { M("mod_autocannon"), M("mod_autocannon"), M("mod_autocannon") }),
                ("VLS 3+레이더", new[] { M("mod_vls"), M("mod_vls"), M("mod_vls"), M("mod_radar") }),
                ("CIWS 3", new[] { M("mod_ciws", 2, 0), M("mod_ciws", 0, 2), M("mod_ciws", 0, -2) }),
                ("균형", new[] { M("mod_autocannon"), M("mod_gun76"), M("mod_vls"), M("mod_radar"), M("mod_ciws", 2, 0), M("mod_asw"), M("mod_sonar") }),
            };
            var gResults = new List<EngageResult>();
            foreach (var (name, mods) in builds)
            {
                ResetLoadout(mods);
                yield return Engage($"G {name}", 50f, () =>
                {
                    CombatDevTools.SpawnRing("ene_fastboat", 8, 40f);
                    CombatDevTools.SpawnRing("ene_missileboat", 3, 46f, 30f);
                    CombatDevTools.SpawnSubmarines(2, 20f);
                }, results, AllDead);
                gResults.Add(Last());
            }
            var balanced = gResults[gResults.Count - 1];
            int singleSolved = 0;
            for (int i = 0; i < gResults.Count - 1; i++) if (gResults[i].Alive == 0 && gResults[i].HullDamage <= balanced.HullDamage) singleSolved++;
            if (singleSolved > 0) Fail($"G: 한 가지 체계 편성 {singleSolved}개가 균형 편성만큼 혼합 위협을 처리함");

            // 원래 시험 무장으로 되돌린다
            ResetLoadout();
            CombatDevTools.InstallTestLoadout();
        }

        private static bool NoMissiles() => TargetRegistry.Get(TargetKind.Missile).Count == 0;

        /// <summary>앞쪽 부채꼴에서 동시에 날아오는 미사일(피해는 작게).</summary>
        private static void FrontVolley(int count, float spreadDeg)
        {
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : -spreadDeg * 0.5f + spreadDeg * i / (count - 1);
                CombatDevTools.MissileVolley(1, 30f, a, damageOverride: 0.2f);
            }
        }

        // ------------------------------------------------------------ 미사일 위협·선체 위험 경고

        /// <summary>
        /// 급사 대책: 미사일정 엘리트화(체력·피해·경험치·동시 최대 2척) · 웨이브 추첨이 동시 최대 수를 지킴 ·
        /// 선체 35%/20% 아래 붉은 가장자리 맥박·경고 띠 · 큰 한 방 번쩍임 · 수리하면 꺼짐.
        /// </summary>
        private IEnumerator ThreatCheck()
        {
            _report.AppendLine("\n## 미사일 위협·선체 위험 경고");
            var ship = GameManager.Instance.Player;
            var mdef = CombatDevTools.FindEnemy("ene_missileboat");
            var fdef = CombatDevTools.FindEnemy("ene_fastboat");
            var spawner = EnemySpawner.Instance;
            CombatDevTools.ClearBattlefield();
            yield return null;

            // 1) 데이터
            _report.AppendLine($"- 미사일정: 등급 {mdef.Rank} · 체력 {mdef.MaxHp} · 미사일 {mdef.AttackDamage}×{mdef.SalvoSize}발/{mdef.AttackCooldown}초 · 경험치 {mdef.XpReward} · 동시 최대 {mdef.MaxAlive}척");
            if (mdef.Rank != Game.Data.EnemyRank.Elite || mdef.MaxAlive != 2 || mdef.AttackDamage <= 18f || mdef.MaxHp <= 22f)
                Fail("미사일정이 엘리트(체력·피해 상향, 동시 최대 2척)로 바뀌지 않음");

            // 2) 웨이브 추첨: 동시 최대 수에 걸린 적은 뽑히지 않는다
            var round = new Game.Data.RoundSet.Round
            {
                Entries = new List<Game.Data.RoundSet.SpawnEntry>
                {
                    new() { Enemy = mdef, Weight = 5f },
                    new() { Enemy = fdef, Weight = 1f },
                },
            };
            System.Func<Game.Data.EnemyDefinition, bool> allowed = d => (bool)Call(spawner, "UnderAliveCap", d);
            int PickMissile() { int n = 0; for (int i = 0; i < 300; i++) if (Game.Data.RoundSet.PickWeighted(round, allowed) == mdef) n++; return n; }
            int free = PickMissile();
            var boats = CombatDevTools.SpawnRing("ene_missileboat", 2, 70f, 90f);
            foreach (var b in boats) b.DevFrozen = true;
            yield return null;
            int capped = PickMissile();
            _report.AppendLine($"- 웨이브 추첨 300회(미사일정 가중치 5 : 고속정 1): 미사일정 0척일 때 {free}회 → 2척 살아 있을 때 {capped}회");
            if (free < 200 || capped != 0) Fail("웨이브 추첨이 미사일정 동시 최대 수를 지키지 않음");
            CombatDevTools.ClearBattlefield();

            // 3) 어뢰 잠수함 사격 패턴: 너무 가까우면 쏘지 않고, 전체 동시 어뢰 수를 제한한다
            yield return SubmarineCheck();

            // 4) 엘리트 표식(월드 · 레이더)
            yield return EliteMarkerCheck();

            // 5) 선체 위험 경고
            var overlay = Game.UI.HullDangerOverlay.Instance;
            if (overlay == null) { Fail("HullDangerOverlay 없음"); yield break; }
            ship.RepairHull(ship.HullMaxHp);
            yield return new WaitForSeconds(0.6f);
            int level0 = overlay.Level; float alpha0 = overlay.VignetteAlpha;
            int hits0 = overlay.BigHitCount;
            ship.ApplyHullDamage(ship.HullHp - ship.HullMaxHp * 0.30f);   // 한 번에 70% → 큰 한 방 + 위험
            float flashAlpha = 0f;
            for (int f = 0; f < 6; f++) { yield return null; flashAlpha = Mathf.Max(flashAlpha, overlay.VignetteAlpha); }
            yield return new WaitForSecondsRealtime(1.2f);
            float dangerMax = 0f;
            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { dangerMax = Mathf.Max(dangerMax, overlay.VignetteAlpha); yield return null; }
            int level1 = overlay.Level; bool banner1 = overlay.BannerVisible;
            yield return ScreenShot("hull_danger");
            ship.ApplyHullDamage(ship.HullHp - ship.HullMaxHp * 0.15f);
            yield return new WaitForSecondsRealtime(0.6f);
            float critMax = 0f;
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { critMax = Mathf.Max(critMax, overlay.VignetteAlpha); yield return null; }
            int level2 = overlay.Level;
            yield return ScreenShot("hull_critical");
            ship.RepairHull(ship.HullMaxHp);
            yield return new WaitForSecondsRealtime(0.6f);
            _report.AppendLine($"- 선체 경고: 정상 단계 {level0}·붉은빛 {alpha0:0.00} → 70% 한 방: 번쩍임 {flashAlpha:0.00}(큰 피해 {overlay.BigHitCount - hits0}회) · 30% 단계 {level1}·최대 {dangerMax:0.00}·경고 띠 {Yes(banner1)} → " +
                               $"15% 단계 {level2}·최대 {critMax:0.00} → 수리 후 단계 {overlay.Level}·{overlay.VignetteAlpha:0.00}·경고 띠 {Yes(overlay.BannerVisible)}");
            if (level0 != 0 || alpha0 > 0.01f) Fail("선체가 멀쩡한데 경고가 보임");
            if (overlay.BigHitCount - hits0 < 1 || flashAlpha < 0.5f) Fail("큰 한 방에 붉은 번쩍임이 없음");
            if (level1 != 1 || !banner1 || dangerMax < 0.3f) Fail("선체 35% 아래 위험 경고가 보이지 않음");
            if (level2 != 2 || critMax <= dangerMax) Fail("선체 20% 아래 치명 경고가 더 강하지 않음");
            if (overlay.Level != 0 || overlay.VignetteAlpha > 0.01f || overlay.BannerVisible) Fail("수리한 뒤에도 경고가 남음");
        }
    }
}
#endif
