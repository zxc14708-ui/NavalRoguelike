#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Game.Core;
using Game.Enemies;
using Game.Progression;
using Game.Ship;

namespace Game.Dev
{
    /// <summary>
    /// 성능 측정(2026-10-12 점검 O04, -perfOnly). 가설 목표 1080p 60fps(프레임 16.7ms)에 대해 세 장면을 잰다.
    ///   1. 항구 시작 화면(출항 전) 15초
    ///   2. 일반 전투: 실제 스테이지·스포너를 켠 채 -perfMinutes 분(기본 3, 30이면 30분 전투). 함선은 죽지 않게 하고 천천히 돈다.
    ///      레벨업 정비는 끄고, 항로·교리 선택이 뜨면 기본값으로 고른다. 10초마다 시간대별 줄(perf_timeline.csv) — 메모리 증가를 본다.
    ///   3. 최대 편대 포화전: 호위함 4척 T3(역할 넷) + 시험 무장, 고속정·미사일정·잠수함·드론·전투기 + 3초마다 미사일 8발, 40초.
    /// 장면마다: 프레임 시간(평균·p50·p95·p99·최대·16.7/33.3ms 초과 비율), 메인 스레드·GPU 시간(FrameTimingManager — 빌드 설정의
    /// Frame Timing Stats가 켜져 있어야, 검증 빌드는 켜서 만든다), GC(프레임당 할당·횟수), 메모리(시작→끝), 배치·SetPass·삼각형.
    /// 측정 중에는 수직 동기화·프레임 제한을 끄고(여유를 보려고) 시간 배속 1. 개발 빌드·배치 모드라 실제 출시 빌드보다 느리게 나온다.
    /// 결과는 보고서 표, perf.csv(장면별), perf_timeline.csv. 목표 미달은 실패가 아니라 기록이다(기준선 측정).
    /// </summary>
    public partial class CombatVerificationRunner
    {
        [Tooltip("성능 측정만 돌린다(-perfOnly): 항구·일반 전투(-perfMinutes, 기본 3분)·최대 편대 포화전")]
        public bool PerfOnly;
        [Tooltip("성능 측정의 일반 전투 길이(분). -perfMinutes <분>")]
        public float PerfMinutes = 3f;

        private const float TargetFrameMs = 1000f / 60f;

        private sealed class PerfWindow : IDisposable
        {
            public readonly string Name;
            private readonly List<float> _frame = new(4096), _main = new(4096), _gpu = new(4096);
            private readonly FrameTiming[] _timing = new FrameTiming[1];
            private ProfilerRecorder _gcRec, _batchRec, _setPassRec, _triRec, _sysMemRec;
            private long _gcBytes, _memStart, _monoStart, _sysStart;
            private double _batches, _setPass, _tris;
            private long _batchMax;
            private int _gcStart, _counted, _playingFrames;
            private float _t0;

            public PerfWindow(string name) => Name = name;

            public void Begin()
            {
                _gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
                _batchRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                _setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                _triRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
                _sysMemRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
                _gcStart = GC.CollectionCount(0);
                _memStart = Profiler.GetTotalAllocatedMemoryLong();
                _monoStart = Profiler.GetMonoUsedSizeLong();
                _sysStart = _sysMemRec.Valid ? _sysMemRec.LastValue : 0;
                _t0 = Time.realtimeSinceStartup;
            }

            public float Elapsed => Time.realtimeSinceStartup - _t0;

            /// <summary>매 프레임 한 번(yield return null 뒤).</summary>
            public void Sample()
            {
                _frame.Add(Time.unscaledDeltaTime * 1000f);
                if (_sysStart == 0 && _sysMemRec.Valid) _sysStart = _sysMemRec.LastValue;   // 첫 프레임 전에는 0
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, _timing) > 0)
                {
                    if (_timing[0].cpuMainThreadFrameTime > 0) _main.Add((float)_timing[0].cpuMainThreadFrameTime);
                    if (_timing[0].gpuFrameTime > 0) _gpu.Add((float)_timing[0].gpuFrameTime);
                }
                if (_gcRec.Valid) _gcBytes += _gcRec.LastValue;
                if (_batchRec.Valid)
                {
                    _batches += _batchRec.LastValue;
                    _batchMax = Math.Max(_batchMax, _batchRec.LastValue);
                }
                if (_setPassRec.Valid) _setPass += _setPassRec.LastValue;
                if (_triRec.Valid) _tris += _triRec.LastValue;
                _counted++;
                if (GameManager.Instance != null && GameManager.Instance.State == GameState.Playing) _playingFrames++;
            }

            public int Frames => _frame.Count;
            public float RecentAverageMs(int frames)
            {
                int n = Math.Min(frames, _frame.Count);
                if (n == 0) return 0f;
                float sum = 0f;
                for (int i = _frame.Count - n; i < _frame.Count; i++) sum += _frame[i];
                return sum / n;
            }

            private static float Pct(List<float> v, float p)
            {
                if (v.Count == 0) return 0f;
                var s = new List<float>(v);
                s.Sort();
                return s[Mathf.Clamp(Mathf.CeilToInt(p * s.Count) - 1, 0, s.Count - 1)];
            }

            private static float Avg(List<float> v)
            {
                if (v.Count == 0) return 0f;
                double sum = 0;
                foreach (var x in v) sum += x;
                return (float)(sum / v.Count);
            }

            private static float Max(List<float> v)
            {
                float m = 0f;
                foreach (var x in v) m = Mathf.Max(m, x);
                return m;
            }

            private float Over(float ms)
            {
                if (_frame.Count == 0) return 0f;
                int n = 0;
                foreach (var x in _frame) if (x > ms) n++;
                return n * 100f / _frame.Count;
            }

            public (string table, string csv, bool meets) End()
            {
                float seconds = Elapsed;
                int frames = _frame.Count;
                float fps = seconds > 0f ? frames / seconds : 0f;
                float p95 = Pct(_frame, 0.95f);
                int gcCount = GC.CollectionCount(0) - _gcStart;
                double mb = 1024.0 * 1024.0;
                long mem = Profiler.GetTotalAllocatedMemoryLong(), mono = Profiler.GetMonoUsedSizeLong();
                long sys = _sysMemRec.Valid ? _sysMemRec.LastValue : 0;
                string gpu = _gpu.Count > 0 ? $"{Avg(_gpu):0.0}/{Pct(_gpu, 0.95f):0.0}" : "측정 불가";
                string main = _main.Count > 0 ? $"{Avg(_main):0.0}/{Pct(_main, 0.95f):0.0}" : "측정 불가";
                float gcKb = _counted > 0 ? (float)(_gcBytes / 1024.0 / _counted) : 0f;
                bool meets = fps >= 60f && p95 <= TargetFrameMs;
                string playing = _counted > 0 ? $"{_playingFrames * 100f / _counted:0}%" : "-";
                string table =
                    $"| {Name} | {seconds:0}초 · {frames} | **{fps:0.0}** | {Avg(_frame):0.0} / {Pct(_frame, 0.5f):0.0} / **{p95:0.0}** / {Pct(_frame, 0.99f):0.0} / {Max(_frame):0} | " +
                    $"{Over(TargetFrameMs):0.0}% · {Over(33.3f):0.0}% | {main} | {gpu} | {gcKb:0.0}KB · {_gcBytes / mb:0.0}MB · {gcCount}회 | " +
                    $"{_memStart / mb:0}→{mem / mb:0} (Mono {_monoStart / mb:0}→{mono / mb:0}, 시스템 {_sysStart / mb:0}→{sys / mb:0}) | " +
                    $"{(_counted > 0 ? _batches / _counted : 0):0}/{_batchMax} · {(_counted > 0 ? _setPass / _counted : 0):0} · {(_counted > 0 ? _tris / _counted / 1000.0 : 0):0}k | {playing} | {(meets ? "충족" : "미달")} |";
                string csv = string.Join(",", Name, seconds.ToString("0.0"), frames, fps.ToString("0.00"), Avg(_frame).ToString("0.00"),
                    Pct(_frame, 0.5f).ToString("0.00"), p95.ToString("0.00"), Pct(_frame, 0.99f).ToString("0.00"), Max(_frame).ToString("0.0"),
                    Over(TargetFrameMs).ToString("0.0"), Over(33.3f).ToString("0.0"),
                    _main.Count > 0 ? Avg(_main).ToString("0.00") : "", _main.Count > 0 ? Pct(_main, 0.95f).ToString("0.00") : "",
                    _gpu.Count > 0 ? Avg(_gpu).ToString("0.00") : "", _gpu.Count > 0 ? Pct(_gpu, 0.95f).ToString("0.00") : "",
                    gcKb.ToString("0.00"), gcCount, (_memStart / mb).ToString("0"), (mem / mb).ToString("0"), (mono / mb).ToString("0"),
                    (sys / mb).ToString("0"), (_counted > 0 ? _batches / _counted : 0).ToString("0"), _batchMax,
                    (_counted > 0 ? _setPass / _counted : 0).ToString("0"), (_counted > 0 ? _tris / _counted : 0).ToString("0"));
                return (table, csv, meets);
            }

            public void Dispose()
            {
                _gcRec.Dispose();
                _batchRec.Dispose();
                _setPassRec.Dispose();
                _triRec.Dispose();
                _sysMemRec.Dispose();
            }
        }

        private readonly List<string> _perfRows = new(), _perfCsv = new();
        private readonly List<string> _perfMisses = new();
        private int _perfSkips;   // 일반 전투 중 자동으로 넘긴 항로·교리 선택·보스 보상 정비 수

        private IEnumerator Measure(string name, float seconds, Func<PerfWindow, IEnumerator> during = null)
        {
            using var w = new PerfWindow(name);
            w.Begin();
            var driver = during != null ? during(w) : null;
            while (w.Elapsed < seconds)
            {
                yield return null;
                w.Sample();
                if (driver != null && !driver.MoveNext()) driver = null;   // 장면 진행(적 추가 등)을 같은 프레임에 한 걸음씩
            }
            var (table, csv, meets) = w.End();
            _perfRows.Add(table);
            _perfCsv.Add(csv);
            if (!meets) _perfMisses.Add(name);
        }

        private IEnumerator PerfCheck()
        {
            _report.AppendLine("\n## 성능 측정 (O04)");
            _report.AppendLine($"- 가설 목표: 1080p에서 60fps(프레임 {TargetFrameMs:0.0}ms) — 평균 FPS ≥ 60 그리고 p95 프레임 ≤ {TargetFrameMs:0.0}ms면 충족");
            _report.AppendLine($"- 측정 조건: {Screen.width}×{Screen.height} · 품질 {QualitySettings.names[QualitySettings.GetQualityLevel()]} · 항구 화질 {DisplaySettings.QualityNames[DisplaySettings.HarborQuality]} · " +
                               $"장식 효과 {DisplaySettings.QualityNames[DisplaySettings.EffectsQuality]} · 수직 동기화·프레임 제한 끔 · 배속 1 · " +
                               $"{(Debug.isDebugBuild ? "개발 빌드" : "출시 빌드")}{(Application.isBatchMode ? " · 배치 모드(창 없음)" : "")}");
            _report.AppendLine($"- CPU {SystemInfo.processorType} ({SystemInfo.processorCount}) · RAM {SystemInfo.systemMemorySize}MB · GPU {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}, {SystemInfo.graphicsMemorySize}MB)");
            int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.timeScale = 1f;
            bool background = Application.runInBackground;
            Application.runInBackground = true;   // 창 모드에서 다른 창을 눌러도 멈추지 않게(빌드 설정은 끔)

            // 1. 항구 시작 화면(출항 전)
            yield return new WaitForSecondsRealtime(2f);
            yield return Measure("항구 시작 화면", 15f);

            // 2. 일반 전투: 실제 스테이지 진행, 함선은 죽지 않게
            LaunchFromStartScreen();
            yield return new WaitForSecondsRealtime(2f);
            foreach (var x in UnityEngine.Object.FindObjectsByType<ExperienceSystem>(FindObjectsSortMode.None)) x.enabled = false;   // 정비 화면이 멈추지 않게
            var ship = GameManager.Instance != null ? GameManager.Instance.Player : null;
            MakeInvulnerable(ship);
            var healthy = StartCoroutine(KeepModulesHealthy());
            if (ship != null)
            {
                ship.SetEngineOrder(0.5f);
                ship.DevRudderOverride = 0.25f;   // 큰 원을 그리며 새 바다·섬을 계속 만난다
            }
            var timeline = new StringBuilder("seconds,fps_10s,frame_ms_10s,total_alloc_mb,mono_mb,enemies,state\n");
            float minutes = Mathf.Max(0.5f, PerfMinutes);
            yield return Measure($"일반 전투 {minutes:0.#}분", minutes * 60f, w => BattleDriver(w, timeline));
            File.WriteAllText(Path.Combine(OutputDirectory, "perf_timeline.csv"), timeline.ToString(), new UTF8Encoding(false));
            _report.AppendLine($"- 일반 전투 중 자동으로 넘긴 선택 화면(항로·교리·보스 보상): {_perfSkips}번");

            // 3. 최대 편대 포화전
            foreach (var d in UnityEngine.Object.FindObjectsByType<StageDirector>(FindObjectsSortMode.None)) d.enabled = false;
            if (EnemySpawner.Instance != null) EnemySpawner.Instance.enabled = false;
            CombatDevTools.ClearBattlefield();
            int escorts = DeployMaxFleet(ship);
            int installed = CombatDevTools.InstallTestLoadout();
            yield return new WaitForSecondsRealtime(2f);
            _report.AppendLine($"- 포화전 준비: 호위함 {escorts}척(T3) · 시험 무장 {installed}개 추가");
            yield return Measure("최대 편대 포화전 40초", 40f, SaturationDriver);

            if (ship != null) { ship.DevRudderOverride = null; ship.SetEngineOrder(0f); }
            StopCoroutine(healthy);
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = target;
            Application.runInBackground = background;

            _report.AppendLine();
            _report.AppendLine("| 장면 | 시간·프레임 | 평균 FPS | 프레임 ms 평균/p50/**p95**/p99/최대 | 16.7·33.3ms 초과 | 메인 스레드 ms 평균/p95 | GPU ms 평균/p95 | GC 프레임당·합계·횟수 | 메모리 MB 시작→끝 | 배치 평균/최대·SetPass·삼각형 | 전투 상태 | 목표 |");
            _report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in _perfRows) _report.AppendLine(row);
            _report.AppendLine(_perfMisses.Count == 0
                ? "- 세 장면 모두 가설 목표 충족"
                : $"- 가설 목표 미달: {string.Join(", ", _perfMisses)} (실패로 세지 않음 — 기준선 기록)");
            _report.AppendLine("- 개발 빌드는 출시 빌드보다 느리다(검사 코드·개발 표시 포함).");
            if (Application.isBatchMode)
                _report.AppendLine("- **배치 모드는 화면을 그리지 않아(배치 0·GPU 측정 불가) 게임 논리만 잰 값이다** — 실제 성능은 -batchmode 없이 창 모드로 잰다.");

            var csv = new StringBuilder("scene,seconds,frames,fps,frame_avg_ms,frame_p50_ms,frame_p95_ms,frame_p99_ms,frame_max_ms,over_16_7_pct,over_33_3_pct," +
                                        "main_avg_ms,main_p95_ms,gpu_avg_ms,gpu_p95_ms,gc_kb_per_frame,gc_count,mem_start_mb,mem_end_mb,mono_mb,system_mb," +
                                        "batches_avg,batches_max,setpass_avg,tris_avg\n");
            foreach (var row in _perfCsv) csv.AppendLine(row);
            File.WriteAllText(Path.Combine(OutputDirectory, "perf.csv"), csv.ToString(), new UTF8Encoding(false));
            _report.AppendLine("- 원자료: perf.csv(장면별) · perf_timeline.csv(일반 전투 10초 간격)");
        }

        private static void MakeInvulnerable(ShipController ship)
        {
            if (ship == null) return;
            var prop = typeof(ShipController).GetProperty(nameof(ShipController.HullMaxHp));
            prop?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
            prop = typeof(ShipController).GetProperty(nameof(ShipController.HullHp));
            prop?.GetSetMethod(true)?.Invoke(ship, new object[] { 100000f });
        }

        /// <summary>일반 전투 진행: 항로·교리 선택을 기본값으로 넘기고, 10초마다 시간대별 줄을 남긴다.</summary>
        private IEnumerator BattleDriver(PerfWindow w, StringBuilder timeline)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<Game.UI.HUDView>();
            var refit = UnityEngine.Object.FindFirstObjectByType<Game.Refit.RefitController>();
            float nextLine = 10f, refitSince = -1f;
            while (true)
            {
                if (hud != null)
                {
                    if (Get<GameObject>(hud, "_routeChoice") != null) { Call(hud, "ChooseRoute", false); _perfSkips++; }
                    if (Get<GameObject>(hud, "_doctrineChoice") != null) { Call(hud, "ChooseDoctrine", NavalDoctrine.AirDefense); _perfSkips++; }
                }
                // 보스 격침 보상 정비(레벨업 정비는 꺼 두었다): 1초 뒤 카드 없이 닫고 다음 진행으로
                var state = GameManager.Instance != null ? GameManager.Instance.State : GameState.Playing;
                if (state == GameState.Refit && refit != null)
                {
                    if (refitSince < 0f) refitSince = w.Elapsed;
                    else if (w.Elapsed - refitSince > 1f) { Call(refit, "Resume"); refitSince = -1f; _perfSkips++; }
                }
                else refitSince = -1f;
                if (w.Elapsed >= nextLine)
                {
                    nextLine += 10f;
                    float ms = w.RecentAverageMs(600);
                    var gm = GameManager.Instance;
                    timeline.AppendLine(string.Join(",", w.Elapsed.ToString("0"), (ms > 0f ? 1000f / ms : 0f).ToString("0.0"), ms.ToString("0.00"),
                        (Profiler.GetTotalAllocatedMemoryLong() / 1048576.0).ToString("0"), (Profiler.GetMonoUsedSizeLong() / 1048576.0).ToString("0"),
                        CountAliveEnemies(), gm != null ? gm.State.ToString() : "-"));
                }
                yield return null;
            }
        }

        /// <summary>포화전 진행: 처음에 수상·수중·항공 적을 깔고, 3초마다 미사일 8발, 수상 적이 줄면 다시 채운다.</summary>
        private IEnumerator SaturationDriver(PerfWindow w)
        {
            CombatDevTools.SpawnRing("ene_fastboat", 8, 45f);
            CombatDevTools.SpawnRing("ene_missileboat", 3, 55f, 20f);
            CombatDevTools.SpawnSubmarines(2, 30f);
            CombatDevTools.SpawnRing("ene_drone", 4, 46f);
            CombatDevTools.SpawnRing("ene_fighter", 2, 50f);
            float nextVolley = 1f, nextRefill = 10f;
            while (true)
            {
                if (w.Elapsed >= nextVolley)
                {
                    nextVolley += 3f;
                    CombatDevTools.MissileVolley(8, 60f, w.Elapsed * 37f);
                }
                if (w.Elapsed >= nextRefill)
                {
                    nextRefill += 10f;
                    if (CountAliveEnemies() < 10)
                    {
                        CombatDevTools.SpawnRing("ene_fastboat", 6, 45f, w.Elapsed * 11f);
                        CombatDevTools.SpawnRing("ene_drone", 4, 46f, w.Elapsed * 7f);
                    }
                }
                yield return null;
            }
        }

        /// <summary>호위함 4척을 역할 넷으로 T3까지 올리고 자체 무장을 켠다. 배치한 수.</summary>
        private static int DeployMaxFleet(ShipController ship)
        {
            var fleet = ship != null ? ship.GetComponent<Game.TaskForce.TaskForceEscortFormation>() : null;
            if (fleet == null) return 0;
            fleet.ClearAll();
            var roles = new[]
            {
                Game.TaskForce.EscortRole.AirDefense, Game.TaskForce.EscortRole.AntiSubmarine,
                Game.TaskForce.EscortRole.ElectronicWarfare, Game.TaskForce.EscortRole.SurfaceStrike,
            };
            int deployed = 0;
            foreach (var role in roles)
            {
                int index = fleet.Deploy();
                if (index < 0) break;
                fleet.AssignRole(index, role);
                for (int t = 0; t < Game.TaskForce.TaskForceEscortFormation.MaxUpgradeLevel; t++) if (!fleet.Upgrade(index)) break;
                deployed++;
            }
            foreach (var e in UnityEngine.Object.FindObjectsByType<Game.TaskForce.EscortDefense>(FindObjectsSortMode.None)) e.enabled = true;
            return deployed;
        }
    }
}
#endif
