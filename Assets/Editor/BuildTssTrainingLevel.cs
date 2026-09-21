using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildTssTrainingLevel
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrefabFolder = "Assets/Prefabs/TSSLevel";
    private const string MaterialFolder = "Assets/Materials/TSSLevel";
    private const string OfficePackPrefabFolder = "Assets/Imported/OfficePack/Prefabs/URP";

    [MenuItem("Tools/TSS/Build Training Support Level")]
    public static void Build()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        DestroyNamed("Prototype Floor");
        DestroyNamed("TSS Level");

        EnsureFolders();
        var materials = CreateMaterials();
        var emptyRack = CreateRackPrefab("TSS_ServerRack_Empty_Models", false, materials);
        var equippedRack = CreateRackPrefab("TSS_ServerRack_Equipped_Models", true, materials);
        var wallPort = CreateWallPortPrefab(materials);
        var door = CreateDoorPrefab(materials);

        var root = new GameObject("TSS Level");
        BuildShell(root.transform, materials);
        BuildServerRoom(root.transform, emptyRack, equippedRack, door, wallPort, materials);
        BuildFirstFloorOffices(root.transform, door, wallPort, materials);
        BuildSecondFloor(root.transform, door, wallPort, materials);
        BuildStairs(root.transform, materials);
        BuildLights(root.transform, materials);
        BuildSecurityProps(root.transform, materials);

        PositionPlayer();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
    }

    private static void EnsureFolders()
    {
        Directory.CreateDirectory(PrefabFolder);
        Directory.CreateDirectory(MaterialFolder);
    }

    private static LevelMaterials CreateMaterials()
    {
        return new LevelMaterials
        {
            Wall = Material("TSS_Wall", new Color(0.72f, 0.75f, 0.74f)),
            Floor = Material("TSS_FloorTile", new Color(0.46f, 0.48f, 0.48f)),
            Carpet = Material("TSS_Carpet", new Color(0.18f, 0.24f, 0.32f)),
            ServerFloor = Material("TSS_ServerFloor", new Color(0.08f, 0.09f, 0.1f)),
            Rack = Material("TSS_RackBlack", new Color(0.015f, 0.017f, 0.02f)),
            Metal = Material("TSS_Metal", new Color(0.42f, 0.45f, 0.46f)),
            Screen = Material("TSS_ScreenGlow", new Color(0.02f, 0.22f, 0.32f), true),
            LightSource = Material("TSS_TempLightEmitter", new Color(1f, 0.94f, 0.72f), true),
            Wood = Material("TSS_DeskWood", new Color(0.42f, 0.29f, 0.17f)),
            Trim = Material("TSS_Trim", new Color(0.12f, 0.13f, 0.14f)),
            Coffee = Material("TSS_Coffee", new Color(0.2f, 0.1f, 0.04f)),
            Pencil = Material("TSS_Pencil", new Color(0.95f, 0.72f, 0.12f))
        };
    }

    private static Material Material(string name, Color color, bool emission = false)
    {
        var path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        material.SetColor("_BaseColor", color);
        if (emission)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 2f);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject CreateRackPrefab(string name, bool equipped, LevelMaterials materials)
    {
        var path = $"{PrefabFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var rack = new GameObject(name);
        Box(rack.transform, "Frame", Vector3.zero, new Vector3(1f, 2.2f, 0.8f), materials.Rack);
        Box(rack.transform, "Glass Door", new Vector3(0f, 0f, -0.41f), new Vector3(0.9f, 2f, 0.04f), materials.Metal);
        if (equipped)
        {
            var hardwarePaths = new[]
            {
                "Assets/Imported/Infrastructure/R760xd2_Storage.fbx",
                "Assets/Imported/Infrastructure/XE8545_GPU_Server.fbx",
                "Assets/Imported/Infrastructure/Catalyst9300_Switch.fbx"
            };
            for (var i = 0; i < 8; i++)
            {
                var y = -0.82f + i * 0.22f;
                var hardware = LoadProp(hardwarePaths[i % hardwarePaths.Length]);
                if (hardware != null)
                {
                    var hardwareInstance = (GameObject)PrefabUtility.InstantiatePrefab(hardware);
                    hardwareInstance.name = $"Server Hardware {i + 1}";
                    hardwareInstance.transform.SetParent(rack.transform, false);
                    hardwareInstance.transform.localPosition = new Vector3(0f, y, -0.35f);
                    hardwareInstance.transform.localScale = new Vector3(1.5f, 1.3f, 1f);
                }
                else
                    Box(rack.transform, $"Server {i + 1}", new Vector3(0f, y, -0.45f), new Vector3(0.82f, 0.12f, 0.08f), materials.Metal);
                Box(rack.transform, $"Status Lights {i + 1}", new Vector3(0.32f, y, -0.5f), new Vector3(0.12f, 0.035f, 0.015f), materials.Screen);
            }
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(rack, path);
        Object.DestroyImmediate(rack);
        return prefab;
    }

    private static GameObject CreateWallPortPrefab(LevelMaterials materials)
    {
        var path = $"{PrefabFolder}/TSS_WallPort_4Ethernet.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var port = new GameObject("TSS_WallPort_4Ethernet");
        Box(port.transform, "Faceplate", Vector3.zero, new Vector3(0.38f, 0.28f, 0.035f), materials.Wall);
        for (var x = 0; x < 2; x++)
        for (var y = 0; y < 2; y++)
        {
            var local = new Vector3(-0.09f + x * 0.18f, -0.06f + y * 0.12f, -0.025f);
            Box(port.transform, $"Ethernet Port {x + y * 2 + 1}", local, new Vector3(0.095f, 0.055f, 0.02f), materials.Trim);
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(port, path);
        Object.DestroyImmediate(port);
        return prefab;
    }

    private static GameObject CreateDoorPrefab(LevelMaterials materials)
    {
        var path = $"{PrefabFolder}/TSS_WorkingDoor.prefab";
        var pivot = new GameObject("TSS_WorkingDoor");
        var swingPanel = new GameObject("Swing Panel");
        swingPanel.transform.SetParent(pivot.transform, false);
        Box(swingPanel.transform, "Door Panel", new Vector3(0.45f, 1f, 0f), new Vector3(0.9f, 2f, 0.08f), materials.Wood);
        Box(swingPanel.transform, "Handle", new Vector3(0.82f, 1f, -0.08f), new Vector3(0.08f, 0.08f, 0.08f), materials.Metal);
        var trigger = Box(pivot.transform, "Interaction Trigger", new Vector3(0.45f, 1f, -0.25f), new Vector3(1.4f, 2f, 1.2f), materials.Trim);
        trigger.GetComponent<MeshRenderer>().enabled = false;
        trigger.GetComponent<BoxCollider>().isTrigger = true;
        var rigidbody = pivot.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        var door = pivot.AddComponent<TssDoorInteractable>();
        var serialized = new SerializedObject(door);
        serialized.FindProperty("doorPanel").objectReferenceValue = swingPanel.transform;
        serialized.FindProperty("openAngle").floatValue = -95f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(pivot, path);
        Object.DestroyImmediate(pivot);
        return prefab;
    }

    private static void BuildShell(Transform root, LevelMaterials materials)
    {
        Box(root, "First Floor Slab", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.1f, 22f), materials.Floor);
        Box(root, "Second Floor Slab With Stair Opening", new Vector3(0f, 3.25f, 1.5f), new Vector3(30f, 0.16f, 19f), materials.Floor);
        Box(root, "Second Floor Stair Landing Left", new Vector3(-10.8f, 3.25f, -9.25f), new Vector3(8.4f, 0.16f, 3.5f), materials.Floor);
        Box(root, "Second Floor Stair Landing Right", new Vector3(6.5f, 3.25f, -9.25f), new Vector3(17f, 0.16f, 3.5f), materials.Floor);

        Wall(root, "North Exterior", new Vector3(0f, 1.5f, 11f), new Vector3(30f, 3f, 0.2f), materials.Wall);
        Wall(root, "South Exterior", new Vector3(0f, 1.5f, -11f), new Vector3(30f, 3f, 0.2f), materials.Wall);
        Wall(root, "West Exterior", new Vector3(-15f, 1.5f, 0f), new Vector3(0.2f, 3f, 22f), materials.Wall);
        Wall(root, "East Exterior", new Vector3(15f, 1.5f, 0f), new Vector3(0.2f, 3f, 22f), materials.Wall);

        Wall(root, "Second North Exterior", new Vector3(0f, 4.8f, 11f), new Vector3(30f, 3f, 0.2f), materials.Wall);
        Wall(root, "Second South Exterior", new Vector3(0f, 4.8f, -11f), new Vector3(30f, 3f, 0.2f), materials.Wall);
        Wall(root, "Second West Exterior", new Vector3(-15f, 4.8f, 0f), new Vector3(0.2f, 3f, 22f), materials.Wall);
        Wall(root, "Second East Exterior", new Vector3(15f, 4.8f, 0f), new Vector3(0.2f, 3f, 22f), materials.Wall);
    }

    private static void BuildServerRoom(Transform root, GameObject emptyRack, GameObject equippedRack, GameObject door, GameObject wallPort, LevelMaterials materials)
    {
        Box(root, "Server Room Dark Floor", new Vector3(-7.5f, 0.01f, 3f), new Vector3(14.7f, 0.04f, 15.5f), materials.ServerFloor);
        Wall(root, "Server Room East Wall", new Vector3(0f, 1.5f, 3f), new Vector3(0.2f, 3f, 16f), materials.Wall);
        WallWithDoorOpenings(root, "Server Room South Wall", -15f, 0f, 1.5f, -5f, new[] { -5.8f }, materials.Wall);
        Place(door, root, "Server Room Door", new Vector3(-5.8f, 0f, -5.12f), Quaternion.identity);

        for (var i = 0; i < 2; i++)
        {
            var prefab = i == 0 ? emptyRack : equippedRack;
            Place(prefab, root, $"Rack Row 1-{i + 1}", new Vector3(-12.5f + i * 2f, 1.1f, -1f), Quaternion.Euler(0f, 180f, 0f));
        }

        for (var i = 0; i < 5; i++)
            Place(wallPort, root, $"Server Wall Port {i + 1}", new Vector3(-13.5f + i * 2.2f, 0.8f, -4.88f), Quaternion.Euler(0f, 180f, 0f));
    }

    private static void BuildFirstFloorOffices(Transform root, GameObject door, GameObject wallPort, LevelMaterials materials)
    {
        var doorHinges = new[] { 1.4f, 6.4f, 11.4f };
        WallWithDoorOpenings(root, "First Hall Divider", -15f, 15f, 1.5f, -2.5f, doorHinges, materials.Wall);
        Wall(root, "First Office Divider 1", new Vector3(5f, 1.5f, 4.2f), new Vector3(0.2f, 3f, 13.2f), materials.Wall);
        Wall(root, "First Office Divider 2", new Vector3(10f, 1.5f, 4.2f), new Vector3(0.2f, 3f, 13.2f), materials.Wall);

        for (var i = 0; i < 3; i++)
        {
            var x = 2.5f + i * 5f;
            Place(door, root, $"First Floor Office Door {i + 1}", new Vector3(x - 1.1f, 0f, -2.62f), Quaternion.identity);
            BuildDesk(root, $"First Office Desk {i + 1}", new Vector3(x, 0f, 5.8f), materials);
            Place(wallPort, root, $"First Office Wall Port {i + 1}", new Vector3(x, 0.8f, -2.38f), Quaternion.identity);
        }
    }

    private static void BuildSecondFloor(Transform root, GameObject door, GameObject wallPort, LevelMaterials materials)
    {
        var doorHinges = new[] { -11f, -5.5f, 0f, 5.5f, 11f };
        WallWithDoorOpenings(root, "Second Hall Divider", -15f, 15f, 4.8f, -2.5f, doorHinges, materials.Wall);
        for (var i = 0; i < 5; i++)
        {
            var x = -11f + i * 5.5f;
            if (i > 0)
                Wall(root, $"Second Office Divider {i}", new Vector3(x - 2.75f, 4.8f, 4.2f), new Vector3(0.2f, 3f, 13.2f), materials.Wall);

            Place(door, root, $"Second Floor Office Door {i + 1}", new Vector3(x, 3.3f, -2.62f), Quaternion.identity);
            BuildDesk(root, $"Second Office Desk {i + 1}", new Vector3(x, 3.3f, 5.6f), materials);
            Place(wallPort, root, $"Second Office Wall Port {i + 1}", new Vector3(x, 4.1f, -2.38f), Quaternion.identity);
            Box(root, $"Office Carpet {i + 1}", new Vector3(x, 3.33f, 4.3f), new Vector3(4.2f, 0.03f, 4.8f), materials.Carpet);
        }
    }

    private static void BuildStairs(Transform root, LevelMaterials materials)
    {
        var stairRoot = new GameObject("TSS Aligned Stairs");
        stairRoot.transform.SetParent(root, false);

        const int steps = 18;
        const float stepHeight = 3.25f / steps;
        const float stepRun = 0.34f;
        for (var i = 0; i < steps; i++)
        {
            var height = stepHeight * (i + 1);
            Box(stairRoot.transform, $"Step {i + 1}", new Vector3(-12.6f + i * stepRun, height * 0.5f, -9.25f), new Vector3(stepRun, height, 2.4f), materials.Floor);
        }
    }

    private static void BuildDesk(Transform root, string name, Vector3 position, LevelMaterials materials)
    {
        var deskPrefab = LoadProp($"{OfficePackPrefabFolder}/Desk.prefab");
        if (deskPrefab != null)
        {
            var desk = Place(deskPrefab, root, name, position + new Vector3(0.75f, 0f, 0.75f), Quaternion.identity);
            var collider = desk.AddComponent<BoxCollider>();
            collider.center = new Vector3(-0.75f, 0.4f, -0.75f);
            collider.size = new Vector3(1.5f, 0.8f, 1.5f);
            Place(LoadProp($"{OfficePackPrefabFolder}/Monitor.prefab"), root, $"{name} Monitor", position + new Vector3(0f, 0.74f, 0f), Quaternion.Euler(0f, 180f, 0f));
            Place(LoadProp($"{OfficePackPrefabFolder}/Laptop.prefab"), root, $"{name} Laptop", position + new Vector3(0.4f, 0.75f, 0.35f), Quaternion.identity);
            Place(LoadProp($"{OfficePackPrefabFolder}/CoffeeMug.prefab"), root, $"{name} Coffee Mug", position + new Vector3(-0.5f, 0.75f, 0.35f), Quaternion.identity);
            Place(LoadProp($"{OfficePackPrefabFolder}/DeskLamp.prefab"), root, $"{name} Desk Lamp", position + new Vector3(0.35f, 0.75f, 0.55f), Quaternion.identity);
            Place(LoadProp($"{OfficePackPrefabFolder}/Chair.prefab"), root, $"{name} Chair", position + new Vector3(0f, 0f, 1.55f), Quaternion.Euler(0f, 180f, 0f));
            return;
        }

        var deskObject = new GameObject(name);
        deskObject.transform.SetParent(root, false);
        deskObject.transform.position = position + new Vector3(0f, 0.78f, 0f);
        Box(deskObject.transform, "Top", Vector3.zero, new Vector3(1.8f, 0.12f, 0.8f), materials.Wood);
        Box(deskObject.transform, "Left Leg", new Vector3(-0.75f, -0.42f, -0.28f), new Vector3(0.12f, 0.75f, 0.12f), materials.Metal);
        Box(deskObject.transform, "Right Leg", new Vector3(0.75f, -0.42f, -0.28f), new Vector3(0.12f, 0.75f, 0.12f), materials.Metal);
        Box(deskObject.transform, "Monitor", new Vector3(0f, 0.45f, -0.18f), new Vector3(0.75f, 0.45f, 0.06f), materials.Screen);
        Box(deskObject.transform, "Keyboard", new Vector3(0f, 0.12f, 0.18f), new Vector3(0.65f, 0.04f, 0.22f), materials.Trim);
        Box(deskObject.transform, "Mouse", new Vector3(0.5f, 0.13f, 0.18f), new Vector3(0.16f, 0.04f, 0.22f), materials.Trim);
        Box(deskObject.transform, "Coffee Mug", new Vector3(-0.55f, 0.2f, 0.18f), new Vector3(0.16f, 0.18f, 0.16f), materials.Coffee);
        Box(deskObject.transform, "Pencil", new Vector3(-0.28f, 0.16f, 0.25f), new Vector3(0.55f, 0.035f, 0.035f), materials.Pencil);
    }

    private static void BuildLights(Transform root, LevelMaterials materials)
    {
        RenderSettings.ambientIntensity = 0.35f;
        AddLight(root, "Server Room Rack Spot Light 1", new Vector3(-12.5f, 2.7f, 1.5f), new Color(0.8f, 0.9f, 1f), 3.1f, 4.5f, LightType.Spot, LightShadows.Soft, materials.LightSource);
        AddLight(root, "Server Room Rack Spot Light 2", new Vector3(-10.5f, 2.7f, 1.5f), new Color(0.8f, 0.9f, 1f), 3.1f, 4.5f, LightType.Spot, LightShadows.Soft, materials.LightSource);
        AddLight(root, "Server Room Door Spot Light", new Vector3(-5.8f, 2.7f, -3.6f), new Color(0.85f, 0.92f, 1f), 2.4f, 3.8f, LightType.Spot, LightShadows.Soft, materials.LightSource);
        AddLight(root, "Server Room Work Area Spot Light", new Vector3(-11.5f, 2.7f, 4.2f), new Color(0.8f, 0.9f, 1f), 2.5f, 4.2f, LightType.Spot, LightShadows.Soft, materials.LightSource);
        for (var i = 0; i < 4; i++)
            AddLight(root, $"First Floor Office Light {i + 1}", new Vector3(3f + i * 4.5f, 2.7f, 4f), Color.white, 3f, 5f, LightType.Spot, LightShadows.Soft, materials.LightSource);
        for (var i = 0; i < 5; i++)
            AddLight(root, $"Second Floor Office Light {i + 1}", new Vector3(-11f + i * 5.5f, 6f, 4f), Color.white, 2.8f, 5f, LightType.Spot, LightShadows.Soft, materials.LightSource);
    }

    private static void BuildSecurityProps(Transform root, LevelMaterials materials)
    {
        for (var i = 0; i < 4; i++)
        {
            var camera = new GameObject($"Security Camera {i + 1}");
            camera.transform.SetParent(root, false);
            camera.transform.position = new Vector3(-13f + i * 8f, i < 2 ? 2.5f : 5.8f, i % 2 == 0 ? -9.5f : 9.5f);
            camera.transform.rotation = Quaternion.Euler(25f, i % 2 == 0 ? 35f : 145f, 0f);
            Box(camera.transform, "Body", Vector3.zero, new Vector3(0.38f, 0.22f, 0.22f), materials.Metal);
            Box(camera.transform, "Lens", new Vector3(0f, 0f, -0.16f), new Vector3(0.18f, 0.18f, 0.08f), materials.Trim);
        }
    }

    private static void PositionPlayer()
    {
        var player = GameObject.Find("Starter Player");
        if (player == null)
            return;

        player.transform.position = new Vector3(-3f, 0f, -8.5f);
        player.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
    }

    private static GameObject Place(GameObject prefab, Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
            return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = rotation;
        return instance;
    }

    private static GameObject LoadProp(string path)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = scale;
        if (material != null)
            box.GetComponent<Renderer>().sharedMaterial = material;
        return box;
    }

    private static void Wall(Transform root, string name, Vector3 position, Vector3 scale, Material material)
    {
        Box(root, name, position, scale, material);
    }

    private static void WallWithDoorOpenings(Transform root, string name, float startX, float endX, float centerY, float z, float[] hingeXs, Material material)
    {
        const float doorWidth = 1.1f;
        var cursor = startX;
        var segment = 1;
        foreach (var hingeX in hingeXs.OrderBy(x => x))
        {
            AddWallSegment(root, $"{name} {segment++}", cursor, hingeX, centerY, z, material);
            cursor = hingeX + doorWidth;
        }

        AddWallSegment(root, $"{name} {segment}", cursor, endX, centerY, z, material);
    }

    private static void AddWallSegment(Transform root, string name, float startX, float endX, float centerY, float z, Material material)
    {
        var width = endX - startX;
        if (width <= 0.05f)
            return;

        Wall(root, name, new Vector3(startX + width * 0.5f, centerY, z), new Vector3(width, 3f, 0.2f), material);
    }

    private static void AddLight(
        Transform root,
        string name,
        Vector3 position,
        Color color,
        float intensity,
        float range,
        LightType type,
        LightShadows shadows,
        Material sourceMaterial,
        LightmapBakeType bakeType = LightmapBakeType.Realtime)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        obj.transform.position = position;
        if (type == LightType.Spot)
            obj.transform.rotation = Quaternion.LookRotation(Vector3.down);
        var light = obj.AddComponent<Light>();
        light.type = type;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = shadows;
        light.lightmapBakeType = bakeType;
        light.spotAngle = 75f;
        light.innerSpotAngle = 45f;
        light.shadowStrength = 0.65f;
        light.shadowBias = 0.04f;
        light.shadowNormalBias = 0.4f;
        AddLightEmitterStandIn(root, $"{name} Temporary Emission Source", position, type, sourceMaterial);
    }

    private static void AddLightEmitterStandIn(Transform root, string name, Vector3 lightPosition, LightType type, Material material)
    {
        var source = Box(root, name, EmitterPosition(lightPosition, type), EmitterScale(type), material);
        source.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        source.GetComponent<Renderer>().receiveShadows = false;
    }

    private static Vector3 EmitterPosition(Vector3 lightPosition, LightType type)
    {
        if (type == LightType.Spot)
            return new Vector3(lightPosition.x, lightPosition.y < 3.5f ? 2.98f : 6.16f, lightPosition.z);

        return lightPosition;
    }

    private static Vector3 EmitterScale(LightType type)
    {
        return type == LightType.Spot ? new Vector3(0.9f, 0.04f, 0.28f) : new Vector3(0.08f, 0.7f, 0.04f);
    }

    private static void DestroyNamed(string name)
    {
        foreach (var obj in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(obj => obj.name == name).ToArray())
        {
            Object.DestroyImmediate(obj);
        }
    }

    private sealed class LevelMaterials
    {
        public Material Wall;
        public Material Floor;
        public Material Carpet;
        public Material ServerFloor;
        public Material Rack;
        public Material Metal;
        public Material Screen;
        public Material LightSource;
        public Material Wood;
        public Material Trim;
        public Material Coffee;
        public Material Pencil;
    }
}
