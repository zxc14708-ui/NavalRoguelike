using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Combat;
using Game.Enemies;

namespace Game.Modules.Runtime
{
    /// <summary>
    /// 대잠 폭뢰. 소나 접촉·발사 흔적·근거리 자체 센서로 잠수함을 찾는다.
    /// 자리에 따라 형태가 바뀐다(ModuleVariants, 격자가 바뀔 때마다 다시 판정):
    ///   - 측면 발사대(옆이 트인 자리): 트인 현측(정횡 ±75°)으로 사거리 안에 던진다. 앞뒤·막힌 현측으로는 못 던진다.
    ///   - 선미 투하대(뒤가 트인 자리): 배 바로 뒤 항적(선미 0~6m)에 차례로 떨어뜨린다. 피해 1.25배·재장전 0.8배 —
    ///     잠수함이 항적 가까이(투하 지점 8m 안) 있어야 한다. 뒤에서 쫓아오는 적 어뢰를 얕은 신관 폭뢰로 요격한다(2026-11-22).
    ///   - 사방이 막힌 자리(형태 없음)에는 설치되지 않는다. 혹시 남아 있으면 쏘지 않는다.
    /// 단계별 한 번 투하 수(2026-11-22 사용자): Lv1 1발 · Lv2 2발 · Lv3 4발. 모델도 단계마다 늘어난다 —
    ///   발사대: 발사기 1문 → 2문 → 4문(2×2), 투하대: 레일 1줄 → 2줄 → 2줄 + 즉응 탄약함.
    /// 소나 모듈 없이도 8m 안에서는 대응할 수 있지만 사거리 활용에는 소나가 유리하다.
    /// </summary>
    public class AswLauncherModule : ModuleRuntime, IAmmoUser
    {
        [SerializeField] private GameObject chargePrefab;
        [SerializeField] private Transform[] launchPoints;

        [Tooltip("예전 고정 투하 수(쓰지 않음 — 단계별 1·2·4발, ChargesForLevel)")]
        [SerializeField, Min(1)] private int salvo = 2;

        [Tooltip("폭뢰끼리 떨어지는 간격(m). 조금 벌려야 빗나가도 하나는 걸린다.")]
        [SerializeField] private float spread = 2.5f;

        [Tooltip("폭뢰 비행 시간. 잠수함 이동 예측에 쓴다(DepthCharge와 맞출 것).")]
        [SerializeField] private float predictedFlightTime = 2.0f;   // 비행 1.1 + 가라앉음 0.9

        // ---- 투하대 차례 투하 · 어뢰 요격(2026-11-22)
        public const float RackDropInterval = 0.25f;    // 항적을 따라 차례로 떨어뜨리는 간격(초)
        public const float RackDropSpacing = 1.8f;      // 항적 위 폭뢰 간격(m)
        public const float InterceptRange = 26f;        // 뒤에서 오는 어뢰를 보는 거리(m)
        public const float InterceptReach = 3.5f;       // 요격 지점이 항적 투하선(선미 0~6m)에서 이만큼 안이어야(바로 뒤에서 터뜨린다)
        public const float InterceptFlight = 0.35f;     // 요격 폭뢰: 짧게 굴려 떨어뜨리고
        public const float InterceptSink = 0.15f;       //            얕은 신관으로 바로 터진다
        public const float InterceptDropInterval = 0.08f;

        private float _cooldown;
        private int _nextTube;
        private readonly AmmoMagazine _ammo = new();
        private float _lastShotTime = -999f;
        private TargetingSystem _targeting;

        private struct PendingDrop { public float At; public Vector3 To; public float Damage, Flight, Sink; }
        private readonly List<PendingDrop> _pending = new();

        // 단계별 모델
        private Transform _visual;
        private Vector3 _visualBasePos, _visualBaseScale = Vector3.one;
        private readonly List<GameObject> _extras = new();
        private readonly List<GameObject> _hidden = new();

        public AmmoMagazine Ammo => _ammo;
        public bool IsEngaged => Time.time - _lastShotTime < 0.6f;

        private ModuleVariant Variant => Instance != null ? Instance.Variant : ModuleVariant.None;
        private int Level => Instance != null ? Instance.Level : 1;

        /// <summary>단계별 한 번 투하 수: Lv1 1 · Lv2 2 · Lv3 이상 4.</summary>
        public static int ChargesForLevel(int level) => level >= 3 ? 4 : Mathf.Max(1, level);
        public int ChargesPerSalvo => ChargesForLevel(Level);

        /// <summary>검증용: 마지막 투하·발사의 목표 지점들.</summary>
        public List<Vector3> LastAims { get; } = new();
        public int Salvos { get; private set; }
        /// <summary>검증용: 어뢰 요격 투하 횟수.</summary>
        public int InterceptSalvos { get; private set; }
        /// <summary>검증용: 지금 쓰는 발사점 수(모델 단계별).</summary>
        public int LaunchPointCount => launchPoints != null ? launchPoints.Length : 0;
        /// <summary>검증용: 단계 모델로 덧붙인 것(발사기 복제 · 즉응 탄약함) 수.</summary>
        public int ExtraVisualCount => _extras.Count;
        /// <summary>검증용: 단계 모델로 숨긴 부품 수.</summary>
        public int HiddenPartCount => _hidden.Count;
        /// <summary>검증용: 아직 떨어뜨리지 않은 차례 투하.</summary>
        public int PendingDrops => _pending.Count;

        /// <summary>형태 모델의 발사점(투하대: 레일 출구, 발사대: 투사기)으로 다시 잇고 단계 모델을 맞춘다.</summary>
        protected override void OnVariantChanged(ModuleVariant variant, Transform visual)
        {
            _extras.Clear();   // 이전 형태 모델과 함께 사라졌다
            _hidden.Clear();
            _visual = visual;
            if (visual == null) return;
            _visualBasePos = visual.localPosition;
            _visualBaseScale = visual.localScale;
            ApplyLevelLook();
        }

        public override void OnUpgraded(int newLevel)
        {
            base.OnUpgraded(newLevel);
            ApplyLevelLook();
        }

        // ------------------------------------------------------------------ 단계별 모델

        private void ApplyLevelLook()
        {
            if (_visual == null) return;
            // 떼어 낸 뒤 지운다(Destroy는 프레임 끝이라 그 전에 복제하면 같이 복제된다)
            foreach (var go in _extras) if (go != null) { go.SetActive(false); go.transform.SetParent(null, false); Destroy(go); }
            _extras.Clear();
            foreach (var go in _hidden) if (go != null) go.SetActive(true);
            _hidden.Clear();
            _visual.localPosition = _visualBasePos;
            _visual.localScale = _visualBaseScale;

            int level = Level;
            if (Variant == ModuleVariant.DepthChargeProjector) BuildProjectorLook(level);
            else if (Variant == ModuleVariant.DepthChargeRack) BuildRackLook(level);
            CollectLaunchPoints();
        }

        /// <summary>발사대: 모델에 발사기 2문(_01 · _02)이 있다. Lv1은 _02를 숨기고, Lv3은 한 벌을 안쪽에 더 놓아 2×2.</summary>
        private void BuildProjectorLook(int level)
            => ShapeProjector(_visual, level, Outboard(), Grid != null ? Grid.CellSize : 1f, _visualBaseScale, _hidden, _extras);

        /// <summary>
        /// 발사대 모델을 단계 모습으로 바꾼다(런타임 · 카드 아이콘 굽기 공용). hidden · extras에 숨긴 부품 · 덧붙인 것을 담는다.
        /// </summary>
        public static void ShapeProjector(Transform visual, int level, Vector3 outboard, float cell, Vector3 baseScale,
                                          List<GameObject> hidden, List<GameObject> extras)
        {
            if (visual == null) return;
            if (level <= 1) HideIndexed(visual, 2, hidden);
            if (level < 3) return;

            float depth = ExtentAlong(visual, outboard) * 2f;
            float k = depth > 0.01f ? Mathf.Clamp(cell * 1.15f / (2f * depth), 0.62f, 1f) : 0.8f;
            // 한 벌을 복제(아직 덧붙인 것이 없을 때의 모습)해 형태 모델 아래에 둔다 — 형태가 바뀌면 함께 사라진다
            var copy = Instantiate(visual.gameObject, visual, true);
            copy.name = "ProjectorPair_Inboard";
            extras?.Add(copy);
            copy.transform.position = visual.position - outboard * depth;
            copy.transform.rotation = visual.rotation;
            // 두 벌을 블록 가운데로 모으고 줄인다
            visual.localScale = baseScale * k;
            visual.position += outboard * (depth * k * 0.5f);
        }

        /// <summary>투하대: 레일 2줄(Rack_01 · Rack_02). Lv1은 Rack_02와 그 줄의 폭뢰를 숨기고, Lv3은 즉응 탄약함을 덧붙인다.</summary>
        private void BuildRackLook(int level)
        {
            if (level <= 1)
            {
                Vector3 c1 = MeanOf(_visual, "Rack_01_"), c2 = MeanOf(_visual, "Rack_02_");
                bool two = c1 != Vector3.zero && c2 != Vector3.zero;
                foreach (var t in _visual.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name;
                    if (n.StartsWith("Rack_02_")) { Hide(t.gameObject); continue; }
                    if (!two) continue;
                    if (n.StartsWith("DepthCharge_") || n.StartsWith("StoredCharge_") || n.StartsWith("LaunchPoint_") || n.StartsWith("DropPoint_"))
                    {
                        Vector3 p = t.position;
                        if ((p - c2).sqrMagnitude < (p - c1).sqrMagnitude) Hide(t.gameObject);
                    }
                }
            }
            if (level < 3) return;

            // 즉응 탄약함: 레일 앞(함수 쪽)에 긴 상자와 그 위 폭뢰 4발
            var b = WorldBounds(_visual);
            Vector3 fwd = ShipForward, right = Vector3.Cross(Vector3.up, fwd);
            float width = Mathf.Max(0.4f, ExtentAlong(_visual, right) * 2f * 0.9f);
            float len = (Grid != null ? Grid.CellSize : 1f) * 0.26f;
            float front = ExtentAlong(_visual, fwd);
            var root = new GameObject("ReadyMagazine");
            root.transform.SetParent(_visual, false);
            root.transform.position = new Vector3(b.center.x, b.min.y, b.center.z) + fwd * (front - len * 0.5f);
            root.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            _extras.Add(root);

            Material plate = MaterialOf(_visual, "DepthRackMountPlate") ?? MaterialOf(_visual, "Rack_01_");
            Material charge = MaterialOf(_visual, "StoredCharge_") ?? MaterialOf(_visual, "DepthCharge_") ?? plate;
            var box = Part(PrimitiveType.Cube, root.transform, plate);
            box.localScale = new Vector3(width, 0.32f, len);
            box.localPosition = new Vector3(0f, 0.16f, 0f);
            var lid = Part(PrimitiveType.Cube, root.transform, plate);
            lid.localScale = new Vector3(width * 1.04f, 0.05f, len * 1.08f);
            lid.localPosition = new Vector3(0f, 0.345f, 0f);
            for (int i = 0; i < 4; i++)
            {
                var c = Part(PrimitiveType.Cylinder, root.transform, charge);
                float r = Mathf.Min(len * 0.36f, width / 9f);
                c.localScale = new Vector3(r * 2f, len * 0.42f, r * 2f);
                c.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 눕혀서 함수 방향
                c.localPosition = new Vector3(Mathf.Lerp(-width * 0.36f, width * 0.36f, i / 3f), 0.37f + r, 0f);
            }
        }

        private Transform Part(PrimitiveType type, Transform parent, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            if (mat != null && go.TryGetComponent<Renderer>(out var r)) r.sharedMaterial = mat;
            return go.transform;
        }

        private void Hide(GameObject go) => Hide(go, _hidden);

        private static void Hide(GameObject go, List<GameObject> hidden)
        {
            if (go == null || !go.activeSelf) return;
            go.SetActive(false);
            hidden?.Add(go);
        }

        /// <summary>이름의 첫 번호(_01 · _02 …)가 index인 부품을 숨긴다(발사기 _02 = 둘째 문).</summary>
        private static void HideIndexed(Transform root, int index, List<GameObject> hidden)
        {
            string tag = $"_{index:00}";
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                string n = t.name;
                int at = n.IndexOf("_0", System.StringComparison.Ordinal);
                if (at < 0 || at + 3 > n.Length) continue;
                if (string.CompareOrdinal(n, at, tag, 0, 3) == 0) Hide(t.gameObject, hidden);
            }
        }

        private void CollectLaunchPoints()
        {
            var points = new List<Transform>();
            foreach (var t in _visual.GetComponentsInChildren<Transform>(false))
                if (t.name.StartsWith("LaunchPoint_")) points.Add(t);
            if (points.Count > 0) launchPoints = points.ToArray();
            _nextTube = 0;
        }

        private Vector3 Outboard()
        {
            var s = ModuleVariantVisual.Find(_visual, "OutboardDirection");
            Vector3 o = s != null ? Flat(s.position - _visual.position) : Vector3.zero;
            if (o.sqrMagnitude < 1e-6f)
            {
                var sides = Instance != null ? Instance.Sides : ModuleSides.None;
                Vector3 right = Vector3.Cross(Vector3.up, ShipForward);
                o = (sides & ModuleSides.Port) != 0 ? -right : right;
            }
            return o.normalized;
        }

        private static Bounds WorldBounds(Transform root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return new Bounds(root.position, Vector3.one * 0.5f);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>모델이 그 방향으로 중심에서 뻗은 길이(월드 m).</summary>
        private static float ExtentAlong(Transform root, Vector3 dir)
        {
            var b = WorldBounds(root);
            return Mathf.Abs(b.extents.x * dir.x) + Mathf.Abs(b.extents.y * dir.y) + Mathf.Abs(b.extents.z * dir.z);
        }

        private static Vector3 MeanOf(Transform root, string prefix)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) { sum += t.position; n++; }
            return n > 0 ? sum / n : Vector3.zero;
        }

        private static Material MaterialOf(Transform root, string prefix)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name.StartsWith(prefix) && r.sharedMaterial != null) return r.sharedMaterial;
            return null;
        }

        // ------------------------------------------------------------------ 겨냥

        private Vector3 ShipForward
        {
            get
            {
                var f = Grid != null ? Grid.transform.forward : transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            }
        }

        /// <summary>선미 투하대가 떨어뜨리는 기준점(블록에서 선미 쪽).</summary>
        public Vector3 RackDropOrigin => transform.position - ShipForward * ModuleVariants.RackDropBehind;

        /// <summary>
        /// 이 형태가 그 지점에 폭뢰를 보낼 수 있는가. 투하대 = 항적(선미 0~6m) 가까이, 발사대 = 트인 현측 ±75°.
        /// </summary>
        public bool CanReach(Vector3 aim)
        {
            Vector3 d = aim - transform.position;
            d.y = 0f;
            switch (Variant)
            {
                case ModuleVariant.DepthChargeRack:
                    return Flat(aim - RackDropPoint(aim)).magnitude <= ModuleVariants.RackReach;
                case ModuleVariant.DepthChargeProjector:
                {
                    if (d.sqrMagnitude < 0.01f) return true;
                    Vector3 right = Vector3.Cross(Vector3.up, ShipForward);
                    var sides = Instance != null ? Instance.Sides : ModuleSides.None;
                    if ((sides & ModuleSides.Starboard) != 0 && Vector3.Angle(right, d) <= ModuleVariants.ProjectorHalfArc) return true;
                    if ((sides & ModuleSides.Port) != 0 && Vector3.Angle(-right, d) <= ModuleVariants.ProjectorHalfArc) return true;
                    return false;
                }
                case ModuleVariant.None:
                    return Instance == null || !ModuleVariants.HasVariants(Instance.Definition.Type);   // 형태가 없는 예전 배치(검증용 직접 설치 등)
                default:
                    return true;
            }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        /// <summary>투하대: 항적선(투하 기준점에서 선미로 0~6m) 위에서 표적에 가장 가까운 점.</summary>
        private Vector3 RackDropPoint(Vector3 aim)
        {
            Vector3 origin = RackDropOrigin;
            Vector3 back = -ShipForward;
            float t = Mathf.Clamp(Vector3.Dot(Flat(aim - origin), back), 0f, 6f);
            var p = origin + back * t;
            p.y = aim.y;
            return p;
        }

        protected override void OnInitialized()
        {
            _targeting = GetComponentInParent<TargetingSystem>();
            if (chargePrefab == null) Debug.LogError("[ASW] chargePrefab 미할당.", this);
            _ammo.Configure(Stats);
            _ammo.Label = LogName;
        }

        protected override void Tick(float dt)
        {
            _ammo.Tick(dt);
            FlushPending();
            if (_cooldown > 0f) { _cooldown -= dt; return; }
            if (chargePrefab == null || !_ammo.CanFire) return;
            bool rack = Variant == ModuleVariant.DepthChargeRack;

            // 선미 투하대: 뒤에서 쫓아오는 어뢰가 먼저(2026-11-22)
            if (rack && TryInterceptTorpedo()) return;

            var target = FindNearestSubmarine();
            Vector3 aim;
            if (target != null)
                aim = target.IsContactConfirmed
                    ? target.LastKnownPosition + target.LastKnownVelocity * predictedFlightTime
                    : target.transform.position;
            else if (FindNearestLowProfile() is { } low)
                aim = low.transform.position + low.MoveVelocity * predictedFlightTime;   // 드러난 반잠수정(2026-11-19)
            else if (_targeting == null || !_targeting.TryGetLaunchCue(transform.position, Stats.Range, out aim))
                return;

            // 형태별로 닿는 곳만(투하대 = 항적 가까이, 발사대 = 트인 현측). 못 닿으면 기다린다.
            if (!CanReach(aim)) { _cooldown = 0.25f; return; }

            int count = TakeCharges();
            if (count == 0) return;
            _lastShotTime = Time.time;
            Salvos++;
            LastAims.Clear();

            float damage = Stats.Damage * (rack ? ModuleVariants.RackDamageMultiplier : 1f);
            if (rack)
            {
                // 항적 위 표적에 가장 가까운 점을 가운데로, 항적을 따라 차례로(앞 → 뒤, 좌우로 조금씩 엇갈려)
                Vector3 center = RackDropPoint(aim);
                Vector3 back = -ShipForward, right = Vector3.Cross(Vector3.up, ShipForward);
                for (int i = 0; i < count; i++)
                {
                    float along = (i - (count - 1) * 0.5f) * RackDropSpacing;
                    float side = count == 1 ? 0f : (i % 2 == 0 ? 0.6f : -0.6f);
                    Vector3 to = center + back * along + right * side;
                    LastAims.Add(to);
                    Queue(i * RackDropInterval, to, damage, -1f, -1f);
                }
                FlushPending();
                AudioManager.Play(Game.Data.SfxId.DepthDrop, transform.position);   // 레일 굴러 떨어지는 소리
            }
            else
            {
                // 비행하고 가라앉는 동안 잠수함이 움직인 곳에 던진다
                Vector3 side = Vector3.Cross(Vector3.up, (aim - transform.position).normalized);
                for (int i = 0; i < count; i++)
                {
                    float offset = count == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (count - 1f)) * 0.5f;
                    Vector3 to = aim + side * offset;
                    if (count >= 4) to += (aim - transform.position).normalized * ((i % 2 == 0 ? 0.9f : -0.9f));   // 4발은 앞뒤로도 벌린다
                    LastAims.Add(to);
                    Throw(to, damage, -1f, -1f);
                }
                AudioManager.Play(Game.Data.SfxId.AutocannonShot, transform.position, 0.5f, 0.6f);
            }
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime * (rack ? ModuleVariants.RackReloadMultiplier : 1f));
        }

        private int TakeCharges()
        {
            int want = ChargesPerSalvo, count = 0;
            while (count < want && _ammo.Consume()) count++;   // 남은 폭뢰만큼만 던진다
            return count;
        }

        /// <summary>
        /// 선미 투하대 어뢰 요격: 배 뒤쪽에서 이 배를 향해 달려오는 적 어뢰가 0.5초 뒤 지날 곳이 항적 투하 범위 안이면
        /// 그 길목에 얕은 신관 폭뢰를 떨어뜨린다(폭발 반경 안 어뢰는 함께 터진다 — DepthCharge.Detonate).
        /// </summary>
        private bool TryInterceptTorpedo()
        {
            var list = Torpedo.Active;
            if (list.Count == 0) return false;
            Vector3 origin = RackDropOrigin, fwd = ShipForward;
            Vector3 shipPos = Grid != null ? Grid.transform.position : transform.position;
            float fuse = InterceptFlight + InterceptSink;
            Torpedo best = null; Vector3 bestPoint = default; float bestSqr = InterceptRange * InterceptRange;
            for (int i = 0; i < list.Count; i++)
            {
                var tp = list[i];
                if (tp == null || !tp.IsAlive) continue;
                Vector3 rel = Flat(tp.transform.position - origin);
                if (rel.sqrMagnitude > bestSqr) continue;
                if (Vector3.Dot(rel, -fwd) < -1f) continue;                                    // 배 뒤쪽에서
                Vector3 run = Flat(tp.transform.forward).normalized;
                if (Vector3.Dot(run, Flat(shipPos - tp.transform.position).normalized) < 0.5f) continue;   // 이 배를 향해 오는 것만
                Vector3 meet = tp.transform.position + run * (tp.Speed * fuse);
                Vector3 drop = RackDropPoint(meet);
                if (Flat(meet - drop).magnitude > InterceptReach) continue;
                bestSqr = rel.sqrMagnitude; best = tp; bestPoint = Flat(meet);
            }
            if (best == null) return false;

            int count = TakeCharges();
            if (count == 0) return false;
            _lastShotTime = Time.time;
            Salvos++;
            InterceptSalvos++;
            LastAims.Clear();
            Vector3 runDir = Flat(best.transform.forward).normalized;
            float damage = Stats.Damage * ModuleVariants.RackDamageMultiplier;
            for (int i = 0; i < count; i++)
            {
                // 어뢰 진로를 따라 앞뒤로 벌려 놓는다(빨리 오든 늦게 오든 하나는 걸리게)
                float along = (i - (count - 1) * 0.5f) * 1.6f;
                Vector3 to = bestPoint + runDir * along;
                LastAims.Add(to);
                Queue(i * InterceptDropInterval, to, damage, InterceptFlight, InterceptSink);
            }
            FlushPending();
            AudioManager.Play(Game.Data.SfxId.DepthDrop, transform.position);
            CombatLog.Add(LogName, $"어뢰 요격 투하 {count}발");
            _cooldown = Mathf.Max(0.5f, Stats.ReloadTime * ModuleVariants.RackReloadMultiplier);
            return true;
        }

        private void Queue(float delay, Vector3 to, float damage, float flight, float sink)
            => _pending.Add(new PendingDrop { At = Time.time + delay, To = to, Damage = damage, Flight = flight, Sink = sink });

        private void FlushPending()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].At > Time.time) continue;
                var p = _pending[i];
                _pending.RemoveAt(i--);
                Throw(p.To, p.Damage, p.Flight, p.Sink);
            }
        }

        private void Throw(Vector3 to, float damage, float flight, float sink)
        {
            var tube = NextTube();
            var go = PoolManager.Instance?.Spawn(chargePrefab, tube.position, tube.rotation);
            go?.GetComponent<DepthCharge>()?.Launch(tube.position, to, damage, flight, sink);
        }

        public override void OnModuleDestroyed()
        {
            base.OnModuleDestroyed();
            _pending.Clear();
        }

        /// <summary>소나 접촉, 실제 부상, 또는 발사기 자체의 매우 짧은 거리 센서로 찾는다.</summary>
        private SubmarineBase FindNearestSubmarine()
        {
            float bestSqr = Stats.Range * Stats.Range;
            SubmarineBase best = null;

            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 trueDelta = sub.transform.position - transform.position;
                trueDelta.y = 0f;
                if (trueDelta.sqrMagnitude <= 64f) sub.ConfirmContact(1f);
                if (!sub.IsContactConfirmed && !sub.IsRevealed) continue;
                Vector3 reported = sub.IsRevealed ? sub.transform.position : sub.LastKnownPosition;
                Vector3 delta = reported - transform.position;
                delta.y = 0f;
                float d = delta.sqrMagnitude;
                if (d >= bestSqr) continue;

                bestSqr = d;
                best = sub;
            }
            return best;
        }

        /// <summary>드러난 반잠수정(소나 · 조명탄 · 근접) 중 가장 가까운 것 — 잠수함이 없을 때만 노린다.</summary>
        private Game.Enemies.EnemyController FindNearestLowProfile()
        {
            float bestSqr = Stats.Range * Stats.Range;
            Game.Enemies.EnemyController best = null;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t is not ILowObservable low || low.Stealthy || t is not Game.Enemies.EnemyController e || !e.IsAlive) continue;
                Vector3 d = e.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude >= bestSqr) continue;
                bestSqr = d.sqrMagnitude;
                best = e;
            }
            return best;
        }

        private Transform NextTube()
        {
            if (launchPoints == null || launchPoints.Length == 0) return transform;
            var t = launchPoints[_nextTube % launchPoints.Length];
            _nextTube++;
            return t != null ? t : transform;
        }
    }
}
