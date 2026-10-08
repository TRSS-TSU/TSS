using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public enum CiscoCliMode
{
    UserExec,        // Switch>
    PrivilegedExec,  // Switch#
    GlobalConfig,    // Switch(config)#
    InterfaceConfig, // Switch(config-if)#
    LineConfig,      // Switch(config-line)#
    VlanConfig       // Switch(config-vlan)#
}

public sealed class TssTerminalSessionPresenter : MonoBehaviour
{
    [Header("Header Elements")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text subheaderText;
    [SerializeField] private Text categoryBadgeText;
    [SerializeField] private Text linkStatusText;
    [SerializeField] private Button closeButton;

    [Header("Console Elements")]
    [SerializeField] private ScrollRect consoleScrollRect;
    [SerializeField] private RectTransform scrollContent;
    [SerializeField] private Text consoleOutputText;
    [SerializeField] private RectTransform inputBar;
    [SerializeField] private Text promptText;
    [SerializeField] private InputField commandInput;
    [SerializeField] private Button sendCommandButton;

    [Header("Legacy Quick Commands (unused)")]
    [SerializeField] private Button btnShowIpIntBrief;
    [SerializeField] private Button btnShowInterfaces;
    [SerializeField] private Button btnShowMacTable;
    [SerializeField] private Button btnShowVlan;
    [SerializeField] private Button btnShowIpRoute;
    [SerializeField] private Button btnPing;
    [SerializeField] private Button btnClear;

    [Header("Sidebar / Logical State")]
    [SerializeField] private Transform interfaceListContainer;
    [SerializeField] private Text sidebarStatusText;

    private TssDeviceLogicalState _state;
    private Action _onClose;
    private readonly StringBuilder _consoleHistory = new();
    private const int MaxHistoryLength = 20000;

    private CiscoCliMode _cliMode = CiscoCliMode.UserExec;
    private string _configuredInterface = "FastEthernet0/1";
    private int _lastSubmitFrame = -1;
    private TssCliCommandProcessor _processor;

    public CiscoCliMode CliMode => _cliMode;

    private void Awake()
    {
        if (closeButton)
            closeButton.onClick.AddListener(Close);
        if (sendCommandButton)
            sendCommandButton.onClick.AddListener(SubmitCurrentInput);
        if (commandInput)
            commandInput.onSubmit.AddListener(_ => SubmitCurrentInput());

        // ponytail: legacy fields stay serialized so unrebuilt scene instances remain loadable.
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (WasEscapePressed())
        {
            Close();
            return;
        }

        if (commandInput && commandInput.isFocused && WasEnterPressed())
        {
            SubmitCurrentInput();
        }
    }

    public void Initialize(TssDeviceLogicalState state, GameObject deviceObject, Action onClose, string deviceKey = null)
    {
        _state = state;
        _onClose = onClose;
        _cliMode = CiscoCliMode.UserExec;
        _processor = new TssCliCommandProcessor(TssTrainingSession.Instance, deviceKey ?? state.Key, state.Category, state.DeviceName);

        var isRouter = state.Category == EquipmentCategory.Router;
        if (titleText)
            titleText.text = $"{state.DeviceName} - Terminal Console";
        if (categoryBadgeText)
            categoryBadgeText.text = isRouter ? "ROUTER (LAYER 3)" : "SWITCH (LAYER 2)";
        if (categoryBadgeText)
            categoryBadgeText.color = isRouter ? new Color(1f, 0.72f, 0.2f, 1f) : new Color(0.25f, 0.9f, 1f, 1f);

        var activeLinks = 0;
        foreach (var iface in state.Interfaces)
        {
            if (iface.IsPhysicalLinkUp)
                activeLinks++;
        }

        if (linkStatusText)
        {
            linkStatusText.text = activeLinks > 0 ? $"LINK: UP ({activeLinks} active)" : "LINK: DOWN (No Cables)";
            linkStatusText.color = activeLinks > 0 ? new Color(0.35f, 1f, 0.5f, 1f) : new Color(0.85f, 0.4f, 0.4f, 1f);
        }

        UpdateSubheader();
        PopulateSidebarInterfaces();

        _consoleHistory.Clear();
        AppendToConsole(isRouter ? GetRouterBootBanner(state) : GetSwitchBootBanner(state));

        UpdatePrompt();

        if (commandInput)
        {
            commandInput.text = string.Empty;
            commandInput.ActivateInputField();
        }

        StartCoroutine(ScrollToBottomNextFrame());
    }

    private void UpdateSubheader()
    {
        if (!subheaderText || _state == null)
            return;

        var isRouter = _state.Category == EquipmentCategory.Router;
        subheaderText.text = isRouter
            ? $"Hostname: {_state.DeviceName}   Gateway: {_state.DefaultGateway}   Interfaces: {_state.Interfaces.Count} Ports"
            : $"Hostname: {_state.DeviceName}   Mgmt IP: {_state.IpAddress}   MAC: {_state.MacAddress}   VLAN 1";
    }

    public string GetCurrentPrompt()
    {
        var isRouter = _state != null && _state.Category == EquipmentCategory.Router;
        var host = _state != null ? _state.DeviceName : (isRouter ? "Router" : "Switch");

        return _cliMode switch
        {
            CiscoCliMode.UserExec => $"{host}>",
            CiscoCliMode.PrivilegedExec => $"{host}#",
            CiscoCliMode.GlobalConfig => $"{host}(config)#",
            CiscoCliMode.InterfaceConfig => $"{host}(config-if)#",
            CiscoCliMode.LineConfig => $"{host}(config-line)#",
            CiscoCliMode.VlanConfig => $"{host}(config-vlan)#",
            _ => $"{host}#"
        };
    }

    public void UpdatePrompt()
    {
        if (promptText)
            promptText.text = GetCurrentPrompt() + " ";
    }

    private void PopulateSidebarInterfaces()
    {
        if (!interfaceListContainer)
            return;

        if (_processor != null && _processor.IsAvailable)
        {
            if (sidebarStatusText)
                sidebarStatusText.text = "Type ? for mode-aware syntax.\nConfiguration is visible only through typed show commands.";
            return;
        }

        for (var i = interfaceListContainer.childCount - 1; i >= 0; i--)
            Destroy(interfaceListContainer.GetChild(i).gameObject);

        if (_state == null)
            return;

        foreach (var iface in _state.Interfaces)
        {
            var row = new GameObject(iface.InterfaceName, typeof(RectTransform), typeof(Image));
            row.transform.SetParent(interfaceListContainer, false);
            var rowImg = row.GetComponent<Image>();
            rowImg.color = iface.IsPhysicalLinkUp ? new Color(0.08f, 0.16f, 0.12f, 0.95f) : new Color(0.12f, 0.12f, 0.12f, 0.75f);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, 42f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(row.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(8f, 2f);
            textRt.offsetMax = new Vector2(-8f, -2f);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.lineSpacing = 1.1f;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            var statusStr = iface.IsPhysicalLinkUp ? "<color=#55FF77>UP</color>" : "<color=#888888>DOWN</color>";
            var peerStr = iface.IsPhysicalLinkUp
                ? $"Peer: {iface.ConnectedPeerDevice} ({iface.ConnectedPeerPort})"
                : "No Cable Connected";

            var l2OrL3 = _state.Category == EquipmentCategory.Switch
                ? $"VLAN {iface.VlanId} [{iface.PortMode}]"
                : (!string.IsNullOrEmpty(iface.IpAddress) ? iface.IpAddress : "Unassigned");

            text.text = $"<b>{iface.InterfaceName}</b> [{statusStr}] {l2OrL3}\n<color=#AAAAAA>{peerStr}</color>";
        }

        if (sidebarStatusText)
        {
            var isRouter = _state.Category == EquipmentCategory.Router;
            sidebarStatusText.text = isRouter
                ? $"<b>Routing Protocol:</b> Static / Connected\n<b>Default Gateway:</b> {_state.DefaultGateway}\n<b>Active Routes:</b> {_state.Interfaces.Count}"
                : $"<b>Spanning Tree (STP):</b> Enabled\n<b>Switch Mode:</b> Layer 2 Multiport Bridge\n<b>VLAN Database:</b> VLAN 1 Active";
        }
    }

    private void SubmitCurrentInput()
    {
        if (Time.frameCount == _lastSubmitFrame)
            return;
        _lastSubmitFrame = Time.frameCount;

        if (!commandInput)
            return;

        var cmd = commandInput.text;
        commandInput.text = string.Empty;

        if (string.IsNullOrEmpty(cmd))
        {
            AppendToConsole($"{GetCurrentPrompt()}\n");
            UpdatePrompt();
            StartCoroutine(ScrollToBottomNextFrame());
            if (commandInput) commandInput.ActivateInputField();
            return;
        }

        ExecuteCommand(cmd.Trim());
        StartCoroutine(ScrollToBottomNextFrame());
    }

    public void ExecuteCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd))
        {
            AppendToConsole($"{GetCurrentPrompt()}\n");
            UpdatePrompt();
            return;
        }

        AppendToConsole($"{GetCurrentPrompt()} {cmd}\n");

        if (_processor != null && _processor.IsAvailable)
        {
            if (string.Equals(cmd, "clear", StringComparison.OrdinalIgnoreCase))
            {
                _consoleHistory.Clear();
                if (consoleOutputText) consoleOutputText.text = string.Empty;
            }
            else
            {
                var result = _processor.Execute(cmd);
                _cliMode = result.Mode switch
                {
                    TssNetworkCliMode.PrivilegedExec => CiscoCliMode.PrivilegedExec,
                    TssNetworkCliMode.GlobalConfig => CiscoCliMode.GlobalConfig,
                    TssNetworkCliMode.InterfaceConfig => CiscoCliMode.InterfaceConfig,
                    TssNetworkCliMode.LineConfig => CiscoCliMode.LineConfig,
                    TssNetworkCliMode.VlanConfig => CiscoCliMode.VlanConfig,
                    _ => CiscoCliMode.UserExec
                };
                AppendToConsole(result.Output);
                if (result.StateChanged) UpdateSubheader();
            }
            UpdatePrompt();
            if (commandInput) commandInput.ActivateInputField();
            return;
        }

        var lower = cmd.ToLowerInvariant().Trim();
        var parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var lowerParts = lower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var verb = lowerParts[0];

        if (lower == "clear" || lower == "cls")
        {
            _consoleHistory.Clear();
            UpdatePrompt();
            return;
        }

        if (lower == "enable" || lower == "en")
        {
            _cliMode = CiscoCliMode.PrivilegedExec;
        }
        else if (lower == "disable")
        {
            _cliMode = CiscoCliMode.UserExec;
        }
        else if (lower == "configure terminal" || lower == "conf t" || lower == "config t" || lower == "configure")
        {
            if (_cliMode == CiscoCliMode.UserExec)
            {
                AppendToConsole("% Type 'enable' to enter privileged mode before configuring.\n");
            }
            else
            {
                _cliMode = CiscoCliMode.GlobalConfig;
                AppendToConsole("Enter configuration commands, one per line.  End with CNTL/Z.\n");
            }
        }
        else if (verb == "interface" || verb == "int")
        {
            if (_cliMode == CiscoCliMode.GlobalConfig || _cliMode == CiscoCliMode.InterfaceConfig)
            {
                _configuredInterface = parts.Length > 1 ? parts[1] : "FastEthernet0/1";
                _cliMode = CiscoCliMode.InterfaceConfig;
            }
            else
            {
                AppendToConsole("% Invalid input detected at '^' marker. Must be in config mode.\n");
            }
        }
        else if (lower.StartsWith("line con") || lower.StartsWith("line vty"))
        {
            if (_cliMode == CiscoCliMode.GlobalConfig)
                _cliMode = CiscoCliMode.LineConfig;
            else
                AppendToConsole("% Invalid input detected at '^' marker.\n");
        }
        else if (verb == "vlan")
        {
            if (_cliMode == CiscoCliMode.GlobalConfig)
                _cliMode = CiscoCliMode.VlanConfig;
            else
                AppendToConsole("% Invalid input detected at '^' marker.\n");
        }
        else if (lower == "exit")
        {
            if (_cliMode == CiscoCliMode.InterfaceConfig || _cliMode == CiscoCliMode.LineConfig || _cliMode == CiscoCliMode.VlanConfig)
            {
                _cliMode = CiscoCliMode.GlobalConfig;
            }
            else if (_cliMode == CiscoCliMode.GlobalConfig)
            {
                _cliMode = CiscoCliMode.PrivilegedExec;
            }
            else if (_cliMode == CiscoCliMode.PrivilegedExec)
            {
                _cliMode = CiscoCliMode.UserExec;
            }
            else if (_cliMode == CiscoCliMode.UserExec)
            {
                AppendToConsole("Connection to terminal closed by foreign host.\n");
                Close();
                return;
            }
        }
        else if (lower == "end")
        {
            _cliMode = CiscoCliMode.PrivilegedExec;
            AppendToConsole("%SYS-5-CONFIG_I: Configured from console by console\n");
        }
        else if (verb == "hostname")
        {
            if (_cliMode == CiscoCliMode.GlobalConfig || _cliMode == CiscoCliMode.PrivilegedExec)
            {
                if (parts.Length > 1 && _state != null)
                {
                    _state.DeviceName = parts[1].Trim();
                    if (titleText) titleText.text = $"{_state.DeviceName} - Terminal Console";
                    UpdateSubheader();
                }
            }
            else
            {
                AppendToConsole("% Invalid input detected at '^' marker.\n");
            }
        }
        else if (lower == "no shutdown" || lower == "no shut")
        {
            if (_cliMode == CiscoCliMode.InterfaceConfig)
                AppendToConsole($"%LINK-3-UPDOWN: Interface {_configuredInterface}, changed state to up\n%LINEPROTO-5-UPDOWN: Line protocol on Interface {_configuredInterface}, changed state to up\n");
            else
                AppendToConsole("% Command only valid in interface configuration mode.\n");
        }
        else if (lower == "shutdown" || lower == "shut")
        {
            if (_cliMode == CiscoCliMode.InterfaceConfig)
                AppendToConsole($"%LINK-5-CHANGED: Interface {_configuredInterface}, changed state to administratively down\n%LINEPROTO-5-UPDOWN: Line protocol on Interface {_configuredInterface}, changed state to down\n");
            else
                AppendToConsole("% Command only valid in interface configuration mode.\n");
        }
        else if (lower.StartsWith("ip address"))
        {
            if (_cliMode == CiscoCliMode.InterfaceConfig)
            {
                if (parts.Length >= 4)
                    AppendToConsole($"Configured interface {_configuredInterface} with IP {parts[2]} {parts[3]}.\n");
                else
                    AppendToConsole("% Incomplete command. Syntax: ip address <address> <mask>\n");
            }
            else
            {
                AppendToConsole("% Command only valid in interface configuration mode.\n");
            }
        }
        else if (lower == "show running-config" || lower == "show run" || lower == "do show run")
        {
            AppendToConsole(BuildShowRunningConfig());
        }
        else if (lower.StartsWith("show ip int") || lower.StartsWith("show ip interface brief") || lower.StartsWith("do show ip int"))
        {
            AppendToConsole(BuildShowIpInterfaceBrief());
        }
        else if (lower == "show interfaces" || lower == "show int" || lower == "do show int" || lower == "do show interfaces")
        {
            AppendToConsole(BuildShowInterfaces());
        }
        else if (lower.StartsWith("show mac") || lower.StartsWith("show mac-address-table") || lower.StartsWith("do show mac"))
        {
            AppendToConsole(BuildShowMacAddressTable());
        }
        else if (lower.StartsWith("show vlan") || lower.StartsWith("do show vlan"))
        {
            AppendToConsole(BuildShowVlanBrief());
        }
        else if (lower.StartsWith("show ip route") || lower == "show route" || lower.StartsWith("do show ip route"))
        {
            AppendToConsole(BuildShowIpRoute());
        }
        else if (lower.StartsWith("ping") || lower.StartsWith("do ping"))
        {
            var target = parts.Length > 1 ? parts[parts.Length - 1] : (_state?.DefaultGateway ?? "192.168.1.1");
            AppendToConsole(BuildPingResult(target));
        }
        else if (lower == "help" || lower == "?")
        {
            AppendToConsole(BuildHelpText());
        }
        else
        {
            AppendToConsole($"% Invalid input detected at '^' marker: \"{cmd}\". Type 'help' or '?' for available commands.\n");
        }

        UpdatePrompt();
        if (commandInput)
            commandInput.ActivateInputField();
    }

    private string BuildHelpText()
    {
        return _cliMode switch
        {
            CiscoCliMode.UserExec =>
                "Exec Commands (User Mode):\n" +
                "  enable                     Turn on privileged commands\n" +
                "  show ip interface brief    Display brief interface summary and status\n" +
                "  show interfaces            Display detailed interface operational metrics\n" +
                "  ping <ip>                  Send ICMP echo request to target IP\n" +
                "  exit                       Terminate terminal session\n" +
                "  clear                      Clear console screen\n",

            CiscoCliMode.PrivilegedExec =>
                "Privileged Exec Commands:\n" +
                "  configure terminal         Enter configuration mode\n" +
                "  disable                    Exit privileged mode to user mode\n" +
                "  show running-config        Display current active configuration\n" +
                "  show ip interface brief    Display brief interface summary and status\n" +
                "  show interfaces            Display detailed interface operational metrics\n" +
                "  show mac address-table     Display forwarding database MAC table\n" +
                "  show vlan brief            Display configured VLANs and port assignments\n" +
                "  show ip route              Display current Layer 3 routing table\n" +
                "  ping <ip>                  Send ICMP echo request to target IP\n" +
                "  exit                       Exit to user mode\n",

            CiscoCliMode.GlobalConfig =>
                "Global Configuration Commands:\n" +
                "  hostname <name>            Set system network name\n" +
                "  interface <name>           Select an interface to configure\n" +
                "  line <con|vty>             Configure a terminal line\n" +
                "  vlan <id>                  Configure VLAN parameters\n" +
                "  exit                       Exit from configure mode\n" +
                "  end                        Exit to privileged EXEC mode\n",

            CiscoCliMode.InterfaceConfig =>
                "Interface Configuration Commands:\n" +
                "  ip address <ip> <mask>     Set IPv4 address on interface\n" +
                "  no shutdown                Enable interface\n" +
                "  shutdown                   Administratively disable interface\n" +
                "  exit                       Exit to global configuration mode\n" +
                "  end                        Exit to privileged EXEC mode\n",

            _ => "Commands: exit, end, clear, help\n"
        };
    }

    private string BuildShowRunningConfig()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Building configuration...");
        sb.AppendLine("Current configuration : 1084 bytes");
        sb.AppendLine("!");
        sb.AppendLine("version 15.0");
        sb.AppendLine("no service timestamps log datetime msec");
        sb.AppendLine("no service password-encryption");
        sb.AppendLine("!");
        sb.AppendLine($"hostname {(_state != null ? _state.DeviceName : "Switch")}");
        sb.AppendLine("!");
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                sb.AppendLine($"interface {iface.InterfaceName}");
                if (_state.Category == EquipmentCategory.Switch)
                {
                    sb.AppendLine($" switchport mode {iface.PortMode.ToLowerInvariant()}");
                    sb.AppendLine($" switchport access vlan {iface.VlanId}");
                }
                else if (!string.IsNullOrEmpty(iface.IpAddress))
                {
                    var ipParts = iface.IpAddress.Split('/');
                    sb.AppendLine($" ip address {ipParts[0]} 255.255.255.0");
                }
                sb.AppendLine("!");
            }
        }
        sb.AppendLine("line con 0");
        sb.AppendLine("line vty 0 4");
        sb.AppendLine(" login");
        sb.AppendLine("!");
        sb.AppendLine("end");
        return sb.ToString();
    }

    private string BuildShowIpInterfaceBrief()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Interface                  IP-Address      OK? Method Status                Protocol");
        sb.AppendLine("-------------------------- --------------- --- ------ --------------------- --------");
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                var ip = !string.IsNullOrEmpty(iface.IpAddress) ? iface.IpAddress.Split('/')[0] : "unassigned";
                var status = iface.IsPhysicalLinkUp ? "up" : "down";
                var protocol = iface.IsPhysicalLinkUp ? "up" : "down";
                sb.AppendLine($"{iface.InterfaceName,-26} {ip,-15} YES NVRAM  {status,-21} {protocol}");
            }
        }
        return sb.ToString();
    }

    private string BuildShowInterfaces()
    {
        var sb = new StringBuilder();
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                var status = iface.IsPhysicalLinkUp ? "up" : "down";
                sb.AppendLine($"{iface.InterfaceName} is {status}, line protocol is {status}");
                sb.AppendLine($"  Hardware is FastEthernet, address is {iface.MacAddress}");
                sb.AppendLine($"  MTU 1500 bytes, BW 100000 Kbit/sec, DLY 100 usec, reliability 255/255");
                sb.AppendLine($"  Full-duplex, 100Mb/s, media type is RJ45");
                sb.AppendLine($"  Peer: {(iface.IsPhysicalLinkUp ? iface.ConnectedPeerDevice : "none")}");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }

    private string BuildShowMacAddressTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("          Mac Address Table");
        sb.AppendLine("-------------------------------------------");
        sb.AppendLine("Vlan    Mac Address       Type        Ports");
        sb.AppendLine("----    -----------       --------    -----");
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                if (iface.IsPhysicalLinkUp)
                {
                    var peerMac = !string.IsNullOrEmpty(iface.ConnectedPeerDevice)
                        ? "0050.79" + Mathf.Abs(iface.ConnectedPeerDevice.GetHashCode() & 0xFFFF).ToString("X4")
                        : iface.MacAddress.Replace(":", ".");
                    sb.AppendLine($"   {iface.VlanId}    {peerMac,-17} DYNAMIC     {iface.InterfaceName}");
                }
            }
        }
        return sb.ToString();
    }

    private string BuildShowVlanBrief()
    {
        var sb = new StringBuilder();
        sb.AppendLine("VLAN Name                             Status    Ports");
        sb.AppendLine("---- -------------------------------- --------- -------------------------------");
        var ports = new StringBuilder();
        if (_state != null)
        {
            for (var i = 0; i < _state.Interfaces.Count; i++)
            {
                if (i > 0) ports.Append(", ");
                ports.Append(_state.Interfaces[i].InterfaceName);
            }
        }
        sb.AppendLine($"1    default                          active    {ports}");
        sb.AppendLine("1002 fddi-default                     act/unsup ");
        sb.AppendLine("1003 token-ring-default               act/unsup ");
        return sb.ToString();
    }

    private string BuildShowIpRoute()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Codes: L - local, C - connected, S - static, R - RIP, M - mobile, B - BGP");
        sb.AppendLine($"Gateway of last resort is {_state?.DefaultGateway ?? "0.0.0.0"} to network 0.0.0.0\n");
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                if (iface.IsPhysicalLinkUp && !string.IsNullOrEmpty(iface.IpAddress))
                {
                    var net = iface.IpAddress.Contains(".") ? iface.IpAddress.Substring(0, iface.IpAddress.LastIndexOf('.') + 1) + "0/24" : "192.168.1.0/24";
                    sb.AppendLine($"C    {net} is directly connected, {iface.InterfaceName}");
                    sb.AppendLine($"L    {iface.IpAddress.Split('/')[0]}/32 is directly connected, {iface.InterfaceName}");
                }
            }
        }
        return sb.ToString();
    }

    private string BuildPingResult(string target)
    {
        var hasActive = false;
        if (_state != null)
        {
            foreach (var iface in _state.Interfaces)
            {
                if (iface.IsPhysicalLinkUp)
                {
                    hasActive = true;
                    break;
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Sending 5, 100-byte ICMP Echos to {target}, timeout is 2 seconds:");
        if (hasActive)
        {
            sb.AppendLine("!!!!!");
            sb.AppendLine("Success rate is 100 percent (5/5), round-trip min/avg/max = 1/2/4 ms");
        }
        else
        {
            sb.AppendLine(".....");
            sb.AppendLine("Success rate is 0 percent (0/5) - Physical link down / cable disconnected");
        }
        return sb.ToString();
    }

    public static string GetDefaultSwitchBootBanner()
    {
        return "Booting Cisco IOS Software (c2960-lanbasek9-mz.150-2.SE4.bin)...\n" +
               "Base Ethernet MAC Address: 00:50:79:6A:3B:12\n" +
               "Xmodem file system is available.\n" +
               "Initializing Flash...\n" +
               "flashfs[0]: 558 files, 19 directories\n" +
               "flashfs[0]: 0 orphaned files, 0 orphaned directories\n" +
               "flashfs[0]: Total bytes: 32514048\n" +
               "flashfs[0]: Bytes used: 11634176\n" +
               "flashfs[0]: Bytes available: 20879872\n" +
               "...done Initializing Flash.\n\n" +
               "Loading \"flash:/c2960-lanbasek9-mz.150-2.SE4.bin\"...\n" +
               "######################################################################### [OK]\n\n" +
               "Cisco IOS Software, C2960 Software (C2960-LANBASEK9-M), Version 15.0(2)SE4\n" +
               "Technical Support: http://www.cisco.com/techsupport\n" +
               "Copyright (c) 1986-2013 by Cisco Systems, Inc.\n" +
               "Compiled Wed 26-Jun-13 02:49 by prod_rel_team\n\n" +
               "Cisco WS-C2960-24TT-L processor with 65536K bytes of memory.\n" +
               "Processor board ID FOC1437W02D\n" +
               "Last reset from power-on\n" +
               "24 FastEthernet interfaces\n" +
               "2 Gigabit Ethernet interfaces\n" +
               "The password-recovery mechanism is enabled.\n\n" +
               "512K bytes of flash-simulated non-volatile configuration memory.\n" +
               "Base Ethernet MAC Address       : 00:50:79:6A:3B:12\n" +
               "Motherboard assembly number     : 73-9832-06\n" +
               "Model number                    : WS-C2960-24TT-L\n" +
               "System serial number            : FOC1437W02D\n\n" +
               "%LINK-5-CHANGED: Interface Vlan1, changed state to up\n" +
               "%LINEPROTO-5-UPDOWN: Line protocol on Interface Vlan1, changed state to up\n\n" +
               "Press RETURN to get started!\n\n";
    }

    public static string GetSwitchBootBanner(TssDeviceLogicalState state)
    {
        return $"Booting Cisco IOS Software (c2960-lanbasek9-mz.150-2.SE4.bin)...\n" +
               $"Base Ethernet MAC Address: {state.MacAddress}\n" +
               $"Xmodem file system is available.\n" +
               $"Initializing Flash...\n" +
               $"flashfs[0]: 558 files, 19 directories\n" +
               $"flashfs[0]: Total bytes: 32514048\n" +
               $"flashfs[0]: Bytes used: 11634176\n" +
               $"flashfs[0]: Bytes available: 20879872\n" +
               $"done Initializing Flash.\n\n" +
               $"Loading \"flash:/c2960-lanbasek9-mz.150-2.SE4.bin\"...\n" +
               $"######################################################################### [OK]\n\n" +
               $"Cisco IOS Software, C2960 Software (C2960-LANBASEK9-M), Version 15.0(2)SE4\n" +
               $"Device: {state.DeviceName} (Catalyst 2960 Series FastEthernet/Gigabit)\n" +
               $"Base Ethernet MAC Address: {state.MacAddress}\n" +
               $"Spanning Tree Protocol enabled on default VLAN 1\n\n" +
               $"%LINK-5-CHANGED: Interface Vlan1, changed state to up\n" +
               $"%LINEPROTO-5-UPDOWN: Line protocol on Interface Vlan1, changed state to up\n\n" +
               $"Press RETURN to get started!\n\n";
    }

    public static string GetRouterBootBanner(TssDeviceLogicalState state)
    {
        return $"System Bootstrap, Version 15.1(4)M4, RELEASE SOFTWARE (fc1)\n" +
               $"Copyright (c) 2012 by cisco Systems, Inc.\n\n" +
               $"Total memory: 512 MB DDR2 SDRAM\n" +
               $"Flash memory size: 256 MB\n" +
               $"Loading \"c2800nm-advipservicesk9-mz.151-4.M12a.bin\"...\n" +
               $"######################################################################### [OK]\n\n" +
               $"Cisco IOS Software, 2800 Software (C2800NM-ADVIPSERVICESK9-M), Version 15.1(4)M12a\n" +
               $"Device: {state.DeviceName} (Integrated Services Router 1U)\n" +
               $"Routing engine initialized: IPv4 routing active\n" +
               $"Base Ethernet MAC Address: {state.MacAddress}\n\n" +
               $"%LINK-5-CHANGED: Interface FastEthernet0/0, changed state to up\n" +
               $"%LINEPROTO-5-UPDOWN: Line protocol on Interface FastEthernet0/0, changed state to up\n\n" +
               $"Press RETURN to get started!\n\n";
    }

    private void AppendToConsole(string text)
    {
        _consoleHistory.Append(text);
        if (_consoleHistory.Length > MaxHistoryLength)
            _consoleHistory.Remove(0, _consoleHistory.Length - MaxHistoryLength);

        if (consoleOutputText)
            consoleOutputText.text = _consoleHistory.ToString();
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollContent)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollContent);
        if (consoleScrollRect)
            consoleScrollRect.verticalNormalizedPosition = 0f;
        if (commandInput)
            commandInput.ActivateInputField();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        _onClose?.Invoke();
    }

    private static bool WasEscapePressed()
    {
        return Keyboard.current != null &&
               Keyboard.current.escapeKey.wasPressedThisFrame;
    }

    private static bool WasEnterPressed()
    {
        return Keyboard.current != null &&
               (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);
    }
}
