using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.View;
using Random = UnityEngine.Random;

namespace Game.TaskForce
{
    /// <summary>호위함 역할. None = 역할을 정하기 전의 고속정(함포만).</summary>
    public enum EscortRole { None, AirDefense, AntiSubmarine, ElectronicWarfare, SurfaceStrike }

    /// <summary>
    /// 기함 편대(카드로 키운다, 2026-10-02 편대 개편).
    ///   - 편대 슬롯 1~4(2026-10-03): 호위함마다 편대 슬롯 번호가 있다(이름의 번호 = 슬롯 번호, "방공 호위함 2" = 2번 슬롯).
    ///     진형 자리는 슬롯마다 고정이다(함대원형진 1번 앞 · 2번 우현 · 3번 뒤 · 4번 좌현, 단종진은 번호 순서로 항적 위).
    ///   - 진형: 함대원형진 · 단종진 · 자율(<see cref="FleetFormations.All"/>). G 키 또는 진형 선택판(FormationSelectorUI) 클릭으로 바꾼다.
    ///     자율이면 호위함이 화면 안에서 역할에 맞는 위협 쪽으로 스스로 움직인다(자율 진형 절).
    ///   - 편대 배치 카드: 역할 없는 고속정 1척을 플레이어가 고른 빈 슬롯에 합류(최대 <see cref="MaxEscorts"/>척).
    ///   - 편대 강화 카드: 플레이어가 고른 슬롯의 호위함 — 역할 없는 고속정이면 역할을 고르고(방공·대잠·전자전·미사일, 그 역할의 T0),
    ///     역할이 있으면 개량(T1~T3).
    ///   - 지원 스킬·출항 편성(CP)은 없다. 역할 능력은 호위함이 스스로 쓴다(EscortDefense):
    ///     모든 함 함포 · 방공 함대공 요격 · 대잠 자동 대잠 타격 · 전자전 근접 교란 · 미사일 주기적 대함 타격.
    ///
    /// 호위함은 아군(Player) 진영 수상 표적이다: PlayerShip 레이어 콜라이더로 적 사격·포탄에 맞는다.
    ///   - 위치: 진형 슬롯(기본 함대원형진 — 기함 둘레 약 16~20m)에서 함께 싸운다.
    ///   - 퇴각 후 복귀: 선체가 0이면 가라앉지 않고 "전투 불능"으로 뒤로 이탈한다(표적 해제·판정 끔·능력 정지).
    ///     <see cref="RecoverSeconds"/>초 뒤 선체 절반으로 복귀하고, 레벨 정비 때는 바로 다 수리된다.
    ///   - 적 표적 규칙: 우선도 0.4(기함보다 2.5배 가까워야 노림), 한 척을 동시에 노리는 적은 최대 2척,
    ///     대함미사일·어뢰·자폭 공격을 하는 적은 기함만 노린다(EnemyController.UpdateTarget, TargetsEscorts).
    /// 외형은 Codex 모델 프리팹(Resources/TaskForce/Escorts/ESC_역할_T단계, 2026-10-02 Redesign)을 쓴다. 역할 없는 고속정은 ESC_PB_T0이 있으면 그것을,
    /// 없으면 ESC_STK_T0의 역할색을 무채색으로 바꿔 쓴다. 포탑·레이더는 EscortTurrets가 움직인다. 모델이 없으면 회색박스로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TaskForceEscortFormation : MonoBehaviour
    {
        /// <summary>편대 최대 척수.</summary>
        public const int MaxEscorts = 4;
        /// <summary>역할을 정한 뒤의 개량 단계(T0~T3).</summary>
        public const int MaxUpgradeLevel = 3;

        /// <summary>기본형 선체 내구도와 개량 단계당 증가량. 기함 선체는 100.</summary>
        public const float BaseHullHp = 60f, HullHpPerLevel = 20f;
        public static float MaxHullFor(int level) => BaseHullHp + HullHpPerLevel * Mathf.Clamp(level, 0, MaxUpgradeLevel);

        /// <summary>전투 불능 뒤 복귀까지 걸리는 시간(초)과 복귀 때 선체 비율.</summary>
        public const float RecoverSeconds = 45f, RecoverHull = 0.5f;
        /// <summary>적이 호위함을 노리는 우선도(기함 1)와 한 척을 동시에 노릴 수 있는 적 수.</summary>
        public const float EscortTargetPriority = 0.4f;
        public const int EscortMaxAttackers = 2;

        /// <summary>편대 강화 카드로 고를 수 있는 역할.</summary>
        public static readonly EscortRole[] Roles =
            { EscortRole.AirDefense, EscortRole.AntiSubmarine, EscortRole.ElectronicWarfare, EscortRole.SurfaceStrike };

        /// <summary>정비 카드·편대 패널이 보는 한 척의 상태.</summary>
        public readonly struct EscortInfo
        {
            public readonly int Index;
            public readonly EscortRole Role;
            public readonly int Tier;
            public readonly string Name;
            public readonly float Hull01;
            public readonly bool Disabled;
            public readonly float RecoverRemaining;
            /// <summary>편대 슬롯(0~3, 화면에는 1~4). 없는 함이면 -1.</summary>
            public readonly int RosterSlot;

            public EscortInfo(int index, EscortRole role, int tier, string name, float hull, bool disabled, float recover, int rosterSlot = -1)
            {
                Index = index; Role = role; Tier = tier; Name = name; Hull01 = hull; Disabled = disabled; RecoverRemaining = recover;
                RosterSlot = rosterSlot;
            }
        }

        /// <summary>합류·역할·개량·전투 불능·복귀가 바뀌었다(편대 패널 갱신용).</summary>
        public event Action Changed;

        private readonly List<EscortVessel> _escorts = new(MaxEscorts);

        // ------------------------------------------------------------ 진형(2026-10-03)
        /// <summary>진형 기준 침로가 기함 침로를 따라가는 시간 상수(초). 기함이 급선회해도 슬롯이 한꺼번에 휙 돌지 않게 늦춘다.</summary>
        public const float FrameLag = 0.5f;
        /// <summary>항적 기록 간격(m)과 최대 길이(m) — 종렬진이 따라간다.</summary>
        private const float TrailStep = 0.5f, TrailMax = 140f;

        private FleetFormation _formation = FleetFormation.Circular;
        private float _frameYaw;
        private bool _frameReady;
        private readonly List<Vector3> _trail = new();   // [0] = 가장 최근(기함 쪽)
        private float _fore = 7f, _aft = -5f, _halfBeam = 1f;
        private float _hullCheckAt;

        /// <summary>지금 진형.</summary>
        public FleetFormation Formation => _formation;
        /// <summary>진형 기준 침로(기함 침로를 늦게 따라감).</summary>
        public Quaternion FrameRotation => Quaternion.Euler(0f, _frameYaw, 0f);
        /// <summary>검증용: 기함 크기(선수·선미 끝 z, 반폭).</summary>
        public Vector3 FlagshipExtent => new(_fore, _aft, _halfBeam);

        /// <summary>진형을 바꾼다. 호위함은 새 슬롯으로 (배처럼) 돌아 들어간다.</summary>
        public void SetFormation(FleetFormation formation)
        {
            if (_formation == formation) return;
            _formation = formation;
            if (formation == FleetFormation.Autonomous)
            {
                // 지금 자리에서 시작해 스스로 판단한다(목표가 한꺼번에 튀지 않게)
                ResetAutonomous();
                foreach (var e in _escorts) if (e != null) InitAutonomous(e.RosterSlot, e);
            }
            Reflow(false);
            if (_escorts.Count > 0)
            {
                GameEvents.RaiseBossPhaseChanged($"진형 변경: {FleetFormations.Name(formation)}");
                CombatLog.Add("편대", $"진형 {FleetFormations.Name(formation)} — {FleetFormations.Summary(formation)}");
            }
            Changed?.Invoke();
        }

        /// <summary>다음 진형으로(조작키 — 기본 G).</summary>
        public void CycleFormation()
        {
            int i = System.Array.IndexOf(FleetFormations.All, _formation);
            SetFormation(FleetFormations.All[(i + 1) % FleetFormations.All.Length]);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateFlagshipExtent();

            // 진형 기준 침로: 기함 침로를 지수적으로 늦게 따라간다(정상 선회 중 지연 ≈ 선회율 × FrameLag)
            float yaw = transform.eulerAngles.y;
            if (!_frameReady) { _frameYaw = yaw; _frameReady = true; }
            else if (dt > 0f) _frameYaw += Mathf.DeltaAngle(_frameYaw, yaw) * (1f - Mathf.Exp(-dt / FrameLag));

            // 항적
            Vector3 p = transform.position;
            if (_trail.Count == 0 || (p - _trail[0]).sqrMagnitude >= TrailStep * TrailStep)
            {
                if (_trail.Count > 0 && (p - _trail[0]).sqrMagnitude > 25f * 25f) _trail.Clear();   // 순간 이동
                _trail.Insert(0, p);
                if (_trail.Count > TrailMax / TrailStep) _trail.RemoveAt(_trail.Count - 1);
            }

            UpdateAutonomous(dt);

            var gm = GameManager.Instance;
            if (gm != null && gm.State == GameState.Playing && _escorts.Count > 0 && GameSettings.Pressed(NavalControl.Formation))
                CycleFormation();
        }

        /// <summary>기함 크기(블록을 붙이면 길어지고 넓어진다). 1초마다 다시 잰다.</summary>
        private void UpdateFlagshipExtent()
        {
            if (Time.unscaledTime < _hullCheckAt) return;
            _hullCheckAt = Time.unscaledTime + 1f;
            var ship = GetComponent<Game.Ship.ShipController>();
            var grid = ship != null ? ship.Grid : GetComponentInChildren<Game.Ship.ShipGrid>();
            if (grid == null || grid.OccupiedCells.Count == 0) return;
            grid.GetExtent(out var min, out var max);
            float half = grid.CellSize * 0.5f;
            float Z(int x) => transform.InverseTransformPoint(grid.CoordToWorld(new Game.Ship.GridCoord(x, 0))).z;
            float X(int z) => transform.InverseTransformPoint(grid.CoordToWorld(new Game.Ship.GridCoord(0, z))).x;
            _fore = Z(max.X) + half;
            _aft = Z(min.X) - half;
            _halfBeam = Mathf.Max(Mathf.Abs(X(min.Z)), Mathf.Abs(X(max.Z))) + half;
        }

        /// <summary>
        /// index번(= 편대 슬롯 번호) 슬롯에 있어야 할 곳과 그때의 침로. 단종진은 기함 항적 위, 자율은 스스로 정한 곳,
        /// 나머지는 늦게 도는 진형 기준틀의 슬롯.
        /// </summary>
        private Vector3 SlotPosition(int index, out Vector3 forward)
        {
            if (_formation == FleetFormation.Autonomous)
                return AutonomousPosition(Mathf.Clamp(index, 0, MaxEscorts - 1), out forward);
            if (_formation == FleetFormation.Column)
                return TrailPoint(FleetFormations.TrailDistance(index, _aft), out forward);
            var o = FleetFormations.Slot(_formation, index, _escorts.Count, _fore, _aft, _halfBeam);
            var rot = FrameRotation;
            forward = rot * Vector3.forward;
            var pos = transform.position + rot * new Vector3(o.x, 0f, o.y);
            pos.y = transform.position.y;
            return pos;
        }

        /// <summary>기함에서 항적을 따라 distance만큼 뒤의 점(항적이 모자라면 기함 정후방으로 잇는다)과 그 점의 진행 방향.</summary>
        private Vector3 TrailPoint(float distance, out Vector3 forward)
        {
            Vector3 prev = transform.position;
            forward = transform.forward; forward.y = 0f; forward.Normalize();
            float left = distance;
            for (int i = 0; i < _trail.Count; i++)
            {
                Vector3 next = _trail[i];
                float seg = Vector3.Distance(prev, next);
                if (seg > 1e-4f)
                {
                    if (seg >= left)
                    {
                        Vector3 dir = (prev - next) / seg;   // 앞쪽(기함 쪽)을 향함
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 1e-6f) forward = dir.normalized;
                        return prev - (prev - next) * (left / seg);
                    }
                    left -= seg;
                    Vector3 d = prev - next; d.y = 0f;
                    if (d.sqrMagnitude > 1e-6f) forward = d.normalized;
                }
                prev = next;
            }
            return prev - forward * left;
        }

        public int EscortCount => _escorts.Count;
        public bool CanDeploy => _escorts.Count < MaxEscorts;

        /// <summary>지금 전투 불능으로 이탈해 있는 호위함 수.</summary>
        public int DisabledCount
        {
            get
            {
                int n = 0;
                foreach (var e in _escorts) if (e != null && e.Disabled) n++;
                return n;
            }
        }

        public EscortInfo GetInfo(int index)
        {
            var v = Vessel(index);
            return v == null ? default
                : new EscortInfo(index, v.Role, v.Tier, v.DisplayName, v.Hull01, v.Disabled, v.RecoverRemaining, v.RosterSlot);
        }

        /// <summary>검증·디버그용: index번 호위함.</summary>
        public Component GetEscort(int index) => Vessel(index);

        /// <summary>검증·디버그용: 그 역할의 첫 호위함(없으면 null).</summary>
        public Component GetEscort(EscortRole role)
        {
            foreach (var e in _escorts) if (e != null && e.Role == role) return e;
            return null;
        }

        private EscortVessel Vessel(int index) => index >= 0 && index < _escorts.Count ? _escorts[index] : null;

        public bool IsEscortDisabled(int index) => Vessel(index)?.Disabled ?? false;
        public float GetRecoverRemaining(int index) => Vessel(index)?.RecoverRemaining ?? 0f;
        /// <summary>그 호위함의 선체 비율(0~1). 없으면 -1.</summary>
        public float GetHull01(int index) => Vessel(index)?.Hull01 ?? -1f;

        // ------------------------------------------------------------ 카드

        /// <summary>편대 배치(슬롯을 고르지 않을 때): 가장 앞의 빈 슬롯에 합류한다. 반환은 새 함의 번호(가득 차면 -1).</summary>
        public int Deploy() => Deploy(FirstEmptyRosterSlot());

        /// <summary>
        /// 편대 배치 카드: 역할 없는 고속정 1척이 rosterSlot(0~3) 슬롯에 합류한다.
        /// 반환은 새 함의 번호(<see cref="GetInfo"/> 등에 쓰는 편대 안 순서). 슬롯이 차 있거나 범위 밖이면 -1.
        /// </summary>
        public int Deploy(int rosterSlot)
        {
            if (!CanDeploy || rosterSlot < 0 || rosterSlot >= MaxEscorts || !IsRosterSlotEmpty(rosterSlot)) return -1;
            var vessel = EscortVessel.Create(this, transform, rosterSlot + 1);
            _escorts.Add(vessel);
            Reflow(true);
            CombatLog.Add("편대", $"{vessel.DisplayName} {rosterSlot + 1}번 슬롯 합류 ({_escorts.Count}/{MaxEscorts}척)");
            Changed?.Invoke();
            return _escorts.Count - 1;
        }

        /// <summary>그 편대 슬롯(0~3)에 있는 호위함의 번호. 비어 있으면 -1.</summary>
        public int IndexOfRosterSlot(int rosterSlot)
        {
            for (int i = 0; i < _escorts.Count; i++)
                if (_escorts[i] != null && _escorts[i].RosterSlot == rosterSlot) return i;
            return -1;
        }

        public bool IsRosterSlotEmpty(int rosterSlot) => IndexOfRosterSlot(rosterSlot) < 0;

        /// <summary>편대 슬롯 번호 순서로 rank번째(0부터) 호위함의 번호(패널을 슬롯 순서로 보여 줄 때). 없으면 -1.</summary>
        public int IndexByRosterOrder(int rank)
        {
            for (int s = 0, seen = 0; s < MaxEscorts; s++)
            {
                int i = IndexOfRosterSlot(s);
                if (i < 0) continue;
                if (seen++ == rank) return i;
            }
            return -1;
        }

        /// <summary>가장 앞의 빈 편대 슬롯(0~3). 가득 차면 -1.</summary>
        public int FirstEmptyRosterSlot()
        {
            for (int s = 0; s < MaxEscorts; s++) if (IsRosterSlotEmpty(s)) return s;
            return -1;
        }

        /// <summary>강화할 수 있는가: 역할이 없거나(역할 지정) 최대 단계가 아니면(개량).</summary>
        public bool CanUpgrade(int index)
        {
            var v = Vessel(index);
            return v != null && (v.Role == EscortRole.None || v.Tier < MaxUpgradeLevel);
        }

        /// <summary>
        /// 편대 강화 카드의 대상: 역할 없는 고속정을 먼저(역할 지정), 없으면 단계가 가장 낮은 호위함(같으면 무작위). 없으면 -1.
        /// </summary>
        public int PickUpgradeTarget()
        {
            int best = -1, bestScore = int.MaxValue, ties = 0;
            for (int i = 0; i < _escorts.Count; i++)
            {
                if (!CanUpgrade(i)) continue;
                var v = _escorts[i];
                int score = v.Role == EscortRole.None ? -1 : v.Tier;
                if (score < bestScore) { bestScore = score; best = i; ties = 1; }
                else if (score == bestScore && Random.Range(0, ++ties) == 0) best = i;
            }
            return best;
        }

        /// <summary>편대 강화(역할 지정): 역할 없는 고속정에 역할을 준다(그 역할의 T0).</summary>
        public bool AssignRole(int index, EscortRole role)
        {
            var v = Vessel(index);
            if (v == null || v.Role != EscortRole.None || role == EscortRole.None) return false;
            string before = v.DisplayName;
            v.SetRole(role);
            CombatLog.Add("편대", $"{before} → {v.DisplayName} 역할 지정 · {RoleSummary(role)}");
            Changed?.Invoke();
            return true;
        }

        /// <summary>편대 강화(개량): 역할이 있는 호위함의 단계를 하나 올린다.</summary>
        public bool Upgrade(int index)
        {
            var v = Vessel(index);
            if (v == null || v.Role == EscortRole.None || v.Tier >= MaxUpgradeLevel) return false;
            v.SetTier(v.Tier + 1);
            CombatLog.Add("편대", $"{v.DisplayName} 개량 {v.Tier}단계 · {TierDescription(v.Role, v.Tier)}");
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ 수명

        private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
        private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;
        private void OnDestroy() => ClearAll();

        /// <summary>레벨 정비에 들어가면 모두 수리한다(전투 불능이던 호위함도 곧바로 복귀).</summary>
        private void OnStateChanged(GameState state)
        {
            if (state == GameState.Refit) RepairAll();
        }

        /// <summary>모든 호위함 선체를 다 채우고, 전투 불능이던 호위함은 복귀시킨다.</summary>
        public void RepairAll()
        {
            _escorts.RemoveAll(e => e == null);
            foreach (var e in _escorts) e.Recover(1f);
            Changed?.Invoke();
        }

        /// <summary>검증·초기화용: 편대를 모두 없앤다.</summary>
        public void ClearAll()
        {
            foreach (var e in _escorts) if (e != null) Destroy(e.gameObject);
            _escorts.Clear();
            ResetAutonomous();
            Changed?.Invoke();
        }

        private void OnVesselDisabled(EscortVessel vessel)
        {
            GameEvents.RaiseBossPhaseChanged($"{vessel.DisplayName} 전투 불능 · {RecoverSeconds:0}초 뒤 복귀");
            CombatLog.Add("편대", $"{vessel.DisplayName} 전투 불능 — 후방으로 이탈, {RecoverSeconds:0}초 뒤 복귀");
            Changed?.Invoke();
        }

        private void OnVesselRecovered(EscortVessel vessel)
        {
            CombatLog.Add("편대", $"{vessel.DisplayName} 복귀 · 선체 {vessel.Hull01 * 100f:0}%");
            Changed?.Invoke();
        }

        /// <summary>
        /// 진형 자리를 다시 준다. 자리는 편대 슬롯마다 고정이다(슬롯 번호 = 진형 자리, 몇 척이 있든 같은 자리).
        /// 새로 합류한 함(목록 끝)은 그 자리에 바로 놓고, 진형을 바꾸면 모두 새 자리로 (배처럼) 돌아 들어간다.
        /// </summary>
        private void Reflow(bool snapNew)
        {
            int n = _escorts.Count;
            for (int i = 0; i < n; i++) _escorts[i].SetSlot(_escorts[i].RosterSlot, snapNew && i == n - 1);
        }

        // ------------------------------------------------------------ 자율 진형(2026-10-03)
        // 호위함이 화면 안에서 스스로 자리를 정한다(0.5초마다 다시 판단). 목표는 기함 위치 기준 오프셋(월드 축)으로 두고
        // 초당 AutoMoveSpeed m씩만 옮겨, 판단이 바뀌어도 목표가 튀지 않는다(배는 그 목표를 평소처럼 조함해 따라간다).
        //   방공·전자전: 적 미사일(없으면 항공기) 쪽, 기함과 위협 사이 13m — 요격·교란 위치
        //   대잠: 잠수함(잠항 중이어도) 쪽으로 나가 9m 앞까지
        //   그 밖(고속정·미사일·위 역할에 위협이 없을 때): 가까운 드러난 적 수상함과 기함 사이(함포 13m, 미사일 26m 거리)
        //   위협이 없으면: 기함 둘레를 천천히 돈다(초계)
        // 어느 경우든 기함에서 AutoLeash m 안, 화면 안(가장자리 여백), 기함 현측 + 9m 밖으로 묶는다.

        /// <summary>자율: 기함에서 이 거리(m) 안에서만 움직인다.</summary>
        public const float AutoLeash = 34f;
        private const float AutoThinkInterval = 0.5f, AutoMoveSpeed = 11f, AutoPatrolTurn = 9f;
        private readonly Vector3[] _autoOffset = new Vector3[MaxEscorts];
        private readonly Vector3[] _autoGoal = new Vector3[MaxEscorts];
        private readonly bool[] _autoReady = new bool[MaxEscorts];
        private readonly string[] _autoTask = new string[MaxEscorts];
        private float _autoThinkAt;

        /// <summary>자율 진형에서 index번 호위함이 지금 하는 일(검증·표시용). 자율이 아니면 빈 문자열.</summary>
        public string AutonomousTask(int index)
        {
            var v = Vessel(index);
            return _formation == FleetFormation.Autonomous && v != null ? _autoTask[v.RosterSlot] ?? "" : "";
        }

        private void ResetAutonomous()
        {
            for (int s = 0; s < MaxEscorts; s++) { _autoReady[s] = false; _autoTask[s] = null; }
            _autoThinkAt = 0f;
        }

        /// <summary>자율 목표를 처음 정한다: 배가 있으면 지금 자리에서(진형을 바꿔도 튀지 않게), 없으면 초계 자리.</summary>
        private void InitAutonomous(int slot, EscortVessel vessel)
        {
            Vector3 o = vessel != null ? vessel.transform.position - transform.position : PatrolOffset(slot);
            o.y = 0f;
            _autoOffset[slot] = _autoGoal[slot] = o;
            _autoReady[slot] = true;
            _autoTask[slot] = "초계";
        }

        private Vector3 AutonomousPosition(int slot, out Vector3 forward)
        {
            if (!_autoReady[slot]) InitAutonomous(slot, null);
            forward = FrameRotation * Vector3.forward;
            var p = transform.position + _autoOffset[slot];
            p.y = transform.position.y;
            return p;
        }

        private void UpdateAutonomous(float dt)
        {
            if (_formation != FleetFormation.Autonomous || _escorts.Count == 0) return;
            bool think = Time.time >= _autoThinkAt;
            if (think) _autoThinkAt = Time.time + AutoThinkInterval;
            foreach (var v in _escorts)
            {
                if (v == null) continue;
                int s = v.RosterSlot;
                if (!_autoReady[s]) InitAutonomous(s, v);
                if (think && !v.Disabled) _autoGoal[s] = DecideAutonomousGoal(v, out _autoTask[s]);
                _autoOffset[s] = Vector3.MoveTowards(_autoOffset[s], _autoGoal[s], AutoMoveSpeed * dt);
            }
        }

        /// <summary>초계 자리: 기함 둘레(원형진 반지름)를 슬롯마다 90°씩 벌려 천천히 돈다.</summary>
        private Vector3 PatrolOffset(int slot)
        {
            var o = FleetFormations.Slot(FleetFormation.Circular, 0, MaxEscorts, _fore, _aft, _halfBeam);
            float r = o.y - (_fore + _aft) * 0.5f;
            float angle = _frameYaw + slot * 90f + Time.time * AutoPatrolTurn;
            return Quaternion.Euler(0f, angle, 0f) * Vector3.forward * r;
        }

        private Vector3 DecideAutonomousGoal(EscortVessel v, out string task)
        {
            Vector3 ship = transform.position;
            int slot = v.RosterSlot;
            // 같은 적에게 몰려도 겹치지 않게 슬롯마다 접근 방향을 조금씩 튼다
            Quaternion spread = Quaternion.Euler(0f, (slot - 1.5f) * 22f, 0f);
            Vector3 goal;
            ITargetable threat = null;
            task = "초계";

            if (v.Role is EscortRole.AirDefense or EscortRole.ElectronicWarfare)
            {
                threat = NearestHostile(TargetKind.Missile, 55f, false) ?? NearestHostile(TargetKind.Aircraft, 50f, false);
                if (threat != null)
                {
                    Vector3 d = Flat(threat.Transform.position - ship);
                    task = v.Role == EscortRole.AirDefense ? "요격 위치" : "교란 위치";
                    return Constrain(spread * (d.normalized * 13f));
                }
            }
            if (v.Role == EscortRole.AntiSubmarine)
            {
                threat = NearestHostile(TargetKind.Submarine, 55f, false);
                if (threat != null)
                {
                    Vector3 d = Flat(threat.Transform.position - ship);
                    task = "대잠 추적";
                    return Constrain(d - spread * (d.normalized * 9f));
                }
            }
            threat = NearestHostile(TargetKind.Surface, 45f, true);
            if (threat != null)
            {
                Vector3 d = Flat(threat.Transform.position - ship);
                float standoff = v.Role == EscortRole.SurfaceStrike ? 26f : 13f;
                goal = d - spread * (d.normalized * Mathf.Min(standoff, d.magnitude * 0.6f));
                task = "수상 교전";
                return Constrain(goal);
            }
            return Constrain(PatrolOffset(slot));
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>기함(위치)에서 range m 안의 가장 가까운 적. revealedOnly면 드러난 것만(잠수함은 잠항 중이어도 쫓는다).</summary>
        private ITargetable NearestHostile(TargetKind kind, float range, bool revealedOnly)
        {
            ITargetable best = null;
            float bestSq = range * range;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, kind))
            {
                if (t == null || !t.IsAlive || (revealedOnly && !t.IsRevealed) || t.Transform == null) continue;
                float sq = Flat(t.Transform.position - transform.position).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; best = t; }
            }
            return best;
        }

        /// <summary>기함에서 AutoLeash 안 · 기함 현측 + 9m 밖 · 화면 안(가장자리 여백)으로 묶는다.</summary>
        private Vector3 Constrain(Vector3 offset)
        {
            offset.y = 0f;
            float minR = _halfBeam + 9f;
            float m = offset.magnitude;
            if (m > AutoLeash) offset *= AutoLeash / m;
            else if (m < minR) offset = m > 0.01f ? offset * (minR / m) : Quaternion.Euler(0f, _frameYaw + 90f, 0f) * Vector3.forward * minR;

            var cam = Camera.main;
            if (cam == null || OnScreen(cam, offset)) return offset;
            // 화면 밖이면 기함 쪽으로 당긴다(최소 거리까지)
            float lo = Mathf.Min(1f, minR / Mathf.Max(0.01f, offset.magnitude)), hi = 1f;
            if (!OnScreen(cam, offset * lo)) return offset * lo;
            for (int i = 0; i < 6; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (OnScreen(cam, offset * mid)) lo = mid; else hi = mid;
            }
            return offset * lo;
        }

        private bool OnScreen(Camera cam, Vector3 offset)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position + offset);
            return vp.z > 0f && vp.x > 0.07f && vp.x < 0.93f && vp.y > 0.2f && vp.y < 0.9f;   // 아래쪽은 HUD에 가리지 않게
        }

        /// <summary>검증용: index번 호위함이 맡은 슬롯 번호.</summary>
        public int SlotOf(int index) => Vessel(index)?.Slot ?? -1;

        /// <summary>검증용: index번 호위함이 지금 가야 할 곳(월드).</summary>
        public Vector3 SlotWorld(int index) { var v = Vessel(index); return v != null ? SlotPosition(v.Slot, out _) : Vector3.zero; }

        /// <summary>호위함의 능력 발사 원점(지원 연출용). 없으면 fallback.</summary>
        public Vector3 GetOrigin(int index, Vector3 fallback) => Vessel(index)?.SupportOrigin ?? fallback;


        // ------------------------------------------------------------ 이름·색·설명

        public static string RoleCode(EscortRole role) => role switch
        {
            EscortRole.AirDefense => "CAP",
            EscortRole.AntiSubmarine => "ASW",
            EscortRole.ElectronicWarfare => "EW",
            EscortRole.SurfaceStrike => "STK",
            _ => "PB",
        };

        public static string RoleName(EscortRole role) => role switch
        {
            EscortRole.AirDefense => "방공 호위함",
            EscortRole.AntiSubmarine => "대잠 호위함",
            EscortRole.ElectronicWarfare => "전자전 호위함",
            EscortRole.SurfaceStrike => "미사일 호위함",
            _ => "고속정",
        };

        public static Color RoleColor(EscortRole role) => role switch
        {
            EscortRole.AirDefense => new Color(0.35f, 1f, 0.58f),
            EscortRole.AntiSubmarine => new Color(0.22f, 0.68f, 1f),
            EscortRole.ElectronicWarfare => new Color(0.83f, 0.37f, 1f),
            EscortRole.SurfaceStrike => new Color(1f, 0.52f, 0.16f),
            _ => new Color(0.72f, 0.8f, 0.84f),
        };

        /// <summary>역할 능력 한 줄(카드·로그).</summary>
        public static string RoleSummary(EscortRole role) => role switch
        {
            EscortRole.AirDefense => "기함을 노리는 미사일을 함대공으로 격추",
            EscortRole.AntiSubmarine => "가까운 잠수함을 찾아 자동 대잠 타격",
            EscortRole.ElectronicWarfare => "가까운 미사일의 유도를 교란",
            EscortRole.SurfaceStrike => "가치 높은 수상 표적에 주기적 대함 타격",
            _ => "소형 함포로 가까운 적을 사격",
        };

        /// <summary>역할 능력 짧은 이름(편대 패널 한 줄).</summary>
        public static string RoleShort(EscortRole role) => role switch
        {
            EscortRole.AirDefense => "미사일 요격",
            EscortRole.AntiSubmarine => "자동 대잠 타격",
            EscortRole.ElectronicWarfare => "미사일 교란",
            EscortRole.SurfaceStrike => "대함 타격",
            _ => "함포 · 역할 미정",
        };

        /// <summary>개량 단계의 효과(카드·로그). 단계는 T0~T3.</summary>
        public static string TierDescription(EscortRole role, int tier) => role switch
        {
            EscortRole.AirDefense => $"요격 재장전 {EscortDefense.SamReload(tier):0.0}초 · {HullClass(tier)} 함체",
            EscortRole.AntiSubmarine => $"대잠 피해 {EscortDefense.AswDamage(tier):0} · 재장전 {EscortDefense.AswReload(tier):0}초 · {HullClass(tier)} 함체",
            EscortRole.ElectronicWarfare => $"교란 {EscortDefense.JamDuration(tier):0.0}초 · 재장전 {EscortDefense.JamReload(tier):0}초 · {HullClass(tier)} 함체",
            EscortRole.SurfaceStrike => $"대함 피해 {EscortDefense.StrikeDamage(tier):0} · 재장전 {EscortDefense.StrikeReload(tier):0}초 · {HullClass(tier)} 함체",
            _ => $"함포 · {HullClass(tier)} 함체",
        };

        private static string HullClass(int level) => level switch
        {
            0 => "기본 고속정",
            1 => "개량 고속정",
            2 => "초계함",
            _ => "호위함",
        };

        /// <summary>
        /// 한 척의 자동 호위함. 기함의 자세를 따라 대형 슬롯으로 부드럽게 이동한다.
        /// 아군 진영 수상 표적(ITargetable)이자 피해 대상(IDamageable): 콜라이더는 PlayerShip(6) 레이어라
        /// 적 탄에만 맞고 아군 탄에는 맞지 않는다. 선체가 0이면 전투 불능으로 뒤로 이탈했다가 복귀한다.
        /// </summary>
        private sealed class EscortVessel : MonoBehaviour, ITargetable, IDamageable, ITargetPriority
        {
            /// <summary>전투 불능 때 물러나는 위치(기함 기준)와 그때의 최대 속력.</summary>
            private static readonly Vector2 WithdrawOffset = new(0f, -60f);

            // 조함(2026-10-03): 배처럼 뱃머리 방향으로만 나아가고(옆으로 미끄러지지 않음), 선회율·가속에 한계가 있다.
            /// <summary>최고 속력(m/s) — 기함(약 15.4)보다 빨라 뒤처진 슬롯을 따라잡는다.</summary>
            public const float MaxSpeed = 22f;
            /// <summary>선회율(°/초): 느릴 때 → 빠를 때. 작은 배일수록 잘 돈다(개량 단계마다 조금 무뎌짐).</summary>
            private const float TurnRateSlow = 28f, TurnRateFast = 78f;
            private const float Accel = 7f, Decel = 10f;
            /// <summary>후진 최고 속력(m/s): 바로 뒤 슬롯으로 물러나거나 전투 불능으로 이탈할 때 돌아서지 않고 뒤로 뺀다.</summary>
            private const float ReverseMaxSpeed = 4f;
            /// <summary>슬롯까지 거리 오차를 속도로 바꾸는 이득(1/초).</summary>
            private const float ArriveGain = 0.8f;
            private const float WithdrawSpeed = 9f;

            private TaskForceEscortFormation _owner;
            private Transform _flagship;
            private Transform _visual;
            private Transform _greybox;
            private GameObject _model;
            private string _modelKey = "";   // 붙인 프리팹 이름(ESC_역할_T단계), 회색박스면 빈 문자열
            private Transform _supportSocket;
            private BoxCollider _hullCollider;
            private EscortRole _role;
            private int _number;
            private int _slot;
            private float _heading;          // 침로(°, 월드 y)
            private float _speed;            // 앞으로 나아가는 속력(m/s)
            private float _yawRate, _bank;   // 선회율(°/초) · 선회 중 기울기(°)
            private Vector3 _prevTarget, _targetVel;
            private bool _hasTarget;
            private float _phase;
            private int _level = -1;
            private float _hp, _maxHp;
            private float _nextSmoke;
            private bool _disabled;
            private float _recoverAt;

            // --- ITargetable / IDamageable
            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public CombatFaction Faction => CombatFaction.Player;
            public bool IsAlive => this != null && isActiveAndEnabled && !_disabled && _hp > 0f;
            public bool IsRevealed => true;
            public float TargetPriority => EscortTargetPriority;
            public int MaxAttackers => EscortMaxAttackers;
            public float Hull01 => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;

            public EscortRole Role => _role;
            public int Tier => Mathf.Max(0, _level);
            public bool Disabled => _disabled;
            public float RecoverRemaining => _disabled ? Mathf.Max(0f, _recoverAt - Time.time) : 0f;
            public string DisplayName => $"{RoleName(_role)} {_number}";

            /// <summary>능력 발사 원점: 모델의 SupportOrigin 소켓(없으면 함 중앙 위).</summary>
            public Vector3 SupportOrigin => _supportSocket != null ? _supportSocket.position : transform.position + Vector3.up * 1.6f;

            public int Slot => _slot;
            /// <summary>편대 슬롯(0~3). 이름의 번호가 슬롯 번호다.</summary>
            public int RosterSlot => _number - 1;

            /// <summary>검증용: 지금 침로·속력·선회율.</summary>
            public float Heading => _heading;
            public float Speed => _speed;
            public float YawRate => _yawRate;

            public static EscortVessel Create(TaskForceEscortFormation owner, Transform flagship, int number)
            {
                var root = new GameObject("Escort");
                root.layer = Factions.PlayerShipLayer;
                root.SetActive(false);   // 모두 갖춘 뒤 켜야 OnEnable 등록 때 살아 있는 표적이다
                var vessel = root.AddComponent<EscortVessel>();
                vessel._owner = owner;
                vessel._flagship = flagship;
                vessel._number = number;
                vessel._role = EscortRole.None;
                vessel._phase = Random.value * Mathf.PI * 2f;

                vessel._hullCollider = root.AddComponent<BoxCollider>();
                vessel._visual = new GameObject("Escort visual").transform;
                vessel._visual.SetParent(root.transform, false);
                vessel.SetTier(0);
                vessel._hp = vessel._maxHp;
                vessel.Rename();

                ShipWake.Ensure(root, () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing ? 1f : 0f);
                // 자율 능력(함포 + 역할 능력). 전투 불능이면 쓰지 않는다.
                root.AddComponent<EscortDefense>().Init(() => vessel._role, () => vessel.IsAlive, () => vessel.Tier, () => vessel.SupportOrigin);
                root.SetActive(true);
                return vessel;
            }

            private void Rename() => gameObject.name = $"Escort {RoleCode(_role)} - {DisplayName}";

            private void OnEnable() { if (!_disabled) TargetRegistry.Register(this); }
            private void OnDisable() => ReleaseAsTarget();

            private void ReleaseAsTarget()
            {
                TargetRegistry.Unregister(this);
                TargetAllocator.ReleaseTarget(transform);
            }

            /// <summary>역할 지정: 그 역할의 기본형(T0)이 된다.</summary>
            public void SetRole(EscortRole role)
            {
                _role = role;
                _level = -1;   // 같은 단계라도 모델을 새 역할로 다시 붙인다
                SetTier(0);
                Rename();
            }

            public void SetTier(int level)
            {
                level = Mathf.Clamp(level, 0, MaxUpgradeLevel);
                bool changed = level != _level;
                _level = level;
                // 기본형은 적 고속정(약 2.2×5m)과 비슷하고, 개량할수록 초계함·호위함급으로 성장한다.
                // Codex 모델은 모든 단계가 명목 3×9m라 이 배율 하나로만 키운다(모델과 배율을 이중으로 키우지 않는다).
                float scale = 0.60f + level * 0.16f;
                if (_visual != null) _visual.localScale = Vector3.one * scale;
                if (changed) BuildVisual(level);
                if (_hullCollider != null)
                {
                    _hullCollider.size = new Vector3(3f, 2.2f, 9f) * scale;
                    _hullCollider.center = new Vector3(0f, 0.6f * scale, 0f);
                }
                // 개량은 정비 중에 한다: 늘어난 최대치만큼 선체도 늘린다
                float newMax = MaxHullFor(level);
                _hp = _maxHp > 0f ? Mathf.Min(newMax, _hp + (newMax - _maxHp)) : newMax;
                _maxHp = newMax;
                SetTierVisible("Upgrade tier 1", level >= 1);
                SetTierVisible("Upgrade tier 2", level >= 2);
                SetTierVisible("Upgrade tier 3", level >= 3);
            }

            /// <summary>선체를 hull01만큼(최소) 채운다. 전투 불능이면 복귀시킨다.</summary>
            public void Recover(float hull01)
            {
                _hp = Mathf.Max(_hp, _maxHp * Mathf.Clamp01(hull01));
                if (!_disabled) return;
                _disabled = false;
                if (_hullCollider != null) _hullCollider.enabled = true;
                if (isActiveAndEnabled) TargetRegistry.Register(this);
                if (_owner != null) _owner.OnVesselRecovered(this);
            }

            /// <summary>
            /// 역할·단계별 Codex 모델을 붙인다. 역할 없는 고속정은 ESC_PB_T0(있으면), 없으면 미사일 기본형(참수리 고속정 계열)의
            /// 역할색을 무채색으로 바꿔 쓴다. 모델의 포탑·레이더는 EscortTurrets가 움직인다.
            /// 프리팹이 없으면 예전 회색박스(단계 파츠 포함)로 돌아간다.
            /// </summary>
            private void BuildVisual(int level)
            {
                bool neutral = _role == EscortRole.None;
                GameObject prefab = neutral ? Resources.Load<GameObject>("TaskForce/Escorts/ESC_PB_T0") : null;
                bool tintNeutral = false;
                if (prefab == null)
                {
                    string code = neutral ? "STK" : RoleCode(_role);
                    prefab = Resources.Load<GameObject>($"TaskForce/Escorts/ESC_{code}_T{(neutral ? 0 : level)}");
                    tintNeutral = neutral;
                }
                if (prefab != null)
                {
                    if (_model != null) Destroy(_model);
                    if (_greybox != null) { Destroy(_greybox.gameObject); _greybox = null; }
                    _model = Instantiate(prefab, _visual, false);
                    _model.name = "Model";
                    _modelKey = tintNeutral ? "ESC_PB(STK_T0)" : prefab.name;
                    if (tintNeutral) Neutralize(_model);
                    _supportSocket = FindChild(_model.transform, "SupportOrigin");
                    EscortTurrets.Attach(_model, transform);
                    return;
                }
                if (_greybox != null) Destroy(_greybox.gameObject);
                _modelKey = "";
                _greybox = new GameObject("Greybox").transform;
                _greybox.SetParent(_visual, false);
                BuildModel(_greybox, _role, RoleColor(_role));
                _supportSocket = null;
            }

            /// <summary>역할색 재질(이름이 "Role "로 시작)을 무채색으로 바꾼다 — 역할을 정하기 전의 고속정.</summary>
            private static void Neutralize(GameObject model)
            {
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool hit = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.StartsWith("Role ")) { mats[i] = NeutralMarking; hit = true; }
                    if (hit) r.sharedMaterials = mats;
                }
            }

            private static Material s_neutral;
            private static Material NeutralMarking => s_neutral ??= MakeMaterial("Escort neutral marking", new Color(0.55f, 0.6f, 0.62f), 0.2f, 0.4f);

            public void TakeDamage(in DamageInfo info)
            {
                if (!IsAlive) return;
                _hp -= info.Amount;
                if (_hp > 0f) return;
                _hp = 0f;
                Disable();
            }

            /// <summary>전투 불능: 폭발·연기와 함께 판정·표적에서 빠지고 뒤로 물러난다. 가라앉지 않는다.</summary>
            private void Disable()
            {
                _disabled = true;
                _recoverAt = Time.time + RecoverSeconds;
                if (_hullCollider != null) _hullCollider.enabled = false;
                ReleaseAsTarget();
                Explosions.Spawn(transform.position + Vector3.up * 1f, 1.1f, Explosions.Kind.Impact);
                AudioManager.Play(Game.Data.SfxId.Explosion, transform.position);
                if (_owner != null) _owner.OnVesselDisabled(this);
            }

            private void SetTierVisible(string wanted, bool visible)
            {
                if (_greybox == null) return;
                var tier = FindChild(_greybox, wanted);
                if (tier != null) tier.gameObject.SetActive(visible);
            }

            /// <summary>이름으로 찾는다. Blender가 붙인 ".001" 같은 중복 접미사는 무시한다.</summary>
            private static Transform FindChild(Transform root, string wanted)
            {
                string n = root.name;
                if (n == wanted || (n.Length > wanted.Length && n[wanted.Length] == '.' && n.StartsWith(wanted))) return root;
                foreach (Transform child in root)
                {
                    var found = FindChild(child, wanted);
                    if (found != null) return found;
                }
                return null;
            }

            /// <summary>진형 슬롯 번호를 정한다. snap이면 그 자리에 바로 놓는다(새로 합류).</summary>
            public void SetSlot(int slot, bool snap)
            {
                _slot = slot;
                if (_flagship == null || _owner == null || !snap) return;
                var pos = Target(out var fwd);
                _heading = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                var ship = _flagship.GetComponent<Game.Ship.ShipController>();
                _speed = ship != null ? Mathf.Max(0f, ship.CurrentSpeed) : 0f;
                transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, _heading, 0f));
                _prevTarget = pos;
                _targetVel = Vector3.zero;
                _hasTarget = true;
            }

            /// <summary>가야 할 곳: 진형 슬롯, 전투 불능이면 진형 뒤쪽 이탈 지점.</summary>
            private Vector3 Target(out Vector3 forward)
            {
                if (!_disabled) return _owner.SlotPosition(_slot, out forward);
                var rot = _owner.FrameRotation;
                forward = rot * Vector3.forward;
                var p = _flagship.position + rot * new Vector3(WithdrawOffset.x, 0f, WithdrawOffset.y);
                p.y = _flagship.position.y;
                return p;
            }

            private void Update()
            {
                if (_flagship == null) { Destroy(gameObject); return; }
                if (_disabled && Time.time >= _recoverAt) Recover(RecoverHull);
                Steer(Time.deltaTime);
                if (_visual != null)
                {
                    float t = Time.time * 1.35f + _phase;
                    float list = _disabled ? 7f : 0f;   // 전투 불능이면 기울어 있다
                    _visual.localPosition = new Vector3(0f, Mathf.Sin(t) * 0.06f - (_disabled ? 0.25f : 0f), 0f);
                    // 선회 중에는 안쪽으로 기운다(소형 고속함)
                    _bank = Mathf.Lerp(_bank, Mathf.Clamp(-_yawRate * 0.09f, -8f, 8f), 1f - Mathf.Exp(-Time.deltaTime * 4f));
                    _visual.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.73f) * 0.8f, 0f, Mathf.Sin(t) * 1.1f + list + _bank);
                }

                // 손상: 선체 절반 아래부터 연기, 4분의 1 아래(전투 불능 포함)는 불길까지
                float hull = Hull01;
                if (hull < 0.5f && Time.deltaTime > 0f && Time.time >= _nextSmoke)
                {
                    _nextSmoke = Time.time + Mathf.Lerp(0.08f, 0.22f, hull * 2f);
                    float scale = _visual != null ? _visual.localScale.y : 1f;
                    Vector3 p = transform.position + Vector3.up * (1.6f * scale) + transform.forward * Random.Range(-1.2f, 1.2f) * scale;
                    float dark = Mathf.Lerp(0.10f, 0.32f, hull * 2f);
                    DecorFx.Emit(DecorFx.Smoke, p, DecorFx.Wind * 0.6f + Vector3.up * 1.6f, 3f, 1.4f, new Color(dark, dark, dark, 0.8f));
                    if (hull < 0.25f)
                        DecorFx.Emit(DecorFx.Fire, p, Vector3.up * 0.8f, 0.35f, 0.9f, new Color(1f, 0.55f, 0.15f, 1f));
                }
            }

            /// <summary>
            /// 배처럼 슬롯을 따라간다: 원하는 속도 = 슬롯 속도 + 거리 오차 × 이득(+ 회피) → 뱃머리를 그쪽으로 선회율 한도 안에서 돌리고,
            /// 뱃머리가 향한 만큼만 속력을 낸다(옆으로 미끄러지지 않음). 슬롯 가까이 오면 진형 침로에 맞춰 선다.
            /// 정비 화면 등 전투 중이 아니면 바로 자리에 놓는다.
            /// </summary>
            private void Steer(float dt)
            {
                var gm = GameManager.Instance;
                bool playing = gm != null && gm.State == GameState.Playing;
                Vector3 target = Target(out Vector3 slotForward);
                if (!playing)
                {
                    // 정비 화면 등(시간이 멈춰 있어도): 진형 자리에 바로 놓는다
                    _heading = Mathf.Atan2(slotForward.x, slotForward.z) * Mathf.Rad2Deg;
                    _speed = 0f;
                    _yawRate = 0f;
                    _prevTarget = target;
                    _targetVel = Vector3.zero;
                    _hasTarget = true;
                    transform.SetPositionAndRotation(new Vector3(target.x, _flagship.position.y, target.z), Quaternion.Euler(0f, _heading, 0f));
                    return;
                }
                if (dt <= 0f) return;
                if (!_hasTarget) { _prevTarget = target; _hasTarget = true; }
                Vector3 tv = (target - _prevTarget) / dt;
                tv.y = 0f;
                _prevTarget = target;
                if (tv.sqrMagnitude > 40f * 40f) tv = Vector3.zero;   // 진형 변경·순간 이동으로 슬롯이 튄 프레임
                _targetVel = Vector3.Lerp(_targetVel, tv, 1f - Mathf.Exp(-dt * 6f));

                Vector3 pos = transform.position;
                Vector3 to = target - pos; to.y = 0f;
                float dist = to.magnitude;
                if (dist > 70f && !_disabled)
                {
                    // 기함이 순간 이동했다(스테이지 시작 등): 자리에 놓는다
                    _heading = Mathf.Atan2(slotForward.x, slotForward.z) * Mathf.Rad2Deg;
                    _speed = _targetVel.magnitude;
                    _yawRate = 0f;
                    transform.SetPositionAndRotation(new Vector3(target.x, _flagship.position.y, target.z), Quaternion.Euler(0f, _heading, 0f));
                    return;
                }

                float maxSpeed = _disabled ? WithdrawSpeed : MaxSpeed;
                // 슬롯 가까이(2m 안)는 오차를 부드럽게 줄여 제자리에서 흔들리지 않게
                Vector3 correction = to * ArriveGain * Mathf.Clamp01(dist / 2f);
                Vector3 desired = _targetVel + correction + Avoidance(pos);
                desired.y = 0f;
                // 대열이 나아가는 중에 뒤 자리로 갈 때는 돌아서지 않고 속력만 줄여(최저 1m/s 전진) 대열이 지나가게 둔다 —
                // 뒤로 돌아 크게 원을 그리며 처지는 일을 막는다. 대열이 멈춰 있으면 후진으로 물러난다(아래).
                float formationSpeed = Vector3.Dot(_targetVel, slotForward);
                if (!_disabled && formationSpeed > 2f)
                {
                    float along = Vector3.Dot(desired, slotForward);
                    if (along < 1f) desired += slotForward * (1f - along);
                }
                if (desired.magnitude > maxSpeed) desired = desired.normalized * maxSpeed;
                float want = desired.magnitude;

                // 원하는 뱃머리: 가는 방향. 슬롯 가까이(6m 안)는 진형 침로로 점점 맞춘다. 거의 멈춰 있으면 진형 침로.
                // 대열이 거의 멈춰 있을 때 가야 할 곳이 바로 뒤(14m 안)이거나 전투 불능 이탈이면 돌아서지 않고 후진한다(뱃머리는 반대쪽).
                // 대열이 나아가는 중에는 후진하지 않는다 — 대열 속력만큼 더 처져 슬롯을 크게 지나치고, 뱃머리까지 뒤로 돌아가 버린다.
                Vector3 current = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                Vector3 desiredDir = want > 0.01f ? desired / want : current;
                bool reverse = want > 0.5f && (_disabled || (dist < 14f && formationSpeed < 2f)) && Vector3.Dot(current, desiredDir) < -0.3f;
                Vector3 wantDir = reverse ? -desiredDir : want > 0.8f ? desiredDir : slotForward;
                float near = 1f - Mathf.Clamp01((dist - 1f) / 5f);
                if (!reverse && want > 0.8f && Vector3.Dot(wantDir, slotForward) > -0.2f)
                    wantDir = Vector3.Slerp(wantDir, slotForward, near * 0.85f);

                float wantYaw = Mathf.Atan2(wantDir.x, wantDir.z) * Mathf.Rad2Deg;
                float turnRate = Mathf.Lerp(TurnRateSlow, TurnRateFast, Mathf.Clamp01(Mathf.Abs(_speed) / 8f)) * (1f - 0.06f * Tier);
                float newYaw = Mathf.MoveTowardsAngle(_heading, wantYaw, turnRate * dt);
                _yawRate = Mathf.DeltaAngle(_heading, newYaw) / dt;
                _heading = newYaw;
                Vector3 dir = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;

                // 뱃머리가(후진이면 선미가) 원하는 방향을 향한 만큼만 속력(반대로 향하면 감속하며 돌아선다)
                float align = Vector3.Dot(reverse ? -dir : dir, desiredDir);
                float wantSpeed = want * Mathf.Clamp01((align + 0.25f) / 1.25f);
                if (reverse) wantSpeed = -Mathf.Min(wantSpeed, ReverseMaxSpeed);
                _speed = Mathf.MoveTowards(_speed, wantSpeed, (Mathf.Abs(wantSpeed) > Mathf.Abs(_speed) ? Accel : Decel) * dt);

                Vector3 next = pos + dir * (_speed * dt);
                next.y = _flagship.position.y;
                var rot = Quaternion.Euler(0f, _heading, 0f);
                // 섬: 박히면 밖으로 밀리고 감속(기함 좌초와 같은 판정)
                if (_hullCollider != null && _hullCollider.enabled &&
                    Game.World.Islands.ResolveOverlap(_hullCollider, ref next, rot, out _))
                    _speed *= 0.6f;
                transform.SetPositionAndRotation(next, rot);
            }

            /// <summary>회피: 다른 호위함(8m), 기함 선체(현측 + 4m), 앞쪽 섬. 원하는 속도에 더하는 벡터(m/s).</summary>
            private Vector3 Avoidance(Vector3 pos)
            {
                Vector3 push = Vector3.zero;
                foreach (var other in _owner._escorts)
                {
                    if (other == null || other == this) continue;
                    Vector3 d = pos - other.transform.position; d.y = 0f;
                    float m = d.magnitude;
                    if (m < 8f && m > 0.01f)
                    {
                        Vector3 fwd = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                        bool overtaking = _speed > 0.5f && Vector3.Dot(fwd, -d / m) > 0.5f && _speed > other._speed + 0.5f;
                        if (overtaking)
                        {
                            // 앞 함을 따라잡는 중: 뒤로 밀지 않고 옆으로만 비켜 지나간다(상대가 있는 반대쪽, 같으면 우현)
                            Vector3 right = Vector3.Cross(Vector3.up, fwd);
                            float side = Vector3.Dot(right, d) >= -0.3f ? 1f : -1f;
                            push += right * side * (8f - m) * 2f;
                        }
                        else push += d / m * (8f - m) * 1.2f;
                    }
                }
                // 기함: 선수~선미 중심선에서 가장 가까운 점
                var ext = _owner.FlagshipExtent;
                Vector3 local = _flagship.InverseTransformPoint(pos);
                Vector3 nearest = _flagship.TransformPoint(new Vector3(0f, local.y, Mathf.Clamp(local.z, ext.y, ext.x)));
                Vector3 away = pos - nearest; away.y = 0f;
                float clear = ext.z + 4f, md = away.magnitude;
                if (md < clear && md > 0.01f) push += away / md * (clear - md) * 2.5f;
                // 섬: 진행 방향 앞을 살펴 섬 바깥쪽으로
                Vector3 dir = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                if (Game.World.Islands.ProbeAhead(pos, _speed < 0f ? -dir : dir, 1.6f, 5f + Mathf.Abs(_speed) * 1.2f, out var hit))
                {
                    Vector3 n = hit.normal; n.y = 0f;
                    if (n.sqrMagnitude > 1e-4f) push += n.normalized * 9f;
                }
                return push;
            }

            private static void BuildModel(Transform root, EscortRole role, Color roleColor)
            {
                var hull = new GameObject("Hull", typeof(MeshFilter), typeof(MeshRenderer));
                hull.transform.SetParent(root, false);
                hull.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                hull.GetComponent<MeshFilter>().sharedMesh = EscortHullMesh;
                hull.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;

                Part(root, "Deck", new Vector3(0f, 0.34f, -0.25f), new Vector3(2.45f, 0.16f, 6.7f), DeckMaterial);
                Part(root, "Superstructure", new Vector3(0f, 0.92f, -0.45f), new Vector3(1.55f, 1.0f, 2.25f), SuperstructureMaterial);
                Part(root, "Bridge", new Vector3(0f, 1.55f, 0.25f), new Vector3(1.45f, 0.38f, 0.65f), GlassMaterial);
                Part(root, "Role stripe", new Vector3(0f, 1.19f, 0.72f), new Vector3(1.62f, 0.12f, 0.12f), RoleMaterial(roleColor));
                Cylinder(root, "Mast", new Vector3(0f, 2.45f, -0.2f), new Vector3(0.12f, 0.82f, 0.12f), SuperstructureMaterial);

                Part(root, "Upgrade tier 1", new Vector3(0f, 0.72f, 2.55f), new Vector3(0.78f, 0.40f, 0.78f), RoleMaterial(roleColor));
                Cylinder(root, "Upgrade tier 2", new Vector3(0f, 3.18f, -0.2f), new Vector3(0.38f, 0.10f, 0.38f), RoleMaterial(roleColor));
                Part(root, "Upgrade tier 3", new Vector3(0f, 1.02f, -2.75f), new Vector3(1.55f, 0.48f, 0.85f), RoleMaterial(roleColor));

                switch (role)
                {
                    case EscortRole.AirDefense:
                        Cylinder(root, "Search radar", new Vector3(0f, 3.25f, -0.2f), new Vector3(0.72f, 0.08f, 0.20f), RoleMaterial(roleColor));
                        Part(root, "Forward VLS", new Vector3(0f, 0.62f, 2.35f), new Vector3(1.35f, 0.16f, 1.15f), DarkMaterial);
                        break;
                    case EscortRole.AntiSubmarine:
                        Part(root, "Hangar", new Vector3(0f, 0.75f, -2.25f), new Vector3(1.65f, 0.70f, 1.35f), SuperstructureMaterial);
                        Cylinder(root, "Helicopter pad", new Vector3(0f, 0.50f, -3.35f), new Vector3(1.15f, 0.035f, 1.15f), RoleMaterial(roleColor));
                        break;
                    case EscortRole.ElectronicWarfare:
                        Sphere(root, "EW dome port", new Vector3(-0.48f, 2.25f, -0.45f), new Vector3(0.34f, 0.34f, 0.34f), RoleMaterial(roleColor));
                        Sphere(root, "EW dome starboard", new Vector3(0.48f, 2.25f, -0.45f), new Vector3(0.34f, 0.34f, 0.34f), RoleMaterial(roleColor));
                        Sphere(root, "EW mast dome", new Vector3(0f, 3.2f, -0.2f), new Vector3(0.28f, 0.28f, 0.28f), RoleMaterial(roleColor));
                        break;
                    case EscortRole.SurfaceStrike:
                        Part(root, "Missile bank port", new Vector3(-0.58f, 0.78f, -1.85f), new Vector3(0.62f, 0.58f, 1.55f), DarkMaterial);
                        Part(root, "Missile bank starboard", new Vector3(0.58f, 0.78f, -1.85f), new Vector3(0.62f, 0.58f, 1.55f), DarkMaterial);
                        Part(root, "Launcher marking", new Vector3(0f, 1.10f, -1.85f), new Vector3(1.55f, 0.07f, 1.4f), RoleMaterial(roleColor));
                        break;
                }
            }

            private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = scale;
                DisableCollider(go);
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            private static void Cylinder(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = scale;
                DisableCollider(go);
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            private static void Sphere(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = position;
                go.transform.localScale = scale;
                DisableCollider(go);
                go.GetComponent<Renderer>().sharedMaterial = material;
            }

            private static void DisableCollider(GameObject go)
            {
                if (go.TryGetComponent<Collider>(out var collider)) collider.enabled = false;
            }

            private static Mesh s_hullMesh;
            private static Mesh EscortHullMesh
            {
                get
                {
                    if (s_hullMesh != null) return s_hullMesh;
                    Vector2[] polygon =
                    {
                        new(-1.5f, -4.1f), new(1.5f, -4.1f), new(1.5f, 2.1f),
                        new(0f, 4.8f), new(-1.5f, 2.1f),
                    };
                    var vertices = new Vector3[10];
                    for (int i = 0; i < 5; i++)
                    {
                        vertices[i] = new Vector3(polygon[i].x, 0.42f, polygon[i].y);
                        vertices[i + 5] = new Vector3(polygon[i].x * 0.72f, -0.42f, polygon[i].y);
                    }
                    var triangles = new List<int>(48);
                    for (int i = 1; i < 4; i++) triangles.AddRange(new[] { 0, i, i + 1 });
                    for (int i = 1; i < 4; i++) triangles.AddRange(new[] { 5, 6 + i, 5 + i });
                    for (int i = 0; i < 5; i++)
                    {
                        int next = (i + 1) % 5;
                        triangles.AddRange(new[] { i, next, i + 5, next, next + 5, i + 5 });
                    }
                    s_hullMesh = new Mesh { name = "Runtime escort hull" };
                    s_hullMesh.SetVertices(vertices);
                    s_hullMesh.SetTriangles(triangles, 0);
                    s_hullMesh.RecalculateNormals();
                    s_hullMesh.RecalculateBounds();
                    return s_hullMesh;
                }
            }

            private static Material s_hull, s_deck, s_superstructure, s_glass, s_dark;
            private static readonly Dictionary<Color32, Material> s_roleMaterials = new();
            private static Material HullMaterial => s_hull ??= MakeMaterial("Escort hull", new Color(0.10f, 0.16f, 0.18f), 0.45f, 0.38f);
            private static Material DeckMaterial => s_deck ??= MakeMaterial("Escort deck", new Color(0.16f, 0.21f, 0.22f), 0.25f, 0.28f);
            private static Material SuperstructureMaterial => s_superstructure ??= MakeMaterial("Escort superstructure", new Color(0.30f, 0.38f, 0.39f), 0.18f, 0.32f);
            private static Material GlassMaterial => s_glass ??= MakeMaterial("Escort bridge glass", new Color(0.035f, 0.15f, 0.18f), 0.55f, 0.76f);
            private static Material DarkMaterial => s_dark ??= MakeMaterial("Escort equipment", new Color(0.055f, 0.075f, 0.08f), 0.48f, 0.35f);

            private static Material RoleMaterial(Color color)
            {
                Color32 key = color;
                if (s_roleMaterials.TryGetValue(key, out var material)) return material;
                material = MakeMaterial("Escort role marking", color, 0.15f, 0.55f);
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.65f);
                }
                s_roleMaterials[key] = material;
                return material;
            }

            private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                var material = new Material(shader) { name = name };
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                else material.color = color;
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
                return material;
            }
        }
    }
}
