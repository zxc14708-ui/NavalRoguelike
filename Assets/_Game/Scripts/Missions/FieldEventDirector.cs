using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data;

namespace Game.Missions
{
    /// <summary>
    /// 전장 이벤트 진행(2026-11-17 — "한 자리에 머물지 않고 계속 움직일 동기"). 스테이지가 시작되면 StageDirector가 연다(현대 판만).
    ///   - 위치 노출 게이지(ExposureGauge)는 스테이지 내내 돈다.
    ///   - 이벤트는 한 번에 하나: 시작 FirstDelay초 뒤 첫 이벤트, 끝나면 GapMin~GapMax초 쉬고 다음.
    ///     후보: 반잠수정 포위(PincerAttack — 2026-11-21 자폭 보트 협공에서 바뀜, 되풀이 가능) · 자폭 보트 일렬 돌격(LineAssault, 2026-11-21, 되풀이 가능) ·
    ///     구조 활동(RescueMission) · 적 보급선 추격(RunnerMission) — 구조 · 추격은 스테이지마다 한 번씩.
    ///     이번 스테이지에 아직 안 나온 종류를 먼저 고른다. 보스 구간 · 보급선 호위 임무 중에는 구조 · 추격을 내지 않는다(협공만).
    ///   - 스테이지가 끝나면(보스 격침 · 판 종료) 진행 중인 이벤트를 정리한다.
    /// </summary>
    public sealed class FieldEventDirector : MonoBehaviour
    {
        public const float FirstDelay = 35f, GapMin = 45f, GapMax = 65f;

        public enum Kind { Pincer, Rescue, Runner, LineAssault }   // 뒤에만 덧붙임

        public static FieldEventDirector Instance { get; private set; }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private System.Func<bool> _bossPhase;
        private int _stage;
        private float _next;
        private bool _active;
        private readonly HashSet<Kind> _doneThisStage = new();
        private MonoBehaviour _current;

        public ExposureGauge Exposure { get; private set; }
        public MonoBehaviour Current => _current;
        public Kind? CurrentKind { get; private set; }
        public float NextIn => _next;
        public bool Active => _active;
        /// <summary>검증 · 기록용: 이번 판에 연 이벤트 수.</summary>
        public int Started { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; s_defs.Clear(); }

        private static FieldEventDirector Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Field event director");
            Instance = go.AddComponent<FieldEventDirector>();
            Instance.Exposure = go.AddComponent<ExposureGauge>();
            Instance.Exposure.enabled = false;
            return Instance;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>스테이지 시작(StageDirector.BeginStage). bossPhase: 지금 마지막(보스) 구간인가.</summary>
        public static void BeginStage(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, int stage, System.Func<bool> bossPhase)
        {
            if (player == null) return;
            var d = Ensure();
            d.CancelCurrent();
            d._player = player;
            d._xp = xp;
            d._stage = Mathf.Max(1, stage);
            d._bossPhase = bossPhase;
            d._doneThisStage.Clear();
            d._next = FirstDelay;
            d._active = true;
            d.Exposure.Begin(player, d._stage);
        }

        /// <summary>스테이지 끝(보스 격침 · 판 종료 · WWII 전투로 바뀜): 진행 중인 이벤트를 정리하고 멈춘다.</summary>
        public static void EndStage()
        {
            if (Instance == null) return;
            Instance._active = false;
            Instance.CancelCurrent();
            Instance.Exposure.Stop();
            Instance.Exposure.enabled = false;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (!_active || gm == null) return;
            if (gm.IsRunOver) { EndStage(); return; }
            if (gm.State != GameState.Playing) return;
            if (_current != null)
            {
                if (!Finished(_current)) return;
                _current = null;
                CurrentKind = null;
                _next = Random.Range(GapMin, GapMax);
            }
            _next -= Time.deltaTime;
            if (_next > 0f || DevNoAuto) return;
            if (!TryStartNext()) _next = 10f;   // 낼 수 있는 게 없으면 잠시 뒤 다시
        }

        private static bool Finished(MonoBehaviour m) => m == null || m switch
        {
            PincerAttack p => p.State == PincerAttack.Phase.Done,
            LineAssault l => l.State == LineAssault.Phase.Done,
            RescueMission r => r.State != RescueMission.Outcome.Running,
            RunnerMission u => u.State != RunnerMission.Outcome.Running,
            _ => true,
        };

        private bool TryStartNext()
        {
            bool quiet = !(_bossPhase?.Invoke() ?? false) && !SupplyMissionRunning;
            var fresh = new List<Kind>();
            var all = new List<Kind> { Kind.Pincer, Kind.LineAssault };
            if (quiet && !_doneThisStage.Contains(Kind.Rescue)) all.Add(Kind.Rescue);
            if (quiet && !_doneThisStage.Contains(Kind.Runner)) all.Add(Kind.Runner);
            foreach (var k in all) if (!_doneThisStage.Contains(k)) fresh.Add(k);
            var pool = fresh.Count > 0 ? fresh : all;
            // 고른 것이 못 나오면(자리 없음 등) 다른 후보를 차례로
            int start = Random.Range(0, pool.Count);
            for (int i = 0; i < pool.Count; i++)
                if (StartEvent(pool[(start + i) % pool.Count])) return true;
            return false;
        }

        private static bool SupplyMissionRunning
        {
            get
            {
                var m = Game.TaskForce.SupplyEscortMission.Current;
                return m != null && (m.State == Game.TaskForce.SupplyEscortMission.Outcome.Pending
                                     || m.State == Game.TaskForce.SupplyEscortMission.Outcome.Waiting
                                     || m.State == Game.TaskForce.SupplyEscortMission.Outcome.Running);
            }
        }

        /// <summary>이벤트 하나를 연다(검증도 부른다). 못 열면 false.</summary>
        public bool StartEvent(Kind kind)
        {
            if (_player == null) return false;
            CancelCurrent();
            MonoBehaviour started = kind switch
            {
                Kind.Pincer => PincerAttack.Begin(_player, _xp, FindEnemy("ene_semisub_suicide"), _stage),      // 스테이지가 오를수록 척수 ↑(강화판)
                Kind.LineAssault => LineAssault.Begin(_player, _xp, FindEnemy("ene_suicide_boat"), null, _stage),
                Kind.Rescue => RescueMission.Begin(_player, _xp),
                Kind.Runner => RunnerMission.Begin(_player, _xp, _stage, FindEnemy("ene_sea_mine"), FindEnemy("ene_fastboat")),
                _ => null,
            };
            if (started == null) return false;
            _current = started;
            CurrentKind = kind;
            _doneThisStage.Add(kind);
            Started++;
            return true;
        }

        /// <summary>검증용: 이벤트를 저절로 열지 않는다(StartEvent로만).</summary>
        [System.NonSerialized] public bool DevNoAuto;

        /// <summary>검증용: 다음 이벤트까지 남은 시간을 바꾼다.</summary>
        public void DevSetNext(float seconds) => _next = seconds;

        private void CancelCurrent()
        {
            switch (_current)
            {
                case PincerAttack p: p.Cancel(); break;
                case LineAssault l: l.Cancel(); break;
                case RescueMission r: r.Cancel(); break;
                case RunnerMission u: u.Cancel(); break;
            }
            _current = null;
            CurrentKind = null;
        }

        // ------------------------------------------------------------ 적 정의 찾기

        private static readonly Dictionary<string, EnemyDefinition> s_defs = new();

        /// <summary>
        /// 이미 불러온 적 정의에서 id로 찾는다(스테이지 RoundSet · 적 프리팹이 참조해 메모리에 있다). 지금 규칙 집합에서 못 쓰는 적이면 null.
        /// </summary>
        public static EnemyDefinition FindEnemy(string id)
        {
            if (!s_defs.TryGetValue(id, out var def) || def == null)
            {
                def = null;
                foreach (var d in Resources.FindObjectsOfTypeAll<EnemyDefinition>())
                    if (d != null && d.Id == id) { def = d; break; }
                s_defs[id] = def;
            }
            return def != null && ContentRegistry.Allows(def) ? def : null;
        }
    }
}
