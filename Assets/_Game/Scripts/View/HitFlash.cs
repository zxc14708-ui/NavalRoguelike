using System.Collections.Generic;
using UnityEngine;

namespace Game.View
{
    /// <summary>
    /// 피격 번쩍임(2026-11-20 타격감): 맞은 적이 0.06~0.12초 하얗게 번쩍인다.
    ///   - 방법: 처음 맞을 때 메시 렌더러마다 같은 메시를 그리는 자식("Hit flash")을 하나씩 만들어 두고(꺼 둠),
    ///     맞으면 잠깐 켜서 더하기 합성으로 빛을 얹는다(Naval/HitFlash · Resources/Effects/MAT_HitFlash).
    ///     원래 렌더러 · 재질 · 재질 블록은 건드리지 않는다 — 보스 부위 그을림 · 반잠수정 덧칠 · 램프 깜박임과 부딪히지 않는다.
    ///   - 세기 · 길이: 한 발 피해가 클수록(기관포 2 → 76mm 24) 세고 길다. 설정 "번쩍임 줄이기"를 따른다.
    ///   - 원래 렌더러가 꺼져 있으면(잠수함 잠항 · 부서진 부위) 그 조각은 번쩍이지 않는다 — 숨은 적이 드러나지 않게.
    ///   - 덧그림 재질은 투명 큐라 격침 잔해(ShipWreck)가 복사하지 않는다.
    /// </summary>
    public sealed class HitFlash : MonoBehaviour
    {
        public const string LayerName = "Hit flash";
        public const float MinDuration = 0.06f, MaxDuration = 0.12f;
        /// <summary>번쩍임 색(HDR 배율 전) — 살짝 따뜻한 흰색.</summary>
        public static readonly Color Tint = new(1f, 0.93f, 0.82f);
        /// <summary>가장 센 번쩍임(76mm 한 발)에 더하는 밝기.</summary>
        public const float Brightness = 0.85f;
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static Material s_material;
        private static bool s_loaded;
        private static MaterialPropertyBlock s_mpb;

        private struct Layer { public Renderer Source; public MeshRenderer Overlay; }
        private readonly List<Layer> _layers = new();
        private bool _built, _on, _fresh;
        private float _t, _duration, _peak;

        /// <summary>검증용: 지금까지 번쩍인 횟수.</summary>
        public static int Flashes { get; private set; }
        /// <summary>검증용: 덧그림 조각 수 · 지금 세기(0이면 꺼짐) · 지금 켜진 조각 수.</summary>
        public int LayerCount => _layers.Count;
        public float Level { get; private set; }
        public int Shown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Flashes = 0; }

        /// <summary>한 발 피해로 세기를 정한다(1 = 76mm 한 발쯤).</summary>
        public static float StrengthFor(float damage) => Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(1f, 24f, damage));

        /// <summary>target(적 뿌리)을 번쩍인다. 모델이 없거나 재질을 못 찾으면 아무 일도 하지 않는다.</summary>
        public static HitFlash Hit(Component target, float damage)
        {
            if (target == null || damage <= 0f) return null;
            if (!target.TryGetComponent<HitFlash>(out var f)) f = target.gameObject.AddComponent<HitFlash>();
            f.Trigger(StrengthFor(damage));
            return f;
        }

        private void Trigger(float strength)
        {
            if (!_built) Build();
            if (_layers.Count == 0) return;
            // 겹쳐 맞으면 남은 빛보다 약해지지 않게
            float remaining = _on ? Level : 0f;
            _peak = Mathf.Max(strength, remaining);
            _duration = Mathf.Lerp(MinDuration, MaxDuration, Mathf.InverseLerp(0.45f, 1f, _peak));
            _t = 0f;
            _on = true;
            _fresh = true;
            Flashes++;
        }

        private void Build()
        {
            _built = true;
            if (!s_loaded)
            {
                s_loaded = true;
                s_material = Resources.Load<Material>("Effects/MAT_HitFlash");
            }
            if (s_material == null) return;
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null || r.name == LayerName || r.name == "Night lamp") continue;
                if (!r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                var mats = r.sharedMaterials;
                bool opaque = mats.Length > 0;
                foreach (var m in mats) if (m == null || m.renderQueue >= 2500) { opaque = false; break; }   // 표시용 반투명(원 · 표식)은 빼고 선체만
                if (!opaque) continue;

                var go = new GameObject(LayerName, typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = r.gameObject.layer;
                go.transform.SetParent(r.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var overlay = go.GetComponent<MeshRenderer>();
                var flash = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < flash.Length; i++) flash[i] = s_material;
                overlay.sharedMaterials = flash;
                overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                overlay.receiveShadows = false;
                overlay.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                overlay.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                overlay.enabled = false;
                _layers.Add(new Layer { Source = r, Overlay = overlay });
            }
        }

        private void LateUpdate()
        {
            if (!_on) return;
            // 맞은 프레임은 시간을 흘리지 않는다 — 프레임이 느려도(짧은 번쩍임보다 한 프레임이 길어도) 한 번은 가장 밝게 보인다
            if (_fresh) _fresh = false;
            else _t += Time.deltaTime;
            if (_t >= _duration) { Hide(); return; }
            // 처음 30%는 가장 밝게, 나머지는 빠르게 꺼진다
            float u = Mathf.Clamp01((_t - _duration * 0.3f) / (_duration * 0.7f));
            float k = 1f - u;
            Level = _peak * k * k;
            s_mpb ??= new MaterialPropertyBlock();
            // 최대 0.85를 더한다 — 1을 넘기면 배가 하얀 실루엣으로 날아가 갑판 · 함교 모양이 사라진다
            s_mpb.SetColor(FlashColorId, Tint * (Brightness * Level * Game.Core.DisplaySettings.FlashScale));
            int shown = 0;
            for (int i = 0; i < _layers.Count; i++)
            {
                var l = _layers[i];
                if (l.Overlay == null) continue;
                bool show = l.Source != null && l.Source.enabled && l.Source.gameObject.activeInHierarchy;
                if (l.Overlay.enabled != show) l.Overlay.enabled = show;
                if (!show) continue;
                l.Overlay.SetPropertyBlock(s_mpb);
                shown++;
            }
            Shown = shown;
        }

        private void Hide()
        {
            _on = false;
            Level = 0f;
            Shown = 0;
            foreach (var l in _layers) if (l.Overlay != null && l.Overlay.enabled) l.Overlay.enabled = false;
        }

        // 풀로 돌아가거나 꺼지면 빛도 끈다
        private void OnDisable() => Hide();
    }
}
