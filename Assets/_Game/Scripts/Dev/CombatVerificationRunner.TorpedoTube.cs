#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 경어뢰 발사관(-tubeOnly, 2026-10-08).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 데이터: Resources/Modules에서 읽히고 설치 카드 목록에 들어감, 현측 전용 규칙
        /// 2) 자리: 양옆이 막힌 자리 거부 · 좌현 끝 칸 → 좌현만 트임 · 그 좌현을 막는 설치 거부
        /// 3) 발사: 잠항(접촉만 확정)한 잠수함 — 트인 현측 30m → 부채꼴 3발·피해, 막힌 현측 30m·최소 거리 안 5m → 발사 없음
        /// </summary>
        private IEnumerator TorpedoTubeCheck()
        {
            _report.AppendLine("\n## 경어뢰 발사관");
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

            Resources.LoadAll<ModuleDefinition>("Modules");
            var tubeDef = CombatDevTools.FindModule("mod_torpedotube");
            var subDef = CombatDevTools.FindEnemy("ene_submarine");
            ModuleDefinition filler = null;
            foreach (var id in new[] { "mod_radar", "mod_repairbay", "mod_ew", "mod_decoy", "mod_magazine" })
            {
                var d = CombatDevTools.FindModule(id);
                if (d != null && d.Width == 1 && d.Height == 1 && d.Placement.Zone == PlacementZone.Anywhere && d.MaxCount == 0) { filler = d; break; }
            }
            if (tubeDef == null || subDef == null || filler == null)
            { Fail($"시험 데이터 없음(발사관 {tubeDef != null} · 잠수함 {subDef != null} · 막는 블록 {filler?.Id ?? "없음"})"); yield break; }

            var pool = Game.Refit.RefitDraft.CollectInstallDefinitions(null);
            bool inPool = pool.Contains(tubeDef);
            _report.AppendLine($"- 데이터: {tubeDef.DisplayName} · 규칙 {tubeDef.Placement.Zone} · 사거리 {tubeDef.Stats.MinRange:0}~{tubeDef.Stats.Range:0} · 피해 {tubeDef.Stats.Damage} · 탄 {tubeDef.Stats.MagazineCapacity} · 설치 카드 목록 {Yes(inPool)} · 프리팹 {Yes(tubeDef.Prefab != null)}");
            if (tubeDef.Placement.Zone != PlacementZone.SideOnly) Fail("발사관 배치 규칙이 현측 전용이 아님");
            if (!inPool) Fail("발사관이 설치 카드 목록에 없음");
            if (tubeDef.Prefab == null || tubeDef.Prefab.GetComponent<TorpedoTubeModule>() == null) Fail("발사관 프리팹·런타임 없음");

            var added = new List<ModuleInstance>();
            ModuleInstance Put1(ModuleDefinition d, int x, int z)
            {
                var c = new GridCoord(x, z);
                if (!grid.CanPlace(d, c, 0, out _)) return null;
                var m = factory.Install(d, c, 0);
                if (m != null) added.Add(m);
                return m;
            }

            // 좌현 끝 줄(-Z)에서 함체가 있는 칸 바로 바깥 = 우현은 함체, 좌현은 바다
            grid.GetExtent(out var min, out var max);
            int px = int.MinValue;
            for (int x = min.X; x <= max.X && px == int.MinValue; x++)
                if (!grid.IsFree(new GridCoord(x, min.Z)) && grid.IsFree(new GridCoord(x, min.Z - 1))) px = x;
            int pz = min.Z - 1;
            if (px == int.MinValue) { Fail("좌현 끝 자리를 찾지 못함(시험 무효)"); yield break; }

            var tube = Put1(tubeDef, px, pz);
            string sides = tube != null ? tube.Sides.ToString() : "-";
            bool portOnly = tube != null && tube.Variant == ModuleVariant.TorpedoTubeSide && tube.Sides == ModuleSides.Port;
            string closeWhy = "-";
            bool closeRefused = tube != null && !grid.CanPlace(filler, new GridCoord(px, pz - 1), 0, out closeWhy);
            _report.AppendLine($"- 자리: 좌현 끝 칸({px},{pz}) 설치 {Yes(tube != null)} → {ModuleVariants.Name(tube?.Variant ?? ModuleVariant.None)} 트인 현측 {sides} · 좌현을 막는 설치 거부 {Yes(closeRefused)}(\"{closeWhy}\")");
            if (tube == null) { Fail("좌현이 트인 자리에 발사관을 설치하지 못함"); yield break; }
            if (!portOnly) Fail("좌현 끝 발사관의 형태·트인 현측이 틀림(좌현만이어야 함)");
            if (!closeRefused) Fail("이미 놓인 발사관의 양옆을 모두 막는 설치가 허용됨");

            // 발사
            var rt = tube.Runtime as TorpedoTubeModule;
            if (rt == null) { Fail("발사관 런타임 없음"); yield break; }
            rt.enabled = false;
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 origin = rt.transform.position;
            // 시작 화면 구성이 바뀌어 출항 절차를 못 거쳤으면 스포너가 함선을 모른다 — 직접 알려 준다(VLS 검사와 같음)
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", GameManager.Instance.Player.transform);

            IEnumerator Engage(Vector3 at, float wait, System.Action<int, float, List<Vector3>> done)
            {
                var e = EnemySpawner.Instance.SpawnAt(subDef, at, Quaternion.LookRotation(fwd));
                var sb = e as SubmarineBase;
                if (sb != null) { sb.DevFrozen = true; sb.ConfirmContact(30f); }   // 잠항 그대로, 소나 접촉만
                float hp0 = sb != null ? sb.CurrentHp : 0f;
                int s0 = rt.Salvos;
                rt.Ammo.Refill();
                Put(rt, "_cooldown", 0f);
                rt.enabled = true;
                float t0 = Time.time;
                yield return null;
                int registered = 0;
                foreach (var t in Game.Combat.TargetRegistry.HostileTo(Game.Combat.CombatFaction.Player, Game.Combat.TargetKind.Submarine)) registered++;
                _report.AppendLine($"  - 진단: 잠수함 생성 {Yes(sb != null)} · 등록 {registered} · 접촉 {Yes(sb != null && sb.IsContactConfirmed)} · " +
                    $"현측 도달 {Yes(sb != null && rt.CanReach(sb.LastKnownPosition))} · 탄 {rt.Ammo.Current}/{rt.Ammo.Capacity} 발사 가능 {Yes(rt.Ammo.CanFire)} · " +
                    $"작동 {Yes(tube.IsOperational)} · 켜짐 {Yes(rt.enabled && rt.gameObject.activeInHierarchy)} · 사거리 {rt.Stats.MinRange:0}~{rt.Stats.Range:0} · " +
                    $"거리 {(sb != null ? Vector3.Distance(sb.transform.position, rt.transform.position) : -1f):0}");
                while (Time.time - t0 < wait && rt.Salvos == s0) { if (sb != null) sb.ConfirmContact(30f); yield return null; }
                if (rt.Salvos != s0)
                {
                    float t1 = Time.time;
                    while (Time.time - t1 < 6f && sb != null && sb.IsAlive && sb.CurrentHp >= hp0) yield return null;
                    yield return new WaitForSeconds(0.5f);
                }
                rt.enabled = false;
                done(rt.Salvos - s0, sb != null ? hp0 - sb.CurrentHp : 0f, new List<Vector3>(rt.LastAims));
                CombatDevTools.ClearBattlefield();
                yield return null;
            }

            int nPort = 0, nStar = 0, nNear = 0; float loss = 0f; List<Vector3> aims = null;
            yield return Engage(origin - right * 30f, 4f, (n, l, a) => { nPort = n; loss = l; aims = a; });
            yield return CloseShot("torpedo_tube", rt.transform, 9f);
            yield return Engage(origin + right * 30f, 3f, (n, _, __) => nStar = n);
            yield return Engage(origin - right * 5f, 3f, (n, _, __) => nNear = n);

            float spread = 0f;
            if (aims != null && aims.Count >= 2)
            {
                Vector3 a = aims[0] - origin, b = aims[^1] - origin; a.y = 0f; b.y = 0f;
                spread = Vector3.Angle(a, b);
            }
            _report.AppendLine($"- 좌현 30m 잠항 잠수함 → {nPort}회 · {aims?.Count ?? 0}발 · 부채꼴 폭 {spread:0}° · 피해 {loss:0} / 우현(막힘) 30m → {nStar}회 / 좌현 5m(최소 거리 안) → {nNear}회");
            if (nPort < 1 || (aims?.Count ?? 0) != 3) Fail("트인 현측의 잠수함에 3발 부채꼴을 쏘지 않음");
            if (spread < 30f || spread > 50f) Fail($"부채꼴 폭이 약 40°가 아님({spread:0}°)");
            if (loss <= 0f) Fail("경어뢰가 잠항 중인 잠수함을 맞히지 못함");
            if (nStar > 0) Fail("막힌 현측으로 쏨");
            if (nNear > 0) Fail("최소 거리 안의 잠수함에 쏨");

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
