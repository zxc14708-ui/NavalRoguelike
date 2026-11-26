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
        /// <summary>-carrierBossOnly: 3스테이지 항공전함 개편(2026-11-14) — 새 모델 소켓 · 현측 기관포 · 거리별 주포(탄 비행 시간·포신 앙각) ·
        /// 대공 요격 확률(6기 50% → 그 현 2기 파괴 후 낮아짐) · 겨냥점과 가려진 약점 · 엘리베이터 드론 2×3 · 함미 보트 3 · 보스 막대 묶음.</summary>
        public bool CarrierBossOnly;

        private IEnumerator CarrierBossCheck()
        {
            _report.AppendLine("\n## 3스테이지 항공전함(새 모델 · 패턴 개편)");
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            CombatDevTools.ClearBattlefield();
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            // 내 무장은 끈다(대공포·부위를 마음대로 부수면 확률 검사가 흐려진다). VLS 하나는 미사일 탄만 얻으려고 설치
            var vls = CombatDevTools.InstallModuleNear("mod_vls", new Game.Ship.GridCoord(0, 0), 8);
            GameObject missilePrefab = vls != null && vls.Runtime != null ? Get<GameObject>(vls.Runtime, "missilePrefab") : null;
            var off = new List<Behaviour>();
            foreach (var r in ship.GetComponentsInChildren<ModuleRuntime>()) if (r.enabled && r is not BridgeModule) { r.enabled = false; off.Add(r); }
            yield return new WaitForSeconds(1f);

            var def = CombatDevTools.FindEnemy("ene_boss2");
            Vector3 PortBeam(Transform b, float d) => b.position - b.right * d;
            // 보스를 기함 앞 25m, 기함을 좌현 쪽에 두도록 돌려 세운다(보스 앞 = 기함의 왼쪽)
            Vector3 at = ship.transform.position + ship.transform.forward * 25f;
            var boss = def != null ? EnemySpawner.Instance.SpawnAt(def, at, Quaternion.LookRotation(-ship.transform.right, Vector3.up)) as HybridBattleshipBoss : null;
            if (boss == null) { Fail("항공전함 생성 실패"); foreach (var b in off) if (b != null) b.enabled = true; yield break; }
            boss.DevStationary = true;
            boss.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-ship.transform.right, Vector3.up));
            yield return new WaitForSeconds(0.5f);

            // 1) 모델 소켓 · 부위
            var parts = boss.Parts;
            var sb = new StringBuilder();
            foreach (var p in parts.Parts) sb.Append($"{p.Name}{(p.Shielded ? "(가림)" : "")} · ");
            bool socketsOk = parts.Parts.Count == 13 && boss.AaCount == 6 && boss.ElevatorAnchor != null && boss.WellAnchor != null;
            int portAa = 0; for (int i = 0; i < boss.AaCount; i++) if (boss.AaIsPort(i)) portAa++;
            _report.AppendLine($"- 소켓·부위 {parts.Parts.Count}개: {sb}대공포 {boss.AaCount}기(좌현 {portAa}) · 보스 막대 칸 {parts.ChipCount}");
            if (!socketsOk || portAa != 3) Fail("새 모델 소켓(주포·기관포·대공포 6·엘리베이터·함미 램프·레이더)을 다 찾지 못함");
            var mats = new StringBuilder();
            foreach (var r in boss.GetComponentsInChildren<MeshRenderer>())
                if (r.name is "FlightDeck_Mesh" or "Hull_Mesh" or "BridgeAssembly_Mesh" or "SternWellDeck_Mesh" or "FlightDeck" or "Hull")
                {
                    mats.Append($"{r.name}[");
                    foreach (var m in r.sharedMaterials) if (m != null) mats.Append($"{m.name}({(m.HasProperty("_BaseColor") ? ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")) : "-")}) ");
                    mats.Append("] ");
                }
            _report.AppendLine($"  - 재질: {mats}");
            yield return CloseShot("carrier_model", boss.transform, 34f);

            // 2) 현측 기관포: 기함이 좌현 25m — 몇 초 안에 점사
            int side0 = boss.DevSideShots;
            yield return new WaitForSeconds(5f);
            _report.AppendLine($"- 현측 기관포: 5초에 {boss.DevSideShots - side0}발");
            if (boss.DevSideShots - side0 < 6) Fail("현측 기관포가 쏘지 않음");

            // 3) 주포: 가까이(약 20m) · 멀리(약 40m)에서 탄 비행 시간과 포신 앙각
            float flightNear = 0f, flightFar = 0f, elevNear = 0f, elevFar = 0f;
            foreach (var dist in new[] { 20f, 40f })
            {
                boss.transform.position = PortBeam(boss.transform, 0f) + (ship.transform.position - boss.transform.position).normalized * ((ship.transform.position - boss.transform.position).magnitude - dist);
                int s0 = boss.DevSalvos;
                float t0 = Time.time;
                while (boss.DevSalvos == s0 && Time.time - t0 < 9f) yield return null;
                yield return new WaitForSeconds(0.05f);
                float el = Mathf.Max(boss.MainGunElevation(0), boss.MainGunElevation(1));
                if (dist < 30f) { flightNear = boss.DevLastFlightTime; elevNear = el; } else { flightFar = boss.DevLastFlightTime; elevFar = el; }
                yield return CloseShot(dist < 30f ? "carrier_maingun_near" : "carrier_maingun_far", boss.BowAnchor, 14f);
            }
            _report.AppendLine($"- 주포: 약 20m 탄 비행 {flightNear:0.00}초 · 포신 {elevNear:0}° / 약 40m {flightFar:0.00}초 · 포신 {elevFar:0}° (예전 2.2초 고정)");
            if (flightNear <= 0f || flightFar <= 0f) Fail("주포가 쏘지 않음");
            else
            {
                if (!(flightNear < flightFar && flightFar <= 1.61f)) Fail("주포 탄 비행 시간이 거리에 따라 짧아지지 않음(최대 1.6초)");
                if (elevFar < elevNear + 5f) Fail("멀리 쏠 때 포신이 더 들리지 않음");
            }

            // 4) 대공 요격(2026-11-14 개편): 50% × 남은 비율 − 격추마다 10%p(4초마다 회복)
            boss.transform.position = ship.transform.position + (boss.transform.position - ship.transform.position).normalized * 30f;
            Missile LaunchAt(Vector3 from)
            {
                var go = missilePrefab != null ? PoolManager.Instance?.Spawn(missilePrefab, from, Quaternion.LookRotation(boss.transform.position - from)) : null;
                var m = go != null ? go.GetComponent<Missile>() : null;
                if (m != null) m.Launch(boss.transform, 1f);
                return m;
            }
            IEnumerator WaitGone(List<Missile> flying)
            {
                float t1 = Time.time;
                while (Time.time - t1 < 5f && flying.Exists(x => x != null && x.IsAlive && x.isActiveAndEnabled)) yield return null;
            }
            // 4-1) 확률표: 지친 정도 0~5 → 50 · 40 · 30 · 20 · 10 · 0%
            var table = new StringBuilder();
            bool tableOk = true;
            for (int f = 0; f <= 5; f++)
            {
                boss.DevSetAaFatigue(f);
                float c = boss.InterceptChance(true);
                table.Append($"{c * 100f:0}% ");
                if (Mathf.Abs(c - (0.5f - 0.1f * f)) > 0.001f) tableOk = false;
            }
            // 4-2) 한 발씩(매번 지친 정도 0) 30발 — 약 50%
            int single = 0, singleKills = 0;
            for (int n = 0; n < 30 && missilePrefab != null; n++)
            {
                boss.DevSetAaFatigue(0f);
                int k0 = boss.DevAaKills;
                var m = LaunchAt(PortBeam(boss.transform, 38f) + boss.transform.forward * ((n % 4 - 1.5f) * 4f) + Vector3.up * 4f);
                yield return WaitGone(new List<Missile> { m });
                single++;
                if (boss.DevAaKills > k0) singleKills++;
            }
            // 4-3) 몰아 쏘기: 지친 정도 0에서 10발을 한꺼번에 — 많아야 5발만 떨어진다(6번째부터는 0%)
            boss.DevSetAaFatigue(0f);
            int burstK0 = boss.DevAaKills, burstJ0 = boss.DevAaJudged;
            var burst = new List<Missile>();
            for (int n = 0; n < 10 && missilePrefab != null; n++)
                burst.Add(LaunchAt(PortBeam(boss.transform, 36f) + boss.transform.forward * ((n % 5 - 2f) * 3f) + Vector3.up * (4f + n / 5)));
            yield return WaitGone(burst);
            int burstKills = boss.DevAaKills - burstK0, burstJudged = boss.DevAaJudged - burstJ0;
            int fatigueAfter = boss.AaFatigue;
            // 4-4) 회복: 지친 정도 3 → 4.3초 뒤 2
            boss.DevSetAaFatigue(3f);
            yield return new WaitForSeconds(4.3f);
            int recovered = boss.AaFatigue;
            // 4-5) 좌현 대공포 2기 파괴 → 좌현 약 17%, 우현 50%
            int broken = 0;
            for (int i = 0; i < boss.AaCount && broken < 2; i++)
            {
                var p = boss.AaPart(i);
                if (p == null || !boss.AaIsPort(i)) continue;
                while (!p.Destroyed) boss.TakeDamage(new DamageInfo(p.MaxHp * 0.5f, p.Anchor.position + Vector3.up * 0.2f, Vector3.down, DamageSource.Gun, "검증"));
                broken++;
            }
            boss.DevSetAaFatigue(0f);
            float portChance = boss.InterceptChance(true), stbdChance = boss.InterceptChance(false);
            boss.DevSetAaFatigue(1f);
            float portAfterOne = boss.InterceptChance(true);
            boss.DevSetAaFatigue(0f);
            yield return new WaitForSeconds(0.4f);
            float singleRate = single > 0 ? singleKills / (float)single : 0f;
            _report.AppendLine($"- 대공 요격 확률표(지친 정도 0~5): {table}");
            _report.AppendLine($"  - 한 발씩 {single}발: 격추 {singleKills}({singleRate:P0}, 기대 50%) · 몰아 쏘기 10발: 판정 {burstJudged} · 격추 {burstKills}(최대 5) · 지친 정도 {fatigueAfter}");
            _report.AppendLine($"  - 회복: 지친 정도 3 → 4.3초 뒤 {recovered} · 좌현 {broken}기 파괴 후 좌현 {portChance:P0} / 우현 {stbdChance:P0} · 좌현 1발 격추 뒤 {portAfterOne:P0}");
            if (missilePrefab == null) Fail("검사용 미사일 탄을 찾지 못함");
            if (!tableOk) Fail("격추마다 요격 확률이 10%p씩 줄지 않음(50 → 0%)");
            if (singleRate < 0.27f || singleRate > 0.73f) Fail("대공포 6기 첫 요격 확률이 50% 근처가 아님");
            if (burstJudged < 8 || burstKills > 5) Fail("몰아 쏘기에서 5발보다 많이 떨어짐(6번째부터는 반드시 들어가야 함)");
            if (recovered != 2) Fail("깎인 요격 확률이 4초마다 한 단계씩 돌아오지 않음");
            if (Mathf.Abs(portChance - 0.5f / 3f) > 0.01f || Mathf.Abs(stbdChance - 0.5f) > 0.01f || Mathf.Abs(portAfterOne - (0.5f / 3f - 0.1f)) > 0.01f) Fail("대공포 파괴에 따른 요격 확률이 규칙과 다름");

            // 5) 겨냥점 · 가려진 약점(엘리베이터 · 함미 웰덱)
            {
                Vector3 beam = PortBeam(boss.transform, 25f) - boss.transform.forward * 1.4f;
                Vector3 ahead = boss.transform.position + boss.transform.forward * 40f;
                Vector3 astern = boss.transform.position - boss.transform.forward * 40f;
                var elevPart = System.Linq.Enumerable.First(parts.Parts, p => p.Name == "엘리베이터");
                var wellPart = System.Linq.Enumerable.First(parts.Parts, p => p.Name == "함미 웰덱");
                // 가려져 있을 때: 현측 엘리베이터 자리 피해 → 엘리베이터는 그대로
                Vector3 sideHit = boss.GetComponent<Collider>().ClosestPoint(beam);
                float e0 = elevPart.Hp;
                boss.TakeDamage(new DamageInfo(20f, sideHit, Vector3.right, DamageSource.Gun, "검증"));
                bool shieldedOk = Mathf.Approximately(e0, elevPart.Hp);
                // 내려가면: 현측 겨냥점 = 엘리베이터, 앞에서는 아님, 같은 자리 피해가 엘리베이터로
                Call(boss, "SetElevator", 1f); Call(boss, "UpdateExposure");
                bool aimBeam = (boss.AimPointFor(beam, false) - boss.ElevatorAnchor.position).sqrMagnitude < 0.01f;
                bool aimAhead = (boss.AimPointFor(ahead, false) - boss.ElevatorAnchor.position).sqrMagnitude > 0.01f;
                bool aimMissile = (boss.AimPointFor(ahead, true) - boss.ElevatorAnchor.position).sqrMagnitude < 0.01f;
                boss.TakeDamage(new DamageInfo(20f, sideHit, Vector3.right, DamageSource.Gun, "검증"));
                bool exposedOk = elevPart.Hp < e0 - 19f;
                yield return CloseShot("carrier_elevator_down", boss.ElevatorAnchor, 12f);
                Call(boss, "SetElevator", 0f); Call(boss, "UpdateExposure");
                // 함미 램프: 열리면 뒤에서 겨냥 = 웰덱, 맞으면 선체 1.5배
                Call(boss, "SetGate", 1f); Call(boss, "UpdateExposure");
                bool aimAstern = (boss.AimPointFor(astern, false) - boss.WellAnchor.position).sqrMagnitude < 0.01f;
                bool aimBeamNotWell = (boss.AimPointFor(beam, false) - boss.WellAnchor.position).sqrMagnitude > 0.01f;
                float hp0 = boss.CurrentHp, w0 = wellPart.Hp;
                boss.TakeDamage(new DamageInfo(10f, boss.WellAnchor.position, Vector3.forward, DamageSource.Gun, "검증"));
                float hullLoss = hp0 - boss.CurrentHp;
                bool wellOk = Mathf.Abs(hullLoss - 15f) < 0.01f && wellPart.Hp < w0 - 14.9f;
                yield return CloseShot("carrier_gate_open", boss.WellAnchor, 13f);
                Call(boss, "SetGate", 0f); Call(boss, "UpdateExposure");
                _report.AppendLine($"- 약점: 가려진 엘리베이터 피해 없음 {Yes(shieldedOk)} · 내려가면 현측 겨냥 {Yes(aimBeam)} · 앞에서는 다른 곳 {Yes(aimAhead)} · 미사일은 앞에서도 {Yes(aimMissile)} · 현측 피해가 엘리베이터로 {Yes(exposedOk)}");
                _report.AppendLine($"  - 함미: 열리면 뒤에서 겨냥 = 웰덱 {Yes(aimAstern)} · 현측에서는 아님 {Yes(aimBeamNotWell)} · 웰덱 10 피해 → 선체 {hullLoss:0.#}(1.5배) · 웰덱 부위도 깎임 {Yes(wellOk)}");
                if (!shieldedOk || !aimBeam || !aimAhead || !aimMissile || !exposedOk) Fail("엘리베이터 약점 · 겨냥점이 규칙대로 동작하지 않음");
                if (!aimAstern || !aimBeamNotWell || !wellOk) Fail("함미 웰덱 약점(뒤에서만 · 1.5배)이 규칙대로 동작하지 않음");
            }

            // 6) 2·3단계: 체력을 35%로(맞은 자리 없는 피해 — 부위는 그대로) → 드론 2×3 · 자폭 보트 3
            boss.TakeDamage(new DamageInfo(boss.CurrentHp - def.MaxHp * 0.35f, Vector3.zero, Vector3.down, DamageSource.Gun, "검증"));
            float t2 = Time.time;
            bool shotDown = false, shotGate = false, shotRide = false, shotDeck = false;
            int ridingSeen = 0, onDeckSeen = 0;
            while (Time.time - t2 < 18f && (boss.DevDronesLaunched < 6 || boss.DevBoatsLaunched < 3))
            {
                if (boss.DronesOnElevator == 2 && boss.Elevator01 > 0.2f && boss.Elevator01 < 0.8f) ridingSeen++;
                if (boss.DronesOnElevator == 2 && boss.Elevator01 < 0.02f) onDeckSeen++;
                if (!shotDown && boss.Elevator01 > 0.95f && boss.DronesOnElevator == 2) { shotDown = true; yield return CloseShot("carrier_wave_elevator", boss.ElevatorAnchor, 12f); }
                if (!shotRide && boss.DronesOnElevator == 2 && boss.Elevator01 > 0.35f && boss.Elevator01 < 0.65f && shotDown) { shotRide = true; yield return CloseShot("carrier_wave_riding", boss.ElevatorAnchor, 10f); }
                if (!shotDeck && boss.DronesOnElevator == 2 && boss.Elevator01 < 0.02f) { shotDeck = true; yield return CloseShot("carrier_wave_on_deck", boss.ElevatorAnchor, 10f); }
                if (!shotGate && boss.Gate01 > 0.95f && boss.DevBoatsLaunched >= 1) { shotGate = true; yield return CloseShot("carrier_wave_boats", boss.WellAnchor, 16f); }
                yield return null;
            }
            var bar = Object.FindFirstObjectByType<Game.UI.BossBarUI>();
            yield return new WaitForSeconds(0.4f);
            _report.AppendLine($"- 총력전: 드론 {boss.DevDronesLaunched}기(기대 6 = 2×3) · 자폭 보트 {boss.DevBoatsLaunched}척(기대 3) · {Time.time - t2:0.0}초 · 단계 \"{boss.PhaseName}\"");
            _report.AppendLine($"  - 드론이 엘리베이터에 실려 올라옴: 오르는 중 {ridingSeen}프레임 · 갑판에 올라온 모습 {onDeckSeen}프레임 · 끝난 뒤 실린 드론 {boss.DronesOnElevator}(기대 0)");
            if (ridingSeen == 0 || onDeckSeen == 0 || boss.DronesOnElevator != 0) Fail("드론이 엘리베이터를 타고 올라오는 모습이 없음");
            _report.AppendLine($"  - 보스 막대: 칸 {(bar != null ? bar.PartChips : -1)}/{parts.ChipCount} · \"{(bar != null ? bar.PartsText : "")}\"");
            if (boss.DevDronesLaunched < 6) Fail("엘리베이터 드론이 2기씩 3번 나오지 않음");
            if (boss.DevBoatsLaunched < 3) Fail("함미 램프에서 자폭 보트 3척이 나오지 않음");
            if (bar != null && bar.Boss == boss && bar.PartChips != parts.ChipCount) Fail("보스 막대 부위 칸이 묶음 수와 다름");
            yield return Shot("carrier_final_phase");

            if (boss.IsAlive) boss.RemoveWithoutReward();
            CombatDevTools.ClearBattlefield();
            foreach (var b in off) if (b != null) b.enabled = true;
            if (vls != null) CombatDevTools.RemoveModule(vls);
        }

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

            // 0) 기만체 채프 구름(2026-11-09): 터진 뒤 수명 끝까지 구름·은박이 남는가 — 쏜 뒤 1.5 · 4 · 6.5초를 찍는다
            DecoyLauncherModule launcher = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is DecoyLauncherModule d && m.IsOperational) { launcher = d; break; }
            if (launcher != null)
            {
                launcher.Deploy(ship.transform.position + ship.transform.forward * 40f);
                float t0 = Time.time;
                var counts = new StringBuilder();
                int late = 0;
                foreach (float at in new[] { 1.5f, 4f, 6.5f })
                {
                    while (Time.time - t0 < at) yield return null;
                    int chaff = Game.View.DecorFx.Count(Game.View.DecorFx.Chaff);
                    if (at > 6f) late = chaff;
                    counts.Append($"{at:0.0}초 은박 {chaff} · ");
                    yield return Shot($"polish_decoy_{Mathf.RoundToInt(at * 10):00}");
                }
                _report.AppendLine($"- 기만체 채프 구름: {counts}");
                if (late < 10) Fail("기만체가 터진 뒤 채프 구름이 남지 않음");
            }
            else _report.AppendLine("- 기만체: 발사기 없음 — 건너뜀");

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
                _report.AppendLine($"  - 보스 막대 부위 줄: 칸 {chips}/{parts.ChipCount} · \"{barText}\"");
                if (chips != parts.ChipCount) Fail($"{def.DisplayName}: 보스 막대에 부위 줄이 없거나 칸 수가 다름");
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
                                   || !Mathf.Approximately(a.SpeedBonus, b.SpeedBonus)
                                   || prof.Module.Type == ModuleType.Howitzer || prof.Module.Type == ModuleType.RamBow
                                   // 지원 블록(2026-11-22): 강화 단계가 수치가 아니라 Lv 규칙(실행 중 행동)을 바꾼다 — 단계가 2개 이상이면 된다
                                   || (ModuleUpgrades.IsSupport(prof.Module.Type) && prof.Module.Type != ModuleType.Magazine && ModuleUpgrades.MaxLevel(prof.Module) >= 2);
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
            if (boat.Count > 0 && boat[0] != null) yield return CloseShot("polish_enemy_hull_foam", boat[0].transform, 10f);   // 배 모양 거품 고리

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
