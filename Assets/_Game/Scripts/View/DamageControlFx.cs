using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Modules;
using Game.Modules.Runtime;
using Game.Ship;

namespace Game.View
{
    /// <summary>
    /// 손상 통제 작동 연출(2026-11-21 사용자: 손상통제 블록이 작동하는 게 보이게).
    ///   - 손상통제 블록: 위에 주황 경광등 — 일하는 동안 빙글빙글 돌며 깜박이고(작은 점광원), 쉬면 꺼진다.
    ///   - 고치는 블록(함교 · 손상통제반이 수리 중): 용접 불꽃이 튄다.
    ///   - 끄는 불: 흰 물줄기가 불 위로 뿌려진다.
    ///   - 막는 구멍: 그 구획 현에서 배수 펌프 물줄기가 바깥으로 쏟아진다.
    /// 기함 뿌리에 붙는다(DamageResolver가 붙임). 원래 재질은 건드리지 않는다.
    /// </summary>
    public sealed class DamageControlFx : MonoBehaviour
    {
        public static readonly Color BeaconColor = new(1f, 0.62f, 0.12f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Material s_beaconMat;

        private sealed class Beacon
        {
            public ModuleInstance Module;
            public Transform Root, Spinner;
            public Renderer[] Lamps;
            public Light Light;
        }

        private ShipController _ship;
        private ShipGrid _grid;
        private ShipDamageControl _dc;
        private ShipFlooding _flood;
        private readonly List<Beacon> _beacons = new();
        private MaterialPropertyBlock _mpb;
        private float _sparkDebt, _sprayDebt, _pumpDebt, _scan;

        /// <summary>검증용: 경광등 수 · 지금 도는(일하는) 경광등 수 · 지금 고치는 블록 수.</summary>
        public int BeaconCount => _beacons.Count;
        public int WorkingBeacons { get; private set; }
        public int RepairTargets { get; private set; }

        private void Awake()
        {
            _ship = GetComponentInParent<ShipController>();
            var resolver = GetComponent<DamageResolver>();
            _grid = resolver != null ? resolver.Grid : null;
            _dc = GetComponent<ShipDamageControl>();
            _flood = GetComponent<ShipFlooding>();
        }

        private readonly List<ModuleInstance> _targets = new();

        private void LateUpdate()
        {
            if (_grid == null) return;
            float dt = Time.deltaTime;
            _scan -= dt;
            if (_scan <= 0f) { _scan = 0.5f; SyncBeacons(); }

            // 지금 고치는 블록(함교 · 손상통제반)
            _targets.Clear();
            bool anyTask = _dc != null && _dc.Assigned.Count > 0;
            foreach (var m in _grid.Modules)
            {
                if (m?.Runtime == null) continue;
                ModuleInstance t = m.Runtime is RepairBayModule bay ? bay.RepairTarget : m.Runtime is BridgeModule br ? br.RepairTarget : null;
                if (t != null && !_targets.Contains(t)) _targets.Add(t);
            }
            RepairTargets = _targets.Count;
            bool working = anyTask || _targets.Count > 0;

            // 경광등
            WorkingBeacons = 0;
            _mpb ??= new MaterialPropertyBlock();
            foreach (var b in _beacons)
            {
                if (b.Root == null) continue;
                bool on = working && b.Module != null && b.Module.IsOperational;
                if (on)
                {
                    WorkingBeacons++;
                    b.Spinner.Rotate(Vector3.up, 540f * dt, Space.World);
                    float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
                    _mpb.SetColor(BaseColorId, BeaconColor * (2.4f * pulse));
                    b.Light.enabled = true;
                    b.Light.intensity = 2.2f * pulse;
                }
                else
                {
                    _mpb.SetColor(BaseColorId, new Color(0.35f, 0.22f, 0.08f));   // 꺼진 등(어두운 주황 유리)
                    b.Light.enabled = false;
                }
                foreach (var r in b.Lamps) if (r != null) r.SetPropertyBlock(_mpb);
            }

            if (dt <= 0f) return;
            float density = Game.Core.DisplaySettings.DecorDensity;

            // 용접 불꽃(고치는 블록)
            _sparkDebt += _targets.Count * 10f * density * dt;
            while (_sparkDebt >= 1f && _targets.Count > 0)
            {
                _sparkDebt -= 1f;
                var t = _targets[Random.Range(0, _targets.Count)];
                Vector3 at = PointOn(t) + Random.insideUnitSphere * 0.35f;
                DecorFx.Emit(DecorFx.Sparks, at, Random.insideUnitSphere * 3.5f + Vector3.up * 2.5f, Random.Range(0.15f, 0.35f), Random.Range(0.05f, 0.1f),
                    Color.Lerp(new Color(1f, 0.95f, 0.7f, 1f), new Color(0.6f, 0.85f, 1f, 1f), Random.value));
            }

            if (_dc == null) return;
            // 소화 물줄기 · 배수 펌프
            int fires = 0, holes = 0;
            foreach (var task in _dc.Assigned) { if (task is ShipFire.Fire) fires++; else if (task is ShipFlooding.Hole) holes++; }
            _sprayDebt += fires * 22f * density * dt;
            while (_sprayDebt >= 1f && fires > 0)
            {
                _sprayDebt -= 1f;
                foreach (var task in _dc.Assigned)
                {
                    if (task is not ShipFire.Fire f || f.Module == null) continue;
                    Vector3 at = PointOn(f.Module) + Vector3.up * 1.4f + Random.insideUnitSphere * 0.3f;
                    DecorFx.Emit(DecorFx.Spray, at, Vector3.down * Random.Range(1.5f, 3f) + Random.insideUnitSphere * 1.6f, Random.Range(0.35f, 0.6f), Random.Range(0.25f, 0.45f),
                        new Color(0.92f, 0.97f, 1f, 0.75f));
                }
            }
            _pumpDebt += holes * 14f * density * dt;
            while (_pumpDebt >= 1f && holes > 0 && _flood != null && _ship != null)
            {
                _pumpDebt -= 1f;
                foreach (var task in _dc.Assigned)
                {
                    if (task is not ShipFlooding.Hole h || h.Compartment >= _flood.Compartments.Count) continue;
                    var c = _flood.Compartments[h.Compartment];
                    float x = (c.MinX + c.MaxX) * 0.5f * _grid.CellSize;
                    var box = _ship.GetComponent<BoxCollider>();
                    float half = box != null ? box.size.x * 0.5f : 2f;
                    Vector3 side = _ship.transform.right * (h.Side > 0f ? 1f : -1f);
                    Vector3 at = _grid.transform.TransformPoint(new Vector3(0f, _grid.DeckHeight, x)) + side * half + Vector3.up * 0.2f;
                    DecorFx.Emit(DecorFx.Spray, at, side * Random.Range(2.5f, 4f) + Vector3.up * Random.Range(0.5f, 1.5f), Random.Range(0.5f, 0.8f), Random.Range(0.3f, 0.5f),
                        new Color(0.9f, 0.96f, 1f, 0.8f));
                }
            }
        }

        private static Vector3 PointOn(ModuleInstance m)
            => m.Runtime != null ? m.Runtime.transform.position + Vector3.up * 0.6f : Vector3.zero;

        /// <summary>손상통제 블록마다 경광등 하나(새로 놓거나 치우면 맞춘다).</summary>
        private void SyncBeacons()
        {
            for (int i = _beacons.Count - 1; i >= 0; i--)
            {
                var b = _beacons[i];
                bool keep = b.Root != null && b.Module != null && _grid.Modules.Contains(b.Module) && b.Module.Runtime != null && b.Root.parent == b.Module.Runtime.transform;
                if (keep) continue;
                if (b.Root != null) Destroy(b.Root.gameObject);
                _beacons.RemoveAt(i);
            }
            foreach (var m in _grid.Modules)
            {
                if (m?.Definition == null || m.Definition.Type != ModuleType.RepairBay || m.Runtime == null) continue;
                if (_beacons.Exists(b => b.Module == m)) continue;
                _beacons.Add(Build(m));
            }
        }

        private Beacon Build(ModuleInstance m)
        {
            var host = m.Runtime.transform;
            // 블록 모델 꼭대기 한가운데
            Bounds bounds = default; bool has = false;
            foreach (var r in host.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r.name == HitFlash.LayerName) continue;
                if (!has) { bounds = r.bounds; has = true; } else bounds.Encapsulate(r.bounds);
            }
            Vector3 top = has ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : host.position + Vector3.up * 1.2f;

            if (s_beaconMat == null) s_beaconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "DC beacon (Runtime)" };
            var root = new GameObject("Damage control beacon").transform;
            root.SetParent(host, true);
            root.position = top;
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGo.name = "Beacon base";
            Destroy(baseGo.GetComponent<Collider>());
            baseGo.transform.SetParent(root, false);
            baseGo.transform.localScale = new Vector3(0.32f, 0.05f, 0.32f);
            baseGo.transform.position = top + Vector3.up * 0.05f;
            var baseR = baseGo.GetComponent<Renderer>();
            baseR.sharedMaterial = s_beaconMat;
            var spinner = new GameObject("Beacon spinner").transform;
            spinner.SetParent(root, false);
            spinner.position = top + Vector3.up * 0.2f;
            var lamps = new List<Renderer>();
            for (int i = 0; i < 2; i++)
            {
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Beacon lamp";
                Destroy(lamp.GetComponent<Collider>());
                lamp.transform.SetParent(spinner, false);
                lamp.transform.localPosition = new Vector3(i == 0 ? 0.09f : -0.09f, 0f, 0f);
                lamp.transform.localScale = new Vector3(0.12f, 0.18f, 0.08f);
                var r = lamp.GetComponent<Renderer>();
                r.sharedMaterial = s_beaconMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lamps.Add(r);
            }
            // 받침은 어두운 쇠색
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(BaseColorId, new Color(0.12f, 0.13f, 0.14f));
            baseR.SetPropertyBlock(mpb);
            baseR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var light = new GameObject("Beacon light").AddComponent<Light>();
            light.transform.SetParent(spinner, false);
            light.type = LightType.Point;
            light.color = BeaconColor;
            light.range = 5f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return new Beacon { Module = m, Root = root, Spinner = spinner, Lamps = lamps.ToArray(), Light = light };
        }
    }
}
