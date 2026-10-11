using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 함교 지붕의 원격 기관총(2026-10-11). 무기 블록을 하나도 못 얻었거나 다 잃어도 소형정·드론에 최소한의 반격을 한다.
    /// 기관포(사거리 22, 초당 약 12)보다 훨씬 약하다(사거리 18, 초당 약 4). 탄약 무한, 함교 위라 사격각 제한 없음(360°).
    /// 모델은 함교 렌더러 위쪽에 작은 받침·포탑·총열을 만들어 얹는다(함교 FBX에는 기관총 자리가 없다).
    /// 함교가 부서지면(Tick이 멈추면) 함께 멈춘다.
    /// </summary>
    [System.Serializable]
    public class BridgeMachineGun
    {
        [Tooltip("탄 프리팹(기관포 탄을 쓴다). 비면 기관총이 없다.")]
        [SerializeField] private GameObject projectile;
        [SerializeField] private float range = 18f;
        [SerializeField] private float damage = 1f;
        [Tooltip("발 간격(초)")]
        [SerializeField] private float interval = 0.15f;
        [SerializeField, Min(1)] private int burstRounds = 5;
        [SerializeField] private float burstPause = 0.5f;
        [SerializeField] private float projectileSpeed = 75f;
        [SerializeField] private float turnRate = 220f;
        [SerializeField] private float spreadDegrees = 1.6f;

        private readonly WeaponController _weapon = new();
        private TargetingSystem _targeting;
        private System.Func<ITargetable, float, float> _score;
        private float _pause;
        private int _burst;
        private Transform _mount;

        /// <summary>검증용: 함교 기관총을 잠깐 쏘지 않게 한다(편대 표적 선택·진형처럼 적이 살아 있어야 하는 검사).</summary>
        public static bool DevHoldFire;
        public bool HasGun => projectile != null && _mount != null;
        /// <summary>탄 프리팹(항공대 호출의 전투기 기총도 같은 탄을 쓴다).</summary>
        public GameObject Projectile => projectile;
        public int Shots { get; private set; }
        public Transform Mount => _mount;
        public float Range => range;

        public void Init(ModuleRuntime owner)
        {
            if (projectile == null || owner == null) return;
            _targeting = owner.GetComponentInParent<TargetingSystem>();
            _score = (t, sqr) => sqr;
            BuildMount(owner.transform);
        }

        public void Tick(float dt, Transform ship)
        {
            if (!HasGun || _targeting == null || DevHoldFire) return;
            _weapon.Tick(dt);
            if (_pause > 0f) _pause -= dt;
            Vector3 from = _mount.position;
            var target = _targeting.GetBest(from, range, TargetClass.Surface | TargetClass.Submarine | TargetClass.Air, _score, null);
            if (target == null) { _burst = 0; return; }

            Vector3 aim = LeadPoint(target);
            _weapon.AimAt(aim, turnRate, dt);
            if (!_weapon.IsAimedAt(aim) || _pause > 0f || !_weapon.TryFire(interval)) return;

            var muzzle = _weapon.Muzzle;
            Vector3 dir = _weapon.FireDirection;
            dir = Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees), Vector3.up) *
                  Quaternion.AngleAxis(Random.Range(-spreadDegrees, spreadDegrees) * 0.5f, Vector3.Cross(dir, Vector3.up).normalized) * dir;
            var go = PoolManager.Instance?.Spawn(projectile, muzzle.position, Quaternion.LookRotation(dir));
            if (go != null && go.TryGetComponent<Projectile>(out var round)) { round.Launch(dir, projectileSpeed, damage, DamageSource.Gun); round.StatsKey = "함교 기관총"; }
            AudioManager.Play(Game.Data.SfxId.AutocannonShot, muzzle.position, 0.45f, 1.35f);   // 기관포보다 가볍고 높게
            Shots++;
            if (++_burst >= burstRounds) { _burst = 0; _pause = burstPause; }
        }

        private Vector3 LeadPoint(ITargetable target)
        {
            Vector3 p = target.Transform.position;
            Vector3 v = target switch
            {
                IHasVelocity moving => moving.Velocity,
                EnemyController enemy => enemy.MoveVelocity,
                _ => Vector3.zero,
            };
            if (target is EnemyController && target is not AirEnemy) p += Vector3.up * 0.5f;
            return v.sqrMagnitude > 0.01f ? Ballistics.PredictIntercept(_weapon.Muzzle.position, p, v, projectileSpeed) : p;
        }

        /// <summary>함교 렌더러 맨 위, 조금 앞쪽에 받침 → 포탑(선회) → 고각부 → 총열·포구를 만든다.</summary>
        private void BuildMount(Transform bridge)
        {
            var renderers = bridge.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            Material metal = null;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
                if (metal == null && r.sharedMaterial != null && r.sharedMaterial.name.ToLowerInvariant().Contains("gunmetal")) metal = r.sharedMaterial;
            }
            if (metal == null) metal = renderers[0].sharedMaterial;

            Vector3 fwd = bridge.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) + fwd * Mathf.Min(1.2f, bounds.extents.magnitude * 0.25f);
            _mount = new GameObject("BridgeMG").transform;
            _mount.SetParent(bridge, true);
            _mount.position = top;
            _mount.rotation = Quaternion.LookRotation(fwd, Vector3.up);

            Part(PrimitiveType.Cylinder, _mount, new Vector3(0f, 0.1f, 0f), new Vector3(0.32f, 0.1f, 0.32f), metal);
            var turret = new GameObject("TurretPivot").transform;
            turret.SetParent(_mount, false);
            turret.localPosition = new Vector3(0f, 0.22f, 0f);
            Part(PrimitiveType.Cube, turret, new Vector3(0f, 0.1f, -0.04f), new Vector3(0.3f, 0.2f, 0.34f), metal);
            var elevation = new GameObject("ElevationPivot").transform;
            elevation.SetParent(turret, false);
            elevation.localPosition = new Vector3(0f, 0.12f, 0.08f);
            var barrel = Part(PrimitiveType.Cylinder, elevation, new Vector3(0f, 0f, 0.3f), new Vector3(0.06f, 0.3f, 0.06f), metal);
            barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(elevation, false);
            muzzle.localPosition = new Vector3(0f, 0f, 0.62f);
            foreach (var t in _mount.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = bridge.gameObject.layer;
            _weapon.BindArtTransforms(turret, elevation, muzzle);
            _weapon.SetElevationLimits(-45f, 70f);   // 지붕 위라 가까운 소형정은 크게 내려다 쏜다(기본 −10°면 머리 위로 넘어간다)
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }
    }
}
