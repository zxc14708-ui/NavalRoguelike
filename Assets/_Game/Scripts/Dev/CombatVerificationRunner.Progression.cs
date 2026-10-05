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
    /// <summary>자동 전투 검증 — 장비 개량·성장 카드·모듈 변형 검사.</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 블록 강화: 강화 데이터 · 카드 흐름(설치/강화 선택·취소·설치) · 블록마다 다른 단계 · 재배치 유지 · 새 블록 0 ·
        /// 최종 스탯 한 번만(기관포·76mm·CIWS 실제 발사) · 탄약고 지원 중복 없음 · 외형 단계·피벗 재연결 · 외형 누락 폴백 ·
        /// 강화해도 탄약 비율·쿨타임 유지 · 예외 없음.
        /// </summary>
        private IEnumerator UpgradeCheck()
        {
            _report.AppendLine("\n## 블록 강화");
            _upgradeErrors = 0;
            Application.logMessageReceived += CountErrors;
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1f);

            // 0) 데이터
            var sbData = new StringBuilder();
            foreach (var id in new[] { "mod_autocannon", "mod_gun76", "mod_ciws", "mod_magazine" })
            {
                var d = CombatDevTools.FindModule(id);
                var p = ModuleUpgrades.ProfileFor(d);
                sbData.Append($"{id} {(p != null ? $"최대 {p.MaxLevel}" : "없음")} · ");
                if (p == null || p.MaxLevel != 2) Fail($"{id} 강화 데이터가 없거나 최대 단계가 2가 아님");
            }
            _report.AppendLine($"- 강화 데이터: {sbData}");

            var ac = CombatDevTools.FindModule("mod_autocannon");
            var a = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            var b = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            if (a == null || b == null || refit == null) { Fail("시험 준비 실패(기관포 설치 또는 정비 화면 없음)"); Application.logMessageReceived -= CountErrors; yield break; }

            // 1) 카드 흐름: 무장 강화는 독립 카드(설치 카드와 따로). 같은 블록이 둘이면 고르고, 취소하면 카드로 돌아온다
            var upgradeCards = new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.WeaponUpgrade(ac), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) };
            GameManager.Instance.SetState(GameState.Refit);
            refit.Open(99, upgradeCards, () => { });   // 3장: 호위함 개량은 3번째 카드를 대체한다
            yield return null;
            yield return RefitShot(refit, "upgrade_1_cards");
            Call(refit, "ChooseCard", 0);
            bool picking0 = Get<bool>(refit, "_upgradePicking"), held0 = Get<ModuleDefinition>(refit, "_held") != null;
            yield return null;
            yield return RefitShot(refit, "upgrade_2_choice");
            int markers = 0;
            foreach (var t in Get<List<Transform>>(refit, "_upgradeMarkers") ?? new List<Transform>()) if (t != null && t.gameObject.activeSelf) markers++;
            Call(refit, "BackToCards");
            bool backToCards = Get<bool>(refit, "_choosing");
            Call(refit, "ChooseCard", 0);
            bool picking = Get<bool>(refit, "_upgradePicking");
            yield return null;
            yield return RefitShot(refit, "upgrade_3_pick");
            bool upgraded = (bool)Call(refit, "TryUpgradeAt", a.Origin);
            int aLv = a.UpgradeLevel, bLv = b.UpgradeLevel;
            _report.AppendLine($"- 카드: 무장 강화 카드 → 강화 대상 선택 {(picking0 ? "예" : "아니오")}(설치 선택 없음: 들기 {held0}) · 강조 {markers}개 · 취소 → 카드 {(backToCards ? "예" : "아니오")} · 다시 선택 {picking} · 기관포 A 강화 {upgraded} → A {aLv} / B {bLv}");
            if (!picking0 || held0 || !backToCards || !picking || !upgraded) Fail("무장 강화 카드 흐름이 동작하지 않음");
            if (aLv != 1 || bLv != 0) Fail("강화가 고른 블록 하나에만 적용되지 않음");
            if (markers < 2) Fail("강화 대상 강조가 부족함");

            // 설치 카드는 강화 선택 없이 바로 들어 올린다(같은 블록이 있어도)
            int countBefore = grid.CountOf(ac);
            refit.Open(99, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.Install(ac), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) }, () => { });
            Call(refit, "ChooseCard", 0);
            bool holding = Get<ModuleDefinition>(refit, "_held") == ac, pickingNow = Get<bool>(refit, "_upgradePicking");
            Call(refit, "PlaceHeld");
            int countAfter = grid.CountOf(ac);
            _report.AppendLine($"- 설치 카드: 들기 {holding} · 강화 선택으로 빠지지 않음 {!pickingNow} · 기관포 {countBefore} → {countAfter}문(강화 단계 A {a.UpgradeLevel} 그대로)");
            if (!holding || pickingNow || countAfter != countBefore + 1) Fail("설치 카드가 동작하지 않음");

            // 대상이 하나뿐이면 고르지 않고 바로 적용(탄약고: 시험장에 없으면 하나만 설치해 둔다)
            var magDef = CombatDevTools.FindModule("mod_magazine");
            var existingMags = new List<ModuleInstance>();
            ModuleUpgrades.GetTargets(grid, magDef, existingMags);
            ModuleInstance onlyMag = existingMags.Count == 1 ? existingMags[0] : null;
            bool installedHere = false;
            if (existingMags.Count == 0) { onlyMag = CombatDevTools.InstallModuleNear("mod_magazine", new GridCoord(0, 0), 8); installedHere = onlyMag != null; }
            if (onlyMag != null)
            {
                refit.Open(99, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.WeaponUpgrade(magDef), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) }, () => { });
                Call(refit, "ChooseCard", 0);
                bool auto = onlyMag.UpgradeLevel == 1 && !Get<bool>(refit, "_upgradePicking");
                _report.AppendLine($"- 대상이 하나뿐인 강화 카드: 바로 적용 {(auto ? "예" : "아니오")}(탄약고 단계 {onlyMag.UpgradeLevel})");
                if (!auto) Fail("대상이 하나뿐인데 바로 적용되지 않음");
                if (installedHere) CombatDevTools.RemoveModule(onlyMag);
            }
            else _report.AppendLine("- 대상이 하나뿐인 강화 카드: 시험장에 탄약고가 둘 이상이라 건너뜀");

            // 2) 블록마다 다른 단계
            a.ApplyUpgrade();
            b.ApplyUpgrade();
            bool maxBlocked = !a.ApplyUpgrade();
            _report.AppendLine($"- 단계: A {a.UpgradeLevel} · B {b.UpgradeLevel} · 최대에서 더 강화 거부 {maxBlocked}");
            if (a.UpgradeLevel != 2 || b.UpgradeLevel != 1 || !maxBlocked) Fail("블록별 강화 단계가 맞지 않음");

            // 7·8) 외형과 피벗
            string Visual(ModuleInstance m)
            {
                var v = m.Runtime != null ? m.Runtime.GetComponent<ModuleUpgradeVisuals>() : null;
                var w = m.Runtime != null ? Get<WeaponController>(m.Runtime, "weapon") : null;
                string muzzleIn = "-";
                if (w?.Muzzle != null)
                    for (var t = w.Muzzle; t != null; t = t.parent)
                        if (t.name.StartsWith("Visual_Level")) { muzzleIn = t.name; break; }
                return $"외형 {(v != null ? v.ActiveIndex.ToString() : "없음")} · 포구 {muzzleIn}";
            }
            string visA = Visual(a), visB = Visual(b);
            _report.AppendLine($"- 외형: A(II) {visA} / B(I) {visB}");
            if (!visA.Contains("외형 2") || !visA.Contains("Visual_Level3")) Fail("강화 II 외형 또는 포구 재연결이 맞지 않음");
            if (!visB.Contains("외형 1") || !visB.Contains("Visual_Level2")) Fail("강화 I 외형 또는 포구 재연결이 맞지 않음");

            // 3) 재배치(정비 화면에서 집어 다른 칸에 놓기)
            int idA = a.InstanceId;
            Put(refit, "_cursor", a.Origin);
            Call(refit, "Confirm");
            GridCoord spot = default;
            int spotRot = 0;
            bool found = false;
            for (int d = 1; d <= 8 && !found; d++)
                for (int x = -d; x <= d && !found; x++)
                    for (int z = -d; z <= d && !found; z++)
                        for (int rot = 0; rot < 4 && !found; rot++)
                        {
                            var c = new GridCoord(x, z);
                            if (c.Equals(Get<GridCoord>(refit, "_heldOrigin")) || !grid.CanPlace(ac, c, rot, out _)) continue;
                            spot = c; spotRot = rot; found = true;
                        }
            Put(refit, "_cursor", spot);
            Put(refit, "_heldRotation", spotRot);
            Call(refit, "PlaceHeld");
            var moved = grid.Get(spot);
            _report.AppendLine($"- 재배치: {(moved != null ? $"({spot.X},{spot.Z})로 이동 · 번호 {idA} → {moved.InstanceId} · 단계 {moved.UpgradeLevel} · {Visual(moved)}" : "실패")}");
            if (moved == null || moved.InstanceId != idA || moved.UpgradeLevel != 2) Fail("재배치 후 강화 단계·번호가 유지되지 않음");
            if (moved != null) a = moved;
            yield return null;
            yield return RefitShot(refit, "upgrade_4_badges");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);

            // 4) 새 블록
            var fresh = new ModuleInstance(ac);
            _report.AppendLine($"- 새 블록: 단계 {fresh.UpgradeLevel} · 번호 {fresh.InstanceId}(기존과 다름 {fresh.InstanceId != idA}) · 새 게임은 씬을 새로 열어 모든 블록이 새로 만들어진다");
            if (fresh.UpgradeLevel != 0 || fresh.InstanceId == idA) Fail("새 블록이 강화 0에서 시작하지 않음");

            // 5) 최종 스탯은 한 번만(기본 대비)
            var s0 = ac.Stats;
            var sa = a.Runtime.Stats;
            var gun = CombatDevTools.InstallModuleNear("mod_gun76", new GridCoord(0, 0), 8);
            var ciws = CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
            if (gun == null || ciws == null) { Fail("76mm·CIWS 설치 실패"); }
            else
            {
                gun.ApplyUpgrade(); gun.ApplyUpgrade();
                ciws.ApplyUpgrade(); ciws.ApplyUpgrade();
                var g0 = gun.Definition.Stats; var g = gun.Runtime.Stats;
                var c0 = ciws.Definition.Stats; var c = ciws.Runtime.Stats;
                int ciwsCap = (ciws.Runtime as IAmmoUser)?.Ammo?.Capacity ?? -1;
                _report.AppendLine($"- 기관포 II: 피해 {s0.Damage} → {sa.Damage:0.##} · 탄창 {s0.MagazineCapacity} → {sa.MagazineCapacity} · 선회 {s0.TurretTurnRate} → {sa.TurretTurnRate:0}");
                _report.AppendLine($"- 76mm II: 피해 {g0.Damage} → {g.Damage:0.##} · 발사 간격 {g0.ReloadTime} → {g.ReloadTime:0.###}");
                _report.AppendLine($"- CIWS II: 선회 {c0.TurretTurnRate} → {c.TurretTurnRate:0} · 탄창 {c0.MagazineCapacity} → {c.MagazineCapacity}(실제 {ciwsCap}) · 재장전 {c0.AmmoReloadTime} → {c.AmmoReloadTime:0.##} · 피해 {c.Damage} · 사거리 {c.Range} · 발사 간격 {c.ReloadTime}");
                if (!Mathf.Approximately(sa.Damage, s0.Damage * 1.5f) || sa.MagazineCapacity != 260 || !Mathf.Approximately(sa.TurretTurnRate, s0.TurretTurnRate * 1.15f)) Fail("기관포 강화 II 수치가 기본 대비가 아님");
                if (!Mathf.Approximately(g.Damage, g0.Damage * 1.5f) || !Mathf.Approximately(g.ReloadTime, g0.ReloadTime * 0.9f)) Fail("76mm 강화 II 수치가 기본 대비가 아님");
                if (!Mathf.Approximately(c.TurretTurnRate, c0.TurretTurnRate * 1.35f) || c.MagazineCapacity != 630 || ciwsCap != 630 ||
                    !Mathf.Approximately(c.AmmoReloadTime, c0.AmmoReloadTime * 0.85f) || c.Damage != c0.Damage || c.Range != c0.Range || c.ReloadTime != c0.ReloadTime)
                    Fail("CIWS 강화 II 수치가 명세와 다름(공격력·사거리·연사는 그대로여야 함)");
                if (sa.Damage * (1f / s0.ReloadTime) >= 2f * s0.Damage * (1f / s0.ReloadTime)) Fail("강화 II 한 문의 화력이 기본 두 문 이상");
            }

            // 실제 발사: 강화 II 기관포 한 문만 켜고 멈춘 고속정을 쏜다
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r is not IRadarSource) r.enabled = r == a.Runtime;
            var target = CombatDevTools.SpawnRing("ene_fastboat", 3, 15f, 30f);   // 사격각이 어느 쪽이든 하나는 들어오게
            foreach (var e in target) e.DevFrozen = true;
            float shotDamage = -1f, t0 = Time.time;
            while (Time.time - t0 < 6f && shotDamage < 0f)
            {
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled && p.name.StartsWith("PRJ_Autocannon")) { shotDamage = Get<float>(p, "_damage"); break; }
                yield return null;
            }
            float eff = ac.TargetEfficiency.For(TargetCategory.SmallSurface);
            _report.AppendLine($"- 실제 발사(기관포 II → 고속정): 탄 피해 {shotDamage:0.###} · 기대 {s0.Damage * 1.5f * eff:0.###}(기본 {s0.Damage} × 1.5 × 상성 {eff})");
            if (shotDamage < 0f || !Mathf.Approximately(shotDamage, s0.Damage * 1.5f * eff)) Fail("발사된 탄의 피해에 강화가 한 번만 적용되지 않음");
            CombatDevTools.ClearBattlefield();
            // 강화 II 기관포 A만 켜 둔다(꺼진 런타임은 설치·강화 이벤트를 받지 않아 탄약고 지원을 다시 잡지 않는다)
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = r == a.Runtime;
            Put(a.Runtime, "_targeting", null);   // 표적이 남아 쏘지 않게(지원 배율만 본다)

            // 6) 탄약고 지원(지원량만 커지고 무기 쪽에서 두 번 곱하지 않는다)
            var mag = CombatDevTools.InstallModuleNear("mod_magazine", a.Origin, 2);
            if (mag == null) Fail("탄약고 설치 실패");
            else
            {
                var bonus0 = MagazineSupport.For(grid, a, true);
                mag.ApplyUpgrade(); mag.ApplyUpgrade();
                var bonus2 = MagazineSupport.For(grid, a, true);
                var aw = Get<WeaponController>(a.Runtime, "weapon");
                float applied = Get<float>(aw, "_reloadMultiplier");
                int cap = (a.Runtime as IAmmoUser).Ammo.Capacity;
                var m0 = mag.Definition.Stats;
                float expectReload = 1f - m0.ReloadBonus * 1.3f, expectCapMul = 1f + m0.AmmoCapacityBonus * 1.3f;
                int expectCap = Mathf.RoundToInt(260 * expectCapMul);
                _report.AppendLine($"- 탄약고 II: 발사 간격 배율 {bonus0.ReloadMultiplier:0.###} → {bonus2.ReloadMultiplier:0.###}(기관포 적용 {applied:0.###}) · 용량 배율 {bonus0.CapacityMultiplier:0.###} → {bonus2.CapacityMultiplier:0.###} · 기관포 II 탄창 {cap}(기대 {expectCap}) · 유폭 {m0.CookOffDamage} → {mag.EffectiveStats.CookOffDamage:0}");
                if (!Mathf.Approximately(bonus2.ReloadMultiplier, expectReload) || !Mathf.Approximately(bonus2.CapacityMultiplier, expectCapMul)) Fail("탄약고 강화 지원량이 기본 대비 +30%가 아님");
                if (!Mathf.Approximately(applied, bonus2.ReloadMultiplier)) Fail("무기에 적용된 탄약고 배율이 다름(중복 또는 미갱신)");
                if (cap != expectCap) Fail("탄약고 용량 보너스가 중복되거나 빠짐");
                if (!Mathf.Approximately(mag.EffectiveStats.CookOffDamage, m0.CookOffDamage * 0.75f)) Fail("탄약고 강화 II 유폭 감소가 적용되지 않음");
            }

            // 9) 강화해도 남은 탄약 비율·쿨타임 유지
            var bAmmo = (b.Runtime as IAmmoUser).Ammo;
            for (int i = 0; i < 60; i++) bAmmo.Consume();
            float ratio = (float)bAmmo.Current / bAmmo.Capacity;
            var bw = Get<WeaponController>(b.Runtime, "weapon");
            Put(bw, "_cooldown", 0.37f);
            b.ApplyUpgrade();
            float ratioAfter = (float)bAmmo.Current / bAmmo.Capacity;
            float cdAfter = Get<float>(bw, "_cooldown");
            _report.AppendLine($"- 강화 중 상태 유지(B I→II): 탄 {ratio:P0} → {ratioAfter:P0}({bAmmo.Current}/{bAmmo.Capacity}) · 쿨타임 0.37 → {cdAfter:0.##}");
            if (Mathf.Abs(ratioAfter - ratio) > 0.02f || !Mathf.Approximately(cdAfter, 0.37f)) Fail("강화할 때 탄약·쿨타임이 초기화됨");

            // 외형 누락 폴백(모델이 없으면 기본 외형 유지)
            var probe = new GameObject("VisualFallbackProbe");
            var baseVis = new GameObject("Visual_Level1"); baseVis.transform.SetParent(probe.transform, false);
            var vis = probe.AddComponent<ModuleUpgradeVisuals>();
            Put(vis, "levels", new GameObject[] { baseVis, null, null });
            vis.ApplyLevel(3, null);
            bool fallback = baseVis.activeSelf && vis.ActiveIndex == 0;
            Destroy(probe);
            _report.AppendLine($"- 외형 모델이 없을 때: 기본 외형 유지 {(fallback ? "예" : "아니오")}");
            if (!fallback) Fail("강화 외형이 없을 때 기본 외형이 꺼짐");

            yield return new WaitForSeconds(0.5f);
            Application.logMessageReceived -= CountErrors;
            _report.AppendLine($"- 검사 중 예외·MissingReference: {_upgradeErrors}건");
            if (_upgradeErrors > 0) Fail("강화 검사 중 예외 발생");

            foreach (var d in off) if (d != null) d.enabled = true;
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            ship.DevRudderOverride = null;
            CombatDevTools.ClearBattlefield();
        }

        /// <summary>
        /// 성장 카드: 등급별 수치(공격력 5/10/20%) · 능력별 실제 반영(공격력·계열 공격력·연사력·장탄수·사거리·수리·탐지·속력·선체·방어력·스킬 회복) ·
        /// 합산 누적 · 강화와 곱해져 한 번만 적용 · 실제 발사 피해 · 초기화 · 카드 추첨 규칙(3장 자리·강화 확률 상승·등급 확률 상승·보스 에픽·장비 조건) ·
        /// 카드 화면과 선택 · 보스 보상 흐름 · Tab 현황 요약.
        /// </summary>
        private IEnumerator GrowthCheck()
        {
            _report.AppendLine("\n## 성장 카드(카드 시스템 개편)");
            Game.Modules.RunUpgrades.Reset();
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            var draft = Object.FindFirstObjectByType<Game.Refit.RefitDraft>(FindObjectsInactive.Include);
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(0.6f);
            if (refit == null || draft == null) { Fail("정비 화면 또는 카드 추첨기가 없음"); yield break; }

            // 1) 등급별 수치
            var sbCat = new StringBuilder();
            bool catalogOk = Game.Modules.RunUpgrades.Catalog.Count == 13;
            foreach (var d in Game.Modules.RunUpgrades.Catalog)
            {
                sbCat.Append($"{d.Name} {d.Values[0] * 100:0.#}/{d.Values[1] * 100:0.#}/{d.Values[2] * 100:0.#}% · ");
                if (!(d.Values[0] < d.Values[1] && d.Values[1] < d.Values[2]) || d.Weight <= 0f || d.Values[0] <= 0f) catalogOk = false;
            }
            var dmgDef = Game.Modules.RunUpgrades.Definition(Game.Modules.RunStat.Damage);
            bool dmgOk = Near(dmgDef.Values[0], 0.05f) && Near(dmgDef.Values[1], 0.10f) && Near(dmgDef.Values[2], 0.20f);
            _report.AppendLine($"- 카탈로그 {Game.Modules.RunUpgrades.Catalog.Count}종(일반/희귀/에픽): {sbCat}");
            if (!catalogOk || !dmgOk) Fail("성장 카드 수치표가 맞지 않음(공격력은 5/10/20%, 등급이 오를수록 커야 함)");

            // 2) 능력별 실제 반영 — 장비를 설치하고 기본값을 적어 둔다
            var ac = CombatDevTools.FindModule("mod_autocannon");
            var a = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            var gun = CombatDevTools.InstallModuleNear("mod_gun76", new GridCoord(0, 0), 8);
            var rocket = CombatDevTools.InstallModuleNear("mod_rocket", new GridCoord(0, 0), 8);
            var ciws = CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
            var repair = CombatDevTools.InstallModuleNear("mod_repairbay", new GridCoord(0, 0), 8);
            var decoy = CombatDevTools.InstallModuleNear("mod_decoy", new GridCoord(0, 0), 8);
            if (a == null || gun == null || rocket == null || ciws == null || repair == null || decoy == null)
            { Fail("성장 카드 시험용 장비 설치 실패"); yield break; }
            yield return new WaitForSeconds(0.3f);

            var A0 = a.Definition.Stats; var G0 = gun.Definition.Stats; var R0 = rocket.Definition.Stats; var C0 = ciws.Definition.Stats; var P0 = repair.Definition.Stats;
            float detection0 = ship.Systems.DetectionRange, speed0 = ship.BaseMaxSpeed, hullMax0 = ship.HullMaxHp, hull0 = ship.HullHp;
            int ammoCap0 = (a.Runtime as IAmmoUser).Ammo.Capacity;
            var Add = (System.Action<Game.Modules.RunStat, Game.Modules.CardTier>)Game.Modules.RunUpgrades.Add;

            // 공격력(공통) 일반+희귀+에픽 = +35% 합산, 계열 카드는 해당 무장에만
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Common);
            bool d5 = Near(a.Runtime.Stats.Damage, A0.Damage * 1.05f);
            float d5Ratio = a.Runtime.Stats.Damage / A0.Damage;
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Rare);
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);
            bool dAll = Near(a.Runtime.Stats.Damage, A0.Damage * 1.35f) && Near(gun.Runtime.Stats.Damage, G0.Damage * 1.35f) &&
                        Near(rocket.Runtime.Stats.Damage, R0.Damage * 1.35f) && Near(ciws.Runtime.Stats.Damage, C0.Damage * 1.35f);
            Add(Game.Modules.RunStat.GunDamage, Game.Modules.CardTier.Rare);       // 함포계 +16%
            Add(Game.Modules.RunStat.MissileDamage, Game.Modules.CardTier.Epic);   // 미사일계 +32%
            bool dClass = Near(a.Runtime.Stats.Damage, A0.Damage * 1.51f) && Near(gun.Runtime.Stats.Damage, G0.Damage * 1.51f) &&
                          Near(rocket.Runtime.Stats.Damage, R0.Damage * 1.67f) && Near(ciws.Runtime.Stats.Damage, C0.Damage * 1.35f);
            _report.AppendLine($"- 공격력: 일반 +5% → 기관포 {d5Ratio:0.###}배(기대 1.05) · 일반+희귀+에픽 합산 {(dAll ? "1.35배 전 무장" : "불일치")} · 계열 카드 후 기관포 {a.Runtime.Stats.Damage / A0.Damage:0.##} 76mm {gun.Runtime.Stats.Damage / G0.Damage:0.##} 유도로켓 {rocket.Runtime.Stats.Damage / R0.Damage:0.##} CIWS {ciws.Runtime.Stats.Damage / C0.Damage:0.##}(기대 1.51/1.51/1.67/1.35)");
            if (!d5) Fail("공격력 일반 카드가 +5%로 반영되지 않음");
            if (!dAll) Fail("공격력 카드가 합산(+35%)으로 모든 무장에 적용되지 않음");
            if (!dClass) Fail("계열 공격력 카드가 해당 무장에만 적용되지 않음");

            // 연사력(공통 +10%) + 방공계(+8%) → CIWS는 +18%
            Add(Game.Modules.RunStat.FireRate, Game.Modules.CardTier.Rare);
            Add(Game.Modules.RunStat.AirDefenseRate, Game.Modules.CardTier.Common);
            bool fr = Near(a.Runtime.Stats.ReloadTime, A0.ReloadTime / 1.10f) && Near(ciws.Runtime.Stats.ReloadTime, C0.ReloadTime / 1.18f) &&
                      Near(decoy.Runtime.Stats.ReloadTime, decoy.Definition.Stats.ReloadTime);   // 기만체(스킬)는 연사력이 아니다
            _report.AppendLine($"- 연사력 +10%: 기관포 발사 간격 {A0.ReloadTime:0.###} → {a.Runtime.Stats.ReloadTime:0.###}(기대 {A0.ReloadTime / 1.10f:0.###}) · CIWS(+방공계 8%) {C0.ReloadTime:0.###} → {ciws.Runtime.Stats.ReloadTime:0.####}(기대 {C0.ReloadTime / 1.18f:0.####}) · 기만체 재장전 그대로");
            if (!fr) Fail("연사력 카드가 발사 간격에 반영되지 않았거나 스킬 장비까지 바뀜");

            // 장탄수(+32%) · 사거리(+8%)
            Add(Game.Modules.RunStat.Magazine, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Range, Game.Modules.CardTier.Rare);
            int expectCap = Game.Modules.ModuleUpgrades.ScaleCapacity(A0.MagazineCapacity, 1.32f, A0.AmmoPerShot);
            int liveCap = (a.Runtime as IAmmoUser).Ammo.Capacity;
            bool mag = a.Runtime.Stats.MagazineCapacity == expectCap && liveCap > ammoCap0;
            bool rng = Near(a.Runtime.Stats.Range, A0.Range * 1.08f) && Near(gun.Runtime.Stats.Range, G0.Range * 1.08f) && Near(a.Runtime.Stats.MinRange, A0.MinRange);
            _report.AppendLine($"- 장탄수 +32%: 기관포 탄창 {A0.MagazineCapacity} → {a.Runtime.Stats.MagazineCapacity}(기대 {expectCap}) · 실제 탄창 {ammoCap0} → {liveCap} / 사거리 +8%: 기관포 {A0.Range:0.#} → {a.Runtime.Stats.Range:0.#}(기대 {A0.Range * 1.08f:0.#}) · 최소 거리 그대로");
            if (!mag) Fail("장탄수 카드가 탄창 용량(스탯·실제 탄창)에 반영되지 않음");
            if (!rng) Fail("사거리 카드가 최대 사거리에만 반영되지 않음");

            // 수리 효율(+40%) · 탐지(+32%) · 속력(+8%)
            Add(Game.Modules.RunStat.Repair, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Detection, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Speed, Game.Modules.CardTier.Rare);
            yield return null;
            bool rep = Near(repair.Runtime.Stats.HullRepairPerSecond, P0.HullRepairPerSecond * 1.4f) && Near(repair.Runtime.Stats.ModuleRepairPerSecond, P0.ModuleRepairPerSecond * 1.4f);
            bool det = Near(ship.Systems.DetectionRange, detection0 * 1.32f);
            bool spd = Near(ship.BaseMaxSpeed, speed0 * 1.08f);
            _report.AppendLine($"- 수리 효율 +40%: 선체 {P0.HullRepairPerSecond:0.##} → {repair.Runtime.Stats.HullRepairPerSecond:0.##}/초 · 탐지 +32%: {detection0:0.#} → {ship.Systems.DetectionRange:0.#} · 최고 속력 +8%: {speed0:0.##} → {ship.BaseMaxSpeed:0.##}");
            if (!rep) Fail("수리 효율 카드가 손상 통제반 수리 속도에 반영되지 않음");
            if (!det) Fail("탐지 거리 카드가 함선 탐지 거리에 반영되지 않음");
            if (!spd) Fail("최고 속력 카드가 반영되지 않음");

            // 선체 강화(+24%): 최대치가 늘고 늘어난 만큼 회복
            Add(Game.Modules.RunStat.HullMax, Game.Modules.CardTier.Epic);
            bool hullMax = Near(ship.HullMaxHp, hullMax0 * 1.24f) && Near(ship.HullHp, hull0 + hullMax0 * 0.24f);
            _report.AppendLine($"- 선체 강화 +24%: 최대 {hullMax0:0} → {ship.HullMaxHp:0} · 현재 {hull0:0} → {ship.HullHp:0}(늘어난 만큼 회복)");
            if (!hullMax) Fail("선체 강화 카드가 최대치와 현재 선체에 반영되지 않음");

            // 방어력(+12%): 받는 피해 ÷ 1.12. 같은 타격을 카드 전후로 비교
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Vector3 hitPoint = ship.transform.position;
            float h0 = ship.HullHp;
            ship.TakeDamage(new DamageInfo(20f, hitPoint, Vector3.down, DamageSource.Gun));
            float lost0 = h0 - ship.HullHp;
            Add(Game.Modules.RunStat.Defense, Game.Modules.CardTier.Epic);
            float h1 = ship.HullHp;
            ship.TakeDamage(new DamageInfo(20f, hitPoint, Vector3.down, DamageSource.Gun));
            float lost1 = h1 - ship.HullHp;
            _report.AppendLine($"- 방어력 +12%: 같은 20 피해의 선체 손실 {lost0:0.###} → {lost1:0.###}(기대 {lost0 / 1.12f:0.###}) · 표시 \"{Game.Modules.RunUpgrades.Describe(Game.Modules.RunStat.Defense, 0.12f)}\"");
            if (lost0 <= 0f || !Near(lost1, lost0 / 1.12f, 0.01f)) Fail("방어력 카드가 받는 피해를 1/1.12로 줄이지 않음");

            // 스킬 회복 속도(+16%): 기만체 재장전 타이머가 1.16배로 흐른다
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Put(decoy.Runtime, "_cooldown", 30f);
            float tA = Time.time;
            yield return new WaitForSeconds(1.5f);
            float dropPlain = 30f - Get<float>(decoy.Runtime, "_cooldown"), spanPlain = Time.time - tA;
            Add(Game.Modules.RunStat.SkillRate, Game.Modules.CardTier.Rare);   // +16%
            Put(decoy.Runtime, "_cooldown", 30f);
            tA = Time.time;
            yield return new WaitForSeconds(1.5f);
            float dropFast = 30f - Get<float>(decoy.Runtime, "_cooldown"), spanFast = Time.time - tA;
            float ratio = (dropFast / spanFast) / (dropPlain / spanPlain);
            _report.AppendLine($"- 스킬 회복 +16%: 기만체 재장전이 1초에 {dropPlain / spanPlain:0.##}초 → {dropFast / spanFast:0.##}초 흐름(비율 {ratio:0.###}, 기대 1.16)");
            if (ratio < 1.12f || ratio > 1.20f) Fail("스킬 회복 속도 카드가 능동 스킬 재장전에 반영되지 않음");

            // 3) 강화(블록)와 성장은 서로 곱해지되 각각 한 번만
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);   // +20%
            a.ApplyUpgrade();                                                // 기관포 강화 I: 피해 ×1.25
            bool both = Near(a.Runtime.Stats.Damage, A0.Damage * 1.25f * 1.20f);
            _report.AppendLine($"- 강화 I(×1.25) + 공격력 에픽(+20%): 기관포 피해 {a.Runtime.Stats.Damage:0.###}(기대 {A0.Damage * 1.5f:0.###} = 기본 {A0.Damage} × 1.25 × 1.20)");
            if (!both) Fail("무장 강화와 성장 카드가 한 번씩만 곱해지지 않음");

            // 4) 실제 발사 피해: 강화 I 기관포 한 문만 켜고 멈춘 고속정을 쏜다
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = r == a.Runtime;
            var target = CombatDevTools.SpawnRing("ene_fastboat", 3, 15f, 30f);
            foreach (var e in target) e.DevFrozen = true;
            float shotDamage = -1f, ts = Time.time;
            while (Time.time - ts < 6f && shotDamage < 0f)
            {
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled && p.name.StartsWith("PRJ_Autocannon")) { shotDamage = Get<float>(p, "_damage"); break; }
                yield return null;
            }
            float eff = ac.TargetEfficiency.For(TargetCategory.SmallSurface);
            _report.AppendLine($"- 실제 발사: 탄 피해 {shotDamage:0.###}(기대 {A0.Damage * 1.5f * eff:0.###} = 기본 × 1.25 × 1.20 × 상성 {eff})");
            if (shotDamage < 0f || !Near(shotDamage, A0.Damage * 1.5f * eff, 0.005f)) Fail("발사된 탄의 피해에 강화·성장이 한 번씩만 적용되지 않음");
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;   // 켜면 스탯을 다시 계산한다
            yield return null;

            // 5) 초기화
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.HullMax, Game.Modules.CardTier.Epic);
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            bool resetOk = !Game.Modules.RunUpgrades.Any && Near(a.Runtime.Stats.Damage, A0.Damage * 1.25f) && Near(ship.HullMaxHp, hullMax0, 0.002f);
            _report.AppendLine($"- 초기화: 보너스 없음 {(!Game.Modules.RunUpgrades.Any ? "예" : "아니오")} · 기관포(강화 I만) 피해 {a.Runtime.Stats.Damage:0.###}(기대 {A0.Damage * 1.25f:0.###}) · 선체 최대 {ship.HullMaxHp:0}(기대 {hullMax0:0})");
            if (!resetOk) Fail("성장 카드 초기화 뒤 수치가 원래대로 돌아오지 않음");

            // 6) 카드 추첨 규칙
            // 6-1) 3장 자리: 1번 = 설치, 2번 = 성장(강화 또는 성장 카드), 같은 카드 중복 없음
            int draws = 400, installSlot = 0, growthSlot = 0, dup = 0, malformed = 0, upgradeAt1 = 0, upgradeAt20 = 0, notUnified = 0;
            for (int i = 0; i < draws; i++)
            {
                var cards = draft.DrawCards(3, 1);
                if (cards.Count != 3) { malformed++; continue; }
                if (cards[0].Kind == Game.Refit.RefitCardKind.Install) installSlot++;
                if (cards[1].Kind != Game.Refit.RefitCardKind.Install) growthSlot++;
                var seen = new HashSet<string>();
                foreach (var c in cards) if (!seen.Add(c.Label)) dup++;
                if (cards[1].Kind == Game.Refit.RefitCardKind.WeaponUpgrade) upgradeAt1++;
                // 통합 장비 강화 카드(2026-10-03): 블록이 정해지지 않은 한 종류, 한 번 뽑을 때 한 장까지
                int upgrades = 0;
                foreach (var c in cards)
                    if (c.Kind == Game.Refit.RefitCardKind.WeaponUpgrade) { upgrades++; if (c.Module != null) notUnified++; }
                if (upgrades > 1) notUnified++;
            }
            for (int i = 0; i < draws; i++)
            {
                var cards = draft.DrawCards(3, 20);
                if (cards.Count == 3 && cards[1].Kind == Game.Refit.RefitCardKind.WeaponUpgrade) upgradeAt20++;
            }
            _report.AppendLine($"- 3장 자리({draws}회): 1번이 설치 {installSlot} · 2번이 성장/강화 {growthSlot} · 중복 {dup} · 장수 이상 {malformed} / 2번 자리의 무장 강화 비율 레벨 1 {upgradeAt1 * 100f / draws:0}% → 레벨 20 {upgradeAt20 * 100f / draws:0}%(기대 약 30% → 65%)");
            if (malformed > 0 || installSlot < draws * 0.98f || growthSlot != draws - malformed) Fail("카드 자리 규칙(1번 설치 · 2번 성장)이 지켜지지 않음");
            if (dup > 0) Fail("한 번 뽑을 때 같은 카드가 겹침");
            _report.AppendLine($"- 장비 강화 카드 통합: 블록별 카드 또는 한 번에 두 장 이상 {notUnified}회(기대 0)");
            if (notUnified > 0) Fail("장비 강화 카드가 하나로 통합되지 않음(블록별 카드 또는 두 장 이상)");
            if (upgradeAt20 <= upgradeAt1 + draws * 0.15f) Fail("진행할수록 무장 강화 카드 확률이 올라가지 않음");

            // 6-2) 등급 확률: 레벨이 오를수록 희귀·에픽이 늘고, 에픽 수치가 일반보다 크다
            int N = 6000, rare1 = 0, epic1 = 0, rare20 = 0, epic20 = 0;
            var rng0 = new System.Random(7);
            for (int i = 0; i < N; i++)
            {
                var t1 = Game.Modules.RunUpgrades.RollTier(1, rng0); if (t1 == Game.Modules.CardTier.Rare) rare1++; else if (t1 == Game.Modules.CardTier.Epic) epic1++;
                var t20 = Game.Modules.RunUpgrades.RollTier(20, rng0); if (t20 == Game.Modules.CardTier.Rare) rare20++; else if (t20 == Game.Modules.CardTier.Epic) epic20++;
            }
            _report.AppendLine($"- 등급 확률({N}회): 레벨 1 희귀 {rare1 * 100f / N:0.#}% 에픽 {epic1 * 100f / N:0.#}% → 레벨 20 희귀 {rare20 * 100f / N:0.#}% 에픽 {epic20 * 100f / N:0.#}%");
            if (rare20 <= rare1 || epic20 < epic1 * 3) Fail("진행할수록 희귀·에픽 확률이 충분히 오르지 않음");

            // 6-3) 보스 보상: 2번 자리가 에픽 성장 카드로 고정
            int bossOk = 0;
            for (int i = 0; i < 100; i++)
            {
                var cards = draft.DrawCards(3, 5, bossReward: true);
                if (cards.Count == 3 && cards[1].Kind == Game.Refit.RefitCardKind.Growth && cards[1].Tier == Game.Modules.CardTier.Epic && cards[1].BossReward) bossOk++;
            }
            _report.AppendLine($"- 보스 보상 카드 100회: 2번 자리가 에픽 성장 카드 {bossOk}회");
            if (bossOk != 100) Fail("보스 보상에 에픽 성장 카드가 보장되지 않음");

            // 6-4) 장비 조건: 계열·수리 카드는 그 장비가 있을 때만
            bool Has(params ModuleType[] types)
            {
                foreach (var m in grid.Modules) if (m != null && m.IsOperational && System.Array.IndexOf(types, m.Definition.Type) >= 0) return true;
                return false;
            }
            int gated = 0, gatedChecked = 0;
            for (int i = 0; i < 600; i++)
                foreach (var c in draft.DrawCards(3, 10))
                {
                    if (c.Kind != Game.Refit.RefitCardKind.Growth) continue;
                    var gd = Game.Modules.RunUpgrades.Definition(c.Stat);
                    if (gd.RequiresAny == null) continue;
                    gatedChecked++;
                    if (!Has(gd.RequiresAny)) gated++;
                }
            // 수리반을 철거한 뒤에는 수리 카드가 한 번도 나오지 않는다
            CombatDevTools.RemoveModule(repair);
            int repairCards = 0;
            for (int i = 0; i < 600; i++)
                foreach (var c in draft.DrawCards(3, 10)) if (c.Kind == Game.Refit.RefitCardKind.Growth && c.Stat == Game.Modules.RunStat.Repair) repairCards++;
            _report.AppendLine($"- 장비 조건: 계열·수리 카드 {gatedChecked}장 중 장비 없이 나온 것 {gated} · 수리반 철거 뒤 수리 카드 {repairCards}장");
            if (gated > 0 || repairCards > 0) Fail("장비가 없는데 계열·수리 성장 카드가 나옴");

            // 7) 카드 화면: 무장 강화 + 보스 에픽 + 설치, 그리고 세 등급
            GameManager.Instance.SetState(GameState.Refit);
            var shown = new List<Game.Refit.RefitCard>
            {
                Game.Refit.RefitCard.WeaponUpgrade(ac),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic, true),
                Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")),
            };
            refit.Open(8, shown, () => { }, bossReward: true);
            yield return null;
            yield return RefitShot(refit, "growth_1_boss_cards");
            var tierCards = new List<Game.Refit.RefitCard>
            {
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.FireRate, Game.Modules.CardTier.Common),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.GunDamage, Game.Modules.CardTier.Rare),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Defense, Game.Modules.CardTier.Epic),
            };
            refit.Open(8, tierCards, () => { }, false);
            yield return null;
            yield return RefitShot(refit, "growth_2_tiers");
            var title = Get<TMPro.TMP_Text>(refit, "titleText");

            // 8) 성장 카드를 고르면 즉시 적용되고 배치로 넘어간다(설치 선택 없음)
            refit.Open(8, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")) }, () => { });
            float dmgBefore = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.Damage);
            Call(refit, "ChooseCard", 0);
            float dmgAfter = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.Damage);
            bool chosen = !Get<bool>(refit, "_choosing") && Get<ModuleDefinition>(refit, "_held") == null;
            string growthText = Get<string>(refit, "_growthResult");
            yield return null;
            yield return RefitShot(refit, "growth_3_applied");
            _report.AppendLine($"- 성장 카드 선택: 공격력 {dmgBefore * 100f:0}% → {dmgAfter * 100f:0}% · 카드 닫힘·배치로 {(chosen ? "예" : "아니오")} · 결과 문구 \"{growthText}\"");
            if (!Near(dmgAfter - dmgBefore, 0.20f) || !chosen || string.IsNullOrEmpty(growthText)) Fail("성장 카드 선택이 즉시 적용·진행되지 않음");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);

            // 9) 보스 보상 흐름: RefitController가 에픽 카드가 든 보상 화면을 열고, 닫으면 콜백이 불린다
            var controller = Object.FindFirstObjectByType<Game.Refit.RefitController>(FindObjectsInactive.Include);
            bool finished = false;
            if (controller == null) Fail("RefitController 없음");
            else
            {
                controller.BeginBossReward(4, () => finished = true);
                var bossCards = Get<List<Game.Refit.RefitCard>>(refit, "_cards");
                bool epicShown = false;
                foreach (var c in bossCards) if (c.Kind == Game.Refit.RefitCardKind.Growth && c.Tier == Game.Modules.CardTier.Epic && c.BossReward) epicShown = true;
                bool titleBoss = (Get<TMPro.TMP_Text>(refit, "titleText")?.text ?? "").Contains("보스");
                int epicIndex = bossCards.FindIndex(c => c.BossReward);
                yield return null;
                yield return RefitShot(refit, "growth_4_boss_flow");
                float before = Game.Modules.RunUpgrades.Get(bossCards[epicIndex].Stat);
                Call(refit, "ChooseCard", epicIndex);
                float gain = Game.Modules.RunUpgrades.Get(bossCards[epicIndex].Stat) - before;
                Call(refit, "TryLaunch");
                _report.AppendLine($"- 보스 보상 화면: 에픽 카드 {(epicShown ? "있음" : "없음")} · 제목에 보스 표시 {(titleBoss ? "예" : "아니오")} · 에픽 선택 +{gain * 100f:0.#}% · 닫으면 다음 단계로 {(finished ? "예" : "아니오")}");
                if (!epicShown || !titleBoss) Fail("보스 보상 화면에 에픽 카드·제목이 표시되지 않음");
                if (gain < 0.05f || !finished) Fail("보스 보상 선택이 적용·종료되지 않음");
                if (GameManager.Instance.State == GameState.Refit) GameManager.Instance.SetState(GameState.Playing);
            }

            // 10) Tab 현황판: 쌓은 성장 요약
            var tab = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (tab != null)
            {
                tab.SetOpen(true);
                yield return new WaitForSecondsRealtime(0.3f);
                string footer = Get<TMPro.TMP_Text>(tab, "_growthFooter")?.text ?? "";
                yield return ScreenShot("growth_5_tab");
                _report.AppendLine($"- Tab 현황 성장 줄: \"{footer}\"");
                if (!footer.Contains("공격력")) Fail("Tab 현황판에 쌓은 성장 카드가 보이지 않음");
                tab.SetOpen(false);
            }

            // 정리
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = null;
        }

        /// <summary>
        /// 위치별 소나·폭뢰(2026-10-03): 자리 → 형태 판정(선수/예인/함내 소나, 투하대/발사대) · 형태를 위치로 다시 판정(앞을 막으면 함내 소나,
        /// 치우면 선수 소나) · 사방이 막힌 자리 설치 불가 · 이미 놓인 폭뢰를 사방으로 막는 설치 거부 · 형태 모델과 방향 ·
        /// 소나 방향·반경(선수 ±70°, 예인 1.5배·빠르면 절반, 함내 65%) · 실제 탐지 · 발사대(트인 현측만)·투하대(항적 위 4발) 실제 투하.
        /// </summary>
        private IEnumerator VariantCheck()
        {
            _report.AppendLine("\n## 위치별 소나·폭뢰");
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(2f);
            if (grid == null || factory == null) { Fail("격자·ModuleFactory 없음"); yield break; }

            var sonarDef = CombatDevTools.FindModule("mod_sonar");
            var aswDef = CombatDevTools.FindModule("mod_asw");
            // 막는 칸에 쓸 1×1 "어디든" 블록
            ModuleDefinition filler = null;
            foreach (var id in new[] { "mod_radar", "mod_repairbay", "mod_ew", "mod_decoy", "mod_magazine", "mod_bridge" })
            {
                var d = CombatDevTools.FindModule(id);
                if (d != null && d.Width == 1 && d.Height == 1 && d.Placement.Zone == PlacementZone.Anywhere && d.MaxCount == 0) { filler = d; break; }
            }
            if (sonarDef == null || aswDef == null || filler == null) { Fail($"시험 블록 없음(소나 {sonarDef != null} · 폭뢰 {aswDef != null} · 막는 블록 {filler?.Id ?? "없음"})"); yield break; }
            _report.AppendLine($"- 폭뢰 배치 규칙 {aswDef.Placement.Zone} · 막는 블록 {filler.DisplayName}");
            if (aswDef.Placement.Zone != PlacementZone.SideOrStern) Fail("폭뢰 배치 규칙이 '옆이나 뒤가 트인 자리'가 아님");

            var added = new List<ModuleInstance>();
            ModuleInstance Put1(ModuleDefinition d, int x, int z)
            {
                var c = new GridCoord(x, z);
                if (!grid.CanPlace(d, c, 0, out _)) return null;
                var m = factory.Install(d, c, 0);
                if (m != null) added.Add(m);
                return m;
            }
            void Take(ModuleInstance m) { if (m == null) return; added.Remove(m); CombatDevTools.RemoveModule(m); }
            string V(ModuleInstance m) => m == null ? "(설치 실패)" : $"{ModuleVariants.Name(m.Variant)}({m.Variant})";
            string VisualName(ModuleInstance m)
            {
                var vv = m?.Runtime != null ? m.Runtime.GetComponent<ModuleVariantVisual>() : null;
                return vv != null && vv.Current != null ? vv.Current.name : "(기본 모델)";
            }
            float SocketAngle(ModuleInstance m, string socket, Vector3 want)
            {
                var vv = m?.Runtime != null ? m.Runtime.GetComponent<ModuleVariantVisual>() : null;
                var s = vv != null ? ModuleVariantVisual.Find(vv.Current, socket) : null;
                if (s == null) return 999f;
                Vector3 have = s.position - vv.Current.position; have.y = 0f; want.y = 0f;
                return Vector3.Angle(have, want);
            }

            // 선수·선미 끝 칸(가운데 줄)
            grid.GetExtent(out var min, out var max);
            int zBow = 0, zStern = 0;
            for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++) { if (!grid.IsFree(new GridCoord(max.X, z))) { zBow = z; if (z == 0) break; } }
            for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++) { if (!grid.IsFree(new GridCoord(min.X, z))) { zStern = z; if (z == 0) break; } }
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // 1) 소나: 앞 끝 → 선수, 뒤 끝 → 예인, 앞을 막으면 함내 → 치우면 다시 선수(위치로 다시 판정)
            var bow = Put1(sonarDef, max.X + 1, zBow);
            var tas = Put1(sonarDef, min.X - 1, zStern);
            string bowV0 = V(bow), tasV0 = V(tas);
            string bowVis = VisualName(bow), tasVis = VisualName(tas);
            float bowAim = SocketAngle(bow, "ForwardMarker", fwd), tasAim = SocketAngle(tas, "ForwardMarker", fwd);
            var block = Put1(filler, max.X + 2, zBow);
            yield return null;
            string bowBlocked = V(bow), bowBlockedVis = VisualName(bow);
            var bowBlockedVariant = bow != null ? bow.Variant : ModuleVariant.None;
            bool blockPlaced = block != null;
            Take(block);
            yield return null;
            string bowAgain = V(bow);
            _report.AppendLine($"- 소나: 앞 끝 → {bowV0}[{bowVis}, 선수 표식 오차 {bowAim:0}°] · 뒤 끝 → {tasV0}[{tasVis}, 오차 {tasAim:0}°] · 앞을 막음 → {bowBlocked}[{bowBlockedVis}] · 다시 치움 → {bowAgain}");
            if (bow == null || bow.Variant != ModuleVariant.BowSonar) Fail("앞이 트인 자리의 소나가 선수 소나가 아님");
            if (tas == null || tas.Variant != ModuleVariant.TowedSonar) Fail("뒤가 트인 자리의 소나가 예인 소나가 아님");
            if (!blockPlaced) Fail("선수 소나 앞을 막는 블록을 놓지 못함(시험 무효)");
            else if (bowBlockedVariant != ModuleVariant.HullSonar) Fail("앞을 막아도 소나 형태가 다시 판정되지 않음(함내 소나여야 함)");
            if (bow != null && bow.Variant != ModuleVariant.BowSonar) Fail("막은 블록을 치워도 선수 소나로 돌아오지 않음");
            if (!bowVis.Contains("MOD_Sonar_Bow") || !tasVis.Contains("MOD_Sonar_TAS") || !bowBlockedVis.Contains("MOD_Sonar_Internal")) Fail("형태별 소나 모델이 붙지 않음");
            if (bowAim > 15f || tasAim > 15f) Fail("소나 모델이 함수 방향으로 놓이지 않음");
            yield return CloseShot("variant_sonar_bow", bow != null && bow.Runtime != null ? bow.Runtime.transform : ship.transform, 6f);
            yield return CloseShot("variant_sonar_tas", tas != null && tas.Runtime != null ? tas.Runtime.transform : ship.transform, 6f);

            // 2) 소나 탐지 범위: 선수만 / 예인만(정지·전속) / 함내만
            var sys = ship.Systems;
            float baseR = sonarDef.Stats.DetectionRange * RunUpgrades.DetectionMultiplier;
            Vector3 P(float along, float side) => ship.transform.position + fwd * along + right * side;
            Take(tas);
            sys.Recalculate();
            bool bowAhead = sys.SonarCovers(P(baseR * 0.8f, 0f)), bowAft = sys.SonarCovers(P(-baseR * 0.5f, 0f)), bowBeam = sys.SonarCovers(P(0f, baseR * 0.5f));
            Take(bow);
            tas = Put1(sonarDef, min.X - 1, zStern);
            sys.Recalculate();
            bool tasFar = sys.SonarCovers(P(-baseR * 1.3f, 0f));
            float tasRest = sys.SonarRange;
            ship.SetEngineOrder(1f);
            float t0 = Time.time;
            while (Time.time - t0 < 12f && sys.SpeedRatio < ModuleVariants.TowedFastSpeedRatio + 0.02f) yield return null;
            bool tasFarFast = sys.SonarCovers(P(-baseR * 1.3f, 0f));
            float tasFast = sys.SonarRange, speedRatio = sys.SpeedRatio;
            ship.SetEngineOrder(0f);
            Take(tas);
            // 함내 소나: 앞뒤를 모두 막은 소나
            var hull = Put1(sonarDef, max.X + 1, zBow);
            var hullFront = Put1(filler, max.X + 2, zBow);
            sys.Recalculate();
            bool hullNear = sys.SonarCovers(P(-baseR * 0.55f, 0f)), hullFar = sys.SonarCovers(P(-baseR * 0.8f, 0f));
            string hullV = V(hull);
            _report.AppendLine($"- 소나 범위(기본 {baseR:0}m): 선수 — 앞 {Yes(bowAhead)} · 뒤 {Yes(bowAft)} · 옆 {Yes(bowBeam)} / 예인 — 뒤 {baseR * 1.3f:0}m {Yes(tasFar)}(반경 {tasRest:0}) · 전속({speedRatio * 100f:0}%) {Yes(tasFarFast)}(반경 {tasFast:0}) / {hullV} — 뒤 {baseR * 0.55f:0}m {Yes(hullNear)} · {baseR * 0.8f:0}m {Yes(hullFar)}");
            if (!bowAhead || bowAft || bowBeam) Fail("선수 소나가 앞쪽 ±70°만 듣지 않음");
            if (!tasFar || !Mathf.Approximately(tasRest, baseR * ModuleVariants.TowedRangeMultiplier)) Fail("예인 소나가 정지 중 1.5배 반경으로 뒤를 듣지 않음");
            if (speedRatio >= ModuleVariants.TowedFastSpeedRatio && (tasFarFast || tasFast > baseR)) Fail("예인 소나가 빠를 때 반경이 줄지 않음");
            if (speedRatio < ModuleVariants.TowedFastSpeedRatio) _report.AppendLine("  - 참고: 12초 안에 최고 속력 70%에 닿지 않아 예인 소나 감속 판정은 생략");
            if (hull == null || hull.Variant != ModuleVariant.HullSonar || !hullNear || hullFar) Fail("함내 소나가 사방 65% 반경으로 듣지 않음");

            // 실제 탐지: 잠항 잠수함을 뒤 20m에 — 선수 소나는 못 찾고 예인 소나는 찾는다
            Take(hullFront); Take(hull);
            yield return new WaitForSeconds(3f);   // 전속에서 멈출 시간
            var subDef = CombatDevTools.FindEnemy("ene_submarine");
            bool Detect(ModuleInstance sonar, out SubmarineBase subOut)
            {
                subOut = null;
                var e = EnemySpawner.Instance.SpawnAt(subDef, P(-20f, 2f), Quaternion.LookRotation(fwd));
                if (e is SubmarineBase sb) { sb.DevFrozen = true; subOut = sb; }
                return subOut != null;
            }
            bow = Put1(sonarDef, max.X + 1, zBow);
            sys.Recalculate();
            bool bowLive = false, tasLive = false;
            if (Detect(bow, out var sub1))
            {
                t0 = Time.time;
                while (Time.time - t0 < 4f && !sub1.IsContactConfirmed) yield return null;
                bowLive = sub1.IsContactConfirmed;
            }
            CombatDevTools.ClearBattlefield();
            Take(bow);
            tas = Put1(sonarDef, min.X - 1, zStern);
            sys.Recalculate();
            if (Detect(tas, out var sub2))
            {
                t0 = Time.time;
                while (Time.time - t0 < 6f && !sub2.IsContactConfirmed) yield return null;
                tasLive = sub2.IsContactConfirmed;
            }
            CombatDevTools.ClearBattlefield();
            Take(tas);   // 선미 끝 자리를 투하대 시험에 비워 둔다
            _report.AppendLine($"- 실제 탐지(잠항 잠수함 뒤 20m): 선수 소나만 → 접촉 {Yes(bowLive)} · 예인 소나만 → 접촉 {Yes(tasLive)}");
            if (bowLive) Fail("선수 소나가 뒤쪽 잠수함을 찾음");
            if (!tasLive) Fail("예인 소나가 뒤쪽 잠수함을 찾지 못함");

            // 3) 폭뢰 자리: 뒤·양옆이 막힌 자리는 설치 불가 → 한쪽을 치우면 발사대 → 그 쪽을 다시 막는 설치는 거부
            int X1 = max.X + 1;
            int zc = zBow;   // 선수 끝의 가운데 칸 앞에 시험 구조(양옆 한 칸씩 필요)
            if (Mathf.Abs(zc) > grid.MaxHalfBeam - 1) Fail("선수 끝 칸이 함폭 끝이라 폭뢰 자리 시험 구조를 만들 수 없음");
            var a = Put1(filler, X1, zc);
            var e1 = Put1(filler, X1, zc + 1);
            var f1 = Put1(filler, X1, zc - 1);
            var pp = Put1(filler, X1 + 1, zc + 1);
            var qq = Put1(filler, X1 + 1, zc - 1);
            bool built = a != null && e1 != null && f1 != null && pp != null && qq != null;
            var T = new GridCoord(X1 + 1, zc);
            bool enclosedRefused = !grid.CanPlace(aswDef, T, 0, out string enclosedReason);
            Take(qq);
            var proj = Put1(aswDef, T.X, T.Z);
            string projV = V(proj), projSides = proj != null ? proj.Sides.ToString() : "-";
            bool reblockRefused = !grid.CanPlace(filler, new GridCoord(X1 + 1, zc - 1), 0, out string reblockReason);
            float projAim = SocketAngle(proj, "OutboardDirection", -right);
            _report.AppendLine($"- 폭뢰 자리: 시험 구조 {Yes(built)} · 뒤·양옆이 막힌 자리 설치 거부 {Yes(enclosedRefused)}(\"{enclosedReason}\") · 좌현을 열면 → {projV} 현측 {projSides}[{VisualName(proj)}, 바깥쪽 표식 오차 {projAim:0}°] · 다시 막는 설치 거부 {Yes(reblockRefused)}(\"{reblockReason}\")");
            if (!built) Fail("폭뢰 자리 시험 구조를 만들지 못함(시험 무효)");
            if (!enclosedRefused) Fail("사방이 막힌 자리에 폭뢰를 설치할 수 있음");
            if (proj == null || proj.Variant != ModuleVariant.DepthChargeProjector || (proj.Sides & ModuleSides.Port) == 0) Fail("옆이 트인 자리의 폭뢰가 좌현 발사대가 아님");
            if (!reblockRefused) Fail("이미 놓인 폭뢰의 옆·뒤를 모두 막는 설치가 허용됨");
            if (!VisualName(proj).Contains("MOD_DepthChargeProjector") || projAim > 20f) Fail("발사대 모델이 없거나 트인 현측을 향하지 않음");

            // 선미 투하대
            var rack = Put1(aswDef, min.X - 1, zStern);
            float rackAim = SocketAngle(rack, "ForwardMarker", fwd);
            _report.AppendLine($"- 뒤 끝 폭뢰 → {V(rack)}[{VisualName(rack)}, 선수 표식 오차 {rackAim:0}°]");
            if (rack == null || rack.Variant != ModuleVariant.DepthChargeRack) Fail("뒤가 트인 자리의 폭뢰가 투하대가 아님");
            if (!VisualName(rack).Contains("MOD_DepthChargeRack") || rackAim > 20f) Fail("투하대 모델이 없거나 방향이 틀림");
            yield return CloseShot("variant_projector", proj != null && proj.Runtime != null ? proj.Runtime.transform : ship.transform, 7f);
            yield return CloseShot("variant_rack", rack != null && rack.Runtime != null ? rack.Runtime.transform : ship.transform, 6f);

            // 4) 실제 투하·발사: 드러난 잠수함을 고정해 두고 해당 폭뢰만 켠다
            var projRt = proj?.Runtime as Game.Modules.Runtime.AswLauncherModule;
            var rackRt = rack?.Runtime as Game.Modules.Runtime.AswLauncherModule;
            foreach (var r in Object.FindObjectsByType<Game.Modules.Runtime.AswLauncherModule>(FindObjectsSortMode.None)) r.enabled = false;
            IEnumerator Engage(Game.Modules.Runtime.AswLauncherModule rt, Vector3 at, float wait, System.Action<int, float, List<Vector3>> done)
            {
                var e = EnemySpawner.Instance.SpawnAt(subDef, at, Quaternion.LookRotation(fwd));
                var sb = e as SubmarineBase;
                if (sb != null) { sb.DevFrozen = true; sb.RevealFor(30f); }
                float hp0 = sb != null ? sb.CurrentHp : 0f;
                int s0 = rt != null ? rt.Salvos : 0;
                if (rt != null) { Put(rt, "_cooldown", 0f); rt.enabled = true; }
                float tt = Time.time;
                while (Time.time - tt < wait && (rt == null || rt.Salvos == s0)) yield return null;
                yield return new WaitForSeconds(2.5f);   // 비행 + 가라앉음
                if (rt != null) rt.enabled = false;
                done(rt != null ? rt.Salvos - s0 : 0, sb != null ? hp0 - sb.CurrentHp : 0f, rt != null ? new List<Vector3>(rt.LastAims) : new List<Vector3>());
                CombatDevTools.ClearBattlefield();
                yield return null;
            }
            Vector3 projPos = proj != null && proj.Runtime != null ? proj.Runtime.transform.position : ship.transform.position;
            int pPort = 0, pStar = 0; float pPortLoss = 0f;
            yield return Engage(projRt, projPos - right * 14f, 4f, (n, loss, _) => { pPort = n; pPortLoss = loss; });
            yield return Engage(projRt, projPos + right * 14f, 3f, (n, _, __) => pStar = n);
            Vector3 dropOrigin = rackRt != null ? rackRt.RackDropOrigin : ship.transform.position;
            int rAft = 0, rBeam = 0; float rLoss = 0f; List<Vector3> rAims = null;
            yield return Engage(rackRt, dropOrigin - fwd * 3f + right * 1f, 4f, (n, loss, aims) => { rAft = n; rLoss = loss; rAims = aims; });
            yield return Engage(rackRt, (rack != null && rack.Runtime != null ? rack.Runtime.transform.position : ship.transform.position) + right * 16f, 3f, (n, _, __) => rBeam = n);
            int behind = 0;
            if (rAims != null) foreach (var p in rAims) if (Vector3.Dot(p - dropOrigin, -fwd) >= -1.5f && Mathf.Abs(Vector3.Dot(p - dropOrigin, right)) <= 3.5f) behind++;
            _report.AppendLine($"- 발사대(좌현): 좌현 14m 잠수함 → {pPort}회 발사 · 피해 {pPortLoss:0} / 우현 14m → {pStar}회 · 투하대: 항적 3m 뒤 → {rAft}회 {rAims?.Count ?? 0}발(항적 위 {behind}발) · 피해 {rLoss:0}(기본 {aswDef.Stats.Damage}×{ModuleVariants.RackDamageMultiplier}) / 옆 16m → {rBeam}회");
            if (pPort < 1 || pPortLoss <= 0f) Fail("발사대가 트인 현측의 잠수함을 공격하지 않음");
            if (pStar > 0) Fail("발사대가 막힌 현측으로 던짐");
            if (rAft < 1 || (rAims?.Count ?? 0) != ModuleVariants.RackSalvo || behind != ModuleVariants.RackSalvo || rLoss <= 0f) Fail("투하대가 항적 위에 4발을 떨어뜨려 맞히지 않음");
            if (rBeam > 0) Fail("투하대가 옆 멀리 있는 잠수함에 던짐(사거리가 없어야 함)");

            // 정리
            for (int i = added.Count - 1; i >= 0; i--) CombatDevTools.RemoveModule(added[i]);
            added.Clear();
            ship.Systems?.Recalculate();
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }
    }
}
#endif
