#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 시작 무장·손상통제 개편·함정 고유 스킬(2026-10-11).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 시작 무장: 시작 함선 넷 모두 함수·함미 끝에 무기(기관포·VLS), 함교 원격 기관총이 가까운 고속정을 쏜다.
        /// 2) 손상통제: 블록 없음 = 응급 수리 1회·자동 회복 50%에서 멈춤, 손상통제반 1개 = 2회·상한 67%·속도 1.5배,
        ///    응급 수리 쓰면 1회 줄고 보스 격침이면 다시 가득.
        /// 3) 고유 스킬 넷: 광역 탐색(표적 지시·잠수함 노출·피해 +25%), 돌격 기동(속력·피해 감소·충돌), 전탄 발사(남은 셀 전부),
        ///    항공대 호출(전투기 3 + 조기경보기 1, 기총 공격·레이더 고리·이탈). 장면마다 찍는다.
        /// </summary>
        private IEnumerator SignatureCheck()
        {
            _report.AppendLine("\n## 시작 무장 · 손상통제 · 함정 고유 스킬");
            var ship = GameManager.Instance.Player;
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            var boatDef = CombatDevTools.FindEnemy("ene_fastboat");
            Vector3 Fwd() { var f = ship.transform.forward; f.y = 0f; return f.normalized; }
            EnemyController Frozen(EnemyDefinition d, Vector3 p)
            {
                var e = EnemySpawner.Instance.SpawnAt(d, p, Quaternion.LookRotation(-Fwd()));
                if (e != null) { e.DevFrozen = true; e.transform.position = p; }
                return e;
            }

            // ---- 1) 시작 무장
            foreach (var concept in StartingShipCatalog.All)
            {
                if (concept == null || concept.StartLoadout == null) continue;
                int minX = int.MaxValue, maxX = int.MinValue;
                foreach (var e in concept.StartLoadout.Entries) { minX = Mathf.Min(minX, e.Origin.X); maxX = Mathf.Max(maxX, e.Origin.X + Mathf.Max(0, e.Module.Width - 1)); }
                string bow = "-", stern = "-";
                foreach (var e in concept.StartLoadout.Entries)
                {
                    bool weapon = e.Module.Type is ModuleType.Autocannon or ModuleType.Vls or ModuleType.NavalGun or ModuleType.Nobong;
                    if (!weapon) continue;
                    if (e.Origin.X == maxX) bow = e.Module.DisplayName;
                    if (e.Origin.X == minX) stern = e.Module.DisplayName;
                }
                _report.AppendLine($"- {concept.Title}: 함수 {bow} · 함미 {stern} (블록 {concept.StartLoadout.Entries.Count}개)");
                if (bow == "-" || stern == "-") Fail($"{concept.Title}: 함수·함미 무기가 하나씩이 아님");
            }

            int placed = ResetLoadout(M("mod_radar"));
            yield return new WaitForSeconds(0.5f);
            BridgeModule bridge = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is BridgeModule b) bridge = b;
            if (bridge == null) { Fail("함교 없음"); yield break; }
            var off = DisableWeapons(r => r is BridgeModule);
            {
                var boat = Frozen(boatDef, ship.transform.position + ship.transform.right * 12f);
                int s0 = bridge.MachineGun.Shots; float hp0 = boat != null ? boat.CurrentHp : 0f;
                yield return new WaitForSeconds(4f);
                float lost = boat != null ? hp0 - (boat.IsAlive ? boat.CurrentHp : 0f) : 0f;
                _report.AppendLine($"- 함교 원격 기관총: 있음 {Yes(bridge.MachineGun.HasGun)} · 12m 고속정에 4초 {bridge.MachineGun.Shots - s0}발 · 피해 {lost:0.#}");
                if (!bridge.MachineGun.HasGun || bridge.MachineGun.Shots - s0 < 5 || lost < 3f) Fail("함교 기관총이 가까운 적을 쏘지 않음");
                yield return CloseShot("signature_bridge_mg", bridge.MachineGun.Mount != null ? bridge.MachineGun.Mount : bridge.transform, 6f);
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- 2) 손상통제
            {
                _report.AppendLine($"- 손상통제반 0개: 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회 · 자동 회복 상한 {bridge.HullRepairCap * 100f:0}% · 속도 {bridge.HullRepairRate:0.##}/초");
                if (bridge.MaxCharges != 1 || bridge.Charges != 1 || !Mathf.Approximately(bridge.HullRepairCap, 0.5f)) Fail("블록 없을 때 응급 수리 1회·상한 50%가 아님");
                // 검사 함선은 선체가 매우 커(100000) 프레임당 회복량이 실수 정밀도에 묻힌다 — 잠깐 실제 크기(100)로 바꿔 잰다.
                // 상한 바로 아래(−3)에서 시작해 상한에 닿고 멈추는지, 상한 위(60%)에서는 그대로인지
                var maxProp = typeof(ShipController).GetProperty("HullMaxHp");
                var hpProp = typeof(ShipController).GetProperty("HullHp");
                float savedMax = ship.HullMaxHp;
                maxProp?.SetValue(ship, 100f);
                hpProp?.SetValue(ship, 100f);
                ship.RepairHull(ship.HullMaxHp);
                ship.ApplyHullDamage(ship.HullMaxHp * 0.5f + 3f);
                float start = ship.HullHp;
                yield return new WaitForSeconds(12f);
                float capHp = ship.HullMaxHp * 0.5f;
                float healed = ship.HullHp - start;
                ship.RepairHull(ship.HullMaxHp);
                ship.ApplyHullDamage(ship.HullMaxHp * 0.4f);   // 60%로
                float high0 = ship.HullHp;
                yield return new WaitForSeconds(4f);
                float highGain = ship.HullHp - high0;
                _report.AppendLine($"- 자동 회복: 상한 3 아래에서 12초 → +{healed:0.##}(상한까지 {capHp - start:0.##}, 최종 {start + healed:0.##}/{capHp:0.##}) · 60%에서 4초 → +{highGain:0.##}");
                if (healed < 2.9f || start + healed > capHp + 0.01f || highGain > 0.01f) Fail("자동 회복이 50%에서 멈추지 않음");
                maxProp?.SetValue(ship, savedMax);
                hpProp?.SetValue(ship, savedMax);

                var bay = CombatDevTools.FindModule("mod_repairbay");
                var factory = Object.FindFirstObjectByType<ModuleFactory>();
                ModuleInstance bayInst = null;
                for (int x = -4; x <= 4 && bayInst == null; x++)
                    for (int z = -2; z <= 2 && bayInst == null; z++)
                        if (ship.Grid.CanPlace(bay, new GridCoord(x, z), 0, out _)) bayInst = factory.Install(bay, new GridCoord(x, z), 0);
                yield return new WaitForSeconds(0.3f);
                _report.AppendLine($"- 손상통제반 1개: 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회 · 상한 {bridge.HullRepairCap * 100f:0}% · 속도 {bridge.HullRepairRate:0.##}/초");
                if (bayInst == null || bridge.MaxCharges != 2 || bridge.Charges != 2 || Mathf.Abs(bridge.HullRepairCap - 0.6667f) > 0.01f) Fail("손상통제반 1개일 때 2회·상한 67%가 아님");
                ship.ApplyHullDamage(200f);   // 응급 수리가 고칠 자리
                float before = ship.HullHp;
                GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);
                yield return new WaitForSeconds(0.2f);
                _report.AppendLine($"- 응급 수리 [R]: 선체 {before:0} → {ship.HullHp:0} · 남은 {bridge.Charges}/{bridge.MaxCharges}회");
                if (bridge.Charges != 1 || ship.HullHp <= before) Fail("응급 수리가 1회 줄며 선체를 고치지 않음");
                GameEvents.RaiseBossDefeated();
                yield return null;
                _report.AppendLine($"- 보스 격침 뒤 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회");
                if (bridge.Charges != bridge.MaxCharges) Fail("보스 격침 뒤 응급 수리가 가득 차지 않음");
                if (bayInst != null) CombatDevTools.RemoveModule(bayInst);
                ship.RepairHull(ship.HullMaxHp);
                yield return new WaitForSeconds(0.3f);
            }

            var skill = ship.GetComponent<SignatureSkill>();
            if (skill == null) { Fail("고유 스킬 컴포넌트 없음"); yield break; }

            // ---- 3a) 광역 탐색(범용 초계함)
            {
                skill.ForceKind(StartShipKind.Patrol);
                var boats = new List<EnemyController>();
                foreach (float a in new[] { -40f, 0f, 40f }) boats.Add(Frozen(boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 45f));
                CombatDevTools.SpawnSubmarines(1, 50f);
                yield return new WaitForSeconds(0.5f);
                foreach (var e in Object.FindObjectsByType<SubmarineBase>(FindObjectsSortMode.None)) if (e.IsAlive) { e.DevFrozen = true; }
                bool used = skill.TryActivate();
                yield return new WaitForSeconds(0.6f);
                var sweep = skill.Ability as RadarSweepAbility;
                var b0 = boats[0];
                float hp = b0 != null ? b0.CurrentHp : 0f;
                b0?.TakeDamage(new DamageInfo(4f, b0.transform.position, Vector3.down, DamageSource.Gun));
                float took = b0 != null ? hp - b0.CurrentHp : 0f;
                _report.AppendLine($"- 광역 탐색: 사용 {Yes(used)} · 표적 지시 {sweep?.LastMarked} · 잠수함 노출 {sweep?.LastRevealedSubs} · 지시된 고속정에 피해 4 → {took:0.##}");
                if (!used || sweep == null || sweep.LastMarked < 3 || sweep.LastRevealedSubs < 1 || Mathf.Abs(took - 5f) > 0.01f) Fail("광역 탐색이 표적 지시·잠수함 노출·피해 +25%를 하지 않음");
                yield return ScreenShot("signature_sweep");
                yield return new WaitForSeconds(0.8f);
                yield return ScreenShot("signature_sweep_markers");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- 3b) 돌격 기동(고속 강습함)
            {
                skill.ForceKind(StartShipKind.Assault);
                ship.SetEngineOrder(1f);
                yield return new WaitForSeconds(2f);
                var target = Frozen(boatDef, ship.transform.position + Fwd() * 30f);
                float v0 = ship.CurrentSpeed;
                bool used = skill.TryActivate();
                var run = skill.Ability as AssaultRunAbility;
                yield return new WaitForSeconds(0.5f);
                yield return ScreenShot("signature_assault");
                float t0 = Time.time;
                while (Time.time - t0 < 6f && run != null && run.Rams == 0)
                {
                    if (target != null && target.IsAlive) { var p = ship.transform.position + Fwd() * 6f; p.y = target.transform.position.y; target.transform.position = Vector3.MoveTowards(target.transform.position, p, 10f * Time.deltaTime); }
                    yield return null;
                }
                float scale = ship.IncomingDamageScale;
                _report.AppendLine($"- 돌격 기동: 사용 {Yes(used)} · 전속 {Yes(ship.IsFlanking)} · 받는 피해 ×{scale:0.##} · 속력 {v0:0.0} → {ship.CurrentSpeed:0.0} · 충돌 {run?.Rams}회");
                if (!used || !ship.IsFlanking || Mathf.Abs(scale - 0.6f) > 0.01f || run == null || run.Rams < 1) Fail("돌격 기동이 속력·피해 감소·충돌 공격을 하지 않음");
                yield return CloseShot("signature_assault_close", ship.transform, 12f);
                yield return new WaitForSeconds(AssaultRunAbility.Duration);
                _report.AppendLine($"- 돌격 기동 끝: 받는 피해 ×{ship.IncomingDamageScale:0.##}");
                if (!Mathf.Approximately(ship.IncomingDamageScale, 1f)) Fail("돌격 기동이 끝나도 피해 감소가 남음");
                ship.SetEngineOrder(0f);
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);
            }

            // ---- 3c) 전탄 발사(미사일 구축함)
            {
                foreach (var b in off) if (b != null) b.enabled = true;
                ResetLoadout(M("mod_radar"), M("mod_vls"));
                yield return new WaitForSeconds(0.5f);
                VlsModule vls = null;
                foreach (var m in ship.Grid.Modules) if (m?.Runtime is VlsModule v) vls = v;
                off = DisableWeapons(r => r is BridgeModule);
                if (vls == null) { Fail("VLS를 설치하지 못함"); }
                else
                {
                    vls.Ammo.Refill();
                    skill.ForceKind(StartShipKind.MissileDestroyer);
                    var boats = new List<EnemyController>();
                    foreach (float a in new[] { -50f, -15f, 15f, 50f }) boats.Add(Frozen(CombatDevTools.FindEnemy("ene_pcc_corvette") ?? boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 34f));
                    yield return new WaitForSeconds(0.5f);
                    vls.enabled = true;
                    int l0 = vls.LaunchCount, cap = vls.Ammo.Current;
                    bool used = skill.TryActivate();
                    yield return new WaitForSeconds(0.55f);
                    yield return CloseShot("signature_salvo_hatches", vls.transform, 7f);
                    yield return new WaitForSeconds(1.8f);
                    var salvo = skill.Ability as FullSalvoAbility;
                    _report.AppendLine($"- 전탄 발사: 사용 {Yes(used)} · 예약 {salvo?.LastScheduled}발(장전 {cap}셀) · 실제 발사 {vls.LaunchCount - l0} · 이후 재장전 {Yes(vls.Ammo.IsReloading)}");
                    if (!used || salvo == null || salvo.LastScheduled != cap || vls.LaunchCount - l0 != cap || !vls.Ammo.IsReloading) Fail("전탄 발사가 남은 셀을 모두 쏘고 재장전에 들어가지 않음");
                    yield return ScreenShot("signature_salvo");
                    yield return new WaitForSeconds(3f);
                    // 장전 중에는 못 쓴다
                    skill.ForceKind(StartShipKind.MissileDestroyer);
                    bool again = skill.TryActivate();
                    _report.AppendLine($"- 재장전 중 다시 사용: {Yes(again)}(아니오여야 함)");
                    if (again) Fail("재장전 중에 전탄 발사가 됨");
                }
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(1f);
            }

            // ---- 3d) 항공대 호출(지휘함)
            {
                skill.ForceKind(StartShipKind.Command);
                var boats = new List<EnemyController>();
                foreach (float a in new[] { -30f, 30f }) boats.Add(Frozen(boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 40f));
                var hp0 = new List<float>(); foreach (var b in boats) hp0.Add(b != null ? b.CurrentHp : 0f);
                bool used = skill.TryActivate();
                var wing = skill.Ability as AirWingAbility;
                yield return new WaitForSeconds(2.5f);
                yield return ScreenShot("signature_airwing_ingress");
                yield return new WaitForSeconds(6f);
                yield return ScreenShot("signature_airwing_attack");
                yield return new WaitForSeconds(6f);
                int fighters = 0, aew = 0, shots = 0, pings = 0;
                if (wing != null)
                    foreach (var a in wing.Flight)
                    {
                        if (a == null) continue;
                        if (a.IsAew) { aew++; pings += a.RadarPings; } else { fighters++; shots += a.ShotsFired; }
                    }
                float lost = 0f; for (int i = 0; i < boats.Count; i++) lost += boats[i] != null ? hp0[i] - (boats[i].IsAlive ? boats[i].CurrentHp : 0f) : hp0[i];
                _report.AppendLine($"- 항공대 호출: 사용 {Yes(used)} · 전투기 {fighters} · 조기경보기 {aew} · 기총 {shots}발 · 적 피해 {lost:0} · 레이더 고리 {pings}회");
                if (!used || fighters != 3 || aew != 1 || shots < 10 || lost <= 0f || pings < 2) Fail("항공대 호출이 편대로 와서 공격·조기경보를 하지 않음");
                yield return new WaitForSeconds(AirWingAbility.Duration - 14.5f + 8f);
                int left = 0; if (wing != null) foreach (var a in wing.Flight) if (a != null) left++;
                _report.AppendLine($"- 항공대 이탈 뒤 남은 항공기 {left}");
                if (left > 0) Fail("항공대가 시간이 지나도 이탈하지 않음");
                CombatDevTools.ClearBattlefield();
            }

            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
