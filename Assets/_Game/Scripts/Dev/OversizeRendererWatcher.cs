#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Game.Combat;
using Game.Core;

namespace Game.Dev
{
    /// <summary>
    /// 개발용(에디터·개발 빌드에서만): 적 함정 근처에 비정상적으로 큰 물체가 생기면 콘솔에 한 번 알린다(2026-10-05,
    /// 사용자 제보 "엘리트 초계함을 따라다니는 거대한 어두운 구" 추적용 — 자동 시험으로는 재현되지 않아 실제 플레이에서 잡는다).
    /// 1초마다 적 30m 안의 메시 렌더러 중 가로 크기가 10m를 넘는 것을 찾아 경로·크기·재질·셰이더·배율을 경고로 남긴다.
    /// 바다·섬·바다 생물·항적·기름띠·편대 전술 표시·플레이어 함선은 원래 큰 것이라 뺀다. 같은 물체는 한 번만 알린다.
    /// </summary>
    public sealed class OversizeRendererWatcher : MonoBehaviour
    {
        private const float MinSize = 10f, Radius = 30f;
        private static readonly string[] Ignore =
            { "Wake", "Oil slick", "Submarine sinking foam", "Task force tactical rig", "Sea life", "Ocean", "Island", "Rock islet", "Wreck (" };

        private readonly HashSet<string> _reported = new();
        private float _next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindFirstObjectByType<OversizeRendererWatcher>() != null) return;
            var go = new GameObject("Oversize renderer watcher (dev)");
            DontDestroyOnLoad(go);
            go.AddComponent<OversizeRendererWatcher>();
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing) return;
            var player = gm.Player != null ? gm.Player.transform : null;

            var enemies = TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface);
            if (enemies.Count == 0) return;
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds;
                float size = Mathf.Max(b.size.x, b.size.z);
                if (size < MinSize) continue;
                if (player != null && r.transform.IsChildOf(player)) continue;
                if (r.gameObject.layer == Game.World.Islands.Layer) continue;
                string path = PathOf(r.transform);
                if (_reported.Contains(path) || IsIgnored(path)) continue;

                ITargetable near = null;
                float best = Radius;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e == null || e.Transform == null) continue;
                    Vector3 d = b.center - e.Transform.position; d.y = 0f;
                    if (d.magnitude < best) { best = d.magnitude; near = e; }
                }
                if (near == null) continue;

                _reported.Add(path);
                var m = r.sharedMaterial;
                Debug.LogWarning($"[큰 물체 감지] {path} · 크기 {b.size.x:0.#}×{b.size.y:0.#}×{b.size.z:0.#}m · 근처 적 {near.Transform.name}({best:0.#}m) · " +
                                 $"메시 {(r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null ? mf.sharedMesh.name : "-")} · " +
                                 $"재질 {(m != null ? m.name : "없음")} / 셰이더 {(m != null && m.shader != null ? m.shader.name : "-")} · 배율 {r.transform.lossyScale}", r);
            }
        }

        private static bool IsIgnored(string path)
        {
            foreach (var s in Ignore) if (path.Contains(s)) return true;
            return false;
        }

        private static string PathOf(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
#endif
