using UnityEngine;
using Game.Core;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 강습 위그선(4스테이지, 모티브 A-90 오를료노크). 수면 위를 날아와 기함 근처에 내려앉고, 기수를 옆으로 열어
    /// 자폭 보트를 내린 뒤 다시 떠서 멀어진다(반복).
    ///   접근(Approach) → 착수(Land: 감속 · 하강) → 하역(Unload: 기수 문 열림 → 보트 n척) → 이수(TakeOff) → 이탈(Retreat)
    /// 날 때는 수면 비행체, 내려앉은 동안은 수상 표적이라 모든 대함 무기가 통한다 — 그때가 공격 기회다.
    /// </summary>
    public class AssaultWig : WigEnemy
    {
        private enum State { Approach, Land, Unload, TakeOff, Retreat }

        [Header("Assault")]
        [SerializeField] private Game.Data.EnemyDefinition boatDefinition;
        [SerializeField, Min(1)] private int boats = 2;
        [SerializeField] private float boatInterval = 0.9f;
        [Tooltip("기함에서 이 거리에 내려앉는다")]
        [SerializeField] private float landDistance = 26f;
        [SerializeField] private float landSeconds = 1.6f;
        [SerializeField] private float doorSeconds = 0.8f;
        [SerializeField] private float holdAfterUnload = 1.2f;
        [SerializeField] private float retreatDistance = 70f;
        [SerializeField] private float doorOpenDegrees = 70f;

        [Header("Model")]
        [SerializeField] private Transform noseDoor;
        [SerializeField] private Transform boatExit;
        [SerializeField] private Transform propeller;
        [SerializeField] private float propellerRpm = 700f;

        private State _state;
        private float _timer, _speed, _door;
        private int _boatsLeft;
        private Quaternion _doorRest;
        private bool _doorRestCaptured;
        private Vector3 _retreatDir;

        /// <summary>검증용: 내린 보트 수 · 내려앉은 횟수 · 기수 문 열림(0~1).</summary>
        public int DevBoatsLaunched { get; private set; }
        public int DevLandings { get; private set; }
        public float Door01 => _door;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _state = State.Approach;
            _speed = def != null ? def.MoveSpeed : 0f;
            _door = 0f;
            DevBoatsLaunched = DevLandings = 0;
            if (noseDoor != null)
            {
                if (!_doorRestCaptured) { _doorRest = noseDoor.localRotation; _doorRestCaptured = true; }
                noseDoor.localRotation = _doorRest;
            }
        }

        protected override void UpdateBehaviour(float dt)
        {
            float cruise = Definition.MoveSpeed, turn = Definition.TurnRateDegPerSec;
            Vector3 toPlayer = Player.position - transform.position; toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (propeller != null) propeller.Rotate(transform.forward, propellerRpm * 6f * dt * Mathf.Lerp(0.25f, 1f, _speed / Mathf.Max(1f, cruise)), Space.World);

            switch (_state)
            {
                case State.Approach:
                    _speed = Mathf.MoveTowards(_speed, cruise, cruise * dt);
                    Glide(toPlayer, _speed, cruiseAltitude, 3f, turn, dt);
                    if (dist <= landDistance + _speed * landSeconds * 0.5f) { _state = State.Land; _timer = landSeconds; }
                    break;

                case State.Land:
                    _timer -= dt;
                    _speed = Mathf.MoveTowards(_speed, 0f, cruise / Mathf.Max(0.2f, landSeconds) * dt);
                    Glide(toPlayer, _speed, WaterY, (cruiseAltitude - WaterY) / Mathf.Max(0.2f, landSeconds), turn * 0.5f, dt);
                    if (_timer <= 0f && transform.position.y <= WaterY + 0.05f)
                    {
                        SetLanded(true);
                        DevLandings++;
                        _state = State.Unload;
                        _boatsLeft = boats;
                        _timer = doorSeconds;
                    }
                    break;

                case State.Unload:
                    Glide(transform.forward, 0f, WaterY, 5f, 0f, dt);
                    _door = Mathf.MoveTowards(_door, 1f, dt / Mathf.Max(0.1f, doorSeconds));
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        if (_boatsLeft > 0) { LaunchBoat(); _boatsLeft--; _timer = _boatsLeft > 0 ? boatInterval : holdAfterUnload; }
                        else { _state = State.TakeOff; _timer = landSeconds; }
                    }
                    break;

                case State.TakeOff:
                    _door = Mathf.MoveTowards(_door, 0f, dt / Mathf.Max(0.1f, doorSeconds));
                    if (_door > 0.05f) { Glide(transform.forward, 0f, WaterY, 5f, 0f, dt); break; }
                    _speed = Mathf.MoveTowards(_speed, cruise, cruise / Mathf.Max(0.2f, landSeconds) * dt);
                    // 활주: 속력이 절반을 넘으면 뜬다
                    bool lift = _speed >= cruise * 0.5f;
                    if (lift && Landed) SetLanded(false);
                    Glide(transform.forward, _speed, lift ? cruiseAltitude : WaterY, 2.5f, 0f, dt);
                    if (_speed >= cruise * 0.95f && transform.position.y >= cruiseAltitude - 0.2f)
                    {
                        _state = State.Retreat;
                        _retreatDir = transform.forward; _retreatDir.y = 0f;
                    }
                    break;

                case State.Retreat:
                    Glide(_retreatDir, _speed, cruiseAltitude, 3f, turn, dt);
                    if (dist >= retreatDistance) _state = State.Approach;
                    break;
            }
            ApplyDoor();
        }

        private void LaunchBoat()
        {
            if (boatDefinition == null || EnemySpawner.Instance == null) return;
            Vector3 at = boatExit != null ? boatExit.position : transform.position + transform.forward * 9f;
            at.y = 0f;
            Vector3 to = Player.position - at; to.y = 0f;
            var rot = Quaternion.LookRotation(to.sqrMagnitude > 0.01f ? to.normalized : transform.forward, Vector3.up);
            if (EnemySpawner.Instance.SpawnAt(boatDefinition, at, rot) != null) DevBoatsLaunched++;
            AudioManager.Play(Game.Data.SfxId.DepthDrop, at, 0.8f, 0.8f);
        }

        /// <summary>기수 문: 기체 위축 둘레로 +각도면 우현으로 열린다(Codex 납품 메모). 기준 자세에서 다시 돌린다.</summary>
        private void ApplyDoor()
        {
            if (noseDoor == null) return;
            noseDoor.localRotation = _doorRest;
            if (_door > 0.001f) noseDoor.Rotate(transform.up, doorOpenDegrees * _door, Space.World);
        }
    }
}
