using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.View;

namespace Game.Missions
{
    /// <summary>
    /// 위치 노출 게이지(2026-11-17). 한 해역에 오래 머물면 적 관측에 위치가 잡혀 포격이 쏟아진다 — 어느 스테이지에서든 제자리 전투를 막는다.
    ///   - 기준점에서 Radius 안에 있는 동안 FillTime초에 걸쳐 차오른다. 기준점에서 Radius를 벗어나면 기준점을 지금 자리로 옮기고
    ///     MoveDrain만큼 줄어든다(빠르게 다니면 금방 바닥, 느릿느릿 기면 조금씩 찬다).
    ///   - 다 차면 집중 포격: Shells발이 ShellGap초 간격으로, 각각 WarnTime초 전에 수면에 붉은 착탄 원을 띄운 뒤 떨어진다.
    ///     첫 발은 기함의 예상 위치, 나머지는 둘레 흩뿌림. 원 안(ShellRadius)의 기함 선체에 ShellDamage(+스테이지마다 늘어남).
    ///     포격이 시작되면 게이지는 AfterBarrage로 내려간다.
    ///   - 스테이지 시작 뒤 Grace초는 차지 않는다. 화면 아래 가운데(경험치 막대 위)에 Show 이상일 때만 막대가 보인다.
    /// </summary>
    public sealed class ExposureGauge : MonoBehaviour
    {
        public const float Radius = 28f, FillTime = 14f, MoveDrain = 0.35f;
        public const float Grace = 20f, Show = 0.3f, AfterBarrage = 0.15f;
        public const int Shells = 5;
        public const float ShellGap = 0.35f, WarnTime = 2.2f, ShellRadius = 7f;
        public const float ShellDamage = 10f, ShellDamagePerStage = 4f;

        private Game.Ship.ShipController _player;
        private int _stage = 1;
        private Vector3 _anchor;
        private float _grace;
        private MissionMarks.Panel _panel;
        private readonly List<Shell> _shells = new();

        private sealed class Shell
        {
            public Vector3 At;
            public float Delay, Age;
            public LineRenderer Ring;
            public Transform Body;
            public bool Fired;
        }

        /// <summary>0~1.</summary>
        public float Value { get; private set; }
        public int Barrages { get; private set; }
        public int ShellHits { get; private set; }
        public int ShellsLanded { get; private set; }
        public bool Running => _player != null && enabled;
        public string PanelText => _panel != null && _panel.Label != null ? _panel.Label.text ?? "" : "";

        /// <summary>검증용: 차오르는 빠르기 배율(1 = FillTime).</summary>
        [System.NonSerialized] public float DevFillScale = 1f;

        public void Begin(Game.Ship.ShipController player, int stage)
        {
            _player = player;
            _stage = Mathf.Max(1, stage);
            _anchor = player != null ? MissionMarks.Flat(player.transform.position) : Vector3.zero;
            _grace = Grace;
            Value = 0f;
            ClearShells();
            enabled = true;
            // 화면 아래 가운데 경험치 막대 위(기함 둘레를 가리지 않게)
            if (_panel == null) _panel = MissionMarks.Panel.Create("Exposure panel", 104f, 440f, new Color(1f, 0.85f, 0.75f), true, true);
            _panel?.Show(false);
        }

        public void Stop()
        {
            _player = null;
            ClearShells();
            _panel?.Show(false);
        }

        /// <summary>검증용: 유예 시간을 없앤다.</summary>
        public void DevSkipGrace() => _grace = 0f;

        private void OnDestroy()
        {
            ClearShells();
            _panel?.Destroy();
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (_player == null || gm == null) return;
            if (gm.IsRunOver || !_player.IsAlive) { ClearShells(); _panel?.Show(false); return; }
            if (gm.State != GameState.Playing) return;
            float dt = Time.deltaTime;

            TickShells(dt);

            Vector3 pos = MissionMarks.Flat(_player.transform.position);
            if ((pos - _anchor).sqrMagnitude > Radius * Radius)
            {
                _anchor = pos;
                Value = Mathf.Max(0f, Value - MoveDrain);
            }
            if (_grace > 0f) _grace -= dt;
            else if (_shells.Count == 0) Value = Mathf.Min(1f, Value + dt / FillTime * DevFillScale);

            if (Value >= 1f && _shells.Count == 0) Barrage();
            RefreshPanel();
        }

        private void Barrage()
        {
            Barrages++;
            Value = AfterBarrage;
            var t = _player.transform;
            Vector3 v = MissionMarks.Flat(t.forward) * _player.CurrentSpeed;
            Vector3 center = MissionMarks.Flat(t.position);
            for (int i = 0; i < Shells; i++)
            {
                float delay = i * ShellGap;
                Vector3 at = center + v * (WarnTime + delay);
                if (i > 0) at += Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(5f, 13f);
                var s = new Shell { At = at, Delay = delay };
                s.Ring = MissionMarks.Line(transform, "Exposure shell ring", new Color(1f, 0.2f, 0.15f, 0f), 0.8f, true);
                _shells.Add(s);
            }
            UiSound.Play(UiSound.Cue.Fail);
            GameEvents.RaiseBanner("위치 노출 — 적 포격이 떨어집니다! 붉은 원 밖으로, 한곳에 머물지 마세요");
        }

        private void TickShells(float dt)
        {
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var s = _shells[i];
                s.Age += dt;
                float t = s.Age - s.Delay;
                if (t < 0f) continue;
                float k = Mathf.Clamp01(t / WarnTime);
                // 착탄 원: 크게 떠서 착탄 지름으로 좁혀 들어온다
                MissionMarks.Ring(s.Ring, s.At, Mathf.Lerp(ShellRadius * 1.6f, ShellRadius, k), 36);
                MissionMarks.Tint(s.Ring, new Color(1f, 0.2f, 0.15f, 0.35f + 0.6f * k));
                // 마지막 0.6초: 하늘에서 포탄이 내리꽂힌다
                if (t > WarnTime - 0.6f)
                {
                    if (s.Body == null) s.Body = MissionMarks.Prop(transform, PrimitiveType.Capsule, "Exposure shell", Vector3.zero, new Vector3(0.45f, 0.9f, 0.45f), new Color(0.12f, 0.12f, 0.12f));
                    float f = Mathf.Clamp01((t - (WarnTime - 0.6f)) / 0.6f);
                    s.Body.position = s.At + Vector3.up * Mathf.Lerp(55f, 0.5f, f);
                }
                if (t >= WarnTime && !s.Fired) Land(s);
                if (s.Fired) { Remove(s); _shells.RemoveAt(i); }
            }
        }

        private void Land(Shell s)
        {
            s.Fired = true;
            ShellsLanded++;
            bool hit = false;
            if (_player != null && _player.IsAlive && _player.TryGetComponent<Collider>(out var hull))
            {
                Vector3 closest = hull.ClosestPoint(s.At);
                Vector3 flat = MissionMarks.Flat(closest - s.At);
                if (flat.sqrMagnitude <= ShellRadius * ShellRadius && _player.TryGetComponent<IDamageable>(out var dmg) && dmg.IsAlive)
                {
                    dmg.TakeDamage(new DamageInfo(ShellDamage + ShellDamagePerStage * (_stage - 1), closest, Vector3.down, DamageSource.Gun, "위치 노출 포격"));
                    hit = true;
                    ShellHits++;
                }
            }
            Explosions.Spawn(new Vector3(s.At.x, 0f, s.At.z), hit ? 1.6f : 1.3f, hit ? Explosions.Kind.Impact : Explosions.Kind.Water);
            AudioManager.Play(hit ? SfxId.HullHitShell : SfxId.ShellSplash, s.At, 0.9f, 0.8f);
        }

        private static void Remove(Shell s)
        {
            if (s.Ring != null) Destroy(s.Ring.gameObject);
            if (s.Body != null) Destroy(s.Body.gameObject);
        }

        private void ClearShells()
        {
            foreach (var s in _shells) Remove(s);
            _shells.Clear();
        }

        private void RefreshPanel()
        {
            if (_panel == null) return;
            bool firing = _shells.Count > 0;
            bool show = firing || Value >= Show;
            _panel.Show(show);
            if (!show) return;
            string text = firing
                ? "<color=#FF6A5A><b>위치 노출 — 포격 낙하!</b></color>  붉은 원 밖으로"
                : Value >= 0.75f ? $"<color=#FF8A6A>위치 노출 {Value * 100f:0}%</color>  — 곧 포격, 자리를 옮기세요"
                : $"위치 노출 {Value * 100f:0}%  — 계속 움직이세요";
            _panel.Set(text, firing ? 1f : Value, firing ? new Color(1f, 0.3f, 0.2f)
                : Color.Lerp(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.2f), Mathf.InverseLerp(0.5f, 1f, Value)));
        }
    }
}
