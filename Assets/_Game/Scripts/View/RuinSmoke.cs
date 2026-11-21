using UnityEngine;
using Game.World;

namespace Game.View
{
    /// <summary>
    /// 시설 잔해(2026-11-21 Codex IslandBaseFollowup_v1 MIL_Ruins_*): 부서진 시설 자리에 잔해 모델을 같은 자리 · 방향 · 크기로 놓고,
    /// SmokePoint에서 40초 동안 검은 연기가 오르다 잦아든다. 잔해는 그 섬 뿌리 아래에 둬서 섬을 치울 때 함께 사라진다.
    /// </summary>
    public sealed class RuinSmoke : MonoBehaviour
    {
        public const float Seconds = 40f;
        private Transform _point;
        private float _age, _debt;

        /// <summary>검증용: 지금까지 놓은 잔해 수.</summary>
        public static int Placed { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Placed = 0;

        /// <summary>path(Resources)의 잔해를 model(없으면 owner)의 자리에 놓는다.</summary>
        public static GameObject LeaveRuins(string path, Transform model, Transform owner)
        {
            var prefab = Resources.Load<GameObject>(path);
            var at = model != null ? model : owner;
            if (prefab == null || at == null) return null;
            Transform parent = null;
            float best = float.MaxValue;
            foreach (var i in Islands.All)
            {
                if (i == null || i.Root == null) continue;
                float d = (new Vector2(i.Center.x, i.Center.z) - new Vector2(at.position.x, at.position.z)).sqrMagnitude;
                if (d < best && d < (i.Radius + 6f) * (i.Radius + 6f)) { best = d; parent = i.Root.transform; }
            }
            var go = Instantiate(prefab, at.position, at.rotation, parent);
            go.name = prefab.name;
            go.transform.localScale = Vector3.one;
            var ls = go.transform.lossyScale;
            var want = at.lossyScale;
            go.transform.localScale = new Vector3(want.x / Mathf.Max(1e-4f, ls.x), want.y / Mathf.Max(1e-4f, ls.y), want.z / Mathf.Max(1e-4f, ls.z));
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);
            var smoke = go.AddComponent<RuinSmoke>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "SmokePoint") { smoke._point = t; break; }
            Placed++;
            return go;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age > Seconds) { enabled = false; return; }
            float k = 1f - _age / Seconds;
            _debt += Time.deltaTime * 9f * k * Game.Core.DisplaySettings.DecorDensity;
            Vector3 p = _point != null ? _point.position : transform.position + Vector3.up;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                float g = Random.Range(0.06f, 0.14f);
                DecorFx.Emit(DecorFx.WreckSmoke, p + Random.insideUnitSphere * 0.4f, Vector3.up * Random.Range(1.6f, 2.8f) + DecorFx.Wind * 0.7f,
                    Random.Range(2.5f, 4f), Random.Range(1f, 1.8f), new Color(g, g, g, 0.6f * k + 0.2f));
            }
        }
    }
}
