using System.Collections.Generic;
using UnityEngine;
using Game.Core;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// 실제로 함선에 설치된 모듈 하나의 런타임 상태.
    /// Definition(데이터)과 Runtime(동작)을 이어주는 중간 객체이며 MonoBehaviour가 아니다.
    /// </summary>
    public class ModuleInstance
    {
        public ModuleDefinition Definition { get; }
        public ModuleRuntime Runtime { get; private set; }

        public GridCoord Origin { get; private set; }
        public int RotationSteps { get; private set; }
        public IReadOnlyList<GridCoord> OccupiedCoords => _occupied;

        public float Hp { get; private set; }
        public bool IsDestroyed { get; private set; }

        /// <summary>강화 상태(이번 출격 동안). 재배치해도 새 인스턴스가 이어받는다.</summary>
        public ModuleUpgradeState Upgrade { get; }
        /// <summary>설치된 블록을 구분하는 번호(좌표와 무관, 재배치해도 유지).</summary>
        public int InstanceId => Upgrade.InstanceId;
        /// <summary>0 = 기본, 1 = 강화 I, 2 = 강화 II.</summary>
        public int UpgradeLevel => Upgrade.Level;
        /// <summary>외형 단계(1 = 기본형, 2 = U1, 3 = U2). ModuleUpgradeVisuals가 쓴다.</summary>
        public int Level => UpgradeLevel + 1;
        public int MaxUpgradeLevel => ModuleUpgrades.MaxLevel(Definition);
        public bool CanUpgrade => !IsDestroyed && UpgradeLevel < MaxUpgradeLevel;

        /// <summary>기본값 + 강화 단계를 반영한 최종 스탯. 런타임은 이것만 읽는다(ModuleRuntime.Stats).</summary>
        public ModuleStats EffectiveStats { get; private set; }

        /// <summary>승강 거치대 위에 올려졌는지. 올리면 이웃의 중간 블록 너머로 쏠 수 있다.</summary>
        public bool IsRaised { get; private set; }

        /// <summary>올린 무기는 노출이 커져 내구가 이만큼으로 줄어든다.</summary>
        public const float RaisedHpMultiplier = 0.75f;

        /// <summary>사격각 판정에 쓰는 높이. 올리면 높음으로 취급한다(중간 블록에 가리지 않고, 이웃에게는 높은 장애물).</summary>
        public ModuleHeight EffectiveHeight => IsRaised ? ModuleHeight.High : Definition.HeightClass;

        public float MaxHp => Definition != null ? Definition.MaxHp * (IsRaised ? RaisedHpMultiplier : 1f) : 1f;

        /// <summary>자리로 정해지는 형태(소나·폭뢰). 격자가 바뀔 때마다 ShipGrid.RefreshVariants가 다시 정한다.</summary>
        public ModuleVariant Variant { get; private set; }
        /// <summary>측면 발사대가 던질 수 있는 현측(트인 쪽).</summary>
        public ModuleSides Sides { get; private set; }

        /// <summary>ShipGrid.RefreshVariants에서만 호출한다. 바뀌면 런타임이 외형·동작을 바꾼다.</summary>
        public void SetVariant(ModuleVariant variant, ModuleSides sides)
        {
            if (Variant == variant && Sides == sides) return;
            Variant = variant;
            Sides = sides;
            Runtime?.HandleVariantChanged();
        }

        /// <summary>파괴되지 않았고 전력이 공급되는 상태.</summary>
        public bool IsOperational => !IsDestroyed;

        private readonly List<GridCoord> _occupied = new();

        /// <param name="carriedUpgrade">재배치로 다시 설치할 때 이전 인스턴스의 강화 상태. 없으면 새 블록(강화 0).</param>
        public ModuleInstance(ModuleDefinition definition, ModuleUpgradeState carriedUpgrade = null)
        {
            Definition = definition;
            Hp = definition != null ? definition.MaxHp : 1f;
            Upgrade = carriedUpgrade ?? new ModuleUpgradeState();
            RecalculateStats();
        }

        private void RecalculateStats() => EffectiveStats = ModuleUpgrades.Compute(Definition, UpgradeLevel);

        /// <summary>성장 카드가 바뀌었을 때 최종 스탯을 다시 계산한다(강화 단계·탄약·쿨타임·HP는 그대로).</summary>
        public void RefreshStats() => RecalculateStats();

        /// <summary>ShipGrid.Place에서만 호출한다.</summary>
        public void SetPlacement(GridCoord origin, int rotationSteps, List<GridCoord> coords)
        {
            Origin = origin;
            RotationSteps = rotationSteps;
            _occupied.Clear();
            _occupied.AddRange(coords);
        }

        /// <summary>프리팹 생성 후 런타임 컴포넌트를 연결한다.</summary>
        public void BindRuntime(ModuleRuntime runtime)
        {
            Runtime = runtime;
            if (runtime == null) return;
            runtime.Initialize(this);
            runtime.GetComponent<ModuleUpgradeVisuals>()?.ApplyLevel(Level, runtime);
            if (Variant != ModuleVariant.None) runtime.HandleVariantChanged();
        }

        public void TakeDamage(float amount)
        {
            if (IsDestroyed || amount <= 0f) return;

            Hp = Mathf.Max(0f, Hp - amount);
            if (Hp > 0f) return;

            IsDestroyed = true;
            Runtime?.OnModuleDestroyed();
            GameEvents.RaiseModuleDestroyed(this);
        }

        /// <summary>수리반이 모듈을 회복시킨다. 이미 파괴된 모듈은 복구되지 않는다.</summary>
        public void Repair(float amount)
        {
            if (IsDestroyed || amount <= 0f || Definition == null) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        /// <summary>ModuleFactory.SetRaised에서만 호출한다. 내려도 줄었던 체력은 돌아오지 않는다(수리로 채운다).</summary>
        public void SetRaised(bool raised)
        {
            if (IsRaised == raised) return;
            IsRaised = raised;
            Hp = Mathf.Min(Hp, MaxHp);
            GameEvents.RaiseModuleRaisedChanged(this);
        }

        /// <summary>
        /// 같은 블록 카드로 한 단계 강화한다(새 블록은 설치하지 않음). 최대 단계·파괴된 블록이면 false.
        /// 스탯을 다시 계산하고, 외형을 바꾸고(탄약·쿨타임·HP는 유지), 이웃 무기가 탄약고 지원을 다시 잡도록 알린다.
        /// </summary>
        public bool ApplyUpgrade()
        {
            if (!CanUpgrade) return false;
            Upgrade.Level++;
            RecalculateStats();
            Runtime?.GetComponent<ModuleUpgradeVisuals>()?.ApplyLevel(Level, Runtime);
            Runtime?.HandleUpgraded();
            GameEvents.RaiseModuleUpgraded(this);
            return true;
        }
    }
}
