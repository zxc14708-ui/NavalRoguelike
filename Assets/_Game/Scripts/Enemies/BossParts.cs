using System.Collections.Generic;
using UnityEngine;
using Game.Combat;
using Game.Core;
using Game.Data;
using Game.View;

namespace Game.Enemies
{
    /// <summary>
    /// 보스 부위 파괴(2026-11-01). 보스의 무장 부위(함포·발사대 등)마다 체력을 따로 두고,
    /// 맞은 자리(DamageInfo.HitPoint)에서 반경 안의 가장 가까운 살아 있는 부위가 선체와 같은 피해를 함께 받는다.
    /// 부위가 부서지면 폭발 · 검게 그을림 · 계속 연기가 나고, 보스는 그 부위가 맡던 공격을 더 쓰지 않는다(보스 코드가 IsDestroyed로 확인).
    /// 선체 체력은 그대로라 부위를 노려도 격침 시간은 같다 — 대신 위험한 공격을 먼저 끌 수 있다.
    /// 광역 피해(곡사포·폭뢰처럼 표적 중심에 들어오는 피해)는 맞은 자리가 없어 선체만 깎는다.
    /// </summary>
    public sealed class BossParts
    {
        public sealed class Part
        {
            public string Name, Effect;
            public Transform Anchor;
            public float Radius, MaxHp, Hp;
            public bool Destroyed;
            internal float SmokeDebt;
            internal readonly List<Renderer> Renderers = new();
            public float HpFraction => MaxHp > 0f ? Mathf.Clamp01(Hp / MaxHp) : 0f;
        }

        private readonly List<Part> _parts = new();
        private static MaterialPropertyBlock s_mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public IReadOnlyList<Part> Parts => _parts;
        /// <summary>검증용: 어떤 보스의 어떤 부위가 부서졌다.</summary>
        public static event System.Action<EnemyController, Part> PartDestroyed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => PartDestroyed = null;

        /// <summary>부위를 모두 지운다(그을림도 되돌린다). 풀에서 다시 나올 때.</summary>
        public void Clear()
        {
            foreach (var p in _parts) foreach (var r in p.Renderers) if (r != null) r.SetPropertyBlock(null);
            _parts.Clear();
        }

        /// <summary>부위 추가. visual이 있으면 그 아래 렌더러를 부서질 때 그을린다. anchor가 없으면 추가하지 않는다.</summary>
        public Part Add(string name, string effect, Transform anchor, float radius, float hp, Transform visual = null)
        {
            if (anchor == null) return null;
            var p = new Part { Name = name, Effect = effect, Anchor = anchor, Radius = radius, MaxHp = hp, Hp = hp };
            if (visual != null) p.Renderers.AddRange(visual.GetComponentsInChildren<Renderer>(true));
            _parts.Add(p);
            return p;
        }

        public bool IsDestroyed(Part p) => p != null && p.Destroyed;

        /// <summary>
        /// 맞은 자리에서 반경 안의 가장 가까운(수평 거리) 살아 있는 부위에 같은 피해. 이번에 부서졌으면 그 부위를 돌려준다.
        /// </summary>
        public Part Hit(in DamageInfo info, EnemyController owner)
        {
            if (info.Amount <= 0f || info.HitPoint == Vector3.zero) return null;
            Part best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in _parts)
            {
                if (p.Destroyed || p.Anchor == null) continue;
                Vector3 d = info.HitPoint - p.Anchor.position; d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr <= p.Radius * p.Radius && sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            if (best == null) return null;
            best.Hp -= info.Amount;
            if (best.Hp > 0f) return null;
            best.Hp = 0f;
            best.Destroyed = true;
            OnDestroyed(owner, best);
            return best;
        }

        private static void OnDestroyed(EnemyController owner, Part p)
        {
            Vector3 at = p.Anchor.position + Vector3.up * 0.4f;
            Explosions.Spawn(at, 1.3f, Explosions.Kind.Impact);
            AudioManager.Play(SfxId.Explosion, at, 0.9f, 0.85f);
            s_mpb ??= new MaterialPropertyBlock();
            foreach (var r in p.Renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(s_mpb);
                var c = new Color(0.16f, 0.15f, 0.14f, 1f);   // 그을린 쇠
                s_mpb.SetColor(BaseColorId, c);
                s_mpb.SetColor(ColorId, c);
                r.SetPropertyBlock(s_mpb);
            }
            GameEvents.RaiseBossPhaseChanged($"부위 파괴: {p.Name} — {p.Effect}");
            CombatLog.Add("보스 부위 파괴", $"{p.Name} · {p.Effect}");
            PartDestroyed?.Invoke(owner, p);
        }

        /// <summary>부서진 부위에서 계속 연기가 난다.</summary>
        public void Tick(float dt)
        {
            foreach (var p in _parts)
            {
                if (!p.Destroyed || p.Anchor == null) continue;
                p.SmokeDebt += dt * 7f;
                while (p.SmokeDebt >= 1f)
                {
                    p.SmokeDebt -= 1f;
                    Vector3 pos = p.Anchor.position + Vector3.up * 0.3f + Random.insideUnitSphere * 0.25f;
                    DecorFx.Emit(DecorFx.WreckSmoke, pos, Vector3.up * Random.Range(1.2f, 2f) + DecorFx.Wind * 0.4f,
                        Random.Range(1.4f, 2.2f), Random.Range(0.6f, 1.1f), new Color(0.1f, 0.1f, 0.1f, 0.65f));
                }
            }
        }
    }
}
