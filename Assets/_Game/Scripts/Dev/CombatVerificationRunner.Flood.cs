#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 손상 통제 1단계: 구획 침수 · 기울기(2026-11-21).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 우현에서 진짜 어뢰 한 발 → 구멍 · 물 · 우현으로 기움(겉모습 Model도) · 속력 저하 · 안내.
        /// 2) 손상 통제반이 구멍을 막고(약 8초) 배수로 물이 빠지며 기울기가 돌아온다.
        /// 3) 좌현 구멍 → 반대로 기움. 4) 구멍 여럿 → 침몰 진행(선체가 깎임). 5) 정비 화면에 들어가면 물 · 구멍이 모두 없어짐.
        /// </summary>
        private IEnumerator FloodCheck()
        {
            _report.AppendLine("\n## 손상 통제 1단계 — 구획 침수 · 기울기(2026-11-21)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            CombatDevTools.InstallTestLoadout();
            yield return null;
            var modules = ship.GetComponentsInChildren<ModuleRuntime>(true);
            foreach (var m in modules) m.enabled = false;
            var flood = ship.GetComponent<ShipFlooding>();
            if (flood == null) { Fail("기함에 ShipFlooding 없음"); yield break; }
            flood.DrainAll();
            MakeInvulnerable(ship);
            var model = ship.transform.Find("Model");
            Quaternion rest = model != null ? model.localRotation : Quaternion.identity;
            float baseSpeed = ship.FloodSpeedMultiplier;

            // ---- 1) 우현 어뢰
            var subDef = CombatDevTools.FindEnemy("ene_submarine");
            var torpedoPrefab = subDef != null && subDef.Prefab != null ? PrivateField<GameObject>(subDef.Prefab.GetComponent<Submarine>(), "torpedoPrefab") : null;
            Vector3 from = ship.transform.position + ship.transform.right * 30f; from.y = 0f;
            int holes0 = flood.HolesMade;
            if (torpedoPrefab != null && PoolManager.Instance != null)
            {
                var go = PoolManager.Instance.Spawn(torpedoPrefab, from, Quaternion.LookRotation(ship.transform.position - from));
                go.GetComponent<Torpedo>()?.Launch(ship.transform.position + ship.transform.forward * ship.CurrentSpeed * 1.2f, 20f);
            }
            float until = Time.time + 6f;
            while (Time.time < until && flood.HolesMade == holes0) yield return null;
            bool torpedoHole = flood.HolesMade > holes0;
            if (!torpedoHole) flood.MakeHole(new GridCoord(0, 1), ShipFlooding.TorpedoHole, -ship.transform.right);   // 어뢰가 빗나가면 같은 구멍을 직접
            yield return new WaitForSeconds(6f);
            float water = flood.FloodedCompartments, list = flood.TargetList, vis = flood.VisibleList, speed = ship.FloodSpeedMultiplier;
            float modelTilt = model != null ? Quaternion.Angle(model.localRotation, rest) : 0f;
            _report.AppendLine($"- 우현 어뢰: 구멍 {(torpedoHole ? "진짜 어뢰로 생김" : "어뢰가 빗나가 직접 냄")} · 구획 {flood.Compartments.Count}개 · 물 {water:0.00}구획 · 목표 기울기 {list:+0.0;-0.0}°(+ 우현) · 보이는 기울기 {vis:0.0}° · Model 기운 각 {modelTilt:0.0}° · 속력 ×{speed:0.00}");
            if (water <= 0.05f) Fail("구멍이 나도 물이 차지 않음");
            if (list <= 0.3f) Fail("우현 구멍인데 우현으로 기울지 않음");
            if (modelTilt < 0.3f) Fail("배 겉모습이 기울지 않음");
            if (speed >= baseSpeed - 0.01f) Fail("침수해도 속력이 줄지 않음");
            yield return CloseShot("flood_list_starboard", ship.transform, 14f);

            // ---- 2) 막기 · 배수
            float patchUntil = Time.time + 20f;
            while (Time.time < patchUntil && flood.Holes.Count > 0) yield return null;
            float peak = flood.FloodedCompartments;
            yield return new WaitForSeconds(8f);
            float after = flood.FloodedCompartments;
            _report.AppendLine($"- 막기 · 배수: 막은 구멍 {flood.HolesPatched} · 남은 구멍 {flood.Holes.Count} · 물 {peak:0.00} → 8초 뒤 {after:0.00}구획 · 기울기 {flood.TargetList:0.0}°");
            if (flood.Holes.Count > 0) Fail("손상 통제반이 20초 안에 구멍을 막지 못함");
            if (after >= peak - 0.02f) Fail("구멍을 막아도 물이 빠지지 않음");

            // ---- 3) 좌현
            flood.DrainAll();
            yield return new WaitForSeconds(0.5f);
            flood.MakeHole(new GridCoord(0, -1), ShipFlooding.TorpedoHole, ship.transform.right);
            yield return new WaitForSeconds(5f);
            float portList = flood.TargetList;
            _report.AppendLine($"- 좌현 구멍: 목표 기울기 {portList:+0.0;-0.0}°");
            if (portList >= -0.3f) Fail("좌현 구멍인데 좌현으로 기울지 않음");

            // ---- 4) 침몰 진행: 여러 구획에 큰 구멍
            flood.DrainAll();
            for (int x = -3; x <= 3; x += 2) flood.MakeHole(new GridCoord(x, 1), 0.3f, -ship.transform.right);
            float hull0 = ship.HullHp;
            float sinkUntil = Time.time + 8f;
            while (Time.time < sinkUntil && !flood.Sinking) yield return null;
            yield return new WaitForSeconds(1f);
            float hullLoss = hull0 - ship.HullHp;
            _report.AppendLine($"- 침몰 진행: 침몰 위험 {Yes(flood.Sinking)} · 물 {flood.FloodFraction:P0} · 기울기 {flood.TargetList:0.0}° · 그동안 선체 −{hullLoss:0}");
            if (!flood.Sinking) Fail("크게 침수해도 침몰 위험이 되지 않음");
            if (hullLoss <= 0f) Fail("침몰 위험인데 선체가 깎이지 않음");
            yield return CloseShot("flood_sinking", ship.transform, 16f);

            // ---- 5) 정비 화면 = 물 · 구멍 없음
            GameManager.Instance.SetState(GameState.Refit);
            yield return null;
            float refitWater = flood.FloodedCompartments; int refitHoles = flood.Holes.Count;
            GameManager.Instance.SetState(GameState.Playing);
            yield return new WaitForSeconds(2.5f);
            float tiltBack = model != null ? Quaternion.Angle(model.localRotation, rest) : 0f;
            _report.AppendLine($"- 정비 화면: 물 {refitWater:0.00} · 구멍 {refitHoles} · 2.5초 뒤 Model 기운 각 {tiltBack:0.0}° · 속력 ×{ship.FloodSpeedMultiplier:0.00}");
            if (refitWater > 0.001f || refitHoles > 0) Fail("정비 화면에 들어가도 물 · 구멍이 남음");
            if (ship.FloodSpeedMultiplier < 0.999f) Fail("물을 다 빼도 속력이 돌아오지 않음");

            foreach (var m in modules) if (m != null) m.enabled = true;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
