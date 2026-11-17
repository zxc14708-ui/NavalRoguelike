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
            // 군항이 열려 있으면 만·군항 뒤 언덕 구간은 멀어도 둔다(멀리서 보면 군항 부지 옆이 트인 바다로 보이지 않게)
            if (SeaRegion.HarborActive)
            {
                float a0 = SeaRegion.HarborCenterAlong + SeaRegion.HarborDir * SeaRegion.HarborEntranceT;
                float a1 = SeaRegion.HarborCenterAlong + SeaRegion.HarborDir * (IslandBuilder.HarborBackEnd + 40f);
                int h0 = Mathf.FloorToInt(Mathf.Min(a0, a1) / IslandBuilder.BankSegmentLength), h1 = Mathf.FloorToInt(Mathf.Max(a0, a1) / IslandBuilder.BankSegmentLength);
                for (int i = h0; i <= h1; i++)
                {
                    if (_banks.ContainsKey(i)) continue;
                    _banks[i] = new[] { IslandBuilder.BuildBank(i, -1, _seed, transform), IslandBuilder.BuildBank(i, 1, _seed, transform) };
                    added = true;
                }
            }
            _bankDrop.Clear();
            foreach (var key in _banks.Keys) if (Mathf.Abs(key - c) > BankDrop && !KeepForHarbor(key)) _bankDrop.Add(key);
            foreach (var key in _bankDrop)
            {
                foreach (var info in _banks[key]) Remove(info);
                _banks.Remove(key);
            }
            if (added) Physics.SyncTransforms();
        }

        /// <summary>군항이 열려 있으면 이 t(만 입구 조금 앞)부터 안쪽에는 바위섬을 두지 않는다.</summary>
        public static float HarborClearFromT => SeaRegion.HarborEntranceT - 20f;

        private static bool KeepForHarbor(int key)
        {
            if (!SeaRegion.HarborActive) return false;
            float t0 = SeaRegion.HarborT(key * IslandBuilder.BankSegmentLength), t1 = SeaRegion.HarborT((key + 1) * IslandBuilder.BankSegmentLength);
            return Mathf.Max(t0, t1) > SeaRegion.HarborEntranceT - IslandBuilder.BankSegmentLength && Mathf.Min(t0, t1) < IslandBuilder.HarborBackEnd + 40f;
        }

        private void ClearBanks()
        {
            foreach (var pair in _banks.Values) foreach (var info in pair) Remove(info);
            _banks.Clear();
            ClearHarbor();
        }

        // ------------------------------------------------------------ 수로 출구 군항(2026-11-15)

        private IslandInfo _harbor;
        /// <summary>검증용: 지금 놓인 군항(없으면 null).</summary>
        public IslandInfo Harbor => _harbor;

        /// <summary>
        /// 군항이 열렸다/닫혔다: 만·부지에 걸친 기슭 구간을 새 모양으로 다시 만들고, 안벽 둘레 바위섬을 치우고, 군항 외형을 세운다.
        /// 멀리 있는 구간은 모양이 그대로라(SeaRegion.HarborShore가 정확히 원래 폭) 건드리지 않는다.
        /// </summary>
        private void OnHarborChanged()
        {
            if (Disabled) return;
            ClearHarbor();
            if (!SeaRegion.IsStrait) return;
            float lo = SeaRegion.HarborEntranceT - 40f, hi = IslandBuilder.HarborBackEnd + 60f;
            _bankDrop.Clear();
            foreach (var key in _banks.Keys)
            {
                float t0 = SeaRegion.HarborActive ? SeaRegion.HarborT(key * IslandBuilder.BankSegmentLength) : 0f;
                float t1 = SeaRegion.HarborActive ? SeaRegion.HarborT((key + 1) * IslandBuilder.BankSegmentLength) : 0f;
                // 닫힐 때는 지난번 군항 자리를 알 수 없으므로 모두 다시 만든다(스테이지 전환 때뿐)
                if (!SeaRegion.HarborActive || (Mathf.Max(t0, t1) > lo && Mathf.Min(t0, t1) < hi)) _bankDrop.Add(key);
            }
            foreach (var key in _bankDrop)
            {
                foreach (var info in _banks[key]) Remove(info);
                _banks.Remove(key);
            }
            if (SeaRegion.HarborActive)
            {
                // 만 안쪽 바위섬은 치운다 — 보스전 무대는 트인 만(바위에 걸려 보스가 멈추지 않게)
                foreach (var list in _chunks.Values)
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        var info = list[i];
                        if (info == null || info.Bank) continue;
                        if (SeaRegion.HarborT(info.Center) > HarborClearFromT - info.Radius) { Remove(info); list.RemoveAt(i); }
                    }
                _harbor = HarborBuilder.Build(transform, _seed);
            }
            RefreshBanks(Focus(out _));
            Physics.SyncTransforms();
        }

        private void ClearHarbor()
        {
            if (_harbor == null) return;
            Remove(_harbor);
            _harbor = null;
        }
    }
}
