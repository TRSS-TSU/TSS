using System.Collections.Generic;
using StarterAssets;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class EndpointPlacementStation : MonoBehaviour
{
    private const string NameSignRootName = "Assigned Name Sign";
    private const string NameSignTextName = "Assigned Name Text";

    [SerializeField] private string stationId;
    [SerializeField] private EquipmentCategory[] acceptedCategories = { EquipmentCategory.DesktopPc, EquipmentCategory.Printer };
    [SerializeField] private Transform[] placementAnchors;
    [SerializeField] private Transform[] desktopPlacementAnchors;
    [SerializeField] private Transform[] printerPlacementAnchors;
    [SerializeField] private Transform placedParent;
    [SerializeField] private GameObject nameSignPrefab;
    [SerializeField] private Vector3 nameSignLocalOffset = new(0f, 1f, 0.76f);
    [SerializeField] private Vector3 nameSignLocalEuler = new(0f, -90f, 0f);
    [SerializeField] private Vector3 nameSignPanelSize = new(1.17f, 0.39f, 0.05f);
    [SerializeField] private Vector3 printerNameSignLocalOffset = new(0f, 1f, 0.76f);
    [SerializeField] private Vector3 printerNameSignLocalEuler = new(0f, -90f, 0f);
    [SerializeField] private Vector3 printerNameSignPanelSize = new(1.17f, 0.39f, 0.05f);
    [SerializeField] private Color nameSignPanelColor = new(0.9f, 0.9f, 0.82f, 1f);
    [SerializeField] private Color nameSignTextColor = Color.black;
    [SerializeField] private float nameSignTextSize = 1.6f;
    [SerializeField] private Vector3 placedInteractionTriggerCenter = new(0f, 0.6f, 0f);
    [SerializeField] private Vector3 placedInteractionTriggerSize = new(1.5f, 1.2f, 1.5f);
    [SerializeField] private Vector3 printerPlacedInteractionTriggerCenter = new(0f, 0.35f, 0f);
    [SerializeField] private Vector3 printerPlacedInteractionTriggerSize = new(0.75f, 0.7f, 0.75f);
    [SerializeField] private Color hoverTint = new(0.2f, 0.75f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float hoverBlend = 0.35f;

    private readonly Dictionary<string, GameObject> _placedVisuals = new();
    private bool _playerInRange;
    private int _nextPlacementNumber = 1;
    private Renderer[] _renderers;
    private Material[][] _materials;
    private Color[][] _baseColors;
    private Camera _mainCamera;
    private bool _isHovered;

    public string StationId => stationId;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(stationId))
            stationId = name;

        if (!placedParent)
            placedParent = transform;

        if (placementAnchors == null || placementAnchors.Length == 0)
            placementAnchors = new[] { transform };

        _mainCamera = Camera.main;
        CacheRenderers();
    }

    private void Update()
    {
        UpdateHoverAndClick();
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }

    private void OnMouseDown()
    {
        if (_isHovered && !IsGameplayInputBlocked())
            TryOpenPlacementPrompt();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other))
            _playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            _playerInRange = false;
            TssRuntimeUi.Instance?.CloseActiveMenu(this);
        }
    }

    public bool CanPlace(EquipmentDefinition equipment)
    {
        if (!equipment || GetFreeAnchor(equipment) == null)
            return false;

        foreach (var category in acceptedCategories)
        {
            if (equipment.category == category)
                return true;
        }

        return false;
    }

    public string NextPlacementId(EquipmentDefinition equipment)
    {
        return $"{StationId}:{equipment.equipmentId}:{_nextPlacementNumber++}";
    }

    private void TryOpenPlacementPrompt()
    {
        var session = TssTrainingSession.Instance;
        if (!session || !CanPlace(session.HeldItem))
            return;

        var equipment = session.HeldItem;
        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowNamePrompt($"Name {equipment.displayName}", string.Empty, deviceName => PlaceHeld(deviceName), this);
            return;
        }

        PlaceHeld(equipment.displayName);
    }

    private void PlaceHeld(string deviceName)
    {
        var equipment = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        var anchor = GetFreeAnchor(equipment);
        if (!anchor || !TssTrainingSession.Instance)
            return;

        if (!TssTrainingSession.Instance.TryPlaceHeldEndpoint(this, deviceName, out var record, out _))
            return;

        CreatePlacedVisual(anchor, record);
    }

    private void CreatePlacedVisual(Transform anchor, EndpointPlacementRecord record)
    {
        var prefab = record.Equipment.GetPlacementPrefab();
        var root = new GameObject(record.PlacementId);
        root.transform.SetParent(placedParent, false);
        root.transform.SetPositionAndRotation(anchor.position, anchor.rotation);

        if (prefab)
        {
            var visual = Instantiate(prefab, root.transform);
            visual.transform.localPosition = record.Equipment.placementLocalOffset;
            visual.transform.localRotation = Quaternion.Euler(record.Equipment.placementLocalEuler);
            visual.transform.localScale = record.Equipment.placementLocalScale;
            TssPortEndpointBinder.ConfigureEndpoints(visual, TssTrainingSession.GetEndpointOwnerPrefix(record.PlacementId), record.Equipment);

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                if (collider.GetComponentInParent<TssPortEndpoint>())
                    continue;

                collider.enabled = false;
            }

            foreach (var rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
                rigidbody.isKinematic = true;
        }

        var target = root.AddComponent<BoxCollider>();
        target.isTrigger = true;
        target.center = GetPlacedInteractionTriggerCenter(record.Equipment);
        target.size = GetPlacedInteractionTriggerSize(record.Equipment);
        root.AddComponent<EndpointPlacedEquipmentTarget>().Initialize(this, record.PlacementId, record.Equipment);
        CreateNameSign(root.transform, record);
        _placedVisuals[record.PlacementId] = root;
    }

    public void ClickPlaced(string placementId, EquipmentDefinition equipment, object owner = null)
    {
        var session = TssTrainingSession.Instance;
        if (!CanInspectPlacedEndpoint(session))
            return;

        if (session.HeldCable && TryEnterPlacedCableView(placementId))
            return;

        var currentName = GetDeviceName(placementId);
        var computerTitle = string.IsNullOrWhiteSpace(currentName) ? equipment.displayName : currentName.Trim();
        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowEndpointMenu(
                $"{StationId} {equipment.displayName}",
                true,
                true,
                equipment.category == EquipmentCategory.DesktopPc,
                deviceName =>
                {
                    if (TssTrainingSession.Instance.RenameEndpoint(placementId, deviceName))
                        UpdateNameSign(placementId, deviceName);
                },
                () => PickupPlaced(placementId, equipment),
                () => TssRuntimeUi.Instance.ShowComputerMenu(computerTitle, owner),
                owner,
                currentName);
            return;
        }

        PickupPlaced(placementId, equipment);
    }

    internal static bool CanInspectPlacedEndpoint(TssTrainingSession session)
    {
        return session && !session.HeldItem && !IsGameplayInputBlocked();
    }

    private bool TryEnterPlacedCableView(string placementId)
    {
        if (!_placedVisuals.TryGetValue(placementId, out var visual) || !visual)
            return false;

        var interactable = visual.GetComponentInChildren<TssObjectInteractable>(true);
        if (!interactable)
            return false;

        interactable.EnterFirstPerson();
        return true;
    }

    private void PickupPlaced(string placementId, EquipmentDefinition equipment)
    {
        if (!TssTrainingSession.Instance.TryPickupEndpoint(placementId, equipment, out _))
            return;

        if (_placedVisuals.TryGetValue(placementId, out var visual) && visual)
            Destroy(visual);

        _placedVisuals.Remove(placementId);
    }

    private void CreateNameSign(Transform root, EndpointPlacementRecord record)
    {
        if (record.Equipment.category != EquipmentCategory.DesktopPc && record.Equipment.category != EquipmentCategory.Printer)
            return;

        var signOffset = GetNameSignLocalOffset(record.Equipment);
        var signEuler = GetNameSignLocalEuler(record.Equipment);
        var signPanelSize = GetNameSignPanelSize(record.Equipment);
        var sign = nameSignPrefab ? Instantiate(nameSignPrefab, root) : CreateGeneratedNameSign(root, signPanelSize);
        sign.name = NameSignRootName;
        sign.transform.localPosition = signOffset;
        sign.transform.localRotation = Quaternion.Euler(signEuler);
        sign.transform.localScale = signPanelSize;

        var text = GetNameSignText(sign);
        if (!text)
            return;

        text.name = NameSignTextName;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = nameSignTextSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.color = nameSignTextColor;
        text.text = GetSignText(record.DeviceName, record.Equipment);
        text.rectTransform.sizeDelta = new Vector2(1.02f, 0.2f);
        text.transform.localPosition = new Vector3(0f, 0f, -0.56f);
        text.transform.localRotation = Quaternion.identity;
        text.transform.localScale = new Vector3(1f / signPanelSize.x, 1f / signPanelSize.y, 1f);
    }

    private GameObject CreateGeneratedNameSign(Transform root, Vector3 signPanelSize)
    {
        var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sign.transform.SetParent(root, false);

        if (sign.TryGetComponent<Collider>(out var collider))
            Destroy(collider);

        if (sign.TryGetComponent<Renderer>(out var renderer))
            renderer.material.color = nameSignPanelColor;

        var textObject = new GameObject(NameSignTextName);
        textObject.transform.SetParent(sign.transform, false);
        textObject.AddComponent<TextMeshPro>();
        return sign;
    }

    private void UpdateNameSign(string placementId, string deviceName)
    {
        if (!_placedVisuals.TryGetValue(placementId, out var visual) || !visual)
            return;

        var sign = visual.transform.Find(NameSignRootName);
        var text = sign ? GetNameSignText(sign.gameObject) : null;
        if (text)
            text.text = GetSignText(deviceName, null);
    }

    private static TextMeshPro GetNameSignText(GameObject sign)
    {
        if (!sign)
            return null;

        var directText = sign.transform.Find(NameSignTextName)?.GetComponent<TextMeshPro>();
        return directText ? directText : sign.GetComponentInChildren<TextMeshPro>(true);
    }

    private static string GetSignText(string deviceName, EquipmentDefinition fallbackEquipment)
    {
        if (!string.IsNullOrWhiteSpace(deviceName))
            return deviceName.Trim();

        return fallbackEquipment ? fallbackEquipment.displayName : string.Empty;
    }

    private Transform GetFreeAnchor(EquipmentDefinition equipment)
    {
        var categoryAnchors = GetCategoryAnchors(equipment);
        if (HasAnchor(categoryAnchors))
            return GetFreeAnchor(categoryAnchors);

        return GetFreeAnchor(placementAnchors);
    }

    private Transform GetFreeAnchor(IReadOnlyList<Transform> anchors)
    {
        if (anchors == null)
            return null;

        foreach (var anchor in anchors)
        {
            if (!anchor)
                continue;

            var occupied = false;
            foreach (var visual in _placedVisuals.Values)
            {
                if (visual && Vector3.Distance(visual.transform.position, anchor.position) < 0.01f)
                {
                    occupied = true;
                    break;
                }
            }

            if (!occupied)
                return anchor;
        }

        return null;
    }

    private IReadOnlyList<Transform> GetCategoryAnchors(EquipmentDefinition equipment)
    {
        if (!equipment)
            return null;

        return equipment.category switch
        {
            EquipmentCategory.DesktopPc => desktopPlacementAnchors,
            EquipmentCategory.Printer => printerPlacementAnchors,
            _ => null
        };
    }

    private static bool HasAnchor(IReadOnlyList<Transform> anchors)
    {
        if (anchors == null)
            return false;

        foreach (var anchor in anchors)
        {
            if (anchor)
                return true;
        }

        return false;
    }

    private Vector3 GetNameSignLocalOffset(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Printer ? printerNameSignLocalOffset : nameSignLocalOffset;
    }

    private Vector3 GetNameSignLocalEuler(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Printer ? printerNameSignLocalEuler : nameSignLocalEuler;
    }

    private Vector3 GetNameSignPanelSize(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Printer ? printerNameSignPanelSize : nameSignPanelSize;
    }

    private Vector3 GetPlacedInteractionTriggerCenter(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Printer ? printerPlacedInteractionTriggerCenter : placedInteractionTriggerCenter;
    }

    private Vector3 GetPlacedInteractionTriggerSize(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Printer ? printerPlacedInteractionTriggerSize : placedInteractionTriggerSize;
    }

    private static string GetDeviceName(string placementId)
    {
        var session = TssTrainingSession.Instance;
        if (!session)
            return string.Empty;

        foreach (var record in session.EndpointPlacements)
        {
            if (record.PlacementId == placementId)
                return record.DeviceName;
        }

        return string.Empty;
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<ThirdPersonController>();
    }

    private static bool IsGameplayInputBlocked()
    {
        return TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked;
    }

    private void UpdateHoverAndClick()
    {
        var hovered = _playerInRange && CanPlace(TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null) && !IsGameplayInputBlocked() && IsMouseOverStation();

        SetHighlight(hovered);
        _isHovered = hovered;

        if (_isHovered && WasPrimaryClickPressed())
            TryOpenPlacementPrompt();
    }

    private bool IsMouseOverStation()
    {
        if (!_mainCamera)
            _mainCamera = Camera.main;

        if (!_mainCamera)
            return false;

        var ray = _mainCamera.ScreenPointToRay(GetMousePosition());
        var hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        foreach (var hit in hits)
        {
            if (hit.collider && hit.collider.transform.IsChildOf(transform))
                return true;

        }

        return false;
    }

    private void CacheRenderers()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _materials = new Material[_renderers.Length][];
        _baseColors = new Color[_renderers.Length][];

        for (var rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            var materials = _renderers[rendererIndex].materials;
            _materials[rendererIndex] = materials;
            _baseColors[rendererIndex] = new Color[materials.Length];

            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                _baseColors[rendererIndex][materialIndex] = GetMaterialColor(materials[materialIndex]);
        }
    }

    private void SetHighlight(bool highlighted)
    {
        if (_materials == null)
            return;

        if (IsGameplayInputBlocked())
            highlighted = false;

        for (var rendererIndex = 0; rendererIndex < _materials.Length; rendererIndex++)
        {
            var materials = _materials[rendererIndex];
            Debug.Log("materials");
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (!material){

                    Debug.Log ("No Material to Color.");
                    continue;
                }
                else{

                var baseColor = _baseColors[rendererIndex][materialIndex];
                    Debug.Log ("Material:" + material);
                SetMaterialColor(material, highlighted ? Color.Lerp(baseColor, hoverTint, hoverBlend) : baseColor);
                }

            }
        }
    }

    private static Color GetMaterialColor(Material material)
    {
        if (!material)
            return Color.white;

        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");

        return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static Vector3 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    private static bool WasPrimaryClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }
}
