using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>유전 플랫폼 가스 불기둥(장식): FlareTip에서 불꽃 · 연기를 계속 뿜는다. 멀면 쉰다.</summary>
    public sealed class FlareFlame : MonoBehaviour
    {
        private Transform _tip;
        private float _size, _debt;

        public void Init(Transform tip, float size) { _tip = tip; _size = size; }

        private void Update()
        {
            if (_tip == null) return;
            var cam = Camera.main;
            if (cam != null && (cam.transform.position - _tip.position).sqrMagnitude > 260f * 260f) return;
            _debt += Time.deltaTime * 16f;
            while (_debt >= 1f)
            {
                _debt -= 1f;
                Vector3 p = _tip.position + Random.insideUnitSphere * 0.25f * _size;
                Game.View.DecorFx.Emit(Game.View.DecorFx.Fire, p, Vector3.up * Random.Range(1.2f, 2.2f) * _size + Game.View.DecorFx.Wind * 0.3f,
                    Random.Range(0.35f, 0.6f), Random.Range(0.9f, 1.5f) * _size, new Color(1f, Random.Range(0.45f, 0.75f), 0.12f, 0.9f));
                if (Random.value < 0.35f)
                    Game.View.DecorFx.Emit(Game.View.DecorFx.WreckSmoke, p + Vector3.up * 1.5f * _size, Vector3.up * 1.4f + Game.View.DecorFx.Wind * 0.5f,
                        Random.Range(2f, 3f), Random.Range(1.2f, 1.8f) * _size, new Color(0.12f, 0.12f, 0.13f, 0.5f));
            }
        }
    }
}
