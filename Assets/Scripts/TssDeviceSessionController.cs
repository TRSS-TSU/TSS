using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssDeviceSessionController : MonoBehaviour
{
    public static TssDeviceSessionController Instance { get; private set; }

    [Header("Session Canvas Prefabs")]
    [SerializeField] private GameObject terminalCanvasPrefab;
    [SerializeField] private GameObject desktopCanvasPrefab;

    [Header("First Person Controls Prefabs")]
    [SerializeField] private GameObject terminalControlsPrefab;
    [SerializeField] private GameObject desktopControlsPrefab;

    [Header("Scene Presenters (Optional, if placed in scene)")]
    [SerializeField] private TssTerminalSessionPresenter terminalPresenter;
    [SerializeField] private TssDesktopSessionPresenter desktopPresenter;

    public bool IsSessionOpen { get; private set; }

    public sealed class DeviceContext
    {
        public GameObject Root;
        public string DeviceName;
        public EquipmentCategory Category;
        public string Key;
        public RackMountController Rack;
        public int StartingU;
        public EquipmentDefinition Equipment;
        public EndpointPlacementStation PlacementStation;
        public string PlacementId;
    }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolvePresenters();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolvePresenters()
    {
        if (!terminalPresenter)
            terminalPresenter = FindFirstObjectByType<TssTerminalSessionPresenter>(FindObjectsInactive.Include);

        if (!desktopPresenter)
            desktopPresenter = FindFirstObjectByType<TssDesktopSessionPresenter>(FindObjectsInactive.Include);
    }

    public GameObject GetControlsPrefabFor(GameObject deviceObject)
    {
        var ctx = ResolveContext(deviceObject);
        if (ctx != null && (ctx.Category == EquipmentCategory.Switch || ctx.Category == EquipmentCategory.Router))
            return terminalControlsPrefab;

        return desktopControlsPrefab;
    }

    public DeviceContext ResolveContext(GameObject deviceObject)
    {
        if (!deviceObject)
        {
            var active = TssObjectInteractable.ActiveInteractable;
            if (active)
                deviceObject = active.gameObject;
        }

        if (!deviceObject)
            return null;

        var root = deviceObject.transform.root ? deviceObject.transform.root.gameObject : deviceObject;
        var session = TssTrainingSession.Instance;

        // 1. Try EndpointPlacedEquipmentTarget
        var placedTarget = deviceObject.GetComponentInParent<EndpointPlacedEquipmentTarget>();
        if (placedTarget)
        {
            var eq = placedTarget.Equipment;
            var pid = placedTarget.PlacementId;
            var station = placedTarget.Station;

            var devName = eq ? eq.displayName : "Placed Device";
            if (session != null && !string.IsNullOrEmpty(pid))
            {
                foreach (var rec in session.EndpointPlacements)
                {
                    if (rec.PlacementId == pid && !string.IsNullOrWhiteSpace(rec.DeviceName))
                    {
                        devName = rec.DeviceName.Trim();
                        break;
                    }
                }
            }

            var cat = eq ? eq.category : EquipmentCategory.DesktopPc;
            return new DeviceContext
            {
                Root = placedTarget.gameObject,
                DeviceName = devName,
                Category = cat,
                Key = !string.IsNullOrEmpty(pid) ? TssTrainingSession.GetEndpointOwnerPrefix(pid) : devName,
                PlacementStation = station,
                PlacementId = pid,
                Equipment = eq
            };
        }

        // 2. Try RackMountController by visual matching
        foreach (var rack in FindObjectsByType<RackMountController>(FindObjectsSortMode.None))
        {
            if (rack.TryGetInstalledAtVisual(deviceObject, out var u, out var eq))
            {
                var devName = rack.GetInstalledDeviceName(u);
                return new DeviceContext
                {
                    Root = deviceObject,
                    DeviceName = !string.IsNullOrWhiteSpace(devName) ? devName : $"{rack.RackId} U{u}",
                    Category = eq ? eq.category : EquipmentCategory.Switch,
                    Key = TssTrainingSession.GetRackOwnerPrefix(rack.RackId, u),
                    Rack = rack,
                    StartingU = u,
                    Equipment = eq
                };
            }
        }

        // 3. Try RackInstalledEquipmentTarget
        var rackTarget = deviceObject.GetComponentInParent<RackInstalledEquipmentTarget>();
        if (rackTarget && rackTarget.Rack)
        {
            var rackId = rackTarget.Rack.RackId;
            var u = rackTarget.StartingU;
            var devName = rackTarget.Rack.GetInstalledDeviceName(u);
            var cat = EquipmentCategory.Switch;
            EquipmentDefinition eq = null;

            if (session != null)
            {
                foreach (var rec in session.Installed)
                {
                    if (rec.RackId == rackId && rec.StartingU == u)
                    {
                        if (rec.Equipment)
                        {
                            cat = rec.Equipment.category;
                            eq = rec.Equipment;
                        }
                        if (!string.IsNullOrWhiteSpace(rec.DeviceName))
                            devName = rec.DeviceName.Trim();
                        break;
                    }
                }
            }

            return new DeviceContext
            {
                Root = rackTarget.gameObject,
                DeviceName = !string.IsNullOrWhiteSpace(devName) ? devName : $"{rackId} U{u}",
                Category = cat,
                Key = $"{rackId}_U{u}",
                Rack = rackTarget.Rack,
                StartingU = u,
                Equipment = eq
            };
        }

        // 3. Try TssPortEndpoint on device
        var ep = deviceObject.GetComponentInChildren<TssPortEndpoint>(true);
        if (ep && session != null)
        {
            var epId = ep.EndpointId;
            var devName = TssPhysicalConnectionPanel.DeviceName(epId, session);
            var cat = DeduceCategoryFromName(devName, deviceObject.name);
            return new DeviceContext
            {
                Root = root,
                DeviceName = !string.IsNullOrWhiteSpace(devName) ? devName : root.name,
                Category = cat,
                Key = !string.IsNullOrEmpty(epId) ? epId : root.name
            };
        }

        // 4. Fallback heuristic
        var fallbackCat = DeduceCategoryFromName(root.name, deviceObject.name);
        return new DeviceContext
        {
            Root = root,
            DeviceName = root.name,
            Category = fallbackCat,
            Key = root.name
        };
    }

    private static EquipmentCategory DeduceCategoryFromName(string primary, string secondary)
    {
        var s = $"{primary} {secondary}".ToLowerInvariant();
        if (s.Contains("router")) return EquipmentCategory.Router;
        if (s.Contains("switch") || s.Contains("2960") || s.Contains("3550")) return EquipmentCategory.Switch;
        if (s.Contains("server")) return EquipmentCategory.Server;
        if (s.Contains("printer")) return EquipmentCategory.Printer;
        return EquipmentCategory.DesktopPc;
    }

    public void OpenSessionForDevice(GameObject deviceObject, bool preferTerminal)
    {
        var ctx = ResolveContext(deviceObject);
        if (ctx == null)
            return;

        var isTerminal = ctx.Category == EquipmentCategory.Switch || ctx.Category == EquipmentCategory.Router || preferTerminal;
        if (isTerminal)
            OpenTerminalSession(deviceObject, ctx);
        else
            OpenDesktopSession(deviceObject, ctx);
    }

    public void OpenTerminalSession(GameObject deviceObject, DeviceContext ctx = null)
    {
        ResolvePresenters();
        if (!terminalPresenter && terminalCanvasPrefab)
        {
            var instance = Instantiate(terminalCanvasPrefab);
            terminalPresenter = instance.GetComponentInChildren<TssTerminalSessionPresenter>(true);
        }

        if (!terminalPresenter)
        {
            Debug.LogError("TssDeviceSessionController: Terminal session canvas presenter not found.");
            return;
        }

        if (ctx == null)
            ctx = ResolveContext(deviceObject);

        var state = new TssDeviceLogicalState(ctx.Key, ctx.Category, ctx.DeviceName);
        var endpoints = deviceObject ? deviceObject.GetComponentsInChildren<TssPortEndpoint>(true) : Array.Empty<TssPortEndpoint>();
        state.RefreshPhysicalConnectivity(endpoints, TssTrainingSession.Instance);
        var network = TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;
        if (network != null)
        {
            var device = network.GetOrCreateDevice(ctx.Key, ctx.Category, ctx.DeviceName);
            foreach (var legacy in state.Interfaces)
            {
                if (!device.Interfaces.TryGetValue(legacy.InterfaceName, out var iface))
                    device.Interfaces[legacy.InterfaceName] = iface = new NetworkInterfaceState { Name = legacy.InterfaceName, AdminUp = ctx.Category == EquipmentCategory.Switch };
                iface.EndpointId = legacy.EndpointId;
                iface.CarrierUp = legacy.IsPhysicalLinkUp;
            }
        }

        terminalPresenter.gameObject.SetActive(true);
        terminalPresenter.Initialize(state, deviceObject, CloseCurrentSession, ctx.Key);
        IsSessionOpen = true;

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PushExternalGameplayLock(this);
    }

    public void OpenDesktopSession(GameObject deviceObject, DeviceContext ctx = null)
    {
        ResolvePresenters();
        if (!desktopPresenter && desktopCanvasPrefab)
        {
            var instance = Instantiate(desktopCanvasPrefab);
            desktopPresenter = instance.GetComponentInChildren<TssDesktopSessionPresenter>(true);
        }

        if (!desktopPresenter)
        {
            Debug.LogError("TssDeviceSessionController: Desktop session canvas presenter not found.");
            return;
        }

        if (ctx == null)
            ctx = ResolveContext(deviceObject);

        var state = new TssDeviceLogicalState(ctx.Key, ctx.Category, ctx.DeviceName);
        var endpoints = deviceObject ? deviceObject.GetComponentsInChildren<TssPortEndpoint>(true) : Array.Empty<TssPortEndpoint>();
        state.RefreshPhysicalConnectivity(endpoints, TssTrainingSession.Instance);

        var network = TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;
        if (network != null)
        {
            var endpoint = network.GetOrCreateEndpoint(ctx.Key, ctx.DeviceName);
            endpoint.Layer1Connected = state.Interfaces.Count > 0 && state.Interfaces[0].IsPhysicalLinkUp;
            endpoint.Layer2Connected = endpoint.Layer1Connected;
        }

        desktopPresenter.gameObject.SetActive(true);
        desktopPresenter.Initialize(state, deviceObject, CloseCurrentSession, ctx.Key);
        IsSessionOpen = true;

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PushExternalGameplayLock(this);
    }

    public void CloseCurrentSession()
    {
        if (terminalPresenter && terminalPresenter.gameObject.activeSelf)
            terminalPresenter.gameObject.SetActive(false);

        if (desktopPresenter && desktopPresenter.gameObject.activeSelf)
            desktopPresenter.gameObject.SetActive(false);

        IsSessionOpen = false;

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PopExternalGameplayLock(this);
    }
}
