#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Progression;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 생물·장식·도감·환경 아트·섬·바다 외형 검사.</summary>
    public partial class CombatVerificationRunner
    {
        // ------------------------------------------------------------ 바다 생물 · 섬 장식 · 폭발

        /// <summary>
        /// 섬 장식(등대·난파선·물개), 갈매기(섬 위·함선 뒤), 돌고래(전속 중 뱃머리 옆 도약), 고래(물 뿜기·꼬리),
        /// 폭발(미사일 명중·자폭 보트 자폭·격침·자폭 드론)이 나오는지 보고 캡처한다.
        /// </summary>
        private IEnumerator LifeCheck()
        {
            _report.AppendLine("\n## 바다 생물 · 섬 장식 · 폭발");
            var ship = GameManager.Instance.Player;
            var field = Game.World.IslandField.Instance;
            var life = Game.View.SeaLife.Instance;
            if (life == null) Fail("SeaLife가 없음");
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);

            // 1) 섬 장식: 모든 장식을 켠 무인도와 바위섬을 함선 옆에 놓는다
            var view = new GameObject("Life view").transform;
            if (field != null)
            {
                Game.World.IslandField.Disabled = true;
                field.ClearAll();
                Game.World.IslandBuilder.ForceAllDecor = true;
                Vector3 right = ship.transform.right; right.y = 0f; right.Normalize();
                var green = field.PlaceAt(ship.transform.position + right * 42f, 13f, false, 7);
                var rock = field.PlaceAt(ship.transform.position - right * 38f, 9f, true, 11);
                Game.World.IslandBuilder.ForceAllDecor = false;
                int lamps = 0;
                foreach (var l in Object.FindObjectsByType<Game.World.IslandLights>(FindObjectsSortMode.None)) lamps += l.LampCount;
                bool wreckCollider = false;
                var wreckT = green.Root.transform.Find("Beached wreck");
                if (wreckT != null) foreach (var c in wreckT.GetComponentsInChildren<MeshCollider>()) if (c.gameObject.layer == Game.World.Islands.Layer) wreckCollider = true;
                _report.AppendLine($"- 무인도(반경 13): 등대 {(green.Lighthouse ? "있음" : "없음")} · 난파선 {(green.Wreck ? "있음" : "없음")}(섬 콜라이더 {(wreckCollider ? "예" : "아니오")}) · 물개 {green.Seals} / 바위섬(반경 9): 등대 {(rock.Lighthouse ? "있음" : "없음")} · 물개 {rock.Seals} · 등불 {lamps}개");
                if (!green.Lighthouse || !green.Wreck) Fail("무인도 장식(등대·난파선)이 없음");
                if (!wreckCollider) Fail("난파선이 섬 콜라이더가 아님(배가 뚫고 지나감)");
                if (rock.Seals + green.Seals == 0) Fail("물개가 없음");
                if (lamps == 0) Fail("등대 불빛이 없음");

                // 자동 생성 섬의 장식 빈도(시드 3개)
                int islands = 0, lh = 0, wr = 0, sealIslands = 0;
                foreach (int seed in new[] { 101, 202, 303 })
                {
                    field.ClearAll();
                    Game.World.IslandField.Disabled = false;
                    field.Regenerate(seed, keepClearAroundPlayer: false);
                    Game.World.IslandField.Disabled = true;
                    foreach (var i in Game.World.Islands.All) { islands++; if (i.Lighthouse) lh++; if (i.Wreck) wr++; if (i.Seals > 0) sealIslands++; }
                }
                _report.AppendLine($"- 자동 생성 섬 {islands}개(시드 3개): 등대 {lh} · 난파선 {wr} · 물개 있는 섬 {sealIslands}");
                if (islands > 0 && lh + wr + sealIslands == 0) Fail("자동 생성 섬에 장식이 하나도 없음");

                field.ClearAll();
                Game.World.IslandBuilder.ForceAllDecor = true;
                green = field.PlaceAt(ship.transform.position + right * 42f, 13f, false, 7);
                rock = field.PlaceAt(ship.transform.position - right * 38f, 9f, true, 11);
                Game.World.IslandBuilder.ForceAllDecor = false;
                yield return new WaitForSeconds(2.5f);   // 갈매기 무리가 생길 시간
                view.SetPositionAndRotation(green.Center, Quaternion.identity);
                yield return CloseShot("life_island_green", view, 30f);
                view.SetPositionAndRotation(rock.Center, Quaternion.identity);
                yield return CloseShot("life_island_rock", view, 26f);
                yield return Shot("life_overview");
            }

            // 2) 갈매기
            if (life != null)
            {
                _report.AppendLine($"- 갈매기: 전체 {life.GullCount}마리 · 함선을 따라다니는 {life.FollowerCount}마리 · 섬 무리 기록 {life.IslandFlockCount}");
                if (life.FollowerCount == 0) Fail("함선을 따라다니는 갈매기가 없음");
            }

            // 3) 돌고래 · 고래(섬은 치우고 전속)
            if (field != null) field.ClearAll();
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(4f);
            if (life != null)
            {
                life.ForceDolphins();
                float t0 = Time.time;
                bool shot = false;
                while (Time.time - t0 < 8f)
                {
                    if (!shot && life.DolphinCount > 0 && life.DolphinMaxLeap > 0.8f)
                    {
                        shot = true;
                        yield return CloseShot("life_dolphins", ship.transform, 20f);
                    }
                    yield return null;
                }
                _report.AppendLine($"- 돌고래: {life.DolphinCount}마리(무리 진행 중) · 최고 도약 {life.DolphinMaxLeap:0.0}m · 함선 {Game.Ship.ShipController.ToKnots(ship.CurrentSpeed):0}노트");
                if (life.DolphinMaxLeap < 0.5f) Fail("돌고래가 뛰어오르지 않음");

                ship.SetEngineOrder(0.25f);
                life.ForceWhale();
                yield return null;
                yield return null;
                t0 = Time.time;
                bool spoutShot = false, flukeShot = false;
                while (Time.time - t0 < 10f && (life.WhaleActive || Time.time - t0 < 0.5f))
                {
                    float age = Time.time - t0;
                    if (!spoutShot && age > 1.9f) { spoutShot = true; yield return Shot("life_whale_spout"); if (life.WhaleTransform != null) { view.SetPositionAndRotation(life.WhaleTransform.position, Quaternion.identity); yield return CloseShot("life_whale_spout_close", view, 26f); } }
                    if (!flukeShot && age > 6.3f && life.WhaleTransform != null) { flukeShot = true; view.SetPositionAndRotation(life.WhaleTransform.position, Quaternion.identity); yield return CloseShot("life_whale_fluke", view, 26f); }
                    yield return null;
                }
                _report.AppendLine($"- 고래: 물 뿜기 입자 {life.WhaleSpouts} · 꼬리 최고 높이 수면 위 {life.WhaleFlukeMaxY + 0.9f:0.0}m · 끝난 뒤 사라짐 {(!life.WhaleActive ? "예" : "아니오")}");
                if (life.WhaleSpouts == 0) Fail("고래가 물을 뿜지 않음");
                if (life.WhaleFlukeMaxY < -0.9f) Fail("고래 꼬리가 물 위로 올라오지 않음");
            }

            // 4) 폭발: 정지, 무장 끔
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(5f);
            int e0 = Game.View.Explosions.SpawnCount;
            CombatDevTools.MissileVolley(3, 40f, 30f, 1f);
            float s0 = Time.time;
            bool mShot = false;
            while (Time.time - s0 < 8f)
            {
                if (!mShot && Game.View.Explosions.SpawnCount > e0) { mShot = true; yield return new WaitForSeconds(0.08f); yield return CloseShot("life_missile_hit", ship.transform, 22f); }
                yield return null;
            }
            int missileBlasts = Game.View.Explosions.SpawnCount - e0;

            e0 = Game.View.Explosions.SpawnCount;
            int wrecks0 = Game.View.ShipWreck.Active.Count;
            var boats = CombatDevTools.SpawnRing("ene_suicide_boat", 1, 40f, 150f);
            s0 = Time.time;
            bool bShot = false;
            while (Time.time - s0 < 10f)
            {
                if (!bShot && boats.Count > 0 && boats[0] is SuicideBoat sb && sb.Detonated) { bShot = true; yield return new WaitForSeconds(0.1f); yield return CloseShot("life_suicide_blast", ship.transform, 24f); break; }
                yield return null;
            }
            int boatBlasts = Game.View.Explosions.SpawnCount - e0;

            // 격침(폭약 유폭): 잔해 없이 폭발만
            e0 = Game.View.Explosions.SpawnCount;
            var shotBoat = CombatDevTools.SpawnRing("ene_suicide_boat", 1, 45f, 60f);
            yield return new WaitForSeconds(0.5f);
            foreach (var b in shotBoat) if (b != null && b.IsAlive) b.TakeDamage(new DamageInfo(999f, b.transform.position, Vector3.down, DamageSource.Gun));
            int killBlasts = Game.View.Explosions.SpawnCount - e0;
            int killWrecks = Game.View.ShipWreck.Active.Count - wrecks0;

            e0 = Game.View.Explosions.SpawnCount;
            var drones = CombatDevTools.SpawnRing("ene_drone", 2, 45f, 200f);
            s0 = Time.time;
            bool dShot = false;
            while (Time.time - s0 < 12f)
            {
                int alive = 0;
                foreach (var d in drones) if (d != null && d.isActiveAndEnabled && d.IsAlive) alive++;
                if (!dShot && Game.View.Explosions.SpawnCount > e0) { dShot = true; yield return new WaitForSeconds(0.08f); yield return CloseShot("life_drone_blast", ship.transform, 22f); }
                if (alive == 0) break;
                yield return null;
            }
            int droneBlasts = Game.View.Explosions.SpawnCount - e0;
            _report.AppendLine($"- 폭발: 대함미사일 3발 → {missileBlasts} · 자폭 보트 충돌 → {boatBlasts} · 자폭 보트 격침 → {killBlasts}(잔해 {killWrecks}) · 자폭 드론 2기 → {droneBlasts}");
            if (missileBlasts < 3) Fail("미사일 명중 폭발이 부족함");
            if (boatBlasts < 1) Fail("자폭 보트 자폭 폭발이 없음");
            if (killBlasts < 1 || killWrecks != 0) Fail("자폭 보트 격침 폭발이 없거나 잔해가 남음");
            if (droneBlasts < 2) Fail("자폭 드론 폭발이 부족함");

            Destroy(view.gameObject);
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        // ------------------------------------------------------------ 장식(격침·물보라·연기·구름)

        /// <summary>
        /// 장식 효과: 구름 그림자 쿠키가 해에 붙어 흐르는지, 속력에 따라 뱃머리 물보라·연돌 연기가 나오는지(기어 올림 검은 연기),
        /// 수상함 격침 잔해가 기울며 가라앉고 기름띠·잔해 조각을 남긴 뒤 사라지는지, 잠항 잠수함은 잔해가 없는지,
        /// 잔해에 콜라이더가 없는지(판정 없음). 캡처 포함.
        /// </summary>
        private IEnumerator DecorCheck()
        {
            _report.AppendLine("\n## 장식(격침·물보라·연기·구름 그림자)");
            var ship = GameManager.Instance.Player;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            Game.View.ShipWreck.ClearAll();
            var off = DisableWeapons(null);

            // 1) 구름 그림자
            var clouds = Game.View.CloudShadows.Instance;
            Vector2 o0 = clouds != null ? clouds.Offset : Vector2.zero;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(4f);
            float drift = clouds != null ? (clouds.Offset - o0).magnitude : 0f;
            _report.AppendLine($"- 구름 그림자: {(clouds != null && clouds.Applied ? "해에 쿠키 적용" : "없음")} · 4초에 {drift:0.0}m 흐름");
            if (clouds == null || !clouds.Applied) Fail("구름 그림자 쿠키가 해에 붙지 않음");
            if (drift < 1f) Fail("구름 그림자가 흐르지 않음");

            // 2) 정지 → 전속: 물보라·연기
            var wake = ship.GetComponent<Game.View.ShipWake>();
            var smoke = ship.GetComponent<Game.View.FunnelSmoke>();
            int idleSpray = Game.View.DecorFx.Count(Game.View.DecorFx.Spray);
            int idleSmoke = Game.View.DecorFx.Count(Game.View.DecorFx.Smoke);
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(0.4f);
            float puff = smoke != null ? smoke.Puff : 0f;
            yield return new WaitForSeconds(7f);
            int fullSpray = Game.View.DecorFx.Count(Game.View.DecorFx.Spray);
            int fullSmoke = Game.View.DecorFx.Count(Game.View.DecorFx.Smoke);
            _report.AppendLine($"- 정지: 물보라 {idleSpray} · 연기 {idleSmoke} / 전속 {Game.Ship.ShipController.ToKnots(ship.CurrentSpeed):0}노트: 물보라 {fullSpray} · 연기 {fullSmoke} · 연돌 {(smoke != null ? smoke.StackCount : 0)}개 · 기어 올림 직후 검은 연기 {puff:0.00}");
            if (smoke == null || smoke.StackCount == 0) Fail("플레이어 함선 연돌을 찾지 못함");
            if (idleSpray > 0) Fail("정지 중에 물보라가 튐");
            if (fullSpray <= 0 || wake == null || !wake.Spraying) Fail("전속에서 물보라가 없음");
            if (fullSmoke <= idleSmoke) Fail("전속에서 연기가 늘지 않음");
            if (puff < 0.5f) Fail("기어를 올려도 검은 연기가 나오지 않음");
            yield return CloseShot("decor_spray_smoke", ship.transform, 24f);
            yield return Shot("decor_clouds");

            // 구름 그림자가 실제로 그려지는지: 같은 장면을 켜고/끄고 찍어 밝기를 비교
            if (clouds != null)
            {
                Time.timeScale = 0f;
                var on = RenderMain();
                clouds.enabled = false;
                yield return null;
                var offPx = RenderMain();
                clouds.enabled = true;
                Time.timeScale = TimeScale;
                int darker = 0; double sumOn = 0, sumOff = 0;
                for (int i = 0; i < on.Length; i++)
                {
                    float a = on[i].r + on[i].g + on[i].b, b = offPx[i].r + offPx[i].g + offPx[i].b;
                    sumOn += a; sumOff += b;
                    if (a < b * 0.9f) darker++;
                }
                float frac = darker / (float)on.Length;
                _report.AppendLine($"- 구름 켬/끔 비교: 그늘진 화면 {frac:P0} · 평균 밝기 {sumOn / Mathf.Max(1f, (float)sumOff):P0} (무늬 전체 그늘 비율 {Game.View.CloudShadows.ShadedFraction:P0})");
                if (frac < 0.05f) Fail("구름 그림자가 화면에 그려지지 않음(쿠키 미적용)");
            }

            // 3) 격침 잔해: 정지한 함선 앞쪽에 초계함·고속정·미사일정, 옆에 잠항 잠수함
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(6f);
            var pcc = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 34f, 20f);
            var boat = CombatDevTools.SpawnRing("ene_fastboat", 1, 30f, 80f);
            var mboat = CombatDevTools.SpawnRing("ene_missileboat", 1, 32f, -50f);
            var sub = CombatDevTools.SpawnRing("ene_submarine", 1, 30f, 180f);
            yield return new WaitForSeconds(1f);
            float realStart = Time.realtimeSinceStartup; int frames0 = Time.frameCount;
            yield return new WaitForSeconds(2f);
            float baseMs = (Time.realtimeSinceStartup - realStart) * 1000f / Mathf.Max(1, Time.frameCount - frames0);

            int subWrecks0 = Game.View.ShipWreck.Active.Count;
            foreach (var e in sub) if (e != null && e.IsAlive && !e.IsRevealed) e.TakeDamage(new DamageInfo(9999f, e.transform.position, Vector3.down, DamageSource.Torpedo));
            int subWrecks = Game.View.ShipWreck.Active.Count - subWrecks0;
            _report.AppendLine($"- 잠항 잠수함 격침: 잔해 {subWrecks}개(모델이 안 보이므로 0이어야 함)");
            if (subWrecks != 0) Fail("잠항 잠수함 격침에 잔해가 생김");

            var killed = new List<EnemyController>();
            killed.AddRange(pcc); killed.AddRange(boat); killed.AddRange(mboat);
            foreach (var e in killed) if (e != null && e.IsAlive) e.TakeDamage(new DamageInfo(9999f, e.transform.position, Vector3.down, DamageSource.Gun));
            var wrecks = new List<Game.View.ShipWreck>(Game.View.ShipWreck.Active);
            string Parts() { var sb = new StringBuilder(); foreach (var w in wrecks) if (w != null) sb.Append($"{w.name.Replace("Wreck ", "")}: 부품 {w.PartCount} · {w.Duration:0.0}초 / "); return sb.ToString(); }
            _report.AppendLine($"- 수상함 3척 격침 → 잔해 {wrecks.Count}개: {Parts()}");
            if (wrecks.Count != 3) Fail("수상함 격침 잔해 수가 맞지 않음");
            foreach (var w in wrecks) if (w != null && w.GetComponentInChildren<Collider>() != null) Fail("잔해에 콜라이더가 있음(판정에 영향)");

            var big = wrecks.Count > 0 ? wrecks[0] : null;
            foreach (var w in wrecks) if (w != null && big != null && w.Duration > big.Duration) big = w;
            if (big == null) { Fail("잔해 없음"); yield break; }
            // 잔해는 기울어지므로 카메라는 똑바로 선 대리점을 따라간다(물속에서 찍지 않게)
            var view = new GameObject("Wreck view").transform;
            void Aim() { if (big != null) view.SetPositionAndRotation(new Vector3(big.transform.position.x, 0f, big.transform.position.z), Quaternion.Euler(0f, big.transform.eulerAngles.y, 0f)); }
            yield return new WaitForSeconds(1.2f);
            Aim();
            yield return CloseShot("decor_wreck_1_burning", view, 30f);
            realStart = Time.realtimeSinceStartup; frames0 = Time.frameCount;
            yield return new WaitForSeconds(big.Duration * 0.45f);
            float wreckMs = (Time.realtimeSinceStartup - realStart) * 1000f / Mathf.Max(1, Time.frameCount - frames0);
            float midRoll = big.RollDegrees, midDepth = big.SinkDepth;
            Aim();
            yield return CloseShot("decor_wreck_2_listing", view, 30f);
            int wreckSmoke = Game.View.DecorFx.Count(Game.View.DecorFx.WreckSmoke), fire = Game.View.DecorFx.Count(Game.View.DecorFx.Fire);
            while (big != null && big.Age < big.Duration + 1.5f) yield return null;
            bool slick = big != null && big.HasSlick;
            int debris = big != null ? big.DebrisCount : 0;
            _report.AppendLine($"- 초계함 잔해: 중간 횡경사 {midRoll:0}° · 침하 {midDepth:0.0}m · 연기 {wreckSmoke} · 불꽃 {fire} / 가라앉은 뒤 기름띠 {(slick ? "있음" : "없음")} · 떠 있는 조각 {debris}");
            if (Mathf.Abs(midRoll) < 15f) Fail("잔해가 기울지 않음");
            if (midDepth < 0.3f) Fail("잔해가 가라앉지 않음");
            if (wreckSmoke <= 0) Fail("격침 연기가 없음");
            if (!slick || debris == 0) Fail("기름띠·잔해 조각이 남지 않음");
            Aim();
            yield return CloseShot("decor_wreck_3_slick", view, 26f);
            Destroy(view.gameObject);

            _report.AppendLine($"- 프레임 시간(실시간, 2배속): 잔해 전 {baseMs:0.0}ms · 잔해 3척 불타는 중 {wreckMs:0.0}ms");

            float t0 = Time.time;
            while (Game.View.ShipWreck.Active.Count > 0 && Time.time - t0 < 30f) yield return null;
            _report.AppendLine($"- 잔해 전부 사라짐: {(Game.View.ShipWreck.Active.Count == 0 ? $"예({Time.time - t0 + big.Duration + 1.5f:0}초 뒤)" : "아니오")}");
            if (Game.View.ShipWreck.Active.Count > 0) Fail("잔해가 사라지지 않음");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        /// <summary>
        /// 편대 진형·조함(2026-10-03): 진형 5종(복렬·종렬·단열·사열·능형)마다 직진 중 슬롯 수렴·기함과 겹치지 않음 · 캡처,
        /// 급선회 중 자연스러움(옆미끄러짐 없음 · 선회율 한도 · 슬롯 이탈 거리) — 예전 방식(슬롯을 기함 침로로 즉시 돌림)이면 필요했을 횡속도와 비교,
        /// 종렬진은 항적을 따라감, 진형 전환 키(G)와 순환.
        /// </summary>
        /// <summary>
        /// 무장 팩 v8: 기관포·76mm·CIWS의 기본/U1/U2 외형마다 조준 소켓(TurretPivot·ElevationPivot·Muzzle, CIWS는 BarrelCluster)이 있고
        /// 포구가 블록 앞(+Z)으로 나와 있다(조준 보정은 고각축→포구 수평 방향을 정면으로 삼는다). 설치 후 강화하면 무기가 그 단계 외형의 포구에 묶인다.
        /// 사전: 메인 화면 버튼 · 탭 3개 항목 수가 게임 데이터와 같음 · 항목마다 본문 · 모든 미리보기 모델에 메시 · 탭별 캡처.
        /// </summary>
        private IEnumerator CodexCheck()
        {
            _report.AppendLine("\n## 사전 · 무장 팩 v8");
            // 1) 무장 외형 소켓·포구 방향(프리팹 그대로, 설치 전)
            foreach (var (id, ciws) in new[] { ("mod_autocannon", false), ("mod_gun76", false), ("mod_ciws", true) })
            {
                var def = CombatDevTools.FindModule(id);
                var vis = def != null && def.Prefab != null ? def.Prefab.GetComponent<ModuleUpgradeVisuals>() : null;
                if (vis == null || vis.VariantCount < 3) { Fail($"{id}: 강화 외형 3단계가 없음"); continue; }
                var holder = new GameObject("codex check holder");
                holder.SetActive(false);
                var copy = Object.Instantiate(def.Prefab, holder.transform);
                copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var sb = new StringBuilder();
                for (int l = 1; l <= 3; l++)
                {
                    var level = FindChild(copy.transform, $"Visual_Level{l}");
                    var elev = level != null ? FindChild(level, "ElevationPivot") : null;
                    var muzzle = level != null ? FindChild(level, "Muzzle") : null;
                    bool sockets = level != null && FindChild(level, "TurretPivot") != null && elev != null && muzzle != null &&
                                   (!ciws || FindChild(level, "BarrelCluster") != null);
                    if (!sockets) { Fail($"{def.DisplayName} {l}단계: 조준 소켓 없음"); continue; }
                    Vector3 barrel = Vector3.ProjectOnPlane(muzzle.position - elev.position, Vector3.up);
                    float align = barrel.sqrMagnitude > 1e-4f ? Vector3.Dot(barrel.normalized, Vector3.forward) : 0f;
                    int meshes = level.GetComponentsInChildren<MeshFilter>(true).Length;
                    sb.Append($"{l}단계 포구 정렬 {align:0.00}·포구 앞 {barrel.magnitude:0.00}m·메시 {meshes}  ");
                    if (align < 0.95f) Fail($"{def.DisplayName} {l}단계: 포구가 블록 앞을 향하지 않음({align:0.00})");
                }
                Object.Destroy(holder);
                _report.AppendLine($"- {def.DisplayName}: {sb}");
            }

            // 설치 후 강화: 단계마다 그 외형이 켜지고 무기가 그 외형의 포구에 묶인다
            foreach (var id in new[] { "mod_autocannon", "mod_gun76", "mod_ciws" })
            {
                var m = CombatDevTools.InstallModuleNear(id, new GridCoord(0, 0), 8);
                if (m == null || m.Runtime == null) { Fail($"{id}: 시험 설치 실패"); continue; }
                var line = new StringBuilder();
                for (int lv = 1; lv <= 3; lv++)
                {
                    if (lv > 1) m.ApplyUpgrade();
                    yield return null;
                    var v = m.Runtime.GetComponent<ModuleUpgradeVisuals>();
                    var w = Get<WeaponController>(m.Runtime, "weapon");
                    string muzzleIn = "-";
                    if (w?.Muzzle != null)
                        for (var t = w.Muzzle; t != null; t = t.parent)
                            if (t.name.StartsWith("Visual_Level")) { muzzleIn = t.name; break; }
                    bool ok = v != null && v.ActiveIndex == lv - 1 && muzzleIn == $"Visual_Level{lv}" && w.FireDirection.sqrMagnitude > 0.5f;
                    line.Append($"{lv}단계 외형 {(v != null ? v.ActiveIndex + 1 : 0)}·포구 {muzzleIn}  ");
                    if (!ok) Fail($"{id} {lv}단계: 외형 또는 포구 연결이 맞지 않음");
                }
                _report.AppendLine($"- 설치 {id}: {line}");
                CombatDevTools.RemoveModule(m);
            }

            // 2) 메인 화면 사전
            UnityEngine.UI.Button codexButton = null;
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.name.StartsWith("사전")) { codexButton = b; break; }
            if (codexButton == null) { Fail("메인 화면에 사전 버튼이 없음"); yield break; }
            GameObject menu = null;
            for (var t = codexButton.transform; t != null; t = t.parent)
                if (t.name.StartsWith("Fleet command")) { menu = t.gameObject; break; }
            if (menu != null) menu.SetActive(true);
            GameManager.Instance.SetState(GameState.Paused);
            yield return null;
            yield return ScreenShot("codex_main_menu");
            codexButton.onClick.Invoke();
            yield return null;
            var codex = Object.FindFirstObjectByType<Game.UI.CodexUI>();
            if (codex == null) { Fail("사전 버튼을 눌러도 사전이 열리지 않음"); yield break; }

            int wantWeapons = Game.UI.CodexCatalog.CollectModules().Count;
            int wantEnemies = Game.UI.CodexCatalog.CollectEnemies().Count;
            var noModel = new List<string>();
            var emptyBody = new List<string>();
            float minCover = 1f;
            string minCoverName = "-";
            foreach (var section in new[] { Game.UI.CodexSection.Weapons, Game.UI.CodexSection.Escorts, Game.UI.CodexSection.Enemies })
            {
                codex.ShowSection(section);
                var list = codex.Entries(section);
                var names = new StringBuilder();
                for (int i = 0; i < list.Count; i++)
                {
                    codex.Select(i);
                    if (string.IsNullOrWhiteSpace(codex.BodyText) || codex.BodyText.Length < 40) emptyBody.Add(list[i].Title);
                    for (int k = 0; k < list[i].Models.Count; k++)
                    {
                        codex.SelectModel(k);
                        // 메시가 올라갔는지 + 실제로 그려져 화면을 차지하는지(배경과 다른 픽셀 비율)
                        float cover = codex.MeasurePreview();
                        if (cover < minCover) { minCover = cover; minCoverName = $"{list[i].Title}({list[i].Models[k].Label})"; }
                        if (codex.PreviewRendererCount == 0 || cover < 0.02f) noModel.Add($"{list[i].Title}({list[i].Models[k].Label}) {cover * 100f:0.#}%");
                    }
                    names.Append(list[i].Title).Append(list[i].Models.Count > 1 ? $"[{list[i].Models.Count}]" : "").Append(' ');
                }
                _report.AppendLine($"- {Game.UI.CodexCatalog.SectionName(section)} {list.Count}종: {names}");
                int want = section == Game.UI.CodexSection.Weapons ? wantWeapons : section == Game.UI.CodexSection.Enemies ? wantEnemies : 5;
                if (list.Count != want || list.Count == 0) Fail($"사전 {Game.UI.CodexCatalog.SectionName(section)} 항목 수가 게임 데이터와 다름({list.Count}/{want})");

                string shotId = section == Game.UI.CodexSection.Weapons ? "mod_ciws" : section == Game.UI.CodexSection.Escorts ? "escort_cap" : "ene_boss2";
                codex.Select(Mathf.Max(0, list.FindIndex(e => e.Id == shotId)));
                yield return new WaitForSecondsRealtime(0.4f);
                codex.MeasurePreview();
                yield return ScreenShot($"codex_{section.ToString().ToLowerInvariant()}");
            }
            // 무장 강화 외형 미리보기(기본 / 강화 I / 강화 II~)
            codex.ShowSection(Game.UI.CodexSection.Weapons);
            foreach (var id in new[] { "mod_ciws", "mod_gun76", "mod_autocannon" })
            {
                int idx = codex.Entries(Game.UI.CodexSection.Weapons).FindIndex(e => e.Id == id);
                if (idx < 0) { Fail($"사전 무장에 {id}가 없음"); continue; }
                codex.Select(idx);
                for (int k = 0; k < codex.Selected.Models.Count; k++)
                {
                    codex.SelectModel(k);
                    yield return new WaitForSecondsRealtime(0.25f);
                    codex.MeasurePreview();
                    yield return ScreenShot($"codex_{id}_{k}");
                }
            }
            _report.AppendLine($"- 미리보기 화면 점유 최소 {minCover * 100f:0.#}%({minCoverName})");
            _report.AppendLine($"- 모델 없는 미리보기: {(noModel.Count == 0 ? "없음" : string.Join(", ", noModel))} · 본문 빈 항목: {(emptyBody.Count == 0 ? "없음" : string.Join(", ", emptyBody))}");
            if (noModel.Count > 0) Fail("사전 미리보기에 모델이 안 나오는 항목이 있음");
            if (emptyBody.Count > 0) Fail("사전 본문이 빈 항목이 있음");

            codex.Close();
            if (menu != null) menu.SetActive(false);
            GameManager.Instance.SetState(GameState.Playing);
            yield return null;
        }

        /// <summary>
        /// Codex 섬·해안 소품: 프리팹 목록, 섬마다 등록·판정(사선 가림·한가운데 막힘(환초 석호 포함)·반대편 트임),
        /// 섬을 치워도 모델 메시가 남는가, 레이더 회전·등대 불빛, 좌초, 적 회피, 자동 생성 비율과 해안 소품. 캡처 포함.
        /// </summary>
        private IEnumerator EnvArtCheck()
        {
            _report.AppendLine("\n## Codex 섬·해안 소품");
            var field = Game.World.IslandField.Instance;
            if (field == null) { Fail("IslandField 없음"); yield break; }
            var ship = GameManager.Instance.Player;
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            Game.World.IslandField.Disabled = true;
            field.ClearAll();
            yield return new WaitForSeconds(2f);

            var arts = new List<Game.World.EnvironmentArt>(Game.World.IslandBuilder.ArtIslands);
            _report.AppendLine($"- 섬 프리팹 {arts.Count}종: " + string.Join(", ", arts.ConvertAll(a => $"{a.name.Replace("ENV_", "")}(반경 {a.shoreRadius:0.#}·높이 {a.height:0.#}·판정 {a.GetComponentsInChildren<Collider>(true).Length})")));
            if (arts.Count < 8) Fail("Codex 섬 프리팹이 8종보다 적음");
            string[] props = { "ENV_FishingBoat", "ENV_FloatingPontoon", "ENV_Breakwater", "ENV_SeaRockArch", "ENV_BeachFishingGear", "ENV_NavigationBuoy" };
            var missingProps = new List<string>();
            foreach (var p in props) if (Resources.Load<GameObject>($"Environment/Props/{p}") == null) missingProps.Add(p);
            _report.AppendLine($"- 해안 소품 프리팹: {props.Length - missingProps.Count}/{props.Length}" + (missingProps.Count > 0 ? $" (없음: {string.Join(", ", missingProps)})" : ""));
            if (missingProps.Count > 0) Fail("해안 소품 프리팹이 없음");

            // 1) 섬마다: 함선 옆에 놓고 판정을 잰다
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            var shotNames = new HashSet<string> { "ENV_RadarOutpost", "ENV_Atoll", "ENV_LighthouseIsland", "ENV_HarborIsland", "ENV_RockIslet", "ENV_WreckCove" };
            foreach (var art in arts)
            {
                field.ClearAll();
                float dist = art.shoreRadius + 22f;
                var info = field.PlaceArt(ship.transform.position + right * dist, art.name, 3);
                yield return null;
                if (info == null) { Fail($"{art.name}: 놓지 못함"); continue; }
                Vector3 c = info.Center;
                bool registered = false;
                foreach (var x in Game.World.Islands.All) if (ReferenceEquals(x, info)) registered = true;
                bool layerOk = true;
                int colliders = 0;
                foreach (var col in info.Root.GetComponentsInChildren<Collider>())
                {
                    colliders++;
                    if (col.gameObject.layer != Game.World.Islands.Layer) layerOk = false;
                }
                Vector3 beyond = c + right * (info.Radius + 14f);
                bool blocks = Game.World.Islands.BlocksShipLine(beyond, ship.transform.position);
                bool openSide = Game.World.Islands.BlocksShipLine(ship.transform.position - right * 40f, ship.transform.position);
                bool centerClear = Game.World.Islands.IsClear(c, 1.5f);   // 환초 석호도 막혀 있어야 한다(둘러싸인 못 — 적 스폰 방지)
                // 해안선 안쪽(반경 60%)은 막히고, 해안선 바깥(반경 + 4m)은 비어야 한다 — 8방향 표본
                int innerBlocked = 0, outerClear = 0;
                for (int k = 0; k < 8; k++)
                {
                    float a = k * Mathf.PI / 4f;
                    var dir = info.Root.transform.TransformDirection(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                    float rim = art.RimAt(a);
                    if (!Game.World.Islands.IsClear(c + dir * rim * 0.6f, 0.5f)) innerBlocked++;
                    if (Game.World.Islands.IsClear(c + dir * (rim + 4f), 0.5f)) outerClear++;
                }
                var lights = info.Root.GetComponent<Game.World.IslandLights>();
                int lamps = lights != null ? lights.LampCount : 0;
                _report.AppendLine($"  - {art.name}: 등록 {(registered ? "O" : "X")} · 판정 {colliders}개(레이어 {(layerOk ? "O" : "X")}) · 해안선 표본 {info.Rim.Length} · " +
                                   $"섬 너머 사선 가림 {(blocks ? "예" : "아니오")} · 반대편 {(openSide ? "막힘" : "트임")} · 한가운데 {(centerClear ? "비어 있음" : "막힘")} · " +
                                   $"해안 안쪽 막힘 {innerBlocked}/8 · 해안 밖 4m 트임 {outerClear}/8 · 등불 {lamps}");
                if (!registered) Fail($"{art.name}: 섬 목록에 등록되지 않음");
                if (colliders == 0 || !layerOk) Fail($"{art.name}: 섬 판정이 없거나 Terrain 레이어가 아님");
                if (!blocks) Fail($"{art.name}: 섬이 사선을 가리지 않음");
                if (openSide) Fail($"{art.name}: 섬 없는 쪽인데 가림");
                if (centerClear) Fail($"{art.name}: 섬 한가운데가 비어 있음");
                if (innerBlocked < 6) Fail($"{art.name}: 해안 안쪽 판정이 비어 있음({innerBlocked}/8)");
                if (outerClear < 5) Fail($"{art.name}: 판정이 해안선 밖으로 너무 나옴({outerClear}/8)");
                if (art.lighthouse && art.lamps != null && art.lamps.Length > 0 && lamps == 0) Fail($"{art.name}: 등대 불빛이 없음");

                // 레이더 회전
                if (art.spinners != null && art.spinners.Length > 0)
                {
                    var spinner = info.Root.GetComponentInChildren<Game.World.EnvironmentArt>().spinners[0];
                    var q0 = spinner.rotation;
                    yield return new WaitForSeconds(1f);
                    float turned = Quaternion.Angle(q0, spinner.rotation);
                    _report.AppendLine($"    - 회전체 {art.spinners.Length}개: 1초에 {turned:0}° 회전");
                    if (turned < 10f) Fail($"{art.name}: 레이더가 돌지 않음");
                }
                if (shotNames.Remove(art.name))
                    yield return CloseShot($"env_{art.name.Replace("ENV_", "").ToLowerInvariant()}", info.Root.transform, info.Radius * 2.4f + 12f);
            }

            // 2) 섬을 치워도 모델(자산) 메시는 남는다
            field.ClearAll();
            yield return null;
            int lost = 0;
            foreach (var art in arts)
                foreach (var mf in art.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh == null) lost++;
            _report.AppendLine($"- 섬을 치운 뒤 프리팹 모델 메시 손실 {lost}개");
            if (lost > 0) Fail("섬을 치울 때 모델 메시(자산)가 지워짐");

            // 3) 좌초: 항구섬을 향해 전속
            fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var harbor = field.PlaceArt(ship.transform.position + fwd * 60f, "ENV_PineIsland", 5);
            if (harbor != null)
            {
                float hp0 = ship.HullHp;
                ship.SetEngineOrder(1f);
                bool aground = false;
                float t1 = Time.time;
                while (Time.time - t1 < 10f)
                {
                    if (ship.IsAground) aground = true;
                    yield return null;
                }
                var box = ship.GetComponent<BoxCollider>();
                Vector3 p = ship.transform.position;
                bool stillInside = Game.World.Islands.ResolveOverlap(box, ref p, ship.transform.rotation, out _) && (p - ship.transform.position).magnitude > 0.3f;
                float centerDist = new Vector2(ship.transform.position.x - harbor.Center.x, ship.transform.position.z - harbor.Center.z).magnitude;
                _report.AppendLine($"- 소나무섬(해안 반경 {harbor.Radius:0}m)을 향해 전속 10초: 좌초 {(aground ? "예" : "아니오")} · 섬 중심까지 {centerDist:0.0}m · 박힘 {(stillInside ? "예" : "아니오")}");
                if (!aground) Fail("Codex 섬에 부딪히지 않음");
                if (stillInside) Fail("함선이 Codex 섬 안에 박힘");
                if (centerDist < harbor.Radius * 0.5f) Fail("함선이 Codex 섬 안쪽 깊이 들어감");
                ship.SetEngineOrder(0f);
                yield return new WaitForSeconds(3f);
            }

            // 4) 적 회피: 항구섬(해안 소품 포함) 둘레 고속정 4척 20초
            field.ClearAll();
            fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var harborIsland = field.PlaceArt(ship.transform.position + fwd * 42f, "ENV_HarborIsland", 2, dress: true);
            if (harborIsland != null)
            {
                var swarm = CombatDevTools.SpawnRing("ene_fastboat", 4, 30f, 45f);
                int overlapFrames = 0, frames = 0;
                float t2 = Time.time;
                while (Time.time - t2 < 20f)
                {
                    foreach (var e in swarm)
                    {
                        if (e == null || !e.IsAlive) continue;
                        var eb = e.GetComponent<BoxCollider>();
                        Vector3 q = e.transform.position;
                        frames++;
                        if (Game.World.Islands.ResolveOverlap(eb, ref q, e.transform.rotation, out _) && (q - e.transform.position).magnitude > 0.3f) overlapFrames++;
                    }
                    yield return null;
                }
                _report.AppendLine($"- 항구섬 둘레 고속정 4척 20초: 0.3m 넘게 박힌 표본 {overlapFrames}/{frames}");
                if (overlapFrames > frames / 50) Fail("적이 Codex 섬을 뚫고 지나감");
                CombatDevTools.ClearBattlefield();
            }

            // 5) 자동 생성: Codex 섬 비율과 해안 소품(시드 4개)
            int islands = 0, artCount = 0, coast = 0;
            var kinds = new Dictionary<string, int>();
            var coastKinds = new Dictionary<string, int>();
            foreach (int seed in new[] { 11, 22, 33, 44 })
            {
                field.ClearAll();
                Game.World.IslandField.Disabled = false;
                field.Regenerate(seed, keepClearAroundPlayer: false);
                Game.World.IslandField.Disabled = true;
                foreach (var i in Game.World.Islands.All)
                {
                    islands++;
                    coast += i.CoastProps;
                    if (i.Art != null)
                    {
                        artCount++;
                        string k = i.Art.StartsWith("ENV_RadarOutpost") ? "RadarOutpost" : i.Art.Replace("ENV_", "");
                        kinds[k] = kinds.TryGetValue(k, out int v) ? v + 1 : 1;
                    }
                    if (i.Root == null) continue;
                    foreach (Transform t in i.Root.transform)
                        if (t.name.StartsWith("Coast "))
                        {
                            string k = t.name.Substring(10);
                            coastKinds[k] = coastKinds.TryGetValue(k, out int v) ? v + 1 : 1;
                        }
                }
            }
            string Join(Dictionary<string, int> d) { var l = new List<string>(); foreach (var kv in d) l.Add($"{kv.Key} {kv.Value}"); l.Sort(); return string.Join(", ", l); }
            _report.AppendLine($"- 자동 생성 섬 {islands}개(시드 4개): Codex 섬 {artCount}개 [{Join(kinds)}] · 해안 소품 {coast}개 [{Join(coastKinds)}]");
            if (artCount == 0) Fail("자동 생성에 Codex 섬이 나오지 않음");
            if (artCount == islands) Fail("자동 생성이 Codex 섬뿐(절차형 섬이 사라짐)");
            if (coast == 0) Fail("해안 소품이 하나도 없음");

            // 전경 캡처(마지막 시드, 함선 주변 섬 가까이)
            var near = new List<Game.World.IslandInfo>(Game.World.Islands.All);
            near.Sort((x, y) => (x.Center - ship.transform.position).sqrMagnitude.CompareTo((y.Center - ship.transform.position).sqrMagnitude));
            yield return Shot("env_overview");
            var dressed = near.Find(x => x.CoastProps > 0);
            if (dressed != null) yield return CloseShot("env_coast_props", dressed.Root.transform, dressed.Radius * 2.4f + 12f);

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            field.ClearAll();
            Game.World.IslandField.Disabled = false;
            field.Regenerate(0);
        }

        /// <summary>
        /// 섬: 자동 배치(출항 지점 비움, 간격), 포탄·어뢰 차단, 사선이 막히면 적이 쏘지 않음, 레이더 그림자,
        /// 함선 좌초(박히지 않고 멈춤), 적의 섬 회피. 캡처 포함.
        /// </summary>
        private IEnumerator IslandCheck()
        {
            _report.AppendLine("\n## 섬(엄폐)");
            var field = Game.World.IslandField.Instance;
            if (field == null) { Fail("IslandField 없음"); yield break; }
            var ship = GameManager.Instance.Player;
            var targeting = ship.GetComponentInChildren<TargetingSystem>();
            CombatDevTools.ClearBattlefield();

            // 1) 자동 배치
            field.Regenerate(12345);
            yield return null;
            var all = Game.World.Islands.All;
            float nearest = float.MaxValue, minGap = float.MaxValue;
            int rocky = 0;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                if (a.Rocky) rocky++;
                float d = new Vector2(a.Center.x - ship.transform.position.x, a.Center.z - ship.transform.position.z).magnitude - a.Radius;
                nearest = Mathf.Min(nearest, d);
                for (int j = i + 1; j < all.Count; j++)
                {
                    var b = all[j];
                    minGap = Mathf.Min(minGap, new Vector2(a.Center.x - b.Center.x, a.Center.z - b.Center.z).magnitude - a.Radius - b.Radius);
                }
            }
            _report.AppendLine($"- 자동 배치(시드 12345, 5×5 구역 = 550m): 섬 {all.Count}개(바위섬 {rocky}·무인도 {all.Count - rocky}) · 함선에서 가장 가까운 해안 {nearest:0}m · 섬 사이 최소 수로 {minGap:0}m");
            if (all.Count < 5) Fail("섬이 너무 적음");
            if (nearest < 30f) Fail("출항 지점 가까이에 섬이 있음");
            yield return Shot("islands_overview");
            if (all.Count > 0)
            {
                // 가장 가까운 섬 둘을 가까이서
                var sorted = new List<Game.World.IslandInfo>(all);
                sorted.Sort((x, y) => (x.Center - ship.transform.position).sqrMagnitude.CompareTo((y.Center - ship.transform.position).sqrMagnitude));
                var rockIsland = sorted.Find(x => x.Rocky);
                var greenIsland = sorted.Find(x => !x.Rocky);
                if (rockIsland != null) yield return CloseShot("island_rock", rockIsland.Root.transform, rockIsland.Radius * 2.6f + 12f);
                if (greenIsland != null) yield return CloseShot("island_green", greenIsland.Root.transform, greenIsland.Radius * 2.6f + 12f);
            }

            // 이후 시험은 섬 하나만 놓고 한다(자동 배치 멈춤)
            Game.World.IslandField.Disabled = true;
            field.ClearAll();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(2f);

            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 islandPos = ship.transform.position + right * 24f;
            field.PlaceAt(islandPos, 7f, true, 7);
            yield return null;
            Vector3 behind = ship.transform.position + right * 48f;
            bool blocked = Game.World.Islands.BlocksShipLine(behind, ship.transform.position);
            bool openSide = Game.World.Islands.BlocksShipLine(ship.transform.position - right * 40f, ship.transform.position);
            _report.AppendLine($"- 사선: 섬 너머(48m) 가림 {(blocked ? "예" : "아니오")} · 반대쪽(40m) 가림 {(openSide ? "예" : "아니오")}");
            if (!blocked) Fail("섬이 사선을 가리지 않음");
            if (openSide) Fail("섬이 없는 쪽인데 가림");

            // 2) 포탄·어뢰 차단: 섬 너머에서 함선을 향해 쏜다
            var boats = CombatDevTools.SpawnRing("ene_fastboat", 1, 60f, 0f);
            var shellPrefab = boats.Count > 0 ? PrivateField<GameObject>(boats[0], "projectilePrefab") : null;
            var subDef = CombatDevTools.FindEnemy("ene_submarine");
            var torpedoPrefab = subDef != null && subDef.Prefab != null ? PrivateField<GameObject>(subDef.Prefab.GetComponent<Submarine>(), "torpedoPrefab") : null;
            CombatDevTools.ClearBattlefield();
            float hp0 = ship.HullHp;
            Vector3 aim = ship.transform.position + Vector3.up * 0.8f;
            if (shellPrefab != null)
                for (int i = 0; i < 6; i++)
                {
                    Vector3 from = behind + fwd * (i - 2.5f) * 1.5f + Vector3.up * 1.2f;
                    var go = PoolManager.Instance.Spawn(shellPrefab, from, Quaternion.LookRotation(aim - from));
                    go.GetComponent<Projectile>()?.Launch((aim - from).normalized, 42f, 5f, DamageSource.Gun);
                }
            if (torpedoPrefab != null)
            {
                var go = PoolManager.Instance.Spawn(torpedoPrefab, behind, Quaternion.identity);
                go.GetComponent<Torpedo>()?.Launch(ship.transform.position, 28f);
            }
            yield return new WaitForSeconds(4f);
            float shellLoss = hp0 - ship.HullHp;
            _report.AppendLine($"- 섬 너머에서 포탄 6발·어뢰 1발 → 선체 피해 {shellLoss:0.#} (포탄 {(shellPrefab != null ? "O" : "없음")}, 어뢰 {(torpedoPrefab != null ? "O" : "없음")})");
            if (shellLoss > 0.01f) Fail("섬이 포탄·어뢰를 막지 못함");

            // 같은 사격을 섬 없는 쪽에서 하면 맞는다(대조)
            hp0 = ship.HullHp;
            Vector3 open = ship.transform.position - right * 40f;
            if (shellPrefab != null)
                for (int i = 0; i < 6; i++)
                {
                    Vector3 from = open + fwd * (i - 2.5f) * 1.5f + Vector3.up * 1.2f;
                    var go = PoolManager.Instance.Spawn(shellPrefab, from, Quaternion.LookRotation(aim - from));
                    go.GetComponent<Projectile>()?.Launch((aim - from).normalized, 42f, 5f, DamageSource.Gun);
                }
            yield return new WaitForSeconds(2f);
            _report.AppendLine($"- 대조(섬 없는 쪽에서 6발) → 선체 피해 {hp0 - ship.HullHp:0.#}");
            if (hp0 - ship.HullHp <= 0.01f) Fail("대조 사격이 맞지 않음(시험 오류)");

            // 3) 사선이 막힌 동안 적이 쏘는가 + 레이더 그림자
            var pccs = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 1f, 0f);
            var pcc = pccs.Count > 0 ? pccs[0] as PccCorvette : null;
            int blockedShots = 0, openShots = 0, blockedFrames = 0, detectedWhileBlocked = 0, frames = 0;
            if (pcc != null)
            {
                pcc.transform.position = behind;
                bool wasFiring = false;
                float t0 = Time.time;
                while (Time.time - t0 < 25f && pcc.IsAlive)
                {
                    bool b = Game.World.Islands.BlocksShipLine(pcc.transform.position, ship.transform.position);
                    frames++;
                    if (b)
                    {
                        blockedFrames++;
                        foreach (var t in targeting.DetectedSurface) if (ReferenceEquals(t, pcc)) { detectedWhileBlocked++; break; }
                    }
                    // 일제사격을 시작한 순간 사선이 막혀 있었는가
                    bool firing = pcc.IsFiring;
                    if (firing && !wasFiring) { if (b) blockedShots++; else openShots++; }
                    wasFiring = firing;
                    yield return null;
                }
            }
            _report.AppendLine($"- 초계함을 섬 너머에 두고 25초: 가린 시간 {(frames > 0 ? blockedFrames * 100 / frames : 0)}% · 가린 채 일제사격 시작 {blockedShots}회 · 트인 채 {openShots}회 · 가린 동안 함선 레이더 탐지 {detectedWhileBlocked}프레임");
            if (blockedFrames == 0) _report.AppendLine("  - 참고: 초계함이 곧바로 섬을 돌아 나와 가린 구간이 없었음");
            if (blockedShots > 0) Fail($"섬에 가린 채 일제사격을 시작함({blockedShots}회)");
            if (detectedWhileBlocked > blockedFrames / 3 + 12) Fail("섬 뒤 적이 레이더에 잡힘(레이더 그림자 없음)");
            CombatDevTools.ClearBattlefield();

            // 4) 좌초: 섬을 향해 전속
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);
            field.ClearAll();
            fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var islet = field.PlaceAt(ship.transform.position + fwd * 55f, 9f, false, 11);
            hp0 = ship.HullHp;
            ship.SetEngineOrder(1f);
            bool aground = false;
            float minCenter = float.MaxValue;
            float t1 = Time.time;
            while (Time.time - t1 < 9f)
            {
                if (ship.IsAground) aground = true;
                minCenter = Mathf.Min(minCenter, new Vector2(ship.transform.position.x - islet.Center.x, ship.transform.position.z - islet.Center.z).magnitude);
                yield return null;
            }
            var box = ship.GetComponent<BoxCollider>();
            Vector3 p = ship.transform.position;
            bool stillInside = Game.World.Islands.ResolveOverlap(box, ref p, ship.transform.rotation, out _) && (p - ship.transform.position).magnitude > 0.3f;
            _report.AppendLine($"- 섬(반경 {islet.Radius:0}m)을 향해 전속 9초: 좌초 {(aground ? "예" : "아니오")} · 함선 중심~섬 중심 최소 {minCenter:0.0}m · 속력 {ship.CurrentSpeed:0.0} · 선체 피해 {hp0 - ship.HullHp:0.#} · 박힘 {(stillInside ? "예" : "아니오")}");
            if (!aground) Fail("섬에 부딪히지 않음(시험 배치 확인)");
            if (stillInside) Fail("함선이 섬 안에 박힘");
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);

            // 5) 적 회피: 섬 둘레에서 고속정 4척이 20초 동안 섬에 박히지 않는가
            field.ClearAll();
            var center = ship.transform.position + fwd * 30f;
            var isl2 = field.PlaceAt(center, 10f, false, 5);
            var swarm = CombatDevTools.SpawnRing("ene_fastboat", 4, 26f, 45f);
            int overlapFrames = 0; frames = 0;
            float t2 = Time.time;
            while (Time.time - t2 < 20f)
            {
                foreach (var e in swarm)
                {
                    if (e == null || !e.IsAlive) continue;
                    var eb = e.GetComponent<BoxCollider>();
                    Vector3 q = e.transform.position;
                    frames++;
                    if (Game.World.Islands.ResolveOverlap(eb, ref q, e.transform.rotation, out _) && (q - e.transform.position).magnitude > 0.3f) overlapFrames++;
                }
                yield return null;
            }
            _report.AppendLine($"- 섬 둘레 고속정 4척 20초: 섬에 0.3m 넘게 박힌 표본 {overlapFrames}/{frames}");
            if (overlapFrames > frames / 50) Fail("적이 섬을 뚫고 지나감");
            yield return Shot("island_cover");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            field.ClearAll();
            Game.World.IslandField.Disabled = false;
            field.Regenerate(0);
        }

        // ------------------------------------------------------------ 바다·물살

        /// <summary>
        /// 바다 수면이 카메라를 따라가는지, 함선·수상함에 물살이 생기는지, 잠항 잠수함은 물살이 없는지 보고 캡처한다.
        /// </summary>
        private IEnumerator WaterCheck()
        {
            _report.AppendLine("\n## 바다·물살");
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            var off = DisableWeapons(null);

            var surface = Object.FindFirstObjectByType<Game.View.OceanSurface>();
            var sceneOcean = GameObject.Find("Ocean");
            var mat = surface != null ? surface.GetComponent<MeshRenderer>().sharedMaterial : null;
            _report.AppendLine($"- 수면: {(surface != null ? "있음" : "없음")} · 셰이더 {(mat != null ? mat.shader.name : "-")} · 씬 평면 숨김 {(sceneOcean != null && !sceneOcean.GetComponent<MeshRenderer>().enabled ? "예" : "아니오")}");
            if (surface == null) Fail("OceanSurface가 없음");
            if (mat == null || mat.shader.name != "Naval/Ocean" || !mat.shader.isSupported) Fail("바다 셰이더가 Naval/Ocean이 아니거나 지원되지 않음");

            // 전속으로 좌현 선회, 주변에 수상함·잠항 잠수함
            ship.SetEngineOrder(1f);
            ship.DevRudderOverride = -0.35f;
            var boats = CombatDevTools.SpawnRing("ene_fastboat", 2, 26f, 40f);
            var pccs = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 30f, 200f);
            var subs = CombatDevTools.SpawnRing("ene_cruise_submarine", 1, 36f, 300f);
            foreach (var e in boats) e.DevFrozen = false;
            yield return new WaitForSeconds(6f);

            var playerWake = ship.GetComponent<Game.View.ShipWake>();
            var boatWake = boats.Count > 0 ? boats[0].GetComponent<Game.View.ShipWake>() : null;
            var pccWake = pccs.Count > 0 ? pccs[0].GetComponent<Game.View.ShipWake>() : null;
            var subWake = subs.Count > 0 ? subs[0].GetComponent<Game.View.ShipWake>() : null;
            string W(Game.View.ShipWake w) => w == null ? "없음" : $"점 {w.KelvinPoints}/{w.WashPoints} · 속력 {w.MeasuredSpeed:0.0} · 보임 {(w.Visible ? "예" : "아니오")}";
            _report.AppendLine($"- 물살 — 플레이어: {W(playerWake)} / 고속정: {W(boatWake)} / 초계함: {W(pccWake)} / 잠항 잠수함: {W(subWake)}");
            if (playerWake == null || !playerWake.Visible) Fail("플레이어 함선 물살이 보이지 않음");
            if (boatWake == null || !boatWake.Visible) Fail("고속정 물살이 보이지 않음");
            if (subWake != null && subWake.Visible && subs[0] is SubmarineBase sb && !sb.IsRevealed) Fail("잠항 중인 잠수함에 물살이 보임");

            // 수면이 카메라를 따라왔는지
            if (surface != null && Camera.main != null)
            {
                var cam = Camera.main.transform;
                float dist = Vector2.Distance(new Vector2(surface.transform.position.x, surface.transform.position.z), new Vector2(ship.transform.position.x, ship.transform.position.z));
                _report.AppendLine($"- 수면 중심 ↔ 함선 수평 거리 {dist:0}m (수면 1200m)");
                if (dist > 200f) Fail("수면이 카메라를 따라가지 않음");
            }

            yield return Shot("water_wake");
            yield return CloseShot("water_wake_close", ship.transform, 30f);

            // 정지하면 새 물살이 생기지 않고 흐려진다
            ship.SetEngineOrder(0f);
            ship.DevRudderOverride = 0f;
            yield return new WaitForSeconds(10f);
            _report.AppendLine($"- 정지 10초 뒤 플레이어 물살: {W(playerWake)}");
            if (playerWake != null && playerWake.Visible) Fail("정지 뒤에도 물살이 남음");

            ship.DevRudderOverride = null;
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
        }
    }
}
#endif
