using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarborOperationsPack
{
    /// <summary>Presentation-only dock choreography. All positions use the original island's local coordinates.</summary>
    public sealed partial class HarborOperations : MonoBehaviour
    {
        public const float CycleSeconds = 170f;
        public const string Resource = "HarborOperations/Operations";
        [SerializeField] private Transform trolley, spreader, transferBox, truck, truckSocket;
        [SerializeField] private Transform truckWheels, serviceTruck, helicopter, rotor, tailRotor;
        [SerializeField] private Transform dockTugLines, towLines, shipWake, tugWake;
        [SerializeField] private Transform craneCableA, craneCableB;
        private Transform _port, _ship, _tug;
        private Pose _shipRest, _tugRest;
        private float _clock;
        private bool _ready;
        private readonly List<(MeshFilter filter, Mesh original, Mesh modified)> _edits = new();

        public bool Ready => _ready;
        public Transform CargoShip => _ship;
        public Transform Tug => _tug;
        public bool TowlineVisible => towLines.gameObject.activeSelf;

        /// <summary>Call immediately after the base menu installs its original facility overlay.</summary>
        public static HarborOperations Attach(Transform island)
        {
            if (island == null) return null;
            var existing = island.Find("HarborOperations");
            if (existing != null) return existing.GetComponent<HarborOperations>();
            var prefab = Resources.Load<GameObject>(Resource);
            if (prefab == null) return null;
            var go = Instantiate(prefab, island, false);
            go.name = "HarborOperations";
            if (!go.TryGetComponent<HarborOperations>(out var instance) || !instance.Bind(island))
            {
                Release(go);
                return null;
            }
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = island.gameObject.layer;
            return instance;
        }

        private bool Bind(Transform island)
        {
            _port = island.Find("HarborFacilityDetails/CargoPort");
            if (_port == null) return false;
            _ship = _port.Find("CargoShip");
            _tug = _port.Find("CargoSupportTug");
            if (_ship == null || _tug == null) return false;
            if (!CheckAndReplaceCargoMeshes())
            {
                Debug.LogWarning("[Harbor Operations] Cargo geometry does not match the source; original cargo display retained.");
                return false;
            }
            _shipRest = new Pose(_ship.localPosition, _ship.localRotation);
            _tugRest = new Pose(_tug.localPosition, _tug.localRotation);
            _ready = true;
            SetTime(0f);
            return true;
        }

        // The source combines geometry per material. Remove just the old ropes, the lower
        // assembly of crane #2, and the single container animated by this extension.
        private bool CheckAndReplaceCargoMeshes()
        {
            const float topBoxZ = 39f;
            bool ropes = FindCut(_port.Find("rope"), _ => true);
            bool spreaderCut = FindCut(_port.Find("safety_yellow"),
                p => p.x > 30.7f && p.x < 39.3f && Between(p.y, 23.2f, 24.2f) && Mathf.Abs(p.z - 125f) < 6f);
            bool cableCut = FindCut(_port.Find("steel_dark"),
                p => Between(p.x, 34.65f, 35.35f) && Between(p.y, 23.75f, 39.75f) &&
                     (Mathf.Abs(p.z - 122.5f) < .45f || Mathf.Abs(p.z - 127.5f) < .45f));
            // Row 6, column 4, level 2 is the top unit at the adjacent loading crane.
            bool container = false;
            foreach (Transform child in _ship)
            {
                if (!child.name.StartsWith("box_", StringComparison.Ordinal)) continue;
                if (FindCut(child, p => Between(p.x, .04f, 3.36f) && Between(p.y, 17.60f, 20.22f) &&
                                        Mathf.Abs(p.z - topBoxZ) < 6.14f)) container = true;
            }
            if (!ropes || !spreaderCut || !cableCut || !container)
            {
                RestoreMeshes();
                return false;
            }
            foreach (var edit in _edits) edit.filter.sharedMesh = edit.modified;
            return true;
        }

        private static bool Between(float n, float low, float high) => n >= low && n <= high;

        private bool FindCut(Transform child, Func<Vector3, bool> cut)
        {
            if (child == null || !child.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null ||
                !filter.sharedMesh.isReadable || filter.sharedMesh.subMeshCount != 1) return false;
            var original = filter.sharedMesh;
            var points = original.vertices;
            var triangles = original.triangles;
            var result = new List<int>(triangles.Length);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                if (cut(points[a]) && cut(points[b]) && cut(points[c])) continue;
                result.Add(a); result.Add(b); result.Add(c);
            }
            if (result.Count == triangles.Length) return false;
            var mesh = Instantiate(original);
            mesh.name = original.name + " (cargo operation)";
            mesh.SetTriangles(result, 0, true);
            _edits.Add((filter, original, mesh));
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_ready || deltaTime <= 0f) return;
            SetTime(_clock + deltaTime);
        }

        public void SetTime(float seconds)
        {
            if (!_ready) return;
            _clock = Mathf.Repeat(seconds, CycleSeconds);
            Sample(_clock);
        }

        private void OnDestroy()
        {
            if (_ready)
            {
                if (_ship != null) { _ship.localPosition = _shipRest.position; _ship.localRotation = _shipRest.rotation; }
                if (_tug != null) { _tug.localPosition = _tugRest.position; _tug.localRotation = _tugRest.rotation; }
            }
            RestoreMeshes();
        }

        private void RestoreMeshes()
        {
            foreach (var edit in _edits)
            {
                if (edit.filter != null && edit.filter.sharedMesh == edit.modified)
                    edit.filter.sharedMesh = edit.original;
                if (edit.modified != null) Release(edit.modified);
            }
            _edits.Clear();
        }

        private static void Release(UnityEngine.Object obj)
        {
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        private static void Put(Transform target, Vector3 at, float yaw)
        {
            target.localPosition = at;
            target.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        private Vector3 AtPort(float x, float y, float z) => _port.localPosition + new Vector3(x,y,z);
        private Vector3 Local(Vector3 world) => transform.InverseTransformPoint(world);

        private static void Stretch(Transform line, Vector3 from, Vector3 to, float width)
        {
            Vector3 segment = to - from;
            if (segment.sqrMagnitude < .0001f)
            {
                line.gameObject.SetActive(false);
                return;
            }
            line.gameObject.SetActive(true);
            line.localPosition = (from + to) * .5f;
            line.localRotation = Quaternion.LookRotation(segment);
            line.localScale = new Vector3(width, width, segment.magnitude);
        }
    }
}

