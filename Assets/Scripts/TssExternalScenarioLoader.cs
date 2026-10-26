using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class TssExternalScenarioLoader
{
    public const string Folder = "Scenarios";
    private readonly TssScenarioCatalog catalog;
    private readonly TssScenarioCatalogResolver catalogResolver;
    private readonly string root;
    private TssScenarioManifest manifest;
    public List<string> Warnings { get; } = new();

    public TssExternalScenarioLoader(TssScenarioCatalog catalog, string streamingAssetsRoot = null)
    {
        this.catalog = catalog;
        catalogResolver = new TssScenarioCatalogResolver(catalog);
        root = streamingAssetsRoot ?? Application.streamingAssetsPath;
    }

    public async Task<TssScenarioManifest> LoadManifestAsync()
    {
        Warnings.Clear();
        manifest = ParseManifest(await ReadJsonAsync("manifest.json"), Warnings);
        return manifest;
    }

    public async Task<TssScenarioDefinition> LoadScenarioAsync(string scenarioId)
    {
        if (manifest == null) await LoadManifestAsync();
        var entry = manifest.scenarios.SingleOrDefault(x => x.scenarioId == scenarioId);
        if (entry == null) throw new InvalidDataException("Scenario is not in the valid manifest: " + scenarioId);
        var data = JsonUtility.FromJson<TssExternalScenarioData>(await ReadJsonAsync(entry.jsonFile));
        NormalizeLegacy(data);
        if (data == null || data.scenarioId != entry.scenarioId || data.sceneName != entry.sceneName || data.displayName != entry.displayName ||
            data.facilityId != entry.facilityId || data.catalogId != entry.catalogId)
            throw new InvalidDataException("Manifest and scenario identity do not match: " + scenarioId);
        if (!Application.CanStreamedLevelBeLoaded(data.sceneName))
            throw new InvalidDataException("Scene is not enabled in Build Settings: " + data.sceneName);
        var selectedCatalog = catalogResolver.Resolve(data.catalogId);
        if (!selectedCatalog)
            throw new InvalidDataException("Unknown catalogId: " + data.catalogId);
        if (!TryCreate(data, selectedCatalog, out var scenario, out var errors))
            throw new InvalidDataException(string.Join("\n", errors));
        return scenario;
    }

    public static TssScenarioManifest ParseManifest(string json, List<string> warnings)
    {
        var parsed = JsonUtility.FromJson<TssScenarioManifest>(json);
        if (parsed == null || parsed.format != TssScenarioManifest.Format ||
            (parsed.schemaVersion != TssExternalScenarioData.LegacyVersion && parsed.schemaVersion != TssExternalScenarioData.Version) ||
            parsed.scenarios == null || parsed.scenarios.Length == 0)
            throw new InvalidDataException("Manifest must use schema version 1 or 2 and contain at least one entry.");
        var legacy = parsed.schemaVersion == TssExternalScenarioData.LegacyVersion;
        parsed.schemaVersion = TssExternalScenarioData.Version;
        var duplicates = parsed.scenarios.Where(x => x != null && !string.IsNullOrWhiteSpace(x.scenarioId))
            .GroupBy(x => x.scenarioId, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileDuplicates = parsed.scenarios.Where(x => x != null && !string.IsNullOrWhiteSpace(x.jsonFile))
            .GroupBy(x => x.jsonFile, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        parsed.scenarios = parsed.scenarios.Where(entry =>
        {
            if (legacy && entry != null)
            {
                entry.facilityId = TssExternalScenarioData.DefaultFacilityId;
                entry.catalogId = TssExternalScenarioData.DefaultCatalogId;
            }
            if (entry != null && !string.IsNullOrWhiteSpace(entry.scenarioId) && entry.scenarioId == entry.scenarioId.Trim() &&
                !duplicates.Contains(entry.scenarioId) && !fileDuplicates.Contains(entry.jsonFile) && !string.IsNullOrWhiteSpace(entry.displayName) &&
                !string.IsNullOrWhiteSpace(entry.facilityId) && !string.IsNullOrWhiteSpace(entry.catalogId) && entry.sceneName == "TSS" &&
                TssExternalScenarioValidator.IsLocalJsonFile(entry.jsonFile)) return true;
            warnings.Add("Unavailable manifest entry: " + (entry?.scenarioId ?? "missing entry"));
            return false;
        }).ToArray();
        return parsed;
    }

    public static bool TryCreate(TssExternalScenarioData data, TssScenarioCatalog catalog, out TssScenarioDefinition scenario, out List<string> errors)
    {
        scenario = null;
        NormalizeLegacy(data);
        errors = TssExternalScenarioValidator.Validate(data, catalog);
        if (errors.Count != 0) return false;
        scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.hideFlags = HideFlags.DontSave;
        scenario.IsExternalRuntime = true;
        // Copy reference-free rows by value, including nested binder rows; never share mutable authored data.
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), scenario);
        scenario.name = data.scenarioId + " (External)";
        scenario.inventory = data.inventory.Select(x => new ScenarioInventoryItem { equipment = catalog.FindEquipment(x.equipmentId), quantity = x.quantity }).ToArray();
        scenario.cableInventory = data.cableInventory.Select(x => new ScenarioCableInventoryItem { cable = catalog.FindCable(x.cableId), quantity = x.quantity }).ToArray();
        scenario.permanentConnections = data.permanentConnections.Select(x => new ScenarioPermanentCableConnection
        {
            connectionId = x.connectionId, cable = catalog.FindCable(x.cableId), cableType = x.cableType,
            patchPanelEndpointId = x.patchPanelEndpointId, wallportEndpointId = x.wallportEndpointId,
            requiresPunchDownTermination = x.requiresPunchDownTermination,
            endpointATerminationModuleId = x.endpointATerminationModuleId, endpointBTerminationModuleId = x.endpointBTerminationModuleId
        }).ToArray();
        return true;
    }

    public static TssExternalScenarioData Export(TssScenarioDefinition scenario)
    {
        var data = JsonUtility.FromJson<TssExternalScenarioData>(JsonUtility.ToJson(scenario));
        data.format = TssExternalScenarioData.Format;
        data.schemaVersion = TssExternalScenarioData.Version;
        data.facilityId = string.IsNullOrWhiteSpace(scenario.facilityId) ? TssExternalScenarioData.DefaultFacilityId : scenario.facilityId;
        data.catalogId = string.IsNullOrWhiteSpace(scenario.catalogId) ? TssExternalScenarioData.DefaultCatalogId : scenario.catalogId;
        data.inventory = (scenario.inventory ?? Array.Empty<ScenarioInventoryItem>()).Select(x => new TssExternalEquipmentItem { equipmentId = x?.equipment ? x.equipment.equipmentId : null, quantity = x?.quantity ?? 0 }).ToArray();
        data.cableInventory = (scenario.cableInventory ?? Array.Empty<ScenarioCableInventoryItem>()).Select(x => new TssExternalCableItem { cableId = x?.cable ? x.cable.cableId : null, quantity = x?.quantity ?? 0 }).ToArray();
        data.permanentConnections = (scenario.permanentConnections ?? Array.Empty<ScenarioPermanentCableConnection>()).Select(x => new TssExternalPermanentConnection
        {
            connectionId = x.connectionId, cableId = x.cable ? x.cable.cableId : null, cableType = x.cableType,
            patchPanelEndpointId = x.patchPanelEndpointId, wallportEndpointId = x.wallportEndpointId,
            requiresPunchDownTermination = x.requiresPunchDownTermination,
            endpointATerminationModuleId = x.endpointATerminationModuleId, endpointBTerminationModuleId = x.endpointBTerminationModuleId
        }).ToArray();
        return data;
    }

    public static void NormalizeLegacy(TssExternalScenarioData data)
    {
        if (data == null) return;
        if (data.schemaVersion == TssExternalScenarioData.LegacyVersion)
        {
            data.schemaVersion = TssExternalScenarioData.Version;
            data.facilityId = TssExternalScenarioData.DefaultFacilityId;
            data.catalogId = TssExternalScenarioData.DefaultCatalogId;
            if (string.IsNullOrWhiteSpace(data.sceneName)) data.sceneName = "TSS";
        }
    }

    public static TssScenarioDefinition[] GetFallbackScenarios(TssScenarioDefinition[] serialized, TssScenarioDefinition sessionFallback)
    {
        var choices = (serialized ?? Array.Empty<TssScenarioDefinition>()).Where(x => x).Distinct().ToArray();
        return choices.Length > 0 ? choices : sessionFallback ? new[] { sessionFallback } : Array.Empty<TssScenarioDefinition>();
    }

    private async Task<string> ReadJsonAsync(string file)
    {
        if (!TssExternalScenarioValidator.IsLocalJsonFile(file)) throw new InvalidDataException("Invalid local scenario filename.");
        var url = root.Contains("://") ? root.TrimEnd('/') + "/" + Folder + "/" + file : new Uri(Path.Combine(root, Folder, file)).AbsoluteUri;
        using var request = UnityWebRequest.Get(url);
        request.timeout = 15;
        var operation = request.SendWebRequest();
        while (!operation.isDone) await Task.Yield();
        if (request.result != UnityWebRequest.Result.Success) throw new IOException("Cannot load " + file + ": " + request.error);
        return request.downloadHandler.text;
    }
}
