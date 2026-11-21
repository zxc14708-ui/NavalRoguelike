using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// 군항 시작 화면의 큰 합친 메시를 구역별로 나눈다(2026-11-21 Codex 점검 — 나무 메시 하나가 46.5 MiB · 정점 33만,
    /// 그 버텍스 데이터 크기가 반복된 D3D12 업로드 경고의 요청 크기와 같았다).
    ///   - 정점 Threshold(6만)를 넘는 메시를 수평(XZ) 격자로 나눈다: 조각 하나가 TargetVerts(4만) 안팎이 되게 칸 수를 정하고,
    ///     삼각형은 무게중심이 든 칸으로 보낸다. 그래도 6만을 넘는 칸은 다시 2×2로 나눈다. 정점 속성(위치 · 법선 · 접선 · 색 · UV 0~7)은 그대로 옮긴다.
    ///   - 조각은 원본 옆에 MESH_…_sNN.asset으로 저장하고 CPU 읽기 사본을 끈다(m_IsReadable 0 — 메모리 절반). 나무는 실행 중 정점을 읽는 곳이 없다.
    ///   - 원본 오브젝트 자리에 같은 이름 + _sNN 오브젝트를 만들고(같은 부모 · 변환 · 층 · 정적 플래그 · 렌더러 설정 복사) 원본은 지운다.
    /// 메뉴 Naval/Art/Split Harbor Menu Large Meshes: Resources/Environment/NavalBaseMenu.prefab에 적용하고, 더 쓰는 곳이 없는 원본 메시 파일은 지운다.
    /// 기지를 새로 지을 때(NavalBaseBuilder.CombineStatic)도 끝에서 같은 분할을 한다.
    /// </summary>
    public static class NavalBaseMeshSplitter
    {
        public const int Threshold = 60000, TargetVerts = 40000;
        private const string PrefabPath = "Assets/_Game/Resources/Environment/NavalBaseMenu.prefab";

        [MenuItem("Naval/Art/Split Harbor Menu Large Meshes")]
        public static void SplitHarborMenu()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            var replaced = new List<string>();
            try
            {
                int parts = SplitLarge(root.transform, replaced);
                if (replaced.Count == 0) { Debug.Log("[MeshSplit] 나눌 메시가 없습니다(이미 나뉘었거나 모두 작음)."); return; }
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[MeshSplit] PASS: 메시 {replaced.Count}개 → 조각 {parts}개 · {PrefabPath} 저장");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            // 원본 메시 파일: 더 쓰는 곳이 없으면 지운다
            foreach (var path in replaced)
            {
                bool used = false;
                foreach (var dep in AssetDatabase.GetDependencies(PrefabPath, false)) if (dep == path) used = true;
                if (!used && AssetDatabase.DeleteAsset(path)) Debug.Log($"[MeshSplit] 원본 지움: {path}");
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>배치 모드 진입점(-executeMethod Game.EditorTools.NavalBaseMeshSplitter.RunBatch).</summary>
        public static void RunBatch()
        {
            try { SplitHarborMenu(); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>root 아래 정점 Threshold를 넘는 메시를 모두 나눈다. 바꾼 원본 메시 경로를 replaced에 넣고, 만든 조각 수를 돌려준다.</summary>
        public static int SplitLarge(Transform root, List<string> replaced = null)
        {
            int parts = 0;
            var targets = new List<MeshFilter>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.sharedMesh.vertexCount > Threshold && mf.sharedMesh.subMeshCount == 1 && mf.GetComponent<MeshRenderer>() != null)
                    targets.Add(mf);
            foreach (var mf in targets)
            {
                var mesh = mf.sharedMesh;
                string src = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(src)) continue;
                string dir = Path.GetDirectoryName(src).Replace('\\', '/');
                string stem = Path.GetFileNameWithoutExtension(src);
                var pieces = Split(mesh);
                if (pieces.Count <= 1) continue;
                var go = mf.gameObject;
                var mr = go.GetComponent<MeshRenderer>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    var piece = pieces[i];
                    piece.name = $"{mesh.name}_s{i:00}";
                    string path = $"{dir}/{stem}_s{i:00}.asset";
                    AssetDatabase.DeleteAsset(path);
                    AssetDatabase.CreateAsset(piece, path);
                    var so = new SerializedObject(piece);
                    var readable = so.FindProperty("m_IsReadable");
                    if (readable != null) { readable.boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); }
                    var child = new GameObject($"{go.name}_s{i:00}", typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(go.transform.parent, false);
                    child.transform.localPosition = go.transform.localPosition;
                    child.transform.localRotation = go.transform.localRotation;
                    child.transform.localScale = go.transform.localScale;
                    child.transform.SetSiblingIndex(go.transform.GetSiblingIndex() + 1 + i);
                    child.layer = go.layer;
                    child.tag = go.tag;
                    child.SetActive(go.activeSelf);
                    GameObjectUtility.SetStaticEditorFlags(child, GameObjectUtility.GetStaticEditorFlags(go));
                    var cmr = child.GetComponent<MeshRenderer>();
                    EditorUtility.CopySerialized(mr, cmr);
                    child.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    parts++;
                }
                replaced?.Add(src);
                Object.DestroyImmediate(go);
            }
            return parts;
        }

        // ------------------------------------------------------------ 나누기

        private sealed class Attr
        {
            public List<Vector3> Pos = new(), Nrm = new();
            public List<Vector4> Tan = new();
            public List<Color> Col = new();
            public List<Vector4>[] Uv = new List<Vector4>[8];
        }

        /// <summary>메시를 수평 격자 조각들로 나눈다(조각마다 정점이 Threshold 아래가 되게).</summary>
        public static List<Mesh> Split(Mesh mesh)
        {
            var src = new Attr();
            mesh.GetVertices(src.Pos);
            bool hasN = mesh.HasVertexAttribute(VertexAttribute.Normal), hasT = mesh.HasVertexAttribute(VertexAttribute.Tangent), hasC = mesh.HasVertexAttribute(VertexAttribute.Color);
            if (hasN) mesh.GetNormals(src.Nrm);
            if (hasT) mesh.GetTangents(src.Tan);
            if (hasC) mesh.GetColors(src.Col);
            var uvDims = new int[8];
            for (int c = 0; c < 8; c++)
            {
                var a = (VertexAttribute)((int)VertexAttribute.TexCoord0 + c);
                if (!mesh.HasVertexAttribute(a)) continue;
                uvDims[c] = mesh.GetVertexAttributeDimension(a);
                src.Uv[c] = new List<Vector4>();
                mesh.GetUVs(c, src.Uv[c]);
            }
            var tris = mesh.GetTriangles(0);
            var all = new List<int>(tris.Length / 3);
            for (int t = 0; t < tris.Length / 3; t++) all.Add(t);

            var groups = new List<List<int>>();
            var b = mesh.bounds;
            int n = Mathf.CeilToInt(mesh.vertexCount / (float)TargetVerts);
            float sx = Mathf.Max(0.01f, b.size.x), sz = Mathf.Max(0.01f, b.size.z);
            int gx = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(n * sx / sz))), gz = Mathf.Max(1, Mathf.CeilToInt(n / (float)gx));
            Grid(all, tris, src.Pos, new Vector2(b.min.x, b.min.z), new Vector2(sx, sz), gx, gz, groups, 0);

            var result = new List<Mesh>();
            foreach (var g in groups) if (g.Count > 0) result.Add(Build(g, tris, src, hasN, hasT, hasC, uvDims));
            return result;
        }

        private static void Grid(List<int> faces, int[] tris, List<Vector3> pos, Vector2 min, Vector2 size, int gx, int gz, List<List<int>> outGroups, int depth)
        {
            var cells = new List<int>[gx * gz];
            for (int i = 0; i < cells.Length; i++) cells[i] = new List<int>();
            foreach (int f in faces)
            {
                Vector3 c = (pos[tris[f * 3]] + pos[tris[f * 3 + 1]] + pos[tris[f * 3 + 2]]) / 3f;
                int ix = Mathf.Clamp(Mathf.FloorToInt((c.x - min.x) / size.x * gx), 0, gx - 1);
                int iz = Mathf.Clamp(Mathf.FloorToInt((c.z - min.y) / size.y * gz), 0, gz - 1);
                cells[iz * gx + ix].Add(f);
            }
            for (int iz = 0; iz < gz; iz++)
                for (int ix = 0; ix < gx; ix++)
                {
                    var cell = cells[iz * gx + ix];
                    if (cell.Count == 0) continue;
                    // 칸 정점이 너무 많으면(빽빽한 숲) 2×2로 더 나눈다(실제 쓰는 정점 수로 — 조각이 너무 잘게 나뉘면 그리기 호출만 는다)
                    if (cell.Count * 3 > Threshold && UniqueVerts(cell, tris) > Threshold && depth < 6)
                    {
                        var cmin = new Vector2(min.x + size.x * ix / gx, min.y + size.y * iz / gz);
                        Grid(cell, tris, pos, cmin, new Vector2(size.x / gx, size.y / gz), 2, 2, outGroups, depth + 1);
                    }
                    else outGroups.Add(cell);
                }
        }

        private static int UniqueVerts(List<int> faces, int[] tris)
        {
            var set = new HashSet<int>();
            foreach (int f in faces) { set.Add(tris[f * 3]); set.Add(tris[f * 3 + 1]); set.Add(tris[f * 3 + 2]); }
            return set.Count;
        }

        private static Mesh Build(List<int> faces, int[] tris, Attr src, bool hasN, bool hasT, bool hasC, int[] uvDims)
        {
            var map = new Dictionary<int, int>();
            var dst = new Attr();
            for (int c = 0; c < 8; c++) if (uvDims[c] > 0) dst.Uv[c] = new List<Vector4>();
            var idx = new List<int>(faces.Count * 3);
            foreach (int f in faces)
                for (int k = 0; k < 3; k++)
                {
                    int v = tris[f * 3 + k];
                    if (!map.TryGetValue(v, out int nv))
                    {
                        nv = dst.Pos.Count;
                        map[v] = nv;
                        dst.Pos.Add(src.Pos[v]);
                        if (hasN) dst.Nrm.Add(src.Nrm[v]);
                        if (hasT) dst.Tan.Add(src.Tan[v]);
                        if (hasC) dst.Col.Add(src.Col[v]);
                        for (int c = 0; c < 8; c++) if (uvDims[c] > 0) dst.Uv[c].Add(src.Uv[c][v]);
                    }
                    idx.Add(nv);
                }
            var m = new Mesh { indexFormat = dst.Pos.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(dst.Pos);
            if (hasN) m.SetNormals(dst.Nrm);
            if (hasT) m.SetTangents(dst.Tan);
            if (hasC) m.SetColors(dst.Col);
            for (int c = 0; c < 8; c++)
            {
                if (uvDims[c] == 0) continue;
                if (uvDims[c] == 2) { var l = new List<Vector2>(dst.Uv[c].Count); foreach (var u in dst.Uv[c]) l.Add(u); m.SetUVs(c, l); }
                else if (uvDims[c] == 3) { var l = new List<Vector3>(dst.Uv[c].Count); foreach (var u in dst.Uv[c]) l.Add(u); m.SetUVs(c, l); }
                else m.SetUVs(c, dst.Uv[c]);
            }
            m.SetTriangles(idx, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
