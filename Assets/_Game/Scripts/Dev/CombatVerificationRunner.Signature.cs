#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>자동 전투 검증 — 시작 무장·손상통제 개편·함정 고유 스킬(2026-10-11).</summary>
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 1) 시작 무장: 시작 함선 넷 모두 함수·함미 끝에 무기(기관포·VLS), 함교 원격 기관총이 가까운 고속정을 쏜다.
        /// 2) 손상통제: 블록 없음 = 응급 수리 1회·자동 회복 50%에서 멈춤, 손상통제반 1개 = 2회·상한 67%·속도 1.5배,
        ///    응급 수리 쓰면 1회 줄고 보스 격침이면 다시 가득.
        /// 3) 고유 스킬 넷: 광역 탐색(표적 지시·잠수함 노출·피해 +25%), 돌격 기동(속력·피해 감소·충돌), 전탄 발사(남은 셀 전부),
        ///    항공대 호출(전투기 3 + 조기경보기 1, 기총 공격·레이더 고리·이탈). 장면마다 찍는다.
        /// </summary>
        /// <summary>
        /// 예광탄(2026-10-21): 기관포는 4발에 1발, CIWS는 5발에 1발 — 꼬리가 길고 굵은 주황, 탄두가 빛난다.
        /// 풀에서 다시 나온 탄은 보통 탄(꼬리·크기·재질)으로 돌아와야 한다.
        /// </summary>
        private IEnumerator TracerCheck()
        {
            _report.AppendLine("\n## 예광탄(기관포 · CIWS)");
            var ship = GameManager.Instance.Player;
            int placed = ResetLoadout(M("mod_autocannon"), M("mod_ciws", 2, 0));
            yield return new WaitForSeconds(0.5f);
            CombatDevTools.ClearBattlefield();
            var boats = CombatDevTools.SpawnRing("ene_fastboat", 6, 18f);
            foreach (var b in boats) b.DevFrozen = true;
            CombatDevTools.SpawnRing("ene_drone", 4, 34f, 45f);

            // 발 수: 풀이 같은 탄 오브젝트를 다시 쓰므로, 오브젝트가 새로 발사될 때(나이가 줄어들 때)마다 한 발로 센다
            var tracers = new Dictionary<string, int>();
            var normals = new Dictionary<string, int>();
            var lastAge = new Dictionary<Projectile, float>();
            int badTracer = 0, badReset = 0;
            bool shot = false;
            Projectile flying = null;
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                {
                    string kind = p.name.StartsWith("PRJ_Autocannon") ? "기관포" : p.name.StartsWith("PRJ_Ciws") ? "CIWS" : null;
                    if (kind == null || !p.isActiveAndEnabled) continue;
                    var trail = Get<TrailRenderer>(p, "_trail");
                    float baseTime = Get<float>(p, "tracerTime");
                    Vector3 baseScale = Get<Vector3>(p, "_baseScale");
                    bool big = p.transform.localScale.x > baseScale.x * 1.2f;
                    float age = Get<float>(p, "_age");
                    bool fresh = !lastAge.TryGetValue(p, out float before) || age < before;
                    lastAge[p] = age;
                    if (!fresh) continue;
                    var counts = p.IsTracerRound ? tracers : normals;
                    counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
                    var head = p.GetComponent<MeshRenderer>();
                    bool glow = head == null || (head.sharedMaterial != null && head.sharedMaterial.name.StartsWith("Tracer round head"));
                    if (p.IsTracerRound) { if (trail == null || trail.time < baseTime * 2f || !big || !glow) badTracer++; if (flying == null && kind == "기관포") flying = p; }
                    else if (big || (trail != null && baseTime > 0f && trail.time > baseTime * 1.5f)) badReset++;
                }
                // 날아가는 기관포 예광탄을 멈춰 세우고 찍는다(가까이 · 전투 화면)
                if (!shot && t > 1f && flying != null && flying.isActiveAndEnabled && flying.IsTracerRound && Get<float>(flying, "_age") > 0.04f)
                {
                    shot = true;
                    float scale = Time.timeScale;
                    Time.timeScale = 0f;
                    yield return CloseShot("tracer_round_close", flying.transform, 5f);
                    yield return ScreenShot("tracer_rounds");
                    Time.timeScale = scale;
                }
                if (flying != null && (!flying.isActiveAndEnabled || !flying.IsTracerRound)) flying = null;
                yield return null;
            }
            int Count(string k) => tracers.TryGetValue(k, out int c) ? c : 0;
            int Normal(string k) => normals.TryGetValue(k, out int n) ? n : 0;
            _report.AppendLine($"- 설치 {placed} · 6초 관찰: 기관포 예광탄 {Count("기관포")} / 보통 탄 {Normal("기관포")}발 · CIWS 예광탄 {Count("CIWS")} / 보통 탄 {Normal("CIWS")}발 · " +
                               $"예광탄 모양 이상 {badTracer} · 풀 재사용 뒤 되돌아오지 않은 탄 {badReset}");
            if (Count("기관포") == 0) Fail("기관포가 예광탄을 쏘지 않음");
            if (Count("CIWS") == 0) Fail("CIWS가 예광탄을 쏘지 않음");
            if (Normal("기관포") == 0 || Normal("CIWS") == 0) Fail("보통 탄이 보이지 않음(모든 탄이 예광탄)");
            if (badTracer > 0) Fail("예광탄의 꼬리·탄두가 보통 탄과 다르지 않음");
            if (badReset > 0) Fail("풀에서 다시 나온 탄이 예광탄 모양으로 남음");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.3f);
        }

        /// <summary>
        /// 함급 패시브(2026-10-23): 시작 배치는 한 줄(옆칸 없음) · 함급 블록(레이더·통신 중계·흡기·사격통제·재장전 구획) 없음,
        /// 지휘함 7칸(헬기데크가 맨 뒤) · 나머지 6칸. 함교를 넷으로 차례로 바꿔 끼워 실제 함선 수치와 카드 제외를 본다.
        /// </summary>
        private IEnumerator ClassBridgeCheck()
        {
            var ship = GameManager.Instance.Player;
            ModuleType[] classBlocks = { ModuleType.Radar, ModuleType.FleetRelay, ModuleType.TurboIntake, ModuleType.FireControlArray, ModuleType.MissileLogistics };
            foreach (var concept in StartingShipCatalog.All)
            {
                if (concept == null || concept.StartLoadout == null) continue;
                int minX = int.MaxValue, maxX = int.MinValue; bool line = true, hasClass = false;
                string order = "";
                var entries = new List<StartingLoadout.Entry>(concept.StartLoadout.Entries);
                entries.Sort((a, b) => b.Origin.X.CompareTo(a.Origin.X));
                foreach (var e in entries)
                {
                    minX = Mathf.Min(minX, e.Origin.X); maxX = Mathf.Max(maxX, e.Origin.X + Mathf.Max(0, e.Module.Width - 1));
                    if (e.Origin.Z != 0) line = false;
                    if (System.Array.IndexOf(classBlocks, e.Module.Type) >= 0) hasClass = true;
                    order += (order.Length > 0 ? " – " : "") + e.Module.DisplayName;
                }
                int cells = maxX - minX + 1;
                bool command = concept.Kind == StartShipKind.Command;
                _report.AppendLine($"- 시작 배치 {concept.Title}: {order} · {cells}칸 · 한 줄 {Yes(line)} · 함급 블록 {(hasClass ? "있음" : "없음")}");
                if (!line) Fail($"{concept.Title}: 시작 배치가 한 줄이 아님");
                if (hasClass) Fail($"{concept.Title}: 시작 배치에 함급 블록이 남음(함교 패시브로 옮겼어야 함)");
                if (cells != (command ? 7 : 6)) Fail($"{concept.Title}: 시작 배치 길이가 {(command ? 7 : 6)}칸이 아님");
                if (command && entries[entries.Count - 1].Module.Type != ModuleType.HelicopterDeck) Fail("지휘함 헬기데크가 맨 뒤가 아님");
            }

            // 함교를 바꿔 끼우며 실제 수치(블록 없이 함교만)
            ResetLoadout();
            yield return null;
            ModuleInstance Bridge() { foreach (var m in ship.Grid.Modules) if (m?.Definition != null && m.Definition.Type == ModuleType.Bridge) return m; return null; }
            var original = Bridge();
            string originalId = original != null ? original.Definition.Id : null;
            GridCoord at = original != null ? original.Origin : new GridCoord(-1, 0);
            var blocks = new[] { ("mod_fleetrelay", ModuleType.FleetRelay), ("mod_turbointake", ModuleType.TurboIntake), ("mod_firecontrolarray", ModuleType.FireControlArray), ("mod_missilelogistics", ModuleType.MissileLogistics) };
            int baseTracks = -1;
            foreach (var (id, expect) in new[] { ("mod_bridge_patrol", ""), ("mod_bridge_command", "FleetRelay"), ("mod_bridge_assault", "TurboIntake"), ("mod_bridge_missile", "FireControlArray MissileLogistics") })
            {
                var old = Bridge();
                if (old != null) CombatDevTools.RemoveModule(old);
                var placedBridge = CombatDevTools.InstallModuleNear(id, at, 2);
                ship.Systems?.Recalculate();
                yield return null;
                if (placedBridge == null) { Fail($"함교 {id}를 끼우지 못함"); continue; }
                var s = ship.Systems;
                string covered = "";
                foreach (var (blockId, type) in blocks)
                {
                    var def = CombatDevTools.FindModule(blockId);
                    if (def != null && Game.Refit.RefitDraft.BridgeCovers(def, ship.Grid)) covered += (covered.Length > 0 ? " " : "") + type;
                }
                int tracks = s.MaxTrackedTargets;
                _report.AppendLine($"- 함교 패시브 {placedBridge.Definition.DisplayName}: 탐지 {s.DetectionRange:0} · 추적 {tracks} · 호위함 재사용 ×{s.EscortFireRateMultiplier:0.00} · " +
                                   $"속력 ×{s.SpeedMultiplier:0.00} · 가속 ×{s.AccelerationMultiplier:0.00} · 유도 사거리 ×{s.GuidedWeaponRangeMultiplier:0.00} · 미사일 장전 ×{s.MissileReloadMultiplier:0.00} · 카드 제외 [{covered}]");
                if (covered != expect) Fail($"{placedBridge.Definition.DisplayName}: 카드에서 빼는 함급 블록이 [{expect}]가 아님");
                switch (id)
                {
                    case "mod_bridge_patrol":
                        baseTracks = tracks - 4;
                        if (s.DetectionRange < 51.9f) Fail("범용 초계함 함교에 레이더 탐지(52)가 없음");
                        break;
                    case "mod_bridge_command":
                        if (Mathf.Abs(s.EscortFireRateMultiplier - 1.15f) > 0.001f) Fail("지휘함 함교에 통신 중계(+15%)가 없음");
                        if (baseTracks >= 0 && tracks != baseTracks + 1) Fail("지휘함 함교 추적 수가 기본(+1)이 아님");
                        break;
                    case "mod_bridge_assault":
                        if (Mathf.Abs(s.SpeedMultiplier - 1.1f) > 0.001f || Mathf.Abs(s.AccelerationMultiplier - 1.15f) > 0.001f) Fail("고속 강습함 함교에 흡기(속력 +10% · 가속 +15%)가 없음");
                        break;
                    case "mod_bridge_missile":
                        if (Mathf.Abs(s.GuidedWeaponRangeMultiplier - 1.1f) > 0.001f || Mathf.Abs(s.MissileReloadMultiplier - 0.85f) > 0.001f || s.DetectionRange < 47.9f)
                            Fail("미사일 구축함 함교에 사격통제·재장전 구획(유도 사거리 +10% · 장전 −15%) · 탐지 48이 없음");
                        if (baseTracks >= 0 && tracks != baseTracks + 3) Fail("미사일 구축함 함교 추적 수가 +3(기본 1 + 사격통제 2)이 아님");
                        break;
                }
            }
            // 원래 함교로 되돌린다
            var now = Bridge();
            if (now != null && now.Definition.Id != originalId) { CombatDevTools.RemoveModule(now); if (originalId != null) CombatDevTools.InstallModuleNear(originalId, at, 2); }
            ship.Systems?.Recalculate();
            yield return null;
        }

        private IEnumerator SignatureCheck()
        {
            _report.AppendLine("\n## 시작 무장 · 손상통제 · 함정 고유 스킬");
            var ship = GameManager.Instance.Player;
            if (GameManager.Instance.State != GameState.Playing) GameManager.Instance.SetState(GameState.Playing);
            Time.timeScale = TimeScale;
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            if (EnemySpawner.Instance != null && Get<Transform>(EnemySpawner.Instance, "_player") == null)
                Put(EnemySpawner.Instance, "_player", ship.transform);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            var boatDef = CombatDevTools.FindEnemy("ene_fastboat");
            Vector3 Fwd() { var f = ship.transform.forward; f.y = 0f; return f.normalized; }
            EnemyController Frozen(EnemyDefinition d, Vector3 p)
            {
                var e = EnemySpawner.Instance.SpawnAt(d, p, Quaternion.LookRotation(-Fwd()));
                if (e != null) { e.DevFrozen = true; e.transform.position = p; }
                return e;
            }

            // ---- 1) 시작 무장
            foreach (var concept in StartingShipCatalog.All)
            {
                if (concept == null || concept.StartLoadout == null) continue;
                static bool IsWeapon(ModuleType t) => t is ModuleType.Autocannon or ModuleType.Vls or ModuleType.NavalGun or ModuleType.Nobong;
                int minX = int.MaxValue, maxX = int.MinValue;
                foreach (var e in concept.StartLoadout.Entries)
                    if (IsWeapon(e.Module.Type)) { minX = Mathf.Min(minX, e.Origin.X); maxX = Mathf.Max(maxX, e.Origin.X + Mathf.Max(0, e.Module.Width - 1)); }
                string bow = "-", stern = "-";
                foreach (var e in concept.StartLoadout.Entries)
                {
                    if (!IsWeapon(e.Module.Type)) continue;
                    if (e.Origin.X == maxX) bow = e.Module.DisplayName;
                    if (e.Origin.X == minX) stern = e.Module.DisplayName;
                }
                _report.AppendLine($"- {concept.Title}: 함수 {bow} · 함미 {stern} (블록 {concept.StartLoadout.Entries.Count}개)");
                if (bow == "-" || stern == "-") Fail($"{concept.Title}: 함수·함미 무기가 하나씩이 아님");
            }

            yield return ClassBridgeCheck();

            int placed = ResetLoadout(M("mod_radar"));
            yield return new WaitForSeconds(0.5f);
            BridgeModule bridge = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is BridgeModule b) bridge = b;
            if (bridge == null) { Fail("함교 없음"); yield break; }
            var off = DisableWeapons(r => r is BridgeModule);
            {
                var boat = Frozen(boatDef, ship.transform.position + ship.transform.right * 12f);
                // 함교 원격 기관총은 2026-10-21 삭제 — 무기 블록 없이 함교만 있으면 가까운 고속정에도 쏘지 않아야 한다
                float hp0 = boat != null ? boat.CurrentHp : 0f;
                yield return new WaitForSeconds(4f);
                float lost = boat != null ? hp0 - (boat.IsAlive ? boat.CurrentHp : 0f) : 0f;
                bool mount = false;
                foreach (var t in bridge.GetComponentsInChildren<Transform>(true)) if (t.name == "BridgeMG") mount = true;
                _report.AppendLine($"- 함교 원격 기관총(삭제): 12m 고속정에 4초 피해 {lost:0.#} · 기관총 모델 {(mount ? "남음" : "없음")}");
                if (lost > 0f) Fail("함교만 있는데 가까운 적이 피해를 입음(삭제한 함교 기관총이 남아 있음)");
                if (mount) Fail("함교 위에 기관총 모델이 남아 있음");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- 2) 손상통제
            {
                _report.AppendLine($"- 손상통제반 0개: 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회 · 자동 회복 상한 {bridge.HullRepairCap * 100f:0}% · 속도 {bridge.HullRepairRate:0.##}/초");
                if (bridge.MaxCharges != 1 || bridge.Charges != 1 || !Mathf.Approximately(bridge.HullRepairCap, 0.5f)) Fail("블록 없을 때 응급 수리 1회·상한 50%가 아님");
                // 검사 함선은 선체가 매우 커(100000) 프레임당 회복량이 실수 정밀도에 묻힌다 — 잠깐 실제 크기(100)로 바꿔 잰다.
                // 상한 바로 아래(−3)에서 시작해 상한에 닿고 멈추는지, 상한 위(60%)에서는 그대로인지
                var maxProp = typeof(ShipController).GetProperty("HullMaxHp");
                var hpProp = typeof(ShipController).GetProperty("HullHp");
                float savedMax = ship.HullMaxHp;
                maxProp?.SetValue(ship, 100f);
                hpProp?.SetValue(ship, 100f);
                ship.RepairHull(ship.HullMaxHp);
                ship.ApplyHullDamage(ship.HullMaxHp * 0.5f + 3f);
                float start = ship.HullHp;
                yield return new WaitForSeconds(12f);
                float capHp = ship.HullMaxHp * 0.5f;
                float healed = ship.HullHp - start;
                ship.RepairHull(ship.HullMaxHp);
                ship.ApplyHullDamage(ship.HullMaxHp * 0.4f);   // 60%로
                float high0 = ship.HullHp;
                yield return new WaitForSeconds(4f);
                float highGain = ship.HullHp - high0;
                _report.AppendLine($"- 자동 회복: 상한 3 아래에서 12초 → +{healed:0.##}(상한까지 {capHp - start:0.##}, 최종 {start + healed:0.##}/{capHp:0.##}) · 60%에서 4초 → +{highGain:0.##}");
                if (healed < 2.9f || start + healed > capHp + 0.01f || highGain > 0.01f) Fail("자동 회복이 50%에서 멈추지 않음");
                maxProp?.SetValue(ship, savedMax);
                hpProp?.SetValue(ship, savedMax);

                var bay = CombatDevTools.FindModule("mod_repairbay");
                var factory = Object.FindFirstObjectByType<ModuleFactory>();
                ModuleInstance bayInst = null;
                for (int x = -4; x <= 4 && bayInst == null; x++)
                    for (int z = -2; z <= 2 && bayInst == null; z++)
                        if (ship.Grid.CanPlace(bay, new GridCoord(x, z), 0, out _)) bayInst = factory.Install(bay, new GridCoord(x, z), 0);
                yield return new WaitForSeconds(0.3f);
                _report.AppendLine($"- 손상통제반 1개: 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회 · 상한 {bridge.HullRepairCap * 100f:0}% · 속도 {bridge.HullRepairRate:0.##}/초");
                if (bayInst == null || bridge.MaxCharges != 2 || bridge.Charges != 2 || Mathf.Abs(bridge.HullRepairCap - 0.6667f) > 0.01f) Fail("손상통제반 1개일 때 2회·상한 67%가 아님");
                ship.ApplyHullDamage(200f);   // 응급 수리가 고칠 자리
                float before = ship.HullHp;
                GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);
                yield return new WaitForSeconds(0.2f);
                _report.AppendLine($"- 응급 수리 [R]: 선체 {before:0} → {ship.HullHp:0} · 남은 {bridge.Charges}/{bridge.MaxCharges}회");
                if (bridge.Charges != 1 || ship.HullHp <= before) Fail("응급 수리가 1회 줄며 선체를 고치지 않음");
                // 쿨타임 충전(2026-10-21): 모자라면 정해진 시간마다 1회씩 다시 찬다 — 기다리는 시간만 줄여 확인
                {
                    float full = bridge.ChargeRechargeSeconds, left0 = bridge.ChargeRechargeLeft;
                    int c0 = bridge.Charges;
                    typeof(BridgeModule).GetField("_burstGapLeft", Inst)?.SetValue(bridge, 0f);   // 연속 사용 간격(4초)만 건너뛴다
                    GameEvents.RaiseSkillRequested(ActiveSkillId.Repair);   // 남은 1회도 쓴다 → 0회, 진행도 표시
                    yield return new WaitForSeconds(0.2f);
                    int empty = bridge.Charges;
                    float shown = bridge.BurstReadiness01;
                    typeof(BridgeModule).GetField("_rechargeLeft", Inst)?.SetValue(bridge, 0.4f);
                    yield return new WaitForSeconds(1.0f);
                    int after = bridge.Charges;
                    _report.AppendLine($"- 응급 수리 쿨타임: 1회 {full:0}초 · 사용 직후 남은 시간 {left0:0.#}초 · 다 쓴 뒤 {empty}회(표시 진행도 {shown:0.00}) → 충전 시간이 지나자 {after}/{bridge.MaxCharges}회");
                    if (full <= 0f || left0 <= 0f || left0 > full + 0.01f) Fail("응급 수리를 쓴 뒤 충전 시간이 돌지 않음");
                    if (empty != c0 - 1 || after != empty + 1) Fail("응급 수리가 충전 시간이 지나도 1회 다시 차지 않음");
                }
                GameEvents.RaiseBossDefeated();
                yield return null;
                _report.AppendLine($"- 보스 격침 뒤 응급 수리 {bridge.Charges}/{bridge.MaxCharges}회");
                if (bridge.Charges != bridge.MaxCharges) Fail("보스 격침 뒤 응급 수리가 가득 차지 않음");
                if (bayInst != null) CombatDevTools.RemoveModule(bayInst);
                ship.RepairHull(ship.HullMaxHp);
                yield return new WaitForSeconds(0.3f);
            }

            var skill = ship.GetComponent<SignatureSkill>();
            if (skill == null) { Fail("고유 스킬 컴포넌트 없음"); yield break; }

            // ---- 3a) 광역 탐색(범용 초계함)
            {
                skill.ForceKind(StartShipKind.Patrol);
                var boats = new List<EnemyController>();
                foreach (float a in new[] { -40f, 0f, 40f }) boats.Add(Frozen(boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 45f));
                CombatDevTools.SpawnSubmarines(1, 50f);
                yield return new WaitForSeconds(0.5f);
                foreach (var e in Object.FindObjectsByType<SubmarineBase>(FindObjectsSortMode.None)) if (e.IsAlive) { e.DevFrozen = true; }
                bool used = skill.TryActivate();
                yield return new WaitForSeconds(0.6f);
                var sweep = skill.Ability as RadarSweepAbility;
                var b0 = boats[0];
                float hp = b0 != null ? b0.CurrentHp : 0f;
                b0?.TakeDamage(new DamageInfo(4f, b0.transform.position, Vector3.down, DamageSource.Gun));
                float took = b0 != null ? hp - b0.CurrentHp : 0f;
                _report.AppendLine($"- 광역 탐색: 사용 {Yes(used)} · 표적 지시 {sweep?.LastMarked} · 잠수함 노출 {sweep?.LastRevealedSubs} · 지시된 고속정에 피해 4 → {took:0.##}");
                if (!used || sweep == null || sweep.LastMarked < 3 || sweep.LastRevealedSubs < 1 || Mathf.Abs(took - 5f) > 0.01f) Fail("광역 탐색이 표적 지시·잠수함 노출·피해 +25%를 하지 않음");
                yield return ScreenShot("signature_sweep");
                yield return new WaitForSeconds(0.8f);
                yield return ScreenShot("signature_sweep_markers");
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(0.5f);
            }

            // ---- 3b) 돌격 기동(고속 강습함)
            {
                skill.ForceKind(StartShipKind.Assault);
                ship.SetEngineOrder(1f);
                yield return new WaitForSeconds(2f);
                var target = Frozen(boatDef, ship.transform.position + Fwd() * 30f);
                float v0 = ship.CurrentSpeed;
                bool used = skill.TryActivate();
                var run = skill.Ability as AssaultRunAbility;
                yield return new WaitForSeconds(0.5f);
                yield return ScreenShot("signature_assault");
                float t0 = Time.time;
                while (Time.time - t0 < 6f && run != null && run.Rams == 0)
                {
                    if (target != null && target.IsAlive) { var p = ship.transform.position + Fwd() * 6f; p.y = target.transform.position.y; target.transform.position = Vector3.MoveTowards(target.transform.position, p, 10f * Time.deltaTime); }
                    yield return null;
                }
                float scale = ship.IncomingDamageScale;
                _report.AppendLine($"- 돌격 기동: 사용 {Yes(used)} · 전속 {Yes(ship.IsFlanking)} · 받는 피해 ×{scale:0.##} · 속력 {v0:0.0} → {ship.CurrentSpeed:0.0} · 충돌 {run?.Rams}회");
                if (!used || !ship.IsFlanking || Mathf.Abs(scale - 0.6f) > 0.01f || run == null || run.Rams < 1) Fail("돌격 기동이 속력·피해 감소·충돌 공격을 하지 않음");
                yield return CloseShot("signature_assault_close", ship.transform, 12f);
                yield return new WaitForSeconds(AssaultRunAbility.Duration);
                _report.AppendLine($"- 돌격 기동 끝: 받는 피해 ×{ship.IncomingDamageScale:0.##}");
                if (!Mathf.Approximately(ship.IncomingDamageScale, 1f)) Fail("돌격 기동이 끝나도 피해 감소가 남음");
                ship.SetEngineOrder(0f);
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2f);
            }

            // ---- 3c) 전탄 발사(미사일 구축함)
            {
                foreach (var b in off) if (b != null) b.enabled = true;
                ResetLoadout(M("mod_radar"), M("mod_vls"));
                yield return new WaitForSeconds(0.5f);
                VlsModule vls = null;
                foreach (var m in ship.Grid.Modules) if (m?.Runtime is VlsModule v) vls = v;
                off = DisableWeapons(r => r is BridgeModule);
                if (vls == null) { Fail("VLS를 설치하지 못함"); }
                else
                {
                    vls.Ammo.Refill();
                    skill.ForceKind(StartShipKind.MissileDestroyer);
                    var boats = new List<EnemyController>();
                    foreach (float a in new[] { -50f, -15f, 15f, 50f }) boats.Add(Frozen(CombatDevTools.FindEnemy("ene_pcc_corvette") ?? boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 34f));
                    yield return new WaitForSeconds(0.5f);
                    vls.enabled = true;
                    int l0 = vls.LaunchCount, cap = vls.Ammo.Current;
                    bool used = skill.TryActivate();
                    yield return new WaitForSeconds(0.55f);
                    yield return CloseShot("signature_salvo_hatches", vls.transform, 7f);
                    yield return new WaitForSeconds(1.8f);
                    var salvo = skill.Ability as FullSalvoAbility;
                    _report.AppendLine($"- 전탄 발사: 사용 {Yes(used)} · 예약 {salvo?.LastScheduled}발(장전 {cap}셀) · 실제 발사 {vls.LaunchCount - l0} · 이후 재장전 {Yes(vls.Ammo.IsReloading)}");
                    if (!used || salvo == null || salvo.LastScheduled != cap || vls.LaunchCount - l0 != cap || !vls.Ammo.IsReloading) Fail("전탄 발사가 남은 셀을 모두 쏘고 재장전에 들어가지 않음");
                    yield return ScreenShot("signature_salvo");
                    yield return new WaitForSeconds(3f);
                    // 장전 중에는 못 쓴다
                    skill.ForceKind(StartShipKind.MissileDestroyer);
                    bool again = skill.TryActivate();
                    _report.AppendLine($"- 재장전 중 다시 사용: {Yes(again)}(아니오여야 함)");
                    if (again) Fail("재장전 중에 전탄 발사가 됨");
                }
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(1f);
            }

            // ---- 3d) 항공대 호출(지휘함)
            {
                skill.ForceKind(StartShipKind.Command);
                var boats = new List<EnemyController>();
                foreach (float a in new[] { -30f, 30f }) boats.Add(Frozen(boatDef, ship.transform.position + Quaternion.Euler(0f, a, 0f) * Fwd() * 40f));
                var hp0 = new List<float>(); foreach (var b in boats) hp0.Add(b != null ? b.CurrentHp : 0f);
                bool used = skill.TryActivate();
                var wing = skill.Ability as AirWingAbility;
                yield return new WaitForSeconds(2.5f);
                yield return ScreenShot("signature_airwing_ingress");
                // 크기(2026-10-21): 전투기 길이 약 3.4(슈퍼호넷 모델) · 조기경보기 길이 약 3.4 — 시작 함선(약 15)의 1/4 안팎.
                // 이 검사의 함선은 블록을 덜어 낸 작은 배라 함선 길이는 참고로만 적는다
                {
                    static Vector3 Size(Component a)
                    {
                        var model = a.transform.GetChild(0);
                        var keep = model.localRotation; model.localRotation = Quaternion.identity;
                        var inv = a.transform.rotation; a.transform.rotation = Quaternion.identity;
                        var b = new Bounds(); bool any = false;
                        foreach (var r in a.GetComponentsInChildren<MeshRenderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                        a.transform.rotation = inv; model.localRotation = keep;
                        return b.size;
                    }
                    float shipLen = 0f;
                    { var b = new Bounds(); bool any = false; foreach (var r in ship.GetComponentsInChildren<MeshRenderer>()) { if (r.bounds.size.magnitude > 40f) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); } shipLen = Mathf.Max(b.size.x, b.size.z); }
                    Vector3 fighter = Vector3.zero, aewSize = Vector3.zero; bool hornet = false, hawkeye = false;
                    if (wing != null)
                        foreach (var a in wing.Flight)
                        {
                            if (a == null) continue;
                            if (a.IsAew) { aewSize = Size(a); hawkeye = FindChild(a.transform, "AIR_Hawkeye(Clone)") != null; }
                            else { fighter = Size(a); hornet |= FindChild(a.transform, "AIR_SuperHornet(Clone)") != null; }
                        }
                    // 로토돔: 기체 위축으로 돌아야 한다(돔 윗면이 기울지 않음)
                    float domeTilt = -1f, domeTurn = 0f;
                    if (wing != null)
                        foreach (var a in wing.Flight)
                        {
                            if (a == null || !a.IsAew) continue;
                            var dome = FindChild(a.transform, "Rotodome");
                            var model = a.transform.GetChild(0);
                            if (dome == null) break;
                            var normal = dome.GetComponent<Renderer>().localBounds.extents;   // 돔은 납작한 원판 — 가장 얇은 자기 축이 돔의 위
                            Vector3 localUp = normal.x < normal.y && normal.x < normal.z ? Vector3.right : normal.y < normal.z ? Vector3.up : Vector3.forward;
                            Vector3 up0 = dome.TransformDirection(localUp), fwd0 = dome.TransformDirection(localUp == Vector3.forward ? Vector3.up : Vector3.forward);
                            Vector3 flat0 = Vector3.ProjectOnPlane(fwd0, model.up);
                            float t0 = Time.time;
                            yield return new WaitForSeconds(0.5f);
                            if (dome == null || model == null) break;
                            domeTilt = Vector3.Angle(dome.TransformDirection(localUp), model.up);
                            domeTilt = Mathf.Min(domeTilt, 180f - domeTilt);
                            domeTurn = Vector3.Angle(flat0, Vector3.ProjectOnPlane(dome.TransformDirection(localUp == Vector3.forward ? Vector3.up : Vector3.forward), model.up));
                            break;
                        }
                    _report.AppendLine($"- 항공대 크기: 함선 길이 {shipLen:0.0} · 전투기(슈퍼호넷 {Yes(hornet)}) 길이 {fighter.z:0.0} 날개폭 {fighter.x:0.0} · " +
                                       $"조기경보기(호크아이 {Yes(hawkeye)}) 길이 {aewSize.z:0.0} 날개폭 {aewSize.x:0.0} · 로토돔 기울기 {domeTilt:0.0}° · 0.5초 회전 {domeTurn:0}°");
                    if (!hornet) Fail("항공대 전투기가 슈퍼호넷 모델이 아님");
                    if (!hawkeye) Fail("항공대 조기경보기가 호크아이 모델이 아님");
                    if (domeTilt < 0f || domeTilt > 3f || domeTurn < 10f) Fail("호크아이 로토돔이 기체 위축으로 돌지 않음");
                    if (fighter.z < 2.8f || fighter.z > 4.2f || aewSize.z < 2.8f || aewSize.z > 4.2f) Fail("항공대 기체 길이가 3.4 안팎이 아님(함선에 비해 너무 크거나 작음)");
                    FriendlyAircraft lead = null;
                    if (wing != null) foreach (var a in wing.Flight) if (a != null && !a.IsAew) { lead = a; break; }
                    if (lead != null) yield return CloseShot("signature_airwing_hornet", lead.transform, 6f);
                }
                yield return new WaitForSeconds(6f);
                yield return ScreenShot("signature_airwing_attack");
                yield return new WaitForSeconds(6f);
                int fighters = 0, aew = 0, shots = 0, pings = 0;
                if (wing != null)
                    foreach (var a in wing.Flight)
                    {
                        if (a == null) continue;
                        if (a.IsAew) { aew++; pings += a.RadarPings; } else { fighters++; shots += a.ShotsFired; }
                    }
                float lost = 0f; for (int i = 0; i < boats.Count; i++) lost += boats[i] != null ? hp0[i] - (boats[i].IsAlive ? boats[i].CurrentHp : 0f) : hp0[i];
                _report.AppendLine($"- 항공대 호출: 사용 {Yes(used)} · 전투기 {fighters} · 조기경보기 {aew} · 기총 {shots}발 · 적 피해 {lost:0} · 레이더 고리 {pings}회");
                if (!used || fighters != 3 || aew != 1 || shots < 10 || lost <= 0f || pings < 2) Fail("항공대 호출이 편대로 와서 공격·조기경보를 하지 않음");
                yield return new WaitForSeconds(AirWingAbility.Duration - 14.5f + 8f);
                int left = 0; if (wing != null) foreach (var a in wing.Flight) if (a != null) left++;
                _report.AppendLine($"- 항공대 이탈 뒤 남은 항공기 {left}");
                if (left > 0) Fail("항공대가 시간이 지나도 이탈하지 않음");
                CombatDevTools.ClearBattlefield();
            }

            foreach (var b in off) if (b != null) b.enabled = true;
            if (field != null) Game.World.IslandField.Disabled = false;
            ship.DevRudderOverride = null;
        }
    }
}
#endif
