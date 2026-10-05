using UnityEngine;
using Game.Core;
using Game.Ship;

namespace Game.Modules
{
    /// <summary>
    /// ModuleDefinition -> 실제 함선 위 오브젝트 생성의 단일 경로.
    /// 격자 등록, 프리팹 부착, 런타임 바인딩, 이벤트 통지를 순서대로 처리한다.
    /// </summary>
    public class ModuleFactory : MonoBehaviour
    {
        /// <summary>정비 화면 카메라가 함선만 비추도록 쓰는 레이어. 셋업이 같은 값을 만든다.</summary>
        public const int LayerShip = 11;

        [SerializeField] private ShipGrid grid;
        [SerializeField] private Transform moduleRoot;

        [Header("Raised Mount")]
        [Tooltip("올린 무기 아래에 세우는 받침. 원점은 바닥, 높이는 raiseHeight에 맞춘다")]
        [SerializeField] private GameObject pedestalPrefab;
        [SerializeField] private float raiseHeight = 1.2f;

        /// <summary>
        /// 모듈을 설치한다. 실패하면 null을 반환하고 아무것도 바꾸지 않는다.
        /// carriedUpgrade: 정비 화면에서 집어 옮기는 블록의 강화 상태(번호·단계를 그대로 잇는다). 새 블록이면 null.
        /// </summary>
        public ModuleInstance Install(ModuleDefinition def, GridCoord origin, int rotationSteps,
                                      ModuleUpgradeState carriedUpgrade = null)
        {
            if (def == null) { Debug.LogError("[ModuleFactory] def가 null입니다."); return null; }
            if (grid == null) { Debug.LogError("[ModuleFactory] grid 미할당.", this); return null; }

            var instance = new ModuleInstance(def, carriedUpgrade);
            if (!grid.Place(instance, origin, rotationSteps))
            {
                Debug.LogWarning($"[ModuleFactory] {def.DisplayName} 배치 실패 at {origin}");
                return null;
            }

            var parent = moduleRoot != null ? moduleRoot : grid.transform;

            if (def.Prefab != null)
            {
                var go = Instantiate(def.Prefab, parent);
                go.transform.localPosition = grid.CoordToLocal(origin)
                                             + GetFootprintOffset(def, rotationSteps, grid.CellSize);
                go.transform.localRotation = Quaternion.Euler(0f, rotationSteps * 90f, 0f);
                go.name = $"MOD_{def.Id}_{origin}";

                SetLayerRecursive(go, LayerShip);

                var runtime = go.GetComponent<ModuleRuntime>();
                if (runtime == null)
                    Debug.LogError($"[ModuleFactory] {def.DisplayName} 프리팹에 ModuleRuntime이 없습니다.", go);

                instance.BindRuntime(runtime);
            }

            GameEvents.RaiseModuleInstalled(instance);
            return instance;
        }

        /// <summary>
        /// 승강 거치대에 올리거나 내린다. 모델을 들어 올리고 아래에 받침을 세운다.
        /// 올리는 조건 검사는 호출하는 쪽(RefitUI)이 FireArcCalculator.CanRaise로 한다.
        /// </summary>
        public void SetRaised(ModuleInstance instance, bool raised)
        {
            if (instance == null || instance.IsRaised == raised) return;

            var runtime = instance.Runtime;
            if (runtime != null)
            {
                var t = runtime.transform;
                t.localPosition += Vector3.up * (raised ? raiseHeight : -raiseHeight);

                var old = t.Find("RaisedPedestal");
                if (old != null) Destroy(old.gameObject);

                if (raised && pedestalPrefab != null)
                {
                    var pedestal = Instantiate(pedestalPrefab, t);
                    pedestal.name = "RaisedPedestal";
                    pedestal.transform.localPosition = Vector3.down * raiseHeight;
                    pedestal.transform.localRotation = Quaternion.identity;
                    SetLayerRecursive(pedestal, LayerShip);
                }
            }

            instance.SetRaised(raised);
        }

        /// <summary>철거. 격자에서 지우고 오브젝트를 파괴한다.</summary>
        public void Uninstall(ModuleInstance instance)
        {
            if (instance == null || grid == null) return;

            grid.Remove(instance);
            if (instance.Runtime != null) Destroy(instance.Runtime.gameObject);

            GameEvents.RaiseModuleRemoved(instance);
        }

        /// <summary>자기 자신과 모든 자식의 레이어를 바꾼다.</summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        /// <summary>
        /// 다중 칸 모듈의 시각적 중심을 맞추기 위한 오프셋.
        /// 격자 X(선수 방향)는 로컬 Z, 격자 Z(좌우)는 로컬 X에 대응한다.
        /// </summary>
        public static Vector3 GetFootprintOffset(ModuleDefinition def, int rotationSteps, float cellSize)
        {
            int w = def.Width, h = def.Height;
            if ((rotationSteps & 1) == 1) { int t = w; w = h; h = t; }

            return new Vector3((h - 1) * 0.5f * cellSize, 0f, (w - 1) * 0.5f * cellSize);
        }
    }
}
