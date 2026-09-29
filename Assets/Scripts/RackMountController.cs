using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
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

    private readonly HashSet<int> _reserved = new();
    private readonly HashSet<int> _occupied = new();
    private readonly HashSet<int> _rearOccupied = new();
    private readonly Dictionary<int, GameObject> _slots = new();
    private readonly Dictionary<int, InstalledRackItem> _installedItems = new();
    private TssTrainingSession _session;
    private bool _playerInRange;
    private Material _slotMaterial;
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
    }

    private void Start()
    {
        SubscribeSession();
    }

    private void Update()
    {
        if (!_playerInRange || !TssTrainingSession.Instance)
            return;

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

        return equipment.useRearRackPlacement
            ? RackSpanValidator.CanPlace(startingU, equipment.rackUnits, lowestU, highestU, null, _rearOccupied)
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
        TssPortEndpointBinder.ConfigureEndpoints(instance, TssTrainingSession.GetRackOwnerPrefix(RackId, startingU), equipment);

        foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
        {
            if (collider.GetComponentInParent<TssPortEndpoint>())
                continue;

            collider.enabled = false;
        }

        var grabTarget = CreateInstalledGrabTarget(equipment, startingU);
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

        if (!TssTrainingSession.Instance)
            return;

        var held = TssTrainingSession.Instance.HeldItem;
        if (!held)
            return;

        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowNamePrompt($"Name {held.displayName}", string.Empty, deviceName =>
            {
                TssTrainingSession.Instance.TryInstallHeld(this, startingU, deviceName, out _);
            });
            return;
        }

        TssTrainingSession.Instance.TryInstallHeld(this, startingU, held.displayName, out _);
    }

    public void ClickInstalled(int startingU)
    {
        if (TssRuntimeUi.Instance && TssRuntimeUi.Instance.IsGameplayInputBlocked)
            return;

        if (!TssTrainingSession.Instance || !_installedItems.TryGetValue(startingU, out var item))
            return;

        if (TssRuntimeUi.Instance)
        {
            TssRuntimeUi.Instance.ShowPlacementCorrection(
                $"{RackId} U{startingU}",
                GetInstalledDeviceName(startingU),
                deviceName => TssTrainingSession.Instance.RenameRackInstalled(RackId, startingU, deviceName),
                () => PickupInstalled(startingU, item));
            return;
        }

        PickupInstalled(startingU, item);
    }

    private void PickupInstalled(int startingU, InstalledRackItem item)
    {
        if (!TssTrainingSession.Instance.TryPickupInstalled(this, item.Equipment, startingU, item.RackUnits, out _))
            return;

        SetOccupied(item.Equipment, startingU, item.RackUnits, false);

        if (item.Visual)
            Destroy(item.Visual);

        if (item.GrabTarget)
            Destroy(item.GrabTarget);

        _installedItems.Remove(startingU);
        RefreshHighlights();
    }

    private string GetInstalledDeviceName(int startingU)
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

    private GameObject CreateInstalledGrabTarget(EquipmentDefinition equipment, int startingU)
    {
        var grabTarget = new GameObject($"Grab Target U{startingU:00}");
        grabTarget.transform.SetParent(mountRoot, false);
        grabTarget.transform.localPosition = GetInstalledLocalPosition(equipment, startingU);
        grabTarget.transform.localRotation = Quaternion.identity;

        var collider = grabTarget.AddComponent<BoxCollider>();
        collider.size = new Vector3(slotLocalSize.x, Mathf.Max(slotLocalSize.y, uSpacingMeters * equipment.rackUnits), Mathf.Max(slotLocalSize.z, 0.08f));
        grabTarget.AddComponent<RackInstalledEquipmentTarget>().Initialize(this, startingU);
        return grabTarget;
    }

    private void RefreshHighlights()
    {
        var held = TssTrainingSession.Instance ? TssTrainingSession.Instance.HeldItem : null;
        foreach (var pair in _slots)
        {
            pair.Value.transform.localPosition = GetSlotLocalPosition(held, pair.Key);
            pair.Value.SetActive(_playerInRange && CanInstall(held, pair.Key));
        }
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
        return GetULocalPosition(u) + (equipment && equipment.useRearRackPlacement ? rearSlotLocalOffset : slotLocalOffset);
    }

    private Vector3 GetInstalledLocalOffset(EquipmentDefinition equipment)
    {
        return equipment && equipment.useRearRackPlacement ? rearInstalledLocalOffset : installedLocalOffset;
    }

    private void SetOccupied(EquipmentDefinition equipment, int startingU, int rackUnits, bool occupied)
    {
        for (var u = startingU; u < startingU + rackUnits; u++)
        {
            SetOccupied(_occupied, u, occupied);

            if (equipment.useRearRackPlacement || equipment.category == EquipmentCategory.Server)
                SetOccupied(_rearOccupied, u, occupied);
        }
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
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.wasPressedThisFrame)
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
