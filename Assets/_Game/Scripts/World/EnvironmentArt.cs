using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Codex 섬·해안 소품 프리팹(Resources/Environment/…)에 붙는 요약. 에디터 빌더(NavalEnvironmentArtBuilder)가 채운다.
    ///   - 섬: 해안선(각도별 수면 반경)·높이·장식 표식. 판정은 프리팹 안 "Collision"의 볼록 조각(Terrain 레이어 13).
    ///   - 움직임: 레이더·풍속계는 돌고, 탐조등·풍향계는 흔들리고, 부표·어선은 물결에 출렁인다. 등불 자리에는 런타임이 깜박이는 빛을 단다.
    /// 메시는 모델(FBX) 자산이라 섬을 치울 때 지우면 안 된다(IslandField.Remove가 이 컴포넌트 아래는 건너뛴다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentArt : MonoBehaviour
    {
        [Header("섬 요약 (섬 프리팹만)")]
        public bool island;
        public bool rocky;
        /// <summary>수면 해안선의 최대 반경(m, 축척 1).</summary>
        public float shoreRadius;
        public float height;
        /// <summary>해안선: 0°(+X)부터 반시계로 같은 간격의 각도마다 수면 반경(로컬 xz, 축척 1).</summary>
        public float[] rimRadii;
        public bool lighthouse, wreck;
        public int seals;
        /// <summary>잔교 끝(배를 대는 자리)과 잔교 방향(로컬). 없으면 비어 있다.</summary>
        public Transform[] jettyEnds;

        [Header("움직임")]
        public Transform[] spinners;
        public Transform[] sweepers;
        public Transform[] bobbers;
        public Transform[] lamps;

        private readonly List<(Transform t, Quaternion rest, float phase)> _sweep = new();
        private readonly List<(Transform t, Vector3 rest, Quaternion restRot, float phase)> _bob = new();
        private bool _ready;

        /// <summary>각도(라디안, 로컬 +X에서 반시계 — xz 평면에서 atan2(z, x))의 수면 반경. 축척 1.</summary>
        public float RimAt(float angle)
        {
            if (rimRadii == null || rimRadii.Length == 0) return shoreRadius;
            int n = rimRadii.Length;
            float f = Mathf.Repeat(angle, Mathf.PI * 2f) / (Mathf.PI * 2f) * n;
            int i = Mathf.FloorToInt(f) % n;
            return Mathf.Lerp(rimRadii[i], rimRadii[(i + 1) % n], f - Mathf.Floor(f));
        }

        private void Init()
        {
            _ready = true;
            int k = 0;
            if (sweepers != null) foreach (var t in sweepers) if (t != null) _sweep.Add((t, t.localRotation, (k++ * 1.7f + transform.position.x * 0.13f) % 6.28f));
            if (bobbers != null) foreach (var t in bobbers) if (t != null) _bob.Add((t, t.localPosition, t.localRotation, (k++ * 2.3f + transform.position.z * 0.11f) % 6.28f));
        }

        private void LateUpdate()
        {
            if (!_ready) Init();
            float dt = Time.deltaTime, time = Time.time;
            if (spinners != null)
                foreach (var t in spinners) if (t != null) t.Rotate(Vector3.up, 60f * dt, Space.World);   // 6초에 한 바퀴
            foreach (var (t, rest, phase) in _sweep)
                if (t != null) t.localRotation = rest * Quaternion.AngleAxis(Mathf.Sin(time * 0.6f + phase) * 35f, t.InverseTransformDirection(Vector3.up));
            foreach (var (t, rest, restRot, phase) in _bob)
            {
                if (t == null) continue;
                float s = time * 1.3f + phase;
                t.localPosition = rest + t.parent.InverseTransformVector(Vector3.up * (Mathf.Sin(s) * 0.12f));
                t.localRotation = restRot * Quaternion.Euler(Mathf.Sin(s * 0.8f) * 3f, 0f, Mathf.Cos(s * 0.7f) * 4f);
            }
        }
    }
}
