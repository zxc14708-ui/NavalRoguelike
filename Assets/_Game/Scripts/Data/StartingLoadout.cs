using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Modules;
using Game.Ship;

namespace Game.Data
{
    /// <summary>
    /// 시작 함선에 미리 설치된 모듈 구성.
    /// 함교(갑판 중앙), 기관포(선수), 엔진(함내 선미)을 데이터로 정의한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Naval/Starting Loadout", fileName = "StartingLoadout")]
    public class StartingLoadout : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ModuleDefinition Module;
            public GridCoord Origin;
            [Range(0, 3)] public int RotationSteps;
        }

        [SerializeField] private List<Entry> entries = new();

        public IReadOnlyList<Entry> Entries => entries;

        /// <summary>원본을 바꾸지 않고 항목을 바꾼 실행 중 사본(정비창 개조안 — 에셋으로 저장되지 않는다).</summary>
        public static StartingLoadout CreateRuntime(string name, IEnumerable<Entry> entries)
        {
            var loadout = CreateInstance<StartingLoadout>();
            loadout.name = name;
            loadout.hideFlags = HideFlags.DontSave;
            loadout.entries = new List<Entry>(entries);
            return loadout;
        }
    }
}
