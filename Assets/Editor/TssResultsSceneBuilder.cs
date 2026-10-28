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
        var card = BuildCardPrefab();
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
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1840f, 650f);
        var layout = root.AddComponent<LayoutElement>();
        layout.minWidth = 1840f;
        layout.preferredWidth = 1840f;
        layout.minHeight = 650f;
        layout.preferredHeight = 650f;

        var card = root.AddComponent<TssResultsCard>();
        var title = CreateText(root.transform, "Title", "WORK PACKAGE", new Vector2(50f, -28f), new Vector2(1200f, 42f), 30, TextAnchor.MiddleLeft, new Color(.95f, .72f, .28f));
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f);
        title.rectTransform.pivot = new Vector2(0f, 1f);
        var status = CreateText(root.transform, "Status", "INCOMPLETE", new Vector2(-50f, -28f), new Vector2(300f, 42f), 24, TextAnchor.MiddleRight, Color.white);
        status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(1f, 1f);
        status.rectTransform.pivot = new Vector2(1f, 1f);
        var detailViewport = CreatePanel(root.transform, "DetailViewport", Vector2.zero, new Vector2(1780f, 560f), new Color(.02f, .04f, .05f, .12f));
        var detailViewportRect = detailViewport.GetComponent<RectTransform>();
        detailViewportRect.anchorMin = new Vector2(.5f, 0f);
        detailViewportRect.anchorMax = new Vector2(.5f, 0f);
        detailViewportRect.pivot = new Vector2(.5f, 0f);
        detailViewportRect.anchoredPosition = new Vector2(0f, 12f);
        detailViewport.AddComponent<Mask>().showMaskGraphic = true;
        var completedViewport = CreatePanel(detailViewport.transform, "CompletedViewport", new Vector2(-445f, 0f), new Vector2(830f, 520f), Color.clear);
        completedViewport.GetComponent<Image>().color = Color.clear;
        completedViewport.AddComponent<RectMask2D>();
        var completed = CreateText(completedViewport.transform, "CompletedDetail", string.Empty, Vector2.zero, new Vector2(790f, 980f), 25, TextAnchor.UpperLeft, new Color(.45f, 1f, .62f));
        completed.rectTransform.anchorMin = new Vector2(.5f, 1f); completed.rectTransform.anchorMax = new Vector2(.5f, 1f); completed.rectTransform.pivot = new Vector2(.5f, 1f); completed.rectTransform.anchoredPosition = new Vector2(0f, -14f); completed.horizontalOverflow = HorizontalWrapMode.Wrap; completed.verticalOverflow = VerticalWrapMode.Overflow;
        var completedScroll = completedViewport.AddComponent<ScrollRect>(); completedScroll.viewport = completedViewport.GetComponent<RectTransform>(); completedScroll.content = completed.rectTransform; completedScroll.horizontal = false; completedScroll.vertical = true; completedScroll.movementType = ScrollRect.MovementType.Clamped;
        var unmetViewport = CreatePanel(detailViewport.transform, "UnmetViewport", new Vector2(445f, 0f), new Vector2(830f, 520f), Color.clear);
        unmetViewport.GetComponent<Image>().color = Color.clear;
        unmetViewport.AddComponent<RectMask2D>();
        var unmet = CreateText(unmetViewport.transform, "UnmetDetail", string.Empty, Vector2.zero, new Vector2(790f, 980f), 25, TextAnchor.UpperLeft, new Color(1f, .75f, .35f));
        unmet.rectTransform.anchorMin = new Vector2(.5f, 1f); unmet.rectTransform.anchorMax = new Vector2(.5f, 1f); unmet.rectTransform.pivot = new Vector2(.5f, 1f); unmet.rectTransform.anchoredPosition = new Vector2(0f, -14f); unmet.horizontalOverflow = HorizontalWrapMode.Wrap; unmet.verticalOverflow = VerticalWrapMode.Overflow;
        var unmetScroll = unmetViewport.AddComponent<ScrollRect>(); unmetScroll.viewport = unmetViewport.GetComponent<RectTransform>(); unmetScroll.content = unmet.rectTransform; unmetScroll.horizontal = false; unmetScroll.vertical = true; unmetScroll.movementType = ScrollRect.MovementType.Clamped;

        var serialized = new SerializedObject(card);
        serialized.FindProperty("title").objectReferenceValue = title;
        serialized.FindProperty("status").objectReferenceValue = status;
        serialized.FindProperty("completedDetail").objectReferenceValue = completed;
        serialized.FindProperty("unmetDetail").objectReferenceValue = unmet;
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

        var contentRoot = CreatePanel(canvas.transform, "ResultsContent", Vector2.zero, new Vector2(1840f, 700f), new Color(.02f, .04f, .05f, 1f));
        AnchorTopCenter(contentRoot.GetComponent<RectTransform>(), new Vector2(0f, -205f));
        var resultsMask = contentRoot.AddComponent<Mask>();
        resultsMask.showMaskGraphic = true;
        resultsMask.enabled = false;
        var content = CreatePanel(contentRoot.transform, "PanelPages", Vector2.zero, new Vector2(9200f, 650f), new Color(0f, 0f, 0f, 0f));
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, .5f);
        contentRect.anchorMax = new Vector2(0f, .5f);
        contentRect.pivot = new Vector2(0f, .5f);
        contentRect.anchoredPosition = Vector2.zero;
        AddLayoutSamples(cardPrefab, content.transform);

        var noResults = CreatePanel(canvas.transform, "NoResultsState", Vector2.zero, new Vector2(1200f, 220f), new Color(.08f, .12f, .12f, .98f));
        AnchorTopCenter(noResults.GetComponent<RectTransform>(), new Vector2(0f, -360f));
        CreateText(noResults.transform, "Message", "No results available", Vector2.zero, new Vector2(1000f, 80f), 34, TextAnchor.MiddleCenter, Color.white);
        var mainMenu = CreateButton(canvas.transform, "MainMenuButton", "MAIN MENU", Vector2.zero, new Vector2(260f, 56f), new Color(.18f, .55f, .28f));
        AnchorBottomCenter(mainMenu.GetComponent<RectTransform>(), new Vector2(0f, 30f));

        var deviceViewer = CreatePanel(content.transform, "DeviceConfigurationReview", new Vector2(1840f, 650f), new Color(.08f, .12f, .12f, .98f));
        SetPageAlignment(deviceViewer.GetComponent<RectTransform>(), 4);
        var deviceElement = deviceViewer.AddComponent<LayoutElement>();
        deviceElement.minWidth = 1840f;
        deviceElement.preferredWidth = 1840f;
        deviceElement.minHeight = 650f;
        deviceElement.preferredHeight = 650f;
        var deviceTitle = CreateText(deviceViewer.transform, "Title", "SWITCH/ROUTER CONFIGURATION REVIEW", Vector2.zero, new Vector2(1700f, 42f), 28, TextAnchor.MiddleCenter, new Color(.95f, .72f, .28f));
        AnchorTopCenter(deviceTitle.rectTransform, new Vector2(0f, -24f));
        var deviceInfo = CreateText(deviceViewer.transform, "Info", "Informational - does not affect results", Vector2.zero, new Vector2(1700f, 24f), 15, TextAnchor.MiddleCenter, new Color(.7f, .8f, .78f));
        AnchorTopCenter(deviceInfo.rectTransform, new Vector2(0f, -64f));
        var tabsViewport = CreatePanel(deviceViewer.transform, "DeviceTabsViewport", Vector2.zero, new Vector2(1700f, 48f), new Color(.02f, .04f, .05f, 1f));
        var tabsViewportRect = tabsViewport.GetComponent<RectTransform>();
        tabsViewportRect.anchorMin = new Vector2(.5f, 1f);
        tabsViewportRect.anchorMax = new Vector2(.5f, 1f);
        tabsViewportRect.pivot = new Vector2(.5f, 1f);
        tabsViewportRect.anchoredPosition = new Vector2(0f, -116f);
        tabsViewport.AddComponent<Mask>().showMaskGraphic = true;
        var tabs = new GameObject("DeviceTabs", typeof(RectTransform), typeof(HorizontalLayoutGroup)); tabs.transform.SetParent(tabsViewport.transform, false);
        var tabsRect = (RectTransform)tabs.transform; tabsRect.anchorMin = new Vector2(0f, 1f); tabsRect.anchorMax = new Vector2(0f, 1f); tabsRect.pivot = new Vector2(0f, 1f); tabsRect.anchoredPosition = Vector2.zero; tabsRect.sizeDelta = new Vector2(0f, 44f);
        var tabLayout = tabs.GetComponent<HorizontalLayoutGroup>(); tabLayout.spacing = 8f; tabLayout.childForceExpandWidth = false;
        AddPreviewDeviceTab(tabs.transform, "SWITCH - AN-1", true);
        AddPreviewDeviceTab(tabs.transform, "SWITCH - AN-2", false);
        AddPreviewDeviceTab(tabs.transform, "ROUTER - CN-1", false);
        var tabsScroll = tabsViewport.AddComponent<ScrollRect>(); tabsScroll.viewport = tabsViewportRect; tabsScroll.content = tabsRect; tabsScroll.horizontal = true; tabsScroll.vertical = false; tabsScroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = CreatePanel(deviceViewer.transform, "ConfigurationViewport", Vector2.zero, new Vector2(1700f, 470f), new Color(.02f, .04f, .05f, 1f));
        var viewportRect = viewport.GetComponent<RectTransform>(); viewportRect.anchorMin = new Vector2(.5f, 0f); viewportRect.anchorMax = new Vector2(.5f, 0f); viewportRect.pivot = new Vector2(.5f, 0f); viewportRect.anchoredPosition = new Vector2(0f, 8f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;
        var config = CreateText(viewport.transform, "ConfigurationText", PreviewSwitchConfiguration, new Vector2(0f, 0f), new Vector2(1640f, 0f), 15, TextAnchor.UpperLeft, Color.white); config.rectTransform.anchorMin = new Vector2(0f, 1f); config.rectTransform.anchorMax = new Vector2(1f, 1f); config.rectTransform.pivot = new Vector2(.5f, 1f); config.rectTransform.offsetMin = new Vector2(12f, 0f); config.rectTransform.offsetMax = new Vector2(-12f, 0f); config.horizontalOverflow = HorizontalWrapMode.Wrap; config.verticalOverflow = VerticalWrapMode.Overflow; config.supportRichText = true; config.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.AddComponent<ScrollRect>(); scroll.viewport = viewportRect; scroll.content = config.rectTransform; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;

        var panelScroll = contentRoot.AddComponent<ScrollRect>();
        panelScroll.viewport = contentRoot.GetComponent<RectTransform>();
        panelScroll.content = contentRect;
        panelScroll.horizontal = true;
        panelScroll.vertical = false;
        panelScroll.movementType = ScrollRect.MovementType.Clamped;
        panelScroll.inertia = true;
        var previousPanel = CreateButton(contentRoot.transform, "PreviousPanelButton", "‹", new Vector2(18f, -350f), new Vector2(54f, 80f), new Color(.12f, .18f, .2f, .9f));
        AnchorLeftCenter(previousPanel.GetComponent<RectTransform>(), new Vector2(18f, 0f));
        var nextPanel = CreateButton(contentRoot.transform, "NextPanelButton", "›", new Vector2(-18f, -350f), new Vector2(54f, 80f), new Color(.12f, .18f, .2f, .9f));
        AnchorRightCenter(nextPanel.GetComponent<RectTransform>(), new Vector2(-18f, 0f));

        var view = canvas.AddComponent<TssResultsView>();
        var serialized = new SerializedObject(view);
        serialized.FindProperty("resultsContent").objectReferenceValue = contentRoot;
        serialized.FindProperty("noResultsState").objectReferenceValue = noResults;
        serialized.FindProperty("overallSummary").objectReferenceValue = summary;
        serialized.FindProperty("scenarioText").objectReferenceValue = scenario;
        serialized.FindProperty("workPackageContainer").objectReferenceValue = content.transform;
        serialized.FindProperty("resultCardPrefab").objectReferenceValue = cardPrefab;
        serialized.FindProperty("mainMenuButton").objectReferenceValue = mainMenu;
        serialized.FindProperty("resultsMask").objectReferenceValue = resultsMask;
        serialized.FindProperty("deviceConfigurationRoot").objectReferenceValue = deviceViewer;
        serialized.FindProperty("deviceTabContainer").objectReferenceValue = tabs.transform;
        serialized.FindProperty("deviceConfigurationText").objectReferenceValue = config;
        serialized.FindProperty("panelScroll").objectReferenceValue = panelScroll;
        serialized.FindProperty("previousPanelButton").objectReferenceValue = previousPanel;
        serialized.FindProperty("nextPanelButton").objectReferenceValue = nextPanel;
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
        var titles = new[] { "WP1 — LAYER 1 PHYSICAL", "WP1 — LAYER 2 DATA LINK", "WP3 — LAYER 3 NETWORK", "WP4 — WIRELESS" };
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
            var page = CreatePanel(parent, "WorkPackagePage" + (i + 1), new Vector2(1840f, 650f), new Color(.08f, .12f, .12f, .98f));
            SetPageAlignment(page.GetComponent<RectTransform>(), i);
            var pageElement = page.AddComponent<LayoutElement>();
            pageElement.minWidth = 1840f;
            pageElement.preferredWidth = 1840f;
            pageElement.minHeight = 650f;
            pageElement.preferredHeight = 650f;
            var sample = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, page.transform);
            sample.name = titles[i].Replace(' ', '_') + "_Sample";
            var title = sample.transform.Find("Title")?.GetComponent<Text>();
            var status = sample.transform.Find("Status")?.GetComponent<Text>();
            var completed = sample.transform.Find("DetailViewport/CompletedViewport/CompletedDetail")?.GetComponent<Text>();
            var unmet = sample.transform.Find("DetailViewport/UnmetViewport/UnmetDetail")?.GetComponent<Text>();
            if (title) title.text = titles[i];
            if (status)
            {
                status.text = statuses[i];
                status.color = statuses[i] == "PASS" ? new Color(.35f, 1f, .55f) : new Color(1f, .65f, .25f);
            }
            if (completed) completed.text = previewCompleted[i];
            if (unmet) unmet.text = previewUnmet[i];
        }
    }

    private static void AddPreviewDeviceTab(Transform parent, string label, bool active)
    {
        var buttonObject = CreateButton(parent, label, label, Vector2.zero, new Vector2(260f, 42f), active ? new Color(.18f, .55f, .28f) : new Color(.12f, .18f, .2f));
        var text = buttonObject.GetComponentInChildren<Text>();
        if (text) text.fontSize = 17;
    }

    private const string PreviewSwitchConfiguration =
        "===== SHOW RUNNING-CONFIG =====\n" +
        "version 15.0\n!\nhostname AN-1\nservice password-encryption\nno ip domain-lookup\n!\ninterface Fa0/1\n switchport mode trunk\n switchport trunk allowed vlan 10,20,30\n switchport trunk native vlan 99\n!\ninterface Fa0/7\n switchport mode access\n switchport access vlan 20\n shutdown\n!\ninterface Vlan1\n ip address 10.24.10.2 255.255.255.0\n!\nline vty 0 4\n login\n!\nend\n\n===== SHOW VLAN BRIEF =====\nVLAN Name                             Status    Ports\n---- -------------------------------- --------- -------------------------------\n1    default                          active    \n10   Management                       active    \n20   Reception                        active    Fa0/7\n30   Data                             active    \n99   Native                           active    \n999  Dead_Vlan                        suspended\n\n===== SHOW INTERFACES TRUNK =====\nFa0/1 on 802.1q trunking\nNative vlan: 99\nVlans allowed on trunk: 10,20,30\n\n===== SHOW IP INTERFACE BRIEF =====\nInterface                  IP-Address      Status                Protocol\nVlan1                      10.24.10.2      up                    up\nFa0/1                      unassigned      up                    up\nFa0/7                      unassigned      administratively down down\n\n===== SHOW MAC ADDRESS-TABLE =====\nMac Address Table\n(no dynamic entries until endpoints are reachable)\n";

    private static readonly string[] previewCompleted =
    {
        "VERIFIED AT EXIT\n\n[PASS] AN-1 installed at Rack01 U35-U38.\n[PASS] IT1 placed at ITDesk.\n[PASS] Cable terminations recorded.",
        "VERIFIED AT EXIT\n\n[PASS] AN-1 VLAN database is complete.\n[PASS] Required access ports are correct.\n[PASS] Required trunks are correct.\n[PASS] VLAN 1 management interface is correct.",
        "VERIFIED AT EXIT\n\n[PASS] Router parent interface is correct.\n[PASS] Router subinterfaces are correct.\n[PASS] Provider-facing WAN configuration is correct.",
        "VERIFIED AT EXIT\n\n[PASS] AP uplink connected.\n[PASS] Test laptop associated to the guest AP.\n[PASS] Expected DHCP lease received."
    };

    private static readonly string[] previewUnmet =
    {
        "ACTIONABLE REMAINING CRITERIA\n\n[ ] Place TestLaptop at LobbyTable01.",
        "REMAINING CRITERIA\n\nNone.",
        "ACTIONABLE REMAINING CRITERIA\n\n[ ] Save the router configuration.\n[ ] Record provider route evidence.",
        "REMAINING CRITERIA\n\nNone."
    };

    private static void AddToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(x => x.path == ResultsScenePath))
            return;
        scenes.Add(new EditorBuildSettingsScene(ResultsScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetPageAlignment(RectTransform rect, int pageIndex)
    {
        rect.anchorMin = new Vector2(0f, .5f);
        rect.anchorMax = new Vector2(0f, .5f);
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = new Vector2(pageIndex * 1840f, 0f);
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

    private static void AnchorLeftCenter(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
        rect.pivot = new Vector2(0f, .5f);
        rect.anchoredPosition = position;
    }

    private static void AnchorRightCenter(RectTransform rect, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
        rect.pivot = new Vector2(1f, .5f);
        rect.anchoredPosition = position;
    }
}
