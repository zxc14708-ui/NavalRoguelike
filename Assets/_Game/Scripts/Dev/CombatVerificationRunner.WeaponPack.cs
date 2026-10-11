#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>자동 전투 검증 — 노봉·곡사포·경어뢰 발사관·충각 함수 강화(2026-10-11).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 네 블록: 강화 데이터 2단계, 외형 Visual_Level1~3이 단계마다 바뀌고 조준·발사 부품이 켜진 외형에 다시 붙는지,
        ///    단계별 수치(피해·탄창·사거리·발사 간격·내구·반동·포탄 수), 강화 카드 문구. 단계마다 가까이서 찍는다.
        /// 2) 노봉: 좌우 포신이 번갈아 한 발씩(발 간격 ≈ 발사 간격 ÷ 2), 한 발에 탄 1.
        /// 3) 곡사포 강화 II: 한 번에 3발, 착탄점이 벌어지고, 탄은 1발만 쓴다.
        /// </summary>
        private IEnumerator WeaponPackCheck()
        {
            _report.AppendLine("\n## 무기 강화(노봉·곡사포·경어뢰·충각)");
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            yield return new WaitForSeconds(1.5f);
            if (grid == null || factory == null) { Fail("격자·ModuleFactory 없음"); yield break; }
            Resources.LoadAll<ModuleDefinition>("Modules");

            ModuleInstance Install(string id)
            {
                var def = CombatDevTools.FindModule(id);
                if (def == null) return null;
                ModuleInstance best = null;
                float bestD = float.MaxValue;
                GridCoord pick = default;
                for (int x = -grid.MaxHalfLength; x <= grid.MaxHalfLength; x++)
                    for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++)
                    {
                        var c = new GridCoord(x, z);
                        if (!grid.CanPlace(def, c, 0, out _)) continue;
                        float d = x * x + z * z;
                        if (d < bestD) { bestD = d; pick = c; best = null; }
                    }
                if (bestD < float.MaxValue) best = factory.Install(def, pick, 0);
                return best;
            }

            // ---- 1) 단계별 외형·부품·수치
            var expect = new Dictionary<string, (float[] dmg, float[] range, float[] reload, int[] mag, float[] hp)>
            {
                ["mod_nobong"] = (new[] { 3.5f, 4.375f, 5.25f }, new[] { 28f, 28f, 32.2f }, null, new[] { 96, 120, 144 }, null),
                ["mod_howitzer"] = (new[] { 18f, 18f, 18f }, new[] { 44f, 44f, 50.6f }, null, null, null),
                ["mod_torpedotube"] = (new[] { 15f, 18f, 21f }, new[] { 45f, 45f, 51.75f }, new[] { 10f, 8.5f, 7.5f }, null, null),
                ["mod_rambow"] = (new[] { 40f, 50f, 60f }, null, null, null, new[] { 70f, 91f, 105f }),
            };
            ModuleInstance nobong = null, howitzer = null;
            foreach (var kv in expect)
            {
                var m = Install(kv.Key);
                if (m == null || m.Runtime == null) { Fail($"{kv.Key} 설치 실패"); continue; }
                if (kv.Key == "mod_nobong") nobong = m;
                if (kv.Key == "mod_howitzer") howitzer = m;
                var rt = m.Runtime;
                rt.enabled = false;
                var vis = rt.GetComponent<ModuleUpgradeVisuals>();
                var levels = vis != null ? Get<GameObject[]>(vis, "levels") : null;
                int maxLevel = ModuleUpgrades.MaxLevel(m.Definition);
                _report.AppendLine($"- {m.Definition.DisplayName} ({m.Origin.X},{m.Origin.Z}): 강화 최대 {maxLevel}단계 · 외형 {(levels != null ? levels.Length : 0)}단계");
                if (maxLevel != 2) Fail($"{m.Definition.DisplayName} 강화 데이터가 2단계가 아님");
                if (levels == null || levels.Length != 3 || levels[0] == null || levels[1] == null || levels[2] == null)
                { Fail($"{m.Definition.DisplayName} 외형 Visual_Level1~3이 없음"); continue; }

                string card0 = ModuleCardText.BuildUpgradeChoice(m.Definition, 0), card1 = ModuleCardText.BuildUpgradeChoice(m.Definition, 1);
                for (int level = 0; level <= 2; level++)
                {
                    if (level > 0 && !m.ApplyUpgrade()) { Fail($"{m.Definition.DisplayName} 강화 {level} 실패"); break; }
                    yield return null;
                    var s = m.EffectiveStats;
                    int active = -1;
                    for (int i = 0; i < 3; i++) if (levels[i].activeSelf) active = (active == -1 ? i : -2);
                    Transform part = rt switch
                    {
                        NobongModule => Get<WeaponController>(rt, "weapon")?.Muzzle,
                        HowitzerModule => Get<Transform>(rt, "muzzle"),
                        TorpedoTubeModule => Get<Transform[]>(rt, "launchPoints") is { Length: > 0 } lp ? lp[0] : null,
                        _ => levels[level].transform,
                    };
                    bool bound = part != null && part.IsChildOf(levels[level].transform) && part.gameObject.activeInHierarchy;
                    string extra = rt switch
                    {
                        HowitzerModule h => $" · 한 번에 {h.ShellsPerVolley}발",
                        RamBowModule r => $" · 반동 {r.SelfDamageRatio * 100f:0}%",
                        _ => "",
                    };
                    _report.AppendLine($"  - 단계 {level}: 켜진 외형 {(active >= 0 ? levels[active].name : "여럿/없음")} · 부품 연결 {Yes(bound)} ({(part != null ? part.name : "-")}) · " +
                                       $"피해 {s.Damage:0.###} · 사거리 {s.Range:0.##} · 간격 {s.ReloadTime:0.###} · 탄창 {s.MagazineCapacity} · 내구 {m.Hp:0}/{m.MaxHp:0}{extra}");
                    if (active != level) Fail($"{m.Definition.DisplayName} 단계 {level}에서 외형 {level + 1}만 켜지지 않음");
                    if (!bound) Fail($"{m.Definition.DisplayName} 단계 {level}: 조준·발사 부품이 켜진 외형에 붙지 않음");
                    var e = kv.Value;
                    if (e.dmg != null && !Near(s.Damage, e.dmg[level], 0.01f)) Fail($"{m.Definition.DisplayName} 단계 {level} 피해 {s.Damage} ≠ {e.dmg[level]}");
                    if (e.range != null && !Near(s.Range, e.range[level], 0.01f)) Fail($"{m.Definition.DisplayName} 단계 {level} 사거리 {s.Range} ≠ {e.range[level]}");
                    if (e.reload != null && !Near(s.ReloadTime, e.reload[level], 0.01f)) Fail($"{m.Definition.DisplayName} 단계 {level} 간격 {s.ReloadTime} ≠ {e.reload[level]}");
                    if (e.mag != null && s.MagazineCapacity != e.mag[level]) Fail($"{m.Definition.DisplayName} 단계 {level} 탄창 {s.MagazineCapacity} ≠ {e.mag[level]}");
                    if (e.hp != null && (!Near(m.MaxHp, e.hp[level], 0.01f) || !Near(m.Hp, m.MaxHp, 0.01f))) Fail($"{m.Definition.DisplayName} 단계 {level} 내구 {m.Hp}/{m.MaxHp} ≠ {e.hp[level]}(가득)");
                    if (rt is HowitzerModule hz && hz.ShellsPerVolley != level + 1) Fail($"곡사포 단계 {level} 포탄 {hz.ShellsPerVolley}발 ≠ {level + 1}");
                    if (rt is RamBowModule rb && !Near(rb.SelfDamageRatio, level == 2 ? 0.1f : 0.25f, 0.01f)) Fail($"충각 단계 {level} 반동 {rb.SelfDamageRatio}");
                    yield return CloseShot($"weaponpack_{m.Definition.Id}_L{level}", rt.transform, 5.5f);
                }
                _report.AppendLine($"  - 강화 카드 I: {Flat(card0)}");
                _report.AppendLine($"  - 강화 카드 II: {Flat(card1)}");
                if (rt is HowitzerModule && !card0.Contains("한 번에")) Fail("곡사포 강화 카드에 포탄 수가 없음");
                if (rt is RamBowModule && (!card0.Contains("내구") || !card1.Contains("반동"))) Fail("충각 강화 카드에 내구·반동이 없음");
            }

            // ---- 2) 노봉 좌우 교대 사격(기본 단계로 새로 설치)
            if (nobong != null) CombatDevTools.RemoveModule(nobong);
            yield return null;
            var nb = Install("mod_nobong");
            if (nb?.Runtime is NobongModule gun)
            {
                gun.enabled = false;
                Vector3 dir = gun.transform.position - ship.transform.position; dir.y = 0f;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : ship.transform.right;
                // 맞아도 쉽게 가라앉지 않는 초계함(고속정은 4발에 가라앉아 사격이 끊겼다)
                var boat = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette"), gun.transform.position + dir * 18f, Quaternion.identity);
                if (boat != null) { boat.DevFrozen = true; boat.transform.position = gun.transform.position + dir * 18f; }
                gun.Ammo.Refill();
                int ammo0 = gun.Ammo.Current, s0 = gun.Salvos, seen = gun.Salvos, used = 0, fired = 0;
                var barrels = new List<int>();
                var times = new List<float>();
                gun.enabled = true;
                float t0 = Time.time;
                while (Time.time - t0 < 6f && barrels.Count < 8)
                {
                    if (gun.Salvos != seen)
                    {
                        seen = gun.Salvos;
                        barrels.Add(gun.LastBarrel);
                        times.Add(Time.time);
                    }
                    yield return null;
                }
                // 탄 사용은 8발 직후(4초 보급 전)에 잰다
                fired = gun.Salvos - s0;
                used = ammo0 - gun.Ammo.Current;
                yield return CloseShot("weaponpack_nobong_alternate", gun.transform, 6f);
                gun.enabled = false;
                bool alternate = barrels.Count >= 4;
                for (int i = 1; alternate && i < barrels.Count; i++) alternate = barrels[i] == -barrels[i - 1];
                var gaps = new List<float>();
                for (int i = 1; i < times.Count; i++) gaps.Add(times[i] - times[i - 1]);
                gaps.Sort();
                float gap = gaps.Count > 0 ? gaps[gaps.Count / 2] : 0f;
                float want = gun.Stats.ReloadTime * 0.5f;
                _report.AppendLine($"- 노봉 사격: {fired}발 · 포신 순서 {string.Join(" ", barrels.ConvertAll(b => b < 0 ? "좌" : "우"))} · 발 간격(중앙값) {gap:0.###}초(기대 {want:0.###}, 탄약고 지원 시 더 짧음) · 탄 사용 {used}");
                if (!alternate) Fail("노봉이 좌우 번갈아 쏘지 않음");
                if (gap > want * 1.25f || gap < want * 0.5f) Fail("노봉 발 간격이 발사 간격의 절반이 아님");
                if (used > fired + 1 || used < fired - 1) Fail("노봉이 한 발에 탄 1발을 쓰지 않음");
                CombatDevTools.ClearBattlefield();
            }
            else Fail("노봉 재설치 실패");

            // ---- 3) 곡사포 강화 II 3발
            if (howitzer?.Runtime is HowitzerModule hw)
            {
                Vector3 dir = hw.transform.position - ship.transform.position; dir.y = 0f;
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : ship.transform.right;
                var boats = new List<EnemyController>();
                foreach (float side in new[] { -2f, 0f, 2f })
                {
                    Vector3 p = hw.transform.position + dir * 30f + Vector3.Cross(Vector3.up, dir) * side;
                    var e = EnemySpawner.Instance.SpawnAt(CombatDevTools.FindEnemy("ene_fastboat"), p, Quaternion.identity);
                    if (e != null) { e.DevFrozen = true; e.transform.position = p; boats.Add(e); }
                }
                hw.Ammo.Refill();
                int v0 = hw.Volleys, sh0 = hw.Shots, a0 = hw.Ammo.Current;
                hw.enabled = true;
                float t0 = Time.time;
                while (Time.time - t0 < 8f && hw.Volleys == v0) yield return null;
                yield return new WaitForSeconds(0.6f);
                hw.enabled = false;
                yield return CloseShot("weaponpack_howitzer_volley", hw.transform, 7f);
                int shells = hw.Shots - sh0, used = a0 - hw.Ammo.Current;
                float spread = 0f;
                for (int i = 0; i < hw.LastVolley.Count; i++)
                    for (int j = i + 1; j < hw.LastVolley.Count; j++) spread = Mathf.Max(spread, Vector3.Distance(hw.LastVolley[i], hw.LastVolley[j]));
                _report.AppendLine($"- 곡사포 강화 II 사격: 한 번에 {shells}발 · 착탄점 {hw.LastVolley.Count}곳(가장 먼 두 점 {spread:0.0} m) · 탄 사용 {used}");
                if (hw.Volleys == v0) Fail("곡사포가 쏘지 않음");
                else
                {
                    if (shells != 3 || hw.LastVolley.Count != 3) Fail("곡사포 강화 II가 한 번에 3발을 쏘지 않음");
                    if (spread < 2f) Fail("곡사포 착탄점이 벌어지지 않음");
                    if (used != 1) Fail("곡사포가 한 번에 탄 1발만 쓰지 않음");
                }
                yield return new WaitForSeconds(3f);
                CombatDevTools.ClearBattlefield();
            }

            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
        }

        private static string Flat(string rich)
            => System.Text.RegularExpressions.Regex.Replace(rich, "<[^>]+>", "").Replace("\n", " / ");
    }
}
#endif
