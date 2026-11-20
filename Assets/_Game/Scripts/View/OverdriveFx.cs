using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 오버드라이브 연출(2026-11-20, 고속 강습함 고유 스킬). 보기 전용.
    ///   - 붉은 달아오름: 함선 메시마다 같은 메시를 더하기 합성으로 한 겹 더 그려(피격 번쩍임과 같은 Naval/HitFlash 셰이더) 붉게 맥박 친다.
    ///   - 잔상: 움직이는 동안 0.08초마다 큰 부품 몇 개를 그 자리에 붉은 잔상으로 남겨 0.35초에 사라지게 한다.
    ///   - 입자: 선미 · 양현에서 붉은 불티가 뒤로 흩날리고, 시작 때 붉은 충격 고리 · 번쩍임, 끝날 때 한 번 더 번쩍이며 식는다.
    ///   - 과열(Overheat): 붉은빛이 식어 가는 주황빛으로 바뀌어 사그라들고, 갑판에서 흰 증기가 피어오른다.
    /// 원래 렌더러 · 재질은 건드리지 않는다. 덧그림은 투명 큐라 격침 잔해에 복사되지 않는다.
    /// </summary>
    public sealed class OverdriveFx : MonoBehaviour
    {
        public static readonly Color Glow = new(1f, 0.16f, 0.1f);
        public static readonly Color Heat = new(1f, 0.42f, 0.08f);
        private const float GhostInterval = 0.08f, GhostLife = 0.35f;
        private const int GhostParts = 10;
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static Material s_material;
        private static MaterialPropertyBlock s_mpb;

        private struct Layer { public Renderer Source; public MeshRenderer Overlay; }
        private struct Ghost { public GameObject Root; public MeshRenderer[] Parts; public float Born; }
        private readonly List<Layer> _layers = new();
        private readonly List<(MeshFilter mf, Renderer r)> _big = new();
        private readonly List<Ghost> _ghosts = new();
        private Game.Ship.ShipController _ship;
        private float _until, _age, _ghostTimer, _endAt = -1f, _heatStart = -1f, _heatUntil = -1f, _steamDebt;
        /// <summary>검증용: 과열 연출 중인가.</summary>
        public bool Overheating => _heatStart >= 0f && Time.time < _heatUntil;

        /// <summary>검증용: 덧그림 조각 수 · 지금 떠 있는 잔상 수 · 지금 달아오른 세기.</summary>
        public int LayerCount => _layers.Count;
        public int GhostCount => _ghosts.Count;
        public float Level { get; private set; }
        public static OverdriveFx Active { get; private set; }

        public static OverdriveFx Begin(Game.Ship.ShipController ship, float duration)
        {
            if (ship == null) return null;
            if (Active != null) Destroy(Active.gameObject);
            var go = new GameObject("Overdrive fx");
            var fx = go.AddComponent<OverdriveFx>();
            fx.Init(ship, duration);
            Active = fx;
            return fx;
        }

        /// <summary>끝: 한 번 번쩍이고 0.6초에 걸쳐 식는다.</summary>
        public void End()
        {
            if (_endAt >= 0f) return;
            _endAt = Time.time;
            if (_ship != null) Explosions.FlashLight(_ship.transform.position + Vector3.up * 2f, 0.4f, Glow);
        }

        /// <summary>과열: 붉은빛 → 식어 가는 주황빛 · 흰 증기, seconds초 뒤 사라진다.</summary>
        public void Overheat(float seconds)
        {
            _heatStart = Time.time;
            _heatUntil = Time.time + Mathf.Max(0.1f, seconds);
            _until = Mathf.Min(_until, Time.time);
            if (_ship != null) Explosions.FlashLight(_ship.transform.position + Vector3.up * 2f, 0.3f, Heat);
        }

        private void Init(Game.Ship.ShipController ship, float duration)
        {
            _ship = ship;
            _until = Time.time + duration;
            if (s_material == null) s_material = Resources.Load<Material>("Effects/MAT_HitFlash");
            if (s_material != null)
            {
                foreach (var r in ship.GetComponentsInChildren<MeshRenderer>(false))
                {
                    if (r == null || !r.enabled || r.name == HitFlash.LayerName || r.name == "Night lamp" || r.name == "Overdrive glow") continue;
                    if (!r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                    bool opaque = true;
                    foreach (var m in r.sharedMaterials) if (m == null || m.renderQueue >= 2500) { opaque = false; break; }
                    if (!opaque) continue;
                    var go = new GameObject("Overdrive glow", typeof(MeshFilter), typeof(MeshRenderer));
                    go.layer = r.gameObject.layer;
                    go.transform.SetParent(r.transform, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var overlay = go.GetComponent<MeshRenderer>();
                    var mats = new Material[mf.sharedMesh.subMeshCount];
                    for (int i = 0; i < mats.Length; i++) mats[i] = s_material;
                    overlay.sharedMaterials = mats;
                    overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    overlay.receiveShadows = false;
                    _layers.Add(new Layer { Source = r, Overlay = overlay });
                    _big.Add((mf, r));
                }
                // 잔상은 큰 부품 몇 개만(선체 · 상부 구조) — 작은 장비까지 복사하면 비싸다
                _big.Sort((a, b) => b.r.bounds.size.sqrMagnitude.CompareTo(a.r.bounds.size.sqrMagnitude));
                if (_big.Count > GhostParts) _big.RemoveRange(GhostParts, _big.Count - GhostParts);
            }
            Vector3 p = ship.transform.position;
            Game.Ship.SignatureFx.Ring(p, 20f, 0.7f, new Color(1f, 0.25f, 0.15f, 0.95f), 1f, 2, 0.12f);
            Explosions.FlashLight(p + Vector3.up * 2f, 0.6f, Glow);
            DecorFx.Emit(DecorFx.Flash, p + Vector3.up * 1.5f, Vector3.zero, 0.25f, 9f, new Color(1f, 0.3f, 0.2f, 0.9f));
            for (int i = 0; i < 24; i++)
            {
                Vector3 d = Random.onUnitSphere; d.y = Mathf.Abs(d.y) * 0.6f + 0.1f;
                DecorFx.Emit(DecorFx.Sparks, p + Vector3.up, d * Random.Range(6f, 12f), Random.Range(0.3f, 0.6f), Random.Range(0.12f, 0.22f), new Color(1f, Random.Range(0.15f, 0.4f), 0.1f, 1f));
            }
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_heatStart >= 0f) { TickOverheat(dt); return; }
            bool ending = _endAt >= 0f || Time.time >= _until;
            if (ending && _endAt < 0f) End();
            float cool = ending ? Mathf.Clamp01(1f - (Time.time - _endAt) / 0.6f) : 1f;
            if (_ship == null || (ending && cool <= 0f && _ghosts.Count == 0)) { Destroy(gameObject); return; }

            // 달아오름: 시작 0.3초 크게 번쩍 → 맥박(0.42~0.62), 끝날 때 식음
            float start = _age < 0.3f ? Mathf.Lerp(1.2f, 0.55f, _age / 0.3f) : 0.52f + 0.1f * Mathf.Sin(Time.time * 9f);
            Level = start * cool;
            s_mpb ??= new MaterialPropertyBlock();
            s_mpb.SetColor(FlashColorId, Glow * (0.85f * Level * Game.Core.DisplaySettings.FlashScale));
            foreach (var l in _layers)
            {
                if (l.Overlay == null) continue;
                bool show = Level > 0.01f && l.Source != null && l.Source.enabled && l.Source.gameObject.activeInHierarchy;
                if (l.Overlay.enabled != show) l.Overlay.enabled = show;
                if (show) l.Overlay.SetPropertyBlock(s_mpb);
            }

            // 잔상 · 불티(움직일 때)
            if (!ending && _ship.CurrentSpeed > 3f)
            {
                _ghostTimer -= dt;
                if (_ghostTimer <= 0f) { _ghostTimer = GhostInterval; SpawnGhost(); }
                Vector3 back = -_ship.transform.forward;
                var col = _ship.GetComponent<BoxCollider>();
                float half = col != null ? col.size.z * 0.5f : 5f, beam = col != null ? col.size.x * 0.5f : 1.5f;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 at = _ship.transform.position + back * half * Random.Range(0.6f, 1f) + _ship.transform.right * Random.Range(-beam, beam) + Vector3.up * Random.Range(0.5f, 2f);
                    DecorFx.Emit(DecorFx.Sparks, at, back * Random.Range(3f, 7f) + Random.insideUnitSphere * 1.5f, Random.Range(0.25f, 0.5f), Random.Range(0.08f, 0.16f),
                        new Color(1f, Random.Range(0.1f, 0.35f), 0.08f, 1f));
                }
            }
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var g = _ghosts[i];
                float u = (Time.time - g.Born) / GhostLife;
                if (u >= 1f || g.Root == null) { if (g.Root != null) Destroy(g.Root); _ghosts.RemoveAt(i); continue; }
                s_mpb.SetColor(FlashColorId, Glow * (0.55f * (1f - u) * (1f - u) * Game.Core.DisplaySettings.FlashScale));
                foreach (var part in g.Parts) if (part != null) part.SetPropertyBlock(s_mpb);
            }
        }

        private void TickOverheat(float dt)
        {
            float len = Mathf.Max(0.1f, _heatUntil - _heatStart);
            float k = Mathf.Clamp01(1f - (Time.time - _heatStart) / len);
            if (_ship == null || (k <= 0f && _ghosts.Count == 0)) { Destroy(gameObject); return; }
            // 식어 가는 주황빛(처음 0.2초는 붉은빛에서 넘어간다)
            float blend = Mathf.Clamp01((Time.time - _heatStart) / 0.2f);
            Level = Mathf.Lerp(0.6f, 0.62f, blend) * Mathf.Sqrt(k) * (0.85f + 0.15f * Mathf.Sin(Time.time * 5f));   // 천천히 식는다(√)
            s_mpb ??= new MaterialPropertyBlock();
            s_mpb.SetColor(FlashColorId, Color.Lerp(Glow, Heat, blend) * (0.85f * Level * Game.Core.DisplaySettings.FlashScale));
            foreach (var l in _layers)
            {
                if (l.Overlay == null) continue;
                bool show = Level > 0.01f && l.Source != null && l.Source.enabled && l.Source.gameObject.activeInHierarchy;
                if (l.Overlay.enabled != show) l.Overlay.enabled = show;
                if (show) l.Overlay.SetPropertyBlock(s_mpb);
            }
            // 흰 증기: 갑판 여기저기서 피어오른다(초당 약 18 · 식을수록 줄어듦)
            _steamDebt += dt * 18f * (0.4f + 0.6f * k);
            var col = _ship.GetComponent<BoxCollider>();
            Vector3 size = col != null ? col.size : new Vector3(3f, 2f, 10f);
            while (_steamDebt >= 1f)
            {
                _steamDebt -= 1f;
                Vector3 local = new(Random.Range(-0.4f, 0.4f) * size.x, size.y * 0.5f + 0.6f, Random.Range(-0.45f, 0.45f) * size.z);
                Vector3 at = _ship.transform.TransformPoint(local);
                float g = Random.Range(0.82f, 0.95f);
                DecorFx.Emit(DecorFx.WreckSmoke, at, Vector3.up * Random.Range(1.5f, 3f) + DecorFx.Wind * 0.5f, Random.Range(0.9f, 1.6f),
                    Random.Range(0.6f, 1.1f), new Color(g, g, g, 0.5f));
            }
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var gh = _ghosts[i];
                float u = (Time.time - gh.Born) / GhostLife;
                if (u >= 1f || gh.Root == null) { if (gh.Root != null) Destroy(gh.Root); _ghosts.RemoveAt(i); continue; }
                s_mpb.SetColor(FlashColorId, Glow * (0.55f * (1f - u) * (1f - u) * Game.Core.DisplaySettings.FlashScale));
                foreach (var part in gh.Parts) if (part != null) part.SetPropertyBlock(s_mpb);
            }
        }

        private void SpawnGhost()
        {
            if (_big.Count == 0 || s_material == null) return;
            var root = new GameObject("Overdrive afterimage");
            var parts = new List<MeshRenderer>(_big.Count);
            foreach (var (mf, r) in _big)
            {
                if (mf == null || r == null || !r.enabled) continue;
                var go = new GameObject("Part", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
                go.transform.localScale = mf.transform.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.GetComponent<MeshRenderer>();
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = s_material;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                parts.Add(mr);
            }
            _ghosts.Add(new Ghost { Root = root, Parts = parts.ToArray(), Born = Time.time });
        }

        private void OnDestroy()
        {
            foreach (var l in _layers) if (l.Overlay != null) Destroy(l.Overlay.gameObject);
            foreach (var g in _ghosts) if (g.Root != null) Destroy(g.Root);
            if (Active == this) Active = null;
        }
    }
}
