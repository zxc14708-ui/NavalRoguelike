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
    /// <summary>자동 전투 검증 — 호위 편대·진영(피아)·진형·자율 기동 검사.</summary>
    public partial class CombatVerificationRunner
    {
        // ------------------------------------------------------------ 편대(카드 · 생존성 · 자율 능력)

        /// <summary>
        /// 편대 개편: 출항 편성 없음 · 편대 배치 카드(첫 보장 레벨 3) · 역할 지정 카드(역할 넷 선택, 취소 가능) · 개량 카드(모델·축척·선체) ·
        /// 최대 4척 · 측후방 위치 · 편대 현황 패널 · 적 표적 선택(우선도 0.4)·공격자 2척 제한·미사일정은 기함만 · 적 탄 피해 ·
        /// 전투 불능(표적·판정 해제, 능력 정지, 이탈, 보상 없음, 적 재조준) · 복귀 · 정비 수리 ·
        /// 자율 능력(함포 · 방공 요격 · 전자전 교란 · 대잠 자동 타격 · 미사일 대함 타격) · 전술 리그.
        /// </summary>
        private IEnumerator EscortCheck()
        {
            _report.AppendLine("\n## 편대(카드 · 생존성 · 자율 능력)");
            var ship = GameManager.Instance.Player;
            var formation = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
            var feedback = Object.FindFirstObjectByType<Game.TaskForce.TaskForceWorldFeedback>();
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            var draft = Object.FindFirstObjectByType<Game.Refit.RefitDraft>(FindObjectsInactive.Include);
            if (formation == null || refit == null || draft == null) { Fail("편대·정비 컴포넌트 없음"); yield break; }
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = false;
            yield return new WaitForSeconds(0.5f);

            string ModelKey(Component c) { var key = c != null ? Get<string>(c, "_modelKey") : null; return string.IsNullOrEmpty(key) ? "(회색박스)" : key; }
            float Scale(Component c) => c != null ? c.transform.Find("Escort visual").localScale.x : 0f;
            string Name(Transform t) => t != null ? t.name : "-";
            bool Listed(Component c) { foreach (var t in TargetRegistry.HostileTo(CombatFaction.Hostile, TargetKind.Surface)) if (ReferenceEquals(t, c)) return true; return false; }

            // 1) 출항 편성 없음 · 처음엔 편대가 비어 있다 · 레벨 3부터 편대 배치 카드 보장
            bool noPreflight = GameObject.Find("Force package overlay") == null;
            bool emptyStart = formation.EscortCount == 0 && Game.UI.TaskForcePanelUI.VisiblePanel == null;
            int guaranteed = 0, earlyFleet = 0;
            for (int i = 0; i < 40; i++)
            {
                var c3 = draft.DrawCards(3, 3);
                if (c3.Count == 3 && c3[2].Kind == Game.Refit.RefitCardKind.FleetDeploy) guaranteed++;
                var c2 = draft.DrawCards(3, 2);
                foreach (var c in c2) if (c.Kind == Game.Refit.RefitCardKind.FleetDeploy) earlyFleet++;
            }
            _report.AppendLine($"- 시작: 출항 편성 화면 없음 {Yes(noPreflight)} · 편대 비어 있음 {Yes(emptyStart)} · 레벨 3 카드 40회 중 3번 자리 편대 배치 {guaranteed}회 · 레벨 2 편대 배치 {earlyFleet}회(무작위)");
            if (!noPreflight || !emptyStart) Fail("출항 편성이 남아 있거나 시작부터 편대가 있음");
            if (guaranteed != 40) Fail("편대가 비었을 때 레벨 3 편대 배치 카드가 보장되지 않음");

            // 2) 편대 배치 카드 → 역할 없는 고속정 합류
            GameManager.Instance.SetState(GameState.Refit);
            var filler1 = Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar"));
            var filler2 = Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Common);
            refit.Open(3, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetDeploy() }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_1_deploy_card");
            Call(refit, "ChooseCard", 2);
            // 편대 슬롯 화면(2026-10-03): 블록 배치 화면 대신 슬롯 1~4가 뜨고, 빈 슬롯을 고른다
            bool slotScreen = Get<bool>(refit, "_fleetPicking") && Get<bool>(refit, "_fleetDeployMode") && formation.EscortCount == 0;
            yield return null;
            yield return RefitShot(refit, "fleet_1b_slot_screen");
            Call(refit, "ChooseFleetSlot", 2);   // 3번 슬롯
            bool resultScreen = Get<bool>(refit, "_fleetDone");
            yield return null;
            yield return RefitShot(refit, "fleet_1c_slot_result");
            var pb = formation.GetEscort(0);
            string pbModel = ModelKey(pb);
            var pbInfo = formation.GetInfo(0);
            string deployText = Get<string>(refit, "_fleetResult");
            _report.AppendLine($"- 편대 배치 카드: 슬롯 화면 {Yes(slotScreen)} · 결과 화면 {Yes(resultScreen)} · {formation.EscortCount}척 · {pbInfo.Name}(슬롯 {pbInfo.RosterSlot + 1}) · 역할 {pbInfo.Role} · 모델 {pbModel} · 축척 {Scale(pb):0.00} · 결과 \"{deployText}\"");
            if (!slotScreen || !resultScreen) Fail("편대 배치 카드가 편대 슬롯 화면을 거치지 않음");
            if (formation.EscortCount != 1 || pbInfo.RosterSlot != 2 || pbInfo.Role != Game.TaskForce.EscortRole.None || !pbModel.StartsWith("ESC_PB") || !Mathf.Approximately(Scale(pb), 0.60f))
                Fail("편대 배치 카드로 고른 슬롯에 역할 없는 고속정이 합류하지 않음");
            refit.Open(4, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetDeploy() }, () => { });
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);   // 이미 찬 슬롯 — 무시돼야 한다
            bool occupiedRefused = formation.EscortCount == 1 && Get<bool>(refit, "_fleetPicking");
            Call(refit, "ChooseFleetSlot", 0);   // 1번 슬롯
            _report.AppendLine($"- 슬롯: 찬 슬롯 거부 {Yes(occupiedRefused)} · 1번 슬롯 합류 {Yes(formation.EscortCount == 2 && formation.GetInfo(1).RosterSlot == 0)} · 이름 {formation.GetInfo(0).Name}/{formation.GetInfo(1).Name}");
            if (!occupiedRefused || formation.GetInfo(1).RosterSlot != 0) Fail("편대 슬롯 선택이 찬 슬롯을 막지 않거나 고른 슬롯에 들어가지 않음");

            // 3) 편대 강화(역할 없음) → 역할 선택 패널 · 취소하면 카드로 · 고르면 그 역할의 기본형
            refit.Open(5, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade(1) }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_2_role_card");
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 0);   // 1번 슬롯 = 두 번째로 합류한 고속정
            bool picking = Get<bool>(refit, "_rolePicking");
            yield return null;
            yield return RefitShot(refit, "fleet_3_role_picker");
            Call(refit, "BackToCards");
            bool cancelled = Get<bool>(refit, "_choosing") && !Get<bool>(refit, "_rolePicking") && !Get<bool>(refit, "_fleetPicking") && formation.GetInfo(1).Role == Game.TaskForce.EscortRole.None;
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 0);
            Call(refit, "ChooseRole", 2);   // 전자전
            refit.Open(5, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade() }, () => { });
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);   // 3번 슬롯 = 처음 합류한 고속정
            Call(refit, "ChooseRole", 0);   // 방공
            var cap = formation.GetEscort(Game.TaskForce.EscortRole.AirDefense);
            var ew = formation.GetEscort(Game.TaskForce.EscortRole.ElectronicWarfare);
            _report.AppendLine($"- 역할 지정 카드: 선택 패널 {Yes(picking)} · 취소 → 카드로·역할 그대로 {Yes(cancelled)} · 0번 → {formation.GetInfo(0).Name}({ModelKey(cap)}) · 1번 → {formation.GetInfo(1).Name}({ModelKey(ew)})");
            if (!picking || !cancelled) Fail("역할 지정 카드의 선택·취소 흐름이 동작하지 않음");
            if (cap == null || ew == null || ModelKey(cap) != "ESC_CAP_T0" || ModelKey(ew) != "ESC_EW_T0") Fail("역할 지정 뒤 그 역할의 기본형 모델이 붙지 않음");

            // 4) 편대 강화(개량): 모델·축척·선체 성장
            refit.Open(6, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade(0) }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_4_upgrade_card");
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);
            var capInfo = formation.GetInfo(0);
            _report.AppendLine($"- 개량 카드: {capInfo.Name} 개량 {capInfo.Tier} · 모델 {ModelKey(cap)} · 축척 {Scale(cap):0.00} · 선체 최대 {Game.TaskForce.TaskForceEscortFormation.MaxHullFor(capInfo.Tier):0}");
            if (capInfo.Tier != 1 || ModelKey(cap) != "ESC_CAP_T1" || !Mathf.Approximately(Scale(cap), 0.76f)) Fail("개량 카드가 모델·축척을 올리지 않음");

            // 5) 강화 대상 고르기·최대 4척
            int third = formation.Deploy();
            int pickNew = formation.PickUpgradeTarget();
            formation.AssignRole(third, Game.TaskForce.EscortRole.SurfaceStrike);
            int fourth = formation.Deploy();
            formation.AssignRole(fourth, Game.TaskForce.EscortRole.AntiSubmarine);
            int fifth = formation.Deploy();
            int fullDeploy = 0;
            for (int i = 0; i < 200; i++) foreach (var c in draft.DrawCards(3, 8)) if (c.Kind == Game.Refit.RefitCardKind.FleetDeploy) fullDeploy++;
            _report.AppendLine($"- 강화 대상: 역할 없는 3번 고속정을 먼저 {Yes(pickNew == third)} · 최대 {Game.TaskForce.TaskForceEscortFormation.MaxEscorts}척에서 더 배치 {(fifth < 0 ? "거부" : "허용")} · 가득 찬 뒤 편대 배치 카드 {fullDeploy}장");
            if (pickNew != third) Fail("편대 강화 카드가 역할 없는 고속정을 먼저 고르지 않음");
            if (fifth >= 0 || fullDeploy > 0) Fail("편대가 4척을 넘거나 가득 찼는데 배치 카드가 나옴");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);
            yield return new WaitForSeconds(2.5f);

            // 6) 진형 위치(기본 함대원형진 — 슬롯마다 고정) · 편대 현황 패널 · 진형 선택판
            var stk = formation.GetEscort(Game.TaskForce.EscortRole.SurfaceStrike);
            var asw = formation.GetEscort(Game.TaskForce.EscortRole.AntiSubmarine);
            var sbPos = new StringBuilder();
            bool posOk = true;
            var fext = formation.FlagshipExtent;
            for (int i = 0; i < formation.EscortCount; i++)
            {
                var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                int rs = formation.GetInfo(i).RosterSlot;
                var want = Game.TaskForce.FleetFormations.Slot(formation.Formation, rs, formation.EscortCount, fext.x, fext.y, fext.z);
                sbPos.Append($"{rs + 1}번({lp.x:0},{lp.z:0}/{want.x:0},{want.y:0}) ");
                if (Vector2.Distance(new Vector2(lp.x, lp.z), want) > 6f) posOk = false;
            }
            var panel = Game.UI.TaskForcePanelUI.VisiblePanel;
            yield return ScreenShot("fleet_5_formation_panel");
            _report.AppendLine($"- 위치({Game.TaskForce.FleetFormations.Name(formation.Formation)}, 슬롯(지금/목표)): {sbPos}· 편대 패널 {(panel != null ? "보임" : "없음")} · 진형 선택판 {(Game.UI.FormationSelectorUI.IsVisible ? "보임" : "없음")}");
            if (!posOk) Fail("호위함이 슬롯에 고정된 진형 자리에 있지 않음");
            if (panel == null) Fail("편대 현황 패널이 보이지 않음");
            if (!Game.UI.FormationSelectorUI.IsVisible) Fail("진형 선택판이 보이지 않음");
            yield return CloseShot("fleet_cap_t1", cap.transform, 12f);

            // 7) 표적 선택 · 공격자 2척 제한 · 미사일정은 기함만(자율 무장은 8)·10)에서 따로 본다)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            var fastDef = CombatDevTools.FindEnemy("ene_fastboat");
            var spawner = EnemySpawner.Instance;
            Vector3 Out(Component c) { Vector3 o = c.transform.position - ship.transform.position; o.y = 0f; return o.normalized; }
            Vector3 At(Component c, float d, float side = 0f) { Vector3 o = Out(c); Vector3 pp = c.transform.position + o * d + Vector3.Cross(Vector3.up, o) * side; pp.y = 0f; return pp; }
            var ea = spawner.SpawnAt(fastDef, At(cap, 6f), Quaternion.LookRotation(-Out(cap)));
            var eb = spawner.SpawnAt(fastDef, ship.transform.position + ship.transform.forward * 30f, Quaternion.LookRotation(-ship.transform.forward));
            // 첫 표적 선택(0.4~1.4초 뒤) 결과를 본다 — 끝 시점만 보면 편대가 나아가 호위함이 멀어진 뒤 기함으로 바꾼 것과 섞인다
            float t0 = Time.time;
            bool aOnCap = false, bOnShip = false;
            while (Time.time - t0 < 4f && !(aOnCap && bOnShip))
            {
                yield return null;
                aOnCap |= ea != null && ea.EngagedTarget == cap.transform;
                bOnShip |= eb != null && eb.EngagedTarget == ship.transform;
            }
            _report.AppendLine($"- 표적 선택: 호위함 옆 6m 적 → {(aOnCap ? Name(cap.transform) : Name(ea != null ? ea.EngagedTarget : null))} · 기함 앞 30m 적 → {(bOnShip ? Name(ship.transform) : Name(eb != null ? eb.EngagedTarget : null))}");
            if (!aOnCap) Fail("호위함 바로 옆 적이 호위함을 노리지 않음");
            if (!bOnShip) Fail("기함 가까운 적이 기함을 노리지 않음");
            CombatDevTools.ClearBattlefield();
            yield return null;
            // 적을 멈춰 두고(움직이면 기함 쪽으로 다가가며 표적을 바꾼다) 표적 선택만 직접 돌린다
            var pack = new List<EnemyController>();
            for (int i = 0; i < 4; i++) pack.Add(spawner.SpawnAt(fastDef, At(ew, 4f, (i - 1.5f) * 2.5f), Quaternion.LookRotation(-Out(ew))));
            var mboat = spawner.SpawnAt(CombatDevTools.FindEnemy("ene_missileboat"), At(cap, 4f), Quaternion.LookRotation(-Out(cap)));
            var thinkers = new List<EnemyController>(pack) { mboat };
            foreach (var e in thinkers) if (e != null) e.DevFrozen = true;
            for (int round = 0; round < 3; round++)
                foreach (var e in thinkers)
                {
                    if (e == null) continue;
                    // 기반 클래스(EnemyController)의 private 멤버라 그 타입으로 찾는다
                    typeof(EnemyController).GetField("_retargetAt", Inst)?.SetValue(e, 0f);
                    typeof(EnemyController).GetMethod("UpdateTarget", Inst)?.Invoke(e, null);
                }
            yield return null;
            int onEw = 0;
            foreach (var e in pack) if (e != null && e.EngagedTarget == ew.transform) onEw++;
            _report.AppendLine($"- 공격자 제한: EW 호위함 옆 고속정 4척 중 호위함을 노리는 적 {onEw}척(최대 {Game.TaskForce.TaskForceEscortFormation.EscortMaxAttackers}) · 호위함 옆 미사일정 → {Name(mboat != null ? mboat.EngagedTarget : null)}");
            if (onEw != Game.TaskForce.TaskForceEscortFormation.EscortMaxAttackers) Fail("한 호위함을 노리는 적 수가 제한대로가 아님");
            if (mboat == null || mboat.EngagedTarget != ship.transform) Fail("미사일정이 호위함을 노림(기함만 노려야 함)");

            // 8) 적 포탄 · 전투 불능(이탈·표적 해제·능력 정지·보상 없음·적 재조준) · 복귀 · 정비 수리
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.3f);
            formation.RepairAll();
            int capIdx = 0;
            float max1 = Game.TaskForce.TaskForceEscortFormation.MaxHullFor(1);
            var pcc = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 80f, 180f);
            foreach (var e in pcc) e.DevFrozen = true;
            GameObject shell = pcc.Count > 0 ? PrivateField<GameObject>(pcc[0], "projectilePrefab") : null;
            float hp0 = formation.GetHull01(capIdx) * max1;
            if (shell != null && PoolManager.Instance != null)
            {
                Vector3 from = cap.transform.position + Out(cap) * 8f + Vector3.up * 1.2f;
                Vector3 dir = (cap.transform.position + Vector3.up * 0.6f - from).normalized;
                var go = PoolManager.Instance.Spawn(shell, from, Quaternion.LookRotation(dir));
                var pr = go != null ? go.GetComponent<Projectile>() : null;
                if (pr != null) pr.Launch(dir, 80f, 5f, DamageSource.Gun);
                yield return new WaitForSeconds(0.6f);
            }
            float hp1 = formation.GetHull01(capIdx) * max1;
            _report.AppendLine($"- 적 포탄 1발: 선체 {hp0:0.#} → {hp1:0.#}");
            if (shell == null || hp0 - hp1 < 4.9f) Fail("적 포탄이 호위함에 맞지 않음");

            CombatDevTools.ClearBattlefield();
            var ec = spawner.SpawnAt(fastDef, At(cap, 4f), Quaternion.LookRotation(-Out(cap)));
            void Think(EnemyController e)
            {
                if (e == null) return;
                typeof(EnemyController).GetField("_retargetAt", Inst)?.SetValue(e, 0f);
                typeof(EnemyController).GetMethod("UpdateTarget", Inst)?.Invoke(e, null);
            }
            if (ec != null) ec.DevFrozen = true;   // 움직이면 기함 쪽으로 다가가며 표적을 바꾼다 — 멈춰 두고 판단만 돌린다
            Think(ec);
            bool ecOnEscort = ec != null && ec.EngagedTarget == cap.transform;
            int kills0 = GameManager.Instance.TotalKills;
            float aftBefore = ship.transform.InverseTransformPoint(cap.transform.position).z;
            var box = cap.GetComponent<BoxCollider>();
            ((IDamageable)cap).TakeDamage(new DamageInfo(999f, cap.transform.position, Vector3.down, DamageSource.Gun));
            yield return null;
            Think(ec);   // 노리던 표적이 사라지면 곧바로 다시 고른다
            yield return null;
            bool retargeted = ec != null && ec.EngagedTarget == ship.transform;
            var capDefense = cap.GetComponent<Game.TaskForce.EscortDefense>();
            bool disabledOk = formation.IsEscortDisabled(capIdx) && !((ITargetable)cap).IsAlive && !Listed(cap) && !box.enabled;
            float recoverLeft = formation.GetRecoverRemaining(capIdx);
            yield return new WaitForSeconds(3f);
            float aftWithdraw = ship.transform.InverseTransformPoint(cap.transform.position).z;
            _report.AppendLine($"- 전투 불능: 표적·판정 해제 {Yes(disabledOk)} · 격침 수 {kills0}→{GameManager.Instance.TotalKills} · 적 재조준 {Name(ec != null ? ec.EngagedTarget : null)} · 기함 뒤 {-aftBefore:0}m → {-aftWithdraw:0}m 이탈 · 복귀까지 {recoverLeft:0}초");
            if (!ecOnEscort) Fail("전투 불능 시험의 적이 호위함을 노리지 않음(시험 무효)");
            if (!disabledOk || formation.DisabledCount != 1) Fail("전투 불능 호위함이 표적·판정에서 빠지지 않음");
            if (GameManager.Instance.TotalKills != kills0) Fail("아군 호위함 손실이 격침 수를 올림");
            if (!retargeted) Fail("호위함을 노리던 적이 기함으로 표적을 바꾸지 않음");
            if (aftWithdraw > aftBefore - 5f) Fail("전투 불능 호위함이 후방으로 이탈하지 않음");
            CombatDevTools.ClearBattlefield();
            yield return ScreenShot("fleet_6_disabled");
            Put(cap, "_recoverAt", Time.time);
            yield return null;
            yield return null;
            bool back = !formation.IsEscortDisabled(capIdx) && ((ITargetable)cap).IsAlive && Listed(cap) && box.enabled;
            float backHull = formation.GetHull01(capIdx);
            ((IDamageable)ew).TakeDamage(new DamageInfo(999f, ew.transform.position, Vector3.down, DamageSource.Gun));
            yield return null;
            GameManager.Instance.SetState(GameState.Refit);
            yield return null;
            float capHull = formation.GetHull01(capIdx), ewHull = formation.GetHull01(1);
            bool ewBack = !formation.IsEscortDisabled(1);
            GameManager.Instance.SetState(GameState.Playing);
            _report.AppendLine($"- 복귀: {Yes(back)} · 선체 {backHull * 100f:0}% · 정비 진입: CAP {capHull * 100f:0}% · 전투 불능 EW 복귀 {Yes(ewBack)} {ewHull * 100f:0}%");
            if (!back || Mathf.Abs(backHull - Game.TaskForce.TaskForceEscortFormation.RecoverHull) > 0.01f) Fail("전투 불능 호위함이 선체 절반으로 복귀하지 않음");
            if (capHull < 1f || ewHull < 1f || !ewBack) Fail("정비 때 호위함이 수리·복귀되지 않음");

            // 9) 자율 능력: 함포 · 방공 요격 · 전자전 교란
            CombatDevTools.ClearBattlefield();
            var capDef = cap.GetComponent<Game.TaskForce.EscortDefense>();
            var ewDef = ew.GetComponent<Game.TaskForce.EscortDefense>();
            // 방공·전자전만 켠다(대잠·미사일 호위함이 시험용 적을 먼저 쏘지 않게 — 10)에서 따로 본다)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) { d.enabled = d == capDef || d == ewDef; d.ResetCooldowns(); }
            yield return new WaitForSeconds(1f);
            int killsG = GameManager.Instance.TotalKills, gun0 = ewDef.GunShots;
            var target = spawner.SpawnAt(fastDef, At(ew, 12f), Quaternion.LookRotation(-Out(ew)));
            if (target != null) target.DevFrozen = true;
            t0 = Time.time;
            while (Time.time - t0 < 6f && target != null && target.IsAlive) yield return null;
            float gunTime = Time.time - t0;
            bool gunKill = target != null && !target.IsAlive && GameManager.Instance.TotalKills > killsG;
            var mdef2 = CombatDevTools.FindEnemy("ene_missileboat");
            int sam0 = capDef.SamKills, jam0 = ewDef.Jams;
            Missile samMissile = null, jamMissile = null;
            if (mdef2 != null && mdef2.MissilePrefab != null && PoolManager.Instance != null)
            {
                Vector3 from = cap.transform.position + Out(cap) * 26f + Vector3.up * 3f;
                var go = PoolManager.Instance.Spawn(mdef2.MissilePrefab, from, Quaternion.LookRotation(ship.transform.position - from));
                samMissile = go != null ? go.GetComponent<Missile>() : null;
                if (samMissile != null) samMissile.Launch(ship.transform, 26f);
                Vector3 from2 = ew.transform.position + Out(ew) * 26f + Vector3.up * 3f;
                var go2 = PoolManager.Instance.Spawn(mdef2.MissilePrefab, from2, Quaternion.LookRotation(ship.transform.position - from2));
                jamMissile = go2 != null ? go2.GetComponent<Missile>() : null;
                if (jamMissile != null) jamMissile.Launch(ship.transform, 26f);
            }
            t0 = Time.time;
            while (Time.time - t0 < 8f && ((samMissile != null && samMissile.IsAlive) || (jamMissile != null && jamMissile.IsAlive))) yield return null;
            yield return null;
            _report.AppendLine($"- 함포·방공·교란: EW 함포 → 12m 고속정 격침 {Yes(gunKill)} ({gunTime:0.0}초, {ewDef.GunShots - gun0}발) · 방공 요격 {capDef.SamKills - sam0}발 · 전자전 교란 {ewDef.Jams - jam0}회");
            if (!gunKill) Fail("호위함 함포가 가까운 적을 격침하지 못함");
            if (capDef.SamKills - sam0 < 1) Fail("방공 호위함이 기함을 노리는 미사일을 요격하지 않음");
            if (ewDef.Jams - jam0 < 1) Fail("전자전 호위함이 가까운 미사일을 교란하지 않음");
            CombatDevTools.ClearBattlefield();

            // 10) 자율 능력: 대잠 자동 타격(잠항 중) · 미사일 대함 타격(가치 높은 표적 우선) · 전술 리그
            int rigs0 = feedback != null ? feedback.SpawnedRigCount : 0;
            var aswDef = asw.GetComponent<Game.TaskForce.EscortDefense>();
            var stkDef = stk.GetComponent<Game.TaskForce.EscortDefense>();
            // 모두 끄고, 대잠·미사일 호위함은 함포를 막은 채 역할 능력만 켠다(적을 띄운 뒤)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            aswDef.ResetCooldowns();
            stkDef.ResetCooldowns();
            Put(aswDef, "_gunAt", Time.time + 999f);
            Put(stkDef, "_gunAt", Time.time + 999f);
            int asw0 = aswDef.AswStrikes, stk0 = stkDef.SurfaceStrikes;
            var subs = new List<EnemyController> { spawner.SpawnAt(CombatDevTools.FindEnemy("ene_submarine"), At(asw, 14f), Quaternion.LookRotation(-Out(asw))) };
            foreach (var e in subs) if (e != null) e.DevFrozen = true;
            var sub = subs[0] as SubmarineBase;
            float subHp0 = sub != null ? sub.CurrentHp : 0f;
            bool hiddenAtStart = sub != null && !sub.IsRevealed;
            var cheap = spawner.SpawnAt(fastDef, At(stk, 22f, 6f), Quaternion.LookRotation(-Out(stk)));
            var valuable = spawner.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette"), At(stk, 40f, -6f), Quaternion.LookRotation(-Out(stk)));
            if (cheap != null) cheap.DevFrozen = true;
            if (valuable != null) valuable.DevFrozen = true;
            float pccHp0 = valuable != null ? valuable.CurrentHp : 0f, cheapHp0 = cheap != null ? cheap.CurrentHp : 0f;
            aswDef.enabled = true;
            stkDef.enabled = true;
            t0 = Time.time;
            while (Time.time - t0 < 3f && (aswDef.AswStrikes == asw0 || stkDef.SurfaceStrikes == stk0)) yield return null;
            yield return new WaitForSeconds(0.4f);
            float subLoss = sub != null ? subHp0 - sub.CurrentHp : 0f;
            float pccLoss = valuable != null ? pccHp0 - valuable.CurrentHp : 0f;
            float cheapLoss = cheap != null ? cheapHp0 - cheap.CurrentHp : 0f;
            bool contact = sub != null && sub.IsContactConfirmed;
            int rigs1 = feedback != null ? feedback.SpawnedRigCount : 0;
            yield return ScreenShot("fleet_7_abilities");
            _report.AppendLine($"- 대잠 자동 타격: {aswDef.AswStrikes - asw0}회 · 잠항 중 잠수함 {Yes(hiddenAtStart)} · 피해 {subLoss:0}(기대 {Game.TaskForce.EscortDefense.AswDamage(0):0}) · 접촉 확정 {Yes(contact)} / 대함 타격: {stkDef.SurfaceStrikes - stk0}회 · 초계함(엘리트) 피해 {pccLoss:0}(기대 {Game.TaskForce.EscortDefense.StrikeDamage(0):0}) · 가까운 고속정 피해 {cheapLoss:0} · 전술 리그 {rigs1 - rigs0}개");
            if (aswDef.AswStrikes - asw0 < 1 || subLoss < Game.TaskForce.EscortDefense.AswDamage(0) - 0.5f || !contact) Fail("대잠 호위함이 잠항 잠수함을 자동 타격하지 않음");
            if (stkDef.SurfaceStrikes - stk0 < 1 || pccLoss < Game.TaskForce.EscortDefense.StrikeDamage(0) - 0.5f || cheapLoss > 0f) Fail("미사일 호위함이 가치 높은 표적을 먼저 타격하지 않음");
            if (rigs1 - rigs0 < 2) Fail("자율 능력 발동 때 Codex 전술 리그가 펼쳐지지 않음");

            // 11) 포탑 회전 · 레이더 회전 · 발사음(Redesign 모델의 가동부)
            CombatDevTools.ClearBattlefield();
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            yield return new WaitForSeconds(0.3f);
            string RigText(Component c)
            {
                var rg = c.GetComponentInChildren<Game.TaskForce.EscortTurrets>();
                if (rg == null) return $"{c.name}: 가동부 없음";
                return $"{ModelKey(c)} 함포 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Gun)}·함대공 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Sam)}·대함 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Strike)}·기만 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Decoy)}·레이더 {rg.SpinnerCount}";
            }
            _report.AppendLine($"- 가동부: {RigText(cap)} / {RigText(ew)} / {RigText(asw)} / {RigText(stk)}");
            var ewRig = ew.GetComponentInChildren<Game.TaskForce.EscortTurrets>();
            if (ewRig == null || ewRig.CountOf(Game.TaskForce.EscortTurrets.MountKind.Gun) == 0) Fail("호위함 모델에서 함포 포탑을 찾지 못함");
            if (cap.GetComponentInChildren<Game.TaskForce.EscortTurrets>() is { } capRig && capRig.CountOf(Game.TaskForce.EscortTurrets.MountKind.Sam) == 0) Fail("방공 호위함(T1)에서 함대공 발사기를 찾지 못함");
            if (ewRig != null)
            {
                // 레이더 회전
                var spinner = ewRig.FirstSpinner;
                var q0 = spinner != null ? spinner.rotation : Quaternion.identity;
                yield return new WaitForSeconds(0.5f);
                float spun = spinner != null ? Quaternion.Angle(q0, spinner.rotation) : 0f;
                // 함포 선회: 호위함 옆(현측 90°) 14m에 고정 표적
                Vector3 side = ew.transform.right; side.y = 0f; side.Normalize();
                var aimTarget = spawner.SpawnAt(fastDef, ew.transform.position + side * 14f, Quaternion.LookRotation(-side));
                if (aimTarget != null) aimTarget.DevFrozen = true;
                ewDef.ResetCooldowns();
                int shots0 = ewDef.GunShots;
                ewDef.enabled = true;
                t0 = Time.time;
                while (Time.time - t0 < 1.5f && ewDef.GunShots == shots0) yield return null;
                yield return new WaitForSeconds(0.3f);
                float turned = ewRig.MaxTurn(Game.TaskForce.EscortTurrets.MountKind.Gun);
                float aimErr = aimTarget != null ? ewRig.BestAimError(Game.TaskForce.EscortTurrets.MountKind.Gun, aimTarget.transform.position) : 999f;
                yield return CloseShot("fleet_8_turret_aim", ew.transform, 9f);
                ewDef.enabled = false;
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2.5f);   // 쏠 일이 없으면 제자리로
                float rest = ewRig.MaxTurn(Game.TaskForce.EscortTurrets.MountKind.Gun);
                _report.AppendLine($"- 포탑: 현측 표적에 함포 {turned:0}° 선회 · 포신 오차 {aimErr:0}° · 첫 발 {ewDef.GunShots - shots0}발 / 표적이 사라지면 제자리까지 {rest:0}° · 레이더 0.5초에 {spun:0}° 회전");
                if (turned < 45f) Fail("호위함 함포가 표적 쪽으로 돌지 않음");
                if (aimErr > 15f) Fail("호위함 함포가 표적을 정확히 향하지 않음");
                if (ewDef.GunShots - shots0 < 1) Fail("포탑을 돌린 뒤 함포를 쏘지 않음");
                if (rest > 10f) Fail("표적이 사라진 뒤 포탑이 제자리로 돌아오지 않음");
                if (spinner != null && spun < 10f) Fail("호위함 레이더가 돌지 않음");
            }
            // 발사음: 이 검사에서 쓴 호위함 효과음이 실제로 재생되었는가(AudioManager 최근 재생 기록)
            var audio = AudioManager.Instance;
            var played = audio != null ? Get<Dictionary<Game.Data.SfxId, float>>(audio, "_lastPlayed") : null;
            var sfxIds = new[] { Game.Data.SfxId.EscortGunShot, Game.Data.SfxId.EscortMissileLaunch, Game.Data.SfxId.EscortTorpedoLaunch, Game.Data.SfxId.EscortJam };
            var heard = new List<string>();
            var silent = new List<string>();
            foreach (var id in sfxIds) (played != null && played.ContainsKey(id) ? heard : silent).Add(id.ToString());
            _report.AppendLine($"- 발사음: 재생됨 [{string.Join(", ", heard)}]" + (silent.Count > 0 ? $" · 안 남 [{string.Join(", ", silent)}]" : ""));
            if (audio == null) _report.AppendLine("  - 참고: AudioManager 없음(소리 검사 생략)");
            else if (silent.Count > 0) Fail("호위함 발사음이 재생되지 않음");

            // 정리: 편대를 비우고(다른 검사에 끼어들지 않게) 무장을 되돌린다
            formation.ClearAll();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        // ------------------------------------------------------------ 진영(아군/적)

        /// <summary>검증용 가짜 유닛: 진영·종류를 정할 수 있는 표적 겸 피해 대상(받은 피해를 센다).</summary>
        private sealed class FactionProbe : MonoBehaviour, ITargetable, IDamageable
        {
            public CombatFaction ProbeFaction;
            public float Received;
            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public bool IsAlive => isActiveAndEnabled;
            public bool IsRevealed => true;
            public CombatFaction Faction => ProbeFaction;
            public void TakeDamage(in DamageInfo info) => Received += info.Amount;

            public static FactionProbe Create(string name, CombatFaction faction, Vector3 at, int layer)
            {
                var go = new GameObject(name);
                go.layer = layer;
                go.transform.position = at;
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(2.2f, 2f, 5f);
                box.center = new Vector3(0f, 0.5f, 0f);
                var p = go.AddComponent<FactionProbe>();
                p.ProbeFaction = faction;
                TargetRegistry.Register(p);
                return p;
            }

            private void OnDestroy() => TargetRegistry.Unregister(this);
        }

        /// <summary>
        /// 진영 구분(작업 A): 표적 등록소 진영별 조회 · 아군(가짜 호위함)은 플레이어 무기·센서·경고에서 빠짐 ·
        /// 적은 기함과 아군을 적대 대상으로 봄 · 아군 탄은 아군 몸체에 피해 없음(레이어가 맞아도) · 보상은 적대 진영만 ·
        /// 기함이 자기 레이더·무기의 표적이 되지 않음.
        /// </summary>
        private IEnumerator FactionCheck()
        {
            _report.AppendLine("\n## 진영(아군/적) 구분");
            var ship = GameManager.Instance.Player;
            var targeting = ship.GetComponentInChildren<TargetingSystem>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            // 앞 검사가 무장을 켠 채 끝나도 시험용 적을 먼저 쏘지 않게(등록소·탐지 목록만 본다). 2)에서 따로 켠다.
            DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            yield return new WaitForSeconds(0.5f);

            // 1) 등록소
            var enemy = CombatDevTools.SpawnRing("ene_fastboat", 1, 18f, 90f);
            foreach (var e in enemy) e.DevFrozen = true;
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var friendly = FactionProbe.Create("Friendly escort probe", CombatFaction.Player, ship.transform.position + fwd * 12f, Factions.PlayerShipLayer);
            yield return new WaitForSeconds(0.8f);   // 탐지 갱신

            bool Contains(IReadOnlyList<ITargetable> list, object o) { foreach (var t in list) if (ReferenceEquals(t, o)) return true; return false; }
            var hostileToPlayer = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            var hostileToEnemy = TargetRegistry.HostileTo(CombatFaction.Hostile, TargetKind.Surface);
            bool enemyListed = enemy.Count > 0 && Contains(hostileToPlayer, enemy[0]);
            bool shipExcluded = !Contains(hostileToPlayer, ship), probeExcluded = !Contains(hostileToPlayer, friendly);
            bool enemySeesShip = Contains(hostileToEnemy, ship), enemySeesProbe = Contains(hostileToEnemy, friendly);
            bool detectedProbe = targeting != null && Contains(targeting.DetectedSurface, friendly);
            bool detectedShip = targeting != null && Contains(targeting.DetectedSurface, ship);
            bool detectedEnemy = targeting != null && enemy.Count > 0 && Contains(targeting.DetectedSurface, enemy[0]);
            string factionDiag = enemy.Count > 0 ? $"적 생존 {enemy[0].IsAlive} · 체력 {enemy[0].CurrentHp:0.#} · 활성 {enemy[0].isActiveAndEnabled} · 적 목록 {hostileToPlayer.Count}개" : "적 스폰 실패";
            _report.AppendLine($"- 등록소({factionDiag}): 플레이어의 적 목록에 적 {Yes(enemyListed)} · 기함 {Yes(!shipExcluded)} · 아군 {Yes(!probeExcluded)} / 적의 적 목록에 기함 {Yes(enemySeesShip)} · 아군 {Yes(enemySeesProbe)}");
            _report.AppendLine($"- 함선 탐지(레이더): 적 {Yes(detectedEnemy)} · 아군 {Yes(detectedProbe)} · 자함 {Yes(detectedShip)}");
            if (!enemyListed || !shipExcluded || !probeExcluded) Fail("플레이어의 적 목록에 아군이 섞이거나 적이 빠짐");
            if (!enemySeesShip || !enemySeesProbe) Fail("적이 기함·아군을 적대 대상으로 보지 못함");
            if (detectedProbe || detectedShip || !detectedEnemy) Fail("탐지 목록에 아군·자함이 들어가거나 적이 빠짐");

            // 2) 무기를 켜 두어도 아군은 맞지 않는다(적은 없애고 아군만 둔다)
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            // 1)에서 적에게 쏜 탄이 아직 날고 있을 수 있다 — 다 사라진 뒤부터 잰다
            float wait0 = Time.time;
            while (ActiveCount("PRJ_") > 0 && Time.time - wait0 < 5f) yield return null;
            friendly.Received = 0f;
            float t0 = Time.time; int maxShells = 0;
            while (Time.time - t0 < 4f) { maxShells = Mathf.Max(maxShells, ActiveCount("PRJ_")); yield return null; }
            _report.AppendLine($"- 무장 켬 · 아군만 12m 앞: 날아간 포탄 최대 {maxShells}발 · 아군이 받은 피해 {friendly.Received:0.#}");
            if (friendly.Received > 0f || maxShells > 0) Fail("아군을 향해 사격함");

            // 3) 아군 탄은 아군 몸체에 피해를 주지 않는다 — 레이어를 적(Enemy)으로 바꿔 물리 충돌은 일어나게 하고 진영으로만 막히는지 본다
            var ac = CombatDevTools.FindModule("mod_autocannon");
            GameObject shell = null;
            foreach (var r in Object.FindObjectsByType<Game.Modules.Runtime.AutocannonModule>(FindObjectsSortMode.None))
            { shell = PrivateField<GameObject>(r, "projectilePrefab"); if (shell != null) break; }
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = false;
            var right = Vector3.Cross(Vector3.up, fwd);
            var ally = FactionProbe.Create("Ally on enemy layer", CombatFaction.Player, ship.transform.position + right * 20f + Vector3.up * 0.5f, Factions.EnemyLayer);
            var foe = FactionProbe.Create("Foe on enemy layer", CombatFaction.Hostile, ship.transform.position - right * 20f + Vector3.up * 0.5f, Factions.EnemyLayer);
            TargetRegistry.Unregister(ally); TargetRegistry.Unregister(foe);   // 무기가 스스로 노리지 않게(탄만 쏜다)
            Physics.SyncTransforms();
            CombatFaction owner = CombatFaction.Neutral;
            if (shell != null && PoolManager.Instance != null)
            {
                foreach (var (target, name) in new[] { (ally, "아군"), (foe, "적") })
                {
                    Vector3 from = ship.transform.position + Vector3.up * 1.5f + (target.transform.position - ship.transform.position).normalized * 3f;
                    Vector3 dir = (target.transform.position + Vector3.up * 0.5f - from).normalized;
                    var go = PoolManager.Instance.Spawn(shell, from, Quaternion.LookRotation(dir));
                    var p = go != null ? go.GetComponent<Projectile>() : null;
                    if (p == null) continue;
                    owner = p.Owner;
                    p.Launch(dir, 80f, 5f, DamageSource.Gun);
                }
                yield return new WaitForSeconds(0.8f);
            }
            _report.AppendLine($"- 아군 탄(소유 {owner})을 Enemy 레이어에 둔 아군·적에 쏨: 아군 피해 {ally.Received:0.#} · 적 피해 {foe.Received:0.#}");
            if (shell == null) Fail("기관포 탄 프리팹을 찾지 못함");
            if (owner != CombatFaction.Player) Fail("기관포 탄의 소유 진영이 Player가 아님");
            if (ally.Received > 0f) Fail("아군 탄이 아군 몸체에 피해를 줌");
            if (foe.Received <= 0f) Fail("대조군(적) 몸체가 피해를 받지 않음(시험 무효)");

            // 4) 보상은 적대 진영만
            int kills0 = GameManager.Instance.TotalKills;
            var victim = CombatDevTools.SpawnRing("ene_fastboat", 1, 25f, 200f);
            yield return null;
            foreach (var v in victim) v.TakeDamage(new DamageInfo(999f, v.transform.position, Vector3.down, DamageSource.Gun));
            int kills1 = GameManager.Instance.TotalKills;
            Object.Destroy(friendly.gameObject);
            yield return null;
            int kills2 = GameManager.Instance.TotalKills;
            bool rules = Factions.GrantsReward(foe) && !Factions.GrantsReward(ally) && !Factions.GrantsReward(ship);
            _report.AppendLine($"- 보상: 적 격침 → 격침 수 {kills0} → {kills1} · 아군 소실 → {kills1} → {kills2} · 규칙(적만 보상) {Yes(rules)}");
            if (kills1 != kills0 + 1 || kills2 != kills1 || !rules) Fail("보상이 적대 진영에만 주어지지 않음");

            Object.Destroy(ally.gameObject);
            Object.Destroy(foe.gameObject);
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        private IEnumerator FormationCheck()
        {
            _report.AppendLine("\n## 편대 진형·조함");
            var ship = GameManager.Instance.Player;
            var formation = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
            if (formation == null) { Fail("편대 없음"); yield break; }
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            formation.ClearAll();
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            var roles = new[] { Game.TaskForce.EscortRole.AirDefense, Game.TaskForce.EscortRole.AntiSubmarine, Game.TaskForce.EscortRole.ElectronicWarfare, Game.TaskForce.EscortRole.SurfaceStrike };
            foreach (var r in roles) { int i = formation.Deploy(); formation.AssignRole(i, r); }
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0.5f);
            yield return new WaitForSeconds(4f);

            T Prop<T>(Component c, string name) => c != null && c.GetType().GetProperty(name) is { } pi ? (T)pi.GetValue(c) : default;
            var ext = formation.FlagshipExtent;
            _report.AppendLine($"- 키: 진형 전환 [{GameSettings.BindingLabel(NavalControl.Formation)}] · 기함 크기 선수 {ext.x:0.#}m · 선미 {ext.y:0.#}m · 반폭 {ext.z:0.#}m");
            if (GameSettings.Binding(NavalControl.Formation) == UnityEngine.InputSystem.Key.None) Fail("진형 전환 키가 없음");

            // 1) 진형마다: 직진 중 슬롯 수렴 · 기함과 겹치지 않음(자율은 따로 — 고정 자리가 없다)
            var cycled = new List<string>();
            foreach (var f in Game.TaskForce.FleetFormations.All)
            {
                formation.SetFormation(f);
                cycled.Add(Game.TaskForce.FleetFormations.Name(formation.Formation));
                if (f == Game.TaskForce.FleetFormation.Autonomous) { yield return AutonomousFormationCheck(formation, ship); continue; }
                for (int k = 0; k < 18; k++)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (f != Game.TaskForce.FleetFormation.Column || k % 4 != 3) continue;   // 종렬진만 2초마다 궤적 기록
                    var tr = new StringBuilder($"  - t{(k + 1) * 0.5f:0.0}: ");
                    for (int i = 0; i < formation.EscortCount; i++)
                    {
                        var esc = formation.GetEscort(i);
                        var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                        var sw = ship.transform.InverseTransformPoint(formation.SlotWorld(i));
                        tr.Append($"#{i}s{formation.SlotOf(i)} ({lp.x:0.0},{lp.z:0.0})->({sw.x:0},{sw.z:0}) v{Prop<float>(esc, "Speed"):0.0} h{Mathf.DeltaAngle(ship.transform.eulerAngles.y, Prop<float>(esc, "Heading")):0} | ");
                    }
                    _report.AppendLine(tr.ToString());
                }
                float worst = 0f, clearance = float.MaxValue;
                var pts = new StringBuilder();
                for (int i = 0; i < formation.EscortCount; i++)
                {
                    var esc = formation.GetEscort(i);
                    var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                    Vector2 want = f == Game.TaskForce.FleetFormation.Column
                        ? new Vector2(0f, -Game.TaskForce.FleetFormations.TrailDistance(formation.SlotOf(i), ext.y))
                        : Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                    worst = Mathf.Max(worst, Vector2.Distance(new Vector2(lp.x, lp.z), want));
                    float along = Mathf.Clamp(lp.z, ext.y, ext.x);
                    clearance = Mathf.Min(clearance, new Vector2(lp.x, lp.z - along).magnitude - ext.z);
                    pts.Append($"({lp.x:0},{lp.z:0}) ");
                    if (f == Game.TaskForce.FleetFormation.Column) { var sw = ship.transform.InverseTransformPoint(formation.SlotWorld(i)); pts.Append($"[슬롯{formation.SlotOf(i)} 목표({sw.x:0},{sw.z:0}) 속력 {Prop<float>(esc, "Speed"):0.0} 침로차 {Mathf.DeltaAngle(ship.transform.eulerAngles.y, Prop<float>(esc, "Heading")):0}°] "); }
                }
                _report.AppendLine($"- {Game.TaskForce.FleetFormations.Name(f)}: {pts}· 슬롯 오차 최대 {worst:0.0}m · 기함 현측과 최소 간격 {clearance:0.0}m");
                if (worst > 3.5f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 직진 중 호위함이 슬롯에 들어가지 않음({worst:0.0}m)");
                if (clearance < 2f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 기함과 겹침");
                yield return Shot($"formation_{f.ToString().ToLowerInvariant()}");
            }
            if (formation.Formation != Game.TaskForce.FleetFormation.Autonomous) Fail("진형 전환이 마지막 진형에 오지 않음");
            formation.CycleFormation();
            bool wrapped = formation.Formation == Game.TaskForce.FleetFormation.Circular;
            // 진형 선택판 클릭(도식 칸)으로도 바뀐다
            Game.UI.FormationSelectorUI.Click(Game.TaskForce.FleetFormation.Column);
            bool clicked = formation.Formation == Game.TaskForce.FleetFormation.Column;
            _report.AppendLine($"- 진형 선택판: 보임 {Yes(Game.UI.FormationSelectorUI.IsVisible)} · 단종진 칸 클릭 → {Game.TaskForce.FleetFormations.Name(formation.Formation)}");
            if (!Game.UI.FormationSelectorUI.IsVisible || !clicked) Fail("진형 선택판이 보이지 않거나 클릭으로 진형이 바뀌지 않음");
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            _report.AppendLine($"- 진형 순환: {string.Join(" → ", cycled)} → {Game.TaskForce.FleetFormations.Name(formation.Formation)}");
            if (!wrapped) Fail("진형 전환이 처음 진형으로 돌아오지 않음");

            // 2) 급선회(전속·타 최대 6초): 함대원형진(바깥 함이 크게 도는 진형)과 단종진
            foreach (var f in new[] { Game.TaskForce.FleetFormation.Circular, Game.TaskForce.FleetFormation.Column })
            {
                formation.SetFormation(f);
                ship.DevRudderOverride = 0f;
                ship.SetEngineOrder(1f);
                yield return new WaitForSeconds(8f);
                var prev = new Vector3[formation.EscortCount];
                for (int i = 0; i < prev.Length; i++) prev[i] = formation.GetEscort(i).transform.position;
                float maxYawRate = 0f, maxSlip = 0f, maxGap = 0f, oldLateral = 0f;
                ship.DevRudderOverride = 1f;
                float t0 = Time.time, lastYaw = ship.transform.eulerAngles.y;
                while (Time.time - t0 < 6f)
                {
                    yield return null;
                    float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                    float shipYawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, ship.transform.eulerAngles.y)) / dt;
                    lastYaw = ship.transform.eulerAngles.y;
                    for (int i = 0; i < prev.Length; i++)
                    {
                        var esc = formation.GetEscort(i);
                        Vector3 v = (esc.transform.position - prev[i]) / dt; v.y = 0f;
                        prev[i] = esc.transform.position;
                        maxSlip = Mathf.Max(maxSlip, Mathf.Abs(Vector3.Dot(v, esc.transform.right)));
                        maxYawRate = Mathf.Max(maxYawRate, Mathf.Abs(Prop<float>(esc, "YawRate")));
                        Vector2 want = f == Game.TaskForce.FleetFormation.Column
                            ? new Vector2(0f, -Game.TaskForce.FleetFormations.TrailDistance(formation.SlotOf(i), ext.y))
                            : Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                        // 예전 방식: 슬롯을 기함 침로로 즉시 돌림 → 슬롯 횡속도 = 기함 선회율 × 슬롯 거리
                        oldLateral = Mathf.Max(oldLateral, shipYawRate * Mathf.Deg2Rad * want.magnitude);
                    }
                }
                // 선회 끝난 직후 진형과의 거리(종렬진은 항적 위)
                for (int i = 0; i < formation.EscortCount; i++)
                {
                    var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                    if (f != Game.TaskForce.FleetFormation.Column)
                    {
                        var want = Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                        maxGap = Mathf.Max(maxGap, Vector2.Distance(new Vector2(lp.x, lp.z), want));
                    }
                }
                ship.DevRudderOverride = 0f;
                yield return Shot($"formation_turn_{f.ToString().ToLowerInvariant()}");
                _report.AppendLine($"- 급선회 6초({Game.TaskForce.FleetFormations.Name(f)}): 호위함 선회율 최대 {maxYawRate:0}°/초 · 옆미끄러짐 최대 {maxSlip:0.0}m/s · " +
                                   (f != Game.TaskForce.FleetFormation.Column ? $"선회 중 기함 기준 슬롯과 거리 최대 {maxGap:0}m · " : "") +
                                   $"(예전 방식이면 바깥 슬롯 횡속도 {oldLateral:0}m/s)");
                if (maxSlip > 1.5f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 옆으로 미끄러짐(게걸음)");
                if (maxYawRate > 90f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 선회율 한도보다 빨리 꺾임");
            }
            // 종렬진: 선회 뒤 직진하면 항적을 따라 한 줄로
            ship.SetEngineOrder(0.75f);
            yield return new WaitForSeconds(7f);
            float colWorst = 0f;
            for (int i = 0; i < formation.EscortCount; i++)
            {
                var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                colWorst = Mathf.Max(colWorst, Mathf.Abs(lp.x));
            }
            _report.AppendLine($"- 종렬진 선회 뒤 7초 직진: 기함 항적에서 옆으로 최대 {colWorst:0.0}m");
            if (colWorst > 3f) Fail("종렬진이 선회 뒤 기함 항적을 따라 한 줄로 서지 않음");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            formation.ClearAll();
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        /// <summary>
        /// 자율 진형(2026-10-03): 위협이 없으면 기함 둘레를 스스로 돈다(자리가 바뀐다) · 기함에서 AutoLeash 안 · 화면 안 · 기함과 겹치지 않음,
        /// 적 고속정이 나타나면 수상 교전 쪽으로 나간다.
        /// </summary>
        private IEnumerator AutonomousFormationCheck(Game.TaskForce.TaskForceEscortFormation formation, Game.Ship.ShipController ship)
        {
            var ext = formation.FlagshipExtent;
            var cam = Camera.main;
            int n = formation.EscortCount;
            var start = new Vector3[n];
            for (int i = 0; i < n; i++) start[i] = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
            float maxDist = 0f, clearance = float.MaxValue, moved = 0f;
            int offScreen = 0;
            for (int k = 0; k < 16; k++)
            {
                yield return new WaitForSeconds(0.5f);
                for (int i = 0; i < n; i++)
                {
                    var esc = formation.GetEscort(i);
                    var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                    maxDist = Mathf.Max(maxDist, new Vector2(lp.x, lp.z - (ext.x + ext.y) * 0.5f).magnitude);
                    float along = Mathf.Clamp(lp.z, ext.y, ext.x);
                    clearance = Mathf.Min(clearance, new Vector2(lp.x, lp.z - along).magnitude - ext.z);
                    if (k >= 6 && cam != null)
                    {
                        var vp = cam.WorldToViewportPoint(esc.transform.position);
                        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) offScreen++;
                    }
                    if (k == 15) moved = Mathf.Max(moved, Vector3.Distance(lp, start[i]));
                }
            }
            var tasks = new StringBuilder();
            for (int i = 0; i < n; i++) tasks.Append($"{formation.GetInfo(i).RosterSlot + 1}번 {formation.AutonomousTask(i)} ");
            _report.AppendLine($"- 자율(위협 없음 8초): 할 일 {tasks}· 기함 중심에서 최대 {maxDist:0.0}m(한도 {Game.TaskForce.TaskForceEscortFormation.AutoLeash:0}) · " +
                               $"기함 현측과 최소 간격 {clearance:0.0}m · 화면 밖 {offScreen}회 · 스스로 자리 옮김 최대 {moved:0.0}m");
            if (maxDist > Game.TaskForce.TaskForceEscortFormation.AutoLeash + 8f) Fail("자율: 호위함이 기함에서 너무 멀리 감");
            if (clearance < 2f) Fail("자율: 호위함이 기함과 겹침");
            if (offScreen > 0) Fail("자율: 호위함이 화면 밖으로 나감");
            if (moved < 3f) Fail("자율: 위협이 없을 때 호위함이 스스로 움직이지 않음(초계)");
            yield return Shot("formation_autonomous");

            // 적 고속정이 우현 앞에 나타나면 수상 교전으로 나간다
            var spawner = EnemySpawner.Instance;
            var fastDef = CombatDevTools.FindEnemy("ene_fastboat");
            if (spawner == null || fastDef == null) { _report.AppendLine("- 자율 교전: 스포너·고속정 데이터가 없어 건너뜀"); yield break; }
            Vector3 spot = ship.transform.position + ship.transform.forward * 18f + ship.transform.right * 22f; spot.y = 0f;
            var foe = spawner.SpawnAt(fastDef, spot, Quaternion.LookRotation(-ship.transform.right));
            float before = float.MaxValue, after = float.MaxValue;
            for (int i = 0; i < n; i++) before = Mathf.Min(before, Vector3.Distance(formation.GetEscort(i).transform.position, foe.transform.position));
            yield return new WaitForSeconds(4f);
            int engaging = 0;
            for (int i = 0; i < n; i++)
            {
                if (formation.AutonomousTask(i) == "수상 교전") engaging++;
                if (foe != null) after = Mathf.Min(after, Vector3.Distance(formation.GetEscort(i).transform.position, foe.transform.position));
            }
            _report.AppendLine($"- 자율 교전: 적 고속정 출현 → 수상 교전 {engaging}척 · 가장 가까운 호위함 거리 {before:0}m → {after:0}m");
            if (engaging == 0) Fail("자율: 적 수상함이 나타나도 교전하러 나가지 않음");
            if (foe != null) foe.RemoveWithoutReward();
            yield return null;
        }
    }
}
#endif
