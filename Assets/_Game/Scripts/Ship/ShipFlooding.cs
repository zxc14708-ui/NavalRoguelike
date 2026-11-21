using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Modules;

namespace Game.Ship
{
    /// <summary>
    /// 손상 통제 — 구획 침수 · 기울기(2026-11-21 1단계, Docs/SESSION_LOG_DAMAGE_PLAN_2026-11-21.md 기획 A).
    ///   - 구획: 블록 격자를 길이 방향으로 2~6구획(격벽, 2칸에 하나). 구획마다 좌현 · 우현 물을 따로 센다(각 0~1).
    ///   - 구멍: 수면 아래를 맞으면 그 칸 구획 · 그 현에 구멍 — 어뢰 · 기뢰는 늘, 낮게 날아온 미사일 20%, 낮게 날아온 큰 포탄(8 이상) 10%.
    ///     구멍마다 초당 들어오는 물: 어뢰 5% · 미사일 2.5% · 포탄 1.2%.
    ///   - 막기: 손상 통제반(ShipDamageControl)이 구멍을 하나씩 막는다(1조가 8초, 교전 중 4배 느림).
    ///   - 배수: 구멍이 없는 구획은 초당 1.2%씩(함교가 부서지면 절반). 85% 넘은 구획은 옆 구획으로 넘친다.
    ///   - 효과: 물 합(구획 단위)마다 최고속력 −12%(최소 50%), 우현 − 좌현 물 차이로 구획당 6°(최대 15°) · 앞뒤 3°, 0.35m까지 가라앉음.
    ///     물 70% 초과 또는 15° 넘게 기울면 선체가 초당 1%씩(침몰 진행).
    ///   - 정비 화면에 들어가면 물을 다 빼고 구멍을 다 막는다.
    /// 겉모습: 기함 Model(선체 + 블록 모델)만 기울이고 내린다 — 격자 · 판정 · 조준 좌표는 그대로.
    /// </summary>
    public sealed class ShipFlooding : MonoBehaviour
    {
        /// <summary>끄면 구멍이 생기지 않는다(검사 · 문제 확인용).</summary>
        public static bool Enabled = true;

        public const float TorpedoHole = 0.05f, MissileHole = 0.025f, ShellHole = 0.012f;
        public const float MissileHoleChance = 0.2f, ShellHoleChance = 0.1f, ShellHoleMinDamage = 8f;
        public const float PumpRate = 0.012f, OverflowAt = 0.85f, OverflowRate = 0.01f;
        public const float SpeedPerCompartment = 0.12f, MinSpeed = 0.5f;
        public const float ListPerCompartment = 6f, TrimPerCompartment = 3f, MaxList = 15f, SinkAtFull = 0.35f;
        public const float PatchSeconds = 8f;
        public const float SinkingFlood = 0.7f, SinkingHullPerSecond = 0.01f;

        public sealed class Compartment
        {
            public int MinX, MaxX;
            public float Port, Stbd;  // 0 ~ 1
            public float Along;       // −1(함미) ~ 1(함수)
            public float Water => (Port + Stbd) * 0.5f;
        }

        public sealed class Hole
        {
            public int Compartment;
            public float Side;       // −1 좌현 · 1 우현
            public float Rate;
            public float Patch;      // 0 ~ 1
        }

        private ShipController _ship;
        private ShipGrid _grid;
        private Transform _model;
        private Quaternion _modelRot;
        private Vector3 _modelPos;
        private bool _modelPosed;
        private readonly List<Compartment> _comps = new();
        private readonly List<Hole> _holes = new();
        private int _layoutHash;
        private float _list, _trim, _sink;     // 지금 보이는 값(부드럽게)
        private float _bannerAt = -99f;

        public IReadOnlyList<Compartment> Compartments => _comps;
        public IReadOnlyList<Hole> Holes => _holes;
        /// <summary>차오른 물 합(구획 수 단위, 0 ~ 구획 수).</summary>
        public float FloodedCompartments { get; private set; }
        /// <summary>전체 침수 비율(0 ~ 1).</summary>
        public float FloodFraction => _comps.Count > 0 ? FloodedCompartments / _comps.Count : 0f;
        /// <summary>목표 기울기(도): 좌우(+ = 우현으로 기움) · 앞뒤(+ = 함수가 잠김).</summary>
        public float TargetList { get; private set; }
        public float TargetTrim { get; private set; }
        public float VisibleList => _list;
        public bool Sinking { get; private set; }
        /// <summary>검증용: 생긴 구멍 수 · 막은 구멍 수.</summary>
        public int HolesMade { get; private set; }
        public int HolesPatched { get; private set; }

        private void Awake()
        {
            _ship = GetComponentInParent<ShipController>();
            var resolver = GetComponent<DamageResolver>();
            _grid = resolver != null ? resolver.Grid : null;
            _model = _ship != null ? _ship.transform.Find("Model") : null;
        }

        private void OnEnable()
        {
            DamageResolver.Resolved += OnHit;
            GameEvents.StateChanged += OnState;
        }

        private void OnDisable()
        {
            DamageResolver.Resolved -= OnHit;
            GameEvents.StateChanged -= OnState;
        }

        private void OnState(GameState s)
        {
            if (s == GameState.Refit) DrainAll();
        }

        /// <summary>물을 다 빼고 구멍을 다 막는다(정비 · 새 판).</summary>
        public void DrainAll()
        {
            _holes.Clear();
            foreach (var c in _comps) c.Port = c.Stbd = 0f;
            Apply(0f);
        }

        /// <summary>구획을 다시 나눈다(블록 배치가 바뀌었을 때).</summary>
        private void EnsureCompartments()
        {
            if (_grid == null) return;
            int minX = int.MaxValue, maxX = int.MinValue;
            foreach (var c in _grid.OccupiedCells) { minX = Mathf.Min(minX, c.X); maxX = Mathf.Max(maxX, c.X); }
            if (minX > maxX) { _comps.Clear(); return; }
            int hash = minX * 397 ^ maxX;
            if (hash == _layoutHash && _comps.Count > 0) return;
            _layoutHash = hash;
            int length = maxX - minX + 1;
            int n = Mathf.Clamp(Mathf.CeilToInt(length / 2f), 2, 6);
            var old = new List<Compartment>(_comps);
            _comps.Clear();
            for (int i = 0; i < n; i++)
            {
                int a = minX + Mathf.FloorToInt(i * length / (float)n);
                int b = minX + Mathf.FloorToInt((i + 1) * length / (float)n) - 1;
                float mid = (a + b) * 0.5f;
                _comps.Add(new Compartment { MinX = a, MaxX = Mathf.Max(a, b),
                    Along = length > 1 ? Mathf.Lerp(-1f, 1f, (mid - minX) / (length - 1f)) : 0f });
            }
            for (int i = 0; i < _comps.Count && i < old.Count; i++) { _comps[i].Port = old[i].Port; _comps[i].Stbd = old[i].Stbd; }
            _holes.RemoveAll(h => h.Compartment >= _comps.Count);
        }

        public int CompartmentOf(GridCoord c)
        {
            for (int i = 0; i < _comps.Count; i++) if (c.X >= _comps[i].MinX && c.X <= _comps[i].MaxX) return i;
            return c.X < (_comps.Count > 0 ? _comps[0].MinX : 0) ? 0 : _comps.Count - 1;
        }

        private void OnHit(DamageResolver r, DamageResolver.HitRecord rec)
        {
            if (!Enabled || DamageResolver.DevMeasureOnly || _grid == null || r.Grid != _grid || _ship == null || !_ship.IsAlive) return;
            var info = rec.Info;
            Vector3 dir = info.Direction.sqrMagnitude > 1e-4f ? info.Direction.normalized : Vector3.down;
            bool low = Mathf.Abs(dir.y) < 0.5f;   // 낮게(옆에서) 날아와 수면선 근처를 맞혔다
            float rate = 0f;
            switch (info.Source)
            {
                case DamageSource.Torpedo: rate = TorpedoHole; break;
                case DamageSource.Missile when low && Random.value < MissileHoleChance: rate = MissileHole; break;
                case DamageSource.Gun when low && info.Amount >= ShellHoleMinDamage && Random.value < ShellHoleChance: rate = ShellHole; break;
            }
            if (rate <= 0f) return;
            MakeHole(rec.Cell, rate, dir);
        }

        /// <summary>그 칸에 구멍을 낸다(검사도 쓴다). dir = 맞힌 탄의 진행 방향.</summary>
        public void MakeHole(GridCoord cell, float rate, Vector3 dir)
        {
            EnsureCompartments();
            if (_comps.Count == 0) return;
            Vector3 local = _grid.CoordToLocal(cell);
            float side = Mathf.Abs(local.x) > 0.1f ? Mathf.Sign(local.x) : -Mathf.Sign(_grid.transform.InverseTransformDirection(dir).x);
            if (side == 0f) side = 1f;
            int comp = CompartmentOf(cell);
            _holes.Add(new Hole { Compartment = comp, Side = side, Rate = rate });
            HolesMade++;
            CombatLog.Add("침수", $"구획 {comp + 1}/{_comps.Count} {(side > 0 ? "우현" : "좌현")} 구멍 · 초당 {rate * 100f:0.#}%");
            if (Time.time - _bannerAt > 8f)
            {
                _bannerAt = Time.time;
                GameEvents.RaiseBanner($"침수 — {(side > 0 ? "우현" : "좌현")} 구획 {comp + 1}에 물이 찹니다(손상 통제반이 막는 중)");
            }
        }

        /// <summary>손상 통제반이 구멍 하나를 막는다(amount = 이번 프레임 진행, 1이면 다 막힘). 다 막았으면 true.</summary>
        public bool Work(Hole h, float amount)
        {
            if (h == null || !_holes.Contains(h)) return false;
            h.Patch += amount;
            if (h.Patch < 1f) return false;
            _holes.Remove(h);
            HolesPatched++;
            CombatLog.Add("침수", $"구멍 막음 · 남은 {_holes.Count}");
            return true;
        }

        private bool BridgeAlive()
        {
            if (_grid == null) return false;
            foreach (var m in _grid.Modules)
                if (m != null && m.IsOperational && m.Definition != null && m.Definition.Type == ModuleType.Bridge) return true;
            return false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _ship == null) return;
            EnsureCompartments();
            if (_comps.Count == 0) return;

            foreach (var h in _holes)
            {
                var c = _comps[h.Compartment];
                if (h.Side > 0f) c.Stbd = Mathf.Min(1f, c.Stbd + h.Rate * dt);
                else c.Port = Mathf.Min(1f, c.Port + h.Rate * dt);
            }

            // 배수 · 넘침
            float pump = PumpRate * (BridgeAlive() ? 1f : 0.5f) * dt;
            for (int i = 0; i < _comps.Count; i++)
            {
                var c = _comps[i];
                bool holed = false;
                foreach (var h in _holes) if (h.Compartment == i) { holed = true; break; }
                if (!holed) { c.Port = Mathf.Max(0f, c.Port - pump); c.Stbd = Mathf.Max(0f, c.Stbd - pump); }
                if (c.Water > OverflowAt)
                {
                    float flow = OverflowRate * dt;
                    for (int n = i - 1; n <= i + 1; n += 2)
                    {
                        if (n < 0 || n >= _comps.Count) continue;
                        _comps[n].Port = Mathf.Min(1f, _comps[n].Port + flow * 0.25f);
                        _comps[n].Stbd = Mathf.Min(1f, _comps[n].Stbd + flow * 0.25f);
                    }
                    c.Port = Mathf.Max(0f, c.Port - flow * 0.5f);
                    c.Stbd = Mathf.Max(0f, c.Stbd - flow * 0.5f);
                }
            }
            Apply(dt);
        }

        /// <summary>침몰 진행 피해의 기준 선체 = 함종 선체 × 선체 강화 카드(검사 환경이 최대 선체를 크게 올려도 그대로).</summary>
        public static float ReferenceHullHp(ShipController ship)
        {
            float b = ship.Hull != null ? ship.Hull.HullHp : ship.Config != null ? ship.Config.HullMaxHp : ship.HullMaxHp;
            return Mathf.Min(ship.HullMaxHp, b * RunUpgrades.HullMultiplier);
        }

        /// <summary>물 → 속력 · 기울기 · 가라앉음 · 침몰 진행.</summary>
        private void Apply(float dt)
        {
            float sum = 0f, list = 0f, trim = 0f;
            foreach (var c in _comps)
            {
                sum += c.Water;
                trim += c.Water * c.Along * TrimPerCompartment;
                list += (c.Stbd - c.Port) * ListPerCompartment;
            }
            FloodedCompartments = sum;
            TargetList = Mathf.Clamp(list, -MaxList - 5f, MaxList + 5f);
            TargetTrim = Mathf.Clamp(trim, -8f, 8f);
            if (_ship != null) _ship.FloodSpeedMultiplier = Mathf.Max(MinSpeed, 1f - SpeedPerCompartment * sum);

            float k = dt > 0f ? 1f - Mathf.Exp(-dt * 1.2f) : 1f;
            _list = Mathf.Lerp(_list, TargetList, k);
            _trim = Mathf.Lerp(_trim, TargetTrim, k);
            _sink = Mathf.Lerp(_sink, SinkAtFull * FloodFraction, k);
            // 물이 있을 때만 Model을 움직인다 — 처음 기울 때 지금 자세를 기준으로 잡고, 물이 다 빠지면 그 자세로 되돌린다
            // (물이 없을 때 매 프레임 덮어쓰면 다른 코드가 바꾼 Model 자리 · 자세를 깨뜨린다 — WWII 순양함 주포 일제가 어긋났다)
            bool still = Mathf.Abs(_list) < 0.02f && Mathf.Abs(_trim) < 0.02f && _sink < 0.002f
                         && Mathf.Abs(TargetList) < 0.02f && Mathf.Abs(TargetTrim) < 0.02f && FloodFraction < 0.002f;
            if (_model != null)
            {
                if (still)
                {
                    if (_modelPosed) { _model.localRotation = _modelRot; _model.localPosition = _modelPos; _modelPosed = false; }
                    _list = _trim = _sink = 0f;
                }
                else
                {
                    if (!_modelPosed) { _modelRot = _model.localRotation; _modelPos = _model.localPosition; _modelPosed = true; }
                    _model.localRotation = Quaternion.Euler(_trim, 0f, -_list) * _modelRot;
                    _model.localPosition = _modelPos + Vector3.down * _sink;
                }
            }

            Sinking = FloodFraction > SinkingFlood || Mathf.Abs(TargetList) > MaxList;
            if (Sinking && dt > 0f && _ship != null && _ship.IsAlive)
            {
                _ship.ApplyHullDamage(ReferenceHullHp(_ship) * SinkingHullPerSecond * dt, "침수 · 전복", hostile: false);
                if (Time.time - _bannerAt > 6f) { _bannerAt = Time.time; GameEvents.RaiseBanner("침몰 위험 — 배가 크게 기울었습니다. 손상 통제반이 구멍을 막는 동안 공격을 피하십시오"); }
            }
        }
    }
}
