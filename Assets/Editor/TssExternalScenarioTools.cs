using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TssExternalScenarioTools
{
    private const string CatalogPath = "Assets/Resources/TssScenarioCatalog.asset";
    private const string BaselinePath = "Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset";

    [MenuItem("TSS/External Scenarios/Rebuild Asset and Facility Catalog")]
    public static void RebuildCatalog()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Rebuild the catalog outside Play Mode.");
        var catalog = AssetDatabase.LoadAssetAtPath<TssScenarioCatalog>(CatalogPath);
        if (!catalog)
        {
            Directory.CreateDirectory("Assets/Resources");
            catalog = ScriptableObject.CreateInstance<TssScenarioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        Undo.RecordObject(catalog, "Rebuild scenario catalog");
        catalog.equipment = AssetDatabase.FindAssets("t:EquipmentDefinition").Select(id => AssetDatabase.LoadAssetAtPath<EquipmentDefinition>(AssetDatabase.GUIDToAssetPath(id))).OrderBy(x => x.equipmentId).ToArray();
        catalog.cables = AssetDatabase.FindAssets("t:TssCableDefinition").Select(id => AssetDatabase.LoadAssetAtPath<TssCableDefinition>(AssetDatabase.GUIDToAssetPath(id))).OrderBy(x => x.cableId).ToArray();
        var preview = EditorSceneManager.OpenPreviewScene("Assets/Scenes/TSS.unity");
        try
        {
            var roots = preview.GetRootGameObjects();
            catalog.facilityId = TssExternalScenarioData.DefaultFacilityId;
            catalog.catalogId = TssExternalScenarioData.DefaultCatalogId;
            catalog.sceneName = "TSS";
            catalog.rackIds = roots.SelectMany(x => x.GetComponentsInChildren<RackMountController>(true)).Select(x => x.RackId).OrderBy(x => x).ToArray();
            catalog.stationIds = roots.SelectMany(x => x.GetComponentsInChildren<EndpointPlacementStation>(true)).Select(x => x.StationId).OrderBy(x => x).ToArray();
            catalog.endpointIds = roots.SelectMany(x => x.GetComponentsInChildren<TssPortEndpoint>(true)).Select(x => x.EndpointId).OrderBy(x => x).ToArray();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        var errors = catalog.Validate();
        if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("[TSS Scenarios] Catalog rebuilt from existing assets and TSS facility.");
    }

    // Explicit export only: never overwrite instructor files on import or when the game starts.
    [MenuItem("TSS/External Scenarios/Export Capstone Scenario")]
    public static void ExportCapstoneScenario()
    {
        var source = AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>(BaselinePath);
        var catalog = AssetDatabase.LoadAssetAtPath<TssScenarioCatalog>(CatalogPath);
        if (!source || !catalog) throw new InvalidOperationException("Baseline scenario/catalog is missing.");
        var manifest = new TssScenarioManifest { format = TssScenarioManifest.Format, schemaVersion = TssExternalScenarioData.Version, scenarios = new TssScenarioManifestEntry[1] };
        var file = "capstone.json";
        var description = "Final capstone scenario for Technical Systems Support course.";
        var folder = Path.Combine(Application.streamingAssetsPath, TssExternalScenarioLoader.Folder);
        Directory.CreateDirectory(folder);
        var data = TssExternalScenarioLoader.Export(source);
        var errors = TssExternalScenarioValidator.Validate(data, catalog);
        if (errors.Count > 0) throw new InvalidDataException(file + ":\n" + string.Join("\n", errors));
        File.WriteAllText(Path.Combine(folder, file), JsonUtility.ToJson(data, true));
        manifest.scenarios[0] = new TssScenarioManifestEntry { scenarioId = data.scenarioId, displayName = data.displayName, facilityId = data.facilityId, catalogId = data.catalogId, jsonFile = file, sceneName = data.sceneName, description = description };
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true));
        AssetDatabase.Refresh();
        Debug.Log("[TSS Scenarios] Exported the validated Capstone scenario and manifest.");
    }
}
