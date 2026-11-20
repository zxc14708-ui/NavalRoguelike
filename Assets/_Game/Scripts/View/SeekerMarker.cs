using System.Collections.Generic;
using UnityEngine;
using Game.Combat;

namespace Game.View
{
    /// <summary>
    /// 함교 저격 미사일 표식(2026-11-21): 미사일이 노리는 기함 블록 위에 붉은 조준 고리(겹 고리 + 십자)를 띄운다.
    /// 미사일이 떨어지거나 · 기만체로 돌거나 · 칸을 잃으면 사라진다. 다가올수록(남은 거리) 고리가 좁아지고 빠르게 깜박인다.
    /// </summary>
    public sealed class SeekerMarker : MonoBehaviour
    {
        public static readonly Color MarkColor = new(1f, 0.18f, 0.12f);
        private static Material s_mat;
        private static readonly List<SeekerMarker> s_active = new();
        /// <summary>검증용: 지금 떠 있는 표식 수.</summary>
        public static int ActiveCount => s_active.Count;

        private Missile _missile;
        private LineRenderer _outer, _inner, _cross;
        private float _startDist = -1f;

        public static void Show(Missile missile)
        {
            if (missile == null) return;
            foreach (var m in s_active) if (m._missile == missile) return;
            var go = new GameObject("Seeker marker");
            var mk = go.AddComponent<SeekerMarker>();
            mk._missile = missile;
            mk._outer = mk.Line("Outer", 48, true);
            mk._inner = mk.Line("Inner", 32, true);
            mk._cross = mk.Line("Cross", 2, false);
            s_active.Add(mk);
        }

        private LineRenderer Line(string name, int count, bool loop)
        {
            if (s_mat == null) s_mat = new Material(Shader.Find("Sprites/Default")) { name = "Seeker marker (Runtime)" };
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = s_mat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.alignment = LineAlignment.TransformZ;
            lr.positionCount = count;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        private void OnDestroy() => s_active.Remove(this);

        private void LateUpdate()
        {
            if (_missile == null || !_missile.isActiveAndEnabled || !_missile.IsCommandSeeker ||
                !_missile.TryGetAimCell(out var grid, out var cell) || grid == null)
            {
                Destroy(gameObject);
                return;
            }
            Vector3 c = grid.CoordToWorld(cell) + Vector3.up * 0.35f;
            float dist = Vector3.Distance(_missile.transform.position, c);
            if (_startDist < 0f) _startDist = Mathf.Max(10f, dist);
            float close = 1f - Mathf.Clamp01(dist / _startDist);      // 0 = 막 쏨, 1 = 도달
            float rate = Mathf.Lerp(5f, 16f, close);
            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * rate));
            float r = Mathf.Lerp(2.6f, 1.0f, close) * Mathf.Max(0.6f, grid.CellSize);
            var col = new Color(MarkColor.r, MarkColor.g, MarkColor.b, pulse);
            Circle(_outer, c, r, 0.16f, col);
            Circle(_inner, c, r * 0.45f, 0.12f, col);
            _cross.startColor = _cross.endColor = col;
            _cross.startWidth = _cross.endWidth = 0.1f;
            // 십자: 미사일이 오는 방향으로 한 줄
            Vector3 toward = _missile.transform.position - c; toward.y = 0f;
            toward = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward;
            _cross.SetPosition(0, c + toward * r * 1.5f);
            _cross.SetPosition(1, c - toward * r * 0.2f);
        }

        private static void Circle(LineRenderer lr, Vector3 c, float r, float width, Color col)
        {
            lr.startColor = lr.endColor = col;
            lr.startWidth = lr.endWidth = width;
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, c.y, c.z + Mathf.Sin(a) * r));
            }
        }
    }
}
