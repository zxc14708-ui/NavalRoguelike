using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 항공 위협의 공통 뼈대. 함정과 달리 고도를 갖고 3차원으로 난다.
    ///
    /// 표적 분류가 Aircraft라 레이더에 잡히고, 기관포·76mm·CIWS·함대공 미사일이 쏠 수 있다.
    /// VLS·유도로켓·대잠 무기는 노리지 않는다 — 대공 모듈이 존재 이유를 갖게 한다.
    /// </summary>
    public abstract class AirEnemy : EnemyController, IHasVelocity
    {
        [Header("Flight")]
        [Tooltip("평소 비행 고도(m)")]
        [SerializeField] protected float cruiseAltitude = 7f;

        private Vector3 _velocity;

        public override TargetKind Kind => TargetKind.Aircraft;

        /// <summary>요격 무기의 예측 사격용.</summary>
        public Vector3 Velocity => DevFrozen ? Vector3.zero : _velocity;   // 멈춰 둔 검증용 기체는 앞질러 겨누지 않게

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);

            // 스폰 링은 수면 높이다. 이미 순항 고도로 날아오던 것처럼 올린다.
            var p = transform.position;
            p.y = cruiseAltitude;
            transform.position = p;
            _velocity = transform.forward * (def != null ? def.MoveSpeed : 0f);
            _host = null; _launching = false;   // 풀에서 다시 나오면 모함 정보를 비운다
        }

        // ------------------------------------------------------------ 모함 발진(2026-11-26)
        // 항공전함 엘리베이터 드론이 함수(함교) 쪽으로 세워져 6m 고도로 곧장 플레이어에게 가면서 함교를 뚫고 지나갔다.
        // 이제 활주로 방향으로 갑판을 달려 모함 끝을 벗어난 뒤 돌고, 그 뒤에도 모함 위를 지날 때는 함교 · 포탑보다 높게 난다.

        /// <summary>검증용: 켜면 예전처럼(모함 무시) 난다.</summary>
        public static bool DevLaunchOff;
        /// <summary>모함 위를 지날 때 가장 높은 곳보다 이만큼 위로.</summary>
        public const float HostClearMargin = 1.2f;
        private const float HostSideMargin = 1f;

        private HostClearance _host;
        private bool _launching;
        private Vector3 _launchDirLocal;
        private float _launchStart, _launchUntil;

        /// <summary>검증용: 활주 중인가.</summary>
        public bool DevLaunching => _launching;
        public HostClearance DevHost => _host;

        /// <summary>
        /// 모함 갑판에서 발진: worldDir(수평) 쪽으로 갑판을 달려 모함 끝을 벗어날 때까지 곧게 오른다(최대 maxRun초).
        /// 그 뒤로도 모함이 살아 있는 동안은 모함 위를 지날 때 높이표보다 높게 난다.
        /// </summary>
        public void LaunchFrom(HostClearance host, Vector3 worldDir, float maxRun = 3f)
        {
            if (host == null || !host.Valid || DevLaunchOff) return;
            _host = host;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 1e-4f) worldDir = -host.Host.forward;
            _launchDirLocal = host.Host.InverseTransformDirection(worldDir.normalized);
            _launchDirLocal.y = 0f; _launchDirLocal.Normalize();
            _launching = true;
            _launchStart = Time.time;
            _launchUntil = Time.time + maxRun;
            transform.rotation = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
            _velocity = transform.forward * (Definition != null ? Definition.MoveSpeed : 0f) * 0.5f;
        }

        /// <summary>활주 중이면 활주로를 따라 날고 참을 돌려준다(파생 기체는 이번 프레임 할 일을 건너뛴다).</summary>
        protected bool FlyLaunchRun(float speed, float turnRate, float dt)
        {
            if (!_launching) return false;
            if (_host == null || !_host.Valid || Time.time > _launchUntil || PastHost()) { _launching = false; return false; }
            Vector3 d = _host.Host.TransformDirection(_launchDirLocal); d.y = 0f; d.Normalize();   // 모함이 돌면 활주로도 같이 돈다
            Vector3 p = transform.position;
            float need = _host.TopAlong(p + d * 2.5f, d, 6f, HostSideMargin) + HostClearMargin;   // 앞쪽만 본다(뒤의 함교는 이미 지나 있다)
            float y = Mathf.Max(need, p.y + 0.6f);
            float accel = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01((Time.time - _launchStart) / 0.8f));   // 갑판에서 속도를 붙인다
            Fly(new Vector3(p.x + d.x * 6f, y, p.z + d.z * 6f), speed * accel, turnRate, false, dt);
            return true;
        }

        private bool PastHost()
        {
            var l = _host.Host.InverseTransformPoint(transform.position);
            float cz = (_host.MinZ + _host.MaxZ) * 0.5f, cx = (_host.MinX + _host.MaxX) * 0.5f;
            float along = (l.x - cx) * _launchDirLocal.x + (l.z - cz) * _launchDirLocal.z;
            float extent = Mathf.Abs(_launchDirLocal.x) * (_host.MaxX - _host.MinX) * 0.5f + Mathf.Abs(_launchDirLocal.z) * (_host.MaxZ - _host.MinZ) * 0.5f;
            return along > extent + 1.5f;
        }

        /// <summary>
        /// target으로 가는 길(최대 lookAhead m, target 너머는 안 봄)이 모함 위를 지나면 그 높이보다 위로 가는 가까운 점을 돌려준다.
        /// 모함이 없거나 길이 모함 위를 안 지나면 target 그대로.
        /// </summary>
        protected Vector3 ClearOfHost(Vector3 target, float lookAhead = 12f)
        {
            if (_host == null) return target;
            if (!_host.Valid) { _host = null; return target; }
            Vector3 p = transform.position;
            Vector3 flat = target - p; flat.y = 0f;
            float dist = flat.magnitude;
            Vector3 dir = dist > 0.01f ? flat / dist : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 fwd = transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : dir;
            float reach = Mathf.Min(lookAhead, dist);
            // 가려는 쪽과 지금 기수 쪽 둘 다 — 도는 동안 기수 앞에 함교가 있을 수 있다
            float top = Mathf.Max(_host.TopAlong(p, dir, reach, HostSideMargin), _host.TopAlong(p, fwd, Mathf.Min(6f, reach + 3f), HostSideMargin));
            if (top == float.MinValue) return target;
            float need = top + HostClearMargin;
            if (need <= target.y) return target;
            float lead = Mathf.Min(6f, Mathf.Max(dist, 0.5f));
            return new Vector3(p.x + dir.x * lead, need, p.z + dir.z * lead);
        }

        /// <summary>
        /// 목표 지점을 향해 기수를 돌리며 전진한다. terminal이면 남은 거리 안에 돌아설 수 있을 만큼 선회를 올려
        /// 표적 주위를 맴돌지 않게 한다(미사일과 같은 규칙).
        /// </summary>
        /// <summary>Fly 없이 직접 움직이는 기체(WWII 뇌격기 진입)가 요격 무기의 예측용 속도를 알린다.</summary>
        protected void SetVelocity(Vector3 velocity) => _velocity = velocity;

        protected void Fly(Vector3 point, float speed, float turnRate, bool terminal, float dt)
        {
            Vector3 to = point - transform.position;
            float dist = to.magnitude;

            if (dist > 0.001f)
            {
                float rate = turnRate;
                if (terminal)
                {
                    float needed = Vector3.Angle(transform.forward, to) * speed / Mathf.Max(dist, 0.5f) * 2f;
                    rate = Mathf.Min(Mathf.Max(rate, needed), 1080f);
                }
                var want = Quaternion.LookRotation(to / dist, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * dt);
            }

            _velocity = transform.forward * speed;
            transform.position += _velocity * dt;
        }
    }
}
