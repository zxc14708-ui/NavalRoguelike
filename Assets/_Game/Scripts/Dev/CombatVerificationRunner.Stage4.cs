#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.View;
using Game.World;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// -stage4Only(2026-11-16): 4스테이지 "내해의 괴물" —
        ///   1) 스테이지 연결(4스테이지 · 내해 · 웨이브 7구간 · 룬급 보스)
        ///   2) 유전 플랫폼(기둥 판정 · 포대 자리) · 모래톱(기함 감속)
        ///   3) 수면 비행 규칙: 위그선은 항공 표적 + 대함 유도탄 표적, 내려앉으면 수상 표적
        ///   4) 새 적: 강습 위그선 착수 · 보트 · 수중익정 부상 · 미사일 초계함 사격 · 플랫폼 포대 · 기름띠
        ///   5) 룬급: 통과선 예고 · 통과 · 미사일 · 재장전 착수(수상 표적) · 엔진 5기 파괴 → 추락 활주 · 돌진
        /// </summary>
        public bool Stage4Only;

        private IEnumerator Stage4Check()
        {
            _report.AppendLine("\n## 4스테이지 — 내해의 괴물");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            var rb = ship.GetComponent<Rigidbody>();
            void Put(Vector3 p) { p.y = ship.transform.position.y; if (rb != null) rb.position = p; ship.transform.position = p; }
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            CoastalBatteryField.Disabled = true;
            // 내 무장은 끈다(적 행동을 보려는데 먼저 격침하지 않게)
            var off = new List<Behaviour>();
            foreach (var r in ship.GetComponentsInChildren<Game.Modules.ModuleRuntime>()) if (r.enabled && r is not Game.Modules.Runtime.BridgeModule) { r.enabled = false; off.Add(r); }

            // ---- 1) 스테이지 연결
            var director = Object.FindFirstObjectByType<StageDirector>();
            int stages = director != null ? director.StageCount : 0;
            var s4 = director != null && director.Stages.Count >= 4 ? director.Stages[3] : null;
            string titles = "";
            EnemyDefinition bossDef = null;
            if (s4 != null) for (int i = 0; i < s4.Count; i++) { var r = s4.Get(i); titles += r.Title + (r.Boss != null ? $"(보스 {r.Boss.DisplayName})" : "") + " · "; if (r.Boss != null) bossDef = r.Boss; }
            _report.AppendLine($"- 스테이지 수 {stages}(기대 4) · 4스테이지 해역 {SeaRegion.ForStage(4)} · 구간: {titles}");
            if (stages != 4 || s4 == null || s4.Count != 7 || bossDef == null || bossDef.Id != "ene_boss4" || SeaRegion.ForStage(4) != SeaKind.InlandSea)
                Fail("4스테이지가 스테이지 3 다음에 이어지지 않음(내해 · 7구간 · 룬급 보스)");

            SeaRegion.Enter(SeaKind.InlandSea, ship.transform.position, ship.transform.forward);
            yield return new WaitForSecondsRealtime(1f);
            var field = IslandField.Instance;
            field.ClearAll();
            Vector3 o = ship.transform.position; o.y = 0f;
            Vector3 f = ship.transform.forward; f.y = 0f; f.Normalize();
            Vector3 rt = Vector3.Cross(Vector3.up, f);

            // ---- 2) 플랫폼 · 모래톱
            var platL = field.PlacePlatform(o + f * 70f + rt * 30f, true, true, 3);
            var platS = field.PlacePlatform(o + f * 60f - rt * 45f, false, true, 4);
            var bar = field.PlaceSandbar(o - f * 40f, 14f, 5);
            Physics.SyncTransforms();
            int legsL = platL != null ? platL.Root.GetComponentsInChildren<BoxCollider>(true).Length : 0;
            if (platL != null) foreach (var bc in platL.Root.GetComponentsInChildren<BoxCollider>(true)) { _report.AppendLine($"  - 기둥 판정 월드 크기 {bc.bounds.size.x:0.0}×{bc.bounds.size.y:0.0}×{bc.bounds.size.z:0.0} · 루트 축척 {platL.Root.transform.lossyScale.x:0.###}"); break; }
            _report.AppendLine($"- 유전 플랫폼: 대형 {Yes(platL != null)}(기둥 판정 {legsL} · 포대 자리 {(platL?.BatterySpots?.Length ?? 0)} · 높이 {(platL?.BatterySpots != null && platL.BatterySpots.Length > 0 ? platL.BatterySpots[0].y : 0f):0.0}m) · 소형 {Yes(platS != null)}(포대 자리 {(platS?.BatterySpots?.Length ?? 0)}) · 모래톱 {Yes(bar != null)}");
            if (platL == null || platS == null || legsL < 4 || platL.BatterySpots == null || platL.BatterySpots.Length < 2) Fail("유전 플랫폼(기둥 판정 4 · 포대 자리 2)이 서지 않음");
            yield return AerialShot("s4_platform_L", platL != null ? platL.Center : o, f, 40f, 30f);

            // 모래톱 밖 30m에서 전속으로 들어간다
            Put(o - f * 40f - f * 44f);
            yield return null;
            _report.AppendLine($"  - 순간이동 확인: 목표와 {Vector3.Distance(Flat(ship.transform.position), Flat(o - f * 84f)):0.0}m 차이 · 모래톱 중심까지 {Vector3.Distance(Flat(ship.transform.position), Flat(bar != null ? bar.Center : o)):0}m · 선수와 f 각 {Vector3.Angle(ship.transform.forward, f):0}°");
            ship.SetEngineOrder(1f);
            float maxIn = 0f, maxOut = 0f, t0 = Time.time;
            bool everIn = false;
            float inSince = -1f;
            while (Time.time - t0 < 8f)
            {
                bool inside = Islands.InSandbar(ship.transform.position);
                everIn |= inside;
                if (inside && inSince < 0f) inSince = Time.time;
                // 들어선 직후는 감속 중이라 1초 뒤부터 잰다
                if (inside) { if (Time.time - inSince > 1f) maxIn = Mathf.Max(maxIn, Mathf.Abs(ship.CurrentSpeed)); }
                else maxOut = Mathf.Max(maxOut, Mathf.Abs(ship.CurrentSpeed));
                yield return null;
            }
            ship.SetEngineOrder(0f);
            _report.AppendLine($"- 모래톱: 들어감 {Yes(everIn)} · 안에서(1초 뒤) 최고 속력 {maxIn:0.0} · 밖 {maxOut:0.0} / 최고 {ship.BaseMaxSpeed:0.0}m/s(기대 안 55% 이하) · 배 위치에서 모래톱까지 {Vector3.Distance(Flat(ship.transform.position), Flat(bar != null ? bar.Center : o)):0}m");
            if (maxIn <= 0.01f || maxIn > ship.BaseMaxSpeed * 0.6f) Fail("모래톱에서 기함이 느려지지 않음");
            yield return AerialShot("s4_sandbar", bar != null ? bar.Center : o, f, 30f, 26f);
            Put(o);
            yield return null;

            // ---- 3) 수면 비행 규칙
            var targeting = ship.GetComponent<TargetingSystem>() ?? ship.GetComponentInChildren<TargetingSystem>();
            var wigs = CombatDevTools.SpawnRing("ene_light_wig", 1, 26f, 30f);
            yield return new WaitForSeconds(0.6f);
            var wig = wigs.Count > 0 ? wigs[0] : null;
            if (wig != null) wig.DevFrozen = true;
            bool air = wig != null && wig.Kind == TargetKind.Aircraft;
            yield return new WaitForSeconds(1f);
            if (wig != null && Physics.Linecast(ship.transform.position + Vector3.up * 2.5f, wig.transform.position, out var blk, Islands.Mask, QueryTriggerInteraction.Ignore))
                _report.AppendLine($"  - 레이더 선을 가림: {blk.collider.name}({blk.collider.transform.root.name}) · {blk.point}");
            _report.AppendLine($"  - 레이더: 항공 {targeting?.DetectedAircraft.Count ?? -1} · 수면 비행 {targeting?.DetectedSkimmers.Count ?? -1} · 위그선 거리 {(wig != null ? Vector3.Distance(wig.transform.position, ship.transform.position) : 0f):0}m");
            bool asSkimmer = targeting != null && wig != null && targeting.GetBest(ship.transform.position, 80f, TargetClass.Surface | TargetClass.Skimmer, (t, sqr) => sqr) == (ITargetable)wig;
            bool notSurface = targeting != null && wig != null && targeting.GetBest(ship.transform.position, 80f, TargetClass.Surface, (t, sqr) => sqr) != (ITargetable)wig;
            float wigY = wig != null ? wig.transform.position.y : 0f;
            _report.AppendLine($"- 경위그선: 항공 표적 {Yes(air)} · 대함 유도탄 표적(수면 비행) {Yes(asSkimmer)} · 수상 전용 무기 표적 아님 {Yes(notSurface)} · 고도 {wigY:0.0}m");
            if (!air || !asSkimmer || !notSurface) Fail("수면 비행 규칙이 맞지 않음(경위그선)");
            if (wig != null)
            {
                yield return CloseShot("s4_light_wig", wig.transform, 9f);
                yield return CheckStage4Wreck(wig, ShipWreck.SinkMode.WigCrash, "s4_light_wig_down", 11f);
            }
            CombatDevTools.ClearBattlefield();
            yield return null;

            // ---- 4) 새 적
            var aw = CombatDevTools.SpawnRing("ene_assault_wig", 1, 60f, 0f);
            var assault = aw.Count > 0 ? aw[0] as AssaultWig : null;
            bool landedSurface = false, shotLanded = false;
            t0 = Time.time;
            while (assault != null && assault.IsAlive && Time.time - t0 < 30f && assault.DevBoatsLaunched < 2)
            {
                if (assault.Landed && assault.Kind == TargetKind.Surface) landedSurface = true;
                if (!shotLanded && assault.Door01 > 0.9f) { shotLanded = true; yield return CloseShot("s4_assault_wig_landed", assault.transform, 18f); }
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            yield return new WaitForSeconds(3f);
            bool flewAgain = assault != null && !assault.Landed;
            _report.AppendLine($"- 강습 위그선: 착수 {assault?.DevLandings ?? 0}번 · 착수 중 수상 표적 {Yes(landedSurface)} · 보트 {assault?.DevBoatsLaunched ?? 0}척(기대 2) · 다시 뜸 {Yes(flewAgain)} · {Time.time - t0:0.0}초");
            if (assault == null || assault.DevLandings < 1 || !landedSurface || assault.DevBoatsLaunched < 2 || !flewAgain) Fail("강습 위그선이 내려앉아 보트를 내리고 다시 뜨지 않음");
            if (assault != null) yield return CheckStage4Wreck(assault, ShipWreck.SinkMode.WigCrash, "s4_assault_wig_down", 23f);
            CombatDevTools.ClearBattlefield();
            yield return null;

            var hy = CombatDevTools.SpawnRing("ene_hydrofoil", 1, 45f, 90f);
            var cv = CombatDevTools.SpawnRing("ene_missile_corvette", 1, 32f, 200f);
            var hc = CombatDevTools.SpawnRing("ene_hovercraft", 1, 40f, 300f);
            var hydro = hy.Count > 0 ? hy[0] as Hydrofoil : null;
            var corvette = cv.Count > 0 ? cv[0] as MissileCorvette : null;
            float maxLift = 0f;
            t0 = Time.time;
            bool shotHydro = false;
            while (Time.time - t0 < 20f)
            {
                if (hydro != null && hydro.IsAlive) maxLift = Mathf.Max(maxLift, hydro.Lift);
                if (!shotHydro && hydro != null && hydro.Lift > 0.8f) { shotHydro = true; yield return CloseShot("s4_hydrofoil", hydro.transform, 12f); }
                ship.RepairHull(ship.HullMaxHp);
                if (corvette != null && corvette.DevMissiles >= 2 && maxLift > 0.6f) break;
                yield return null;
            }
            _report.AppendLine($"- 수중익정 선체 부상 최대 {maxLift:0.00}m(기대 0.6 이상) · 미사일 초계함 미사일 {corvette?.DevMissiles ?? 0} · 함포 {corvette?.DevGunShots ?? 0} · 공기부양정 {Yes(hc.Count > 0 && hc[0].IsAlive)}");
            if (maxLift < 0.6f) Fail("수중익정 선체가 떠오르지 않음");
            if (corvette == null || corvette.DevMissiles < 2) Fail("미사일 초계함이 미사일을 쏘지 않음");
            if (hc.Count > 0) yield return CloseShot("s4_hovercraft", hc[0].transform, 20f);
            if (corvette != null)
            {
                yield return CloseShot("s4_missile_corvette", corvette.transform, 16f);
                CheckSternLamp(corvette.transform);
            }
            if (hc.Count > 0) yield return CheckStage4Wreck(hc[0], ShipWreck.SinkMode.CushionCollapse, "s4_hovercraft_down", 22f);
            if (hydro != null) yield return CheckStage4Wreck(hydro, ShipWreck.SinkMode.FoilCollapse, "s4_hydrofoil_down", 19f);
            if (corvette != null) yield return CheckStage4Wreck(corvette, ShipWreck.SinkMode.Superstructure, "s4_corvette_down", 18f);
            CombatDevTools.ClearBattlefield();
            yield return null;

            // 플랫폼 포대: 70m에서 세운 뒤 부숴 기름띠
            if (platL != null && platL.BatterySpots != null && platL.BatterySpots.Length > 0)
            {
                Vector3 spot = platL.BatterySpots[0];
                Vector3 away = spot - platL.Center; away.y = 0f; away = away.sqrMagnitude > 0.01f ? away.normalized : -f;
                Put(platL.Center + away * 70f);
                CoastalBatteryField.Disabled = false;
                yield return new WaitForSeconds(1.5f);
                PlatformBattery pb = null;
                foreach (var e in CoastalBatteryField.Instance.Placed) if (e is PlatformBattery p && p.IsAlive) { pb = p; break; }
                _report.AppendLine($"- 플랫폼 포대: 섬 {Yes(pb != null)} · 높이 {(pb != null ? pb.transform.position.y : 0f):0.0}m · 놓인 고정 적 {CoastalBatteryField.Instance.ActiveCount} · 격침 기록 {CoastalBatteryField.Instance.DestroyedCount} · 자리까지 {Vector3.Distance(Flat(spot), Flat(ship.transform.position)):0}m · 데이터 {Yes(Resources.Load<EnemyDefinition>(IslandBuilder.PlatformBatteryPath) != null)}");
                if (pb == null) Fail("무장 플랫폼에 플랫폼 포대가 서지 않음");
                else
                {
                    yield return CloseShot("s4_platform_battery", pb.transform, 10f);
                    int slicks = OilSlick.Active;
                    pb.TakeDamage(new DamageInfo(9999f, pb.transform.position, Vector3.down, DamageSource.Gun, "검증"));
                    var batteryWreck = ShipWreck.Active.FindLast(w => w != null && w.Mode == ShipWreck.SinkMode.DeckCollapse);
                    if (batteryWreck == null || batteryWreck.FlyingCount == 0) Fail("플랫폼 포대의 부품이 갑판 위에서 떨어지지 않음");
                    else yield return CloseShot("s4_platform_battery_down", batteryWreck.transform, 13f);
                    yield return new WaitForSeconds(1.5f);
                    _report.AppendLine($"  - 부서진 뒤 불타는 기름띠 {OilSlick.Active - slicks}개(기대 1)");
                    if (OilSlick.Active <= slicks) Fail("플랫폼 포대가 부서져도 기름띠가 번지지 않음");
                    yield return AerialShot("s4_oil_slick", platL.OilSlickPoint, f, 30f, 22f);
                }
                CoastalBatteryField.Disabled = true;
                CombatDevTools.ClearBattlefield();
            }
            Put(o);
            yield return new WaitForSeconds(0.5f);

            // ---- 5) 룬급
            var bossList = CombatDevTools.SpawnRing("ene_boss4", 1, 60f, 0f);
            var lun = bossList.Count > 0 ? bossList[0] as LunBoss : null;
            if (lun == null) { Fail("룬급 보스를 만들지 못함"); yield break; }
            _report.AppendLine($"- 룬급: 엔진 {lun.EngineCount}(기대 8) · 발사관 쌍 {lun.LauncherCount}(기대 3) · 부위 {lun.Parts.Parts.Count} · 보스 막대 칸 {lun.Parts.ChipCount}");
            if (lun.EngineCount != 8 || lun.LauncherCount != 3) Fail("룬급 모델 소켓(엔진 8 · 발사관 3쌍)을 다 찾지 못함");
            bool lineSeen = false, shotLine = false, shotPass = false;
            t0 = Time.time;
            while (Time.time - t0 < 40f && lun.IsAlive && (lun.DevPasses < 2 || lun.DevMissiles < 2))
            {
                lun.DevSkipWait();
                if (lun.LineVisible) lineSeen = true;
                if (!shotLine && lun.LineVisible) { shotLine = true; yield return AerialShot("s4_lun_warning", ship.transform.position, f, 70f, 55f); }
                if (!shotPass && lun.StateName == "Pass" && Vector3.Distance(lun.transform.position, ship.transform.position) < 24f) { shotPass = true; yield return CloseShot("s4_lun_pass", lun.transform, 30f); }
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            _report.AppendLine($"  - 1단계: 통과선 예고 {Yes(lineSeen)} · 통과 {lun.DevPasses} · 미사일 {lun.DevMissiles} · 꼬리 기관포 {lun.DevTailShots} · {Time.time - t0:0.0}초 · 날 때 항공 표적 {Yes(lun.Kind == TargetKind.Aircraft)}");
            if (!lineSeen || lun.DevPasses < 1 || lun.DevMissiles < 2) Fail("룬급이 예고 뒤 통과하며 미사일을 쏘지 않음");

            // 2단계: 체력 60%로 · 미사일 0 → 재장전 착수
            lun.TakeDamage(new DamageInfo(lun.Definition.MaxHp * 0.4f, Vector3.zero, Vector3.down, DamageSource.Gun, "검증"));
            lun.DevSetAmmo(0);
            bool landedAsSurface = false, shotRearm = false;
            float landedBonus = 0f;
            t0 = Time.time;
            while (Time.time - t0 < 45f && lun.IsAlive && lun.DevRearms < 1) { lun.DevSkipWait(); ship.RepairHull(ship.HullMaxHp); yield return null; }
            yield return new WaitForSeconds(0.5f);
            if (lun.Landed && lun.Kind == TargetKind.Surface) landedAsSurface = true;
            if (lun.StateName == "Rearm")
            {
                shotRearm = true;
                yield return CloseShot("s4_lun_rearm", lun.transform, 34f);
                // 착수 중 피해 1.5배
                var hpField = typeof(EnemyController).GetField("_hp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                float h0 = (float)hpField.GetValue(lun);
                lun.TakeDamage(new DamageInfo(10f, Vector3.zero, Vector3.down, DamageSource.Torpedo, "검증"));
                landedBonus = h0 - (float)hpField.GetValue(lun);
            }
            _report.AppendLine($"  - 2단계({lun.PhaseName}): 재장전 착수 {lun.DevRearms}번 · 바지선 {Yes(lun.Barge != null)} · 착수 중 수상 표적 {Yes(landedAsSurface)} · 피해 10 → {landedBonus:0.0}(기대 15) · {Time.time - t0:0.0}초");
            if (lun.DevRearms < 1 || !landedAsSurface || !shotRearm || Mathf.Abs(landedBonus - 15f) > 0.5f) Fail("룬급이 재장전하러 내려앉지 않음(수상 표적 · 피해 1.5배)");

            // 4단계: 엔진 5기 파괴 → 추락 활주 · 돌진
            int broke = 0;
            for (int i = 0; i < lun.EngineCount && broke < 5; i++)
            {
                var part = lun.EnginePart(i);
                if (part == null || part.Destroyed) continue;
                int guard = 0;
                while (!part.Destroyed && guard++ < 40) lun.TakeDamage(new DamageInfo(part.MaxHp * 0.5f, part.Anchor.position, Vector3.down, DamageSource.Gun, "검증"));
                broke++;
            }
            yield return new WaitForSeconds(0.5f);
            bool grounded = lun.Phase == 3 && lun.Landed && lun.Kind == TargetKind.Surface;
            t0 = Time.time;
            bool shotTaxi = false;
            while (Time.time - t0 < 25f && lun.IsAlive && lun.DevCharges < 1)
            {
                if (!shotTaxi && Time.time - t0 > 1.5f) { shotTaxi = true; yield return CloseShot("s4_lun_crash", lun.transform, 30f); }
                ship.RepairHull(ship.HullMaxHp);
                yield return null;
            }
            yield return new WaitForSeconds(chargeWait);
            _report.AppendLine($"  - 엔진 {lun.EnginesDown}기 파괴 → {lun.PhaseName}: 수면 활주(수상 표적) {Yes(grounded)} · 돌진 {lun.DevCharges}번 · 들이받음 {lun.DevRams} · 보스 막대 \"{lun.PhaseName}\"");
            if (!grounded || lun.DevCharges < 1) Fail("엔진 5기를 부숴도 룬급이 추락 활주 · 돌진으로 넘어가지 않음");
            yield return CheckLunFinale(lun);
            CombatDevTools.ClearBattlefield();
            CoastalBatteryField.Disabled = false;
            ship.DevRudderOverride = null;
            foreach (var b in off) if (b != null) b.enabled = true;
        }

        private const float chargeWait = 3f;

        private void CheckSternLamp(Transform ship)
        {
            var lamps = NavLights.Ensure(ship.gameObject);
            var bulbs = new List<Transform>();
            foreach (Transform t in ship)
                if (t.name == "Night lamp") bulbs.Add(t);
            float ratio = bulbs.Count == NavLights.PerShip
                ? bulbs[3].lossyScale.x / bulbs[0].lossyScale.x : 0f;
            _report.AppendLine($"- 선미등: 현측등 지름 대비 {ratio:P1}(기대 7.5%, 기존 선미등의 10%)");
            if (lamps == null || Mathf.Abs(ratio - .075f) > .001f) Fail("선미등 지름이 이전 값의 10%가 아님");
        }

        private IEnumerator CheckStage4Wreck(EnemyController enemy, ShipWreck.SinkMode mode, string shot, float distance)
        {
            if (!enemy.IsAlive)
            {
                Fail($"{shot}: 확인할 적이 생존하지 않음");
                yield break;
            }
            ShipWreck.TrySpawnStage4(enemy, enemy.CurrentSpeed, DamageSource.Gun, enemy.transform.position);
            var wreck = ShipWreck.Active.FindLast(w => w != null && w.Mode == mode && w.Age == 0f);
            enemy.RemoveWithoutReward();
            if (wreck == null || wreck.PartCount < 2 || wreck.GetComponentInChildren<Collider>() != null)
            {
                Fail($"{shot}: 장식 잔해가 생성되지 않음");
                yield break;
            }
            yield return new WaitForSeconds(1.25f);
            _report.AppendLine($"- {shot}: {wreck.PartCount} 부품 · 경사 {wreck.RollDegrees:0.0}° · 파편 {wreck.FlyingCount}");
            if (wreck.FlyingCount == 0 || (mode != ShipWreck.SinkMode.Superstructure && !wreck.HitSurface))
                Fail($"{shot}: 파손·수면 접촉 연출 누락");
            yield return CloseShot(shot, wreck.transform, distance);
        }

        private IEnumerator CheckLunFinale(LunBoss lun)
        {
            lun.DevFrozen = true;
            lun.transform.position = new Vector3(lun.transform.position.x, 1.7f, lun.transform.position.z);
            ShipWreck.TrySpawnStage4(lun, 0f, DamageSource.Missile, lun.transform.position);
            lun.RemoveWithoutReward();
            var wreck = ShipWreck.Active.FindLast(w => w != null && w.Mode == ShipWreck.SinkMode.LunCrash);
            if (wreck == null)
            {
                Fail("룬급 최종 잔해가 없음");
                yield break;
            }
            ShipWreck.BeginFinale();
            var result = FindFirstObjectByType<Game.UI.ResultScreenUI>();
            GameManager.Instance.TriggerVictory();
            if (!ShipWreck.PlayingFinale || Time.timeScale != 0f || result == null || result.IsShown)
                Fail("룬급 연출 도중 승리 화면이 먼저 뜨거나 전투 시간이 멈추지 않음");
            yield return new WaitForSecondsRealtime(1.15f);
            if (!wreck.HitSurface || wreck.FlyingCount == 0) Fail("정지 상태의 룬급이 수면에 부딪히지 않음");
            var camera = Camera.main;
            Vector3 viewport = camera != null ? camera.WorldToViewportPoint(wreck.transform.position) : Vector3.zero;
            if (viewport.z <= 0f || viewport.x < .12f || viewport.x > .88f || viewport.y < .12f || viewport.y > .88f)
                Fail("승리 연출 카메라에 룬급 잔해가 들어오지 않음");
            yield return CloseShot("s4_lun_down", wreck.transform, 28f);
            yield return new WaitForSecondsRealtime(3.3f);
            _report.AppendLine($"- 룬급 최종 파괴 종료 {Yes(!ShipWreck.PlayingFinale)} · 승리 화면 {Yes(result != null && result.IsShown)}");
            if (ShipWreck.PlayingFinale || result == null || !result.IsShown) Fail("룬급 연출이 끝난 뒤 승리 화면이 열리지 않음");
        }
    }
}
#endif
