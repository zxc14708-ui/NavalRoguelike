using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 좁은 수로의 양안 기슭(2026-10-11 점검 K04). 물길 축을 따라 함선 앞뒤 ±6구간(40m씩, 약 ±260m)을 채우고,
    /// ±8구간 밖은 치운다. 구간 모양은 (판 시드, 구간 번호, 쪽)으로 정해져 되돌아가도 같은 기슭이다.
    /// </summary>
    public partial class IslandField
    {
        private const int BankKeep = 6, BankDrop = 8;
        private readonly Dictionary<int, IslandInfo[]> _banks = new();
        private readonly List<int> _bankDrop = new();

        /// <summary>검증용: 지금 놓인 기슭 구간 수(한 구간 = 양쪽 두 기슭).</summary>
        public int BankSegments => _banks.Count;
        /// <summary>검증·해안포대용: 지금 놓인 기슭들.</summary>
        public IEnumerable<(int index, IslandInfo left, IslandInfo right)> Banks
        {
            get { foreach (var kv in _banks) yield return (kv.Key, kv.Value[0], kv.Value[1]); }
        }

        private void RefreshBanks(Vector3 focus)
        {
            if (!SeaRegion.IsStrait) { if (_banks.Count > 0) ClearBanks(); return; }
            int c = Mathf.FloorToInt(SeaRegion.Along(focus) / IslandBuilder.BankSegmentLength);
            bool added = false;
            for (int i = c - BankKeep; i <= c + BankKeep; i++)
            {
                if (_banks.ContainsKey(i)) continue;
                _banks[i] = new[] { IslandBuilder.BuildBank(i, -1, _seed, transform), IslandBuilder.BuildBank(i, 1, _seed, transform) };
                added = true;
            }
            _bankDrop.Clear();
            foreach (var key in _banks.Keys) if (Mathf.Abs(key - c) > BankDrop) _bankDrop.Add(key);
            foreach (var key in _bankDrop)
            {
                foreach (var info in _banks[key]) Remove(info);
                _banks.Remove(key);
            }
            if (added) Physics.SyncTransforms();
        }

        private void ClearBanks()
        {
            foreach (var pair in _banks.Values) foreach (var info in pair) Remove(info);
            _banks.Clear();
        }
    }
}
