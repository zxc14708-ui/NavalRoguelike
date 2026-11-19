using System.Collections.Generic;
using UnityEngine;
using Game.Enemies;

namespace Game.Combat
{
    /// <summary>
    /// 소노부이(2026-11-19 낮과 밤). 헬기 갑판을 "대잠 초계"로 두면 헬기가 순찰 지점마다 떨군다(AswHelicopter).
    ///   - 반경 Radius 안의 잠수함은 확정 접촉(대잠 무기가 위치를 안다 — 물 위로 띄우지는 않는다), 반잠수정은 드러난다.
    ///   - 갑판 하나당 MaxPerDeck개 — 넘으면 가장 오래된 것부터 가라앉는다. Life초 뒤 가라앉는다.
    ///   - 노란 부이(Codex PRJ_Sonobuoy — 원점 = 수면선, 없으면 원통) + 밤에 깜박이는 등(NightLampSet, 모델의 LampPoint).
    /// </summary>
    public sealed class Sonobuoy : MonoBehaviour
    {
        public const float Radius = 16f, Life = 40f;
        public const int MaxPerDeck = 3;
        private const float Interval = 0.5f;

        private static readonly Dictionary<object, List<Sonobuoy>> s_byDeck = new();
        private static readonly List<Sonobuoy> s_all = new();
        private static Material s_body;

        private object _owner;
        private float _age, _tick, _seed;

        public static IReadOnlyList<Sonobuoy> All => s_all;
        /// <summary>이 부이가 확정 접촉으로 만든 횟수(검증용).</summary>
        public int Contacts { get; private set; }
        /// <summary>떨군 소노부이 누계(검증용).</summary>
        public static int Dropped { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { s_byDeck.Clear(); s_all.Clear(); Dropped = 0; }

        public static int CountFor(object deck) => deck != null && s_byDeck.TryGetValue(deck, out var l) ? l.Count : 0;

        public static Sonobuoy Drop(object deck, Vector3 at)
        {
            at.y = Game.View.Waterline.WaterY;
            if (!Game.World.Islands.IsClear(at, 2f)) return null;
            if (!s_byDeck.TryGetValue(deck, out var list)) s_byDeck[deck] = list = new List<Sonobuoy>();
            while (list.Count >= MaxPerDeck) { var old = list[0]; list.RemoveAt(0); if (old != null) Destroy(old.gameObject); }

            var go = new GameObject("Sonobuoy");
            go.transform.position = at;
            var b = go.AddComponent<Sonobuoy>();
            b._owner = deck;
            b._seed = Random.value * 6f;
            var lampColor = new Color(1f, 0.85f, 0.2f);
            var model = Game.View.DayNightProps.Place(go.transform, Game.View.DayNightProps.Sonobuoy, Vector3.zero, Random.Range(0f, 360f));
            if (model != null)
            {
                model.name = "Sonobuoy body";
                Game.View.NightLampSet.On(go).Add(go.transform.InverseTransformPoint(model.transform.TransformPoint(Game.View.DayNightProps.Socket(model, "LampPoint", Vector3.zero))) + Vector3.up * 0.05f,
                    lampColor, 0.4f, 1.5f, 0.3f, b._seed);
            }
            else
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(body.GetComponent<Collider>());
                body.name = "Sonobuoy body";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                body.transform.localScale = new Vector3(0.45f, 0.35f, 0.45f);
                if (s_body == null)
                {
                    s_body = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Sonobuoy (Runtime)" };
                    var y = new Color(0.95f, 0.75f, 0.1f);
                    if (s_body.HasProperty("_BaseColor")) s_body.SetColor("_BaseColor", y);
                    s_body.color = y;
                }
                body.GetComponent<MeshRenderer>().sharedMaterial = s_body;
                Game.View.NightLampSet.On(go).Add(new Vector3(0f, 0.75f, 0f), lampColor, 0.4f, 1.5f, 0.3f, b._seed);
            }
            list.Add(b);
            s_all.Add(b);
            Dropped++;
            Game.Core.AudioManager.Play(Game.Data.SfxId.ShellSplash, at, 0.5f, 1.4f);
            return b;
        }

        private void OnDestroy()
        {
            s_all.Remove(this);
            if (_owner != null && s_byDeck.TryGetValue(_owner, out var l)) l.Remove(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= Life) { Destroy(gameObject); return; }
            var p = transform.position;
            transform.position = new Vector3(p.x, Game.View.Waterline.WaterY + Mathf.Sin(Time.time * 1.3f + _seed) * 0.06f, p.z);   // 물결에 출렁
            _tick -= dt;
            if (_tick > 0f) return;
            _tick = Interval;
            Vector3 c = transform.position; c.y = 0f;
            float r2 = Radius * Radius;
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Submarine))
            {
                if (t is not SubmarineBase sub || !sub.IsAlive) continue;
                Vector3 d = sub.transform.position - c; d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                if (!sub.IsContactConfirmed) Contacts++;
                sub.ConfirmContact(Interval + 1f);
            }
            foreach (var t in TargetRegistry.HostileTo(CombatFaction.Player, TargetKind.Surface))
            {
                if (t is not ILowObservable low || !t.IsAlive || t.Transform == null) continue;
                Vector3 d = t.Transform.position - c; d.y = 0f;
                if (d.sqrMagnitude <= r2) low.MarkDetected(Interval + 1f);
            }
        }
    }
}
