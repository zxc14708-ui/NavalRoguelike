using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;
using Game.Ship;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 정찰 드론 격납고(2026-11-22, 지원 블록). 일정 시간마다 드론이 날아가 적 위를 맴돌며
    ///   - 둘레 24m 안의 적을 드러낸다: 잠항 중인 잠수함도 찾아 부상 표시 · 접촉 확정, 반잠수정도 탐지.
    ///   - 그 안에서 가장 큰 적 하나에 약점 표시(받는 피해 +20%, 갈라진 방패 문장)를 붙인다.
    /// 단계별: 간격 25 · 22 · 18초, 드론 1 · 2 · 3기, 맴도는 시간 6 · 8 · 10초. 드론은 서로 다른 적(체력 큰 순, 잠수함 먼저)으로 간다.
    /// 드론은 표적이 아니다(격추되지 않음). 끝나면 돌아와 사라진다.
    /// </summary>
    public sealed class ReconDroneBayModule : ModuleRuntime, ISupportTimer
    {
        public const float SearchRadius = 90f;
        public const float RevealRadius = 24f;
        public const float MarkMultiplier = 1.2f;
        public const float FlySpeed = 26f;
        public const float Altitude = 9f;

        public static float IntervalFor(int level) => level >= 3 ? 18f : level == 2 ? 22f : 25f;
        public static int DronesFor(int level) => level >= 3 ? 3 : level == 2 ? 2 : 1;
        public static float LoiterFor(int level) => level >= 3 ? 10f : level == 2 ? 8f : 6f;

        private static readonly Color MarkColor = new(1f, 0.55f, 0.25f, 1f);

        private float _timer;
        private readonly List<ReconDrone> _drones = new();

        private int Level => Instance != null ? Instance.Level : 1;
        /// <summary>검증용: 띄운 횟수 · 드러낸 잠수함 수 · 약점 표시 수.</summary>
        public int Launches { get; private set; }
        public int SubsRevealed { get; internal set; }
        public int Marks { get; internal set; }
        public int ActiveDrones { get { _drones.RemoveAll(d => d == null); return _drones.Count; } }

        protected override void OnInitialized()
        {
            _timer = IntervalFor(Level) * 0.4f;
            SupportTimers.Register(this);
        }

        private void OnDestroy()
        {
            SupportTimers.Unregister(this);
            foreach (var d in _drones) if (d != null) d.Recall();
        }

        protected override void Tick(float dt)
        {
            if (ActiveDrones > 0) return;   // 드론이 돌아온 뒤부터 센다
            _timer -= dt;
            if (_timer > 0f) return;
            if (!Launch()) { _timer = 1f; return; }
            _timer = IntervalFor(Level);
        }

        /// <summary>검증용: 지금 바로 띄운다(표적이 없으면 false).</summary>
        public bool DevLaunchNow() { bool ok = Launch(); if (ok) _timer = IntervalFor(Level); return ok; }

        private bool Launch()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Player == null) return false;
            Vector3 self = gm.Player.transform.position;
            var targets = new List<EnemyController>();
            foreach (var kind in new[] { TargetKind.Submarine, TargetKind.Surface })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is EnemyController e && e.IsAlive && Flat(e.transform.position - self).sqrMagnitude <= SearchRadius * SearchRadius)
                        targets.Add(e);
            }
            if (targets.Count == 0) return false;
            // 잠수함 먼저, 그다음 체력 큰 순. 서로 RevealRadius 안에 있는 적은 같은 무리로 보고 건너뛴다
            targets.Sort((a, b) =>
            {
                int sa = a is SubmarineBase ? 0 : 1, sb = b is SubmarineBase ? 0 : 1;
                if (sa != sb) return sa.CompareTo(sb);
                float ha = a.Definition != null ? a.Definition.MaxHp : a.CurrentHp, hb = b.Definition != null ? b.Definition.MaxHp : b.CurrentHp;
                return hb.CompareTo(ha);
            });
            var picks = new List<EnemyController>();
            int want = DronesFor(Level);
            foreach (var e in targets)
            {
                if (picks.Count >= want) break;
                bool near = false;
                foreach (var p in picks) if (Flat(p.transform.position - e.transform.position).sqrMagnitude < RevealRadius * RevealRadius * 0.6f) { near = true; break; }
                if (!near) picks.Add(e);
            }
            for (int i = 0; picks.Count < want && i < targets.Count; i++) if (!picks.Contains(targets[i])) picks.Add(targets[i]);

            Launches++;
            float loiter = LoiterFor(Level);
            for (int i = 0; i < picks.Count; i++)
            {
                var go = new GameObject("Recon drone");
                go.transform.position = transform.position + Vector3.up * 1.5f;
                var d = go.AddComponent<ReconDrone>();
                d.Begin(this, picks[i], loiter, i * 0.35f);
                _drones.Add(d);
            }
            AudioManager.Play(Game.Data.SfxId.DroneBuzz, transform.position, 0.7f, 1.3f);
            CombatLog.Add(LogName, $"정찰 드론 {picks.Count}기 출격 · {loiter:0}초 정찰");
            return true;
        }

        internal void Spot(Vector3 center, float hold, ReconDrone drone)
        {
            float r2 = RevealRadius * RevealRadius;
            EnemyController biggest = null; float bigHp = -1f;
            foreach (var kind in new[] { TargetKind.Submarine, TargetKind.Surface })
            {
                var list = TargetRegistry.HostileTo(CombatFaction.Player, kind);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count || list[i] is not EnemyController e || !e.IsAlive) continue;
                    if (Flat(e.transform.position - center).sqrMagnitude > r2) continue;
                    if (e is SubmarineBase sub)
                    {
                        if (!sub.IsRevealed) SubsRevealed++;
                        sub.RevealFor(hold);
                        sub.ConfirmContact(hold);
                    }
                    if (e is ILowObservable low) low.MarkDetected(hold);
                    float hp = e.Definition != null ? e.Definition.MaxHp : e.CurrentHp;
                    if (hp > bigHp) { bigHp = hp; biggest = e; }
                }
            }
            if (biggest != null)
            {
                if (!biggest.IsMarked) Marks++;
                biggest.Mark(hold, MarkMultiplier);
                SignatureFx.BrokenShield(biggest.transform, hold, MarkColor, biggest is AirEnemy ? 2.2f : 2.7f);
                drone.Follow = biggest.transform;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // --- 지원 판
        public string TimerLabel => "정찰 드론";
        public string TimerStatus => Instance == null || !Instance.IsOperational ? "<color=#e0956a>파손</color>"
            : ActiveDrones > 0 ? $"정찰 중 · {ActiveDrones}기" : _timer <= 1f ? "표적 찾는 중" : $"다음 출격 {_timer:0}초";
        public bool TimerActive => ActiveDrones > 0;
    }

    /// <summary>정찰 드론 한 기: 이륙 → 표적으로 비행 → 둘레를 맴돌며 0.5초마다 정찰 → 돌아와 사라진다.</summary>
    public sealed class ReconDrone : MonoBehaviour
    {
        private enum Phase { Wait, Out, Loiter, Home }
        private ReconDroneBayModule _bay;
        private Transform _model;
        private Phase _phase;
        private float _wait, _loiterLeft, _spotTimer, _angle, _homeTimer;
        private float _hold;
        public Transform Follow { get; set; }

        public void Begin(ReconDroneBayModule bay, EnemyController target, float loiter, float delay)
        {
            _bay = bay;
            Follow = target != null ? target.transform : null;
            _loiterLeft = loiter;
            _wait = delay;
            _hold = 1.6f;
            _phase = Phase.Wait;
            _angle = Random.Range(0f, 360f);
            _model = SupportFx.BuildDrone(transform);
        }

        public void Recall() { if (this != null) { _phase = Phase.Home; _homeTimer = 0f; } }

        private Vector3 HomePoint => _bay != null ? _bay.transform.position + Vector3.up * 1.5f : transform.position + Vector3.up * 30f;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            switch (_phase)
            {
                case Phase.Wait:
                    _wait -= dt;
                    transform.position = HomePoint;
                    if (_wait <= 0f) _phase = Phase.Out;
                    break;
                case Phase.Out:
                {
                    if (Follow == null) { Recall(); break; }
                    Vector3 to = Follow.position + Vector3.up * ReconDroneBayModule.Altitude;
                    Fly(to, dt);
                    if (Flat(to - transform.position).magnitude < 8f) { _phase = Phase.Loiter; _spotTimer = 0f; }
                    break;
                }
                case Phase.Loiter:
                {
                    Vector3 c = Follow != null ? Follow.position : transform.position;
                    _angle += dt * 70f;
                    float r = 7f;
                    Vector3 to = new Vector3(c.x + Mathf.Cos(_angle * Mathf.Deg2Rad) * r, ReconDroneBayModule.Altitude, c.z + Mathf.Sin(_angle * Mathf.Deg2Rad) * r);
                    Fly(to, dt);
                    _spotTimer -= dt;
                    if (_spotTimer <= 0f)
                    {
                        _spotTimer = 0.5f;
                        if (_bay != null) _bay.Spot(new Vector3(c.x, 0f, c.z), Mathf.Min(_hold, _loiterLeft + 0.6f), this);
                    }
                    _loiterLeft -= dt;
                    if (_loiterLeft <= 0f) Recall();
                    break;
                }
                case Phase.Home:
                {
                    _homeTimer += dt;
                    Vector3 to = HomePoint;
                    Fly(to, dt);
                    if ((to - transform.position).magnitude < 2f || _homeTimer > 8f) Destroy(gameObject);
                    break;
                }
            }
        }

        private void Fly(Vector3 to, float dt)
        {
            Vector3 d = to - transform.position;
            float step = ReconDroneBayModule.FlySpeed * dt;
            Vector3 move = d.magnitude <= step ? d : d.normalized * step;
            if (_phase == Phase.Out && transform.position.y < ReconDroneBayModule.Altitude) move.y = Mathf.Max(move.y, 6f * dt);   // 이륙
            transform.position += move;
            Vector3 flat = Flat(move);
            if (flat.sqrMagnitude > 1e-6f)
            {
                var want = Quaternion.LookRotation(flat.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, _phase == Phase.Loiter ? 22f : 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-8f * dt));
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
