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
        var lower = command.ToLowerInvariant();
        if (lower == "?" || lower == "help" || lower.EndsWith("?")) return Result(device, Help(device.Mode), false, true);
        if (lower.StartsWith("do "))
        {
            if (device.Mode == TssNetworkCliMode.UserExec || device.Mode == TssNetworkCliMode.PrivilegedExec) return Invalid(device);
            return ExecuteExec(device, command.Substring(3));
        }
        if (lower is "enable" or "en") { device.Mode = TssNetworkCliMode.PrivilegedExec; return Result(device, string.Empty, false, true); }
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
        if (lower.StartsWith("show ") || lower.StartsWith("sh ") || lower.StartsWith("ping ")) return ExecuteExec(device, command);
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
        if (lower.StartsWith("hostname ") && parts.Length == 2) { d.Hostname = parts[1]; return Changed(d, string.Empty); }
        if (lower.StartsWith("enable secret ") && parts.Length >= 3) { d.EnableSecret = string.Join(" ", parts.Skip(2)); return Changed(d, string.Empty); }
        if (lower == "service password-encryption") { d.PasswordEncryption = true; return Changed(d, string.Empty); }
        if (lower == "no ip domain-lookup") { d.DomainLookupDisabled = true; return Changed(d, string.Empty); }
        if (lower.StartsWith("banner motd ") && command.Length > 13)
        {
            var rest = command.Substring(12).Trim(); if (rest.Length < 3 || rest[0] != rest[^1]) return Incomplete(d, "banner motd <delimiter>text<delimiter>");
            d.Motd = rest.Substring(1, rest.Length - 2); return Changed(d, string.Empty);
        }
        if (lower is "line console 0" or "line con 0") { d.SelectedLine = "console"; d.Mode = TssNetworkCliMode.LineConfig; return Result(d, string.Empty, false, true); }
        if (lower == "line vty 0 4") { d.SelectedLine = "vty"; d.Mode = TssNetworkCliMode.LineConfig; return Result(d, string.Empty, false, true); }
        if (lower.StartsWith("vlan ") && int.TryParse(parts.LastOrDefault(), out var vlan) && ValidVlan(vlan)) { d.SelectedVlan = vlan; if (!d.Vlans.ContainsKey(vlan)) d.Vlans[vlan] = "VLAN" + vlan; d.Mode = TssNetworkCliMode.VlanConfig; return Changed(d, string.Empty); }
        if (lower.StartsWith("interface ") || lower.StartsWith("int "))
            return SelectInterfaces(d, command.Substring(command.IndexOf(' ') + 1));
        if (lower.StartsWith("ip route ") && parts.Length == 5 && ValidAddress(parts[2]) && ValidMask(parts[3]) && ValidAddress(parts[4])) { d.Routes.Add(new NetworkRouteState { Network = parts[2], Mask = parts[3], NextHop = parts[4] }); return Changed(d, string.Empty); }
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
        else { if (!TryCanonical(selection, out var canonical, d.Category == EquipmentCategory.Router)) return Invalid(d); names.Add(canonical); }
        d.SelectedInterfaces = names;
        foreach (var name in names) if (!d.Interfaces.ContainsKey(name)) d.Interfaces[name] = new NetworkInterfaceState { Name = name, AdminUp = d.Category == EquipmentCategory.Switch };
        d.Mode = TssNetworkCliMode.InterfaceConfig; return Result(d, string.Empty, false, true);
    }

    private TssCliResult Interface(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant(); var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (lower is "shutdown" or "shut") { foreach (var i in Selected(d)) i.AdminUp = false; return Changed(d, string.Empty); }
        if (lower is "no shutdown" or "no shut") { foreach (var i in Selected(d)) i.AdminUp = true; return Changed(d, string.Empty); }
        if (lower == "switchport mode access") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.IsTrunk = false; return Changed(d, string.Empty); }
        if (lower == "switchport mode trunk") { if (d.Category != EquipmentCategory.Switch) return Invalid(d); foreach (var i in Selected(d)) i.IsTrunk = true; return Changed(d, string.Empty); }
        if (lower.StartsWith("switchport access vlan ") && int.TryParse(parts.LastOrDefault(), out var vlan) && ValidVlan(vlan)) { foreach (var i in Selected(d)) i.AccessVlan = vlan; return Changed(d, string.Empty); }
        if (lower.StartsWith("switchport trunk native vlan ") && int.TryParse(parts.LastOrDefault(), out var native) && ValidVlan(native)) { foreach (var i in Selected(d)) i.NativeVlan = native; return Changed(d, string.Empty); }
        if (lower.StartsWith("switchport trunk allowed vlan "))
        {
            if (!TryVlans(parts.LastOrDefault(), out var vlans)) return Invalid(d); foreach (var i in Selected(d)) { i.AllowedVlans.Clear(); foreach (var allowedVlan in vlans) i.AllowedVlans.Add(allowedVlan); } return Changed(d, string.Empty);
        }
        if (lower.StartsWith("ip address ") && parts.Length == 4 && ValidAddress(parts[2]) && ValidMask(parts[3])) { foreach (var i in Selected(d)) { i.Address = parts[2]; i.Mask = parts[3]; } return Changed(d, string.Empty); }
        if (lower.StartsWith("encapsulation dot1q ") && int.TryParse(parts.LastOrDefault(), out var dot1q) && ValidVlan(dot1q)) { foreach (var i in Selected(d)) i.Dot1qVlan = dot1q; return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult Line(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (command.Equals("login", StringComparison.OrdinalIgnoreCase)) return Changed(d, string.Empty);
        if (parts.Length >= 2 && parts[0].Equals("password", StringComparison.OrdinalIgnoreCase)) { if (d.SelectedLine == "console") d.ConsolePassword = parts[1]; else d.VtyPassword = parts[1]; return Changed(d, string.Empty); }
        return Invalid(d);
    }
    private TssCliResult Vlan(NetworkDeviceState d, string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 2 && parts[0].Equals("name", StringComparison.OrdinalIgnoreCase)) { d.Vlans[d.SelectedVlan] = parts[1]; return Changed(d, string.Empty); }
        if (command.Equals("state suspend", StringComparison.OrdinalIgnoreCase)) { d.Vlans[d.SelectedVlan] = d.Vlans[d.SelectedVlan] + " (suspended)"; return Changed(d, string.Empty); }
        return Invalid(d);
    }

    private TssCliResult ExecuteExec(NetworkDeviceState d, string command)
    {
        var lower = command.ToLowerInvariant();
        if (lower.StartsWith("sh ")) { command = "show" + command.Substring(2); lower = command.ToLowerInvariant(); }
        if (lower is "show running-config" or "show run") return Result(d, Running(d), false, true);
        if (lower is "show startup-config" or "show start") return Result(d, d.StartupSnapshot == null ? "startup-config is not present\n" : Running(d.StartupSnapshot), false, true);
        if (lower.StartsWith("show vlan")) { d.Evidence.Add("show vlan brief"); _network.Evidence.Add("show vlan brief"); return Result(d, Vlans(d), false, true); }
        if (lower.StartsWith("show interfaces trunk") || lower == "show int trunk") { d.Evidence.Add("show interfaces trunk"); _network.Evidence.Add("show interfaces trunk"); return Result(d, Trunks(d), false, true); }
        if (lower.StartsWith("show ip interface") || lower.StartsWith("show ip int")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show interfaces")) return Result(d, Interfaces(d), false, true);
        if (lower.StartsWith("show mac")) return Result(d, "Mac Address Table\n(no dynamic entries until endpoints are reachable)\n", false, true);
        if (lower.StartsWith("show ip route") || lower == "show route") { d.Evidence.Add("show ip route"); _network.Evidence.Add("show ip route"); return Result(d, Routes(d), false, true); }
        if (lower.StartsWith("ping ")) return Result(d, "Sending 5, 100-byte ICMP Echos to " + command.Split(' ').Last() + ", timeout is 2 seconds:\n.....\nSuccess rate is 0 percent (0/5) - use endpoint diagnostics for Phase 19 evidence\n", false, true);
        return Invalid(d);
    }
    private TssCliResult Exit(NetworkDeviceState d) { d.Mode = d.Mode switch { TssNetworkCliMode.InterfaceConfig or TssNetworkCliMode.LineConfig or TssNetworkCliMode.VlanConfig => TssNetworkCliMode.GlobalConfig, TssNetworkCliMode.GlobalConfig => TssNetworkCliMode.PrivilegedExec, TssNetworkCliMode.PrivilegedExec => TssNetworkCliMode.UserExec, _ => TssNetworkCliMode.UserExec }; return Result(d, string.Empty, false, true); }
    private TssCliResult Changed(NetworkDeviceState d, string text) { _session.NotifyNetworkStateChanged(); return Result(d, text, true, true); }
    private static TssCliResult Result(NetworkDeviceState d, string text, bool changed, bool accepted) => new(text, d.Mode, changed, accepted);
    private static TssCliResult Invalid(NetworkDeviceState d) => Result(d, "% Invalid input detected at '^' marker.\n", false, false);
    private static TssCliResult Incomplete(NetworkDeviceState d, string syntax) => Result(d, "% Incomplete command. Syntax: " + syntax + "\n", false, false);
    private static IEnumerable<NetworkInterfaceState> Selected(NetworkDeviceState d) => d.SelectedInterfaces.Where(d.Interfaces.ContainsKey).Select(x => d.Interfaces[x]);
    private static bool ValidVlan(int value) => value is >= 1 and <= 4094;
    private static bool ValidAddress(string value) => TssNetworkAddress.IsAddress(value);
    private static bool ValidMask(string value) => TssNetworkAddress.IsMask(value);
    private static bool TryVlans(string value, out List<int> vlans) { vlans = new List<int>(); if (string.IsNullOrWhiteSpace(value)) return false; foreach (var item in value.Split(',')) { if (!int.TryParse(item, out var vlan) || !ValidVlan(vlan)) return false; vlans.Add(vlan); } return vlans.Count > 0; }
    private static bool TryPort(string value, out int port) { port = 0; return TryCanonical(value, out var n) && int.TryParse(n.Split('/').Last(), out port); }
    private static bool TryCanonical(string value, out string name) => TryCanonical(value, out name, false);

    private static bool TryCanonical(string value, out string name, bool allowFastRouterAliases)
    {
        name = null;
        var s = value.Replace("FastEthernet", "Fa", StringComparison.OrdinalIgnoreCase).Replace("GigabitEthernet", "Gi", StringComparison.OrdinalIgnoreCase).Trim();
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
    private static string Help(TssNetworkCliMode mode) => mode switch
    {
        TssNetworkCliMode.GlobalConfig => "hostname <name>\nenable secret <value>\ninterface <name>|range <first> - <last>\ninterface Fa0/0.<vlan>\nvlan <id>\nline console 0|vty 0 4\nip route <network> <mask> <next-hop>\n",
        TssNetworkCliMode.InterfaceConfig => "shutdown | no shutdown\nswitchport mode access|trunk\nswitchport access vlan <id>\nswitchport trunk allowed vlan <id>[,<id>]\nswitchport trunk native vlan <id>\nip address <address> <mask>\nencapsulation dot1q <vlan>\n",
        TssNetworkCliMode.LineConfig => "password <value>\nlogin\n",
        TssNetworkCliMode.VlanConfig => "name <name>\nstate suspend\n",
        _ => "enable | configure terminal | show running-config | show startup-config | show vlan brief | show interfaces trunk | show ip interface brief | show ip route\n"
    };
    private static string Running(NetworkDeviceState d) { var sb = new StringBuilder("version 15.0\n"); sb.AppendLine("hostname " + d.Hostname); if (d.PasswordEncryption) sb.AppendLine("service password-encryption"); if (d.DomainLookupDisabled) sb.AppendLine("no ip domain-lookup"); foreach (var v in d.Vlans) sb.AppendLine("vlan " + v.Key + "\n name " + v.Value); foreach (var i in d.Interfaces.Values) { sb.AppendLine("interface " + i.Name); if (i.IsTrunk) { sb.AppendLine(" switchport mode trunk"); sb.AppendLine(" switchport trunk allowed vlan " + string.Join(",", i.AllowedVlans.OrderBy(x => x))); sb.AppendLine(" switchport trunk native vlan " + i.NativeVlan); } else sb.AppendLine(" switchport access vlan " + i.AccessVlan); if (!i.AdminUp) sb.AppendLine(" shutdown"); if (!string.IsNullOrEmpty(i.Address)) sb.AppendLine(" ip address " + i.Address + " " + i.Mask); if (i.Dot1qVlan > 0) sb.AppendLine(" encapsulation dot1q " + i.Dot1qVlan); } foreach (var r in d.Routes) sb.AppendLine("ip route " + r.Network + " " + r.Mask + " " + r.NextHop); return sb.Append("end\n").ToString(); }
    private static string Vlans(NetworkDeviceState d) { var sb = new StringBuilder("VLAN Name                             Status    Ports\n"); foreach (var v in d.Vlans.OrderBy(x => x.Key)) sb.AppendLine($"{v.Key,-4} {v.Value,-32} active"); return sb.ToString(); }
    private static string Trunks(NetworkDeviceState d) { var sb = new StringBuilder(); foreach (var i in d.Interfaces.Values.Where(x => x.IsTrunk)) sb.AppendLine($"{i.Name} on 802.1q trunking\nNative vlan: {i.NativeVlan}\nVlans allowed on trunk: {string.Join(",", i.AllowedVlans.OrderBy(x => x))}"); return sb.Length == 0 ? "No active trunks.\n" : sb.ToString(); }
    private static string Interfaces(NetworkDeviceState d) { var sb = new StringBuilder("Interface                  IP-Address      Status                Protocol\n"); foreach (var i in d.Interfaces.Values) { var status = i.AdminUp ? (i.CarrierUp ? "up" : "down") : "administratively down"; sb.AppendLine($"{i.Name,-26} {(i.Address ?? "unassigned"),-15} {status,-21} {(i.AdminUp && i.CarrierUp ? "up" : "down")}"); } return sb.ToString(); }
    private static string Routes(NetworkDeviceState d) { var sb = new StringBuilder("Codes: C - connected, S - static\n"); foreach (var i in d.Interfaces.Values.Where(i => i.AdminUp && i.CarrierUp && !string.IsNullOrEmpty(i.Address))) sb.AppendLine("C    " + i.Address + "/24 is directly connected, " + i.Name); foreach (var r in d.Routes) sb.AppendLine("S*   " + r.Network + "/0 [1/0] via " + r.NextHop); return sb.ToString(); }
}
