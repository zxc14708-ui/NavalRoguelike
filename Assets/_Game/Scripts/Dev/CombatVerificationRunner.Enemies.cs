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
    /// <summary>자동 전투 검증 — 적별 동작 검사(잠수함·엘리트 표식·자폭 보트·어뢰 회피·초계함 추적·새 적).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 잠수함 패치: 엘리트 데이터(체력·피해·간격·동시 1척·경험치) · 웨이브 가중치 축소 · 40m보다 가까우면 쏘지 않음 ·
        /// 등장 직후 첫 발 지연 · 여러 척이 있어도 동시 어뢰 2발 이하.
        /// </summary>
        private IEnumerator SubmarineCheck()
        {
            var ship = GameManager.Instance.Player;
            var def = CombatDevTools.FindEnemy("ene_submarine");
            var off = DisableWeapons(null);
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(2f);

            // 데이터와 웨이브 가중치
            float maxWeight = 0f;
            foreach (var rs in Resources.FindObjectsOfTypeAll<Game.Data.RoundSet>())
                foreach (var round in rs.Rounds)
                    if (round.Entries != null) foreach (var en in round.Entries) if (en.Enemy == def) maxWeight = Mathf.Max(maxWeight, en.Weight);
            var prefabSub = def.Prefab.GetComponent<Submarine>();
            float minFire = Get<float>(prefabSub, "minFireRange"), maxFire = Get<float>(prefabSub, "torpedoRange"), solution = Get<float>(prefabSub, "firingSolutionTime");
            _report.AppendLine($"- 어뢰 잠수함: 등급 {def.Rank} · 체력 {def.MaxHp} · 어뢰 피해 {def.AttackDamage} · 간격 {def.AttackCooldown}초 · 동시 최대 {def.MaxAlive}척 · 경험치 {def.XpReward} · 선회 {def.PreferredRange}m · 사격 {minFire}~{maxFire}m · 경고 {solution}초 · 웨이브 가중치 최대 {maxWeight:0.###}(예전 0.35)");
            if (def.Rank != Game.Data.EnemyRank.Elite || def.MaxAlive != 1 || def.MaxHp < 60f || def.AttackDamage < 50f || def.AttackCooldown < 14f)
                Fail("어뢰 잠수함이 엘리트(체력·피해 상향, 간격 증가, 동시 1척)로 바뀌지 않음");
            if (maxWeight <= 0f || maxWeight > 0.2f) Fail("어뢰 잠수함 웨이브 가중치가 줄지 않음");
            if (minFire < 38f || solution < 1.8f) Fail("어뢰 잠수함의 최소 사격 거리·경고 시간이 부족함");

            // A) 코앞(30m)에 나타난 잠수함: 40m 안에서는 쏘지 않는다. 등장 직후에도 바로 쏘지 않는다.
            CombatDevTools.ClearBattlefield();
            foreach (var tp in new List<Torpedo>(Torpedo.Active)) if (tp != null) PoolManager.Instance?.Despawn(tp.gameObject);
            var near = CombatDevTools.SpawnRing("ene_submarine", 1, 30f, 100f);
            var sub = near.Count > 0 ? near[0] as Submarine : null;
            if (sub == null) { Fail("잠수함 스폰 실패"); yield break; }
            float openingTimer = Get<float>(sub, "AttackTimer");
            Put(sub, "AttackTimer", 0f);   // 첫 발 지연은 따로 보고, 여기서는 거리 규칙만 본다
            float t0 = Time.time, prepDist = -1f, minDist = 999f;
            while (Time.time - t0 < 30f)
            {
                float d = Vector3.Distance(sub.transform.position, ship.transform.position);
                minDist = Mathf.Min(minDist, d);
                if (prepDist < 0f && sub.IsPreparingTorpedo) { prepDist = d; break; }
                yield return null;
            }
            _report.AppendLine($"- 코앞(30m)에 나온 잠수함: 등장 직후 공격 대기 {openingTimer:0.#}초(간격 {def.AttackCooldown}초의 60%) · 최단 {minDist:0}m · 사격 준비를 시작한 거리 {(prepDist < 0f ? "쏘지 않음" : prepDist.ToString("0") + "m")} (최소 사격 {minFire}m)");
            if (openingTimer < def.AttackCooldown * 0.5f) Fail("등장 직후 첫 발 지연이 적용되지 않음");
            if (prepDist >= 0f && prepDist < minFire - 2f) Fail($"너무 가까운 곳({prepDist:0}m)에서 어뢰를 쏨");
            CombatDevTools.ClearBattlefield();

            // B) 잠수함 4척이 동시에 쏘려 해도 전체 어뢰(준비 중 + 비행 중)는 2발 이하
            foreach (var tp in new List<Torpedo>(Torpedo.Active)) if (tp != null) PoolManager.Instance?.Despawn(tp.gameObject);
            var pack = CombatDevTools.SpawnRing("ene_submarine", 4, 56f, 20f);
            foreach (var e in pack) Put(e, "AttackTimer", 0f);
            int peak = 0, seen = 0;
            t0 = Time.time;
            while (Time.time - t0 < 16f)
            {
                peak = Mathf.Max(peak, Submarine.TorpedoesInPlay);
                seen = Mathf.Max(seen, Torpedo.Active.Count);
                yield return null;
            }
            _report.AppendLine($"- 잠수함 4척이 56m에서 16초: 동시에 준비·비행한 어뢰 최대 {peak}발(제한 2) · 실제 발사 관측 {(seen > 0 ? "있음" : "없음")}");
            if (peak > 2) Fail($"동시 어뢰가 제한(2발)을 넘음: {peak}발");
            if (peak < 1) Fail("잠수함 4척이 어뢰를 한 발도 쏘지 않음(시험 무효)");
            CombatDevTools.ClearBattlefield();
            foreach (var tp in new List<Torpedo>(Torpedo.Active)) if (tp != null) PoolManager.Instance?.Despawn(tp.gameObject);

            foreach (var d in off) if (d != null) d.enabled = true;
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        /// <summary>엘리트 표식: 엘리트 적에게만 · 드러나지 않은 잠수함은 숨김 · 레이더 에코도 황금색.</summary>
        private IEnumerator EliteMarkerCheck()
        {
            var ship = GameManager.Instance.Player;
            var off = DisableWeapons(null);
            CombatDevTools.ClearBattlefield();
            yield return null;
            var normal = CombatDevTools.SpawnRing("ene_fastboat", 1, 30f, 20f);
            var mboat = CombatDevTools.SpawnRing("ene_missileboat", 1, 30f, 140f);
            var pcc = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 34f, 250f);
            var sub = CombatDevTools.SpawnRing("ene_submarine", 1, 32f, 320f);
            foreach (var e in normal) e.DevFrozen = true;
            foreach (var e in mboat) e.DevFrozen = true;
            foreach (var e in pcc) e.DevFrozen = true;
            foreach (var e in sub) e.DevFrozen = true;
            yield return new WaitForSeconds(0.5f);

            var mNormal = normal.Count > 0 ? normal[0].GetComponent<Game.View.EliteMarker>() : null;
            var mMissile = mboat.Count > 0 ? mboat[0].GetComponent<Game.View.EliteMarker>() : null;
            var mPcc = pcc.Count > 0 ? pcc[0].GetComponent<Game.View.EliteMarker>() : null;
            var mSub = sub.Count > 0 ? sub[0].GetComponent<Game.View.EliteMarker>() : null;
            bool subHidden = mSub != null && !mSub.Visible;
            yield return ScreenShot("elite_marker");
            if (sub.Count > 0) { Call(sub[0], "RevealFor", 3f); }
            for (int f = 0; f < 4; f++) yield return null;   // 표식은 LateUpdate에서 갱신된다 — 느린 프레임에서도 몇 번 돌게
            bool subShown = mSub != null && mSub.Visible;
            if (mboat.Count > 0) yield return CloseShot("elite_missileboat", mboat[0].transform, 16f);

            // 레이더: 한 바퀴(약 3초) 돌 때까지 기다려 에코를 읽는다
            var radar = Object.FindFirstObjectByType<Game.UI.RadarScopeUI>(FindObjectsInactive.Include);
            int elite = 0, plain = 0;
            for (float t = 0f; t < 8f && (elite < 2 || plain < 1); t += 0.25f)
            {
                yield return new WaitForSeconds(0.25f);
                elite = 0; plain = 0;
                if (radar == null) break;
                if (Get<System.Collections.IEnumerable>(radar, "_echoes") is not { } echoes) break;
                foreach (var e in echoes)
                {
                    if (!Get<bool>(e, "Active")) continue;
                    var type = Get<object>(e, "Type")?.ToString();
                    if (type is "Clutter" or "Land") continue;
                    if (Get<bool>(e, "Elite")) elite++; else plain++;
                }
            }
            _report.AppendLine($"- 엘리트 표식: 일반 고속정 {(mNormal == null ? "없음" : "있음")} · 미사일정 {(mMissile != null && mMissile.Visible ? "보임" : "없음")} · 초계함 {(mPcc != null && mPcc.Visible ? "보임" : "없음")} · " +
                               $"잠항 중 잠수함 {(subHidden ? "숨김" : "보임")} → 부상 후 {(subShown ? "보임" : "숨김")} · 레이더 에코 엘리트 {elite} / 일반 {plain}");
            if (mNormal != null) Fail("일반 적에게 엘리트 표식이 붙음");
            if (mMissile == null || !mMissile.Visible) Fail("엘리트 미사일정에 표식이 보이지 않음");
            if (mPcc == null || !mPcc.Visible) Fail("엘리트 초계함에 표식이 보이지 않음");
            if (!subHidden) Fail("잠항 중인 엘리트 잠수함의 위치가 표식으로 드러남");
            if (!subShown) Fail("부상한 엘리트 잠수함에 표식이 보이지 않음");
            if (radar != null && (elite < 2 || plain < 1)) Fail("레이더에 엘리트(황금)·일반 에코가 구별되어 찍히지 않음");

            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 자폭 보트

        /// <summary>
        /// 자폭 보트: 고속정보다 작고 빠르고 약한지, 전속으로 달아나는 함선을 따라잡아 부딪혀 터지는지(무장 끔),
        /// 기본 무장(기관포)으로 막을 수 있는지, 격침하면 경험치·함선 피해 없음인지. 캡처 포함.
        /// </summary>
        private IEnumerator SuicideBoatCheck()
        {
            _report.AppendLine("\n## 자폭 보트");
            var ship = GameManager.Instance.Player;
            var boatDef = CombatDevTools.FindEnemy("ene_suicide_boat");
            var fastDef = CombatDevTools.FindEnemy("ene_fastboat");
            if (boatDef == null || boatDef.Prefab == null) { Fail("자폭 보트 데이터·프리팹 없음"); yield break; }
            var bc = boatDef.Prefab.GetComponent<BoxCollider>();
            var fc = fastDef != null && fastDef.Prefab != null ? fastDef.Prefab.GetComponent<BoxCollider>() : null;
            _report.AppendLine($"- 데이터: 체력 {boatDef.MaxHp} (고속정 {fastDef?.MaxHp}) · 속력 {boatDef.MoveSpeed} m/s ≈ {Game.Ship.ShipController.ToKnots(boatDef.MoveSpeed):0}노트 (고속정 {fastDef?.MoveSpeed}, 함선 최고 {ship.BaseMaxSpeed:0.0}) · 충돌 피해 {boatDef.AttackDamage} · 선체 {bc?.size} (고속정 {fc?.size})");
            if (fastDef != null && !(boatDef.MaxHp < fastDef.MaxHp && boatDef.MoveSpeed > fastDef.MoveSpeed)) Fail("자폭 보트가 고속정보다 약하고 빠르지 않음");
            if (bc != null && fc != null && bc.size.z >= fc.size.z) Fail("자폭 보트가 고속정보다 작지 않음");
            if (boatDef.MoveSpeed <= ship.BaseMaxSpeed) Fail("자폭 보트가 함선보다 느림");

            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);

            // 1) 무장 끔, 전속으로 달아나는 함선 뒤쪽 45m에서 3척
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(1f);
            yield return new WaitForSeconds(3f);
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            float hp0 = ship.HullHp;
            var boats = new List<EnemyController>();
            for (int i = 0; i < 3; i++)
            {
                Vector3 pos = ship.transform.position - fwd * 45f + Vector3.Cross(Vector3.up, fwd) * (i - 1) * 12f;
                var e = EnemySpawner.Instance.SpawnAt(boatDef, pos, Quaternion.LookRotation(fwd, Vector3.up));
                if (e != null) boats.Add(e);
            }
            float t0 = Time.time, lastHit = -1f, topSpeed = 0f;
            bool shot = false;
            while (Time.time - t0 < 30f)
            {
                int alive = 0;
                foreach (var b in boats) if (b != null && b.isActiveAndEnabled && b.IsAlive) { alive++; topSpeed = Mathf.Max(topSpeed, b.CurrentSpeed); }
                if (!shot && Time.time - t0 > 1.5f && boats.Count > 0 && boats[0].isActiveAndEnabled)
                {
                    shot = true;
                    yield return CloseShot("suicide_boat", boats[0].transform, 11f);
                }
                if (ship.HullHp < hp0 - 0.01f && lastHit < 0f) lastHit = Time.time - t0;
                if (alive == 0) break;
                yield return null;
            }
            int detonated = 0;
            foreach (var b in boats) if (b is SuicideBoat sb && sb.Detonated) detonated++;
            _report.AppendLine($"- 무장 끔 · 전속 도주 · 뒤쪽 45m에서 3척: 충돌 폭발 {detonated}/3 · 첫 피해 {lastHit:0.0}초 · 선체 피해 {hp0 - ship.HullHp:0.#} · 보트 최고 속력 {topSpeed:0.0}");
            if (detonated < 3) Fail("자폭 보트가 도주하는 함선에 부딪히지 못함");
            if (hp0 - ship.HullHp <= 0.01f) Fail("자폭 보트 충돌 피해가 없음");

            // 2) 기본 무장으로 막기: 정지한 함선 둘레 50m에서 4척
            foreach (var d in off) if (d != null) d.enabled = true;
            ship.SetEngineOrder(0f);
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(3f);
            hp0 = ship.HullHp;
            int kills0 = GameManager.Instance.TotalKills;
            var swarm = CombatDevTools.SpawnRing("ene_suicide_boat", 4, 50f, 20f);
            t0 = Time.time;
            while (Time.time - t0 < 15f)
            {
                int alive = 0;
                foreach (var b in swarm) if (b != null && b.isActiveAndEnabled && b.IsAlive) alive++;
                if (alive == 0) break;
                yield return null;
            }
            detonated = 0;
            foreach (var b in swarm) if (b is SuicideBoat sb && sb.Detonated) detonated++;
            int killed = GameManager.Instance.TotalKills - kills0;
            _report.AppendLine($"- 무장 켬(시험 편성) · 정지 · 둘레 50m에서 4척: 격침 {killed} · 충돌 {detonated} · 선체 피해 {hp0 - ship.HullHp:0.#}");
            if (killed + detonated < 4) Fail("자폭 보트가 사라지지 않음(격침·충돌 합계 부족)");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        // ------------------------------------------------------------ 어뢰 회피

        /// <summary>
        /// 어뢰 잠수함 한 척이 3/4 속력으로 곧게 가는 함선의 옆에서 어뢰를 쏜다.
        /// 그대로 가면 맞는지, 발사를 본 순간 전령기를 한 칸 내리면(또는 올리면) 피하는지 여러 번 잰다.
        /// </summary>
        private IEnumerator TorpedoDodgeCheck()
        {
            _report.AppendLine("\n## 어뢰 회피");
            var ship = GameManager.Instance.Player;
            var def = CombatDevTools.FindEnemy("ene_submarine");
            _report.AppendLine($"- 어뢰 잠수함: 선회 {def.PreferredRange}m · 피해 {def.AttackDamage} · 간격 {def.AttackCooldown}초 · 어뢰 {(def.Prefab != null && PrivateField<GameObject>(def.Prefab.GetComponent<Submarine>(), "torpedoPrefab") is GameObject tpo && tpo.TryGetComponent<Torpedo>(out var torpedoInfo) ? torpedoInfo.Speed : 0f):0.#} m/s(함선 최고 {ship.BaseMaxSpeed:0.#})");
            // 회피 조작만 재도록 섬은 치운다(판마다 다른 섬 배치가 어뢰를 막아 결과가 흔들린다)
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            var off = DisableWeapons(null);

            var cases = new (string name, int shift, float delay)[]
            {
                ("반응 없음", 0, 0f),
                ("경고 0.6초 뒤 전령기 한 칸 내림(3/4→1/2)", -1, 0.6f),
                ("경고 0.6초 뒤 한 칸 올림(3/4→FULL)", +1, 0.6f),
                ("경고 2.0초 뒤(발사 순간) 한 칸 내림", -1, 2.0f),
                ("경고 2.0초 뒤(발사 순간) 한 칸 올림", +1, 2.0f),
            };
            const int Trials = 4;
            foreach (var (label, shift, delay) in cases)
            {
                int hits = 0, launched = 0;
                float travelSum = 0f, warnSum = 0f;
                for (int trial = 0; trial < Trials; trial++)
                {
                    CombatDevTools.ClearBattlefield();
                    foreach (var tp in new List<Torpedo>(Torpedo.Active)) if (tp != null) PoolManager.Instance?.Despawn(tp.gameObject);
                    ship.DevRudderOverride = 0f;
                    ship.SetEngineOrder(0.75f);
                    yield return new WaitForSeconds(3f);   // 속력이 붙을 때까지

                    // 좌현·우현을 번갈아, 약간 앞쪽 옆에서
                    float side = trial % 2 == 0 ? 1f : -1f;
                    Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, fwd);
                    Vector3 pos = ship.transform.position + right * side * 48f + fwd * 12f;   // 최소 사격 거리(40m) 밖
                    pos.y = 0f;
                    var sub = EnemySpawner.Instance.SpawnAt(def, pos, Quaternion.LookRotation((ship.transform.position - pos).normalized, Vector3.up));
                    Put(sub, "AttackTimer", 0f);   // 등장 직후 대기(첫 발 지연)를 건너뛰고 바로 쏘게
                    float hp0 = ship.HullHp, t0 = Time.time, launchAt = -1f, warnAt = -1f;
                    bool reacted = false;
                    var torpedoSub = sub as Submarine;
                    while (Time.time - t0 < 11f)
                    {
                        if (warnAt < 0f && torpedoSub != null && torpedoSub.IsPreparingTorpedo) warnAt = Time.time;
                        if (launchAt < 0f && Torpedo.Active.Count > 0)
                        {
                            launchAt = Time.time;
                            if (sub != null) sub.DevFrozen = true;   // 두 번째 어뢰 없이 한 발만 잰다
                        }
                        // 경고(사격 제원 결정)를 본 뒤 delay초에 반응
                        if (warnAt > 0f && !reacted && Time.time - warnAt >= delay)
                        {
                            reacted = true;
                            if (shift != 0) ship.ShiftEngineOrder(shift);
                        }
                        if (launchAt > 0f && Torpedo.Active.Count == 0) break;
                        yield return null;
                    }
                    if (warnAt > 0f && launchAt > 0f) warnSum += launchAt - warnAt;
                    if (launchAt > 0f) { launched++; travelSum += Time.time - launchAt; }
                    if (ship.HullHp < hp0 - 0.01f) hits++;
                }
                _report.AppendLine($"- {label}: 발사 {launched}/{Trials} · 명중 {hits} · 경고→발사 {(launched > 0 ? warnSum / launched : 0f):0.0}초 · 발사→소멸 평균 {(launched > 0 ? travelSum / launched : 0f):0.0}초");
                if (label == "반응 없음" && hits < Trials / 2) _report.AppendLine("  - 참고: 반응하지 않아도 절반 넘게 빗나감");
                if (shift != 0 && delay <= 0.6f && hits > Trials / 2) Fail($"{label}: 경고를 보고 반응했는데도 절반 넘게 맞음({hits}/{Trials})");
                if (shift == 0 && hits < Trials / 2) Fail($"{label}: 반응하지 않았는데 절반 넘게 빗나감({hits}/{Trials})");
            }

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        /// <summary>
        /// 엘리트 초계함 주변 대형 물체 추적(2026-10-05, 사용자 제보: 초계함을 따라다니는 거대한 어두운 구).
        /// 무장·호위함·기만체를 그대로 켠 실제 교전에서 초계함(+ 미사일정·고속정)을 60초 동안 상대하며, 0.25초마다
        /// 초계함 30m 안에서 크기가 8m를 넘는 렌더러(바다·섬·플레이어 함선 제외)를 모두 기록한다(경로·종류·크기·재질).
        /// </summary>
        private IEnumerator PccSoakCheck()
        {
            _report.AppendLine("\n## 엘리트 초계함 주변 대형 물체 추적");
            var ship = GameManager.Instance.Player;
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var formation = ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>();
            if (formation != null)
            {
                formation.ClearAll();
                foreach (var role in new[] { Game.TaskForce.EscortRole.AirDefense, Game.TaskForce.EscortRole.ElectronicWarfare })
                {
                    int i = formation.Deploy();
                    if (i >= 0) formation.AssignRole(i, role);
                }
            }
            ship.SetEngineOrder(0.5f);

            string PathOf(Transform t)
            {
                var sb = new StringBuilder(t.name);
                for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
                return sb.ToString();
            }

            var seen = new Dictionary<string, string>();
            var pccs = new List<EnemyController>();
            float t0 = Time.time, nextSpawnCheck = 0f, nextShot = 5f;
            int shots = 0, kills = 0;
            bool hurt = false, sunk = false;
            Vector3 lastPcc = ship.transform.position;
            while (Time.time - t0 < 75f)
            {
                float t = Time.time - t0;
                // 15초: 체력 40%(회피·손상 단계), 45초: 격침(잔해) — 그 뒤 새 초계함은 띄우지 않는다
                if (!hurt && t >= 15f && pccs.Count > 0 && pccs[0] != null)
                {
                    hurt = true;
                    var e = pccs[0];
                    e.TakeDamage(new DamageInfo(e.Definition.MaxHp * 0.6f, e.transform.position, Vector3.up, DamageSource.Gun));
                    _report.AppendLine($"- t{t:0.0}: 초계함 체력 60% 피해");
                }
                if (!sunk && t >= 45f && pccs.Count > 0 && pccs[0] != null)
                {
                    sunk = true;
                    var e = pccs[0];
                    lastPcc = e.transform.position;
                    e.TakeDamage(new DamageInfo(e.Definition.MaxHp * 2f, e.transform.position, Vector3.up, DamageSource.Gun));
                    _report.AppendLine($"- t{t:0.0}: 초계함 격침");
                    nextSpawnCheck = 999f;
                }
                ship.RepairHull(100f);   // 관찰 중 함선이 가라앉지 않게
                pccs.RemoveAll(e => e == null || !e.IsAlive);
                if (pccs.Count == 0 && t >= nextSpawnCheck)
                {
                    if (t > 1f) kills++;
                    pccs.AddRange(CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 30f, Random.Range(0f, 360f)));
                    CombatDevTools.SpawnRing("ene_missileboat", 1, 40f, Random.Range(0f, 360f));
                    CombatDevTools.SpawnRing("ene_fastboat", 2, 26f, Random.Range(0f, 360f));
                    nextSpawnCheck = t + 2f;
                }

                var centers = new List<Vector3>();
                foreach (var pcc in pccs) if (pcc != null) { centers.Add(pcc.transform.position); lastPcc = pcc.transform.position; }
                if (centers.Count == 0) centers.Add(lastPcc);
                foreach (var c in centers)
                {
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                        var b = r.bounds;
                        float size = Mathf.Max(b.size.x, b.size.z);
                        if (size < 8f) continue;
                        Vector3 d = b.center - c; d.y = 0f;
                        if (d.magnitude > 30f) continue;
                        if (r.transform.IsChildOf(ship.transform) || r.GetComponentInParent<Game.View.OceanSurface>() != null) continue;
                        if (r.gameObject.layer == Game.World.Islands.Layer || r.GetComponentInParent<Game.World.IslandField>() != null) continue;
                        string key = PathOf(r.transform);
                        if (seen.ContainsKey(key)) continue;
                        string mat = r.sharedMaterial != null ? $"{r.sharedMaterial.name}({(r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "?")})" : "없음";
                        var mf = r.GetComponent<MeshFilter>();
                        seen[key] = $"t{t:0.0} · {r.GetType().Name} · 크기 {b.size.x:0.#}×{b.size.y:0.#}×{b.size.z:0.#} · 중심 거리 {d.magnitude:0.#}m · 메시 {(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} · 재질 {mat} · 배율 {r.transform.lossyScale.x:0.##}";
                    }
                }

                if (t >= nextShot && shots < 6)
                {
                    shots++;
                    nextShot = t + 12f;
                    if (pccs.Count > 0 && pccs[0] != null) yield return CloseShot($"pcc_soak_{shots}", pccs[0].transform, 26f);
                    else yield return Shot($"pcc_soak_{shots}_wreck");
                }
                yield return new WaitForSeconds(0.25f);
            }

            _report.AppendLine($"- 75초 교전 · 초계함 격침(교전 중) {kills}회 · 30m 안 8m 넘는 렌더러 {seen.Count}개");
            foreach (var kv in seen) _report.AppendLine($"  - {kv.Key}: {kv.Value}");
            ship.SetEngineOrder(0f);
            if (formation != null) formation.ClearAll();
            CombatDevTools.ClearBattlefield();
        }

        /// <summary>
        /// 순항미사일 잠수함·엘리트 초계함 검사, 기존 적(고속정·미사일정·어뢰 잠수함·보스) 기본 동작, 스테이지 정리.
        /// </summary>
        private IEnumerator NewEnemyCheck()
        {
            _report.AppendLine("\n## 스테이지 2 추가 적");
            var ship = GameManager.Instance.Player;
            var targeting = ship.GetComponentInChildren<TargetingSystem>();

            // 0) 데이터
            var subDef = CombatDevTools.FindEnemy("ene_cruise_submarine");
            var pccDef = CombatDevTools.FindEnemy("ene_pcc_corvette");
            if (subDef == null || pccDef == null) { Fail("추가 적 데이터를 찾지 못함(RoundSet_Stage2 참조 확인)"); yield break; }
            _report.AppendLine($"- 데이터: {subDef.DisplayName} HP {subDef.MaxHp} 거리 {subDef.PreferredRange} 미사일 {(subDef.MissilePrefab != null ? subDef.MissilePrefab.name : "없음")} · " +
                               $"{pccDef.DisplayName} HP {pccDef.MaxHp} 거리 {pccDef.PreferredRange} 피해 {pccDef.AttackDamage} 쿨다운 {pccDef.AttackCooldown} 가치 {pccDef.TargetValue}");

            // 1) 순항미사일 잠수함: 발사 준비 때만 부상, 발사 직후 잠항하고 마지막 발사 위치만 남긴다.
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var off = DisableWeapons(r => r is CiwsModule);
            CombatStats.Reset();
            CombatLog.Clear();
            var subs = CombatDevTools.SpawnRing("ene_cruise_submarine", 1, 48f, 60f);
            var sub = subs.Count > 0 ? subs[0] as CruiseMissileSubmarine : null;
            if (sub == null) { Fail("순항미사일 잠수함 스폰 실패(프리팹 컴포넌트 확인)"); foreach (var d in off) d.enabled = true; yield break; }

            bool hiddenAtStart = !sub.IsRevealed;
            var submarineModel = sub.transform.Find("ModelPivot");
            bool modelHiddenAtStart = submarineModel != null;
            if (submarineModel != null)
                foreach (var renderer in submarineModel.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) modelHiddenAtStart = false;
            float start = Time.time, firedAt = -1f, hiddenAgainAt = -1f, minDist = 999f, maxDist = 0f;
            bool surfacedDuringPrep = false, trackedWhileRevealed = false, signatureAfterFire = false;
            int torpedoes = 0, missilesSeen = 0;
            bool shotTaken = false;
            while (Time.time - start < 30f && sub.IsAlive)
            {
                float d = Vector3.Distance(sub.transform.position, ship.transform.position);
                if (Time.time - start > 8f) { minDist = Mathf.Min(minDist, d); maxDist = Mathf.Max(maxDist, d); }
                torpedoes = Mathf.Max(torpedoes, ActiveTorpedoes());
                missilesSeen = Mathf.Max(missilesSeen, TargetRegistry.Get(TargetKind.Missile).Count);
                if (sub.IsAttacking && sub.IsRevealed) surfacedDuringPrep = true;
                if (sub.IsRevealed && targeting != null)
                    foreach (var t in targeting.DetectedSubmarines) if (ReferenceEquals(t, sub)) trackedWhileRevealed = true;
                if (firedAt < 0f && sub.MissilesFired > 0)
                {
                    firedAt = Time.time;
                    signatureAfterFire = sub.HasLaunchSignature;
                }
                if (!shotTaken && firedAt > 0f && Time.time - firedAt > 1.5f)
                {
                    shotTaken = true;
                    yield return CloseShot("cruise_sub_launch", sub.transform, 14f);
                }
                if (firedAt > 0f && hiddenAgainAt < 0f && !sub.IsRevealed) hiddenAgainAt = Time.time;
                if (hiddenAgainAt > 0f && Time.time - hiddenAgainAt > 1f) break;
                yield return null;
            }
            int intercepted = CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var ie) ? ie.Count : 0;
            int hit = CombatStats.Threats.TryGetValue(ThreatOutcome.HitShip, out var he) ? he.Count : 0;
            float exposed = firedAt > 0f && hiddenAgainAt > 0f ? hiddenAgainAt - firedAt : -1f;
            _report.AppendLine($"- 순항미사일 잠수함: 시작 잠항 {(hiddenAtStart ? "예" : "아니오")} · 첫 발사 {(firedAt > 0 ? $"{firedAt - start:0.0}초" : "없음")} · 미사일 {sub.MissilesFired}발 · 어뢰 {torpedoes} · " +
                               $"준비 중 부상 {(surfacedDuringPrep ? "예" : "아니오")} · 발사 뒤 노출 {exposed:0.0}초 · 발사 흔적 {(signatureAfterFire ? "예" : "아니오")} · 부상 중 추적 {(trackedWhileRevealed ? "예" : "아니오")} · " +
                               $"거리 {minDist:0}~{maxDist:0}m · 미사일 요격/명중 {intercepted}/{hit}");
            if (!hiddenAtStart) Fail("순항미사일 잠수함이 처음부터 드러나 있음");
            if (!modelHiddenAtStart) Fail("잠항 중인 잠수함 모델이 화면에 보임");
            if (firedAt < 0f) Fail("순항미사일 잠수함이 미사일을 쏘지 않음");
            if (torpedoes > 0) Fail("순항미사일 잠수함이 어뢰를 쏨");
            if (firedAt > 0f && !surfacedDuringPrep) Fail("미사일 발사 준비 중 잠수함이 부상하지 않음");
            if (firedAt > 0f && (exposed < 0f || exposed > 0.5f)) Fail($"미사일 발사 뒤 즉시 잠항하지 않음({exposed:0.0}초)");
            if (firedAt > 0f && !signatureAfterFire) Fail("발사 뒤 수색 단서가 남지 않음");
            if (firedAt > 0f && !trackedWhileRevealed) Fail("드러난 잠수함을 함선이 추적하지 못함");
            if (minDist < 40f) Fail($"순항미사일 잠수함이 너무 가까이 옴({minDist:0}m)");
            if (intercepted + hit == 0 && sub.MissilesFired > 0) _report.AppendLine("  - 참고: 발사한 미사일의 결과가 기록되지 않음(시험 시간 안에 도달하지 않음)");

            // 노출 중에는 일반 무기가 노릴 수 있다: 폭뢰가 드러낸 뒤 기관포 표적에 잡히는지
            sub.RevealFor(10f);
            yield return new WaitForSeconds(0.6f);
            bool surfacedModelVisible = false;
            if (submarineModel != null)
                foreach (var renderer in submarineModel.GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) surfacedModelVisible = true;
            if (!surfacedModelVisible) Fail("부상 후에도 잠수함 모델이 보이지 않음");
            var best = targeting != null ? targeting.GetBest(sub.transform.position, 5f, TargetClass.Submarine, (t, sqr) => sqr) : null;
            _report.AppendLine($"- 드러난 잠수함을 일반 무기 표적 선택이 고름: {(ReferenceEquals(best, sub) ? "예" : "아니오")}");
            if (!ReferenceEquals(best, sub)) Fail("드러난 순항미사일 잠수함이 일반 무기 표적이 되지 않음");

            // 사망 시 정리
            sub.TakeDamage(new DamageInfo(9999f, sub.transform.position, Vector3.up, DamageSource.Gun));
            yield return null;
            _report.AppendLine($"- 잠수함 격침 뒤: 활성 {sub.isActiveAndEnabled} · 공격 절차 {sub.IsAttacking} · 노출 {sub.IsRevealed}");
            if (sub.isActiveAndEnabled || sub.IsAttacking || sub.IsRevealed) Fail("격침된 순항미사일 잠수함 상태가 정리되지 않음");
            foreach (var d in off) if (d != null) d.enabled = true;
            AppendStats();

            // 2) 엘리트 초계함: 4문 독립 선회, 4발 살보, 연막 중 사격 중지, 회피, 정리
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            off = DisableWeapons(null);
            CombatLog.Clear();
            var pccs = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 30f, 30f);
            var pcc = pccs.Count > 0 ? pccs[0] as PccCorvette : null;
            if (pcc == null) { Fail("초계함 스폰 실패"); foreach (var d in off) d.enabled = true; yield break; }

            var turrets = new List<Transform>();
            foreach (var name in new[] { "ForeGun01", "ForeGun02", "AftGun01", "AftGun02" })
            {
                var t = FindChild(pcc.transform, name);
                if (t != null) turrets.Add(t);
            }
            var startRot = new List<Quaternion>();
            foreach (var t in turrets) startRot.Add(Quaternion.Inverse(pcc.transform.rotation) * t.rotation);
            var maxTurn = new float[turrets.Count];
            int visible = 0;
            foreach (var t in turrets)
                foreach (var r in t.GetComponentsInChildren<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) { visible++; break; }

            start = Time.time;
            int maxShellsPerSalvo = 0, lastShells = 0, lastSalvos = 0;
            float firstSalvo = -1f;
            bool pccShot = false;
            while (Time.time - start < 25f && pcc.IsAlive)
            {
                for (int i = 0; i < turrets.Count; i++)
                {
                    var rel = Quaternion.Inverse(pcc.transform.rotation) * turrets[i].rotation;
                    maxTurn[i] = Mathf.Max(maxTurn[i], Quaternion.Angle(startRot[i], rel));
                }
                if (pcc.SalvosFired != lastSalvos)
                {
                    maxShellsPerSalvo = Mathf.Max(maxShellsPerSalvo, pcc.ShellsFired - lastShells);
                    lastShells = pcc.ShellsFired;
                    lastSalvos = pcc.SalvosFired;
                    if (firstSalvo < 0f) firstSalvo = Time.time - start;
                }
                if (!pccShot && pcc.IsFiring)
                {
                    pccShot = true;
                    yield return CloseShot("pcc_salvo", pcc.transform, 16f);
                }
                if (pcc.SalvosFired >= 3 && !pcc.IsFiring) break;
                yield return null;
            }
            var turns = new StringBuilder();
            int turned = 0;
            for (int i = 0; i < turrets.Count; i++) { turns.Append($"{turrets[i].name} {maxTurn[i]:0}° "); if (maxTurn[i] > 10f) turned++; }
            _report.AppendLine($"- 초계함: 포탑 {turrets.Count}개(보임 {visible}) · 선회 {turns}· 첫 살보 {firstSalvo:0.0}초 · 살보 {pcc.SalvosFired} · 포탄 {pcc.ShellsFired} · 살보당 최대 {maxShellsPerSalvo}발");
            if (turrets.Count != 4 || visible != 4) Fail("초계함 포탑 4개가 모두 보이지 않음");
            if (turned < 4) Fail($"초계함 포탑이 모두 돌지 않음({turned}/4)");
            if (pcc.SalvosFired == 0) Fail("초계함이 일제사격하지 않음");
            if (maxShellsPerSalvo != 4) Fail($"초계함 살보가 4발이 아님({maxShellsPerSalvo})");

            // 연막 중에는 쏘지 않는다
            var smoke = SmokeScreen.Instance;
            if (smoke != null)
            {
                yield return new WaitUntil(() => !pcc.IsFiring);
                smoke.Deploy(10f);
                int shells0 = pcc.ShellsFired;
                float s0 = Time.time;
                while (Time.time - s0 < 9.5f && pcc.IsAlive) yield return null;
                int during = pcc.ShellsFired - shells0;
                _report.AppendLine($"- 연막 9.5초 동안 초계함 포탄 {during}발 (쿨다운 {pccDef.AttackCooldown}초)");
                if (during > 0) Fail($"연막 중 초계함이 {during}발 쏨");
                while (SmokeScreen.IsActive) yield return null;
            }
            else _report.AppendLine("- 참고: SmokeScreen이 씬에 없어 연막 시험을 건너뜀");

            // 엘리트 회피: 절반 이하에서 한 번, 속력 ×1.35
            float hpHit = pccDef.MaxHp * 0.55f;
            pcc.TakeDamage(new DamageInfo(hpHit, pcc.transform.position, Vector3.up, DamageSource.Gun));
            yield return null;
            yield return null;
            bool evading = pcc.IsEvading;
            float topSpeed = 0f, e0 = Time.time;
            while (Time.time - e0 < 2.2f && pcc.IsAlive) { topSpeed = Mathf.Max(topSpeed, pcc.CurrentSpeed); yield return null; }
            yield return new WaitForSeconds(0.6f);
            bool evadeEnded = !pcc.IsEvading;
            _report.AppendLine($"- 체력 {pcc.CurrentHp:0}/{pccDef.MaxHp}: 회피 시작 {(evading ? "예" : "아니오")} · 최고 속력 {topSpeed:0.0} (기본 {pccDef.MoveSpeed}) · 2.8초 뒤 종료 {(evadeEnded ? "예" : "아니오")} · 체력 회복 없음 {(pcc.CurrentHp <= pccDef.MaxHp - hpHit + 0.01f ? "예" : "아니오")}");
            if (!evading) Fail("초계함이 체력 절반에서 회피 기동을 하지 않음");
            if (topSpeed <= pccDef.MoveSpeed + 0.2f) Fail("회피 중 속력이 오르지 않음");
            if (!evadeEnded) Fail("회피 기동이 끝나지 않음");

            // 살보 도중 격침 → 사격·회피 정리
            float w0 = Time.time;
            while (!pcc.IsFiring && Time.time - w0 < 10f) yield return null;
            bool killedMidSalvo = pcc.IsFiring;
            pcc.TakeDamage(new DamageInfo(9999f, pcc.transform.position, Vector3.up, DamageSource.Gun));
            yield return null;
            _report.AppendLine($"- 초계함 격침(살보 도중 {(killedMidSalvo ? "예" : "아니오")}): 활성 {pcc.isActiveAndEnabled} · 사격 {pcc.IsFiring} · 회피 {pcc.IsEvading}");
            if (pcc.isActiveAndEnabled || pcc.IsFiring || pcc.IsEvading) Fail("격침된 초계함의 사격·회피 상태가 정리되지 않음");
            foreach (var d in off) if (d != null) d.enabled = true;

            // 3) 기존 적 기본 동작(사격 여부) — 아군 무기를 끄고 잠깐 둔다
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            off = DisableWeapons(null);
            CombatStats.Reset();
            var olds = new List<EnemyController>();
            olds.AddRange(CombatDevTools.SpawnRing("ene_fastboat", 2, 20f, 0f));
            olds.AddRange(CombatDevTools.SpawnRing("ene_missileboat", 1, 34f, 45f));
            olds.AddRange(CombatDevTools.SpawnRing("ene_submarine", 1, 50f, 200f));   // 최소 사격 거리(40m) 밖에서 시작
            foreach (var e in olds) if (e is Submarine s) { s.RevealFor(0f); Put(s, "AttackTimer", 0f); }   // 등장 직후 첫 발 지연은 따로 검증
            int enemyShells = 0, oldTorps = 0, oldMissiles = 0;
            start = Time.time;
            while (Time.time - start < 16f)
            {
                enemyShells = Mathf.Max(enemyShells, ActiveCount("PRJ_EnemyGun"));
                oldTorps = Mathf.Max(oldTorps, ActiveTorpedoes());
                oldMissiles = Mathf.Max(oldMissiles, TargetRegistry.Get(TargetKind.Missile).Count);
                yield return null;
            }
            _report.AppendLine($"- 기존 적(고속정 2·미사일정 1·어뢰 잠수함 1[50m], 16초): 고속정 포탄 {(enemyShells > 0 ? "O" : "X")} · 미사일 {(oldMissiles > 0 ? "O" : "X")} · 어뢰 {(oldTorps > 0 ? "O" : "X")}");
            if (enemyShells == 0) Fail("고속정이 쏘지 않음");
            if (oldMissiles == 0) Fail("미사일정이 쏘지 않음");
            if (oldTorps == 0) Fail("어뢰 잠수함이 어뢰를 쏘지 않음");

            // 4) 스테이지 전환 정리: 추가 적이 살아 있는 상태에서 ClearAll
            var both = new List<EnemyController>();
            both.AddRange(CombatDevTools.SpawnRing("ene_cruise_submarine", 1, 45f, 120f));
            both.AddRange(CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 25f, 240f));
            yield return new WaitForSeconds(3f);
            EnemySpawner.Instance?.ClearAll();
            yield return null;
            int alive = 0;
            foreach (var e in both) if (e != null && e.isActiveAndEnabled) alive++;
            foreach (var e in olds) if (e != null && e.isActiveAndEnabled) alive++;
            _report.AppendLine($"- 스테이지 정리(ClearAll) 뒤 남은 적: {alive}");
            if (alive > 0) Fail("스테이지 정리 뒤 적이 남음");
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
        }
    }
}
#endif
