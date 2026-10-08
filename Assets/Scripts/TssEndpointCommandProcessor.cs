using System;
using System.Text;

public readonly struct TssEndpointCommandResult
{
    public readonly string Output;
    public readonly bool StateChanged;
    public readonly bool Accepted;
    public TssEndpointCommandResult(string output, bool stateChanged, bool accepted) { Output = output; StateChanged = stateChanged; Accepted = accepted; }
}

public sealed class TssEndpointCommandProcessor
{
    private readonly TssNetworkSessionState _state;
    private readonly TssNetworkReachabilityEvaluator _reachability;
    public TssEndpointCommandProcessor(TssNetworkSessionState state) { _state = state; _reachability = new TssNetworkReachabilityEvaluator(state); }

    public TssEndpointCommandResult Execute(string sourceKey, string input)
    {
        var command = Normalize(input);
        if (command.Equals("cls", StringComparison.OrdinalIgnoreCase)) return new TssEndpointCommandResult(string.Empty, false, true);
        if (!_state.TryGetEndpoint(sourceKey, out var source)) return new TssEndpointCommandResult("Network endpoint is unavailable.\n", false, false);
        if (command.Equals("ipconfig", StringComparison.OrdinalIgnoreCase)) return new TssEndpointCommandResult(IpConfig(source), false, true);
        if (command.StartsWith("ping ", StringComparison.OrdinalIgnoreCase)) return Diagnose(sourceKey, command.Substring(5), false);
        if (command.StartsWith("tracert ", StringComparison.OrdinalIgnoreCase)) return Diagnose(sourceKey, command.Substring(7), true);
        return new TssEndpointCommandResult("'" + command + "' is not recognized.\n", false, false);
    }

    public bool TryApply(string sourceKey, bool useDhcp, string address, string mask, string gateway, string dns, out string error)
    {
        error = string.Empty;
        if (!_state.TryGetEndpoint(sourceKey, out var source)) { error = "Network endpoint is unavailable."; return false; }
        address = (address ?? string.Empty).Trim(); mask = (mask ?? string.Empty).Trim(); gateway = (gateway ?? string.Empty).Trim(); dns = (dns ?? string.Empty).Trim();
        if ((!string.IsNullOrWhiteSpace(address) && !TssNetworkAddress.IsAddress(address))
            || (!string.IsNullOrWhiteSpace(mask) && !TssNetworkAddress.IsMask(mask))
            || (!string.IsNullOrWhiteSpace(gateway) && !TssNetworkAddress.IsAddress(gateway))
            || (!string.IsNullOrWhiteSpace(dns) && !TssNetworkAddress.IsAddress(dns))
            || (!useDhcp && (!TssNetworkAddress.IsAddress(address) || !TssNetworkAddress.IsMask(mask) || !TssNetworkAddress.IsAddress(gateway))))
        { error = "Enter valid IPv4 settings."; return false; }
        source.UseDhcp = useDhcp;
        if (!useDhcp) { source.Address = address; source.Mask = mask; source.Gateway = gateway; source.Dns = dns; }
        return true;
    }

    private TssEndpointCommandResult Diagnose(string sourceKey, string target, bool trace)
    {
        if (!TssNetworkAddress.IsAddress(target)) return new TssEndpointCommandResult("Invalid IPv4 address.\n", false, false);
        var result = _reachability.Evaluate(sourceKey, target);
        _state.TryGetEndpoint(sourceKey, out var source);
        var sb = new StringBuilder();
        if (trace)
        {
            sb.AppendLine("Tracing route to " + target);
            foreach (var hop in result.TraceHops) sb.AppendLine("  " + hop);
        }
        sb.AppendLine(result.Success ? "Reply from " + target + ": reachable." : result.Diagnostic);
        if (result.Success)
            RecordRequiredEvidence(sourceKey, source, "ping " + target);
        return new TssEndpointCommandResult(sb.ToString(), false, true);
    }

    private void RecordRequiredEvidence(string sourceKey, EndpointNetworkState source, string command)
    {
        foreach (var requirement in _state.Profile?.EvidenceRequirements ?? Array.Empty<NetworkEvidenceRequirement>())
        {
            if (requirement == null || string.IsNullOrWhiteSpace(requirement.evidenceId) || !Normalize(requirement.command).Equals(command, StringComparison.OrdinalIgnoreCase))
                continue;
            source.Evidence.Add(requirement.evidenceId);
            _state.Evidence.Add(sourceKey + ":" + requirement.evidenceId);
        }
    }

    private static string Normalize(string input)
    {
        return string.Join(" ", (input ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string IpConfig(EndpointNetworkState source) => $"Windows IP Configuration\n\nIPv4 Address . . . . . . : {source.Address}\nSubnet Mask  . . . . . . : {source.Mask}\nDefault Gateway . . . .  : {source.Gateway}\nDNS Servers . . . . . . : {source.Dns}\nPhysical Address . . . . : {source.MacAddress}\n";
}
