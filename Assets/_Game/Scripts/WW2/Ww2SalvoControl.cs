using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Core;
using Game.Combat;
using Game.Ship;

namespace Game.WW2
{
    /// <summary>
    /// 주포 일제(WWII 패치 C3a, 패치 A 4장 "주포 일제: 자동 사격을 잠시 보류하고 준비된 포만 발사하며 무료 추가탄을 생성하지 않는다").
    ///   - [주포 일제] 키(Ww2Controls.MainSalvo, 기본 V)를 <b>누르고 있는 동안</b> 대구경 주포가 자동 사격을 멈추고 겨눈 곳으로 돈다.
    ///     마우스 근처(<see cref="SnapRadius"/> m 안)에 수상함이 있으면 그 배의 앞(탄 비행 시간)을 겨눈다. 수면에 탄착 원을 그린다.
    ///   - <b>떼면</b> 장전·조준이 끝난 주포만 한꺼번에 쏜다(흩어짐 좁게). 못 쏜 포는 이유를 모아 띄운다.
    ///   - 전투 중(Playing)에만 받는다. 대구경 주포가 없으면 아무것도 하지 않는다.
    /// </summary>
    public sealed class Ww2SalvoControl : MonoBehaviour
    {
        public const float SnapRadius = 12f;

        public static Ww2SalvoControl Instance { get; private set; }

        private static readonly Color Ready = new(1f, 0.86f, 0.42f, 0.9f);
        private static readonly Color NotReady = new(1f, 0.3f, 0.22f, 0.9f);

        private ShipController _ship;
        private readonly List<Ww2HeavyGunModule> _guns = new();
        private float _scanTimer;
        private bool _aiming;
        private Vector3 _point;
        private ITargetable _target;
        private LineRenderer _ring;

        public string LastMessage { get; private set; }
        public float LastMessageTime { get; private set; } = -99f;
        public bool Aiming => _aiming;
        public int Salvos { get; private set; }
        public IReadOnlyList<Ww2HeavyGunModule> Guns { get { Scan(); return _guns; } }
        /// <summary>검증용: 마우스 대신 이 지점을 겨눈다.</summary>
        [System.NonSerialized] public Vector3? DevAimPoint;

        public static Ww2SalvoControl Attach(ShipController ship)
        {
            if (ship == null) return null;
            var c = ship.GetComponent<Ww2SalvoControl>();
            if (c == null) c = ship.gameObject.AddComponent<Ww2SalvoControl>();
            c._ship = ship;
            return c;
        }

        private void Awake() { Instance = this; _ship = GetComponent<ShipController>(); }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Scan()
        {
            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer > 0f && _guns.Count > 0) return;
            _scanTimer = 0.5f;
            _guns.Clear();
            if (_ship == null || _ship.Grid == null) return;
            foreach (var m in _ship.Grid.Modules)
                if (m?.Runtime is Ww2HeavyGunModule g) _guns.Add(g);
        }

        /// <summary>장전된 주포 · 전체 · 가장 빠른 장전 남은 초.</summary>
        public (int loaded, int total, float reload) Status()
        {
            Scan();
            int loaded = 0, total = 0;
            float reload = float.MaxValue;
            foreach (var g in _guns)
            {
                if (g == null || !g.Operational) continue;
                total++;
                if (g.Loaded) loaded++;
                else reload = Mathf.Min(reload, g.Weapon.IsReady ? 0f : ReloadLeft(g));
            }
            return (loaded, total, reload == float.MaxValue ? 0f : reload);
        }

        private static float ReloadLeft(Ww2HeavyGunModule g) => g.Weapon.CooldownLeft;

        private static bool InCombat => GameManager.Instance == null || GameManager.Instance.State == GameState.Playing;

        private void Update()
        {
            if (!InCombat) { Cancel(); return; }
            if (Ww2Controls.Pressed(Ww2Control.MainSalvo)) Begin();
            if (_aiming && Ww2Controls.Released(Ww2Control.MainSalvo)) { UpdateAim(); Fire(); Cancel(); return; }
            if (_aiming && !DevAimPoint.HasValue && Keyboard.current != null && !Ww2Controls.Held(Ww2Control.MainSalvo)) { Cancel(); return; }
            if (_aiming) { UpdateAim(); Draw(); }
        }

        private void Begin()
        {
            Scan();
            if (_guns.Count == 0) { Report(false, "대구경 주포 없음"); return; }
            _aiming = true;
        }

        /// <summary>마우스(또는 검증용 지점) 쪽 — 근처 수상함이 있으면 그 배의 앞을 겨눈다.</summary>
        private void UpdateAim()
        {
            Vector3? raw = DevAimPoint;
            if (!raw.HasValue && Mouse.current != null && Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Mathf.Abs(ray.direction.y) > 1e-4f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    if (t > 0f) raw = ray.origin + ray.direction * t;
                }
            }
            if (!raw.HasValue) return;
            _target = NearestSurface(raw.Value);
            Ww2HeavyGunModule first = null;
            foreach (var g in _guns) if (g != null && g.Operational) { first = g; break; }
            _point = _target != null && first != null ? first.LeadPoint(_target) : raw.Value;
            foreach (var g in _guns) if (g != null) g.Hold = _point;
        }

        private static ITargetable NearestSurface(Vector3 p)
        {
            ITargetable best = null;
            float bestSqr = SnapRadius * SnapRadius;
            foreach (var e in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (e == null || !e.IsAlive || e.Transform == null) continue;
                Vector3 d = e.Transform.position - p;
                d.y = 0f;
                if (d.sqrMagnitude < bestSqr) { bestSqr = d.sqrMagnitude; best = e; }
            }
            return best;
        }

        /// <summary>준비된 주포로 일제. 쏜 주포 수.</summary>
        private int Fire()
        {
            int guns = 0, shells = 0;
            string why = null;
            foreach (var g in _guns)
            {
                if (g == null) continue;
                if (!g.CanSalvoAt(_point, out string r)) { why ??= r; continue; }
                int n = g.FireSalvo(_point, g.DamageFor(_target));
                if (n > 0) { guns++; shells += n; }
            }
            if (guns == 0) { Report(false, why ?? "쏠 주포 없음"); return 0; }
            Salvos++;
            Report(true, $"주포 일제 {guns}문 · {shells}발" + (why != null && guns < _guns.Count ? $" (나머지: {why})" : ""));
            return guns;
        }

        /// <summary>검증·봇용: 키 없이 point를 겨눠 일제 사격까지(조준이 끝날 때까지 기다리지 않는다 — 준비된 포만).</summary>
        public int DevSalvo(Vector3 point)
        {
            Scan();
            DevAimPoint = point;
            UpdateAim();
            int n = Fire();
            Cancel();
            return n;
        }

        /// <summary>검증용: 키 없이 조준만 시작/끝(자동 사격 보류 확인).</summary>
        public void DevHold(Vector3? point)
        {
            Scan();
            DevAimPoint = point;
            if (point.HasValue) { _aiming = true; UpdateAim(); Draw(); }
            else Cancel();
        }

        private void Report(bool ok, string message)
        {
            LastMessage = message;
            LastMessageTime = Time.unscaledTime;
            if (!ok && InCombat) GameEvents.RaiseBanner($"주포 일제 안 함 — {message}");
        }

        private void Draw()
        {
            if (_ring == null) _ring = Ww2Marks.Line(null, "WW2 salvo aim", Ready, 0.4f, true);
            int ready = 0;
            foreach (var g in _guns) if (g != null && g.CanSalvoAt(_point, out _)) ready++;
            Ww2Marks.Ring(_ring, _point, _target != null ? 4f : 2.5f);
            Ww2Marks.SetColor(_ring, ready > 0 ? Ready : NotReady);
            _ring.enabled = true;
        }

        private void Cancel()
        {
            _aiming = false;
            DevAimPoint = null;
            foreach (var g in _guns) if (g != null) g.Hold = null;
            if (_ring != null) _ring.enabled = false;
        }

        private void OnDisable() => Cancel();
    }
}
