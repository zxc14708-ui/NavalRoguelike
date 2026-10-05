using UnityEngine;
using Game.Data;
using Game.Enemies;
using Game.Progression;
using Game.Refit;

namespace Game.Core
{
    /// <summary>
    /// 스테이지 진행. 스테이지마다 RoundSet 하나이고, 그 구간들을 쉬지 않고 이어서 흘려보낸다.
    ///
    /// 구간이 바뀌면 적 구성만 바뀌고 전투는 끊기지 않는다. 성장은 시간이 아니라
    /// ExperienceSystem의 레벨업으로 들어온다. 마지막 구간은 시간이 지나도 끝나지 않고,
    /// 보스를 격침해야 스테이지가 끝난다(BossShip → GameEvents.BossDefeated).
    /// 다음 스테이지가 있으면 남은 적을 치우고 선체를 조금 고친 뒤 이어서 시작하고, 없으면 승리.
    /// 함선·모듈·레벨은 스테이지를 넘어 그대로 이어진다.
    /// </summary>
    public class StageDirector : MonoBehaviour
    {
        [Tooltip("스테이지 순서. 1번이 첫 스테이지")]
        [SerializeField] private RoundSet[] stages;
        [SerializeField] private EnemySpawner spawner;
        [SerializeField] private ExperienceSystem experience;
        [SerializeField] private RefitController refit;
        [SerializeField] private Transform player;

        [Tooltip("스테이지를 클리어하면 선체를 이만큼 고친다")]
        [SerializeField] private float hullRepairOnStageClear = 40f;

        private int _stageIndex;
        private int _index = -1;
        private float _elapsed;
        private float _phaseEndsAt;
        private float _totalDuration;
        private bool _dangerRoute;
        private bool _stageTransitioning;

        public int StageNumber => _stageIndex + 1;
        /// <summary>스테이지별 웨이브 구성(사전이 적 목록·등장 스테이지를 읽는다).</summary>
        public System.Collections.Generic.IReadOnlyList<RoundSet> Stages => stages;
        private RoundSet Phases => stages != null && _stageIndex < stages.Length ? stages[_stageIndex] : null;

        private void Awake()
        {
            if (stages == null || stages.Length == 0 || stages[0] == null) Debug.LogError("[StageDirector] 스테이지 RoundSet 미할당.", this);
            AppendLinkedStages();
            if (spawner == null) Debug.LogError("[StageDirector] EnemySpawner 미할당.", this);
            if (experience == null) Debug.LogError("[StageDirector] ExperienceSystem 미할당.", this);
            if (refit == null) Debug.LogError("[StageDirector] RefitController 미할당.", this);
        }

        private void OnEnable()
        {
            if (experience != null) experience.LevelUpRequested += OnLevelUpRequested;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        private void OnDisable()
        {
            if (experience != null) experience.LevelUpRequested -= OnLevelUpRequested;
            GameEvents.BossDefeated -= OnBossDefeated;
        }

        /// <summary>
        /// 씬에 적힌 스테이지 목록 뒤에, 마지막 스테이지의 RoundSet.NextStage를 따라 이어 붙인다(2026-10-05 스테이지 3 —
        /// 씬을 고치지 않고 스테이지를 늘린다). 같은 RoundSet이 다시 나오면 멈춘다.
        /// </summary>
        private void AppendLinkedStages()
        {
            if (stages == null || stages.Length == 0) return;
            var list = new System.Collections.Generic.List<RoundSet>(stages);
            var next = list[list.Count - 1] != null ? list[list.Count - 1].NextStage : null;
            while (next != null && !list.Contains(next) && list.Count < 12)
            {
                list.Add(next);
                next = next.NextStage;
            }
            if (list.Count != stages.Length) stages = list.ToArray();
        }

        private void Start()
        {
            if (Phases == null || Phases.Count == 0) return;
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (hud != null)
            {
                GameManager.Instance.SetState(GameState.Paused);
                hud.ShowStartScreen(() =>
                {
                    GameManager.Instance.SetState(GameState.Playing);
                    BeginStage(0);
                });
            }
            else
            {
                GameManager.Instance.SetState(GameState.Playing);
                BeginStage(0);
            }
        }

        private void BeginStage(int stageIndex)
        {
            _stageTransitioning = false;
            _stageIndex = stageIndex;
            _elapsed = 0f;
            _totalDuration = 0f;
            foreach (var p in Phases.Rounds) _totalDuration += Mathf.Max(5f, p.Duration);
            EnterPhase(0);
        }

        private void Update()
        {
            var phases = Phases;
            if (phases == null || GameManager.Instance == null) return;
            if (GameManager.Instance.State != GameState.Playing) return;

            _elapsed += Time.deltaTime;
            GameEvents.RaiseStageProgressChanged(Mathf.Min(_elapsed, _totalDuration), _totalDuration);

            bool isLast = _index >= phases.Count - 1;
            if (!isLast && _elapsed >= _phaseEndsAt) EnterPhase(_index + 1);
        }

        private void EnterPhase(int index)
        {
            var phases = Phases;
            _index = index;
            var phase = phases.Get(index);
            _phaseEndsAt = _elapsed + Mathf.Max(5f, phase.Duration);

            if (_dangerRoute)
            {
                phase.SpawnRate *= 1.25f;
                phase.MaxAlive = Mathf.Max(phase.MaxAlive, Mathf.CeilToInt(phase.MaxAlive * 1.25f));
            }
            spawner?.SetPhase(phase, player);

            string title = string.IsNullOrEmpty(phase.Title) ? $"구간 {index + 1}" : phase.Title;
            GameEvents.RaisePhaseStarted(StageNumber, index + 1, phases.Count, title);
        }

        /// <summary>보스 격침: 다음 스테이지가 있으면 이어서, 없으면 승리.</summary>
        private void OnBossDefeated()
        {
            if (_stageTransitioning) return;
            int next = _stageIndex + 1;
            bool hasNext = stages != null && next < stages.Length && stages[next] != null && stages[next].Count > 0;

            if (!hasNext)
            {
                GameManager.Instance?.TriggerVictory();
                return;
            }

            _stageTransitioning = true;
            spawner?.ClearAll();

            // 보스 격침 보상: 에픽 성장 카드가 한 장 들어간 카드 선택 → 배치 → 항로 선택
            if (refit != null)
            {
                refit.BeginBossReward(experience != null ? experience.Level : 1, () => ContinueAfterBossReward(next));
                return;
            }
            ContinueAfterBossReward(next);
        }

        private void ContinueAfterBossReward(int next)
        {
            GameManager.Instance?.SetState(GameState.Paused);
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (hud != null) hud.ShowRouteChoice(next + 1, hullRepairOnStageClear, danger => CompleteStageTransition(next, danger));
            else CompleteStageTransition(next, false);
        }

        private void CompleteStageTransition(int next, bool danger)
        {
            if (!_stageTransitioning) return;
            _dangerRoute = danger;
            if (experience != null) experience.RewardMultiplier = danger ? 1.25f : 1f;
            if (!danger && player != null && player.TryGetComponent<Game.Ship.ShipController>(out var ship))
                ship.RepairHull(hullRepairOnStageClear);
            var hud = FindFirstObjectByType<Game.UI.HUDView>();
            if (CombatPolicies.Doctrine == NavalDoctrine.None && hud != null)
            {
                hud.ShowDoctrineChoice(doctrine => FinishStageTransition(next, doctrine));
                return;
            }
            FinishStageTransition(next, NavalDoctrine.None);
        }

        private void FinishStageTransition(int next, NavalDoctrine doctrine)
        {
            if (!_stageTransitioning) return;
            CombatPolicies.ChooseDoctrine(doctrine);
            BeginStage(next);
            GameManager.Instance?.SetState(GameState.Playing);
        }

        /// <summary>레벨업: 시간을 멈추고 카드 선택·배치 화면을 연다. 닫히면 전투로 돌아간다.</summary>
        private void OnLevelUpRequested(int level, System.Action onClosed)
        {
            if (refit == null) { onClosed?.Invoke(); return; }

            refit.BeginLevelUp(level, () =>
            {
                if (GameManager.Instance.State == GameState.Refit)
                    GameManager.Instance.SetState(GameState.Playing);
                onClosed?.Invoke();
            });
        }
    }
}
