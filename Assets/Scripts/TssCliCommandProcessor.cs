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

    public TssCliResult Execute(string input)
    {
        var device = Device;
        if (device == null) return new TssCliResult("% Network training state is unavailable.\n", TssNetworkCliMode.UserExec, false, false);
        var command = (input ?? string.Empty).Trim();
        if (command.Length == 0) return Result(device, string.Empty, false, true);
        if (device.AwaitingEnablePassword)
        {
            device.AwaitingEnablePassword = false;
            var configuredEnablePassword = !string.IsNullOrEmpty(device.EnableSecret)
                ? device.EnableSecret
                : !string.IsNullOrEmpty(device.EnablePassword)
                    ? device.EnablePassword
                    : device.ConsolePassword;
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
                || !string.IsNullOrEmpty(device.EnablePassword)
                || !string.IsNullOrEmpty(device.ConsolePassword))
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
        if (device.Mode == TssNetworkCliMode.GlobalConfig) return Global(device, command);
        if (device.Mode == TssNetworkCliMode.InterfaceConfig) return Interface(device, command);
        if (device.Mode == TssNetworkCliMode.LineConfig) return Line(device, command);
        if (device.Mode == TssNetworkCliMode.VlanConfig) return Vlan(device, command);
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
        if (lower == "no service password-encryption") { d.PasswordEncryption = false; return Changed(d, string.Empty); }
        if (lower == "service password-encryption") { d.PasswordEncryption = true; return Changed(d, string.Empty); }
        if (lower == "ip domain-lookup") { d.DomainLookupDisabled = false; return Changed(d, string.Empty); }
        if (lower == "no ip domain-lookup") { d.DomainLookupDisabled = true; return Changed(d, string.Empty); }
        if (lower == "no banner motd") { d.Motd = string.Empty; return Changed(d, string.Empty); }
        if (lower.StartsWith("banner motd ") && command.Length > 13)
        {
            var rest = command.Substring(12).Trim(); if (rest.Length < 3 || rest[0] != rest[^1]) return Incomplete(d, "banner motd <delimiter>text<delimiter>");
            d.Motd = rest.Substring(1, rest.Length - 2); return Changed(d, string.Empty);
        }
        if (lower is "line console 0" or "line con 0") { d.SelectedLine = "console"; d.Mode = TssNetworkCliMode.LineConfig; return Result(d, string.Empty, false, true); }
        if (lower == "line vty 0 4") { d.SelectedLine = "vty"; d.Mode = TssNetworkCliMode.LineConfig; return Result(d, string.Empty, false, true); }
        if (lower.StartsWith("no vlan ") && d.Category == EquipmentCategory.Switch && int.TryParse(parts.LastOrDefault(), out var removeVlan) && ValidVlan(removeVlan) && removeVlan != 1) { d.Vlans.Remove(removeVlan); return Changed(d, string.Empty); }
        if (lower.StartsWith("vlan ") && d.Category == EquipmentCategory.Switch && int.TryParse(parts.LastOrDefault(), out var vlan) && ValidVlan(vlan)) { d.SelectedVlan = vlan; if (!d.Vlans.ContainsKey(vlan)) d.Vlans[vlan] = "VLAN" + vlan; d.Mode = TssNetworkCliMode.VlanConfig; return Changed(d, string.Empty); }
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
                d.Svis[svi] = new NetworkInterfaceState { Name = "Vlan" + svi, AdminUp = svi == 1 };
        }
        else { if (!TryCanonical(selection, out var canonical, d.Category == EquipmentCategory.Router)) return Invalid(d); names.Add(canonical); }
        d.SelectedInterfaces = names;
        foreach (var name in names)
            if (!name.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase) && !d.Interfaces.ContainsKey(name))
                d.Interfaces[name] = new NetworkInterfaceState { Name = name, AdminUp = d.Category == EquipmentCategory.Switch };
        d.Mode = TssNetworkCliMode.InterfaceConfig; return Result(d, string.Empty, false, true);
    }

    private TssCliResult Interface(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant(); var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lower.StartsWith("interface ") || lower.StartsWith("int "))
        {
            var selection = command.Substring(command.IndexOf(' ') + 1).Trim();
            return SelectInterfaces(d, selection);
        }
        if (lower == "ip add" || lower.StartsWith("ip add "))
        {
            command = "ip address" + command.Substring(6);
            lower = command.ToLowerInvariant();
            parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
        if (lower is "shutdown" or "shut") { foreach (var i in Selected(d)) i.AdminUp = false; return Changed(d, string.Empty); }
        if (lower is "no shutdown" or "no shut") { foreach (var i in Selected(d)) i.AdminUp = true; return Changed(d, string.Empty); }
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
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Contains("Vlan1", StringComparer.OrdinalIgnoreCase))
            {
                d.ManagementAddress = string.Empty;
                d.ManagementMask = string.Empty;
                if (d.Svis.TryGetValue(1, out var managementSvi)) { managementSvi.Address = string.Empty; managementSvi.Mask = string.Empty; }
                return Changed(d, string.Empty);
            }
            foreach (var i in Selected(d)) { i.Address = string.Empty; i.Mask = string.Empty; }
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Any(x => x.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase)))
            { foreach (var i in Selected(d)) i.Address = i.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase) ? d.ManagementAddress : i.Address; return Changed(d, string.Empty); }
            return Changed(d, string.Empty);
        }
        if (lower.StartsWith("ip address ") && parts.Length == 4 && ValidAddress(parts[2]) && ValidMask(parts[3]))
        {
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Contains("Vlan1", StringComparer.OrdinalIgnoreCase))
            {
                d.ManagementAddress = parts[2];
                d.ManagementMask = parts[3];
                if (d.Svis.TryGetValue(1, out var managementSvi)) { managementSvi.Address = parts[2]; managementSvi.Mask = parts[3]; }
                return Changed(d, string.Empty);
            }
            foreach (var i in Selected(d)) { i.Address = parts[2]; i.Mask = parts[3]; }
            if (d.Category == EquipmentCategory.Switch && d.SelectedInterfaces.Any(x => x.StartsWith("Vlan", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var i in Selected(d)) if (i.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase)) { d.ManagementAddress = parts[2]; d.ManagementMask = parts[3]; }
            }
            return Changed(d, string.Empty);
        }
        if (lower == "no encapsulation dot1q") { if (d.Category != EquipmentCategory.Router) return Invalid(d); foreach (var i in Selected(d)) i.Dot1qVlan = 0; return Changed(d, string.Empty); }
        if (d.Category == EquipmentCategory.Router && lower.StartsWith("encapsulation dot1q ") && int.TryParse(parts.LastOrDefault(), out var dot1q) && ValidVlan(dot1q)) { foreach (var i in Selected(d)) i.Dot1qVlan = dot1q; return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult Line(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (command.Equals("login", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsoleLoginRequired = true; else d.VtyLoginRequired = true; return Changed(d, string.Empty); }
        if (command.Equals("no login", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsoleLoginRequired = false; else d.VtyLoginRequired = false; return Changed(d, string.Empty); }
        if (parts.Length == 2 && parts[0].Equals("no", StringComparison.OrdinalIgnoreCase) && parts[1].Equals("password", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsolePassword = string.Empty; else d.VtyPassword = string.Empty; return Changed(d, string.Empty); }
        if (parts.Length >= 2 && parts[0].Equals("password", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsolePassword = parts[1]; else d.VtyPassword = parts[1]; return Changed(d, string.Empty); }
        return Invalid(d);
    }
    private TssCliResult Vlan(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant();
        if (lower.StartsWith("interface ") || lower.StartsWith("int "))
            return SelectInterfaces(d, command.Substring(command.IndexOf(' ') + 1));
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 2 && parts[0].Equals("name", StringComparison.OrdinalIgnoreCase)) { d.Vlans[d.SelectedVlan] = parts[1]; return Changed(d, string.Empty); }
        if (lower == "no name") { d.Vlans[d.SelectedVlan] = "VLAN" + d.SelectedVlan; return Changed(d, string.Empty); }
        if (command.Equals("state suspend", StringComparison.OrdinalIgnoreCase)) { d.Vlans[d.SelectedVlan] = d.Vlans[d.SelectedVlan] + " (suspended)"; return Changed(d, string.Empty); }
        if (lower == "no state suspend") { d.Vlans[d.SelectedVlan] = d.Vlans[d.SelectedVlan].Replace(" (suspended)", string.Empty, StringComparison.OrdinalIgnoreCase); return Changed(d, string.Empty); }
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
            d.Evidence.Add("show vlan brief"); _network.Evidence.Add("show vlan brief");
            return Result(d, Vlans(d), false, true);
        }
        if (lower.StartsWith("show interfaces trunk") || lower == "show int trunk")
        {
            if (d.Category != EquipmentCategory.Switch) return Invalid(d);
            d.Evidence.Add("show interfaces trunk"); _network.Evidence.Add("show interfaces trunk");
            return Result(d, Trunks(d), false, true);
        }
        if (lower.StartsWith("show ip interface") || lower.StartsWith("show ip int")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show interfaces")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show mac")) return d.Category == EquipmentCategory.Switch
            ? Result(d, "Mac Address Table\n(no dynamic entries until endpoints are reachable)\n", false, true)
            : Invalid(d);
        if (lower.StartsWith("show ip route") || lower == "show route") { d.Evidence.Add("show ip route"); _network.Evidence.Add("show ip route"); return Result(d, Routes(d), false, true); }
        if (lower.StartsWith("ping ")) return Result(d, "Sending 5, 100-byte ICMP Echos to " + command.Split(' ').Last() + ", timeout is 2 seconds:\n.....\nSuccess rate is 0 percent (0/5) - use endpoint diagnostics for Phase 19 evidence\n", false, true);
        return Invalid(d);
    }
    private TssCliResult Exit(NetworkDeviceState d) { d.Mode = d.Mode switch { TssNetworkCliMode.InterfaceConfig or TssNetworkCliMode.LineConfig or TssNetworkCliMode.VlanConfig => TssNetworkCliMode.GlobalConfig, TssNetworkCliMode.GlobalConfig => TssNetworkCliMode.PrivilegedExec, TssNetworkCliMode.PrivilegedExec => TssNetworkCliMode.UserExec, _ => TssNetworkCliMode.UserExec }; return Result(d, string.Empty, false, true); }
    private TssCliResult Changed(NetworkDeviceState d, string text) { _session.NotifyNetworkStateChanged(); return Result(d, text, true, true); }
    private static TssCliResult Result(NetworkDeviceState d, string text, bool changed, bool accepted) => new(text, d.Mode, changed, accepted);
    private static TssCliResult Invalid(NetworkDeviceState d) => Result(d, "% Invalid input detected at '^' marker.\n", false, false);
    private static TssCliResult Incomplete(NetworkDeviceState d, string syntax) => Result(d, "% Incomplete command. Syntax: " + syntax + "\n", false, false);
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
            "enable secret <password>\tSet the privileged-mode password",
            "service password-encryption\tEncrypt configured line passwords",
            "ip domain-lookup\tSet DNS lookup state",
            "banner motd #<text>#\tSet the DoD warning banner",
            "line console 0\tSelect the console line",
            "line vty 0 4\tSelect remote terminal lines"
        });
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
            "login\tRequire the selected line password"
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
            ? "hostname <name>\nenable password <value>\nenable secret <value>\ninterface <name>\nip route <network> <mask> <next-hop>\nline console 0|vty 0 4\n"
            : "hostname <name>\nenable password <value>\nenable secret <value>\ninterface <name>|range <first> - <last>|Vlan<id>\nvlan <id>\nline console 0|vty 0 4\n",
        TssNetworkCliMode.InterfaceConfig => category == EquipmentCategory.Router
            ? "shutdown | no shutdown\nip address <address> <mask> | ip add <address> <mask>\nencapsulation dot1q <vlan>\n"
            : "shutdown | no shutdown\ndescription <text> | no description\nswitchport trunk encapsulation dot1q\nswitchport mode access|trunk\nswitchport access vlan <id> | no switchport access vlan\nswitchport trunk allowed vlan <id>[,<id>]\nswitchport trunk native vlan <id>\ninterface Vlan<id>\nip address <address> <mask> | ip add <address> <mask>\n",
        TssNetworkCliMode.LineConfig => "password <value>\nlogin\n",
        TssNetworkCliMode.VlanConfig => category == EquipmentCategory.Switch ? "name <name>\nstate suspend\n" : "% VLAN configuration is switch-only.\n",
        _ => category == EquipmentCategory.Router
            ? "enable | configure terminal | show running-config | show startup-config | show ip interface brief | show interfaces | show ip route | ping <ip>\n"
            : "enable | configure terminal | show running-config | show startup-config | show vlan brief | show interfaces trunk | show ip interface brief | show interfaces | show mac address-table | ping <ip>\n"
    };
    public static string BuildRunningConfig(NetworkDeviceState d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("version 15.0");
        sb.AppendLine("!");
        sb.AppendLine("hostname " + d.Hostname);
        if (!string.IsNullOrWhiteSpace(d.EnablePassword)) sb.AppendLine("enable password " + (d.PasswordEncryption ? "<encrypted>" : d.EnablePassword));
        if (!string.IsNullOrWhiteSpace(d.EnableSecret)) sb.AppendLine("enable secret <configured>");
        if (d.PasswordEncryption) sb.AppendLine("service password-encryption");
        if (d.DomainLookupDisabled) sb.AppendLine("no ip domain-lookup");
        if (!string.IsNullOrWhiteSpace(d.Motd))
        {
            sb.AppendLine("!");
            var motd = d.Motd.Trim();
            const string legacyPrefix = "banner motd #";
            if (motd.StartsWith(legacyPrefix, StringComparison.OrdinalIgnoreCase))
                motd = motd.Substring(legacyPrefix.Length);
            if (motd.EndsWith("#", StringComparison.Ordinal))
                motd = motd[..^1];
            sb.AppendLine(motd);
        }
        sb.AppendLine("!");
        foreach (var i in d.Interfaces.Values.OrderBy(x => InterfaceOrderKey(x.Name), StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine("interface " + i.Name);
            if (!string.IsNullOrWhiteSpace(i.Description)) sb.AppendLine(" description " + i.Description);
            if (d.Category == EquipmentCategory.Switch)
            {
                if (i.TrunkEncapsulationDot1q && i.IsTrunk)
                    sb.AppendLine(" switchport trunk encapsulation dot1q");
                if (i.SwitchportModeConfigured)
                    sb.AppendLine(" switchport mode " + (i.IsTrunk ? "trunk" : "access"));
                if (i.IsTrunk && i.AllowedVlans.Count > 0)
                    sb.AppendLine(" switchport trunk allowed vlan " + string.Join(",", i.AllowedVlans.OrderBy(x => x)));
                if (i.IsTrunk && i.NativeVlan != 1)
                    sb.AppendLine(" switchport trunk native vlan " + i.NativeVlan);
                if (!i.IsTrunk && i.SwitchportAccessVlanConfigured)
                    sb.AppendLine(" switchport access vlan " + i.AccessVlan);
            }
            if (!i.AdminUp) sb.AppendLine(" shutdown");
            if (i.Dot1qVlan > 0) sb.AppendLine(" encapsulation dot1q " + i.Dot1qVlan);
            if (!string.IsNullOrEmpty(i.Address)) sb.AppendLine(" ip address " + i.Address + " " + i.Mask);
            sb.AppendLine("!");
        }
        if (d.Category == EquipmentCategory.Switch && (!string.IsNullOrWhiteSpace(d.ManagementAddress) || (d.Svis.TryGetValue(1, out var vlan1State) && !string.IsNullOrWhiteSpace(vlan1State.Description))))
        {
            sb.AppendLine("interface Vlan1");
            if (d.Svis.TryGetValue(1, out var vlan1) && !string.IsNullOrWhiteSpace(vlan1.Description)) sb.AppendLine(" description " + vlan1.Description);
            if (!string.IsNullOrWhiteSpace(d.ManagementAddress)) sb.AppendLine(" ip address " + d.ManagementAddress + " " + d.ManagementMask);
            sb.AppendLine("!");
        }
        if (d.Category == EquipmentCategory.Switch)
            foreach (var svi in d.Svis.Values.Where(x => !x.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(svi.Description) && string.IsNullOrWhiteSpace(svi.Address) && svi.AdminUp) continue;
                sb.AppendLine("interface " + svi.Name);
                if (!string.IsNullOrWhiteSpace(svi.Description)) sb.AppendLine(" description " + svi.Description);
                if (!string.IsNullOrWhiteSpace(svi.Address)) sb.AppendLine(" ip address " + svi.Address + " " + svi.Mask);
                if (!svi.AdminUp) sb.AppendLine(" shutdown");
                sb.AppendLine("!");
            }
        if (!string.IsNullOrWhiteSpace(d.ConsolePassword) || d.ConsoleLoginRequired)
        {
            sb.AppendLine("line console 0");
            if (!string.IsNullOrWhiteSpace(d.ConsolePassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.ConsolePassword));
            if (d.ConsoleLoginRequired) sb.AppendLine(" login");
            sb.AppendLine("!");
        }
        if (!string.IsNullOrWhiteSpace(d.VtyPassword) || d.VtyLoginRequired)
        {
            sb.AppendLine("line vty 0 4");
            if (!string.IsNullOrWhiteSpace(d.VtyPassword)) sb.AppendLine(" password " + (d.PasswordEncryption ? "<encrypted>" : d.VtyPassword));
            if (d.VtyLoginRequired) sb.AppendLine(" login");
            sb.AppendLine("!");
        }
        foreach (var v in d.Vlans.OrderBy(x => x.Key))
        {
            sb.AppendLine("vlan " + v.Key);
            if (!string.Equals(v.Value, "default", StringComparison.OrdinalIgnoreCase))
                sb.AppendLine(" name " + v.Value);
            sb.AppendLine("!");
        }
        foreach (var r in d.Routes)
        {
            sb.AppendLine("ip route " + r.Network + " " + r.Mask + " " + r.NextHop);
            sb.AppendLine("!");
        }
        return sb.Append("end\n").ToString();
    }

    public static IReadOnlyList<string> ResultsShowCommands(EquipmentCategory category)
    {
        return category == EquipmentCategory.Router
            ? new[] { "show running-config", "show ip interface brief", "show route" }
            : new[] { "show running-config", "show vlan brief", "show interfaces trunk", "show ip interface brief", "show interfaces", "show mac address-table" };
    }

    public static string BuildResultsShowReview(NetworkDeviceState device, IReadOnlyList<string> incorrectCriteria = null)
    {
        var sb = new StringBuilder();
        foreach (var command in ResultsShowCommands(device?.Category ?? default))
        {
            sb.AppendLine("<color=#FFFFFF>===== " + command.ToUpperInvariant() + " =====</color>");
            var context = string.Empty;
            foreach (var line in BuildResultsShowOutput(device, command).Split('\n'))
            {
                if (line.StartsWith("interface ", StringComparison.OrdinalIgnoreCase)) context = line.Substring("interface ".Length).Trim();
                if (line.StartsWith("line ", StringComparison.OrdinalIgnoreCase)) context = line.Trim();
                if (line == "!") context = string.Empty;
                var escaped = line.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                sb.AppendLine(IsIncorrectResultsLine(command, line, context, incorrectCriteria) ? "<color=#FF4D4D>" + escaped + "</color>" : "<color=#FFFFFF>" + escaped + "</color>");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static bool IsIncorrectResultsLine(string command, string line, string context, IReadOnlyList<string> criteria)
    {
        if (criteria == null || criteria.Count == 0 || string.IsNullOrWhiteSpace(line)) return false;
        var lowerCommand = command.ToLowerInvariant();
        var lowerLine = line.ToLowerInvariant();
        var lowerContext = (context ?? string.Empty).ToLowerInvariant();
        foreach (var criterion in criteria)
        {
            var warning = (criterion ?? string.Empty).ToLowerInvariant();
            if (lowerLine.StartsWith("vlan name") || lowerLine.StartsWith("----") || lowerLine.StartsWith("interface ")) continue;
            var interfaceName = ExtractInterfaceName(warning);
            if (!string.IsNullOrEmpty(interfaceName) && (lowerContext == interfaceName || lowerLine.Contains(interfaceName))) return true;
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

    public static string BuildResultsShowOutput(NetworkDeviceState device, string command)
    {
        if (device == null) return string.Empty;
        var lower = (command ?? string.Empty).ToLowerInvariant();
        if (lower == "show running-config") return BuildRunningConfig(device);
        if (lower == "show startup-config") return device.StartupSnapshot == null ? "startup-config is not present\n" : BuildRunningConfig(device.StartupSnapshot);
        if (lower == "show vlan brief") return Vlans(device);
        if (lower == "show interfaces trunk") return Trunks(device);
        if (lower == "show ip interface brief" || lower == "show interfaces") return Interfaces(device);
        if (lower == "show ip route" || lower == "show route") return ResultsRoutes(device);
        if (lower == "show mac address-table") return "Mac Address Table\n(no dynamic entries until endpoints are reachable)\n";
        return "% Invalid show command.\n";
    }

    private static string ResultsRoutes(NetworkDeviceState d)
    {
        var sb = new StringBuilder("Codes: C - connected, S - static\n");
        foreach (var i in d.Interfaces.Values.Where(i => i.AdminUp && i.CarrierUp && TssNetworkAddress.IsAddress(i.Address) && TssNetworkAddress.IsMask(i.Mask)))
            sb.AppendLine("C    " + i.Address + "/" + TssNetworkAddress.PrefixLength(i.Mask) + " is directly connected, " + i.Name);
        foreach (var r in d.Routes.Where(r => IsInstalledStaticRoute(d, r)))
            sb.AppendLine("S*   " + r.Network + "/0 [1/0] via " + r.NextHop);
        return sb.ToString();
    }
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
    private static string Vlans(NetworkDeviceState d) { var ports = new Dictionary<int, List<string>>(); foreach (var i in d.Interfaces.Values) if (!i.IsTrunk) { if (!ports.TryGetValue(i.AccessVlan, out var list)) ports[i.AccessVlan] = list = new List<string>(); list.Add(i.Name); } var ids = new SortedSet<int> { 1 }; foreach (var v in d.Vlans.Keys) ids.Add(v); foreach (var v in ports.Keys) ids.Add(v); var sb = new StringBuilder("VLAN Name                             Status    Ports\n---- -------------------------------- --------- -------------------------------\n"); foreach (var id in ids) { var name = d.Vlans.TryGetValue(id, out var configured) ? configured.Replace(" (suspended)", string.Empty, StringComparison.OrdinalIgnoreCase) : id == 1 ? "default" : "VLAN" + id; ports.TryGetValue(id, out var list); sb.AppendLine($"{id,-4} {name,-32} active    {string.Join(", ", list?.OrderBy(x => x) ?? Enumerable.Empty<string>())}"); } return sb.ToString(); }
    private static string Trunks(NetworkDeviceState d) { var sb = new StringBuilder(); foreach (var i in d.Interfaces.Values.Where(x => x.IsTrunk)) sb.AppendLine($"{i.Name} on 802.1q trunking\nNative vlan: {i.NativeVlan}\nVlans allowed on trunk: {string.Join(",", i.AllowedVlans.OrderBy(x => x))}"); return sb.Length == 0 ? "No active trunks.\n" : sb.ToString(); }
    private static string Interfaces(NetworkDeviceState d) { var sb = new StringBuilder("Interface                  IP-Address      Status                Protocol\n"); foreach (var i in d.Svis.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)) { var address = i.Name.Equals("Vlan1", StringComparison.OrdinalIgnoreCase) ? d.ManagementAddress : i.Address; var status = i.AdminUp ? "up" : "administratively down"; sb.AppendLine($"{i.Name,-26} {(string.IsNullOrWhiteSpace(address) ? "unassigned" : address),-15} {status,-21} {(i.AdminUp ? "up" : "down")}"); } foreach (var i in d.Interfaces.Values) { var status = i.AdminUp ? (i.CarrierUp ? "up" : "down") : "administratively down"; var protocolUp = i.AdminUp && i.CarrierUp && (d.Category != EquipmentCategory.Router || !i.Name.Contains('.') || i.Dot1qVlan > 0); sb.AppendLine($"{i.Name,-26} {(i.Address ?? "unassigned"),-15} {status,-21} {(protocolUp ? "up" : "down")}"); } return sb.ToString(); }
    private string Routes(NetworkDeviceState d) { var sb = new StringBuilder("Codes: C - connected, S - static\n"); foreach (var i in d.Interfaces.Values.Where(i => _network.IsInterfaceIpRoutingAvailable(d, i))) sb.AppendLine("C    " + i.Address + "/" + TssNetworkAddress.PrefixLength(i.Mask) + " is directly connected, " + i.Name); foreach (var r in d.Routes.Where(r => IsInstalledStaticRoute(d, r))) sb.AppendLine("S*   " + r.Network + "/0 [1/0] via " + r.NextHop); return sb.ToString(); }
    private static bool IsInstalledStaticRoute(NetworkDeviceState d, NetworkRouteState route) => d.Interfaces.Values.Any(i => i.AdminUp && i.CarrierUp && TssNetworkAddress.IsAddress(i.Address) && TssNetworkAddress.IsMask(i.Mask) && TssNetworkAddress.IsInSameSubnet(i.Address, i.Mask, route.NextHop));
}
