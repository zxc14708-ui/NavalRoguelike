#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.UI;
using Game.World;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 해역 차별화(2026-10-11 점검 K04, -seaOnly).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 스테이지 → 해역, 해역별 섬 수(군도 &gt; 개방해역), 수로 기슭 · 물길 안 바위섬만
        /// 2) 레이더 배율, 적 구성·정비 카드 가중치(뽑기 통계), 물색·안개
        /// 3) 좁은 수로: 기슭으로 전속 돌진해도 경계를 넘지 않음, 기슭 가까이서도 적은 물길 안에 나옴, 기슭이 사선을 가림
        /// 4) 해안포대: 다가가면 기슭 물가에 서고 포격, 격침하면 경험치·격침 수, 같은 자리에 다시 서지 않음
        /// 5) 항로 선택에 다음 해역 안내, 해역 안내 카드, 해역을 벗어나면 원래대로
        /// </summary>
        private IEnumerator SeaCheck()
        {
            _report.AppendLine("\n## 해역 차별화(K04)");
            var gm = GameManager.Instance;
            var ship = gm.Player;
            var targeting = ship.GetComponent<TargetingSystem>();
            var field = IslandField.Instance;
            var hud = Object.FindFirstObjectByType<HUDView>();
            if (field == null) { Fail("섬 생성기가 없음"); yield break; }
            if (gm.State != GameState.Playing) gm.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            IslandField.Disabled = false;
            IslandField.SeedOverride = 424242;
            CoastalBatteryField.Disabled = true;

            // ---- 1) 스테이지 → 해역
            bool map = SeaRegion.ForStage(1) == SeaKind.Archipelago && SeaRegion.ForStage(2) == SeaKind.Strait && SeaRegion.ForStage(3) == SeaKind.Open;
            _report.AppendLine($"- 스테이지 1·2·3 → 군도·좁은 수로·개방해역 {Yes(map)}");
            if (!map) Fail("스테이지별 해역이 다름(K04)");

            SeaRegion.Leave();
            yield return new WaitForSecondsRealtime(1f);
            float baseRadar = targeting != null ? targeting.RadarRange : 0f;

            var counts = new Dictionary<SeaKind, int>();
            var radar = new Dictionary<SeaKind, float>();
            int straitBanks = 0, straitOutside = 0;
            foreach (var kind in new[] { SeaKind.Open, SeaKind.Archipelago, SeaKind.Strait })
            {
                SeaRegion.Enter(kind, ship.transform.position, ship.transform.forward);
                yield return new WaitForSecondsRealtime(1.5f);
                int n = 0;
                foreach (var isl in Islands.All)
                {
                    if (isl == null || isl.Bank) continue;
                    n++;
                    if (kind == SeaKind.Strait && Mathf.Abs(SeaRegion.Lateral(isl.Center)) > SeaRegion.ChannelHalfWidth - isl.Radius - 10f) straitOutside++;
                }
                counts[kind] = n;
                radar[kind] = targeting != null ? targeting.RadarRange : 0f;
                if (kind == SeaKind.Strait) straitBanks = field.BankSegments;
                if (kind == SeaKind.Archipelago) yield return ScreenShot("sea_archipelago");
                if (kind == SeaKind.Open) yield return ScreenShot("sea_open");
            }
            _report.AppendLine($"- 섬 수(주변 5×5 구역): 군도 {counts[SeaKind.Archipelago]} · 수로 {counts[SeaKind.Strait]}(물길 밖 {straitOutside}) · 개방해역 {counts[SeaKind.Open]} · 수로 기슭 {straitBanks}구간");
            if (counts[SeaKind.Archipelago] <= counts[SeaKind.Open] * 2) Fail("군도에 섬이 개방해역보다 충분히 많지 않음(K04)");
            if (straitBanks < 10) Fail("좁은 수로에 기슭이 이어지지 않음(K04)");
            if (straitOutside > 0) Fail("좁은 수로의 바위섬이 물길 밖에 있음(K04)");

            float Ratio(SeaKind k) => baseRadar > 0f ? radar[k] / baseRadar : 0f;
            _report.AppendLine($"- 레이더(해역 없음 {baseRadar:0.#}m 대비): 군도 ×{Ratio(SeaKind.Archipelago):0.00} · 수로 ×{Ratio(SeaKind.Strait):0.00} · 개방해역 ×{Ratio(SeaKind.Open):0.00}");
            if (!Near(Ratio(SeaKind.Archipelago), 0.8f, 0.03f) || !Near(Ratio(SeaKind.Strait), 0.9f, 0.03f) || !Near(Ratio(SeaKind.Open), 1.15f, 0.03f))
                Fail("해역별 레이더 배율이 설계와 다름(K04)");

            // ---- 2) 적 구성·카드 가중치(뽑기 통계)
            var fast = CombatDevTools.FindEnemy("ene_fastboat");
            var missile = CombatDevTools.FindEnemy("ene_missileboat");
            if (fast != null && missile != null)
            {
                var round = new RoundSet.Round { Entries = new List<RoundSet.SpawnEntry> { new() { Enemy = fast, Weight = 1f }, new() { Enemy = missile, Weight = 1f } } };
                float Share(SeaKind k)
                {
                    SeaRegion.Enter(k, ship.transform.position, ship.transform.forward);
                    Random.InitState(77);
                    int f = 0;
                    for (int i = 0; i < 3000; i++) if (RoundSet.PickWeighted(round, _ => true, d => SeaRegion.EnemyWeight(d.Id)) == fast) f++;
                    return f / 3000f;
                }
                float archShare = Share(SeaKind.Archipelago), openShare = Share(SeaKind.Open);
                _report.AppendLine($"- 적 구성(고속정:미사일정 1:1 표): 군도 고속정 {archShare * 100f:0}%(기대 67) · 개방해역 {openShare * 100f:0}%(기대 33)");
                if (Mathf.Abs(archShare - 0.667f) > 0.04f || Mathf.Abs(openShare - 0.333f) > 0.04f) Fail("해역별 적 구성 가중치가 맞지 않음(K04)");
            }
            SeaRegion.Enter(SeaKind.Strait, ship.transform.position, ship.transform.forward);
            float aswCard = SeaRegion.CardWeight("mod_asw");
            SeaRegion.Enter(SeaKind.Open, ship.transform.position, ship.transform.forward);
            float samCard = SeaRegion.CardWeight("mod_sam"), plainCard = SeaRegion.CardWeight("mod_magazine");
            _report.AppendLine($"- 정비 카드 가중치: 수로 대잠 ×{aswCard:0.##} · 개방해역 대공 ×{samCard:0.##} · 해역과 무관한 블록 ×{plainCard:0.##}");
            if (aswCard <= 1f || samCard <= 1f || !Mathf.Approximately(plainCard, 1f)) Fail("해역별 정비 카드 가중치가 맞지 않음(K04)");

            // ---- 3) 좁은 수로: 물색·안개, 경계, 스폰, 사선
            SeaRegion.Enter(SeaKind.Strait, ship.transform.position, ship.transform.right);   // 기슭(왼쪽)을 향해 선다
            yield return new WaitForSecondsRealtime(4.6f);
            var atmo = Game.View.SeaAtmosphere.Instance;
            float fog = atmo != null ? atmo.CurrentFog : 0f;
            bool tint = atmo != null && Vector4.Distance(atmo.CurrentDeep, SeaRegion.For(SeaKind.Strait).Deep) < 0.02f;
            var card = Object.FindFirstObjectByType<SeaRegionCardUI>();
            bool cardShown = card != null && card.Visible && card.Text.Contains("좁은 수로");
            _report.AppendLine($"- 수로 물색·안개: 물색 {Yes(tint)} · 안개 {fog:0.0000}(기대 0.0055) · 렌더 안개 켜짐 {Yes(RenderSettings.fog)} · 해역 안내 카드 {Yes(cardShown)}");
            if (atmo == null || !tint || !Near(fog, 0.0055f, 0.05f) || !RenderSettings.fog) Fail("해역 물색·안개가 바뀌지 않음(K04)");
            if (!cardShown) Fail("해역 안내 카드가 나오지 않음(K04)");
            yield return ScreenShot("sea_strait");

            float maxLat = 0f;
            bool aground = false;
            ship.SetEngineOrder(1f);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 9f)
            {
                maxLat = Mathf.Max(maxLat, Mathf.Abs(SeaRegion.Lateral(ship.transform.position)));
                aground |= ship.IsAground;
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            ship.SetEngineOrder(0f);
            yield return ScreenShot("sea_strait_bank");
            _report.AppendLine($"- 기슭으로 전속 9초: 물길 가운데서 최대 {maxLat:0.0}m(물길 절반 {SeaRegion.ChannelHalfWidth}m) · 좌초 판정 {Yes(aground)}");
            if (maxLat > SeaRegion.ChannelHalfWidth + 3f) Fail($"기함이 수로 경계를 넘음({maxLat:0.0}m, K04)");

            var spawner = EnemySpawner.Instance;
            int ringOut = 0, rings = 0;
            if (spawner != null)
                for (int i = 0; i < 200; i++)
                {
                    var p = (Vector3)Call(spawner, "GetRingPosition");
                    rings++;
                    if (!SeaRegion.InWater(p, 8f)) ringOut++;
                }
            Vector3 mid = SeaRegion.ChannelPoint(SeaRegion.Along(ship.transform.position), 0f);
            bool BankOnLine(Vector3 from, Vector3 to)
            {
                from.y = 1.5f; to.y = 1.2f;
                foreach (var h in Physics.RaycastAll(from, (to - from).normalized, Vector3.Distance(from, to), Islands.Mask, QueryTriggerInteraction.Ignore))
                    if (h.collider != null && h.collider.name == "Bank collider") return true;
                return false;
            }
            float along = SeaRegion.Along(ship.transform.position);
            bool blocked = BankOnLine(mid, SeaRegion.ChannelPoint(along, -(SeaRegion.ChannelHalfWidth + 40f)));
            bool open = !BankOnLine(mid, SeaRegion.ChannelPoint(along + 90f, 0f));
            _report.AppendLine($"- 기슭 가까이서 적 출현 자리 {rings}곳 중 물길 밖 {ringOut} · 기슭이 사선을 가림 {Yes(blocked)} · 물길 안 사선은 트임 {Yes(open)}");
            if (ringOut > 0) Fail("좁은 수로에서 적이 물길 밖에 나타남(K04)");
            if (!blocked || !open) Fail("기슭의 사선 가림이 맞지 않음(K04)");

            // ---- 4) 해안포대: 기슭 물가에 서고 다가가면 포격, 격침하면 다시 서지 않음
            {
                Vector3 a = ship.transform.forward; a.y = 0f; a.Normalize();
                Vector3 r = new(a.z, 0f, -a.x);
                SeaRegion.Enter(SeaKind.Strait, ship.transform.position + a * 60f - r * 30f, a);   // 기함 = 물길 (−60, +30)
                yield return new WaitForSecondsRealtime(1.2f);
                CoastalBatteryField.Disabled = false;
                var batteries = CoastalBatteryField.Instance;
                CoastalBattery battery = null;
                t0 = Time.realtimeSinceStartup;
                while (battery == null && Time.realtimeSinceStartup - t0 < 6f)
                {
                    if (batteries != null) foreach (var e in batteries.Placed) if (e is CoastalBattery b && b.IsAlive) { battery = b; break; }
                    yield return null;
                }
                if (battery == null) { Fail("좁은 수로에 해안포대가 서지 않음(K04)"); }
                else
                {
                    float lat = SeaRegion.Lateral(battery.transform.position);
                    float shore = IslandBuilder.BankShore(SeaRegion.Along(battery.transform.position), lat > 0f ? 1 : -1, field.Seed);
                    bool atShore = Mathf.Abs(Mathf.Abs(lat) - (shore - 2.2f)) < 0.5f;
                    bool seen = targeting != null && System.Linq.Enumerable.Contains(targeting.DetectedSurface, battery);
                    yield return CloseShot("sea_battery", battery.transform, 9f);
                    ship.SetEngineOrder(0.5f);
                    t0 = Time.realtimeSinceStartup;
                    while (battery.ShellsFired == 0 && Time.realtimeSinceStartup - t0 < 18f) { ship.RepairHull(ship.HullMaxHp); yield return null; }
                    ship.SetEngineOrder(0f);
                    int fired = battery.ShellsFired;
                    float dist = Vector3.Distance(battery.transform.position, ship.transform.position);
                    int killsBefore = gm.TotalKills;
                    battery.TakeDamage(new DamageInfo(9999f, battery.transform.position, Vector3.down, DamageSource.Gun));
                    yield return new WaitForSecondsRealtime(2.5f);
                    bool respawned = false;
                    foreach (var e in batteries.Placed)
                        if (e != null && e.IsAlive && Vector3.Distance(e.transform.position, battery.transform.position) < 1f) respawned = true;
                    _report.AppendLine($"- 해안포대: 물가 {Yes(atShore)}(물길 가운데서 {Mathf.Abs(lat):0.0}m) · 레이더 탐지 {Yes(seen)} · 포격 {fired}발(거리 {dist:0}m) · 격침 수 +{gm.TotalKills - killsBefore} · 같은 자리 다시 안 섬 {Yes(!respawned)} · 격침 기록 {batteries.DestroyedCount}");
                    if (!atShore) Fail("해안포대가 기슭 물가에 있지 않음(K04)");
                    if (fired == 0) Fail("해안포대가 사거리 안에서 쏘지 않음(K04)");
                    if (gm.TotalKills - killsBefore != 1) Fail("해안포대 격침이 기록되지 않음(K04)");
                    if (respawned) Fail("격침한 해안포대가 같은 자리에 다시 섬(K04)");
                }
                CoastalBatteryField.Disabled = true;
                batteries?.Reset();
            }

            // ---- 군도의 해안포대 후보(섬 자리로 정해짐)
            {
                SeaRegion.Enter(SeaKind.Archipelago, ship.transform.position, ship.transform.forward);
                yield return new WaitForSecondsRealtime(1.5f);
                CoastalBatteryField.Disabled = false;
                yield return new WaitForSecondsRealtime(1.5f);
                int placed = CoastalBatteryField.Instance != null ? CoastalBatteryField.Instance.ActiveCount : 0;
                _report.AppendLine($"- 군도 해안포대(50~115m 안의 큰 섬 35%): 지금 {placed}개");
                if (placed > CoastalBatteryField.MaxActive) Fail("해안포대가 동시 최대 수를 넘음(K04)");
                CoastalBatteryField.Disabled = true;
                CoastalBatteryField.Instance?.Reset();
            }

            // ---- 5) 항로 선택의 다음 해역 안내 · 해역을 벗어나면 원래대로
            if (hud != null)
            {
                gm.SetState(GameState.Paused);
                Game.Core.RouteMap.Clear();   // 기존 3칸 선택(지도 없는 경로)을 본다
                hud.ShowRouteChoice(3, 30f, _ => { }, () => { });
                yield return null;
                var overlay = Get<GameObject>(hud, "_routeChoice");
                var hint = overlay != null ? overlay.transform.Find("Route briefing/Hint")?.GetComponent<TMPro.TMP_Text>() : null;
                var eyebrow = overlay != null ? overlay.transform.Find("Route briefing/Eyebrow")?.GetComponent<TMPro.TMP_Text>() : null;
                bool told = hint != null && hint.text.Contains("개방해역") && eyebrow != null && eyebrow.text.Contains("개방해역");
                _report.AppendLine($"- 항로 선택(스테이지 3 앞)에 다음 해역 안내 {Yes(told)}");
                if (!told) Fail("항로 선택에 다음 해역 안내가 없음(K04)");
                yield return ScreenShot("sea_route_choice");
                Call(hud, "ChooseRoute", false);
                gm.SetState(GameState.Playing);
            }
            SeaRegion.Leave();
            yield return new WaitForSecondsRealtime(4.6f);
            bool restored = Near(targeting != null ? targeting.RadarRange : 0f, baseRadar, 0.01f) && (atmo == null || atmo.CurrentFog < 0.0001f) && field.BankSegments == 0;
            _report.AppendLine($"- 해역을 벗어나면 레이더·안개·기슭 원래대로 {Yes(restored)}");
            if (!restored) Fail("해역을 벗어나도 효과가 남음(K04)");

            IslandField.SeedOverride = 0;
            foreach (var b in off) if (b != null) b.enabled = true;
        }
    }
}
#endif
