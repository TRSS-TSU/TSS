using System;
using System.Collections.Generic;

public sealed class TssWorkPackageResult
{
    public readonly List<string> UnmetConditions = new();
    public bool Complete => UnmetConditions.Count == 0;
}

public sealed class TssNetworkWorkPackageEvaluator
{
    private readonly TssNetworkSessionState _state;
    public TssNetworkWorkPackageEvaluator(TssNetworkSessionState state) { _state = state; }

    public TssWorkPackageResult EvaluateLayer2()
    {
        var result = new TssWorkPackageResult();
        var an1 = Find("AN-1"); var an2 = Find("AN-2");
        CheckSwitch(an1, "AN-1", result); CheckSwitch(an2, "AN-2", result);
        return result;
    }

    public TssWorkPackageResult EvaluateLayer3()
    {
        var result = new TssWorkPackageResult();
        var router = Find("CN-1");
        if (router == null) { result.UnmetConditions.Add("Router configuration is unavailable."); return result; }
        if (!router.Evidence.Contains("show ip route")) result.UnmetConditions.Add("Required router verification was not typed.");
        if (!router.Evidence.Contains("save") || router.StartupSnapshot == null) result.UnmetConditions.Add("Router configuration has not been saved.");
        var hasDefault = false;
        foreach (var route in router.Routes)
            if (route.Network == "0.0.0.0" && route.Mask == "0.0.0.0" && route.NextHop == _state.Profile.WanProviderAddress) { hasDefault = true; break; }
        if (!hasDefault) result.UnmetConditions.Add("Provider routing is incomplete.");
        return result;
    }

    private void CheckSwitch(NetworkDeviceState device, string name, TssWorkPackageResult result)
    {
        if (device == null) { result.UnmetConditions.Add(name + " configuration is unavailable."); return; }
        if (!device.Evidence.Contains("show vlan brief") || !device.Evidence.Contains("show interfaces trunk")) result.UnmetConditions.Add(name + " verification was not typed.");
        if (!device.Evidence.Contains("save") || device.StartupSnapshot == null) result.UnmetConditions.Add(name + " configuration has not been saved.");
        if (device.Vlans.Count < 6) result.UnmetConditions.Add(name + " VLAN database is incomplete.");
    }

    private NetworkDeviceState Find(string hostname)
    {
        foreach (var device in _state.Devices)
            if (device.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase)) return device;
        return null;
    }
}
