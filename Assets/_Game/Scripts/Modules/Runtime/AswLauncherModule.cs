using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 대잠 폭뢰. 소나 접촉·발사 흔적·근거리 자체 센서로 잠수함을 찾는다.
    /// 자리에 따라 형태가 바뀐다(ModuleVariants, 격자가 바뀔 때마다 다시 판정):
    ///   - 측면 발사대(옆이 트인 자리): 트인 현측(정횡 ±75°)으로 사거리 안에 2발 던진다. 앞뒤·막힌 현측으로는 못 던진다.
    ///   - 선미 투하대(뒤가 트인 자리): 배 바로 뒤(선미 0~6m)에 4발을 떨어뜨린다. 피해 1.25배·재장전 0.8배 —
    ///     잠수함이 항적 가까이(투하 지점 8m 안) 있어야 한다.
    ///   - 사방이 막힌 자리(형태 없음)에는 설치되지 않는다. 혹시 남아 있으면 쏘지 않는다.
    /// 소나 모듈 없이도 8m 안에서는 대응할 수 있지만 사거리 활용에는 소나가 유리하다.
    /// </summary>
    public class AswLauncherModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject chargePrefab;
        [SerializeField] private Transform[] launchPoints;

        [Tooltip("한 번에 던지는 폭뢰 수")]
        [SerializeField, Min(1)] private int salvo = 2;

        [Tooltip("폭뢰끼리 떨어지는 간격(m). 조금 벌려야 빗나가도 하나는 걸린다.")]
        [SerializeField] private float spread = 2.5f;

        [Tooltip("폭뢰 비행 시간. 잠수함 이동 예측에 쓴다(DepthCharge와 맞출 것).")]
        [SerializeField] private float predictedFlightTime = 2.0f;   // 비행 1.1 + 가라앉음 0.9

        private float _cooldown;
        private int _nextTube;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;
        private TargetingSystem _targeting;

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;

        private ModuleVariant Variant => Instance != null ? Instance.Variant : ModuleVariant.None;

        /// <summary>검증용: 마지막 투하·발사의 목표 지점들.</summary>
        public System.Collections.Generic.List<Vector3> LastAims { get; } = new();
        public int Salvos { get; private set; }

        /// <summary>형태 모델의 발사점(투하대: 레일 출구, 발사대: 투사기)으로 다시 잇는다.</summary>
        protected override void OnVariantChanged(ModuleVariant variant, Transform visual)
        {
            if (visual == null) return;
            var points = new System.Collections.Generic.List<Transform>();
            for (int i = 1; i <= 8; i++)
            {
                var t = ModuleVariantVisual.Find(visual, $"LaunchPoint_{i:00}");
                if (t != null) points.Add(t);
            }
            if (points.Count > 0) launchPoints = points.ToArray();
        }

        private Vector3 ShipForward
        {
            get
            {
                var f = Grid != null ? Grid.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>선미 투하대가 떨어뜨리는 기준점(블록에서 선미 쪽).</summary>
        public Vector3 RackDropOrigin => transform.position - ShipForward * ModuleVariants.RackDropBehind;

        /// <summary>
        /// 이 형태가 그 지점에 폭뢰를 보낼 수 있는가. 투하대 = 항적(선미 0~6m) 가까이, 발사대 = 트인 현측 ±75°.
        /// </summary>
        public bool CanReach(Vector3 aim)
        {
            Vector3 d = aim - transform.position;
            d.y = 0f;
            switch (Variant)
            {
                case ModuleVariant.DepthChargeRack:
                    return Flat(aim - RackDropPoint(aim)).magnitude <= ModuleVariants.RackReach;
                case ModuleVariant.DepthChargeProjector:
                {
                    if (d.sqrMagnitude < 0.01f) return true;
                    Vector3 right = Vector3.Cross(Vector3.up, ShipForward);
                    var sides = Instance != null ? Instance.Sides : ModuleSides.None;
                    if ((sides & ModuleSides.Starboard) != 0 && Vector3.Angle(right, d) <= ModuleVariants.ProjectorHalfArc) return true;
                    if ((sides & ModuleSides.Port) != 0 && Vector3.Angle(-right, d) <= ModuleVariants.ProjectorHalfArc) return true;
                    return false;
                }
                case ModuleVariant.None:
                    return Instance == null || !ModuleVariants.HasVariants(Instance.Definition.Type);   // 형태가 없는 예전 배치(검증용 직접 설치 등)
                default:
                    return true;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>투하대: 항적선(투하 기준점에서 선미로 0~6m) 위에서 표적에 가장 가까운 점.</summary>
        private Vector3 RackDropPoint(Vector3 aim)
        {
            Vector3 origin = RackDropOrigin;
            Vector3 back = -ShipForward;
            float t = Mathf.Clamp(Vector3.Dot(Flat(aim - origin), back), 0f, 6f);
            var p = origin + back * t;
            p.y = aim.y;
            return p;
        }

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (chargePrefab == null) Debug.LogError("[ASW] chargePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
        }

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            if (_cooldown > 0f) { _cooldown -= dt; return; }
            if (chargePrefab == null || !_ammo.CanFire) return;

            var target = FindNearestSubmarine();
            Vector3 aim;
            if (target != null)
                aim = target.IsContactConfirmed
                    ? target.LastKnownPosition + target.LastKnownVelocity * predictedFlightTime
                    : target.transform.position;
            else if (_targeting == null || !_targeting.TryGetLaunchCue(transform.position, Stats.Range, out aim))
                return;

            // 형태별로 닿는 곳만(투하대 = 항적 가까이, 발사대 = 트인 현측). 못 닿으면 기다린다.
            if (!CanReach(aim)) { _cooldown = 0.25f; return; }
            bool rack = Variant == ModuleVariant.DepthChargeRack;

            int want = rack ? ModuleVariants.RackSalvo : salvo;
            int count = 0;
            while (count < want && _ammo.Consume()) count++;   // 남은 폭뢰만큼만 던진다
            _lastShotTime = Time.time;
            Salvos++;
            LastAims.Clear();

            float damage = Stats.Damage * (rack ? ModuleVariants.RackDamageMultiplier : 1f);
            if (rack)
            {
                // 항적 위 표적에 가장 가까운 점을 가운데로 2×2 마름모(앞뒤·좌우 1.4m)
                Vector3 center = RackDropPoint(aim);
                Vector3 back = -ShipForward, right = Vector3.Cross(Vector3.up, ShipForward);
                for (int i = 0; i < count; i++)
                {
                    Vector3 offset = i switch { 0 => back * -1.4f, 1 => right * 1.4f, 2 => -right * 1.4f, _ => back * 1.4f };
                    var tube = NextTube();
                    Vector3 to = center + offset;
                    LastAims.Add(to);
                    var go = PoolManager.Instance?.Spawn(chargePrefab, tube.position, tube.rotation);
                    go?.GetComponent<DepthCharge>()?.Launch(tube.position, to, damage);
                }
                AudioManager.Play(Game.Data.SfxId.EscortTorpedoLaunch, transform.position, 0.7f, 0.55f);   // 레일 굴러 떨어지는 소리
            }
            else
            {
                // 비행하고 가라앉는 동안 잠수함이 움직인 곳에 던진다
                Vector3 side = Vector3.Cross(Vector3.up, (aim - transform.position).normalized);
                for (int i = 0; i < count; i++)
                {
                    float offset = count == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (count - 1f)) * 0.5f;
                    var tube = NextTube();
                    Vector3 to = aim + side * offset;
                    LastAims.Add(to);
                    var go = PoolManager.Instance?.Spawn(chargePrefab, tube.position, tube.rotation);
                    go?.GetComponent<DepthCharge>()?.Launch(tube.position, to, damage);
                }
                AudioManager.Play(Game.Data.SfxId.AutocannonShot, transform.position, 0.5f, 0.6f);
            }
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime * (rack ? ModuleVariants.RackReloadMultiplier : 1f));
        }

        /// <summary>소나 접촉, 실제 부상, 또는 발사기 자체의 매우 짧은 거리 센서로 찾는다.</summary>
        private SubmarineBase FindNearestSubmarine()
        {
            float bestSqr = Stats.Range * Stats.Range;
            SubmarineBase best = null;

            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 trueDelta = sub.transform.position - transform.position;
                trueDelta.y = 0f;
                if (trueDelta.sqrMagnitude <= 64f) sub.ConfirmContact(1f);
                if (!sub.IsContactConfirmed && !sub.IsRevealed) continue;
                Vector3 reported = sub.IsRevealed ? sub.transform.position : sub.LastKnownPosition;
                Vector3 delta = reported - transform.position;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d >= bestSqr) continue;

                bestSqr = d;
                best = sub;
            }
            return best;
        }

        private Transform NextTube()
        {
            if (launchPoints == null || launchPoints.Length == 0) return transform;
            var t = launchPoints[_nextTube % launchPoints.Length];
            _nextTube++;
            return t != null ? t : transform;
        }
    }
}
