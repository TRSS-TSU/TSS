using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class TssDeviceSessionTests
{
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
    public void NetworkStateRemovesDeviceByStableKey()
    {
        var state = new TssNetworkSessionState(TssNetworkRunProfile.Create(1, new NetworkScenarioConfig()));
        state.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
        state.RemoveDevice("Rack:Rack01:U35");

        Assert.IsFalse(state.TryGetDevice("Rack:Rack01:U35", out _));
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
