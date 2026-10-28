using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TssDeviceSessionPrefabBuilder
{
    private const string UiDir = "Assets/Prefabs/UI";

    private static readonly Color BgDark = new(0.04f, 0.055f, 0.07f, 0.98f);
    private static readonly Color HeaderDark = new(0.07f, 0.09f, 0.12f, 1f);
    private static readonly Color CardDark = new(0.08f, 0.11f, 0.15f, 0.95f);
    private static readonly Color CardInner = new(0.05f, 0.07f, 0.09f, 1f);
    private static readonly Color TextPrimary = new(0.92f, 0.96f, 0.98f, 1f);
    private static readonly Color TextSecondary = new(0.65f, 0.72f, 0.78f, 1f);
    private static readonly Color AccentCyan = new(0.2f, 0.75f, 0.95f, 1f);
    private static readonly Color AccentGreen = new(0.25f, 0.9f, 0.45f, 1f);
    private static readonly Color AccentAmber = new(0.95f, 0.65f, 0.2f, 1f);
    private static readonly Color BtnNormal = new(0.12f, 0.22f, 0.28f, 0.95f);
    private static readonly Color BtnDanger = new(0.45f, 0.15f, 0.15f, 0.95f);
    private static readonly Color InputBg = new(0.03f, 0.04f, 0.05f, 1f);

    [MenuItem("TSS/UI/Build Device Session Canvases and Controls")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(UiDir))
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

        var terminalControlsPrefab = BuildFirstPersonControlsPrefab("TssTerminalFirstPersonControls", true);
        var desktopControlsPrefab = BuildFirstPersonControlsPrefab("TssDesktopFirstPersonControls", false);
        var laptopControlsPrefab = BuildFirstPersonControlsPrefab("TssLaptopFirstPersonControls", false);
        var terminalCanvasPrefab = BuildTerminalSessionCanvasPrefab();
        var desktopCanvasPrefab = BuildDesktopSessionCanvasPrefab("TssDesktopSessionCanvas");
        var laptopCanvasPrefab = BuildDesktopSessionCanvasPrefab("TssLaptopSessionCanvas");

        AssignControlsToDevicePrefabs(terminalControlsPrefab, desktopControlsPrefab);
        AssignCanvasPrefab("Assets/Laptop/prefab/Laptop 1.prefab", laptopControlsPrefab);
        InstallCanvasesIntoTssScene(terminalCanvasPrefab, desktopCanvasPrefab, laptopCanvasPrefab, terminalControlsPrefab, desktopControlsPrefab, laptopControlsPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Successfully built device session canvases and first-person view controls!");
    }

    private static GameObject BuildFirstPersonControlsPrefab(string name, bool isTerminal)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TssDeviceFirstPersonControls), typeof(VerticalLayoutGroup));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 450;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        scaler.matchWidthOrHeight = 0.5f;

        var rootVlg = root.GetComponent<VerticalLayoutGroup>();
        rootVlg.childAlignment = TextAnchor.LowerCenter;
        rootVlg.childControlWidth = false;
        rootVlg.childControlHeight = false;
        rootVlg.childForceExpandWidth = true;
        rootVlg.childForceExpandHeight = true;

        var displayAndControls = new GameObject("Display and Controls", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        displayAndControls.transform.SetParent(root.transform, false);
        var dcRt = displayAndControls.GetComponent<RectTransform>();
        dcRt.sizeDelta = new Vector2(1280f, 100f);
        var dcHlg = displayAndControls.GetComponent<HorizontalLayoutGroup>();
        dcHlg.childAlignment = TextAnchor.MiddleCenter;
        dcHlg.childControlWidth = false;
        dcHlg.childControlHeight = false;
        dcHlg.childForceExpandWidth = true;
        dcHlg.childForceExpandHeight = true;

        var titleGroup = new GameObject("Title", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        titleGroup.transform.SetParent(displayAndControls.transform, false);
        var tgRt = titleGroup.GetComponent<RectTransform>();
        tgRt.sizeDelta = new Vector2(200f, 100f);
        var tgHlg = titleGroup.GetComponent<HorizontalLayoutGroup>();
        tgHlg.childAlignment = TextAnchor.MiddleCenter;
        tgHlg.childControlWidth = false;
        tgHlg.childControlHeight = false;
        tgHlg.childForceExpandWidth = true;
        tgHlg.childForceExpandHeight = true;

        var titleGo = MakeText(titleGroup.transform, "DeviceTitle", "Device Inspection", 13, TextAnchor.MiddleCenter);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.sizeDelta = new Vector2(200f, 26f);
        titleGo.GetComponent<Text>().color = AccentCyan;

        var buttonsGroup = new GameObject("Buttons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonsGroup.transform.SetParent(displayAndControls.transform, false);
        var bgRt = buttonsGroup.GetComponent<RectTransform>();
        bgRt.sizeDelta = new Vector2(600f, 100f);
        var bgHlg = buttonsGroup.GetComponent<HorizontalLayoutGroup>();
        bgHlg.childAlignment = TextAnchor.MiddleCenter;
        bgHlg.spacing = 20f;
        bgHlg.childControlWidth = false;
        bgHlg.childControlHeight = false;
        bgHlg.childForceExpandWidth = true;
        bgHlg.childForceExpandHeight = true;

        // ponytail: a transparent Image is enough to consume empty-row raycasts.
        if (isTerminal)
        {
            var rowRaycastBlocker = new GameObject("ButtonRowRaycastBlocker", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowRaycastBlocker.transform.SetParent(buttonsGroup.transform, false);
            rowRaycastBlocker.transform.SetAsFirstSibling();
            var blockerRt = rowRaycastBlocker.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            var blockerImage = rowRaycastBlocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.001f);
            blockerImage.raycastTarget = true;
            rowRaycastBlocker.GetComponent<LayoutElement>().ignoreLayout = true;
        }

        var returnBtn = MakeButton(buttonsGroup.transform, "ReturnButton", "Return", Vector2.zero, new Vector2(150f, 44f), BtnNormal);
        var sessionLabel = isTerminal ? "Terminal Session" : "Desktop Session";
        var sessionBtn = MakeButton(buttonsGroup.transform, "SessionButton", sessionLabel, Vector2.zero, new Vector2(150f, 44f), isTerminal ? new Color(0.12f, 0.32f, 0.28f, 0.95f) : new Color(0.12f, 0.26f, 0.42f, 0.95f));
        var renameBtn = MakeButton(buttonsGroup.transform, "RenameButton", "Rename", Vector2.zero, new Vector2(150f, 44f), BtnNormal);
        var pickupBtn = MakeButton(buttonsGroup.transform, "PickupButton", "Pick Up", Vector2.zero, new Vector2(150f, 44f), new Color(0.35f, 0.15f, 0.15f, 0.95f));

        var controls = root.GetComponent<TssDeviceFirstPersonControls>();
        var ser = new SerializedObject(controls);
        ser.FindProperty("returnButton").objectReferenceValue = returnBtn;
        ser.FindProperty("sessionButton").objectReferenceValue = sessionBtn;
        ser.FindProperty("renameButton").objectReferenceValue = renameBtn;
        ser.FindProperty("pickupButton").objectReferenceValue = pickupBtn;
        ser.FindProperty("sessionButtonText").objectReferenceValue = sessionBtn.GetComponentInChildren<Text>();
        ser.FindProperty("deviceTitleText").objectReferenceValue = titleGo.GetComponent<Text>();
        ser.FindProperty("isTerminalSession").boolValue = isTerminal;
        ser.ApplyModifiedPropertiesWithoutUndo();

        var path = $"{UiDir}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildTerminalSessionCanvasPrefab()
    {
        var root = new GameObject("TssTerminalSessionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TssTerminalSessionPresenter));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        scaler.matchWidthOrHeight = 0.5f;

        // Dimmed backdrop
        var backdrop = MakeImage(root.transform, "Backdrop", new Color(0f, 0f, 0f, 0.7f));
        Stretch(backdrop.rectTransform, 0f, 0f, 1f, 1f);

        // Window root (margins 30px)
        var win = MakeImage(root.transform, "TerminalWindow", BgDark);
        Stretch(win.rectTransform, 0f, 0f, 1f, 1f, 30f, 24f, -30f, -24f);

        // Header (50px height)
        var header = MakeImage(win.transform, "Header", HeaderDark);
        AnchorTop(header.rectTransform, 50f);

        var title = MakeText(header.transform, "TitleText", "Cisco Switch - Terminal Console", 17, TextAnchor.MiddleLeft);
        title.rectTransform.anchorMin = new Vector2(0f, 0f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.offsetMin = new Vector2(16f, 0f);
        title.rectTransform.offsetMax = Vector2.zero;
        title.color = TextPrimary;

        var badge = MakeText(header.transform, "CategoryBadge", "SWITCH (LAYER 2)", 13, TextAnchor.MiddleCenter);
        badge.rectTransform.anchorMin = new Vector2(0.5f, 0.15f);
        badge.rectTransform.anchorMax = new Vector2(0.68f, 0.85f);
        badge.rectTransform.offsetMin = badge.rectTransform.offsetMax = Vector2.zero;
        badge.color = AccentCyan;

        var link = MakeText(header.transform, "LinkStatusText", "LINK: UP (2 active)", 13, TextAnchor.MiddleCenter);
        link.rectTransform.anchorMin = new Vector2(0.69f, 0.15f);
        link.rectTransform.anchorMax = new Vector2(0.85f, 0.85f);
        link.rectTransform.offsetMin = link.rectTransform.offsetMax = Vector2.zero;
        link.color = AccentGreen;

        var closeBtn = MakeButton(header.transform, "CloseButton", "Exit Terminal (Esc)", new Vector2(-16f, 0f), new Vector2(160f, 34f), BtnDanger);
        var closeRt = closeBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 0.5f);
        closeRt.anchorMax = new Vector2(1f, 0.5f);
        closeRt.pivot = new Vector2(1f, 0.5f);

        // Subheader
        var subheader = MakeImage(win.transform, "Subheader", CardDark);
        subheader.rectTransform.anchorMin = new Vector2(0f, 1f);
        subheader.rectTransform.anchorMax = new Vector2(1f, 1f);
        subheader.rectTransform.pivot = new Vector2(0.5f, 1f);
        subheader.rectTransform.anchoredPosition = new Vector2(0f, -50f);
        subheader.rectTransform.sizeDelta = new Vector2(0f, 32f);

        var subText = MakeText(subheader.transform, "SubheaderText", "Hostname: Switch01   Mgmt IP: 192.168.1.2   MAC: 00:50:79:01:02:03   VLAN 1", 12, TextAnchor.MiddleLeft);
        subText.rectTransform.anchorMin = Vector2.zero;
        subText.rectTransform.anchorMax = Vector2.one;
        subText.rectTransform.offsetMin = new Vector2(16f, 0f);
        subText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        subText.color = TextSecondary;

        // Content Area
        var content = new GameObject("ContentArea", typeof(RectTransform));
        content.transform.SetParent(win.transform, false);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = Vector2.zero;
        contentRt.anchorMax = Vector2.one;
        contentRt.offsetMin = new Vector2(14f, 14f);
        contentRt.offsetMax = new Vector2(-14f, -90f);

        // Left: Console Panel (leave a narrow syntax-only reference at right)
        var consolePanel = MakeImage(content.transform, "ConsolePanel", InputBg);
        var cpRt = consolePanel.rectTransform;
        cpRt.anchorMin = Vector2.zero;
        cpRt.anchorMax = Vector2.one;
        cpRt.offsetMin = Vector2.zero;
        cpRt.offsetMax = new Vector2(-270f, 0f);

        // Console ScrollView
        var svGo = new GameObject("ConsoleScrollView", typeof(RectTransform), typeof(ScrollRect));
        svGo.transform.SetParent(consolePanel.transform, false);
        var svRt = svGo.GetComponent<RectTransform>();
        svRt.anchorMin = Vector2.zero;
        svRt.anchorMax = Vector2.one;
        svRt.offsetMin = new Vector2(10f, 10f);
        svRt.offsetMax = new Vector2(-10f, -10f);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(svGo.transform, false);
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;

        var scrollContent = new GameObject("ScrollContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        scrollContent.transform.SetParent(viewport.transform, false);
        var scRt = scrollContent.GetComponent<RectTransform>();
        scRt.anchorMin = new Vector2(0f, 1f);
        scRt.anchorMax = new Vector2(1f, 1f);
        scRt.pivot = new Vector2(0.5f, 1f);
        scRt.offsetMin = scRt.offsetMax = Vector2.zero;

        var scVlg = scrollContent.GetComponent<VerticalLayoutGroup>();
        scVlg.childControlWidth = true;
        scVlg.childControlHeight = true;
        scVlg.childForceExpandWidth = true;
        scVlg.childForceExpandHeight = false;
        scVlg.spacing = 2f;
        scVlg.padding = new RectOffset(6, 6, 6, 6);

        var csf = scrollContent.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Child 1: ConsoleOutputText defaults to Cisco Switch bootup text
        var consoleText = MakeText(scrollContent.transform, "ConsoleOutputText", TssTerminalSessionPresenter.GetDefaultSwitchBootBanner() + EditorPreviewConsole, 13, TextAnchor.UpperLeft);
        consoleText.color = AccentGreen;
        consoleText.lineSpacing = 1.15f;
        consoleText.supportRichText = true;
        var ctCsf = consoleText.gameObject.AddComponent<ContentSizeFitter>();
        ctCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ctCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // Child 2: Inline InputBar right beneath ConsoleOutputText
        var inputBar = new GameObject("InputBar", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        inputBar.transform.SetParent(scrollContent.transform, false);
        var ibRt = inputBar.GetComponent<RectTransform>();
        ibRt.anchorMin = new Vector2(0f, 1f);
        ibRt.anchorMax = new Vector2(1f, 1f);
        ibRt.pivot = new Vector2(0.5f, 1f);

        var ibLe = inputBar.GetComponent<LayoutElement>();
        ibLe.preferredHeight = 24f;
        ibLe.minHeight = 24f;
        ibLe.flexibleHeight = 0f;

        var ibHlg = inputBar.GetComponent<HorizontalLayoutGroup>();
        ibHlg.childControlWidth = true;
        ibHlg.childControlHeight = true;
        ibHlg.childForceExpandWidth = false;
        ibHlg.childForceExpandHeight = true;
        ibHlg.spacing = 2f;
        ibHlg.padding = new RectOffset(0, 0, 0, 0);

        var prompt = MakeText(inputBar.transform, "PromptText", "Switch> ", 13, TextAnchor.MiddleLeft);
        prompt.color = AccentGreen;
        prompt.raycastTarget = false;

        var cmdInput = MakeInputField(inputBar.transform, "CommandInput", "");
        var ciLe = cmdInput.gameObject.AddComponent<LayoutElement>();
        ciLe.flexibleWidth = 1f;
        ciLe.minWidth = 100f;
        cmdInput.caretColor = AccentGreen;
        cmdInput.customCaretColor = true;
        cmdInput.textComponent.color = AccentGreen;
        cmdInput.textComponent.fontSize = 13;
        cmdInput.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var scrollRect = svGo.GetComponent<ScrollRect>();
        scrollRect.viewport = vpRt;
        scrollRect.content = scRt;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // Right Sidebar Panel
        var sidebar = MakeImage(content.transform, "SidebarPanel", CardDark);
        var sbRt = sidebar.rectTransform;
        sbRt.anchorMin = new Vector2(1f, 0f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.pivot = new Vector2(1f, 0.5f);
        sbRt.anchoredPosition = Vector2.zero;
        sbRt.sizeDelta = new Vector2(260f, 0f);

        var sbTitle = MakeText(sidebar.transform, "SidebarTitle", "Switch Command Reference", 13, TextAnchor.MiddleLeft);
        AnchorTop(sbTitle.rectTransform, 32f);
        sbTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        sbTitle.color = AccentAmber;

        // Static syntax reference. It intentionally contains no live topology/configuration values.
        var ifaceSv = new GameObject("CommandReference", typeof(RectTransform), typeof(ScrollRect));
        ifaceSv.transform.SetParent(sidebar.transform, false);
        var isvRt = ifaceSv.GetComponent<RectTransform>();
        isvRt.anchorMin = new Vector2(0f, 0.44f);
        isvRt.anchorMax = new Vector2(1f, 1f);
        isvRt.offsetMin = new Vector2(8f, 0f);
        isvRt.offsetMax = new Vector2(-8f, -34f);

        var ifaceVp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        ifaceVp.transform.SetParent(ifaceSv.transform, false);
        var ivpRt = ifaceVp.GetComponent<RectTransform>();
        ivpRt.anchorMin = Vector2.zero;
        ivpRt.anchorMax = Vector2.one;
        ivpRt.offsetMin = ivpRt.offsetMax = Vector2.zero;

        var ifaceList = new GameObject("ReferenceText", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        ifaceList.transform.SetParent(ifaceVp.transform, false);
        var ilRt = ifaceList.GetComponent<RectTransform>();
        ilRt.anchorMin = new Vector2(0f, 1f);
        ilRt.anchorMax = new Vector2(1f, 1f);
        ilRt.pivot = new Vector2(0.5f, 1f);
        ilRt.offsetMin = ilRt.offsetMax = Vector2.zero;

        var vlg = ifaceList.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 4f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        var ifaceCsf = ifaceList.GetComponent<ContentSizeFitter>();
        ifaceCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var ifaceScroll = ifaceSv.GetComponent<ScrollRect>();
        ifaceScroll.viewport = ivpRt;
        ifaceScroll.content = ilRt;
        ifaceScroll.horizontal = false;
        ifaceScroll.vertical = true;

        var reference = MakeText(ifaceList.transform, "Syntax", "<b>EXEC</b>\nenable\nshow running-config\nshow vlan brief\nshow interfaces trunk\nshow ip route\nping &lt;address&gt;\n\n<b>CONFIG</b>\nconf t\nhostname &lt;name&gt;\nvlan &lt;id&gt;\ninterface &lt;name&gt;\nline console 0\ncopy run start", 11, TextAnchor.UpperLeft);
        reference.color = TextSecondary;
        reference.supportRichText = true;
        reference.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sbStatus = MakeText(sidebar.transform, "SidebarStatusText", "Type ? for mode-aware syntax.\nConfiguration is visible only through typed show commands.", 11, TextAnchor.MiddleLeft);
        var sbStRt = sbStatus.rectTransform;
        sbStRt.anchorMin = Vector2.zero;
        sbStRt.anchorMax = new Vector2(1f, 0.12f);
        sbStRt.offsetMin = new Vector2(10f, 4f);
        sbStRt.offsetMax = new Vector2(-10f, -4f);
        sbStatus.color = TextSecondary;

        // Wire Presenter
        var pres = root.GetComponent<TssTerminalSessionPresenter>();
        var pSer = new SerializedObject(pres);
        pSer.FindProperty("titleText").objectReferenceValue = title;
        pSer.FindProperty("subheaderText").objectReferenceValue = subText;
        pSer.FindProperty("categoryBadgeText").objectReferenceValue = badge;
        pSer.FindProperty("linkStatusText").objectReferenceValue = link;
        pSer.FindProperty("closeButton").objectReferenceValue = closeBtn;
        pSer.FindProperty("consoleScrollRect").objectReferenceValue = scrollRect;
        pSer.FindProperty("scrollContent").objectReferenceValue = scRt;
        pSer.FindProperty("consoleOutputText").objectReferenceValue = consoleText;
        pSer.FindProperty("inputBar").objectReferenceValue = ibRt;
        pSer.FindProperty("promptText").objectReferenceValue = prompt;
        pSer.FindProperty("commandInput").objectReferenceValue = cmdInput;
        pSer.FindProperty("sendCommandButton").objectReferenceValue = null;
        pSer.FindProperty("btnShowIpIntBrief").objectReferenceValue = null;
        pSer.FindProperty("btnShowInterfaces").objectReferenceValue = null;
        pSer.FindProperty("btnShowMacTable").objectReferenceValue = null;
        pSer.FindProperty("btnShowVlan").objectReferenceValue = null;
        pSer.FindProperty("btnShowIpRoute").objectReferenceValue = null;
        pSer.FindProperty("btnPing").objectReferenceValue = null;
        pSer.FindProperty("btnClear").objectReferenceValue = null;
        pSer.FindProperty("interfaceListContainer").objectReferenceValue = ifaceList.transform;
        pSer.FindProperty("sidebarStatusText").objectReferenceValue = sbStatus;
        pSer.ApplyModifiedPropertiesWithoutUndo();

        var path = $"{UiDir}/TssTerminalSessionCanvas.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private const string EditorPreviewConsole =
        "\nAN-1#show running-config\n" +
        "version 15.0\nhostname AN-1\nservice password-encryption\nno ip domain-lookup\n!\ninterface Fa0/1\n switchport mode trunk\n switchport trunk allowed vlan 10,20,30\n switchport trunk native vlan 99\n!\ninterface Vlan1\n ip address 10.24.10.2 255.255.255.0\n!\nend\n" +
        "\nAN-1#show vlan brief\n" +
        "10 Management active\n20 Reception active\n30 Data active\n99 Native active\n999 Dead_Vlan suspended\n" +
        "\nCN-1#show ip interface brief\n" +
        "Interface                  IP-Address      Status                Protocol\n" +
        "Fa0/0                     unassigned      up                    up\n" +
        "Fa0/0.10                  10.24.10.1      up                    up\n" +
        "Fa0/0.20                  10.24.20.1      up                    up\n" +
        "Fa0/0.30                  10.24.30.1      up                    up\n" +
        "Fa0/1                     203.0.113.2     up                    up\n" +
        "\nCN-1#show route\n" +
        "Codes: C - connected, S - static\n" +
        "C    10.24.10.0/24 is directly connected, Fa0/0.10\n" +
        "C    10.24.20.0/24 is directly connected, Fa0/0.20\n" +
        "C    10.24.30.0/24 is directly connected, Fa0/0.30\n" +
        "S*   0.0.0.0/0 [1/0] via 203.0.113.1\n";

    private static GameObject BuildDesktopSessionCanvasPrefab(string canvasName)
    {
        var root = new GameObject(canvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TssDesktopSessionPresenter));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        scaler.matchWidthOrHeight = 0.5f;

        // Desktop Wallpaper / BG
        var bg = MakeImage(root.transform, "DesktopBackground", new Color(0.08f, 0.11f, 0.16f, 1f));
        Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);

        // OS Top Taskbar
        var taskbar = MakeImage(root.transform, "TopTaskbar", HeaderDark);
        AnchorTop(taskbar.rectTransform, 44f);

        var topTitle = MakeText(taskbar.transform, "TopBarTitleText", "Workstation OS Enterprise", 15, TextAnchor.MiddleLeft);
        topTitle.rectTransform.anchorMin = new Vector2(0f, 0f);
        topTitle.rectTransform.anchorMax = new Vector2(0.28f, 1f);
        topTitle.rectTransform.offsetMin = new Vector2(16f, 0f);
        topTitle.rectTransform.offsetMax = Vector2.zero;
        topTitle.color = TextPrimary;

        var topHost = MakeText(taskbar.transform, "TopBarHostnameText", "WS01 [DesktopPc]", 14, TextAnchor.MiddleCenter);
        topHost.rectTransform.anchorMin = new Vector2(0.28f, 0f);
        topHost.rectTransform.anchorMax = new Vector2(0.52f, 1f);
        topHost.rectTransform.offsetMin = topHost.rectTransform.offsetMax = Vector2.zero;
        topHost.color = AccentCyan;

        var topLink = MakeText(taskbar.transform, "TopBarLinkStatusText", "Connected (1 Gbps)", 13, TextAnchor.MiddleCenter);
        topLink.rectTransform.anchorMin = new Vector2(0.53f, 0f);
        topLink.rectTransform.anchorMax = new Vector2(0.72f, 1f);
        topLink.rectTransform.offsetMin = topLink.rectTransform.offsetMax = Vector2.zero;
        topLink.color = AccentGreen;

        var topTime = MakeText(taskbar.transform, "TopBarTimeText", "12:00:00", 13, TextAnchor.MiddleCenter);
        topTime.rectTransform.anchorMin = new Vector2(0.73f, 0f);
        topTime.rectTransform.anchorMax = new Vector2(0.84f, 1f);
        topTime.rectTransform.offsetMin = topTime.rectTransform.offsetMax = Vector2.zero;
        topTime.color = TextSecondary;

        var discBtn = MakeButton(taskbar.transform, "DisconnectButton", "Log Off / Disconnect", new Vector2(-12f, 0f), new Vector2(170f, 32f), BtnDanger);
        var discRt = discBtn.GetComponent<RectTransform>();
        discRt.anchorMin = new Vector2(1f, 0.5f);
        discRt.anchorMax = new Vector2(1f, 0.5f);
        discRt.pivot = new Vector2(1f, 0.5f);

        // Main Desktop Area
        var desktopArea = new GameObject("DesktopArea", typeof(RectTransform));
        desktopArea.transform.SetParent(root.transform, false);
        var daRt = desktopArea.GetComponent<RectTransform>();
        daRt.anchorMin = Vector2.zero;
        daRt.anchorMax = Vector2.one;
        daRt.offsetMin = new Vector2(24f, 20f);
        daRt.offsetMax = new Vector2(-24f, -60f);

        // Left Card: Network Configuration (Layer 2 & Layer 3)
        var netCard = MakeImage(desktopArea.transform, "NetworkConfigCard", CardDark);
        var ncRt = netCard.rectTransform;
        ncRt.anchorMin = new Vector2(0f, 0f);
        ncRt.anchorMax = new Vector2(0.48f, 1f);
        ncRt.offsetMin = ncRt.offsetMax = Vector2.zero;

        var ncHeader = MakeImage(netCard.transform, "CardHeader", CardInner);
        AnchorTop(ncHeader.rectTransform, 36f);
        var ncTitle = MakeText(ncHeader.transform, "Title", "ETHERNET NETWORK ADAPTER (L2 / L3)", 13, TextAnchor.MiddleLeft);
        ncTitle.rectTransform.anchorMin = Vector2.zero;
        ncTitle.rectTransform.anchorMax = Vector2.one;
        ncTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        ncTitle.color = AccentCyan;

        // Content in Network Config Card
        var ncBody = new GameObject("CardBody", typeof(RectTransform));
        ncBody.transform.SetParent(netCard.transform, false);
        var ncbRt = ncBody.GetComponent<RectTransform>();
        ncbRt.anchorMin = Vector2.zero;
        ncbRt.anchorMax = Vector2.one;
        ncbRt.offsetMin = new Vector2(14f, 14f);
        ncbRt.offsetMax = new Vector2(-14f, -44f);

        var netAdapterName = MakeText(ncBody.transform, "NetAdapterName", "Ethernet Adapter (Eth01)", 13, TextAnchor.MiddleLeft);
        PlaceElement(netAdapterName.rectTransform, 0f, 24f);
        netAdapterName.color = TextPrimary;

        var physLinkStatus = MakeText(ncBody.transform, "PhysicalLinkStatus", "Status: Connected / Physical Link Active", 12, TextAnchor.MiddleLeft);
        PlaceElement(physLinkStatus.rectTransform, -26f, 22f);
        physLinkStatus.color = AccentGreen;

        var peerDev = MakeText(ncBody.transform, "PeerDevice", "Connected To: Switch Fa0/1", 12, TextAnchor.MiddleLeft);
        PlaceElement(peerDev.rectTransform, -50f, 22f);
        peerDev.color = TextSecondary;

        var macAddr = MakeText(ncBody.transform, "MacAddress", "Physical MAC Address: 00:50:79:01:02:03", 12, TextAnchor.MiddleLeft);
        PlaceElement(macAddr.rectTransform, -74f, 22f);
        macAddr.color = TextSecondary;

        // Toggle row (DHCP / Static)
        var modeRow = new GameObject("ModeRow", typeof(RectTransform));
        modeRow.transform.SetParent(ncBody.transform, false);
        PlaceElement(modeRow.GetComponent<RectTransform>(), -106f, 32f);

        var dhcpTog = MakeToggle(modeRow.transform, "DhcpToggle", "DHCP (Auto)");
        var staticTog = MakeToggle(modeRow.transform, "StaticToggle", "Static IP");
        dhcpTog.GetComponent<RectTransform>().anchoredPosition = new Vector2(10f, 0f);
        staticTog.GetComponent<RectTransform>().anchoredPosition = new Vector2(160f, 0f);

        // Input Fields (IP, Subnet, Gateway, DNS)
        var ipIn = MakeLabeledInputField(ncBody.transform, "IpInput", "IPv4 Address:", "192.168.1.101", -150f);
        var maskIn = MakeLabeledInputField(ncBody.transform, "MaskInput", "Subnet Mask:", "255.255.255.0", -196f);
        var gwIn = MakeLabeledInputField(ncBody.transform, "GatewayInput", "Default Gateway:", "192.168.1.1", -242f);
        var dnsIn = MakeLabeledInputField(ncBody.transform, "DnsInput", "DNS Server:", "8.8.8.8", -288f);

        var applyBtn = MakeButton(ncBody.transform, "ApplyButton", "Save / Apply Settings", new Vector2(0f, -344f), new Vector2(190f, 38f), BtnNormal);
        var checkServerBtn = MakeButton(ncBody.transform, "CheckServerConnectionButton", "Verify Server", new Vector2(200f, -344f), new Vector2(170f, 38f), BtnNormal);
        var applyFeedback = MakeText(ncBody.transform, "ApplyFeedback", "", 12, TextAnchor.MiddleLeft);
        PlaceElement(applyFeedback.rectTransform, -390f, 24f);

        // Right Upper Card: Device Specific (PC / Server / Printer)
        var rightTop = MakeImage(desktopArea.transform, "DeviceSpecificCard", CardDark);
        var rtRt = rightTop.rectTransform;
        rtRt.anchorMin = new Vector2(0.51f, 0.44f);
        rtRt.anchorMax = new Vector2(1f, 1f);
        rtRt.offsetMin = rtRt.offsetMax = Vector2.zero;

        // 1. PC Tools
        var pcCard = new GameObject("PcToolsCard", typeof(RectTransform));
        pcCard.transform.SetParent(rightTop.transform, false);
        Stretch(pcCard.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);

        var pcHeader = MakeImage(pcCard.transform, "Header", CardInner);
        AnchorTop(pcHeader.rectTransform, 36f);
        var pcTitle = MakeText(pcHeader.transform, "Title", "NETWORK DIAGNOSTICS & PING", 13, TextAnchor.MiddleLeft);
        pcTitle.rectTransform.anchorMin = Vector2.zero;
        pcTitle.rectTransform.anchorMax = Vector2.one;
        pcTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        pcTitle.color = AccentAmber;

        var openApBrowserBtn = MakeButton(pcHeader.transform, "OpenAccessPointBrowserButton", "AP Browser", Vector2.zero, new Vector2(92f, 26f), BtnNormal);
        var apButtonRt = openApBrowserBtn.GetComponent<RectTransform>();
        apButtonRt.anchorMin = apButtonRt.anchorMax = new Vector2(1f, 0.5f);
        apButtonRt.pivot = new Vector2(1f, 0.5f);
        apButtonRt.anchoredPosition = new Vector2(-10f, 0f);

        var commandInput = MakeInputField(pcCard.transform, "CommandInput", "Enter command: ipconfig, ping <address>, tracert <address>, cls");
        var ciRt = commandInput.GetComponent<RectTransform>();
        ciRt.anchorMin = new Vector2(0f, 1f);
        ciRt.anchorMax = new Vector2(0.68f, 1f);
        ciRt.anchoredPosition = new Vector2(12f, -54f);
        ciRt.sizeDelta = new Vector2(0f, 32f);

        var submitBtn = MakeButton(pcCard.transform, "SubmitCommandButton", "Enter", Vector2.zero, new Vector2(70f, 32f), BtnNormal);
        PositionAnchor(submitBtn.GetComponent<RectTransform>(), 0.70f, 1f, new Vector2(0f, -54f), new Vector2(70f, 32f));
        var historyUpBtn = MakeButton(pcCard.transform, "HistoryUpButton", "Up", Vector2.zero, new Vector2(50f, 32f), BtnNormal);
        PositionAnchor(historyUpBtn.GetComponent<RectTransform>(), 0.81f, 1f, new Vector2(0f, -54f), new Vector2(50f, 32f));
        var historyDownBtn = MakeButton(pcCard.transform, "HistoryDownButton", "Down", Vector2.zero, new Vector2(58f, 32f), BtnNormal);
        PositionAnchor(historyDownBtn.GetComponent<RectTransform>(), 0.90f, 1f, new Vector2(0f, -54f), new Vector2(58f, 32f));

        var outputViewport = MakeImage(pcCard.transform, "CommandOutputViewport", InputBg);
        Stretch(outputViewport.rectTransform, 0f, 0f, 1f, 1f, 12f, 12f, -12f, -96f);
        var pcDiagText = MakeText(outputViewport.transform, "CommandOutputText", "Endpoint command prompt ready.\n", 11, TextAnchor.UpperLeft);
        var pcdRt = pcDiagText.rectTransform;
        pcdRt.anchorMin = new Vector2(0f, 1f);
        pcdRt.anchorMax = new Vector2(1f, 1f);
        pcdRt.pivot = new Vector2(0.5f, 1f);
        pcdRt.offsetMin = new Vector2(8f, -2000f);
        pcdRt.offsetMax = new Vector2(-8f, -8f);
        pcDiagText.color = TextSecondary;
        var outputScroll = pcCard.AddComponent<ScrollRect>();
        outputScroll.viewport = outputViewport.rectTransform;
        outputScroll.content = pcDiagText.rectTransform;
        outputScroll.horizontal = false;
        outputScroll.vertical = true;
        outputScroll.movementType = ScrollRect.MovementType.Clamped;

        // Browser-only AP configuration page. It stays within the endpoint session so the
        // browser cannot bypass the same L1/L2/L3 state used by diagnostics.
        var apBrowserCard = new GameObject("AccessPointBrowserCard", typeof(RectTransform), typeof(Image));
        apBrowserCard.transform.SetParent(rightTop.transform, false);
        Stretch(apBrowserCard.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        apBrowserCard.GetComponent<Image>().color = CardDark;
        apBrowserCard.SetActive(false);

        var apHeader = MakeImage(apBrowserCard.transform, "Header", CardInner);
        AnchorTop(apHeader.rectTransform, 36f);
        var apTitle = MakeText(apHeader.transform, "Title", "ACCESS POINT MANAGEMENT", 13, TextAnchor.MiddleLeft);
        apTitle.rectTransform.anchorMin = Vector2.zero;
        apTitle.rectTransform.anchorMax = Vector2.one;
        apTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        apTitle.color = AccentAmber;
        var closeApBrowserBtn = MakeButton(apHeader.transform, "CloseButton", "Back", Vector2.zero, new Vector2(60f, 26f), BtnNormal);
        var closeApRt = closeApBrowserBtn.GetComponent<RectTransform>();
        closeApRt.anchorMin = closeApRt.anchorMax = new Vector2(1f, 0.5f);
        closeApRt.pivot = new Vector2(1f, 0.5f);
        closeApRt.anchoredPosition = new Vector2(-10f, 0f);

        var apStatus = MakeText(apBrowserCard.transform, "BrowserStatusText", "https://AP-1 | Waiting for connection", 11, TextAnchor.MiddleLeft);
        PlaceElement(apStatus.rectTransform, -56f, 22f);
        apStatus.color = TextSecondary;
        var apSsid = MakeLabeledInputField(apBrowserCard.transform, "SsidInput", "Guest SSID:", "Enter SOP SSID", -98f);
        var apAdmin = MakeLabeledInputField(apBrowserCard.transform, "AdminPasswordInput", "Admin Password:", "Enter SOP administrator password", -144f);
        var apPassphrase = MakeLabeledInputField(apBrowserCard.transform, "PassphraseInput", "WPA2 Passphrase:", "Enter SOP passphrase", -190f);
        var apChannel = MakeLabeledInputField(apBrowserCard.transform, "ChannelInput", "Channel:", "1, 6, or 11", -236f);
        var apWps = MakeToggle(apBrowserCard.transform, "DisableWpsToggle", "Disable WPS");
        apWps.GetComponent<RectTransform>().anchorMin = apWps.GetComponent<RectTransform>().anchorMax = new Vector2(0f, 1f);
        apWps.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
        apWps.GetComponent<RectTransform>().anchoredPosition = new Vector2(14f, -284f);
        var saveApBtn = MakeButton(apBrowserCard.transform, "SaveAccessPointButton", "Save AP Settings", new Vector2(14f, 14f), new Vector2(160f, 34f), BtnNormal);
        var saveApRt = saveApBtn.GetComponent<RectTransform>();
        saveApRt.anchorMin = saveApRt.anchorMax = saveApRt.pivot = Vector2.zero;
        saveApRt.anchoredPosition = new Vector2(14f, 14f);
        var apFeedback = MakeText(apBrowserCard.transform, "BrowserFeedbackText", "", 11, TextAnchor.UpperLeft);
        var apFeedbackRt = apFeedback.rectTransform;
        apFeedbackRt.anchorMin = new Vector2(0f, 0f);
        apFeedbackRt.anchorMax = new Vector2(1f, 0f);
        apFeedbackRt.offsetMin = new Vector2(184f, 8f);
        apFeedbackRt.offsetMax = new Vector2(-12f, 46f);
        apFeedback.color = TextSecondary;

        // 2. Server Roles Card
        var srvCard = new GameObject("ServerRolesCard", typeof(RectTransform));
        srvCard.transform.SetParent(rightTop.transform, false);
        Stretch(srvCard.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        srvCard.SetActive(false);

        var srvHeader = MakeImage(srvCard.transform, "Header", CardInner);
        AnchorTop(srvHeader.rectTransform, 36f);
        var srvTitle = MakeText(srvHeader.transform, "Title", "SERVER ROLES & SERVICES", 13, TextAnchor.MiddleLeft);
        srvTitle.rectTransform.anchorMin = Vector2.zero;
        srvTitle.rectTransform.anchorMax = Vector2.one;
        srvTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        srvTitle.color = AccentCyan;

        var srvStatus = MakeText(srvCard.transform, "ServerRolesStatusText", "Active Services: DHCP, DNS, HTTP", 12, TextAnchor.UpperLeft);
        var ssRt = srvStatus.rectTransform;
        ssRt.anchorMin = new Vector2(0f, 0.35f);
        ssRt.anchorMax = Vector2.one;
        ssRt.offsetMin = new Vector2(14f, 0f);
        ssRt.offsetMax = new Vector2(-14f, -44f);
        srvStatus.color = TextPrimary;

        var srvClients = MakeText(srvCard.transform, "ServerClientsText", "Active DHCP Leases: 2", 12, TextAnchor.UpperLeft);
        var scRt2 = srvClients.rectTransform;
        scRt2.anchorMin = Vector2.zero;
        scRt2.anchorMax = new Vector2(1f, 0.35f);
        scRt2.offsetMin = new Vector2(14f, 10f);
        scRt2.offsetMax = new Vector2(-14f, 0f);
        srvClients.color = TextSecondary;

        // 3. Printer Console Card
        var prnCard = new GameObject("PrinterConsoleCard", typeof(RectTransform));
        prnCard.transform.SetParent(rightTop.transform, false);
        Stretch(prnCard.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f);
        prnCard.SetActive(false);

        var prnHeader = MakeImage(prnCard.transform, "Header", CardInner);
        AnchorTop(prnHeader.rectTransform, 36f);
        var prnTitle = MakeText(prnHeader.transform, "Title", "NETWORK PRINTER MANAGEMENT", 13, TextAnchor.MiddleLeft);
        prnTitle.rectTransform.anchorMin = Vector2.zero;
        prnTitle.rectTransform.anchorMax = Vector2.one;
        prnTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        prnTitle.color = AccentGreen;

        var prnStatus = MakeText(prnCard.transform, "PrinterStatusText", "Printer Status: Ready / Online", 12, TextAnchor.UpperLeft);
        var psRt = prnStatus.rectTransform;
        psRt.anchorMin = new Vector2(0f, 0.45f);
        psRt.anchorMax = Vector2.one;
        psRt.offsetMin = new Vector2(14f, 0f);
        psRt.offsetMax = new Vector2(-14f, -44f);
        prnStatus.color = TextPrimary;

        var testPrintBtn = MakeButton(prnCard.transform, "PrintTestPageButton", "Print Network Test Page", new Vector2(14f, 14f), new Vector2(220f, 34f), BtnNormal);
        var tpRt = testPrintBtn.GetComponent<RectTransform>();
        tpRt.anchorMin = tpRt.anchorMax = tpRt.pivot = Vector2.zero;
        tpRt.anchoredPosition = new Vector2(14f, 14f);

        var prnQueue = MakeText(prnCard.transform, "PrinterQueueText", "Print Queue: 0 jobs waiting", 12, TextAnchor.MiddleLeft);
        var pqRt = prnQueue.rectTransform;
        pqRt.anchorMin = new Vector2(0f, 0f);
        pqRt.anchorMax = new Vector2(1f, 0.45f);
        pqRt.offsetMin = new Vector2(14f, 54f);
        pqRt.offsetMax = new Vector2(-14f, 0f);
        prnQueue.color = TextSecondary;

        // Right Lower Card: Physical & Infrastructure Path
        var rightBottom = MakeImage(desktopArea.transform, "PhysicalPathCard", CardDark);
        var rbRt = rightBottom.rectTransform;
        rbRt.anchorMin = new Vector2(0.51f, 0f);
        rbRt.anchorMax = new Vector2(1f, 0.41f);
        rbRt.offsetMin = rbRt.offsetMax = Vector2.zero;

        var pathHeader = MakeImage(rightBottom.transform, "Header", CardInner);
        AnchorTop(pathHeader.rectTransform, 32f);
        var pathTitle = MakeText(pathHeader.transform, "Title", "PHYSICAL & EFFECTIVE PATH (SYSTEM STATE)", 12, TextAnchor.MiddleLeft);
        pathTitle.rectTransform.anchorMin = Vector2.zero;
        pathTitle.rectTransform.anchorMax = Vector2.one;
        pathTitle.rectTransform.offsetMin = new Vector2(12f, 0f);
        pathTitle.color = AccentCyan;

        var pathDetails = MakeText(rightBottom.transform, "PhysicalPathDetailsText", "Physical connectivity path details...", 11, TextAnchor.UpperLeft);
        var pdRt = pathDetails.rectTransform;
        pdRt.anchorMin = Vector2.zero;
        pdRt.anchorMax = Vector2.one;
        pdRt.offsetMin = new Vector2(14f, 10f);
        pdRt.offsetMax = new Vector2(-14f, -40f);
        pathDetails.color = TextSecondary;

        // Wire Presenter
        var pres = root.GetComponent<TssDesktopSessionPresenter>();
        var dSer = new SerializedObject(pres);
        dSer.FindProperty("topBarTitleText").objectReferenceValue = topTitle;
        dSer.FindProperty("topBarHostnameText").objectReferenceValue = topHost;
        dSer.FindProperty("topBarLinkStatusText").objectReferenceValue = topLink;
        dSer.FindProperty("topBarTimeText").objectReferenceValue = topTime;
        dSer.FindProperty("disconnectButton").objectReferenceValue = discBtn;

        dSer.FindProperty("netAdapterNameText").objectReferenceValue = netAdapterName;
        dSer.FindProperty("physicalLinkStatusText").objectReferenceValue = physLinkStatus;
        dSer.FindProperty("peerDeviceText").objectReferenceValue = peerDev;
        dSer.FindProperty("macAddressText").objectReferenceValue = macAddr;
        dSer.FindProperty("dhcpToggle").objectReferenceValue = dhcpTog;
        dSer.FindProperty("staticToggle").objectReferenceValue = staticTog;
        dSer.FindProperty("ipAddressInput").objectReferenceValue = ipIn;
        dSer.FindProperty("subnetMaskInput").objectReferenceValue = maskIn;
        dSer.FindProperty("defaultGatewayInput").objectReferenceValue = gwIn;
        dSer.FindProperty("dnsServerInput").objectReferenceValue = dnsIn;
        dSer.FindProperty("applySettingsButton").objectReferenceValue = applyBtn;
        dSer.FindProperty("checkServerConnectionButton").objectReferenceValue = checkServerBtn;
        dSer.FindProperty("applyFeedbackText").objectReferenceValue = applyFeedback;

        dSer.FindProperty("openAccessPointBrowserButton").objectReferenceValue = openApBrowserBtn;
        dSer.FindProperty("accessPointBrowserCard").objectReferenceValue = apBrowserCard;
        dSer.FindProperty("accessPointBrowserStatusText").objectReferenceValue = apStatus;
        dSer.FindProperty("accessPointSsidInput").objectReferenceValue = apSsid;
        dSer.FindProperty("accessPointAdminPasswordInput").objectReferenceValue = apAdmin;
        dSer.FindProperty("accessPointPassphraseInput").objectReferenceValue = apPassphrase;
        dSer.FindProperty("accessPointChannelInput").objectReferenceValue = apChannel;
        dSer.FindProperty("accessPointDisableWpsToggle").objectReferenceValue = apWps;
        dSer.FindProperty("saveAccessPointButton").objectReferenceValue = saveApBtn;
        dSer.FindProperty("closeAccessPointBrowserButton").objectReferenceValue = closeApBrowserBtn;
        dSer.FindProperty("accessPointBrowserFeedbackText").objectReferenceValue = apFeedback;

        dSer.FindProperty("pcToolsCard").objectReferenceValue = pcCard;
        dSer.FindProperty("serverRolesCard").objectReferenceValue = srvCard;
        dSer.FindProperty("printerConsoleCard").objectReferenceValue = prnCard;

        dSer.FindProperty("commandInput").objectReferenceValue = commandInput;
        dSer.FindProperty("submitCommandButton").objectReferenceValue = submitBtn;
        dSer.FindProperty("historyUpButton").objectReferenceValue = historyUpBtn;
        dSer.FindProperty("historyDownButton").objectReferenceValue = historyDownBtn;
        dSer.FindProperty("commandOutputText").objectReferenceValue = pcDiagText;

        dSer.FindProperty("serverRolesStatusText").objectReferenceValue = srvStatus;
        dSer.FindProperty("serverClientsText").objectReferenceValue = srvClients;

        dSer.FindProperty("printerStatusText").objectReferenceValue = prnStatus;
        dSer.FindProperty("printerQueueText").objectReferenceValue = prnQueue;
        dSer.FindProperty("printTestPageButton").objectReferenceValue = testPrintBtn;

        dSer.FindProperty("physicalPathDetailsText").objectReferenceValue = pathDetails;
        dSer.ApplyModifiedPropertiesWithoutUndo();

        var path = $"{UiDir}/{canvasName}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AssignControlsToDevicePrefabs(GameObject terminalControls, GameObject desktopControls)
    {
        AssignCanvasPrefab("Assets/Prefabs/2960_Cisco_Switch_Prefab.prefab", terminalControls);
        AssignCanvasPrefab("Assets/Prefabs/RackRouter_1U_RearFourLED_19in.prefab", terminalControls);
        AssignCanvasPrefab("Assets/Prefabs/Server_Prefab.prefab", desktopControls);
        AssignCanvasPrefab("Assets/used_imported_assets/OfficeEssentialsPack/Prefabs/(Prb)PCCase.prefab", desktopControls);
        AssignCanvasPrefab("Assets/Prefabs/TSSLevel/TSS_Printer_Placement.prefab", desktopControls);
    }

    private static void AssignCanvasPrefab(string prefabPath, GameObject controlsPrefab)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!prefab)
        {
            Debug.LogWarning("AssignCanvasPrefab: Not found: " + prefabPath);
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        var interactable = root.GetComponentInChildren<TssObjectInteractable>(true);
        if (interactable)
        {
            var ser = new SerializedObject(interactable);
            ser.FindProperty("canvasPrefab").objectReferenceValue = controlsPrefab;
            ser.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void InstallCanvasesIntoTssScene(GameObject terminalPrefab, GameObject desktopPrefab, GameObject laptopPrefab, GameObject termControls, GameObject deskControls, GameObject laptopControls)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "TSS")
            return;

        var controller = Object.FindFirstObjectByType<TssDeviceSessionController>();
        if (!controller)
        {
            var go = new GameObject("DeviceSessionManager", typeof(TssDeviceSessionController));
            controller = go.GetComponent<TssDeviceSessionController>();
        }

        // Check if canvases already in scene
        var existingTerm = Object.FindFirstObjectByType<TssTerminalSessionPresenter>(FindObjectsInactive.Include);
        var existingDesk = Object.FindFirstObjectByType<TssDesktopSessionPresenter>(FindObjectsInactive.Include);
        TssDesktopSessionPresenter existingLaptop = null;
        foreach (var presenter in Object.FindObjectsByType<TssDesktopSessionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (presenter.name.Contains("Laptop")) { existingLaptop = presenter; break; }

        if (!existingTerm && terminalPrefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(terminalPrefab);
            inst.name = "TssTerminalSessionCanvas";
            inst.SetActive(false);
            existingTerm = inst.GetComponent<TssTerminalSessionPresenter>();
        }

        if (!existingDesk && desktopPrefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(desktopPrefab);
            inst.name = "TssDesktopSessionCanvas";
            inst.SetActive(false);
            existingDesk = inst.GetComponent<TssDesktopSessionPresenter>();
        }

        if (!existingLaptop && laptopPrefab)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(laptopPrefab);
            inst.name = "TssLaptopSessionCanvas";
            inst.SetActive(false);
            existingLaptop = inst.GetComponent<TssDesktopSessionPresenter>();
        }

        var cSer = new SerializedObject(controller);
        cSer.FindProperty("terminalCanvasPrefab").objectReferenceValue = terminalPrefab;
        cSer.FindProperty("desktopCanvasPrefab").objectReferenceValue = desktopPrefab;
        cSer.FindProperty("laptopCanvasPrefab").objectReferenceValue = laptopPrefab;
        cSer.FindProperty("terminalControlsPrefab").objectReferenceValue = termControls;
        cSer.FindProperty("desktopControlsPrefab").objectReferenceValue = deskControls;
        cSer.FindProperty("laptopControlsPrefab").objectReferenceValue = laptopControls;
        if (existingTerm)
            cSer.FindProperty("terminalPresenter").objectReferenceValue = existingTerm;
        if (existingDesk)
            cSer.FindProperty("desktopPresenter").objectReferenceValue = existingDesk;
        if (existingLaptop)
            cSer.FindProperty("laptopPresenter").objectReferenceValue = existingLaptop;
        cSer.ApplyModifiedProperties();

        // Ensure preinstalled cable tray on Rack 01
        var rack01Tray = GameObject.Find("TSS Level/60 Network Devices/Racks/Rack 01/Runtime Installed Equipment/RackCableTray U30-U31");
        if (!rack01Tray)
        {
            var rack01Parent = GameObject.Find("TSS Level/60 Network Devices/Racks/Rack 01/Runtime Installed Equipment");
            var trayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RackCableTray_2U_Front_FingerDuct_19in.prefab");
            if (rack01Parent && trayPrefab)
            {
                var trayInst = (GameObject)PrefabUtility.InstantiatePrefab(trayPrefab, rack01Parent.transform);
                trayInst.name = "RackCableTray U30-U31";
                trayInst.transform.localPosition = new Vector3(0f, 1.33f, 0.33f);
                trayInst.transform.localRotation = Quaternion.identity;
                trayInst.transform.localScale = Vector3.one;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Helper builders
    private static Image MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private static Text MakeText(Transform parent, string name, string content, int fontSize, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var txt = go.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = fontSize;
        txt.alignment = alignment;
        txt.text = content;
        txt.color = TextPrimary;
        return txt;
    }

    private static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color bgColor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = bgColor;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        var txt = MakeText(go.transform, "Text", label, 13, TextAnchor.MiddleCenter);
        txt.rectTransform.anchorMin = Vector2.zero;
        txt.rectTransform.anchorMax = Vector2.one;
        txt.rectTransform.offsetMin = txt.rectTransform.offsetMax = Vector2.zero;
        txt.raycastTarget = false;
        return btn;
    }

    private static InputField MakeInputField(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = InputBg;

        var textGo = MakeText(go.transform, "Text", "", 13, TextAnchor.MiddleLeft);
        textGo.rectTransform.anchorMin = Vector2.zero;
        textGo.rectTransform.anchorMax = Vector2.one;
        textGo.rectTransform.offsetMin = new Vector2(8f, 2f);
        textGo.rectTransform.offsetMax = new Vector2(-8f, -2f);

        var phGo = MakeText(go.transform, "Placeholder", placeholder, 13, TextAnchor.MiddleLeft);
        phGo.rectTransform.anchorMin = Vector2.zero;
        phGo.rectTransform.anchorMax = Vector2.one;
        phGo.rectTransform.offsetMin = new Vector2(8f, 2f);
        phGo.rectTransform.offsetMax = new Vector2(-8f, -2f);
        phGo.color = new Color(0.5f, 0.55f, 0.6f, 0.65f);

        var input = go.GetComponent<InputField>();
        input.textComponent = textGo;
        input.placeholder = phGo;
        return input;
    }

    private static InputField MakeLabeledInputField(Transform parent, string name, string label, string defaultValue, float yOffset)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        PlaceElement(row.GetComponent<RectTransform>(), yOffset, 36f);

        var lbl = MakeText(row.transform, "Label", label, 12, TextAnchor.MiddleLeft);
        var lRt = lbl.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(0.38f, 1f);
        lRt.offsetMin = lRt.offsetMax = Vector2.zero;
        lbl.color = TextSecondary;

        var input = MakeInputField(row.transform, "Input", "");
        var iRt = input.GetComponent<RectTransform>();
        iRt.anchorMin = new Vector2(0.40f, 0f);
        iRt.anchorMax = new Vector2(1f, 1f);
        iRt.offsetMin = new Vector2(0f, 2f);
        iRt.offsetMax = new Vector2(-6f, -2f);
        input.text = defaultValue;
        return input;
    }

    private static Toggle MakeToggle(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(130f, 26f);

        var bg = MakeImage(go.transform, "Background", CardInner);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.anchoredPosition = new Vector2(0f, 0f);
        bgRt.sizeDelta = new Vector2(20f, 20f);

        var check = MakeImage(bg.transform, "Checkmark", AccentGreen);
        var chRt = check.rectTransform;
        chRt.anchorMin = chRt.anchorMax = chRt.pivot = new Vector2(0.5f, 0.5f);
        chRt.sizeDelta = new Vector2(12f, 12f);

        var txt = MakeText(go.transform, "Label", label, 12, TextAnchor.MiddleLeft);
        var tRt = txt.rectTransform;
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(26f, 0f);
        tRt.offsetMax = Vector2.zero;

        var tog = go.GetComponent<Toggle>();
        tog.targetGraphic = bg;
        tog.graphic = check;
        return tog;
    }

    private static void Stretch(RectTransform rt, float minX, float minY, float maxX, float maxY, float offMinX = 0f, float offMinY = 0f, float offMaxX = 0f, float offMaxY = 0f)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(offMinX, offMinY);
        rt.offsetMax = new Vector2(offMaxX, offMaxY);
    }

    private static void AnchorTop(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, height);
    }

    private static void PlaceElement(RectTransform rt, float yPos, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, yPos);
        rt.sizeDelta = new Vector2(0f, height);
    }

    private static void PositionAnchor(RectTransform rt, float anchorX, float anchorY, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(anchorX, anchorY);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
