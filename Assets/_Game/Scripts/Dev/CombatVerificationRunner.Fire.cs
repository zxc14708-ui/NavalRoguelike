#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 손상 통제 2 · 3 · 4단계: 화재 · 탄약고 유폭 · 손상 통제반 배치 · 화면 한 줄 · 적 포격이 불난 곳을 노림(2026-11-21).</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator FireCheck()
        {
            _report.AppendLine("\n## 손상 통제 2 · 3 · 4단계 — 화재 · 탄약고 · 손상 통제반(2026-11-21)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            CombatDevTools.InstallTestLoadout();
            var magazine = CombatDevTools.InstallModuleNear("mod_magazine", new GridCoord(0, 0), 4);
            yield return null;
            var modules = ship.GetComponentsInChildren<ModuleRuntime>(true);
            foreach (var m in modules) m.enabled = false;
            var fire = ship.GetComponent<ShipFire>();
            var flood = ship.GetComponent<ShipFlooding>();
            var dc = ship.GetComponent<ShipDamageControl>();
            if (fire == null || flood == null || dc == null) { Fail("기함에 ShipFire · ShipFlooding · ShipDamageControl이 없음"); yield break; }
            MakeInvulnerable(ship);
            ShipFire.Enabled = true; ShipFlooding.Enabled = true;   // 검사 환경은 꺼 둔다 — 이 검사에서만 켠다
            s_pauseModuleHealing = true;   // 화재가 블록 내구를 깎는지 보려면 검사기의 블록 채우기를 멈춘다
            fire.ExtinguishAll(); flood.DrainAll();
            ShipDamageControl.DevPaused = false;
            var grid = ship.Grid;
            var gun = grid.Modules.FirstOrDefault(m => m != null && m.Definition.Type == ModuleType.Autocannon && !m.IsDestroyed);
            float radar0 = ship.Systems.DetectionRange;

            // ---- 1) 불: 커지고 · 블록과 선체를 깎고 · 화면 한 줄 · 연기로 레이더 감소
            // (2026-11-21 Codex 점검: 불 하나 진화와 옮겨 붙기를 따로 본다 — 여기서는 옮겨 붙지 않게)
            ShipFire.DevNoSpread = true;
            float hp0 = gun != null ? gun.Hp : 0f, hull0 = ship.HullHp;
            var f1 = fire.Ignite(gun, 0.5f);
            yield return new WaitForSeconds(1.5f);
            float hp1 = gun != null ? gun.Hp : 0f;
            float radar1 = ship.Systems.DetectionRange;
            bool hud = dc.HudVisible;
            string line = dc.StatusLine;
            _report.AppendLine($"- 기관포 불: 세기 {(f1 != null ? f1.Intensity : 0f):0.00} · 블록 내구 {hp0:0.0} → {hp1:0.0} · 선체 −{hull0 - ship.HullHp:0.0} · 레이더 {radar0:0} → {radar1:0}m · 화면 한 줄 {Yes(hud)} \"{line}\"");
            if (gun == null || f1 == null) Fail("불을 붙일 기관포가 없음");
            if (hp1 >= hp0 - 0.1f) Fail("불이 블록 내구를 깎지 않음");
            if (radar1 >= radar0 - 0.5f) Fail("연기로 레이더 탐지가 줄지 않음");
            if (!hud || !line.Contains("화재")) Fail("손상 통제 화면 한 줄에 화재가 보이지 않음");
            yield return CloseShot("fire_autocannon", gun?.Runtime != null ? gun.Runtime.transform : ship.transform, 9f);
            yield return ScreenShot("fire_hud");

            // ---- 2) 손상 통제반이 끈다
            float until = Time.time + 12f, fightStart = Time.time;
            bool calm = Time.time - ship.LastHullDamageTime >= ShipDamageControl.UnderFireWindow;   // 불의 선체 피해는 "교전 중"으로 치지 않는다
            while (Time.time < until && fire.Fires.Count > 0) yield return null;
            _report.AppendLine($"- 진화(불 하나 · 옮겨 붙기 끔): 손상 통제 {dc.Crews}조 · {Time.time - fightStart:0.0}초 · 남은 불 {fire.Fires.Count} · 끈 불 {fire.Extinguished} · 불에 타는 동안 교전 중 아님 {Yes(calm)}");
            ShipFire.DevNoSpread = false;
            if (!calm) Fail("불 자체의 선체 피해가 교전 중으로 쳐져 진화가 느려짐");
            if (fire.Fires.Count > 0) Fail("손상 통제반이 12초 안에 불을 끄지 못함");

            // ---- 3) 옮겨 붙기(앞 단계 불이 남았어도 깨끗이 시작)
            fire.ExtinguishAll();
            var f2 = fire.Ignite(gun, 1f);
            int before = fire.Fires.Count;
            for (int i = 0; i < 3 && fire.Fires.Count == before; i++) Call(fire, "TrySpread", f2);
            _report.AppendLine($"- 옮겨 붙기: 불 {before} → {fire.Fires.Count}개(맞닿은 블록)");
            if (fire.Fires.Count <= before) Fail("불이 맞닿은 블록으로 옮겨 붙지 않음");
            fire.ExtinguishAll();

            // ---- 4) 자동 순서: 유폭 위험 화재가 먼저(조 1 기준 — 구멍 · 보통 불보다)
            flood.MakeHole(new GridCoord(0, 1), ShipFlooding.MissileHole, -ship.transform.right);
            fire.Ignite(gun, 0.6f);
            ShipFire.Fire magFire = magazine != null ? fire.Ignite(magazine, 0.5f) : null;
            yield return null; yield return null;
            object first = dc.Assigned.Count > 0 ? dc.Assigned[0] : null;
            bool magFirst = magFire != null && ReferenceEquals(first, magFire);
            string dcLine = dc.StatusLine;
            _report.AppendLine($"- 자동 순서: 첫 일 = {(magFirst ? "탄약고 불(유폭 위험)" : first?.GetType().Name ?? "없음")} · 화면 \"{dcLine}\"");
            if (magazine != null && !magFirst) Fail("자동 순서에서 유폭 위험 화재가 먼저가 아님");
            if (!dcLine.Contains("화재") || !dcLine.Contains("침수")) Fail("화면 한 줄에 화재 · 침수가 보이지 않음");

            // ---- 4-1) 손상통제 블록 작동 연출: 일이 있으면 경광등이 돈다
            var fx = ship.GetComponent<Game.View.DamageControlFx>();
            var bay = CombatDevTools.InstallModuleNear("mod_repairbay", new GridCoord(0, 0), 4);
            yield return new WaitForSeconds(0.7f);   // 경광등을 맞추는 주기(0.5초)
            int working = fx != null ? fx.WorkingBeacons : 0, beacons = fx != null ? fx.BeaconCount : 0;
            _report.AppendLine($"- 손상통제 블록 작동 연출: 블록 {Yes(bay != null)} · 경광등 {beacons} · 일하는 중(도는) {working}");
            if (bay != null && (beacons < 1 || working < 1)) Fail("손상통제 블록이 일하는데 경광등이 돌지 않음");
            if (bay?.Runtime != null) yield return CloseShot("dc_beacon_working", bay.Runtime.transform, 7f);

            // ---- 5) 탄약고 유폭: 손상 통제반을 잠깐 멈춘 동안 9초 카운트다운 → 폭발
            if (magFire != null)
            {
                ShipDamageControl.DevPaused = true;
                magFire.Intensity = 0.9f;
                for (int i = 0; i < 4; i++) flood.MakeHole(new GridCoord(-1 + (i % 3), i % 2 == 0 ? 1 : -1), ShipFlooding.ShellHole, ship.transform.right);
                int cook0 = fire.CookOffs;
                float cookUntil = Time.time + 14f;
                bool sawCountdown = false;
                while (Time.time < cookUntil && fire.CookOffs == cook0)
                {
                    if (magFire.CookOff >= 0f) sawCountdown = true;
                    if (fire.FireOn(magazine) == null) magFire = fire.Ignite(magazine, 0.9f);
                    yield return null;
                }
                _report.AppendLine($"- 탄약고 유폭: 카운트다운 {Yes(sawCountdown)} · 유폭 {fire.CookOffs - cook0} · 탄약고 부서짐 {Yes(magazine.IsDestroyed)}");
                if (!sawCountdown) Fail("탄약고 화재에 유폭 카운트다운이 없음");
                if (fire.CookOffs == cook0 || !magazine.IsDestroyed) Fail("카운트다운이 끝나도 탄약고가 터지지 않음");
                yield return ScreenShot("fire_cookoff");
                ShipDamageControl.DevPaused = false;
            }
            else _report.AppendLine("- 탄약고를 놓을 자리가 없어 유폭 검사 생략");
            fire.ExtinguishAll(); flood.DrainAll();

            // ---- 5-1) 부서진 블록도 수리로 30%가 차면 다시 작동하고 겉모습이 돌아온다
            var victim = grid.Modules.FirstOrDefault(m => m != null && !m.IsDestroyed && m.Definition.Type == ModuleType.Autocannon && m != gun) ?? gun;
            if (victim != null)
            {
                victim.TakeDamage(victim.Hp + 1f);
                yield return null;
                var wreck = ship.GetComponent<Game.View.ModuleWreckFx>();
                bool wrecked = victim.IsDestroyed && wreck != null && wreck.IsShown(victim);
                victim.Repair(victim.MaxHp * 0.2f);
                bool stillDown = victim.IsDestroyed;
                victim.Repair(victim.MaxHp * 0.15f);
                yield return null;
                bool back = !victim.IsDestroyed && (wreck == null || !wreck.IsShown(victim));
                _report.AppendLine($"- 수리로 되살아남: 부서짐 · 그을림 {Yes(wrecked)} → 20% 수리 뒤 아직 부서짐 {Yes(stillDown)} → 35% 수리 뒤 작동 · 그을림 사라짐 {Yes(back)}");
                if (!wrecked || !stillDown || !back) Fail("부서진 블록이 수리로 30%가 차도 다시 작동하지 않음(또는 너무 일찍 작동)");
            }

            // ---- 6) 적 포격이 불난 곳을 노림(측정만)
            // 바깥 줄(우현 끝) 블록 — 그쪽에 있는 적에게 바로 보인다. 적은 모두 우현 쪽에 둔다
            var target = grid.Modules.Where(m => m != null && !m.IsDestroyed && m.Definition.Type != ModuleType.Bridge)
                                     .OrderByDescending(m => m.OccupiedCoords.Max(c => c.Z)).FirstOrDefault();
            // 고르기 함수: 불난 바깥 블록은 그쪽(우현)에서는 고르고, 반대쪽(좌현)에서는 고르지 않는다
            fire.Ignite(target, 0.8f);
            Vector3 stbdPt = ship.transform.position + ship.transform.right * 40f, portPt = ship.transform.position - ship.transform.right * 40f;
            bool pickStbd = ShipHitPoints.TryPickBurning(grid, stbdPt, out var pc) && target.OccupiedCoords.Contains(pc);
            bool pickPort = ShipHitPoints.TryPickBurning(grid, portPt, out _);
            fire.ExtinguishAll();
            _report.AppendLine($"- 불난 블록 고르기: 우현에서 {Yes(pickStbd)} · 좌현(안 보임)에서 {Yes(pickPort)}");
            if (!pickStbd) Fail("보이는 쪽에서 불난 블록을 겨냥으로 고르지 않음");
            if (pickPort) Fail("가려진 쪽에서도 불난 블록을 고름");
            int picks0 = EnemyController.DevBurningPicks;
            // 같은 자리 · 같은 적으로 불 없이 한 번(대조), 불을 켜고 한 번 — 탄착은 참고(포탄이 선체 판정 상자에 먼저 닿고 적이 움직여 들쭉날쭉)
            var hits = new List<DamageResolver.HitRecord>();
            void OnHit(DamageResolver r, DamageResolver.HitRecord rec) { if (r.Grid == grid && rec.Info.Source == DamageSource.Gun) hits.Add(rec); }
            DamageResolver.Resolved += OnHit;
            DamageResolver.DevMeasureOnly = true;
            ShipDamageControl.DevPaused = true;   // 불을 끄지 않게
            float[] shares = new float[2]; int[] counts = new int[2];
            for (int pass = 0; pass < 2; pass++)
            {
                hits.Clear();
                float starboard = Mathf.Atan2(ship.transform.right.x, ship.transform.right.z) * Mathf.Rad2Deg;
                for (int i = 0; i < 4; i++)
                    foreach (var e in CombatDevTools.SpawnRing("ene_fastboat", 1, 40f, starboard - 30f + i * 20f)) e.DevFrozen = false;
                float focusUntil = Time.time + 14f;
                while (Time.time < focusUntil)
                {
                    if (pass == 1)
                    {
                        var tf = fire.FireOn(target);
                        if (tf == null) fire.Ignite(target, 0.8f); else tf.Intensity = 0.8f;
                    }
                    yield return null;
                }
                CombatDevTools.ClearBattlefield();
                fire.ExtinguishAll();
                int on = target != null ? hits.Count(h => target.OccupiedCoords.Contains(h.Cell)) : 0;
                counts[pass] = hits.Count;
                shares[pass] = hits.Count > 0 ? (float)on / hits.Count : 0f;
                yield return new WaitForSeconds(0.5f);
            }
            DamageResolver.Resolved -= OnHit;
            DamageResolver.DevMeasureOnly = false;
            int picks = EnemyController.DevBurningPicks - picks0;
            _report.AppendLine($"- 불난 블록({target?.Definition.DisplayName}) 포격: 적 포수가 불난 블록을 겨냥으로 고른 횟수 {picks} · 탄착 참고 — 불 없을 때 {counts[0]}발 중 {shares[0]:P0} → 불났을 때 {counts[1]}발 중 {shares[1]:P0}");
            if (picks < 1) Fail("교전 중 적 포수가 불난 블록을 한 번도 겨누지 않음");
            fire.ExtinguishAll(); flood.DrainAll();
            ShipDamageControl.DevPaused = false;

            s_pauseModuleHealing = false;
            foreach (var m in modules) if (m != null) m.enabled = true;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
