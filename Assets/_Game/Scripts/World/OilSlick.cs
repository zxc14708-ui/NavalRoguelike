using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// 불타는 기름띠(내해, 2026-11-16): 플랫폼 포대가 부서지면 플랫폼 아래 수면에 번진다. 반경 안의 기함은 초당 damagePerSecond 피해.
    /// lifetime 뒤 사그라든다. 불꽃 · 검은 연기 · 짙은 수면 얼룩.
    /// </summary>
    public sealed class OilSlick : MonoBehaviour
    {
        public const float Radius = 9f, Lifetime = 22f, DamagePerSecond = 3f;
        private float _age, _debt;
        private static Material s_mat, s_glowMat;
        private Transform _stain, _glow;
        private Material _glowInstance;

        public static int Active { get; private set; }

        public static OilSlick Spawn(Vector3 at)
        {
            var go = new GameObject("Burning oil slick");
            go.transform.position = new Vector3(at.x, 0f, at.z);
            var s = go.AddComponent<OilSlick>();
            s.BuildStain();
            Game.Core.GameEvents.RaiseBanner("불타는 기름띠 — 들어가면 선체가 탑니다");
            return s;
        }

        private void OnEnable() => Active++;
        private void OnDisable() => Active--;

        private void BuildStain()
        {
            if (s_mat == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                s_mat = new Material(shader) { name = "Oil slick (Runtime)", color = new Color(0.04f, 0.03f, 0.02f, 0.72f) };
                s_glowMat = new Material(shader) { name = "Oil slick glow (Runtime)", color = new Color(1f, 0.45f, 0.08f, 0.35f) };
            }
            // 둥글고 들쭉날쭉한 기름띠(반경 1 기준 — 자라면서 키운다). 안쪽은 불붙은 주황빛이 일렁인다.
            _stain = Disc("Stain", s_mat, 0.06f, 1f, 0.22f);
            _glowInstance = new Material(s_glowMat);
            _glow = Disc("Burning sheen", _glowInstance, 0.08f, 0.72f, 0.3f);
        }

        private Transform Disc(string name, Material mat, float lift, float scale, float rough)
        {
            const int n = 28;
            var verts = new Vector3[n + 1];
            var cols = new Color[n + 1];
            var tris = new int[n * 3];
            float ph = Random.value * 6f, ph2 = Random.value * 6f;
            verts[0] = Vector3.zero; cols[0] = Color.white;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                float rr = scale * (1f + rough * (0.6f * Mathf.Sin(a * 3f + ph) + 0.4f * Mathf.Sin(a * 7f + ph2)));
                verts[i + 1] = new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                cols[i + 1] = new Color(1f, 1f, 1f, 0.15f);   // 가장자리는 옅게(정점색 알파)
                tris[i * 3] = 0; tris[i * 3 + 1] = (i + 1) % n + 1; tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = name, vertices = verts, colors = cols, triangles = tris };
            mesh.RecalculateNormals();
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, IslandBuilder.SeaY + lift, 0f);
            go.transform.localScale = Vector3.one * 0.1f;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        private void OnDestroy()
        {
            if (_glowInstance != null) Destroy(_glowInstance);
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float grow = Mathf.Clamp01(_age / 2.5f), fade = Mathf.Clamp01((Lifetime - _age) / 4f);
            float r = Radius * grow;
            if (_stain != null) _stain.localScale = Vector3.one * Mathf.Max(0.1f, r * 1.05f);
            if (_glow != null)
            {
                _glow.localScale = Vector3.one * Mathf.Max(0.1f, r * 1.05f);
                _glow.Rotate(0f, 6f * dt, 0f);
                if (_glowInstance != null) _glowInstance.color = new Color(1f, 0.45f, 0.08f, (0.22f + 0.12f * Mathf.Sin(_age * 5.3f)) * fade);
            }
            if (_age >= Lifetime) { Destroy(gameObject); return; }

            _debt += dt * 40f * fade;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                var d = Random.insideUnitCircle * r;
                Vector3 p = transform.position + new Vector3(d.x, IslandBuilder.SeaY + 0.2f, d.y);
                Game.View.DecorFx.Emit(Game.View.DecorFx.Fire, p, Vector3.up * Random.Range(0.6f, 1.4f) + Game.View.DecorFx.Wind * 0.2f,
                    Random.Range(0.5f, 1.0f), Random.Range(1.4f, 2.6f), new Color(1f, Random.Range(0.35f, 0.6f), 0.08f, 0.9f));
                if (Random.value < 0.3f)
                    Game.View.DecorFx.Emit(Game.View.DecorFx.WreckSmoke, p + Vector3.up, Vector3.up * 1.6f + Game.View.DecorFx.Wind * 0.5f,
                        Random.Range(2f, 3.2f), Random.Range(1.2f, 2f), new Color(0.06f, 0.06f, 0.07f, 0.6f));
            }

            var gm = Game.Core.GameManager.Instance;
            var ship = gm != null ? gm.Player : null;
            if (ship == null || fade <= 0f) return;
            Vector3 dp = ship.transform.position - transform.position; dp.y = 0f;
            if (dp.sqrMagnitude <= r * r) ship.ApplyHullDamage(DamagePerSecond * dt, "불타는 기름띠");
        }
    }
}
