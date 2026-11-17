using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Missions
{
    /// <summary>
    /// 전장 이벤트(2026-11-17 — 자폭 보트 협공 · 구조 활동 · 적 보급선 추격 · 위치 노출)가 같이 쓰는 표시 도구:
    /// 수면에 눕힌 선 · 원, 멀리서 보이는 기둥, 화면 위쪽 가운데 막대. 보급선 호위 임무(SupplyEscortMission)와 같은 모양.
    /// </summary>
    public static class MissionMarks
    {
        public const float WaterLine = 0.15f;   // 수면 선 높이(항로 선과 같게)

        private static Material s_line;

        public static Material LineMaterial()
        {
            if (s_line != null) return s_line;
            var shader = Shader.Find("Sprites/Default");
            s_line = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Field event line (Runtime)" };
            return s_line;
        }

        public static LineRenderer Line(Transform parent, string name, Color color, float width, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial();
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.loop = loop;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 수면에 눕힌다
            line.numCapVertices = 2;
            line.positionCount = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public static void Ring(LineRenderer line, Vector3 center, float radius, int segments = 48)
        {
            if (line == null) return;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * radius, WaterLine, center.z + Mathf.Cos(a) * radius));
            }
        }

        public static void Segment(LineRenderer line, Vector3 a, Vector3 b)
        {
            if (line == null) return;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(a.x, WaterLine, a.z));
            line.SetPosition(1, new Vector3(b.x, WaterLine, b.z));
        }

        public static void Tint(LineRenderer line, Color color)
        {
            if (line != null) line.startColor = line.endColor = color;
        }

        /// <summary>멀리서도 보이는 반투명 기둥(콜라이더 없음).</summary>
        public static Transform Beacon(Transform parent, string name, Vector3 at, Color color, float height, float width = 1.4f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = new Vector3(at.x, height * 0.5f - 0.5f, at.z);
            go.transform.localScale = new Vector3(width, height * 0.5f, width);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = LineMaterial();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            return go.transform;
        }

        /// <summary>조명받는 단색 상자 · 원기둥(구명정 · 표류자 같은 작은 소품).</summary>
        public static Transform Prop(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = LitMaterial();
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
            return go.transform;
        }

        private static Material s_lit;

        private static Material LitMaterial()
        {
            if (s_lit != null) return s_lit;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            s_lit = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Field event prop (Runtime)" };
            if (s_lit.HasProperty("_Smoothness")) s_lit.SetFloat("_Smoothness", 0.3f);
            return s_lit;
        }

        /// <summary>화면 위쪽 가운데 막대(글 한 줄 + 아래 진행 막대). HUD가 없으면 null.</summary>
        public sealed class Panel
        {
            public RectTransform Root;
            public TMP_Text Label;
            public RectTransform Fill;
            private Image _fillImage;
            private float _width;

            /// <param name="y">위쪽 가운데 기준(음수 = 아래로). fromBottom이면 아래쪽 가운데 기준(양수 = 위로).</param>
            public static Panel Create(string name, float y, float width, Color textColor, bool bar = true, bool fromBottom = false)
            {
                var hud = Object.FindFirstObjectByType<Game.UI.HUDView>();
                if (hud == null) return null;
                var p = new Panel { _width = width - 28f };
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(hud.transform, false);
                p.Root = (RectTransform)go.transform;
                p.Root.anchorMin = p.Root.anchorMax = p.Root.pivot = new Vector2(0.5f, fromBottom ? 0f : 1f);
                p.Root.anchoredPosition = new Vector2(0f, y);
                p.Root.sizeDelta = new Vector2(width, bar ? 52f : 34f);
                var bg = go.GetComponent<Image>();
                bg.color = new Color(0.02f, 0.07f, 0.09f, 0.82f);
                bg.raycastTarget = false;

                var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(p.Root, false);
                var text = label.GetComponent<TextMeshProUGUI>();
                text.font = hud.Font;
                text.fontSize = bar ? 18 : 17;
                text.color = textColor;
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                var lr = text.rectTransform;
                lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 1f);
                lr.anchoredPosition = new Vector2(0f, -4f);
                lr.sizeDelta = new Vector2(width - 20f, 28f);
                p.Label = text;
                if (!bar) return p;

                var back = new GameObject("Bar back", typeof(RectTransform), typeof(Image));
                back.transform.SetParent(p.Root, false);
                var br = (RectTransform)back.transform;
                br.anchorMin = br.anchorMax = br.pivot = Vector2.zero;
                br.anchoredPosition = new Vector2(14f, 9f);
                br.sizeDelta = new Vector2(p._width, 9f);
                back.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
                back.GetComponent<Image>().raycastTarget = false;
                var fill = new GameObject("Bar fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(br, false);
                p.Fill = (RectTransform)fill.transform;
                p.Fill.anchorMin = p.Fill.anchorMax = p.Fill.pivot = Vector2.zero;
                p.Fill.sizeDelta = new Vector2(p._width, 9f);
                p._fillImage = fill.GetComponent<Image>();
                p._fillImage.raycastTarget = false;
                return p;
            }

            public void Set(string text, float fill01, Color barColor)
            {
                if (Label != null) Label.text = text;
                if (Fill == null) return;
                Fill.sizeDelta = new Vector2(_width * Mathf.Clamp01(fill01), 9f);
                _fillImage.color = barColor;
            }

            public void Show(bool on)
            {
                if (Root != null && Root.gameObject.activeSelf != on) Root.gameObject.SetActive(on);
            }

            public void Destroy()
            {
                if (Root != null) Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        public static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>배가 둘 수 있는 물 위인가(섬 · 기슭 · 수로 밖이 아님).</summary>
        public static bool OpenWater(Vector3 p, float clearance)
            => Game.World.Islands.IsClear(p, clearance) && Game.World.SeaRegion.InWater(p, clearance + 2f);
    }
}
