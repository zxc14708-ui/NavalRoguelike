using UnityEngine;

namespace Game.UI
{
    /// <summary>정비창 확대 중 문형 크레인의 운반 장치와 고리를 움직인다. 좌표는 축소 전 섬 로컬 단위.</summary>
    public sealed class HarborCraneMotion
    {
        private sealed class Hoist
        {
            public Transform Trolley, Cable, Hook;
            public Vector3 TrolleyStart, CableScale, HookStart;
            public float Span, Period, Lift;

            public void Apply(float time)
            {
                float slide = Mathf.Sin(time * (2f * Mathf.PI / Period)) * Span;
                float liftScale = 1f + Mathf.Sin(time * (2f * Mathf.PI / (Period * .73f))) * Lift;
                Trolley.localPosition = TrolleyStart + Vector3.right * slide;
                Cable.localScale = Vector3.Scale(CableScale, new Vector3(1f, liftScale, 1f));
                Hook.localPosition = new Vector3(HookStart.x, -.5f + (HookStart.y + .5f) * liftScale, HookStart.z);
            }
        }

        private readonly Transform _workshop;
        private readonly Hoist _large, _small;
        private bool _focused;
        private float _time;

        public HarborCraneMotion(Transform details)
        {
            _workshop = details != null ? details.Find("Workshop") : null;
            _large = FindHoist("Large", 4f, 16f, .08f);
            _small = FindHoist("Small", 3f, 13f, .10f);
        }

        private Hoist FindHoist(string prefix, float span, float period, float lift)
        {
            var trolley = _workshop != null ? _workshop.Find(prefix + "CraneTrolley") : null;
            var cable = trolley != null ? trolley.Find(prefix + "CraneCable") : null;
            var hook = trolley != null ? trolley.Find(prefix + "CraneHook") : null;
            if (trolley == null || cable == null || hook == null) return null;
            return new Hoist { Trolley = trolley, Cable = cable, Hook = hook,
                TrolleyStart = trolley.localPosition, CableScale = cable.localScale, HookStart = hook.localPosition,
                Span = span, Period = period, Lift = lift };
        }

        public void FocusAt(Vector3 islandLocal)
        {
            _focused = _workshop != null && Mathf.Abs(islandLocal.x - _workshop.localPosition.x) < .1f
                && Mathf.Abs(islandLocal.z - _workshop.localPosition.z) < .1f;
        }

        public void Stop() => _focused = false;

        /// <summary>정지 설정과 일시 정지는 호출자가 전달하는 dt=0을 따른다.</summary>
        public void Tick(float dt)
        {
            if (!_focused || dt <= 0f) return;
            _time += dt;
            _large?.Apply(_time);
            _small?.Apply(_time);
        }
    }
}
