using System.Collections.Generic;
using UnityEngine;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// 형태별 모델 교체(Codex SonarPositionPack · DepthChargePositionPack → Resources/Modules/Variants/…).
    /// 기본 모델(ModelPivot)은 숨기고 형태 모델을 블록 중심에 붙인다. 방향은 블록 회전과 무관하게 함선 기준으로 맞춘다:
    ///   선수·예인·함내 소나와 투하대는 ForwardMarker 소켓을 함수 쪽으로, 측면 발사대는 OutboardDirection 소켓을 트인 현측으로.
    /// 형태 모델 프리팹이 없으면 기본 모델을 그대로 쓴다(이름표·동작만 바뀐다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModuleVariantVisual : MonoBehaviour
    {
        private static readonly Dictionary<string, GameObject> s_prefabs = new();

        private Transform _original;
        private Transform _current;
        private ModuleVariant _variant;

        public ModuleVariant Variant => _variant;
        public Transform Current => _current;

        /// <summary>형태 모델을 붙인다(같은 형태·현측이면 그대로 둔다). 붙인 모델 루트, 없으면 null.</summary>
        public static Transform Apply(ModuleRuntime runtime, ModuleVariant variant, ModuleSides sides)
        {
            if (runtime == null) return null;
            if (!runtime.TryGetComponent<ModuleVariantVisual>(out var v)) v = runtime.gameObject.AddComponent<ModuleVariantVisual>();
            return v.Set(variant, sides);
        }

        private Transform Set(ModuleVariant variant, ModuleSides sides)
        {
            if (_original == null) _original = transform.Find("ModelPivot");
            if (_current != null) { Destroy(_current.gameObject); _current = null; }
            _variant = variant;

            string name = ModuleVariants.ModelName(variant);
            var prefab = name != null ? Load(name) : null;
            if (prefab == null)
            {
                if (_original != null) _original.gameObject.SetActive(true);
                return null;
            }

            var go = Instantiate(prefab, transform, false);
            go.name = "Variant " + name;
            _current = go.transform;
            SetLayer(go, gameObject.layer);
            if (_original != null) _original.gameObject.SetActive(false);

            // 함선 기준 방향: 격자(함선)의 앞 = 함수, 오른쪽 = 우현
            var ship = GetComponentInParent<ShipGrid>();
            Transform frame = ship != null ? ship.transform : transform.parent != null ? transform.parent : transform;
            _current.rotation = frame.rotation;
            Vector3 want = frame.forward;
            string socket = "ForwardMarker";
            if (variant == ModuleVariant.DepthChargeProjector)
            {
                socket = "OutboardDirection";
                want = (sides & ModuleSides.Starboard) != 0 ? frame.right : -frame.right;
            }
            var s = Find(_current, socket);
            if (s != null)
            {
                Vector3 have = Vector3.ProjectOnPlane(s.position - _current.position, frame.up);
                if (have.sqrMagnitude > 1e-6f)
                    _current.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(have, Vector3.ProjectOnPlane(want, frame.up), frame.up), frame.up) * _current.rotation;
            }
            return _current;
        }

        private static GameObject Load(string name)
        {
            if (!s_prefabs.TryGetValue(name, out var p))
            {
                p = Resources.Load<GameObject>($"Modules/Variants/{name}");
                s_prefabs[name] = p;
            }
            return p;
        }

        public static Transform Find(Transform t, string name)
        {
            if (t == null) return null;
            string n = t.name;
            int dot = n.IndexOf('.');
            if ((dot > 0 ? n.Substring(0, dot) : n) == name) return t;
            foreach (Transform c in t)
            {
                var f = Find(c, name);
                if (f != null) return f;
            }
            return null;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }
    }
}
