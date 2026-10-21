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
    [MenuItem("TSS/External Scenarios/Export Four Sample Scenarios")]
    public static void ExportSamples()
    {
        var source = AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>(BaselinePath);
        var catalog = AssetDatabase.LoadAssetAtPath<TssScenarioCatalog>(CatalogPath);
        if (!source || !catalog) throw new InvalidOperationException("Baseline scenario/catalog is missing.");
        var manifest = new TssScenarioManifest { format = TssScenarioManifest.Format, schemaVersion = 1, scenarios = new TssScenarioManifestEntry[4] };
        var files = new[] { "balanced-lab.json", "installation-practice.json", "network-verification.json", "wireless-readiness.json" };
        var titles = new[] { source.displayName, "Installation Practice", "Network Verification", "Wireless Readiness" };
        var descriptions = new[] { "Original balanced facility exercise.", "Extra equipment and cables; start with the second rack.", "Alternate VLAN bounds and additional endpoint diagnostics.", "Wireless-focused binder and readiness checks with spare equipment." };
        var folder = Path.Combine(Application.streamingAssetsPath, TssExternalScenarioLoader.Folder);
        Directory.CreateDirectory(folder);
        for (var index = 0; index < files.Length; index++)
        {
            var data = TssExternalScenarioLoader.Export(source);
            if (index > 0)
            {
                data.scenarioId = new[] { "", "INSTALLATION_PRACTICE", "NETWORK_VERIFICATION", "WIRELESS_READINESS" }[index];
                data.displayName = titles[index];
                data.cableInventory[0].quantity += index * 5;
                data.inventory.First(x => catalog.FindEquipment(x.equipmentId).category == EquipmentCategory.DesktopPc).quantity += index;
                data.binderReference.namingAndRacks.intro = descriptions[index] + " " + data.binderReference.namingAndRacks.intro;
                data.binderReference.cableSchedule.introParagraph = "Training variant: " + titles[index] + ". " + data.binderReference.cableSchedule.introParagraph;
            }
            if (index == 1)
            {
                Array.Reverse(data.expectedRackPlacements);
                foreach (var row in data.expectedRackPlacements) row.phaseLabel = row.rackId == "Rack02" ? "Phase 1" : "Phase 2";
                foreach (var row in data.namingRows) row.rationale = "Installation practice: verify this label before fitting equipment.";
            }
            if (index == 2)
            {
                data.networkScenario.vlanMinimum = 100;
                data.networkScenario.vlanMaximum = 290;
                data.networkScenario.managementVlanName = "Administration";
                data.networkScenario.dataVlanName = "Operations";
                foreach (var role in data.networkScenario.roleBindings) role.displayName += " - Verification";
                data.networkScenario.evidenceRequirements = data.networkScenario.evidenceRequirements.Concat(new[]
                {
                    new NetworkEvidenceRequirement { evidenceId = "Provider-trace", command = "tracert provider", description = "Record a successful provider route trace." }
                }).ToArray();
                data.binderReference.ipPlan.paragraph = "Verify the Administration and Operations networks using your generated addresses. " + data.binderReference.ipPlan.paragraph;
            }
            if (index == 3)
            {
                data.inventory.First(x => catalog.FindEquipment(x.equipmentId).category == EquipmentCategory.AccessPoint).quantity++;
                data.binderReference.wireless.intro = "Wireless readiness: configure AP-1, associate TestLaptop, then verify provider access. " + data.binderReference.wireless.intro;
                data.networkScenario.evidenceRequirements = data.networkScenario.evidenceRequirements.Concat(new[]
                {
                    new NetworkEvidenceRequirement { evidenceId = "Wireless-provider-trace", command = "tracert provider", description = "Record a provider route trace during wireless readiness checks." }
                }).ToArray();
            }
            var errors = TssExternalScenarioValidator.Validate(data, catalog);
            if (errors.Count > 0) throw new InvalidDataException(files[index] + ":\n" + string.Join("\n", errors));
            File.WriteAllText(Path.Combine(folder, files[index]), JsonUtility.ToJson(data, true));
            manifest.scenarios[index] = new TssScenarioManifestEntry { scenarioId = data.scenarioId, displayName = data.displayName, jsonFile = files[index], sceneName = data.sceneName, description = descriptions[index] };
        }
        File.WriteAllText(Path.Combine(folder, "manifest.json"), JsonUtility.ToJson(manifest, true));
        AssetDatabase.Refresh();
        Debug.Log("[TSS Scenarios] Exported four validated scenario files and manifest.");
    }
}
