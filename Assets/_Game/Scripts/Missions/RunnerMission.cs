using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.Enemies;
using Game.View;

namespace Game.Missions
{
    /// <summary>
    /// 전장 이벤트 "적 보급선 추격"(2026-11-17). 달아나는 고가치 표적을 쫓게 해서 기함을 움직이게 한다.
    ///   - 기함 옆쪽 SpawnDistance에 적 보급선이 나타나 기함에서 멀어지는 쪽으로 RouteLength 항로를 Speed로 달아난다
    ///     (기함 최고속력보다 느리다). 호위 고속정 둘이 따라 나온다.
    ///   - 달아나며 MineEvery초마다 함미로 부유 기뢰(ene_sea_mine)를 떨군다 — 뒤를 그대로 따라가면 기뢰밭, 옆으로 앞질러야 한다.
    ///   - 격침: 연쇄 폭발과 함께 선체 수리 RepairRatio · 정비 1회(다음 레벨까지 경험치를 채운다).
    ///   - 항로 끝에 닿으면 도주 — 실패(보상 없음). 붉은 표식 원 · 기함에서 보급선까지 붉은 선 · 화면 위 막대(남은 도주 거리 · 적 선체).
    /// </summary>
    public sealed class RunnerMission : MonoBehaviour
    {
        public const float Speed = 9f;
        public const float RouteLength = 650f;
        public const float SpawnDistance = 120f;
        public const float MineEvery = 4.5f, MineRange = 150f;
        public const int MaxOwnMines = 6;
        public const float RepairRatio = 0.25f;
        public const float BaseHp = 200f, HpPerStage = 90f;

        public enum Outcome { Running, Sunk, Escaped, Cancelled }

        private Game.Ship.ShipController _player;
        private Game.Progression.ExperienceSystem _xp;
        private EnemyDefinition _mineDef;
        private RunnerShip _ship;
        private float _mineTimer;
        private readonly List<SeaMine> _mines = new();
        private LineRenderer _guide, _mark;
        private MissionMarks.Panel _panel;

        public Outcome State { get; private set; } = Outcome.Running;
        public RunnerShip Ship => _ship;
        public int MinesLaid { get; private set; }
        public int RewardXp { get; private set; }
        public string PanelText => _panel != null && _panel.Label != null ? _panel.Label.text ?? "" : "";

        public static RunnerMission Begin(Game.Ship.ShipController player, Game.Progression.ExperienceSystem xp, int stage,
                                          EnemyDefinition mineDef, EnemyDefinition escortDef, float routeLength = RouteLength)
        {
            if (player == null) return null;
            var m = new GameObject("Runner mission").AddComponent<RunnerMission>();
            m._player = player;
            m._xp = xp;
            m._mineDef = mineDef;
            if (!m.Spawn(stage, routeLength, escortDef)) { Destroy(m.gameObject); return null; }
            m._guide = MissionMarks.Line(m.transform, "Runner guide", new Color(1f, 0.3f, 0.25f, 0.6f), 0.7f, false);
            m._mark = MissionMarks.Line(m.transform, "Runner mark", new Color(1f, 0.3f, 0.25f, 0.9f), 1f, true);
            m._panel = MissionMarks.Panel.Create("Runner panel", -212f, 600f, new Color(1f, 0.78f, 0.72f));
            m._mineTimer = MineEvery;
            UiSound.Play(UiSound.Cue.Confirm);
            GameEvents.RaiseBanner("적 보급선 발견 — 달아나기 전에 격침하세요(선체 수리 · 정비 1회). 뒤에 기뢰를 뿌립니다: 옆으로 앞지르세요");
            m.Refresh();
            return m;
        }

        private bool Spawn(int stage, float routeLength, EnemyDefinition escortDef)
        {
            var t = _player.transform;
            Vector3 fwd = MissionMarks.Flat(t.forward).normalized;
            if (fwd.sqrMagnitude < 0.5f) fwd = Vector3.forward;
            float side = Random.value < 0.5f ? -1f : 1f;
            Vector3 start = Vector3.zero, dir = fwd;
            bool found = false;
            // 기함 옆 비스듬히 앞(±40~100°) — 막혔으면 각도를 바꿔 본다
            for (int k = 0; k < 14 && !found; k++)
            {
                float a = side * (40f + (k % 7) * 10f) * (k >= 7 ? -1f : 1f);
                dir = Quaternion.Euler(0f, a, 0f) * fwd;
                var p = MissionMarks.Flat(t.position) + dir * SpawnDistance;
                if (Game.World.SeaRegion.IsStrait)
                {
                    p = Game.World.SeaRegion.ChannelPoint(Game.World.SeaRegion.Along(p), Mathf.Clamp(Game.World.SeaRegion.Lateral(p), -30f, 30f));
                    p.y = 0f;
                }
                if (MissionMarks.OpenWater(p, 14f)) { start = p; found = true; }
            }
            if (!found) return false;
            var route = Game.TaskForce.SupplyEscortMission.PlanRoute(start, dir, routeLength, RunSeed.Current + Mathf.RoundToInt(start.x * 11f + start.z * 3f));
            var first = route.Count > 1 ? MissionMarks.Flat(route[1] - route[0]) : dir;
            float hp = BaseHp + HpPerStage * Mathf.Max(0, stage - 1);
            _ship = RunnerShip.Create(start, Mathf.Atan2(first.x, first.z) * Mathf.Rad2Deg, hp, this);
            _ship.SetRoute(route);

            if (escortDef != null && EnemySpawner.Instance != null)
            {
                var right = new Vector3(first.z, 0f, -first.x).normalized;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = start + right * (i == 0 ? 12f : -12f) - first.normalized * 6f;
                    if (MissionMarks.OpenWater(p, 6f)) EnemySpawner.Instance.SpawnAt(escortDef, p, Quaternion.LookRotation(first.normalized, Vector3.up));
                }
            }
            return true;
        }

        private void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.IsRunOver || _player == null) { Cancel(); return; }
            if (gm.State != GameState.Playing || State != Outcome.Running) return;
            if (_ship == null) { Cancel(); return; }
            if (_ship.Sunk) { Sink(); return; }
            if (_ship.Arrived) { Escape(); return; }

            _mineTimer -= Time.deltaTime;
            if (_mineTimer <= 0f)
            {
                _mineTimer = MineEvery;
                TryDropMine();
            }
            Refresh();
        }

        private void TryDropMine()
        {
            if (_mineDef == null || EnemySpawner.Instance == null || _ship == null) return;
            _mines.RemoveAll(x => x == null || !x.IsAlive);
            if (_mines.Count >= MaxOwnMines || MineLayer.MinesAlive >= MineLayer.GlobalMineCap) return;
            if (MissionMarks.Flat(_ship.transform.position - _player.transform.position).sqrMagnitude > MineRange * MineRange) return;
            Vector3 p = _ship.transform.position - _ship.transform.forward * 8.5f;
            p.y = 0f;
            var mine = EnemySpawner.Instance.SpawnAt(_mineDef, p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)) as SeaMine;
            if (mine == null) return;
            _mines.Add(mine);
            MineLayer.TrackMine(mine);
            MinesLaid++;
            AudioManager.Play(SfxId.DepthDrop, p, 0.8f, 0.85f);
        }

        private void Sink()
        {
            State = Outcome.Sunk;
            float repair = _player.HullMaxHp * RepairRatio;
            if (_player.IsAlive) _player.RepairHull(repair);
            UiSound.Play(UiSound.Cue.Victory);
            GameEvents.RaiseBanner($"적 보급선 격침 — 선체 수리 +{repair:0} · 정비 1회");
            if (_xp != null)
            {
                RewardXp = Mathf.Max(1, _xp.XpToNext - _xp.Xp);
                _xp.AddXp(RewardXp);   // 정비 1회: 다음 레벨까지 채운다(보급선 호위 보상과 같은 방식)
            }
            Close();
        }

        private void Escape()
        {
            State = Outcome.Escaped;
            _ship.Depart();
            UiSound.Play(UiSound.Cue.Fail);
            GameEvents.RaiseBanner("적 보급선 도주 — 임무 실패(보상 없음)");
            Close();
        }

        public void Cancel()
        {
            if (State != Outcome.Running) return;
            State = Outcome.Cancelled;
            if (_ship != null) Destroy(_ship.gameObject);
            Close();
        }

        private void Close()
        {
            _panel?.Destroy();
            if (_guide != null) Destroy(_guide.gameObject);
            if (_mark != null) Destroy(_mark.gameObject);
            Destroy(gameObject, 0.1f);
        }

        private void OnDestroy() => _panel?.Destroy();

        private void Refresh()
        {
            if (_ship == null || _player == null) return;
            Vector3 a = MissionMarks.Flat(_player.transform.position), b = MissionMarks.Flat(_ship.transform.position);
            float dist = (b - a).magnitude;
            if (dist > 45f) MissionMarks.Segment(_guide, a, b);
            else if (_guide != null) _guide.positionCount = 0;
            MissionMarks.Ring(_mark, b, 11f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 1.5f, 32);
            if (_panel == null) return;
            float hull = _ship.Hull01;
            float remain = _ship.RemainingDistance;
            string run = remain < 120f ? $"<color=#FF7A6A>도주까지 {remain:0}m</color>" : $"도주까지 <b>{remain:0}m</b>";
            _panel.Set($"<color=#FF7A6A>임무</color>  적 보급선 격침   {run}   ·   적 선체 {hull * 100f:0}%   ·   거리 {dist:0}m",
                       hull, new Color(1f, 0.38f, 0.3f));
        }

        // ------------------------------------------------------------ 적 보급선

        /// <summary>
        /// 달아나는 적 보급선 한 척: 적 수상 표적(ITargetable) · 피해 대상(IDamageable). 몸체는 Enemy 레이어라 아군 탄에 맞는다.
        /// 항로 꺾은 점을 차례로 배처럼(선회율 한계) 따라가고, 앞 섬은 비켜 돈다. 모델은 보급선 호위의 화물선을 붉게 물들여 쓴다.
        /// </summary>
        public sealed class RunnerShip : MonoBehaviour, ITargetable, IDamageable, IHasVelocity
        {
            private const float TurnRate = 32f, Accel = 2.2f, WaypointRadius = 12f;
            private const float VisualScale = 0.75f;

            private RunnerMission _mission;
            private List<Vector3> _route = new();
            private int _index = 1;
            private float _hp, _maxHp, _heading, _speed, _sinkAge, _nextBlast;
            private int _blasts;
            private bool _sinking, _departing;
            private BoxCollider _hull;
            private Transform _visual;

            public Transform Transform => transform;
            public TargetKind Kind => TargetKind.Surface;
            public CombatFaction Faction => CombatFaction.Hostile;
            public bool IsAlive => this != null && isActiveAndEnabled && !_sinking && !_departing && _hp > 0f;
            public bool IsRevealed => true;
            public bool Sunk => _sinking;
            public float Hull01 => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;
            public float MaxHp => _maxHp;
            public bool Arrived => _route.Count > 0 && _index >= _route.Count;
            public float CurrentSpeed => _speed;
            /// <summary>아군 무장이 앞을 겨누도록(리드 사격).</summary>
            public Vector3 Velocity => IsAlive ? transform.forward * _speed : Vector3.zero;

            public float RemainingDistance
            {
                get
                {
                    if (Arrived || _route.Count == 0) return 0f;
                    float d = MissionMarks.Flat(_route[_index] - transform.position).magnitude;
                    for (int i = _index; i < _route.Count - 1; i++) d += MissionMarks.Flat(_route[i + 1] - _route[i]).magnitude;
                    return d;
                }
            }

            public static RunnerShip Create(Vector3 position, float heading, float hp, RunnerMission mission)
            {
                var root = new GameObject("Enemy supply ship");
                root.layer = Factions.EnemyLayer;
                root.SetActive(false);
                root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
                var ship = root.AddComponent<RunnerShip>();
                ship._mission = mission;
                ship._heading = heading;
                ship._hp = ship._maxHp = hp;
                ship._speed = Speed * 0.6f;
                ship._hull = root.AddComponent<BoxCollider>();
                ship._hull.size = new Vector3(4.4f, 3f, 16f) * VisualScale;
                ship._hull.center = new Vector3(0f, 1f, 0f) * VisualScale;
                ship._visual = BuildModel(root.transform);
                ship._visual.localScale = Vector3.one * VisualScale;
                var waterline = FindDeep(ship._visual, "Waterline");
                if (waterline != null) ship._visual.position += Vector3.up * (root.transform.position.y + Waterline.WaterY - waterline.position.y);
                ShipWake.Ensure(root, () => GameManager.Instance != null && GameManager.Instance.State == GameState.Playing ? 1f : 0f);
                root.SetActive(true);
                Waterline.Settle(root.transform, ship._visual);
                return ship;
            }

            private static Transform FindDeep(Transform t, string name)
            {
                if (t.name == name) return t;
                foreach (Transform c in t) { var f = FindDeep(c, name); if (f != null) return f; }
                return null;
            }

            /// <summary>보급선 호위의 화물선 모델을 적 도색(붉은 기)으로. 없으면 회색 박스 화물선.</summary>
            private static Transform BuildModel(Transform root)
            {
                Transform model;
                var art = Resources.Load<GameObject>("TaskForce/SupplyShip");
                if (art != null)
                {
                    model = Instantiate(art, root, false).transform;
                    model.name = "Enemy supply visual";
                    foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = root.gameObject.layer;
                    foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
                    var tint = new MaterialPropertyBlock();
                    foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        r.GetPropertyBlock(tint);
                        tint.SetColor("_BaseColor", new Color(0.78f, 0.52f, 0.48f));
                        r.SetPropertyBlock(tint);
                    }
                }
                else
                {
                    model = new GameObject("Enemy supply visual").transform;
                    model.SetParent(root, false);
                    MissionMarks.Prop(model, PrimitiveType.Cube, "Hull", new Vector3(0f, 0.2f, 0f), new Vector3(4.2f, 2.2f, 15.5f), new Color(0.3f, 0.3f, 0.3f));
                    MissionMarks.Prop(model, PrimitiveType.Cube, "Bridge", new Vector3(0f, 2.4f, -5.6f), new Vector3(3.6f, 2.5f, 2.6f), new Color(0.7f, 0.68f, 0.64f));
                    for (int i = 0; i < 4; i++)
                        MissionMarks.Prop(model, PrimitiveType.Cube, "Cargo", new Vector3(0f, 1.9f, -2.4f + i * 2.6f), new Vector3(3.4f, 1.2f, 2.3f), new Color(0.55f, 0.2f, 0.16f));
                }
                // 붉은 기: 적 보급선 표식
                MissionMarks.Prop(model, PrimitiveType.Cube, "Enemy flag pole", new Vector3(0f, 6.2f, -6.2f), new Vector3(0.12f, 3.2f, 0.12f), new Color(0.15f, 0.15f, 0.15f));
                MissionMarks.Prop(model, PrimitiveType.Cube, "Enemy flag", new Vector3(0f, 7.3f, -6.95f), new Vector3(0.06f, 0.9f, 1.4f), new Color(0.85f, 0.12f, 0.1f));
                return model;
            }

            public void SetRoute(List<Vector3> route) { _route = route; _index = Mathf.Min(1, route.Count); }

            private void OnEnable() { if (IsAlive) TargetRegistry.Register(this); }
            private void OnDisable() => Release();

            private void Release()
            {
                TargetRegistry.Unregister(this);
                TargetAllocator.ReleaseTarget(transform);
            }

            public void TakeDamage(in DamageInfo info)
            {
                if (!IsAlive) return;
                _hp -= info.Amount;
                if (_hp > 0f) return;
                _hp = 0f;
                _sinking = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Explosions.Spawn(transform.position + Vector3.up * 1.5f, 2.2f, Explosions.Kind.Impact);
                AudioManager.Play(SfxId.WreckBlast, transform.position, 1f, 0.9f);
                _nextBlast = 0.35f;
            }

            /// <summary>도주 성공: 표적에서 빠지고 그대로 가서 사라진다.</summary>
            public void Depart()
            {
                _departing = true;
                if (_hull != null) _hull.enabled = false;
                Release();
                Destroy(gameObject, 10f);
            }

            private void Update()
            {
                if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing && !_sinking) return;
                float dt = Time.deltaTime;
                if (_sinking)
                {
                    // 화물이 연쇄로 터지며 가라앉는다
                    _sinkAge += dt;
                    if (_blasts < 4 && _sinkAge >= _nextBlast)
                    {
                        _blasts++;
                        _nextBlast = _sinkAge + Random.Range(0.3f, 0.55f);
                        Vector3 at = transform.position + transform.forward * Random.Range(-5f, 5f) + transform.right * Random.Range(-1.5f, 1.5f) + Vector3.up * 1.6f;
                        Explosions.Spawn(at, Random.Range(1.2f, 1.9f), Explosions.Kind.Impact);
                        AudioManager.Play(SfxId.Explosion, at, 0.8f, Random.Range(0.85f, 1.1f));
                    }
                    transform.position += Vector3.down * (0.6f * dt);
                    if (_visual != null) _visual.localRotation = Quaternion.Euler(Mathf.Min(10f, _sinkAge * 2.5f), 0f, Mathf.Min(26f, _sinkAge * 6f));
                    if (_sinkAge > 7f) Destroy(gameObject);
                    return;
                }

                float desired = _heading, wantSpeed = Speed;
                if (!_departing && !Arrived)
                {
                    var to = MissionMarks.Flat(_route[_index] - transform.position);
                    if (to.magnitude <= WaypointRadius && _index < _route.Count) { _index++; if (Arrived) return; to = MissionMarks.Flat(_route[_index] - transform.position); }
                    desired = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    var fwd = Quaternion.Euler(0f, _heading, 0f) * Vector3.forward;
                    if (Game.World.Islands.ProbeAhead(transform.position, fwd, 4f, 22f, out var hit))
                    {
                        var away = MissionMarks.Flat(hit.normal);
                        if (away.sqrMagnitude > 1e-4f) desired = Mathf.Atan2(away.x + fwd.x, away.z + fwd.z) * Mathf.Rad2Deg;
                    }
                    float turn = Mathf.Abs(Mathf.DeltaAngle(_heading, desired));
                    wantSpeed = Speed * Mathf.Lerp(1f, 0.5f, Mathf.Clamp01((turn - 15f) / 60f));
                }
                _heading = Mathf.MoveTowardsAngle(_heading, desired, TurnRate * dt * Mathf.Lerp(0.4f, 1f, _speed / Speed));
                _speed = Mathf.MoveTowards(_speed, wantSpeed, Accel * dt);
                var rot = Quaternion.Euler(0f, _heading, 0f);
                Vector3 p = transform.position + rot * Vector3.forward * (_speed * dt);
                p.y = 0f;
                if (_hull != null && Game.World.Islands.ResolveOverlap(_hull, ref p, rot, out _)) _speed *= 0.6f;
                if (Game.World.SeaRegion.ClampToChannel(ref p, -2f)) _speed *= 0.6f;
                transform.SetPositionAndRotation(p, rot);
            }
        }
    }
}
