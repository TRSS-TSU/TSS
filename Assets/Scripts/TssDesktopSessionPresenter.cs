using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class TssDesktopSessionPresenter : MonoBehaviour
{
    [Header("Top Bar")]
    [SerializeField] private Text topBarTitleText;
    [SerializeField] private Text topBarHostnameText;
    [SerializeField] private Text topBarLinkStatusText;
    [SerializeField] private Text topBarTimeText;
    [SerializeField] private Button disconnectButton;

    [Header("Network Configuration Card (L2 / L3)")]
    [SerializeField] private Text netAdapterNameText;
    [SerializeField] private Text physicalLinkStatusText;
    [SerializeField] private Text peerDeviceText;
    [SerializeField] private Text macAddressText;
    [SerializeField] private Toggle dhcpToggle;
    [SerializeField] private Toggle staticToggle;
    [SerializeField] private InputField ipAddressInput;
    [SerializeField] private InputField subnetMaskInput;
    [SerializeField] private InputField defaultGatewayInput;
    [SerializeField] private InputField dnsServerInput;
    [SerializeField] private Button applySettingsButton;
    [SerializeField] private Button checkServerConnectionButton;
    [SerializeField] private Text applyFeedbackText;

    [Header("Access Point Browser")]
    [SerializeField] private Button openAccessPointBrowserButton;
    [SerializeField] private GameObject accessPointBrowserCard;
    [SerializeField] private Text accessPointBrowserStatusText;
    [SerializeField] private InputField accessPointSsidInput;
    [SerializeField] private InputField accessPointAdminPasswordInput;
    [SerializeField] private InputField accessPointPassphraseInput;
    [SerializeField] private InputField accessPointChannelInput;
    [SerializeField] private Toggle accessPointDisableWpsToggle;
    [SerializeField] private Button saveAccessPointButton;
    [SerializeField] private Button closeAccessPointBrowserButton;
    [SerializeField] private Text accessPointBrowserFeedbackText;

    [Header("Device-Specific Cards")]
    [SerializeField] private GameObject pcToolsCard;
    [SerializeField] private GameObject serverRolesCard;
    [SerializeField] private GameObject printerConsoleCard;

    [Header("PC Tools")]
    [SerializeField] private InputField commandInput;
    [SerializeField] private Button submitCommandButton;
    [SerializeField] private Button historyUpButton;
    [SerializeField] private Button historyDownButton;
    [SerializeField] private Text commandOutputText;

    [Header("Server Roles")]
    [SerializeField] private Text serverRolesStatusText;
    [SerializeField] private Text serverClientsText;

    [Header("Printer Console")]
    [SerializeField] private Text printerStatusText;
    [SerializeField] private Text printerQueueText;
    [SerializeField] private Button printTestPageButton;

    [Header("Physical Path Card")]
    [SerializeField] private Text physicalPathDetailsText;

    private TssDeviceLogicalState _state;
    private Action _onClose;
    private float _feedbackTimer;
    private string _deviceKey;
    private string _initiatingSourceKey;
    private bool _useDhcp;
    private TssEndpointCommandProcessor _commands;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;

    private void Awake()
    {
        if (disconnectButton)
            disconnectButton.onClick.AddListener(Close);
        if (applySettingsButton)
            applySettingsButton.onClick.AddListener(ApplySettings);
        if (checkServerConnectionButton)
            checkServerConnectionButton.onClick.AddListener(CheckServerConnection);
        if (openAccessPointBrowserButton)
            openAccessPointBrowserButton.onClick.AddListener(OpenAccessPointBrowser);
        if (saveAccessPointButton)
            saveAccessPointButton.onClick.AddListener(SaveAccessPointConfiguration);
        if (closeAccessPointBrowserButton)
            closeAccessPointBrowserButton.onClick.AddListener(CloseAccessPointBrowser);
        if (dhcpToggle)
            dhcpToggle.onValueChanged.AddListener(OnDhcpToggled);
        if (staticToggle)
            staticToggle.onValueChanged.AddListener(OnStaticToggled);

        if (submitCommandButton)
            submitCommandButton.onClick.AddListener(SubmitCommand);
        if (historyUpButton)
            historyUpButton.onClick.AddListener(() => BrowseHistory(-1));
        if (historyDownButton)
            historyDownButton.onClick.AddListener(() => BrowseHistory(1));
        if (commandInput)
            commandInput.onEndEdit.AddListener(_ => SubmitCommand());
        if (printTestPageButton)
            printTestPageButton.onClick.AddListener(PrintTestPage);
    }

    private void Update()
    {
        if (gameObject.activeInHierarchy && WasEscapePressed())
            Close();

        if (topBarTimeText)
        {
            var now = DateTime.Now;
            topBarTimeText.text = $"{now:HH:mm:ss}";
        }

        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= Time.deltaTime;
            if (_feedbackTimer <= 0f && applyFeedbackText)
                applyFeedbackText.text = string.Empty;
        }
    }

    public void Initialize(TssDeviceLogicalState state, GameObject deviceObject, Action onClose, string deviceKey = null, string initiatingSourceKey = null)
    {
        _state = state;
        _onClose = onClose;
        _deviceKey = deviceKey ?? state.DeviceKey;
        _initiatingSourceKey = initiatingSourceKey;
        var network = TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;
        _commands = network != null ? new TssEndpointCommandProcessor(network) : null;
        if (network != null && network.TryGetEndpoint(_deviceKey, out var endpoint))
        {
            state.UseDhcp = endpoint.UseDhcp;
            state.IpAddress = endpoint.Address;
            state.SubnetMask = endpoint.Mask;
            state.DefaultGateway = endpoint.Gateway;
            state.DnsServer = endpoint.Dns;
            state.MacAddress = endpoint.MacAddress;
        }
        _useDhcp = state.UseDhcp;

        var category = state.Category;
        var osName = category switch
        {
            EquipmentCategory.Server => "Enterprise Server OS 2024",
            EquipmentCategory.Printer => "Embedded Web Print Server",
            EquipmentCategory.AccessPoint => "Wireless Access Point",
            EquipmentCategory.Laptop => "Wireless Laptop OS",
            _ => "Workstation OS Enterprise"
        };

        if (topBarTitleText)
            topBarTitleText.text = osName;
        if (topBarHostnameText)
            topBarHostnameText.text = $"{state.DeviceName} [{category}]";

        var isPhysicalLinkUp = false;
        var peerLabel = "No Cable Connected";
        var portLabel = "Eth01";
        if (state.Interfaces.Count > 0)
        {
            var primary = state.Interfaces[0];
            portLabel = primary.InterfaceName;
            isPhysicalLinkUp = primary.IsPhysicalLinkUp;
            if (isPhysicalLinkUp)
                peerLabel = $"{primary.ConnectedPeerDevice} ({primary.ConnectedPeerPort})";
        }
        else if (category == EquipmentCategory.Laptop && network != null && network.TryGetEndpoint(_deviceKey, out var wirelessEndpoint))
        {
            portLabel = "Wi-Fi";
            isPhysicalLinkUp = wirelessEndpoint.Layer1Connected && wirelessEndpoint.Layer2Connected;
            peerLabel = isPhysicalLinkUp ? (network.AccessPoint.Ssid ?? "Guest AP") : "No AP association";
        }

        if (topBarLinkStatusText)
        {
            topBarLinkStatusText.text = isPhysicalLinkUp ? (category == EquipmentCategory.Laptop ? "Associated (Wi-Fi)" : "Connected (1 Gbps)") : "Cable Unplugged";
            topBarLinkStatusText.color = isPhysicalLinkUp ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(0.9f, 0.4f, 0.4f, 1f);
        }

        if (netAdapterNameText)
            netAdapterNameText.text = $"Ethernet Adapter ({portLabel})";
        if (physicalLinkStatusText)
        {
            physicalLinkStatusText.text = isPhysicalLinkUp ? (category == EquipmentCategory.Laptop ? "Status: Associated / Wireless Link Active" : "Status: Connected / Physical Link Active") : "Status: Cable Unplugged / No Carrier";
            physicalLinkStatusText.color = isPhysicalLinkUp ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(0.9f, 0.4f, 0.4f, 1f);
        }
        if (peerDeviceText)
            peerDeviceText.text = $"Connected To: {peerLabel}";
        if (macAddressText)
            macAddressText.text = $"Physical MAC Address: {state.MacAddress}";

        if (dhcpToggle)
            dhcpToggle.isOn = state.UseDhcp;
        if (staticToggle)
            staticToggle.isOn = !state.UseDhcp;

        if (ipAddressInput)
            ipAddressInput.text = state.IpAddress;
        if (subnetMaskInput)
            subnetMaskInput.text = state.SubnetMask;
        if (defaultGatewayInput)
            defaultGatewayInput.text = state.DefaultGateway;
        if (dnsServerInput)
            dnsServerInput.text = state.DnsServer;

        SetInputInteractable(!state.UseDhcp);

        if (pcToolsCard)
            pcToolsCard.SetActive(category == EquipmentCategory.DesktopPc || category == EquipmentCategory.Laptop);
        if (openAccessPointBrowserButton)
            openAccessPointBrowserButton.gameObject.SetActive(category == EquipmentCategory.DesktopPc || category == EquipmentCategory.Laptop);
        if (checkServerConnectionButton)
            checkServerConnectionButton.gameObject.SetActive(category == EquipmentCategory.DesktopPc || category == EquipmentCategory.Laptop);
        if (accessPointBrowserCard)
            accessPointBrowserCard.SetActive(false);
        if (serverRolesCard)
            serverRolesCard.SetActive(category == EquipmentCategory.Server);
        if (printerConsoleCard)
            printerConsoleCard.SetActive(category == EquipmentCategory.Printer);

        RefreshDeviceSpecificCard(isPhysicalLinkUp);
        RefreshPhysicalPathCard(deviceObject);

        _commandHistory.Clear();
        _historyIndex = -1;
        if (commandInput)
            commandInput.text = string.Empty;
        if (commandOutputText)
            commandOutputText.text = string.Empty;
        if (applyFeedbackText)
            applyFeedbackText.text = category == EquipmentCategory.AccessPoint
                ? "Set the AP management address here. Configure SSID and security from a connected desktop or laptop browser."
                : string.Empty;
    }

    private void OnDhcpToggled(bool isDhcp)
    {
        _useDhcp = isDhcp;
        if (staticToggle && staticToggle.isOn == isDhcp)
            staticToggle.isOn = !isDhcp;
        SetInputInteractable(!isDhcp);
    }

    private void OnStaticToggled(bool isStatic)
    {
        _useDhcp = !isStatic;
        if (dhcpToggle && dhcpToggle.isOn == isStatic)
            dhcpToggle.isOn = !isStatic;
        SetInputInteractable(isStatic);
    }

    private void SetInputInteractable(bool canEdit)
    {
        if (ipAddressInput) ipAddressInput.interactable = canEdit;
        if (subnetMaskInput) subnetMaskInput.interactable = canEdit;
        if (defaultGatewayInput) defaultGatewayInput.interactable = canEdit;
        if (dnsServerInput) dnsServerInput.interactable = canEdit;
    }

    private void ApplySettings()
    {
        if (_state == null)
            return;

        var ip = ipAddressInput ? ipAddressInput.text.Trim() : string.Empty;
        var mask = subnetMaskInput ? subnetMaskInput.text.Trim() : string.Empty;
        var gateway = defaultGatewayInput ? defaultGatewayInput.text.Trim() : string.Empty;
        var dns = dnsServerInput ? dnsServerInput.text.Trim() : string.Empty;
        if (_commands == null)
        {
            if (applyFeedbackText) applyFeedbackText.text = "Network settings are unavailable.";
            return;
        }
        if (!_commands.TryApply(_deviceKey, _useDhcp, ip, mask, gateway, dns, out var error))
        {
            if (applyFeedbackText) { applyFeedbackText.text = error; applyFeedbackText.color = new Color(1f, 0.45f, 0.45f, 1f); }
            return;
        }
        if (networkStateForPresenter() is { } network && network.TryGetEndpoint(_deviceKey, out var endpoint))
        {
            _state.UseDhcp = endpoint.UseDhcp;
            _state.IpAddress = endpoint.Address; _state.SubnetMask = endpoint.Mask;
            _state.DefaultGateway = endpoint.Gateway; _state.DnsServer = endpoint.Dns;
        }
        TssTrainingSession.Instance?.NotifyNetworkStateChanged();

        if (applyFeedbackText)
        {
            applyFeedbackText.text = $"Settings applied: IP {_state.IpAddress} / Mask {_state.SubnetMask}";
            applyFeedbackText.color = new Color(0.35f, 1f, 0.5f, 1f);
            _feedbackTimer = 3.5f;
        }
    }

    private void CheckServerConnection()
    {
        var network = networkStateForPresenter();
        var profile = network?.Profile;
        if (network == null || profile == null || !profile.EndpointAddresses.TryGetValue("IT2", out var serverAddress))
            return;

        var result = new TssNetworkReachabilityEvaluator(network).Evaluate(_deviceKey, serverAddress);
        if (applyFeedbackText)
        {
            applyFeedbackText.text = result.Success ? "Server (IT2) connectivity confirmed." : result.Diagnostic;
            applyFeedbackText.color = result.Success ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
        }
    }

    private void OpenAccessPointBrowser()
    {
        var network = networkStateForPresenter();
        var session = TssTrainingSession.Instance;
        if (network == null || session == null)
            return;

        network.SetAccessPointUplink(session.IsAccessPointUplinkConnected());
        var reachable = new TssNetworkReachabilityEvaluator(network).Evaluate(_deviceKey, network.Profile.ApManagementAddress);
        if (!network.AccessPoint.UplinkConnected || !network.IsAccessPointSwitchPortReady() || !reachable.Success)
        {
            if (applyFeedbackText)
            {
                applyFeedbackText.text = !network.AccessPoint.UplinkConnected ? "Layer 1: connect the AP uplink first." : !network.IsAccessPointSwitchPortReady() ? "Layer 2: AP switchport Fa0/7 is not ready." : reachable.Diagnostic;
                applyFeedbackText.color = new Color(1f, 0.45f, 0.45f, 1f);
            }
            return;
        }

        if (accessPointBrowserCard)
            accessPointBrowserCard.SetActive(true);
        if (accessPointBrowserStatusText)
            accessPointBrowserStatusText.text = "https://" + network.Profile.ApManagementAddress + "  |  Connected";
        if (accessPointBrowserFeedbackText)
            accessPointBrowserFeedbackText.text = string.Empty;
    }

    private void SaveAccessPointConfiguration()
    {
        var network = networkStateForPresenter();
        if (network == null)
            return;

        var saved = network.TryConfigureAccessPoint(
            _deviceKey,
            accessPointSsidInput ? accessPointSsidInput.text : string.Empty,
            accessPointAdminPasswordInput ? accessPointAdminPasswordInput.text : string.Empty,
            accessPointPassphraseInput ? accessPointPassphraseInput.text : string.Empty,
            accessPointChannelInput ? accessPointChannelInput.text : string.Empty,
            accessPointDisableWpsToggle && accessPointDisableWpsToggle.isOn,
            out var error);
        if (accessPointBrowserFeedbackText)
        {
            accessPointBrowserFeedbackText.text = saved ? "Saved. Guest wireless is secured and ready for association." : error;
            accessPointBrowserFeedbackText.color = saved ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
        }
        if (saved)
            TssTrainingSession.Instance?.NotifyNetworkStateChanged();
    }

    private void CloseAccessPointBrowser()
    {
        if (accessPointBrowserCard)
            accessPointBrowserCard.SetActive(false);
    }

    private void RefreshDeviceSpecificCard(bool isPhysicalLinkUp)
    {
        if (_state == null)
            return;

        if (_state.Category == EquipmentCategory.DesktopPc || _state.Category == EquipmentCategory.Laptop)
        {
            if (commandOutputText)
                commandOutputText.text = "Endpoint command prompt ready.\n";
        }
        else if (_state.Category == EquipmentCategory.Server)
        {
            if (serverRolesStatusText)
            {
                serverRolesStatusText.text = BuildEndpointStatus(isPhysicalLinkUp);
            }

            if (serverClientsText)
            {
                serverClientsText.text = "DHCP leases are not simulated.";
            }
        }
        else if (_state.Category == EquipmentCategory.Printer)
        {
            if (printerStatusText)
            {
                printerStatusText.text = BuildEndpointStatus(isPhysicalLinkUp);
            }

            if (printerQueueText)
                printerQueueText.text = BuildPrintStatus();
        }
    }

    private void RefreshPhysicalPathCard(GameObject deviceObject)
    {
        if (!physicalPathDetailsText)
            return;

        physicalPathDetailsText.text = "Use endpoint diagnostics to isolate connectivity faults.";
    }

    private string BuildEndpointStatus(bool isPhysicalLinkUp)
    {
        if (!isPhysicalLinkUp)
            return "<b>Adapter:</b> offline";
        var network = networkStateForPresenter();
        if (network == null || !network.TryGetEndpoint(_deviceKey, out var endpoint))
            return "<b>Adapter:</b> unavailable";
        var result = new TssNetworkReachabilityEvaluator(network).Evaluate(_deviceKey, endpoint.Address);
        return result.Success ? "<b>Adapter:</b> online\nReachability: available" : "<b>Adapter:</b> online\nReachability: " + result.Diagnostic;
    }

    private string BuildPrintStatus()
    {
        var network = networkStateForPresenter();
        if (network == null || !network.TryGetEndpoint(_deviceKey, out var printer) || printer.PrintAttemptCount == 0)
            return "Print Queue: 0 jobs processed.";
        return $"Last job: {(printer.LastPrintSucceeded ? "completed" : "failed")}\n" + printer.LastPrintDiagnostic;
    }

    private void SubmitCommand()
    {
        if (!commandInput || _commands == null)
            return;
        var input = commandInput.text.Trim();
        if (string.IsNullOrEmpty(input))
            return;
        _commandHistory.Add(input);
        _historyIndex = _commandHistory.Count;
        var result = _commands.Execute(_deviceKey, input);
        if (input.Equals("cls", StringComparison.OrdinalIgnoreCase))
            if (commandOutputText) commandOutputText.text = string.Empty;
        else if (commandOutputText)
            commandOutputText.text += "> " + input + "\n" + result.Output;
        commandInput.text = string.Empty;
    }

    private void BrowseHistory(int direction)
    {
        if (!commandInput || _commandHistory.Count == 0)
            return;
        _historyIndex = Mathf.Clamp(_historyIndex + direction, 0, _commandHistory.Count);
        commandInput.text = _historyIndex < _commandHistory.Count ? _commandHistory[_historyIndex] : string.Empty;
        commandInput.ActivateInputField();
    }

    private void PrintTestPage()
    {
        var network = networkStateForPresenter();
        if (network == null || !network.TryPrintTestPage(_deviceKey, _initiatingSourceKey, out _))
        {
            if (printerQueueText) printerQueueText.text = BuildPrintStatus();
            TssTrainingSession.Instance?.NotifyNetworkStateChanged();
            return;
        }
        if (printerQueueText) printerQueueText.text = BuildPrintStatus();
        TssTrainingSession.Instance?.NotifyNetworkStateChanged();
    }

    private static TssNetworkSessionState networkStateForPresenter() => TssTrainingSession.Instance ? TssTrainingSession.Instance.NetworkState : null;

    public void Close()
    {
        gameObject.SetActive(false);
        _onClose?.Invoke();
    }

    private static bool WasEscapePressed()
    {
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
    }
}
