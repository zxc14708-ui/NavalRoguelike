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
    /// <summary>자동 전투 검증 — 노봉 40mm 쌍열포(-nobongOnly, 2026-10-08).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 데이터: 설치 카드 목록, 수상 28 · 대공 약 20(기관포 22와 76mm 34 사이)
        /// 2) 좌현 16 m 드론 3기 무리 → 쌍열 발사, 근접신관 공중 폭발, 2기 이상 피해
        /// 3) 좌현 25 m 고속정(기관포 22 밖) → 맞힘
        /// 4) 좌현 24 m 전투기(대공 사거리 밖) → 쏘지 않음
        /// </summary>
        private IEnumerator NobongCheck()
        {
            _report.AppendLine("\n## 노봉 40mm 쌍열포");
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
            var def = CombatDevTools.FindModule("mod_nobong");
            var droneDef = CombatDevTools.FindEnemy("ene_drone");
            var boatDef = CombatDevTools.FindEnemy("ene_fastboat");
            var jetDef = CombatDevTools.FindEnemy("ene_fighter");
            if (def == null || droneDef == null || boatDef == null || jetDef == null)
            { Fail($"시험 데이터 없음(노봉 {def != null} · 드론 {droneDef != null} · 고속정 {boatDef != null} · 전투기 {jetDef != null})"); yield break; }
            bool inPool = Game.Refit.RefitDraft.CollectInstallDefinitions(null).Contains(def);
            _report.AppendLine($"- 데이터: {def.DisplayName} · 수상 {def.Stats.MinRange:0}~{def.Stats.Range:0} · 피해 {def.Stats.Damage}×2 · 간격 {def.Stats.ReloadTime}초 · 탄 {def.Stats.MagazineCapacity} · 설치 카드 목록 {Yes(inPool)}");
            if (!inPool) Fail("노봉이 설치 카드 목록에 없음");

            // 좌현 끝 줄 바깥 칸(좌현 쪽은 바다라 막히지 않는다)
            grid.GetExtent(out var min, out var max);
            ModuleInstance gun = null;
            for (int x = 0; x <= max.X && gun == null; x++)
                foreach (int xx in new[] { x, -x })
                {
                    var c = new GridCoord(xx, min.Z - 1);
                    if (grid.CanPlace(def, c, 0, out _)) { gun = factory.Install(def, c, 0); break; }
                }
            if (gun == null || gun.Runtime is not NobongModule rt) { Fail("좌현에 노봉을 설치하지 못함"); yield break; }
            rt.enabled = false;
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 port = -Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = rt.transform.position; origin.y = 0f;
            _report.AppendLine($"- 설치: 좌현 ({gun.Origin.X},{gun.Origin.Z}) · 대공 사거리 {rt.AirRange:0}");

            IEnumerator Engage(List<EnemyController> targets, float wait, System.Action<int, int, float, int> done)
            {
                var hp0 = new List<float>();
                foreach (var e in targets) hp0.Add(e != null ? e.CurrentHp : 0f);
                int s0 = rt.Salvos, b0 = Projectile.ProximityBursts;
                rt.Ammo.Refill();
                rt.enabled = true;
                yield return new WaitForSeconds(wait);
                rt.enabled = false;
                yield return new WaitForSeconds(0.5f);
                int hurt = 0; float total = 0f;
                for (int i = 0; i < targets.Count; i++)
                {
                    float hp = targets[i] != null && targets[i].IsAlive ? targets[i].CurrentHp : 0f;
                    float loss = hp0[i] - hp;
                    if (loss > 0.01f) { hurt++; total += loss; }
                }
                done(rt.Salvos - s0, hurt, total, Projectile.ProximityBursts - b0);
                CombatDevTools.ClearBattlefield();
                yield return null;
            }
            List<EnemyController> Frozen(EnemyDefinition d, params Vector3[] at)
            {
                var list = new List<EnemyController>();
                foreach (var p in at)
                {
                    var e = EnemySpawner.Instance.SpawnAt(d, p, Quaternion.LookRotation(fwd));
                    if (e != null) { e.DevFrozen = true; e.transform.position = p; list.Add(e); }
                }
                return list;
            }

            Vector3 d16 = origin + port * 16f + Vector3.up * 6f;
            int s1 = 0, h1 = 0, b1 = 0; float l1 = 0f;
            yield return Engage(Frozen(droneDef, d16, d16 + fwd * 1f, d16 - fwd * 1f), 1.6f, (s, h, l, b) => { s1 = s; h1 = h; l1 = l; b1 = b; });
            yield return CloseShot("nobong", rt.transform, 6f);

            // 빗나가는 탄: 드론 옆 0.8 m를 지나가도록 한 발만 직접 쏜다 → 근접신관으로 공중 폭발해야 한다
            int nearBursts = 0; float nearLoss = 0f;
            var shellPrefab = Get<GameObject>(rt, "projectilePrefab");
            var lone = Frozen(droneDef, d16);
            if (shellPrefab != null && lone.Count > 0)
            {
                yield return null;
                var drone = lone[0];
                float hp = drone.CurrentHp;
                Vector3 from = rt.transform.position + Vector3.up * 1f;
                Vector3 side = Vector3.Cross(Vector3.up, (drone.transform.position - from).normalized).normalized;
                Vector3 aimPast = drone.transform.position + side * 2.0f;   // 드론 선체(반폭 1.1) + 탄 반경 밖
                int b0 = Projectile.ProximityBursts;
                var go = PoolManager.Instance.Spawn(shellPrefab, from, Quaternion.LookRotation(aimPast - from));
                go.GetComponent<Projectile>().Launch((aimPast - from).normalized, 85f, 3.5f, DamageSource.Gun);
                yield return new WaitForSeconds(0.6f);
                nearBursts = Projectile.ProximityBursts - b0;
                nearLoss = hp - (drone.IsAlive ? drone.CurrentHp : 0f);
            }
            CombatDevTools.ClearBattlefield();

            int s2 = 0, h2 = 0; float l2 = 0f;
            yield return Engage(Frozen(boatDef, origin + port * 25f), 3f, (s, h, l, _) => { s2 = s; h2 = h; l2 = l; });

            int s3 = 0;
            yield return Engage(Frozen(jetDef, origin + port * 24f + Vector3.up * 8f), 2f, (s, _, __, ___) => s3 = s);

            _report.AppendLine($"- 좌현 16 m 드론 3기 → {s1}회(×2발) · 근접신관 폭발 {b1} · 피해 입은 드론 {h1}기 · 피해 합 {l1:0} / 드론 옆 2 m로 빗나가는 탄 → 근접신관 폭발 {nearBursts} · 피해 {nearLoss:0.#} / 25 m 고속정 → {s2}회 · 피해 {l2:0} / 24 m 전투기(대공 사거리 밖) → {s3}회");
            if (s1 < 1) Fail("드론에 쏘지 않음");
            if (h1 < 2) Fail("드론 무리 여러 기에 피해를 주지 않음(직격 + 파편)");
            if (nearBursts < 1 || nearLoss <= 0f) Fail("드론 곁을 빗나가는 탄이 근접신관으로 터지지 않음");
            if (s2 < 1 || h2 < 1) Fail("기관포 사거리 밖(25 m) 고속정을 맞히지 않음");
            if (s3 > 0) Fail("대공 사거리 밖 항공기에 쏨");

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
