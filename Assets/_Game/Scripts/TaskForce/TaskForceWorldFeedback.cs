using System.Collections.Generic;
using UnityEngine;

namespace Game.TaskForce
{
    /// <summary>
    /// 편대 능력의 판정 없는 월드 연출. Codex 전술 표시 리그(Resources/TaskForce/Effects/FX_코드_Tactical)를
    /// 발동 지점에 펼친다: 0.2→1 크기로 펼쳐지며 밝기(최대 70%) 1→0으로 사라지고, 스캔·방위선 피벗은 월드 Y축으로 돈다.
    /// 리그 프리팹이 없으면 아무것도 하지 않는다. 호위함 자율 능력(EscortDefense)이 대잠·대함 타격·교란 때 부른다.
    /// (예전 지원 스킬의 발동 연출은 편대 개편 때 지원 스킬과 함께 없앴다.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskForceWorldFeedback : MonoBehaviour
    {
        public static TaskForceWorldFeedback Instance { get; private set; }

        /// <summary>펼친 전술 표시 리그 수(검증용).</summary>
        public int SpawnedRigCount { get; private set; }

        /// <summary>Codex 리그의 바깥 눈금 반경(m). 원하는 월드 반경 ÷ 이것 = 배율.</summary>
        private const float RigRadius = 4.45f;
        private static readonly Dictionary<string, GameObject> s_rigs = new();

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>전술 표시 리그를 수면 위에 펼친다(프리팹이 없으면 아무것도 안 함). code = AEW·CAP·ASW·EW·STK.</summary>
        public void PlayRig(string code, Vector3 center, float yaw, float worldRadius, float duration, float spinDegPerSec)
        {
            if (!s_rigs.TryGetValue(code, out var prefab))
            {
                prefab = Resources.Load<GameObject>($"TaskForce/Effects/FX_{code}_Tactical");
                s_rigs[code] = prefab;
            }
            if (prefab == null) return;
            center.y = transform.position.y + 0.25f;
            var go = Instantiate(prefab, center, Quaternion.Euler(0f, yaw, 0f));
            go.name = $"Task force tactical rig {code}";
            go.AddComponent<RigCue>().Begin(worldRadius / RigRadius, duration, spinDegPerSec);
            SpawnedRigCount++;
        }

        /// <summary>Codex 리그 재생: 0.2→1 크기로 펼치고(앞 35%), 잠깐 켜졌다가 밝기를 1→0으로 낮춘다. 스캔 피벗은 Y축 회전.</summary>
        private sealed class RigCue : MonoBehaviour
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private Renderer[] _renderers;
            private Color[] _colors;
            private Transform[] _spinners;
            private MaterialPropertyBlock _block;
            private float _scale, _duration, _spin, _age;

            public void Begin(float scale, float duration, float spinDegPerSec)
            {
                _scale = scale;
                _duration = Mathf.Max(0.1f, duration);
                _spin = spinDegPerSec;
                _renderers = GetComponentsInChildren<Renderer>();
                _colors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var m = _renderers[i].sharedMaterial;
                    _colors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                }
                var spin = new System.Collections.Generic.List<Transform>();
                foreach (var t in GetComponentsInChildren<Transform>())
                    if (t.name.StartsWith("SweepPivot") || t.name.StartsWith("BearingLinePivot")) spin.Add(t);
                _spinners = spin.ToArray();
                _block = new MaterialPropertyBlock();
                Apply(0f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float t = Mathf.Clamp01(_age / _duration);
                if (_spin != 0f)
                    foreach (var s in _spinners) s.Rotate(0f, _spin * Time.deltaTime, 0f, Space.World);
                Apply(t);
                if (t >= 1f) Destroy(gameObject);
            }

            private void Apply(float t)
            {
                float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.35f), 3f);
                transform.localScale = Vector3.one * (_scale * Mathf.Lerp(0.2f, 1f, grow));
                // 최대 밝기 70%: 여러 지원이 겹쳐도 함선·탄·경고가 비쳐 보이게
                float alpha = 0.7f * (t < 0.08f ? t / 0.08f : 1f - Mathf.Clamp01((t - 0.3f) / 0.7f));
                for (int i = 0; i < _renderers.Length; i++)
                {
                    var c = _colors[i];
                    _renderers[i].GetPropertyBlock(_block);
                    _block.SetColor(BaseColorId, new Color(c.r, c.g, c.b, c.a * alpha));
                    _renderers[i].SetPropertyBlock(_block);
                }
            }
        }

    }
}
