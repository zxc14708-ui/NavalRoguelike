using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Data;
using Game.World;

namespace Game.Enemies
{
    /// <summary>
    /// 2스테이지 보스전 무대: 수로 출구 적 군항(2026-11-15). 좁은 수로(폭 124m)에서는 보스 초계함이 선회 원을 그리다 기슭에 막혀
    /// 공격 자세를 못 잡았다. 보스 구간이 시작되면:
    ///   1. 진입: 기함이 향한 쪽 물길 앞에 군항을 연다(만 입구가 약 95m 앞, SeaRegion.OpenHarbor → 기슭이 만으로 벌어지고 안벽·부두가 선다).
    ///      새 적은 잠시 나오지 않는다(남은 적은 그대로). 수면에 붉은 진입 선 · 만 입구 고리.
    ///   2. 출항: 기함이 만 입구에서 18m 들어오거나 60초가 지나면 보스가 두 돌제 사이에서, 호위정이 부두 옆에서 출항한다.
    ///      안벽 양 끝 해안포대 2기. 새 적 생성 재개.
    ///   3. 전투: 만(지름 약 240m) 안에서 싸운다. 수로로 물러나도 보스는 따라온다(EnemyController.KeepInWater가 기슭을 피함).
    /// 스테이지 전환(EnemySpawner.ClearAll)에서 표시를 치운다. 군항 지형은 다음 스테이지 해역이 바뀔 때 함께 사라진다.
    /// </summary>
    public sealed class HarborAssault : MonoBehaviour
    {
        public const float ApproachDistance = 95f;
        public const float ArriveDepth = 18f;
        public const float MaxWait = 60f;
        private const float WaterLine = 0.12f;

        public enum Step { None, Approach, Battle }

        public Step State { get; private set; }
        public EnemyController Boss { get; private set; }
        public readonly List<EnemyController> Batteries = new();
        /// <summary>검증용: 기다리다 시간이 다 돼 출항했는가.</summary>
        public bool TimedOut { get; private set; }
        public float Waited => _elapsed;

        private EnemySpawner _spawner;
        private RoundSet.Round _phase;
        private Transform _player;
        private float _elapsed, _nextReminder;
        private LineRenderer _route, _ring;
        private static Material s_line;

        public static bool Wants(RoundSet.Round phase) => phase.Boss != null && SeaRegion.IsStrait;

        /// <summary>만 입구에서 ArriveDepth 들어온 곳(진입 판정 · 표시).</summary>
        public static Vector3 ArrivePoint => SeaRegion.HarborPoint(SeaRegion.HarborEntranceT + ArriveDepth, 0f);

        public void Begin(EnemySpawner spawner, RoundSet.Round phase, Transform player)
        {
            Cancel();
            _spawner = spawner;
            _phase = phase;
            _player = player;
            _elapsed = 0f;
            TimedOut = false;
            Boss = null;
            Batteries.Clear();

            // 기함이 물길 축의 어느 쪽을 보고 있는지로 군항 방향을 정한다(되돌아가는 중이면 뒤쪽에)
            Vector3 fwd = player != null ? player.forward : SeaRegion.ChannelAxis;
            int dir = Vector3.Dot(fwd, SeaRegion.ChannelAxis) >= 0f ? 1 : -1;
            float along = player != null ? SeaRegion.Along(player.position) : 0f;
            float center = along + dir * (ApproachDistance - SeaRegion.HarborEntranceT);
            SeaRegion.OpenHarbor(center, dir);

            State = Step.Approach;
            spawner.HoldSpawns = true;
            _nextReminder = 18f;
            BuildMarks();
            GameEvents.RaiseBanner("수로 출구 — 앞쪽 적 군항으로 진입하십시오. 만에 들어서면 현대화 초계함이 출항합니다");
        }

        /// <summary>스테이지 전환·검사: 표시를 치우고 멈춘다(군항 지형은 해역이 바뀔 때 사라진다).</summary>
        public void Cancel()
        {
            State = Step.None;
            if (_spawner != null) _spawner.HoldSpawns = false;
            DestroyMarks();
        }

        private void Update()
        {
            if (State != Step.Approach) return;
            var gm = GameManager.Instance;
            if (gm == null || gm.State != GameState.Playing || _player == null) return;
            _elapsed += Time.deltaTime;
            float t = SeaRegion.HarborT(_player.position);
            bool arrived = t >= SeaRegion.HarborEntranceT + ArriveDepth;
            if (arrived || _elapsed >= MaxWait)
            {
                TimedOut = !arrived;
                StartBattle();
                return;
            }
            RefreshMarks();
            if (_elapsed >= _nextReminder)
            {
                _nextReminder += 18f;
                float left = Mathf.Max(0f, SeaRegion.HarborEntranceT + ArriveDepth - t);
                GameEvents.RaiseBanner($"적 군항까지 약 {left:0}m — {Mathf.Max(0f, MaxWait - _elapsed):0}초 뒤에는 초계함이 먼저 출항합니다");
            }
        }

        /// <summary>보스 · 호위정 출항, 해안포대, 새 적 생성 재개.</summary>
        public void StartBattle()
        {
            if (State != Step.Approach) return;
            State = Step.Battle;
            DestroyMarks();
            _spawner.HoldSpawns = false;

            var seaward = Quaternion.LookRotation(-SeaRegion.HarborForward, Vector3.up);
            var spots = new List<Vector3>();
            foreach (var e in HarborBuilder.EscortBerths) spots.Add(HarborBuilder.ToWorld(e));
            Boss = _spawner.SpawnBossAt(_phase, HarborBuilder.ToWorld(HarborBuilder.BossBerth), seaward, spots);

            var battery = CoastalBatteryField.Instance != null ? CoastalBatteryField.Instance.Definition : Resources.Load<EnemyDefinition>("Enemies/ene_coastal_battery");
            if (battery != null)
                foreach (var spot in HarborBuilder.BatterySpots)
                {
                    var e = _spawner.SpawnFixed(battery, HarborBuilder.ToWorld(spot), seaward);
                    if (e != null) Batteries.Add(e);
                }
            GameEvents.RaiseBanner(TimedOut
                ? "적 군항에서 현대화 초계함 출항 — 수로로 다가옵니다"
                : "적 군항 — 현대화 초계함 출항! 안벽 양 끝 해안포대 주의");
        }

        // ------------------------------------------------------------ 수면 표시(진입 선 · 만 입구 고리)

        private void BuildMarks()
        {
            _route = Line("Harbor approach line", new Color(1f, 0.35f, 0.25f, 0.75f), 1.1f, false);
            _ring = Line("Harbor entrance ring", new Color(1f, 0.35f, 0.25f, 0.85f), 1.3f, true);
            const int n = 48;
            _ring.positionCount = n;
            Vector3 c = ArrivePoint;
            float radius = SeaRegion.ChannelHalfWidth * 0.55f;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                _ring.SetPosition(i, new Vector3(c.x + Mathf.Sin(a) * radius, WaterLine, c.z + Mathf.Cos(a) * radius));
            }
            RefreshMarks();
        }

        private void RefreshMarks()
        {
            if (_route == null || _player == null) return;
            Vector3 a = _player.position, b = ArrivePoint;
            _route.positionCount = 2;
            _route.SetPosition(0, new Vector3(a.x, WaterLine, a.z));
            _route.SetPosition(1, new Vector3(b.x, WaterLine, b.z));
        }

        private void DestroyMarks()
        {
            if (_route != null) Destroy(_route.gameObject);
            if (_ring != null) Destroy(_ring.gameObject);
            _route = _ring = null;
        }

        private LineRenderer Line(string name, Color color, float width, bool loop)
        {
            if (s_line == null)
            {
                var shader = Shader.Find("Sprites/Default");
                s_line = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit")) { name = "Harbor marks (Runtime)" };
            }
            var go = new GameObject(name);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // 수면에 눕힌다
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = s_line;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width;
            line.loop = loop;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
