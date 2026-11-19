#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.World;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 낮과 밤(2026-11-19): 시간 흐름 · 밤 적 구성 · 대잠 카드 가중치 · 조명.</summary>
    public partial class CombatVerificationRunner
    {
        private IEnumerator DayNightCheck()
        {
            _report.AppendLine("\n## 낮과 밤(2026-11-19)");
            var ship = GameManager.Instance.Player;
            var director = Object.FindFirstObjectByType<StageDirector>();
            if (director != null) director.enabled = false;   // 구간은 직접 계산한다
            EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();

            // ---- 1) 시간 흐름: 스테이지 1(구간 8개, 밤은 5번째 구간부터)
            {
                bool ok = true;
                void At(int round, float elapsed, DayPhase want, float dark, bool night)
                {
                    DayNight.Tick(round, 8, 4, elapsed, 60f);
                    bool isNight = DayNight.IsNightRound(round, 8, 4);
                    _report.AppendLine($"- {round + 1}구간 {elapsed:0}초: {DayNight.Phase} 어둠 {DayNight.Darkness01:0.00}(기대 {want} {dark:0.00}) · 밤 구간 {Yes(isNight)} · 대잠 카드↑ {Yes(DayNight.NightAhead)}");
                    if (DayNight.Phase != want || Mathf.Abs(DayNight.Darkness01 - dark) > 0.06f || isNight != night) { ok = false; Fail($"{round + 1}구간 {elapsed}초 때가 틀림"); }
                }
                At(0, 10f, DayPhase.Day, 0f, false);
                At(3, 30f, DayPhase.Day, 0f, false);
                At(3, 42f, DayPhase.Dusk, 0.23f, false);
                At(3, 54f, DayPhase.Dusk, 0.73f, false);
                At(4, 1f, DayPhase.Night, 1f, true);
                At(6, 50f, DayPhase.Night, 1f, true);
                At(7, 8f, DayPhase.Dawn, 0.65f, false);
                At(7, 40f, DayPhase.Dawn, 0.3f, false);
                DayNight.Tick(2, 8, 4, 30f, 60f);
                if (DayNight.NightAhead) { ok = false; Fail("해질녘 두 구간 앞인데 대잠 카드 가중치가 켜짐"); }
                DayNight.Tick(3, 8, -1, 59f, 60f);
                if (DayNight.Phase != DayPhase.Day) { ok = false; Fail("밤 설정이 없는(-1) 스테이지가 낮이 아님"); }
                _report.AppendLine($"- 시간 흐름: {Yes(ok)}");
            }

            // ---- 2) 밤 적 구성: 스테이지마다 낮·밤 구간의 잠수함 비중
            {
                var stages = director != null ? director.Stages : null;
                if (stages == null || stages.Count == 0) Fail("스테이지 목록 없음");
                else
                    for (int s = 0; s < stages.Count; s++)
                    {
                        var set = stages[s];
                        if (set == null) continue;
                        var sb = new System.Text.StringBuilder();
                        float daySub = 0f, nightSub = 0f; int nights = 0;
                        for (int i = 0; i < set.Count; i++)
                        {
                            bool night = DayNight.IsNightRound(i, set.Count, set.NightFromRound);
                            var r = set.ForTimeOfDay(i, night);
                            float total = 0f, sub = 0f;
                            foreach (var e in r.Entries) { total += e.Weight; if (e.Enemy.Kind == TargetKind.Submarine || e.Enemy.Id == "ene_torpedo_boat" || e.Enemy.Id == "ene_minelayer" || e.Enemy.Id.StartsWith("ene_semisub")) sub += e.Weight; }
                            float share = total > 0f ? sub / total : 0f;
                            if (night) { nightSub += share; nights++; } else if (i < set.Count - 1) daySub = Mathf.Max(daySub, share);
                            sb.Append($"{(night ? "밤" : i == set.Count - 1 ? "새벽" : "낮")} {share:P0} ");
                        }
                        float nightAvg = nights > 0 ? nightSub / nights : 0f;
                        _report.AppendLine($"- 스테이지 {s + 1}(밤 {set.NightFromRound + 1}구간부터, 밤 잠수함 +{set.NightExtraSubmarines}): 수중·어뢰 비중 {sb}· 밤 평균 {nightAvg:P0}");
                        if (set.NightFromRound < 0) { Fail($"스테이지 {s + 1}에 밤이 없음"); continue; }
                        if (nightAvg < 0.4f) Fail($"스테이지 {s + 1} 밤 구간의 수중·어뢰 비중이 낮음({nightAvg:P0})");
                        if (s < 2)
                            for (int i = 0; i < set.NightFromRound; i++)
                                foreach (var e in set.ForTimeOfDay(i, false).Entries)
                                    if (e.Enemy.Kind == TargetKind.Submarine) Fail($"스테이지 {s + 1} 낮 {i + 1}구간에 잠수함이 남음");
                    }
            }

            // ---- 3) 대잠 카드 가중치
            {
                var sonar = CombatDevTools.FindModule("mod_sonar");
                var gun = CombatDevTools.FindModule("mod_gun76");
                DayNight.Tick(4, 8, 4, 1f, 60f);
                float wSonarNight = Game.Refit.RefitDraft.NightCardWeight(sonar), wGunNight = Game.Refit.RefitDraft.NightCardWeight(gun);
                DayNight.Tick(0, 8, 4, 1f, 60f);
                float wSonarDay = Game.Refit.RefitDraft.NightCardWeight(sonar);
                _report.AppendLine($"- 카드 가중치: 소나 밤 ×{wSonarNight:0.#} · 낮 ×{wSonarDay:0.#} · 76mm 밤 ×{wGunNight:0.#}");
                if (sonar == null || !Mathf.Approximately(wSonarNight, DayNight.AswCardWeight) || !Mathf.Approximately(wSonarDay, 1f) || !Mathf.Approximately(wGunNight, 1f))
                    Fail("밤 대잠 카드 가중치가 틀림");
            }

            // ---- 4) 조명: 낮 · 해질녘 · 밤 · 새벽 사진(적 몇 척을 둘레에 세워 둔다)
            {
                var fast = Game.Missions.FieldEventDirector.FindEnemy("ene_fastboat");
                var sub = Game.Missions.FieldEventDirector.FindEnemy("ene_submarine");
                for (int i = 0; i < 5; i++)
                {
                    var def = i == 4 ? sub : fast;
                    var e = def != null ? EnemySpawner.Instance.SpawnAt(def, ship.transform.position + Quaternion.Euler(0f, i * 72f + 20f, 0f) * Vector3.forward * 16f, Quaternion.identity) : null;
                    if (e != null) e.DevFrozen = true;
                }
                ship.SetEngineOrder(0f);
                var lighting = Game.View.DayNightLighting.Instance;
                if (lighting == null) { Fail("낮밤 조명(DayNightLighting)이 전투 씬에 없음"); }
                else
                {
                    var names = new System.Text.StringBuilder();
                    foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (l.type == LightType.Directional) names.Append($"{l.name}({l.intensity:0.##}, 켜짐 {Yes(l.isActiveAndEnabled)}, 마스크 {l.cullingMask}) ");
                    _report.AppendLine($"- 해: {lighting.SunName} · 방향광: {names}");
                    if (lighting.SunName == "Harbor daylight") Fail("항구 메뉴 빛을 해로 잡음");
                }
                float daySun = 0f;
                foreach (var p in new[] { DayPhase.Day, DayPhase.Dusk, DayPhase.Night, DayPhase.Dawn })
                {
                    DayNight.Forced = p;
                    DayNight.Tick(0, 1, -1, 0f, 1f);
                    yield return null;
                    yield return null;
                    yield return new WaitForSeconds(0.3f);
                    float sun = lighting != null ? lighting.SunIntensity : 0f;
                    if (p == DayPhase.Day) daySun = sun;
                    var tint = lighting != null ? lighting.OceanTint : Color.white;
                    var sky = RenderSettings.ambientSkyColor;
                    _report.AppendLine($"- {p}: 해 세기 {sun:0.00} · 주변광 {RenderSettings.ambientMode} 위 ({sky.r:0.00}, {sky.g:0.00}, {sky.b:0.00}) ×{(lighting != null ? lighting.AmbientScale : 0f):0.00} · 물빛 ×({tint.r:0.00}, {tint.g:0.00}, {tint.b:0.00}) · 항해등 켬 {Yes(DayNight.LightsOn)}");
                    yield return ScreenShot($"daynight_{p.ToString().ToLowerInvariant()}");
                    if (p == DayPhase.Night && (sun > daySun * 0.3f || tint.b > 0.7f || !DayNight.LightsOn)) Fail("밤인데 충분히 어둡지 않음");
                    if (p == DayPhase.Day && (tint != Color.white || DayNight.LightsOn)) Fail("낮인데 조명이 바뀜");
                }

                // 밤 밝기 설정
                DayNight.Forced = DayPhase.Night;
                float[] sunAt = new float[3];
                for (int b = 0; b < 3; b++)
                {
                    DisplaySettings.SetNightBrightness(b);
                    DayNight.Tick(0, 1, -1, 0f, 1f);
                    yield return null;
                    yield return null;
                    sunAt[b] = lighting != null ? lighting.SunIntensity : 0f;
                }
                _report.AppendLine($"- 밤 밝기: 어둡게 {sunAt[0]:0.00} · 보통 {sunAt[1]:0.00} · 밝게 {sunAt[2]:0.00}");
                yield return ScreenShot("daynight_night_bright");
                if (!(sunAt[0] < sunAt[1] && sunAt[1] < sunAt[2])) Fail("밤 밝기 설정이 해 세기에 반영되지 않음");
                DisplaySettings.SetNightBrightness(1);
                CombatDevTools.ClearBattlefield();

                // ---- 5) 조명탄[B]: 밤 — 반경 안 잠수함 확정 접촉 · 낮 — 빛만
                var flare = ship.GetComponent<Game.Ship.FlareSkill>();
                if (flare == null) Fail("함선에 조명탄 스킬이 없음");
                else
                {
                    yield return null;
                    int max0 = flare.MaxCharges;
                    Vector3 spot = ship.transform.position + ship.transform.forward * 26f;
                    var s1 = sub != null ? EnemySpawner.Instance.SpawnAt(sub, spot + Vector3.right * 6f, Quaternion.identity) as SubmarineBase : null;
                    var s2 = sub != null ? EnemySpawner.Instance.SpawnAt(sub, spot + Vector3.right * 45f, Quaternion.identity) as SubmarineBase : null;
                    if (s1 != null) s1.DevFrozen = true;
                    if (s2 != null) s2.DevFrozen = true;
                    yield return null;
                    bool before = s1 != null && s1.IsContactConfirmed;
                    DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
                    bool fired = flare.FireAt(spot);
                    int left = flare.Charges;
                    bool again = flare.FireAt(spot);   // 1초 간격 안 — 안 나가야
                    yield return new WaitForSeconds(Game.Combat.ParachuteFlare.FlightSeconds + 1.2f);
                    var f0 = Game.Combat.ParachuteFlare.Active.Count > 0 ? Game.Combat.ParachuteFlare.Active[0] : null;
                    bool near = s1 != null && s1.IsContactConfirmed, far = s2 != null && s2.IsContactConfirmed;
                    bool lit = Game.Combat.ParachuteFlare.IsLit(spot);
                    yield return ScreenShot("flare_night");
                    _report.AppendLine($"- 조명탄: 최대 {max0}발(함교 1 + 함포·곡사포) · 쏨 {Yes(fired)} → 남은 {left} · 1초 안 다시 쏨 {Yes(again)} · 빛 안 {Yes(lit)} · 가까운 잠수함 접촉 {Yes(before)}→{Yes(near)} · 45m 밖 잠수함 접촉 {Yes(far)} · 접촉 수 {f0?.Contacts ?? 0}");
                    if (!fired || again || !lit || !near || far) Fail("밤 조명탄이 반경 안 잠수함만 확정 접촉으로 만들지 않음");
                    if (max0 < 1) Fail("함교가 있는데 조명탄이 없음");

                    // 낮: 빛만
                    CombatDevTools.ClearBattlefield();
                    foreach (var x in Object.FindObjectsByType<Game.Combat.ParachuteFlare>(FindObjectsSortMode.None)) Object.Destroy(x.gameObject);
                    DayNight.Forced = DayPhase.Day; DayNight.Tick(0, 1, -1, 0f, 1f);
                    var s3 = sub != null ? EnemySpawner.Instance.SpawnAt(sub, spot, Quaternion.identity) as SubmarineBase : null;
                    if (s3 != null) s3.DevFrozen = true;
                    yield return null;
                    flare.DevRefill();
                    bool dayFired = flare.FireAt(spot);
                    yield return new WaitForSeconds(Game.Combat.ParachuteFlare.FlightSeconds + 1.2f);
                    bool dayContact = s3 != null && s3.IsContactConfirmed;
                    _report.AppendLine($"- 낮 조명탄: 쏨 {Yes(dayFired)} · 잠수함 접촉 {Yes(dayContact)}(아니오여야)");
                    if (dayContact) Fail("낮 조명탄이 잠수함을 확정 접촉으로 만듦");
                    CombatDevTools.ClearBattlefield();
                }

                // ---- 6) 반잠수정: 레이더 40% · 조명탄 · 근접에 드러남 · 폭뢰에 맞음
                {
                    var targeting = ship.GetComponent<TargetingSystem>() ?? ship.GetComponentInChildren<TargetingSystem>();
                    var semiDef = Game.Missions.FieldEventDirector.FindEnemy("ene_semisub_suicide");
                    var torpDef = Game.Missions.FieldEventDirector.FindEnemy("ene_semisub_torpedo");
                    if (semiDef == null || torpDef == null || targeting == null) Fail("반잠수정 검사 준비 실패(정의 · 표적 시스템)");
                    else
                    {
                        DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
                        float radar = targeting.RadarRange;
                        Vector3 at = ship.transform.position + ship.transform.right * (radar * 0.6f);
                        var semi = EnemySpawner.Instance.SpawnAt(semiDef, at, Quaternion.identity);
                        var semiT = EnemySpawner.Instance.SpawnAt(torpDef, ship.transform.position - ship.transform.right * 18f + ship.transform.forward * 4f, Quaternion.identity);
                        var boat = EnemySpawner.Instance.SpawnAt(fast, at + ship.transform.forward * 6f, Quaternion.identity);
                        foreach (var e in new[] { semi, semiT, boat }) if (e != null) e.DevFrozen = true;
                        yield return new WaitForSeconds(0.6f);
                        bool semiSeen = false, boatSeen = false;
                        foreach (var t in targeting.DetectedSurface) { if (t == (ITargetable)semi) semiSeen = true; if (t == (ITargetable)boat) boatSeen = true; }
                        bool hidden = semi is ILowObservable lo && lo.Stealthy && !semi.IsRevealed;
                        _report.AppendLine($"- 반잠수정: 레이더 {radar:0}m의 60% 거리 — 고속정 탐지 {Yes(boatSeen)} · 반잠수정 탐지 {Yes(semiSeen)}(아니오여야) · 숨음 {Yes(hidden)}");
                        if (!boatSeen || semiSeen || !hidden) Fail("반잠수정이 레이더 거리 60%에서 잡히거나 고속정이 안 잡힘");
                        flare.DevRefill();
                        flare.FireAt(at);
                        yield return new WaitForSeconds(Game.Combat.ParachuteFlare.FlightSeconds + 1.2f);
                        semiSeen = false;
                        foreach (var t in targeting.DetectedSurface) if (t == (ITargetable)semi) semiSeen = true;
                        _report.AppendLine($"- 조명탄 뒤: 반잠수정 드러남 {Yes(semi != null && semi.IsRevealed)} · 탐지 {Yes(semiSeen)}");
                        if (semi == null || !semi.IsRevealed || !semiSeen) Fail("조명탄에 비친 반잠수정이 드러나지 않음");
                        yield return ScreenShot("semisub_flare");

                        // 폭뢰: 반잠수정 바로 위에서 터뜨린다
                        float hp0 = semi.CurrentHp;
                        var asw = CombatDevTools.FindModule("mod_asw");
                        var chargePrefab = asw != null && asw.Prefab != null ? asw.Prefab.GetComponentInChildren<Game.Modules.Runtime.AswLauncherModule>(true) : null;
                        var prefabField = chargePrefab != null ? typeof(Game.Modules.Runtime.AswLauncherModule).GetField("chargePrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) : null;
                        var charge = prefabField?.GetValue(chargePrefab) as GameObject;
                        if (charge != null)
                        {
                            var go = PoolManager.Instance.Spawn(charge, semi.transform.position + Vector3.up * 3f, Quaternion.identity);
                            go.GetComponent<DepthCharge>()?.Launch(semi.transform.position + Vector3.up * 3f, semi.transform.position, 5f);
                            yield return new WaitForSeconds(3f);
                        }
                        float hp1 = semi != null && semi.IsAlive ? semi.CurrentHp : 0f;
                        _report.AppendLine($"- 폭뢰: 반잠수정 체력 {hp0:0.#} → {hp1:0.#}");
                        if (charge == null) Fail("폭뢰 프리팹을 찾지 못함");
                        else if (hp1 >= hp0) Fail("폭뢰가 반잠수정에 피해를 주지 못함");
                        bool nearSeen = semiT != null && semiT.IsRevealed;
                        _report.AppendLine($"- 18m 거리 반잠수 어뢰정 드러남 {Yes(nearSeen)}(근접 14m 밖 — 아니오여야)");
                        if (nearSeen) Fail("근접 거리 밖 반잠수 어뢰정이 드러남");
                    }
                    CombatDevTools.ClearBattlefield();
                }

                // ---- 7) 밤 조명: 항해등 · 탐조등 · 소노부이
                {
                    DayNight.Forced = DayPhase.Night; DayNight.Tick(0, 1, -1, 0f, 1f);
                    var light = Game.View.Searchlight.Active;
                    var semiDef = Game.Missions.FieldEventDirector.FindEnemy("ene_semisub_suicide");
                    var fleet = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
                    if (fleet != null && fleet.EscortCount == 0) fleet.Deploy();
                    var boats = new System.Collections.Generic.List<EnemyController>();
                    for (int i = 0; i < 3; i++)
                    {
                        var e = EnemySpawner.Instance.SpawnAt(fast, ship.transform.position + Quaternion.Euler(0f, 110f + i * 70f, 0f) * ship.transform.forward * 20f, Quaternion.Euler(0f, i * 50f, 0f));
                        if (e != null) { e.DevFrozen = true; boats.Add(e); }
                    }
                    yield return null;
                    Vector3 beam = light != null ? light.transform.forward : ship.transform.forward; beam.y = 0f; beam.Normalize();
                    var semi = semiDef != null ? EnemySpawner.Instance.SpawnAt(semiDef, ship.transform.position + beam * 24f, Quaternion.identity) : null;
                    if (semi != null) semi.DevFrozen = true;
                    yield return new WaitForSeconds(0.6f);
                    var lamps = boats.Count > 0 ? boats[0].GetComponent<Game.View.NightLampSet>() : null;
                    var semiLamps = semi != null ? semi.GetComponent<Game.View.NightLampSet>() : null;
                    var shipLamps = ship.GetComponent<Game.View.NightLampSet>();
                    var escort = fleet != null && fleet.EscortCount > 0 ? fleet.GetEscort(0) : null;
                    var escortLamps = escort != null ? escort.GetComponentInChildren<Game.View.NightLampSet>() : null;
                    float escortSpread = 0f;   // 좌현 · 우현 등 간격(선체 폭에 맞게 벌어졌는가)
                    if (escortLamps != null && escortLamps.transform.childCount >= 2)
                    {
                        var lampTs = escortLamps.GetComponentsInChildren<Transform>();
                        var a = System.Array.Find(lampTs, t => t.name == "Night lamp");
                        if (a != null) foreach (var t in lampTs) if (t.name == "Night lamp") escortSpread = Mathf.Max(escortSpread, Vector3.Distance(a.position, t.position));
                    }
                    bool beamOn = light != null && light.Light.enabled && light.Light.intensity > 1f;
                    int revealed = light != null ? light.Revealed : 0;
                    _report.AppendLine($"- 항해등: 고속정 {lamps?.Lit ?? 0}/{lamps?.Count ?? 0} 켜짐 · 기함 {shipLamps?.Lit ?? 0}/{shipLamps?.Count ?? 0} · 호위함 {escortLamps?.Lit ?? 0}/{escortLamps?.Count ?? 0}(등 간격 {escortSpread:0.0}m) · 반잠수정 {semiLamps?.Count ?? 0}개(0이어야) · 탐조등 켬 {Yes(beamOn)} · 탐조등에 드러난 반잠수정 {revealed} · 드러남 {Yes(semi != null && semi.IsRevealed)}");
                    if (lamps == null || lamps.Lit != Game.View.NavLights.PerShip || (semiLamps != null && semiLamps.Count > 0) || shipLamps == null || shipLamps.Lit < Game.View.NavLights.PerShip) Fail("밤 항해등이 맞게 켜지지 않음");
                    if (fleet != null && (escortLamps == null || escortLamps.Lit != Game.View.NavLights.PerShip || escortSpread < 1f)) Fail("아군 호위함 항해등이 없거나 한곳에 뭉침");
                    if (!beamOn || semi == null || !semi.IsRevealed) Fail("탐조등이 켜지지 않거나 빛 안 반잠수정이 드러나지 않음");
                    yield return ScreenShot("night_lights");

                    // 소노부이: 반경 안 잠수함 확정 접촉 · 갑판당 3개
                    var deckKey = new object();
                    Vector3 bp = ship.transform.position - ship.transform.forward * 30f;
                    var s4 = sub != null ? EnemySpawner.Instance.SpawnAt(sub, bp + Vector3.right * 8f, Quaternion.identity) as SubmarineBase : null;
                    if (s4 != null) s4.DevFrozen = true;
                    for (int i = 0; i < 4; i++) Game.Combat.Sonobuoy.Drop(deckKey, bp + Vector3.forward * (i * 3f));
                    yield return new WaitForSeconds(0.8f);
                    int count = Game.Combat.Sonobuoy.CountFor(deckKey);
                    bool contact = s4 != null && s4.IsContactConfirmed;
                    _report.AppendLine($"- 소노부이: 4개 떨굼 → 남은 {count}개(최대 {Game.Combat.Sonobuoy.MaxPerDeck}) · 8m 옆 잠수함 확정 접촉 {Yes(contact)} · 떠오름 {Yes(s4 != null && s4.IsRevealed)}(아니오여야)");
                    if (count != Game.Combat.Sonobuoy.MaxPerDeck || !contact || (s4 != null && s4.IsRevealed)) Fail("소노부이가 맞게 동작하지 않음");
                    foreach (var b in Object.FindObjectsByType<Game.Combat.Sonobuoy>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);

                    DayNight.Forced = DayPhase.Day; DayNight.Tick(0, 1, -1, 0f, 1f);
                    yield return null; yield return null;
                    bool dayOff = (lamps == null || lamps.Lit == 0) && (light == null || !light.Light.enabled);
                    _report.AppendLine($"- 낮: 항해등 · 탐조등 꺼짐 {Yes(dayOff)}");
                    if (!dayOff) Fail("낮인데 항해등·탐조등이 켜짐");
                    CombatDevTools.ClearBattlefield();
                }

                DayNight.Forced = null;
                DayNight.Reset();
                yield return null;
                yield return null;
                if (lighting != null && Mathf.Abs(lighting.SunIntensity - daySun) > 0.01f) Fail("낮으로 돌아와도 해 세기가 그대로가 아님");
                CombatDevTools.ClearBattlefield();
            }
        }
    }
}
#endif
