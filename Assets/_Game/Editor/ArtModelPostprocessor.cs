using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 아트 모델 임포트 보정. 면이 안쪽을 향한(노멀이 뒤집힌) 닫힌 메시를 찾아 바깥으로 돌려놓는다.
    ///
    /// Blender는 면의 앞뒤를 모두 그려서 뒤집혀도 멀쩡해 보이지만, Unity는 뒷면을 그리지 않아
    /// 그 부분이 투명하게 뚫려 보인다(예: 함교탑·연돌). 모델을 다시 받지 않아도 임포트 때 고쳐진다.
    ///
    /// 판정: 닫힌 메시의 부호 있는 부피가 음수면 면이 안쪽을 향한 것이다.
    /// 판자·유리처럼 부피가 거의 없는 열린 메시는 판정이 불안정하므로 건드리지 않는다.
    /// </summary>
    public class ArtModelPostprocessor : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/_Game/Art/Models/";

        /// <summary>경계 상자 부피 대비 이 비율보다 뚜렷하게 음수일 때만 뒤집는다.</summary>
        private const float InsideOutRatio = 0.1f;

        // 판정 규칙을 바꾸면 올린다. 올리면 모든 모델이 다시 임포트된다.
        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtFolder)) return;

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                FixInsideOut(filter.sharedMesh, filter.name);

            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                FixInsideOut(skinned.sharedMesh, skinned.name);
        }

        private void FixInsideOut(Mesh mesh, string objectName)
        {
            if (mesh == null) return;

            var vertices = mesh.vertices;
            double volume = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 a = vertices[tris[i]], b = vertices[tris[i + 1]], c = vertices[tris[i + 2]];
                    volume += Vector3.Dot(a, Vector3.Cross(b, c));
                }
            }
            volume /= 6.0;

            Vector3 size = mesh.bounds.size;
            double boxVolume = (double)size.x * size.y * size.z;
            if (boxVolume <= 1e-9 || volume > -InsideOutRatio * boxVolume) return;

            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                    (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
                mesh.SetTriangles(tris, s, false);
            }

            // 가져온 노멀도 안쪽을 향하고 있으면 같이 뒤집는다(조명이 반대로 먹지 않게)
            var normals = mesh.normals;
            if (normals != null && normals.Length == vertices.Length && NormalsPointInward(mesh, vertices, normals))
            {
                for (int i = 0; i < normals.Length; i++) normals[i] = -normals[i];
                mesh.normals = normals;
                if (mesh.tangents != null && mesh.tangents.Length == vertices.Length) mesh.RecalculateTangents();
            }

            Debug.Log($"[ArtImport] {System.IO.Path.GetFileName(assetPath)} / {objectName}: 안쪽을 향한 면을 바깥으로 뒤집었습니다.");
        }

        /// <summary>뒤집은 뒤의 면 방향과 정점 노멀이 대체로 반대인지.</summary>
        private static bool NormalsPointInward(Mesh mesh, Vector3[] vertices, Vector3[] normals)
        {
            double agreement = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    int ia = tris[i], ib = tris[i + 1], ic = tris[i + 2];
                    Vector3 face = Vector3.Cross(vertices[ib] - vertices[ia], vertices[ic] - vertices[ia]);
                    agreement += Vector3.Dot(face, normals[ia] + normals[ib] + normals[ic]);
                }
            }
            return agreement < 0;
        }
    }
}
