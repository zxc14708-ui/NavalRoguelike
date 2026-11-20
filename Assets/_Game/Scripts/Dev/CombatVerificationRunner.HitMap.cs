#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 블록 피격 분포 · 파괴 측정(2026-11-21, 사용자: 적 미사일 · 드론이 함교만 공격 · 블록이 부서지지 않음).</summary>
    public partial class CombatVerificationRunner
    {
        private struct HitRow
        {
            public string Phase;
            public int Wave;
            public DamageResolver.HitRecord R;
            public Vector3 CellWorld, ShipCenter, ShipRight;
            public float HpAfter, Time;
            public int Id;
        }

        /// <summary>
        /// 1) 측정만(피해 없음): 사방 미사일 일제사격 · 한쪽 옆면 미사일 · 자폭 드론 · 고속정/초계함 포격 · 어뢰정 — 칸별로 몇 번 맞는지.
        /// 2) 실제 피해(선체는 버티게): 미사일 · 드론 · 포격으로 블록이 몇 개 부서지는지, 부서진 칸 피격이 피해를 어디로 보내는지.
        /// 기준: 함교 피격 비율 ≤ 함교 칸 비율 + 5%p · 옆면 공격의 90% 이상이 다가온 쪽 절반 · 일제사격 칸 중복 평균 ≤ 0.5 ·
        ///       실제 피해에서 함교 말고 부서진 블록 ≥ 3 · "블록이 하나도 안 받고 선체만 받은 피격" ≤ 15%.
        /// </summary>
        private IEnumerator HitMapCheck()
        {
            _report.AppendLine("\n## 블록 피격 분포 · 파괴 측정(2026-11-21)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            // 시작 배치(6칸, 함교가 절반)는 너무 작다 — 실제 판 중반처럼 무장을 붙여 폭과 길이가 있는 배로 잰다
            int added = CombatDevTools.InstallTestLoadout();
            foreach (var id in new[] { "mod_ciws", "mod_ciws", "mod_autocannon", "mod_gun76", "mod_decoy" })
                if (CombatDevTools.InstallModule(id)) added++;
            yield return null;
            var modules = ship.GetComponentsInChildren<ModuleRuntime>(true);
            foreach (var m in modules) m.enabled = false;   // 요격 · 기만 없이 맞는 위치만
            var grid = ship.Grid;
            if (grid == null) { Fail("기함 격자 없음"); yield break; }

            var cells = grid.OccupiedCells.ToList();
            int bridgeCells = cells.Count(c => IsBridge(grid.Get(c)));
            float bridgeRef = cells.Count > 0 ? (float)bridgeCells / cells.Count : 0f;
            int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X), minZ = cells.Min(c => c.Z), maxZ = cells.Max(c => c.Z);
            _report.AppendLine($"- 시험 기함: 블록 {added}개 추가 → 칸 {cells.Count}개(길이 {maxX - minX + 1} × 폭 {maxZ - minZ + 1}) · 함교 {bridgeCells}칸(비율 {bridgeRef:P0} — 고르게 맞으면 함교 피격도 이 정도)");

            var rows = new List<HitRow>();
            string phase = ""; int wave = 0;
            void OnHit(DamageResolver r, DamageResolver.HitRecord rec)
            {
                if (r.Grid != grid) return;
                rows.Add(new HitRow { Phase = phase, Wave = wave, R = rec, CellWorld = grid.CoordToWorld(rec.Cell),
                    ShipCenter = ship.transform.position, ShipRight = ship.transform.right,
                    HpAfter = rec.Module != null ? rec.Module.Hp : -1f, Time = UnityEngine.Time.time,
                    Id = rec.Module != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rec.Module) : 0 });
            }
            DamageResolver.Resolved += OnHit;
            var missileDef = CombatDevTools.FindEnemy("ene_missileboat");
            var missilePrefab = missileDef != null ? missileDef.MissilePrefab : null;

            // ---------------- 1) 측정만
            DamageResolver.DevMeasureOnly = true;
            phase = "volley";
            for (wave = 0; wave < 8; wave++)
            {
                CombatDevTools.MissileVolley(12, 60f, wave * 11f, 0.01f);
                yield return WaitMissilesGone(12f);
            }
            phase = "side";
            for (wave = 0; wave < 4; wave++)
            {
                FireFromSide(ship.transform, missilePrefab, 6, 55f, wave % 2 == 0 ? 1f : -1f);
                yield return WaitMissilesGone(12f);
            }
            phase = "drone"; wave = 0;
            yield return SpawnAndWait("ene_drone", 12, 50f, 0f, 30f);
            wave = 1;
            yield return SpawnAndWait("ene_drone", 12, 50f, 15f, 30f);
            phase = "gun"; wave = 0;
            CombatDevTools.SpawnRing("ene_fastboat", 3, 40f, 30f);
            CombatDevTools.SpawnRing("ene_pcc_corvette", 2, 46f, 200f);
            yield return new WaitForSeconds(18f);
            CombatDevTools.ClearBattlefield();
            phase = "torpedo";
            CombatDevTools.SpawnRing("ene_torpedo_boat", 2, 42f, 120f);
            yield return new WaitForSeconds(16f);
            CombatDevTools.ClearBattlefield();
            // 함교 저격(레이더 전파 추적) 6발: 작동 중인 함교 · 레이더 칸을 노리고, 쏘자마자 표식이 뜬다
            phase = "command"; wave = 0;
            int markers = 0;
            if (missilePrefab != null && PoolManager.Instance != null)
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    Vector3 pos = ship.transform.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * 60f; pos.y = 3f;
                    var go = PoolManager.Instance.Spawn(missilePrefab, pos, Quaternion.LookRotation((ship.transform.position - pos).normalized, Vector3.up));
                    if (go != null && go.TryGetComponent<Missile>(out var m)) { m.Launch(ship.transform, 0.01f); m.TargetCommand(); }
                }
            yield return null;
            markers = Game.View.SeekerMarker.ActiveCount;
            yield return new WaitForSeconds(1.2f);
            yield return ScreenShot("hitmap_command_marker");
            yield return WaitMissilesGone(12f);
            DamageResolver.DevMeasureOnly = false;
            yield return new WaitForSeconds(0.5f);

            // ---------------- 2) 실제 피해(선체는 버티게)
            // 이만큼 선체가 깎이면 실제로는 격침 — 그 전까지만 따로 센다(검사 환경은 이미 선체를 크게 올려 두므로 함종 데이터 값)
            float hullLimit = ship.Hull != null ? ship.Hull.HullHp : ship.Config != null ? ship.Config.HullMaxHp : 300f;
            MakeInvulnerable(ship);
            // 검사기가 0.25초마다 블록을 가득 채우는 도우미(KeepModulesHealthy)를 이 단계에서만 멈춘다 — 실제 게임에는 없는 수리
            s_pauseModuleHealing = true;
            phase = "real";
            // 모듈 합계 내구 — 끝에 받은 피해와 비교해 수리된 양을 추정한다
            float hpBefore = grid.Modules.Where(m => m != null).Sum(m => m.Hp);
            for (wave = 0; wave < 8; wave++)
            {
                CombatDevTools.MissileVolley(12, 60f, wave * 17f);
                yield return WaitMissilesGone(12f);
            }
            wave = 6;
            yield return SpawnAndWait("ene_drone", 12, 50f, 40f, 30f);
            wave = 7;
            CombatDevTools.SpawnRing("ene_fastboat", 3, 40f, 80f);
            yield return new WaitForSeconds(14f);
            CombatDevTools.ClearBattlefield();
            DamageResolver.Resolved -= OnHit;
            float hpAfter = grid.Modules.Where(m => m != null).Sum(m => m.Hp);

            // 부서진 블록 겉모습: 그을림 · 기울기 · 연기
            var wreck = ship.GetComponent<Game.View.ModuleWreckFx>();
            var broken = grid.Modules.Where(m => m != null && m.IsDestroyed).ToList();
            int shown = wreck != null ? broken.Count(m => wreck.IsShown(m)) : 0;
            int tilted = wreck != null ? broken.Count(m => wreck.TiltOf(m) > 3f) : 0;
            var firstBroken = broken.FirstOrDefault(m => !IsBridge(m)) ?? broken.FirstOrDefault();
            if (firstBroken != null && firstBroken.Runtime != null) yield return CloseShot("hitmap_wreck_block", firstBroken.Runtime.transform, 9f);
            yield return ScreenShot("hitmap_wreck_view");

            // 정비 수리: 정비 화면에 들어가는 경로(RefitController.RestoreDestroyedBlocks)로 절반 내구 복구 · 겉모습 원래대로
            var refit = Object.FindFirstObjectByType<Game.Refit.RefitController>();
            var restoreMethod = typeof(Game.Refit.RefitController).GetMethod("RestoreDestroyedBlocks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (refit != null && restoreMethod != null) restoreMethod.Invoke(refit, null);
            yield return null;
            int stillBroken = grid.Modules.Count(m => m != null && m.IsDestroyed);
            var notHalf = broken.Where(m => Mathf.Abs(m.Hp - m.MaxHp * Game.Refit.RefitController.RefitRestoreFraction) > Mathf.Max(0.6f, m.MaxHp * 0.03f)).ToList();
            int halfHp = broken.Count - notHalf.Count;
            int stillShown = wreck != null ? wreck.Count : 0;

            // ---------------- 집계
            var measured = rows.Where(r => r.Phase != "real").ToList();
            var csv = new StringBuilder("phase,wave,source,cell_x,cell_z,module,was_destroyed,destroyed_now,to_module,to_hull,hp_after,instance,time\n");
            foreach (var r in rows)
                csv.AppendLine($"{r.Phase},{r.Wave},{r.R.Info.Source},{r.R.Cell.X},{r.R.Cell.Z},{(r.R.Module != null ? r.R.Module.Definition.Id : "")},{(r.R.WasDestroyed ? 1 : 0)},{(r.R.DestroyedNow ? 1 : 0)},{r.R.ToModule:0.##},{r.R.ToHull:0.##},{r.HpAfter:0.##},{r.Id},{r.Time:0.00}");
            File.WriteAllText(Path.Combine(OutputDirectory, "hitmap.csv"), csv.ToString(), new UTF8Encoding(false));

            _report.AppendLine("| 공격 | 맞음 | 함교 | 함교 비율 | 맞은 칸 종류 | 가장 많이 맞은 칸 |");
            _report.AppendLine("|---|---|---|---|---|---|");
            float worstBridge = 0f;
            foreach (var (label, key) in new[] { ("사방 미사일 일제사격", "volley"), ("한쪽 옆면 미사일", "side"), ("자폭 드론", "drone"), ("고속정 · 초계함 포격", "gun"), ("어뢰정", "torpedo") })
            {
                var set = measured.Where(r => r.Phase == key).ToList();
                if (set.Count == 0) { _report.AppendLine($"| {label} | 0 | - | - | - | - |"); continue; }
                int b = set.Count(r => IsBridge(r.R.Module));
                float share = (float)b / set.Count;
                if (key != "torpedo") worstBridge = Mathf.Max(worstBridge, share);
                var top = set.GroupBy(r => r.R.Cell).OrderByDescending(g => g.Count()).First();
                string topName = grid.Get(top.Key) != null ? grid.Get(top.Key).Definition.DisplayName : "빈 칸";
                _report.AppendLine($"| {label} | {set.Count} | {b} | {share:P0} | {set.Select(r => r.R.Cell).Distinct().Count()} | ({top.Key.X},{top.Key.Z}) {topName} {top.Count()}회 |");
            }

            // 옆면: 다가온 쪽에 맞았나 — 다가온 쪽 절반이거나, 그 줄에서 다가온 쪽으로 더 바깥 칸이 없는 칸(가운데 줄만 있는 함수 · 함미)
            // 미사일만(다른 사격이 섞이지 않게). 다가온 방향은 터질 때 미사일의 진행 방향 반대 — 배가 돌아도 그때의 쏜 쪽
            var side = measured.Where(r => r.Phase == "side" && r.R.Info.Source == DamageSource.Missile).ToList();
            int facing = 0;
            float size = Mathf.Max(0.01f, grid.CellSize);
            foreach (var r in side)
            {
                Vector3 from = -r.R.Info.Direction; from.y = 0f;
                if (from.sqrMagnitude < 1e-4f) continue;
                Vector3 d = grid.transform.InverseTransformDirection(from.normalized); d.y = 0f; d.Normalize();
                Vector3 perp = new(-d.z, 0f, d.x);
                Vector3 p = grid.CoordToLocal(r.R.Cell); p.y = 0f;
                bool outer = !cells.Any(c => { Vector3 q = grid.CoordToLocal(c); q.y = 0f;
                    return Mathf.RoundToInt(Vector3.Dot(q, perp) / size) == Mathf.RoundToInt(Vector3.Dot(p, perp) / size) && Vector3.Dot(q, d) > Vector3.Dot(p, d) + 0.01f; });
                if (outer || Vector3.Dot(r.CellWorld - r.ShipCenter, from) > 0.05f) facing++;
            }
            float facingShare = side.Count > 0 ? (float)facing / side.Count : 0f;
            // 일제사격 칸 중복: 같은 일제사격에서 이미 맞은 칸에 또 맞은 수
            var volley = measured.Where(r => r.Phase == "volley" && r.R.Info.Source == DamageSource.Missile).GroupBy(r => r.Wave).ToList();
            float dupAvg = volley.Count > 0 ? (float)volley.Average(g => g.Count() - g.Select(r => r.R.Cell).Distinct().Count()) : 0f;
            _report.AppendLine($"- 옆면 공격이 다가온 쪽(바깥 칸)에 맞음 {facing}/{side.Count}({facingShare:P0}) · 사방 일제사격(12발) 한 번에 같은 칸 중복 평균 {dupAvg:0.0}발");

            // 실제 피해
            var real = rows.Where(r => r.Phase == "real").ToList();
            var destroyed = real.Where(r => r.R.DestroyedNow).Select(r => r.R.Module).Distinct().ToList();
            // 기준은 "실제라면 아직 떠 있는 동안"(선체 누적 피해 < 원래 최대 선체)만 본다 — 그 뒤는 블록이 거의 다 부서져 옆 블록이 없다
            var alive = new List<HitRow>();
            float hullSum = 0f;
            foreach (var r in real) { if (hullSum >= hullLimit) break; alive.Add(r); hullSum += r.R.ToHull; }
            var aliveDestroyed = alive.Where(r => r.R.DestroyedNow).Select(r => r.R.Module).Distinct().ToList();
            // 긴 교전(실제 피해 단계 전체 — 실제로는 선체 자동 회복 · 정비 수리 사이에 쌓이는 블록 피해)에서 함교 말고 부서진 블록
            int nonBridgeDestroyed = destroyed.Count(m => !IsBridge(m));
            int hullOnly = alive.Count(r => r.R.ToModule <= 0.0001f);
            float hullOnlyShare = alive.Count > 0 ? (float)hullOnly / alive.Count : 0f;
            float aliveModule = alive.Sum(r => r.R.ToModule), aliveHull = alive.Sum(r => r.R.ToHull);
            float moduleShare = aliveModule + aliveHull > 0f ? aliveModule / (aliveModule + aliveHull) : 0f;
            _report.AppendLine($"- 떠 있는 동안(선체 누적 {hullLimit:0}까지, 피격 {alive.Count}): 부서진 블록 {aliveDestroyed.Count}개 · 블록은 안 받고 선체만 받은 피격 {hullOnly}({hullOnlyShare:P0}) · 블록 피해 {aliveModule:0} / 선체 {aliveHull:0}(블록 몫 {moduleShare:P0}) — 고치기 전에는 함교가 부서진 뒤 선체가 거의 다 받았다(기준선 선체 몫 약 88%)");
            float toModule = real.Sum(r => r.R.ToModule), toHull = real.Sum(r => r.R.ToHull);
            float repaired = Mathf.Max(0f, hpAfter - (hpBefore - toModule));   // 부서진 블록의 넘친 피해만큼 적게 잡힌다(대략)
            _report.AppendLine($"- 실제 피해: 피격 {real.Count} · 부서진 블록 {destroyed.Count}개({string.Join(", ", destroyed.Select(m => $"{m.Definition.DisplayName}({m.Origin.X},{m.Origin.Z})"))}) · " +
                               $"블록은 안 받고 선체만 받은 피격 {real.Count(r => r.R.ToModule <= 0.0001f)}(블록이 거의 다 부서진 뒤 포함) · 이미 부서진 칸 피격 {real.Count(r => r.R.WasDestroyed)} · 블록이 받은 피해 {toModule:0} / 선체 {toHull:0} · 그동안 수리된 블록 내구 약 {repaired:0}");

            // 함교 저격
            var cmd = measured.Where(r => r.Phase == "command" && r.R.Info.Source == DamageSource.Missile).ToList();
            int cmdOnCommand = cmd.Count(r => r.R.Module != null && (r.R.Module.Definition.Type == ModuleType.Bridge || r.R.Module.Definition.Type == ModuleType.Radar));
            float cmdShare = cmd.Count > 0 ? (float)cmdOnCommand / cmd.Count : 0f;
            _report.AppendLine($"- 함교 저격 6발: 맞음 {cmd.Count} · 함교 · 레이더에 {cmdOnCommand}({cmdShare:P0}) · 쏘자마자 뜬 표식 {markers}개");
            _report.AppendLine($"- 부서진 블록 겉모습: {broken.Count}개 중 그을림 {shown} · 기울어짐 {tilted} · 사진 hitmap_wreck_block.png");
            _report.AppendLine($"- 정비 수리: 정비 화면 경로로 복구 후 부서진 블록 {stillBroken}개 · 절반 내구 {halfHp}/{broken.Count}{(notHalf.Count > 0 ? $"(아님: {string.Join(", ", notHalf.Select(m => $"{m.Definition.DisplayName} {m.Hp:0.#}/{m.MaxHp:0.#}"))})" : "")} · 남은 그을림 {stillShown}");

            DrawHeatmap(grid, cells, measured, "hitmap_measured.png");
            DrawHeatmap(grid, cells, real, "hitmap_real.png");
            _report.AppendLine("- 원자료 hitmap.csv · 칸별 그림 hitmap_measured.png(측정) · hitmap_real.png(실제 피해, 부서진 블록 검은 테두리)");

            if (worstBridge > bridgeRef + 0.05f) Fail($"함교에 몰림: 공격 종류 중 함교 피격 비율 최대 {worstBridge:P0}(기준 {bridgeRef + 0.05f:P0} 이하)");
            if (side.Count > 0 && facingShare < 0.9f) Fail($"옆면 공격이 다가온 쪽에 맞지 않음({facingShare:P0}, 기준 90%)");
            if (dupAvg > 0.5f) Fail($"일제사격이 같은 칸에 겹침(평균 {dupAvg:0.0}발, 기준 0.5 이하)");
            if (nonBridgeDestroyed < 3) Fail($"긴 교전에서 함교 말고 부서진 블록이 {nonBridgeDestroyed}개(기준 3개 이상)");
            if (alive.Count > 0 && moduleShare < 0.45f) Fail($"떠 있는 동안 블록이 받은 피해 몫이 {moduleShare:P0}(기준 45% 이상 — 블록이 피해를 나눠 받아야 한다)");
            if (hullOnlyShare > 0.15f) Fail($"블록이 하나도 안 받고 선체만 받은 피격 {hullOnlyShare:P0}(기준 15% 이하)");
            if (cmd.Count > 0 && cmdShare < 0.9f) Fail($"함교 저격이 함교 · 레이더에 맞지 않음({cmdShare:P0}, 기준 90%)");
            if (markers < 1) Fail("함교 저격 표식이 뜨지 않음");
            if (broken.Count > 0 && (shown < broken.Count || tilted < broken.Count)) Fail("부서진 블록이 그을리거나 기울지 않음");
            if (broken.Count > 0 && (stillBroken > 0 || halfHp < broken.Count || stillShown > 0)) Fail("정비 수리 후에도 부서진 블록 · 그을림이 남음(또는 절반 내구가 아님)");

            s_pauseModuleHealing = false;
            foreach (var m in modules) if (m != null) m.enabled = true;
            ship.DevRudderOverride = null;
        }

        private static bool IsBridge(ModuleInstance m) => m != null && m.Definition != null && m.Definition.Type == ModuleType.Bridge;

        private static IEnumerator WaitMissilesGone(float maxSeconds)
        {
            float until = Time.time + maxSeconds;
            yield return new WaitForSeconds(0.5f);
            while (Time.time < until && Object.FindObjectsByType<Missile>(FindObjectsSortMode.None).Any(m => m.isActiveAndEnabled))
                yield return new WaitForSeconds(0.25f);
            yield return new WaitForSeconds(0.3f);
        }

        private static IEnumerator SpawnAndWait(string id, int count, float radius, float angle, float maxSeconds)
        {
            var list = CombatDevTools.SpawnRing(id, count, radius, angle);
            float until = Time.time + maxSeconds;
            while (Time.time < until && list.Any(e => e != null && e.isActiveAndEnabled && e.IsAlive))
                yield return new WaitForSeconds(0.25f);
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>기함 오른쪽(sign 1) 또는 왼쪽(−1)에서만 미사일 n발(정횡 ±25°).</summary>
        private static void FireFromSide(Transform player, GameObject prefab, int n, float radius, float sign)
        {
            if (prefab == null || player == null || PoolManager.Instance == null) return;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-25f, 25f, n > 1 ? i / (n - 1f) : 0.5f);
                Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * (player.right * sign);
                Vector3 pos = player.position + dir * radius; pos.y = 3f;
                var go = PoolManager.Instance.Spawn(prefab, pos, Quaternion.LookRotation((player.position - pos).normalized, Vector3.up));
                if (go != null && go.TryGetComponent<Missile>(out var m)) m.Launch(player, 0.01f);
            }
        }

        /// <summary>칸별 피격 수 그림(선수 오른쪽 · 위가 우현). 많이 맞을수록 붉게, 함교는 흰 테두리, 부서진 블록은 검은 테두리.</summary>
        private void DrawHeatmap(ShipGrid grid, List<GridCoord> cells, List<HitRow> set, string file)
        {
            if (cells.Count == 0) return;
            int minX = cells.Min(c => c.X), maxX = cells.Max(c => c.X), minZ = cells.Min(c => c.Z), maxZ = cells.Max(c => c.Z);
            const int px = 40, pad = 8;
            int w = (maxX - minX + 1) * px + pad * 2, h = (maxZ - minZ + 1) * px + pad * 2;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var bg = new Color(0.08f, 0.1f, 0.13f);
            var fill = Enumerable.Repeat(bg, w * h).ToArray();
            tex.SetPixels(fill);
            var counts = set.GroupBy(r => r.R.Cell).ToDictionary(g => g.Key, g => g.Count());
            int max = counts.Count > 0 ? counts.Values.Max() : 1;
            foreach (var c in cells)
            {
                counts.TryGetValue(c, out int n);
                float t = Mathf.Sqrt((float)n / max);
                Color col = n == 0 ? new Color(0.22f, 0.26f, 0.3f) : Color.Lerp(new Color(0.95f, 0.85f, 0.3f), new Color(0.85f, 0.1f, 0.08f), t);
                var m = grid.Get(c);
                Color edge = m != null && m.IsDestroyed ? Color.black : IsBridge(m) ? Color.white : new Color(0.1f, 0.12f, 0.15f);
                int x0 = pad + (c.X - minX) * px, y0 = pad + (c.Z - minZ) * px;
                for (int y = 0; y < px; y++)
                    for (int x = 0; x < px; x++)
                    {
                        bool border = x < 3 || y < 3 || x >= px - 3 || y >= px - 3;
                        tex.SetPixel(x0 + x, y0 + y, border ? edge : col);
                    }
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, file), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
#endif
