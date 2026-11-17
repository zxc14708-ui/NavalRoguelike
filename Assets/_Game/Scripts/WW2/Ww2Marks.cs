using UnityEngine;

namespace Game.WW2
{
    /// <summary>
    /// 수면 표시선(WWII 패치 B): 어뢰 예상 경로·현측 사격각·뇌격기 진입선. 보급선 항로 표시와 같은 방식 —
    /// LineRenderer를 수면에 눕혀(위에서 보는 굵기 그대로) 그린다. 그림자·조명 없음.
    /// </summary>
    public static class Ww2Marks
    {
        public const float Height = 0.18f;   // 수면 바로 위(항로 선 0.15보다 조금 위)

        private static Material s_line;

        public static Material LineMaterial()
        {
            if (s_line != null) return s_line;
            var shader = Shader.Find("Sprites/Default");
            s_line = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "WW2 marks (Runtime)" };
            return s_line;
        }

        public static LineRenderer Line(Transform parent, string name, Color color, float width, bool loop = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = LineMaterial();
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.loop = loop;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.positionCount = 0;
            return line;
        }

        public static void Segment(LineRenderer line, Vector3 from, Vector3 to)
        {
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(from.x, Height, from.z));
            line.SetPosition(1, new Vector3(to.x, Height, to.z));
        }

        public static void Ring(LineRenderer line, Vector3 center, float radius, int segments = 40)
        {
            line.loop = true;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(center.x + Mathf.Sin(a) * radius, Height, center.z + Mathf.Cos(a) * radius));
            }
        }

        /// <summary>부채꼴 테두리(가운데 방향 ± half도, 반지름 radius).</summary>
        public static void Wedge(LineRenderer line, Vector3 origin, Vector3 center, float halfDegrees, float radius, int segments = 16)
        {
            center.y = 0f;
            if (center.sqrMagnitude < 1e-4f) center = Vector3.forward;
            center.Normalize();
            line.loop = true;
            line.positionCount = segments + 2;
            line.SetPosition(0, new Vector3(origin.x, Height, origin.z));
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(-halfDegrees, halfDegrees, i / (float)segments);
                Vector3 p = origin + Quaternion.Euler(0f, a, 0f) * center * radius;
                line.SetPosition(i + 1, new Vector3(p.x, Height, p.z));
            }
        }

        public static void SetColor(LineRenderer line, Color color) { line.startColor = line.endColor = color; }
    }
}
