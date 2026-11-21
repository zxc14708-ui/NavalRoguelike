using UnityEngine;
using Game.Combat;

namespace Game.Enemies
{
    /// <summary>
    /// 반잠수 자폭정: 낮게 다가와 기함을 들이받는다(자폭 보트와 같은 돌진). (2026-11-19 낮과 밤 — 밤 구간 적)
    /// 물에 거의 잠긴 저피탐 선체(LowProfileHull): 레이더 탐지 거리 40%, 소나 · 조명탄 · 탐조등 · 근접에 드러난다. 폭뢰에도 맞는다.
    /// 포위 기습(2026-11-21, Game.Missions.PincerAttack): BeginAmbush — 물속에 잠긴 채 나와(모델이 2m 아래) 거품을 뿜으며 떠오르고,
    ///   물보라와 함께 수면에 오르면 드러난 채(12초) 기함을 쫓아 들이받는다. 기습 중에는 35% 빠르다(기함 최고속력보다 빠르게).
    /// </summary>
    public class SemiSubSuicideBoat : SuicideBoat, ILowObservable, IIlluminable
    {
        public const float SubmergedDepth = 2f, AmbushSpeed = 1.35f, AmbushReveal = 12f;

        private readonly LowProfileHull _hull = new();
        private Transform _model;
        private Vector3 _modelBase;
        private float _riseLeft, _riseTotal;
        private bool _ambush;

        public bool Stealthy => _hull.Stealthy;
        public void MarkDetected(float seconds) => _hull.MarkDetected(seconds);
        public void Illuminate(float seconds) => _hull.MarkDetected(seconds);
        /// <summary>드러나지 않으면 표식도 달리지 않는다.</summary>
        public override bool IsRevealed => !_hull.Stealthy;
        /// <summary>포위 기습으로 떠오르는 중인가 · 기습 중인가(검증).</summary>
        public bool Surfacing => _riseLeft > 0f;
        public bool Ambushing => _ambush;

        protected override float SpeedMultiplier => _ambush ? AmbushSpeed : 1f;

        public override void Setup(Game.Data.EnemyDefinition def, Transform player)
        {
            base.Setup(def, player);
            _hull.Reset();
            _hull.Shape(this);
            _hull.Repaint(this);
            if (_model != null) _model.localPosition = _modelBase;
            _riseLeft = 0f; _ambush = false;
        }

        /// <summary>포위 기습: 물속에서 riseSeconds 동안 떠오른 뒤 기함을 쫓는다.</summary>
        public void BeginAmbush(float riseSeconds)
        {
            if (_model == null)
            {
                foreach (Transform child in transform)
                    if (child.GetComponentInChildren<Renderer>() != null) { _model = child; _modelBase = child.localPosition; break; }
            }
            _ambush = true;
            _riseTotal = _riseLeft = Mathf.Max(0.1f, riseSeconds);
            if (_model != null) _model.localPosition = _modelBase + Vector3.down * SubmergedDepth;
            _hull.MarkDetected(riseSeconds + AmbushReveal);
        }

        protected override void UpdateBehaviour(float dt)
        {
            _hull.Tick(this, Player, dt);
            if (_riseLeft > 0f)
            {
                _riseLeft -= dt;
                float k = 1f - Mathf.Clamp01(_riseLeft / _riseTotal);
                float e = 1f - (1f - k) * (1f - k);
                if (_model != null) _model.localPosition = _modelBase + Vector3.down * (SubmergedDepth * (1f - e));
                Vector3 p = transform.position;
                if (Random.value < 0.6f)   // 떠오르는 거품
                    Game.View.DecorFx.Emit(Game.View.DecorFx.Spray, new Vector3(p.x, -0.7f, p.z) + Random.insideUnitSphere * 1.6f,
                        Vector3.up * Random.Range(0.8f, 2f), Random.Range(0.5f, 0.9f), Random.Range(0.5f, 1f), new Color(0.9f, 0.96f, 1f, 0.6f));
                if (_riseLeft <= 0f)
                {
                    if (_model != null) _model.localPosition = _modelBase;
                    Game.View.Explosions.Splash(p, 1f);   // 물보라를 뚫고 떠오른다(불 없이)
                }
                return;
            }
            base.UpdateBehaviour(dt);
        }
    }
}
