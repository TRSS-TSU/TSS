using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public readonly struct TssCliResult
{
    public readonly string Output;
    public readonly TssNetworkCliMode Mode;
    public readonly bool StateChanged;
    public readonly bool Accepted;
    public TssCliResult(string output, TssNetworkCliMode mode, bool changed, bool accepted) { Output = output; Mode = mode; StateChanged = changed; Accepted = accepted; }
}

public sealed class TssCliCommandProcessor
{
    private readonly TssTrainingSession _session;
    private readonly TssNetworkSessionState _network;
    private readonly string _key;
    private readonly EquipmentCategory _category;
    private readonly string _displayName;

    public TssCliCommandProcessor(TssTrainingSession session, string key, EquipmentCategory category, string displayName)
    { _session = session; _network = session ? session.NetworkState : null; _key = key; _category = category; _displayName = displayName; }

    public bool IsAvailable => _network != null;
    public NetworkDeviceState Device => _network?.GetOrCreateDevice(_key, _category, _displayName);

    public bool BeginConsoleLogin()
    {
        var device = Device;
        if (device != null)
        {
            device.Mode = TssNetworkCliMode.UserExec;
            device.AwaitingEnablePassword = false;
            device.AwaitingConsolePassword = device.ConsoleLoginRequired && !string.IsNullOrEmpty(device.ConsolePassword);
        }
        return device != null && device.AwaitingConsolePassword;
    }

    public TssCliResult Execute(string input)
    {
        var device = Device;
        if (device == null) return new TssCliResult("% Network training state is unavailable.\n", TssNetworkCliMode.UserExec, false, false);
        var command = (input ?? string.Empty).Trim();
        if (command.Length == 0) return Result(device, string.Empty, false, true);
        if (device.AwaitingConsolePassword)
        {
            if (string.Equals(command, device.ConsolePassword, StringComparison.Ordinal))
            {
                device.AwaitingConsolePassword = false;
                return Result(device, string.Empty, false, true);
            }

            return Result(device, "% Access denied.\nPassword: ", false, false);
        }
        if (device.AwaitingEnablePassword)
        {
            device.AwaitingEnablePassword = false;
            var configuredEnablePassword = !string.IsNullOrEmpty(device.EnableSecret)
                ? device.EnableSecret
                : device.EnablePassword;
            if (!string.IsNullOrEmpty(configuredEnablePassword)
                && string.Equals(command, configuredEnablePassword, StringComparison.Ordinal))
            {
                device.Mode = TssNetworkCliMode.PrivilegedExec;
                return Result(device, string.Empty, false, true);
            }
            device.AwaitingEnablePassword = true;
            return Result(device, "% Access denied.\nPassword: ", false, false);
        }
        var lower = command.ToLowerInvariant();
        if (device.Mode == TssNetworkCliMode.GlobalConfig && lower == "line vty 0 ?")
            return Result(device, "  <0-15>  First line number; 16 VTY lines exist (0 through 15).\n", false, true);
        if (lower == "?" || lower == "help" || lower.EndsWith("?")) return Result(device, Help(device.Mode, device.Category), false, true);
        if (lower == "clear") return Result(device, string.Empty, false, true);
        if (lower.StartsWith("do "))
        {
            if (device.Mode == TssNetworkCliMode.UserExec || device.Mode == TssNetworkCliMode.PrivilegedExec) return Invalid(device);
            return ExecuteExec(device, command.Substring(3));
        }
        if (lower is "enable" or "en")
        {
            if (!string.IsNullOrEmpty(device.EnableSecret)
                || !string.IsNullOrEmpty(device.EnablePassword))
            {
                device.AwaitingEnablePassword = true;
                return Result(device, "Password: ", false, true);
            }
            device.Mode = TssNetworkCliMode.PrivilegedExec;
            return Result(device, string.Empty, false, true);
        }
        if (lower == "disable") { device.Mode = TssNetworkCliMode.UserExec; return Result(device, string.Empty, false, true); }
        if (lower is "configure terminal" or "conf t" or "config t" or "configure")
        {
            if (device.Mode != TssNetworkCliMode.PrivilegedExec) return Invalid(device);
            device.Mode = TssNetworkCliMode.GlobalConfig; return Result(device, "Enter configuration commands, one per line. End with CNTL/Z.\n", false, true);
        }
        if (lower == "end") { device.Mode = TssNetworkCliMode.PrivilegedExec; return Result(device, string.Empty, false, true); }
        if (lower == "exit") return Exit(device);
        if (lower is "copy running-config startup-config" or "copy run start" or "write memory" or "wr mem")
        {
            if (device.Mode != TssNetworkCliMode.PrivilegedExec) return Invalid(device);
            device.StartupSnapshot = device.Clone(); device.Evidence.Add("save"); return Changed(device, "Building configuration...\n[OK]\n");
        }
        if (lower.StartsWith("show ") || lower.StartsWith("sh ") || lower.StartsWith("ping "))
            return device.Mode is TssNetworkCliMode.UserExec or TssNetworkCliMode.PrivilegedExec
                ? ExecuteExec(device, command)
                : Invalid(device);
        if (device.Mode is TssNetworkCliMode.GlobalConfig or TssNetworkCliMode.InterfaceConfig or TssNetworkCliMode.InterfaceRangeConfig or TssNetworkCliMode.SubinterfaceConfig or TssNetworkCliMode.LineConfig or TssNetworkCliMode.VlanConfig)
        {
            if (lower is "line console 0" or "line con 0") return SelectLine(device, "console");
            if (lower is "line aux 0" or "line auxiliary 0") return device.Category == EquipmentCategory.Router ? SelectLine(device, "aux") : Invalid(device);
            if (lower is "line vty 0 4" or "line vty 5 15")
                return SelectLine(device, lower == "line vty 0 4" ? "vty" : "vty5-15");
            if (lower.StartsWith("interface ") || lower.StartsWith("int "))
                return SelectInterfaces(device, command.Substring(command.IndexOf(' ') + 1).Trim());
        }
        if (device.Mode == TssNetworkCliMode.GlobalConfig) return Global(device, command);
        if (device.Mode is TssNetworkCliMode.InterfaceConfig or TssNetworkCliMode.InterfaceRangeConfig or TssNetworkCliMode.SubinterfaceConfig) return Interface(device, command);
        if (device.Mode == TssNetworkCliMode.LineConfig) return Line(device, command);
        if (device.Mode == TssNetworkCliMode.VlanConfig) return TrySelectVlan(device, command) ? Changed(device, string.Empty) : Vlan(device, command);
        return Invalid(device);
    }

    private TssCliResult Global(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lower = command.ToLowerInvariant();
        if (lower == "no hostname") { d.Hostname = string.IsNullOrWhiteSpace(d.ExpectedRole) ? d.Category.ToString() : d.ExpectedRole; _session.RenameNetworkDevice(_key, d.Hostname); return Changed(d, string.Empty); }
        if (lower.StartsWith("hostname ") && parts.Length == 2) { d.Hostname = parts[1]; _session.RenameNetworkDevice(_key, d.Hostname); return Changed(d, string.Empty); }
        if (lower == "no enable secret") { d.EnableSecret = string.Empty; return Changed(d, string.Empty); }
        if (lower.StartsWith("enable secret ") && parts.Length >= 3) { d.EnableSecret = string.Join(" ", parts.Skip(2)); return Changed(d, string.Empty); }
        if (lower == "no enable password") { d.EnablePassword = string.Empty; return Changed(d, string.Empty); }
        if (lower.StartsWith("enable password ") && parts.Length >= 3) { d.EnablePassword = string.Join(" ", parts.Skip(2)); return Changed(d, string.Empty); }
        if (lower.StartsWith("no username ") && parts.Length == 3) { d.UserPasswords.Remove(parts[2]); return Changed(d, string.Empty); }
        if (lower.StartsWith("username ") && parts.Length >= 4 && parts[2].Equals("password", StringComparison.OrdinalIgnoreCase))
        { d.UserPasswords[parts[1]] = string.Join(" ", parts.Skip(3)); return Changed(d, string.Empty); }
        if (lower is "no service password-encryption" or "no ser pass") { d.PasswordEncryption = false; return Changed(d, string.Empty); }
        if (lower is "service password-encryption" or "ser pass") { d.PasswordEncryption = true; return Changed(d, string.Empty); }
        if (lower == "ip domain-lookup") { d.DomainLookupDisabled = false; return Changed(d, string.Empty); }
        if (lower == "no ip domain-lookup") { d.DomainLookupDisabled = true; return Changed(d, string.Empty); }
        if (lower == "no banner motd") { d.Motd = string.Empty; return Changed(d, string.Empty); }
        if (d.Category == EquipmentCategory.Switch && lower == "no ip default-gateway") { d.DefaultGateway = string.Empty; return Changed(d, string.Empty); }
        if (d.Category == EquipmentCategory.Switch && parts.Length == 3 && lower.StartsWith("ip default-gateway ") && ValidAddress(parts[2])) { d.DefaultGateway = parts[2]; return Changed(d, string.Empty); }
        if (lower.StartsWith("banner motd ") && command.Length > 13)
        {
            var rest = command.Substring(12).Trim(); if (rest.Length < 3 || rest[0] != rest[^1]) return Incomplete(d, "banner motd <delimiter>text<delimiter>");
            d.Motd = rest.Substring(1, rest.Length - 2); return Changed(d, string.Empty);
        }
        if ((lower.StartsWith("no interface ") || lower.StartsWith("no int ")) && d.Category == EquipmentCategory.Router)
        {
            var selection = command.Substring(command.IndexOf(' ') + 1).Trim();
            selection = selection.Substring(selection.IndexOf(' ') + 1).Trim();
            if (!TryCanonical(selection, out var subinterface, true) || subinterface.IndexOf('.') < 0 || !d.Interfaces.Remove(subinterface)) return Invalid(d);
            d.SelectedInterfaces.RemoveAll(name => name.Equals(subinterface, StringComparison.OrdinalIgnoreCase));
            return Changed(d, string.Empty);
        }
        if (lower.StartsWith("no vlan ") && d.Category == EquipmentCategory.Switch && int.TryParse(parts.LastOrDefault(), out var removeVlan) && ValidVlan(removeVlan) && removeVlan != 1) { d.Vlans.Remove(removeVlan); d.SuspendedVlans.Remove(removeVlan); return Changed(d, string.Empty); }
        if (TrySelectVlan(d, command)) return Changed(d, string.Empty);
        if (lower.StartsWith("no ip route ") && parts.Length == 5 && ValidAddress(parts[2]) && ValidRouteMask(parts[3]) && ValidAddress(parts[4])) { d.Routes.RemoveAll(route => route.Network == parts[2] && route.Mask == parts[3] && route.NextHop == parts[4]); return Changed(d, string.Empty); }
        if (lower.StartsWith("interface ") || lower.StartsWith("int "))
            return SelectInterfaces(d, command.Substring(command.IndexOf(' ') + 1));
        if (lower.StartsWith("ip route ") && parts.Length == 5 && ValidAddress(parts[2]) && ValidRouteMask(parts[3]) && ValidAddress(parts[4])) { d.Routes.Add(new NetworkRouteState { Network = parts[2], Mask = parts[3], NextHop = parts[4] }); return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult SelectInterfaces(NetworkDeviceState d, string selection)
    {
        var names = new List<string>();
        selection = selection.Trim();
        if (selection.StartsWith("range ", StringComparison.OrdinalIgnoreCase))
        {
            var range = selection.Substring(6).Split('-').Select(value => value.Trim()).ToArray(); if (range.Length != 2 || !TryPort(range[0], out var first) || !TryPort(range[1], out var last) || first > last) return Invalid(d);
            for (var i = first; i <= last; i++) names.Add("Fa0/" + i);
        }
        else if (d.Category == EquipmentCategory.Switch && selection.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(selection.Substring(4), out var svi) && ValidVlan(svi))
        {
            names.Add("Vlan" + svi);
            if (!d.Svis.ContainsKey(svi))
                d.Svis[svi] = new NetworkInterfaceState { Name = "Vlan" + svi, AdminUp = svi != 1 };
        }
        else { if (!TryCanonical(selection, out var canonical, d.Category == EquipmentCategory.Router)) return Invalid(d); names.Add(canonical); }
        d.SelectedInterfaces = names;
        foreach (var name in names)
            if (!name.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase) && !d.Interfaces.ContainsKey(name))
                d.Interfaces[name] = new NetworkInterfaceState { Name = name, AdminUp = d.Category == EquipmentCategory.Switch || name.IndexOf('.') >= 0 };
        d.Mode = d.Category == EquipmentCategory.Router && names.Any(name => name.IndexOf('.') >= 0)
            ? TssNetworkCliMode.SubinterfaceConfig
            : selection.StartsWith("range ", StringComparison.OrdinalIgnoreCase) ? TssNetworkCliMode.InterfaceRangeConfig : TssNetworkCliMode.InterfaceConfig;
        return Result(d, string.Empty, false, true);
    }

    private static bool TrySelectVlan(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (d.Category != EquipmentCategory.Switch || parts.Length != 2 || !parts[0].Equals("vlan", StringComparison.OrdinalIgnoreCase) || !int.TryParse(parts[1], out var vlan) || !ValidVlan(vlan)) return false;
        d.SelectedVlan = vlan;
        if (!d.Vlans.ContainsKey(vlan)) d.Vlans[vlan] = vlan == 1 ? "default" : "VLAN" + vlan;
        d.Mode = TssNetworkCliMode.VlanConfig;
        return true;
    }

    private static TssCliResult SelectLine(NetworkDeviceState d, string line)
    {
        d.SelectedLine = line;
        d.Mode = TssNetworkCliMode.LineConfig;
        return Result(d, string.Empty, false, true);
    }

    private TssCliResult Interface(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant(); var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lower == "ip add" || lower.StartsWith("ip add "))
        {
            command = "ip address" + command.Substring(6);
            lower = command.ToLowerInvariant();
            parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
        if (lower is "shutdown" or "shut" or "no shutdown" or "no shut")
        {
            if (d.Mode == TssNetworkCliMode.SubinterfaceConfig) return Result(d, "% Subinterfaces inherit shutdown state from the parent interface.\n", false, false);
            foreach (var i in Selected(d)) i.AdminUp = lower is "no shutdown" or "no shut";
            return Changed(d, string.Empty);
        }
        if (lower == "no description") { foreach (var i in Selected(d)) i.Description = string.Empty; return Changed(d, string.Empty); }
        if (lower.StartsWith("description ", StringComparison.Ordinal) && command.Length > 12)
        { foreach (var i in Selected(d)) i.Description = command.Substring(12).Trim(); return Changed(d, string.Empty); }
        if (IsNoAccessModeCommand(lower)) { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) { i.IsTrunk = false; i.SwitchportModeConfigured = false; } return Changed(d, string.Empty); }
        if (IsAccessModeCommand(lower)) { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) { i.IsTrunk = false; i.SwitchportModeConfigured = true; } return Changed(d, string.Empty); }
        if (IsNoTrunkModeCommand(lower)) { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) { i.IsTrunk = false; i.SwitchportModeConfigured = false; } return Changed(d, string.Empty); }
        if (IsTrunkModeCommand(lower)) { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) { i.IsTrunk = true; i.SwitchportModeConfigured = true; } return Changed(d, string.Empty); }
        if (lower == "no switchport trunk encapsulation dot1q") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.TrunkEncapsulationDot1q = false; return Changed(d, string.Empty); }
        // ponytail: the modeled 2960 supports only 802.1Q, so this command confirms its fixed encapsulation.
        if (lower == "switchport trunk encapsulation dot1q") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.TrunkEncapsulationDot1q = true; return Changed(d, string.Empty); }
        if (IsNoAccessVlanCommand(lower)) { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) { i.AccessVlan = 1; i.SwitchportAccessVlanConfigured = false; } return Changed(d, string.Empty); }
        if (TryAccessVlanCommand(lower, parts, out var vlan)) { foreach (var i in Selected(d)) { i.AccessVlan = vlan; i.SwitchportAccessVlanConfigured = true; } return Changed(d, string.Empty); }
        if (lower == "no switchport trunk native vlan") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.NativeVlan = 1; return Changed(d, string.Empty); }
        if (d.Category == EquipmentCategory.Switch && lower.StartsWith("switchport trunk native vlan ") && int.TryParse(parts.LastOrDefault(), out var native) && ValidVlan(native)) { foreach (var i in Selected(d)) i.NativeVlan = native; return Changed(d, string.Empty); }
        if (lower == "no switchport trunk allowed vlan") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.AllowedVlans.Clear(); return Changed(d, string.Empty); }
        if (lower.StartsWith("switchport trunk allowed vlan "))
        {
            if (d.Category != EquipmentCategory.Switch || !TryVlans(parts.LastOrDefault(), out var vlans)) return Invalid(d); foreach (var i in Selected(d)) { i.AllowedVlans.Clear(); foreach (var allowedVlan in vlans) i.AllowedVlans.Add(allowedVlan); } return Changed(d, string.Empty);
        }
        if (lower is "no ip address" or "no ip add")
        {
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Contains("Vlan" + _network.Profile.ManagementVlan, StringComparer.OrdinalIgnoreCase))
            {
                d.ManagementAddress = string.Empty;
                d.ManagementMask = string.Empty;
                foreach (var i in Selected(d)) { i.Address = string.Empty; i.Mask = string.Empty; }
                return Changed(d, string.Empty);
            }
            foreach (var i in Selected(d)) { i.Address = string.Empty; i.Mask = string.Empty; }
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Any(x => x.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase)))
            { foreach (var i in Selected(d)) i.Address = i.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase) ? d.ManagementAddress : i.Address; return Changed(d, string.Empty); }
            return Changed(d, string.Empty);
        }
        if (lower.StartsWith("ip address ") && parts.Length == 4 && ValidAddress(parts[2]) && ValidMask(parts[3]))
        {
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Contains("Vlan" + _network.Profile.ManagementVlan, StringComparer.OrdinalIgnoreCase))
            {
                d.ManagementAddress = parts[2];
                d.ManagementMask = parts[3];
                foreach (var i in Selected(d)) { i.Address = parts[2]; i.Mask = parts[3]; }
                return Changed(d, string.Empty);
            }
            foreach (var i in Selected(d)) { i.Address = parts[2]; i.Mask = parts[3]; }
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Any(x => x.Equals("Vlan" + _network.Profile.ManagementVlan, StringComparison.OrdinalIgnoreCase)))
            {
                d.ManagementAddress = parts[2]; d.ManagementMask = parts[3];
            }
            return Changed(d, string.Empty);
        }
        if (lower == "no encapsulation dot1q") { if (d.Category != EquipmentCategory.Router) return Invalid(d); foreach (var i in Selected(d)) i.Dot1qVlan = 0; return Changed(d, string.Empty); }
        if (d.Category == EquipmentCategory.Router && lower.StartsWith("encapsulation dot1q ") && int.TryParse(parts.LastOrDefault(), out var dot1q) && ValidVlan(dot1q)) { foreach (var i in Selected(d)) i.Dot1qVlan = dot1q; return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult Line(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (command.Equals("login", StringComparison.OrdinalIgnoreCase)) { SetLineLogin(d, true); return Changed(d, string.Empty); }
        if (command.Equals("no login", StringComparison.OrdinalIgnoreCase)) { SetLineLogin(d, false); return Changed(d, string.Empty); }
        if (command.Equals("no exec", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty5-15") return Invalid(d); d.Vty5To15ExecDisabled = true; return Changed(d, string.Empty); }
        if (command.Equals("exec", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty5-15") return Invalid(d); d.Vty5To15ExecDisabled = false; return Changed(d, string.Empty); }
        if (command.Equals("login local", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty") return Invalid(d); d.VtyLoginRequired = true; d.VtyLoginLocal = true; return Changed(d, string.Empty); }
        if (command.Equals("no login local", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty") return Invalid(d); d.VtyLoginLocal = false; return Changed(d, string.Empty); }
        if (command.Equals("logging synchronous", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsoleLoggingSynchronous = true; else if (d.SelectedLine == "aux") d.AuxLoggingSynchronous = true; else if (d.SelectedLine == "vty") d.VtyLoggingSynchronous = true; else return Invalid(d); return Changed(d, string.Empty); }
        if (command.Equals("no logging synchronous", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsoleLoggingSynchronous = false; else if (d.SelectedLine == "aux") d.AuxLoggingSynchronous = false; else if (d.SelectedLine == "vty") d.VtyLoggingSynchronous = false; else return Invalid(d); return Changed(d, string.Empty); }
        if (parts.Length == 2 && parts[0].Equals("exec-timeout", StringComparison.OrdinalIgnoreCase) && int.TryParse(parts[1], out var timeout) && timeout >= 0)
        { if (d.SelectedLine != "vty") return Invalid(d); d.VtyExecTimeoutMinutes = timeout; return Changed(d, string.Empty); }
        if (command.Equals("no exec-timeout", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty") return Invalid(d); d.VtyExecTimeoutMinutes = 0; return Changed(d, string.Empty); }
        if (command.Equals("transport input ssh", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty") return Invalid(d); d.VtyTransportInputSsh = true; return Changed(d, string.Empty); }
        if (command.Equals("no transport input ssh", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine != "vty") return Invalid(d); d.VtyTransportInputSsh = false; return Changed(d, string.Empty); }
        if (parts.Length == 2 && parts[0].Equals("no", StringComparison.OrdinalIgnoreCase) && parts[1].Equals("password", StringComparison.OrdinalIgnoreCase)) { SetLinePassword(d, string.Empty); return Changed(d, string.Empty); }
        if (parts.Length >= 2 && parts[0].Equals("password", StringComparison.OrdinalIgnoreCase)) { SetLinePassword(d, parts[1]); return Changed(d, string.Empty); }
        return Invalid(d);
    }
    private void SetLineLogin(NetworkDeviceState d, bool required)
    {
        if (d.SelectedLine == "console") d.ConsoleLoginRequired = required;
        else if (d.SelectedLine == "aux") d.AuxLoginRequired = required;
        else if (d.SelectedLine == "vty5-15") d.Vty5To15LoginRequired = required;
        else { d.VtyLoginRequired = required; d.VtyLoginLocal = false; }
    }
    private void SetLinePassword(NetworkDeviceState d, string password) { if (d.SelectedLine == "console") d.ConsolePassword = password; else if (d.SelectedLine == "aux") d.AuxPassword = password; else d.VtyPassword = password; }
    private TssCliResult Vlan(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant();
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 2 && parts[0].Equals("name", StringComparison.OrdinalIgnoreCase)) { d.Vlans[d.SelectedVlan] = parts[1]; return Changed(d, string.Empty); }
        if (lower == "no name") { d.Vlans[d.SelectedVlan] = d.SelectedVlan == 1 ? "default" : "VLAN" + d.SelectedVlan; return Changed(d, string.Empty); }
        if (command.Equals("state suspend", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedVlan == 1) return Result(d, "% VLAN 1 cannot be suspended.\n", false, false); d.SuspendedVlans.Add(d.SelectedVlan); return Changed(d, string.Empty); }
        if (lower == "no state suspend") { d.SuspendedVlans.Remove(d.SelectedVlan); d.Vlans[d.SelectedVlan] = d.GetVlanName(d.SelectedVlan); return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult ExecuteExec(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant();
        if (lower.StartsWith("sh ")) { command = "show" + command.Substring(2); lower = command.ToLowerInvariant(); }
        if (lower is "show running-config" or "show run") return Result(d, BuildRunningConfig(d), false, true);
        if (lower is "show startup-config" or "show start") return Result(d, d.StartupSnapshot == null ? "startup-config is not present\n" : BuildRunningConfig(d.StartupSnapshot), false, true);
        if (lower.StartsWith("show vlan"))
        {
            if (d.Category != EquipmentCategory.Switch) return Invalid(d);
            RecordEvidence(d, "show vlan brief");
            return Result(d, Vlans(d), false, true);
        }
        if (lower.StartsWith("show interfaces trunk") || lower == "show int trunk")
        {
            if (d.Category != EquipmentCategory.Switch) return Invalid(d);
            RecordEvidence(d, "show interfaces trunk");
            return Result(d, Trunks(d), false, true);
        }
        if (lower.StartsWith("show ip interface") || lower.StartsWith("show ip int")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show interfaces")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show mac")) return d.Category == EquipmentCategory.Switch
            ? Result(d, "Mac Address Table\n(no dynamic entries until endpoints are reachable)\n", false, true)
            : Invalid(d);
        if (lower is "show ip route" or "show ip ro") { RecordEvidence(d, "show ip route"); return Result(d, Routes(d), false, true); }
        if (lower.StartsWith("ping "))
        {
            var target = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
            var reachability = _network == null
                ? new TssReachabilityResult(false, 1, TssReachabilityFaultCategory.PhysicalPathUnavailable, "Network state is unavailable.", d.Hostname, string.Empty, Array.Empty<string>())
                : new TssNetworkReachabilityEvaluator(_network).EvaluateDevice(_key, target);
            return Result(d, PingOutput(target, reachability), false, true);
        }
        return Invalid(d);
    }
    private void RecordEvidence(NetworkDeviceState d, string evidenceId)
    {
        d.Evidence.Add(evidenceId);
        _network.Evidence.Add(evidenceId);
        _session.NotifyNetworkStateChanged();
    }
    private TssCliResult Exit(NetworkDeviceState d) { d.Mode = d.Mode switch { TssNetworkCliMode.InterfaceConfig or TssNetworkCliMode.InterfaceRangeConfig or TssNetworkCliMode.SubinterfaceConfig or TssNetworkCliMode.LineConfig or TssNetworkCliMode.VlanConfig => TssNetworkCliMode.GlobalConfig, TssNetworkCliMode.GlobalConfig => TssNetworkCliMode.PrivilegedExec, TssNetworkCliMode.PrivilegedExec => TssNetworkCliMode.UserExec, _ => TssNetworkCliMode.UserExec }; return Result(d, string.Empty, false, true); }
    private TssCliResult Changed(NetworkDeviceState d, string text) { _session.NotifyNetworkStateChanged(); return Result(d, text, true, true); }
    private static TssCliResult Result(NetworkDeviceState d, string text, bool changed, bool accepted) => new(text, d.Mode, changed, accepted);
    private static TssCliResult Invalid(NetworkDeviceState d) => Result(d, "% Invalid input detected at '^' marker.\n", false, false);
    private static TssCliResult Incomplete(NetworkDeviceState d, string syntax) => Result(d, "% Incomplete command. Syntax: " + syntax + "\n", false, false);
    private static string PingOutput(string target, TssReachabilityResult result)
    {
        if (!TssNetworkAddress.IsAddress(target))
            return "% Invalid IPv4 address.\n";

        return result.Success
            ? $"Sending 5, 100-byte ICMP Echos to {target}, timeout is 2 seconds:\n!!!!!\nSuccess rate is 100 percent (5/5), round-trip min/avg/max = 1/2/4 ms\n"
            : $"Sending 5, 100-byte ICMP Echos to {target}, timeout is 2 seconds:\n.....\nSuccess rate is 0 percent (0/5) - {result.Diagnostic}\n";
    }
    private static IEnumerable<NetworkInterfaceState> Selected(NetworkDeviceState d) => d.SelectedInterfaces.Select(name =>
        name.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.Substring(4), out var vlan) && d.Svis.TryGetValue(vlan, out var svi)
            ? svi : d.Interfaces.TryGetValue(name, out var physical) ? physical : null).Where(x => x != null);
    private static bool ValidVlan(int value) => value is >= 1 and <= 4094;
    private static bool IsNoAccessModeCommand(string lower) => lower.StartsWith("no ", StringComparison.Ordinal) && IsAccessModeCommand(lower.Substring(3));
    private static bool IsAccessModeCommand(string lower) => lower is
        "switchport mode access" or "swi mode access" or "sw mode access" or
        "switchport mode acc" or "swi mode acc" or "sw mode acc" or
        "swi mo access" or "sw mo access" or "swi mo acc" or "sw mo acc";
    private static bool IsNoTrunkModeCommand(string lower) => lower.StartsWith("no ", StringComparison.Ordinal) && IsTrunkModeCommand(lower.Substring(3));
    private static bool IsTrunkModeCommand(string lower)
    {
        var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;
        var port = parts[0];
        var mode = parts[1];
        var trunk = parts[2];
        return (port is "switchport" or "swi" or "sw")
            && (mode is "mode" or "mo")
            && (trunk is "trunk" or "tr");
    }
    private static bool IsNoAccessVlanCommand(string lower)
    {
        var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 3 or > 4 || parts[0] != "no")
            return false;
        var port = parts[1];
        var access = parts[2];
        var vlanWord = parts.Length == 4 ? parts[3] : string.Empty;
        var validPort = port is "switchport" or "swi" or "sw";
        var validAccess = access is "access" or "acc";
        var validVlanWord = parts.Length == 3 || vlanWord is "vlan" or "vl";
        return validPort && validAccess && validVlanWord;
    }
    private static bool TryAccessVlanCommand(string lower, string[] parts, out int vlan)
    {
        vlan = 0;
        if (parts.Length != 4 || !int.TryParse(parts[3], out vlan) || !ValidVlan(vlan))
            return false;
        var port = parts[0];
        var access = parts[1];
        var vlanWord = parts[2];
        var validPort = lower.StartsWith("switchport ", StringComparison.Ordinal)
            || port is "swi" or "sw";
        var validAccess = access is "access" or "acc";
        var validVlanWord = vlanWord is "vlan" or "vl";
        return validPort && validAccess && validVlanWord;
    }
    private static bool ValidAddress(string value) => TssNetworkAddress.IsAddress(value);
    private static bool ValidMask(string value) => TssNetworkAddress.IsMask(value);
    private static bool ValidRouteMask(string value) => value == "0.0.0.0" || ValidMask(value);
    private static bool TryVlans(string value, out List<int> vlans) { vlans = new List<int>(); if (string.IsNullOrWhiteSpace(value)) return false; foreach (var item in value.Split(',')) { if (!int.TryParse(item, out var vlan) || !ValidVlan(vlan)) return false; vlans.Add(vlan); } return vlans.Count > 0; }
    private static bool TryPort(string value, out int port) { port = 0; return TryCanonical(value, out var n) && int.TryParse(n.Split('/').Last(), out port); }
    private static bool TryCanonical(string value, out string name) => TryCanonical(value, out name, false);

    private static bool TryCanonical(string value, out string name, bool allowFastRouterAliases)
    {
        name = null;
        var s = value.Replace("FastEthernet", "Fa", StringComparison.OrdinalIgnoreCase).Replace("GigabitEthernet", "Gi", StringComparison.OrdinalIgnoreCase).Trim();
        if (s.StartsWith("F0/", StringComparison.OrdinalIgnoreCase))
            s = "Fa" + s.Substring(1);
        var separator = s.IndexOf('.');
        var suffix = string.Empty;
        if (separator >= 0)
        {
            suffix = s.Substring(separator + 1);
            if (!int.TryParse(suffix, out var vlan) || !ValidVlan(vlan))
                return false;
            s = s.Substring(0, separator);
        }

        if (allowFastRouterAliases && (s.Equals("Fa00", StringComparison.OrdinalIgnoreCase) || s.Equals("Fa0/0", StringComparison.OrdinalIgnoreCase))) { name = "Fa0/0" + (suffix.Length > 0 ? "." + suffix : string.Empty); return true; }
        if (allowFastRouterAliases && (s.Equals("Fa01", StringComparison.OrdinalIgnoreCase) || s.Equals("Fa0/1", StringComparison.OrdinalIgnoreCase))) { name = "Fa0/1" + (suffix.Length > 0 ? "." + suffix : string.Empty); return true; }
        if (s.StartsWith("Fa0/", StringComparison.OrdinalIgnoreCase) && int.TryParse(s.Substring(4), out var p) && p is >= 1 and <= 24) { name = "Fa0/" + p; return true; }
        return false;
    }
    public static string BuildCommandReference(EquipmentCategory category)
    {
        var isRouter = category == EquipmentCategory.Router;
        var execCommands = isRouter
            ? new[] { "show ip interface brief\tSummarize interface addresses and status", "show interfaces\tDisplay interface details", "show ip route\tDisplay connected and default routes", "ping <address>\tTest reachability" }
            : new[] { "show interfaces\tDisplay interface details", "show vlan brief\tDisplay VLANs and assigned ports", "show interfaces trunk\tVerify trunks, allowed VLANs, and native VLAN", "ping <address>\tTest reachability" };
        var interfaceCommands = isRouter
            ? new[] { "description <text>\tDescribe the interface", "no description\tRemove the interface description", "encapsulation dot1q <vlan>\tAssociate a VLAN with a router subinterface", "ip address <address> <mask>\tAssign an IPv4 address", "shutdown\tSet the interface administrative state" }
            : new[] { "description <text>\tDescribe the switchport", "no description\tRemove the switchport description", "switchport mode access\tSet an access port", "switchport access vlan <id>\tAssign the access VLAN", "switchport mode trunk\tSet a trunk port", "switchport trunk allowed vlan <list>\tSet VLANs carried by the trunk", "switchport trunk native vlan <id>\tSet the trunk's untagged VLAN", "shutdown\tSet the port administrative state" };

        var sb = new StringBuilder("\n");
        sb.AppendLine("  COMMAND".PadRight(36) + " | WHAT IT DOES");
        AppendReferenceSection(sb, "User / Privileged EXEC", new[]
        {
            "enable\tEnter privileged EXEC mode",
            "configure terminal\tEnter global configuration mode",
            "show running-config\tDisplay the active configuration",
            "show startup-config\tDisplay the saved configuration",
            "copy running-config startup-config\tSave the configuration",
            "exit\tLeave the current mode",
            "end\tReturn to privileged EXEC mode",
            "do <EXEC command>\tRun an EXEC command from configuration mode"
        });
        AppendReferenceSection(sb, "EXEC show and test commands", execCommands);
        AppendReferenceSection(sb, "Global configuration", new[]
        {
            "hostname <name>\tSet the device name",
            "enable password <password>\tSet the plain-text fallback privileged password",
            "enable secret <password>\tSet the privileged-mode password",
            "service password-encryption | ser pass\tEncrypt configured plain-text passwords",
            "username <name> password <password>\tSet a local user password",
            "ip domain-lookup\tSet DNS lookup state",
            "banner motd #<text>#\tSet the DoD warning banner",
            "line console 0\tSelect the console line",
            "line vty 0 4 | line vty 5 15\tSelect remote terminal lines"
        });
        if (isRouter) AppendReferenceSection(sb, "Router line configuration", new[] { "line aux 0\tSelect the auxiliary line" });
        if (!isRouter) AppendReferenceSection(sb, "Switch management", new[] { "ip default-gateway <address>\tSet the management default gateway", "no ip default-gateway\tClear the management default gateway" });
        if (!isRouter)
        {
            AppendReferenceSection(sb, "Switch management interface", new[]
            {
                "interface Vlan<id>\tSelect a VLAN interface (1-4094)",
                "description <text>\tDescribe the management SVI",
                "no description\tRemove the management SVI description",
                "ip address <address> <mask>\tAssign the switch management address",
                "no ip address\tRemove the switch management address"
            });
        }
        if (!isRouter)
        {
            AppendReferenceSection(sb, "Switch VLAN configuration", new[]
            {
                "vlan <id>\tCreate or select a VLAN",
                "name <name>\tName the selected VLAN",
                "state suspend\tSuspend the selected VLAN"
            });
            AppendReferenceSection(sb, "Switchport selection", new[]
            {
                "interface Fa0/<port>\tSelect one switchport",
                "interface range Fa0/<first> - Fa0/<last>\tSelect a continuous port range"
            });
        }
        else
        {
            AppendReferenceSection(sb, "Router interface selection", new[]
            {
                "interface Fa0/0\tSelect the switched-network uplink",
                "interface Fa0/0.<vlan>\tCreate or select a VLAN subinterface",
                "interface Fa0/1\tSelect the provider-facing WAN interface",
                "ip route 0.0.0.0 0.0.0.0 <next-hop>\tSet the provider default route"
            });
        }
        AppendReferenceSection(sb, "Interface configuration", interfaceCommands);
        AppendReferenceSection(sb, "Line configuration", new[]
        {
            "password <password>\tSet the selected line password",
            "login\tRequire the selected line password",
            "no login\tDisable login on the selected line",
            "login local\tUse the local username database on VTY lines",
            "exec-timeout 10\tSet the VTY inactivity timeout in minutes",
            "transport input ssh\tAllow SSH on VTY lines",
            "logging synchronous\tSynchronize line output"
        });
        return sb.ToString();
    }

    private static void AppendReferenceSection(StringBuilder sb, string title, IEnumerable<string> rows)
    {
        sb.AppendLine(title);
        sb.AppendLine("  " + new string('-', 34) + "-+------------------------------");
        foreach (var row in rows)
        {
            var columns = row.Split('\t');
            sb.Append("  ").Append(columns[0].PadRight(34)).Append(" | ").AppendLine(columns.Length > 1 ? columns[1] : string.Empty);
        }
        sb.AppendLine();
    }

    private static string Help(TssNetworkCliMode mode, EquipmentCategory category) => mode switch
    {
        TssNetworkCliMode.GlobalConfig => category == EquipmentCategory.Router
            ? "hostname <name>\nenable password <value>\nenable secret <value>\nservice password-encryption | ser pass\nusername <name> password <value>\ninterface <name>\nip route <network> <mask> <next-hop>\nline console 0|aux 0|vty 0 4|vty 5 15\n"
            : "hostname <name>\nenable password <value>\nenable secret <value>\nservice password-encryption | ser pass\nusername <name> password <value>\nip default-gateway <address>\ninterface <name>|range <first> - <last>|Vlan<id>\nvlan <id>\nline console 0|vty 0 4|vty 5 15\n",
        TssNetworkCliMode.InterfaceConfig => category == EquipmentCategory.Router
            ? "shutdown | no shutdown\nip address <address> <mask> | ip add <address> <mask>\nencapsulation dot1q <vlan>\n"
            : "shutdown | no shutdown\ndescription <text> | no description\nswitchport trunk encapsulation dot1q\nswitchport mode access|trunk\nswitchport access vlan <id> | no switchport access vlan\nswitchport trunk allowed vlan <id>[,<id>]\nswitchport trunk native vlan <id>\ninterface Vlan<id>\nip address <address> <mask> | ip add <address> <mask>\n",
        TssNetworkCliMode.SubinterfaceConfig => category == EquipmentCategory.Router
            ? "description <text> | no description\nip address <address> <mask> | ip add <address> <mask>\nencapsulation dot1q <vlan>\n(no shutdown commands; state is inherited from the parent interface)\n"
            : "shutdown | no shutdown\ndescription <text> | no description\nswitchport trunk encapsulation dot1q\nswitchport mode access|trunk\nswitchport access vlan <id> | no switchport access vlan\nswitchport trunk allowed vlan <id>[,<id>]\nswitchport trunk native vlan <id>\ninterface Vlan<id>\nip address <address> <mask> | ip add <address> <mask>\n",
        TssNetworkCliMode.LineConfig => "password <value>\nlogin | no login | login local\nno exec | exec (VTY 5-15)\nexec-timeout <minutes>\ntransport input ssh\nlogging synchronous\n",
        TssNetworkCliMode.VlanConfig => category == EquipmentCategory.Switch ? "name <name>\nno name\nstate suspend\nno state suspend\nvlan <id>\n" : "% VLAN configuration is switch-only.\n",
        _ => category == EquipmentCategory.Router
            ? "enable | configure terminal | show running-config | show startup-config | show ip interface brief | show interfaces | show ip route | ping <ip>\n"
            : "enable | configure terminal | show running-config | show startup-config | show vlan brief | show interfaces trunk | show ip interface brief | show interfaces | show mac address-table | ping <ip>\n"
    };
    public static string BuildRunningConfig(NetworkDeviceState d)
    {
        if (d.Category == EquipmentCategory.Router)
            return BuildRouterRunningConfig(d);

        var sb = new StringBuilder();
        sb.AppendLine("version 15.0");
        sb.AppendLine("!");
        sb.AppendLine("service timestamps debug datetime msec");
        sb.AppendLine("service timestamps log datetime msec");
        sb.AppendLine(d.PasswordEncryption ? "service password-encryption" : "no service password-encryption");
        sb.AppendLine("!");
        sb.AppendLine("hostname " + d.Hostname);
        if (!string.IsNullOrWhiteSpace(d.EnablePassword)) sb.AppendLine("enable password " + (d.PasswordEncryption ? "<encrypted>" : d.EnablePassword));
        if (!string.IsNullOrWhiteSpace(d.EnableSecret)) sb.AppendLine("enable secret <configured>");
        foreach (var user in d.UserPasswords.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine("username " + user.Key + " password " + (d.PasswordEncryption ? "<encrypted>" : user.Value));
        if (d.DomainLookupDisabled) sb.AppendLine("no ip domain-lookup");
        sb.AppendLine("!");
        sb.AppendLine("spanning-tree mode pvst");
        sb.AppendLine("!");
        var interfaces = d.Interfaces.Values.Where(i => !IsLineEndpoint(i.Name)).ToList();
        for (var port = 1; port <= 24; port++)
        {
            var name = "Fa0/" + port;
            if (!interfaces.Any(i => i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                interfaces.Add(new NetworkInterfaceState { Name = name, AdminUp = true });
        }
        foreach (var i in interfaces.OrderBy(x => InterfaceOrderKey(x.Name), StringComparer.OrdinalIgnoreCase))
        {
            if (IsSwitchGigabitInterface(d, i.Name)) continue;
            sb.AppendLine("interface " + SwitchRunningConfigInterfaceName(i.Name));
            if (!string.IsNullOrWhiteSpace(i.Description)) sb.AppendLine(" description " + i.Description);
            if (i.TrunkEncapsulationDot1q && i.IsTrunk)
                sb.AppendLine(" switchport trunk encapsulation dot1q");
            if (i.SwitchportModeConfigured)
                sb.AppendLine(" switchport mode " + (i.IsTrunk ? "trunk" : "access"));
            else if (!i.IsTrunk && !i.SwitchportAccessVlanConfigured)
                sb.AppendLine(" switchport mode dynamic auto");
            if (i.IsTrunk && i.AllowedVlans.Count > 0)
                sb.AppendLine(" switchport trunk allowed vlan " + string.Join(",", i.AllowedVlans.OrderBy(x => x)));
            if (i.IsTrunk && i.NativeVlan != 1)
                sb.AppendLine(" switchport trunk native vlan " + i.NativeVlan);
            if (!i.IsTrunk && i.SwitchportAccessVlanConfigured)
                sb.AppendLine(" switchport access vlan " + i.AccessVlan);
            if (!i.AdminUp && i.Name.IndexOf('.') < 0) sb.AppendLine(" shutdown");
            if (i.Dot1qVlan > 0) sb.AppendLine(" encapsulation dot1q " + i.Dot1qVlan);
            if (!string.IsNullOrEmpty(i.Address)) sb.AppendLine(" ip address " + i.Address + " " + i.Mask);
            sb.AppendLine("!");
        }
        sb.AppendLine("interface Vlan1");
        if (d.Svis.TryGetValue(1, out var vlan1) && !string.IsNullOrWhiteSpace(vlan1.Description)) sb.AppendLine(" description " + vlan1.Description);
        if (!string.IsNullOrWhiteSpace(vlan1?.Address)) sb.AppendLine(" ip address " + vlan1.Address + " " + vlan1.Mask);
        else sb.AppendLine(" no ip address");
        if (!d.Svis.TryGetValue(1, out vlan1) || !vlan1.AdminUp) sb.AppendLine(" shutdown");
        sb.AppendLine("!");
        foreach (var svi in d.Svis.Values.Where(x => !x.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(svi.Description) && string.IsNullOrWhiteSpace(svi.Address) && svi.AdminUp) continue;
            sb.AppendLine("interface " + svi.Name);
            if (!string.IsNullOrWhiteSpace(svi.Description)) sb.AppendLine(" description " + svi.Description);
            if (!string.IsNullOrWhiteSpace(svi.Address)) sb.AppendLine(" ip address " + svi.Address + " " + svi.Mask);
            if (!svi.AdminUp) sb.AppendLine(" shutdown");
            sb.AppendLine("!");
        }
        sb.AppendLine("ip classless");
        if (!string.IsNullOrWhiteSpace(d.DefaultGateway)) sb.AppendLine("ip default-gateway " + d.DefaultGateway);
        sb.AppendLine("!");
        if (!string.IsNullOrWhiteSpace(d.Motd))
        {
            var motd = d.Motd.Trim();
            const string legacyPrefix = "banner motd #";
            if (motd.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
                motd = motd.Substring(legacyPrefix.Length);
            if (motd.EndsWith("#", StringComparison.Ordinal))
                motd = motd[..^1];
            sb.AppendLine("banner motd #" + motd + "#");
            sb.AppendLine("!");
        }
        sb.AppendLine("line con 0");
        if (!string.IsNullOrWhiteSpace(d.ConsolePassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.ConsolePassword));
        if (d.ConsoleLoginRequired) sb.AppendLine(" login");
        if (d.ConsoleLoggingSynchronous) sb.AppendLine(" logging synchronous");
        sb.AppendLine("!");
        if (d.Category == EquipmentCategory.Router && (!string.IsNullOrWhiteSpace(d.AuxPassword) || d.AuxLoginRequired || d.AuxLoggingSynchronous))
        {
            sb.AppendLine("line aux 0");
            if (!string.IsNullOrWhiteSpace(d.AuxPassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.AuxPassword));
            if (d.AuxLoginRequired) sb.AppendLine(" login");
            if (d.AuxLoggingSynchronous) sb.AppendLine(" logging synchronous");
            sb.AppendLine("!");
        }
        sb.AppendLine("line vty 0 4");
        if (!string.IsNullOrWhiteSpace(d.VtyPassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.VtyPassword));
        if (d.VtyLoginLocal) sb.AppendLine(" login local");
        else if (d.VtyLoginRequired || d.Category == EquipmentCategory.Switch) sb.AppendLine(" login");
        if (d.VtyExecTimeoutMinutes > 0) sb.AppendLine(" exec-timeout " + d.VtyExecTimeoutMinutes);
        if (d.VtyTransportInputSsh) sb.AppendLine(" transport input ssh");
        if (d.VtyLoggingSynchronous) sb.AppendLine(" logging synchronous");
        sb.AppendLine("!");
        sb.AppendLine("line vty 5 15");
        if (d.Vty5To15LoginRequired) sb.AppendLine(" login");
        else sb.AppendLine(" no login");
        if (d.Vty5To15ExecDisabled) sb.AppendLine(" no exec");
        sb.AppendLine("!");
        foreach (var v in d.Vlans.OrderBy(x => x.Key))
        {
            var name = VlanName(v.Value);
            var suspended = IsVlanSuspended(d, v.Key);
            sb.AppendLine("vlan " + v.Key);
            if (!string.Equals(name, "default", StringComparison.OrdinalIgnoreCase)) sb.AppendLine(" name " + name);
            if (suspended) sb.AppendLine(" state suspend");
            sb.AppendLine("!");
        }
        foreach (var r in d.Routes)
        {
            sb.AppendLine("ip route " + r.Network + " " + r.Mask + " " + r.NextHop);
            sb.AppendLine("!");
        }
        return sb.Append("end\n").ToString();
    }

    private static string SwitchRunningConfigInterfaceName(string name)
    {
        if (name.StartsWith("Fa0/", StringComparison.OrdinalIgnoreCase)) return "FastEthernet" + name.Substring(2);
        if (name.StartsWith("Gi0/", StringComparison.OrdinalIgnoreCase)) return "GigabitEthernet" + name.Substring(2);
        return name;
    }

    private static string BuildRouterRunningConfig(NetworkDeviceState d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version 15.0");
        sb.AppendLine("!");
        sb.AppendLine("service timestamps debug datetime msec");
        sb.AppendLine("service timestamps log datetime msec");
        sb.AppendLine(d.PasswordEncryption ? "service password-encryption" : "no service password-encryption");
        sb.AppendLine("!");
        sb.AppendLine("hostname " + d.Hostname);
        if (!string.IsNullOrWhiteSpace(d.EnablePassword)) sb.AppendLine("enable password " + (d.PasswordEncryption ? "<encrypted>" : d.EnablePassword));
        if (!string.IsNullOrWhiteSpace(d.EnableSecret)) sb.AppendLine("enable secret <configured>");
        foreach (var user in d.UserPasswords.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine("username " + user.Key + " password " + (d.PasswordEncryption ? "<encrypted>" : user.Value));
        if (d.DomainLookupDisabled) sb.AppendLine("no ip domain-lookup");
        sb.AppendLine("!");
        sb.AppendLine("no aaa new-model");
        sb.AppendLine("ip cef");
        sb.AppendLine("!");
        var interfaces = d.Interfaces.Values.Where(i => !IsLineEndpoint(i.Name)).ToList();
        if (!interfaces.Any(i => i.Name.Equals("Fa0/0", StringComparison.OrdinalIgnoreCase))) interfaces.Add(new NetworkInterfaceState { Name = "Fa0/0" });
        if (!interfaces.Any(i => i.Name.Equals("Fa0/1", StringComparison.OrdinalIgnoreCase))) interfaces.Add(new NetworkInterfaceState { Name = "Fa0/1" });
        foreach (var i in interfaces.OrderBy(x => InterfaceOrderKey(x.Name), StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("interface " + RouterRunningConfigInterfaceName(i.Name));
            if (string.IsNullOrWhiteSpace(i.Address)) sb.AppendLine(" no ip address");
            if (!string.IsNullOrWhiteSpace(i.Description)) sb.AppendLine(" description " + i.Description);
            if (!i.AdminUp && i.Name.IndexOf('.') < 0) sb.AppendLine(" shutdown");
            if (i.Dot1qVlan > 0) sb.AppendLine(" encapsulation dot1q " + i.Dot1qVlan);
            if (!string.IsNullOrEmpty(i.Address)) sb.AppendLine(" ip address " + i.Address + " " + i.Mask);
            sb.AppendLine("!");
        }
        sb.AppendLine("ip forward-protocol nd");
        sb.AppendLine("!");
        if (!string.IsNullOrWhiteSpace(d.Motd))
        {
            var motd = d.Motd.Trim();
            const string legacyPrefix = "banner motd #";
            if (motd.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase)) motd = motd.Substring(legacyPrefix.Length);
            if (motd.EndsWith("#", StringComparison.Ordinal)) motd = motd[..^1];
            sb.AppendLine("banner motd #" + motd + "#");
            sb.AppendLine("!");
        }
        sb.AppendLine("line con 0");
        if (!string.IsNullOrWhiteSpace(d.ConsolePassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.ConsolePassword));
        if (d.ConsoleLoginRequired) sb.AppendLine(" login");
        if (d.ConsoleLoggingSynchronous) sb.AppendLine(" logging synchronous");
        sb.AppendLine("!");
        sb.AppendLine("line aux 0");
        if (!string.IsNullOrWhiteSpace(d.AuxPassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.AuxPassword));
        if (d.AuxLoginRequired) sb.AppendLine(" login");
        if (d.AuxLoggingSynchronous) sb.AppendLine(" logging synchronous");
        sb.AppendLine("!");
        sb.AppendLine("line vty 0 4");
        if (!string.IsNullOrWhiteSpace(d.VtyPassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.VtyPassword));
        if (d.VtyLoginLocal) sb.AppendLine(" login local");
        else if (d.VtyLoginRequired || d.Category == EquipmentCategory.Router) sb.AppendLine(" login");
        if (d.VtyExecTimeoutMinutes > 0) sb.AppendLine(" exec-timeout " + d.VtyExecTimeoutMinutes);
        if (d.VtyTransportInputSsh) sb.AppendLine(" transport input ssh");
        if (d.VtyLoggingSynchronous) sb.AppendLine(" logging synchronous");
        sb.AppendLine("!");
        sb.AppendLine("line vty 5 15");
        if (d.Vty5To15LoginRequired) sb.AppendLine(" login");
        else sb.AppendLine(" no login");
        if (d.Vty5To15ExecDisabled) sb.AppendLine(" no exec");
        sb.AppendLine("!");
        foreach (var r in d.Routes)
        {
            sb.AppendLine("ip route " + r.Network + " " + r.Mask + " " + r.NextHop);
            sb.AppendLine("!");
        }
        return sb.Append("end\n").ToString();
    }

    private static string RouterRunningConfigInterfaceName(string name)
    {
        if (name.Equals("Fa0/0", StringComparison.OrdinalIgnoreCase)) return "FastEthernet0/0";
        if (name.Equals("Fa0/1", StringComparison.OrdinalIgnoreCase)) return "FastEthernet0/1";
        if (name.StartsWith("Fa0/0.", StringComparison.OrdinalIgnoreCase)) return "FastEthernet0/0" + name.Substring(5);
        if (name.StartsWith("Fa0/1.", StringComparison.OrdinalIgnoreCase)) return "FastEthernet0/1" + name.Substring(5);
        return name;
    }

    public static IReadOnlyList<string> ResultsShowCommands(EquipmentCategory category)
    {
        return category == EquipmentCategory.Router
            ? new[] { "show running-config", "show ip interface brief", "show ip route" }
            : new[] { "show running-config", "show vlan brief", "show interfaces trunk", "show ip interface brief" };
    }

    public static string BuildResultsShowReview(NetworkDeviceState device, IReadOnlyList<string> incorrectCriteria = null)
    {
        var sb = new StringBuilder();
        foreach (var command in ResultsShowCommands(device?.Category ?? default))
        {
            sb.AppendLine("<color=#FFFFFF>===== " + command.ToUpperInvariant() + " =====</color>");
            var context = string.Empty;
            foreach (var rawLine in BuildResultsShowOutput(device, command).Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("interface ", StringComparison.OrdinalIgnoreCase)) context = line.Substring("interface ".Length).Trim();
                if (line.StartsWith("line ", StringComparison.OrdinalIgnoreCase)) context = line.Trim();
                if (line == "!") context = string.Empty;
                var escaped = line.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                sb.AppendLine(IsIncorrectResultsLine(command, line, context, device, incorrectCriteria) ? "<color=#FF4D4D>" + escaped + "</color>" : "<color=#FFFFFF>" + escaped + "</color>");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static bool IsIncorrectResultsLine(string command, string line, string context, NetworkDeviceState device, IReadOnlyList<string> criteria)
    {
        if (criteria == null || criteria.Count == 0 || string.IsNullOrWhiteSpace(line)) return false;
        var lowerCommand = command.ToLowerInvariant();
        var lowerLine = line.ToLowerInvariant();
        var lowerContext = (context ?? string.Empty).ToLowerInvariant();
        foreach (var criterion in criteria)
        {
            var warning = (criterion ?? string.Empty).ToLowerInvariant();
            if (lowerLine.StartsWith("vlan name") || lowerLine.StartsWith("----") || lowerLine.StartsWith("interface ")) continue;
            if (warning.Contains("extra device") && lowerLine.StartsWith("hostname ")) return true;
            var interfaceName = ExtractInterfaceName(warning);
            if (!string.IsNullOrEmpty(interfaceName) && (lowerContext == interfaceName || lowerLine.Contains(interfaceName))) return true;
            if (warning.Contains("unused ports") && TryGetFastEthernetPort(lowerLine, out var port) && port >= (string.Equals(device?.ExpectedRole, "AN-2", StringComparison.OrdinalIgnoreCase) ? 5 : 7)) return true;
            if (warning.Contains("vlan database") && (lowerCommand.Contains("vlan") || lowerLine.StartsWith("vlan "))) return true;
            if (warning.Contains("trunk") && (lowerCommand.Contains("trunk") || lowerLine.Contains("trunk") || lowerLine.Contains("native vlan") || lowerLine.Contains("vlans allowed"))) return true;
            if (warning.Contains("management interface") && (lowerContext == "vlan1" || lowerLine.Contains("vlan1") || lowerLine.Contains("ip address"))) return true;
            if (warning.Contains("subinterfaces") && (lowerContext.Contains(".") || lowerLine.Contains("fa0/0."))) return true;
            if (warning.Contains("wan configuration") && (lowerContext == "fa0/1" || lowerLine.Contains("fa0/1"))) return true;
            if (warning.Contains("default route") && (lowerCommand.Contains("route") || lowerLine.StartsWith("s*"))) return true;
            if (warning.Contains("hardening") && (lowerLine.StartsWith("hostname ") || lowerLine.StartsWith("enable ") || lowerLine.Contains("password-encryption") || lowerLine.Contains("domain-lookup") || lowerLine.StartsWith("banner ") || lowerContext.StartsWith("line ") || lowerLine == "login" || lowerLine.StartsWith(" password"))) return true;
        }
        return false;
    }

    private static string ExtractInterfaceName(string warning)
    {
        var marker = warning.IndexOf(" fa0/", StringComparison.Ordinal);
        if (marker < 0) return string.Empty;
        var start = marker + 1;
        var end = warning.IndexOf(' ', start);
        return (end < 0 ? warning.Substring(start) : warning.Substring(start, end - start)).TrimEnd('.');
    }

    private static bool TryGetFastEthernetPort(string line, out int port)
    {
        port = 0;
        var marker = line.IndexOf("fa0/", StringComparison.OrdinalIgnoreCase);
        return marker >= 0 && int.TryParse(line.Substring(marker + 4).Split(' ', '\t')[0], out port);
    }

    public static string BuildResultsShowOutput(NetworkDeviceState device, string command)
    {
        if (device == null) return string.Empty;
        var lower = (command ?? string.Empty).ToLowerInvariant();
        if (lower == "show running-config") return BuildRunningConfig(device);
        if (lower == "show startup-config") return device.StartupSnapshot == null ? "startup-config is not present\n" : BuildRunningConfig(device.StartupSnapshot);
        if (lower == "show vlan brief") return Vlans(device);
        if (lower == "show interfaces trunk") return Trunks(device);
        if (lower == "show ip interface brief" || lower == "show interfaces") return Interfaces(device);
        if (lower is "show ip route" or "show ip ro") return ResultsRoutes(device);
        if (lower == "show mac address-table") return "Mac Address Table\n(no dynamic entries until endpoints are reachable)\n";
        return "% Invalid show command.\n";
    }

    private static string ResultsRoutes(NetworkDeviceState d) => BuildRoutes(d, null);
    private static string InterfaceOrderKey(string name)
    {
        var key = new StringBuilder();
        for (var index = 0; index < (name ?? string.Empty).Length; index++)
        {
            if (!char.IsDigit(name[index]))
            {
                key.Append(char.ToLowerInvariant(name[index]));
                continue;
            }
            var start = index;
            while (index + 1 < name.Length && char.IsDigit(name[index + 1])) index++;
            key.Append(int.Parse(name.Substring(start, index - start + 1)).ToString("D6"));
        }
        return key.ToString();
    }
    private static string Vlans(NetworkDeviceState d) { var ports = new Dictionary<int, List<string>>(); foreach (var i in d.Interfaces.Values) if (!i.IsTrunk && !IsSwitchGigabitInterface(d, i.Name)) { if (!ports.TryGetValue(i.AccessVlan, out var list)) ports[i.AccessVlan] = list = new List<string>(); list.Add(i.Name); } var ids = new SortedSet<int> { 1 }; foreach (var v in d.Vlans.Keys) ids.Add(v); foreach (var v in ports.Keys) ids.Add(v); var sb = new StringBuilder("VLAN Name                             Status    Ports\n---- -------------------------------- --------- -------------------------------\n"); foreach (var id in ids) { var name = d.GetVlanName(id); ports.TryGetValue(id, out var list); sb.AppendLine($"{id,-4} {name,-32} {(d.IsVlanSuspended(id) ? "suspend" : "active"),-9} {string.Join(", ", list?.OrderBy(InterfaceOrderKey, StringComparer.OrdinalIgnoreCase) ?? Enumerable.Empty<string>())}"); } return sb.ToString(); }
    private static bool IsSwitchGigabitInterface(NetworkDeviceState d, string name) => d.Category == EquipmentCategory.Switch && (name.StartsWith("Gi0/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("GigabitEthernet0/", StringComparison.OrdinalIgnoreCase));
    private static string Trunks(NetworkDeviceState d) { var sb = new StringBuilder(); foreach (var i in d.Interfaces.Values.Where(x => x.IsTrunk && !IsSwitchGigabitInterface(d, x.Name))) sb.AppendLine($"{i.Name} on 802.1q trunking\nNative vlan: {i.NativeVlan}\nVlans allowed on trunk: {string.Join(",", i.AllowedVlans.OrderBy(x => x))}"); return sb.Length == 0 ? "No active trunks.\n" : sb.ToString(); }
    private static string Interfaces(NetworkDeviceState d) { var sb = new StringBuilder("Interface                  IP-Address      Status                Protocol\n"); foreach (var i in d.Svis.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) { var address = i.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase) ? d.ManagementAddress : i.Address; var status = i.AdminUp ? "up" : "administratively down"; sb.AppendLine($"{i.Name,-26} {(string.IsNullOrWhiteSpace(address) ? "unassigned" : address),-15} {status,-21} {(i.AdminUp ? "up" : "down")}"); } foreach (var i in d.Interfaces.Values.Where(i => !IsLineEndpoint(i.Name) && !IsSwitchGigabitInterface(d, i.Name))) { var adminUp = d.IsInterfaceAdminUp(i); var operational = d.IsInterfaceOperational(i); var status = adminUp ? (operational ? "up" : "down") : "administratively down"; var name = d.Category == EquipmentCategory.Router ? RouterRunningConfigInterfaceName(i.Name) : i.Name; sb.AppendLine($"{name,-26} {(i.Address ?? "unassigned"),-15} {status,-21} {(operational ? "up" : "down")}"); } return sb.ToString(); }
    private static bool IsLineEndpoint(string name) => !string.IsNullOrWhiteSpace(name) && (name.Equals("Console", StringComparison.OrdinalIgnoreCase) || name.Equals("Con", StringComparison.OrdinalIgnoreCase) || name.Equals("Aux", StringComparison.OrdinalIgnoreCase) || name.Equals("Auxiliary", StringComparison.OrdinalIgnoreCase));
    private string Routes(NetworkDeviceState d) => BuildRoutes(d, _network);
    private static string BuildRoutes(NetworkDeviceState d, TssNetworkSessionState network)
    {
        var connected = d.Interfaces.Values
            .Where(i => IsIpRouteEligible(d, i, network))
            .OrderBy(i => TssNetworkAddress.PrefixLength(i.Mask))
            .ThenBy(i => TssNetworkAddress.NetworkAddress(i.Address, i.Mask), StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var installed = d.Routes.Where(r => IsInstalledStaticRoute(d, r)).ToArray();
        var defaultRoute = installed.FirstOrDefault(IsDefaultRoute);
        var sb = new StringBuilder();
        sb.AppendLine(defaultRoute != null
            ? "Gateway of last resort is " + defaultRoute.NextHop + " to network 0.0.0.0"
            : "Gateway of last resort is not set");
        sb.AppendLine("Codes: C - connected, L - local, S - static");
        foreach (var i in connected)
        {
            var prefix = TssNetworkAddress.PrefixLength(i.Mask);
            var networkAddress = TssNetworkAddress.NetworkAddress(i.Address, i.Mask);
            var interfaceName = d.Category == EquipmentCategory.Router ? RouterRunningConfigInterfaceName(i.Name) : i.Name;
            sb.AppendLine($"C    {networkAddress}/{prefix} is directly connected, {interfaceName}");
            sb.AppendLine($"L    {i.Address}/32 is directly connected, {interfaceName}");
        }
        foreach (var r in installed.OrderByDescending(r => TssNetworkAddress.PrefixLength(r.Mask)).ThenBy(r => r.Network, StringComparer.Ordinal).ThenBy(r => r.NextHop, StringComparer.Ordinal))
        {
            var code = IsDefaultRoute(r) ? "S*" : "S";
            sb.AppendLine($"{code,-4} {r.Network}/{TssNetworkAddress.PrefixLength(r.Mask)} [1/0] via {r.NextHop}");
        }
        return sb.ToString();
    }
    private static bool IsIpRouteEligible(NetworkDeviceState d, NetworkInterfaceState iface, TssNetworkSessionState network) =>
        network != null ? network.IsInterfaceIpRoutingAvailable(d, iface) : d.IsInterfaceOperational(iface) && TssNetworkAddress.IsAddress(iface.Address) && TssNetworkAddress.IsMask(iface.Mask);
    private static bool IsDefaultRoute(NetworkRouteState route) => route != null && route.Network == "0.0.0.0" && route.Mask == "0.0.0.0";
    private static bool IsInstalledStaticRoute(NetworkDeviceState d, NetworkRouteState route) => d.Interfaces.Values.Any(i => d.IsInterfaceOperational(i) && TssNetworkAddress.IsAddress(i.Address) && TssNetworkAddress.IsMask(i.Mask) && TssNetworkAddress.IsInSameSubnet(i.Address, i.Mask, route.NextHop));
}
