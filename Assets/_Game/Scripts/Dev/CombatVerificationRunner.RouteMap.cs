using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.UI;

namespace Game.Dev
{
    public partial class CombatVerificationRunner
    {
        /// <summary>
        /// 항로 지도(2026-10-31, RouteMap): 생성 규칙(시드 300개 × 스테이지 3·5), 같은 시드 = 같은 지도, 이어하기 복원,
        /// 실제 화면에서 이어진 갈림길만 누를 수 있는지와 고른 길이 다음 선택을 좁히는지.
        /// </summary>
        private IEnumerator RouteMapCheck()
        {
            _report.AppendLine("\n## 항로 지도");

            // 1) 생성 규칙
            int maps = 0, bad = 0, missions = 0, dangers = 0, forced = 0, layers = 0;
            string firstBad = null;
            foreach (int stages in new[] { 3, 5 })
                for (int seed = 1; seed <= 300; seed++)
                {
                    var m = RouteMap.Generate(seed, stages);
                    var again = RouteMap.Generate(seed, stages);
                    maps++;
                    string why = null;
                    if (m.LayerCount != stages) why = "열 수";
                    else if (m.HasCrossing()) why = "선 엇갈림";
                    for (int k = 1; k < m.LayerCount && why == null; k++)
                    {
                        var layer = m.Layers[k];
                        layers++;
                        int supply = 0, mission = 0;
                        for (int i = 0; i < layer.Count; i++)
                        {
                            var n = layer[i];
                            if (n.Kind == RouteKind.Supply) supply++;
                            if (n.Kind == RouteKind.Mission) { mission++; missions++; }
                            if (n.Kind == RouteKind.Danger) dangers++;
                            bool incoming = false;
                            foreach (var p in m.Layers[k - 1]) incoming |= p.Next.Contains(i);
                            if (!incoming) why = $"{k}열 {i}번에 들어오는 선 없음";
                            if (k + 1 < m.LayerCount && n.Next.Count == 0) why = $"{k}열 {i}번에서 나가는 선 없음";
                            if (k + 1 < m.LayerCount && n.Next.Count == 1) forced++;
                            if (n.Kind != again.Layers[k][i].Kind || n.Next.Count != again.Layers[k][i].Next.Count) why = "같은 시드인데 지도가 다름";
                        }
                        if (layer.Count < 2 || layer.Count > 3) why = $"{k}열 갈림길 {layer.Count}개";
                        if (supply == 0) why = $"{k}열에 보급 항로 없음";
                        if (mission > 1) why = $"{k}열에 임무 항로 {mission}개";
                    }
                    if (why != null) { bad++; firstBad ??= $"시드 {seed}·스테이지 {stages}: {why}"; }
                }
            _report.AppendLine($"- 생성 규칙: 지도 {maps}개 중 어긋남 {bad}{(firstBad != null ? $"(첫 번째 {firstBad})" : "")} · 갈림길 열 {layers}개에 위험 {dangers}·임무 {missions} · 다음 길이 하나뿐인 갈림길 {forced}");
            if (bad > 0) Fail($"항로 지도 생성 규칙 어긋남: {firstBad}");

            // 2) 이어하기 복원
            RouteMap.Begin(777, 3);
            var first = RouteMap.Current.Reachable(1);
            RouteMap.Choose(1, first[first.Count - 1].Index);
            var saved = new List<int>(RouteMap.Path);
            RouteMap.Restore(777, 3, saved);
            bool restored = RouteMap.Path.Count == saved.Count && RouteMap.Path[1] == saved[1];
            RouteMap.Restore(777, 3, new List<int> { 0, 9 });
            bool rejected = RouteMap.Path.Count == 1;
            _report.AppendLine($"- 이어하기: 고른 길 되살림 {Yes(restored)} · 지도에 없는 길은 버림 {Yes(rejected)}");
            if (!restored || !rejected) Fail("항로 지도 이어하기 복원이 맞지 않음");

            // 3) 화면: 스테이지 2 앞(첫 갈림길은 모두 열림) → 하나 고르기 → 스테이지 3 앞은 이어진 것만
            var hud = Object.FindFirstObjectByType<HUDView>();
            if (hud == null) { Fail("HUD 없음"); yield break; }
            var gm = GameManager.Instance;
            gm.SetState(GameState.Paused);
            int stageCount = 3;
            RouteMap.Begin(2468, stageCount);
            var map = RouteMap.Current;
            string picked = null;
            hud.ShowRouteChoice(2, 30f, danger => picked = danger ? "위험" : "보급", () => picked = "임무");
            yield return null;
            var overlay = Get<GameObject>(hud, "_routeChoice");
            int buttons = overlay != null ? overlay.GetComponentsInChildren<UnityEngine.UI.Button>().Length : 0;
            int open1 = map.Reachable(1).Count;
            yield return ScreenShot("route_map_stage2");
            // 다음 열에서 고를 수 있는 길이 가장 적은 갈림길을 고른다(좁혀지는지 보이게)
            RouteMap.Node choose = null;
            foreach (var n in map.Layers[1]) if (choose == null || n.Next.Count < choose.Next.Count) choose = n;
            Call(hud, "PickRouteNode", 1, choose);
            yield return null;
            string expected = choose.Kind == RouteKind.Danger ? "위험" : choose.Kind == RouteKind.Mission ? "임무" : "보급";
            bool pathOk = RouteMap.Path.Count == 2 && RouteMap.Path[1] == choose.Index;
            _report.AppendLine($"- 스테이지 2 앞: 갈림길 버튼 {buttons}/{open1}(첫 열은 모두) · {choose.Index}번 {expected} 항로 고름 → 결과 {picked ?? "없음"} · 길 기록 {Yes(pathOk)}");
            if (buttons != open1) Fail("첫 갈림길 버튼 수가 맞지 않음");
            if (picked != expected || !pathOk) Fail("고른 갈림길의 항로 종류·길 기록이 맞지 않음");

            picked = null;
            hud.ShowRouteChoice(3, 30f, danger => picked = danger ? "위험" : "보급", () => picked = "임무");
            yield return null;
            overlay = Get<GameObject>(hud, "_routeChoice");
            buttons = overlay != null ? overlay.GetComponentsInChildren<UnityEngine.UI.Button>().Length : 0;
            int open2 = map.Reachable(2).Count, all2 = map.Layers[2].Count;
            yield return ScreenShot("route_map_stage3");
            bool narrowed = buttons == open2 && open2 == choose.Next.Count;
            // 키·기존 경로(종류로 고름)도 길에 남는다
            var keyKind = map.Reachable(2)[0].Kind;
            if (keyKind == RouteKind.Mission) Call(hud, "ChooseMission"); else Call(hud, "ChooseRoute", keyKind == RouteKind.Danger);
            yield return null;
            bool keyed = RouteMap.Path.Count == 3 && map.Layers[2][RouteMap.Path[2]].Kind == keyKind;
            _report.AppendLine($"- 스테이지 3 앞: 열 갈림길 {all2}개 중 이어진 {open2}개만 버튼 {buttons} {Yes(narrowed)} · 종류로 고른 길도 기록 {Yes(keyed)}");
            if (!narrowed) Fail("앞에서 고른 길과 이어진 갈림길만 열려야 함");
            if (!keyed) Fail("키·종류로 고른 항로가 지도 길에 기록되지 않음");

            RouteMap.Clear();
            gm.SetState(GameState.Playing);
        }
    }
}
