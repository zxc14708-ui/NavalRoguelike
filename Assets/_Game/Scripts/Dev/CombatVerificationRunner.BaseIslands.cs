#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.World;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// -baseIslandsOnly(2026-11-15): Codex IslandBiomePack_Refined 적용 —
        ///   1) 적 도색: 항공기 회색 동체 + 붉은 포인트, 항공전함 비행갑판 칠하지 않음
        ///   2) 적 기지 섬 소·중·대: 시설 수 · 미사일 포대 자리 · 해안선 반경 · 판정
        ///   3) 자연 섬: 모래섬(야자수·텐트·난파 보트) · 바위섬 · 절차형 섬 장식
        ///   4) 해안 미사일 포대: 다가가면 기지 섬에 서고, 발사관을 들어 올린 뒤 2발
        /// </summary>
        public bool BaseIslandsOnly;

        private IEnumerator BaseIslandsCheck()
        {
            _report.AppendLine("\n## Codex 섬 · 기지 팩(적 기지 섬 · 해안 미사일 포대 · 자연 장식 · 적 도색)");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            CoastalBatteryField.Disabled = true;
            SeaRegion.Enter(SeaKind.Archipelago, ship.transform.position, ship.transform.forward);
            yield return new WaitForSecondsRealtime(1f);

            // ---- 1) 적 도색
            static string Colors(Component c)
            {
                var sb = new StringBuilder();
                var seen = new HashSet<string>();
                foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m != null && Game.View.EnemyPaint.IsPainted(m) && seen.Add(m.name) && m.HasProperty("_BaseColor"))
                            sb.Append($"{m.name.Replace(" (enemy paint)", "")}=#{ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor"))} ");
                return sb.ToString();
            }
            var air = new List<EnemyController>();
            foreach (var id in new[] { "ene_fighter", "ene_drone", "ene_recon" })
            {
                var l = CombatDevTools.SpawnRing(id, 1, 30f, air.Count * 40f - 40f);
                foreach (var e in l) { e.DevFrozen = true; air.Add(e); }
            }
            yield return new WaitForSeconds(0.3f);
            bool airOk = air.Count == 3;
            foreach (var e in air)
            {
                bool hasDeck = false, hasRedAccent = false, greyBody = false, wrongBody = false;
                foreach (var r in e.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null || !Game.View.EnemyPaint.IsPainted(m) || !m.HasProperty("_BaseColor")) continue;
                        var c = m.GetColor("_BaseColor");
                        if (c == Game.View.EnemyPaint.Deck) hasDeck = true;
                        if (c == Game.View.EnemyPaint.AircraftAccent) hasRedAccent = true;
                        if (c == Game.View.EnemyPaint.AircraftGrey) greyBody = true;
                    }
                _report.AppendLine($"- 항공기 도색 {e.Definition.Id}: {Colors(e)}· 회색 동체 {Yes(greyBody)} · 붉은 포인트 {Yes(hasRedAccent)} · 적갈색 {Yes(hasDeck)}(기대 아니오)");
                if (hasDeck || !greyBody || wrongBody) airOk = false;
            }
            if (!airOk) Fail("적 항공기가 회색 동체 + 붉은 포인트가 아님(예전처럼 적갈색)");
            if (air.Count > 0) yield return CloseShot("paint_fighter", air[0].transform, 7f);
            if (air.Count > 1) yield return CloseShot("paint_drone", air[1].transform, 5f);
            if (air.Count > 2) yield return CloseShot("paint_recon", air[2].transform, 9f);
            CombatDevTools.ClearBattlefield();
            yield return null;
            var bossDef = CombatDevTools.FindEnemy("ene_boss2");
            var boss = bossDef != null ? EnemySpawner.Instance.SpawnAt(bossDef, ship.transform.position + ship.transform.forward * 40f, Quaternion.LookRotation(ship.transform.right)) : null;
            if (boss != null)
            {
                boss.DevFrozen = true;
                yield return new WaitForSeconds(0.3f);
                bool deckPainted = false, anyDeck = false;
                foreach (var r in boss.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.name.StartsWith("FlightDeck"))
                    {
                        anyDeck = true;
                        foreach (var m in r.sharedMaterials) if (Game.View.EnemyPaint.IsPainted(m)) deckPainted = true;
                    }
                _report.AppendLine($"- 항공전함 비행갑판: 찾음 {Yes(anyDeck)} · 칠함 {Yes(deckPainted)}(기대 아니오) · 칠한 재질 {Colors(boss)}");
                if (!anyDeck || deckPainted) Fail("항공전함 비행갑판이 회색으로 남지 않음");
                yield return CloseShot("paint_carrier_deck", boss.transform, 40f);
                boss.RemoveWithoutReward();
            }

            // ---- 2) 3) 섬 배치
            var field = IslandField.Instance;
            if (field == null) { Fail("IslandField 없음"); yield break; }
            field.ClearAll();
            Vector3 o = ship.transform.position; o.y = 0f;
            Vector3 f = ship.transform.forward; f.y = 0f; f.Normalize();
            Vector3 rt = Vector3.Cross(Vector3.up, f);
            var places = new (IslandBuilder.Kit kind, Vector3 at)[]
            {
                (IslandBuilder.Kit.BaseS, o + f * 95f - rt * 75f),
                (IslandBuilder.Kit.BaseM, o + f * 115f + rt * 70f),
                (IslandBuilder.Kit.BaseL, o + f * 250f),
                (IslandBuilder.Kit.SandIslet, o - f * 70f - rt * 60f),
                (IslandBuilder.Kit.RockIslet, o - f * 60f + rt * 55f),
            };
            var infos = new List<(IslandBuilder.Kit kind, IslandInfo info)>();
            foreach (var (kind, at) in places)
            {
                var info = field.PlaceKit(at, kind, 7);
                infos.Add((kind, info));
                if (info == null) { Fail($"키트 섬 {kind}를 만들지 못함(모델 없음?)"); continue; }
                int mil = 0, nat = 0;
                foreach (var t in info.Root.GetComponentsInChildren<KeepSharedMeshes>(true))
                {
                    if (t.name.StartsWith("MIL_")) mil++;
                    if (t.name.StartsWith("NAT_")) nat++;
                }
                int colliders = info.Root.GetComponentsInChildren<MeshCollider>(true).Length;
                _report.AppendLine($"- {kind}: 해안선 반경 {info.Radius:0.0}m · 판정 {colliders} · 시설 {mil} · 자연물 {nat} · 미사일 포대 자리 {(info.BatterySpots != null ? info.BatterySpots.Length : 0)} · 기지 {Yes(info.MilitaryBase)}");
                if (colliders == 0) Fail($"{kind} 섬 판정이 없음");
                if (IslandBuilder.IsBase(kind))
                {
                    int want = kind == IslandBuilder.Kit.BaseL ? 2 : 1;
                    if (!info.MilitaryBase || info.BatterySpots == null || info.BatterySpots.Length < want) Fail($"{kind} 기지 섬의 미사일 포대 자리가 {want}개보다 적음");
                    int minFac = kind == IslandBuilder.Kit.BaseS ? 8 : kind == IslandBuilder.Kit.BaseM ? 18 : 28;
                    if (mil < minFac) Fail($"{kind} 기지 섬 시설이 너무 적음({mil} < {minFac})");
                }
                if (kind == IslandBuilder.Kit.SandIslet && nat < 4) Fail("모래섬 장식(야자수·텐트·모닥불·유목)이 다 놓이지 않음");
            }
            // Codex 후속(2026-11-21): 부두 · 돌제 · 헬기 착륙장 · 탐조등
            foreach (var (kind, info) in infos)
            {
                if (info == null || !IslandBuilder.IsBase(kind)) continue;
                int pier = 0, breakwater = 0, pontoon = 0, lights = 0, pads = 0, upright = 0;
                foreach (var t in info.Root.GetComponentsInChildren<Transform>(true))
                {
                    // 2026-11-21 Codex 점검: 부두 · 돌제 · 부잔교는 수면에 누워야 한다(높이 < 가로 · 세로 중 긴 쪽의 60%) — 예전에는 해안에 수직으로 섰다
                    if (t.name.StartsWith("Coast ENV_"))
                    {
                        Bounds pb = default; bool anyR = false;
                        foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { if (!anyR) { pb = r.bounds; anyR = true; } else pb.Encapsulate(r.bounds); }
                        if (anyR && pb.size.y > 0.6f * Mathf.Max(pb.size.x, pb.size.z)) upright++;
                    }
                    if (t.name == "Coast ENV_BaseMooringPier") pier++;
                    else if (t.name == "Coast ENV_BaseBreakwater") breakwater++;
                    else if (t.name == "Coast ENV_FloatingPontoon") pontoon++;
                    else if (t.name == "MIL_Helipad") pads++;
                }
                lights = info.Root.GetComponentsInChildren<Game.View.BaseSearchlight>(true).Length;
                _report.AppendLine($"- {kind} 부두 · 시설(Codex 후속): 계류 부두 {pier} · 돌제 {breakwater} · 부잔교 {pontoon} · 헬기 착륙장 {pads} · 탐조등 {lights} · 세워진 부두 {upright}(0이어야)");
                if (upright > 0) Fail($"{kind} 기지 섬 부두가 수면에 눕지 않고 섬");
                if (pier + pontoon + breakwater == 0) Fail($"{kind} 기지 섬에 부두가 없음");
                if (kind != IslandBuilder.Kit.BaseS && lights < 2) Fail($"{kind} 기지 섬에 탐조등이 2개보다 적음");
                if (kind == IslandBuilder.Kit.BaseL && pads == 0) _report.AppendLine("  - 대형 기지에 헬기 착륙장 자리가 없음(부지가 좁음 — 기록만)");
            }
            var dressed = new[]
            {
                field.PlaceDressed(o - f * 40f - rt * 130f, 14f, false, 11),
                field.PlaceDressed(o + rt * 145f, 12f, false, 23),
                field.PlaceDressed(o - rt * 125f + f * 30f, 7f, true, 5),
            };
            int natProc = 0;
            foreach (var di in dressed)
                if (di != null) foreach (var t in di.Root.GetComponentsInChildren<KeepSharedMeshes>(true)) if (t.name.StartsWith("NAT_")) natProc++;
            _report.AppendLine($"- 절차형 섬 장식(무인도 2 · 바위섬 1): 자연물 {natProc}개");
            yield return new WaitForSeconds(0.5f);
            foreach (var (kind, info) in infos)
            {
                if (info == null) continue;
                float d = Mathf.Max(26f, info.Radius * 1.2f);
                yield return AerialShot($"kit_{kind}", info.Center, (info.Center - o).normalized, d, d * 0.9f);
            }
            for (int i = 0; i < 2; i++)
                if (dressed[i] != null) yield return AerialShot($"kit_dressed_{i}", dressed[i].Center, f, 26f, 20f);

            // ---- 3-1) 기지 섬 지형 변형(2026-11-21 Codex IslandBaseFollowup_v1 B · C형): 규모마다 세 변형이 모두 서고, 시설 · 포대 자리가 놓인다
            {
                var variantInfos = new List<IslandInfo>();
                var kinds = new[] { IslandBuilder.Kit.BaseS, IslandBuilder.Kit.BaseM, IslandBuilder.Kit.BaseL };
                var seen = new HashSet<string>();
                Vector3 far = o + f * 900f;
                for (int k = 0; k < 3; k++)
                    for (int v = 0; v < IslandBuilder.BaseVariants; v++)
                    {
                        Vector3 at = far + rt * ((v - 1) * 260f) + f * (k * 260f);
                        var vi = field.PlaceKit(at, kinds[k], 31 + v, 1f, v);
                        if (vi == null) { Fail($"{kinds[k]} 변형 {v} 섬을 만들지 못함"); continue; }
                        variantInfos.Add(vi);
                        seen.Add(vi.Art);
                        int mil = 0;
                        foreach (var t in vi.Root.GetComponentsInChildren<KeepSharedMeshes>(true)) if (t.name.StartsWith("MIL_")) mil++;
                        int want = kinds[k] == IslandBuilder.Kit.BaseL ? 2 : 1;
                        int minFac = kinds[k] == IslandBuilder.Kit.BaseS ? 8 : kinds[k] == IslandBuilder.Kit.BaseM ? 18 : 28;
                        _report.AppendLine($"- 변형 {vi.Art}: 해안선 반경 {vi.Radius:0.0}m · 시설 {mil} · 포대 자리 {(vi.BatterySpots != null ? vi.BatterySpots.Length : 0)}");
                        if (vi.BatterySpots == null || vi.BatterySpots.Length < want) Fail($"{vi.Art} 포대 자리가 {want}개보다 적음");
                        if (mil < minFac) Fail($"{vi.Art} 시설이 너무 적음({mil} < {minFac})");
                    }
                // 같은 판의 섬 자리별 변형이 고르게 나오나(자리 400곳)
                var counts = new int[IslandBuilder.BaseVariants];
                for (int i = 0; i < 400; i++) counts[IslandBuilder.VariantFor(IslandBuilder.Kit.BaseM, new Vector3(i * 37f - 7000f, 0f, (i * 53) % 900 - 450f))]++;
                _report.AppendLine($"- 지형 변형 모델 {seen.Count}종({string.Join(", ", seen)}) · 섬 자리 400곳의 변형 분포 A {counts[0]} · B {counts[1]} · C {counts[2]}");
                if (seen.Count < 9) Fail($"기지 섬 지형 변형이 9종이 아님({seen.Count}종)");
                for (int v = 0; v < counts.Length; v++) if (counts[v] < 80) Fail("섬 자리별 지형 변형이 한쪽으로 몰림");
                yield return new WaitForSeconds(0.3f);
                if (variantInfos.Count > 0)
                    yield return AerialShot("kit_variants", far + f * 260f, f, 520f, 600f);
                // 섬 목록(Islands.All)에 남아 있으므로 지우지 않는다 — 기함에서 900m 넘게 떨어져 뒤 검사와 겹치지 않는다
            }

            // ---- 4) 해안 미사일 포대: 중형 기지 섬 앞으로
            var baseM = infos.Find(x => x.kind == IslandBuilder.Kit.BaseM).info;
            if (baseM != null && baseM.BatterySpots != null && baseM.BatterySpots.Length > 0)
            {
                Vector3 spot = baseM.BatterySpots[0];
                Vector3 away = spot - baseM.Center; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : -f;
                float fromCenter = Flat(spot - baseM.Center).magnitude;
                var rb = ship.GetComponent<Rigidbody>();
                void Put(Vector3 p) { p.y = ship.transform.position.y; if (rb != null) rb.position = p; ship.transform.position = p; }
                // 포대는 함선에서 50~115m일 때만 선다(눈앞에 갑자기 생기지 않게) — 70m에서 세운 뒤 사거리(50m) 안으로 다가간다
                Put(spot + away * 72f);
                CoastalBatteryField.Disabled = false;
                yield return new WaitForSeconds(1.5f);
                Put(spot + away * Mathf.Max(30f, baseM.Radius - fromCenter + 10f));
                yield return new WaitForSeconds(0.3f);
                CoastalMissileBattery bat = null;
                float best = float.MaxValue;
                foreach (var e in CoastalBatteryField.Instance.Placed)
                    if (e is CoastalMissileBattery m && m.IsAlive)
                    {
                        float dd = Flat(m.transform.position - ship.transform.position).magnitude;
                        if (dd < best) { best = dd; bat = m; }
                    }
                _report.AppendLine($"- 해안 미사일 포대: 기지 섬에 섬 {Yes(bat != null)} · 놓인 고정 적 {CoastalBatteryField.Instance.ActiveCount}");
                if (bat == null) Fail("다가가도 기지 섬에 해안 미사일 포대가 서지 않음");
                else
                {
                    float dist = Vector3.Distance(Flat(bat.transform.position), Flat(ship.transform.position));
                    bool raised = false, shot = false;
                    float maxElev = 0f, maxHatch = 0f, t0 = Time.time;
                    while (Time.time - t0 < 24f && bat.IsAlive)
                    {
                        if (bat.Raising) raised = true;
                        maxElev = Mathf.Max(maxElev, bat.Elevation);
                        maxHatch = Mathf.Max(maxHatch, bat.HatchOpen);
                        if (raised && !shot && bat.Elevation > 18f) { shot = true; yield return CloseShot("coastal_missile_raise", bat.transform, 9f); }
                        ship.RepairHull(ship.HullMaxHp);
                        if (bat.MissilesFired >= 2 && Time.time - t0 > 5f) break;
                        yield return null;
                    }
                    _report.AppendLine($"  - 거리 {dist:0}m · 예고(발사관 들어 올림) {Yes(raised)} · 최대 고각 {maxElev:0}° · 쏜 미사일 {bat.MissilesFired} · 높이 y {bat.transform.position.y:0.00}");
                    if (!raised || bat.MissilesFired < 2) Fail("해안 미사일 포대가 예고 뒤 2발을 쏘지 않음");
                    _report.AppendLine($"  - 발사관 덮개(Codex): 덮개 {bat.HatchCount}개 · 가장 크게 열린 정도 {maxHatch:P0}");
                    if (bat.HatchCount < 2) Fail("해안 미사일 포대 모델에 발사관 덮개가 없음");
                    if (maxHatch < 0.9f) Fail("발사 예고 때 발사관 덮개가 열리지 않음");
                    yield return AerialShot("coastal_missile_base", bat.transform.position, (bat.transform.position - ship.transform.position).normalized, 36f, 28f);
                    // 격침 → 잔해(MIL_Ruins_Battery) · 연기
                    int ruins0 = Game.View.RuinSmoke.Placed;
                    bat.TakeDamage(new DamageInfo(99999f, bat.transform.position, Vector3.down, DamageSource.Gun));
                    yield return new WaitForSeconds(1.2f);
                    _report.AppendLine($"  - 격침 뒤 잔해: {Game.View.RuinSmoke.Placed - ruins0}개");
                    if (Game.View.RuinSmoke.Placed <= ruins0) Fail("해안 미사일 포대를 부숴도 잔해가 남지 않음");
                    yield return AerialShot("coastal_missile_ruins", bat.transform.position, (bat.transform.position - ship.transform.position).normalized, 30f, 22f);
                }
            }
            CombatDevTools.ClearBattlefield();
            CoastalBatteryField.Disabled = false;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
