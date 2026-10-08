using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Core;
using Game.TaskForce;
using System.Collections.Generic;

namespace Game.Ship
{
    /// <summary>
    /// 게임 시작 시 StartingLoadout대로 초기 모듈을 설치한다.
    /// 시작 구성이 코드가 아니라 에셋에 있으므로 밸런스 실험이 쉽다.
    /// </summary>
    public class ShipInitializer : MonoBehaviour
    {
        [SerializeField] private StartingLoadout loadout;

        /// <summary>시작 모듈 배치(사전이 함교 등 카드에 없는 블록을 읽는다).</summary>
        public StartingLoadout Loadout => loadout;
        [SerializeField] private ModuleFactory factory;
        [SerializeField] private ShipSystems systems;
        public StartingShipConcept SelectedConcept { get; private set; }
        public bool SelectionLocked { get; private set; }
        private bool _initialized;

        private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;
        private void OnStateChanged(GameState state) { if (state == GameState.Playing) LockForLaunch(); }

        /// <summary>
        /// 씬에 스킬 컴포넌트가 빠져 있으면 붙인다. 예전에 세 스킬이 한 파일(SupportSkills.cs)에 있어
        /// 씬에 하나만 저장됐던 문제를 셋업을 다시 돌리지 않고도 바로잡는다.
        /// </summary>
        private void Awake()
        {
            var ship = GetComponent<ShipController>();
            var grid = ship != null ? ship.Grid : GetComponentInChildren<ShipGrid>();
            if (grid == null) return;

            EnsureSkill<RepairBurstSkill>(grid);
            EnsureSkill<SmokeSkill>(grid);
            EnsureSkill<FlankSkill>(grid);
        }

        private void EnsureSkill<T>(ShipGrid grid) where T : Component
        {
            if (GetComponent<T>() != null) return;
            var skill = gameObject.AddComponent<T>();
            switch (skill)
            {
                case RepairBurstSkill r: r.Bind(grid); break;
                case SmokeSkill s: s.Bind(grid); break;
                case FlankSkill f: f.Bind(grid); break;
            }
        }

        private void Start()
        {
            if (_initialized) return;
            if (loadout == null || factory == null)
            {
                Debug.LogError("[ShipInitializer] loadout 또는 factory 미할당.", this);
                return;
            }

            InstallLoadout();
        }

        private void InstallLoadout()
        {
            foreach (var e in loadout.Entries)
            {
                if (e.Module == null) continue;

                var inst = factory.Install(e.Module, e.Origin, e.RotationSteps);
                if (inst == null)
                    Debug.LogError($"[ShipInitializer] 시작 모듈 '{e.Module.DisplayName}' 배치 실패 at {e.Origin}. " +
                                   "StartingLoadout 좌표와 PlacementRule을 확인하세요.", this);
            }

            systems?.Recalculate();
            _initialized = true;
        }

        /// <summary>Only pre-launch changes are allowed. Validate the complete replacement before removing anything.</summary>
        public bool TrySelect(StartingShipConcept concept, out string reason)
        {
            reason = null;
            if (SelectionLocked) { reason = "출항한 뒤에는 시작 함선을 변경할 수 없습니다."; return false; }
            var ship = GetComponent<ShipController>();
            var grid = ship != null ? ship.Grid : GetComponentInChildren<ShipGrid>();
            if (concept == null || concept.StartLoadout == null || factory == null || grid == null)
            { reason = "시작 함선 또는 모듈 생성기 설정이 없습니다."; return false; }
            if (SelectedConcept == concept && _initialized) return true;
            if (!ValidateLoadout(concept.StartLoadout, grid, out reason)) return false;

            var previous = new List<ModuleInstance>(grid.Modules);
            foreach (var instance in previous)
            {
                // Destroy is deferred in play mode; stop old weapons/visuals before installing replacements.
                if (instance.Runtime != null) instance.Runtime.gameObject.SetActive(false);
                factory.Uninstall(instance);
            }
            loadout = concept.StartLoadout;
            SelectedConcept = concept;
            InstallLoadout();
            return true;
        }

        private static bool ValidateLoadout(StartingLoadout candidate, ShipGrid target, out string reason)
        {
            reason = null;
            var probeGo = new GameObject("Starting loadout validation") { hideFlags = HideFlags.HideAndDontSave };
            var probe = probeGo.AddComponent<ShipGrid>();
            // ShipGrid has only serialized extent/spacing settings; readonly occupancy is never copied.
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(target), probe);
            try
            {
                if (candidate.Entries.Count == 0) { reason = "시작 배치가 비어 있습니다."; return false; }
                foreach (var entry in candidate.Entries)
                {
                    var def = entry.Module;
                    if (def == null || def.Prefab == null || def.Prefab.GetComponent<ModuleRuntime>() == null)
                    { reason = "시작 모듈의 데이터/전투 프리팹이 없습니다."; return false; }
                    if (!def.CanRotate && entry.RotationSteps != 0)
                    { reason = $"{def.DisplayName}: 회전할 수 없는 블록입니다."; return false; }
                    if (!probe.CanPlace(def, entry.Origin, entry.RotationSteps, out reason) ||
                        !probe.Place(new ModuleInstance(def), entry.Origin, entry.RotationSteps)) return false;
                }
                foreach (var module in probe.Modules)
                    if (!PlacementRuleEvaluator.Evaluate(module.Definition.Placement, probe, module.OccupiedCoords, out reason)) return false;
                return true;
            }
            finally { DestroyImmediate(probeGo); }
        }

        /// <summary>Called once at launch; command starter receives two role-free patrol escorts.</summary>
        public void LockForLaunch()
        {
            if (SelectionLocked) return;
            SelectionLocked = true;
            int count = SelectedConcept != null ? SelectedConcept.InitialEscortCount : 0;
            if (count <= 0) return;
            var formation = GetComponent<TaskForceEscortFormation>();
            if (formation == null) formation = gameObject.AddComponent<TaskForceEscortFormation>();
            for (int slot = 0; slot < Mathf.Min(count, TaskForceEscortFormation.MaxEscorts); slot++)
                if (formation.IsRosterSlotEmpty(slot)) formation.Deploy(slot);
        }
    }
}
