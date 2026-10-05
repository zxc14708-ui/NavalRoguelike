using UnityEngine;
using Game.Core;

namespace Game.Ship
{
    /// <summary>
    /// 플레이어가 직접 누르는 함선 스킬의 공통 뼈대(기만체 Q, 재밍 E, 응급 수리 R, 연막 F, 전속 Shift).
    /// 스킬 자체는 모듈이 제공한다. 이 컴포넌트는 입력을 받아 준비된 장비를 작동시키고,
    /// 준비 상태를 주기적으로 HUD에 알린다.
    /// </summary>
    public abstract class ActiveSkillBase<TSource> : MonoBehaviour where TSource : class
    {
        [SerializeField] protected ShipGrid grid;

        [Tooltip("HUD 갱신 간격(초)")]
        [SerializeField] private float statusInterval = 0.1f;

        private float _statusTimer;

        protected abstract bool WasKeyPressed();
        protected abstract void Subscribe();
        protected abstract void Unsubscribe();
        protected abstract void RaiseStatus(int ready, int total, float next01);

        protected abstract bool IsReady(TSource source);

        /// <summary>0 = 막 사용함, 1 = 준비 완료.</summary>
        protected abstract float Readiness01(TSource source);

        /// <summary>준비된 장비 하나를 작동시킨다.</summary>
        protected abstract void Fire(TSource source);

        /// <summary>이 장비가 실제로 이 스킬을 갖는가(같은 클래스라도 없는 경우: 함교의 연막).</summary>
        protected virtual bool Provides(TSource source) => true;

        /// <summary>true면 한 번 누를 때 준비된 장비를 모두, false면 하나만 쓴다(효과가 겹치지 않는 스킬).</summary>
        protected virtual bool FireAllSources => true;

        /// <summary>한 번 누를 때 모든 장비 작동 전에 한 번 준비할 일(위협 방향 계산 등).</summary>
        protected virtual void BeforeFire() { }

        /// <summary>코드로 붙일 때 격자를 연결한다(ShipInitializer의 누락 보정).</summary>
        public void Bind(ShipGrid shipGrid) => grid = shipGrid;

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        private void Update()
        {
            if (WasKeyPressed()) Activate();

            _statusTimer -= Time.unscaledDeltaTime;
            if (_statusTimer > 0f) return;
            _statusTimer = statusInterval;
            PublishStatus();
        }

        /// <summary>준비된 장비를 작동시킨다. 전투 중이 아니면 무시한다.</summary>
        public void Activate()
        {
            if (grid == null) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            BeforeFire();
            int fired = 0;

            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational) continue;
                if (m.Runtime is not TSource source || !Provides(source) || !IsReady(source)) continue;

                Fire(source);
                fired++;
                if (!FireAllSources) break;
            }

            if (fired > 0) PublishStatus();
        }

        private void PublishStatus()
        {
            if (grid == null) return;

            int ready = 0, total = 0;
            float soonest = 0f;

            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational || m.Runtime is not TSource source || !Provides(source)) continue;

                total++;
                if (IsReady(source)) ready++;
                else soonest = Mathf.Max(soonest, Readiness01(source));
            }

            RaiseStatus(ready, total, ready > 0 ? 1f : soonest);
        }
    }

    /// <summary>기만체·재밍 장비의 공통 상태.</summary>
    public interface ISkillSource
    {
        bool IsReady { get; }

        /// <summary>0 = 막 사용함, 1 = 준비 완료.</summary>
        float Readiness01 { get; }
    }

    /// <summary>
    /// 응급 수리·연막·전속처럼 HUD와 공용 이벤트(GameEvents.SkillRequested / SkillStatusChanged)로 통신하는 스킬.
    /// </summary>
    public abstract class SlotSkillBase<TSource> : ActiveSkillBase<TSource> where TSource : class
    {
        protected abstract ActiveSkillId Id { get; }

        protected override void Subscribe() => GameEvents.SkillRequested += OnRequested;
        protected override void Unsubscribe() => GameEvents.SkillRequested -= OnRequested;

        private void OnRequested(ActiveSkillId id)
        {
            if (id == Id) Activate();
        }

        protected override void RaiseStatus(int ready, int total, float next01)
            => GameEvents.RaiseSkillStatusChanged(Id, ready, total, next01);
    }
}
