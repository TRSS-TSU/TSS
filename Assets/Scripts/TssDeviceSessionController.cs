using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class TssDeviceSessionController : MonoBehaviour
{
    public static TssDeviceSessionController Instance { get; private set; }

    [Header("Session Canvas Prefabs")]
    [SerializeField] private GameObject terminalCanvasPrefab;
    [SerializeField] private GameObject desktopCanvasPrefab;
    [SerializeField] private GameObject laptopCanvasPrefab;

    [Header("First Person Controls Prefabs")]
    [SerializeField] private GameObject terminalControlsPrefab;
    [SerializeField] private GameObject desktopControlsPrefab;
    [SerializeField] private GameObject laptopControlsPrefab;

    [Header("Scene Presenters (Optional, if placed in scene)")]
    [SerializeField] private TssTerminalSessionPresenter terminalPresenter;
    [SerializeField] private TssDesktopSessionPresenter desktopPresenter;
    [SerializeField] private TssDesktopSessionPresenter laptopPresenter;

    public bool IsSessionOpen { get; private set; }
    private string _lastWorkstationSourceKey;

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

        if (!laptopPresenter)
        {
            foreach (var presenter in FindObjectsByType<TssDesktopSessionPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (presenter.name.Contains("Laptop"))
                {
                    laptopPresenter = presenter;
                    break;
                }
        }
    }

    public GameObject GetControlsPrefabFor(GameObject deviceObject)
    {
        var ctx = ResolveContext(deviceObject);
        if (ctx != null && (ctx.Category == EquipmentCategory.Switch || ctx.Category == EquipmentCategory.Router))
            return terminalControlsPrefab;

        if (ctx != null && ctx.Category == EquipmentCategory.Laptop && laptopControlsPrefab)
            return laptopControlsPrefab;

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

        var endpoints = deviceObject ? deviceObject.GetComponentsInChildren<TssPortEndpoint>(true) : Array.Empty<TssPortEndpoint>();
        var network = TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;
        TssDeviceLogicalState state;
        if (network != null)
        {
            var device = network.GetOrCreateDevice(ctx.Key, ctx.Category, ctx.DeviceName);
            foreach (var endpoint in endpoints)
            {
                var name = SessionInterfaceName(endpoint, ctx.Category);
                if (!device.Interfaces.TryGetValue(name, out var iface))
                    device.Interfaces[name] = iface = new NetworkInterfaceState { Name = name, AdminUp = ctx.Category == EquipmentCategory.Switch };
                iface.EndpointId = endpoint.EndpointId;
                iface.CarrierUp = ctx.Category == EquipmentCategory.Router && name == "Fa0/1"
                    ? TssTrainingSession.Instance.IsProviderDemarcConnected(endpoint.EndpointId)
                    : TssTrainingSession.Instance.IsEndpointConnected(endpoint.EndpointId);
            }
            network.SetProviderDemarcConnected(ProviderDemarcConnected(network, TssTrainingSession.Instance));
            state = TssDeviceLogicalState.FromNetwork(device, TssTrainingSession.Instance);
        }
        else
        {
            state = new TssDeviceLogicalState(ctx.Key, ctx.Category, ctx.DeviceName);
            state.RefreshPhysicalConnectivity(endpoints, TssTrainingSession.Instance);
        }

        terminalPresenter.gameObject.SetActive(true);
        terminalPresenter.Initialize(state, deviceObject, CloseCurrentSession, ctx.Key);
        IsSessionOpen = true;

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PushExternalGameplayLock(this);
    }

    private static string SessionInterfaceName(TssPortEndpoint endpoint, EquipmentCategory category)
    {
        var name = string.IsNullOrWhiteSpace(endpoint?.DisplayLabel) ? endpoint?.name : endpoint.DisplayLabel;
        var endpointId = endpoint?.EndpointId ?? string.Empty;
        var separator = endpointId.LastIndexOf(':');
        var suffix = separator >= 0 ? endpointId.Substring(separator + 1) : endpointId;
        if (category == EquipmentCategory.Switch
            && suffix.StartsWith("Fa", System.StringComparison.OrdinalIgnoreCase)
            && int.TryParse(suffix.Substring(2), out var switchPort))
            return "Fa0/" + switchPort;

        if (category != EquipmentCategory.Router)
            return name;

        if (suffix.Equals("Fa00", System.StringComparison.OrdinalIgnoreCase) || suffix.Equals("Fa0/0", System.StringComparison.OrdinalIgnoreCase))
            return "Fa0/0";
        if (suffix.Equals("Fa01", System.StringComparison.OrdinalIgnoreCase) || suffix.Equals("Fa0/1", System.StringComparison.OrdinalIgnoreCase))
            return "Fa0/1";
        return name;
    }

    private static bool ProviderDemarcConnected(TssNetworkSessionState network, TssTrainingSession session)
    {
        if (network == null || !session)
            return false;

        foreach (var device in network.Devices)
            if (device.Category == EquipmentCategory.Router
                && device.Interfaces.TryGetValue("Fa0/1", out var wan)
                && session.IsProviderDemarcConnected(wan.EndpointId))
                return true;
        return false;
    }

    public void OpenDesktopSession(GameObject deviceObject, DeviceContext ctx = null)
    {
        ResolvePresenters();
        if (ctx == null)
            ctx = ResolveContext(deviceObject);

        var isLaptop = ctx != null && ctx.Category == EquipmentCategory.Laptop;
        var presenter = isLaptop ? laptopPresenter : desktopPresenter;
        var prefab = isLaptop ? laptopCanvasPrefab : desktopCanvasPrefab;
        if (!presenter && prefab)
        {
            var instance = Instantiate(prefab);
            presenter = instance.GetComponentInChildren<TssDesktopSessionPresenter>(true);
            if (isLaptop) laptopPresenter = presenter;
            else desktopPresenter = presenter;
        }

        if (!presenter)
        {
            Debug.LogError("TssDeviceSessionController: Desktop session canvas presenter not found.");
            return;
        }

        var endpoints = deviceObject ? deviceObject.GetComponentsInChildren<TssPortEndpoint>(true) : Array.Empty<TssPortEndpoint>();
        var network = TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;
        TssDeviceLogicalState state;
        if (network != null)
        {
            var endpoint = network.GetOrCreateEndpoint(ctx.Key, ctx.DeviceName, ctx.Category);
            endpoint.DeviceKey = ctx.Key;
            var session = TssTrainingSession.Instance;
            network.SetProviderDemarcConnected(ProviderDemarcConnected(network, session));
            network.SetAccessPointUplink(session && session.IsAccessPointUplinkConnected());
            if (ctx.Category == EquipmentCategory.Laptop)
            {
                var associated = network.IsWirelessAssociated(ctx.Key);
                endpoint.Layer1Connected = associated;
                endpoint.Layer2Connected = associated;
            }
            else
            {
                endpoint.Layer1Connected = endpoints.Length > 0 && session
                    && (session.IsEndpointConnected(endpoints[0].EndpointId) || session.IsEndpointOnEffectivePhysicalPath(endpoints[0].EndpointId));
                endpoint.Layer2Connected = endpoint.Layer1Connected;
            }
            state = new TssDeviceLogicalState(ctx.Key, ctx.Category, ctx.DeviceName)
            {
                MacAddress = endpoint.MacAddress, IpAddress = endpoint.Address, SubnetMask = endpoint.Mask,
                DefaultGateway = endpoint.Gateway, DnsServer = endpoint.Dns, UseDhcp = endpoint.UseDhcp
            };
            state.RefreshPhysicalConnectivity(endpoints, TssTrainingSession.Instance);
        }
        else
        {
            state = new TssDeviceLogicalState(ctx.Key, ctx.Category, ctx.DeviceName);
            state.RefreshPhysicalConnectivity(endpoints, TssTrainingSession.Instance);
        }

        presenter.gameObject.SetActive(true);
        var initiatingSourceKey = ctx.Category == EquipmentCategory.Printer ? _lastWorkstationSourceKey : null;
        if (ctx.Category == EquipmentCategory.DesktopPc)
            _lastWorkstationSourceKey = ctx.Key;
        presenter.Initialize(state, deviceObject, CloseCurrentSession, ctx.Key, initiatingSourceKey);
        IsSessionOpen = true;

        if (network != null)
            TssTrainingSession.Instance?.NotifyNetworkStateChanged();

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PushExternalGameplayLock(this);
    }

    public void CloseCurrentSession()
    {
        if (terminalPresenter && terminalPresenter.gameObject.activeSelf)
            terminalPresenter.gameObject.SetActive(false);

        if (desktopPresenter && desktopPresenter.gameObject.activeSelf)
            desktopPresenter.gameObject.SetActive(false);

        if (laptopPresenter && laptopPresenter.gameObject.activeSelf)
            laptopPresenter.gameObject.SetActive(false);

        IsSessionOpen = false;

        if (TssRuntimeUi.Instance)
            TssRuntimeUi.Instance.PopExternalGameplayLock(this);
    }
}
