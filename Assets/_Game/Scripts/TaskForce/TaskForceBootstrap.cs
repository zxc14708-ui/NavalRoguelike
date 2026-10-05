using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Ship;

namespace Game.TaskForce
{
    /// <summary>기존 씬을 재생성하지 않아도 편대(호위함)·편대 연출·편대 현황 패널·진형 선택판을 플레이어 기함에 붙인다.</summary>
    public static class TaskForceBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        private static void OnSceneLoaded(Scene _, LoadSceneMode __) => Ensure();

        private static void Ensure()
        {
            var ship = Object.FindFirstObjectByType<ShipController>();
            if (ship == null) return;
            var formation = ship.GetComponent<TaskForceEscortFormation>();
            if (formation == null) formation = ship.gameObject.AddComponent<TaskForceEscortFormation>();
            if (ship.GetComponent<TaskForceWorldFeedback>() == null) ship.gameObject.AddComponent<TaskForceWorldFeedback>();
            Game.UI.TaskForcePanelUI.Ensure(formation);
            Game.UI.FormationSelectorUI.Ensure(formation);
        }
    }
}
