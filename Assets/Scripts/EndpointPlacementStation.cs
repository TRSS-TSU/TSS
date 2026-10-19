using System.Collections.Generic;
using System;
using StarterAssets;
using TMPro;
using UnityEngine;

public sealed class EndpointPlacementStation : MonoBehaviour
{
    private const string NameSignRootName = "Assigned Name Sign";
    private const string NameSignTextName = "Assigned Name Text";

    [SerializeField] private string stationId;
    [SerializeField] private EquipmentCategory[] acceptedCategories =
    {
        EquipmentCategory.DesktopPc,
        EquipmentCategory.Printer,
        EquipmentCategory.AccessPoint,
        EquipmentCategory.Laptop
    };
    [SerializeField] private Transform[] placementAnchors;
    [SerializeField] private Transform[] desktopPlacementAnchors;
    [SerializeField] private Transform[] printerPlacementAnchors;
    [SerializeField] private Transform[] accessPointPlacementAnchors;
    [SerializeField] private Transform[] laptopPlacementAnchors;
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
    private readonly Dictionary<string, GameObject> _placedVisuals = new();
    private bool _playerInRange;
    private int _nextPlacementNumber = 1;
    private TssTrainingSession _session;

    public string StationId => stationId;
    internal bool IsPlayerInRange => _playerInRange;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(stationId))
            stationId = name;

        if (!placedParent)
            placedParent = transform;

        if (placementAnchors == null || placementAnchors.Length == 0)
            placementAnchors = new[] { transform };

        RefreshPlacementTriggers();
    }

    private void OnEnable()
    {
        SubscribeSession();
    }

    private void Start()
    {
        SubscribeSession();
    }

    private void OnDisable()
    {
        if (_session)
            _session.StateChanged -= HandleSessionStateChanged;
        _session = null;
    }

    private void SubscribeSession()
    {
        if (_session)
            return;

        _session = TssTrainingSession.Instance;
        if (!_session)
            return;

        _session.StateChanged += HandleSessionStateChanged;
        HandleSessionStateChanged();
    }

    private void HandleSessionStateChanged()
    {
        RefreshPlacementTriggers();
        foreach (var pair in _placedVisuals)
        {
            if (!pair.Value)
                continue;

            var deviceName = GetDeviceName(pair.Key);
            UpdateNameSign(pair.Key, deviceName);
            pair.Value.GetComponentInChildren<TssRackDeviceNamePlaque>(true)?.RefreshCurrentName();
            foreach (var endpoint in pair.Value.GetComponentsInChildren<TssPortEndpoint>(true))
                endpoint.SetDeviceName(deviceName);
        }
    }

    internal bool IsAnchorOccupied(Transform anchor)
    {
        if (!anchor)
            return false;

        foreach (var visual in _placedVisuals.Values)
        {
            if (visual && Vector3.Distance(visual.transform.position, anchor.position) < 0.01f)
                return true;
        }

        return false;
    }

    private void RefreshPlacementTriggers()
    {
        foreach (var trigger in GetComponentsInChildren<EndpointPlacementTrigger>(true))
            trigger.RefreshAvailability();
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

    internal bool CanPlaceCategory(EquipmentCategory category)
    {
        foreach (var acceptedCategory in acceptedCategories)
        {
            if (acceptedCategory != category)
                continue;

            var categoryAnchors = GetCategoryAnchors(category);
            return HasAnchor(categoryAnchors)
                ? GetFreeAnchor(categoryAnchors) != null
                : GetFreeAnchor(placementAnchors) != null;
        }

        return false;
    }

    internal void ClickPlacementTrigger(EquipmentCategory category)
    {
        if (CanPlaceCategory(category))
            TryOpenPlacementPrompt();
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
            TssRuntimeUi.Instance.ShowNamePrompt($"Name {equipment.displayName}", string.Empty, deviceName =>
            {
                PlaceHeld(deviceName);
            }, this);
            return;
        }

        PlaceHeld(string.Empty);
    }

    private void PlaceHeld(string deviceName)
    {
        var equipment = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        var anchor = GetFreeAnchor(equipment);
        if (!anchor || !TssTrainingSession.Instance)
            return;

        if (!TssTrainingSession.Instance.TryPlaceHeldEndpoint(this, deviceName, out var record, out var message))
        {
            TssRuntimeUi.Instance?.ShowHudMessage(message);
            return;
        }

        CreatePlacedVisual(anchor, record);
        TssTrainingSession.Instance.NotifyStateChanged();
        TssRuntimeUi.Instance?.ShowHudMessage(message);
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
            var ownerPrefix = TssTrainingSession.GetEndpointOwnerPrefix(record.PlacementId);
            TssPortEndpointBinder.ConfigureEndpoints(visual, ownerPrefix, record.Equipment, record.DeviceName);
            TssRackDeviceNamePlaque.Ensure(visual)?.Configure(ownerPrefix, record.DeviceName);

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
        var canAccessComputer = equipment.category == EquipmentCategory.DesktopPc
            || equipment.category == EquipmentCategory.Laptop
            || equipment.category == EquipmentCategory.Printer;
        Action accessComputer = canAccessComputer
            ? () =>
            {
                if (session.HeldCable && TryEnterPlacedCableView(placementId))
                    return;

                ShowComputerForPlacement(placementId, computerTitle);
            }
            : null;
        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowEndpointMenu(
                $"{StationId} {equipment.displayName}",
                true,
                true,
                canAccessComputer,
                deviceName =>
                {
                    if (TssTrainingSession.Instance.RenameEndpoint(placementId, deviceName))
                    {
                        UpdateNameSign(placementId, deviceName);
                        if (_placedVisuals.TryGetValue(placementId, out var visual) && visual)
                        {
                            foreach (var ep in visual.GetComponentsInChildren<TssPortEndpoint>(true))
                                ep.SetDeviceName(deviceName);
                        }
                    }
                },
                () => PickupPlaced(placementId, equipment),
                accessComputer,
                owner,
                currentName);
            return;
        }

        PickupPlaced(placementId, equipment);
    }

    public bool TryGetPlacedEndpoint(string placementId, out TssPortEndpoint endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(placementId)
            || !_placedVisuals.TryGetValue(placementId, out var visual)
            || !visual)
            return false;

        endpoint = visual.GetComponentInChildren<TssPortEndpoint>(true);
        return endpoint;
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

    public void PickupPlaced(string placementId, EquipmentDefinition equipment)
    {
        _placedVisuals.TryGetValue(placementId, out var visual);
        if (visual)
            visual.SetActive(false);

        if (!TssTrainingSession.Instance.TryPickupEndpoint(placementId, equipment, out var message))
        {
            if (visual)
                visual.SetActive(true);
            TssRuntimeUi.Instance?.ShowHudMessage(message);
            return;
        }

        if (visual)
            Destroy(visual);

        _placedVisuals.Remove(placementId);
        RefreshPlacementTriggers();
        TssRuntimeUi.Instance?.ShowHudMessage(message);
    }

    private void ShowComputerForPlacement(string placementId, string title)
    {
        if (_placedVisuals.TryGetValue(placementId, out var visual) && visual && TssDeviceSessionController.Instance)
        {
            TssDeviceSessionController.Instance.OpenDesktopSession(visual);
            return;
        }

        Debug.LogWarning($"Unable to open desktop session for placed computer '{title}'.");
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
        return equipment ? GetCategoryAnchors(equipment.category) : null;
    }

    private IReadOnlyList<Transform> GetCategoryAnchors(EquipmentCategory category)
    {
        return category switch
        {
            EquipmentCategory.DesktopPc => desktopPlacementAnchors,
            EquipmentCategory.Printer => printerPlacementAnchors,
            EquipmentCategory.AccessPoint => accessPointPlacementAnchors,
            EquipmentCategory.Laptop => laptopPlacementAnchors,
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

}

