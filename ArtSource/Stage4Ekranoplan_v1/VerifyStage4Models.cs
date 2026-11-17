// Place only inside Assets/Editor of a disposable Unity project.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class VerifyStage4Models
{
    private static Vector3 Point(string value)
    {
        float[] v = value.Split(',').Select(n => float.Parse(n, CultureInfo.InvariantCulture)).ToArray();
        return new Vector3(v[0], v[2], v[1]);
    }

    private static string Vector(Vector3 v) => $"{v.x:F3},{v.y:F3},{v.z:F3}";

    public static void Run()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        var rows = File.ReadAllLines(Path.Combine(project, "expected.tsv"))
            .Select(s => s.Split('\t')).GroupBy(fields => fields[0]).ToArray();
        var report = new List<string> { $"Unity {Application.unityVersion} / isolated model import" };
        var errors = new List<string>();
        foreach (var model in rows)
        {
            string path = "Assets/Models/" + model.Key + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset)
            {
                errors.Add(model.Key + " could not import");
                continue;
            }

            var obj = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            var wrapper = new GameObject("ModelPivot");
            // The game's existing FBX wrappers face Blender +Y hulls forward.
            wrapper.transform.rotation = Quaternion.Euler(0, 180, 0);
            obj.transform.SetParent(wrapper.transform, false);
            try
            {
                var transforms = obj.GetComponentsInChildren<Transform>(true);
                foreach (string[] fields in model.Where(row => row[1] != "@bounds"))
                {
                    string name = fields[1];
                    Transform part = transforms.FirstOrDefault(t => t.name == name);
                    if (!part)
                    {
                        errors.Add(model.Key + " / missing " + name);
                        continue;
                    }
                    if (name == model.Key) continue;
                    if (part.parent.name != fields[2])
                        errors.Add(model.Key + " / parent of " + name + " is " + part.parent.name);
                    if ((fields[3] == "MESH") != (part.GetComponent<MeshFilter>() != null))
                        errors.Add(model.Key + " / mesh designation of " + name);
                    Vector3 expected = Point(fields[4]);
                    if (Vector3.Distance(part.position, expected) > .015f)
                        errors.Add($"{model.Key} / {name} position {Vector(part.position)} expected {Vector(expected)}");
                    if (fields[3] != "EMPTY" || fields[6] != "direction") continue;
                    Vector3 axis = Point(fields[5]).normalized;
                    if (Vector3.Angle(axis, part.forward) > 1f)
                        errors.Add($"{model.Key} / {name} local +Z {Vector(part.forward)} expected {Vector(axis)}");
                }

                var filters = obj.GetComponentsInChildren<MeshFilter>();
                int tris = 0;
                foreach (var filter in filters)
                {
                    if (!filter.sharedMesh || filter.sharedMesh.vertexCount == 0)
                        errors.Add(model.Key + " / mesh missing on " + filter.name);
                    else
                        tris += filter.sharedMesh.triangles.Length / 3;
                }

                var renderers = obj.GetComponentsInChildren<Renderer>();
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                string[] boundsRow = model.Single(row => row[1] == "@bounds");
                if (Vector3.Distance(bounds.min, Point(boundsRow[2])) > .17f ||
                    Vector3.Distance(bounds.max, Point(boundsRow[3])) > .17f)
                    errors.Add(model.Key + " / bounds " + Vector(bounds.min) + " to " + Vector(bounds.max) +
                               " expected " + Vector(Point(boundsRow[2])) + " to " + Vector(Point(boundsRow[3])));
                if (int.Parse(boundsRow[4], CultureInfo.InvariantCulture) != tris)
                    errors.Add(model.Key + " / unexpected triangle count " + tris);
                if (obj.GetComponentsInChildren<Collider>().Length != 0)
                    errors.Add(model.Key + " contains an imported collider");

                var materials = renderers.SelectMany(r => r.sharedMaterials).Where(m => m)
                    .Select(m => m.name).Distinct().OrderBy(n => n).ToArray();
                if (!new HashSet<string>(materials).SetEquals(boundsRow[5].Split('|')))
                    errors.Add(model.Key + " / material names " + string.Join("|", materials));
                report.Add($"{model.Key}  {tris} tris  {Vector(bounds.size)} m  root scale {Vector(obj.transform.localScale)}");
                if (Mathf.Abs(obj.transform.localScale.x - 100f) > .02f)
                    errors.Add(model.Key + " / unexpected FBX root scale");
                CheckPoses(model.Key, transforms, report, errors);
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
        report.AddRange(errors);
        report.Add(errors.Count == 0 ? "PASS: mesh bounds, hierarchy, part types, socket axes and materials" : $"FAIL: {errors.Count} errors");
        File.WriteAllLines(Path.Combine(project, "UnityValidation.txt"), report);
        if (errors.Count != 0) throw new InvalidOperationException($"Stage 4 model import: {errors.Count} errors");
    }

    private static void CheckPoses(string model, Transform[] transforms, List<string> report, List<string> errors)
    {
        Transform Find(string name) => transforms.FirstOrDefault(t => t.name == name);
        if (model == "ENE_Boss4_Lun")
        {
            var hatches = transforms.Where(t => t.name.StartsWith("LauncherHatch_")).ToArray();
            if (hatches.Length != 6) errors.Add("Lun hatch count");
            foreach (var hatch in hatches)
            {
                var renderer = hatch.GetComponent<Renderer>();
                Vector3 before = renderer.bounds.center;
                hatch.Rotate(Vector3.right, -70, Space.World);
                Vector3 delta = renderer.bounds.center - before;
                if (delta.y < .20f || delta.z < .18f) errors.Add($"Lun hatch did not lift: {hatch.name}");
            }
            report.Add("  Lun: six hatches rotated through 70 degrees about their hinges.");
        }
        if (model == "ENE_AssaultWig")
        {
            var hinge = Find("NoseDoorPivot");
            var door = Find("NoseDoor").GetComponent<Renderer>();
            Vector3 before = door.bounds.center;
            hinge.Rotate(Vector3.up, 70, Space.World);
            if (door.bounds.center.x - before.x < 1f) errors.Add("Assault WIG nose did not swing to starboard");
            report.Add("  Assault WIG: the bow door swung to starboard about a vertical hinge.");
        }
        if (model == "ENE_Hydrofoil")
        {
            var bowFoil = Find("Foil_Bow");
            var sternFoil = Find("Foil_Stern");
            var hull = Find("Hull");
            Vector3 bow = bowFoil.position, stern = sternFoil.position, sittingHull = hull.position;
            Find("HullPivot").position += Vector3.up * 1.2f;
            if (bowFoil.position != bow || sternFoil.position != stern ||
                Mathf.Abs(hull.position.y - sittingHull.y - 1.2f) > .001f)
                errors.Add("Hydrofoil supports did not stay fixed when lifting the hull");
            report.Add("  Hydrofoil: HullPivot lifted 1.2 m while both foil nodes stayed fixed.");
        }
    }
}

