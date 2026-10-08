using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class TssWallportTerminationPrefabBuilder
{
    private const string Folder = "Assets/Prefabs/TSSInfrastructureTermination";
    private const string SpritePath = "Assets/Art/Termination/WallportFourPort.png";
    private const string RearModulePath = "Assets/Art/Termination/RearModule.jpg";
    private const string PatchPanelPath = "Assets/Art/Termination/PatchPanelRear.png";
    private const string ToolSpritePath = "Assets/Art/Termination/PunchDownTool.png";

    [MenuItem("TSS/Termination/Install Infrastructure Procedures")]
    public static void Install()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "TSSInfrastructureTermination");
        AssetDatabase.DeleteAsset(Folder + "/TssTerminationWorkbench.prefab");
        AssetDatabase.DeleteAsset(Folder + "/TssPunchDownAction.prefab");
        AssetDatabase.DeleteAsset(Folder + "/Materials");

        var wallportSprite = ImportSprite(SpritePath);
        var rearModuleSprite = ImportSprite(RearModulePath);
        var patchPanelSprite = ImportSprite(PatchPanelPath);
        var punchToolSprite = ImportSprite(ToolSpritePath);

        var wallport = new GameObject("TssWallportTermination", typeof(RectTransform), typeof(TssWallportTerminationView));
        var view = wallport.GetComponent<TssWallportTerminationView>();
        var viewData = new SerializedObject(view);
        viewData.FindProperty("wallportSprite").objectReferenceValue = wallportSprite;
        viewData.FindProperty("rearModuleSprite").objectReferenceValue = rearModuleSprite;
        viewData.FindProperty("punchToolSprite").objectReferenceValue = punchToolSprite;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(wallport, Folder + "/TssWallportTermination.prefab");
        Object.DestroyImmediate(wallport);

        var patchPanel = new GameObject("TssPatchPanelTermination", typeof(RectTransform), typeof(TssWallportTerminationView));
        var patchData = new SerializedObject(patchPanel.GetComponent<TssWallportTerminationView>());
        patchData.FindProperty("isPatchPanel").boolValue = true;
        patchData.FindProperty("patchPanelSprite").objectReferenceValue = patchPanelSprite;
        patchData.FindProperty("punchToolSprite").objectReferenceValue = punchToolSprite;
        patchData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(patchPanel, Folder + "/TssPatchPanelTermination.prefab");
        Object.DestroyImmediate(patchPanel);

        var controlsPrefab = BuildControlsPrefab("TssTerminationFirstPersonControls", "Terminate Wallport");
        var patchControlsPrefab = BuildControlsPrefab("TssPatchPanelFirstPersonControls", "Terminate Patch Panel");

        var wallportPrefab = AssetDatabase.LoadAssetAtPath<TssWallportTerminationView>(Folder + "/TssWallportTermination.prefab");
        var patchPanelPrefab = AssetDatabase.LoadAssetAtPath<TssWallportTerminationView>(Folder + "/TssPatchPanelTermination.prefab");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "TSS")
            throw new System.InvalidOperationException("Open the TSS scene before installing the infrastructure procedures.");

        var controller = Object.FindFirstObjectByType<TssTerminationGameController>();
        if (!controller)
            throw new System.InvalidOperationException("TSS scene has no termination controller.");
        var controllerData = new SerializedObject(controller);
        controllerData.FindProperty("wallportPrefab").objectReferenceValue = wallportPrefab;
        controllerData.FindProperty("patchPanelPrefab").objectReferenceValue = patchPanelPrefab;
        controllerData.ApplyModifiedProperties();

        var wallportCount = 0;
        var patchPanelCount = 0;
        foreach (var entry in Object.FindObjectsByType<TssTerminationEntryAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var isWallport = false;
            var isPatchPanel = false;
            foreach (var port in entry.GetComponentsInChildren<TssPortEndpoint>(true))
            {
                if (string.IsNullOrEmpty(port.EndpointId))
                    continue;
                isWallport |= port.EndpointId.StartsWith("WallPort:", System.StringComparison.Ordinal);
                isPatchPanel |= port.EndpointId.StartsWith("PatchPanel:", System.StringComparison.Ordinal);
            }
            if (!isWallport && !isPatchPanel)
                continue;
            var entryData = new SerializedObject(entry);
            entryData.FindProperty("controlsPrefab").objectReferenceValue = isPatchPanel ? patchControlsPrefab : controlsPrefab;
            entryData.ApplyModifiedProperties();
            if (isPatchPanel)
                patchPanelCount++;
            else
                wallportCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Installed rear termination procedures for {wallportCount} wallports and {patchPanelCount} patch panels.");
    }

    private static Sprite ImportSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer)
            throw new System.InvalidOperationException("Termination image is missing: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TssTerminationFirstPersonControls BuildControlsPrefab(string name, string terminationLabel)
    {
        var controls = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(TssTerminationFirstPersonControls));
        var canvas = controls.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 450;
        var scaler = controls.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 800f);
        var front = MakeButton(controls.transform, "Return", new Vector2(24f, 24f));
        var rear = MakeButton(controls.transform, terminationLabel, new Vector2(200f, 24f));
        var controlsData = new SerializedObject(controls.GetComponent<TssTerminationFirstPersonControls>());
        controlsData.FindProperty("frontViewButton").objectReferenceValue = front;
        controlsData.FindProperty("rearTerminationViewButton").objectReferenceValue = rear;
        controlsData.ApplyModifiedPropertiesWithoutUndo();
        var path = Folder + "/" + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(controls, path);
        Object.DestroyImmediate(controls);
        return AssetDatabase.LoadAssetAtPath<TssTerminationFirstPersonControls>(path);
    }

    private static Button MakeButton(Transform parent, string title, Vector2 position)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(title == "Return" ? 160f : 240f, 44f);
        var image = go.GetComponent<Image>();
        image.color = new Color(0.10f, 0.25f, 0.24f, 0.95f);
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var label = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var textRect = label.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        var text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = title;
        text.fontSize = 17;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return button;
    }
}
