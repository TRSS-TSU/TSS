using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class TssWednesdayInteractionPlayModeTests
{
    [UnityTest]
    public IEnumerator CorrectionPanelExplainsBlockedPickupAndClearsPreviousState()
    {
        var root = new GameObject("WednesdayCorrectionTest");
        root.SetActive(false);
        var ui = root.AddComponent<TssRuntimeUi>();
        var panel = new GameObject("CorrectionPanel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var buttonObject = new GameObject("PickupButton", typeof(RectTransform), typeof(Button));
        buttonObject.transform.SetParent(panel.transform, false);
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var button = buttonObject.GetComponent<Button>();
        Set(ui, "_correctionPanel", panel);
        Set(ui, "_corrPickupButton", button);
        var cursorLock = Cursor.lockState;
        var cursorVisible = Cursor.visible;
        try
        {
            ui.ShowEndpointMenu("Connected", false, true, false, null, null, null, pickupBlocked: true);
            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(buttonObject.activeSelf);
            Assert.IsFalse(button.interactable);
            StringAssert.Contains("Disconnect cables before picking up this device.", label.text);
            Assert.IsTrue(label.gameObject.activeSelf);

            ui.ShowEndpointMenu("Disconnected", false, true, false, null, null, null);
            Assert.IsTrue(button.interactable);
            Assert.AreEqual("Pick Up", label.text);
            ui.ShowEndpointMenu("Fixed equipment", false, false, false, null, null, null);
            Assert.IsFalse(buttonObject.activeSelf);
        }
        finally
        {
            panel.SetActive(false);
            Invoke(ui, "ApplyGameplayInputLock", false);
            Object.Destroy(root);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator MountedBodyHoverPreservesPortTintAndRestoresBodyOnDisable()
    {
        var root = new GameObject("WednesdayMountedHoverTest");
        var visual = new GameObject("MountedVisual");
        visual.transform.SetParent(root.transform, false);
        var body = visual.AddComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Assert.IsNotNull(shader);
        var material = new Material(shader);
        body.sharedMaterial = material;
        var property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        var originalBody = new MaterialPropertyBlock();
        originalBody.SetColor(property, Color.magenta);
        body.SetPropertyBlock(originalBody, 0);
        var portObject = new GameObject("Port");
        portObject.transform.SetParent(visual.transform, false);
        var port = portObject.AddComponent<MeshRenderer>();
        port.sharedMaterial = material;
        portObject.AddComponent<TssPortEndpoint>();
        var portMaterial = port.sharedMaterial;
        var originalPort = new MaterialPropertyBlock();
        originalPort.SetColor(property, Color.yellow);
        port.SetPropertyBlock(originalPort, 0);
        var proxy = root.AddComponent<RackInstalledEquipmentTarget>();
        proxy.Initialize(null, 10, visual);
        try
        {
            proxy.SetHovered(true);
            Assert.AreNotEqual(Color.magenta, BlockColor(body, property));
            Assert.AreEqual(Color.yellow, BlockColor(port, property));
            proxy.enabled = false;
            Assert.AreEqual(Color.magenta, BlockColor(body, property));
            Assert.AreEqual(Color.yellow, BlockColor(port, property));
        }
        finally
        {
            Object.Destroy(root);
            if (portMaterial != material) Object.Destroy(portMaterial);
            Object.Destroy(material);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator FirstPersonPortsRefreshOnCableCheckoutAndUnsubscribeOnDisable()
    {
        var sessionObject = new GameObject("WednesdayPortSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.enabled = false;
        var cable = ScriptableObject.CreateInstance<TssCableDefinition>();
        cable.displayName = "Test cable";
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        session.BeginScenario(scenario);
        var device = new GameObject("WednesdayPortDevice");
        var portObject = new GameObject("Port");
        portObject.transform.SetParent(device.transform, false);
        var port = portObject.AddComponent<TssPortEndpoint>();
        port.Configure("Wednesday:Eth01", null);
        var adapter = device.AddComponent<TssPortSelectionAdapter>();
        try
        {
            Invoke(adapter, "BeginSelection");
            Assert.IsFalse(Get<bool>(port, "_selectable"));
            Assert.IsTrue(session.TryCheckoutCable(cable, out var error), error);
            Assert.IsTrue(Get<bool>(port, "_selectable"));
            adapter.enabled = false;
            Assert.IsFalse(Get<bool>(port, "_selectable"));
            Assert.IsNull(Get<TssTrainingSession>(adapter, "_session"));
            Assert.IsTrue(session.ReturnHeldCable(out error), error);
            Assert.IsTrue(session.TryCheckoutCable(cable, out error), error);
            Assert.IsFalse(Get<bool>(port, "_selectable"), "Disabled adapter must not receive session refreshes.");
        }
        finally
        {
            Object.Destroy(device);
            Object.Destroy(sessionObject);
            Object.Destroy(scenario);
            Object.Destroy(cable);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator HudOmitsRackElevationAndKeepsPhysicalDetailOutOfObjective()
    {
        var sessionObject = new GameObject("WednesdayHudSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.enabled = false;
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.displayName = "Capstone";
        scenario.expectedRackPlacements = new[] { new ExpectedRackPlacement
            { rackId = "Rack01", startingU = 35, endingU = 38, category = EquipmentCategory.Switch, requiredName = "AN-1" } };
        var uiObject = new GameObject("WednesdayHudUi");
        uiObject.SetActive(false);
        var ui = uiObject.AddComponent<TssRuntimeUi>();
        Set(ui, "session", session);
        try
        {
            session.BeginScenario(scenario);
            Assert.AreEqual("Capstone | Placement: AN-1 at Rack01", Invoke(ui, "HudObjectiveText"));
            scenario.expectedRackPlacements = new ExpectedRackPlacement[0];
            scenario.cableTypeRequirements = new[] { new ScenarioCableTypeRequirement
                { requirementId = "MissingCable", endpointAId = "A", endpointBId = "B" } };
            Assert.AreEqual("Capstone | Connect and terminate physical paths", Invoke(ui, "HudObjectiveText"));
            var physical = new TssPhysicalWorkPackageEvaluator(session).Evaluate();
            Assert.IsFalse(physical.Complete);
            StringAssert.Contains("A to B", physical.UnmetConditions[0]);

            scenario.networkScenario = new NetworkScenarioConfig { workPackageObjectives = new[] {
                new NetworkWorkPackageObjective { workPackage = 2, groups = new[] {
                    new NetworkWorkPackageObjectiveGroup { heading = "Configure AN-1 management VLAN", conditionIdPrefixes = new[] { "WP2.Management" } }
                } }
            } };
            var result = new TssWorkPackageResult();
            result.Add("WP2.Management.Svi", "Detailed diagnostic");
            Assert.AreEqual("Configure AN-1 management VLAN", Invoke(ui, "FirstIncompleteWorkPackageTask", 2, result));
            var complete = new TssWorkPackageResult();
            complete.Pass("WP2.Management.Svi", "Complete");
            Assert.IsNull(Invoke(ui, "FirstIncompleteWorkPackageTask", 2, complete));
        }
        finally
        {
            Object.Destroy(uiObject);
            Object.Destroy(sessionObject);
            Object.Destroy(scenario);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator HudAdvancesThroughNetworkWorkPackagesToScenarioComplete()
    {
        var sessionObject = new GameObject("WednesdayHudProgressSession");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        session.enabled = false;
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.displayName = "Capstone";
        scenario.cableTypeRequirements = new[] { new ScenarioCableTypeRequirement
            { requirementId = "Physical", endpointAId = "A", endpointBId = "B" } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true, routerRole = "CN-1", routerParentInterface = "Fa0/0", routerWanInterface = "Fa0/1",
            roleBindings = new[] { new NetworkRoleBinding { role = "AN-1", deviceKey = "switch", category = EquipmentCategory.Switch, managementAddressSuffix = 2 } },
            routerSubinterfaceRequirements = new[] { new NetworkRouterSubinterfaceRequirement { vlanName = "Management", networkName = "Management" } },
            requirePasswordEncryption = false, requireDomainLookupDisabled = false, requireConsoleLogin = false,
            requireVtyLogin = false, requireSave = false, requireRouteEvidence = false, requireDefaultRoute = false,
            requireAccessPointUplink = false, requireAccessPointSwitchPort = false, requireLaptopAssociation = false,
            requireDhcpLease = false, requireLaptopDiagnostics = false, requireRedWolfEvidence = false,
            workPackageObjectives = new[] {
                Objective(2, "Configure switch", "WP2.AN-1"),
                Objective(3, "Configure router", "WP3"),
                Objective(4, "Configure AP", "WP4.APConfig")
            }
        };
        var uiObject = new GameObject("WednesdayHudProgressUi");
        uiObject.SetActive(false);
        var ui = uiObject.AddComponent<TssRuntimeUi>();
        Set(ui, "session", session);
        try
        {
            session.BeginScenario(scenario);
            Get<System.Collections.IList>(session, "_physicalConnections").Add(new TssPhysicalConnectionRecord(
                "Physical", null, TssCableType.StraightThrough, "A", "B", null));
            Assert.IsTrue(new TssPhysicalWorkPackageEvaluator(session).Evaluate().Complete);
            Assert.AreEqual("Capstone | WP2: Configure switch", Invoke(ui, "HudObjectiveText"));

            var network = session.NetworkState;
            var profile = network.Profile;
            var sw = network.GetOrCreateDevice("switch", EquipmentCategory.Switch, "AN-1", "AN-1");
            sw.EnableSecret = profile.EnableSecret;
            sw.Motd = profile.Motd;
            sw.DefaultGateway = profile.ManagementNetwork + ".1";
            sw.Svis[1].AdminUp = false;
            sw.Svis[profile.ManagementVlan] = new NetworkInterfaceState { Name = "Vlan" + profile.ManagementVlan,
                AdminUp = true, Address = profile.ManagementNetwork + ".2", Mask = "255.255.255.0" };
            sw.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true,
                CarrierUp = true, AccessVlan = profile.ManagementVlan };
            sw.Vlans[profile.ManagementVlan] = profile.ManagementVlanName;
            sw.Vlans[profile.ReceptionVlan] = profile.ReceptionVlanName;
            sw.Vlans[profile.DataVlan] = profile.DataVlanName;
            sw.Vlans[profile.NativeVlan] = profile.NativeVlanName;
            sw.Vlans[profile.DeadVlan] = profile.DeadVlanName;
            sw.SuspendedVlans.Add(profile.DeadVlan);
            sw.Evidence.Add("show vlan brief");
            sw.Evidence.Add("show interfaces trunk");
            Assert.IsTrue(new TssNetworkWorkPackageEvaluator(network).EvaluateLayer2().Complete);
            Assert.AreEqual("Capstone | WP3: Configure router", Invoke(ui, "HudObjectiveText"));

            var router = network.GetOrCreateDevice("router", EquipmentCategory.Router, "CN-1", "CN-1");
            router.EnableSecret = profile.EnableSecret;
            router.Motd = profile.Motd;
            router.Interfaces["Fa0/0"] = new NetworkInterfaceState { Name = "Fa0/0", AdminUp = true, CarrierUp = true };
            router.Interfaces["Fa0/0.10"] = new NetworkInterfaceState { Name = "Fa0/0.10", Dot1qVlan = profile.ManagementVlan,
                Address = profile.ManagementNetwork + ".1", Mask = "255.255.255.0" };
            router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", AdminUp = true, CarrierUp = true,
                Address = profile.WanCustomerAddress, Mask = "255.255.255.252" };
            Assert.IsTrue(new TssNetworkWorkPackageEvaluator(network).EvaluateLayer3().Complete);
            Assert.AreEqual("Capstone | WP4: Configure AP", Invoke(ui, "HudObjectiveText"));
            network.AccessPoint.Configured = true;
            Assert.IsTrue(new TssNetworkWorkPackageEvaluator(network).EvaluateLayer4().Complete);
            Assert.AreEqual("Capstone | Scenario complete", Invoke(ui, "HudObjectiveText"));
        }
        finally
        {
            Object.Destroy(uiObject);
            Object.Destroy(sessionObject);
            Object.Destroy(scenario);
        }
        yield return null;
    }

    private static NetworkWorkPackageObjective Objective(int wp, string heading, string prefix) =>
        new NetworkWorkPackageObjective { workPackage = wp, groups = new[] {
            new NetworkWorkPackageObjectiveGroup { heading = heading, conditionIdPrefixes = new[] { prefix } }
        } };

    private static Color BlockColor(Renderer renderer, string property)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block, 0);
        return block.GetColor(property);
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private static T Get<T>(object target, string field) =>
        (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    private static object Invoke(object target, string method, params object[] arguments) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
}
