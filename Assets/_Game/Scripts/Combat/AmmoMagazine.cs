using UnityEngine;
using Game.Modules;

namespace Game.Combat
{
    /// <summary>
    /// 무기 한 문의 탄약. 발사 간격(WeaponController)과 별개로 "몇 발 남았나"를 관리한다.
    /// 무기 모듈이 WeaponController처럼 구성으로 들고 쓴다.
    ///
    /// 보급 방식 두 가지(데이터 ModuleStats로 정한다)
    ///   - 조금씩 보급(AmmoReloadAmount > 0): 가득 차 있지 않으면 AmmoReloadTime마다 AmmoReloadAmount씩 채운다. 남은 탄이 있으면 계속 쏜다.
    ///     기관포·76mm·VLS 셀·함대공·유도로켓·폭뢰.
    ///   - 전량 재장전(AmmoReloadAmount == 0): 비면 AmmoReloadTime 동안 재장전하고, 그동안 쏘지 못한다. CIWS.
    ///     교전이 없을 때 미리 재장전(BeginReload)할 수 있다.
    /// 용량 0이면 무한 탄약(데이터를 넣기 전의 예전 동작).
    /// 정비 페이즈에 들어가면 모듈이 Refill로 전량 보급한다.
    /// </summary>
    public sealed class AmmoMagazine
    {
        public enum State { Ready, Low, Reloading, Empty }

        private int _baseCapacity, _perShot, _baseAmount;
        private float _baseInterval;
        private float _capacityMul = 1f, _resupplyMul = 1f;

        private int _capacity;
        private float _timer;        // 다음 보급(또는 재장전 완료)까지
        private bool _fullReloading;

        public bool Infinite => _capacity <= 0;
        public int Capacity => _capacity;
        public int Current { get; private set; }
        public int PerShot => _perShot;
        public bool IsFullReloadMode => _baseAmount <= 0;
        public bool IsReloading => _fullReloading;
        public float LowFraction { get; set; } = 0.25f;

        /// <summary>전투 기록에 쓸 무기 이름.</summary>
        public string Label { get; set; } = "무기";

        /// <summary>한 번 쏠 수 있는가.</summary>
        public bool CanFire => Infinite || (!_fullReloading && Current >= _perShot);

        /// <summary>보급 간격(보너스 반영).</summary>
        public float Interval => Mathf.Max(0.1f, _baseInterval / Mathf.Max(0.1f, _resupplyMul));

        /// <summary>다음 보급 또는 재장전 완료까지 남은 시간. 채울 것이 없으면 0.</summary>
        public float SecondsToNext => Infinite || (!_fullReloading && Current >= _capacity) ? 0f : Mathf.Max(0f, _timer);

        public State Status
        {
            get
            {
                if (Infinite) return State.Ready;
                if (_fullReloading) return State.Reloading;
                if (Current < _perShot) return State.Empty;
                return Current <= Mathf.CeilToInt(_capacity * LowFraction) ? State.Low : State.Ready;
            }
        }

        /// <summary>데이터로 초기화하고 가득 채운다.</summary>
        public void Configure(in ModuleStats stats)
        {
            _baseCapacity = Mathf.Max(0, stats.MagazineCapacity);
            _perShot = Mathf.Max(1, stats.AmmoPerShot);
            _baseAmount = Mathf.Max(0, stats.AmmoReloadAmount);
            _baseInterval = Mathf.Max(0.1f, stats.AmmoReloadTime);
            ApplyBonus(_capacityMul, _resupplyMul);
            Refill();
        }

        /// <summary>
        /// 블록 강화로 기본 수치가 바뀌었을 때. 가득 채우지 않고 남은 탄 비율을 유지한다(Configure와 달리 Refill하지 않음).
        /// 재장전 중이면 남은 진행률을 유지하며 새 간격에 맞춰 이어진다.
        /// </summary>
        public void Reconfigure(in ModuleStats stats)
        {
            float oldInterval = Interval;
            _baseCapacity = Mathf.Max(0, stats.MagazineCapacity);
            _perShot = Mathf.Max(1, stats.AmmoPerShot);
            _baseAmount = Mathf.Max(0, stats.AmmoReloadAmount);
            _baseInterval = Mathf.Max(0.1f, stats.AmmoReloadTime);
            ApplyBonus(_capacityMul, _resupplyMul);
            // 간격이 달라져도 현재 보급 진행률을 유지한다. 착탈로 즉시 탄을 생성하지 않는다.
            if (_timer > 0f) _timer *= Interval / oldInterval;
        }

        /// <summary>
        /// 탄약고 보너스. 용량 배율은 남은 탄 비율을 유지하며 늘린다(재배치로 탄이 생기거나 사라지지 않게).
        /// </summary>
        public void ApplyBonus(float capacityMul, float resupplyMul)
        {
            _capacityMul = Mathf.Max(0.1f, capacityMul);
            _resupplyMul = Mathf.Max(0.1f, resupplyMul);
            if (_baseCapacity <= 0) { _capacity = 0; return; }

            int newCapacity = Mathf.Max(1, Mathf.RoundToInt(_baseCapacity * _capacityMul));
            if (_capacity > 0 && newCapacity != _capacity)
                Current = Mathf.Clamp(Mathf.RoundToInt((float)Current / _capacity * newCapacity), 0, newCapacity);
            _capacity = newCapacity;
        }

        /// <summary>한 발 분량을 쓴다. 쏠 수 없으면 false.</summary>
        public bool Consume()
        {
            if (Infinite) return true;
            if (!CanFire) return false;
            Current -= _perShot;
            if (Current < _perShot)
            {
                if (IsFullReloadMode) BeginReload();
                else CombatLog.Add("탄약", $"{Label} 탄약 소진 · 다음 보급 {Interval:0.#}초");
            }
            return true;
        }

        /// <summary>전량 재장전 시작(전량 재장전 방식 전용). 이미 가득하거나 재장전 중이면 무시.</summary>
        public bool BeginReload()
        {
            if (Infinite || !IsFullReloadMode || _fullReloading || Current >= _capacity) return false;
            _fullReloading = true;
            _timer = Interval;
            CombatLog.Add("탄약", $"{Label} 재장전 시작 ({Current}/{_capacity}, {Interval:0.#}초간 사격 불가)");
            return true;
        }

        public void Refill()
        {
            _fullReloading = false;
            Current = _capacity;
            _timer = Interval;
        }

        public void Tick(float dt)
        {
            if (Infinite) return;

            if (IsFullReloadMode)
            {
                if (!_fullReloading) return;
                _timer -= dt;
                if (_timer > 0f) return;
                Refill();
                return;
            }

            if (Current >= _capacity) { _timer = Interval; return; }
            _timer -= dt;
            if (_timer > 0f) return;
            Current = Mathf.Min(_capacity, Current + _baseAmount);
            _timer = Interval;
        }
    }

    /// <summary>탄약을 가진 무기. 무장 패널이 읽는다.</summary>
    public interface IAmmoUser
    {
        AmmoMagazine Ammo { get; }

        /// <summary>지금 표적과 교전 중인가(CIWS 상태 표시용).</summary>
        bool IsEngaged { get; }
    }
}
