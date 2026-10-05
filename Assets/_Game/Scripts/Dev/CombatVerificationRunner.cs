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
    /// </summary>
    public class CombatVerificationRunner : MonoBehaviour
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
        [Tooltip("편대 진형·조함 검사만 돌린다(-formationOnly)")]
        public bool FormationOnly;
        [Tooltip("메인 화면 사전·무장 팩 v8 외형 검사만 돌린다(-codexOnly)")]
        public bool CodexOnly;
        [Tooltip("엘리트 초계함 주변 대형 물체 추적만 돌린다(-pccSoak)")]
        public bool PccSoak;

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

        // ------------------------------------------------------------ 레이더 화면

        /// <summary>
        /// 레이더 화면 검사: 스윕이 함교 안테나 모델의 실제 회전과 같은 속도·방위로 도는지,
        /// 탐지거리 안 표적만 찍히는지, 표적이 사라지면 에코가 한 바퀴 안에 사라지는지 보고 UI 포함 스크린샷을 남긴다.
        /// </summary>
        private IEnumerator RadarCheck()
        {
            _report.AppendLine("\n## 레이더 화면");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            var scope = Game.UI.RadarScopeUI.Instance;
            if (scope == null) { Fail("레이더 화면(RadarScopeUI)이 생성되지 않음"); yield break; }
            if (!scope.Online) { Fail("레이더 화면이 NO RADAR 상태"); yield break; }

            var source = scope.Source;
            var sourceBehaviour = source as Component;
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var antennaField = sourceBehaviour == null ? null :
                sourceBehaviour.GetType().GetField("radarAntenna", Flags) ?? sourceBehaviour.GetType().GetField("antenna", Flags);
            var antenna = antennaField?.GetValue(sourceBehaviour) as Transform;
            _report.AppendLine($"- 스윕 기준: {sourceBehaviour?.GetType().Name}, 안테나 {source.AntennaRpm:0} rpm (한 바퀴 {60f / source.AntennaRpm:0.0}초), 표시 거리 {scope.DisplayRange:0}");

            // 1) 안테나 모델 회전 vs 화면 스윕: 속도와 방위
            float sweepTurn = 0f, antennaTurn = 0f, maxOffset = 0f, sumOffset = 0f;
            int samples = 0;
            yield return new WaitForEndOfFrame();   // 안테나(Update)와 스윕(LateUpdate)이 모두 갱신된 뒤에 읽는다
            float prevSweep = scope.SweepBearing;
            // 안테나 모델에서 월드 수평에 가장 가까운 로컬 축을 골라 그 방위 변화를 잰다(스윕 계산과 무관한 독립 측정)
            Vector3 localAxis = Vector3.forward;
            if (antenna != null)
            {
                float best = float.MaxValue;
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                {
                    float y = Mathf.Abs(antenna.TransformDirection(axis).y);
                    if (y < best) { best = y; localAxis = axis; }
                }
            }
            float AntennaYaw() { var d = antenna.TransformDirection(localAxis); return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
            float prevAntennaYaw = antenna != null ? AntennaYaw() : 0f;
            float start = Time.time;
            while (Time.time - start < 6f)
            {
                yield return new WaitForEndOfFrame();
                float sweep = scope.SweepBearing;
                sweepTurn += Mathf.DeltaAngle(prevSweep, sweep);
                prevSweep = sweep;

                if (antenna != null)
                {
                    // 모델이 수평면에서 돈 양(함선 선회분 포함)
                    float yaw = AntennaYaw();
                    antennaTurn += Mathf.DeltaAngle(prevAntennaYaw, yaw);
                    prevAntennaYaw = yaw;
                }

                float offset = Mathf.Abs(Mathf.DeltaAngle(sweep, source.AntennaBearing));
                maxOffset = Mathf.Max(maxOffset, offset);
                sumOffset += offset;
                samples++;
            }
            float elapsed = Time.time - start;
            float sweepRpm = sweepTurn / elapsed / 6f;
            _report.AppendLine($"- {elapsed:0.0}초 측정: 화면 스윕 {sweepRpm:0.0} rpm" +
                               (antenna != null ? $", 함교 안테나 모델 {antennaTurn / elapsed / 6f:0.0} rpm" : ", 안테나 Transform 없음(적산 방식)") +
                               $", 스윕-안테나 방위 차 평균 {sumOffset / Mathf.Max(1, samples):0.0}° / 최대 {maxOffset:0.0}°");
            if (Mathf.Abs(sweepRpm - source.AntennaRpm) > 1f) Fail($"스윕 {sweepRpm:0.0} rpm ≠ 안테나 {source.AntennaRpm:0} rpm");
            if (antenna != null && Mathf.Abs(antennaTurn - sweepTurn) > Mathf.Abs(sweepTurn) * 0.02f) Fail($"안테나 모델 회전 {antennaTurn:0}° vs 스윕 {sweepTurn:0}° 불일치");
            if (maxOffset > 1f) Fail($"스윕이 안테나 방위와 최대 {maxOffset:0.0}° 어긋남");

            // 2) 탐지거리 안(20·28)은 찍히고 밖(범위+8)은 안 찍힘
            float range = scope.DisplayRange;
            var frozen = new List<EnemyController>();
            frozen.AddRange(CombatDevTools.SpawnRing("ene_fastboat", 1, 20f, 45f));
            frozen.AddRange(CombatDevTools.SpawnRing("ene_missileboat", 1, 28f, 200f));
            frozen.AddRange(CombatDevTools.SpawnRing("ene_fastboat", 1, range + 8f, 300f));
            foreach (var e in frozen) e.DevFrozen = true;
            var weapons = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not IRadarSource) { r.enabled = false; weapons.Add(r); }   // 표적이 격침되지 않게

            float period = 60f / source.AntennaRpm;
            yield return new WaitForSeconds(period * 1.2f);
            int contacts = scope.CountEchoes(false);
            _report.AppendLine($"- 멈춘 표적 20·28(범위 안)과 {range + 8f:0}(범위 밖): 한 바퀴 뒤 표적 에코 {contacts}개, 클러터 포함 {scope.CountEchoes(true)}개");
            if (contacts != 2) Fail($"범위 안 표적 2척인데 에코 {contacts}개");
            yield return RadarShot("radar_static");

            // 3) 표적이 사라지면 에코는 한 바퀴 안에 사라짐
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(period * 1.05f);
            int left = scope.CountEchoes(false);
            _report.AppendLine($"- 표적 제거 후 한 바퀴 뒤 남은 표적 에코: {left}");
            if (left != 0) Fail($"표적이 사라진 뒤에도 에코 {left}개가 남음");
            foreach (var w in weapons) if (w != null) w.enabled = true;

            // 4) 교전 장면: 고속정 무리 + 드론 + 미사일
            CombatDevTools.SpawnRing("ene_fastboat", 6, 30f, 20f);
            CombatDevTools.SpawnRing("ene_drone", 2, 30f, 100f);
            CombatDevTools.SpawnRing("ene_missileboat", 4, 66f, 40f);   // 화면 밖 표식(마름모)
            CombatDevTools.MissileVolley(4, 34f, 250f, damageOverride: 0.2f);
            yield return new WaitForSeconds(period * 1.3f);
            yield return RadarShot("radar_combat");
            yield return new WaitForSeconds(period);
            yield return RadarShot("radar_combat_2");

            // 5) 전용 레이더(52) 장착 후 거리 눈금
            CombatDevTools.ClearBattlefield();
            CombatDevTools.InstallTestLoadout();
            yield return new WaitForSeconds(1.5f);
            _report.AppendLine($"- 시험 무장(전용 레이더) 장착 후 표시 거리 {scope.DisplayRange:0}, 스윕 기준 {(scope.Source as Component)?.GetType().Name}");
            CombatDevTools.SpawnRing("ene_missileboat", 3, 46f, 30f);
            CombatDevTools.SpawnRing("ene_fastboat", 4, 38f, 160f);
            yield return new WaitForSeconds(period * 1.5f);
            yield return RadarShot("radar_long_range");
            CombatDevTools.ClearBattlefield();
        }

        /// <summary>오버레이 캔버스를 잠시 카메라 캔버스로 바꿔 UI까지 그리고, 레이더 부분을 잘라 저장한다.</summary>
        /// <summary>정비 화면 전체(카드·선택 패널·함선 뷰)를 찍는다.</summary>
        private IEnumerator RefitShot(Game.UI.RefitUI refit, string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var root = Get<GameObject>(refit, "root");
            var canvas = root != null ? root.GetComponentInParent<Canvas>() : null;
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

            // 함선 뷰만(정비 카메라가 보는 그대로, UI 없이)
            var shipCam = Get<Camera>(refit, "shipCamera");
            if (shipCam == null) yield break;
            var srt = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var prevShipTarget = shipCam.targetTexture;
            shipCam.targetTexture = srt;
            shipCam.Render();
            RenderTexture.active = srt;
            var stex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            stex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            stex.Apply();
            shipCam.targetTexture = prevShipTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(srt);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + "_ship.png"), stex.EncodeToPNG());
            Destroy(stex);
        }

        private IEnumerator RadarShot(string name)
        {
            yield return new WaitForEndOfFrame();
            var cam = Camera.main;
            var scope = Game.UI.RadarScopeUI.Instance;
            if (cam == null || scope == null) yield break;
            var canvas = scope.transform.parent != null ? scope.transform.parent.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) yield break;

            const int w = 1920, h = 1080;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevMode = canvas.renderMode;
            var group = scope.GetComponentInParent<CanvasGroup>();
            float prevAlpha = group != null ? group.alpha : 1f;

            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.5f;
            if (group != null) group.alpha = 1f;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            RenderTexture.active = rt;
            var full = new Texture2D(w, h, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            full.Apply();

            // 왼쪽 아래 HUD 기둥(함 현황 + 레이더)을 잘라낸다
            var corners = new Vector3[4];
            ((RectTransform)scope.transform).GetWorldCorners(corners);
            Vector3 topRight = cam.WorldToScreenPoint(corners[2]);
            var weaponPanel = Object.FindFirstObjectByType<Game.UI.WeaponStatusPanelUI>();
            if (weaponPanel != null)
            {
                ((RectTransform)weaponPanel.transform).GetWorldCorners(corners);
                topRight.x = Mathf.Max(topRight.x, cam.WorldToScreenPoint(corners[2]).x);
            }
            var hudView = Object.FindFirstObjectByType<Game.UI.HUDView>();
            var levelPanel = hudView != null ? FindChild(hudView.transform, "Level bar") as RectTransform : null;
            if (levelPanel != null)
            {
                levelPanel.GetWorldCorners(corners);
                Vector3 tr = cam.WorldToScreenPoint(corners[2]);
                topRight = new Vector3(Mathf.Max(topRight.x, tr.x), Mathf.Max(topRight.y, tr.y), 0f);
            }
            // 왼쪽 끝: 하단 덩어리가 가운데로 옮겨졌으므로 레이더 왼쪽 아래 모서리부터
            ((RectTransform)scope.transform).GetWorldCorners(corners);
            int x0 = Mathf.Clamp(Mathf.FloorToInt(cam.WorldToScreenPoint(corners[0]).x) - 12, 0, w - 64);
            int cw = Mathf.Clamp(Mathf.CeilToInt(topRight.x) + 12 - x0, 64, w - x0);
            int ch = Mathf.Clamp(Mathf.CeilToInt(topRight.y) + 12, 64, h);
            var crop = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            crop.SetPixels(full.GetPixels(x0, 0, cw, ch));
            crop.Apply();

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            canvas.renderMode = prevMode;
            if (group != null) group.alpha = prevAlpha;

            File.WriteAllBytes(Path.Combine(OutputDirectory, name + "_full.png"), full.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), crop.EncodeToPNG());
            Destroy(full);
            Destroy(crop);
        }

        // ------------------------------------------------------------ 탄약

        private static List<(string name, IAmmoUser user)> AmmoUsers()
        {
            var result = new List<(string, IAmmoUser)>();
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (ship == null || ship.Grid == null) return result;
            foreach (var m in ship.Grid.Modules)
                if (m?.Runtime is IAmmoUser u && u.Ammo != null && !m.IsDestroyed)
                    result.Add((m.Definition.DisplayName, u));
            return result;
        }

        private void AppendAmmo(string label)
        {
            var sb = new StringBuilder();
            foreach (var (name, u) in AmmoUsers())
                sb.Append(u.Ammo.Infinite ? $"{name} 무한 · " : $"{name} {u.Ammo.Current}/{u.Ammo.Capacity}({u.Ammo.Status}) · ");
            _report.AppendLine($"- 탄약 {label}: {sb}");
        }

        /// <summary>
        /// 탄약 검사(1차): 모든 무기가 탄약 데이터를 읽었는지, 쏘면 줄고 보급되는지,
        /// VLS가 셀 수 + 보급량보다 더 쏘지 않는지, CIWS가 비면 재장전하는지 본다. UI 포함 스크린샷.
        /// </summary>
        private IEnumerator AmmoCheck()
        {
            _report.AppendLine("\n## 탄약");
            CombatDevTools.ClearBattlefield();
            CombatDevTools.InstallTestLoadout();
            yield return new WaitForSeconds(1f);

            var users = AmmoUsers();
            foreach (var (name, u) in users)
                if (u.Ammo.Infinite) Fail($"{name}: 탄약 데이터가 없어 무한 탄약");
            AppendAmmo("시작");

            // 1) 고속정 무리: 기관포·76mm·로켓 소모
            CombatStats.Reset();
            CombatDevTools.SpawnRing("ene_fastboat", 10, 40f);
            float start = Time.time;
            int minCiws = int.MaxValue;
            while (Time.time - start < 25f) yield return null;
            AppendAmmo("고속정 10척 25초 후");
            AppendStats();
            foreach (var (name, u) in AmmoUsers())
                if (u.Ammo.Current < 0 || u.Ammo.Current > u.Ammo.Capacity) Fail($"{name}: 탄약 {u.Ammo.Current}/{u.Ammo.Capacity} 범위 밖");
            yield return RadarShot("ammo_fastpack");

            // 2) VLS 소진: 가치 높은 표적(미사일정) 여러 척을 세워 두고 쏘게 한다
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            CombatStats.Reset();
            // VLS 한 문만 남기고 끈다(검증을 이어 돌리면 여러 문이 표적을 나눠 셀이 비지 않는다)
            var launchers = AmmoUsers().FindAll(x => x.user is VlsModule);
            for (int i = 1; i < launchers.Count; i++) ((Behaviour)launchers[i].user).enabled = false;
            var disabledVls = launchers.GetRange(Mathf.Min(1, launchers.Count), Mathf.Max(0, launchers.Count - 1));
            if (launchers.Count > 1) launchers = launchers.GetRange(0, 1);
            int vlsCells = 0;
            foreach (var l in launchers) vlsCells += l.user.Ammo.Capacity;
            var frozen = CombatDevTools.SpawnRing("ene_missileboat", vlsCells + 6, 42f);   // 셀보다 많아야 셀이 빈다
            foreach (var e in frozen) e.DevFrozen = true;
            start = Time.time;
            int minCells = int.MaxValue;
            while (Time.time - start < 40f)
            {
                foreach (var l in launchers) minCells = Mathf.Min(minCells, l.user.Ammo.Current);
                yield return null;
            }
            // 미사일정(가치 3)만 있으면 VLS는 셀 25%(8셀 → 2셀)를 대형·보스용으로 남겨야 한다
            int reserve = launchers.Count > 0 ? Mathf.CeilToInt(launchers[0].user.Ammo.Capacity * 0.25f) : 0;
            float elapsed = Time.time - start;
            int vlsFired = CombatStats.Weapons.TryGetValue("VLS", out var ve) ? ve.Fired : 0;
            float interval = launchers.Count > 0 ? launchers[0].user.Ammo.Interval : 15f;
            int allowed = vlsCells + launchers.Count * (Mathf.FloorToInt(elapsed / interval) + 1);
            _report.AppendLine($"- VLS {launchers.Count}문: 셀 {vlsCells}, 미사일정 {frozen.Count}척, {elapsed:0}초 동안 {vlsFired}발 발사(허용 최대 {allowed}), 최저 셀 {minCells} (예비 {reserve}셀 유지 기대)");
            foreach (var d in disabledVls) ((Behaviour)d.user).enabled = true;
            if (launchers.Count == 0) Fail("VLS가 설치되지 않음");
            else
            {
                if (vlsFired > allowed) Fail($"VLS가 셀+보급보다 많이 쏨({vlsFired} > {allowed})");
                if (minCells < reserve) Fail($"VLS가 가치 3 표적에 예비 셀까지 씀(최저 {minCells} < {reserve})");
                if (minCells > reserve) Fail($"VLS가 쓸 수 있는 셀을 남김(최저 {minCells} > 예비 {reserve})");
            }
            AppendAmmo("미사일정 40초 후");
            yield return RadarShot("ammo_vls_empty");

            // 3) CIWS 소진: 드론 무리 뒤에 미사일 일제사격
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            var weapons = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not CiwsModule && r is not IRadarSource) { r.enabled = false; weapons.Add(r); }   // CIWS 혼자 막게
            CombatStats.Reset();
            var ciws = AmmoUsers().Find(x => x.user is CiwsModule).user;
            bool reloadSeen = false;
            start = Time.time;
            CombatDevTools.SpawnRing("ene_drone", 10, 30f);
            bool volleyFired = false;
            while (Time.time - start < 30f)
            {
                if (!volleyFired && Time.time - start > 12f) { CombatDevTools.MissileVolley(6, 40f, damageOverride: 0.2f); volleyFired = true; }
                if (ciws != null)
                {
                    minCiws = Mathf.Min(minCiws, ciws.Ammo.Current);
                    if (ciws.Ammo.IsReloading) reloadSeen = true;
                }
                yield return null;
            }
            foreach (var w in weapons) if (w != null) w.enabled = true;
            _report.AppendLine($"- CIWS 단독(드론 10 → 12초 뒤 미사일 6): 최저 탄약 {(minCiws == int.MaxValue ? -1 : minCiws)}, 재장전 관측 {(reloadSeen ? "예" : "아니오")}");
            AppendStats();
            if (ciws == null) Fail("CIWS가 없음");
            AppendAmmo("CIWS 시험 후");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ CIWS 점사

        /// <summary>
        /// CIWS 한 문이 미사일 1발·드론 1대를 처리하는 데 드는 시간·탄약·점사 수를 여러 번 잰다.
        /// 점사는 최소 시간만큼은 끝까지 쏘므로, 한 번 교전에 최소 점사 탄약 이상이 들어야 한다.
        /// </summary>
        private IEnumerator BurstCheck()
        {
            _report.AppendLine("\n## CIWS 점사");
            CiwsModule ciws = null;
            foreach (var c in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None))
                if (c.Instance != null && c.enabled && !c.Instance.IsDestroyed) { ciws = c; break; }
            if (ciws == null) { Fail("CIWS 없음"); yield break; }

            var ship = GameManager.Instance.Player;
            const int Trials = 6;
            foreach (bool drone in new[] { false, true })
            {
                int kills = 0, engaged = 0;
                float timeSum = 0f, ammoSum = 0f, burstSum = 0f;
                for (int trial = 0; trial < Trials; trial++)
                {
                    CombatDevTools.ClearBattlefield();
                    yield return new WaitForSeconds(0.5f);
                    var disabled = new List<Behaviour>();
                    foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                        if (r.enabled && r != ciws && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
                    ciws.Ammo.Refill();
                    CombatStats.Reset();
                    int ammo0 = ciws.Ammo.Current, bursts0 = ciws.BurstCount, shots0 = ciws.ShotsFired;

                    float angle = -20f + 40f * trial / (Trials - 1);
                    ITargetable target = null;
                    if (drone)
                    {
                        var list = CombatDevTools.SpawnRing("ene_drone", 1, 26f, angle);
                        if (list.Count > 0) target = list[0] as ITargetable;
                    }
                    else CombatDevTools.MissileVolley(1, 30f, angle, damageOverride: 0.2f);
                    yield return null;
                    if (target == null)
                    {
                        var kind = drone ? TargetKind.Aircraft : TargetKind.Missile;
                        var reg = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                        if (reg.Count > 0) target = reg[0];
                    }

                    float start = Time.time, firstShot = -1f, gone = -1f, lastDist = 999f;
                    while (Time.time - start < 20f)
                    {
                        if (firstShot < 0f && ciws.ShotsFired != shots0) firstShot = Time.time;
                        bool alive = target != null && target.IsAlive && target.Transform != null;
                        if (alive) lastDist = Vector3.Distance(target.Transform.position, ship.transform.position);
                        else if (gone < 0f) gone = Time.time;
                        if (gone > 0f && !ciws.InBurst && Time.time - gone > 0.3f) break;
                        yield return null;
                    }

                    bool killed = drone
                        ? gone > 0f && lastDist > 4f
                        : CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var ie) && ie.Count > 0;
                    if (firstShot > 0f)
                    {
                        engaged++;
                        ammoSum += ammo0 - ciws.Ammo.Current;
                        burstSum += ciws.BurstCount - bursts0;
                        if (killed) { kills++; timeSum += gone - firstShot; }
                    }
                    foreach (var d in disabled) if (d != null) d.enabled = true;
                }

                string name = drone ? "드론" : "미사일";
                float avgAmmo = engaged > 0 ? ammoSum / engaged : 0f;
                _report.AppendLine($"- {name} 1개 × {Trials}회: 격추 {kills}/{Trials} · 첫 발부터 격추까지 평균 {(kills > 0 ? timeSum / kills : 0f):0.00}초 · " +
                                   $"교전당 탄약 {avgAmmo:0} · 점사 {(engaged > 0 ? burstSum / engaged : 0f):0.0}회");
                if (kills == 0) Fail($"CIWS가 {name}을 한 번도 격추하지 못함");
                if (engaged > 0 && avgAmmo < 18f) Fail($"{name} 교전당 탄약 {avgAmmo:0} — 최소 점사가 지켜지지 않음");
            }
            _report.AppendLine($"- 점사 평균 탄약 {ciws.AverageBurstAmmo:0} (누적 {ciws.BurstCount}회)");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 방어(2차)

        /// <summary>CIWS만 켜 두고 미사일을 쏘아 요격·피격을 센다. 방향은 사격 금지 구역이 없는 앞쪽 부채꼴로 모은다.</summary>
        private IEnumerator CiwsOnly(string label, int missiles, float spreadDeg, List<(string, int, int)> results)
        {
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var disabled = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not CiwsModule && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
            foreach (var (_, u) in AmmoUsers()) u.Ammo.Refill();
            CombatStats.Reset();

            // 앞쪽(±spread/2)에서 동시에 날아오게: 한 발씩 각도를 나눠 쏜다
            for (int i = 0; i < missiles; i++)
            {
                float a = missiles == 1 ? 0f : -spreadDeg * 0.5f + spreadDeg * i / (missiles - 1);
                CombatDevTools.MissileVolley(1, 30f, a, damageOverride: 0.2f);
            }
            var mounts = new List<CiwsModule>();
            foreach (var c in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (c.Instance != null && c.enabled) mounts.Add(c);
            var shots0 = new List<int>();
            foreach (var c in mounts) shots0.Add(c.ShotsFired);
            var bursts0 = new List<int>();
            foreach (var c in mounts) bursts0.Add(c.BurstCount);
            var reasons = new Dictionary<string, int>();
            float start = Time.time;
            while (Time.time - start < 12f && TargetRegistry.Get(TargetKind.Missile).Count > 0)
            {
                for (int i = 0; i < mounts.Count; i++)
                {
                    string key = $"#{i}:{(mounts[i].HasTarget ? (string.IsNullOrEmpty(mounts[i].LastBlockReason) ? "사격" : mounts[i].LastBlockReason) : "표적 없음")}";
                    reasons[key] = reasons.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            var diag = new StringBuilder();
            for (int i = 0; i < mounts.Count; i++) diag.Append($"#{i} {mounts[i].ShotsFired - shots0[i]}발 · 점사 {mounts[i].BurstCount - bursts0[i]}회 · ");
            foreach (var kv in reasons) diag.Append($"{kv.Key} {kv.Value}f · ");

            int intercepted = CombatStats.Threats.TryGetValue(ThreatOutcome.Intercepted, out var ie) ? ie.Count : 0;
            int hit = CombatStats.Threats.TryGetValue(ThreatOutcome.HitShip, out var he) ? he.Count : 0;
            int fired = CombatStats.Weapons.TryGetValue("CIWS", out var we) ? we.Fired : 0;
            _report.AppendLine($"- {label}: 미사일 {missiles}발 → 요격 {intercepted} · 피격 {hit} (CIWS {fired}발)");
            _report.AppendLine($"  - 문별: {diag}");
            results.Add((label, intercepted, hit));
            foreach (var d in disabled) if (d != null) d.enabled = true;
        }

        /// <summary>
        /// 2차 검사: CIWS 교전 절차·분담(1문 vs 2문), 미사일정 연발, VLS 표적 가치 규칙, 분류별 효율 데이터.
        /// </summary>
        private IEnumerator DefenseCheck()
        {
            _report.AppendLine("\n## 방어·표적 분류 (2차)");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(1f);

            // 1) 분류 데이터
            var sb = new StringBuilder();
            foreach (var id in new[] { "ene_fastboat", "ene_missileboat", "ene_submarine", "ene_boss", "ene_drone", "ene_fighter" })
            {
                var def = CombatDevTools.FindEnemy(id);
                if (def == null) continue;
                sb.Append($"{def.DisplayName} {def.Category}/{def.TargetValue} · ");
                if (def.Category == TargetCategory.Unspecified) Fail($"{id}: 표적 분류가 비어 있음");
            }
            _report.AppendLine($"- 분류/가치: {sb}");

            yield return BurstCheck();

            // 2) CIWS 1문
            var results = new List<(string, int, int)>();
            int ciwsCount = 0;
            foreach (var r in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (r.Instance != null) ciwsCount++;
            _report.AppendLine($"- 설치된 CIWS {ciwsCount}문");
            yield return CiwsOnly($"CIWS {ciwsCount}문 · 2발", 2, 20f, results);
            yield return CiwsOnly($"CIWS {ciwsCount}문 · 6발 포화", 6, 60f, results);

            // 3) CIWS 한 문 추가
            bool added = CombatDevTools.InstallModule("mod_ciws");
            yield return new WaitForSeconds(0.5f);
            int ciwsAfter = 0;
            foreach (var r in Object.FindObjectsByType<CiwsModule>(FindObjectsSortMode.None)) if (r.Instance != null) ciwsAfter++;
            yield return CiwsOnly($"CIWS {ciwsAfter}문 · 6발 포화", 6, 60f, results);
            if (!added) Fail("CIWS 추가 설치 실패");

            if (results.Count == 3)
            {
                if (results[0].Item2 == 0) Fail("CIWS가 2발 중 하나도 요격하지 못함");
                if (results[1].Item3 == 0) _report.AppendLine("  - 참고: CIWS 1문이 6발 포화를 모두 막음(포화 돌파가 일어나지 않음)");
                if (results[2].Item2 < results[1].Item2) _report.AppendLine("  - 참고: CIWS를 늘렸는데 요격 수가 늘지 않음");
            }

            // 4) 미사일정 연발: 한 척이 한 번에 몇 발 쏘는지
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);
            var disabled = new List<Behaviour>();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r.enabled && r is not IRadarSource) { r.enabled = false; disabled.Add(r); }
            var boat = CombatDevTools.SpawnRing("ene_missileboat", 1, 34f);
            int maxInFlight = 0;
            float start = Time.time;
            while (Time.time - start < 8f)
            {
                maxInFlight = Mathf.Max(maxInFlight, TargetRegistry.Get(TargetKind.Missile).Count);
                yield return null;
            }
            foreach (var d in disabled) if (d != null) d.enabled = true;
            var boatDef = CombatDevTools.FindEnemy("ene_missileboat");
            int salvo = boatDef != null ? boatDef.SalvoSize : 1;
            _report.AppendLine($"- 미사일정 1척: 연발 {salvo}발 설정, 동시에 날아간 미사일 최대 {maxInFlight}발");
            if (maxInFlight < salvo) Fail($"미사일정 연발이 {salvo}발인데 동시 비행 {maxInFlight}발");
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 탄약고·웨이브(3차)

        /// <summary>
        /// 3차 검사: 탄약고 근접 보너스(용량·보급), 여러 개 겹쳐도 하나만 적용, 파괴 시 유폭과 보너스 해제,
        /// 스테이지 웨이브의 위협 혼합, 보스 호위 스폰.
        /// </summary>
        private IEnumerator LogisticsCheck()
        {
            _report.AppendLine("\n## 탄약고·웨이브 (3차)");
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.5f);

            // 1) 기관포 한 문을 기준으로
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            AutocannonModule gun = null;
            if (ship != null && ship.Grid != null)
                foreach (var m in ship.Grid.Modules)
                    if (m?.Runtime is AutocannonModule a && !m.IsDestroyed) { gun = a; break; }
            if (gun == null) { Fail("기관포가 없어 탄약고 검사를 못 함"); yield break; }

            int baseCap = gun.Ammo.Capacity;
            float baseInterval = gun.Ammo.Interval;

            var mag = CombatDevTools.InstallModuleNear("mod_magazine", gun.Instance.Origin, 1);
            yield return null;
            int magCap = gun.Ammo.Capacity;
            float magInterval = gun.Ammo.Interval;
            _report.AppendLine($"- 기관포 {gun.Instance.Origin}: 탄약고 없음 {baseCap}발·보급 {baseInterval:0.0}초 → 탄약고 {(mag != null ? mag.Origin.ToString() : "설치 실패")} 옆 {magCap}발·보급 {magInterval:0.0}초");
            if (mag == null) { Fail("기관포 옆에 탄약고를 설치하지 못함"); yield break; }
            if (magCap != Mathf.RoundToInt(baseCap * 1.5f)) Fail($"탄약고 용량 보너스가 +50%가 아님({baseCap} → {magCap})");
            if (Mathf.Abs(magInterval - baseInterval / 1.25f) > 0.05f) Fail($"탄약고 보급 보너스가 25%가 아님({baseInterval:0.00} → {magInterval:0.00})");

            // 2) 두 번째 탄약고: 겹쳐도 하나만
            var mag2 = CombatDevTools.InstallModuleNear("mod_magazine", gun.Instance.Origin, 2);
            yield return null;
            _report.AppendLine($"- 탄약고 2개 겹침: {gun.Ammo.Capacity}발 (하나만 적용 기대 {magCap})");
            if (mag2 != null && gun.Ammo.Capacity != magCap) Fail("탄약고 보너스가 겹쳐 쌓임");

            // 3) CIWS는 탄약고 보너스를 받지 않는다
            CiwsModule ciws = null;
            foreach (var m in ship.Grid.Modules) if (m?.Runtime is CiwsModule c && !m.IsDestroyed) { ciws = c; break; }
            if (ciws != null)
            {
                var near = CombatDevTools.InstallModuleNear("mod_magazine", ciws.Instance.Origin, 1);
                yield return null;
                _report.AppendLine($"- CIWS 옆 탄약고({(near != null ? "설치" : "자리 없음")}): CIWS 탄약 {ciws.Ammo.Capacity}발 (보너스 없음 기대 {ciws.Definition.Stats.MagazineCapacity})");
                if (near != null && ciws.Ammo.Capacity != ciws.Definition.Stats.MagazineCapacity) Fail("CIWS가 탄약고 보너스를 받음");
                if (near != null) CombatDevTools.RemoveModule(near);   // 기관포 검사에 섞이지 않게
            }

            // 4) 유폭: 두 번째 탄약고를 먼저 치우고(연쇄 방지) 첫 탄약고를 파괴
            if (mag2 != null) CombatDevTools.RemoveModule(mag2);
            yield return null;
            float gunHpBefore = gun.Instance.Hp;
            float hullBefore = ship.HullHp;
            mag.TakeDamage(99999f);
            float gunHpAfter = gun.Instance.Hp;
            var cook = MagazineModule.LastCookOff;
            yield return null;
            _report.AppendLine($"- 유폭: 피해 모듈 {cook.damaged}개, 파괴 {cook.destroyed}개, 옆 기관포 HP {gunHpBefore:0} → {gunHpAfter:0}, 선체 {hullBefore - ship.HullHp:0} 피해, 기관포 탄약 용량 {gun.Ammo.Capacity}발");
            if (cook.damaged == 0) Fail("유폭 피해를 받은 모듈이 없음");
            if (gunHpAfter >= gunHpBefore) Fail("탄약고 옆 기관포가 유폭 피해를 받지 않음");
            if (!gun.Instance.IsDestroyed && gun.Ammo.Capacity != baseCap) Fail($"탄약고가 부서졌는데 보너스가 남음({gun.Ammo.Capacity})");
            yield return Shot("cookoff");

            // 5) 웨이브 구성
            var director = Object.FindFirstObjectByType<StageDirector>(FindObjectsInactive.Include);
            var stages = director != null
                ? typeof(StageDirector).GetField("stages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(director) as Game.Data.RoundSet[]
                : null;
            if (stages == null) { Fail("StageDirector 스테이지를 읽지 못함"); yield break; }

            Game.Data.RoundSet.Round bossRound = default;
            bool hasBossRound = false;
            for (int s = 0; s < stages.Length; s++)
            {
                if (stages[s] == null) continue;
                _report.AppendLine($"- 스테이지 {s + 1}:");
                for (int i = 0; i < stages[s].Count; i++)
                {
                    var r = stages[s].Get(i);
                    var kinds = new StringBuilder();
                    var categories = new HashSet<TargetCategory>();
                    if (r.Entries != null)
                        foreach (var e in r.Entries)
                            if (e.Enemy != null && e.Weight > 0f)
                            {
                                kinds.Append($"{e.Enemy.DisplayName} {e.Weight:0.#} · ");
                                categories.Add(e.Enemy.Category);
                            }
                    string boss = r.Boss != null ? $" + 보스 {r.Boss.DisplayName}" + (r.Escort != null ? $"·호위 {r.Escort.DisplayName}×{r.EscortCount}" : "") : "";
                    _report.AppendLine($"  - {i + 1}. {r.Title}: {kinds}{boss}");
                    if (s == 0 && i >= 1 && categories.Count < 2) Fail($"스테이지 1 {i + 1}구간 '{r.Title}'이 한 종류 위협뿐");
                    if (r.Boss != null)
                    {
                        if (r.Escort == null || r.EscortCount <= 0) Fail($"스테이지 {s + 1} 보스 구간에 호위가 없음");
                        if (s == 0) { bossRound = r; hasBossRound = true; }
                    }
                }
            }

            // 6) 보스 호위 실제 스폰
            if (hasBossRound && EnemySpawner.Instance != null && ship != null)
            {
                CombatDevTools.ClearBattlefield();
                yield return null;
                int before = CountAliveEnemies();
                EnemySpawner.Instance.SetPhase(bossRound, ship.transform);
                yield return new WaitForSeconds(0.3f);
                int spawned = CountAliveEnemies() - before;
                _report.AppendLine($"- 보스 구간 시작: 보스 1 + 호위 {bossRound.EscortCount} 기대, 실제 {spawned}척");
                if (spawned != 1 + bossRound.EscortCount) Fail($"보스 구간 스폰 수 {spawned} ≠ {1 + bossRound.EscortCount}");
                yield return Shot("boss_escort");
                CombatDevTools.ClearBattlefield();
            }
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

        /// <summary>
        /// 명세 18번 시험 A~G. 한 가지 무기·방어로는 모든 위협을 안정적으로 막지 못하는지 본다.
        /// 선체 피해는 검증용으로 늘린 선체(100000)에서 잰 실제 피해량이다.
        /// </summary>
        private IEnumerator SpecTests()
        {
            _report.AppendLine("\n## 명세 시험 A~G (4차)");
            var results = new List<EngageResult>();
            EngageResult Last() => results[results.Count - 1];

            // A. 기관포만
            _report.AppendLine($"\n### A. 기관포 1문만 (설치 {ResetLoadout(M("mod_autocannon"))})");
            TableHeader(_report);
            yield return Engage("A1 고속정 8", 45f, () => CombatDevTools.SpawnRing("ene_fastboat", 8, 40f), results, AllDead);
            var a1 = Last();
            yield return Engage("A2 미사일정 3", 40f, () => CombatDevTools.SpawnRing("ene_missileboat", 3, 44f), results);
            var a2 = Last();
            if (a1.Alive > 0) Fail("A: 기관포가 고속정 무리를 처리하지 못함");
            if (a2.Alive == 0) Fail("A: 기관포만으로 미사일정을 모두 잡음(한계가 없음)");

            // B. VLS 위주
            _report.AppendLine($"\n### B. VLS 3문 + 레이더 (설치 {ResetLoadout(M("mod_vls"), M("mod_vls"), M("mod_vls"), M("mod_radar"))})");
            TableHeader(_report);
            yield return Engage("B1 보스 1", 45f, () => CombatDevTools.SpawnRing("ene_boss", 1, 42f), results, AllDead);
            var b1 = Last();
            yield return Engage("B2 고속정 10", 30f, () => CombatDevTools.SpawnRing("ene_fastboat", 10, 40f), results);
            var b2 = Last();
            int vlsAtSmall = CombatStats.Weapons.TryGetValue("VLS", out var bv) ? bv.Fired : 0;
            _report.AppendLine($"- B2에서 VLS가 고속정에 쏜 수: {vlsAtSmall}");
            int minCells = int.MaxValue;
            var vlsList = AmmoUsers().FindAll(x => x.user is VlsModule);
            yield return Engage("B3 미사일정 12 (장기전)", 60f, () => CombatDevTools.SpawnRing("ene_missileboat", 12, 46f), results,
                                () => { foreach (var v in vlsList) minCells = Mathf.Min(minCells, v.user.Ammo.Current); return false; });
            var b3 = Last();
            _report.AppendLine($"- B3 VLS 최저 셀: {(minCells == int.MaxValue ? -1 : minCells)} (문당 8셀)");
            if (b1.Alive > 0) Fail("B: VLS가 보스를 제거하지 못함");
            if (vlsAtSmall > 0) Fail("B: VLS가 고속정에 미사일을 낭비함");
            if (b2.Alive == 0 && b2.HullDamage <= 0f) Fail("B: VLS 위주 함선이 고속정 무리에 아무 압박도 받지 않음");
            if (minCells > 2) _report.AppendLine("  - 참고: 장기전에서 VLS 셀이 예비선까지 줄지 않음");

            // C. CIWS 1문
            _report.AppendLine($"\n### C. CIWS 1문만 (설치 {ResetLoadout(M("mod_ciws", 2, 0))})");
            TableHeader(_report);
            yield return Engage("C1 미사일 2", 12f, () => FrontVolley(2, 20f), results, NoMissiles);
            var c1 = Last();
            yield return Engage("C2 미사일 6 포화", 12f, () => FrontVolley(6, 70f), results, NoMissiles);
            var c2 = Last();
            if (c1.MissilesIntercepted < 2) Fail($"C: CIWS 1문이 미사일 2발을 다 막지 못함({c1.MissilesIntercepted}/2)");
            if (c2.MissilesHit == 0) Fail("C: CIWS 1문이 6발 포화를 모두 막음(포화 돌파가 없음)");

            // D. CIWS 여러 문(서로 다른 곳)
            int dInstalled = ResetLoadout(M("mod_ciws", 2, 0), M("mod_ciws", 0, 2), M("mod_ciws", 0, -2), M("mod_ciws", -3, 0));
            int slots = GameManager.Instance.Player.Grid.OccupiedCells.Count;
            _report.AppendLine($"\n### D. CIWS {dInstalled}문 분산 (함선 칸 {slots}, 다른 모듈을 넣을 자리를 CIWS가 차지)");
            TableHeader(_report);
            yield return Engage("D1 미사일 6 포화", 12f, () => FrontVolley(6, 70f), results, NoMissiles);
            var d1 = Last();
            yield return Engage("D2 미사일 8 전방위", 12f, () => CombatDevTools.MissileVolley(8, 30f, 0f, damageOverride: 0.2f), results, NoMissiles);
            if (d1.MissilesIntercepted < c2.MissilesIntercepted) Fail($"D: CIWS를 늘렸는데 요격이 줄어듦({c2.MissilesIntercepted} → {d1.MissilesIntercepted})");

            // E. CIWS가 드론에 탄을 쓴 뒤 포화
            _report.AppendLine($"\n### E. CIWS 1문, 드론 뒤 포화 (설치 {ResetLoadout(M("mod_ciws", 2, 0))})");
            TableHeader(_report);
            var ciws = AmmoUsers().Find(x => x.user is CiwsModule).user;
            int ammoAtVolley = -1;
            bool reloadingAtVolley = false;
            bool volley = false;
            float eStart = 0f;
            yield return Engage("E1 드론 16 → 10초 뒤 미사일 6", 22f, () =>
            {
                CombatDevTools.SpawnRing("ene_drone", 16, 26f);
                eStart = Time.time;
            }, results, () =>
            {
                if (!volley && Time.time - eStart > 10f)
                {
                    volley = true;
                    if (ciws != null) { ammoAtVolley = ciws.Ammo.Current; reloadingAtVolley = ciws.Ammo.IsReloading; }
                    FrontVolley(6, 70f);
                }
                return false;
            });
            var e1 = Last();
            _report.AppendLine($"- 포화 순간 CIWS 탄약 {ammoAtVolley}/{(ciws != null ? ciws.Ammo.Capacity : 0)}, 재장전 중 {(reloadingAtVolley ? "예" : "아니오")} → 요격 {e1.MissilesIntercepted} · 피격 {e1.MissilesHit} (C2 대비 {c2.MissilesIntercepted}/{c2.MissilesHit})");
            if (ciws != null && ammoAtVolley >= ciws.Ammo.Capacity) Fail("E: 드론을 상대하고도 CIWS 탄약이 줄지 않음");

            // F. 탄약고: 3차 검사를 그대로 쓴다
            _report.AppendLine("\n### F. 탄약고 배치·유폭");
            ResetLoadout(M("mod_autocannon"), M("mod_ciws", 2, 0));
            yield return LogisticsCheck();

            // G. 혼합 웨이브: 한 가지 체계 vs 균형 편성
            _report.AppendLine("\n### G. 혼합 위협(고속정 8 · 미사일정 3 · 잠수함 2)");
            TableHeader(_report);
            var builds = new (string name, (string, GridCoord?)[] mods)[]
            {
                ("기관포 3", new[] { M("mod_autocannon"), M("mod_autocannon"), M("mod_autocannon") }),
                ("VLS 3+레이더", new[] { M("mod_vls"), M("mod_vls"), M("mod_vls"), M("mod_radar") }),
                ("CIWS 3", new[] { M("mod_ciws", 2, 0), M("mod_ciws", 0, 2), M("mod_ciws", 0, -2) }),
                ("균형", new[] { M("mod_autocannon"), M("mod_gun76"), M("mod_vls"), M("mod_radar"), M("mod_ciws", 2, 0), M("mod_asw"), M("mod_sonar") }),
            };
            var gResults = new List<EngageResult>();
            foreach (var (name, mods) in builds)
            {
                ResetLoadout(mods);
                yield return Engage($"G {name}", 50f, () =>
                {
                    CombatDevTools.SpawnRing("ene_fastboat", 8, 40f);
                    CombatDevTools.SpawnRing("ene_missileboat", 3, 46f, 30f);
                    CombatDevTools.SpawnSubmarines(2, 20f);
                }, results, AllDead);
                gResults.Add(Last());
            }
            var balanced = gResults[gResults.Count - 1];
            int singleSolved = 0;
            for (int i = 0; i < gResults.Count - 1; i++) if (gResults[i].Alive == 0 && gResults[i].HullDamage <= balanced.HullDamage) singleSolved++;
            if (singleSolved > 0) Fail($"G: 한 가지 체계 편성 {singleSolved}개가 균형 편성만큼 혼합 위협을 처리함");

            // 원래 시험 무장으로 되돌린다
            ResetLoadout();
            CombatDevTools.InstallTestLoadout();
        }

        private static bool NoMissiles() => TargetRegistry.Get(TargetKind.Missile).Count == 0;

        /// <summary>앞쪽 부채꼴에서 동시에 날아오는 미사일(피해는 작게).</summary>
        private static void FrontVolley(int count, float spreadDeg)
        {
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : -spreadDeg * 0.5f + spreadDeg * i / (count - 1);
                CombatDevTools.MissileVolley(1, 30f, a, damageOverride: 0.2f);
            }
        }

        // ------------------------------------------------------------ 미사일 위협·선체 위험 경고

        /// <summary>
        /// 급사 대책: 미사일정 엘리트화(체력·피해·경험치·동시 최대 2척) · 웨이브 추첨이 동시 최대 수를 지킴 ·
        /// 선체 35%/20% 아래 붉은 가장자리 맥박·경고 띠 · 큰 한 방 번쩍임 · 수리하면 꺼짐.
        /// </summary>
        private IEnumerator ThreatCheck()
        {
            _report.AppendLine("\n## 미사일 위협·선체 위험 경고");
            var ship = GameManager.Instance.Player;
            var mdef = CombatDevTools.FindEnemy("ene_missileboat");
            var fdef = CombatDevTools.FindEnemy("ene_fastboat");
            var spawner = EnemySpawner.Instance;
            CombatDevTools.ClearBattlefield();
            yield return null;

            // 1) 데이터
            _report.AppendLine($"- 미사일정: 등급 {mdef.Rank} · 체력 {mdef.MaxHp} · 미사일 {mdef.AttackDamage}×{mdef.SalvoSize}발/{mdef.AttackCooldown}초 · 경험치 {mdef.XpReward} · 동시 최대 {mdef.MaxAlive}척");
            if (mdef.Rank != Game.Data.EnemyRank.Elite || mdef.MaxAlive != 2 || mdef.AttackDamage <= 18f || mdef.MaxHp <= 22f)
                Fail("미사일정이 엘리트(체력·피해 상향, 동시 최대 2척)로 바뀌지 않음");

            // 2) 웨이브 추첨: 동시 최대 수에 걸린 적은 뽑히지 않는다
            var round = new Game.Data.RoundSet.Round
            {
                Entries = new List<Game.Data.RoundSet.SpawnEntry>
                {
                    new() { Enemy = mdef, Weight = 5f },
                    new() { Enemy = fdef, Weight = 1f },
                },
            };
            System.Func<Game.Data.EnemyDefinition, bool> allowed = d => (bool)Call(spawner, "UnderAliveCap", d);
            int PickMissile() { int n = 0; for (int i = 0; i < 300; i++) if (Game.Data.RoundSet.PickWeighted(round, allowed) == mdef) n++; return n; }
            int free = PickMissile();
            var boats = CombatDevTools.SpawnRing("ene_missileboat", 2, 70f, 90f);
            foreach (var b in boats) b.DevFrozen = true;
            yield return null;
            int capped = PickMissile();
            _report.AppendLine($"- 웨이브 추첨 300회(미사일정 가중치 5 : 고속정 1): 미사일정 0척일 때 {free}회 → 2척 살아 있을 때 {capped}회");
            if (free < 200 || capped != 0) Fail("웨이브 추첨이 미사일정 동시 최대 수를 지키지 않음");
            CombatDevTools.ClearBattlefield();

            // 3) 어뢰 잠수함 사격 패턴: 너무 가까우면 쏘지 않고, 전체 동시 어뢰 수를 제한한다
            yield return SubmarineCheck();

            // 4) 엘리트 표식(월드 · 레이더)
            yield return EliteMarkerCheck();

            // 5) 선체 위험 경고
            var overlay = Game.UI.HullDangerOverlay.Instance;
            if (overlay == null) { Fail("HullDangerOverlay 없음"); yield break; }
            ship.RepairHull(ship.HullMaxHp);
            yield return new WaitForSeconds(0.6f);
            int level0 = overlay.Level; float alpha0 = overlay.VignetteAlpha;
            int hits0 = overlay.BigHitCount;
            ship.ApplyHullDamage(ship.HullHp - ship.HullMaxHp * 0.30f);   // 한 번에 70% → 큰 한 방 + 위험
            float flashAlpha = 0f;
            for (int f = 0; f < 6; f++) { yield return null; flashAlpha = Mathf.Max(flashAlpha, overlay.VignetteAlpha); }
            yield return new WaitForSecondsRealtime(1.2f);
            float dangerMax = 0f;
            for (float t = 0f; t < 1.2f; t += Time.unscaledDeltaTime) { dangerMax = Mathf.Max(dangerMax, overlay.VignetteAlpha); yield return null; }
            int level1 = overlay.Level; bool banner1 = overlay.BannerVisible;
            yield return ScreenShot("hull_danger");
            ship.ApplyHullDamage(ship.HullHp - ship.HullMaxHp * 0.15f);
            yield return new WaitForSecondsRealtime(0.6f);
            float critMax = 0f;
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) { critMax = Mathf.Max(critMax, overlay.VignetteAlpha); yield return null; }
            int level2 = overlay.Level;
            yield return ScreenShot("hull_critical");
            ship.RepairHull(ship.HullMaxHp);
            yield return new WaitForSecondsRealtime(0.6f);
            _report.AppendLine($"- 선체 경고: 정상 단계 {level0}·붉은빛 {alpha0:0.00} → 70% 한 방: 번쩍임 {flashAlpha:0.00}(큰 피해 {overlay.BigHitCount - hits0}회) · 30% 단계 {level1}·최대 {dangerMax:0.00}·경고 띠 {Yes(banner1)} → " +
                               $"15% 단계 {level2}·최대 {critMax:0.00} → 수리 후 단계 {overlay.Level}·{overlay.VignetteAlpha:0.00}·경고 띠 {Yes(overlay.BannerVisible)}");
            if (level0 != 0 || alpha0 > 0.01f) Fail("선체가 멀쩡한데 경고가 보임");
            if (overlay.BigHitCount - hits0 < 1 || flashAlpha < 0.5f) Fail("큰 한 방에 붉은 번쩍임이 없음");
            if (level1 != 1 || !banner1 || dangerMax < 0.3f) Fail("선체 35% 아래 위험 경고가 보이지 않음");
            if (level2 != 2 || critMax <= dangerMax) Fail("선체 20% 아래 치명 경고가 더 강하지 않음");
            if (overlay.Level != 0 || overlay.VignetteAlpha > 0.01f || overlay.BannerVisible) Fail("수리한 뒤에도 경고가 남음");
        }

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

        // ------------------------------------------------------------ 편대(카드 · 생존성 · 자율 능력)

        /// <summary>
        /// 편대 개편: 출항 편성 없음 · 편대 배치 카드(첫 보장 레벨 3) · 역할 지정 카드(역할 넷 선택, 취소 가능) · 개량 카드(모델·축척·선체) ·
        /// 최대 4척 · 측후방 위치 · 편대 현황 패널 · 적 표적 선택(우선도 0.4)·공격자 2척 제한·미사일정은 기함만 · 적 탄 피해 ·
        /// 전투 불능(표적·판정 해제, 능력 정지, 이탈, 보상 없음, 적 재조준) · 복귀 · 정비 수리 ·
        /// 자율 능력(함포 · 방공 요격 · 전자전 교란 · 대잠 자동 타격 · 미사일 대함 타격) · 전술 리그.
        /// </summary>
        private IEnumerator EscortCheck()
        {
            _report.AppendLine("\n## 편대(카드 · 생존성 · 자율 능력)");
            var ship = GameManager.Instance.Player;
            var formation = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
            var feedback = Object.FindFirstObjectByType<Game.TaskForce.TaskForceWorldFeedback>();
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            var draft = Object.FindFirstObjectByType<Game.Refit.RefitDraft>(FindObjectsInactive.Include);
            if (formation == null || refit == null || draft == null) { Fail("편대·정비 컴포넌트 없음"); yield break; }
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = false;
            yield return new WaitForSeconds(0.5f);

            string ModelKey(Component c) { var key = c != null ? Get<string>(c, "_modelKey") : null; return string.IsNullOrEmpty(key) ? "(회색박스)" : key; }
            float Scale(Component c) => c != null ? c.transform.Find("Escort visual").localScale.x : 0f;
            string Name(Transform t) => t != null ? t.name : "-";
            bool Listed(Component c) { foreach (var t in TargetRegistry.HostileTo(CombatFaction.Hostile, TargetKind.Surface)) if (ReferenceEquals(t, c)) return true; return false; }

            // 1) 출항 편성 없음 · 처음엔 편대가 비어 있다 · 레벨 3부터 편대 배치 카드 보장
            bool noPreflight = GameObject.Find("Force package overlay") == null;
            bool emptyStart = formation.EscortCount == 0 && Game.UI.TaskForcePanelUI.VisiblePanel == null;
            int guaranteed = 0, earlyFleet = 0;
            for (int i = 0; i < 40; i++)
            {
                var c3 = draft.DrawCards(3, 3);
                if (c3.Count == 3 && c3[2].Kind == Game.Refit.RefitCardKind.FleetDeploy) guaranteed++;
                var c2 = draft.DrawCards(3, 2);
                foreach (var c in c2) if (c.Kind == Game.Refit.RefitCardKind.FleetDeploy) earlyFleet++;
            }
            _report.AppendLine($"- 시작: 출항 편성 화면 없음 {Yes(noPreflight)} · 편대 비어 있음 {Yes(emptyStart)} · 레벨 3 카드 40회 중 3번 자리 편대 배치 {guaranteed}회 · 레벨 2 편대 배치 {earlyFleet}회(무작위)");
            if (!noPreflight || !emptyStart) Fail("출항 편성이 남아 있거나 시작부터 편대가 있음");
            if (guaranteed != 40) Fail("편대가 비었을 때 레벨 3 편대 배치 카드가 보장되지 않음");

            // 2) 편대 배치 카드 → 역할 없는 고속정 합류
            GameManager.Instance.SetState(GameState.Refit);
            var filler1 = Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar"));
            var filler2 = Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Common);
            refit.Open(3, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetDeploy() }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_1_deploy_card");
            Call(refit, "ChooseCard", 2);
            // 편대 슬롯 화면(2026-10-03): 블록 배치 화면 대신 슬롯 1~4가 뜨고, 빈 슬롯을 고른다
            bool slotScreen = Get<bool>(refit, "_fleetPicking") && Get<bool>(refit, "_fleetDeployMode") && formation.EscortCount == 0;
            yield return null;
            yield return RefitShot(refit, "fleet_1b_slot_screen");
            Call(refit, "ChooseFleetSlot", 2);   // 3번 슬롯
            bool resultScreen = Get<bool>(refit, "_fleetDone");
            yield return null;
            yield return RefitShot(refit, "fleet_1c_slot_result");
            var pb = formation.GetEscort(0);
            string pbModel = ModelKey(pb);
            var pbInfo = formation.GetInfo(0);
            string deployText = Get<string>(refit, "_fleetResult");
            _report.AppendLine($"- 편대 배치 카드: 슬롯 화면 {Yes(slotScreen)} · 결과 화면 {Yes(resultScreen)} · {formation.EscortCount}척 · {pbInfo.Name}(슬롯 {pbInfo.RosterSlot + 1}) · 역할 {pbInfo.Role} · 모델 {pbModel} · 축척 {Scale(pb):0.00} · 결과 \"{deployText}\"");
            if (!slotScreen || !resultScreen) Fail("편대 배치 카드가 편대 슬롯 화면을 거치지 않음");
            if (formation.EscortCount != 1 || pbInfo.RosterSlot != 2 || pbInfo.Role != Game.TaskForce.EscortRole.None || !pbModel.StartsWith("ESC_PB") || !Mathf.Approximately(Scale(pb), 0.60f))
                Fail("편대 배치 카드로 고른 슬롯에 역할 없는 고속정이 합류하지 않음");
            refit.Open(4, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetDeploy() }, () => { });
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);   // 이미 찬 슬롯 — 무시돼야 한다
            bool occupiedRefused = formation.EscortCount == 1 && Get<bool>(refit, "_fleetPicking");
            Call(refit, "ChooseFleetSlot", 0);   // 1번 슬롯
            _report.AppendLine($"- 슬롯: 찬 슬롯 거부 {Yes(occupiedRefused)} · 1번 슬롯 합류 {Yes(formation.EscortCount == 2 && formation.GetInfo(1).RosterSlot == 0)} · 이름 {formation.GetInfo(0).Name}/{formation.GetInfo(1).Name}");
            if (!occupiedRefused || formation.GetInfo(1).RosterSlot != 0) Fail("편대 슬롯 선택이 찬 슬롯을 막지 않거나 고른 슬롯에 들어가지 않음");

            // 3) 편대 강화(역할 없음) → 역할 선택 패널 · 취소하면 카드로 · 고르면 그 역할의 기본형
            refit.Open(5, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade(1) }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_2_role_card");
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 0);   // 1번 슬롯 = 두 번째로 합류한 고속정
            bool picking = Get<bool>(refit, "_rolePicking");
            yield return null;
            yield return RefitShot(refit, "fleet_3_role_picker");
            Call(refit, "BackToCards");
            bool cancelled = Get<bool>(refit, "_choosing") && !Get<bool>(refit, "_rolePicking") && !Get<bool>(refit, "_fleetPicking") && formation.GetInfo(1).Role == Game.TaskForce.EscortRole.None;
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 0);
            Call(refit, "ChooseRole", 2);   // 전자전
            refit.Open(5, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade() }, () => { });
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);   // 3번 슬롯 = 처음 합류한 고속정
            Call(refit, "ChooseRole", 0);   // 방공
            var cap = formation.GetEscort(Game.TaskForce.EscortRole.AirDefense);
            var ew = formation.GetEscort(Game.TaskForce.EscortRole.ElectronicWarfare);
            _report.AppendLine($"- 역할 지정 카드: 선택 패널 {Yes(picking)} · 취소 → 카드로·역할 그대로 {Yes(cancelled)} · 0번 → {formation.GetInfo(0).Name}({ModelKey(cap)}) · 1번 → {formation.GetInfo(1).Name}({ModelKey(ew)})");
            if (!picking || !cancelled) Fail("역할 지정 카드의 선택·취소 흐름이 동작하지 않음");
            if (cap == null || ew == null || ModelKey(cap) != "ESC_CAP_T0" || ModelKey(ew) != "ESC_EW_T0") Fail("역할 지정 뒤 그 역할의 기본형 모델이 붙지 않음");

            // 4) 편대 강화(개량): 모델·축척·선체 성장
            refit.Open(6, new List<Game.Refit.RefitCard> { filler1, filler2, Game.Refit.RefitCard.FleetUpgrade(0) }, () => { });
            yield return null;
            yield return RefitShot(refit, "fleet_4_upgrade_card");
            Call(refit, "ChooseCard", 2);
            Call(refit, "ChooseFleetSlot", 2);
            var capInfo = formation.GetInfo(0);
            _report.AppendLine($"- 개량 카드: {capInfo.Name} 개량 {capInfo.Tier} · 모델 {ModelKey(cap)} · 축척 {Scale(cap):0.00} · 선체 최대 {Game.TaskForce.TaskForceEscortFormation.MaxHullFor(capInfo.Tier):0}");
            if (capInfo.Tier != 1 || ModelKey(cap) != "ESC_CAP_T1" || !Mathf.Approximately(Scale(cap), 0.76f)) Fail("개량 카드가 모델·축척을 올리지 않음");

            // 5) 강화 대상 고르기·최대 4척
            int third = formation.Deploy();
            int pickNew = formation.PickUpgradeTarget();
            formation.AssignRole(third, Game.TaskForce.EscortRole.SurfaceStrike);
            int fourth = formation.Deploy();
            formation.AssignRole(fourth, Game.TaskForce.EscortRole.AntiSubmarine);
            int fifth = formation.Deploy();
            int fullDeploy = 0;
            for (int i = 0; i < 200; i++) foreach (var c in draft.DrawCards(3, 8)) if (c.Kind == Game.Refit.RefitCardKind.FleetDeploy) fullDeploy++;
            _report.AppendLine($"- 강화 대상: 역할 없는 3번 고속정을 먼저 {Yes(pickNew == third)} · 최대 {Game.TaskForce.TaskForceEscortFormation.MaxEscorts}척에서 더 배치 {(fifth < 0 ? "거부" : "허용")} · 가득 찬 뒤 편대 배치 카드 {fullDeploy}장");
            if (pickNew != third) Fail("편대 강화 카드가 역할 없는 고속정을 먼저 고르지 않음");
            if (fifth >= 0 || fullDeploy > 0) Fail("편대가 4척을 넘거나 가득 찼는데 배치 카드가 나옴");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);
            yield return new WaitForSeconds(2.5f);

            // 6) 진형 위치(기본 함대원형진 — 슬롯마다 고정) · 편대 현황 패널 · 진형 선택판
            var stk = formation.GetEscort(Game.TaskForce.EscortRole.SurfaceStrike);
            var asw = formation.GetEscort(Game.TaskForce.EscortRole.AntiSubmarine);
            var sbPos = new StringBuilder();
            bool posOk = true;
            var fext = formation.FlagshipExtent;
            for (int i = 0; i < formation.EscortCount; i++)
            {
                var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                int rs = formation.GetInfo(i).RosterSlot;
                var want = Game.TaskForce.FleetFormations.Slot(formation.Formation, rs, formation.EscortCount, fext.x, fext.y, fext.z);
                sbPos.Append($"{rs + 1}번({lp.x:0},{lp.z:0}/{want.x:0},{want.y:0}) ");
                if (Vector2.Distance(new Vector2(lp.x, lp.z), want) > 6f) posOk = false;
            }
            var panel = Game.UI.TaskForcePanelUI.VisiblePanel;
            yield return ScreenShot("fleet_5_formation_panel");
            _report.AppendLine($"- 위치({Game.TaskForce.FleetFormations.Name(formation.Formation)}, 슬롯(지금/목표)): {sbPos}· 편대 패널 {(panel != null ? "보임" : "없음")} · 진형 선택판 {(Game.UI.FormationSelectorUI.IsVisible ? "보임" : "없음")}");
            if (!posOk) Fail("호위함이 슬롯에 고정된 진형 자리에 있지 않음");
            if (panel == null) Fail("편대 현황 패널이 보이지 않음");
            if (!Game.UI.FormationSelectorUI.IsVisible) Fail("진형 선택판이 보이지 않음");
            yield return CloseShot("fleet_cap_t1", cap.transform, 12f);

            // 7) 표적 선택 · 공격자 2척 제한 · 미사일정은 기함만(자율 무장은 8)·10)에서 따로 본다)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            var fastDef = CombatDevTools.FindEnemy("ene_fastboat");
            var spawner = EnemySpawner.Instance;
            Vector3 Out(Component c) { Vector3 o = c.transform.position - ship.transform.position; o.y = 0f; return o.normalized; }
            Vector3 At(Component c, float d, float side = 0f) { Vector3 o = Out(c); Vector3 pp = c.transform.position + o * d + Vector3.Cross(Vector3.up, o) * side; pp.y = 0f; return pp; }
            var ea = spawner.SpawnAt(fastDef, At(cap, 6f), Quaternion.LookRotation(-Out(cap)));
            var eb = spawner.SpawnAt(fastDef, ship.transform.position + ship.transform.forward * 30f, Quaternion.LookRotation(-ship.transform.forward));
            // 첫 표적 선택(0.4~1.4초 뒤) 결과를 본다 — 끝 시점만 보면 편대가 나아가 호위함이 멀어진 뒤 기함으로 바꾼 것과 섞인다
            float t0 = Time.time;
            bool aOnCap = false, bOnShip = false;
            while (Time.time - t0 < 4f && !(aOnCap && bOnShip))
            {
                yield return null;
                aOnCap |= ea != null && ea.EngagedTarget == cap.transform;
                bOnShip |= eb != null && eb.EngagedTarget == ship.transform;
            }
            _report.AppendLine($"- 표적 선택: 호위함 옆 6m 적 → {(aOnCap ? Name(cap.transform) : Name(ea != null ? ea.EngagedTarget : null))} · 기함 앞 30m 적 → {(bOnShip ? Name(ship.transform) : Name(eb != null ? eb.EngagedTarget : null))}");
            if (!aOnCap) Fail("호위함 바로 옆 적이 호위함을 노리지 않음");
            if (!bOnShip) Fail("기함 가까운 적이 기함을 노리지 않음");
            CombatDevTools.ClearBattlefield();
            yield return null;
            // 적을 멈춰 두고(움직이면 기함 쪽으로 다가가며 표적을 바꾼다) 표적 선택만 직접 돌린다
            var pack = new List<EnemyController>();
            for (int i = 0; i < 4; i++) pack.Add(spawner.SpawnAt(fastDef, At(ew, 4f, (i - 1.5f) * 2.5f), Quaternion.LookRotation(-Out(ew))));
            var mboat = spawner.SpawnAt(CombatDevTools.FindEnemy("ene_missileboat"), At(cap, 4f), Quaternion.LookRotation(-Out(cap)));
            var thinkers = new List<EnemyController>(pack) { mboat };
            foreach (var e in thinkers) if (e != null) e.DevFrozen = true;
            for (int round = 0; round < 3; round++)
                foreach (var e in thinkers)
                {
                    if (e == null) continue;
                    // 기반 클래스(EnemyController)의 private 멤버라 그 타입으로 찾는다
                    typeof(EnemyController).GetField("_retargetAt", Inst)?.SetValue(e, 0f);
                    typeof(EnemyController).GetMethod("UpdateTarget", Inst)?.Invoke(e, null);
                }
            yield return null;
            int onEw = 0;
            foreach (var e in pack) if (e != null && e.EngagedTarget == ew.transform) onEw++;
            _report.AppendLine($"- 공격자 제한: EW 호위함 옆 고속정 4척 중 호위함을 노리는 적 {onEw}척(최대 {Game.TaskForce.TaskForceEscortFormation.EscortMaxAttackers}) · 호위함 옆 미사일정 → {Name(mboat != null ? mboat.EngagedTarget : null)}");
            if (onEw != Game.TaskForce.TaskForceEscortFormation.EscortMaxAttackers) Fail("한 호위함을 노리는 적 수가 제한대로가 아님");
            if (mboat == null || mboat.EngagedTarget != ship.transform) Fail("미사일정이 호위함을 노림(기함만 노려야 함)");

            // 8) 적 포탄 · 전투 불능(이탈·표적 해제·능력 정지·보상 없음·적 재조준) · 복귀 · 정비 수리
            CombatDevTools.ClearBattlefield();
            yield return new WaitForSeconds(0.3f);
            formation.RepairAll();
            int capIdx = 0;
            float max1 = Game.TaskForce.TaskForceEscortFormation.MaxHullFor(1);
            var pcc = CombatDevTools.SpawnRing("ene_pcc_corvette", 1, 80f, 180f);
            foreach (var e in pcc) e.DevFrozen = true;
            GameObject shell = pcc.Count > 0 ? PrivateField<GameObject>(pcc[0], "projectilePrefab") : null;
            float hp0 = formation.GetHull01(capIdx) * max1;
            if (shell != null && PoolManager.Instance != null)
            {
                Vector3 from = cap.transform.position + Out(cap) * 8f + Vector3.up * 1.2f;
                Vector3 dir = (cap.transform.position + Vector3.up * 0.6f - from).normalized;
                var go = PoolManager.Instance.Spawn(shell, from, Quaternion.LookRotation(dir));
                var pr = go != null ? go.GetComponent<Projectile>() : null;
                if (pr != null) pr.Launch(dir, 80f, 5f, DamageSource.Gun);
                yield return new WaitForSeconds(0.6f);
            }
            float hp1 = formation.GetHull01(capIdx) * max1;
            _report.AppendLine($"- 적 포탄 1발: 선체 {hp0:0.#} → {hp1:0.#}");
            if (shell == null || hp0 - hp1 < 4.9f) Fail("적 포탄이 호위함에 맞지 않음");

            CombatDevTools.ClearBattlefield();
            var ec = spawner.SpawnAt(fastDef, At(cap, 4f), Quaternion.LookRotation(-Out(cap)));
            void Think(EnemyController e)
            {
                if (e == null) return;
                typeof(EnemyController).GetField("_retargetAt", Inst)?.SetValue(e, 0f);
                typeof(EnemyController).GetMethod("UpdateTarget", Inst)?.Invoke(e, null);
            }
            if (ec != null) ec.DevFrozen = true;   // 움직이면 기함 쪽으로 다가가며 표적을 바꾼다 — 멈춰 두고 판단만 돌린다
            Think(ec);
            bool ecOnEscort = ec != null && ec.EngagedTarget == cap.transform;
            int kills0 = GameManager.Instance.TotalKills;
            float aftBefore = ship.transform.InverseTransformPoint(cap.transform.position).z;
            var box = cap.GetComponent<BoxCollider>();
            ((IDamageable)cap).TakeDamage(new DamageInfo(999f, cap.transform.position, Vector3.down, DamageSource.Gun));
            yield return null;
            Think(ec);   // 노리던 표적이 사라지면 곧바로 다시 고른다
            yield return null;
            bool retargeted = ec != null && ec.EngagedTarget == ship.transform;
            var capDefense = cap.GetComponent<Game.TaskForce.EscortDefense>();
            bool disabledOk = formation.IsEscortDisabled(capIdx) && !((ITargetable)cap).IsAlive && !Listed(cap) && !box.enabled;
            float recoverLeft = formation.GetRecoverRemaining(capIdx);
            yield return new WaitForSeconds(3f);
            float aftWithdraw = ship.transform.InverseTransformPoint(cap.transform.position).z;
            _report.AppendLine($"- 전투 불능: 표적·판정 해제 {Yes(disabledOk)} · 격침 수 {kills0}→{GameManager.Instance.TotalKills} · 적 재조준 {Name(ec != null ? ec.EngagedTarget : null)} · 기함 뒤 {-aftBefore:0}m → {-aftWithdraw:0}m 이탈 · 복귀까지 {recoverLeft:0}초");
            if (!ecOnEscort) Fail("전투 불능 시험의 적이 호위함을 노리지 않음(시험 무효)");
            if (!disabledOk || formation.DisabledCount != 1) Fail("전투 불능 호위함이 표적·판정에서 빠지지 않음");
            if (GameManager.Instance.TotalKills != kills0) Fail("아군 호위함 손실이 격침 수를 올림");
            if (!retargeted) Fail("호위함을 노리던 적이 기함으로 표적을 바꾸지 않음");
            if (aftWithdraw > aftBefore - 5f) Fail("전투 불능 호위함이 후방으로 이탈하지 않음");
            CombatDevTools.ClearBattlefield();
            yield return ScreenShot("fleet_6_disabled");
            Put(cap, "_recoverAt", Time.time);
            yield return null;
            yield return null;
            bool back = !formation.IsEscortDisabled(capIdx) && ((ITargetable)cap).IsAlive && Listed(cap) && box.enabled;
            float backHull = formation.GetHull01(capIdx);
            ((IDamageable)ew).TakeDamage(new DamageInfo(999f, ew.transform.position, Vector3.down, DamageSource.Gun));
            yield return null;
            GameManager.Instance.SetState(GameState.Refit);
            yield return null;
            float capHull = formation.GetHull01(capIdx), ewHull = formation.GetHull01(1);
            bool ewBack = !formation.IsEscortDisabled(1);
            GameManager.Instance.SetState(GameState.Playing);
            _report.AppendLine($"- 복귀: {Yes(back)} · 선체 {backHull * 100f:0}% · 정비 진입: CAP {capHull * 100f:0}% · 전투 불능 EW 복귀 {Yes(ewBack)} {ewHull * 100f:0}%");
            if (!back || Mathf.Abs(backHull - Game.TaskForce.TaskForceEscortFormation.RecoverHull) > 0.01f) Fail("전투 불능 호위함이 선체 절반으로 복귀하지 않음");
            if (capHull < 1f || ewHull < 1f || !ewBack) Fail("정비 때 호위함이 수리·복귀되지 않음");

            // 9) 자율 능력: 함포 · 방공 요격 · 전자전 교란
            CombatDevTools.ClearBattlefield();
            var capDef = cap.GetComponent<Game.TaskForce.EscortDefense>();
            var ewDef = ew.GetComponent<Game.TaskForce.EscortDefense>();
            // 방공·전자전만 켠다(대잠·미사일 호위함이 시험용 적을 먼저 쏘지 않게 — 10)에서 따로 본다)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) { d.enabled = d == capDef || d == ewDef; d.ResetCooldowns(); }
            yield return new WaitForSeconds(1f);
            int killsG = GameManager.Instance.TotalKills, gun0 = ewDef.GunShots;
            var target = spawner.SpawnAt(fastDef, At(ew, 12f), Quaternion.LookRotation(-Out(ew)));
            if (target != null) target.DevFrozen = true;
            t0 = Time.time;
            while (Time.time - t0 < 6f && target != null && target.IsAlive) yield return null;
            float gunTime = Time.time - t0;
            bool gunKill = target != null && !target.IsAlive && GameManager.Instance.TotalKills > killsG;
            var mdef2 = CombatDevTools.FindEnemy("ene_missileboat");
            int sam0 = capDef.SamKills, jam0 = ewDef.Jams;
            Missile samMissile = null, jamMissile = null;
            if (mdef2 != null && mdef2.MissilePrefab != null && PoolManager.Instance != null)
            {
                Vector3 from = cap.transform.position + Out(cap) * 26f + Vector3.up * 3f;
                var go = PoolManager.Instance.Spawn(mdef2.MissilePrefab, from, Quaternion.LookRotation(ship.transform.position - from));
                samMissile = go != null ? go.GetComponent<Missile>() : null;
                if (samMissile != null) samMissile.Launch(ship.transform, 26f);
                Vector3 from2 = ew.transform.position + Out(ew) * 26f + Vector3.up * 3f;
                var go2 = PoolManager.Instance.Spawn(mdef2.MissilePrefab, from2, Quaternion.LookRotation(ship.transform.position - from2));
                jamMissile = go2 != null ? go2.GetComponent<Missile>() : null;
                if (jamMissile != null) jamMissile.Launch(ship.transform, 26f);
            }
            t0 = Time.time;
            while (Time.time - t0 < 8f && ((samMissile != null && samMissile.IsAlive) || (jamMissile != null && jamMissile.IsAlive))) yield return null;
            yield return null;
            _report.AppendLine($"- 함포·방공·교란: EW 함포 → 12m 고속정 격침 {Yes(gunKill)} ({gunTime:0.0}초, {ewDef.GunShots - gun0}발) · 방공 요격 {capDef.SamKills - sam0}발 · 전자전 교란 {ewDef.Jams - jam0}회");
            if (!gunKill) Fail("호위함 함포가 가까운 적을 격침하지 못함");
            if (capDef.SamKills - sam0 < 1) Fail("방공 호위함이 기함을 노리는 미사일을 요격하지 않음");
            if (ewDef.Jams - jam0 < 1) Fail("전자전 호위함이 가까운 미사일을 교란하지 않음");
            CombatDevTools.ClearBattlefield();

            // 10) 자율 능력: 대잠 자동 타격(잠항 중) · 미사일 대함 타격(가치 높은 표적 우선) · 전술 리그
            int rigs0 = feedback != null ? feedback.SpawnedRigCount : 0;
            var aswDef = asw.GetComponent<Game.TaskForce.EscortDefense>();
            var stkDef = stk.GetComponent<Game.TaskForce.EscortDefense>();
            // 모두 끄고, 대잠·미사일 호위함은 함포를 막은 채 역할 능력만 켠다(적을 띄운 뒤)
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            aswDef.ResetCooldowns();
            stkDef.ResetCooldowns();
            Put(aswDef, "_gunAt", Time.time + 999f);
            Put(stkDef, "_gunAt", Time.time + 999f);
            int asw0 = aswDef.AswStrikes, stk0 = stkDef.SurfaceStrikes;
            var subs = new List<EnemyController> { spawner.SpawnAt(CombatDevTools.FindEnemy("ene_submarine"), At(asw, 14f), Quaternion.LookRotation(-Out(asw))) };
            foreach (var e in subs) if (e != null) e.DevFrozen = true;
            var sub = subs[0] as SubmarineBase;
            float subHp0 = sub != null ? sub.CurrentHp : 0f;
            bool hiddenAtStart = sub != null && !sub.IsRevealed;
            var cheap = spawner.SpawnAt(fastDef, At(stk, 22f, 6f), Quaternion.LookRotation(-Out(stk)));
            var valuable = spawner.SpawnAt(CombatDevTools.FindEnemy("ene_pcc_corvette"), At(stk, 40f, -6f), Quaternion.LookRotation(-Out(stk)));
            if (cheap != null) cheap.DevFrozen = true;
            if (valuable != null) valuable.DevFrozen = true;
            float pccHp0 = valuable != null ? valuable.CurrentHp : 0f, cheapHp0 = cheap != null ? cheap.CurrentHp : 0f;
            aswDef.enabled = true;
            stkDef.enabled = true;
            t0 = Time.time;
            while (Time.time - t0 < 3f && (aswDef.AswStrikes == asw0 || stkDef.SurfaceStrikes == stk0)) yield return null;
            yield return new WaitForSeconds(0.4f);
            float subLoss = sub != null ? subHp0 - sub.CurrentHp : 0f;
            float pccLoss = valuable != null ? pccHp0 - valuable.CurrentHp : 0f;
            float cheapLoss = cheap != null ? cheapHp0 - cheap.CurrentHp : 0f;
            bool contact = sub != null && sub.IsContactConfirmed;
            int rigs1 = feedback != null ? feedback.SpawnedRigCount : 0;
            yield return ScreenShot("fleet_7_abilities");
            _report.AppendLine($"- 대잠 자동 타격: {aswDef.AswStrikes - asw0}회 · 잠항 중 잠수함 {Yes(hiddenAtStart)} · 피해 {subLoss:0}(기대 {Game.TaskForce.EscortDefense.AswDamage(0):0}) · 접촉 확정 {Yes(contact)} / 대함 타격: {stkDef.SurfaceStrikes - stk0}회 · 초계함(엘리트) 피해 {pccLoss:0}(기대 {Game.TaskForce.EscortDefense.StrikeDamage(0):0}) · 가까운 고속정 피해 {cheapLoss:0} · 전술 리그 {rigs1 - rigs0}개");
            if (aswDef.AswStrikes - asw0 < 1 || subLoss < Game.TaskForce.EscortDefense.AswDamage(0) - 0.5f || !contact) Fail("대잠 호위함이 잠항 잠수함을 자동 타격하지 않음");
            if (stkDef.SurfaceStrikes - stk0 < 1 || pccLoss < Game.TaskForce.EscortDefense.StrikeDamage(0) - 0.5f || cheapLoss > 0f) Fail("미사일 호위함이 가치 높은 표적을 먼저 타격하지 않음");
            if (rigs1 - rigs0 < 2) Fail("자율 능력 발동 때 Codex 전술 리그가 펼쳐지지 않음");

            // 11) 포탑 회전 · 레이더 회전 · 발사음(Redesign 모델의 가동부)
            CombatDevTools.ClearBattlefield();
            foreach (var d in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) d.enabled = false;
            yield return new WaitForSeconds(0.3f);
            string RigText(Component c)
            {
                var rg = c.GetComponentInChildren<Game.TaskForce.EscortTurrets>();
                if (rg == null) return $"{c.name}: 가동부 없음";
                return $"{ModelKey(c)} 함포 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Gun)}·함대공 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Sam)}·대함 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Strike)}·기만 {rg.CountOf(Game.TaskForce.EscortTurrets.MountKind.Decoy)}·레이더 {rg.SpinnerCount}";
            }
            _report.AppendLine($"- 가동부: {RigText(cap)} / {RigText(ew)} / {RigText(asw)} / {RigText(stk)}");
            var ewRig = ew.GetComponentInChildren<Game.TaskForce.EscortTurrets>();
            if (ewRig == null || ewRig.CountOf(Game.TaskForce.EscortTurrets.MountKind.Gun) == 0) Fail("호위함 모델에서 함포 포탑을 찾지 못함");
            if (cap.GetComponentInChildren<Game.TaskForce.EscortTurrets>() is { } capRig && capRig.CountOf(Game.TaskForce.EscortTurrets.MountKind.Sam) == 0) Fail("방공 호위함(T1)에서 함대공 발사기를 찾지 못함");
            if (ewRig != null)
            {
                // 레이더 회전
                var spinner = ewRig.FirstSpinner;
                var q0 = spinner != null ? spinner.rotation : Quaternion.identity;
                yield return new WaitForSeconds(0.5f);
                float spun = spinner != null ? Quaternion.Angle(q0, spinner.rotation) : 0f;
                // 함포 선회: 호위함 옆(현측 90°) 14m에 고정 표적
                Vector3 side = ew.transform.right; side.y = 0f; side.Normalize();
                var aimTarget = spawner.SpawnAt(fastDef, ew.transform.position + side * 14f, Quaternion.LookRotation(-side));
                if (aimTarget != null) aimTarget.DevFrozen = true;
                ewDef.ResetCooldowns();
                int shots0 = ewDef.GunShots;
                ewDef.enabled = true;
                t0 = Time.time;
                while (Time.time - t0 < 1.5f && ewDef.GunShots == shots0) yield return null;
                yield return new WaitForSeconds(0.3f);
                float turned = ewRig.MaxTurn(Game.TaskForce.EscortTurrets.MountKind.Gun);
                float aimErr = aimTarget != null ? ewRig.BestAimError(Game.TaskForce.EscortTurrets.MountKind.Gun, aimTarget.transform.position) : 999f;
                yield return CloseShot("fleet_8_turret_aim", ew.transform, 9f);
                ewDef.enabled = false;
                CombatDevTools.ClearBattlefield();
                yield return new WaitForSeconds(2.5f);   // 쏠 일이 없으면 제자리로
                float rest = ewRig.MaxTurn(Game.TaskForce.EscortTurrets.MountKind.Gun);
                _report.AppendLine($"- 포탑: 현측 표적에 함포 {turned:0}° 선회 · 포신 오차 {aimErr:0}° · 첫 발 {ewDef.GunShots - shots0}발 / 표적이 사라지면 제자리까지 {rest:0}° · 레이더 0.5초에 {spun:0}° 회전");
                if (turned < 45f) Fail("호위함 함포가 표적 쪽으로 돌지 않음");
                if (aimErr > 15f) Fail("호위함 함포가 표적을 정확히 향하지 않음");
                if (ewDef.GunShots - shots0 < 1) Fail("포탑을 돌린 뒤 함포를 쏘지 않음");
                if (rest > 10f) Fail("표적이 사라진 뒤 포탑이 제자리로 돌아오지 않음");
                if (spinner != null && spun < 10f) Fail("호위함 레이더가 돌지 않음");
            }
            // 발사음: 이 검사에서 쓴 호위함 효과음이 실제로 재생되었는가(AudioManager 최근 재생 기록)
            var audio = AudioManager.Instance;
            var played = audio != null ? Get<Dictionary<Game.Data.SfxId, float>>(audio, "_lastPlayed") : null;
            var sfxIds = new[] { Game.Data.SfxId.EscortGunShot, Game.Data.SfxId.EscortMissileLaunch, Game.Data.SfxId.EscortTorpedoLaunch, Game.Data.SfxId.EscortJam };
            var heard = new List<string>();
            var silent = new List<string>();
            foreach (var id in sfxIds) (played != null && played.ContainsKey(id) ? heard : silent).Add(id.ToString());
            _report.AppendLine($"- 발사음: 재생됨 [{string.Join(", ", heard)}]" + (silent.Count > 0 ? $" · 안 남 [{string.Join(", ", silent)}]" : ""));
            if (audio == null) _report.AppendLine("  - 참고: AudioManager 없음(소리 검사 생략)");
            else if (silent.Count > 0) Fail("호위함 발사음이 재생되지 않음");

            // 정리: 편대를 비우고(다른 검사에 끼어들지 않게) 무장을 되돌린다
            formation.ClearAll();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        // ------------------------------------------------------------ 진영(아군/적)

        /// <summary>검증용 가짜 유닛: 진영·종류를 정할 수 있는 표적 겸 피해 대상(받은 피해를 센다).</summary>
        private sealed class FactionProbe : MonoBehaviour, ITargetable, IDamageable
        {
            public CombatFaction ProbeFaction;
            public float Received;
            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public bool IsAlive => isActiveAndEnabled;
            public bool IsRevealed => true;
            public CombatFaction Faction => ProbeFaction;
            public void TakeDamage(in DamageInfo info) => Received += info.Amount;

            public static FactionProbe Create(string name, CombatFaction faction, Vector3 at, int layer)
            {
                var go = new GameObject(name);
                go.layer = layer;
                go.transform.position = at;
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(2.2f, 2f, 5f);
                box.center = new Vector3(0f, 0.5f, 0f);
                var p = go.AddComponent<FactionProbe>();
                p.ProbeFaction = faction;
                TargetRegistry.Register(p);
                return p;
            }

            private void OnDestroy() => TargetRegistry.Unregister(this);
        }

        /// <summary>
        /// 진영 구분(작업 A): 표적 등록소 진영별 조회 · 아군(가짜 호위함)은 플레이어 무기·센서·경고에서 빠짐 ·
        /// 적은 기함과 아군을 적대 대상으로 봄 · 아군 탄은 아군 몸체에 피해 없음(레이어가 맞아도) · 보상은 적대 진영만 ·
        /// 기함이 자기 레이더·무기의 표적이 되지 않음.
        /// </summary>
        private IEnumerator FactionCheck()
        {
            _report.AppendLine("\n## 진영(아군/적) 구분");
            var ship = GameManager.Instance.Player;
            var targeting = ship.GetComponentInChildren<TargetingSystem>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            // 앞 검사가 무장을 켠 채 끝나도 시험용 적을 먼저 쏘지 않게(등록소·탐지 목록만 본다). 2)에서 따로 켠다.
            DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            yield return new WaitForSeconds(0.5f);

            // 1) 등록소
            var enemy = CombatDevTools.SpawnRing("ene_fastboat", 1, 18f, 90f);
            foreach (var e in enemy) e.DevFrozen = true;
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            var friendly = FactionProbe.Create("Friendly escort probe", CombatFaction.Player, ship.transform.position + fwd * 12f, Factions.PlayerShipLayer);
            yield return new WaitForSeconds(0.8f);   // 탐지 갱신

            bool Contains(IReadOnlyList<ITargetable> list, object o) { foreach (var t in list) if (ReferenceEquals(t, o)) return true; return false; }
            var hostileToPlayer = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            var hostileToEnemy = TargetRegistry.HostileTo(CombatFaction.Hostile, TargetKind.Surface);
            bool enemyListed = enemy.Count > 0 && Contains(hostileToPlayer, enemy[0]);
            bool shipExcluded = !Contains(hostileToPlayer, ship), probeExcluded = !Contains(hostileToPlayer, friendly);
            bool enemySeesShip = Contains(hostileToEnemy, ship), enemySeesProbe = Contains(hostileToEnemy, friendly);
            bool detectedProbe = targeting != null && Contains(targeting.DetectedSurface, friendly);
            bool detectedShip = targeting != null && Contains(targeting.DetectedSurface, ship);
            bool detectedEnemy = targeting != null && enemy.Count > 0 && Contains(targeting.DetectedSurface, enemy[0]);
            string factionDiag = enemy.Count > 0 ? $"적 생존 {enemy[0].IsAlive} · 체력 {enemy[0].CurrentHp:0.#} · 활성 {enemy[0].isActiveAndEnabled} · 적 목록 {hostileToPlayer.Count}개" : "적 스폰 실패";
            _report.AppendLine($"- 등록소({factionDiag}): 플레이어의 적 목록에 적 {Yes(enemyListed)} · 기함 {Yes(!shipExcluded)} · 아군 {Yes(!probeExcluded)} / 적의 적 목록에 기함 {Yes(enemySeesShip)} · 아군 {Yes(enemySeesProbe)}");
            _report.AppendLine($"- 함선 탐지(레이더): 적 {Yes(detectedEnemy)} · 아군 {Yes(detectedProbe)} · 자함 {Yes(detectedShip)}");
            if (!enemyListed || !shipExcluded || !probeExcluded) Fail("플레이어의 적 목록에 아군이 섞이거나 적이 빠짐");
            if (!enemySeesShip || !enemySeesProbe) Fail("적이 기함·아군을 적대 대상으로 보지 못함");
            if (detectedProbe || detectedShip || !detectedEnemy) Fail("탐지 목록에 아군·자함이 들어가거나 적이 빠짐");

            // 2) 무기를 켜 두어도 아군은 맞지 않는다(적은 없애고 아군만 둔다)
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            // 1)에서 적에게 쏜 탄이 아직 날고 있을 수 있다 — 다 사라진 뒤부터 잰다
            float wait0 = Time.time;
            while (ActiveCount("PRJ_") > 0 && Time.time - wait0 < 5f) yield return null;
            friendly.Received = 0f;
            float t0 = Time.time; int maxShells = 0;
            while (Time.time - t0 < 4f) { maxShells = Mathf.Max(maxShells, ActiveCount("PRJ_")); yield return null; }
            _report.AppendLine($"- 무장 켬 · 아군만 12m 앞: 날아간 포탄 최대 {maxShells}발 · 아군이 받은 피해 {friendly.Received:0.#}");
            if (friendly.Received > 0f || maxShells > 0) Fail("아군을 향해 사격함");

            // 3) 아군 탄은 아군 몸체에 피해를 주지 않는다 — 레이어를 적(Enemy)으로 바꿔 물리 충돌은 일어나게 하고 진영으로만 막히는지 본다
            var ac = CombatDevTools.FindModule("mod_autocannon");
            GameObject shell = null;
            foreach (var r in Object.FindObjectsByType<Game.Modules.Runtime.AutocannonModule>(FindObjectsSortMode.None))
            { shell = PrivateField<GameObject>(r, "projectilePrefab"); if (shell != null) break; }
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = false;
            var right = Vector3.Cross(Vector3.up, fwd);
            var ally = FactionProbe.Create("Ally on enemy layer", CombatFaction.Player, ship.transform.position + right * 20f + Vector3.up * 0.5f, Factions.EnemyLayer);
            var foe = FactionProbe.Create("Foe on enemy layer", CombatFaction.Hostile, ship.transform.position - right * 20f + Vector3.up * 0.5f, Factions.EnemyLayer);
            TargetRegistry.Unregister(ally); TargetRegistry.Unregister(foe);   // 무기가 스스로 노리지 않게(탄만 쏜다)
            Physics.SyncTransforms();
            CombatFaction owner = CombatFaction.Neutral;
            if (shell != null && PoolManager.Instance != null)
            {
                foreach (var (target, name) in new[] { (ally, "아군"), (foe, "적") })
                {
                    Vector3 from = ship.transform.position + Vector3.up * 1.5f + (target.transform.position - ship.transform.position).normalized * 3f;
                    Vector3 dir = (target.transform.position + Vector3.up * 0.5f - from).normalized;
                    var go = PoolManager.Instance.Spawn(shell, from, Quaternion.LookRotation(dir));
                    var p = go != null ? go.GetComponent<Projectile>() : null;
                    if (p == null) continue;
                    owner = p.Owner;
                    p.Launch(dir, 80f, 5f, DamageSource.Gun);
                }
                yield return new WaitForSeconds(0.8f);
            }
            _report.AppendLine($"- 아군 탄(소유 {owner})을 Enemy 레이어에 둔 아군·적에 쏨: 아군 피해 {ally.Received:0.#} · 적 피해 {foe.Received:0.#}");
            if (shell == null) Fail("기관포 탄 프리팹을 찾지 못함");
            if (owner != CombatFaction.Player) Fail("기관포 탄의 소유 진영이 Player가 아님");
            if (ally.Received > 0f) Fail("아군 탄이 아군 몸체에 피해를 줌");
            if (foe.Received <= 0f) Fail("대조군(적) 몸체가 피해를 받지 않음(시험 무효)");

            // 4) 보상은 적대 진영만
            int kills0 = GameManager.Instance.TotalKills;
            var victim = CombatDevTools.SpawnRing("ene_fastboat", 1, 25f, 200f);
            yield return null;
            foreach (var v in victim) v.TakeDamage(new DamageInfo(999f, v.transform.position, Vector3.down, DamageSource.Gun));
            int kills1 = GameManager.Instance.TotalKills;
            Object.Destroy(friendly.gameObject);
            yield return null;
            int kills2 = GameManager.Instance.TotalKills;
            bool rules = Factions.GrantsReward(foe) && !Factions.GrantsReward(ally) && !Factions.GrantsReward(ship);
            _report.AppendLine($"- 보상: 적 격침 → 격침 수 {kills0} → {kills1} · 아군 소실 → {kills1} → {kills2} · 규칙(적만 보상) {Yes(rules)}");
            if (kills1 != kills0 + 1 || kills2 != kills1 || !rules) Fail("보상이 적대 진영에만 주어지지 않음");

            Object.Destroy(ally.gameObject);
            Object.Destroy(foe.gameObject);
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        private static string Yes(bool v) => v ? "예" : "아니오";

        // ------------------------------------------------------------ UI 전체 화면

        /// <summary>
        /// UI 점검용 전체 화면 캡처: 전투 HUD(적·미사일 경고 포함), Tab 현황판, 정비 카드(모듈·강화·호위함 개량), 설치/강화 선택.
        /// 규칙 검사는 겹침(카드 위로 비치는 정비 문구)과 전투 중 불필요한 전투단 수치 표시만 본다.
        /// </summary>
        private IEnumerator UiShotsCheck()
        {
            _report.AppendLine("\n## UI 전체 화면");
            var ship = GameManager.Instance.Player;
            var off = DisableWeapons(null);
            CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            CombatDevTools.SpawnRing("ene_fastboat", 3, 30f, 20f);
            CombatDevTools.SpawnRing("ene_missileboat", 1, 40f, 200f);
            yield return new WaitForSeconds(2f);
            yield return ScreenShot("ui_combat");

            var tab = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (tab != null)
            {
                tab.SetOpen(true);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return ScreenShot("ui_tab");
                tab.SetOpen(false);
            }
            CombatDevTools.ClearBattlefield();

            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            if (refit != null)
            {
                GameManager.Instance.SetState(GameState.Refit);
                refit.Open(7, new List<ModuleDefinition> { CombatDevTools.FindModule("mod_autocannon"), CombatDevTools.FindModule("mod_gun76"), CombatDevTools.FindModule("mod_sam") }, () => { });
                yield return null;
                yield return ScreenShot("ui_refit_cards");
                Call(refit, "ChooseCard", 0);
                yield return null;
                yield return ScreenShot("ui_refit_choice");
                Call(refit, "BackToCards");
                Call(refit, "ChooseCard", 1);
                yield return null;
                yield return ScreenShot("ui_refit_place");
                refit.Close();
                GameManager.Instance.SetState(GameState.Playing);
            }
            foreach (var d in off) if (d != null) d.enabled = true;
        }

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

        /// <summary>
        /// 블록 강화: 강화 데이터 · 카드 흐름(설치/강화 선택·취소·설치) · 블록마다 다른 단계 · 재배치 유지 · 새 블록 0 ·
        /// 최종 스탯 한 번만(기관포·76mm·CIWS 실제 발사) · 탄약고 지원 중복 없음 · 외형 단계·피벗 재연결 · 외형 누락 폴백 ·
        /// 강화해도 탄약 비율·쿨타임 유지 · 예외 없음.
        /// </summary>
        private IEnumerator UpgradeCheck()
        {
            _report.AppendLine("\n## 블록 강화");
            _upgradeErrors = 0;
            Application.logMessageReceived += CountErrors;
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(1f);

            // 0) 데이터
            var sbData = new StringBuilder();
            foreach (var id in new[] { "mod_autocannon", "mod_gun76", "mod_ciws", "mod_magazine" })
            {
                var d = CombatDevTools.FindModule(id);
                var p = ModuleUpgrades.ProfileFor(d);
                sbData.Append($"{id} {(p != null ? $"최대 {p.MaxLevel}" : "없음")} · ");
                if (p == null || p.MaxLevel != 2) Fail($"{id} 강화 데이터가 없거나 최대 단계가 2가 아님");
            }
            _report.AppendLine($"- 강화 데이터: {sbData}");

            var ac = CombatDevTools.FindModule("mod_autocannon");
            var a = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            var b = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            if (a == null || b == null || refit == null) { Fail("시험 준비 실패(기관포 설치 또는 정비 화면 없음)"); Application.logMessageReceived -= CountErrors; yield break; }

            // 1) 카드 흐름: 무장 강화는 독립 카드(설치 카드와 따로). 같은 블록이 둘이면 고르고, 취소하면 카드로 돌아온다
            var upgradeCards = new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.WeaponUpgrade(ac), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) };
            GameManager.Instance.SetState(GameState.Refit);
            refit.Open(99, upgradeCards, () => { });   // 3장: 호위함 개량은 3번째 카드를 대체한다
            yield return null;
            yield return RefitShot(refit, "upgrade_1_cards");
            Call(refit, "ChooseCard", 0);
            bool picking0 = Get<bool>(refit, "_upgradePicking"), held0 = Get<ModuleDefinition>(refit, "_held") != null;
            yield return null;
            yield return RefitShot(refit, "upgrade_2_choice");
            int markers = 0;
            foreach (var t in Get<List<Transform>>(refit, "_upgradeMarkers") ?? new List<Transform>()) if (t != null && t.gameObject.activeSelf) markers++;
            Call(refit, "BackToCards");
            bool backToCards = Get<bool>(refit, "_choosing");
            Call(refit, "ChooseCard", 0);
            bool picking = Get<bool>(refit, "_upgradePicking");
            yield return null;
            yield return RefitShot(refit, "upgrade_3_pick");
            bool upgraded = (bool)Call(refit, "TryUpgradeAt", a.Origin);
            int aLv = a.UpgradeLevel, bLv = b.UpgradeLevel;
            _report.AppendLine($"- 카드: 무장 강화 카드 → 강화 대상 선택 {(picking0 ? "예" : "아니오")}(설치 선택 없음: 들기 {held0}) · 강조 {markers}개 · 취소 → 카드 {(backToCards ? "예" : "아니오")} · 다시 선택 {picking} · 기관포 A 강화 {upgraded} → A {aLv} / B {bLv}");
            if (!picking0 || held0 || !backToCards || !picking || !upgraded) Fail("무장 강화 카드 흐름이 동작하지 않음");
            if (aLv != 1 || bLv != 0) Fail("강화가 고른 블록 하나에만 적용되지 않음");
            if (markers < 2) Fail("강화 대상 강조가 부족함");

            // 설치 카드는 강화 선택 없이 바로 들어 올린다(같은 블록이 있어도)
            int countBefore = grid.CountOf(ac);
            refit.Open(99, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.Install(ac), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) }, () => { });
            Call(refit, "ChooseCard", 0);
            bool holding = Get<ModuleDefinition>(refit, "_held") == ac, pickingNow = Get<bool>(refit, "_upgradePicking");
            Call(refit, "PlaceHeld");
            int countAfter = grid.CountOf(ac);
            _report.AppendLine($"- 설치 카드: 들기 {holding} · 강화 선택으로 빠지지 않음 {!pickingNow} · 기관포 {countBefore} → {countAfter}문(강화 단계 A {a.UpgradeLevel} 그대로)");
            if (!holding || pickingNow || countAfter != countBefore + 1) Fail("설치 카드가 동작하지 않음");

            // 대상이 하나뿐이면 고르지 않고 바로 적용(탄약고: 시험장에 없으면 하나만 설치해 둔다)
            var magDef = CombatDevTools.FindModule("mod_magazine");
            var existingMags = new List<ModuleInstance>();
            ModuleUpgrades.GetTargets(grid, magDef, existingMags);
            ModuleInstance onlyMag = existingMags.Count == 1 ? existingMags[0] : null;
            bool installedHere = false;
            if (existingMags.Count == 0) { onlyMag = CombatDevTools.InstallModuleNear("mod_magazine", new GridCoord(0, 0), 8); installedHere = onlyMag != null; }
            if (onlyMag != null)
            {
                refit.Open(99, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.WeaponUpgrade(magDef), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")) }, () => { });
                Call(refit, "ChooseCard", 0);
                bool auto = onlyMag.UpgradeLevel == 1 && !Get<bool>(refit, "_upgradePicking");
                _report.AppendLine($"- 대상이 하나뿐인 강화 카드: 바로 적용 {(auto ? "예" : "아니오")}(탄약고 단계 {onlyMag.UpgradeLevel})");
                if (!auto) Fail("대상이 하나뿐인데 바로 적용되지 않음");
                if (installedHere) CombatDevTools.RemoveModule(onlyMag);
            }
            else _report.AppendLine("- 대상이 하나뿐인 강화 카드: 시험장에 탄약고가 둘 이상이라 건너뜀");

            // 2) 블록마다 다른 단계
            a.ApplyUpgrade();
            b.ApplyUpgrade();
            bool maxBlocked = !a.ApplyUpgrade();
            _report.AppendLine($"- 단계: A {a.UpgradeLevel} · B {b.UpgradeLevel} · 최대에서 더 강화 거부 {maxBlocked}");
            if (a.UpgradeLevel != 2 || b.UpgradeLevel != 1 || !maxBlocked) Fail("블록별 강화 단계가 맞지 않음");

            // 7·8) 외형과 피벗
            string Visual(ModuleInstance m)
            {
                var v = m.Runtime != null ? m.Runtime.GetComponent<ModuleUpgradeVisuals>() : null;
                var w = m.Runtime != null ? Get<WeaponController>(m.Runtime, "weapon") : null;
                string muzzleIn = "-";
                if (w?.Muzzle != null)
                    for (var t = w.Muzzle; t != null; t = t.parent)
                        if (t.name.StartsWith("Visual_Level")) { muzzleIn = t.name; break; }
                return $"외형 {(v != null ? v.ActiveIndex.ToString() : "없음")} · 포구 {muzzleIn}";
            }
            string visA = Visual(a), visB = Visual(b);
            _report.AppendLine($"- 외형: A(II) {visA} / B(I) {visB}");
            if (!visA.Contains("외형 2") || !visA.Contains("Visual_Level3")) Fail("강화 II 외형 또는 포구 재연결이 맞지 않음");
            if (!visB.Contains("외형 1") || !visB.Contains("Visual_Level2")) Fail("강화 I 외형 또는 포구 재연결이 맞지 않음");

            // 3) 재배치(정비 화면에서 집어 다른 칸에 놓기)
            int idA = a.InstanceId;
            Put(refit, "_cursor", a.Origin);
            Call(refit, "Confirm");
            GridCoord spot = default;
            int spotRot = 0;
            bool found = false;
            for (int d = 1; d <= 8 && !found; d++)
                for (int x = -d; x <= d && !found; x++)
                    for (int z = -d; z <= d && !found; z++)
                        for (int rot = 0; rot < 4 && !found; rot++)
                        {
                            var c = new GridCoord(x, z);
                            if (c.Equals(Get<GridCoord>(refit, "_heldOrigin")) || !grid.CanPlace(ac, c, rot, out _)) continue;
                            spot = c; spotRot = rot; found = true;
                        }
            Put(refit, "_cursor", spot);
            Put(refit, "_heldRotation", spotRot);
            Call(refit, "PlaceHeld");
            var moved = grid.Get(spot);
            _report.AppendLine($"- 재배치: {(moved != null ? $"({spot.X},{spot.Z})로 이동 · 번호 {idA} → {moved.InstanceId} · 단계 {moved.UpgradeLevel} · {Visual(moved)}" : "실패")}");
            if (moved == null || moved.InstanceId != idA || moved.UpgradeLevel != 2) Fail("재배치 후 강화 단계·번호가 유지되지 않음");
            if (moved != null) a = moved;
            yield return null;
            yield return RefitShot(refit, "upgrade_4_badges");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);

            // 4) 새 블록
            var fresh = new ModuleInstance(ac);
            _report.AppendLine($"- 새 블록: 단계 {fresh.UpgradeLevel} · 번호 {fresh.InstanceId}(기존과 다름 {fresh.InstanceId != idA}) · 새 게임은 씬을 새로 열어 모든 블록이 새로 만들어진다");
            if (fresh.UpgradeLevel != 0 || fresh.InstanceId == idA) Fail("새 블록이 강화 0에서 시작하지 않음");

            // 5) 최종 스탯은 한 번만(기본 대비)
            var s0 = ac.Stats;
            var sa = a.Runtime.Stats;
            var gun = CombatDevTools.InstallModuleNear("mod_gun76", new GridCoord(0, 0), 8);
            var ciws = CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
            if (gun == null || ciws == null) { Fail("76mm·CIWS 설치 실패"); }
            else
            {
                gun.ApplyUpgrade(); gun.ApplyUpgrade();
                ciws.ApplyUpgrade(); ciws.ApplyUpgrade();
                var g0 = gun.Definition.Stats; var g = gun.Runtime.Stats;
                var c0 = ciws.Definition.Stats; var c = ciws.Runtime.Stats;
                int ciwsCap = (ciws.Runtime as IAmmoUser)?.Ammo?.Capacity ?? -1;
                _report.AppendLine($"- 기관포 II: 피해 {s0.Damage} → {sa.Damage:0.##} · 탄창 {s0.MagazineCapacity} → {sa.MagazineCapacity} · 선회 {s0.TurretTurnRate} → {sa.TurretTurnRate:0}");
                _report.AppendLine($"- 76mm II: 피해 {g0.Damage} → {g.Damage:0.##} · 발사 간격 {g0.ReloadTime} → {g.ReloadTime:0.###}");
                _report.AppendLine($"- CIWS II: 선회 {c0.TurretTurnRate} → {c.TurretTurnRate:0} · 탄창 {c0.MagazineCapacity} → {c.MagazineCapacity}(실제 {ciwsCap}) · 재장전 {c0.AmmoReloadTime} → {c.AmmoReloadTime:0.##} · 피해 {c.Damage} · 사거리 {c.Range} · 발사 간격 {c.ReloadTime}");
                if (!Mathf.Approximately(sa.Damage, s0.Damage * 1.5f) || sa.MagazineCapacity != 260 || !Mathf.Approximately(sa.TurretTurnRate, s0.TurretTurnRate * 1.15f)) Fail("기관포 강화 II 수치가 기본 대비가 아님");
                if (!Mathf.Approximately(g.Damage, g0.Damage * 1.5f) || !Mathf.Approximately(g.ReloadTime, g0.ReloadTime * 0.9f)) Fail("76mm 강화 II 수치가 기본 대비가 아님");
                if (!Mathf.Approximately(c.TurretTurnRate, c0.TurretTurnRate * 1.35f) || c.MagazineCapacity != 630 || ciwsCap != 630 ||
                    !Mathf.Approximately(c.AmmoReloadTime, c0.AmmoReloadTime * 0.85f) || c.Damage != c0.Damage || c.Range != c0.Range || c.ReloadTime != c0.ReloadTime)
                    Fail("CIWS 강화 II 수치가 명세와 다름(공격력·사거리·연사는 그대로여야 함)");
                if (sa.Damage * (1f / s0.ReloadTime) >= 2f * s0.Damage * (1f / s0.ReloadTime)) Fail("강화 II 한 문의 화력이 기본 두 문 이상");
            }

            // 실제 발사: 강화 II 기관포 한 문만 켜고 멈춘 고속정을 쏜다
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None))
                if (r is not IRadarSource) r.enabled = r == a.Runtime;
            var target = CombatDevTools.SpawnRing("ene_fastboat", 3, 15f, 30f);   // 사격각이 어느 쪽이든 하나는 들어오게
            foreach (var e in target) e.DevFrozen = true;
            float shotDamage = -1f, t0 = Time.time;
            while (Time.time - t0 < 6f && shotDamage < 0f)
            {
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled && p.name.StartsWith("PRJ_Autocannon")) { shotDamage = Get<float>(p, "_damage"); break; }
                yield return null;
            }
            float eff = ac.TargetEfficiency.For(TargetCategory.SmallSurface);
            _report.AppendLine($"- 실제 발사(기관포 II → 고속정): 탄 피해 {shotDamage:0.###} · 기대 {s0.Damage * 1.5f * eff:0.###}(기본 {s0.Damage} × 1.5 × 상성 {eff})");
            if (shotDamage < 0f || !Mathf.Approximately(shotDamage, s0.Damage * 1.5f * eff)) Fail("발사된 탄의 피해에 강화가 한 번만 적용되지 않음");
            CombatDevTools.ClearBattlefield();
            // 강화 II 기관포 A만 켜 둔다(꺼진 런타임은 설치·강화 이벤트를 받지 않아 탄약고 지원을 다시 잡지 않는다)
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = r == a.Runtime;
            Put(a.Runtime, "_targeting", null);   // 표적이 남아 쏘지 않게(지원 배율만 본다)

            // 6) 탄약고 지원(지원량만 커지고 무기 쪽에서 두 번 곱하지 않는다)
            var mag = CombatDevTools.InstallModuleNear("mod_magazine", a.Origin, 2);
            if (mag == null) Fail("탄약고 설치 실패");
            else
            {
                var bonus0 = MagazineSupport.For(grid, a, true);
                mag.ApplyUpgrade(); mag.ApplyUpgrade();
                var bonus2 = MagazineSupport.For(grid, a, true);
                var aw = Get<WeaponController>(a.Runtime, "weapon");
                float applied = Get<float>(aw, "_reloadMultiplier");
                int cap = (a.Runtime as IAmmoUser).Ammo.Capacity;
                var m0 = mag.Definition.Stats;
                float expectReload = 1f - m0.ReloadBonus * 1.3f, expectCapMul = 1f + m0.AmmoCapacityBonus * 1.3f;
                int expectCap = Mathf.RoundToInt(260 * expectCapMul);
                _report.AppendLine($"- 탄약고 II: 발사 간격 배율 {bonus0.ReloadMultiplier:0.###} → {bonus2.ReloadMultiplier:0.###}(기관포 적용 {applied:0.###}) · 용량 배율 {bonus0.CapacityMultiplier:0.###} → {bonus2.CapacityMultiplier:0.###} · 기관포 II 탄창 {cap}(기대 {expectCap}) · 유폭 {m0.CookOffDamage} → {mag.EffectiveStats.CookOffDamage:0}");
                if (!Mathf.Approximately(bonus2.ReloadMultiplier, expectReload) || !Mathf.Approximately(bonus2.CapacityMultiplier, expectCapMul)) Fail("탄약고 강화 지원량이 기본 대비 +30%가 아님");
                if (!Mathf.Approximately(applied, bonus2.ReloadMultiplier)) Fail("무기에 적용된 탄약고 배율이 다름(중복 또는 미갱신)");
                if (cap != expectCap) Fail("탄약고 용량 보너스가 중복되거나 빠짐");
                if (!Mathf.Approximately(mag.EffectiveStats.CookOffDamage, m0.CookOffDamage * 0.75f)) Fail("탄약고 강화 II 유폭 감소가 적용되지 않음");
            }

            // 9) 강화해도 남은 탄약 비율·쿨타임 유지
            var bAmmo = (b.Runtime as IAmmoUser).Ammo;
            for (int i = 0; i < 60; i++) bAmmo.Consume();
            float ratio = (float)bAmmo.Current / bAmmo.Capacity;
            var bw = Get<WeaponController>(b.Runtime, "weapon");
            Put(bw, "_cooldown", 0.37f);
            b.ApplyUpgrade();
            float ratioAfter = (float)bAmmo.Current / bAmmo.Capacity;
            float cdAfter = Get<float>(bw, "_cooldown");
            _report.AppendLine($"- 강화 중 상태 유지(B I→II): 탄 {ratio:P0} → {ratioAfter:P0}({bAmmo.Current}/{bAmmo.Capacity}) · 쿨타임 0.37 → {cdAfter:0.##}");
            if (Mathf.Abs(ratioAfter - ratio) > 0.02f || !Mathf.Approximately(cdAfter, 0.37f)) Fail("강화할 때 탄약·쿨타임이 초기화됨");

            // 외형 누락 폴백(모델이 없으면 기본 외형 유지)
            var probe = new GameObject("VisualFallbackProbe");
            var baseVis = new GameObject("Visual_Level1"); baseVis.transform.SetParent(probe.transform, false);
            var vis = probe.AddComponent<ModuleUpgradeVisuals>();
            Put(vis, "levels", new GameObject[] { baseVis, null, null });
            vis.ApplyLevel(3, null);
            bool fallback = baseVis.activeSelf && vis.ActiveIndex == 0;
            Destroy(probe);
            _report.AppendLine($"- 외형 모델이 없을 때: 기본 외형 유지 {(fallback ? "예" : "아니오")}");
            if (!fallback) Fail("강화 외형이 없을 때 기본 외형이 꺼짐");

            yield return new WaitForSeconds(0.5f);
            Application.logMessageReceived -= CountErrors;
            _report.AppendLine($"- 검사 중 예외·MissingReference: {_upgradeErrors}건");
            if (_upgradeErrors > 0) Fail("강화 검사 중 예외 발생");

            foreach (var d in off) if (d != null) d.enabled = true;
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;
            ship.DevRudderOverride = null;
            CombatDevTools.ClearBattlefield();
        }

        // ------------------------------------------------------------ 성장 카드(카드 시스템 개편)

        private static bool Near(float a, float b, float rel = 0.002f) => Mathf.Abs(a - b) <= Mathf.Max(1e-4f, Mathf.Abs(b)) * rel;

        /// <summary>
        /// 성장 카드: 등급별 수치(공격력 5/10/20%) · 능력별 실제 반영(공격력·계열 공격력·연사력·장탄수·사거리·수리·탐지·속력·선체·방어력·스킬 회복) ·
        /// 합산 누적 · 강화와 곱해져 한 번만 적용 · 실제 발사 피해 · 초기화 · 카드 추첨 규칙(3장 자리·강화 확률 상승·등급 확률 상승·보스 에픽·장비 조건) ·
        /// 카드 화면과 선택 · 보스 보상 흐름 · Tab 현황 요약.
        /// </summary>
        private IEnumerator GrowthCheck()
        {
            _report.AppendLine("\n## 성장 카드(카드 시스템 개편)");
            Game.Modules.RunUpgrades.Reset();
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var refit = Object.FindFirstObjectByType<Game.UI.RefitUI>(FindObjectsInactive.Include);
            var draft = Object.FindFirstObjectByType<Game.Refit.RefitDraft>(FindObjectsInactive.Include);
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(0.6f);
            if (refit == null || draft == null) { Fail("정비 화면 또는 카드 추첨기가 없음"); yield break; }

            // 1) 등급별 수치
            var sbCat = new StringBuilder();
            bool catalogOk = Game.Modules.RunUpgrades.Catalog.Count == 13;
            foreach (var d in Game.Modules.RunUpgrades.Catalog)
            {
                sbCat.Append($"{d.Name} {d.Values[0] * 100:0.#}/{d.Values[1] * 100:0.#}/{d.Values[2] * 100:0.#}% · ");
                if (!(d.Values[0] < d.Values[1] && d.Values[1] < d.Values[2]) || d.Weight <= 0f || d.Values[0] <= 0f) catalogOk = false;
            }
            var dmgDef = Game.Modules.RunUpgrades.Definition(Game.Modules.RunStat.Damage);
            bool dmgOk = Near(dmgDef.Values[0], 0.05f) && Near(dmgDef.Values[1], 0.10f) && Near(dmgDef.Values[2], 0.20f);
            _report.AppendLine($"- 카탈로그 {Game.Modules.RunUpgrades.Catalog.Count}종(일반/희귀/에픽): {sbCat}");
            if (!catalogOk || !dmgOk) Fail("성장 카드 수치표가 맞지 않음(공격력은 5/10/20%, 등급이 오를수록 커야 함)");

            // 2) 능력별 실제 반영 — 장비를 설치하고 기본값을 적어 둔다
            var ac = CombatDevTools.FindModule("mod_autocannon");
            var a = CombatDevTools.InstallModuleNear("mod_autocannon", new GridCoord(0, 0), 8);
            var gun = CombatDevTools.InstallModuleNear("mod_gun76", new GridCoord(0, 0), 8);
            var rocket = CombatDevTools.InstallModuleNear("mod_rocket", new GridCoord(0, 0), 8);
            var ciws = CombatDevTools.InstallModuleNear("mod_ciws", new GridCoord(0, 0), 8);
            var repair = CombatDevTools.InstallModuleNear("mod_repairbay", new GridCoord(0, 0), 8);
            var decoy = CombatDevTools.InstallModuleNear("mod_decoy", new GridCoord(0, 0), 8);
            if (a == null || gun == null || rocket == null || ciws == null || repair == null || decoy == null)
            { Fail("성장 카드 시험용 장비 설치 실패"); yield break; }
            yield return new WaitForSeconds(0.3f);

            var A0 = a.Definition.Stats; var G0 = gun.Definition.Stats; var R0 = rocket.Definition.Stats; var C0 = ciws.Definition.Stats; var P0 = repair.Definition.Stats;
            float detection0 = ship.Systems.DetectionRange, speed0 = ship.BaseMaxSpeed, hullMax0 = ship.HullMaxHp, hull0 = ship.HullHp;
            int ammoCap0 = (a.Runtime as IAmmoUser).Ammo.Capacity;
            var Add = (System.Action<Game.Modules.RunStat, Game.Modules.CardTier>)Game.Modules.RunUpgrades.Add;

            // 공격력(공통) 일반+희귀+에픽 = +35% 합산, 계열 카드는 해당 무장에만
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Common);
            bool d5 = Near(a.Runtime.Stats.Damage, A0.Damage * 1.05f);
            float d5Ratio = a.Runtime.Stats.Damage / A0.Damage;
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Rare);
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);
            bool dAll = Near(a.Runtime.Stats.Damage, A0.Damage * 1.35f) && Near(gun.Runtime.Stats.Damage, G0.Damage * 1.35f) &&
                        Near(rocket.Runtime.Stats.Damage, R0.Damage * 1.35f) && Near(ciws.Runtime.Stats.Damage, C0.Damage * 1.35f);
            Add(Game.Modules.RunStat.GunDamage, Game.Modules.CardTier.Rare);       // 함포계 +16%
            Add(Game.Modules.RunStat.MissileDamage, Game.Modules.CardTier.Epic);   // 미사일계 +32%
            bool dClass = Near(a.Runtime.Stats.Damage, A0.Damage * 1.51f) && Near(gun.Runtime.Stats.Damage, G0.Damage * 1.51f) &&
                          Near(rocket.Runtime.Stats.Damage, R0.Damage * 1.67f) && Near(ciws.Runtime.Stats.Damage, C0.Damage * 1.35f);
            _report.AppendLine($"- 공격력: 일반 +5% → 기관포 {d5Ratio:0.###}배(기대 1.05) · 일반+희귀+에픽 합산 {(dAll ? "1.35배 전 무장" : "불일치")} · 계열 카드 후 기관포 {a.Runtime.Stats.Damage / A0.Damage:0.##} 76mm {gun.Runtime.Stats.Damage / G0.Damage:0.##} 유도로켓 {rocket.Runtime.Stats.Damage / R0.Damage:0.##} CIWS {ciws.Runtime.Stats.Damage / C0.Damage:0.##}(기대 1.51/1.51/1.67/1.35)");
            if (!d5) Fail("공격력 일반 카드가 +5%로 반영되지 않음");
            if (!dAll) Fail("공격력 카드가 합산(+35%)으로 모든 무장에 적용되지 않음");
            if (!dClass) Fail("계열 공격력 카드가 해당 무장에만 적용되지 않음");

            // 연사력(공통 +10%) + 방공계(+8%) → CIWS는 +18%
            Add(Game.Modules.RunStat.FireRate, Game.Modules.CardTier.Rare);
            Add(Game.Modules.RunStat.AirDefenseRate, Game.Modules.CardTier.Common);
            bool fr = Near(a.Runtime.Stats.ReloadTime, A0.ReloadTime / 1.10f) && Near(ciws.Runtime.Stats.ReloadTime, C0.ReloadTime / 1.18f) &&
                      Near(decoy.Runtime.Stats.ReloadTime, decoy.Definition.Stats.ReloadTime);   // 기만체(스킬)는 연사력이 아니다
            _report.AppendLine($"- 연사력 +10%: 기관포 발사 간격 {A0.ReloadTime:0.###} → {a.Runtime.Stats.ReloadTime:0.###}(기대 {A0.ReloadTime / 1.10f:0.###}) · CIWS(+방공계 8%) {C0.ReloadTime:0.###} → {ciws.Runtime.Stats.ReloadTime:0.####}(기대 {C0.ReloadTime / 1.18f:0.####}) · 기만체 재장전 그대로");
            if (!fr) Fail("연사력 카드가 발사 간격에 반영되지 않았거나 스킬 장비까지 바뀜");

            // 장탄수(+32%) · 사거리(+8%)
            Add(Game.Modules.RunStat.Magazine, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Range, Game.Modules.CardTier.Rare);
            int expectCap = Game.Modules.ModuleUpgrades.ScaleCapacity(A0.MagazineCapacity, 1.32f, A0.AmmoPerShot);
            int liveCap = (a.Runtime as IAmmoUser).Ammo.Capacity;
            bool mag = a.Runtime.Stats.MagazineCapacity == expectCap && liveCap > ammoCap0;
            bool rng = Near(a.Runtime.Stats.Range, A0.Range * 1.08f) && Near(gun.Runtime.Stats.Range, G0.Range * 1.08f) && Near(a.Runtime.Stats.MinRange, A0.MinRange);
            _report.AppendLine($"- 장탄수 +32%: 기관포 탄창 {A0.MagazineCapacity} → {a.Runtime.Stats.MagazineCapacity}(기대 {expectCap}) · 실제 탄창 {ammoCap0} → {liveCap} / 사거리 +8%: 기관포 {A0.Range:0.#} → {a.Runtime.Stats.Range:0.#}(기대 {A0.Range * 1.08f:0.#}) · 최소 거리 그대로");
            if (!mag) Fail("장탄수 카드가 탄창 용량(스탯·실제 탄창)에 반영되지 않음");
            if (!rng) Fail("사거리 카드가 최대 사거리에만 반영되지 않음");

            // 수리 효율(+40%) · 탐지(+32%) · 속력(+8%)
            Add(Game.Modules.RunStat.Repair, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Detection, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.Speed, Game.Modules.CardTier.Rare);
            yield return null;
            bool rep = Near(repair.Runtime.Stats.HullRepairPerSecond, P0.HullRepairPerSecond * 1.4f) && Near(repair.Runtime.Stats.ModuleRepairPerSecond, P0.ModuleRepairPerSecond * 1.4f);
            bool det = Near(ship.Systems.DetectionRange, detection0 * 1.32f);
            bool spd = Near(ship.BaseMaxSpeed, speed0 * 1.08f);
            _report.AppendLine($"- 수리 효율 +40%: 선체 {P0.HullRepairPerSecond:0.##} → {repair.Runtime.Stats.HullRepairPerSecond:0.##}/초 · 탐지 +32%: {detection0:0.#} → {ship.Systems.DetectionRange:0.#} · 최고 속력 +8%: {speed0:0.##} → {ship.BaseMaxSpeed:0.##}");
            if (!rep) Fail("수리 효율 카드가 손상 통제반 수리 속도에 반영되지 않음");
            if (!det) Fail("탐지 거리 카드가 함선 탐지 거리에 반영되지 않음");
            if (!spd) Fail("최고 속력 카드가 반영되지 않음");

            // 선체 강화(+24%): 최대치가 늘고 늘어난 만큼 회복
            Add(Game.Modules.RunStat.HullMax, Game.Modules.CardTier.Epic);
            bool hullMax = Near(ship.HullMaxHp, hullMax0 * 1.24f) && Near(ship.HullHp, hull0 + hullMax0 * 0.24f);
            _report.AppendLine($"- 선체 강화 +24%: 최대 {hullMax0:0} → {ship.HullMaxHp:0} · 현재 {hull0:0} → {ship.HullHp:0}(늘어난 만큼 회복)");
            if (!hullMax) Fail("선체 강화 카드가 최대치와 현재 선체에 반영되지 않음");

            // 방어력(+12%): 받는 피해 ÷ 1.12. 같은 타격을 카드 전후로 비교
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Vector3 hitPoint = ship.transform.position;
            float h0 = ship.HullHp;
            ship.TakeDamage(new DamageInfo(20f, hitPoint, Vector3.down, DamageSource.Gun));
            float lost0 = h0 - ship.HullHp;
            Add(Game.Modules.RunStat.Defense, Game.Modules.CardTier.Epic);
            float h1 = ship.HullHp;
            ship.TakeDamage(new DamageInfo(20f, hitPoint, Vector3.down, DamageSource.Gun));
            float lost1 = h1 - ship.HullHp;
            _report.AppendLine($"- 방어력 +12%: 같은 20 피해의 선체 손실 {lost0:0.###} → {lost1:0.###}(기대 {lost0 / 1.12f:0.###}) · 표시 \"{Game.Modules.RunUpgrades.Describe(Game.Modules.RunStat.Defense, 0.12f)}\"");
            if (lost0 <= 0f || !Near(lost1, lost0 / 1.12f, 0.01f)) Fail("방어력 카드가 받는 피해를 1/1.12로 줄이지 않음");

            // 스킬 회복 속도(+16%): 기만체 재장전 타이머가 1.16배로 흐른다
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Put(decoy.Runtime, "_cooldown", 30f);
            float tA = Time.time;
            yield return new WaitForSeconds(1.5f);
            float dropPlain = 30f - Get<float>(decoy.Runtime, "_cooldown"), spanPlain = Time.time - tA;
            Add(Game.Modules.RunStat.SkillRate, Game.Modules.CardTier.Rare);   // +16%
            Put(decoy.Runtime, "_cooldown", 30f);
            tA = Time.time;
            yield return new WaitForSeconds(1.5f);
            float dropFast = 30f - Get<float>(decoy.Runtime, "_cooldown"), spanFast = Time.time - tA;
            float ratio = (dropFast / spanFast) / (dropPlain / spanPlain);
            _report.AppendLine($"- 스킬 회복 +16%: 기만체 재장전이 1초에 {dropPlain / spanPlain:0.##}초 → {dropFast / spanFast:0.##}초 흐름(비율 {ratio:0.###}, 기대 1.16)");
            if (ratio < 1.12f || ratio > 1.20f) Fail("스킬 회복 속도 카드가 능동 스킬 재장전에 반영되지 않음");

            // 3) 강화(블록)와 성장은 서로 곱해지되 각각 한 번만
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);   // +20%
            a.ApplyUpgrade();                                                // 기관포 강화 I: 피해 ×1.25
            bool both = Near(a.Runtime.Stats.Damage, A0.Damage * 1.25f * 1.20f);
            _report.AppendLine($"- 강화 I(×1.25) + 공격력 에픽(+20%): 기관포 피해 {a.Runtime.Stats.Damage:0.###}(기대 {A0.Damage * 1.5f:0.###} = 기본 {A0.Damage} × 1.25 × 1.20)");
            if (!both) Fail("무장 강화와 성장 카드가 한 번씩만 곱해지지 않음");

            // 4) 실제 발사 피해: 강화 I 기관포 한 문만 켜고 멈춘 고속정을 쏜다
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) if (r is not IRadarSource) r.enabled = r == a.Runtime;
            var target = CombatDevTools.SpawnRing("ene_fastboat", 3, 15f, 30f);
            foreach (var e in target) e.DevFrozen = true;
            float shotDamage = -1f, ts = Time.time;
            while (Time.time - ts < 6f && shotDamage < 0f)
            {
                foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled && p.name.StartsWith("PRJ_Autocannon")) { shotDamage = Get<float>(p, "_damage"); break; }
                yield return null;
            }
            float eff = ac.TargetEfficiency.For(TargetCategory.SmallSurface);
            _report.AppendLine($"- 실제 발사: 탄 피해 {shotDamage:0.###}(기대 {A0.Damage * 1.5f * eff:0.###} = 기본 × 1.25 × 1.20 × 상성 {eff})");
            if (shotDamage < 0f || !Near(shotDamage, A0.Damage * 1.5f * eff, 0.005f)) Fail("발사된 탄의 피해에 강화·성장이 한 번씩만 적용되지 않음");
            CombatDevTools.ClearBattlefield();
            foreach (var r in Object.FindObjectsByType<ModuleRuntime>(FindObjectsSortMode.None)) r.enabled = true;   // 켜면 스탯을 다시 계산한다
            yield return null;

            // 5) 초기화
            Add(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic);
            Add(Game.Modules.RunStat.HullMax, Game.Modules.CardTier.Epic);
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            bool resetOk = !Game.Modules.RunUpgrades.Any && Near(a.Runtime.Stats.Damage, A0.Damage * 1.25f) && Near(ship.HullMaxHp, hullMax0, 0.002f);
            _report.AppendLine($"- 초기화: 보너스 없음 {(!Game.Modules.RunUpgrades.Any ? "예" : "아니오")} · 기관포(강화 I만) 피해 {a.Runtime.Stats.Damage:0.###}(기대 {A0.Damage * 1.25f:0.###}) · 선체 최대 {ship.HullMaxHp:0}(기대 {hullMax0:0})");
            if (!resetOk) Fail("성장 카드 초기화 뒤 수치가 원래대로 돌아오지 않음");

            // 6) 카드 추첨 규칙
            // 6-1) 3장 자리: 1번 = 설치, 2번 = 성장(강화 또는 성장 카드), 같은 카드 중복 없음
            int draws = 400, installSlot = 0, growthSlot = 0, dup = 0, malformed = 0, upgradeAt1 = 0, upgradeAt20 = 0, notUnified = 0;
            for (int i = 0; i < draws; i++)
            {
                var cards = draft.DrawCards(3, 1);
                if (cards.Count != 3) { malformed++; continue; }
                if (cards[0].Kind == Game.Refit.RefitCardKind.Install) installSlot++;
                if (cards[1].Kind != Game.Refit.RefitCardKind.Install) growthSlot++;
                var seen = new HashSet<string>();
                foreach (var c in cards) if (!seen.Add(c.Label)) dup++;
                if (cards[1].Kind == Game.Refit.RefitCardKind.WeaponUpgrade) upgradeAt1++;
                // 통합 장비 강화 카드(2026-10-03): 블록이 정해지지 않은 한 종류, 한 번 뽑을 때 한 장까지
                int upgrades = 0;
                foreach (var c in cards)
                    if (c.Kind == Game.Refit.RefitCardKind.WeaponUpgrade) { upgrades++; if (c.Module != null) notUnified++; }
                if (upgrades > 1) notUnified++;
            }
            for (int i = 0; i < draws; i++)
            {
                var cards = draft.DrawCards(3, 20);
                if (cards.Count == 3 && cards[1].Kind == Game.Refit.RefitCardKind.WeaponUpgrade) upgradeAt20++;
            }
            _report.AppendLine($"- 3장 자리({draws}회): 1번이 설치 {installSlot} · 2번이 성장/강화 {growthSlot} · 중복 {dup} · 장수 이상 {malformed} / 2번 자리의 무장 강화 비율 레벨 1 {upgradeAt1 * 100f / draws:0}% → 레벨 20 {upgradeAt20 * 100f / draws:0}%(기대 약 30% → 65%)");
            if (malformed > 0 || installSlot < draws * 0.98f || growthSlot != draws - malformed) Fail("카드 자리 규칙(1번 설치 · 2번 성장)이 지켜지지 않음");
            if (dup > 0) Fail("한 번 뽑을 때 같은 카드가 겹침");
            _report.AppendLine($"- 장비 강화 카드 통합: 블록별 카드 또는 한 번에 두 장 이상 {notUnified}회(기대 0)");
            if (notUnified > 0) Fail("장비 강화 카드가 하나로 통합되지 않음(블록별 카드 또는 두 장 이상)");
            if (upgradeAt20 <= upgradeAt1 + draws * 0.15f) Fail("진행할수록 무장 강화 카드 확률이 올라가지 않음");

            // 6-2) 등급 확률: 레벨이 오를수록 희귀·에픽이 늘고, 에픽 수치가 일반보다 크다
            int N = 6000, rare1 = 0, epic1 = 0, rare20 = 0, epic20 = 0;
            var rng0 = new System.Random(7);
            for (int i = 0; i < N; i++)
            {
                var t1 = Game.Modules.RunUpgrades.RollTier(1, rng0); if (t1 == Game.Modules.CardTier.Rare) rare1++; else if (t1 == Game.Modules.CardTier.Epic) epic1++;
                var t20 = Game.Modules.RunUpgrades.RollTier(20, rng0); if (t20 == Game.Modules.CardTier.Rare) rare20++; else if (t20 == Game.Modules.CardTier.Epic) epic20++;
            }
            _report.AppendLine($"- 등급 확률({N}회): 레벨 1 희귀 {rare1 * 100f / N:0.#}% 에픽 {epic1 * 100f / N:0.#}% → 레벨 20 희귀 {rare20 * 100f / N:0.#}% 에픽 {epic20 * 100f / N:0.#}%");
            if (rare20 <= rare1 || epic20 < epic1 * 3) Fail("진행할수록 희귀·에픽 확률이 충분히 오르지 않음");

            // 6-3) 보스 보상: 2번 자리가 에픽 성장 카드로 고정
            int bossOk = 0;
            for (int i = 0; i < 100; i++)
            {
                var cards = draft.DrawCards(3, 5, bossReward: true);
                if (cards.Count == 3 && cards[1].Kind == Game.Refit.RefitCardKind.Growth && cards[1].Tier == Game.Modules.CardTier.Epic && cards[1].BossReward) bossOk++;
            }
            _report.AppendLine($"- 보스 보상 카드 100회: 2번 자리가 에픽 성장 카드 {bossOk}회");
            if (bossOk != 100) Fail("보스 보상에 에픽 성장 카드가 보장되지 않음");

            // 6-4) 장비 조건: 계열·수리 카드는 그 장비가 있을 때만
            bool Has(params ModuleType[] types)
            {
                foreach (var m in grid.Modules) if (m != null && m.IsOperational && System.Array.IndexOf(types, m.Definition.Type) >= 0) return true;
                return false;
            }
            int gated = 0, gatedChecked = 0;
            for (int i = 0; i < 600; i++)
                foreach (var c in draft.DrawCards(3, 10))
                {
                    if (c.Kind != Game.Refit.RefitCardKind.Growth) continue;
                    var gd = Game.Modules.RunUpgrades.Definition(c.Stat);
                    if (gd.RequiresAny == null) continue;
                    gatedChecked++;
                    if (!Has(gd.RequiresAny)) gated++;
                }
            // 수리반을 철거한 뒤에는 수리 카드가 한 번도 나오지 않는다
            CombatDevTools.RemoveModule(repair);
            int repairCards = 0;
            for (int i = 0; i < 600; i++)
                foreach (var c in draft.DrawCards(3, 10)) if (c.Kind == Game.Refit.RefitCardKind.Growth && c.Stat == Game.Modules.RunStat.Repair) repairCards++;
            _report.AppendLine($"- 장비 조건: 계열·수리 카드 {gatedChecked}장 중 장비 없이 나온 것 {gated} · 수리반 철거 뒤 수리 카드 {repairCards}장");
            if (gated > 0 || repairCards > 0) Fail("장비가 없는데 계열·수리 성장 카드가 나옴");

            // 7) 카드 화면: 무장 강화 + 보스 에픽 + 설치, 그리고 세 등급
            GameManager.Instance.SetState(GameState.Refit);
            var shown = new List<Game.Refit.RefitCard>
            {
                Game.Refit.RefitCard.WeaponUpgrade(ac),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic, true),
                Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")),
            };
            refit.Open(8, shown, () => { }, bossReward: true);
            yield return null;
            yield return RefitShot(refit, "growth_1_boss_cards");
            var tierCards = new List<Game.Refit.RefitCard>
            {
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.FireRate, Game.Modules.CardTier.Common),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.GunDamage, Game.Modules.CardTier.Rare),
                Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Defense, Game.Modules.CardTier.Epic),
            };
            refit.Open(8, tierCards, () => { }, false);
            yield return null;
            yield return RefitShot(refit, "growth_2_tiers");
            var title = Get<TMPro.TMP_Text>(refit, "titleText");

            // 8) 성장 카드를 고르면 즉시 적용되고 배치로 넘어간다(설치 선택 없음)
            refit.Open(8, new List<Game.Refit.RefitCard> { Game.Refit.RefitCard.Growth(Game.Modules.RunStat.Damage, Game.Modules.CardTier.Epic), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_sonar")), Game.Refit.RefitCard.Install(CombatDevTools.FindModule("mod_radar")) }, () => { });
            float dmgBefore = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.Damage);
            Call(refit, "ChooseCard", 0);
            float dmgAfter = Game.Modules.RunUpgrades.Get(Game.Modules.RunStat.Damage);
            bool chosen = !Get<bool>(refit, "_choosing") && Get<ModuleDefinition>(refit, "_held") == null;
            string growthText = Get<string>(refit, "_growthResult");
            yield return null;
            yield return RefitShot(refit, "growth_3_applied");
            _report.AppendLine($"- 성장 카드 선택: 공격력 {dmgBefore * 100f:0}% → {dmgAfter * 100f:0}% · 카드 닫힘·배치로 {(chosen ? "예" : "아니오")} · 결과 문구 \"{growthText}\"");
            if (!Near(dmgAfter - dmgBefore, 0.20f) || !chosen || string.IsNullOrEmpty(growthText)) Fail("성장 카드 선택이 즉시 적용·진행되지 않음");
            refit.Close();
            GameManager.Instance.SetState(GameState.Playing);

            // 9) 보스 보상 흐름: RefitController가 에픽 카드가 든 보상 화면을 열고, 닫으면 콜백이 불린다
            var controller = Object.FindFirstObjectByType<Game.Refit.RefitController>(FindObjectsInactive.Include);
            bool finished = false;
            if (controller == null) Fail("RefitController 없음");
            else
            {
                controller.BeginBossReward(4, () => finished = true);
                var bossCards = Get<List<Game.Refit.RefitCard>>(refit, "_cards");
                bool epicShown = false;
                foreach (var c in bossCards) if (c.Kind == Game.Refit.RefitCardKind.Growth && c.Tier == Game.Modules.CardTier.Epic && c.BossReward) epicShown = true;
                bool titleBoss = (Get<TMPro.TMP_Text>(refit, "titleText")?.text ?? "").Contains("보스");
                int epicIndex = bossCards.FindIndex(c => c.BossReward);
                yield return null;
                yield return RefitShot(refit, "growth_4_boss_flow");
                float before = Game.Modules.RunUpgrades.Get(bossCards[epicIndex].Stat);
                Call(refit, "ChooseCard", epicIndex);
                float gain = Game.Modules.RunUpgrades.Get(bossCards[epicIndex].Stat) - before;
                Call(refit, "TryLaunch");
                _report.AppendLine($"- 보스 보상 화면: 에픽 카드 {(epicShown ? "있음" : "없음")} · 제목에 보스 표시 {(titleBoss ? "예" : "아니오")} · 에픽 선택 +{gain * 100f:0.#}% · 닫으면 다음 단계로 {(finished ? "예" : "아니오")}");
                if (!epicShown || !titleBoss) Fail("보스 보상 화면에 에픽 카드·제목이 표시되지 않음");
                if (gain < 0.05f || !finished) Fail("보스 보상 선택이 적용·종료되지 않음");
                if (GameManager.Instance.State == GameState.Refit) GameManager.Instance.SetState(GameState.Playing);
            }

            // 10) Tab 현황판: 쌓은 성장 요약
            var tab = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (tab != null)
            {
                tab.SetOpen(true);
                yield return new WaitForSecondsRealtime(0.3f);
                string footer = Get<TMPro.TMP_Text>(tab, "_growthFooter")?.text ?? "";
                yield return ScreenShot("growth_5_tab");
                _report.AppendLine($"- Tab 현황 성장 줄: \"{footer}\"");
                if (!footer.Contains("공격력")) Fail("Tab 현황판에 쌓은 성장 카드가 보이지 않음");
                tab.SetOpen(false);
            }

            // 정리
            Game.Modules.RunUpgrades.Reset();
            yield return null;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = null;
        }

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

        // ------------------------------------------------------------ 섬(엄폐)

        private static T PrivateField<T>(object obj, string name) where T : class
        {
            var f = obj?.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(obj) as T;
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

        private IEnumerator FormationCheck()
        {
            _report.AppendLine("\n## 편대 진형·조함");
            var ship = GameManager.Instance.Player;
            var formation = Object.FindFirstObjectByType<Game.TaskForce.TaskForceEscortFormation>();
            if (formation == null) { Fail("편대 없음"); yield break; }
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            formation.ClearAll();
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            var roles = new[] { Game.TaskForce.EscortRole.AirDefense, Game.TaskForce.EscortRole.AntiSubmarine, Game.TaskForce.EscortRole.ElectronicWarfare, Game.TaskForce.EscortRole.SurfaceStrike };
            foreach (var r in roles) { int i = formation.Deploy(); formation.AssignRole(i, r); }
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0.5f);
            yield return new WaitForSeconds(4f);

            T Prop<T>(Component c, string name) => c != null && c.GetType().GetProperty(name) is { } pi ? (T)pi.GetValue(c) : default;
            var ext = formation.FlagshipExtent;
            _report.AppendLine($"- 키: 진형 전환 [{GameSettings.BindingLabel(NavalControl.Formation)}] · 기함 크기 선수 {ext.x:0.#}m · 선미 {ext.y:0.#}m · 반폭 {ext.z:0.#}m");
            if (GameSettings.Binding(NavalControl.Formation) == UnityEngine.InputSystem.Key.None) Fail("진형 전환 키가 없음");

            // 1) 진형마다: 직진 중 슬롯 수렴 · 기함과 겹치지 않음(자율은 따로 — 고정 자리가 없다)
            var cycled = new List<string>();
            foreach (var f in Game.TaskForce.FleetFormations.All)
            {
                formation.SetFormation(f);
                cycled.Add(Game.TaskForce.FleetFormations.Name(formation.Formation));
                if (f == Game.TaskForce.FleetFormation.Autonomous) { yield return AutonomousFormationCheck(formation, ship); continue; }
                for (int k = 0; k < 18; k++)
                {
                    yield return new WaitForSeconds(0.5f);
                    if (f != Game.TaskForce.FleetFormation.Column || k % 4 != 3) continue;   // 종렬진만 2초마다 궤적 기록
                    var tr = new StringBuilder($"  - t{(k + 1) * 0.5f:0.0}: ");
                    for (int i = 0; i < formation.EscortCount; i++)
                    {
                        var esc = formation.GetEscort(i);
                        var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                        var sw = ship.transform.InverseTransformPoint(formation.SlotWorld(i));
                        tr.Append($"#{i}s{formation.SlotOf(i)} ({lp.x:0.0},{lp.z:0.0})->({sw.x:0},{sw.z:0}) v{Prop<float>(esc, "Speed"):0.0} h{Mathf.DeltaAngle(ship.transform.eulerAngles.y, Prop<float>(esc, "Heading")):0} | ");
                    }
                    _report.AppendLine(tr.ToString());
                }
                float worst = 0f, clearance = float.MaxValue;
                var pts = new StringBuilder();
                for (int i = 0; i < formation.EscortCount; i++)
                {
                    var esc = formation.GetEscort(i);
                    var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                    Vector2 want = f == Game.TaskForce.FleetFormation.Column
                        ? new Vector2(0f, -Game.TaskForce.FleetFormations.TrailDistance(formation.SlotOf(i), ext.y))
                        : Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                    worst = Mathf.Max(worst, Vector2.Distance(new Vector2(lp.x, lp.z), want));
                    float along = Mathf.Clamp(lp.z, ext.y, ext.x);
                    clearance = Mathf.Min(clearance, new Vector2(lp.x, lp.z - along).magnitude - ext.z);
                    pts.Append($"({lp.x:0},{lp.z:0}) ");
                    if (f == Game.TaskForce.FleetFormation.Column) { var sw = ship.transform.InverseTransformPoint(formation.SlotWorld(i)); pts.Append($"[슬롯{formation.SlotOf(i)} 목표({sw.x:0},{sw.z:0}) 속력 {Prop<float>(esc, "Speed"):0.0} 침로차 {Mathf.DeltaAngle(ship.transform.eulerAngles.y, Prop<float>(esc, "Heading")):0}°] "); }
                }
                _report.AppendLine($"- {Game.TaskForce.FleetFormations.Name(f)}: {pts}· 슬롯 오차 최대 {worst:0.0}m · 기함 현측과 최소 간격 {clearance:0.0}m");
                if (worst > 3.5f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 직진 중 호위함이 슬롯에 들어가지 않음({worst:0.0}m)");
                if (clearance < 2f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 기함과 겹침");
                yield return Shot($"formation_{f.ToString().ToLowerInvariant()}");
            }
            if (formation.Formation != Game.TaskForce.FleetFormation.Autonomous) Fail("진형 전환이 마지막 진형에 오지 않음");
            formation.CycleFormation();
            bool wrapped = formation.Formation == Game.TaskForce.FleetFormation.Circular;
            // 진형 선택판 클릭(도식 칸)으로도 바뀐다
            Game.UI.FormationSelectorUI.Click(Game.TaskForce.FleetFormation.Column);
            bool clicked = formation.Formation == Game.TaskForce.FleetFormation.Column;
            _report.AppendLine($"- 진형 선택판: 보임 {Yes(Game.UI.FormationSelectorUI.IsVisible)} · 단종진 칸 클릭 → {Game.TaskForce.FleetFormations.Name(formation.Formation)}");
            if (!Game.UI.FormationSelectorUI.IsVisible || !clicked) Fail("진형 선택판이 보이지 않거나 클릭으로 진형이 바뀌지 않음");
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            _report.AppendLine($"- 진형 순환: {string.Join(" → ", cycled)} → {Game.TaskForce.FleetFormations.Name(formation.Formation)}");
            if (!wrapped) Fail("진형 전환이 처음 진형으로 돌아오지 않음");

            // 2) 급선회(전속·타 최대 6초): 함대원형진(바깥 함이 크게 도는 진형)과 단종진
            foreach (var f in new[] { Game.TaskForce.FleetFormation.Circular, Game.TaskForce.FleetFormation.Column })
            {
                formation.SetFormation(f);
                ship.DevRudderOverride = 0f;
                ship.SetEngineOrder(1f);
                yield return new WaitForSeconds(8f);
                var prev = new Vector3[formation.EscortCount];
                for (int i = 0; i < prev.Length; i++) prev[i] = formation.GetEscort(i).transform.position;
                float maxYawRate = 0f, maxSlip = 0f, maxGap = 0f, oldLateral = 0f;
                ship.DevRudderOverride = 1f;
                float t0 = Time.time, lastYaw = ship.transform.eulerAngles.y;
                while (Time.time - t0 < 6f)
                {
                    yield return null;
                    float dt = Mathf.Max(Time.deltaTime, 1e-4f);
                    float shipYawRate = Mathf.Abs(Mathf.DeltaAngle(lastYaw, ship.transform.eulerAngles.y)) / dt;
                    lastYaw = ship.transform.eulerAngles.y;
                    for (int i = 0; i < prev.Length; i++)
                    {
                        var esc = formation.GetEscort(i);
                        Vector3 v = (esc.transform.position - prev[i]) / dt; v.y = 0f;
                        prev[i] = esc.transform.position;
                        maxSlip = Mathf.Max(maxSlip, Mathf.Abs(Vector3.Dot(v, esc.transform.right)));
                        maxYawRate = Mathf.Max(maxYawRate, Mathf.Abs(Prop<float>(esc, "YawRate")));
                        Vector2 want = f == Game.TaskForce.FleetFormation.Column
                            ? new Vector2(0f, -Game.TaskForce.FleetFormations.TrailDistance(formation.SlotOf(i), ext.y))
                            : Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                        // 예전 방식: 슬롯을 기함 침로로 즉시 돌림 → 슬롯 횡속도 = 기함 선회율 × 슬롯 거리
                        oldLateral = Mathf.Max(oldLateral, shipYawRate * Mathf.Deg2Rad * want.magnitude);
                    }
                }
                // 선회 끝난 직후 진형과의 거리(종렬진은 항적 위)
                for (int i = 0; i < formation.EscortCount; i++)
                {
                    var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                    if (f != Game.TaskForce.FleetFormation.Column)
                    {
                        var want = Game.TaskForce.FleetFormations.Slot(f, formation.SlotOf(i), formation.EscortCount, ext.x, ext.y, ext.z);
                        maxGap = Mathf.Max(maxGap, Vector2.Distance(new Vector2(lp.x, lp.z), want));
                    }
                }
                ship.DevRudderOverride = 0f;
                yield return Shot($"formation_turn_{f.ToString().ToLowerInvariant()}");
                _report.AppendLine($"- 급선회 6초({Game.TaskForce.FleetFormations.Name(f)}): 호위함 선회율 최대 {maxYawRate:0}°/초 · 옆미끄러짐 최대 {maxSlip:0.0}m/s · " +
                                   (f != Game.TaskForce.FleetFormation.Column ? $"선회 중 기함 기준 슬롯과 거리 최대 {maxGap:0}m · " : "") +
                                   $"(예전 방식이면 바깥 슬롯 횡속도 {oldLateral:0}m/s)");
                if (maxSlip > 1.5f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 옆으로 미끄러짐(게걸음)");
                if (maxYawRate > 90f) Fail($"{Game.TaskForce.FleetFormations.Name(f)}: 호위함이 선회율 한도보다 빨리 꺾임");
            }
            // 종렬진: 선회 뒤 직진하면 항적을 따라 한 줄로
            ship.SetEngineOrder(0.75f);
            yield return new WaitForSeconds(7f);
            float colWorst = 0f;
            for (int i = 0; i < formation.EscortCount; i++)
            {
                var lp = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
                colWorst = Mathf.Max(colWorst, Mathf.Abs(lp.x));
            }
            _report.AppendLine($"- 종렬진 선회 뒤 7초 직진: 기함 항적에서 옆으로 최대 {colWorst:0.0}m");
            if (colWorst > 3f) Fail("종렬진이 선회 뒤 기함 항적을 따라 한 줄로 서지 않음");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            formation.SetFormation(Game.TaskForce.FleetFormation.Circular);
            formation.ClearAll();
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
        }

        /// <summary>
        /// 자율 진형(2026-10-03): 위협이 없으면 기함 둘레를 스스로 돈다(자리가 바뀐다) · 기함에서 AutoLeash 안 · 화면 안 · 기함과 겹치지 않음,
        /// 적 고속정이 나타나면 수상 교전 쪽으로 나간다.
        /// </summary>
        private IEnumerator AutonomousFormationCheck(Game.TaskForce.TaskForceEscortFormation formation, Game.Ship.ShipController ship)
        {
            var ext = formation.FlagshipExtent;
            var cam = Camera.main;
            int n = formation.EscortCount;
            var start = new Vector3[n];
            for (int i = 0; i < n; i++) start[i] = ship.transform.InverseTransformPoint(formation.GetEscort(i).transform.position);
            float maxDist = 0f, clearance = float.MaxValue, moved = 0f;
            int offScreen = 0;
            for (int k = 0; k < 16; k++)
            {
                yield return new WaitForSeconds(0.5f);
                for (int i = 0; i < n; i++)
                {
                    var esc = formation.GetEscort(i);
                    var lp = ship.transform.InverseTransformPoint(esc.transform.position);
                    maxDist = Mathf.Max(maxDist, new Vector2(lp.x, lp.z - (ext.x + ext.y) * 0.5f).magnitude);
                    float along = Mathf.Clamp(lp.z, ext.y, ext.x);
                    clearance = Mathf.Min(clearance, new Vector2(lp.x, lp.z - along).magnitude - ext.z);
                    if (k >= 6 && cam != null)
                    {
                        var vp = cam.WorldToViewportPoint(esc.transform.position);
                        if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) offScreen++;
                    }
                    if (k == 15) moved = Mathf.Max(moved, Vector3.Distance(lp, start[i]));
                }
            }
            var tasks = new StringBuilder();
            for (int i = 0; i < n; i++) tasks.Append($"{formation.GetInfo(i).RosterSlot + 1}번 {formation.AutonomousTask(i)} ");
            _report.AppendLine($"- 자율(위협 없음 8초): 할 일 {tasks}· 기함 중심에서 최대 {maxDist:0.0}m(한도 {Game.TaskForce.TaskForceEscortFormation.AutoLeash:0}) · " +
                               $"기함 현측과 최소 간격 {clearance:0.0}m · 화면 밖 {offScreen}회 · 스스로 자리 옮김 최대 {moved:0.0}m");
            if (maxDist > Game.TaskForce.TaskForceEscortFormation.AutoLeash + 8f) Fail("자율: 호위함이 기함에서 너무 멀리 감");
            if (clearance < 2f) Fail("자율: 호위함이 기함과 겹침");
            if (offScreen > 0) Fail("자율: 호위함이 화면 밖으로 나감");
            if (moved < 3f) Fail("자율: 위협이 없을 때 호위함이 스스로 움직이지 않음(초계)");
            yield return Shot("formation_autonomous");

            // 적 고속정이 우현 앞에 나타나면 수상 교전으로 나간다
            var spawner = EnemySpawner.Instance;
            var fastDef = CombatDevTools.FindEnemy("ene_fastboat");
            if (spawner == null || fastDef == null) { _report.AppendLine("- 자율 교전: 스포너·고속정 데이터가 없어 건너뜀"); yield break; }
            Vector3 spot = ship.transform.position + ship.transform.forward * 18f + ship.transform.right * 22f; spot.y = 0f;
            var foe = spawner.SpawnAt(fastDef, spot, Quaternion.LookRotation(-ship.transform.right));
            float before = float.MaxValue, after = float.MaxValue;
            for (int i = 0; i < n; i++) before = Mathf.Min(before, Vector3.Distance(formation.GetEscort(i).transform.position, foe.transform.position));
            yield return new WaitForSeconds(4f);
            int engaging = 0;
            for (int i = 0; i < n; i++)
            {
                if (formation.AutonomousTask(i) == "수상 교전") engaging++;
                if (foe != null) after = Mathf.Min(after, Vector3.Distance(formation.GetEscort(i).transform.position, foe.transform.position));
            }
            _report.AppendLine($"- 자율 교전: 적 고속정 출현 → 수상 교전 {engaging}척 · 가장 가까운 호위함 거리 {before:0}m → {after:0}m");
            if (engaging == 0) Fail("자율: 적 수상함이 나타나도 교전하러 나가지 않음");
            if (foe != null) foe.RemoveWithoutReward();
            yield return null;
        }

        /// <summary>
        /// 위치별 소나·폭뢰(2026-10-03): 자리 → 형태 판정(선수/예인/함내 소나, 투하대/발사대) · 형태를 위치로 다시 판정(앞을 막으면 함내 소나,
        /// 치우면 선수 소나) · 사방이 막힌 자리 설치 불가 · 이미 놓인 폭뢰를 사방으로 막는 설치 거부 · 형태 모델과 방향 ·
        /// 소나 방향·반경(선수 ±70°, 예인 1.5배·빠르면 절반, 함내 65%) · 실제 탐지 · 발사대(트인 현측만)·투하대(항적 위 4발) 실제 투하.
        /// </summary>
        private IEnumerator VariantCheck()
        {
            _report.AppendLine("\n## 위치별 소나·폭뢰");
            var ship = GameManager.Instance.Player;
            var grid = ship.Grid;
            var factory = Object.FindFirstObjectByType<ModuleFactory>();
            var field = Game.World.IslandField.Instance;
            if (field != null) { Game.World.IslandField.Disabled = true; field.ClearAll(); }
            CombatDevTools.ClearBattlefield();
            var off = DisableWeapons(null);
            foreach (var e in Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = false;
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(2f);
            if (grid == null || factory == null) { Fail("격자·ModuleFactory 없음"); yield break; }

            var sonarDef = CombatDevTools.FindModule("mod_sonar");
            var aswDef = CombatDevTools.FindModule("mod_asw");
            // 막는 칸에 쓸 1×1 "어디든" 블록
            ModuleDefinition filler = null;
            foreach (var id in new[] { "mod_radar", "mod_repairbay", "mod_ew", "mod_decoy", "mod_magazine", "mod_bridge" })
            {
                var d = CombatDevTools.FindModule(id);
                if (d != null && d.Width == 1 && d.Height == 1 && d.Placement.Zone == PlacementZone.Anywhere && d.MaxCount == 0) { filler = d; break; }
            }
            if (sonarDef == null || aswDef == null || filler == null) { Fail($"시험 블록 없음(소나 {sonarDef != null} · 폭뢰 {aswDef != null} · 막는 블록 {filler?.Id ?? "없음"})"); yield break; }
            _report.AppendLine($"- 폭뢰 배치 규칙 {aswDef.Placement.Zone} · 막는 블록 {filler.DisplayName}");
            if (aswDef.Placement.Zone != PlacementZone.SideOrStern) Fail("폭뢰 배치 규칙이 '옆이나 뒤가 트인 자리'가 아님");

            var added = new List<ModuleInstance>();
            ModuleInstance Put1(ModuleDefinition d, int x, int z)
            {
                var c = new GridCoord(x, z);
                if (!grid.CanPlace(d, c, 0, out _)) return null;
                var m = factory.Install(d, c, 0);
                if (m != null) added.Add(m);
                return m;
            }
            void Take(ModuleInstance m) { if (m == null) return; added.Remove(m); CombatDevTools.RemoveModule(m); }
            string V(ModuleInstance m) => m == null ? "(설치 실패)" : $"{ModuleVariants.Name(m.Variant)}({m.Variant})";
            string VisualName(ModuleInstance m)
            {
                var vv = m?.Runtime != null ? m.Runtime.GetComponent<ModuleVariantVisual>() : null;
                return vv != null && vv.Current != null ? vv.Current.name : "(기본 모델)";
            }
            float SocketAngle(ModuleInstance m, string socket, Vector3 want)
            {
                var vv = m?.Runtime != null ? m.Runtime.GetComponent<ModuleVariantVisual>() : null;
                var s = vv != null ? ModuleVariantVisual.Find(vv.Current, socket) : null;
                if (s == null) return 999f;
                Vector3 have = s.position - vv.Current.position; have.y = 0f; want.y = 0f;
                return Vector3.Angle(have, want);
            }

            // 선수·선미 끝 칸(가운데 줄)
            grid.GetExtent(out var min, out var max);
            int zBow = 0, zStern = 0;
            for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++) { if (!grid.IsFree(new GridCoord(max.X, z))) { zBow = z; if (z == 0) break; } }
            for (int z = -grid.MaxHalfBeam; z <= grid.MaxHalfBeam; z++) { if (!grid.IsFree(new GridCoord(min.X, z))) { zStern = z; if (z == 0) break; } }
            Vector3 fwd = ship.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // 1) 소나: 앞 끝 → 선수, 뒤 끝 → 예인, 앞을 막으면 함내 → 치우면 다시 선수(위치로 다시 판정)
            var bow = Put1(sonarDef, max.X + 1, zBow);
            var tas = Put1(sonarDef, min.X - 1, zStern);
            string bowV0 = V(bow), tasV0 = V(tas);
            string bowVis = VisualName(bow), tasVis = VisualName(tas);
            float bowAim = SocketAngle(bow, "ForwardMarker", fwd), tasAim = SocketAngle(tas, "ForwardMarker", fwd);
            var block = Put1(filler, max.X + 2, zBow);
            yield return null;
            string bowBlocked = V(bow), bowBlockedVis = VisualName(bow);
            var bowBlockedVariant = bow != null ? bow.Variant : ModuleVariant.None;
            bool blockPlaced = block != null;
            Take(block);
            yield return null;
            string bowAgain = V(bow);
            _report.AppendLine($"- 소나: 앞 끝 → {bowV0}[{bowVis}, 선수 표식 오차 {bowAim:0}°] · 뒤 끝 → {tasV0}[{tasVis}, 오차 {tasAim:0}°] · 앞을 막음 → {bowBlocked}[{bowBlockedVis}] · 다시 치움 → {bowAgain}");
            if (bow == null || bow.Variant != ModuleVariant.BowSonar) Fail("앞이 트인 자리의 소나가 선수 소나가 아님");
            if (tas == null || tas.Variant != ModuleVariant.TowedSonar) Fail("뒤가 트인 자리의 소나가 예인 소나가 아님");
            if (!blockPlaced) Fail("선수 소나 앞을 막는 블록을 놓지 못함(시험 무효)");
            else if (bowBlockedVariant != ModuleVariant.HullSonar) Fail("앞을 막아도 소나 형태가 다시 판정되지 않음(함내 소나여야 함)");
            if (bow != null && bow.Variant != ModuleVariant.BowSonar) Fail("막은 블록을 치워도 선수 소나로 돌아오지 않음");
            if (!bowVis.Contains("MOD_Sonar_Bow") || !tasVis.Contains("MOD_Sonar_TAS") || !bowBlockedVis.Contains("MOD_Sonar_Internal")) Fail("형태별 소나 모델이 붙지 않음");
            if (bowAim > 15f || tasAim > 15f) Fail("소나 모델이 함수 방향으로 놓이지 않음");
            yield return CloseShot("variant_sonar_bow", bow != null && bow.Runtime != null ? bow.Runtime.transform : ship.transform, 6f);
            yield return CloseShot("variant_sonar_tas", tas != null && tas.Runtime != null ? tas.Runtime.transform : ship.transform, 6f);

            // 2) 소나 탐지 범위: 선수만 / 예인만(정지·전속) / 함내만
            var sys = ship.Systems;
            float baseR = sonarDef.Stats.DetectionRange * RunUpgrades.DetectionMultiplier;
            Vector3 P(float along, float side) => ship.transform.position + fwd * along + right * side;
            Take(tas);
            sys.Recalculate();
            bool bowAhead = sys.SonarCovers(P(baseR * 0.8f, 0f)), bowAft = sys.SonarCovers(P(-baseR * 0.5f, 0f)), bowBeam = sys.SonarCovers(P(0f, baseR * 0.5f));
            Take(bow);
            tas = Put1(sonarDef, min.X - 1, zStern);
            sys.Recalculate();
            bool tasFar = sys.SonarCovers(P(-baseR * 1.3f, 0f));
            float tasRest = sys.SonarRange;
            ship.SetEngineOrder(1f);
            float t0 = Time.time;
            while (Time.time - t0 < 12f && sys.SpeedRatio < ModuleVariants.TowedFastSpeedRatio + 0.02f) yield return null;
            bool tasFarFast = sys.SonarCovers(P(-baseR * 1.3f, 0f));
            float tasFast = sys.SonarRange, speedRatio = sys.SpeedRatio;
            ship.SetEngineOrder(0f);
            Take(tas);
            // 함내 소나: 앞뒤를 모두 막은 소나
            var hull = Put1(sonarDef, max.X + 1, zBow);
            var hullFront = Put1(filler, max.X + 2, zBow);
            sys.Recalculate();
            bool hullNear = sys.SonarCovers(P(-baseR * 0.55f, 0f)), hullFar = sys.SonarCovers(P(-baseR * 0.8f, 0f));
            string hullV = V(hull);
            _report.AppendLine($"- 소나 범위(기본 {baseR:0}m): 선수 — 앞 {Yes(bowAhead)} · 뒤 {Yes(bowAft)} · 옆 {Yes(bowBeam)} / 예인 — 뒤 {baseR * 1.3f:0}m {Yes(tasFar)}(반경 {tasRest:0}) · 전속({speedRatio * 100f:0}%) {Yes(tasFarFast)}(반경 {tasFast:0}) / {hullV} — 뒤 {baseR * 0.55f:0}m {Yes(hullNear)} · {baseR * 0.8f:0}m {Yes(hullFar)}");
            if (!bowAhead || bowAft || bowBeam) Fail("선수 소나가 앞쪽 ±70°만 듣지 않음");
            if (!tasFar || !Mathf.Approximately(tasRest, baseR * ModuleVariants.TowedRangeMultiplier)) Fail("예인 소나가 정지 중 1.5배 반경으로 뒤를 듣지 않음");
            if (speedRatio >= ModuleVariants.TowedFastSpeedRatio && (tasFarFast || tasFast > baseR)) Fail("예인 소나가 빠를 때 반경이 줄지 않음");
            if (speedRatio < ModuleVariants.TowedFastSpeedRatio) _report.AppendLine("  - 참고: 12초 안에 최고 속력 70%에 닿지 않아 예인 소나 감속 판정은 생략");
            if (hull == null || hull.Variant != ModuleVariant.HullSonar || !hullNear || hullFar) Fail("함내 소나가 사방 65% 반경으로 듣지 않음");

            // 실제 탐지: 잠항 잠수함을 뒤 20m에 — 선수 소나는 못 찾고 예인 소나는 찾는다
            Take(hullFront); Take(hull);
            yield return new WaitForSeconds(3f);   // 전속에서 멈출 시간
            var subDef = CombatDevTools.FindEnemy("ene_submarine");
            bool Detect(ModuleInstance sonar, out SubmarineBase subOut)
            {
                subOut = null;
                var e = EnemySpawner.Instance.SpawnAt(subDef, P(-20f, 2f), Quaternion.LookRotation(fwd));
                if (e is SubmarineBase sb) { sb.DevFrozen = true; subOut = sb; }
                return subOut != null;
            }
            bow = Put1(sonarDef, max.X + 1, zBow);
            sys.Recalculate();
            bool bowLive = false, tasLive = false;
            if (Detect(bow, out var sub1))
            {
                t0 = Time.time;
                while (Time.time - t0 < 4f && !sub1.IsContactConfirmed) yield return null;
                bowLive = sub1.IsContactConfirmed;
            }
            CombatDevTools.ClearBattlefield();
            Take(bow);
            tas = Put1(sonarDef, min.X - 1, zStern);
            sys.Recalculate();
            if (Detect(tas, out var sub2))
            {
                t0 = Time.time;
                while (Time.time - t0 < 6f && !sub2.IsContactConfirmed) yield return null;
                tasLive = sub2.IsContactConfirmed;
            }
            CombatDevTools.ClearBattlefield();
            Take(tas);   // 선미 끝 자리를 투하대 시험에 비워 둔다
            _report.AppendLine($"- 실제 탐지(잠항 잠수함 뒤 20m): 선수 소나만 → 접촉 {Yes(bowLive)} · 예인 소나만 → 접촉 {Yes(tasLive)}");
            if (bowLive) Fail("선수 소나가 뒤쪽 잠수함을 찾음");
            if (!tasLive) Fail("예인 소나가 뒤쪽 잠수함을 찾지 못함");

            // 3) 폭뢰 자리: 뒤·양옆이 막힌 자리는 설치 불가 → 한쪽을 치우면 발사대 → 그 쪽을 다시 막는 설치는 거부
            int X1 = max.X + 1;
            int zc = zBow;   // 선수 끝의 가운데 칸 앞에 시험 구조(양옆 한 칸씩 필요)
            if (Mathf.Abs(zc) > grid.MaxHalfBeam - 1) Fail("선수 끝 칸이 함폭 끝이라 폭뢰 자리 시험 구조를 만들 수 없음");
            var a = Put1(filler, X1, zc);
            var e1 = Put1(filler, X1, zc + 1);
            var f1 = Put1(filler, X1, zc - 1);
            var pp = Put1(filler, X1 + 1, zc + 1);
            var qq = Put1(filler, X1 + 1, zc - 1);
            bool built = a != null && e1 != null && f1 != null && pp != null && qq != null;
            var T = new GridCoord(X1 + 1, zc);
            bool enclosedRefused = !grid.CanPlace(aswDef, T, 0, out string enclosedReason);
            Take(qq);
            var proj = Put1(aswDef, T.X, T.Z);
            string projV = V(proj), projSides = proj != null ? proj.Sides.ToString() : "-";
            bool reblockRefused = !grid.CanPlace(filler, new GridCoord(X1 + 1, zc - 1), 0, out string reblockReason);
            float projAim = SocketAngle(proj, "OutboardDirection", -right);
            _report.AppendLine($"- 폭뢰 자리: 시험 구조 {Yes(built)} · 뒤·양옆이 막힌 자리 설치 거부 {Yes(enclosedRefused)}(\"{enclosedReason}\") · 좌현을 열면 → {projV} 현측 {projSides}[{VisualName(proj)}, 바깥쪽 표식 오차 {projAim:0}°] · 다시 막는 설치 거부 {Yes(reblockRefused)}(\"{reblockReason}\")");
            if (!built) Fail("폭뢰 자리 시험 구조를 만들지 못함(시험 무효)");
            if (!enclosedRefused) Fail("사방이 막힌 자리에 폭뢰를 설치할 수 있음");
            if (proj == null || proj.Variant != ModuleVariant.DepthChargeProjector || (proj.Sides & ModuleSides.Port) == 0) Fail("옆이 트인 자리의 폭뢰가 좌현 발사대가 아님");
            if (!reblockRefused) Fail("이미 놓인 폭뢰의 옆·뒤를 모두 막는 설치가 허용됨");
            if (!VisualName(proj).Contains("MOD_DepthChargeProjector") || projAim > 20f) Fail("발사대 모델이 없거나 트인 현측을 향하지 않음");

            // 선미 투하대
            var rack = Put1(aswDef, min.X - 1, zStern);
            float rackAim = SocketAngle(rack, "ForwardMarker", fwd);
            _report.AppendLine($"- 뒤 끝 폭뢰 → {V(rack)}[{VisualName(rack)}, 선수 표식 오차 {rackAim:0}°]");
            if (rack == null || rack.Variant != ModuleVariant.DepthChargeRack) Fail("뒤가 트인 자리의 폭뢰가 투하대가 아님");
            if (!VisualName(rack).Contains("MOD_DepthChargeRack") || rackAim > 20f) Fail("투하대 모델이 없거나 방향이 틀림");
            yield return CloseShot("variant_projector", proj != null && proj.Runtime != null ? proj.Runtime.transform : ship.transform, 7f);
            yield return CloseShot("variant_rack", rack != null && rack.Runtime != null ? rack.Runtime.transform : ship.transform, 6f);

            // 4) 실제 투하·발사: 드러난 잠수함을 고정해 두고 해당 폭뢰만 켠다
            var projRt = proj?.Runtime as Game.Modules.Runtime.AswLauncherModule;
            var rackRt = rack?.Runtime as Game.Modules.Runtime.AswLauncherModule;
            foreach (var r in Object.FindObjectsByType<Game.Modules.Runtime.AswLauncherModule>(FindObjectsSortMode.None)) r.enabled = false;
            IEnumerator Engage(Game.Modules.Runtime.AswLauncherModule rt, Vector3 at, float wait, System.Action<int, float, List<Vector3>> done)
            {
                var e = EnemySpawner.Instance.SpawnAt(subDef, at, Quaternion.LookRotation(fwd));
                var sb = e as SubmarineBase;
                if (sb != null) { sb.DevFrozen = true; sb.RevealFor(30f); }
                float hp0 = sb != null ? sb.CurrentHp : 0f;
                int s0 = rt != null ? rt.Salvos : 0;
                if (rt != null) { Put(rt, "_cooldown", 0f); rt.enabled = true; }
                float tt = Time.time;
                while (Time.time - tt < wait && (rt == null || rt.Salvos == s0)) yield return null;
                yield return new WaitForSeconds(2.5f);   // 비행 + 가라앉음
                if (rt != null) rt.enabled = false;
                done(rt != null ? rt.Salvos - s0 : 0, sb != null ? hp0 - sb.CurrentHp : 0f, rt != null ? new List<Vector3>(rt.LastAims) : new List<Vector3>());
                CombatDevTools.ClearBattlefield();
                yield return null;
            }
            Vector3 projPos = proj != null && proj.Runtime != null ? proj.Runtime.transform.position : ship.transform.position;
            int pPort = 0, pStar = 0; float pPortLoss = 0f;
            yield return Engage(projRt, projPos - right * 14f, 4f, (n, loss, _) => { pPort = n; pPortLoss = loss; });
            yield return Engage(projRt, projPos + right * 14f, 3f, (n, _, __) => pStar = n);
            Vector3 dropOrigin = rackRt != null ? rackRt.RackDropOrigin : ship.transform.position;
            int rAft = 0, rBeam = 0; float rLoss = 0f; List<Vector3> rAims = null;
            yield return Engage(rackRt, dropOrigin - fwd * 3f + right * 1f, 4f, (n, loss, aims) => { rAft = n; rLoss = loss; rAims = aims; });
            yield return Engage(rackRt, (rack != null && rack.Runtime != null ? rack.Runtime.transform.position : ship.transform.position) + right * 16f, 3f, (n, _, __) => rBeam = n);
            int behind = 0;
            if (rAims != null) foreach (var p in rAims) if (Vector3.Dot(p - dropOrigin, -fwd) >= -1.5f && Mathf.Abs(Vector3.Dot(p - dropOrigin, right)) <= 3.5f) behind++;
            _report.AppendLine($"- 발사대(좌현): 좌현 14m 잠수함 → {pPort}회 발사 · 피해 {pPortLoss:0} / 우현 14m → {pStar}회 · 투하대: 항적 3m 뒤 → {rAft}회 {rAims?.Count ?? 0}발(항적 위 {behind}발) · 피해 {rLoss:0}(기본 {aswDef.Stats.Damage}×{ModuleVariants.RackDamageMultiplier}) / 옆 16m → {rBeam}회");
            if (pPort < 1 || pPortLoss <= 0f) Fail("발사대가 트인 현측의 잠수함을 공격하지 않음");
            if (pStar > 0) Fail("발사대가 막힌 현측으로 던짐");
            if (rAft < 1 || (rAims?.Count ?? 0) != ModuleVariants.RackSalvo || behind != ModuleVariants.RackSalvo || rLoss <= 0f) Fail("투하대가 항적 위에 4발을 떨어뜨려 맞히지 않음");
            if (rBeam > 0) Fail("투하대가 옆 멀리 있는 잠수함에 던짐(사거리가 없어야 함)");

            // 정리
            for (int i = added.Count - 1; i >= 0; i--) CombatDevTools.RemoveModule(added[i]);
            added.Clear();
            ship.Systems?.Recalculate();
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            foreach (var d in off) if (d != null) d.enabled = true;
            CombatDevTools.ClearBattlefield();
            if (field != null) { Game.World.IslandField.Disabled = false; field.Regenerate(0); }
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

        // ------------------------------------------------------------ 하단 HUD 배치

        /// <summary>
        /// 하단 HUD 덩어리: 레벨 막대 · 스킬 줄 · [레이더|함 현황|무장]이 겹치지 않고 화면 안에 있는지,
        /// 스킬 그림이 준비·재장전·장비 없음을 구분하는지 보고 캡처한다.
        /// </summary>
        private IEnumerator HudLayoutCheck()
        {
            _report.AppendLine("\n## 하단 HUD 배치");
            CombatDevTools.ClearBattlefield();
            // 스킬 장비를 갖춘다: 전자전(재밍), 수리반(응급 수리), 기만체 발사기(연막)
            foreach (var id in new[] { "mod_ew", "mod_repairbay", "mod_decoy" }) CombatDevTools.InstallModule(id);
            yield return new WaitForSeconds(1f);

            var names = new[] { "Level bar", "Skills", "Radar scope", "Ship status", "Weapon status" };
            var rects = new List<(string, Rect)>();
            var hud = Object.FindFirstObjectByType<Game.UI.HUDView>();
            foreach (var n in names)
            {
                var t = hud != null ? FindChild(hud.transform, n) as RectTransform : null;
                if (t == null) { Fail($"HUD 패널 없음: {n}"); continue; }
                var c = new Vector3[4];
                t.GetWorldCorners(c);
                var canvas = t.GetComponentInParent<Canvas>().rootCanvas;
                float s = canvas.transform.lossyScale.x;
                rects.Add((n, new Rect(c[0].x / s, c[0].y / s, (c[2].x - c[0].x) / s, (c[2].y - c[0].y) / s)));
            }
            var sb = new StringBuilder();
            foreach (var (n, r) in rects) sb.Append($"{n} ({r.xMin:0},{r.yMin:0})~({r.xMax:0},{r.yMax:0}) · ");
            _report.AppendLine($"- 패널(캔버스 좌표): {sb}");
            if (rects.Count == 5)
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                foreach (var (_, r) in rects) { minX = Mathf.Min(minX, r.xMin); maxX = Mathf.Max(maxX, r.xMax); }
                var root = hud.GetComponentInParent<Canvas>().rootCanvas.GetComponent<RectTransform>();
                float canvasW = root.rect.width;
                _report.AppendLine($"- 덩어리 가로 {minX:0}~{maxX:0} (캔버스 폭 {canvasW:0}, 왼쪽 여백 {minX:0} · 오른쪽 여백 {canvasW - maxX:0})");
                if (Mathf.Abs(minX - (canvasW - maxX)) > 2f) Fail("하단 HUD가 가운데가 아님");
            }
            for (int i = 0; i < rects.Count; i++)
                for (int j = i + 1; j < rects.Count; j++)
                {
                    var a = rects[i].Item2; var b = rects[j].Item2;
                    var inter = Rect.MinMaxRect(Mathf.Max(a.xMin, b.xMin), Mathf.Max(a.yMin, b.yMin), Mathf.Min(a.xMax, b.xMax), Mathf.Min(a.yMax, b.yMax));
                    if (inter.width > 0.5f && inter.height > 0.5f) Fail($"HUD 패널 겹침: {rects[i].Item1} / {rects[j].Item1}");
                }

            // 스킬: 기만체·전속은 쓰고(재장전), 나머지는 준비 상태로
            GameEvents.RaiseDecoyRequested();
            GameEvents.RaiseSkillRequested(ActiveSkillId.Flank);
            var ship = GameManager.Instance.Player;
            ship.DevRudderOverride = -0.6f;
            ship.SetEngineOrder(0.75f);
            yield return new WaitForSeconds(2.5f);
            yield return RadarShot("hud_layout");
            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
            _report.AppendLine("- 캡처: hud_layout.png (기만체·전속 재장전 중, 좌현 타)");
        }

        // ------------------------------------------------------------ 조함

        /// <summary>
        /// 실제 배식 조함: 전령기 단계 가속, 전령기를 내린 뒤 미끄러짐, 타각 전환 시간, 선회 반경, 타를 푼 뒤 계속 도는 각도.
        /// </summary>
        private IEnumerator HelmCheck()
        {
            _report.AppendLine("\n## 조함(전령기·방향타)");
            var ship = GameManager.Instance.Player;
            var t = ship.transform;
            CombatDevTools.ClearBattlefield();
            ship.DevRudderOverride = 0f;
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(4f);

            // 1) 전령기 한 칸씩: STOP → 1/4 → 1/2
            ship.SetEngineOrder(0f);
            ship.ShiftEngineOrder(+1);
            float o1 = ship.ThrottleInput;
            ship.ShiftEngineOrder(+1);
            float o2 = ship.ThrottleInput;
            ship.ShiftEngineOrder(-1); ship.ShiftEngineOrder(-1); ship.ShiftEngineOrder(-1);
            float oRev = ship.ThrottleInput;
            ship.ShiftEngineOrder(-1);
            float oRev2 = ship.ThrottleInput;
            _report.AppendLine($"- 전령기: STOP+1 → {o1}, +1 → {o2}, -3 → {oRev}, 더 내려도 {oRev2}");
            if (!Mathf.Approximately(o1, 0.25f) || !Mathf.Approximately(o2, 0.5f) || oRev >= 0f || !Mathf.Approximately(oRev, oRev2))
                Fail("전령기 단계 이동이 이상함");

            // 2) 가속: STOP → FULL, 입력 없이도 유지
            ship.SetEngineOrder(0f);
            yield return new WaitForSeconds(3f);
            ship.SetEngineOrder(1f);
            float start = Time.time, reach90 = -1f;
            while (Time.time - start < 8f)
            {
                if (reach90 < 0f && ship.CurrentSpeed >= ship.BaseMaxSpeed * 0.9f) reach90 = Time.time - start;
                yield return null;
            }
            float cruise = ship.CurrentSpeed;
            _report.AppendLine($"- FULL: 최고속력 90%까지 {reach90:0.0}초, 8초 뒤 {cruise:0.0} m/s (키를 누르지 않아도 유지)");
            if (reach90 < 0f) Fail("FULL에서 최고속력에 닿지 않음");

            // 3) 선회: 타 우현 최대. 타각 전환 시간, 선회율, 선회 반경(한 바퀴 궤적)
            ship.DevRudderOverride = 1f;
            start = Time.time;
            float fullRudderAt = -1f;
            float headingPrev = t.eulerAngles.y, turned = 0f;
            var minP = new Vector2(float.MaxValue, float.MaxValue);
            var maxP = new Vector2(float.MinValue, float.MinValue);
            float minSpeed = cruise;
            while (Time.time - start < 25f && turned < 400f)
            {
                if (fullRudderAt < 0f && ship.RudderAngle >= ship.MaxRudderAngle - 0.01f) fullRudderAt = Time.time - start;
                float h = t.eulerAngles.y;
                turned += Mathf.DeltaAngle(headingPrev, h);
                headingPrev = h;
                if (turned > 45f)
                {
                    var p = new Vector2(t.position.x, t.position.z);
                    minP = Vector2.Min(minP, p);
                    maxP = Vector2.Max(maxP, p);
                    minSpeed = Mathf.Min(minSpeed, ship.CurrentSpeed);
                }
                yield return null;
            }
            float circle = turned >= 400f ? Mathf.Max(maxP.x - minP.x, maxP.y - minP.y) : -1f;
            float yawRate = ship.YawRate;
            _report.AppendLine($"- 타 우현 최대: 타각 {ship.MaxRudderAngle:0}°까지 {fullRudderAt:0.0}초 · 선회율 {yawRate:0.0}°/초 · 선회 지름 약 {circle:0.0}m · 선회 중 속력 {minSpeed:0.0} m/s");
            if (fullRudderAt < 0f) Fail("타각이 최대에 닿지 않음");
            if (circle < 0f) Fail("한 바퀴 선회를 마치지 못함");
            if (yawRate <= 0f) Fail("우현 타에 우현으로 돌지 않음");
            yield return RadarShot("helm_turn");

            // 4) 타 중앙: 타를 풀어도 한동안 더 돈다
            ship.DevRudderOverride = 0f;
            headingPrev = t.eulerAngles.y;
            float carry = 0f;
            start = Time.time;
            while (Time.time - start < 6f)
            {
                float h = t.eulerAngles.y;
                carry += Mathf.DeltaAngle(headingPrev, h);
                headingPrev = h;
                yield return null;
            }
            _report.AppendLine($"- 타 중앙으로 돌린 뒤 더 돈 각도: {carry:0}° · 6초 뒤 선회율 {ship.YawRate:0.00}°/초");
            if (carry < 5f) Fail("타를 풀자마자 선회가 멈춤(관성 없음)");
            if (Mathf.Abs(ship.YawRate) > 1f) Fail("타 중앙인데 계속 돔");

            // 5) STOP: 전령기를 내려도 미끄러진다
            ship.SetEngineOrder(0f);
            Vector3 p0 = t.position;
            start = Time.time;
            float stopAt = -1f;
            while (Time.time - start < 10f)
            {
                if (stopAt < 0f && ship.CurrentSpeed < 0.3f) stopAt = Time.time - start;
                yield return null;
            }
            float glide = Vector3.Distance(new Vector3(p0.x, 0, p0.z), new Vector3(t.position.x, 0, t.position.z));
            _report.AppendLine($"- STOP: 멈추기까지 {stopAt:0.0}초, 미끄러진 거리 {glide:0.0}m");
            if (stopAt < 1f) Fail("STOP에 곧바로 멈춤");

            // 6) 정지 상태 타: 거의 돌지 않는다
            ship.DevRudderOverride = 1f;
            yield return new WaitForSeconds(2f);
            float idleYaw = ship.YawRate;
            _report.AppendLine($"- 정지 상태 최대 타 선회율: {idleYaw:0.0}°/초 (항주 중 {yawRate:0.0})");
            if (Mathf.Abs(idleYaw) > Mathf.Abs(yawRate) * 0.3f) Fail("정지 상태에서 너무 잘 돔");

            ship.DevRudderOverride = null;
            ship.SetEngineOrder(0f);
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

        // ------------------------------------------------------------ Tab 현황판

        /// <summary>모듈을 가득 설치해도 Tab 현황판이 창 안에 들어가는지(두 단·글자 줄이기·생략) 보고 캡처한다.</summary>
        private IEnumerator TabLayoutCheck()
        {
            _report.AppendLine("\n## Tab 현황판 레이아웃");
            var ui = Object.FindFirstObjectByType<Game.UI.ModuleStatusUI>(FindObjectsInactive.Include);
            if (ui == null) { Fail("ModuleStatusUI 없음"); yield break; }

            string[] fill = { "mod_autocannon", "mod_ciws", "mod_magazine", "mod_radar", "mod_sonar", "mod_asw", "mod_gun76", "mod_sam", "mod_rocket", "mod_decoy", "mod_repairbay", "mod_ew" };
            int[] targets = { 12, 30, 60 };
            foreach (int target in targets)
            {
                var grid = GameManager.Instance.Player.Grid;
                int guard = 0;
                while (grid.Modules.Count < target && guard++ < 400)
                    if (!CombatDevTools.InstallModule(fill[guard % fill.Length]) && guard > 200) break;

                // 몇 개는 부서지거나 탄이 떨어진 상태로
                int n = 0;
                foreach (var m in grid.Modules)
                    if (m.Definition.Type != ModuleType.Bridge && ++n % 7 == 0) m.TakeDamage(m.MaxHp * 0.8f);

                ui.SetOpen(true);
                yield return null;
                yield return null;
                var l = ui.LastLayout;
                _report.AppendLine($"- 모듈 {grid.Modules.Count}개: 표시 {l.shown} · 생략 {l.hidden} · 글자 {l.fontSize:0} · {l.columns}단");
                if (l.shown + l.hidden != grid.Modules.Count) Fail($"Tab 현황판 줄 수 불일치({l.shown}+{l.hidden} ≠ {grid.Modules.Count})");
                if (grid.Modules.Count <= 40 && l.hidden > 0) Fail($"모듈 {grid.Modules.Count}개인데 생략이 생김");
                yield return RadarShot($"tab_{grid.Modules.Count}");
                ui.SetOpen(false);
            }
            ResetLoadout();
            CombatDevTools.InstallTestLoadout();
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
