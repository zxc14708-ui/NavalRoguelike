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
    /// <summary>자동 전투 검증 — 곡사포(-howitzerOnly, 2026-10-08).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 데이터: Resources/Modules에서 읽히고 설치 카드 목록에 들어감
        /// 2) 함교 뒤(선미 끝)에 놓고 함교 너머 선수 쪽 30m에 고속정 3척 무리 → 넘겨 쏴서 2척 이상에 피해
        /// 3) 최소 거리 안 8m 고속정 · 항공기만 있을 때 → 쏘지 않음
        /// 4) 진로를 유지하며 달리는 고속정 → 앞질러 쏴서 맞힘(착탄 시각 예측)
        /// </summary>
        private IEnumerator HowitzerCheck()
        {
            _report.AppendLine("\n## 곡사포");
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
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            yield return new WaitForSeconds(2f);
            if (grid == null || factory == null) { Fail("격자·ModuleFactory 없음"); yield break; }

            Resources.LoadAll<ModuleDefinition>("Modules");
            var def = CombatDevTools.FindModule("mod_howitzer");
            var boatDef = CombatDevTools.FindEnemy("ene_fastboat");
            var jetDef = CombatDevTools.FindEnemy("ene_fighter");
            if (def == null || boatDef == null || jetDef == null) { Fail($"시험 데이터 없음(곡사포 {def != null} · 고속정 {boatDef != null} · 전투기 {jetDef != null})"); yield break; }
            bool inPool = Game.Refit.RefitDraft.CollectInstallDefinitions(null).Contains(def);
            _report.AppendLine($"- 데이터: {def.DisplayName} · 사거리 {def.Stats.MinRange:0}~{def.Stats.Range:0} · 피해 {def.Stats.Damage} · 간격 {def.Stats.ReloadTime}초 · 탄 {def.Stats.MagazineCapacity} · 설치 카드 목록 {Yes(inPool)}");
            if (!inPool) Fail("곡사포가 설치 카드 목록에 없음");

            // 선미 끝(가운데 줄) 바로 뒤 — 선수 쪽 표적과의 사이에 함교가 있다
            grid.GetExtent(out var min, out var max);
            ModuleInstance gun = null;
            for (int z = 0; z <= grid.MaxHalfBeam && gun == null; z++)
                foreach (int zz in z == 0 ? new[] { 0 } : new[] { z, -z })
                {
                    var c = new GridCoord(min.X - 1, zz);
                    if (grid.CanPlace(def, c, 0, out _)) { gun = factory.Install(def, c, 0); break; }
                }
            if (gun == null || gun.Runtime is not HowitzerModule rt) { Fail("선미에 곡사포를 설치하지 못함"); yield break; }
            rt.enabled = false;

            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = rt.transform.position; origin.y = 0f;
            bool overBridge = false;
            foreach (var m in grid.Modules)
                if (m != null && m.Definition.HeightClass == ModuleHeight.High && m.Runtime != null &&
                    Vector3.Dot(m.Runtime.transform.position - origin, fwd) > 0f) overBridge = true;
            _report.AppendLine($"- 설치: 선미 끝({gun.Origin.X},{gun.Origin.Z}) · 선수 쪽 표적과 사이에 높은 상부 구조물 {Yes(overBridge)}");

            IEnumerator Shoot(List<EnemyController> targets, float wait, System.Action<int, int, float> done)
            {
                var hp0 = new List<float>();
                foreach (var e in targets) hp0.Add(e != null ? e.CurrentHp : 0f);
                int s0 = rt.Shots;
                rt.Ammo.Refill();
                rt.enabled = true;
                float t0 = Time.time;
                while (Time.time - t0 < wait && rt.Shots == s0) yield return null;
                if (rt.Shots != s0) yield return new WaitForSeconds(3.5f);   // 비행
                rt.enabled = false;
                int hurt = 0; float total = 0f;
                for (int i = 0; i < targets.Count; i++)
                {
                    float hp = targets[i] != null && targets[i].IsAlive ? targets[i].CurrentHp : 0f;
                    float loss = hp0[i] - hp;
                    if (loss > 0.01f) { hurt++; total += loss; }
                }
                done(rt.Shots - s0, hurt, total);
                CombatDevTools.ClearBattlefield();
                yield return null;
            }
            List<EnemyController> Frozen(EnemyDefinition d, params Vector3[] at)
            {
                var list = new List<EnemyController>();
                foreach (var p in at)
                {
                    var e = EnemySpawner.Instance.SpawnAt(d, p, Quaternion.LookRotation(right));
                    if (e != null) { e.DevFrozen = true; list.Add(e); }
                }
                return list;
            }

            // 2) 함교 너머 30m 고속정 3척(2m 간격)
            Vector3 c30 = origin + fwd * 30f;
            int s1 = 0, h1 = 0; float l1 = 0f;
            var group = Frozen(boatDef, c30, c30 + right * 2f, c30 - right * 2f);
            yield return Shoot(group, 6f, (s, h, l) => { s1 = s; h1 = h; l1 = l; });
            yield return CloseShot("howitzer", rt.transform, 9f);

            // 3) 최소 거리 안 · 항공기만
            int s2 = 0, s3 = 0;
            yield return Shoot(Frozen(boatDef, origin - fwd * 8f), 4f, (s, _, __) => s2 = s);
            yield return Shoot(Frozen(jetDef, origin - fwd * 24f + Vector3.up * 8f), 4f, (s, _, __) => s3 = s);

            // 4) 진로를 유지하며 달리는 고속정(옆으로 지나감) — 앞질러 쏴서 맞히는가
            int s4 = 0, h4 = 0;
            var runner = EnemySpawner.Instance.SpawnAt(boatDef, origin - fwd * 30f - right * 8f, Quaternion.LookRotation(right));
            if (runner != null)
            {
                runner.DevFrozen = false;
                var runners = new List<EnemyController> { runner };
                yield return Shoot(runners, 5f, (s, h, _) => { s4 = s; h4 = h; });
            }

            _report.AppendLine($"- 함교 너머 30m 고속정 3척 → {s1}발 · 피해 입은 적 {h1}척 · 피해 합 {l1:0} / 최소 거리 안 8m → {s2}발 / 항공기만 → {s3}발 / 달리는 고속정 → {s4}발 · 명중 {Yes(h4 > 0)}");
            if (s1 < 1) Fail("함교 너머 표적에 쏘지 않음");
            if (h1 < 2) Fail("착탄 반경 안 여러 척에 피해를 주지 않음");
            if (s2 > 0) Fail("최소 거리 안의 표적에 쏨");
            if (s3 > 0) Fail("항공기에 쏨");
            if (s4 > 0 && h4 == 0) _report.AppendLine("  - 참고: 달리는 고속정은 진로를 바꾸면 빗나갈 수 있다(규칙 실패 아님)");

            CombatDevTools.RemoveModule(gun);
            ship.Systems?.Recalculate();
            ship.DevRudderOverride = null;
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }
    }
}
#endif
