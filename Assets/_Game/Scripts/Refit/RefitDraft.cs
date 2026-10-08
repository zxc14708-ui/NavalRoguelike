using System.Collections.Generic;
using UnityEngine;
using Game.Data;
using Game.Modules;
using Game.Ship;
using Game.Core;
using Game.TaskForce;

namespace Game.Refit
{
    /// <summary>
    /// 정비 보상 카드를 뽑는다. 카드 종류는 셋이다.
    ///   설치(Install)        — 새 블록. 놓을 자리가 있는 블록만
    ///   장비 강화(WeaponUpgrade) — 통합 카드 한 종류(2026-10-03, 예전 블록·단계별 "기관포 강화 I" 카드를 하나로).
    ///                         고르면 설치된 블록 중 강화 단계가 남은 것 하나를 골라 한 단계 올린다. 한 번 뽑을 때 한 장까지
    ///   성장(Growth)         — 공격력·연사력·스킬 회복 같은 함선 전체 능력. 희귀도(일반/희귀/에픽)가 있다
    ///
    /// 3장 규칙: 1번 = 설치, 2번 = 성장(무장 강화 또는 성장 카드), 3번 = 자유.
    /// 설치 카드가 한 장도 안 나오거나 성장이 막히는 일이 없게 자리를 나눠 보장한다.
    /// 진행(레벨)에 따라: 무장 강화 카드가 나올 확률이 올라가고(30% → 최대 65%), 성장 카드의 희귀·에픽 확률이 올라간다.
    /// 보스 격침 보상에서는 성장 슬롯이 에픽 성장 카드로 고정된다.
    ///
    /// 편대 카드(2026-10-02): 편대 배치(고속정 합류, 최대 4척)와 편대 강화(역할 지정 또는 개량)는 3번 자유 자리에만,
    /// 한 번에 한 장까지 나온다. 편대가 비어 있으면 레벨 <see cref="FirstFleetLevel"/>부터 3번 자리에 편대 배치가 보장된다.
    /// </summary>
    public class RefitDraft : MonoBehaviour
    {
        [SerializeField] private ProgressionConfig config;
        [SerializeField] private ShipGrid grid;

        private readonly List<ModuleDefinition> _installPool = new();

        /// <summary>편대가 비어 있을 때 편대 배치 카드가 보장되는 첫 레벨.</summary>
        public const int FirstFleetLevel = 3;
        /// <summary>3번 자유 자리에서 편대 배치·편대 강화 카드의 몫(설치 0.30 · 성장 0.40 · 강화와 함께 가중 추첨).</summary>
        public const float FleetDeployWeight = 0.22f, FleetUpgradeWeight = 0.30f;

        private static TaskForceEscortFormation Fleet
            => GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.GetComponent<TaskForceEscortFormation>() : null;

        /// <summary>무장 강화 카드가 나올 몫(성장 슬롯 안). 레벨 1 = 30%, 레벨당 +4%p, 최대 65%.</summary>
        public static float UpgradeShare(int level) => Mathf.Clamp(0.30f + 0.04f * Mathf.Max(0, level - 1), 0.30f, 0.65f);

        /// <summary>
        /// 카드 count장을 뽑는다. 같은 카드(같은 블록의 설치/강화, 같은 능력)는 한 번만 나온다.
        /// </summary>
        public List<RefitCard> DrawCards(int count, int level, bool bossReward = false)
        {
            var result = new List<RefitCard>(count);
            if (config == null) { Debug.LogError("[RefitDraft] ProgressionConfig 미할당.", this); return result; }

            var usedInstall = new HashSet<ModuleDefinition>();
            var usedUpgrade = new HashSet<ModuleDefinition>();
            var usedStats = new HashSet<RunStat>();
            bool usedFleet = false;
            var fleet = Fleet;
            bool fleetGuarantee = fleet != null && fleet.EscortCount == 0 && fleet.CanDeploy && level >= FirstFleetLevel;

            for (int i = 0; i < count; i++)
            {
                RefitCard card = i switch
                {
                    0 => DrawInstall(usedInstall) ?? DrawGrowth(level, false, usedUpgrade, usedStats),
                    1 => bossReward ? DrawEpicStat(usedStats) ?? DrawGrowth(level, false, usedUpgrade, usedStats)
                                    : DrawGrowth(level, false, usedUpgrade, usedStats),
                    _ => fleetGuarantee && !usedFleet ? RefitCard.FleetDeploy()
                         : DrawFree(level, usedInstall, usedUpgrade, usedStats, fleet, usedFleet),
                };
                if (card != null && card.Kind is RefitCardKind.FleetDeploy or RefitCardKind.FleetUpgrade) usedFleet = true;
                // 한 자리가 비면(풀이 모자람) 다른 종류로 채운다
                card ??= DrawInstall(usedInstall) ?? DrawGrowth(level, false, usedUpgrade, usedStats);
                if (card == null) break;

                Mark(card, usedInstall, usedUpgrade, usedStats);
                result.Add(card);
            }

            EnsureAntiSub(result);
            return result;
        }

        private static void Mark(RefitCard c, HashSet<ModuleDefinition> install, HashSet<ModuleDefinition> upgrade, HashSet<RunStat> stats)
        {
            if (c.Kind == RefitCardKind.Install) install.Add(c.Module);
            else if (c.Kind == RefitCardKind.WeaponUpgrade) upgrade.Add(UpgradeKey);
            else if (c.Kind == RefitCardKind.Growth) stats.Add(c.Stat);
        }

        // ------------------------------------------------------------ 종류별 추첨

        private RefitCard DrawFree(int level, HashSet<ModuleDefinition> usedInstall, HashSet<ModuleDefinition> usedUpgrade, HashSet<RunStat> usedStats,
                                   TaskForceEscortFormation fleet = null, bool usedFleet = true)
        {
            bool canInstall = HasInstallCandidate(usedInstall);
            bool canUpgrade = HasUpgradeCandidate(usedUpgrade);
            bool canStat = HasStatCandidate(usedStats);
            int fleetTarget = !usedFleet && fleet != null ? fleet.PickUpgradeTarget() : -1;
            bool canDeploy = !usedFleet && fleet != null && fleet.CanDeploy;

            float wInstall = canInstall ? 0.30f : 0f;
            float wUpgrade = canUpgrade ? 0.70f * UpgradeShare(level) : 0f;
            float wStat = canStat ? 0.40f : 0f;
            float wDeploy = canDeploy ? FleetDeployWeight : 0f;
            float wFleetUp = fleetTarget >= 0 ? FleetUpgradeWeight : 0f;
            float total = wInstall + wUpgrade + wStat + wDeploy + wFleetUp;
            if (total <= 0f) return null;

            float roll = Random.value * total;
            if ((roll -= wInstall) <= 0f) return DrawInstall(usedInstall);
            if ((roll -= wUpgrade) <= 0f) return DrawUpgrade(level, usedUpgrade);
            if ((roll -= wDeploy) <= 0f) return RefitCard.FleetDeploy();
            if ((roll -= wFleetUp) <= 0f) return RefitCard.FleetUpgrade(fleetTarget);
            return DrawStat(level, usedStats, false);
        }

        /// <summary>성장 슬롯: 무장 강화 카드(진행할수록 더 자주) 또는 성장 카드.</summary>
        private RefitCard DrawGrowth(int level, bool boss, HashSet<ModuleDefinition> usedUpgrade, HashSet<RunStat> usedStats)
        {
            bool canUpgrade = HasUpgradeCandidate(usedUpgrade);
            bool canStat = HasStatCandidate(usedStats);
            if (canUpgrade && (!canStat || Random.value < UpgradeShare(level))) return DrawUpgrade(level, usedUpgrade);
            if (canStat) return DrawStat(level, usedStats, boss);
            return null;
        }

        private RefitCard DrawEpicStat(HashSet<RunStat> usedStats)
        {
            var card = DrawStat(99, usedStats, true, CardTier.Epic);
            return card;
        }

        // ------------------------------------------------------------ 설치

        private bool HasInstallCandidate(HashSet<ModuleDefinition> used)
        {
            BuildInstallPool(used);
            return _installPool.Count > 0;
        }

        private RefitCard DrawInstall(HashSet<ModuleDefinition> used)
        {
            BuildInstallPool(used);
            var def = PickWeighted(_installPool, d => Mathf.Max(0.01f, d.Weight));
            return def != null ? RefitCard.Install(def) : null;
        }

        /// <summary>지금 바로 놓을 자리가 있는 블록. 하나도 없으면 개수 제한에 걸리지 않은 블록(기존 모듈을 철거하고 자리를 만들라는 뜻).</summary>
        private void BuildInstallPool(HashSet<ModuleDefinition> used)
        {
            _installPool.Clear();
            var definitions = CollectInstallDefinitions(config);
            foreach (var def in definitions)
            {
                if (def == null || used.Contains(def)) continue;
                if (grid != null && !grid.HasAnyValidPlacement(def)) continue;
                _installPool.Add(def);
            }
            if (_installPool.Count > 0) return;

            foreach (var def in definitions)
            {
                if (def == null || used.Contains(def)) continue;
                if (def.MaxCount > 0 && grid != null && grid.CountOf(def) >= def.MaxCount) continue;
                _installPool.Add(def);
            }
        }

        /// <summary>
        /// 설치 카드와 장비 사전이 함께 읽는 블록 목록.
        /// 새 지원 블록은 Resources/Modules에서 더하므로 기존 ProgressionConfig를 다시 만들지 않아도 된다.
        /// 가중치 0인 함교·전시 전용 블록은 설치 카드에 나오지 않는다.
        /// </summary>
        public static List<ModuleDefinition> CollectInstallDefinitions(ProgressionConfig progression)
        {
            var definitions = new List<ModuleDefinition>();
            void Add(ModuleDefinition def)
            {
                if (def != null && def.Weight > 0f && !definitions.Contains(def)) definitions.Add(def);
            }

            if (progression != null)
                foreach (var def in progression.Pool) Add(def);
            foreach (var def in Resources.LoadAll<ModuleDefinition>("Modules"))
                if (def != null && IsConceptSupport(def.Type)) Add(def);
            return definitions;
        }

        public static bool IsConceptSupport(ModuleType type)
            => type is ModuleType.FleetRelay or ModuleType.TurboIntake
                or ModuleType.FireControlArray or ModuleType.MissileLogistics
                or ModuleType.TorpedoTube or ModuleType.Howitzer or ModuleType.RamBow or ModuleType.MineRail;   // 경어뢰 발사관·곡사포(2026-10-08)도 Resources/Modules에서 더한다

        // ------------------------------------------------------------ 장비 강화(통합 카드)

        /// <summary>뽑은 카드 기록용 열쇠: 통합 카드는 정의가 없으므로(null) 이 값 하나로 "이미 나왔음"을 센다.</summary>
        private static readonly ModuleDefinition UpgradeKey = null;

        /// <summary>강화 단계가 남은 블록이 하나라도 있고, 이번 뽑기에서 아직 장비 강화 카드가 나오지 않았다.</summary>
        private bool HasUpgradeCandidate(HashSet<ModuleDefinition> used)
        {
            if (grid == null || used.Contains(UpgradeKey)) return false;
            foreach (var m in grid.Modules)
                if (m != null && m.CanUpgrade) return true;
            return false;
        }

        private RefitCard DrawUpgrade(int level, HashSet<ModuleDefinition> used)
            => HasUpgradeCandidate(used) ? RefitCard.EquipmentUpgrade() : null;

        // ------------------------------------------------------------ 성장

        private bool HasStatCandidate(HashSet<RunStat> used)
        {
            foreach (var def in RunUpgrades.Catalog)
                if (!used.Contains(def.Stat) && StatEligible(def)) return true;
            return false;
        }

        private RefitCard DrawStat(int level, HashSet<RunStat> used, bool boss, CardTier? forceTier = null)
        {
            var pool = new List<RunStatDefinition>();
            foreach (var def in RunUpgrades.Catalog)
                if (!used.Contains(def.Stat) && StatEligible(def)) pool.Add(def);
            var pick = PickWeighted(pool, d => d.Weight);
            if (pick == null) return null;
            return RefitCard.Growth(pick.Stat, forceTier ?? RunUpgrades.RollTier(level), boss);
        }

        /// <summary>계열·수리 카드는 그 장비가 있을 때만, 장탄수는 탄약을 쓰는 무장이 있을 때만 나온다.</summary>
        private bool StatEligible(RunStatDefinition def)
        {
            if (grid == null) return true;
            if (def.RequiresAny != null)
            {
                foreach (var m in grid.Modules)
                    if (m != null && m.IsOperational && System.Array.IndexOf(def.RequiresAny, m.Definition.Type) >= 0) return true;
                return false;
            }
            if (def.Stat is RunStat.Damage or RunStat.FireRate or RunStat.Range or RunStat.Magazine)
            {
                foreach (var m in grid.Modules)
                {
                    if (m == null || !m.IsOperational || !RunUpgrades.IsWeapon(m.Definition.Type)) continue;
                    if (def.Stat != RunStat.Magazine || m.EffectiveStats.MagazineCapacity > 0) return true;
                }
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------ 대잠 보장

        /// <summary>
        /// 첫 잠수함 구간(약 300초) 전에 대잠 수단을 최소 한 번은 선택지로 제시한다(설치 카드 자리에).
        /// 선택은 플레이어 몫이며, 이미 폭뢰·헬기 또는 소나+VLS가 있으면 끼어들지 않는다.
        /// </summary>
        private void EnsureAntiSub(List<RefitCard> cards)
        {
            if (cards.Count == 0 || GameManager.Instance == null || GameManager.Instance.ElapsedTime < 180f || HasAntiSubCapability()) return;
            if (cards[0].Kind != RefitCardKind.Install) return;
            foreach (var def in config.Pool)
            {
                if (def == null || def.Type != ModuleType.AswLauncher) continue;
                bool dup = false;
                foreach (var c in cards) if (c.Kind == RefitCardKind.Install && c.Module == def) dup = true;
                if (dup) return;
                cards[0] = RefitCard.Install(def);
                return;
            }
        }

        private bool HasAntiSubCapability()
        {
            if (grid == null) return false;
            bool sonar = false, vls = false;
            foreach (var m in grid.Modules)
            {
                if (m == null || !m.IsOperational) continue;
                switch (m.Definition.Type)
                {
                    case ModuleType.AswLauncher:
                    case ModuleType.TorpedoTube:
                    case ModuleType.HelicopterDeck: return true;
                    case ModuleType.Sonar: sonar = true; break;
                    case ModuleType.Vls: vls = true; break;
                }
            }
            return sonar && vls;
        }

        // ------------------------------------------------------------ 공통

        private static T PickWeighted<T>(List<T> pool, System.Func<T, float> weight) where T : class
        {
            if (pool.Count == 0) return null;

            float total = 0f;
            foreach (var d in pool) total += weight(d);

            float roll = Random.value * total;
            foreach (var d in pool)
            {
                roll -= weight(d);
                if (roll <= 0f) return d;
            }
            return pool[^1];
        }
    }
}
