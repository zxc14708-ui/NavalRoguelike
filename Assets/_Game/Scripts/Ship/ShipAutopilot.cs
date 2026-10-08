using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Game.Core;

namespace Game.Ship
{
    /// <summary>
    /// 우클릭 항로(자동 조함, 2026-10-07).
    ///   - 우클릭: 목표 하나로 바꾸기 · Shift+우클릭: 경유지 추가(최대 5) · 앵커 위 우클릭: 그 목표 취소 · 섬 위는 받지 않음
    ///   - 배를 직접 움직이지 않고 타각만 대신 잡는다(<see cref="ShipController.AutoRudder"/>) — 선회 반경·가속은 수동 조함과 같다.
    ///     정지·후진 중에 항로를 찍으면 전령기를 전속 전진으로 올린다.
    ///   - 마지막 목표를 지나면 그때의 침로로 계속 직진한다(침로 유지).
    ///   - 조함 키(W/S/A/D/X, 게임패드 스틱)를 누르면 항로와 침로 유지를 모두 풀고 수동 조함으로 돌아간다.
    /// 예상 항로는 같은 조함 식(<see cref="ShipController.StepHelm"/>)으로 앞을 계산해 만든다. 표시는 <see cref="Game.View.NavRouteView"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipAutopilot : MonoBehaviour
    {
        public const int MaxWaypoints = 5;
        /// <summary>앵커 위 우클릭 판정 반경(화면 픽셀).</summary>
        private const float AnchorPickPixels = 22f;
        /// <summary>침로 오차(도)를 타각으로 바꾸는 폭: 이 각도 이상 틀어져 있으면 타를 끝까지 쓴다.</summary>
        private const float FullRudderErrorDeg = 25f;
        private const float PredictStep = 0.1f, PredictSeconds = 70f, TailSeconds = 5f;
        /// <summary>목표점 둘레에 이만큼(m) 섬이 없어야 받는다.</summary>
        private const float LandClearance = 3f;

        private ShipController _ship;
        private readonly List<Vector3> _route = new();
        private Leg _leg;
        private bool _holding;
        private float _holdHeading;
        private float _nextPredict;

        /// <summary>
        /// 지금 구간의 진행 상태. 실제 조함과 예상 항로 계산이 같은 규칙(<see cref="Passed"/>)을 쓰도록 값으로 들고 다닌다.
        /// </summary>
        private struct Leg
        {
            public bool Approached;   // 목표를 한 번이라도 선수 60° 안에 두고 다가갔는가
            public float Time;        // 구간을 시작한 뒤 흐른 시간(초)
        }

        /// <summary>남은 목표(수면 위 점, 순서대로).</summary>
        public IReadOnlyList<Vector3> Route => _route;
        /// <summary>예상 항로(배 위치부터). <see cref="PredictedTailStart"/>부터는 마지막 목표 뒤 직진 구간.</summary>
        public readonly List<Vector3> Predicted = new();
        public int PredictedTailStart { get; private set; }
        /// <summary>최대 개수를 넘겨 찍었거나 섬 위를 찍은 시각(표시가 잠깐 깜박인다).</summary>
        public float RejectedAt { get; private set; } = -99f;
        public bool HasRoute => _route.Count > 0;
        public bool IsHolding => _holding;
        /// <summary>침로 유지 중인 방향(도, 북 = 0, 시계 방향).</summary>
        public float HoldHeading => _holdHeading;

        public static ShipAutopilot Ensure(ShipController ship)
        {
            if (ship == null) return null;
            var pilot = ship.GetComponent<ShipAutopilot>();
            if (pilot == null) pilot = ship.gameObject.AddComponent<ShipAutopilot>();
            Game.View.NavRouteView.Ensure(pilot);
            return pilot;
        }

        private void Awake()
        {
            _ship = GetComponent<ShipController>();
            _ship.ManualHelm += Cancel;
        }

        private void OnDestroy()
        {
            if (_ship != null) _ship.ManualHelm -= Cancel;
        }

        /// <summary>항로·침로 유지를 모두 풀고 수동 조함으로.</summary>
        public void Cancel()
        {
            _route.Clear();
            Predicted.Clear();
            _holding = false;
            if (_ship != null) _ship.AutoRudder = null;
        }

        /// <summary>목표를 하나로 바꾼다(append = 경유지로 덧붙인다). 개수를 넘기면 false.</summary>
        public bool SetTarget(Vector3 point, bool append)
        {
            point.y = 0f;
            if (append && _route.Count >= MaxWaypoints) { RejectedAt = Time.unscaledTime; return false; }
            if (!append) _route.Clear();
            if (_route.Count == 0) BeginLeg();
            _route.Add(point);
            _holding = false;
            // 정지·후진 중이면 앞으로 가야 항로를 따라갈 수 있다
            if (_ship.ThrottleInput <= 0.01f) _ship.SetEngineOrder(1f);
            _nextPredict = 0f;
            return true;
        }

        /// <summary>i번째 목표를 뺀다. 다 빠지면 지금 방향으로 직진한다.</summary>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _route.Count) return;
            _route.RemoveAt(index);
            if (index == 0) BeginLeg();
            if (_route.Count == 0) Hold(Heading(transform.forward));
            _nextPredict = 0f;
        }

        private void BeginLeg() => _leg = default;

        private void Hold(float heading)
        {
            _holding = true;
            _holdHeading = heading;
            Predicted.Clear();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (_ship == null || gm == null || gm.Player != _ship || !_ship.IsAlive) { Release(); return; }
            if (gm.State != GameState.Playing || Time.timeScale <= 0f) return;

            HandleClick();
            Steer();
            if (Time.unscaledTime >= _nextPredict)
            {
                _nextPredict = Time.unscaledTime + 0.1f;
                Predict();
            }
        }

        private void Release()
        {
            if (_route.Count > 0 || _holding) Cancel();
        }

        private void HandleClick()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector2 screen = mouse.position.ReadValue();
            int hit = PickAnchor(cam, screen);
            if (hit >= 0) { RemoveAt(hit); return; }

            var ray = cam.ScreenPointToRay(screen);
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return;
            float t = -ray.origin.y / ray.direction.y;
            if (t <= 0f) return;
            Vector3 point = ray.origin + ray.direction * t;
            // 섬(육지) 위는 목표로 받지 않는다 — 배가 박혀 멈춘다
            if (!Game.World.Islands.IsClear(point, LandClearance)) { RejectedAt = Time.unscaledTime; return; }
            var keyboard = Keyboard.current;
            bool append = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            SetTarget(point, append);
        }

        private int PickAnchor(Camera cam, Vector2 screen)
        {
            int best = -1;
            float bestSq = AnchorPickPixels * AnchorPickPixels;
            for (int i = 0; i < _route.Count; i++)
            {
                // 앵커는 점 위로 솟아 있으므로 점과 그 위쪽(약 12픽셀)을 함께 본다
                Vector3 s = cam.WorldToScreenPoint(_route[i]);
                if (s.z <= 0f) continue;
                Vector2 d = screen - new Vector2(s.x, s.y + 12f);
                if (d.sqrMagnitude < bestSq) { bestSq = d.sqrMagnitude; best = i; }
            }
            return best;
        }

        private void Steer()
        {
            if (_route.Count == 0)
            {
                _ship.AutoRudder = _holding ? RudderFor(_holdHeading, Heading(transform.forward), _ship.YawRate) : (float?)null;
                return;
            }

            Vector3 pos = Flat(transform.position);
            float heading = Heading(transform.forward);
            if (Passed(pos, heading, _ship.CurrentSpeed, _route[0], ref _leg, Time.deltaTime))
            {
                _route.RemoveAt(0);
                BeginLeg();
                if (_route.Count == 0) Hold(heading);
                Steer();
                return;
            }
            _ship.AutoRudder = SteerTo(pos, heading, _ship.CurrentSpeed, _ship.YawRate, _route[0]);
        }

        /// <summary>
        /// 목표로 가는 타각. 목표가 선회권(그쪽으로 타를 끝까지 썼을 때 그리는 원) 안쪽이면 돌아도 닿지 않으므로
        /// 침로를 그대로 두고 앞으로 나가 거리를 벌린 뒤 돈다(맴돌지 않게).
        /// </summary>
        private float SteerTo(Vector3 pos, float heading, float speed, float yawRate, Vector3 target)
        {
            Vector3 to = target - pos;
            float h = heading * Mathf.Deg2Rad;
            Vector3 right = new(Mathf.Cos(h), 0f, -Mathf.Sin(h));
            float side = Vector3.Dot(to, right) >= 0f ? 1f : -1f;
            float turnRate = _ship.Config != null ? _ship.Config.BaseTurnRateDegPerSec : 20f;
            float radius = Mathf.Abs(speed) / Mathf.Max(0.05f, turnRate * Mathf.Deg2Rad) * 1.15f;
            Vector3 center = pos + right * (side * radius);
            if (Vector3.Distance(target, center) < radius && Mathf.Abs(Mathf.DeltaAngle(heading, Heading(to))) > 20f)
                return RudderFor(heading, heading, yawRate);
            return RudderFor(Heading(to), heading, yawRate);
        }

        /// <summary>
        /// 목표를 지났는가(시간 간격과 무관한 기하 규칙이라 예상 항로와 실제가 같은 순간에 넘어간다):
        ///   ① 도착 반경(속력 × 0.5, 최소 5m) 안
        ///   ② 한 번 다가간 뒤(선수 60° 안) 목표가 옆을 지나 뒤로 갔다(선수에서 90° 넘게) — 반경 밖으로 스쳐 지나감
        ///   ③ 선회권 안쪽이라 닿을 수 없어 한 바퀴 넘게 맴돌았다
        /// </summary>
        private bool Passed(Vector3 pos, float heading, float speed, Vector3 target, ref Leg leg, float dt)
        {
            leg.Time += dt;
            Vector3 to = target - pos;
            float dist = to.magnitude;
            if (dist <= Mathf.Max(5f, Mathf.Abs(speed) * 0.5f)) return true;
            float off = Mathf.Abs(Mathf.DeltaAngle(heading, Heading(to)));
            if (off < 60f) leg.Approached = true;
            if (leg.Approached && off > 90f) return true;
            float turnRate = _ship.Config != null ? _ship.Config.BaseTurnRateDegPerSec : 20f;
            float circle = 360f / Mathf.Max(1f, turnRate);   // 타 끝까지 한 바퀴 도는 데 걸리는 시간
            return leg.Time > circle * 1.3f + 4f && dist < Mathf.Abs(speed) / Mathf.Max(0.05f, turnRate * Mathf.Deg2Rad) * 2.2f;
        }

        /// <summary>원하는 침로로 가는 타각. 선회율을 앞당겨 빼서(타각·선체 지연) 지나치게 돌지 않게 한다.</summary>
        private float RudderFor(float wantHeading, float heading, float yawRate)
        {
            var cfg = _ship.Config;
            float lead = cfg != null ? cfg.TurnResponseTime + cfg.RudderShiftTime * 0.5f : 1f;
            float error = Mathf.DeltaAngle(heading, wantHeading);
            return Mathf.Clamp((error - yawRate * lead) / FullRudderErrorDeg, -1f, 1f);
        }

        /// <summary>같은 조함 식·같은 통과 규칙으로 앞으로의 항로를 계산한다(섬 충돌은 보지 않는다).</summary>
        private void Predict()
        {
            Predicted.Clear();
            PredictedTailStart = 0;
            if (_route.Count == 0) return;

            Vector3 pos = Flat(transform.position);
            float heading = Heading(transform.forward);
            float speed = _ship.CurrentSpeed, yaw = _ship.YawRate, rudder = _ship.RudderInput;
            float throttle = _ship.ThrottleInput;
            float step = _ship.RudderStep(PredictStep);
            var leg = _leg;
            int i = 0;
            bool holding = false;
            float holdHeading = 0f, tail = 0f;
            Predicted.Add(pos);

            for (float t = 0f; t < PredictSeconds; t += PredictStep)
            {
                float goal;
                if (!holding && Passed(pos, heading, speed, _route[i], ref leg, PredictStep))
                {
                    leg = default;
                    if (++i >= _route.Count)
                    {
                        holding = true;
                        holdHeading = heading;
                        PredictedTailStart = Predicted.Count - 1;
                    }
                }
                if (!holding) goal = SteerTo(pos, heading, speed, yaw, _route[i]);
                else
                {
                    goal = RudderFor(holdHeading, heading, yaw);
                    tail += PredictStep;
                    if (tail > TailSeconds) break;
                }

                rudder = Mathf.MoveTowards(rudder, goal, step);
                _ship.StepHelm(throttle, rudder, ref speed, ref yaw, PredictStep);
                heading += yaw * PredictStep;
                pos += Quaternion.Euler(0f, heading, 0f) * Vector3.forward * (speed * PredictStep);
                if (Vector3.Distance(Predicted[Predicted.Count - 1], pos) > 0.6f) Predicted.Add(pos);
            }
            if (!holding) PredictedTailStart = Predicted.Count;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
        private static float Heading(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }
}
