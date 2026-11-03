#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>-polishOnly: 전투 기능 · 바다 후속 묶음(2026-11-01) 확인.</summary>
        public bool PolishOnly;

        /// <summary>
        /// 1) 함교 파괴 → 사격통제 저하  2) CIWS 구조물 받침 → 앞쪽도 쏨  3) 곡사포 선회 표적 예측  4) 보스 부위 파괴
        /// 5) 나머지 블록 강화 데이터  6) 배 흔들림이 파도를 따름(적 포함)  7) 선체 둘레 거품  8) 섬 거품 고리가 해안 물빛과 겹치지 않음.
        /// </summary>
        private IEnumerator PolishCheck()
        {
            _report.AppendLine("\n## 전투 기능 · 바다 후속");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(2f);

            // 2) CIWS: 1단 그대로 — 상부 구조물(함교)에 가린 방향은 쏘지 못하는 것이 규칙(2026-11-03 사용자: 받침으로 올리지 않는다).
            //    시작 배치에서 함교 바로 뒤 CIWS는 앞쪽을 못 막고 뒤쪽은 막는지 본다.
            CiwsModule ciws = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is CiwsModule c) { ciws = c; break; }
            if (ciws != null)
            {
                Vector3 ahead = ship.transform.position + ship.transform.forward * 30f + Vector3.up * 1.2f;
                Vector3 astern = ship.transform.position - ship.transform.forward * 30f + Vector3.up * 1.2f;
                bool front = ciws.CoversPoint(ahead), back = ciws.CoversPoint(astern);
                _report.AppendLine($"- CIWS(1단, 함교에 가린 방향은 사격 금지): 앞쪽 30m 덮음 {Yes(front)} · 뒤쪽 30m 덮음 {Yes(back)}");
                if (!back) Fail("CIWS가 트인 뒤쪽도 덮지 못함");
                yield return CloseShot("polish_ciws", ciws.transform, 9f);
            }
            else _report.AppendLine("- CIWS: 시작 배치에 없음 — 건너뜀");

            // 1) 함교 파괴
            ModuleInstance bridge = null; WeaponModuleProbe(ship, out var gun);
            foreach (var m in ship.Grid.Modules) if (m?.Definition != null && m.Definition.Type == ModuleType.Bridge) { bridge = m; break; }
            if (bridge != null && gun != null)
            {
                var sys = ship.Systems;
                float reload0 = gun.Stats.ReloadTime, range0 = gun.Stats.Range, det0 = sys.DetectionRange;
                int track0 = sys.MaxTrackedTargets;
                bridge.TakeDamage(99999f);
                yield return null;
                float reload1 = gun.Stats.ReloadTime, range1 = gun.Stats.Range, det1 = sys.DetectionRange;
                _report.AppendLine($"- 함교 파괴: 사격통제 저하 {Yes(sys.BridgeDown)} · {gun.Definition.DisplayName} 재장전 {reload0:0.00} → {reload1:0.00}초 · 사거리 {range0:0} → {range1:0} · 탐지 {det0:0} → {det1:0} · 동시 추적 {track0} → {sys.MaxTrackedTargets}");
                if (!sys.BridgeDown || reload1 <= reload0 || range1 >= range0) Fail("함교가 부서져도 사격통제가 떨어지지 않음");
            }
            else _report.AppendLine("- 함교 파괴: 함교 또는 무장이 없어 건너뜀");

            // 3) 곡사포 선회 표적 예측(수식 확인): 반경 18m를 8m/s로 도는 표적, 사수에서 40m
            {
                // 유니티 Y 회전은 위에서 볼 때 시계 방향이 + — 반시계로 도는 표적은 선회율이 음수
                float r = 18f, v = 8f, yaw = -v / r * Mathf.Rad2Deg;
                Vector3 shooter = new(0f, 0f, -24f), centre = Vector3.zero;
                Vector3 pos = centre + new Vector3(r, 0f, 0f), vel = new Vector3(0f, 0f, v);
                Ballistics.SolveLateralIntercept(shooter, pos, vel, 15f, 1.4f, out var straight, out float tS);
                Vector3 turning = straight; float tT = tS;
                for (int i = 0; i < 4; i++) { turning = Ballistics.PredictTurning(pos, vel, yaw, tT); tT = Mathf.Max(1.4f, (turning - shooter).magnitude / 15f); }
                turning = Ballistics.PredictTurning(pos, vel, yaw, tT);
                Vector3 Truth(float t) { float a = v / r * t; return centre + new Vector3(r * Mathf.Cos(a), 0f, r * Mathf.Sin(a)); }
                float errS = (straight - Truth(tS)).magnitude, errT = (turning - Truth(tT)).magnitude;
                _report.AppendLine($"- 곡사포 예측(반경 18m를 8m/s로 도는 표적, 사수 30m): 직선 예측 오차 {errS:0.0}m → 선회 유지 예측 {errT:0.0}m (착탄 반경 4m)");
                if (errT > 1.5f || errT >= errS) Fail("선회 표적 예측이 직선 예측보다 낫지 않음");
            }

            // 4) 보스 부위 파괴: 네 보스(1·2·3스테이지 + WWII 순양함)를 멈춰 세우고 첫 부위 자리에 피해, 보스 막대의 부위 줄도 본다
            var ww2Ruleset = Resources.Load<Game.Data.RulesetDefinition>("Rulesets/ww2_v1");
            foreach (var id in new[] { "ene_boss", "ene_boss_corvette", "ene_boss2", "ww2_boss_gunnery_cruiser" })
            {
                CombatDevTools.ClearBattlefield();
                yield return null;
                var def = CombatDevTools.FindEnemy(id);
                if (def == null && ww2Ruleset != null)
                    foreach (var e in ww2Ruleset.Enemies) if (e != null && e.Id == id) def = e;
                Vector3 at = ship.transform.position + ship.transform.forward * 45f;
                var boss = def != null ? EnemySpawner.Instance.SpawnAt(def, at, Quaternion.identity) : null;
                if (boss == null && def != null && def.Prefab != null)
                {
                    // 스포너는 지금 규칙(현대)에 없는 WWII 적을 막는다 — 검사에서만 풀에서 바로 꺼내 맞춘다
                    var go = PoolManager.Instance?.Spawn(def.Prefab, at, Quaternion.identity);
                    if (go != null && go.TryGetComponent<EnemyController>(out var direct)) { direct.Setup(def, ship.transform); direct.transform.SetPositionAndRotation(at, Quaternion.identity); boss = direct; }
                }
                if (boss == null) { _report.AppendLine($"- 보스 부위({id}): 생성 실패 — 건너뜀"); continue; }
                boss.DevFrozen = true;
                yield return null;
                BossParts parts = boss is IBossParts bp ? bp.Parts : null;
                if (parts == null || parts.Parts.Count == 0) { Fail($"{def.DisplayName}에 부위가 없음"); continue; }
                // 보스 막대: 부위 줄이 생기는가(막대는 0.25초마다 보스를 찾는다)
                var bar = Object.FindFirstObjectByType<Game.UI.BossBarUI>();
                yield return new WaitForSeconds(0.6f);
                int chips = bar != null && bar.Boss == boss ? bar.PartChips : -1;
                var first = parts.Parts[0];
                float hp0 = boss.CurrentHp / Mathf.Max(1f, def.MaxHp);
                int hits = 0;
                while (!first.Destroyed && hits < 60 && boss.IsAlive)
                {
                    boss.TakeDamage(new DamageInfo(first.MaxHp * 0.25f, first.Anchor.position + Vector3.up * 0.3f, Vector3.down, DamageSource.Gun, "검증"));
                    hits++;
                }
                var sb = new StringBuilder();
                foreach (var p in parts.Parts) sb.Append($"{p.Name} {p.MaxHp:0}{(p.Destroyed ? "(파괴)" : "")} · ");
                _report.AppendLine($"- 보스 부위({def.DisplayName}): {sb}첫 부위 {hits}번에 파괴 {Yes(first.Destroyed)} · 선체 {hp0:P0} → {boss.CurrentHp / Mathf.Max(1f, def.MaxHp):P0}(살아 있음 {Yes(boss.IsAlive)})");
                if (!first.Destroyed) Fail($"{def.DisplayName}의 {first.Name}이 부서지지 않음");
                if (!boss.IsAlive) Fail("부위만 노렸는데 보스가 격침됨");
                yield return new WaitForSeconds(0.6f);
                string barText = bar != null ? bar.PartsText : "";
                _report.AppendLine($"  - 보스 막대 부위 줄: 칸 {chips}/{parts.Parts.Count} · \"{barText}\"");
                if (chips != parts.Parts.Count) Fail($"{def.DisplayName}: 보스 막대에 부위 줄이 없거나 칸 수가 다름");
                if (!barText.Contains(first.Name + " 파괴")) Fail("보스 막대가 부서진 부위를 표시하지 않음");
                yield return Shot($"polish_boss_bar_{id}");
                yield return CloseShot($"polish_boss_part_{id}", first.Anchor, 12f);
                // 광역 피해(맞은 자리 없음)는 부위를 깎지 않는다
                var second = parts.Parts.Count > 1 ? parts.Parts[1] : null;
                if (second != null)
                {
                    float before = second.Hp;
                    boss.TakeDamage(new DamageInfo(10f, Vector3.zero, Vector3.down, DamageSource.Gun, "검증"));
                    if (!Mathf.Approximately(before, second.Hp)) Fail("맞은 자리 없는 피해가 부위를 깎음");
                }
                if (boss.IsAlive) boss.RemoveWithoutReward();   // 풀에서 바로 꺼낸 보스도 치운다
            }
            CombatDevTools.ClearBattlefield();

            // 5) 블록 강화 데이터: 모든 강화 데이터가 블록 수치를 실제로 바꾸는가
            {
                var sb = new StringBuilder();
                int count = 0;
                foreach (var prof in Resources.LoadAll<ModuleUpgradeProfile>("Upgrades"))
                {
                    if (prof.Module == null) { Fail($"{prof.name}: 대상 블록 없음"); continue; }
                    var a = ModuleUpgrades.Compute(prof.Module, 0); var b = ModuleUpgrades.Compute(prof.Module, 2);
                    bool changed = !Mathf.Approximately(a.Damage, b.Damage) || !Mathf.Approximately(a.Range, b.Range) || !Mathf.Approximately(a.ReloadTime, b.ReloadTime)
                                   || a.MagazineCapacity != b.MagazineCapacity || !Mathf.Approximately(a.AmmoReloadTime, b.AmmoReloadTime)
                                   || !Mathf.Approximately(a.DetectionRange, b.DetectionRange) || !Mathf.Approximately(a.TurretTurnRate, b.TurretTurnRate)
                                   || !Mathf.Approximately(a.SortieCooldown, b.SortieCooldown) || !Mathf.Approximately(a.ReloadBonus, b.ReloadBonus)
                                   || prof.Module.Type == ModuleType.Howitzer || prof.Module.Type == ModuleType.RamBow;
                    count++;
                    sb.Append($"{prof.Module.DisplayName}{(changed ? "" : "(변화 없음)")} · ");
                    if (!changed) Fail($"{prof.name}: 강화 II가 수치를 바꾸지 않음");
                }
                _report.AppendLine($"- 블록 강화 데이터 {count}종: {sb}");
                if (count < 17) Fail("블록 강화 데이터가 17종보다 적음");
            }

            // 6) 배 흔들림: 파도 계산이 끝난 뒤 기함·적 모두 파도를 따라 움직이는가
            for (float w = 0f; w < 8f && !Game.View.OceanWaveTexture.HeightReady; w += 0.25f) yield return new WaitForSeconds(0.25f);
            var sway = ship.GetComponentInChildren<Game.View.ShipVisualSway>();
            var boat = CombatDevTools.SpawnRing("ene_fastboat", 1, 24f, 40f);
            if (boat.Count > 0 && boat[0] is EnemyController eb) eb.DevFrozen = true;
            float maxTilt = 0f, maxBob = 0f, enemyTilt = 0f;
            for (float t = 0f; t < 4f; t += 0.1f)
            {
                yield return new WaitForSeconds(0.1f);
                if (sway != null) { var m = sway.WaveMotion; maxTilt = Mathf.Max(maxTilt, Mathf.Abs(m.y), Mathf.Abs(m.z)); maxBob = Mathf.Max(maxBob, Mathf.Abs(m.x)); }
                if (boat.Count > 0 && boat[0] is Component bc && bc.transform.Find("ModelPivot")?.GetComponent<Game.View.ShipVisualSway>() is { } es)
                    enemyTilt = Mathf.Max(enemyTilt, Mathf.Abs(es.WaveMotion.y), Mathf.Abs(es.WaveMotion.z));
            }
            _report.AppendLine($"- 배 흔들림(파도 따라, 4초): 기함 오르내림 최대 {maxBob:0.00}m · 기울기 최대 {maxTilt:0.0}° · 적 고속정 기울기 최대 {enemyTilt:0.0}°");
            if (!Game.View.OceanWaveTexture.HeightReady) Fail("파도 높이를 CPU에서 읽을 수 없음");
            if (sway == null || maxTilt < 0.2f) Fail("기함 흔들림이 파도를 따르지 않음");
            if (enemyTilt < 0.2f) Fail("적 수상함에 파도 흔들림이 없음");

            // 7-1) 호위함 파도 흔들림(2026-11-03)
            var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
            if (fleet != null)
            {
                if (fleet.EscortCount == 0) fleet.Deploy();
                yield return new WaitForSeconds(3f);
                float escortTilt = 0f;
                var esc = fleet.EscortCount > 0 ? fleet.GetEscort(0) : null;
                var prop = esc != null ? esc.GetType().GetProperty("WaveMotion") : null;
                for (float t = 0f; t < 3f && prop != null; t += 0.1f)
                {
                    yield return new WaitForSeconds(0.1f);
                    var m = (Vector3)prop.GetValue(esc);
                    escortTilt = Mathf.Max(escortTilt, Mathf.Abs(m.y), Mathf.Abs(m.z));
                }
                _report.AppendLine($"- 호위함 파도 흔들림(3초): 기울기 최대 {escortTilt:0.0}°");
                if (escortTilt < 0.2f) Fail("호위함에 파도 흔들림이 없음");
            }
            CombatDevTools.ClearBattlefield();

            // 7) 선체 둘레 거품: 정지 → 전속
            var wake = ship.GetComponent<Game.View.ShipWake>();
            float idle = wake != null ? wake.HullFoamStrength : 0f;
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(6f);
            float full = wake != null ? wake.HullFoamStrength : 0f;
            bool hasFoam = ship.transform.Find("Hull foam") != null;
            _report.AppendLine($"- 선체 둘레 거품: 있음 {Yes(hasFoam)} · 정지 세기 {idle:0.00} → 전속 {full:0.00}");
            if (!hasFoam || full <= idle + 0.2f) Fail("선체 둘레 거품이 없거나 속력에 따라 짙어지지 않음");
            Time.timeScale = 0f;
            yield return CloseShot("polish_hull_foam", ship.transform, 14f);
            Time.timeScale = TimeScale;
            ship.SetEngineOrder(0f);

            // 8) 섬 거품 고리: 해안 물빛이 켜져 있으면 예전 거품 고리 메시가 없다
            int rings = 0, islands = 0;
            foreach (var i in Game.World.Islands.All)
            {
                if (i.Root == null) continue;
                islands++;
                foreach (Transform c in i.Root.transform) if (c.name == "Shore foam") rings++;
            }
            _report.AppendLine($"- 섬 거품 고리: 해안 물빛 {(Game.View.ShoreWaterMask.Enabled ? "켬" : "끔")} · 섬 {islands}개 중 예전 고리 {rings}개");
            if (Game.View.ShoreWaterMask.Enabled && rings > 0) Fail("해안 물빛이 켜져 있는데 예전 거품 고리가 남음(겹침)");
        }

        /// <summary>함교 파괴 시험에 쓸 무장 하나(기관포 우선).</summary>
        private static void WeaponModuleProbe(Game.Ship.ShipController ship, out ModuleRuntime gun)
        {
            gun = null;
            foreach (var m in ship.Grid.Modules)
                if (m?.Runtime != null && m.Definition != null && RunUpgrades.IsWeapon(m.Definition.Type) && m.Definition.Type != ModuleType.Ciws) { gun = m.Runtime; return; }
        }
    }
}
#endif
