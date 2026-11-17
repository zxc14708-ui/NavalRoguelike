using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.View;

namespace Game.Missions
{
    /// <summary>
    /// 전장 이벤트 "구조 활동"(2026-11-17). 흩어진 조난 신호를 돌며 구하게 해서 기함을 움직이게 하고, 구하는 동안의 감속이 위험이 된다.
    ///   - 기함에서 MinDistance~MaxDistance 떨어진 세 곳(대략 120° 간격)에 구명정이 뜬다. 저체온 시간이 서로 달라(Lives)
    ///     어느 쪽부터 갈지 고르게 된다. 주황 기둥 · 구조 원 · 가장 가까운 구명정까지 주황 선.
    ///   - 구조: 구조 원(Radius) 안에서 MaxKnots 이하로 HoldTime초 머문다(벗어나거나 빨라지면 진행이 빠르게 줄어든다).
    ///   - 보상(한 명마다): 선체 수리 RepairRatio · 경험치(다음 레벨까지의 XpRatio). 모두 구하면 다시 뽑기 +1.
    ///   - 시간이 다 된 구명정은 가라앉는다(벌칙 없음). 모두 끝나면 결과 알림.
    /// </summary>
    public sealed class RescueMission : MonoBehaviour
    {
        public const float Radius = 16f, HoldTime = 3f, MaxKnots = 8f;
        public const float MinDistance = 85f, MaxDistance = 150f;
        public const float RepairRatio = 0.1f, XpRatio = 0.2f;
        public static readonly float[] Lives = { 55f, 75f, 95f };

        public enum Outcome { Running, Finished, Cancelled }

        public sealed class Raft
        {
            public Transform Root;
            public Vector3 Position;
            public float TimeLeft, Hold, Yaw;
            public bool Rescued, Lost;
            public LineRenderer Ring;
            public Transform Beacon;
            public bool Active => !Rescued && !Lost;
        }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private readonly List<Raft> _rafts = new();
        private LineRenderer _guide;
        private MissionMarks.Panel _panel;
        private float _age;

        public Outcome State { get; private set; } = Outcome.Running;
        public IReadOnlyList<Raft> Rafts => _rafts;
        public int RescuedCount { get; private set; }
        public int LostCount { get; private set; }
        public bool AllRescuedBonus { get; private set; }
        public string PanelText => _panel != null && _panel.Label != null ? _panel.Label.text ?? "" : "";

        public static RescueMission Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp)
        {
            if (player == null) return null;
            var m = new GameObject("Rescue mission").AddComponent<RescueMission>();
            m._player = player;
            m._xp = xp;
            m.SpawnRafts();
            if (m._rafts.Count == 0) { Destroy(m.gameObject); return null; }
            m._guide = MissionMarks.Line(m.transform, "Rescue guide", new Color(1f, 0.62f, 0.2f, 0.7f), 0.7f, false);
            m._panel = MissionMarks.Panel.Create("Rescue panel", -212f, 600f, new Color(1f, 0.85f, 0.6f));
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner($"조난 신호 {m._rafts.Count}곳 — 주황 원 안에서 {MaxKnots:0}노트 이하로 {HoldTime:0}초 머물러 구조(수리 · 경험치, 모두 구하면 다시 뽑기 +1)");
            m.RefreshPanel();
            return m;
        }

        private void SpawnRafts()
        {
            Vector3 c = MissionMarks.Flat(_player.transform.position);
            float baseAngle = Random.Range(0f, 360f);
            var order = new List<float>(Lives);
            for (int i = 0; i < order.Count; i++) { int j = Random.Range(i, order.Count); (order[i], order[j]) = (order[j], order[i]); }
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = Vector3.zero;
                bool found = false;
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = baseAngle + i * 120f + Random.Range(-35f, 35f) + (k / 4) * 20f * (k % 2 == 0 ? 1f : -1f);
                    float d = Random.Range(MinDistance, MaxDistance) * (k >= 10 ? 0.75f : 1f);
                    var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * d;
                    if (MissionMarks.OpenWater(p, 12f)) { spot = p; found = true; }
                }
                if (!found) continue;
                _rafts.Add(BuildRaft(spot, order[i], i));
            }
        }

        private Raft BuildRaft(Vector3 at, float life, int index)
        {
            var root = new GameObject($"Rescue raft {index}").transform;
            root.SetParent(transform, false);
            root.position = new Vector3(at.x, Waterline.WaterY, at.z);
            float yaw = Random.Range(0f, 360f);
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            var orange = new Color(1f, 0.45f, 0.08f);
            MissionMarks.Prop(root, PrimitiveType.Cylinder, "Raft hull", new Vector3(0f, 0.15f, 0f), new Vector3(2.8f, 0.25f, 2.8f), orange);
            MissionMarks.Prop(root, PrimitiveType.Cylinder, "Raft canopy", new Vector3(0f, 0.75f, 0f), new Vector3(1.6f, 0.45f, 1.6f), new Color(1f, 0.55f, 0.15f));
            for (int s = 0; s < 3; s++)
            {
                float a = s * 120f * Mathf.Deg2Rad;
                MissionMarks.Prop(root, PrimitiveType.Capsule, "Survivor", new Vector3(Mathf.Sin(a) * 1.05f, 0.55f, Mathf.Cos(a) * 1.05f), new Vector3(0.38f, 0.38f, 0.38f), new Color(0.2f, 0.24f, 0.3f));
            }
            var raft = new Raft
            {
                Root = root,
                Position = at,
                Yaw = yaw,
                TimeLeft = life,
                Ring = MissionMarks.Line(transform, $"Rescue ring {index}", new Color(1f, 0.6f, 0.2f, 0.85f), 1.1f, true),
                Beacon = MissionMarks.Beacon(transform, $"Rescue beacon {index}", at, new Color(1f, 0.55f, 0.15f, 0.45f), 14f),
            };
            MissionMarks.Ring(raft.Ring, at, Radius);
            return raft;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver || _player == null) { Cancel(); return; }
            if (gm.State != GameState.Playing || State != Outcome.Running) return;
            float dt = Time.deltaTime;
            _age += dt;

            Vector3 ship = MissionMarks.Flat(_player.transform.position);
            float knots = Game.Ship.ShipController.ToKnots(Mathf.Abs(_player.CurrentSpeed));
            foreach (var r in _rafts)
            {
                if (!r.Active) continue;
                r.TimeLeft -= dt;
                // 물결에 흔들린다
                r.Yaw += dt * 4f;
                r.Root.localRotation = Quaternion.Euler(Mathf.Sin(_age * 1.7f + r.Position.x) * 6f, r.Yaw, Mathf.Cos(_age * 1.3f + r.Position.z) * 6f);
                bool inside = (ship - r.Position).sqrMagnitude <= Radius * Radius;
                if (inside && knots <= MaxKnots) r.Hold += dt;
                else r.Hold = Mathf.Max(0f, r.Hold - dt * 2f);
                float urgent = r.TimeLeft < 15f ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(_age * 6f)) : 1f;
                MissionMarks.Tint(r.Ring, r.Hold > 0f ? new Color(0.35f, 1f, 0.55f, 0.95f)
                                                       : inside ? new Color(1f, 0.9f, 0.3f, 0.95f) : new Color(1f, 0.6f, 0.2f, 0.85f * urgent));
                if (r.Hold >= HoldTime) Rescue(r);
                else if (r.TimeLeft <= 0f) Lose(r);
            }

            var near = Nearest(ship);
            if (near != null) MissionMarks.Segment(_guide, ship, near.Position);
            else if (_guide != null) _guide.positionCount = 0;
            RefreshPanel();
            if (_rafts.TrueForAll(r => !r.Active)) Finish();
        }

        private Raft Nearest(Vector3 from)
        {
            Raft best = null;
            float bestSqr = float.MaxValue;
            foreach (var r in _rafts)
            {
                if (!r.Active) continue;
                float d = (r.Position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = r; }
            }
            return best;
        }

        private void Rescue(Raft r)
        {
            r.Rescued = true;
            RescuedCount++;
            float repair = _player.HullMaxHp * RepairRatio;
            if (_player.IsAlive) _player.RepairHull(repair);
            int xp = _xp != null ? Mathf.Max(1, Mathf.CeilToInt(_xp.XpToNext * XpRatio)) : 0;
            if (_xp != null) _xp.AddXp(xp);
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner($"표류자 구조 ({RescuedCount}/{_rafts.Count}) — 선체 수리 +{repair:0} · 경험치 +{xp}");
            Clear(r);
        }

        private void Lose(Raft r)
        {
            r.Lost = true;
            LostCount++;
            UiSound.Play(UiSound.Cue.Fail);
            GameEvents.RaiseBanner($"구명정 한 척이 가라앉았습니다 — 남은 조난 신호 {_rafts.FindAll(x => x.Active).Count}");
            Clear(r);
        }

        private static void Clear(Raft r)
        {
            if (r.Root != null) Destroy(r.Root.gameObject);
            if (r.Ring != null) Destroy(r.Ring.gameObject);
            if (r.Beacon != null) Destroy(r.Beacon.gameObject);
        }

        private void Finish()
        {
            if (State != Outcome.Running) return;
            State = Outcome.Finished;
            if (RescuedCount == _rafts.Count && RescuedCount > 0)
            {
                AllRescuedBonus = true;
                Game.Refit.RefitDraft.GrantReroll();
                UiSound.Play(UiSound.Cue.Victory);
                GameEvents.RaiseBanner($"구조 활동 완료 — 전원 구조 {RescuedCount}/{_rafts.Count} · 다시 뽑기 +1");
            }
            else GameEvents.RaiseBanner($"구조 활동 종료 — 구조 {RescuedCount}/{_rafts.Count}");
            Close();
        }

        public void Cancel()
        {
            if (State != Outcome.Running) return;
            State = Outcome.Cancelled;
            Close();
        }

        private void Close()
        {
            foreach (var r in _rafts) Clear(r);
            _panel?.Destroy();
            Destroy(gameObject, 0.1f);
        }

        private void OnDestroy() => _panel?.Destroy();

        /// <summary>검증용: 남은 저체온 시간을 바꾼다.</summary>
        public void DevSetTimeLeft(int index, float seconds)
        {
            if (index >= 0 && index < _rafts.Count) _rafts[index].TimeLeft = seconds;
        }

        private void RefreshPanel()
        {
            if (_panel == null || _player == null) return;
            Vector3 ship = MissionMarks.Flat(_player.transform.position);
            var near = Nearest(ship);
            int left = _rafts.FindAll(r => r.Active).Count;
            string head = "<color=#FFB060>임무</color>  구조 활동";
            if (near == null) { _panel.Set($"{head}   ·   구조 {RescuedCount}/{_rafts.Count}", 1f, new Color(0.35f, 0.9f, 0.55f)); return; }
            float dist = (near.Position - ship).magnitude;
            string body = near.Hold > 0f
                ? $"<b>구조 중</b> {near.Hold:0.0}/{HoldTime:0}초 — 그대로 머무세요"
                : dist <= Radius ? $"<color=#FF7A6A>{MaxKnots:0}노트 이하로 줄이세요</color>"
                : $"가장 가까운 <b>{dist:0}m</b>";
            string time = near.TimeLeft < 15f ? $"<color=#FF7A6A>저체온 {near.TimeLeft:0}초</color>" : $"저체온 {near.TimeLeft:0}초";
            _panel.Set($"{head}   남은 표류자 {left}   ·   {body}   ·   {time}",
                near.Hold > 0f ? near.Hold / HoldTime : near.TimeLeft / 95f,
                near.Hold > 0f ? new Color(0.35f, 1f, 0.55f) : near.TimeLeft < 15f ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.62f, 0.2f));
        }
    }
}
