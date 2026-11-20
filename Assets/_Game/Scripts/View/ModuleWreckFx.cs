using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Modules;

namespace Game.View
{
    /// <summary>
    /// 부서진 블록 겉모습(2026-11-21). 예전에는 블록이 부서져도 모델이 그대로라(레이더 안테나만 숨김) 화면 구석 도해를 보지 않으면 몰랐다.
    ///   - 부서지는 순간: 작은 폭발 · 불똥.
    ///   - 부서진 동안: 렌더러를 그을린 쇳빛으로(재질 블록만 — 원래 재질은 그대로, 되살리면 예전 재질 블록으로 되돌림),
    ///     블록이 6~10° 기울고 조금 내려앉음, 처음 6초는 불길 · 계속 검은 연기.
    ///   - 정비 수리 · 임시 복구(ModuleInstance.Restore)나 철거되면 원래대로.
    /// 기함 뿌리에 붙어 연기를 낸다. 모듈 이벤트는 정적으로 한 번만 구독한다.
    /// </summary>
    public sealed class ModuleWreckFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly Color Scorched = new(0.17f, 0.155f, 0.14f, 1f);
        public const float FireSeconds = 6f;

        private sealed class Entry
        {
            public ModuleInstance Module;
            public Transform Root;
            public Vector3 LocalPos;
            public Quaternion LocalRot;
            public readonly List<(Renderer r, MaterialPropertyBlock saved, bool had)> Renderers = new();
            public float Age, SmokeDebt, FireDebt;
        }

        private readonly List<Entry> _entries = new();
        /// <summary>검증용: 지금 그을린 블록 수.</summary>
        public int Count => _entries.Count;
        /// <summary>검증용: 이 블록이 지금 그을려 있나.</summary>
        public bool IsShown(ModuleInstance m) => _entries.Exists(e => e.Module == m);
        /// <summary>검증용: 부서지기 전 자세에서 기운 각도(그을리지 않았으면 0).</summary>
        public float TiltOf(ModuleInstance m)
        {
            var e = _entries.Find(x => x.Module == m);
            return e != null && e.Root != null ? Quaternion.Angle(e.Root.localRotation, e.LocalRot) : 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Subscribe()
        {
            GameEvents.ModuleDestroyed -= OnDestroyed;
            GameEvents.ModuleRestored -= OnRestored;
            GameEvents.ModuleRemoved -= OnRestored;
            GameEvents.ModuleDestroyed += OnDestroyed;
            GameEvents.ModuleRestored += OnRestored;
            GameEvents.ModuleRemoved += OnRestored;
        }

        private static void OnDestroyed(ModuleInstance m)
        {
            var rt = m?.Runtime;
            if (rt == null) return;
            var ship = rt.GetComponentInParent<Game.Ship.ShipController>();
            if (ship == null) return;
            if (!ship.TryGetComponent<ModuleWreckFx>(out var fx)) fx = ship.gameObject.AddComponent<ModuleWreckFx>();
            fx.Add(m, rt.transform);
        }

        private static void OnRestored(ModuleInstance m)
        {
            var rt = m?.Runtime;
            var ship = rt != null ? rt.GetComponentInParent<Game.Ship.ShipController>() : null;
            if (ship != null && ship.TryGetComponent<ModuleWreckFx>(out var fx)) fx.Remove(m);
        }

        /// <summary>블록 기준 높이(갑판 위) 한가운데.</summary>
        private static Vector3 Center(Entry e) => e.Root.position + Vector3.up * 0.5f;

        private void Add(ModuleInstance m, Transform root)
        {
            if (root == null || _entries.Exists(e => e.Module == m)) return;
            var entry = new Entry { Module = m, Root = root, LocalPos = root.localPosition, LocalRot = root.localRotation };
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.name == HitFlash.LayerName || r.name == "Night lamp") continue;
                var saved = new MaterialPropertyBlock();
                bool had = r.HasPropertyBlock();
                if (had) r.GetPropertyBlock(saved);
                entry.Renderers.Add((r, saved, had));
                var mpb = new MaterialPropertyBlock();
                if (had) r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, Scorched);
                mpb.SetColor(ColorId, Scorched);
                r.SetPropertyBlock(mpb);
            }
            // 기울어 내려앉은 잔해(블록마다 다른 방향)
            var rng = new System.Random(m.InstanceId * 7919 + 13);
            float pitch = (float)(rng.NextDouble() * 2 - 1) * 9f, roll = (rng.NextDouble() < 0.5 ? -1f : 1f) * (6f + (float)rng.NextDouble() * 4f);
            root.localRotation = entry.LocalRot * Quaternion.Euler(pitch, 0f, roll);
            root.localPosition = entry.LocalPos + Vector3.down * 0.12f;
            _entries.Add(entry);

            Vector3 at = Center(entry);
            Explosions.Spawn(at, 1.0f, Explosions.Kind.Impact);
            for (int i = 0; i < 10; i++)
                DecorFx.Emit(DecorFx.Fire, at + Random.insideUnitSphere * 0.3f, Random.insideUnitSphere * 3.5f + Vector3.up * 2.5f,
                    Random.Range(0.25f, 0.5f), Random.Range(0.25f, 0.5f), new Color(1f, 0.6f, 0.2f, 0.95f));
        }

        private void Remove(ModuleInstance m)
        {
            int i = _entries.FindIndex(e => e.Module == m);
            if (i < 0) return;
            var e = _entries[i];
            foreach (var (r, saved, had) in e.Renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(had ? saved : null);
            }
            if (e.Root != null) { e.Root.localRotation = e.LocalRot; e.Root.localPosition = e.LocalPos; }
            _entries.RemoveAt(i);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Root == null || e.Module == null || !e.Module.IsDestroyed) { _entries.RemoveAt(i); continue; }
                e.Age += dt;
                Vector3 at = Center(e);
                float smokeRate = DisplaySettings.DecorDensity * (e.Age < FireSeconds ? 7f : 3.5f);
                e.SmokeDebt += smokeRate * dt;
                while (e.SmokeDebt >= 1f)
                {
                    e.SmokeDebt -= 1f;
                    float g = Random.Range(0.07f, 0.15f);
                    DecorFx.Emit(DecorFx.WreckSmoke, at + Random.insideUnitSphere * 0.25f, Vector3.up * Random.Range(1.3f, 2.2f) + DecorFx.Wind * 0.5f,
                        Random.Range(1.6f, 2.6f), Random.Range(0.6f, 1.1f), new Color(g, g, g, 0.62f));
                }
                if (e.Age < FireSeconds)
                {
                    e.FireDebt += 9f * (1f - e.Age / FireSeconds) * dt;
                    while (e.FireDebt >= 1f)
                    {
                        e.FireDebt -= 1f;
                        DecorFx.Emit(DecorFx.Fire, at + Random.insideUnitSphere * 0.25f, Vector3.up * Random.Range(0.8f, 1.8f),
                            Random.Range(0.25f, 0.5f), Random.Range(0.35f, 0.7f),
                            Color.Lerp(new Color(1f, 0.85f, 0.45f, 0.95f), new Color(1f, 0.38f, 0.1f, 0.85f), Random.value));
                    }
                }
            }
        }
    }
}
