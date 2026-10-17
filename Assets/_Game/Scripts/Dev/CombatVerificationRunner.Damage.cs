#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 피격 칸 판정(블록 내구가 실제로 피해를 받는지).</summary>
    public partial class CombatVerificationRunner
    {
        // ------------------------------------------------------------ 피격 칸 판정

        /// <summary>
        /// 피격 칸 판정(2026-10-09): 맞은 지점이 블록 칸으로 잡히는지 본다.
        /// 1) 표본 — 사방에서 수평으로 쏜 탄(판정 상자 표면), 근접 폭발(표면 밖 0.3~1.4m), 표면 최근접점(드론·자폭 보트)을
        ///    예전 판정(반올림한 칸)과 지금 판정(<see cref="DamageResolver.HitCell"/>)으로 각각 센다.
        /// 2) 실전 — 고속정·미사일정·초계함과 미사일 일제사격에 맞으며 전투 기록의 피격 칸이 "빈 칸"(선체만)인 비율을 센다.
        /// </summary>
        private IEnumerator BlockHitCheck()
        {
            _report.AppendLine("\n## 피격 칸 판정(블록 내구)");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);

            var ship = GameManager.Instance.Player;
            var grid = ship != null ? ship.Grid : null;
            var resolver = ship != null ? ship.GetComponent<DamageResolver>() : null;
            var box = ship != null ? ship.GetComponent<BoxCollider>() : null;
            if (grid == null || resolver == null || box == null) { Fail("함선 격자·피해 분배·판정 상자를 찾지 못함"); yield break; }

            grid.GetExtent(out var min, out var max);
            int cells = grid.OccupiedCells.Count, rect = (max.X - min.X + 1) * (max.Z - min.Z + 1);
            _report.AppendLine($"- 블록 {grid.Modules.Count}개 · 칸 {cells}개 / 경계 사각형 {max.X - min.X + 1}×{max.Z - min.Z + 1} = {rect}칸 · 판정 상자 {box.size.x:0.#}×{box.size.z:0.#} m");

            // 1) 표본
            Random.InitState(20261009);
            var b = box.bounds;
            int direct = 0, oldDirect = 0, newDirect = 0, prox = 0, oldProx = 0, newProx = 0, surf = 0, oldSurf = 0, newSurf = 0;
            float worstReach = 0f;
            bool OldHits(Vector3 p) => grid.Get(grid.WorldToNearestCoord(p)) != null;
            bool NewHits(Vector3 p)
            {
                var c = resolver.HitCell(p);
                if (grid.Get(c) == null) return false;
                var l = grid.transform.InverseTransformPoint(p);
                var q = grid.CoordToLocal(c);
                float half = grid.CellSize * 0.5f;
                worstReach = Mathf.Max(worstReach, new Vector2(Mathf.Max(0f, Mathf.Abs(l.x - q.x) - half), Mathf.Max(0f, Mathf.Abs(l.z - q.z) - half)).magnitude);
                return true;
            }
            for (int i = 0; i < 720; i++)
            {
                Vector3 outDir = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                Vector3 aim = b.center + new Vector3(Random.Range(-b.extents.x, b.extents.x) * 0.9f, 0f, Random.Range(-b.extents.z, b.extents.z) * 0.9f);
                var ray = new Ray(aim + outDir * 80f, -outDir);
                if (box.Raycast(ray, out var hit, 200f))
                {
                    direct++;
                    if (OldHits(hit.point)) oldDirect++;
                    if (NewHits(hit.point)) newDirect++;
                    Vector3 p = hit.point - ray.direction * Random.Range(0.3f, 1.4f);
                    prox++;
                    if (OldHits(p)) oldProx++;
                    if (NewHits(p)) newProx++;
                }
                Vector3 s = box.ClosestPoint(b.center + outDir * (b.extents.magnitude + Random.Range(1f, 6f)));
                surf++;
                if (OldHits(s)) oldSurf++;
                if (NewHits(s)) newSurf++;
            }
            string Rate(int n, int of) => of > 0 ? $"{n * 100f / of:0}%" : "-";
            _report.AppendLine($"- 표본 직사 탄(표면) {direct}발: 블록 칸 예전 {Rate(oldDirect, direct)} → 지금 {Rate(newDirect, direct)}");
            _report.AppendLine($"- 표본 근접 폭발(표면 밖 0.3~1.4m) {prox}발: 예전 {Rate(oldProx, prox)} → 지금 {Rate(newProx, prox)}");
            _report.AppendLine($"- 표본 표면 최근접점(드론·자폭 보트) {surf}발: 예전 {Rate(oldSurf, surf)} → 지금 {Rate(newSurf, surf)}");
            _report.AppendLine($"- 지금 판정이 고른 칸과 맞은 지점의 최대 거리 {worstReach:0.00} m");
            if (newDirect < direct || newSurf < surf) Fail("표면에 맞은 탄이 블록 칸으로 잡히지 않음");
            if (newProx < prox) Fail("근접 폭발이 블록 칸으로 잡히지 않음");

            // 2) 실전
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            void SpawnAttackers()
            {
                CombatDevTools.SpawnRing("ene_fastboat", 3, 30f, 0f);
                CombatDevTools.SpawnRing("ene_missileboat", 2, 38f, 60f);
                CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 40f, 200f);
            }
            SpawnAttackers();
            float since = Time.time, start = Time.time, nextVolley = Time.time;
            var tally = new SortedDictionary<string, (int hits, int module, int empty)>();
            var hitModules = new HashSet<string>();
            while (Time.time - start < 50f)
            {
                ship.RepairHull(ship.HullMaxHp);
                if (Time.time >= nextVolley) { CombatDevTools.MissileVolley(4, 40f, Random.value * 90f); nextVolley = Time.time + 10f; }
                if (CountAliveEnemies() < 3) SpawnAttackers();
                foreach (var e in CombatLog.Entries)
                {
                    if (e.Time <= since || e.Category != "피격") continue;
                    string src = e.Message.Split(' ')[0];
                    tally.TryGetValue(src, out var t);
                    t.hits++;
                    if (e.Message.Contains("빈 칸")) t.empty++;
                    else
                    {
                        t.module++;
                        int at = e.Message.IndexOf(") ");
                        if (at >= 0) hitModules.Add(e.Message.Substring(at + 2).Split(' ')[0].Split('(')[0]);
                    }
                    tally[src] = t;
                }
                foreach (var e in CombatLog.Entries) since = Mathf.Max(since, e.Time);
                yield return null;
            }
            int all = 0, onModule = 0;
            foreach (var kv in tally)
            {
                all += kv.Value.hits;
                onModule += kv.Value.module;
                _report.AppendLine($"- 실전 {kv.Key}: {kv.Value.hits}발 중 블록 칸 {kv.Value.module} · 빈 칸(선체만) {kv.Value.empty}");
            }
            _report.AppendLine($"- 실전 합계 {all}발 중 블록 칸 {Rate(onModule, all)} · 맞은 블록 종류 {hitModules.Count}개: {string.Join(", ", hitModules)}");
            if (all < 10) Fail($"실전 피격이 너무 적음({all}발)");
            else if (onModule < all * 0.9f) Fail("실전 피격의 10% 넘게 빈 칸(선체만)으로 잡힘");

            CombatDevTools.ClearBattlefield();
            ship.RepairHull(ship.HullMaxHp);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
#endif
