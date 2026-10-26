using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TssResultsSceneBuilder
{
    private const string ResultsScenePath = "Assets/Scenes/Results.unity";
    private const string CardPath = "Assets/Prefabs/UI/TssResultsCard.prefab";
    private const string RuntimeCanvasPath = "Assets/Prefabs/UI/TssRuntimeCanvas.prefab";

    [MenuItem("TSS/Results/Build Results Scene and End Scenario UI")]
    public static void Build()
    {
        PatchRuntimeCanvas();
        var card = AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
        if (!card)
            card = BuildCardPrefab();
        BuildResultsScene(card);
        AddToBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TSS Results] Built serialized Results scene, result-card prefab, and End Scenario confirmation UI.");
    }

    private static void PatchRuntimeCanvas()
    {
        var root = PrefabUtility.LoadPrefabContents(RuntimeCanvasPath);
        try
        {
            var hud = root.transform.Find("HUD");
            if (hud && !hud.Find("EndScenarioButton"))
            {
                var button = CreateButton(hud, "EndScenarioButton", "END SCENARIO", new Vector2(-220f, -12f), new Vector2(120f, 36f), new Color(.55f, .18f, .12f));
                button.transform.SetSiblingIndex(0);
            }

            if (!root.transform.Find("EndScenarioPanel"))
            {
                var panel = CreatePanel(root.transform, "EndScenarioPanel", new Vector2(720f, 360f), new Color(.03f, .05f, .05f, .98f));
                CreateText(panel.transform, "Title", "END SCENARIO?", new Vector2(0f, 105f), new Vector2(620f, 44f), 30, TextAnchor.MiddleCenter, Color.white);
                CreateText(panel.transform, "Body", "The current state will be assessed and shown in Results. This works for complete or incomplete attempts.", new Vector2(0f, 25f), new Vector2(620f, 90f), 22, TextAnchor.MiddleCenter, new Color(.84f, .9f, .84f));
                CreateButton(panel.transform, "ConfirmButton", "VIEW RESULTS", new Vector2(-145f, -105f), new Vector2(240f, 52f), new Color(.18f, .55f, .28f));
                CreateButton(panel.transform, "CancelButton", "CANCEL", new Vector2(145f, -105f), new Vector2(180f, 52f), new Color(.25f, .3f, .3f));
            }

            PrefabUtility.SaveAsPrefabAsset(root, RuntimeCanvasPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject BuildCardPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TssResultsCard>(CardPath);
        if (existing)
            AssetDatabase.DeleteAsset(CardPath);

        var root = CreatePanel(null, "TssResultsCard", new Vector2(0f, 280f), new Color(.08f, .12f, .12f, .98f));
        var rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 280f);
        var layout = root.AddComponent<LayoutElement>();
        layout.minHeight = 280f;
        layout.preferredHeight = 280f;

        var card = root.AddComponent<TssResultsCard>();
        var title = CreateText(root.transform, "Title", "WORK PACKAGE", new Vector2(-270f, -28f), new Vector2(430f, 42f), 28, TextAnchor.MiddleLeft, new Color(.95f, .72f, .28f));
        var status = CreateText(root.transform, "Status", "INCOMPLETE", new Vector2(270f, -28f), new Vector2(220f, 42f), 24, TextAnchor.MiddleRight, Color.white);
        var detail = CreateText(root.transform, "Detail", string.Empty, new Vector2(0f, -145f), new Vector2(900f, 220f), 19, TextAnchor.UpperLeft, new Color(.88f, .92f, .88f));
        detail.horizontalOverflow = HorizontalWrapMode.Wrap;
        detail.verticalOverflow = VerticalWrapMode.Overflow;

        var serialized = new SerializedObject(card);
        serialized.FindProperty("title").objectReferenceValue = title;
        serialized.FindProperty("status").objectReferenceValue = status;
        serialized.FindProperty("detail").objectReferenceValue = detail;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, CardPath);
        Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<GameObject>(CardPath);
    }

    private static void BuildResultsScene(GameObject cardPrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = new GameObject("ResultsCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.transform.localScale = Vector3.one;
        var canvasComponent = canvas.GetComponent<Canvas>();
        canvasComponent.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        var background = CreatePanel(canvas.transform, "Background", Vector2.zero, new Color(.015f, .025f, .03f, 1f));
        Stretch(background.GetComponent<RectTransform>());
        var header = CreateText(canvas.transform, "Header", "SCENARIO RESULTS", Vector2.zero, new Vector2(900f, 62f), 40, TextAnchor.MiddleRight, new Color(.95f, .72f, .28f));
        AnchorTopRight(header.rectTransform, new Vector2(-50f, -36f));
        var scenario = CreateText(canvas.transform, "Scenario", "Scenario", Vector2.zero, new Vector2(900f, 42f), 25, TextAnchor.MiddleRight, Color.white);
        AnchorTopRight(scenario.rectTransform, new Vector2(-50f, -108f));
        var summary = CreateText(canvas.transform, "OverallSummary", string.Empty, Vector2.zero, new Vector2(1200f, 42f), 25, TextAnchor.MiddleRight, Color.white);
        AnchorTopRight(summary.rectTransform, new Vector2(-50f, -164f));

        var contentRoot = CreatePanel(canvas.transform, "ResultsContent", Vector2.zero, new Vector2(1880f, 760f), new Color(0f, 0f, 0f, 0f));
        AnchorTopCenter(contentRoot.GetComponent<RectTransform>(), new Vector2(0f, -220f));
        var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
        content.transform.SetParent(contentRoot.transform, false);
        var contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 0f);
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(450f, 280f);
        grid.spacing = new Vector2(14f, 14f);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        AddLayoutSamples(cardPrefab, content.transform);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        var noResults = CreatePanel(canvas.transform, "NoResultsState", Vector2.zero, new Vector2(1200f, 220f), new Color(.08f, .12f, .12f, .98f));
        AnchorTopCenter(noResults.GetComponent<RectTransform>(), new Vector2(0f, -360f));
        CreateText(noResults.transform, "Message", "No results available", Vector2.zero, new Vector2(1000f, 80f), 34, TextAnchor.MiddleCenter, Color.white);
        var mainMenu = CreateButton(canvas.transform, "MainMenuButton", "MAIN MENU", Vector2.zero, new Vector2(260f, 56f), new Color(.18f, .55f, .28f));
        AnchorBottomCenter(mainMenu.GetComponent<RectTransform>(), new Vector2(0f, 30f));

        var view = canvas.AddComponent<TssResultsView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("resultsContent").objectReferenceValue = contentRoot;
        serialized.FindProperty("noResultsState").objectReferenceValue = noResults;
        serialized.FindProperty("overallSummary").objectReferenceValue = summary;
        serialized.FindProperty("scenarioText").objectReferenceValue = scenario;
        serialized.FindProperty("workPackageContainer").objectReferenceValue = content.transform;
        serialized.FindProperty("resultCardPrefab").objectReferenceValue = cardPrefab;
        serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        serialized.ApplyModifiedPropertiesWithoutUndo();

#if ENABLE_INPUT_SYSTEM
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
#else
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        noResults.SetActive(false);
        EditorSceneManager.SaveScene(scene, ResultsScenePath);
        Object.DestroyImmediate(eventSystem);
        Object.DestroyImmediate(canvas);
    }

    private static void AddLayoutSamples(GameObject cardPrefab, Transform parent)
    {
        var titles = new[] { "WORK PACKAGE 1", "WORK PACKAGE 2", "WORK PACKAGE 3", "WORK PACKAGE 4" };
        var statuses = new[] { "INCOMPLETE", "PASS", "INCOMPLETE", "PASS" };
        var details = new[]
        {
            "Verified at exit:\n  [PASS] AN-1 at Rack01 U35-U38.\n  [PASS] IT1 at ITDesk.\n\nActionable remaining criteria:\n  [ ] Place TestLaptop at LobbyTable01.",
            "Verified at exit:\n  [PASS] AN-1 VLAN database is complete.\n  [PASS] Required trunks are correct.\n\nNo remaining criteria.",
            "Verified at exit:\n  [PASS] Router parent interface is correct.\n\nActionable remaining criteria:\n  [ ] Provider-facing WAN configuration is incomplete.\n  [ ] Router configuration has not been saved.",
            "Verified at exit:\n  [PASS] AP uplink connected.\n  [PASS] Test laptop received the expected DHCP lease.\n\nNo remaining criteria."
        };

        for (var i = 0; i < titles.Length; i++)
        {
            var sample = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, parent);
            sample.name = titles[i].Replace(' ', '_') + "_Sample";
            var title = sample.transform.Find("Title")?.GetComponent<Text>();
            var status = sample.transform.Find("Status")?.GetComponent<Text>();
            var detail = sample.transform.Find("Detail")?.GetComponent<Text>();
            if (title) title.text = titles[i];
            if (status)
            {
                status.text = statuses[i];
                status.color = statuses[i] == "PASS" ? new Color(.35f, 1f, .55f) : new Color(1f, .65f, .25f);
            }
            if (detail) detail.text = details[i];
        }
    }

    private static void AddToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(x => x.path == ResultsScenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(ResultsScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (parent) root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        root.GetComponent<Image>().color = color;
        return root;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color) => CreatePanel(parent, name, Vector2.zero, size, color);

    private static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color color)
    {
        var root = CreatePanel(parent, name, position, size, color);
        var button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        var text = CreateText(root.transform, "Text", label, Vector2.zero, size, 18, TextAnchor.MiddleCenter, Color.white);
        text.resizeTextForBestFit = true;
        return button;
    }

    private static Text CreateText(Transform parent, string name, string value, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        root.transform.SetParent(parent, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = root.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void AnchorTopRight(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = position;
    }

    private static void AnchorTopCenter(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = position;
    }

    private static void AnchorBottomCenter(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = position;
    }
}
