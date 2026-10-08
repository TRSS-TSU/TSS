using System;
using System.Collections.Generic;

public readonly struct TssReachabilityResult
{
    public readonly bool Success;
    public readonly int FailedLayer;
    public readonly string Diagnostic;
    public readonly string SourceRole;
    public readonly string DestinationRole;
    public readonly IReadOnlyList<string> TraceHops;
    public TssReachabilityResult(bool success, int failedLayer, string diagnostic, string sourceRole, string destinationRole, IReadOnlyList<string> traceHops)
    { Success = success; FailedLayer = failedLayer; Diagnostic = diagnostic; SourceRole = sourceRole; DestinationRole = destinationRole; TraceHops = traceHops; }
}

public sealed class TssNetworkReachabilityEvaluator
{
    private readonly TssNetworkSessionState _state;
    public TssNetworkReachabilityEvaluator(TssNetworkSessionState state) { _state = state; }

    public TssReachabilityResult Evaluate(string sourceKey, string destinationAddress)
    {
        if (_state == null || !_state.TryGetEndpoint(sourceKey, out var source))
            return Fail(1, "Layer 1: source endpoint is unavailable.", string.Empty, string.Empty);
        if (!source.Layer1Connected)
            return Fail(1, "Layer 1: no usable physical path.", source.Role, string.Empty);
        if (!source.Layer2Connected)
            return Fail(2, "Layer 2: switching path is unavailable.", source.Role, string.Empty);
        if (!source.HasValidAddress || !TssNetworkAddress.IsAddress(source.Gateway))
            return Fail(3, "Layer 3: endpoint addressing is invalid.", source.Role, string.Empty);

        var profile = _state.Profile;
        var destinationRole = ResolveRole(destinationAddress, profile);
        if (string.IsNullOrEmpty(destinationRole))
            return Fail(3, "Layer 3: destination does not respond.", source.Role, string.Empty);
        if (destinationAddress == profile.WanProviderAddress)
            return ProviderResult(source, profile);
        if (!GatewayMatches(source, profile))
            return Fail(3, "Layer 3: default gateway is unavailable.", source.Role, destinationRole);
        return new TssReachabilityResult(true, 0, "Reachable.", source.Role, destinationRole, new[] { source.Gateway, destinationAddress });
    }

    private TssReachabilityResult ProviderResult(EndpointNetworkState source, TssNetworkRunProfile profile)
    {
        if (!GatewayMatches(source, profile)) return Fail(3, "Layer 3: default gateway is unavailable.", source.Role, "Provider");
        foreach (var candidate in _state.Devices)
            if (candidate.Category == EquipmentCategory.Router && HasProviderRoute(candidate, profile))
                return new TssReachabilityResult(true, 0, "Reachable.", source.Role, "Provider", new[] { source.Gateway, profile.WanCustomerAddress, profile.WanProviderAddress });
        return Fail(3, "Layer 3: provider route is unavailable.", source.Role, "Provider");
    }

    private static bool HasProviderRoute(NetworkDeviceState router, TssNetworkRunProfile profile)
    {
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == profile.WanProviderAddress) return true;
        return false;
    }

    private static bool GatewayMatches(EndpointNetworkState source, TssNetworkRunProfile profile) => source.Gateway == EndpointNetworkState.GatewayFor(source.Address, profile);
    private static string ResolveRole(string address, TssNetworkRunProfile profile)
    {
        if (profile == null) return string.Empty;
        if (address == profile.WanProviderAddress) return "Provider";
        foreach (var pair in profile.EndpointAddresses) if (pair.Value == address) return pair.Key;
        if (address == profile.ManagementNetwork + ".1") return "Management gateway";
        if (address == profile.ReceptionNetwork + ".1") return "Reception gateway";
        if (address == profile.DataNetwork + ".1") return "Data gateway";
        return string.Empty;
    }

    private static TssReachabilityResult Fail(int layer, string diagnostic, string source, string destination) => new(false, layer, diagnostic, source, destination, Array.Empty<string>());
}
