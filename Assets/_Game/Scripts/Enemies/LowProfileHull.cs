using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 반잠수정 공통(2026-11-19 낮과 밤): 물에 거의 잠긴 낮은 선체와 숨음 상태.
    ///   - 외형: 원래 보트 모델을 위아래로 눌러(높이 45%) 수면에 붙이고, 어두운 회청색으로 칠한다(MaterialPropertyBlock — 재질 원본은 그대로).
    ///     항해등이 없다(밤에 불빛으로 보이지 않는다).
    ///   - 숨음: 드러남 타이머가 0이면 Stealthy. 소나 범위 · 조명탄 · 탐조등 · 폭뢰 폭발 · 기함 근접(14m)이 타이머를 채운다.
    ///   - 드러나지 않으면 적아 식별 표식도 달리지 않는다(IsRevealed = false).
    /// </summary>
    public sealed class LowProfileHull
    {
        public const float NearReveal = 14f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Hull = new(0.1f, 0.12f, 0.13f, 1f);

        private float _detected;
        private bool _shaped;
        private MaterialPropertyBlock _mpb;

        public bool Stealthy => _detected <= 0f;
        public void MarkDetected(float seconds) => _detected = Mathf.Max(_detected, seconds);

        /// <summary>처음 한 번 외형을 바꾼다(풀에서 다시 나와도 그대로).</summary>
        public void Shape(EnemyController owner)
        {
            if (_shaped || owner == null) return;
            _shaped = true;
            // 모델 뿌리(첫 렌더러가 붙은 자식)를 위아래로 누른다
            foreach (Transform child in owner.transform)
            {
                if (child.GetComponentInChildren<Renderer>() == null) continue;
                var s = child.localScale;
                child.localScale = new Vector3(s.x, s.y * 0.45f, s.z);
                break;
            }
            Repaint(owner);
        }

        /// <summary>적 도색(EnemyPaint) 뒤에 덮어 칠한다.</summary>
        public void Repaint(EnemyController owner)
        {
            _mpb ??= new MaterialPropertyBlock();
            foreach (var r in owner.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, Hull);
                r.SetPropertyBlock(_mpb);
            }
        }

        public void Tick(EnemyController owner, Transform player, float dt)
        {
            if (_detected > 0f) _detected -= dt;
            if (player != null)
            {
                Vector3 d = player.position - owner.transform.position; d.y = 0f;
                if (d.sqrMagnitude <= NearReveal * NearReveal) MarkDetected(0.5f);
            }
        }

        public void Reset() => _detected = 0f;
    }
}
