using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.UI
{
    /// <summary>
    /// 항로 지도 화면(2026-10-31, RouteMap). 보스를 격침한 뒤 여는 항로 선택을 지도로 보여 준다:
    /// 출항 지점 → 스테이지마다 갈림길 → 최종 보스. 지나온 길은 밝게, 지금 고를 수 있는 갈림길만 누를 수 있고(1~3 키),
    /// 이어지지 않은 갈림길은 흐리게 보인다. 고른 갈림길의 종류(보급·위험·임무)가 기존 항로 선택과 같은 효과를 낸다.
    /// 지도가 없으면(WWII 시험 전투·자동 검증의 단독 호출) 기존 3칸 선택을 그대로 쓴다.
    /// </summary>
    public partial class HUDView
    {
        private readonly List<System.Action> _routeKeys = new();
        private int _routeLayer = -1;

        private static readonly Color SupplyColor = new(0.10f, 0.29f, 0.34f), DangerColor = new(0.34f, 0.17f, 0.15f), MissionColor = new(0.30f, 0.25f, 0.08f);

        private static string RouteName(RouteKind kind) => kind switch
        {
            RouteKind.Danger => "위험 항로",
            RouteKind.Mission => "임무 항로",
            _ => "보급 항로",
        };

        private static string RouteButtonName(RouteKind kind) => kind switch
        {
            RouteKind.Danger => "Danger route",
            RouteKind.Mission => "Mission route",
            _ => "Supply route",
        };

        /// <summary>지도가 있으면 항로 지도를 그리고 true(없으면 기존 3칸 선택).</summary>
        private bool ShowRouteMap(RectTransform overlay, int nextStage, float repairAmount)
        {
            var map = RouteMap.Current;
            int layer = nextStage - 1;
            if (map == null || layer < 1 || layer >= map.LayerCount) return false;
            _routeLayer = layer;
            _routeKeys.Clear();
            var reach = map.Reachable(layer);
            if (reach.Count == 0) return false;

            var content = Panel(overlay, "Route briefing", new Vector2(0.5f, 0.5f), new Vector2(-560f, -300f), new Vector2(1120f, 600f), Color.clear);
            var nextSea = Game.World.SeaRegion.For(Game.World.SeaRegion.ForStage(nextStage));
            Label(content, "Eyebrow", $"STAGE {nextStage}  /  항로 지도  ·  다음 해역: {nextSea.Name}", new Vector2(12, 556), new Vector2(1096, 36), 21, Cyan);
            Label(content, "Title", "이어진 항로 중 하나를 고르세요 — 선을 따라서만 나아갑니다", new Vector2(12, 506), new Vector2(1096, 48), 28, Color.white);
            var hint = Label(content, "Hint", $"<color=#e0c05a>{nextSea.Title}</color>  {nextSea.Tactics}  <color=#7f939e>({(reach.Count > 1 ? $"1~{reach.Count}" : "1")} 키로도 선택)</color>",
                new Vector2(12, 462), new Vector2(1096, 44), 15, new Color(0.67f, 0.78f, 0.83f));
            hint.textWrappingMode = TextWrappingModes.Normal;

            const float W = 1096f, H = 380f;
            var area = Panel(content, "Route map", Vector2.zero, new Vector2(12f, 72f), new Vector2(W, H), new Color(0.03f, 0.09f, 0.12f, 0.92f));
            int columns = map.LayerCount + 1;   // 마지막 열 = 최종 보스
            float colW = W / columns;
            Vector2 Pos(RouteMap.Node n) => new(colW * (n.Layer + 0.5f), H - 52f - n.Y * (H - 90f));
            var bossPos = new Vector2(colW * (map.LayerCount + 0.5f), (H - 52f) - 0.5f * (H - 90f));

            for (int k = 0; k <= map.LayerCount; k++)
            {
                string head = k == 0 ? "출항" : k == map.LayerCount ? "최종 보스" : $"스테이지 {k + 1} · {Game.World.SeaRegion.For(Game.World.SeaRegion.ForStage(k + 1)).Name}";
                var t = Label(area, $"Column {k}", head, new Vector2(colW * k, H - 28f), new Vector2(colW, 24f), 14,
                    k == layer ? Gold : new Color(0.6f, 0.72f, 0.76f));
                t.alignment = TextAlignmentOptions.Center;
            }

            bool Taken(RouteMap.Node n) => n.Layer < RouteMap.Path.Count && RouteMap.Path[n.Layer] == n.Index && n.Layer < layer;
            bool Open(RouteMap.Node n) => n.Layer == layer && reach.Contains(n);

            // 선(갈림길 뒤에)
            for (int k = 0; k < map.LayerCount; k++)
                foreach (var n in map.Layers[k])
                {
                    if (k + 1 < map.LayerCount)
                        foreach (int j in n.Next)
                        {
                            var m = map.Layers[k + 1][j];
                            bool walked = Taken(n) && (Taken(m) || Open(m));
                            Line(area, Pos(n), Pos(m), walked ? new Color(0.9f, 0.85f, 0.55f, 0.9f) : new Color(0.45f, 0.55f, 0.6f, 0.35f), walked ? 4f : 2f);
                        }
                    else Line(area, Pos(n), bossPos, new Color(0.45f, 0.55f, 0.6f, 0.35f), 2f);
                }

            // 갈림길
            var size = new Vector2(Mathf.Min(150f, colW - 24f), 62f);
            foreach (var layerNodes in map.Layers)
                foreach (var n in layerNodes)
                {
                    bool open = Open(n), taken = Taken(n);
                    int key = open ? reach.IndexOf(n) + 1 : 0;
                    Color c = n.Layer == 0 ? new Color(0.16f, 0.24f, 0.28f) : n.Kind == RouteKind.Danger ? DangerColor : n.Kind == RouteKind.Mission ? MissionColor : SupplyColor;
                    if (!open && !taken) c = Color.Lerp(c, new Color(0.03f, 0.09f, 0.12f), 0.6f);
                    var rect = Panel(area, open ? RouteButtonName(n.Kind) : $"Route node {n.Layer}-{n.Index}", Vector2.zero, Pos(n) - size * 0.5f, size, c);
                    string title = n.Layer == 0 ? "출항 지점" : (open ? $"[{key}] " : "") + RouteName(n.Kind);
                    string detail = n.Layer == 0 ? "" : n.Kind == RouteKind.Danger ? "적 +25% · XP +25%" : n.Kind == RouteKind.Mission ? "보급선 호위" : $"선체 {repairAmount:0} 수리";
                    var tl = Label(rect, "Title", title, new Vector2(6f, 30f), new Vector2(size.x - 12f, 28f), 18,
                        open || taken ? Color.white : new Color(0.55f, 0.62f, 0.66f));
                    tl.alignment = TextAlignmentOptions.Center;
                    var dl = Label(rect, "Effect", detail, new Vector2(6f, 6f), new Vector2(size.x - 12f, 24f), 14,
                        open ? Gold : new Color(0.55f, 0.62f, 0.66f));
                    dl.alignment = TextAlignmentOptions.Center;
                    if (taken) rect.gameObject.AddComponent<Outline>().effectColor = new Color(0.95f, 0.85f, 0.5f, 0.9f);
                    if (!open) continue;
                    var image = rect.GetComponent<Image>();
                    image.raycastTarget = true;
                    var button = rect.gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    var node = n;
                    button.onClick.AddListener(() => PickRouteNode(layer, node));
                    _routeKeys.Add(() => PickRouteNode(layer, node));
                }
            var boss = Panel(area, "Route boss", Vector2.zero, bossPos - size * 0.5f, size, new Color(0.38f, 0.08f, 0.08f));
            var bl = Label(boss, "Title", "최종 보스", new Vector2(6f, 18f), new Vector2(size.x - 12f, 28f), 18, Color.white);
            bl.alignment = TextAlignmentOptions.Center;

            var legend = Label(content, "Legend",
                $"보급 항로: 선체 {repairAmount:0} 수리 · 적 편성 기본   ·   위험 항로: 적 등장·동시 수 +25% · 격침 XP +25%   ·   " +
                $"임무 항로: 보급선 약 {Game.TaskForce.SupplyEscortMission.RouteLength:0}m 호위 — 성공: 완전 수리 · 다시 뽑기 +1 · 정비 1회",
                new Vector2(12f, 10f), new Vector2(1096f, 54f), 15, Gold);
            legend.textWrappingMode = TextWrappingModes.Normal;
            return true;
        }

        private void PickRouteNode(int layer, RouteMap.Node node)
        {
            RouteMap.Choose(layer, node.Index);
            if (node.Kind == RouteKind.Mission && _onMissionChosen != null) ChooseMission();
            else ChooseRoute(node.Kind == RouteKind.Danger);
        }

        /// <summary>지도에서 갈림길을 누르지 않고(기존 키·버튼 경로) 골랐으면 그 종류의 이어진 갈림길로 기록한다.</summary>
        private void RecordRouteKind(RouteKind kind)
        {
            if (_routeLayer >= 1 && RouteMap.Path.Count <= _routeLayer) RouteMap.ChooseKind(_routeLayer, kind);
            _routeLayer = -1;
            _routeKeys.Clear();
        }

        /// <summary>두 점을 잇는 선(얇은 사각형을 돌려서).</summary>
        private void Line(RectTransform parent, Vector2 a, Vector2 b, Color color, float width)
        {
            var d = b - a;
            var rect = Panel(parent, "Route line", Vector2.zero, a, new Vector2(d.magnitude, width), color);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = a;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }
}
