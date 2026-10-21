#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using Game.WW2;

namespace Game.Dev
{
    /// <summary>
    /// 자동 전투 검증 — WWII 패치 C2·C3a(-ww2HeavyOnly, 2026-10-18).
    ///   0) 마운트 등급(출항 전): 구축함·순양함은 대구경 주포를 "마운트 등급 초과"로 거부, 전함은 놓을 자리가 있다
    ///   1) 항구 [WWII 전함] 버튼: 전함 함종 수치 · 대구경 주포 3기 · 탑재 예산 · 2×2 포탑 사격각(발자국 가운데)
    ///   2) 대구경 주포: 항공기는 쏘지 않는다 · 정횡 포격함을 자동으로 쏘아 맞힌다(연장 동시)
    ///   3) 주포 일제: 겨누는 동안 자동 사격 보류 · 떼면 준비된 주포가 한꺼번에(흩어짐 좁게) · 곧바로 다시 = 장전 중 거부 · 사거리 밖 거부
    /// </summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator Ww2HeavyCheck()
        {
            _report.AppendLine("\n## WWII 패치 C2·C3a — 마운트 등급 · 전함 · 대구경 주포");
            int exceptions = 0;
            void OnLog(string m, string st, LogType t) { if (t == LogType.Exception) { exceptions++; if (exceptions <= 5) _report.AppendLine($"  - 예외: {m}"); } }
            Application.logMessageReceived += OnLog;

            var def = Ruleset.Ww2Definition;
            if (def == null) { Fail("Resources/Rulesets/ww2_v1 없음"); yield break; }
            var problems = def.Validate();
            _report.AppendLine($"- 규칙 집합 점검: {(problems.Count == 0 ? "문제 없음" : string.Join(" / ", problems))}");
            if (problems.Count > 0) Fail("규칙 집합 정의에 문제가 있음");
            var heavyDef = def.Modules.FirstOrDefault(m => m != null && m.Type == ModuleType.Ww2HeavyGun);
            var battleshipHull = def.Hulls.FirstOrDefault(h => h != null && h.Id == "ww2_hull_battleship");
            if (heavyDef == null || battleshipHull == null) { Fail("대구경 주포·전함 데이터가 없음"); yield break; }
            _report.AppendLine($"- 대구경 주포: {heavyDef.Width}×{heavyDef.Height}칸 · {heavyDef.Tonnage:0}t · {MountClasses.Name(heavyDef.MountClass)} · 사거리 {heavyDef.Stats.MinRange:0}~{heavyDef.Stats.Range:0} m · 피해 {heavyDef.Stats.Damage:0} · 장전 {heavyDef.Stats.ReloadTime:0.0}초");

            // ---- 0) 마운트 등급(출항 전, 실제 버튼과 같이 WWII 규칙 집합으로)
            {
                var pShip = GameManager.Instance.Player;
                var pInit = Object.FindFirstObjectByType<ShipInitializer>();
                var pGrid = pShip.Grid;
                Ruleset.Enter(Ruleset.Ww2);
                foreach (var hull in def.Hulls)
                {
                    if (hull == null) continue;
                    if (!pInit.TrySelect(hull, out string why)) { Fail($"{hull.Id} 선택 실패: {why}"); continue; }
                    int spots = 0;
                    string refused = null;
                    for (int x = -pGrid.MaxHalfLength; x <= pGrid.MaxHalfLength; x++)
                        for (int z = -pGrid.MaxHalfBeam; z <= pGrid.MaxHalfBeam; z++)
                            for (int rot = 0; rot < 2; rot++)
                            {
                                if (pGrid.CanPlace(heavyDef, new GridCoord(x, z), rot, out string r)) spots++;
                                else if (refused == null && r != null && r.Contains("마운트")) refused = r;
                            }
                    var hp = hull.HullProfile;
                    _report.AppendLine($"- {hull.Id}: 마운트 한도 {MountClasses.Name(pGrid.MaxMountClass)} · 대구경 주포 놓을 자리 {spots} · 거부 \"{refused}\"");
                    bool heavyAllowed = hp != null && hp.MaxMountClass >= MountClass.Heavy;
                    if (heavyAllowed && spots == 0) Fail($"{hull.Id}: 대구경 주포를 허용하는데 놓을 자리가 없음");
                    if (!heavyAllowed && (spots > 0 || refused == null)) Fail($"{hull.Id}: 마운트 등급이 대구경 주포를 막지 않음");
                }
            }

            // ---- 1) 전함으로 출항
            var button = FindButton("WW2 hull battleship");
            _report.AppendLine($"- 항구 [WWII 전함] 버튼: {Yes(button != null)}");
            if (button == null) { Fail("항구에 WWII 전함 버튼이 없음"); yield break; }
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(1.5f);
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            var salvo = Ww2SalvoControl.Instance;
            if (gm.State != GameState.Playing || !Ruleset.IsWw2 || salvo == null) { Fail("WWII 전함으로 들어가지 못함"); yield break; }
            var guns = salvo.Guns.ToList();
            var bh = battleshipHull.HullProfile;
            _report.AppendLine($"- 전함: 최고속력 {ship.BaseMaxSpeed:0.0}m/s · 선회 {ship.BaseTurnRate:0}°/초 · 선체 {ship.HullMaxHp:0} · 장갑 {ship.HullArmor * 100f:0}% · 격자 ±{ship.Grid.MaxHalfLength}/±{ship.Grid.MaxHalfBeam} · 탑재 {ship.Grid.Tonnage:0.#}/{ship.Grid.TonnageBudget:0.#}t · 블록 {ship.Grid.Modules.Count} · 대구경 주포 {guns.Count}");
            if (ship.Hull != bh || guns.Count != 3) Fail("전함 함종 수치·대구경 주포 3기가 적용되지 않음");
            foreach (var g in guns)
                _report.AppendLine($"  - 주포 {g.Instance.Origin} 회전 {g.Instance.RotationSteps}: 수면 사격각 {g.Weapon.Arc.Width:0}° · 포신 {g.Barrels}");
            if (guns.Any(g => g.Weapon.Arc.Width < 60f)) Fail("대구경 주포 사격각이 너무 좁음(발자국 가운데 계산 확인)");
            yield return new WaitForSeconds(1f);
            yield return CloseShot("ww2_battleship_close", ship.transform, 26f);

            // 검사 환경: 웨이브·섬·경험치 끔, 함선 튼튼하게, 주포만
            director.enabled = false;
            EnemySpawner.Instance?.ClearAll();
            CombatDevTools.ClearBattlefield();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            foreach (var x in Object.FindObjectsByType<Game.Progression.ExperienceSystem>(FindObjectsSortMode.None)) x.enabled = false;
            typeof(ShipController).GetProperty(nameof(ShipController.HullMaxHp))?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            typeof(ShipController).GetProperty(nameof(ShipController.HullHp))?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            var healer = StartCoroutine(KeepModulesHealthy());
            var off = DisableWeapons(r => r is Ww2HeavyGunModule);
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(3f);
            int Shells() => guns.Sum(g => g.ShellsFired);
            var gunboat = def.Enemies.First(e => e.Id == "ww2_enemy_gunboat");
            var bomber = def.Enemies.First(e => e.Id == "ww2_enemy_torpedo_bomber");

            // ---- 2) 항공기는 쏘지 않는다
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            int s0 = Shells();
            var plane = EnemySpawner.Instance.SpawnAt(bomber, ship.transform.position + right * 30f + Vector3.up * 9f, Quaternion.LookRotation(fwd));
            if (plane != null) plane.DevFrozen = true;
            yield return new WaitForSeconds(4f);
            _report.AppendLine($"- 항공기만(정횡 30m 뇌격기): 주포 포탄 {Shells() - s0}");
            if (Shells() != s0) Fail("대구경 주포가 항공기를 쏨");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            // ---- 2-1) 정횡 포격함을 자동으로
            var target = EnemySpawner.Instance.SpawnAt(gunboat, ship.transform.position + right * 40f + fwd * 4f, Quaternion.LookRotation(fwd));
            if (target != null) target.DevFrozen = true;
            float hp0 = target != null ? target.CurrentHp : 0f;
            int v0 = guns.Sum(g => g.AutoVolleys);
            s0 = Shells();
            float t0 = Time.time;
            while (Time.time - t0 < 14f && target != null && target.IsAlive) yield return null;
            float dealt = target != null ? hp0 - Mathf.Max(0f, target.CurrentHp) : 0f;
            int volleys = guns.Sum(g => g.AutoVolleys) - v0;
            _report.AppendLine($"- 정횡 40m 포격함(자동): 일제 {volleys}회 · 포탄 {Shells() - s0} · 피해 {dealt:0}/{hp0:0} · 격침 {Yes(target == null || !target.IsAlive)}");
            if (volleys == 0 || dealt <= 0f) Fail("대구경 주포가 정횡 수상함을 자동으로 쏘아 맞히지 못함");
            if (volleys > 0 && Shells() - s0 < volleys * 2) Fail("대구경 주포 연장 포신이 함께 쏘지 않음");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            // ---- 3) 주포 일제: 겨누는 동안 보류 → 떼면 준비된 포만 함께
            var big = EnemySpawner.Instance.SpawnAt(gunboat, ship.transform.position - right * 42f, Quaternion.LookRotation(fwd));
            if (big != null) big.DevFrozen = true;
            Vector3 aimAt = big != null ? big.transform.position : ship.transform.position - right * 42f;
            s0 = Shells();
            salvo.DevHold(aimAt);
            float hold = Time.time;
            // 장전(5.5초)보다 오래 겨눈다 — 보류가 안 되면 자동으로 쐈을 것. 반대 현측으로 180° 도는 데 22°/초면 8초 넘게 걸린다
            while (Time.time - hold < 7f || (guns.Count(g => g.CanSalvoAt(aimAt, out _)) < guns.Count && Time.time - hold < 14f))
            { salvo.DevHold(aimAt); yield return null; }
            int heldShots = Shells() - s0;
            int ready = guns.Count(g => g.CanSalvoAt(aimAt, out _));
            string notReady = string.Join(" / ", guns.Where(g => !g.CanSalvoAt(aimAt, out _)).Select(g => { g.CanSalvoAt(aimAt, out string r); return $"{g.Instance.Origin}: {r}"; }));
            yield return ScreenShot("ww2_salvo_aim");
            _report.AppendLine($"- 좌현 42m 표적에 {Time.time - hold:0.0}초 겨눔: 그동안 쏜 포탄 {heldShots} · 일제 준비 {ready}/{guns.Count}" + (notReady.Length > 0 ? $" (못 쏨: {notReady})" : ""));
            if (heldShots > 0) Fail("주포 일제로 겨누는 동안 자동 사격이 멈추지 않음");
            if (ready == 0) Fail("14초를 겨눠도 일제 준비된 주포가 없음");
            float bigHp0 = big != null ? big.CurrentHp : 0f;
            int sv0 = salvo.Salvos;
            s0 = Shells();
            int fired = salvo.DevSalvo(aimAt);
            int shells = Shells() - s0;
            float spread = guns.Where(g => g.SalvoVolleys > 0).Select(g => g.LastDispersion).DefaultIfEmpty(-1f).Max();
            float autoSpread = Get<float>(guns[0], "dispersion");
            yield return new WaitForSeconds(3f);
            float bigDealt = big != null ? bigHp0 - Mathf.Max(0f, big.CurrentHp) : 0f;
            _report.AppendLine($"- 일제 발사: 주포 {fired}/{ready}문 · 포탄 {shells} · 흩어짐 ±{spread:0.0}°(자동 ±{autoSpread:0.0}°) · 피해 {bigDealt:0}/{bigHp0:0} · \"{salvo.LastMessage}\"");
            if (fired != ready || salvo.Salvos != sv0 + 1) Fail("주포 일제가 준비된 주포를 모두 함께 쏘지 않음");
            if (shells != fired * guns[0].Barrels) Fail("주포 일제가 포신 수만큼 쏘지 않음(무료 추가탄 없이)");
            if (spread < 0f || spread >= autoSpread) Fail("주포 일제의 흩어짐이 자동 사격보다 좁지 않음");
            if (bigDealt <= 0f) Fail("주포 일제가 표적을 맞히지 못함");

            int again = salvo.DevSalvo(aimAt);
            _report.AppendLine($"- 곧바로 다시: 주포 {again}문 · \"{salvo.LastMessage}\"");
            if (again != 0 || salvo.LastMessage == null || !salvo.LastMessage.Contains("장전")) Fail("장전 중인데 주포 일제가 나가거나 이유가 없음");
            yield return new WaitForSeconds(6f);
            int far = salvo.DevSalvo(ship.transform.position + fwd * 95f);
            _report.AppendLine($"- 사거리 밖(95m): 주포 {far}문 · \"{salvo.LastMessage}\"");
            if (far != 0 || salvo.LastMessage == null || !salvo.LastMessage.Contains("사거리")) Fail("사거리 밖 주포 일제가 거부되지 않거나 이유가 없음");

            // 정리
            CombatDevTools.ClearBattlefield();
            if (healer != null) StopCoroutine(healer);
            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
            Application.logMessageReceived -= OnLog;
            if (exceptions > 0) Fail($"예외 {exceptions}건");
        }
    }
}
#endif
