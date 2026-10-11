#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;
using Game.UI;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — Codex 시안 적용(2026-10-11): 플레이어 헬기데크·대잠 헬기 T1~T3, 노봉 v22(-aviationOnly).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 헬기데크: 강화 2단계, 단계마다 데크·주기 헬기 외형이 바뀌고 착함점·주기 헬기·로터가 켜진 외형에 다시 붙는지,
        ///    출격 간격·속력·수색 반경·무장 피해·내구, 모델 방향(로터 위·기수 앞·헬기가 데크 위에 앉음), 강화 카드 문구
        /// 2) 강화 II 데크가 실제로 T3 헬기를 띄우고 피해 배율이 붙는지, 비행 헬기 로터 방향
        /// 3) 노봉 v22: 단계마다 외형·좌우 포구(Muzzle_01·02)·쉬는 방향(포구가 블록 앞쪽)
        /// </summary>
        private IEnumerator AviationCheck()
        {
            _report.AppendLine("\n## Codex 시안 적용 — 헬기데크·헬기 T1~T3·노봉 v22");
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1f);
            if (grid == null || factory == null) { Fail("격자·ModuleFactory 없음"); yield break; }

            ModuleInstance Install(string id)
            {
                var def = CombatDevTools.FindModule(id);
                if (def == null) return null;
                float bestD = float.MaxValue;
                GridCoord pick = default;
                for (int x = -grid.MaxHalfLength; x <= grid.MaxHalfLength; x++)
                    for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++)
                    {
                        var c = new GridCoord(x, z);
                        if (!grid.CanPlace(def, c, 0, out _)) continue;
                        float d = (x + 3) * (x + 3) + z * z;   // 함미 쪽(데크 자리)부터
                        if (d < bestD) { bestD = d; pick = c; }
                    }
                return bestD < float.MaxValue ? factory.Install(def, pick, 0) : null;
            }

            // ---- 1) 헬기데크 단계별
            var deck = Install("mod_helideck");
            if (deck?.Runtime is not HelicopterDeckModule hd) { Fail("헬기데크 설치 실패"); yield break; }
            hd.enabled = false;   // 단계 확인 중에는 출격하지 않게
            var vis = hd.GetComponent<ModuleUpgradeVisuals>();
            var levels = vis != null ? Get<GameObject[]>(vis, "levels") : null;
            int maxLevel = ModuleUpgrades.MaxLevel(deck.Definition);
            _report.AppendLine($"- 헬기데크 ({deck.Origin.X},{deck.Origin.Z}): 강화 최대 {maxLevel}단계 · 외형 {(levels != null ? levels.Length : 0)}단계");
            if (maxLevel != 2) Fail("헬기데크 강화 데이터가 2단계가 아님");
            if (levels == null || levels.Length != 3) { Fail("헬기데크 외형 Visual_Level1~3이 없음"); yield break; }
            string card0 = ModuleCardText.BuildUpgradeChoice(deck.Definition, 0), card1 = ModuleCardText.BuildUpgradeChoice(deck.Definition, 1);

            float[] cool = { 12f, 9.6f, 7.8f }, speed = { 18f, 19.8f, 21.6f }, radius = { 60f, 69f, 78f }, dmg = { 1f, 1.3f, 1.6f }, hp = { 50f, 62.5f, 75f };
            for (int level = 0; level <= 2; level++)
            {
                if (level > 0 && !deck.ApplyUpgrade()) { Fail($"헬기데크 강화 {level} 실패"); break; }
                yield return null;
                var s = deck.EffectiveStats;
                int active = vis.ActiveIndex;
                var spot = Get<Transform>(hd, "landingSpot");
                var parked = Get<GameObject>(hd, "parkedHelicopter");
                var rotor = Get<Transform>(hd, "parkedRotor");
                bool bound = spot != null && parked != null && rotor != null && spot.IsChildOf(levels[level].transform) &&
                             parked.transform.IsChildOf(levels[level].transform) && parked.activeInHierarchy;
                string prefab = hd.CurrentHelicopterPrefab != null ? hd.CurrentHelicopterPrefab.name : "-";

                // 모델 방향: 로터 축이 위, 기수(ChinEOHousing)가 로터보다 블록 앞쪽, 주기 헬기 바닥이 착함점 높이
                bool upright = rotor != null && Vector3.Dot(hd.ParkedRotorAxisWorld, Vector3.up) > 0.95f;
                var nose = parked != null ? FindChildDeep(parked.transform, "ChinEOHousing") : null;
                bool noseFront = nose != null && rotor != null && Vector3.Dot(nose.position - rotor.position, hd.transform.forward) > 0.05f;
                float seat = parked != null && spot != null ? RendererBottom(parked.transform) - spot.position.y : 99f;
                _report.AppendLine($"  - 단계 {level}: 외형 {(active >= 0 ? levels[active].name : "없음")} · 부품 연결 {Yes(bound)} · 출격 헬기 {prefab} · " +
                                   $"간격 {s.SortieCooldown:0.##}초 · 속력 {s.HelicopterSpeed:0.##} · 수색 {hd.SearchRadius:0.#}m · 피해 ×{hd.DamageScale:0.##} · 내구 {deck.Hp:0.#}/{deck.MaxHp:0.#} · " +
                                   $"로터 위 {Yes(upright)} · 기수 앞 {Yes(noseFront)} · 앉은 높이 {seat:+0.00;-0.00}m");
                if (active != level) Fail($"헬기데크 단계 {level}에서 외형 {level + 1}만 켜지지 않음");
                if (!bound) Fail($"헬기데크 단계 {level}: 착함점·주기 헬기·로터가 켜진 외형에 붙지 않음");
                if (prefab != $"HEL_PlayerAsw_T{level + 1}") Fail($"헬기데크 단계 {level} 출격 헬기 {prefab} ≠ HEL_PlayerAsw_T{level + 1}");
                if (!Near(s.SortieCooldown, cool[level], 0.01f) || !Near(s.HelicopterSpeed, speed[level], 0.01f) || !Near(hd.SearchRadius, radius[level], 0.01f) ||
                    !Near(hd.DamageScale, dmg[level], 0.01f) || !Near(deck.MaxHp, hp[level], 0.01f))
                    Fail($"헬기데크 단계 {level} 수치가 설계와 다름");
                if (!upright || !noseFront) Fail($"헬기데크 단계 {level}: 주기 헬기 방향이 틀림");
                if (Mathf.Abs(seat) > 0.12f) Fail($"헬기데크 단계 {level}: 주기 헬기가 데크에 앉지 않음({seat:0.00}m)");
                yield return CloseShot($"aviation_deck_L{level}", hd.transform, 4.5f);
            }
            _report.AppendLine($"  - 강화 카드 I: {Flat(card0)}");
            _report.AppendLine($"  - 강화 카드 II: {Flat(card1)}");
            if (!card0.Contains("출격") && !card0.Contains("헬기")) Fail("헬기데크 강화 카드 문구가 비어 있음");

            // ---- 2) 강화 II 출격: T3 헬기, 피해 배율, 비행 로터 방향
            hd.enabled = true;
            hd.SetPolicy(HelicopterPolicy.AntiSubPatrol);
            AswHelicopter flying = null;
            float t0 = Time.realtimeSinceStartup;
            while (flying == null && Time.realtimeSinceStartup - t0 < 20f)
            {
                foreach (var h in Object.FindObjectsByType<AswHelicopter>(FindObjectsSortMode.None))
                    if (h.isActiveAndEnabled && h.name.StartsWith("HEL_PlayerAsw")) { flying = h; break; }
                yield return null;
            }
            if (flying != null)
            {
                yield return new WaitForSeconds(1.5f);
                bool fUp = Vector3.Dot(flying.RotorAxisWorld, flying.transform.up) > 0.95f;
                bool parkedHidden = Get<GameObject>(hd, "parkedHelicopter") is { } p && !p.activeInHierarchy;
                _report.AppendLine($"- 강화 II 출격: {flying.name} · 공격 피해 {flying.AttackDamage:0.#} (기본 40 × 1.6) · 비행 로터 위 {Yes(fUp)} · 출격 중 주기 헬기 숨김 {Yes(parkedHidden)}");
                if (!flying.name.StartsWith("HEL_PlayerAsw_T3")) Fail($"강화 II 데크가 T3 헬기를 띄우지 않음({flying.name})");
                if (!Near(flying.AttackDamage, 64f, 0.01f)) Fail($"강화 II 헬기 피해 {flying.AttackDamage} ≠ 64");
                if (!fUp) Fail("비행 헬기 로터 축이 위가 아님");
                if (!parkedHidden) Fail("출격 중에도 주기 헬기가 보임");
                yield return CloseShot("aviation_flying_T3", flying.transform, 6f);
            }
            else Fail("헬기데크가 20초 안에 출격하지 않음");
            hd.SetPolicy(HelicopterPolicy.Automatic);
            CombatDevTools.RemoveModule(deck);
            yield return null;

            // ---- 3) 노봉 v22
            var nb = Install("mod_nobong");
            if (nb?.Runtime is NobongModule gun)
            {
                gun.enabled = false;
                var nvis = gun.GetComponent<ModuleUpgradeVisuals>();
                var nlevels = nvis != null ? Get<GameObject[]>(nvis, "levels") : null;
                for (int level = 0; level <= 2 && nlevels != null; level++)
                {
                    if (level > 0 && !nb.ApplyUpgrade()) { Fail($"노봉 강화 {level} 실패"); break; }
                    yield return null;
                    var a = Get<Transform>(gun, "_muzzleA");
                    var b = Get<Transform>(gun, "_muzzleB");
                    var wc = Get<WeaponController>(gun, "weapon");
                    var muzzle = wc != null ? wc.Muzzle : null;
                    var turret = muzzle != null ? FindChildDeep(nlevels[level].transform, "TurretPivot") : null;
                    bool twin = a != null && b != null && a.IsChildOf(nlevels[level].transform) && b.IsChildOf(nlevels[level].transform);
                    bool front = muzzle != null && turret != null && Vector3.Dot((muzzle.position - turret.position).normalized, gun.transform.forward) > 0.7f;
                    float spread = twin ? Vector3.Distance(a.position, b.position) : 0f;
                    _report.AppendLine($"- 노봉 단계 {level}: 외형 {(nvis.ActiveIndex >= 0 ? nlevels[nvis.ActiveIndex].name : "없음")} · 좌우 포구 {Yes(twin)} (간격 {spread:0.00}m) · 포구가 앞쪽 {Yes(front)}");
                    if (nvis.ActiveIndex != level) Fail($"노봉 단계 {level} 외형이 바뀌지 않음");
                    if (!twin) Fail($"노봉 단계 {level}: 좌우 포구 Muzzle_01·02를 찾지 못함");
                    if (!front) Fail($"노봉 단계 {level}: 쉬는 자세의 포구가 블록 앞쪽이 아님");
                    yield return CloseShot($"aviation_nobong_L{level}", gun.transform, 4f);
                }
                CombatDevTools.RemoveModule(nb);
            }
            else Fail("노봉 설치 실패");

            foreach (var b in off) if (b != null) b.enabled = true;
        }

        private static Transform FindChildDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root) { var f = FindChildDeep(c, name); if (f != null) return f; }
            return null;
        }

        private static float RendererBottom(Transform root)
        {
            float min = float.MaxValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) min = Mathf.Min(min, r.bounds.min.y);
            return min == float.MaxValue ? 0f : min;
        }
    }
}
#endif
