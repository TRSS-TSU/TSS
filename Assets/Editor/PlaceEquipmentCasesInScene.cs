using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaceEquipmentCasesInScene
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ReportPath = "Assets/Generated/Blender/EquipmentCases_PlacementReport.txt";
    private const string RequestPath = "Assets/Generated/Blender/.place_equipment_cases_request";
    private const string VisibleLidName = "Visible_Open_Lid";

    private static readonly CaseSpec[] Cases =
    {
        new CaseSpec(
            "Equipment Case Inventory A",
            "Assets/Generated/Blender/EquipmentCase_InventoryStandIn/EquipmentCase_InventoryStandIn.fbx",
            new Vector3(-11.8f, 0f, 3.6f)),
        new CaseSpec(
            "Equipment Case Inventory B",
            "Assets/Generated/Blender/EquipmentCase_InventoryStandIn_VariantB/EquipmentCase_InventoryStandIn_VariantB.fbx",
            new Vector3(-9.8f, 0f, 3.6f))
    };

    [InitializeOnLoadMethod]
    private static void AutoPlaceWhenRequested()
    {
        if (!File.Exists(RequestPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath))
                return;

            Place();
            File.Delete(RequestPath);
            AssetDatabase.Refresh();
        };
    }

    [MenuItem("Tools/TSS/Place Equipment Cases")]
    public static void Place()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        RemoveExisting();

        var root = new GameObject("Equipment Case Stand-Ins");
        var report = new StringBuilder();
        report.AppendLine("Equipment case placement report");
        report.AppendLine($"Scene: {ScenePath}");

        foreach (var spec in Cases)
            PlaceCase(spec, root.transform, report);

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report.ToString());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
    }

    private static void PlaceCase(CaseSpec spec, Transform parent, StringBuilder report)
    {
        AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceUpdate);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.AssetPath);
        if (!prefab)
            throw new FileNotFoundException($"Missing equipment case FBX: {spec.AssetPath}");

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = spec.Name;
        instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        var bounds = GetRendererBounds(instance);
        instance.transform.position += Vector3.up * -bounds.min.y;
        AddVisibleOpenLid(instance);
        bounds = GetRendererBounds(instance);

        var childNames = instance.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
        var hasLid = childNames.Any(n => n.Contains("Lid_Shell"));
        var hasFoam = childNames.Any(n => n.Contains("Lid_InnerFoam") || n.Contains("FoamBump"));
        var hasHinge = childNames.Any(n => n.Contains("Lid_Hinge"));

        report.AppendLine();
        report.AppendLine(spec.Name);
        report.AppendLine($"  Asset: {spec.AssetPath}");
        report.AppendLine($"  Position: {Format(instance.transform.position)}");
        report.AppendLine($"  Rotation: {Format(instance.transform.eulerAngles)}");
        report.AppendLine($"  Scale: {Format(instance.transform.localScale)}");
        report.AppendLine($"  Bounds min/max: {Format(bounds.min)} / {Format(bounds.max)}");
        report.AppendLine($"  Bounds size: {Format(bounds.size)}");
        report.AppendLine($"  Contains lid shell: {hasLid}");
        report.AppendLine($"  Contains lid foam: {hasFoam}");
        report.AppendLine($"  Contains lid hinge: {hasHinge}");
        report.AppendLine($"  Added visible lid: True");

        if (!hasLid || !hasFoam || !hasHinge)
            Debug.LogWarning($"{spec.Name} is missing expected lid or foam parts after import.");

        if (bounds.size.y < 0.9f || bounds.size.y > 1.4f || bounds.size.x < 1.0f || bounds.size.x > 2.0f)
            Debug.LogWarning($"{spec.Name} imported with unexpected size {bounds.size}.");
    }

    private static Bounds GetRendererBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.zero);

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private static void AddVisibleOpenLid(GameObject instance)
    {
        var existing = instance.transform.Find(VisibleLidName);
        if (existing)
            Object.DestroyImmediate(existing.gameObject);

        var shellMaterial = FindCaseMaterial(instance, "shell") ?? FindCaseMaterial(instance, "case") ?? FindCaseMaterial(instance, "");
        var foamMaterial = FindCaseMaterial(instance, "foam") ?? shellMaterial;

        var lid = new GameObject(VisibleLidName);
        lid.transform.SetParent(instance.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.69f, 0.57f);
        lid.transform.localRotation = Quaternion.identity;

        AddCube(lid.transform, "Shell", new Vector3(0f, 0f, 0f), new Vector3(1.45f, 0.9f, 0.07f), shellMaterial);
        AddCube(lid.transform, "Inner_Foam", new Vector3(0f, 0f, -0.04f), new Vector3(1.25f, 0.72f, 0.035f), foamMaterial);

        for (var x = -0.48f; x <= 0.48f; x += 0.24f)
        {
            for (var y = -0.24f; y <= 0.24f; y += 0.24f)
                AddCube(lid.transform, "Foam_Bump", new Vector3(x, y, -0.075f), new Vector3(0.13f, 0.1f, 0.035f), foamMaterial);
        }
    }

    private static void AddCube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = localPosition;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = localScale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(cube.GetComponent<Collider>());
    }

    private static Material FindCaseMaterial(GameObject instance, string nameHint)
    {
        return instance
            .GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m && m.shader && !m.shader.name.Contains("Error") && !m.name.Contains("VisibleLid"))
            .FirstOrDefault(m => m.name.ToLowerInvariant().Contains(nameHint));
    }

    private static void RemoveExisting()
    {
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (obj && obj.name == "Equipment Case Stand-Ins")
                Object.DestroyImmediate(obj);
        }
    }

    private static string Format(Vector3 value)
    {
        return $"({value.x:0.###}, {value.y:0.###}, {value.z:0.###})";
    }

    private readonly struct CaseSpec
    {
        public readonly string Name;
        public readonly string AssetPath;
        public readonly Vector3 Position;

        public CaseSpec(string name, string assetPath, Vector3 position)
        {
            Name = name;
            AssetPath = assetPath;
            Position = position;
        }
    }
}
