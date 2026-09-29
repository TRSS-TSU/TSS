using System.Collections.Generic;
using UnityEngine;

public static class TssPlacementEvaluator
{
    public static TssPlacementEvaluationResult Evaluate(
        TssScenarioDefinition scenario,
        IReadOnlyList<InstalledEquipmentRecord> rackPlacements,
        IReadOnlyList<EndpointPlacementRecord> endpointPlacements)
    {
        var messages = new List<string>();
        if (!scenario)
        {
            messages.Add("No scenario loaded.");
            return new TssPlacementEvaluationResult(messages);
        }

        CheckRackPlacements(scenario.expectedRackPlacements, rackPlacements, messages);
        CheckEndpointPlacements(scenario.expectedEndpointPlacements, endpointPlacements, messages);
        return new TssPlacementEvaluationResult(messages);
    }

    public static IReadOnlyList<TssPlacementRequirementStatus> EvaluateRequirements(
        TssScenarioDefinition scenario,
        IReadOnlyList<InstalledEquipmentRecord> rackPlacements,
        IReadOnlyList<EndpointPlacementRecord> endpointPlacements)
    {
        var statuses = new List<TssPlacementRequirementStatus>();
        if (!scenario)
            return statuses;

        AddRackStatuses(scenario.expectedRackPlacements, rackPlacements, statuses);
        AddEndpointStatuses(scenario.expectedEndpointPlacements, endpointPlacements, statuses);
        return statuses;
    }

    public static TssPlacementEvaluationResult EvaluateInventory(TssScenarioDefinition scenario)
    {
        var messages = new List<string>();
        if (!scenario)
        {
            messages.Add("No scenario loaded.");
            return new TssPlacementEvaluationResult(messages);
        }

        RequireInventory(scenario, EquipmentCategory.Pdu, 2, messages);
        RequireInventory(scenario, EquipmentCategory.PatchPanel, 2, messages);
        RequireInventory(scenario, EquipmentCategory.Switch, 2, messages);
        RequireInventory(scenario, EquipmentCategory.Router, 1, messages);
        RequireInventory(scenario, EquipmentCategory.Server, 1, messages);
        RequireInventory(scenario, EquipmentCategory.DesktopPc, 4, messages);
        RequireInventory(scenario, EquipmentCategory.Printer, 2, messages);
        return new TssPlacementEvaluationResult(messages);
    }

    private static void CheckRackPlacements(
        IReadOnlyList<ExpectedRackPlacement> expected,
        IReadOnlyList<InstalledEquipmentRecord> actual,
        List<string> messages)
    {
        if (expected == null)
            return;

        foreach (var required in expected)
        {
            var firstU = required.endingU > 0 ? Mathf.Min(required.startingU, required.endingU) : required.startingU;
            var lastU = Mathf.Max(required.startingU, required.endingU);
            var location = firstU == lastU
                ? $"{required.rackId} U{firstU}"
                : $"{required.rackId} U{firstU}-U{lastU}";
            var found = false;
            for (var i = 0; actual != null && i < actual.Count; i++)
            {
                var record = actual[i];
                if (record.RackId != required.rackId || record.StartingU < firstU || record.StartingU > lastU)
                    continue;
                if (!IsOnExpectedRackSide(required, record))
                    continue;

                found = true;
                var category = record.Equipment ? record.Equipment.category : (EquipmentCategory?)null;
                if (category != required.category)
                    messages.Add($"{location}: expected {required.category}.");

                if (record.DeviceName != required.requiredName)
                    messages.Add($"{location}: expected name {required.requiredName}.");
            }

            if (!found)
                messages.Add($"{location}: missing {required.requiredName}.");
        }
    }

    private static void CheckEndpointPlacements(
        IReadOnlyList<ExpectedEndpointPlacement> expected,
        IReadOnlyList<EndpointPlacementRecord> actual,
        List<string> messages)
    {
        if (expected == null)
            return;

        foreach (var required in expected)
        {
            var matches = 0;
            for (var i = 0; actual != null && i < actual.Count; i++)
            {
                var record = actual[i];
                if (record.StationId != required.stationId)
                    continue;

                var category = record.Equipment ? record.Equipment.category : (EquipmentCategory?)null;
                if (category == required.category && record.DeviceName == required.requiredName)
                    matches++;
            }

            if (matches == 0)
                messages.Add($"{required.stationId}: missing {required.requiredName}.");
            else if (matches > 1)
                messages.Add($"{required.stationId}: duplicate {required.requiredName}.");
        }
    }

    private static void AddRackStatuses(
        IReadOnlyList<ExpectedRackPlacement> expected,
        IReadOnlyList<InstalledEquipmentRecord> actual,
        List<TssPlacementRequirementStatus> statuses)
    {
        if (expected == null)
            return;

        foreach (var required in expected)
        {
            var firstU = required.endingU > 0 ? Mathf.Min(required.startingU, required.endingU) : required.startingU;
            var lastU = Mathf.Max(required.startingU, required.endingU);
            var found = false;
            var hasMismatch = false;
            for (var i = 0; actual != null && i < actual.Count; i++)
            {
                var record = actual[i];
                if (record.RackId != required.rackId || record.StartingU < firstU || record.StartingU > lastU)
                    continue;
                if (!IsOnExpectedRackSide(required, record))
                    continue;

                found = true;
                var category = record.Equipment ? record.Equipment.category : (EquipmentCategory?)null;
                if (category != required.category || record.DeviceName != required.requiredName)
                    hasMismatch = true;
            }

            statuses.Add(new TssPlacementRequirementStatus(
                required.phaseLabel,
                required.requiredName,
                firstU == lastU ? $"{required.rackId} U{firstU}" : $"{required.rackId} U{firstU}-U{lastU}",
                required.category,
                found && !hasMismatch));
        }
    }

    private static void AddEndpointStatuses(
        IReadOnlyList<ExpectedEndpointPlacement> expected,
        IReadOnlyList<EndpointPlacementRecord> actual,
        List<TssPlacementRequirementStatus> statuses)
    {
        if (expected == null)
            return;

        foreach (var required in expected)
        {
            var matches = 0;
            for (var i = 0; actual != null && i < actual.Count; i++)
            {
                var record = actual[i];
                if (record.StationId != required.stationId)
                    continue;

                var category = record.Equipment ? record.Equipment.category : (EquipmentCategory?)null;
                if (category == required.category && record.DeviceName == required.requiredName)
                    matches++;
            }

            statuses.Add(new TssPlacementRequirementStatus(
                required.phaseLabel,
                required.requiredName,
                required.stationId,
                required.category,
                matches == 1));
        }
    }

    private static bool IsOnExpectedRackSide(ExpectedRackPlacement required, InstalledEquipmentRecord record)
    {
        return !record.Equipment || record.Equipment.useRearRackPlacement == (required.category == EquipmentCategory.Pdu);
    }

    private static void RequireInventory(TssScenarioDefinition scenario, EquipmentCategory category, int requiredCount, List<string> messages)
    {
        var count = 0;
        if (scenario.inventory != null)
        {
            foreach (var item in scenario.inventory)
            {
                if (item != null && item.equipment && item.equipment.category == category)
                    count += item.quantity;
            }
        }

        if (count != requiredCount)
            messages.Add($"Inventory {category}: expected {requiredCount}, found {count}.");
    }
}

public readonly struct TssPlacementRequirementStatus
{
    public readonly string PhaseLabel;
    public readonly string RequiredName;
    public readonly string Location;
    public readonly EquipmentCategory Category;
    public readonly bool IsComplete;

    public TssPlacementRequirementStatus(string phaseLabel, string requiredName, string location, EquipmentCategory category, bool isComplete)
    {
        PhaseLabel = phaseLabel;
        RequiredName = requiredName;
        Location = location;
        Category = category;
        IsComplete = isComplete;
    }
}

public readonly struct TssPlacementEvaluationResult
{
    public readonly IReadOnlyList<string> Messages;
    public bool IsComplete => Messages.Count == 0;

    public TssPlacementEvaluationResult(IReadOnlyList<string> messages)
    {
        Messages = messages;
    }
}
