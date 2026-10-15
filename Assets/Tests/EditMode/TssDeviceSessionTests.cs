using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TssDeviceSessionTests
{
    [Test]
    public void LinkIndicatorResubscribesAfterBeingReenabled()
    {
        var sessionObject = new GameObject("Session");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        var indicatorObject = new GameObject("Indicator");
        var indicator = indicatorObject.AddComponent<TssDeviceLinkIndicator>();

        try
        {
            typeof(TssTrainingSession).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(session, null);
            var onEnable = typeof(TssDeviceLinkIndicator).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
            onEnable.Invoke(indicator, null);
            typeof(TssDeviceLinkIndicator).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(indicator, null);
            onEnable.Invoke(indicator, null);

            var stateChanged = (Action)typeof(TssTrainingSession)
                .GetField("StateChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(session);
            var listeners = 0;
            foreach (var callback in stateChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                if (ReferenceEquals(callback.Target, indicator))
                    listeners++;

            Assert.AreEqual(1, listeners);
        }
        finally
        {
            Object.DestroyImmediate(indicatorObject);
            Object.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void SwitchLayer2AvailabilityFollowsActiveVlanAndAdminState()
    {
        var profile = TssNetworkRunProfile.Create(42, null);
        var network = new TssNetworkSessionState(profile);
        var device = network.GetOrCreateDevice("SW", EquipmentCategory.Switch, "Switch");
        var iface = new NetworkInterfaceState
        {
            Name = "Fa0/1",
            EndpointId = "SW:Fa01",
            AdminUp = true,
            CarrierUp = true,
            AccessVlan = profile.ReceptionVlan
        };
        device.Interfaces[iface.Name] = iface;

        Assert.IsFalse(network.IsEndpointLayer2Available(iface.EndpointId));
        device.Vlans[profile.ReceptionVlan] = "Reception";
        Assert.IsTrue(network.IsEndpointLayer2Available(iface.EndpointId));

        iface.AdminUp = false;
        Assert.IsFalse(network.IsEndpointLayer2Available(iface.EndpointId));
    }

    [Test]
    public void RouterLayer2DoesNotRequireIpAddressButRoutingDoes()
    {
        var network = new TssNetworkSessionState(TssNetworkRunProfile.Create(42, null));
        var device = network.GetOrCreateDevice("R1", EquipmentCategory.Router, "Router");
        var iface = new NetworkInterfaceState
        {
            Name = "Fa0/1",
            EndpointId = "R1:Fa01",
            AdminUp = true,
            CarrierUp = true
        };
        device.Interfaces[iface.Name] = iface;

        Assert.IsTrue(network.IsEndpointLayer2Available(iface.EndpointId));
        Assert.IsFalse(network.IsEndpointIpRoutingAvailable(iface.EndpointId));

        iface.Address = "192.0.2.1";
        iface.Mask = "255.255.255.252";
        Assert.IsTrue(network.IsEndpointIpRoutingAvailable(iface.EndpointId));
        Assert.IsFalse(network.IsEndpointLayer3Operational(iface.EndpointId));

        iface.Address = network.Profile.WanCustomerAddress;
        Assert.IsTrue(network.IsEndpointLayer3Operational(iface.EndpointId));
        Assert.AreEqual(30, TssNetworkAddress.PrefixLength(iface.Mask));
    }

    [Test]
    public void UnregisteredPortDefaultsUpUntilItIsConfigured()
    {
        var network = new TssNetworkSessionState(TssNetworkRunProfile.Create(42, null));

        Assert.IsTrue(network.IsEndpointLayer1Available("Switch:Fa02"));
        Assert.IsTrue(network.IsEndpointLayer2Available("Switch:Fa02"));
    }

    [Test]
    public void SwitchPortBindingUsesTheCliInterfaceName()
    {
        var endpoint = new GameObject("Fa02").AddComponent<TssPortEndpoint>();
        endpoint.Configure("Rack:Rack01:U36:Fa02", new EquipmentInterface { name = "Fa02", label = "FastEthernet 0/2" });

        try
        {
            var method = typeof(TssDeviceSessionController).GetMethod("SessionInterfaceName", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual("Fa0/2", method.Invoke(null, new object[] { endpoint, EquipmentCategory.Switch }));
        }
        finally
        {
            Object.DestroyImmediate(endpoint.gameObject);
        }
    }

    [Test]
    public void SwitchCliAcceptsShortFaAlias()
    {
        var sessionObject = new GameObject("CliTestSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        session.BeginScenario(scenario);
        var processor = new TssCliCommandProcessor(session, "Switch", EquipmentCategory.Switch, "Switch");

        processor.Execute("enable");
        processor.Execute("configure terminal");
        Assert.IsTrue(processor.Execute("int f0/2").Accepted);
        Assert.IsTrue(processor.Execute("shutdown").Accepted);
        Assert.IsFalse(processor.Device.Interfaces["Fa0/2"].AdminUp);

        Object.DestroyImmediate(scenario);
        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void SwitchCliResetsAccessVlanToDefault()
    {
        var sessionObject = new GameObject("CliTestSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        session.BeginScenario(scenario);
        var processor = new TssCliCommandProcessor(session, "Switch", EquipmentCategory.Switch, "Switch");

        processor.Execute("enable");
        processor.Execute("configure terminal");
        Assert.IsTrue(processor.Execute("interface Fa0/2").Accepted);
        Assert.IsTrue(processor.Execute("switchport access vlan 20").Accepted);
        Assert.AreEqual(20, processor.Device.Interfaces["Fa0/2"].AccessVlan);
        Assert.IsTrue(processor.Execute("no switchport access vlan").Accepted);
        Assert.AreEqual(1, processor.Device.Interfaces["Fa0/2"].AccessVlan);

        Object.DestroyImmediate(scenario);
        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void SwitchCommandReferenceCommandsAreAcceptedAndPersisted()
    {
        var sessionObject = new GameObject("CliReferenceSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };

        try
        {
            session.BeginScenario(scenario);
            var processor = new TssCliCommandProcessor(session, "Switch", EquipmentCategory.Switch, "Switch");
            Assert.IsTrue(processor.Execute("enable").Accepted);
            Assert.IsTrue(processor.Execute("configure terminal").Accepted);
            Assert.IsTrue(processor.Execute("hostname SW-CLI").Accepted);
            Assert.AreEqual("SW-CLI", processor.Device.Hostname);
            Assert.IsTrue(processor.Execute("enable secret secret").Accepted);
            Assert.IsTrue(processor.Execute("no ip domain-lookup").Accepted);
            Assert.IsTrue(processor.Execute("service password-encryption").Accepted);
            Assert.IsTrue(processor.Execute("banner motd #AUTHORIZED#").Accepted);
            Assert.IsTrue(processor.Execute("interface range Fa0/2 - Fa0/3").Accepted);
            Assert.IsTrue(processor.Execute("switchport trunk encapsulation dot1q").Accepted);
            Assert.IsTrue(processor.Execute("switchport mode trunk").Accepted);
            Assert.IsTrue(processor.Execute("switchport trunk allowed vlan 10,20").Accepted);
            Assert.IsTrue(processor.Execute("switchport trunk native vlan 99").Accepted);
            Assert.IsTrue(processor.Execute("ip address 192.0.2.2 255.255.255.0").Accepted);
            Assert.IsTrue(processor.Execute("shutdown").Accepted);
            Assert.IsTrue(processor.Execute("do show vlan brief").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.IsTrue(processor.Execute("line console 0").Accepted);
            Assert.IsTrue(processor.Execute("password consolepw").Accepted);
            Assert.IsTrue(processor.Execute("login").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.IsTrue(processor.Execute("line vty 0 4").Accepted);
            Assert.IsTrue(processor.Execute("password vtypw").Accepted);
            Assert.IsTrue(processor.Execute("login").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.IsTrue(processor.Execute("vlan 10").Accepted);
            Assert.IsTrue(processor.Execute("name Data").Accepted);
            Assert.IsTrue(processor.Execute("state suspend").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.IsTrue(processor.Execute("ip route 10.10.10.0 255.255.255.0 192.0.2.1").Accepted);
            Assert.IsTrue(processor.Execute("end").Accepted);

            foreach (var command in new[]
            {
                "show running-config", "show startup-config", "show ip interface brief",
                "show interfaces", "show interfaces trunk", "show vlan brief",
                "show mac address-table", "ping 192.0.2.1", "clear"
            })
                Assert.IsTrue(processor.Execute(command).Accepted, command);

            Assert.IsTrue(processor.Execute("copy running-config startup-config").Accepted);
            Assert.IsNotNull(processor.Device.StartupSnapshot);
            Assert.IsTrue(processor.Device.Interfaces["Fa0/2"].TrunkEncapsulationDot1q);
            Assert.IsTrue(processor.Device.ConsoleLoginRequired);
            Assert.IsTrue(processor.Device.VtyLoginRequired);
            Assert.AreEqual("Data (suspended)", processor.Device.Vlans[10]);
            Assert.IsTrue(processor.Execute("disable").Accepted);
            Assert.AreEqual(TssNetworkCliMode.UserExec, processor.Device.Mode);
            Assert.IsTrue(processor.Execute("enable").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.AreEqual(TssNetworkCliMode.UserExec, processor.Device.Mode);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void RouterCommandReferenceCommandsAreAcceptedAndPersisted()
    {
        var sessionObject = new GameObject("CliRouterReferenceSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };

        try
        {
            session.BeginScenario(scenario);
            var processor = new TssCliCommandProcessor(session, "Router", EquipmentCategory.Router, "Router");
            Assert.IsTrue(processor.Execute("enable").Accepted);
            Assert.IsTrue(processor.Execute("configure terminal").Accepted);
            Assert.IsTrue(processor.Execute("hostname R-CLI").Accepted);
            Assert.IsTrue(processor.Execute("interface Fa0/0.10").Accepted);
            Assert.IsTrue(processor.Execute("encapsulation dot1q 10").Accepted);
            Assert.IsTrue(processor.Execute("ip add 192.168.10.1 255.255.255.0").Accepted);
            Assert.IsTrue(processor.Execute("no shutdown").Accepted);
            Assert.IsTrue(processor.Execute("exit").Accepted);
            Assert.IsTrue(processor.Execute("ip route 10.10.10.0 255.255.255.0 192.168.10.254").Accepted);
            Assert.IsTrue(processor.Execute("end").Accepted);

            foreach (var command in new[]
            {
                "show running-config", "show startup-config", "show ip interface brief",
                "show interfaces", "show ip route", "ping 192.168.10.254"
            })
                Assert.IsTrue(processor.Execute(command).Accepted, command);

            Assert.AreEqual("R-CLI", processor.Device.Hostname);
            Assert.AreEqual(10, processor.Device.Interfaces["Fa0/0.10"].Dot1qVlan);
            Assert.AreEqual("192.168.10.1", processor.Device.Interfaces["Fa0/0.10"].Address);
            Assert.AreEqual(1, processor.Device.Routes.Count);
        }
        finally
        {
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void FirstPersonDeviceTitleRefreshesWhenNetworkHostnameChanges()
    {
        var sessionObject = new GameObject("FirstPersonTitleSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        var controlsObject = new GameObject("TssTerminalFirstPersonControls(Clone)");
        var controls = controlsObject.AddComponent<TssDeviceFirstPersonControls>();
        var titleObject = new GameObject("DeviceTitle", typeof(RectTransform), typeof(Text));
        var title = titleObject.GetComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        try
        {
            session.BeginScenario(scenario);
            session.NetworkState.GetOrCreateDevice("Rack:Rack01:U01", EquipmentCategory.Switch, "Original").Hostname = "Renamed";
            typeof(TssDeviceFirstPersonControls).GetField("deviceTitleText", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controls, title);
            typeof(TssDeviceFirstPersonControls).GetField("_cachedContext", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controls, new TssDeviceSessionController.DeviceContext
                {
                    Key = "Rack:Rack01:U01",
                    DeviceName = "Original",
                    Category = EquipmentCategory.Switch
                });

            session.NetworkState.TryGetDevice("Rack:Rack01:U01", out var device);
            device.Hostname = "ChangedByCli";
            session.NotifyNetworkStateChanged();

            Assert.AreEqual("ChangedByCli (Switch)", title.text);
        }
        finally
        {
            Object.DestroyImmediate(titleObject);
            Object.DestroyImmediate(controlsObject);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(sessionObject);
        }
    }

    [Test]
    public void EndpointBindingUsesExactSuffixOnly()
    {
        var root = new GameObject("Root");
        var exact = new GameObject("Exact").AddComponent<TssPortEndpoint>();
        exact.Configure("Device:GE01", new EquipmentInterface { name = "GE01", label = "GE0/1" });
        var partial = new GameObject("Partial").AddComponent<TssPortEndpoint>();
        partial.Configure("Device:GE010", new EquipmentInterface { name = "GE010", label = "GE0/10" });
        exact.transform.SetParent(root.transform);
        partial.transform.SetParent(root.transform);

        try
        {
            var endpoints = root.GetComponentsInChildren<TssPortEndpoint>(true);
            Assert.AreSame(exact, TssPortEndpointBinder.FindMatchingEndpoint(endpoints, null, "GE01"));
            Assert.IsNull(TssPortEndpointBinder.FindMatchingEndpoint(endpoints, null, "E01"));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void MonitorLayerStatesKeepIncompleteTextureSetsOnLegacyScreens()
    {
        var go = new GameObject("Monitor");
        var renderer = go.AddComponent<MeshRenderer>();
        var rotator = go.AddComponent<MonitorTextureRotator>();
        var serialized = new UnityEditor.SerializedObject(rotator);
        serialized.FindProperty("targetRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("screens").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            rotator.SetLayerStates(true, true, true);
            var screens = serialized.FindProperty("screens");
            Assert.AreEqual(1, screens.arraySize);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
    [Test]
    public void NetworkProfileIsDeterministicAndUsesUniqueVlans()
    {
        var config = new NetworkScenarioConfig();
        var first = TssNetworkRunProfile.Create(12345, config);
        var second = TssNetworkRunProfile.Create(12345, config);

        Assert.AreEqual(first.ManagementVlan, second.ManagementVlan);
        Assert.AreEqual(first.WanCustomerAddress, second.WanCustomerAddress);
        Assert.AreEqual(first.EnableSecret, second.EnableSecret);
        Assert.AreEqual(5, new System.Collections.Generic.HashSet<int>
        {
            first.ManagementVlan, first.ReceptionVlan, first.DataVlan, first.NativeVlan, first.DeadVlan
        }.Count);
    }

    [Test]
    public void PlayerSeedNormalizesIdentityAndProducesStableDistinctProfiles()
    {
        Assert.IsTrue(TssScenarioLaunchState.TryCreateStudentId("  Smith ", " 17 ", out var firstId));
        Assert.IsTrue(TssScenarioLaunchState.TryCreateStudentId("smith", "17", out var normalizedId));
        Assert.IsFalse(TssScenarioLaunchState.TryCreateStudentId("Smith", "0", out _));

        var first = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed(firstId, "BALANCED_12_PIECE_LAB"), new NetworkScenarioConfig(), firstId);
        var repeat = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed(normalizedId, "balanced_12_piece_lab"), new NetworkScenarioConfig(), normalizedId);
        var different = TssNetworkRunProfile.Create(TssNetworkRunProfile.DeriveSeed("JONES-17", "BALANCED_12_PIECE_LAB"), new NetworkScenarioConfig(), "JONES-17");

        Assert.AreEqual("SMITH-17", firstId);
        Assert.AreEqual(first.Seed, repeat.Seed);
        Assert.AreEqual(first.Ssid, repeat.Ssid);
        Assert.AreNotEqual(first.Seed, different.Seed);
        Assert.AreEqual(888, first.DeadVlan);
        CollectionAssert.AllItemsAreUnique(new[] { first.ManagementVlan, first.ReceptionVlan, first.DataVlan, first.NativeVlan });
        foreach (var vlan in new[] { first.ManagementVlan, first.ReceptionVlan, first.DataVlan, first.NativeVlan })
            Assert.IsTrue(vlan >= 10 && vlan <= 390 && vlan % 10 == 0, "VLAN must be a ten from 10 through 390.");
        StringAssert.StartsWith("192.168.", first.ManagementNetwork);
        StringAssert.StartsWith("10.", first.WanNetwork);
        Assert.AreEqual(first.ReceptionNetwork + ".50", first.ApManagementAddress);
    }

    [Test]
    public void BalancedScenarioAuthorsEveryNetworkScenarioList()
    {
        var scenario = UnityEditor.AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>("Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset");

        Assert.IsNotNull(scenario);
        Assert.IsNotNull(scenario.networkScenario);
        Assert.IsNotEmpty(scenario.networkScenario.endpointAliases);
        Assert.IsNotEmpty(scenario.networkScenario.roleBindings);
        Assert.IsNotEmpty(scenario.networkScenario.requiredPorts);
        Assert.IsNotEmpty(scenario.networkScenario.evidenceRequirements);

        var profile = TssNetworkRunProfile.Create(scenario.networkScenario.profileSeed, scenario.networkScenario);
        Assert.IsNotEmpty(profile.EndpointAliases);
        Assert.IsNotEmpty(profile.RoleBindings);
        Assert.IsNotEmpty(profile.RequiredPorts);
        Assert.IsNotEmpty(profile.EvidenceRequirements);
        Assert.AreEqual(profile.EndpointAddresses["IT1"], new TssNetworkSessionState(profile).GetOrCreateEndpoint("Endpoint:IT1", "IT1").Address);
    }

    [Test]
    // Capstone placement contract.
    public void BalancedScenarioIncludesGuestLoungePlacements()
    {
        var scenario = UnityEditor.AssetDatabase.LoadAssetAtPath<TssScenarioDefinition>("Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset");

        Assert.IsNotNull(scenario);
        Assert.IsTrue(System.Array.Exists(scenario.expectedEndpointPlacements, x => x.stationId == "LobbyTable01" && x.category == EquipmentCategory.AccessPoint && x.requiredName == "AP-1"));
        Assert.IsTrue(System.Array.Exists(scenario.expectedEndpointPlacements, x => x.stationId == "LobbyTable01" && x.category == EquipmentCategory.Laptop && x.requiredName == "TestLaptop"));
    }

    [Test]
    public void NetworkStateRemovesDeviceByStableKey()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(1, new NetworkScenarioConfig()));
        state.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
        state.RemoveDevice("Rack:Rack01:U35");

        Assert.IsFalse(state.TryGetDevice("Rack:Rack01:U35", out _));
    }

    [Test]
    public void NetworkResetClearsEvidenceAndCompletionSnapshots()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(2, new NetworkScenarioConfig()));
        state.Evidence.Add("show vlan brief");
        state.SaveCheckpoint("before");
        state.SaveCompletionSnapshot("WP2");

        state.Clear();

        Assert.IsEmpty(state.Evidence);
        Assert.IsEmpty(state.Checkpoints);
        Assert.IsEmpty(state.CompletionSnapshots);
    }

    [Test]
    public void WorkPackageReportsNeutralStructuredCategoryWithoutAnswerData()
    {
        var profile = TssNetworkRunProfile.Create(3, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        state.GetOrCreateDevice("AN2", EquipmentCategory.Switch, "AN-2");

        var result = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2();

        Assert.IsFalse(result.Complete);
        Assert.IsTrue(result.UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Hardening"));
        StringAssert.DoesNotContain(profile.ManagementVlan.ToString(), string.Join(" ", result.UnmetConditions));
        StringAssert.DoesNotContain(profile.EnableSecret, string.Join(" ", result.UnmetConditions));
    }

    [Test]
    public void CompletionSnapshotIsStickyAfterLaterStateChanges()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(4, new NetworkScenarioConfig()));
        var snapshot = state.SaveCompletionSnapshot("WP2");
        var device = state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        device.Hostname = "AN-1-reused";

        var evaluator = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2();

        Assert.IsTrue(evaluator.Complete);
        Assert.AreSame(snapshot, evaluator.CompletionSnapshot);
        Assert.AreEqual(4, evaluator.CompletionSnapshot.ProfileSeed);
    }

    [Test]
    public void SuspendedDeadVlanIsAcceptedAndUnusedPortsAreEvaluated()
    {
        var profile = TssNetworkRunProfile.Create(5, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var an1 = state.GetOrCreateDevice("AN1", EquipmentCategory.Switch, "AN-1");
        var an2 = state.GetOrCreateDevice("AN2", EquipmentCategory.Switch, "AN-2");
        an1.Vlans[profile.ManagementVlan] = profile.ManagementVlanName;
        an1.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
        an1.Vlans[profile.DataVlan] = profile.DataVlanName;
        an1.Vlans[profile.NativeVlan] = profile.NativeVlanName;
        an1.Vlans[profile.DeadVlan] = profile.DeadVlanName + " (suspended)";

        for (var port = 8; port <= 24; port++)
            an1.Interfaces["Fa0/" + port] = new NetworkInterfaceState { Name = "Fa0/" + port, AccessVlan = profile.DeadVlan, AdminUp = false };
        for (var port = 5; port <= 24; port++)
            an2.Interfaces["Fa0/" + port] = new NetworkInterfaceState { Name = "Fa0/" + port, AccessVlan = profile.DeadVlan, AdminUp = false };

        var result = new TssNetworkWorkPackageEvaluator(state).EvaluateLayer2();

        Assert.IsFalse(result.UnmetCategories.Exists(x => x.Id == "WP2.AN-1.Vlans"));
        Assert.IsFalse(result.UnmetCategories.Exists(x => x.Id == "WP2.UnusedPorts"));
        Assert.IsTrue(result.WasAttempted("WP2.UnusedPorts"));
    }

    [Test]
    public void ProviderAliasRecordsEndpointEvidence()
    {
        var config = new NetworkScenarioConfig
        {
            evidenceRequirements = new[]
            {
                new NetworkEvidenceRequirement { evidenceId = "IT1-provider", command = "ping provider" }
            }
        };
        var profile = TssNetworkRunProfile.Create(6, config);
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("IT1", "IT1");
        endpoint.Layer1Connected = endpoint.Layer2Connected = true;

        var result = new TssEndpointCommandProcessor(state).Execute(endpoint.Key, "ping provider");

        Assert.IsTrue(result.Accepted);
        Assert.IsTrue(endpoint.Evidence.Contains("IT1-provider"));
    }

    [Test]
    public void WirelessStateBackedWorkPackageCompletesAfterLeaseDiagnosticsAndPage()
    {
        var profile = TssNetworkRunProfile.Create(8, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var laptop = state.GetOrCreateEndpoint("laptop", "TestLaptop", EquipmentCategory.Laptop);
        state.SetAccessPointUplink(true);
        state.AccessPoint.Configured = true;
        var accessSwitch = state.GetOrCreateDevice("switch", EquipmentCategory.Switch, "AN-1");
        accessSwitch.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
        accessSwitch.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", AdminUp = true, CarrierUp = true, AccessVlan = profile.ReceptionVlan };
        accessSwitch.Interfaces["Fa0/6"] = new NetworkInterfaceState { Name = "Fa0/6", AdminUp = true, CarrierUp = true, IsTrunk = true, NativeVlan = profile.NativeVlan, AllowedVlans = new System.Collections.Generic.HashSet<int> { profile.ManagementVlan, profile.ReceptionVlan, profile.DataVlan } };
        var router = state.GetOrCreateDevice("router", EquipmentCategory.Router, "CN-1");
        router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true, CarrierUp = true, Address = profile.WanCustomerAddress, Mask = "255.255.255.252" };
        router.Routes.Add(new NetworkRouteState { Network = "0.0.0.0", Mask = "0.0.0.0", NextHop = profile.WanProviderAddress });
        state.SetProviderDemarcConnected(true);

        Assert.IsTrue(state.TryAssociateWirelessClient(laptop.Key, profile.Ssid, profile.WifiPassphrase, out _));
        Assert.IsTrue(new TssEndpointCommandProcessor(state).TryApply(laptop.Key, true, string.Empty, string.Empty, string.Empty, string.Empty, out _));
        var commands = new TssEndpointCommandProcessor(state);
        commands.Execute(laptop.Key, "ipconfig");
        Assert.IsTrue(commands.Execute(laptop.Key, "ping provider").Accepted);
        Assert.IsTrue(state.TryLoadRedWolfPage(laptop.Key, out _));

        Assert.IsTrue(new TssNetworkWorkPackageEvaluator(state).EvaluateLayer4().Complete);
    }

    [Test]
    public void EndpointDiagnosticsUseAuthoritativeReachability()
    {
        var profile = TssNetworkRunProfile.Create(7, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        endpoint.Layer1Connected = true;
        endpoint.Layer2Connected = true;

        var result = new TssNetworkReachabilityEvaluator(state).Evaluate("Endpoint:IT1", profile.ManagementNetwork + ".1");

        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.FailedLayer);
    }

    [Test]
    public void ReachabilityReturnsLowestBrokenLayerAndNeutralFaults()
    {
        var profile = TssNetworkRunProfile.Create(7, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        var evaluator = new TssNetworkReachabilityEvaluator(state);

        var physical = evaluator.Evaluate(endpoint.Key, profile.ManagementNetwork + ".1");
        Assert.AreEqual(1, physical.FailedLayer);
        Assert.AreEqual(TssReachabilityFaultCategory.PhysicalPathUnavailable, physical.FaultCategory);

        endpoint.Layer1Connected = true;
        var switching = evaluator.Evaluate(endpoint.Key, profile.ManagementNetwork + ".1");
        Assert.AreEqual(2, switching.FailedLayer);
        Assert.AreEqual(TssReachabilityFaultCategory.VlanNotCarried, switching.FaultCategory);

        endpoint.Layer2Connected = true;
        endpoint.Gateway = "10.255.255.1";
        var routing = evaluator.Evaluate(endpoint.Key, profile.ManagementNetwork + ".1");
        Assert.AreEqual(3, routing.FailedLayer);
        Assert.AreEqual(TssReachabilityFaultCategory.GatewayUnavailable, routing.FaultCategory);
        StringAssert.DoesNotContain(profile.ManagementNetwork, routing.Diagnostic);
    }

    [Test]
    public void UnknownValidDestinationTimesOutAtLayer3()
    {
        var profile = TssNetworkRunProfile.Create(7, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        endpoint.Layer1Connected = true;
        endpoint.Layer2Connected = true;

        var result = new TssNetworkReachabilityEvaluator(state).Evaluate(endpoint.Key, "192.0.2.10");

        Assert.AreEqual(3, result.FailedLayer);
        Assert.AreEqual(TssReachabilityFaultCategory.DestinationUnavailable, result.FaultCategory);
        Assert.IsFalse(result.Success);
    }

    [Test]
    public void EndpointApplyRejectsInvalidCandidateAtomically()
    {
        var profile = TssNetworkRunProfile.Create(8, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        var originalAddress = endpoint.Address;
        var originalMask = endpoint.Mask;
        var originalGateway = endpoint.Gateway;
        var originalDns = endpoint.Dns;

        var applied = new TssEndpointCommandProcessor(state).TryApply("Endpoint:IT1", false, "999.1.1.1", "255.255.255.0", endpoint.Gateway, "8.8.8.8", out _);

        Assert.IsFalse(applied);
        Assert.AreEqual(originalAddress, endpoint.Address);
        Assert.AreEqual(originalMask, endpoint.Mask);
        Assert.AreEqual(originalGateway, endpoint.Gateway);
        Assert.AreEqual(originalDns, endpoint.Dns);
        Assert.IsFalse(state.Evidence.Contains("Endpoint:IT1:apply"));
    }

    [Test]
    public void EndpointApplyCommitsValidButWrongGatewayAndReachabilityReportsLayer3()
    {
        var profile = TssNetworkRunProfile.Create(8, new NetworkScenarioConfig());
        var state = new TssNetworkSessionState(profile);
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        endpoint.Layer1Connected = true;
        endpoint.Layer2Connected = true;

        Assert.IsTrue(new TssEndpointCommandProcessor(state).TryApply(endpoint.Key, false, endpoint.Address, endpoint.Mask, "10.255.255.1", endpoint.Dns, out _));
        Assert.AreEqual("10.255.255.1", endpoint.Gateway);
        Assert.AreEqual(3, new TssNetworkReachabilityEvaluator(state).Evaluate(endpoint.Key, profile.ManagementNetwork + ".1").FailedLayer);
    }

    [Test]
    public void EndpointCommandsNormalizeWhitespaceAndDoNotMutateOnUnsupportedInput()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(9, new NetworkScenarioConfig()));
        var endpoint = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        var address = endpoint.Address;

        var result = new TssEndpointCommandProcessor(state).Execute(endpoint.Key, "  unsupported   command ");

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(address, endpoint.Address);
    }

    [Test]
    public void PrinterTestPageRequiresRecordedWorkstationReachability()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(10, new NetworkScenarioConfig()));
        var workstation = state.GetOrCreateEndpoint("Endpoint:IT1", "IT1");
        var printer = state.GetOrCreateEndpoint("Endpoint:Printer", "Reception2");
        workstation.Layer1Connected = workstation.Layer2Connected = true;
        printer.Layer1Connected = printer.Layer2Connected = true;

        Assert.IsFalse(state.TryPrintTestPage(printer.Key, "Endpoint:Unknown", out _));
        Assert.IsFalse(printer.LastPrintSucceeded);
        Assert.IsTrue(state.TryPrintTestPage(printer.Key, workstation.Key, out _));
        Assert.IsTrue(printer.LastPrintSucceeded);
        Assert.AreEqual(workstation.Key, printer.LastPrintSourceKey);
    }

    [Test]
    public void EquipmentCategoriesClassifyCorrectly()
    {
        var switchState = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "CoreSwitch");
        var routerState = new TssDeviceLogicalState("r1", EquipmentCategory.Router, "EdgeRouter");
        var pcState = new TssDeviceLogicalState("pc1", EquipmentCategory.DesktopPc, "Desktop01");
        var serverState = new TssDeviceLogicalState("srv1", EquipmentCategory.Server, "MainServer");
        var printerState = new TssDeviceLogicalState("prn1", EquipmentCategory.Printer, "FloorPrinter");

        Assert.IsTrue(switchState.IsSwitchOrRouter);
        Assert.IsFalse(switchState.IsDesktopSessionType);

        Assert.IsTrue(routerState.IsSwitchOrRouter);
        Assert.IsFalse(routerState.IsDesktopSessionType);

        Assert.IsTrue(pcState.IsDesktopSessionType);
        Assert.IsFalse(pcState.IsSwitchOrRouter);

        Assert.IsTrue(serverState.IsDesktopSessionType);
        Assert.IsFalse(serverState.IsSwitchOrRouter);

        Assert.IsTrue(printerState.IsDesktopSessionType);
        Assert.IsFalse(printerState.IsSwitchOrRouter);
    }

    [Test]
    public void DefaultLogicalLayerSettingsAssignedProperly()
    {
        var routerState = new TssDeviceLogicalState("r1", EquipmentCategory.Router, "Router01");
        Assert.AreEqual(string.Empty, routerState.IpAddress);
        Assert.AreEqual(string.Empty, routerState.DefaultGateway);
        Assert.IsFalse(routerState.UseDhcp);

        var switchState = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "Switch01");
        Assert.AreEqual("192.168.1.2", switchState.IpAddress);
        Assert.AreEqual("255.255.255.0", switchState.SubnetMask);
        Assert.AreEqual("192.168.1.1", switchState.DefaultGateway);
        Assert.IsFalse(switchState.UseDhcp);

        var pcState = new TssDeviceLogicalState("pc1", EquipmentCategory.DesktopPc, "PC01");
        Assert.AreEqual("192.168.1.101", pcState.IpAddress);
        Assert.IsTrue(pcState.UseDhcp);

        var serverState = new TssDeviceLogicalState("srv1", EquipmentCategory.Server, "SRV01");
        Assert.AreEqual("192.168.1.10", serverState.IpAddress);
        Assert.IsFalse(serverState.UseDhcp);

        var printerState = new TssDeviceLogicalState("prn1", EquipmentCategory.Printer, "PRN01");
        Assert.AreEqual("192.168.1.200", printerState.IpAddress);
        Assert.IsFalse(printerState.UseDhcp);
    }

    [Test]
    public void PhysicalConnectivityPopulatesLogicalInterfaces()
    {
        var state = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "Switch01");

        var root = new GameObject("DeviceRoot");
        var p1 = new GameObject("Port1").AddComponent<TssPortEndpoint>();
        p1.Configure("Switch01:Fa01", new EquipmentInterface { name = "Fa01", label = "Fa0/1" });
        var p2 = new GameObject("Port2").AddComponent<TssPortEndpoint>();
        p2.Configure("Switch01:Fa02", new EquipmentInterface { name = "Fa02", label = "Fa0/2" });

        try
        {
            state.RefreshPhysicalConnectivity(new[] { p1, p2 }, null);
            Assert.AreEqual(2, state.Interfaces.Count);
            Assert.AreEqual("Fa0/1", state.Interfaces[0].InterfaceName);
            Assert.AreEqual("Switch01:Fa01", state.Interfaces[0].EndpointId);
            Assert.IsFalse(state.Interfaces[0].IsPhysicalLinkUp);
            Assert.AreEqual(1, state.Interfaces[0].VlanId);
            Assert.AreEqual("Access", state.Interfaces[0].PortMode);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(p1.gameObject);
            Object.DestroyImmediate(p2.gameObject);
        }
    }

    [Test]
    public void TerminalSessionPresenterExecutesCommands()
    {
        var go = new GameObject("TerminalPresenter", typeof(RectTransform), typeof(TssTerminalSessionPresenter));
        var pres = go.GetComponent<TssTerminalSessionPresenter>();

        var textGo = new GameObject("Output", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform);
        var outputText = textGo.GetComponent<Text>();
        outputText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
        promptGo.transform.SetParent(go.transform);
        var promptText = promptGo.GetComponent<Text>();

        var ser = new UnityEditor.SerializedObject(pres);
        ser.FindProperty("consoleOutputText").objectReferenceValue = outputText;
        ser.FindProperty("promptText").objectReferenceValue = promptText;
        ser.ApplyModifiedPropertiesWithoutUndo();

        var state = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "TestSwitch");
        pres.Initialize(state, null, null);

        Assert.IsTrue(outputText.text.Contains("TestSwitch"));
        Assert.AreEqual("TestSwitch> ", promptText.text);

        // enable -> PrivilegedExec
        pres.ExecuteCommand("enable");
        Assert.AreEqual(CiscoCliMode.PrivilegedExec, pres.CliMode);
        Assert.AreEqual("TestSwitch# ", promptText.text);

        // conf t -> GlobalConfig
        pres.ExecuteCommand("configure terminal");
        Assert.AreEqual(CiscoCliMode.GlobalConfig, pres.CliMode);
        Assert.AreEqual("TestSwitch(config)# ", promptText.text);

        // interface -> InterfaceConfig
        pres.ExecuteCommand("interface FastEthernet0/1");
        Assert.AreEqual(CiscoCliMode.InterfaceConfig, pres.CliMode);
        Assert.AreEqual("TestSwitch(config-if)# ", promptText.text);

        // exit -> back to GlobalConfig
        pres.ExecuteCommand("exit");
        Assert.AreEqual(CiscoCliMode.GlobalConfig, pres.CliMode);
        Assert.AreEqual("TestSwitch(config)# ", promptText.text);

        // exit -> back to PrivilegedExec
        pres.ExecuteCommand("exit");
        Assert.AreEqual(CiscoCliMode.PrivilegedExec, pres.CliMode);
        Assert.AreEqual("TestSwitch# ", promptText.text);

        // Commands output test
        pres.ExecuteCommand("show ip interface brief");
        Assert.IsTrue(outputText.text.Contains("show ip interface brief"));

        pres.ExecuteCommand("show mac address-table");
        Assert.IsTrue(outputText.text.Contains("Mac Address Table"));

        pres.ExecuteCommand("show vlan brief");
        Assert.IsTrue(outputText.text.Contains("default"));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TerminalSessionHistorySupportsDraftAndDownToBlank()
    {
        var go = new GameObject("TerminalHistoryPresenter", typeof(RectTransform), typeof(TssTerminalSessionPresenter));
        var pres = go.GetComponent<TssTerminalSessionPresenter>();

        var outputGo = new GameObject("Output", typeof(RectTransform), typeof(Text));
        outputGo.transform.SetParent(go.transform);
        var output = outputGo.GetComponent<Text>();
        output.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
        promptGo.transform.SetParent(go.transform);
        var prompt = promptGo.GetComponent<Text>();

        var inputGo = new GameObject("Input", typeof(RectTransform), typeof(InputField));
        inputGo.transform.SetParent(go.transform);
        var input = inputGo.GetComponent<InputField>();

        var serialized = new UnityEditor.SerializedObject(pres);
        serialized.FindProperty("consoleOutputText").objectReferenceValue = output;
        serialized.FindProperty("promptText").objectReferenceValue = prompt;
        serialized.FindProperty("commandInput").objectReferenceValue = input;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var state = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "TestSwitch");
        pres.Initialize(state, null, null);
        pres.ExecuteCommand("enable");
        pres.ExecuteCommand("show interfaces");

        input.text = "draft command";
        pres.BrowseHistoryUp();
        Assert.AreEqual("show interfaces", input.text);
        pres.BrowseHistoryUp();
        Assert.AreEqual("enable", input.text);
        pres.BrowseHistoryDown();
        Assert.AreEqual("show interfaces", input.text);
        pres.BrowseHistoryDown();
        Assert.AreEqual("draft command", input.text);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void LongShowOutputStartsAtTopAndRequiresMorePages()
    {
        var go = new GameObject("TerminalPagingPresenter", typeof(RectTransform), typeof(TssTerminalSessionPresenter));
        var pres = go.GetComponent<TssTerminalSessionPresenter>();
        var outputGo = new GameObject("Output", typeof(RectTransform), typeof(Text));
        outputGo.transform.SetParent(go.transform);
        var output = outputGo.GetComponent<Text>();
        output.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
        promptGo.transform.SetParent(go.transform);
        var prompt = promptGo.GetComponent<Text>();
        var inputGo = new GameObject("Input", typeof(RectTransform), typeof(InputField));
        inputGo.transform.SetParent(go.transform);
        var input = inputGo.GetComponent<InputField>();

        var state = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "TestSwitch");
        for (var i = 1; i <= 24; i++)
            state.Interfaces.Add(new TssDeviceLogicalState.InterfaceLogicalRecord { InterfaceName = "Fa0/" + i });

        var serialized = new UnityEditor.SerializedObject(pres);
        serialized.FindProperty("consoleOutputText").objectReferenceValue = output;
        serialized.FindProperty("promptText").objectReferenceValue = prompt;
        serialized.FindProperty("commandInput").objectReferenceValue = input;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        pres.Initialize(state, null, null);
        pres.ExecuteCommand("show interfaces");

        Assert.IsTrue(input.readOnly);
        Assert.IsFalse(input.gameObject.activeSelf);
        Assert.IsFalse(prompt.gameObject.activeSelf);
        StringAssert.Contains("Fa0/1", output.text);
        StringAssert.Contains("--More-- Press SPACE for more --", output.text);
        Assert.IsFalse(output.text.Contains("Fa0/24"));

        pres.ShowMoreOutput();
        Assert.IsTrue(input.readOnly);
        Assert.IsFalse(output.text.Contains("Fa0/24"));

        for (var page = 0; page < 10 && input.readOnly; page++)
            pres.ShowMoreOutput();
        Assert.IsFalse(input.readOnly);
        Assert.IsTrue(input.gameObject.activeSelf);
        Assert.IsTrue(prompt.gameObject.activeSelf);
        StringAssert.Contains("Fa0/24", output.text);
        StringAssert.DoesNotContain("--More-- Press SPACE for more --", output.text);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ShowCommandHistoryKeepsEachOutputBlockIndependent()
    {
        var go = new GameObject("TerminalShowHistoryPresenter", typeof(RectTransform), typeof(TssTerminalSessionPresenter));
        var pres = go.GetComponent<TssTerminalSessionPresenter>();
        var outputGo = new GameObject("Output", typeof(RectTransform), typeof(Text));
        outputGo.transform.SetParent(go.transform);
        var output = outputGo.GetComponent<Text>();
        output.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var promptGo = new GameObject("Prompt", typeof(RectTransform), typeof(Text));
        promptGo.transform.SetParent(go.transform);
        var prompt = promptGo.GetComponent<Text>();
        var inputGo = new GameObject("Input", typeof(RectTransform), typeof(InputField));
        inputGo.transform.SetParent(go.transform);
        var input = inputGo.GetComponent<InputField>();

        var serialized = new UnityEditor.SerializedObject(pres);
        serialized.FindProperty("consoleOutputText").objectReferenceValue = output;
        serialized.FindProperty("promptText").objectReferenceValue = prompt;
        serialized.FindProperty("commandInput").objectReferenceValue = input;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var state = new TssDeviceLogicalState("sw1", EquipmentCategory.Switch, "TestSwitch");
        pres.Initialize(state, null, null);
        pres.ExecuteCommand("show running-config");
        pres.ExecuteCommand("show vlan brief");

        var history = output.text;
        var latestCommand = history.LastIndexOf("show vlan brief", StringComparison.Ordinal);
        Assert.GreaterOrEqual(latestCommand, 0);
        var latestOutput = history.Substring(latestCommand);
        StringAssert.Contains("VLAN Name", latestOutput);
        Assert.IsFalse(latestOutput.Contains("version 15.0"));
        StringAssert.Contains("show running-config", history);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void RouterCliAcceptsDocumentedFaAliasesAndSubinterfaces()
    {
        var sessionObject = new GameObject("CliTestSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true, profileSeed = 42 };
        session.BeginScenario(scenario);
        var processor = new TssCliCommandProcessor(session, "Router", EquipmentCategory.Router, "CN-1");

        processor.Execute("enable");
        processor.Execute("configure terminal");
        var defaultRoute = processor.Execute("ip route 0.0.0.0 0.0.0.0 10.42.17.2");
        Assert.IsTrue(defaultRoute.Accepted);
        Assert.IsTrue(defaultRoute.StateChanged);
        Assert.AreEqual(1, processor.Device.Routes.Count);
        Assert.IsFalse(processor.Execute("show ip route").Accepted);
        StringAssert.DoesNotContain("S*   0.0.0.0/0 [1/0] via 10.42.17.2", processor.Execute("do show ip route").Output);
        Assert.IsTrue(processor.Execute("interface Fa0/1").Accepted);
        Assert.IsTrue(processor.Execute("no shutdown").Accepted);
        Assert.IsTrue(processor.Execute("ip add 10.42.17.1 255.255.255.252").Accepted);
        processor.Device.Interfaces["Fa0/1"].CarrierUp = true;
        StringAssert.Contains("S*   0.0.0.0/0 [1/0] via 10.42.17.2", processor.Execute("do show ip route").Output);
        Assert.IsTrue(processor.Execute("exit").Accepted);
        Assert.IsFalse(processor.Execute("interface GE00").Accepted);
        Assert.IsFalse(processor.Execute("interface GE01").Accepted);
        Assert.IsFalse(processor.Execute("interface Gi0/0").Accepted);
        Assert.IsFalse(processor.Execute("interface Gi0/1").Accepted);
        Assert.IsFalse(processor.Device.Interfaces.ContainsKey("GE00"));
        Assert.IsFalse(processor.Device.Interfaces.ContainsKey("GE01"));
        Assert.IsFalse(processor.Device.Interfaces.ContainsKey("Fa0/0"));
        Assert.IsTrue(processor.Execute("int f0/0").Accepted);
        Assert.IsTrue(processor.Execute("no shutdown").Accepted);
        Assert.IsTrue(processor.Execute("exit").Accepted);
        Assert.IsTrue(processor.Execute("interface Fa0/0.123").Accepted);
        Assert.IsTrue(processor.Execute("encapsulation dot1q 123").Accepted);
        Assert.IsTrue(processor.Execute("ip add 10.20.30.1 255.255.255.0").Accepted);

        Assert.IsTrue(processor.Device.Interfaces.ContainsKey("Fa0/0"));
        Assert.IsTrue(processor.Device.Interfaces.ContainsKey("Fa0/0.123"));
        Assert.AreEqual(123, processor.Device.Interfaces["Fa0/0.123"].Dot1qVlan);
        Assert.AreEqual("10.20.30.1", processor.Device.Interfaces["Fa0/0.123"].Address);

        var before = processor.Device.Interfaces["Fa0/0.123"].Address;
        var invalid = processor.Execute("ip address 999.1.1.1 255.255.255.0");
        Assert.IsFalse(invalid.Accepted);
        Assert.AreEqual(before, processor.Device.Interfaces["Fa0/0.123"].Address);

        Object.DestroyImmediate(scenario);
        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void SwitchCliAcceptsIpAddAlias()
    {
        var sessionObject = new GameObject("SwitchCliTestSession", typeof(TssTrainingSession));
        var session = sessionObject.GetComponent<TssTrainingSession>();
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true, profileSeed = 43 };
        session.BeginScenario(scenario);
        var processor = new TssCliCommandProcessor(session, "Switch", EquipmentCategory.Switch, "SW-1");

        processor.Execute("enable");
        processor.Execute("configure terminal");
        Assert.IsTrue(processor.Execute("interface Fa0/1").Accepted);
        Assert.IsTrue(processor.Execute("ip add 192.168.1.2 255.255.255.0").Accepted);
        Assert.AreEqual("192.168.1.2", processor.Device.Interfaces["Fa0/1"].Address);

        Object.DestroyImmediate(scenario);
        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void DesktopSessionPresenterInitializesFields()
    {
        var go = new GameObject("DesktopPresenter", typeof(RectTransform), typeof(TssDesktopSessionPresenter));
        var pres = go.GetComponent<TssDesktopSessionPresenter>();

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(go.transform);
        var titleText = titleGo.GetComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var ipGo = new GameObject("IpInput", typeof(RectTransform), typeof(InputField));
        ipGo.transform.SetParent(go.transform);
        var ipInput = ipGo.GetComponent<InputField>();

        var ser = new UnityEditor.SerializedObject(pres);
        ser.FindProperty("topBarTitleText").objectReferenceValue = titleText;
        ser.FindProperty("ipAddressInput").objectReferenceValue = ipInput;
        ser.ApplyModifiedPropertiesWithoutUndo();

        var state = new TssDeviceLogicalState("pc1", EquipmentCategory.DesktopPc, "Workstation01");
        pres.Initialize(state, null, null);

        Assert.AreEqual("Workstation OS Enterprise", titleText.text);
        Assert.AreEqual("192.168.1.101", ipInput.text);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void ClickInstalledEntersFirstPersonView()
    {
        var camGo = new GameObject("TestCam");
        var cam = camGo.AddComponent<Camera>();
        cam.tag = "MainCamera";

        var rackGo = new GameObject("TestRack");
        var rack = rackGo.AddComponent<RackMountController>();
        var visualGo = new GameObject("RouterVisual");
        var interactable = visualGo.AddComponent<TssObjectInteractable>();

        var itSer = new UnityEditor.SerializedObject(interactable);
        itSer.FindProperty("mainCamera").objectReferenceValue = cam;
        itSer.ApplyModifiedPropertiesWithoutUndo();

        try
        {
            var eq = ScriptableObject.CreateInstance<EquipmentDefinition>();
            eq.displayName = "TestRouter";
            eq.category = EquipmentCategory.Router;

            var installedItemsField = typeof(RackMountController).GetField("_installedItems", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var installedItems = installedItemsField.GetValue(rack);
            var installedItemType = typeof(RackMountController).GetNestedType("InstalledRackItem", System.Reflection.BindingFlags.NonPublic);
            var itemInstance = System.Activator.CreateInstance(installedItemType, eq, visualGo, null, 1);
            var addMethod = installedItems.GetType().GetMethod("set_Item");
            addMethod.Invoke(installedItems, new object[] { 10, itemInstance });

            Assert.IsFalse(interactable.IsFirstPerson);

            rack.ClickInstalled(10);

            Assert.IsTrue(interactable.IsFirstPerson, "ClickInstalled should immediately enter first-person view on installed device");
        }
        finally
        {
            Object.DestroyImmediate(rackGo);
            Object.DestroyImmediate(visualGo);
            Object.DestroyImmediate(camGo);
        }
    }

    [Test]
    public void PreinstalledCableTraysExistInBothRacks()
    {
        var r1Tray = GameObject.Find("TSS Level/60 Network Devices/Racks/Rack 01/Runtime Installed Equipment/RackCableTray U30-U31");
        var r2Tray = GameObject.Find("TSS Level/60 Network Devices/Racks/Rack 02/Runtime Installed Equipment/RackCableTray U30-U31");

        Assert.IsNotNull(r1Tray, "Rack 01 must have preinstalled cable tray at U30-U31");
        Assert.IsNotNull(r2Tray, "Rack 02 must have preinstalled cable tray at U30-U31");
    }
}
