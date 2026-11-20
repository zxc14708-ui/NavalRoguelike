using UnityEngine;
using UnityEngine.InputSystem;
using Game.Combat;
using Game.Core;
using Game.Enemies;

namespace Game.View
{
    /// <summary>
    /// 적 추적(2026-11-20): [Z](설정 › 조작 "적 추적")로 적 하나를 골라 따라간다.
    ///   - 고르는 순서: 마우스 아래 적(화면 70px 안) → 살아 있는 보스 → 기함에 가장 가까운 적(숨은 반잠수정 · 잠항 잠수함은 빼고).
    ///   - 추적 중: 카메라가 기함과 그 적을 함께 담도록 초점을 그쪽으로 옮기고 조금 물러난다(QuarterViewCamera — 기함은 화면 안에 남김).
    ///     수면에 호박색 고리가 표적을 따라다니고, 화면 밖이면 거리에 상관없이 가장자리에 "추적 · 종류 거리"(MissileWarningUI).
    ///   - 다시 누르면 해제(마우스 아래 다른 적이 있으면 그 적으로 바꾼다). 표적이 격침 · 잠항 · 은닉하면 저절로 해제.
    /// 보기 전용 — 무장 조준 · 판정은 바꾸지 않는다.
    /// </summary>
    public sealed class EnemyTracker : MonoBehaviour
    {
        public const float HoverPixels = 70f;
        private static readonly Color RingColor = new(1f, 0.72f, 0.2f);
        private static readonly TargetKind[] Kinds = { TargetKind.Surface, TargetKind.Aircraft, TargetKind.Submarine };

        /// <summary>지금 추적 중인 적(없으면 null).</summary>
        public static EnemyController Target { get; private set; }
        /// <summary>카메라가 따라갈 자리(추적 중이 아니면 null).</summary>
        public static Transform TrackedTransform => Target != null ? Target.transform : null;
        /// <summary>검증용: 추적을 시작한 횟수.</summary>
        public static int Started { get; private set; }

        private Camera _camera;
        private LineRenderer _ring;
        private static Material s_ringMat;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Target = null; Started = 0; }

        private void Awake() => _camera = GetComponent<Camera>();

        private void OnDisable() { Target = null; HideRing(); }

        private void Update()
        {
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.State == GameState.Playing;
            if (Target != null && !Trackable(Target)) Stop("추적 표적을 놓쳤습니다");
            if (playing && GameSettings.Pressed(NavalControl.Track)) Toggle();
            UpdateRing();
        }

        /// <summary>[Z]: 마우스 아래 적 → 보스 → 가장 가까운 적. 이미 추적 중이면 해제(마우스 아래 다른 적이 있으면 바꿈).</summary>
        public void Toggle()
        {
            var hovered = Hovered();
            if (Target != null && (hovered == null || hovered == Target)) { Stop("추적 해제"); return; }
            var pick = hovered != null ? hovered : Boss() ?? Nearest();
            if (pick == null) { GameEvents.RaiseBanner("추적할 적이 없습니다"); return; }
            Begin(pick);
        }

        /// <summary>표적을 정해 추적을 시작한다(검증 · 다른 UI에서도 부른다).</summary>
        public static void Begin(EnemyController enemy)
        {
            if (enemy == null || !Trackable(enemy)) return;
            Target = enemy;
            Started++;
            string name = enemy.Definition != null ? enemy.Definition.DisplayName : enemy.name;
            GameEvents.RaiseBanner($"{name} 추적 — {GameSettings.BindingLabel(NavalControl.Track)}로 해제");
        }

        public static void Stop(string message = null)
        {
            if (Target == null) return;
            Target = null;
            if (!string.IsNullOrEmpty(message)) GameEvents.RaiseBanner(message);
        }

        /// <summary>추적할 수 있는 적인가: 살아 있고, 적대 진영이고, 숨지 않았다.</summary>
        public static bool Trackable(EnemyController e)
        {
            if (e == null || !e.IsAlive || !e.isActiveAndEnabled || e.Faction != CombatFaction.Hostile) return false;
            if (e.Kind == TargetKind.Submarine && !e.IsRevealed) return false;
            if (e is ILowObservable low && low.Stealthy) return false;
            return true;
        }

        private EnemyController Hovered()
        {
            var mouse = Mouse.current;
            if (mouse == null || _camera == null) return null;
            Vector2 m = mouse.position.ReadValue();
            float best = HoverPixels * HoverPixels;
            EnemyController found = null;
            foreach (var kind in Kinds)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is not EnemyController e || !Trackable(e)) continue;
                    Vector3 sp = _camera.WorldToScreenPoint(e.transform.position);
                    if (sp.z < 0f) continue;
                    float d = ((Vector2)sp - m).sqrMagnitude;
                    if (d < best) { best = d; found = e; }
                }
            }
            return found;
        }

        private static EnemyController Boss()
        {
            foreach (var kind in Kinds)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is EnemyController e && e is IBossTelegraph && Trackable(e)) return e;
            }
            return null;
        }

        private static EnemyController Nearest()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return null;
            Vector3 origin = player.transform.position;
            float best = float.MaxValue;
            EnemyController found = null;
            foreach (var kind in Kinds)
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is not EnemyController e || !Trackable(e)) continue;
                    Vector3 d = e.transform.position - origin; d.y = 0f;
                    if (d.sqrMagnitude < best) { best = d.sqrMagnitude; found = e; }
                }
            }
            return found;
        }

        // ------------------------------------------------------------ 수면 고리

        private void UpdateRing()
        {
            if (Target == null) { HideRing(); return; }
            if (_ring == null)
            {
                if (s_ringMat == null) s_ringMat = new Material(Shader.Find("Sprites/Default")) { name = "Track ring (Runtime)" };
                var go = new GameObject("Track ring");
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                _ring = go.AddComponent<LineRenderer>();
                _ring.sharedMaterial = s_ringMat;
                _ring.useWorldSpace = true;
                _ring.loop = true;
                _ring.alignment = LineAlignment.TransformZ;
                _ring.positionCount = 40;
                _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ring.receiveShadows = false;
            }
            _ring.enabled = true;
            float radius = RadiusOf(Target);
            float pulse = 0.65f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f);
            _ring.startColor = _ring.endColor = new Color(RingColor.r, RingColor.g, RingColor.b, pulse);
            _ring.startWidth = _ring.endWidth = 0.55f;
            Vector3 c = Target.transform.position;
            for (int i = 0; i < 40; i++)
            {
                float a = i / 40f * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * radius, 0.16f, c.z + Mathf.Sin(a) * radius));
            }
        }

        private static float RadiusOf(EnemyController e)
        {
            if (e.TryGetComponent<BoxCollider>(out var box))
            {
                Vector3 s = Vector3.Scale(box.size, e.transform.lossyScale);
                return Mathf.Clamp(Mathf.Max(s.x, s.z) * 0.6f + 1.5f, 3f, 20f);
            }
            return 5f;
        }

        private void HideRing() { if (_ring != null) _ring.enabled = false; }

        private void OnDestroy() { if (_ring != null) Destroy(_ring.gameObject); }
    }
}
