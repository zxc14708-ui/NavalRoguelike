using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>항로 지도의 갈림길 종류 — 기존 항로 선택(보급 · 위험 · 임무)과 같다.</summary>
    public enum RouteKind { Supply, Danger, Mission }

    /// <summary>
    /// 항로 지도(2026-10-31). 판을 시작할 때 런 시드로 스테이지 사이 갈림길을 미리 정해 두고, 보스를 격침할 때마다
    /// 지금 자리에서 이어진 갈림길 중 하나를 골라 다음 스테이지로 간다. 갈림길끼리 이어진 선만 따라갈 수 있어
    /// 다음 선택까지 내다보고 고른다(분기 지도 방식 — Docs/GITHUB_REFERENCES.md 6, 코드는 가져오지 않고 새로 작성).
    ///   - 열 k = 스테이지 k+1. 0열은 출항 지점 하나, 1열부터 갈림길 2~3개(첫 열은 출항 지점에서 모두 이어짐).
    ///   - 열마다 보급 항로가 하나 이상, 임무 항로는 하나 이하. 이웃 열 사이 선은 서로 엇갈리지 않고 모든 갈림길에 들어오는 선이 있다.
    ///   - 같은 시드면 같은 지도(이어하기는 시드와 고른 길로 되살린다).
    /// WWII 시험 전투·훈련·자동 검증의 단독 호출에서는 지도가 없고(Current = null) 기존 3칸 선택을 쓴다.
    /// </summary>
    public sealed class RouteMap
    {
        public sealed class Node
        {
            public int Layer, Index;
            public RouteKind Kind;
            /// <summary>열 안에서의 위치(0 위 ~ 1 아래).</summary>
            public float Y;
            /// <summary>다음 열에서 이어진 갈림길 번호.</summary>
            public readonly List<int> Next = new();
        }

        private readonly List<List<Node>> _layers = new();
        public IReadOnlyList<IReadOnlyList<Node>> Layers => _layers;
        public int LayerCount => _layers.Count;
        public int Seed { get; }

        /// <summary>지금 판의 지도(없으면 null).</summary>
        public static RouteMap Current { get; private set; }
        /// <summary>열마다 고른 갈림길 번호(0열 = 출항 지점 0).</summary>
        public static readonly List<int> Path = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Current = null; Path.Clear(); }

        private RouteMap(int seed) { Seed = seed; }

        /// <summary>새 판: 지도를 만들고 출항 지점에서 시작한다.</summary>
        public static void Begin(int runSeed, int stageCount)
        {
            Current = stageCount >= 2 ? Generate(MapSeed(runSeed), stageCount) : null;
            Path.Clear();
            if (Current != null) Path.Add(0);
        }

        /// <summary>이어하기: 같은 시드로 지도를 다시 만들고 저장된 길을 되살린다(예전 저장은 길이 비어 있다 — 출항 지점부터).</summary>
        public static void Restore(int runSeed, int stageCount, IList<int> path)
        {
            Begin(runSeed, stageCount);
            if (Current == null || path == null) return;
            for (int k = 1; k < path.Count && k < Current.LayerCount; k++)
            {
                int i = path[k];
                if (i < 0 || i >= Current._layers[k].Count || !Current.Reachable(k).Exists(n => n.Index == i)) break;
                Path.Add(i);
            }
        }

        public static void Clear() { Current = null; Path.Clear(); }

        private static int MapSeed(int runSeed) => unchecked(runSeed * 486187739 + 0x5EA);

        /// <summary>layer 열에서 지금 고를 수 있는 갈림길(바로 앞 열에서 고른 자리와 이어진 것). 앞 열을 아직 안 골랐으면 그 열 전부.</summary>
        public List<Node> Reachable(int layer)
        {
            var result = new List<Node>();
            if (layer <= 0 || layer >= _layers.Count) return result;
            if (Path.Count < layer) { result.AddRange(_layers[layer]); return result; }
            var from = _layers[layer - 1][Mathf.Clamp(Path[layer - 1], 0, _layers[layer - 1].Count - 1)];
            foreach (int i in from.Next) result.Add(_layers[layer][i]);
            result.Sort((a, b) => a.Y.CompareTo(b.Y));
            return result;
        }

        /// <summary>layer 열의 index 갈림길을 고른다(그 뒤 열의 선택은 지운다).</summary>
        public static void Choose(int layer, int index)
        {
            if (Current == null || layer <= 0 || layer >= Current.LayerCount) return;
            while (Path.Count > layer) Path.RemoveAt(Path.Count - 1);
            while (Path.Count < layer) Path.Add(0);
            Path.Add(index);
        }

        /// <summary>종류로 고른다(키·기존 버튼 경로): 고를 수 있는 것 중 그 종류의 첫 갈림길. 없으면 false.</summary>
        public static bool ChooseKind(int layer, RouteKind kind)
        {
            if (Current == null) return false;
            var node = Current.Reachable(layer).Find(n => n.Kind == kind);
            if (node == null) return false;
            Choose(layer, node.Index);
            return true;
        }

        // ------------------------------------------------------------ 생성

        public static RouteMap Generate(int seed, int stageCount)
        {
            var rng = new System.Random(seed);
            var map = new RouteMap(seed);
            map._layers.Add(new List<Node> { new() { Layer = 0, Index = 0, Kind = RouteKind.Supply, Y = 0.5f } });
            for (int k = 1; k < stageCount; k++)
            {
                int count = rng.NextDouble() < 0.35 ? 2 : 3;
                var kinds = new List<RouteKind> { RouteKind.Supply };
                bool mission = false;
                while (kinds.Count < count)
                {
                    double r = rng.NextDouble();
                    var kind = r < 0.38 ? RouteKind.Supply : r < 0.78 ? RouteKind.Danger : RouteKind.Mission;
                    if (kind == RouteKind.Mission && mission) kind = RouteKind.Danger;
                    mission |= kind == RouteKind.Mission;
                    kinds.Add(kind);
                }
                for (int i = kinds.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (kinds[i], kinds[j]) = (kinds[j], kinds[i]); }
                var layer = new List<Node>();
                for (int i = 0; i < count; i++)
                    layer.Add(new Node { Layer = k, Index = i, Kind = kinds[i], Y = (i + 0.5f) / count + ((float)rng.NextDouble() - 0.5f) * 0.12f });
                map._layers.Add(layer);
            }

            // 출항 지점은 첫 갈림길 모두와 이어진다
            for (int i = 0; i < map._layers[1].Count; i++) map._layers[0][0].Next.Add(i);
            for (int k = 1; k + 1 < map._layers.Count; k++) Link(map._layers[k], map._layers[k + 1], rng);
            return map;
        }

        /// <summary>
        /// 이웃 두 열을 잇는다(위에서 아래로 계단처럼 걷기): 두 열의 맨 위끼리 잇고, 매번 한쪽 또는 양쪽을 한 칸 내려가며 잇는다.
        /// 선이 엇갈리지 않고, 두 열의 모든 갈림길에 선이 하나 이상 생긴다.
        /// </summary>
        private static void Link(List<Node> a, List<Node> b, System.Random rng)
        {
            int i = 0, j = 0;
            a[0].Next.Add(0);
            while (i < a.Count - 1 || j < b.Count - 1)
            {
                double r = rng.NextDouble();
                if (i == a.Count - 1) j++;
                else if (j == b.Count - 1) i++;
                else if (r < 0.34) i++;
                else if (r < 0.67) j++;
                else { i++; j++; }
                if (!a[i].Next.Contains(j)) a[i].Next.Add(j);
            }

            // 다음 길이 하나뿐인 갈림길은 엇갈리지 않으면 이웃 갈림길로 한 줄 더(대부분 두 갈래 중 고르게)
            for (int n = 0; n < a.Count; n++)
            {
                if (a[n].Next.Count != 1 || rng.NextDouble() < 0.2) continue;
                int j0 = a[n].Next[0];
                int first = rng.NextDouble() < 0.5 ? -1 : 1;
                foreach (int step in new[] { first, -first })
                {
                    int t = j0 + step;
                    if (t < 0 || t >= b.Count || !CanAdd(a, n, t)) continue;
                    a[n].Next.Add(t);
                    break;
                }
            }
        }

        /// <summary>a[n]에서 t로 선을 더해도 엇갈리지 않는가(위 갈림길은 모두 t 이하, 아래는 모두 t 이상으로만 이어져야).</summary>
        private static bool CanAdd(List<Node> a, int n, int t)
        {
            for (int o = 0; o < a.Count; o++)
            {
                if (o == n) continue;
                foreach (int x in a[o].Next)
                    if (o < n ? x > t : x < t) return false;
            }
            return true;
        }

        /// <summary>검증용: 이웃 열 사이 선이 엇갈리는 곳이 있는가.</summary>
        public bool HasCrossing()
        {
            for (int k = 0; k + 1 < _layers.Count; k++)
                foreach (var p in _layers[k])
                    foreach (var q in _layers[k])
                        if (p.Y < q.Y)
                            foreach (int x in p.Next)
                                foreach (int y in q.Next)
                                    if (x > y) return true;
            return false;
        }
    }
}
