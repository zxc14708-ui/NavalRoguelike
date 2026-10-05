using UnityEngine;
using Game.Combat;
using Game.Data;
using Game.Enemies;

namespace Game.View
{
    /// <summary>
    /// 엘리트 적 표식. 일반 적과 한눈에 구별되도록 황금색 표식을 단다.
    ///   - 수면 위 맥박치는 금색 고리(함체 크기에 맞춤)
    ///   - 머리 위 금색 마름모(항상 카메라를 향함, 위아래로 천천히 흔들림)
    /// 판정·충돌이 없는 연출이다. 잠항 중인 잠수함처럼 아직 드러나지 않은 적은 위치를 알려 주지 않도록 숨긴다.
    /// 풀에서 재사용되는 적에 한 번만 붙고, 적이 풀로 돌아가면 함께 꺼진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EliteMarker : MonoBehaviour
    {
        /// <summary>엘리트 표식색: 무장 사거리 원(노랑)·위험 원(붉은색)과 구별되게 주황 쪽 호박색.</summary>
        public static readonly Color Gold = new(1f, 0.58f, 0.08f, 1f);

        private EnemyController _enemy;
        private LineRenderer _ring;
        private LineRenderer _diamond;
        private Transform _diamondRoot;
        private float _radius, _height;
        private float _phase;
        private static Material s_material;

        /// <summary>표식이 지금 보이는가(검증용).</summary>
        public bool Visible { get; private set; }
        public float Alpha { get; private set; }

        /// <summary>엘리트 적에게만 표식을 붙인다(이미 있으면 크기만 다시 맞춘다).</summary>
        public static void Apply(EnemyController enemy)
        {
            if (enemy == null || enemy.Definition == null || enemy.Definition.Rank != EnemyRank.Elite) return;
            var marker = enemy.GetComponent<EliteMarker>();
            if (marker == null) marker = enemy.gameObject.AddComponent<EliteMarker>();
            marker.Init(enemy);
        }

        private void Init(EnemyController enemy)
        {
            _enemy = enemy;
            _phase = Random.value * Mathf.PI * 2f;

            // 함체(콜라이더) 크기에 맞춘 고리 반지름과 표식 높이
            float halfLength = 2.5f, top = 1.5f;
            if (enemy.TryGetComponent<BoxCollider>(out var box))
            {
                var s = transform.lossyScale;
                halfLength = Mathf.Max(box.size.z * Mathf.Abs(s.z), box.size.x * Mathf.Abs(s.x)) * 0.5f;
                top = (box.center.y + box.size.y * 0.5f) * Mathf.Abs(s.y);
            }
            bool aircraft = enemy.Kind == TargetKind.Aircraft;
            _radius = Mathf.Clamp(halfLength + 1.1f, 2.4f, 12f);
            _height = aircraft ? 2.4f : Mathf.Max(top, 1.2f) + 2.4f;

            if (_ring == null) _ring = MakeLine("Elite ring", transform, 48, true, 0.2f);
            if (_diamondRoot == null)
            {
                var go = new GameObject("Elite diamond");
                _diamondRoot = go.transform;
                _diamondRoot.SetParent(transform, false);
                _diamond = MakeLine("Diamond outline", _diamondRoot, 4, true, 0.2f);
            }
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Sin(a) * _radius, 0f, Mathf.Cos(a) * _radius));
            }
            const float d = 0.95f;
            _diamond.SetPosition(0, new Vector3(0f, d, 0f));
            _diamond.SetPosition(1, new Vector3(d * 0.62f, 0f, 0f));
            _diamond.SetPosition(2, new Vector3(0f, -d, 0f));
            _diamond.SetPosition(3, new Vector3(-d * 0.62f, 0f, 0f));
            SetVisual(false, 0f);
        }

        private static LineRenderer MakeLine(string name, Transform parent, int points, bool loop, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            if (s_material == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                s_material = new Material(shader) { name = "Elite marker (Runtime)" };
            }
            line.sharedMaterial = s_material;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.startWidth = line.endWidth = width;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private void LateUpdate()
        {
            if (_enemy == null || _ring == null) return;
            // 격침·퇴장했거나 드러나지 않은 잠수함이면 숨긴다
            bool show = _enemy.IsAlive && _enemy.IsRevealed;
            float t = Time.unscaledTime * 1.6f + _phase;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI);
            SetVisual(show, 0.55f + 0.45f * pulse);
            if (!show) return;

            // 고리는 수면(월드 y ≈ 0.12)에, 표식은 머리 위에서 카메라를 향한다
            var p = transform.position;
            _ring.transform.position = new Vector3(p.x, 0.12f, p.z);
            _ring.transform.rotation = Quaternion.identity;
            float breathe = 1f + 0.05f * Mathf.Sin(t * Mathf.PI * 0.5f);
            _ring.transform.localScale = new Vector3(breathe, 1f, breathe) * (1f / Mathf.Max(0.01f, transform.lossyScale.x));
            _diamondRoot.position = p + Vector3.up * (_height + 0.25f * Mathf.Sin(t * 1.3f));
            var cam = Camera.main;
            if (cam != null) _diamondRoot.rotation = cam.transform.rotation;
            _diamondRoot.localScale = Vector3.one * (1f / Mathf.Max(0.01f, transform.lossyScale.x));
        }

        private void SetVisual(bool show, float alpha)
        {
            Visible = show;
            Alpha = show ? alpha : 0f;
            if (_ring != null) _ring.enabled = show;
            if (_diamond != null) _diamond.enabled = show;
            if (!show) return;
            var c = new Color(Gold.r, Gold.g, Gold.b, alpha);
            _ring.startColor = _ring.endColor = new Color(Gold.r, Gold.g, Gold.b, alpha * 0.8f);
            _diamond.startColor = _diamond.endColor = c;
        }

        private void OnDisable()
        {
            Visible = false;
            if (_ring != null) _ring.enabled = false;
            if (_diamond != null) _diamond.enabled = false;
        }
    }
}
