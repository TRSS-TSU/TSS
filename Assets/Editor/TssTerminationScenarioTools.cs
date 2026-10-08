using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class TssTerminationScenarioTools
{
    private const string ScenarioPath = "Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset";

    [MenuItem("TSS/Termination/Configure Balanced Lab Punch Downs")]
    [MenuItem("TSS/Termination/Configure Balanced Lab Punch Down")]
    public static void ConfigureBalancedLabPunchDowns()
    {
        var scenario = AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>(ScenarioPath);
        if (!scenario)
        {
            Debug.LogError($"Scenario not found: {ScenarioPath}");
            return;
        }

        var serialized = new SerializedObject(scenario);
        var connections = serialized.FindProperty("permanentConnections");
        if (connections == null || !connections.isArray)
        {
            Debug.LogError("Scenario has no permanentConnections array.");
            return;
        }

        var messages = new List<string>();
        var ids = new HashSet<string>();
        for (var i = 0; i < connections.arraySize; i++)
        {
            var link = connections.GetArrayElementAtIndex(i);
            var connectionId = link.FindPropertyRelative("connectionId");
            var endpointA = link.FindPropertyRelative("patchPanelEndpointId");
            var endpointB = link.FindPropertyRelative("wallportEndpointId");

            var id = Clean(connectionId.stringValue);
            if (string.IsNullOrEmpty(id))
                messages.Add($"Row {i}: blank connection ID.");
            else if (!ids.Add(id))
                messages.Add($"{id}: duplicate connection ID.");

            if (string.IsNullOrEmpty(Clean(endpointA.stringValue)))
                messages.Add($"{id}: blank endpoint A.");
            if (string.IsNullOrEmpty(Clean(endpointB.stringValue)))
                messages.Add($"{id}: blank endpoint B.");

            link.FindPropertyRelative("requiresPunchDownTermination").boolValue = true;
            link.FindPropertyRelative("endpointATerminationModuleId").stringValue = $"{id}:A";
            link.FindPropertyRelative("endpointBTerminationModuleId").stringValue = $"{id}:B";
            CheckEndpoint(id, endpointA.stringValue, messages);
            CheckEndpoint(id, endpointB.stringValue, messages);
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(scenario);
        AssetDatabase.SaveAssets();

        var summary = $"Configured {connections.arraySize} permanent runs for punch-down termination.";
        Debug.Log(messages.Count == 0 ? summary : $"{summary}\n{string.Join("\n", messages)}");
    }

    private static void CheckEndpoint(string connectionId, string endpointId, List<string> messages)
    {
        endpointId = Clean(endpointId);
        if (string.IsNullOrEmpty(endpointId))
            return;

        var count = 0;
        foreach (var endpoint in Object.FindObjectsByType<TssPortEndpoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (endpoint.EndpointId == endpointId)
                count++;
        }

        if (count != 1)
            messages.Add($"{connectionId}: endpoint '{endpointId}' resolves {count} times in the loaded scene.");
    }

    private static string Clean(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
