using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>
    /// 지원 블록 개편(2026-11-22) 검사(-supportOnly):
    ///   카드 풀(사격통제 · 재장전 구획 빠짐, 새 3종 들어감) · 미사일 구축함 함교 흡수 수치 · 지원 블록 강화 단계(2단계) ·
    ///   전투단 통신 중계(보급선 + 고속정 6척, 보급 · 수리 · 스킬 단축 · 고속정 사격) · 포격 지원(경고 → 낙탄 · 피해) ·
    ///   정찰 드론(잠항 잠수함 드러냄 · 약점 표시) · 응급 차폐 장갑(큰 한 방 → 3초 −40% · 재사용) · 지원 판 표시.
    /// </summary>
    public partial class CombatVerificationRunner
    {
        public bool SupportOnly;

        private IEnumerator SupportCheck()
        {
            _report.AppendLine("\n## 지원 블록 개편");
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var grid = ship.Grid;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1.5f);
            Resources.LoadAll<ModuleDefinition>("Modules");
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            var added = new List<ModuleInstance>();

            // 1) 카드 풀 · 함교 · 강화 단계
            var pool = Game.Refit.RefitDraft.CollectInstallDefinitions(null);
            bool Has(string id) { foreach (var d in pool) if (d != null && d.Id == id) return true; return false; }
            string[] wantIn = { "mod_fleetrelay", "mod_firesupport", "mod_recondronebay", "mod_emergencyshield" };
            string[] wantOut = { "mod_firecontrolarray", "mod_missilelogistics" };
            var missing = new List<string>(); var leaked = new List<string>();
            foreach (var id in wantIn) if (!Has(id)) missing.Add(id);
            foreach (var id in wantOut) if (Has(id)) leaked.Add(id);
            _report.AppendLine($"- 카드 풀: 새 지원 블록 빠짐 [{string.Join(", ", missing)}] · 흡수한 블록 남음 [{string.Join(", ", leaked)}]");
            if (missing.Count > 0) Fail("새 지원 블록이 카드 풀에 없음");
            if (leaked.Count > 0) Fail("미사일 구축함이 흡수한 블록이 카드 풀에 남음");

            var mb = CombatDevTools.FindModule("mod_bridge_missile");
            if (mb == null) foreach (var d in Resources.FindObjectsOfTypeAll<ModuleDefinition>()) if (d.Id == "mod_bridge_missile") mb = d;
            if (mb != null)
            {
                var s = mb.Stats;
                _report.AppendLine($"- 미사일 구축함 함교: 유도 사거리 +{s.GuidedRangeBonus * 100f:0}% · 사격통제 추적 +{s.FireControlTracks} · 발사 간격 −{s.MissileReloadReduction * 100f:0}% · 전량 재장전 −{s.MissileResupplyReduction * 100f:0}%");
                if (!Mathf.Approximately(s.GuidedRangeBonus, 0.15f) || s.FireControlTracks != 3 || !Mathf.Approximately(s.MissileReloadReduction, 0.25f) || !Mathf.Approximately(s.MissileResupplyReduction, 0.2f))
                    Fail("미사일 구축함 함교 흡수 수치가 계획(+15% · +3 · −25% · −20%)과 다름");
                // 임시 집계기(같은 프레임에 지워 Start의 전체 집계는 돌지 않는다)
                var tmp = new GameObject("ShipSystems test");
                var sys = tmp.AddComponent<ShipSystems>();
                sys.RaiseMissileLogistics(s.MissileReloadReduction, s.MissileResupplyReduction);
                float rm = sys.MissileReloadMultiplier, sm = sys.MissileResupplyMultiplier;
                Object.DestroyImmediate(tmp);
                if (!Mathf.Approximately(rm, 0.75f) || !Mathf.Approximately(sm, 0.8f))
                    Fail($"발사 간격 · 전량 재장전 배율이 따로 적용되지 않음({rm:0.##} / {sm:0.##})");
            }
            else Fail("미사일 구축함 함교 정의를 찾지 못함");

            var lv = new List<string>();
            foreach (var id in new[] { "mod_repairbay", "mod_turbointake", "mod_fleetrelay", "mod_firesupport", "mod_recondronebay", "mod_emergencyshield", "mod_magazine" })
            {
                var d = CombatDevTools.FindModule(id);
                int max = d != null ? ModuleUpgrades.MaxLevel(d) + 1 : 0;
                lv.Add($"{id.Replace("mod_", "")} Lv{max}");
                if (max != 3) Fail($"지원 블록 {id}의 최고 단계가 Lv3이 아님(Lv{max})");
            }
            _report.AppendLine($"- 지원 블록 최고 단계: {string.Join(" · ", lv)}");

            // 2) 전투단 통신 중계 Lv3: 보급선 + 고속정 6척
            var relay = CombatDevTools.InstallModuleNear("mod_fleetrelay", new GridCoord(0, 0), 8);
            if (relay != null) added.Add(relay);
            var relayRt = relay?.Runtime as FleetRelayModule;
            if (relayRt == null) Fail("전투단 통신 중계를 설치하지 못함");
            else
            {
                relay.ApplyUpgrade(); relay.ApplyUpgrade();
                // 보급할 거리: 블록 하나 부수고 · 탄약 비우고 · 전속 재사용 걸기
                ModuleInstance broken = null; IAmmoUser drained = null;
                foreach (var m in grid.Modules)
                {
                    if (m == null || m == relay || m.Definition == null || m.Definition.Type == ModuleType.Bridge) continue;
                    if (drained == null && m.Runtime is IAmmoUser au && au.Ammo != null && !au.Ammo.Infinite) { drained = au; while (au.Ammo.Consume()) { } continue; }
                    if (broken == null && m.IsOperational) broken = m;
                }
                broken?.TakeDamage(broken.MaxHp + 1f);
                BridgeModule bridge = null;
                foreach (var m in grid.Modules) if (m?.Runtime is BridgeModule b) bridge = b;
                if (bridge != null) { Put(bridge, "_flankCooldown", 20f); }
                var foe = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_fastboat"), ship.transform.position + right * 30f + fwd * 6f, Quaternion.LookRotation(-right));
                if (foe != null) foe.DevFrozen = true;
                float hp0 = foe != null ? foe.CurrentHp : 0f;

                // 섬(2026-11-22): 기함 바로 뒤 · 지원 함정이 오는 길목에 섬을 하나 둔다 — 비켜 와야 한다
                var blocker = field != null ? field.PlaceArt(ship.transform.position - fwd * 30f - right * 7f, "ENV_PineIsland", 3) : null;
                yield return null;
                bool called = relayRt.DevCallNow();
                var fleet = relayRt.Fleet;
                int boats = fleet != null ? fleet.BoatCount : 0;
                float t0 = Time.time;
                while (Time.time - t0 < 24f && fleet != null && !fleet.Supplied) yield return null;
                float arrive = Time.time - t0;
                string panel0 = SupportTimersPanel.Text;
                yield return new WaitForSeconds(1.5f);
                yield return CloseShot("support_fleet", ship.transform, 34f);
                yield return Shot("support_fleet_hud");
                t0 = Time.time;
                while (Time.time - t0 < 6f && fleet != null && fleet.BoatShots == 0) yield return null;
                bool restored = broken == null || broken.IsOperational;
                bool refilled = drained == null || drained.Ammo.Current > 0;
                float flankLeft = bridge != null ? bridge.FlankCooldownLeft : -1f;
                _report.AppendLine($"- 통신 중계 Lv{relay.Level}: 호출 {Yes(called)} · 보급선 Lv{fleet?.Level ?? 0} + 고속정 {boats}척 · 도착 {arrive:0.0}초 · 보급 {Yes(fleet != null && fleet.Supplied)}");
                _report.AppendLine($"  - 보급: 탄약 {fleet?.LastRefilled ?? 0}기(비운 무장 다시 참 {Yes(refilled)}) · 수리 {fleet?.LastRepaired ?? 0}개(부순 {broken?.Definition.DisplayName ?? "-"} 되살아남 {Yes(restored)}) · 스킬 \"{fleet?.LastSkill}\"(전속 20초 → {flankLeft:0.0}초)");
                _report.AppendLine($"  - 섬 회피: 길목 섬 {(blocker != null ? $"반경 {blocker.Radius:0}m" : "없음")} · 비켜 간 프레임 {fleet?.AvoidFrames ?? 0} · 섬 안에 들어간 프레임 {fleet?.IslandOverlapFrames ?? 0}(기대 0)");
                if (blocker == null) Fail("섬 회피 시험용 섬을 놓지 못함(시험 무효)");
                else if (fleet != null && fleet.IslandOverlapFrames > 0) Fail("지원 함정이 섬을 뚫고 지나감");
                _report.AppendLine($"  - 고속정 사격 {fleet?.BoatShots ?? 0}발 · 피해 {fleet?.BoatDamage ?? 0f:0} (표적 체력 {hp0:0} → {(foe != null ? foe.CurrentHp : 0f):0}) · 지원 판 \"{panel0.Replace("\n", " / ")}\"");
                if (!called || fleet == null || boats != 6 || fleet.Level != 3) Fail("Lv3 통신 중계가 보급선 Lv3 + 고속정 6척을 부르지 않음");
                if (fleet != null && !fleet.Supplied) Fail("보급선이 기함 옆에 붙어 보급하지 않음");
                if (!restored || !refilled) Fail("보급선이 부서진 블록 수리 · 탄약 보급을 하지 않음");
                if (fleet != null && fleet.LastSkill == null) Fail("보급선이 재사용 중인 스킬을 줄이지 않음");
                if (bridge != null && fleet != null && fleet.LastSkill == "전속" && flankLeft > 10.5f) Fail("전속 재사용이 50% 줄지 않음");
                if (fleet != null && fleet.BoatShots == 0) Fail("고속정이 가까운 적을 쏘지 않음");
                if (!panel0.Contains("지원 함정")) Fail("지원 판에 지원 함정 줄이 없음");
                fleet?.Dismiss();
                yield return new WaitForSeconds(DepartWait());
                if (fleet != null) Fail("지원 함정이 복귀 뒤 사라지지 않음");
                CombatDevTools.ClearBattlefield();
                if (field != null) field.ClearAll();

                // 좁은 수로(2026-11-23 Codex 점검): 기함 한쪽 뱃전이 해안에 붙으면 보급선은 반대쪽으로 붙어 보급해야 한다.
                // 나타날 자리도 함정마다 실제 자리를 본다(섬에 걸려 생기면 안 됨)
                if (field != null)
                {
                    float hb = grid != null ? (grid.MaxHalfBeam + 0.5f) * grid.CellSize : 3f;
                    foreach (int blocked in new[] { -1, 1 })
                    {
                        field.ClearAll();
                        var probe = field.PlaceArt(ship.transform.position + right * blocked * 200f, "ENV_PineIsland", 4);
                        float rad = probe != null ? probe.Radius : 15f;
                        field.ClearAll();
                        var shore = field.PlaceArt(ship.transform.position + right * blocked * (hb + rad + 1.2f), "ENV_PineIsland", 4);
                        yield return null;
                        IAmmoUser dr = null;
                        foreach (var m in grid.Modules)
                            if (m?.Runtime is IAmmoUser au && au.Ammo != null && !au.Ammo.Infinite) { dr = au; while (au.Ammo.Consume()) { } break; }
                        bool shoreCalled = relayRt.DevCallNow();
                        var fl = relayRt.Fleet;
                        int sideAtStart = fl != null ? fl.SupplySide : 0;
                        float st = Time.time;
                        while (Time.time - st < 26f && fl != null && !fl.Supplied) yield return null;
                        string SideName(int v) => v < 0 ? "왼현" : v > 0 ? "오른현" : "-";
                        bool shoreSupplied = fl != null && fl.Supplied;
                        bool shoreRefilled = dr == null || dr.Ammo.Current > 0;
                        int shoreOverlap = fl != null ? fl.IslandOverlapFrames : -1;
                        int shoreAdjusted = fl != null ? fl.SpawnAdjusted : 0;
                        if (blocked == -1) yield return CloseShot("support_fleet_shore", ship.transform, 30f);
                        _report.AppendLine($"  - {SideName(blocked)}이 해안(섬 반경 {rad:0}m, 뱃전에서 1.2m)에 붙음: 보급선 {SideName(sideAtStart)}에 붙음 · 보급 {Yes(shoreSupplied)} · 탄약 다시 참 {Yes(shoreRefilled)} · 나타날 자리 옮김 {shoreAdjusted}척 · 섬 안에 들어간 프레임 {shoreOverlap}");
                        if (shore == null) Fail("해안 시험용 섬을 놓지 못함(시험 무효)");
                        else
                        {
                            if (sideAtStart != -blocked) Fail($"{SideName(blocked)}이 해안에 붙었는데 보급선이 그쪽으로 붙음");
                            if (!shoreCalled || !shoreSupplied || !shoreRefilled) Fail($"{SideName(blocked)}이 해안에 붙으면 보급선이 보급하지 못함");
                            if (shoreOverlap > 0) Fail("해안 옆 지원 함정이 섬에 박힘");
                        }
                        fl?.Dismiss();
                        yield return new WaitForSeconds(DepartWait());
                    }
                    field.ClearAll();
                    CombatDevTools.ClearBattlefield();
                }
            }

            // 3) 포격 지원 Lv1: 몰린 적 3척 → 경고 원 → 6발
            var fs = CombatDevTools.InstallModuleNear("mod_firesupport", new GridCoord(0, 0), 8);
            if (fs != null) added.Add(fs);
            var fsRt = fs?.Runtime as FireSupportModule;
            if (fsRt == null) Fail("포격 지원 요청을 설치하지 못함");
            else
            {
                var cluster = new List<EnemyController>();
                Vector3 c = ship.transform.position + right * 26f + fwd * 6f;   // 전투 화면 안(경고 원이 보이게)
                for (int i = 0; i < 3; i++)
                {
                    var e = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette") ?? CombatDevTools.FindEnemy("ene_missileboat"), c + right * (i - 1) * 3f, Quaternion.LookRotation(-fwd));
                    if (e != null) { e.DevFrozen = true; cluster.Add(e); }
                }
                yield return new WaitForSeconds(0.3f);
                float hp0 = 0f; foreach (var e in cluster) hp0 += e.CurrentHp;
                bool called = fsRt.DevCallNow();
                yield return new WaitForSeconds(1.0f);
                yield return Shot("support_fire_warning");
                yield return CloseShot("support_fire_warning_close", cluster.Count > 0 ? cluster[0].transform : ship.transform, 34f);
                yield return new WaitForSeconds(1.5f);
                yield return Shot("support_fire_barrage");
                float t0 = Time.time;
                while (Time.time - t0 < 4f && fsRt.ShellsLanded < FireSupportModule.ShellsFor(1)) yield return null;
                float hp1 = 0f; foreach (var e in cluster) hp1 += e != null && e.IsAlive ? e.CurrentHp : 0f;
                float centerErr = Vector3.Distance(Flat(fsRt.LastCenter), Flat(c));
                _report.AppendLine($"- 포격 지원 Lv1: 요청 {Yes(called)} · 낙탄 중심 오차 {centerErr:0.0}m · 포탄 {fsRt.ShellsLanded}발 · 피해 합 {fsRt.DamageDealt:0}(적 체력 {hp0:0} → {hp1:0})");
                if (!called || fsRt.ShellsLanded != FireSupportModule.ShellsFor(1)) Fail("포격 지원이 6발을 떨어뜨리지 않음");
                if (centerErr > 4f) Fail("포격 지원이 적이 몰린 곳을 노리지 않음");
                if (fsRt.DamageDealt <= 0f || hp1 >= hp0) Fail("포격 지원이 적에게 피해를 주지 않음");
                CombatDevTools.ClearBattlefield();
            }

            // 4) 정찰 드론 Lv2: 잠항 잠수함 드러냄 · 큰 적 약점 표시
            var rd = CombatDevTools.InstallModuleNear("mod_recondronebay", new GridCoord(0, 0), 8);
            if (rd != null) added.Add(rd);
            var rdRt = rd?.Runtime as ReconDroneBayModule;
            if (rdRt == null) Fail("정찰 드론 격납고를 설치하지 못함");
            else
            {
                rd.ApplyUpgrade();
                var sub = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_submarine"), ship.transform.position - right * 45f, Quaternion.LookRotation(fwd)) as SubmarineBase;
                if (sub != null) sub.DevFrozen = true;
                var big = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette") ?? CombatDevTools.FindEnemy("ene_missileboat"), ship.transform.position + right * 50f, Quaternion.LookRotation(-right));
                if (big != null) big.DevFrozen = true;
                yield return new WaitForSeconds(0.3f);
                bool hidden0 = sub != null && !sub.IsRevealed;
                bool launched = rdRt.DevLaunchNow();
                int drones = rdRt.ActiveDrones;
                float t0 = Time.time;
                while (Time.time - t0 < 8f && ((sub != null && !sub.IsRevealed) || (big != null && !big.IsMarked))) yield return null;
                yield return new WaitForSeconds(0.6f);
                yield return CloseShot("support_recon_drone", big != null ? big.transform : ship.transform, 30f);
                bool subSeen = sub != null && sub.IsRevealed, marked = big != null && big.IsMarked;
                _report.AppendLine($"- 정찰 드론 Lv{rd.Level}: 출격 {Yes(launched)} · 드론 {drones}기 · 잠항 잠수함(처음 숨음 {Yes(hidden0)}) 드러남 {Yes(subSeen)} · 큰 적 약점 표시 {Yes(marked)}({(big != null ? big.MarkedSecondsLeft : 0f):0.0}초)");
                if (!launched || drones != 2) Fail("Lv2 정찰 드론이 2기 뜨지 않음");
                if (!subSeen) Fail("정찰 드론이 잠항 잠수함을 드러내지 않음");
                if (!marked) Fail("정찰 드론이 큰 적에 약점 표시를 붙이지 않음");
                CombatDevTools.ClearBattlefield();
            }

            // 5) 응급 차폐 장갑: 작은 피해는 그대로, 큰 한 방 → 3초 −40% → 재사용
            var es = CombatDevTools.InstallModuleNear("mod_emergencyshield", new GridCoord(0, 0), 8);
            if (es != null) added.Add(es);
            var esRt = es?.Runtime as EmergencyShieldModule;
            if (esRt == null) Fail("응급 차폐 장갑을 설치하지 못함");
            else
            {
                var info = new DamageInfo(1f, ship.transform.position, Vector3.down, DamageSource.Missile, "test");
                float small = ship.HullMaxHp * 0.03f, bigHit = ship.HullMaxHp * 0.12f;
                float a = ship.FilterIncoming(info, small);
                bool upAfterSmall = esRt.ShieldUp;
                float b = ship.FilterIncoming(info, bigHit);
                bool upAfterBig = esRt.ShieldUp;
                float c2 = ship.FilterIncoming(info, small);
                // 연출(2026-11-23 사용자 시안 C · 장갑판 전개): 선체 둘레에 판이 솟고, 맞으면 그 판이 번쩍이고, 끝나면 사라진다
                float fxT0 = Time.time;
                var fx = ArmorPanelFx.Last;
                bool fxOnShip = fx != null && fx.transform.IsChildOf(ship.transform);
                // 차례로 솟나: 처음 판이 반쯤 솟은 프레임에 아직 안 솟은 판이 있어야 한다
                float riseEarly = 0f, restEarly = 1f;
                while (fx != null && Time.time - fxT0 < 0.6f)
                {
                    yield return null;
                    if (fx != null && fx.MaxRise >= 0.5f) { riseEarly = fx.MaxRise; restEarly = fx.MinRise; break; }
                }
                yield return CloseShot("shield_panels_rising", ship.transform, 26f);
                while (Time.time - fxT0 < 0.9f) yield return null;
                float riseFull = fx != null ? fx.MinRise : 0f;
                int hits0 = fx != null ? fx.Hits : 0;
                var sideHit = new DamageInfo(1f, ship.transform.position + ship.transform.right * 6f + Vector3.up, -ship.transform.right, DamageSource.Missile, "test");
                ship.FilterIncoming(sideHit, small);
                int hits1 = fx != null ? fx.Hits : 0;
                yield return null;
                yield return CloseShot("shield_panels_hit", ship.transform, 26f); yield return CloseShot("shield_panels_anchor", ship.transform, 13f);
                // 외곽이 실제 선체(선체 조각 상자, 배 기준)를 감싸나 — 격자 최대 칸이 아니라 지금 지은 선체
                var hullB = new Bounds(Vector3.zero, Vector3.zero); bool hullAny = false;
                var hb2 = ship.GetComponentInChildren<ShipHullBuilder>();
                if (hb2 != null && hb2.HullRoot != null)
                    foreach (var mf in hb2.HullRoot.GetComponentsInChildren<MeshFilter>())
                    {
                        if (mf.sharedMesh == null) continue;
                        var meshB = mf.sharedMesh.bounds;
                        var m = ship.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                        for (int k = 0; k < 8; k++)
                        {
                            var pt = m.MultiplyPoint3x4(meshB.center + Vector3.Scale(meshB.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                            if (!hullAny) { hullB = new Bounds(pt, Vector3.zero); hullAny = true; } else hullB.Encapsulate(pt);
                        }
                    }
                float hl = hullB.extents.z, hbm = hullB.extents.x;
                var ol = fx != null ? fx.OutlineLocal : default;
                bool wraps = fx != null && hullAny && ol.size.z >= hl * 2f && ol.size.x >= hbm * 2f * 0.9f;
                _report.AppendLine($"  - 연출 장갑판: 배에 붙음 {Yes(fxOnShip)} · 판 {(fx != null ? fx.PanelCount : 0)}장 · 선수 판 {riseEarly:0.00} 솟을 때 가장 늦은 판 {restEarly:0.00} · 0.9초 모두 솟음 {riseFull:0.00} · 외곽 {ol.size.x:0.0}×{ol.size.z:0.0}m(선체 {hbm * 2f:0.0}×{hl * 2f:0.0}) · 맞은 판 번쩍 {hits1 - hits0}회 · 닻 문양 {(fx != null ? fx.EmblemCount : 0)}장 · 기울기 {(fx != null ? fx.Tilt : 0f):0.00}m");
                if (fx == null || !fxOnShip) Fail("차폐 장갑이 켜졌는데 장갑판 연출이 배에 생기지 않음");
                else
                {
                    if (fx.PanelCount < 10) Fail($"장갑판이 너무 적음({fx.PanelCount}장)");
                    if (fx.EmblemCount < fx.PanelCount / 2) Fail($"닻 문양이 들어간 판이 너무 적음({fx.EmblemCount}/{fx.PanelCount})");
                    if (fx.Tilt > 0.001f) Fail("장갑판이 수직이 아니라 기울어 있음");
                    if (riseEarly < 0.5f || restEarly >= 0.5f) Fail("장갑판이 차례로 솟지 않고 한 번에 다 솟음");
                    if (riseFull < 0.95f) Fail("장갑판이 다 솟지 않음");
                    if (!wraps) Fail("장갑판 외곽이 선체를 감싸지 않음");
                    if (hits1 - hits0 != 1) Fail("켜진 동안 맞아도 장갑판이 번쩍이지 않음");
                }
                while (Time.time - fxT0 < EmergencyShieldModule.Duration + 0.3f) yield return null;
                bool fxGone = fx == null;
                _report.AppendLine($"  - 3초 뒤 장갑판 사라짐 {Yes(fxGone)}");
                if (!fxGone) Fail("차폐가 끝났는데 장갑판 연출이 남음");
                bool down = !esRt.ShieldUp;
                float d = ship.FilterIncoming(info, bigHit);
                float cd = esRt.CooldownLeft;
                _report.AppendLine($"- 응급 차폐 장갑 Lv{es.Level}: 작은 피해 {small:0} → {a:0}(켜짐 {Yes(upAfterSmall)}) · 큰 한 방 {bigHit:0} → {b:0}(켜짐 {Yes(upAfterBig)}) · 켜진 동안 작은 피해 → {c2:0} · 3초 뒤 꺼짐 {Yes(down)} · 재사용 중 큰 한 방 → {d:0}(남은 {cd:0}초)");
                if (upAfterSmall || !Mathf.Approximately(a, small)) Fail("작은 피해에 차폐 장갑이 켜짐");
                if (!upAfterBig || !Mathf.Approximately(b, bigHit * 0.6f) || !Mathf.Approximately(c2, small * 0.6f)) Fail("큰 한 방에 차폐 장갑이 켜져 −40%를 주지 않음");
                if (!down || !Mathf.Approximately(d, bigHit) || cd < 30f) Fail("차폐 장갑이 3초 뒤 꺼지고 재사용(40초)을 기다리지 않음");
            }

            // 6) 스킬 사용 횟수(2026-11-22 사용자: 횟수가 쌓여도 한 번 쓰면 처음부터 다시 참) — 기만체는 한 번에 하나씩, 응급 수리는 쓴 직후에도 남은 횟수가 보인다
            {
                var dm = CombatDevTools.InstallModuleNear("mod_decoy", new GridCoord(0, 0), 8);
                if (dm != null) added.Add(dm);
                var bay = CombatDevTools.InstallModuleNear("mod_repairbay", new GridCoord(0, 0), 8);
                if (bay != null) added.Add(bay);
                yield return null;
                var cdField = typeof(DecoyLauncherModule).GetField("_cooldown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                int ReadyDecoys()
                {
                    int n = 0;
                    foreach (var m in grid.Modules)
                        if (m != null && m.IsOperational && m.Runtime is IDecoySource d && (m.Runtime is not DecoyLauncherModule dl || dl.DecoyEnabled) && d.IsReady) n++;
                    return n;
                }
                foreach (var m in grid.Modules) if (m?.Runtime is DecoyLauncherModule dl) cdField?.SetValue(dl, 0f);
                int d0 = ReadyDecoys();
                GameEvents.RaiseDecoyRequested();
                int d1 = ReadyDecoys();
                GameEvents.RaiseDecoyRequested();
                int d2 = ReadyDecoys();
                _report.AppendLine($"- 기만체 사용 횟수: 준비 {d0} → Q 한 번 {d1} → 한 번 더 {d2}(하나씩 줄어야 함)");
                if (d0 < 2) Fail("기만체 횟수 시험 준비 실패(장비 2개 이상 필요)");
                else if (d1 != d0 - 1 || d2 != d0 - 2) Fail("기만체가 한 번 누를 때 하나씩 쓰이지 않음(쌓인 횟수가 한꺼번에 사라짐)");

                BridgeModule bridge = null;
                foreach (var m in grid.Modules) if (m?.Runtime is BridgeModule b && m.IsOperational) bridge = b;
                if (bridge != null)
                {
                    Put(bridge, "_charges", bridge.MaxCharges);
                    Put(bridge, "_burstGapLeft", 0f);
                    Put(bridge, "_rechargeLeft", 0f);
                    int max = bridge.MaxCharges, shown = -1; float shownNext = -1f;
                    System.Action<ActiveSkillId, int, int, float> onStatus = (id, r, t, n) => { if (id == ActiveSkillId.Repair) { shown = r; shownNext = n; } };
                    GameEvents.SkillStatusChanged += onStatus;
                    GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);
                    yield return new WaitForSecondsRealtime(0.25f);
                    int afterOne = bridge.Charges, shownAfter = shown; float nextAfter = shownNext;
                    yield return new WaitForSeconds(BridgeModule.MaxBurstGap + 0.1f);
                    GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);
                    yield return new WaitForSecondsRealtime(0.25f);
                    GameEvents.SkillStatusChanged -= onStatus;
                    int afterTwo = bridge.Charges;
                    _report.AppendLine($"- 응급 수리 횟수: {max}회 → 한 번 쓴 직후 {afterOne}회(HUD 표시 {shownAfter} · 다음 1회 진행 {nextAfter:0.00}) → {BridgeModule.MaxBurstGap:0.#}초 뒤 한 번 더 {afterTwo}회");
                    if (max < 2) Fail("응급 수리 횟수 시험 준비 실패(손상 통제반으로 2회 이상 필요)");
                    else
                    {
                        if (afterOne != max - 1 || shownAfter != max - 1) Fail("응급 수리를 쓴 직후 남은 횟수가 HUD에 그대로 보이지 않음");
                        if (afterTwo != max - 2) Fail("모아 둔 응급 수리를 짧은 간격 뒤 이어 쓸 수 없음");
                    }
                }
            }

            // 정리
            for (int i = added.Count - 1; i >= 0; i--) CombatDevTools.RemoveModule(added[i]);
            ship.Systems?.Recalculate();
            ship.DevRudderOverride = null;
            foreach (var w in off) if (w != null) w.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }

            static float DepartWait() => SupportCallFleet.DepartTime + 0.5f;
            static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        }
    }
}
