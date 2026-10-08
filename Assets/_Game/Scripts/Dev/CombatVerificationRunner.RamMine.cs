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
    /// <summary>자동 전투 검증 — 충각 함수·선미 기뢰 투하궤(-ramMineOnly, 2026-10-08).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 기뢰 투하궤: 선미 끝에만 설치 · 뒤를 막는 설치 거부 · 쫓아오는 적이 없으면 안 떨어뜨림 · 뒤쪽 적이 있으면 투하 ·
        ///             작동한 기뢰에 적이 닿으면 폭발해 반경 안 두 척에 피해.
        /// 충각 함수: 선수 끝에만 설치 · 앞을 막는 설치 거부 · 멈춰 있으면 안 받음 · 전속으로 앞의 고속정을 들이받음 ·
        ///           정면으로 달려드는 자폭 보트를 터지기 전에 받아 격침.
        /// </summary>
        private IEnumerator RamMineCheck()
        {
            _report.AppendLine("\n## 충각 함수 · 기뢰 투하궤");
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
            var railDef = CombatDevTools.FindModule("mod_minerail");
            var ramDef = CombatDevTools.FindModule("mod_rambow");
            var boatDef = CombatDevTools.FindEnemy("ene_fastboat");
            var suicideDef = CombatDevTools.FindEnemy("ene_suicide_boat");
            ModuleDefinition filler = null;
            foreach (var id in new[] { "mod_radar", "mod_repairbay", "mod_ew", "mod_decoy", "mod_magazine" })
            {
                var d = CombatDevTools.FindModule(id);
                if (d != null && d.Width == 1 && d.Height == 1 && d.Placement.Zone == PlacementZone.Anywhere && d.MaxCount == 0) { filler = d; break; }
            }
            if (railDef == null || ramDef == null || boatDef == null || suicideDef == null || filler == null)
            { Fail($"시험 데이터 없음(투하궤 {railDef != null} · 충각 {ramDef != null} · 고속정 {boatDef != null} · 자폭 보트 {suicideDef != null} · 막는 블록 {filler != null})"); yield break; }
            var pool = Game.Refit.RefitDraft.CollectInstallDefinitions(null);
            _report.AppendLine($"- 데이터: {railDef.DisplayName}({railDef.Placement.Zone}) · {ramDef.DisplayName}({ramDef.Placement.Zone}) · 설치 카드 목록 {Yes(pool.Contains(railDef) && pool.Contains(ramDef))}");
            if (!pool.Contains(railDef) || !pool.Contains(ramDef)) Fail("설치 카드 목록에 없음");

            var added = new List<ModuleInstance>();
            ModuleInstance Put1(ModuleDefinition d, GridCoord c)
            {
                if (!grid.CanPlace(d, c, 0, out _)) return null;
                var m = factory.Install(d, c, 0);
                if (m != null) added.Add(m);
                return m;
            }
            grid.GetExtent(out var min, out var max);
            int RowZ(int x) { for (int z = 0; z <= grid.MaxHalfBeam; z++) { if (!grid.IsFree(new GridCoord(x, z))) return z; if (!grid.IsFree(new GridCoord(x, -z))) return -z; } return 0; }
            int zs = RowZ(min.X), zb = RowZ(max.X);

            // ---- 자리
            var rail = Put1(railDef, new GridCoord(min.X - 1, zs));
            string railBlockWhy = "-";
            bool railBlockRefused = rail != null && !grid.CanPlace(filler, new GridCoord(min.X - 2, zs), 0, out railBlockWhy);
            var ram = Put1(ramDef, new GridCoord(max.X + 1, zb));
            string ramBlockWhy = "-";
            bool ramBlockRefused = ram != null && !grid.CanPlace(filler, new GridCoord(max.X + 2, zb), 0, out ramBlockWhy);
            _report.AppendLine($"- 자리: 투하궤 선미 끝 설치 {Yes(rail != null)} · 뒤를 막는 설치 거부 {Yes(railBlockRefused)}(\"{railBlockWhy}\") / 충각 선수 끝 설치 {Yes(ram != null)} · 앞을 막는 설치 거부 {Yes(ramBlockRefused)}(\"{ramBlockWhy}\")");
            if (rail == null || ram == null) { Fail("선미·선수 끝에 설치하지 못함"); yield break; }
            if (!railBlockRefused) Fail("기뢰 투하궤의 뒤를 막는 설치가 허용됨");
            if (!ramBlockRefused) Fail("충각 함수의 앞을 막는 설치가 허용됨");

            var railRt = rail.Runtime as MineRailModule;
            var ramRt = ram.Runtime as RamBowModule;
            if (railRt == null || ramRt == null) { Fail("런타임 없음"); yield break; }
            railRt.enabled = false; ramRt.enabled = false;
            Vector3 Fwd() { var f = ship.transform.forward; f.y = 0f; return f.normalized; }

            // ---- 기뢰 투하궤
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(3f);   // 속력을 붙인다
            railRt.Ammo.Refill();
            railRt.enabled = true;
            int d0 = railRt.Drops;
            yield return new WaitForSeconds(2.5f);
            int noChaser = railRt.Drops - d0;

            var chaser = EnemySpawner.Instance.SpawnAt(boatDef, railRt.transform.position - Fwd() * 14f, Quaternion.LookRotation(Fwd()));
            if (chaser != null) chaser.DevFrozen = true;
            d0 = railRt.Drops;
            float t0 = Time.time;
            while (Time.time - t0 < 4f && railRt.Drops == d0) yield return null;
            int chasedDrops = railRt.Drops - d0;
            railRt.enabled = false;
            CombatDevTools.ClearBattlefield();

            // 떨어진 기뢰 하나에 적 두 척을 붙인다(작동 대기)
            PlayerMine mine = PlayerMine.Active.Count > 0 ? PlayerMine.Active[^1] : null;
            int boom = -1; float loss = 0f;
            if (mine != null)
            {
                yield return new WaitForSeconds(1.5f);
                Vector3 mp = mine.transform.position;
                var a = EnemySpawner.Instance.SpawnAt(boatDef, mp + Vector3.right * 6f, Quaternion.identity);
                var b = EnemySpawner.Instance.SpawnAt(boatDef, mp + Vector3.right * 8f, Quaternion.identity);
                if (a != null) a.DevFrozen = true;
                if (b != null) b.DevFrozen = true;
                float ha = a != null ? a.CurrentHp : 0f, hb = b != null ? b.CurrentHp : 0f;
                yield return null;
                if (a != null) a.transform.position = mp + Vector3.right * 0.8f;   // 하나는 기뢰에 닿고
                if (b != null) b.transform.position = mp - Vector3.right * 2.2f;   // 하나는 폭발 반경 안
                yield return new WaitForSeconds(0.6f);
                boom = PlayerMine.LastHitCount;
                loss = (a != null ? ha - (a.IsAlive ? a.CurrentHp : 0f) : 0f) + (b != null ? hb - (b.IsAlive ? b.CurrentHp : 0f) : 0f);
                CombatDevTools.ClearBattlefield();
            }
            _report.AppendLine($"- 기뢰 투하궤: 전진 중 쫓는 적 없음 → {noChaser}개 / 뒤 14 m 적 → {chasedDrops}개 투하 · 기뢰에 닿음 → 폭발 피해 {boom}척, 피해 합 {loss:0}");
            if (noChaser > 0) Fail("쫓아오는 적이 없는데 기뢰를 떨어뜨림");
            if (chasedDrops < 1) Fail("뒤쪽 적이 있는데 기뢰를 떨어뜨리지 않음");
            if (boom < 2 || loss <= 0f) Fail("기뢰가 닿은 적과 반경 안 적에 피해를 주지 않음");

            // ---- 충각 함수
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(4f);   // 멈춘다
            ramRt.enabled = true;
            int r0 = ramRt.Rams;
            Vector3 face = ramRt.transform.position + Fwd() * 1f;
            var idle = EnemySpawner.Instance.SpawnAt(boatDef, face + Fwd() * 1.2f, Quaternion.LookRotation(Vector3.Cross(Vector3.up, Fwd())));
            if (idle != null) idle.DevFrozen = true;
            yield return new WaitForSeconds(1f);
            int idleRams = ramRt.Rams - r0;
            CombatDevTools.ClearBattlefield();

            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(3f);
            face = ramRt.transform.position + Fwd() * 1f;
            var victim = EnemySpawner.Instance.SpawnAt(boatDef, face + Fwd() * 8f, Quaternion.LookRotation(Vector3.Cross(Vector3.up, Fwd())));
            if (victim != null) victim.DevFrozen = true;
            float hv = victim != null ? victim.CurrentHp : 0f;
            r0 = ramRt.Rams;
            t0 = Time.time;
            while (Time.time - t0 < 4f && ramRt.Rams == r0) yield return null;
            int fastRams = ramRt.Rams - r0;
            float ramLoss = victim != null ? hv - (victim.IsAlive ? victim.CurrentHp : 0f) : 0f;
            yield return CloseShot("ram_bow", ramRt.transform, 7f);
            CombatDevTools.ClearBattlefield();

            face = ramRt.transform.position + Fwd() * 1f;
            var bomber = EnemySpawner.Instance.SpawnAt(suicideDef, face + Fwd() * 14f, Quaternion.LookRotation(-Fwd())) as SuicideBoat;
            bool bomberRammed = false, bomberDetonated = false;
            if (bomber != null)
            {
                r0 = ramRt.Rams;
                t0 = Time.time;
                while (Time.time - t0 < 5f && bomber.IsAlive && bomber.isActiveAndEnabled) yield return null;
                bomberRammed = ramRt.Rams > r0;
                bomberDetonated = bomber.Detonated;
            }
            ramRt.enabled = false;
            ship.SetEngineOrder(0f);
            CombatDevTools.ClearBattlefield();
            _report.AppendLine($"- 충각 함수: 멈춤 상태 앞 1 m 적 → {idleRams}회 / 전속으로 앞 고속정 → {fastRams}회 · 피해 {ramLoss:0}(마지막 {ramRt.LastRamDamage:0}) / 정면 자폭 보트 → 충각에 받힘 {Yes(bomberRammed)} · 자폭 {Yes(bomberDetonated)}");
            if (idleRams > 0) Fail("멈춰 있는데 들이받음");
            if (fastRams < 1 || ramLoss <= 0f) Fail("전속으로 앞의 적을 들이받지 않음");
            if (!bomberRammed || bomberDetonated) Fail("정면 자폭 보트를 터지기 전에 받아 격침하지 못함");

            for (int i = added.Count - 1; i >= 0; i--) CombatDevTools.RemoveModule(added[i]);
            ship.Systems?.Recalculate();
            ship.DevRudderOverride = null;
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }
    }
}
#endif
