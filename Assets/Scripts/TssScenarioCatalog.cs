using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TSS/External Scenario Catalog")]
public sealed class TssScenarioCatalog : ScriptableObject
{
    public const string ResourcePath = "TssScenarioCatalog";
    public string facilityId = TssExternalScenarioData.DefaultFacilityId;
    public string catalogId = TssExternalScenarioData.DefaultCatalogId;
    public EquipmentDefinition[] equipment;
    public TssCableDefinition[] cables;
    public string sceneName;
    public string[] rackIds;
    public string[] stationIds;
    public string[] endpointIds;

    public EquipmentDefinition FindEquipment(string id) => Array.Find(equipment ?? Array.Empty<EquipmentDefinition>(), x => x && x.equipmentId == id);
    public TssCableDefinition FindCable(string id) => Array.Find(cables ?? Array.Empty<TssCableDefinition>(), x => x && x.cableId == id);

    public List<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in equipment ?? Array.Empty<EquipmentDefinition>())
            if (!item || string.IsNullOrWhiteSpace(item.equipmentId) || !ids.Add(item.equipmentId))
                errors.Add("Catalog has a missing or duplicate equipment ID.");
        ids.Clear();
        foreach (var item in cables ?? Array.Empty<TssCableDefinition>())
            if (!item || string.IsNullOrWhiteSpace(item.cableId) || !ids.Add(item.cableId))
                errors.Add("Catalog has a missing or duplicate cable ID.");
        if (equipment == null || equipment.Length == 0 || cables == null || cables.Length == 0)
            errors.Add("Catalog requires equipment and cables.");
        if (string.IsNullOrWhiteSpace(facilityId)) errors.Add("Catalog facilityId is required.");
        if (string.IsNullOrWhiteSpace(catalogId)) errors.Add("Catalog catalogId is required.");
        if (string.IsNullOrWhiteSpace(sceneName) || sceneName != "TSS" || rackIds == null || stationIds == null || endpointIds == null)
            errors.Add("Catalog requires the TSS facility identifiers.");
        foreach (var group in new[] { rackIds, stationIds, endpointIds })
        {
            ids.Clear();
            foreach (var id in group ?? Array.Empty<string>())
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) errors.Add("Catalog has a missing or duplicate facility ID: " + id);
        }
        return errors;
    }
}
