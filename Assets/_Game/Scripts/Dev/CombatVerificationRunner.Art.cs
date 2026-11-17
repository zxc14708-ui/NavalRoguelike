#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;

namespace Game.Dev
{
    /// <summary>
    /// 시각 규칙 점검(2026-10-13 점검 G06, -artOnly). 게임 안에 실제로 띄운 크기로 기함·호위함(역할별 T0·T3)·수상 적·보스를 재서
    /// 길이·폭·수면 위 높이·흘수(수면 아래 깊이)를 표로 남기고, 규칙을 본다:
    ///   - 수상함은 물에 앉아 있어야 한다: 바닥이 수면(y −0.9) 위로 1m 넘게 뜨거나, 수면 위 몸체가 0.3m 안 되면 실패
    ///   - 수면선 일관성(기록): 흘수가 기함과 0.4m 넘게 다르면 "수면선 다름" — 첫 점검(2026-10-13)에서 기함 흘수 +0.29m에 비해
    ///     호위함·소형 적 대부분이 바닥이 수면 위 0.2~0.6m(전투 시점에서는 거의 안 보임). 모델을 고칠지는 아트 판단(CODEX_MODEL_REQUESTS.md)
    ///   - 크기 순서(기록): 호위함 ≤ 기함 × 1.1, 보스 ≥ 기함 — 어긋나면 표에 표시(실패로 세지 않음, 아트 판단)
    /// 적·보스는 기함 앞에 한 줄로 세워 대표 장면(art_lineup.png)을 찍는다. 측정은 MeshRenderer·SkinnedMeshRenderer만(효과·표식 제외).
    /// </summary>
    public partial class CombatVerificationRunner
    {
        [Tooltip("시각 규칙 점검만 돌린다(-artOnly): 기함·호위함·적·보스의 크기·수면선·흘수 표와 한 줄 대표 장면")]
        public bool ArtOnly;

        private const float WaterLevel = -0.9f;

        private struct HullSize
        {
            public string Name, Group;
            public float Length, Beam, Above, Draft;
            public bool Valid;
        }

        private static HullSize Measure(GameObject go, string name, string group)
        {
            var size = new HullSize { Name = name, Group = group };
            bool any = false;
            var b = new Bounds();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r.gameObject.layer == 5) continue;   // UI
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return size;
            // 길이·폭은 함의 앞·옆 방향 기준(돌아 있어도 같게) — 경계 상자의 앞뒤 방향 투영
            var f = go.transform.forward; f.y = 0f; f.Normalize();
            var right = new Vector3(f.z, 0f, -f.x);
            float Extent(Vector3 axis) => Mathf.Abs(axis.x) * b.size.x + Mathf.Abs(axis.z) * b.size.z;
            size.Length = Extent(f);
            size.Beam = Extent(right);
            size.Above = b.max.y - WaterLevel;
            size.Draft = WaterLevel - b.min.y;
            size.Valid = true;
            return size;
        }

        /// <summary>
        /// 절차형 선체(2026-11-01): 함수(Bow) · 함미 트랜섬(Stern) · 옆면 곡선(Sides)이 생겼는지, 트랜섬이 맨 뒤 블록 뒤로 나오는지,
        /// 옆면이 칸 경계 밖으로 벌어지되 지나치지 않는지 재고 함미·옆면을 찍는다.
        /// </summary>
        private IEnumerator HullShapeCheck(Game.Ship.ShipController ship)
        {
            var grid = ship.GetComponentInChildren<Game.Ship.ShipGrid>();
            Transform Part(string n)
            {
                foreach (var t in ship.GetComponentsInChildren<MeshFilter>())
                    if (t.name == n) return t.transform;
                return null;
            }
            var bow = Part("Bow");
            var stern = Part("Stern");
            var sides = Part("Sides");
            int minX = int.MaxValue, minZ = int.MaxValue, maxZ = int.MinValue;
            if (grid != null)
                foreach (var c in grid.OccupiedCells) { minX = Mathf.Min(minX, c.X); minZ = Mathf.Min(minZ, c.Z); maxZ = Mathf.Max(maxZ, c.Z); }
            float s = grid != null ? grid.CellSize : 2f;
            float rearEdge = (minX - 0.5f) * s;
            float sternBack = 0f, flare = 0f;
            int sideTris = 0;
            if (stern != null)
            {
                var b = stern.GetComponent<MeshFilter>().sharedMesh.bounds;
                sternBack = rearEdge - b.min.z;
            }
            if (sides != null)
            {
                var m = sides.GetComponent<MeshFilter>().sharedMesh;
                var b = m.bounds;
                flare = Mathf.Max(b.max.x - (maxZ + 0.5f) * s, (minZ - 0.5f) * s - b.min.x);
                sideTris = m.triangles.Length / 3;
            }
            _report.AppendLine($"- 절차형 선체: 함수 {(bow != null ? "있음" : "없음")} · 함미 트랜섬 {(stern != null ? $"맨 뒤 블록 뒤로 {sternBack:0.00}m" : "없음")} · 옆면 곡선 {(sides != null ? $"칸 경계 밖 최대 {flare:0.00}m, 삼각형 {sideTris}" : "없음")}");
            if (bow == null) Fail("함수 메시가 없음");
            if (stern == null || sternBack < 0.3f || sternBack > 1.2f) Fail("함미 트랜섬이 없거나 길이가 맞지 않음(0.3~1.2m)");
            if (Ruleset.Active == null || Ruleset.Active.HullSideSkirt == null)
            {
                if (sides == null || flare < 0.12f || flare > 0.4f) Fail("옆면 곡선이 없거나 벌어짐이 맞지 않음(0.12~0.4m)");
            }
            else if (sides != null) Fail("자체 뱃전 모델이 있는 규칙 집합인데 옆면 곡선이 덮음");
            if (stern != null)
            {
                var mark = new GameObject("Stern shot").transform;
                mark.position = stern.TransformPoint(new Vector3(0f, 0f, rearEdge));
                yield return CloseShot("hull_stern_transom", mark, 9f);
                mark.position = ship.transform.position + ship.transform.right * ((maxZ + 0.5f) * s);
                yield return CloseShot("hull_side_flare", mark, 11f);
                Destroy(mark.gameObject);
            }
        }

        private IEnumerator ArtCheck()
        {
            _report.AppendLine("\n## 시각 규칙 점검 (G06)");
            var sizes = new List<HullSize>();
            var gm = GameManager.Instance;
            var ship = gm != null ? gm.Player : null;
            if (ship == null) { Fail("기함 없음"); yield break; }
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            var fleet = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
            fleet?.ClearAll();   // 시작 호위정이 기함 크기에 섞이지 않게
            yield return new WaitForSeconds(1f);
            var flagship = Measure(ship.gameObject, ship.name, "기함");
            sizes.Add(flagship);
            yield return HullShapeCheck(ship);

            // 호위함: 역할 넷을 T0 → T3
            if (fleet != null)
            {
                var roles = Game.TaskForce.TaskForceEscortFormation.Roles;
                var indices = new List<int>();
                foreach (var role in roles)
                {
                    int i = fleet.Deploy();
                    if (i < 0) break;
                    fleet.AssignRole(i, role);
                    indices.Add(i);
                }
                yield return new WaitForSeconds(2f);
                MeasureEscorts(sizes, "T0");
                foreach (int i in indices)
                    for (int t = 0; t < Game.TaskForce.TaskForceEscortFormation.MaxUpgradeLevel; t++) fleet.Upgrade(i);
                yield return new WaitForSeconds(2f);
                MeasureEscorts(sizes, "T3");
            }

            // 수상 적·보스: 기함 앞에 한 줄로(간격은 크기에 맞춰)
            var spawner = EnemySpawner.Instance;
            var defs = new List<EnemyDefinition>();
            foreach (var (def, _) in Game.UI.CodexCatalog.CollectEnemies())
                if (def != null && def.Prefab != null && def.Kind == TargetKind.Surface && !defs.Contains(def)) defs.Add(def);
            var spawned = new List<(EnemyController e, EnemyDefinition def)>();
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var side = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 origin = ship.transform.position + fwd * 70f;
            float x = 0f;
            foreach (var def in defs)
            {
                var e = spawner != null ? spawner.SpawnAt(def, origin, Quaternion.LookRotation(side)) : null;
                if (e == null) continue;
                e.DevFrozen = true;
                spawned.Add((e, def));
            }
            yield return new WaitForSeconds(1.5f);   // 물 위 높이·모델이 자리 잡을 시간
            // 크기를 잰 뒤 길이만큼 띄워 나란히
            var measured = new List<HullSize>();
            foreach (var (e, def) in spawned)
            {
                if (e == null) continue;
                var m = Measure(e.gameObject, def.DisplayName, def.Rank == EnemyRank.Boss ? "보스" : def.Rank == EnemyRank.Elite ? "정예" : "적");
                measured.Add(m);
            }
            float total = 0f;
            foreach (var m in measured) total += m.Length + 4f;
            x = -total * 0.5f;
            for (int i = 0; i < spawned.Count && i < measured.Count; i++)
            {
                var e = spawned[i].e;
                if (e == null) continue;
                float half = measured[i].Length * 0.5f + 2f;
                x += half;
                var p = origin + side * x;
                p.y = e.transform.position.y;
                e.transform.position = p;
                x += half;
            }
            sizes.AddRange(measured);

            // 대표 장면: 기함 뒤 위에서 한 줄을 내려다본다
            var cam = Camera.main;
            if (cam != null)
            {
                var keepPos = cam.transform.position;
                var keepRot = cam.transform.rotation;
                var keepFov = cam.fieldOfView;
                var follow = cam.GetComponent<Game.View.QuarterViewCamera>();   // 기함을 따라가는 카메라만 잠시 멈춘다
                if (follow != null) follow.enabled = false;
                cam.transform.position = ship.transform.position - fwd * 40f + Vector3.up * 70f;
                cam.transform.LookAt(origin + Vector3.up * 2f);
                cam.fieldOfView = Mathf.Clamp(Mathf.Atan2(total * 0.55f, 110f) * Mathf.Rad2Deg * 2f, 30f, 80f);
                yield return null;
                yield return ScreenShot("art_lineup");
                if (follow != null) follow.enabled = true;
                cam.transform.SetPositionAndRotation(keepPos, keepRot);
                cam.fieldOfView = keepFov;
            }

            // 표 · 규칙
            var sb = new StringBuilder();
            sb.AppendLine("| 무리 | 이름 | 길이 | 폭 | 수면 위 높이 | 흘수 | 기함 대비 길이 | 판정 |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");
            int floating = 0, sunk = 0, order = 0, waterline = 0;
            var csv = new StringBuilder("group,name,length,beam,above_water,draft,length_vs_flagship,verdict\n");
            foreach (var s in sizes)
            {
                if (!s.Valid) { sb.AppendLine($"| {s.Group} | {s.Name} | — | — | — | — | — | 메시 없음 |"); continue; }
                float ratio = flagship.Length > 0f ? s.Length / flagship.Length : 0f;
                var notes = new List<string>();
                if (s.Draft < -1f) { notes.Add("**떠 있음**"); floating++; }
                else if (Mathf.Abs(s.Draft - flagship.Draft) > 0.4f) { notes.Add(s.Draft < flagship.Draft ? "수면선 다름(높게 뜸)" : "수면선 다름(깊게 잠김)"); waterline++; }
                if (s.Above < 0.3f) { notes.Add("**가라앉음**"); sunk++; }
                if (s.Group.StartsWith("호위") && ratio > 1.1f) { notes.Add("기함보다 큼"); order++; }
                if (s.Group == "보스" && ratio < 1f) { notes.Add("기함보다 작음"); order++; }
                string verdict = notes.Count == 0 ? "맞음" : string.Join(" · ", notes);
                sb.AppendLine($"| {s.Group} | {s.Name} | {s.Length:0.0} | {s.Beam:0.0} | {s.Above:0.0} | {s.Draft:0.00} | {ratio:0.00} | {verdict} |");
                csv.AppendLine(string.Join(",", s.Group, s.Name.Replace(",", " "), s.Length.ToString("0.00"), s.Beam.ToString("0.00"),
                    s.Above.ToString("0.00"), s.Draft.ToString("0.00"), ratio.ToString("0.00"), verdict.Replace(",", " ")));
            }
            _report.Append(sb);
            File.WriteAllText(Path.Combine(OutputDirectory, "art_sizes.csv"), csv.ToString(), new UTF8Encoding(false));
            _report.AppendLine($"- 수상함 {sizes.Count}척 · 떠 있음 {floating} · 가라앉음 {sunk} · 수면선이 기함(흘수 {flagship.Draft:0.00}m)과 다름 {waterline}(기록만) · 크기 순서 어긋남 {order}(기록만) · 원자료 art_sizes.csv · 대표 장면 art_lineup.png");
            if (floating > 0) Fail($"수면 위에 떠 있는 함정 {floating}척(G06 수면선)");
            if (sunk > 0) Fail($"수면 아래로 가라앉아 보이지 않는 함정 {sunk}척(G06 수면선)");

            foreach (var (e, _) in spawned) if (e != null) e.gameObject.SetActive(false);
            fleet?.ClearAll();
            ship.DevRudderOverride = null;
        }

        private static void MeasureEscorts(List<HullSize> sizes, string tier)
        {
            foreach (var m in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (m == null || m.GetType().Name != "EscortVessel") continue;
                var t = m.GetType();
                string name = t.GetProperty("DisplayName")?.GetValue(m) as string ?? m.name;
                sizes.Add(Measure(m.gameObject, $"{name} {tier}", "호위함"));
            }
        }
    }
}
#endif
