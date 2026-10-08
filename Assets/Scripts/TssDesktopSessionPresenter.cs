using System;
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
    [SerializeField] private Text applyFeedbackText;

    [Header("Device-Specific Cards")]
    [SerializeField] private GameObject pcToolsCard;
    [SerializeField] private GameObject serverRolesCard;
    [SerializeField] private GameObject printerConsoleCard;

    [Header("PC Tools")]
    [SerializeField] private InputField pingTargetInput;
    [SerializeField] private Button pingButton;
    [SerializeField] private Button pingGatewayButton;
    [SerializeField] private Button ipconfigButton;
    [SerializeField] private Text pcDiagnosticsOutputText;

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
    private int _testPrintCount;
    private string _deviceKey;
    private bool _useDhcp;
    private TssEndpointCommandProcessor _commands;

    private void Awake()
    {
        if (disconnectButton)
            disconnectButton.onClick.AddListener(Close);
        if (applySettingsButton)
            applySettingsButton.onClick.AddListener(ApplySettings);
        if (dhcpToggle)
            dhcpToggle.onValueChanged.AddListener(OnDhcpToggled);
        if (staticToggle)
            staticToggle.onValueChanged.AddListener(OnStaticToggled);

        if (pingButton)
            pingButton.onClick.AddListener(ExecutePing);
        if (pingGatewayButton)
            pingGatewayButton.onClick.AddListener(() =>
            {
                if (pingTargetInput && _state != null)
                    pingTargetInput.text = _state.DefaultGateway;
                ExecutePing();
            });
        if (ipconfigButton)
            ipconfigButton.onClick.AddListener(ExecuteIpconfig);
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

    public void Initialize(TssDeviceLogicalState state, GameObject deviceObject, Action onClose, string deviceKey = null)
    {
        _state = state;
        _onClose = onClose;
        _deviceKey = deviceKey ?? state.DeviceKey;
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

        if (topBarLinkStatusText)
        {
            topBarLinkStatusText.text = isPhysicalLinkUp ? "Connected (1 Gbps)" : "Cable Unplugged";
            topBarLinkStatusText.color = isPhysicalLinkUp ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(0.9f, 0.4f, 0.4f, 1f);
        }

        if (netAdapterNameText)
            netAdapterNameText.text = $"Ethernet Adapter ({portLabel})";
        if (physicalLinkStatusText)
        {
            physicalLinkStatusText.text = isPhysicalLinkUp ? "Status: Connected / Physical Link Active" : "Status: Cable Unplugged / No Carrier";
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
            pcToolsCard.SetActive(category == EquipmentCategory.DesktopPc);
        if (serverRolesCard)
            serverRolesCard.SetActive(category == EquipmentCategory.Server);
        if (printerConsoleCard)
            printerConsoleCard.SetActive(category == EquipmentCategory.Printer);

        RefreshDeviceSpecificCard(isPhysicalLinkUp);
        RefreshPhysicalPathCard(deviceObject);

        if (applyFeedbackText)
            applyFeedbackText.text = string.Empty;
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
        if (_commands != null && !_commands.TryApply(_deviceKey, _useDhcp, ip, mask, gateway, dns, out var error))
        {
            if (applyFeedbackText) { applyFeedbackText.text = error; applyFeedbackText.color = new Color(1f, 0.45f, 0.45f, 1f); }
            return;
        }
        _state.UseDhcp = _useDhcp;
        _state.IpAddress = ip; _state.SubnetMask = mask; _state.DefaultGateway = gateway; _state.DnsServer = dns;
        TssTrainingSession.Instance?.NotifyNetworkStateChanged();

        if (applyFeedbackText)
        {
            applyFeedbackText.text = $"Settings applied: IP {_state.IpAddress} / Mask {_state.SubnetMask}";
            applyFeedbackText.color = new Color(0.35f, 1f, 0.5f, 1f);
            _feedbackTimer = 3.5f;
        }
    }

    private void RefreshDeviceSpecificCard(bool isPhysicalLinkUp)
    {
        if (_state == null)
            return;

        if (_state.Category == EquipmentCategory.DesktopPc)
        {
            if (pingTargetInput && string.IsNullOrEmpty(pingTargetInput.text))
                pingTargetInput.text = _state.DefaultGateway;
            if (pcDiagnosticsOutputText)
                pcDiagnosticsOutputText.text = "Network diagnostics ready. Select a tool or enter IP to ping.";
        }
        else if (_state.Category == EquipmentCategory.Server)
        {
            if (serverRolesStatusText)
            {
                serverRolesStatusText.text = isPhysicalLinkUp
                    ? "<b>Adapter:</b> available\nServices are not simulated in this work package."
                    : "<b>Adapter:</b> offline";
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
                printerStatusText.text = isPhysicalLinkUp
                    ? "<b>Printer Status:</b> network adapter available"
                    : "<b>Printer Status:</b> offline";
            }

            if (printerQueueText)
                printerQueueText.text = $"Print Queue: {_testPrintCount} job(s) processed.";
        }
    }

    private void RefreshPhysicalPathCard(GameObject deviceObject)
    {
        if (!physicalPathDetailsText)
            return;

        physicalPathDetailsText.text = "Use endpoint diagnostics to isolate connectivity faults.";
    }

    private void ExecutePing()
    {
        if (!pcDiagnosticsOutputText)
            return;

        var target = pingTargetInput && !string.IsNullOrWhiteSpace(pingTargetInput.text)
            ? pingTargetInput.text.Trim()
            : (_state?.DefaultGateway ?? "192.168.1.1");

        pcDiagnosticsOutputText.text = _commands != null
            ? _commands.Execute(_deviceKey, "ping " + target).Output
            : "Network diagnostics are unavailable.";
    }

    private void ExecuteIpconfig()
    {
        if (!pcDiagnosticsOutputText || _state == null)
            return;

        pcDiagnosticsOutputText.text = _commands != null ? _commands.Execute(_deviceKey, "ipconfig").Output : "Network diagnostics are unavailable.";
    }

    private void PrintTestPage()
    {
        if (_commands == null || !_commands.Execute(_deviceKey, "ping " + (_state?.IpAddress ?? string.Empty)).Accepted)
            return;
        _testPrintCount++;
        if (printerQueueText)
        {
            printerQueueText.text = $"[JOB #{_testPrintCount:D3}] Network Test Page - Printing completed successfully.\n" +
                                   $"Total pages: {_testPrintCount}";
        }
    }

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
