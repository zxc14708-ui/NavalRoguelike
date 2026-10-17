using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// 수동 어뢰 일제 발사(WWII 패치 B, 패치 A 4장 "좌/우 발사 방향 선택과 예상 경로 표시 후 발사").
    ///   - [좌현]/[우현] 키(Ww2Controls, 기본 Q/E)를 <b>누르고 있는 동안</b> 그 현측 사격각(정횡 ±30°)과 장전된 어뢰의 예상 경로를
    ///     수면에 그린다. 마우스 쪽으로 겨누며, 사격각 밖이면 붉게 바뀌고 이유를 띄운다.
    ///   - 키를 <b>떼면</b> 그 방향으로 발사한다. 사격각 밖·재장전 중·발사관 없음·통로 막힘이면 이유를 띄우고 쏘지 않는다.
    ///   - 전투 중(Playing)에만 받는다 — 일시정지·정비·결과 화면에서 키를 눌러도 쏘지 않고, 조준도 풀린다.
    /// 플레이어 기함에 붙는다(WWII 시험 전투가 시작할 때). 어뢰는 직진이라 적의 앞을 예측해 쏴야 한다.
    /// </summary>
    public sealed class Ww2TorpedoControl : MonoBehaviour
    {
        public static Ww2TorpedoControl Instance { get; private set; }

        private static readonly Color Valid = new(1f, 0.86f, 0.42f, 0.85f);
        private static readonly Color ArcColor = new(1f, 0.86f, 0.42f, 0.35f);
        private static readonly Color Invalid = new(1f, 0.3f, 0.22f, 0.85f);

        private ShipController _ship;
        private readonly List<Ww2BroadsideTorpedoModule> _mounts = new();
        private Ww2Side? _aiming;
        private Vector3 _aimDir;
        private bool _aimValid;
        private LineRenderer _arc;
        private readonly List<LineRenderer> _paths = new();
        private float _scanTimer;

        /// <summary>마지막 안내(발사 결과·못 쏜 이유)와 그 시각.</summary>
        public string LastMessage { get; private set; }
        public float LastMessageTime { get; private set; } = -99f;
        public Ww2Side? Aiming => _aiming;
        public bool AimValid => _aimValid;
        public IReadOnlyList<Ww2BroadsideTorpedoModule> Mounts { get { Scan(); return _mounts; } }
        /// <summary>검증용: 마우스 대신 이 지점을 겨눈다.</summary>
        [System.NonSerialized] public Vector3? DevAimPoint;

        public static Ww2TorpedoControl Attach(ShipController ship)
        {
            if (ship == null) return null;
            var c = ship.GetComponent<Ww2TorpedoControl>();
            if (c == null) c = ship.gameObject.AddComponent<Ww2TorpedoControl>();
            c._ship = ship;
            return c;
        }

        private void Awake() { Instance = this; _ship = GetComponent<ShipController>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Scan()
        {
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer > 0f && _mounts.Count > 0) return;
            _scanTimer = 0.5f;
            _mounts.Clear();
            if (_ship == null || _ship.Grid == null) return;
            foreach (var m in _ship.Grid.Modules)
                if (m?.Runtime is Ww2BroadsideTorpedoModule t) _mounts.Add(t);
        }

        /// <summary>현측별 상태: 장전된 어뢰 · 전체 · 가장 빠른 재장전 남은 초 · 그 현측으로 쏠 수 있는 발사관 수.</summary>
        public (int loaded, int tubes, float reload, int mounts) Status(Ww2Side side)
        {
            Scan();
            int loaded = 0, tubes = 0, mounts = 0;
            float reload = float.MaxValue;
            foreach (var m in _mounts)
            {
                if (m == null || !m.Operational || !m.IsOpen(side)) continue;
                mounts++;
                tubes += m.Tubes;
                if (m.Reloading) reload = Mathf.Min(reload, m.ReloadLeft);
                else loaded += m.Loaded;
            }
            return (loaded, tubes, reload == float.MaxValue ? 0f : reload, mounts);
        }

        private static bool InCombat => GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

        private void Update()
        {
            if (!InCombat) { CancelAim(); return; }
            foreach (Ww2Side side in new[] { Ww2Side.Port, Ww2Side.Starboard })
            {
                var control = side == Ww2Side.Port ? Ww2Control.TorpedoPort : Ww2Control.TorpedoStarboard;
                if (Ww2Controls.Pressed(control)) _aiming = side;
                if (_aiming == side && Ww2Controls.Released(control))
                {
                    UpdateAim(side);
                    TryFire(side, _aimDir, out _);
                    CancelAim();
                }
            }
            if (_aiming.HasValue)
            {
                var held = _aiming == Ww2Side.Port ? Ww2Control.TorpedoPort : Ww2Control.TorpedoStarboard;
                if (!Ww2Controls.Held(held) && Keyboard.current != null) { CancelAim(); return; }
                UpdateAim(_aiming.Value);
                DrawPreview(_aiming.Value);
            }
        }

        /// <summary>마우스(또는 검증용 지점) 쪽으로 겨눈다. 사격각 밖이면 경계로 잘린 방향 + 무효.</summary>
        private void UpdateAim(Ww2Side side)
        {
            Scan();
            Vector3 center = transform.position;
            Vector3 want = Vector3.zero;
            if (DevAimPoint.HasValue) want = DevAimPoint.Value - center;
            else if (Mouse.current != null && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Mathf.Abs(ray.direction.y) > 1e-4f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    if (t > 0f) want = ray.origin + ray.direction * t - center;
                }
            }
            want.y = 0f;
            var mount = FirstMount(side);
            if (mount == null) { _aimDir = (side == Ww2Side.Port ? -transform.right : transform.right); _aimValid = false; return; }
            _aimValid = mount.ClampToArc(side, want.sqrMagnitude > 1f ? want : mount.Beam(side), out _aimDir, out _);
            foreach (var m in _mounts) if (m != null && m.IsOpen(side)) m.Train(side);
        }

        private Ww2BroadsideTorpedoModule FirstMount(Ww2Side side)
        {
            foreach (var m in _mounts) if (m != null && m.Operational && m.IsOpen(side)) return m;
            return null;
        }

        /// <summary>
        /// 그 현측의 발사관 전부로 일제 발사. direction은 월드 수평 방향(사격각 밖이면 쏘지 않는다). 쏜 발 수가 있으면 true.
        /// </summary>
        public bool TryFire(Ww2Side side, Vector3 direction, out string reason)
        {
            reason = null;
            Scan();
            if (!InCombat) { reason = "전투 중이 아님"; return Report(false, reason); }
            Ww2BroadsideTorpedoModule first = null;
            string blocked = null;
            foreach (var m in _mounts)
            {
                if (m == null) continue;
                if (!m.IsOpen(side)) { blocked ??= $"{Ww2BroadsideTorpedoModule.SideName(side)} 발사 통로가 막힘"; continue; }
                if (!m.Operational) { blocked ??= "발사관 파괴됨"; continue; }
                first ??= m;
            }
            if (first == null) { reason = blocked ?? $"{Ww2BroadsideTorpedoModule.SideName(side)}으로 쏠 발사관 없음"; return Report(false, reason); }
            if (!first.ClampToArc(side, direction, out _, out float off))
            {
                reason = $"사격각 밖 — {Ww2BroadsideTorpedoModule.SideName(side)} 정횡 ±{first.HalfArc:0}° (지금 {Mathf.Abs(off):0}°)";
                return Report(false, reason);
            }
            int fired = 0;
            string why = null;
            foreach (var m in _mounts)
            {
                if (m == null || !m.IsOpen(side)) continue;
                if (!m.CanFire(side, out string r)) { why ??= r; continue; }
                fired += m.Fire(side, direction);
            }
            if (fired == 0) { reason = why ?? "장전된 어뢰 없음"; return Report(false, reason); }
            return Report(true, $"{Ww2BroadsideTorpedoModule.SideName(side)} 어뢰 {fired}발 발사");
        }

        private bool Report(bool ok, string message)
        {
            LastMessage = message;
            LastMessageTime = Time.unscaledTime;
            if (!ok && InCombat) GameEvents.RaiseBanner($"어뢰 발사 안 함 — {message}");
            return ok;
        }

        // ------------------------------------------------------------ 수면 표시

        private void EnsureLines(int paths)
        {
            if (_arc == null) _arc = Ww2Marks.Line(null, "WW2 torpedo arc", ArcColor, 0.35f, true);
            while (_paths.Count < paths) _paths.Add(Ww2Marks.Line(null, $"WW2 torpedo path {_paths.Count}", Valid, 0.42f));
        }

        private void DrawPreview(Ww2Side side)
        {
            var mount = FirstMount(side);
            int loaded = 0;
            foreach (var m in _mounts) if (m != null && m.Operational && m.IsOpen(side) && !m.Reloading) loaded += m.Loaded;
            EnsureLines(Mathf.Max(loaded, 1));
            if (mount == null) { HideLines(); return; }
            float range = mount.Stats.Range;
            Ww2Marks.Wedge(_arc, mount.LaunchOrigin(side), mount.Beam(side), mount.HalfArc, range * 0.45f);
            _arc.enabled = true;
            Color c = _aimValid && loaded > 0 ? Valid : Invalid;
            int shown = Mathf.Max(loaded, 1);
            for (int i = 0; i < _paths.Count; i++)
            {
                var line = _paths[i];
                if (i >= shown) { line.enabled = false; continue; }
                float a = (i - (shown - 1) * 0.5f) * Ww2BroadsideTorpedoModule.SpreadDegrees;
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * _aimDir;
                Vector3 from = mount.LaunchOrigin(side);
                Ww2Marks.Segment(line, from, from + dir * range);
                Ww2Marks.SetColor(line, c);
                line.enabled = true;
            }
        }

        /// <summary>검증·스크린샷용: 키 없이 그 현측 조준 미리보기를 그린다(point를 겨눔). null이면 지운다.</summary>
        public void DevPreview(Ww2Side side, Vector3? point)
        {
            DevAimPoint = point;
            if (!point.HasValue) { CancelAim(); return; }
            UpdateAim(side);
            DrawPreview(side);
        }

        private void HideLines()
        {
            if (_arc != null) _arc.enabled = false;
            foreach (var l in _paths) if (l != null) l.enabled = false;
        }

        private void CancelAim()
        {
            _aiming = null;
            HideLines();
        }

        private void OnDisable() => HideLines();
    }
}
