using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed class RackMountController : MonoBehaviour
{
    [SerializeField] private string rackId = "Rack01";
    [SerializeField] private int lowestU = 1;
    [SerializeField] private int highestU = 40;
    [SerializeField] private int firstInstallableU = 3;
    [SerializeField] private int lastInstallableU = 39;
    [SerializeField] private int[] reservedUPositions = { 40 };
    [SerializeField] private Transform mountRoot;
    [SerializeField] private Transform u1Anchor;
    [SerializeField] private float uSpacingMeters = 0.04445f;
    [SerializeField] private Vector3 installedLocalOffset = new(0f, 0f, -0.4f);
    [SerializeField] private Vector3 rearInstalledLocalOffset = new(0f, 0f, -0.56f);
    [SerializeField] private Vector3 installedLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 slotLocalOffset = new(0f, 0f, -0.47f);
    [SerializeField] private Vector3 rearSlotLocalOffset = new(0f, 0f, -0.56f);
    [SerializeField] private Vector3 slotLocalSize = new(0.55f, 0.035f, 0.035f);
    [SerializeField] private Color validSlotColor = new(0.2f, 1f, 0.35f, 0.55f);
    [SerializeField] private Color hoveredSlotColor = new(0.1f, 0.45f, 1f, 0.8f);

    private readonly HashSet<int> _reserved = new();
    private readonly HashSet<int> _occupied = new();
    private readonly HashSet<int> _rearOccupied = new();
    private readonly Dictionary<int, GameObject> _slots = new();
    private readonly Dictionary<int, InstalledRackItem> _installedItems = new();
    private TssTrainingSession _session;
    private bool _playerInRange;
    private Material _slotMaterial;
    private Material _hoveredSlotMaterial;
    private int _hoveredU;
    private Camera _mainCamera;

    public string RackId => rackId;

    private void Awake()
    {
        if (!mountRoot)
            mountRoot = transform;
        if (!u1Anchor)
            u1Anchor = mountRoot;

        _mainCamera = Camera.main;

        _reserved.Clear();
        foreach (var u in reservedUPositions)
            _reserved.Add(u);

        CreateSlots();
        ApplyScenarioConfig();
    }

    private void OnEnable()
    {
        SubscribeSession();
    }

    private void OnDisable()
    {
        if (_session)
            _session.StateChanged -= HandleSessionStateChanged;
        _session = null;
        ClearHoveredSlot();
    }

    private void OnDestroy()
    {
        if (_slotMaterial)
            Destroy(_slotMaterial);
        if (_hoveredSlotMaterial)
            Destroy(_hoveredSlotMaterial);
    }

    private void Start()
    {
        SubscribeSession();
    }

    private void Update()
    {
        if (!IsPlayerInRange() || !TssTrainingSession.Instance)
        {
            ClearHoveredSlot();
            return;
        }

        UpdateSlotHover();

        if (!WasPrimaryPressed(out var screenPosition))
            return;

        var rayCamera = _mainCamera ? _mainCamera : Camera.main;
        if (!rayCamera)
            return;

        var ray = rayCamera.ScreenPointToRay(screenPosition);
        var held = TssTrainingSession.Instance.HeldItem;
        RackUClickTarget clickedSlot = null;
        RackInstalledEquipmentTarget clickedInstalled = null;
        var closestSlotDistance = float.PositiveInfinity;
        var closestInstalledDistance = float.PositiveInfinity;

        foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide))
        {
            if (held)
            {
                var target = hit.collider.GetComponentInParent<RackUClickTarget>();
                if (!target || target.Rack != this || hit.distance >= closestSlotDistance)
                    continue;

                clickedSlot = target;
                closestSlotDistance = hit.distance;
            }
            else
            {
                var target = hit.collider.GetComponentInParent<RackInstalledEquipmentTarget>();
                if (!target || target.Rack != this || hit.distance >= closestInstalledDistance)
                    continue;

                clickedInstalled = target;
                closestInstalledDistance = hit.distance;
            }
        }

        if (clickedSlot)
            clickedSlot.Click();
        else if (clickedInstalled)
            clickedInstalled.Click();
    }

    private bool IsPlayerInRange()
    {
        if (_playerInRange)
            return true;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player && Vector3.Distance(player.transform.position, transform.position) <= 3.25f)
            return true;

        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playerInRange = true;
        RefreshHighlights();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        _playerInRange = false;
        RefreshHighlights();
    }

    public bool CanInstall(EquipmentDefinition equipment, int startingU)
    {
        if (!equipment)
            return false;

        if (IsFrontSlotBlocked(equipment, startingU) || _reserved.Contains(startingU))
            return false;

        if (IsDualSidedRackEquipment(equipment))
        {
            return RackSpanValidator.CanPlace(startingU, equipment.rackUnits, firstInstallableU, lastInstallableU, _reserved, _occupied)
                && RackSpanValidator.CanPlace(startingU, equipment.rackUnits, lowestU, highestU, _reserved, _rearOccupied);
        }

        return equipment.useRearRackPlacement
            ? RackSpanValidator.CanPlace(startingU, equipment.rackUnits, lowestU, highestU, _reserved, _rearOccupied)
            : RackSpanValidator.CanPlace(startingU, equipment.rackUnits, firstInstallableU, lastInstallableU, _reserved, _occupied);
    }

    public bool TryInstall(EquipmentDefinition equipment, int startingU, out string message)
    {
        if (!CanInstall(equipment, startingU))
        {
            message = "That rack position is not available.";
            return false;
        }

        var prefab = equipment.rackPrefab;
        if (!prefab)
        {
            message = "Equipment has no rack model.";
            return false;
        }

        var instance = Instantiate(prefab, mountRoot);
        instance.name = $"{equipment.displayName} U{startingU}";
        instance.transform.localPosition = GetInstalledLocalPosition(equipment, startingU);
        instance.transform.localRotation = Quaternion.Euler(installedLocalEuler + equipment.rackLocalEuler);
        instance.transform.localScale = equipment.rackLocalScale;
        var ownerPrefix = TssTrainingSession.GetRackOwnerPrefix(RackId, startingU);
        TssPortEndpointBinder.ConfigureEndpoints(instance, ownerPrefix, equipment);
        TssRackDeviceNamePlaque.Ensure(instance)?.Configure(ownerPrefix, equipment.displayName);

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            if (collider.GetComponentInParent<TssPortEndpoint>())
                continue;

            collider.enabled = false;
        }

        var grabTarget = CreateInstalledGrabTarget(equipment, startingU, instance);
        _installedItems[startingU] = new InstalledRackItem(equipment, instance, grabTarget, equipment.rackUnits);

        SetOccupied(equipment, startingU, equipment.rackUnits, true);

        message = $"Installed {equipment.displayName} at {RackId} U{startingU}";
        RefreshHighlights();
        return true;
    }

    public void ClickU(int startingU)
    {
        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
            return;

        var held = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        if (IsFrontSlotBlocked(held, startingU) || _reserved.Contains(startingU))
            return;

        if (!TssTrainingSession.Instance)
            return;

        if (!held)
            return;

        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowNamePrompt($"Name {held.displayName}", string.Empty, deviceName =>
            {
                if (TssTrainingSession.Instance.TryInstallHeld(this, startingU, deviceName, out var message))
                {
                    SetInstalledDeviceName(startingU, deviceName);
                    TssRuntimeUi.Instance.ShowHudMessage(message);
                }
                else
                {
                    TssRuntimeUi.Instance.ShowHudMessage(message);
                }
            });
            return;
        }

        if (TssTrainingSession.Instance.TryInstallHeld(this, startingU, string.Empty, out var fallbackMessage))
        {
            SetInstalledDeviceName(startingU, string.Empty);
            TssRuntimeUi.Instance?.ShowHudMessage(fallbackMessage);
        }
        else
        {
            TssRuntimeUi.Instance?.ShowHudMessage(fallbackMessage);
        }
    }

    public void SetInstalledDeviceName(int startingU, string name)
    {
        if (_installedItems.TryGetValue(startingU, out var item) && item.Visual)
        {
            foreach (var ep in item.Visual.GetComponentsInChildren<TssPortEndpoint>(true))
                ep.SetDeviceName(name);
        }
    }

    public void ClickInstalled(int startingU)
    {
        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
            return;

        if (!_installedItems.TryGetValue(startingU, out var item))
            return;

        if (TryEnterInstalledCableView(item))
            return;

        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowPlacementCorrection(
                $"{RackId} U{startingU}",
                GetInstalledDeviceName(startingU),
                deviceName =>
                {
                    if (TssTrainingSession.Instance.RenameRackInstalled(RackId, startingU, deviceName))
                        SetInstalledDeviceName(startingU, deviceName);
                },
                () => PickupInstalled(startingU, item),
                () => TryEnterInstalledCableView(item));
            return;
        }

        PickupInstalled(startingU, item);
    }

    public void PickupInstalled(int startingU)
    {
        if (_installedItems.TryGetValue(startingU, out var item))
            PickupInstalled(startingU, item);
    }

    public bool TryGetInstalledAtVisual(GameObject visual, out int startingU, out EquipmentDefinition equipment)
    {
        foreach (var pair in _installedItems)
        {
            if (pair.Value.Visual == visual || (visual != null && pair.Value.Visual != null && visual.transform.IsChildOf(pair.Value.Visual.transform)))
            {
                startingU = pair.Key;
                equipment = pair.Value.Equipment;
                return true;
            }
        }
        startingU = 0;
        equipment = null;
        return false;
    }

    private static bool TryEnterInstalledCableView(InstalledRackItem item)
    {
        if (!item.Visual)
            return false;

        var interactable = item.Visual.GetComponentInChildren<TssObjectInteractable>(true);
        if (!interactable)
            return false;

        interactable.EnterFirstPerson();
        return true;
    }

    private void PickupInstalled(int startingU, InstalledRackItem item)
    {
        if (!TssTrainingSession.Instance.TryPickupInstalled(this, item.Equipment, startingU, item.RackUnits, out var message))
        {
            TssRuntimeUi.Instance?.ShowHudMessage(message);
            return;
        }

        SetOccupied(item.Equipment, startingU, item.RackUnits, false);

        if (item.Visual)
            Destroy(item.Visual);

        if (item.GrabTarget)
            Destroy(item.GrabTarget);

        _installedItems.Remove(startingU);
        RefreshHighlights();
        TssRuntimeUi.Instance?.ShowHudMessage(message);
    }

    public string GetInstalledDeviceName(int startingU)
    {
        var session = TssTrainingSession.Instance;
        if (!session)
            return string.Empty;

        foreach (var record in session.Installed)
        {
            if (record.RackId == RackId && record.StartingU == startingU)
                return record.DeviceName;
        }

        return string.Empty;
    }

    private void ApplyScenarioConfig()
    {
        var session = TssTrainingSession.Instance;
        if (!session || !session.Scenario || session.Scenario.racks == null)
            return;

        foreach (var config in session.Scenario.racks)
        {
            if (config == null || config.rackId != rackId)
                continue;

            lowestU = config.lowestU;
            highestU = config.highestU;
            firstInstallableU = config.firstInstallableU;
            lastInstallableU = config.lastInstallableU;
            _reserved.Clear();
            if (config.reservedUPositions != null)
            {
                foreach (var u in config.reservedUPositions)
                    _reserved.Add(u);
            }
            RefreshHighlights();
            return;
        }
    }

    private void HandleSessionStateChanged()
    {
        ApplyScenarioConfig();
        foreach (var item in _installedItems)
        {
            SetInstalledDeviceName(item.Key, GetInstalledDeviceName(item.Key));
            item.Value.Visual?.GetComponentInChildren<TssRackDeviceNamePlaque>(true)?.RefreshCurrentName();
        }
        RefreshHighlights();
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

    private void CreateSlots()
    {
        _slotMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _slotMaterial.color = validSlotColor;
        _slotMaterial.SetFloat("_Surface", 1f);
        _hoveredSlotMaterial = new Material(_slotMaterial);
        _hoveredSlotMaterial.color = hoveredSlotColor;

        for (var u = lowestU; u <= highestU; u++)
        {
            var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = $"Install Slot U{u:00}";
            slot.transform.SetParent(transform, false);
            slot.transform.localPosition = GetSlotLocalPosition(null, u);
            slot.transform.localRotation = Quaternion.identity;
            slot.transform.localScale = slotLocalSize;
            slot.GetComponent<Renderer>().sharedMaterial = _slotMaterial;
            slot.AddComponent<RackUClickTarget>().Initialize(this, u);
            slot.SetActive(false);
            _slots[u] = slot;
        }
    }

    private GameObject CreateInstalledGrabTarget(EquipmentDefinition equipment, int startingU, GameObject visualInstance)
    {
        var grabTarget = new GameObject($"Grab Target U{startingU:00}");
        grabTarget.transform.SetParent(mountRoot, false);

        var rends = visualInstance != null ? visualInstance.GetComponentsInChildren<Renderer>(true) : null;
        Vector3 colliderSize;
        if (rends != null && rends.Length > 0)
        {
            var bounds = rends[0].bounds;
            for (var i = 1; i < rends.Length; i++)
                bounds.Encapsulate(rends[i].bounds);

            grabTarget.transform.position = bounds.center;
            grabTarget.transform.rotation = Quaternion.identity;

            var extents = bounds.extents;
            colliderSize = new Vector3(
                Mathf.Max(slotLocalSize.x, extents.x * 2f),
                Mathf.Max(slotLocalSize.y, Mathf.Max(uSpacingMeters * equipment.rackUnits, extents.y * 2f)),
                Mathf.Max(slotLocalSize.z, Mathf.Max(0.16f, extents.z * 2f)));
        }
        else
        {
            grabTarget.transform.localPosition = GetInstalledLocalPosition(equipment, startingU);
            grabTarget.transform.localRotation = Quaternion.identity;
            colliderSize = new Vector3(slotLocalSize.x, Mathf.Max(slotLocalSize.y, uSpacingMeters * equipment.rackUnits), Mathf.Max(slotLocalSize.z, 0.08f));
        }

        Physics.SyncTransforms();
        var collider = grabTarget.AddComponent<BoxCollider>();
        collider.size = colliderSize;
        grabTarget.AddComponent<RackInstalledEquipmentTarget>().Initialize(this, startingU);
        return grabTarget;
    }

    private void RefreshHighlights()
    {
        var held = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        foreach (var pair in _slots)
        {
            pair.Value.transform.localPosition = GetSlotLocalPosition(held, pair.Key);
            pair.Value.SetActive(IsPlayerInRange() && CanInstall(held, pair.Key));
        }

        if (_hoveredU != 0 && (!_slots.TryGetValue(_hoveredU, out var hoveredSlot) || !hoveredSlot.activeSelf))
            ClearHoveredSlot();
    }

    private void UpdateSlotHover()
    {
        var held = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        if (!held || !_mainCamera)
        {
            ClearHoveredSlot();
            return;
        }

        var ray = _mainCamera.ScreenPointToRay(GetPointerPosition());
        RackUClickTarget hoveredTarget = null;
        var closestDistance = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide))
        {
            var target = hit.collider.GetComponentInParent<RackUClickTarget>();
            if (!target || target.Rack != this || hit.distance >= closestDistance)
                continue;

            hoveredTarget = target;
            closestDistance = hit.distance;
        }

        SetHoveredSlot(hoveredTarget ? hoveredTarget.U : 0);
    }

    private void SetHoveredSlot(int u)
    {
        if (_hoveredU == u)
        {
            if (u != 0)
                PositionHoverLabel(u);
            return;
        }

        if (_slots.TryGetValue(_hoveredU, out var previousSlot))
            previousSlot.GetComponent<Renderer>().sharedMaterial = _slotMaterial;

        _hoveredU = u;
        if (!_slots.TryGetValue(u, out var hoveredSlot))
        {
            TssRuntimeUi.Instance?.HideRackSlotHover();
            return;
        }

        hoveredSlot.GetComponent<Renderer>().sharedMaterial = _hoveredSlotMaterial;
        PositionHoverLabel(u);
    }

    private void PositionHoverLabel(int u)
    {
        var held = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        var rear = held && held.useRearRackPlacement && !IsDualSidedRackEquipment(held);
        TssRuntimeUi.Instance?.ShowRackSlotHover(u, rear);
    }

    private void ClearHoveredSlot()
    {
        if (_slots.TryGetValue(_hoveredU, out var previousSlot))
            previousSlot.GetComponent<Renderer>().sharedMaterial = _slotMaterial;

        _hoveredU = 0;
        TssRuntimeUi.Instance?.HideRackSlotHover();
    }

    private static Vector3 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : Vector3.zero;
#else
        return Input.mousePosition;
#endif
    }

    private Vector3 GetULocalPosition(int u)
    {
        return (u1Anchor ? u1Anchor.localPosition : Vector3.zero) + Vector3.up * ((u - 1) * uSpacingMeters);
    }

    private Vector3 GetInstalledLocalPosition(EquipmentDefinition equipment, int u)
    {
        return GetULocalPosition(u) + GetInstalledLocalOffset(equipment) + equipment.rackLocalOffset;
    }

    private Vector3 GetSlotLocalPosition(EquipmentDefinition equipment, int u)
    {
        return GetULocalPosition(u) + (equipment && equipment.useRearRackPlacement && !IsDualSidedRackEquipment(equipment) ? rearSlotLocalOffset : slotLocalOffset);
    }

    private Vector3 GetInstalledLocalOffset(EquipmentDefinition equipment)
    {
        return equipment && equipment.useRearRackPlacement && !IsDualSidedRackEquipment(equipment) ? rearInstalledLocalOffset : installedLocalOffset;
    }

    private void SetOccupied(EquipmentDefinition equipment, int startingU, int rackUnits, bool occupied)
    {
        if (IsDualSidedRackEquipment(equipment))
        {
            for (var u = startingU; u < startingU + rackUnits; u++)
            {
                SetOccupied(_occupied, u, occupied);
                SetOccupied(_rearOccupied, u, occupied);
            }

            return;
        }

        var occupiedSet = equipment && equipment.useRearRackPlacement ? _rearOccupied : _occupied;
        for (var u = startingU; u < startingU + rackUnits; u++)
            SetOccupied(occupiedSet, u, occupied);
    }

    private static bool IsDualSidedRackEquipment(EquipmentDefinition equipment)
    {
        return equipment && equipment.category == EquipmentCategory.Server;
    }

    private static bool IsFrontSlotBlocked(EquipmentDefinition equipment, int startingU)
    {
        if (!equipment || (equipment.useRearRackPlacement && !IsDualSidedRackEquipment(equipment)))
            return false;

        return startingU == 30 || startingU == 31;
    }

    private static void SetOccupied(HashSet<int> occupiedSet, int u, bool occupied)
    {
        if (occupied)
            occupiedSet.Add(u);
        else
            occupiedSet.Remove(u);
    }

    private static bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") || other.GetComponentInParent<ThirdPersonController>();
    }

    private static bool WasPrimaryPressed(out Vector2 screenPosition)
    {
        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
        {
            screenPosition = default;
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current && EventSystem.current.IsPointerOverGameObject())
            {
                screenPosition = default;
                return false;
            }

            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.wasPressedThisFrame)
                    continue;

                if (EventSystem.current && EventSystem.current.IsPointerOverGameObject(touch.touchId.ReadValue()))
                    continue;

                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        screenPosition = default;
        return false;
#else
        screenPosition = Input.mousePosition;
        return Input.GetMouseButtonDown(0);
#endif
    }

    private readonly struct InstalledRackItem
    {
        public readonly EquipmentDefinition Equipment;
        public readonly GameObject Visual;
        public readonly GameObject GrabTarget;
        public readonly int RackUnits;

        public InstalledRackItem(EquipmentDefinition equipment, GameObject visual, GameObject grabTarget, int rackUnits)
        {
            Equipment = equipment;
            Visual = visual;
            GrabTarget = grabTarget;
            RackUnits = rackUnits;
        }
    }
}


