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
    /// <summary>
    /// 자동 전투 검증(개발용). 전투 씬에서 시나리오를 차례로 돌려 무기별 발사·명중·요격, 적 미사일 화면 체류 시간,
    /// 교전 규칙(탐지 밖·최소거리 안)을 기록하고 보고서와 스크린샷을 남긴다.
    /// 에디터 메뉴 Naval/Dev/Run Combat Verification 또는 배치 모드(-executeMethod)로 실행한다.
    /// 진행 중에는 구간 진행·레벨업을 멈추고 선체를 크게 늘려 끝까지 관찰한다.
    /// 이 파일은 진행 순서(Start)·기본 시나리오·공용 도구. 분야별 검사는 CombatVerificationRunner.*.cs(무장·명세·적·편대·성장·환경·UI)로 나눴다(2026-10-06).
    /// </summary>
    public partial class CombatVerificationRunner : MonoBehaviour
    {
        public static bool Finished { get; private set; }
        public static bool Succeeded { get; private set; }

        public string OutputDirectory;
        public float TimeScale = 2f;
        [Tooltip("플레이어 빌드에서 끝나면 종료(통과 0, 규칙 실패 2)")]
        public bool QuitWhenDone;
        [Tooltip("레이더 화면 검사만 돌린다(-radarOnly)")]
        public bool RadarOnly;
        [Tooltip("탄약 검사만 돌린다(-ammoOnly)")]
        public bool AmmoOnly;
        [Tooltip("방어·표적 분류 검사만 돌린다(-defenseOnly)")]
        public bool DefenseOnly;
        [Tooltip("탄약고·웨이브 검사만 돌린다(-logisticsOnly)")]
        public bool LogisticsOnly;
        [Tooltip("명세 시험 A~G만 돌린다(-specOnly). 오래 걸려 기본 실행에는 넣지 않는다.")]
        public bool SpecOnly;
        [Tooltip("Tab 현황판 레이아웃 검사만 돌린다(-tabOnly)")]
        public bool TabOnly;
        [Tooltip("CIWS 점사 측정만 돌린다(-burstOnly)")]
        public bool BurstOnly;
        [Tooltip("스테이지 2 추가 적 검사만 돌린다(-enemyOnly)")]
        public bool EnemyOnly;
        [Tooltip("조함 측정만 돌린다(-helmOnly)")]
        public bool HelmOnly;
        [Tooltip("하단 HUD 배치 검사만 돌린다(-hudOnly)")]
        public bool HudOnly;
        [Tooltip("바다·물살 검사만 돌린다(-waterOnly)")]
        public bool WaterOnly;
        [Tooltip("어뢰 회피 검사만 돌린다(-torpedoOnly)")]
        public bool TorpedoOnly;
        [Tooltip("섬(엄폐) 검사만 돌린다(-islandOnly)")]
        public bool IslandOnly;
        [Tooltip("자폭 보트 검사만 돌린다(-suicideOnly)")]
        public bool SuicideOnly;
        [Tooltip("장식(격침·물보라·연기·구름) 검사만 돌린다(-decorOnly)")]
        public bool DecorOnly;
        [Tooltip("바다 생물·섬 장식·폭발 검사만 돌린다(-lifeOnly)")]
        public bool LifeOnly;
        [Tooltip("블록 강화 검사만 돌린다(-upgradeOnly)")]
        public bool UpgradeOnly;
        [Tooltip("UI 전체 화면 캡처만 돌린다(-uiShots)")]
        public bool UiShots;
        [Tooltip("진영(아군/적) 구분 검사만 돌린다(-factionOnly)")]
        public bool FactionOnly;
        [Tooltip("호위함 생존성·적 표적 선택 검사만 돌린다(-escortOnly)")]
        public bool EscortOnly;
        [Tooltip("미사일 위협·선체 위험 경고 검사만 돌린다(-threatOnly)")]
        public bool ThreatOnly;
        [Tooltip("성장 카드·카드 추첨 검사만 돌린다(-growthOnly)")]
        public bool GrowthOnly;
        [Tooltip("Codex 섬·해안 소품 검사만 돌린다(-envOnly)")]
        public bool EnvOnly;
        [Tooltip("위치별 소나·폭뢰 형태 검사만 돌린다(-variantOnly)")]
        public bool VariantOnly;
        [Tooltip("경어뢰 발사관 검사만 돌린다(-tubeOnly)")]
        public bool TubeOnly;
        [Tooltip("곡사포 검사만 돌린다(-howitzerOnly)")]
        public bool HowitzerOnly;
        [Tooltip("충각 함수·기뢰 투하궤 검사만 돌린다(-ramMineOnly)")]
        public bool RamMineOnly;
        [Tooltip("노봉 40mm 검사만 돌린다(-nobongOnly)")]
        public bool NobongOnly;
        [Tooltip("편대 진형·조함 검사만 돌린다(-formationOnly)")]
        public bool FormationOnly;
        [Tooltip("메인 화면 사전·무장 팩 v8 외형 검사만 돌린다(-codexOnly)")]
        public bool CodexOnly;
        [Tooltip("엘리트 초계함 주변 대형 물체 추적만 돌린다(-pccSoak)")]
        public bool PccSoak;
        [Tooltip("우클릭 항로(자동 조함) 검사만 돌린다(-routeOnly)")]
        public bool RouteOnly;
        [Tooltip("VLS 8셀 덮개 열림 검사만 돌린다(-vlsOnly)")]
        public bool VlsOnly;

        private readonly StringBuilder _report = new();
        private int _failures;

        private IEnumerator Start()
        {
            Finished = false;
            Directory.CreateDirectory(OutputDirectory);
            _report.AppendLine("# 전투 검증 보고서");
            _report.AppendLine($"- 실행: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}, 시간 배속 {TimeScale}");

            yield return new WaitForSecondsRealtime(1.5f);   // StageDirector.Start·ShipInitializer 대기

            PrepareSandbox();
            // 배치 모드 플레이어는 창 해상도가 제각각이라 측정·스크린샷은 16:9 기준으로 고정한다
            if (Camera.main != null) Camera.main.aspect = 16f / 9f;
            _report.AppendLine($"- 화면 {Screen.width}×{Screen.height}, 측정은 16:9로 고정");
            StartCoroutine(KeepModulesHealthy());
            Time.timeScale = TimeScale;
            CombatDevTools.Instance?.SetOverlay(true);
            yield return new WaitForSeconds(2f);             // 카메라가 자리 잡을 시간

            if (SpecOnly)
            {
                yield return SpecTests();
                Finish();
                yield break;
            }
            if (GrowthOnly)
            {
                yield return GrowthCheck();
                SaveLog("growth");
                Finish();
                yield break;
            }
            if (EnvOnly)
            {
                yield return EnvArtCheck();
                SaveLog("env");
                Finish();
                yield break;
            }
            if (FormationOnly)
            {
                yield return FormationCheck();
                SaveLog("formation");
                Finish();
                yield break;
            }
            if (VlsOnly)
            {
                yield return VlsHatchCheck();
                SaveLog("vls");
                Finish();
                yield break;
            }
            if (RouteOnly)
            {
                yield return RouteCheck();
                SaveLog("route");
                Finish();
                yield break;
            }
            if (PccSoak)
            {
                yield return PccSoakCheck();
                SaveLog("pcc_soak");
                Finish();
                yield break;
            }
            if (CodexOnly)
            {
                yield return CodexCheck();
                SaveLog("codex");
                Finish();
                yield break;
            }
            if (VariantOnly)
            {
                yield return VariantCheck();
                SaveLog("variant");
                Finish();
                yield break;
            }
            if (NobongOnly)
            {
                yield return NobongCheck();
                SaveLog("nobong");
                Finish();
                yield break;
            }
            if (RamMineOnly)
            {
                yield return RamMineCheck();
                SaveLog("rammine");
                Finish();
                yield break;
            }
            if (HowitzerOnly)
            {
                yield return HowitzerCheck();
                SaveLog("howitzer");
                Finish();
                yield break;
            }
            if (TubeOnly)
            {
                yield return TorpedoTubeCheck();
                SaveLog("tube");
                Finish();
                yield break;
            }
            if (ThreatOnly)
            {
                yield return ThreatCheck();
                SaveLog("threat");
                Finish();
                yield break;
            }
            if (EscortOnly)
            {
                yield return EscortCheck();
                SaveLog("escort");
                Finish();
                yield break;
            }
            if (FactionOnly)
            {
                yield return FactionCheck();
                SaveLog("faction");
                Finish();
                yield break;
            }
            if (UiShots)
            {
                yield return UiShotsCheck();
                Finish();
                yield break;
            }
            if (UpgradeOnly)
            {
                yield return UpgradeCheck();
                SaveLog("upgrade");
                Finish();
                yield break;
            }
            if (LifeOnly)
            {
                yield return LifeCheck();
                SaveLog("life");
                Finish();
                yield break;
            }
            if (DecorOnly)
            {
                yield return DecorCheck();
                SaveLog("decor");
                Finish();
                yield break;
            }
            if (SuicideOnly)
            {
                yield return SuicideBoatCheck();
                SaveLog("suicide");
                Finish();
                yield break;
            }
            if (IslandOnly)
            {
                yield return IslandCheck();
                SaveLog("islands");
                Finish();
                yield break;
            }
            if (TorpedoOnly)
            {
                yield return TorpedoDodgeCheck();
                SaveLog("torpedo");
                Finish();
                yield break;
            }
            if (WaterOnly)
            {
                yield return WaterCheck();
                Finish();
                yield break;
            }
            if (HudOnly)
            {
                yield return HudLayoutCheck();
                Finish();
                yield break;
            }
            if (HelmOnly)
            {
                yield return HelmCheck();
                Finish();
                yield break;
            }
            if (EnemyOnly)
            {
                yield return NewEnemyCheck();
                SaveLog("new_enemies");
                Finish();
                yield break;
            }
            if (BurstOnly)
            {
                yield return BurstCheck();
                var r0 = new List<(string, int, int)>();
                yield return CiwsOnly("CIWS · 2발", 2, 20f, r0);
                yield return CiwsOnly("CIWS · 6발 포화", 6, 60f, r0);
                SaveLog("burst");
                Finish();
                yield break;
            }
            if (TabOnly)
            {
                yield return TabLayoutCheck();
                Finish();
                yield break;
            }
            if (LogisticsOnly)
            {
                yield return LogisticsCheck();
                Finish();
                yield break;
            }
            if (DefenseOnly)
            {
                yield return DefenseCheck();
                CombatDevTools.InstallTestLoadout();
                yield return new WaitForSeconds(1f);
                yield return RuleChecks();
                Finish();
                yield break;
            }
            if (AmmoOnly)
            {
                yield return AmmoCheck();
                Finish();
                yield break;
            }
            if (RadarOnly)
            {
                yield return RadarCheck();
                Finish();
                yield break;
            }

            yield return ViewCheck();

            yield return Scenario("고속정 무리 8척 — 시작 무장", 45f, "fastpack_default",
                () => CombatDevTools.SpawnRing("ene_fastboat", 8, 44f));

            int installed = CombatDevTools.InstallTestLoadout();
            _report.AppendLine($"\n시험 무장 장착: {installed}개 (76mm·유도로켓·SAM·VLS·레이더·폭뢰·기관포)");
            yield return new WaitForSeconds(1f);
            yield return Shot("ship_loadout");

            yield return Scenario("고속정 무리 8척 — 시험 무장", 45f, "fastpack_loadout",
                () => CombatDevTools.SpawnRing("ene_fastboat", 8, 44f));

            yield return Scenario("미사일정 3척", 50f, "missileboats",
                () => CombatDevTools.SpawnRing("ene_missileboat", 3, 50f));

            yield return MissileTimingWithoutDefense();

            yield return Scenario("적 미사일 동시 접근 6발 × 3회 — 방어 작동", 40f, "missiles_defended",
                () => CombatDevTools.MissileVolley(6, 52f, damageOverride: 0.5f), repeatEvery: 12f, repeats: 3);

            yield return Scenario("잠수함 2척(드러냄)", 40f, "submarines",
                () => CombatDevTools.SpawnSubmarines(2, 18f));

            yield return Scenario("항공: 자폭 드론 4 + 전투기 2", 35f, "air",
                () => { CombatDevTools.SpawnRing("ene_drone", 4, 46f); CombatDevTools.SpawnRing("ene_fighter", 2, 50f); });

            yield return RuleChecks();
            yield return RadarCheck();
            yield return AmmoCheck();
            yield return DefenseCheck();
            yield return LogisticsCheck();
            yield return TabLayoutCheck();
            yield return NewEnemyCheck();
            yield return WaterCheck();
            yield return TorpedoDodgeCheck();
            yield return IslandCheck();
            yield return SuicideBoatCheck();
            yield return DecorCheck();
            yield return LifeCheck();
            yield return UpgradeCheck();
            yield return FactionCheck();
            yield return EscortCheck();
            yield return ThreatCheck();
            yield return GrowthCheck();
            yield return EnvArtCheck();
            yield return VariantCheck();
            yield return TorpedoTubeCheck();
            yield return HowitzerCheck();
            yield return RamMineCheck();
            yield return NobongCheck();
            yield return FormationCheck();
            yield return CodexCheck();
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            _report.AppendLine(_failures == 0 ? "\n## 결과: 규칙 검사 모두 통과" : $"\n## 결과: 규칙 검사 실패 {_failures}건");
            File.WriteAllText(Path.Combine(OutputDirectory, "report.md"), _report.ToString(), new UTF8Encoding(false));
            Debug.Log($"[CombatVerification] 완료 → {OutputDirectory}\n{_report}");

            Succeeded = _failures == 0;
            Finished = true;
            if (QuitWhenDone && !Application.isEditor) Application.Quit(Succeeded ? 0 : 2);
        }

        // ------------------------------------------------------------ 준비

        private static void PrepareSandbox()
        {
            // 시작 화면이 있으면 출항 버튼을 눌러 전투 상태로 들어간다(스포너가 함선을 알게 된다)
            foreach (var button in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if ((button.name == "Launch" || button.name.StartsWith("출항")) && button.interactable) { button.onClick.Invoke(); break; }

            // 출항 뒤에는 전투단 편성 화면(FORCE PACKAGE)이 열린다 — 기본 편성 그대로 확정해야 전투가 시작된다
            var overlay = GameObject.Find("Force package overlay");
            if (overlay != null)
                foreach (var button in overlay.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if (button.name == "Confirm" && button.interactable) { button.onClick.Invoke(); break; }
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                Debug.LogWarning($"[CombatVerification] 출항 절차 뒤에도 전투 상태가 아님: {GameManager.Instance.State}");

            foreach (var d in Object.FindObjectsByType<StageDirector>(FindObjectsSortMode.None)) d.enabled = false;
            foreach (var x in Object.FindObjectsByType<ExperienceSystem>(FindObjectsSortMode.None)) x.enabled = false;
            // 호위함 자체 무장은 끈다: 기함 단독 검사(자폭 보트 충돌·정지 물보라 등)에 끼어들지 않게. 호위함 검사가 필요할 때 켠다.
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            if (EnemySpawner.Instance != null) EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();

            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (ship != null)
            {
                var prop = typeof(ShipController).GetProperty(nameof(ShipController.HullMaxHp));
                prop?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
                prop = typeof(ShipController).GetProperty(nameof(ShipController.HullHp));
                prop?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            }
        }

        /// <summary>검증 중 모듈이 파괴되어 다음 시나리오에 영향을 주지 않도록 계속 채운다.</summary>
        private static IEnumerator KeepModulesHealthy()
        {
            while (true)
            {
                var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
                if (ship != null && ship.Grid != null)
                    foreach (var m in ship.Grid.Modules)
                        if (m != null && !m.IsDestroyed) m.Repair(m.MaxHp);
                yield return new WaitForSecondsRealtime(0.25f);
            }
        }

        // ------------------------------------------------------------ 시야

        private IEnumerator ViewCheck()
        {
            var cam = Camera.main;
            var player = CombatDevTools.PlayerTransform();
            if (cam == null || player == null) yield break;

            var e = CombatDevTools.MeasureVisibleSeaDistance(cam, player.position);
            _report.AppendLine("\n## 1. 카메라 시야");
            _report.AppendLine($"- 시야각 {cam.fieldOfView:0}°, 원근={!cam.orthographic}, 화면 비 {cam.aspect:0.00}, 카메라-함선 거리 {Vector3.Distance(cam.transform.position, player.position):0.0}");
            _report.AppendLine($"- 함선에서 화면 가장자리까지 수면 거리: **최소 {e.min:0.0}** (위 {e.up:0.0} / 아래 {e.down:0.0} / 좌우 {e.side:0.0})");

            // 22·34·48 거리에 멈춘 고속정을 세워 두고 한 장 찍는다
            var markers = new List<EnemyController>();
            float[] angles = { 90f, 0f, 270f };
            for (int i = 0; i < CombatDevTools.RingDistances.Length; i++)
            {
                var spawned = CombatDevTools.SpawnRing("ene_fastboat", 1, CombatDevTools.RingDistances[i], angles[i]);
                foreach (var s in spawned) { s.DevFrozen = true; markers.Add(s); }
            }
            yield return new WaitForSeconds(0.2f);

            foreach (var m in markers)
            {
                float d = Vector3.Distance(m.transform.position, player.position);
                bool on = CombatStats.IsOnScreen(m.transform.position);
                _report.AppendLine($"- {d:0}m 표적 화면 안: {(on ? "예" : "아니오")}");
                if (d <= 44f && !on) Fail($"{d:0}m 표적이 기본 화면 밖에 있음");
            }
            yield return Shot("view_rings");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
        }

        // ------------------------------------------------------------ 시나리오

        private IEnumerator Scenario(string title, float seconds, string shotName, System.Action spawn,
                                     float repeatEvery = 0f, int repeats = 1)
        {
            CombatDevTools.ClearBattlefield();
            CombatStats.Reset();
            yield return new WaitForSeconds(0.5f);

            float start = Time.time;
            spawn();
            int spawned = 1;
            bool shot = false;

            while (Time.time - start < seconds)
            {
                if (repeatEvery > 0f && spawned < repeats && Time.time - start >= repeatEvery * spawned)
                {
                    spawn();
                    spawned++;
                }
                if (!shot && Time.time - start >= Mathf.Min(10f, seconds * 0.3f))
                {
                    shot = true;
                    yield return Shot(shotName);
                }
                yield return null;
            }

            int alive = CountAliveEnemies();
            _report.AppendLine($"\n## {title}");
            _report.AppendLine($"- {seconds:0}초 경과 후 남은 적: {alive}");
            AppendStats();
        }

        /// <summary>방어 무기를 끄고 미사일만 날려, 화면 진입부터 피격까지 걸리는 순수 시간을 잰다.</summary>
        private IEnumerator MissileTimingWithoutDefense()
        {
            CombatDevTools.ClearBattlefield();
            CombatStats.Reset();
            var weapons = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled) { r.enabled = false; weapons.Add(r); }

            yield return new WaitForSeconds(0.5f);
            CombatDevTools.MissileVolley(6, 52f, damageOverride: 0.5f);
            float start = Time.time;
            bool shot = false;
            while (Time.time - start < 16f && TargetRegistry.Get(TargetKind.Missile).Count > 0)
            {
                if (!shot && Time.time - start > 2.5f) { shot = true; yield return Shot("missiles_undefended"); }
                yield return null;
            }

            foreach (var w in weapons) if (w != null) w.enabled = true;

            _report.AppendLine("\n## 적 미사일 6발 — 방어 끔(순수 비행 시간)");
            AppendStats();
            if (CombatStats.Threats.TryGetValue(ThreatOutcome.HitShip, out var hit) && hit.Seen > 0)
            {
                float avg = hit.SumSeconds / hit.Seen;
                _report.AppendLine($"- 화면 진입 → 피격 평균 {avg:0.0}초 (목표 4~6초)");
                if (avg < 3.5f || avg > 7f) Fail($"미사일 화면 진입 후 피격까지 {avg:0.0}초 — 목표 4~6초에서 벗어남");
            }
            else Fail("방어를 끈 상태에서 미사일 피격 기록이 없음");
        }

        /// <summary>탐지 밖·최소거리 안 규칙: 멈춰 둔 표적에 어떤 무기가 쏘는지 본다.</summary>
        private IEnumerator RuleChecks()
        {
            _report.AppendLine("\n## 교전 규칙 검사 (멈춘 표적)");

            yield return Frozen("ene_missileboat", 44f, 8f, "44m(VLS 16~48 안, 76mm 34·로켓 38 밖)",
                expectFire: new[] { "VLS" }, expectSilent: new[] { "76mm", "유도로켓", "기관포" });

            yield return Frozen("ene_fastboat", 12f, 6f, "12m(VLS 최소 16 안쪽)",
                expectFire: new[] { "기관포" }, expectSilent: new[] { "VLS" });

            yield return Frozen("ene_fastboat", 30f, 6f, "30m 고속정(가치 1, VLS 대역 안)",
                expectFire: new string[0], expectSilent: new[] { "VLS" });

            yield return Frozen("ene_fastboat", 60f, 6f, "60m(전용 레이더 52 밖)",
                expectFire: new string[0], expectSilent: new[] { "VLS", "76mm", "유도로켓", "기관포" });
        }

        private IEnumerator Frozen(string enemyId, float distance, float seconds, string label,
                                   string[] expectFire, string[] expectSilent)
        {
            CombatDevTools.ClearBattlefield();
            CombatStats.Reset();
            yield return new WaitForSeconds(0.5f);

            var targets = CombatDevTools.SpawnRing(enemyId, 1, distance, 135f);
            foreach (var t in targets) t.DevFrozen = true;
            yield return new WaitForSeconds(seconds);

            var sb = new StringBuilder();
            foreach (var w in expectFire)
            {
                int n = CombatStats.Weapons.TryGetValue(w, out var e) ? e.Fired : 0;
                sb.Append($"{w} 발사 {n} ");
                if (n == 0) Fail($"{label}: {w}가 쏘지 않음");
            }
            foreach (var w in expectSilent)
            {
                int n = CombatStats.Weapons.TryGetValue(w, out var e) ? e.Fired : 0;
                sb.Append($"{w} 발사 {n} ");
                if (n > 0) Fail($"{label}: {w}가 쏘면 안 되는데 {n}발 쏨");
            }
            _report.AppendLine($"- {label}: {sb}");
        }

        // ------------------------------------------------------------ 스펙 시험 A~G (4차)

        private struct EngageResult
        {
            public string Label;
            public int Spawned, Alive, MissilesHit, MissilesIntercepted;
            public float HullDamage, Seconds;
            public int Kills => Spawned - Alive;
        }

        /// <summary>함교만 남기고 모듈을 모두 걷어낸 뒤 ids를 설치한다. near가 있으면 그 칸 가까이에.</summary>
        private static int ResetLoadout(params (string id, GridCoord? near)[] modules)
        {
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (ship == null || ship.Grid == null) return 0;
            var remove = new List<ModuleInstance>();
            foreach (var m in ship.Grid.Modules)
                if (m != null && m.Definition.Type != ModuleType.Bridge) remove.Add(m);
            foreach (var m in remove) CombatDevTools.RemoveModule(m);

            int ok = 0;
            foreach (var (id, near) in modules)
            {
                bool placed = near.HasValue
                    ? CombatDevTools.InstallModuleNear(id, near.Value, 3) != null
                    : CombatDevTools.InstallModule(id);
                if (placed) ok++;
            }
            ship.Systems?.Recalculate();
            return ok;
        }

        private static (string, GridCoord?) M(string id) => (id, null);
        private static (string, GridCoord?) M(string id, int x, int z) => (id, new GridCoord(x, z));

        private IEnumerator Engage(string label, float seconds, System.Action spawn, List<EngageResult> results,
                                   System.Func<bool> stopWhen = null)
        {
            CombatDevTools.ClearBattlefield();
            CombatLog.Clear();
            yield return new WaitForSeconds(0.6f);
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            CombatStats.Reset();

            var ship = GameManager.Instance.Player;
            float hull0 = ship.HullHp;
            int before = CountAliveEnemies();
            spawn();
            yield return null;
            int spawned = CountAliveEnemies() - before;

            float start = Time.time;
            while (Time.time - start < seconds)
            {
                if (stopWhen != null && stopWhen()) break;
                yield return null;
            }

            var r = new EngageResult
            {
                Label = label,
                Spawned = spawned,
                Alive = CountAliveEnemies() - before,
                HullDamage = hull0 - ship.HullHp,
                Seconds = Time.time - start,
                MissilesHit = CombatStats.Threats.TryGetValue(ThreatOutcome.HitShip, out var h) ? h.Count : 0,
                MissilesIntercepted = CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var i) ? i.Count : 0,
            };
            results.Add(r);
            _report.AppendLine($"| {label} | {r.Kills}/{r.Spawned} | {r.Seconds:0}초 | {r.HullDamage:0} | {r.MissilesIntercepted}/{r.MissilesHit} |");
            SaveLog(label);
        }

        /// <summary>시험마다 전투 기록을 파일로 남긴다(왜 방어에 실패했는지 추적).</summary>
        private void SaveLog(string label)
        {
            var sb = new StringBuilder();
            foreach (var e in CombatLog.Entries) sb.AppendLine(e.ToString());
            string file = "log_" + string.Join("_", label.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(' ', '_') + ".txt";
            File.WriteAllText(Path.Combine(OutputDirectory, file), sb.ToString(), new UTF8Encoding(false));
        }

        private static void TableHeader(StringBuilder report)
        {
            report.AppendLine("\n| 시험 | 격침/등장 | 시간 | 선체 피해 | 미사일 요격/피격 |");
            report.AppendLine("|---|---|---|---|---|");
        }

        private static bool AllDead() => CountAliveEnemies() == 0;

        private static string Yes(bool v) => v ? "예" : "아니오";

        /// <summary>HUD 캔버스 전체(월드 + UI)를 1920×1080으로 찍는다.</summary>
        private IEnumerator ScreenShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var hud = Object.FindFirstObjectByType<Game.UI.HUDView>();
            var canvas = hud != null ? hud.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (cam == null || canvas == null) yield break;
            const int w = 1920, h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevMode = canvas.renderMode;
            var prevCam = canvas.worldCamera;
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.5f;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            canvas.renderMode = prevMode;
            canvas.worldCamera = prevCam;
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        // ------------------------------------------------------------ 블록 강화

        private static readonly System.Reflection.BindingFlags Inst =
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        private static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Inst)?.Invoke(o, args);
        private static T Get<T>(object o, string field) => o?.GetType().GetField(field, Inst) is { } f ? (T)f.GetValue(o) : default;
        private static void Put(object o, string field, object value) => o?.GetType().GetField(field, Inst)?.SetValue(o, value);

        private int _upgradeErrors;
        private void CountErrors(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || (type == LogType.Error && message.Contains("Exception"))) _upgradeErrors++;
        }

        // ------------------------------------------------------------ 성장 카드(카드 시스템 개편)

        private static bool Near(float a, float b, float rel = 0.002f) => Mathf.Abs(a - b) <= Mathf.Max(1e-4f, Mathf.Abs(b)) * rel;

        // ------------------------------------------------------------ 섬(엄폐)

        private static T PrivateField<T>(object obj, string name) where T : class
        {
            var f = obj?.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(obj) as T;
        }

        // ------------------------------------------------------------ 스테이지 2 추가 적

        /// <summary>아군 무기를 끈다(적이 죽지 않게). keep이 참인 모듈은 남긴다. 되돌릴 목록을 돌려준다.</summary>
        private static List<Behaviour> DisableWeapons(System.Func<ModuleRuntime, bool> keep)
        {
            var list = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not IRadarSource && (keep == null || !keep(r))) { r.enabled = false; list.Add(r); }
            return list;
        }

        private static int ActiveCount(string prefabNamePrefix)
        {
            int n = 0;
            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                if (p.isActiveAndEnabled && p.name.StartsWith(prefabNamePrefix)) n++;
            return n;
        }

        private static int ActiveTorpedoes()
        {
            int n = 0;
            foreach (var t in Object.FindObjectsByType<Torpedo>(FindObjectsSortMode.None)) if (t.isActiveAndEnabled) n++;
            return n;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var f = FindChild(root.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>표적 가까이에 임시 카메라를 두고 찍는다(모델·포탑 확인용).</summary>
        private IEnumerator CloseShot(string name, Transform target, float distance)
        {
            yield return new WaitForEndOfFrame();
            var main = Camera.main;
            var go = new GameObject("CloseShotCam");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); }
            cam.fieldOfView = 40f;
            Vector3 side = Quaternion.AngleAxis(35f, Vector3.up) * target.right;
            cam.transform.position = target.position + side * distance + Vector3.up * distance * 0.55f;
            cam.transform.LookAt(target.position + Vector3.up * 0.8f);

            const int w = 1280, h = 720;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(go);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }

        // ------------------------------------------------------------ 기록

        private void AppendStats()
        {
            _report.AppendLine("\n| 무기 | 발사 | 명중 | 요격 | 화면 내 명중 |");
            _report.AppendLine("|---|---|---|---|---|");
            foreach (var kv in CombatStats.Weapons)
            {
                var e = kv.Value;
                string ratio = e.Hits > 0 ? $"{100f * e.OnScreenHits / e.Hits:0}%" : "-";
                _report.AppendLine($"| {kv.Key} | {e.Fired} | {e.Hits} | {e.Intercepts} | {ratio} |");
            }

            foreach (var kv in CombatStats.Threats)
            {
                var e = kv.Value;
                string label = kv.Key switch { ThreatOutcome.Intercepted => "요격", ThreatOutcome.HitShip => "피격", _ => "소멸" };
                string t = e.Seen > 0 ? $"화면 진입 후 평균 {e.SumSeconds / e.Seen:0.0}초 (최소 {e.MinSeconds:0.0} / 최대 {e.MaxSeconds:0.0})" : "화면 진입 기록 없음";
                _report.AppendLine($"- 적 미사일 {label} {e.Count}발 — {t}");
            }
        }

        private void Fail(string message)
        {
            _failures++;
            _report.AppendLine($"- **실패**: {message}");
        }

        private static int CountAliveEnemies()
        {
            int n = 0;
            foreach (var kind in new[] { TargetKind.Surface, TargetKind.Submarine, TargetKind.Aircraft })
                foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, kind)) if (t != null && t.IsAlive) n++;
            return n;
        }

        /// <summary>메인 카메라를 텍스처로 그려 PNG로 저장한다(월드만, 오버레이 UI 제외).</summary>
        /// <summary>주 카메라를 작은 화면으로 한 번 그려 픽셀을 돌려준다(비교용).</summary>
        private static Color32[] RenderMain()
        {
            var cam = Camera.main;
            const int w = 320, h = 180;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            Destroy(tex);
            return px;
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            if (cam == null) yield break;

            const int w = 1600, h = 900;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
#endif
