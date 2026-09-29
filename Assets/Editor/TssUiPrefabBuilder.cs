using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class TssUiPrefabBuilder
{
    private const string UiDir = "Assets/Prefabs/UI";
    private static readonly Color Surface = new(0.055f, 0.075f, 0.065f, 0.94f);
    private static readonly Color Surface2 = new(0.1f, 0.13f, 0.1f, 0.96f);
    private static readonly Color Amber = new(0.9f, 0.55f, 0.16f, 1f);
    private static readonly Color TextColor = new(0.92f, 0.96f, 0.9f, 1f);

    [MenuItem("TSS/Rebuild WP1 UI Prefabs")]
    public static void RebuildWp1UiPrefabs()
    {
        Directory.CreateDirectory(UiDir);
        var rowPrefab = CreateTrackerRow();
        var trackerPrefab = CreateTrackerPanel(rowPrefab);
        SplitRuntimeCanvas(trackerPrefab);
        StylePrefab($"{UiDir}/TssScenarioSelectionCanvas.prefab");
        StylePrefab($"{UiDir}/TssScenarioButton.prefab");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static TssPlacementTrackerRow CreateTrackerRow()
    {
        var root = new GameObject("TssPlacementTrackerRow", typeof(RectTransform), typeof(TssPlacementTrackerRow));
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340f, 22f);

        var nameText = AddText(root.transform, "NameText", "PDU1", 14, TextAnchor.MiddleLeft);
        var detailText = AddText(root.transform, "DetailText", "Rack01 U1  Pdu", 12, TextAnchor.MiddleRight);
        detailText.color = TextColor;
        Stretch(nameText.rectTransform, 0f, 0.48f);
        Stretch(detailText.rectTransform, 0.5f, 1f);

        var row = root.GetComponent<TssPlacementTrackerRow>();
        var serializedRow = Serialized(row);
        serializedRow.FindProperty("nameText").objectReferenceValue = nameText;
        serializedRow.FindProperty("detailText").objectReferenceValue = detailText;
        serializedRow.ApplyModifiedPropertiesWithoutUndo();

        var prefab = Save(root, $"{UiDir}/TssPlacementTrackerRow.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<TssPlacementTrackerRow>();
    }

    private static GameObject CreateTrackerPanel(TssPlacementTrackerRow rowPrefab)
    {
        var root = new GameObject("PlacementTrackerPanel", typeof(RectTransform), typeof(Image), typeof(TssPlacementTrackerPanel));
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta = new Vector2(380f, 440f);
        root.GetComponent<Image>().color = Surface;

        var title = AddText(root.transform, "Title", "WP1 PLACEMENT", 18, TextAnchor.MiddleLeft);
        title.color = Amber;
        var titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(-28f, 28f);

        var rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(root.transform, false);
        var rowsRect = rows.GetComponent<RectTransform>();
        rowsRect.anchorMin = new Vector2(0f, 0f);
        rowsRect.anchorMax = new Vector2(1f, 1f);
        rowsRect.pivot = new Vector2(0.5f, 1f);
        rowsRect.offsetMin = new Vector2(14f, 12f);
        rowsRect.offsetMax = new Vector2(-14f, -46f);

        var panel = root.GetComponent<TssPlacementTrackerPanel>();
        var serialized = Serialized(panel);
        serialized.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        serialized.FindProperty("rowsContainer").objectReferenceValue = rows.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var prefab = Save(root, $"{UiDir}/TssPlacementTrackerPanel.prefab");
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void SplitRuntimeCanvas(GameObject trackerPrefab)
    {
        var path = $"{UiDir}/TssRuntimeCanvas.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var panelNames = new[] { "HUD", "CasePanel", "SopPanel", "NamePromptPanel", "CorrectionPanel", "ComputerPanel", "DebugPanel" };

        foreach (var panelName in panelNames)
        {
            var child = root.transform.Find(panelName);
            if (!child)
                continue;

            var panelPath = $"{UiDir}/{PanelPrefabName(panelName)}.prefab";
            var copy = Object.Instantiate(child.gameObject);
            copy.name = panelName;
            StyleTree(copy);
            var panelPrefab = Save(copy, panelPath);
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(child.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, root.transform);
            instance.name = panelName;
        }

        var oldTracker = root.transform.Find("PlacementTrackerPanel");
        if (oldTracker)
            Object.DestroyImmediate(oldTracker.gameObject);

        var tracker = (GameObject)PrefabUtility.InstantiatePrefab(trackerPrefab, root.transform);
        tracker.name = "PlacementTrackerPanel";
        tracker.transform.SetAsLastSibling();

        StyleTree(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static string PanelPrefabName(string childName)
    {
        return childName switch
        {
            "HUD" => "TssRuntimeHud",
            "CasePanel" => "TssEquipmentCasePanel",
            "SopPanel" => "TssSopPanel",
            "NamePromptPanel" => "TssNamePromptPanel",
            "CorrectionPanel" => "TssCorrectionPanel",
            "ComputerPanel" => "TssComputerPanel",
            "DebugPanel" => "TssDebugPanel",
            _ => $"Tss{childName}"
        };
    }

    private static Text AddText(Transform parent, string name, string value, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = anchor;
        text.color = TextColor;
        return text;
    }

    private static void Stretch(RectTransform rect, float minX, float maxX)
    {
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StylePrefab(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        StyleTree(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void StyleTree(GameObject root)
    {
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            image.color = image.GetComponent<Button>() ? Amber : Surface2;

        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            var colors = button.colors;
            colors.normalColor = Amber;
            colors.highlightedColor = new Color(1f, 0.66f, 0.24f, 1f);
            colors.pressedColor = new Color(0.62f, 0.35f, 0.08f, 1f);
            button.colors = colors;
        }

        foreach (var input in root.GetComponentsInChildren<InputField>(true))
        {
            if (input.GetComponent<Image>())
                input.GetComponent<Image>().color = new Color(0.86f, 0.88f, 0.8f, 1f);
        }

        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            if (text.fontSize >= 20 || text.name.Contains("Title"))
                text.color = Amber;
            else if (!text.transform.GetComponentInParent<InputField>())
                text.color = TextColor;
        }
    }

    private static GameObject Save(GameObject go, string path)
    {
        return PrefabUtility.SaveAsPrefabAsset(go, path);
    }

    private static SerializedObject Serialized(Object target)
    {
        return new SerializedObject(target);
    }
}
