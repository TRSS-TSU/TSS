using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TssPlacementEvaluatorTests
{
    [Test]
    public void ValidatesWp1InventoryCounts()
    {
        var scenario = CreateScenario();

        Assert.IsTrue(TssPlacementEvaluator.EvaluateInventory(scenario).IsComplete);

        scenario.inventory[0].quantity = 1;

        var result = TssPlacementEvaluator.EvaluateInventory(scenario);
        Assert.IsFalse(result.IsComplete);
        Assert.That(Messages(result), Does.Contain("Inventory Pdu"));
    }

    [Test]
    public void AcceptsRequiredRackAndEndpointPlacements()
    {
        var result = TssPlacementEvaluator.Evaluate(CreateScenario(), CorrectRackPlacements(), CorrectEndpointPlacements());

        Assert.IsTrue(result.IsComplete, Messages(result));
    }

    [Test]
    public void RequirementStatusesAreCompleteOnlyForExactPlacementAndName()
    {
        var scenario = CreateScenario();
        var empty = TssPlacementEvaluator.EvaluateRequirements(scenario, new InstalledEquipmentRecord[0], new EndpointPlacementRecord[0]);

        Assert.AreEqual(14, empty.Count);
        Assert.IsFalse(empty[0].IsComplete);
        Assert.IsFalse(empty[13].IsComplete);
        Assert.AreEqual("Phase 1", empty[0].PhaseLabel);
        Assert.AreEqual("Phase 4", empty[13].PhaseLabel);

        var wrongRack = CorrectRackPlacements();
        wrongRack[0] = RackRecord("Rack01", Equipment(EquipmentCategory.Switch), 1, 1, "PDU1");
        var wrongEndpoint = CorrectEndpointPlacements();
        wrongEndpoint[5] = EndpointRecord("office2-printer", "Office2Desk", Equipment(EquipmentCategory.DesktopPc), "Data3");
        var wrong = TssPlacementEvaluator.EvaluateRequirements(scenario, wrongRack, wrongEndpoint);

        Assert.IsFalse(wrong[0].IsComplete);
        Assert.IsFalse(wrong[13].IsComplete);

        var correct = TssPlacementEvaluator.EvaluateRequirements(scenario, CorrectRackPlacements(), CorrectEndpointPlacements());

        Assert.IsTrue(correct[0].IsComplete);
        Assert.IsTrue(correct[13].IsComplete);
    }

    [Test]
    public void RejectsWrongRackNameSlotAndCategory()
    {
        var rack = CorrectRackPlacements();
        rack[0] = RackRecord("Rack01", Equipment(EquipmentCategory.Switch), 1, 1, "PDU1");
        rack[1] = RackRecord("Rack01", Equipment(EquipmentCategory.PatchPanel), 9, 1, "Patch1");
        rack[2] = RackRecord("Rack01", Equipment(EquipmentCategory.Switch), 3, 1, "AN 1");

        var result = TssPlacementEvaluator.Evaluate(CreateScenario(), rack, CorrectEndpointPlacements());

        Assert.IsFalse(result.IsComplete);
        var messages = Messages(result);
        Assert.That(messages, Does.Contain("expected Pdu"));
        Assert.That(messages, Does.Contain("missing Patch1"));
        Assert.That(messages, Does.Contain("expected name AN-1"));
    }

    [Test]
    public void AcceptsRackPlacementWithinConfiguredURange()
    {
        var scenario = CreateScenario();
        scenario.expectedRackPlacements[7].startingU = 5;
        scenario.expectedRackPlacements[7].endingU = 3;
        var rack = CorrectRackPlacements();
        rack[7] = RackRecord("Rack02", Equipment(EquipmentCategory.Switch), 4, 1, "AN-2");

        var result = TssPlacementEvaluator.EvaluateRequirements(scenario, rack, CorrectEndpointPlacements());

        Assert.IsTrue(result[7].IsComplete);
        Assert.AreEqual("Rack02 U3-U5", result[7].Location);
    }

    [Test]
    public void AcceptsFrontSwitchAndRearPduInOverlappingURanges()
    {
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.expectedRackPlacements = new[]
        {
            Rack("Phase 1", "Rack01", 35, EquipmentCategory.Pdu, "PDU1", 38),
            Rack("Phase 1", "Rack01", 35, EquipmentCategory.Switch, "AN-1", 38)
        };
        var pdu = Equipment(EquipmentCategory.Pdu);
        var placements = new[]
        {
            RackRecord("Rack01", pdu, 38, 1, "PDU1"),
            RackRecord("Rack01", Equipment(EquipmentCategory.Switch), 37, 1, "AN-1")
        };

        Assert.IsTrue(TssPlacementEvaluator.Evaluate(scenario, placements, null).IsComplete);
    }

    [Test]
    public void RejectsWrongEndpointStationNameAndDuplicate()
    {
        var endpoints = new[]
        {
            EndpointRecord("bad-station", "ReceptionDesk", Equipment(EquipmentCategory.DesktopPc), "IT1"),
            EndpointRecord("bad-name", "ReceptionDesk", Equipment(EquipmentCategory.DesktopPc), "Reception 1"),
            EndpointRecord("rec-printer", "ReceptionDesk", Equipment(EquipmentCategory.Printer), "Reception2"),
            EndpointRecord("office1", "Office1Desk", Equipment(EquipmentCategory.DesktopPc), "Data1"),
            EndpointRecord("office2-pc", "Office2Desk", Equipment(EquipmentCategory.DesktopPc), "Data2"),
            EndpointRecord("office2-printer", "Office2Desk", Equipment(EquipmentCategory.Printer), "Data3"),
            EndpointRecord("duplicate", "Office2Desk", Equipment(EquipmentCategory.Printer), "Data3")
        };

        var result = TssPlacementEvaluator.Evaluate(CreateScenario(), CorrectRackPlacements(), endpoints);

        Assert.IsFalse(result.IsComplete);
        var messages = Messages(result);
        Assert.That(messages, Does.Contain("ITDesk: missing IT1"));
        Assert.That(messages, Does.Contain("ReceptionDesk: missing Reception1"));
        Assert.That(messages, Does.Contain("Office2Desk: duplicate Data3"));
    }

    [Test]
    public void RenameChangesValidationResult()
    {
        var endpoints = CorrectEndpointPlacements();
        endpoints[0] = endpoints[0].WithDeviceName("Wrong");

        Assert.IsFalse(TssPlacementEvaluator.Evaluate(CreateScenario(), CorrectRackPlacements(), endpoints).IsComplete);

        endpoints[0] = endpoints[0].WithDeviceName("IT1");

        Assert.IsTrue(TssPlacementEvaluator.Evaluate(CreateScenario(), CorrectRackPlacements(), endpoints).IsComplete);
    }

    [Test]
    public void CheckoutAndReturnPreserveInventoryCounts()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        session.BeginScenario(scenario);
        var pdu = scenario.inventory[0].equipment;

        Assert.AreEqual(2, session.GetRemaining(pdu));
        Assert.IsTrue(session.TryCheckout(pdu, out _));
        Assert.AreEqual(1, session.GetRemaining(pdu));
        Assert.IsTrue(session.ReturnHeldItem(out _));
        Assert.AreEqual(2, session.GetRemaining(pdu));

        Object.DestroyImmediate(go);
    }

    [Test]
    public void CableCheckoutConnectDisconnectAndPickupBlock()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        session.BeginScenario(scenario);

        var endpointA = EndpointObject("A").AddComponent<TssPortEndpoint>();
        var endpointB = EndpointObject("B").AddComponent<TssPortEndpoint>();
        endpointA.Configure(TssTrainingSession.GetEndpointOwnerPrefix("ITDesk:PC:1") + ":Eth01", Port("Eth01"));
        endpointB.Configure("WallPort:Office01:Port04", Port("Port04"));

        Assert.AreEqual(1, session.GetRemaining(cable));
        Assert.IsTrue(session.TryCheckoutCable(cable, out _));
        Assert.AreEqual(0, session.GetRemaining(cable));
        Assert.IsTrue(session.TryUsePort(endpointA, out _));
        Assert.IsTrue(session.TryUsePort(endpointB, out _));
        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.IsFalse(session.TryPickupEndpoint("ITDesk:PC:1", Equipment(EquipmentCategory.DesktopPc), out _));

        Assert.IsTrue(session.TryUsePort(endpointA, out _));
        Assert.AreEqual(0, session.PhysicalConnections.Count);
        Assert.AreSame(cable, session.HeldCable);
        Assert.AreEqual(0, session.GetRemaining(cable));
        Assert.IsTrue(session.ReturnHeldCable(out _));
        Assert.AreEqual(1, session.GetRemaining(cable));

        Object.DestroyImmediate(endpointA.gameObject);
        Object.DestroyImmediate(endpointB.gameObject);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void DirectDeviceCableCountsAsEffectivePath()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        session.BeginScenario(scenario);

        var server = EndpointObject("Server").AddComponent<TssPortEndpoint>();
        var switchPort = EndpointObject("SwitchPort").AddComponent<TssPortEndpoint>();
        server.Configure("Rack:Rack01:U04:GE01", Port("GE01"));
        switchPort.Configure("Rack:Rack01:U36:Fa02", Port("Fa02"));
        var networkSwitch = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U36", EquipmentCategory.Switch, "Switch");
        networkSwitch.Interfaces["Fa0/2"] = new NetworkInterfaceState
        {
            Name = "Fa0/2", EndpointId = switchPort.EndpointId, AdminUp = true
        };

        Assert.IsTrue(session.TryCheckoutCable(cable, out _));
        Assert.IsTrue(session.TryUsePort(server, out _));
        Assert.IsTrue(session.TryUsePort(switchPort, out _));
        Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);
        Assert.IsTrue(session.IsEndpointOnEffectivePhysicalPath(server.EndpointId));
        Assert.IsTrue(session.IsEndpointOnEffectivePhysicalPath(switchPort.EndpointId));
        Assert.IsTrue(networkSwitch.Interfaces["Fa0/2"].CarrierUp);

        Object.DestroyImmediate(server.gameObject);
        Object.DestroyImmediate(switchPort.gameObject);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void ScheduleMismatchedSwitchLinkStillProvidesPhysicalCarrier()
    {
        var root = new GameObject("Schedule mismatch signal path");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var crossover = Cable(TssCableType.Crossover);
        scenario.defaultPlayerCableType = TssCableType.StraightThrough;
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = crossover, quantity = 1 } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true,
            roleBindings = new[]
            {
                new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U35", category = EquipmentCategory.Switch },
                new NetworkRoleBinding { role = "AN-2", deviceKey = "Rack:Rack02:U35", category = EquipmentCategory.Switch }
            }
        };

        var first = EndpointObject("AN-1 Fa0/7").AddComponent<TssPortEndpoint>();
        var second = EndpointObject("AN-2 Fa0/7").AddComponent<TssPortEndpoint>();
        first.Configure("Rack:Rack01:U35:Fa07", Port("Fa07"));
        second.Configure("Rack:Rack02:U35:Fa07", Port("Fa07"));

        try
        {
            session.BeginScenario(scenario);
            var an1 = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
            an1.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", EndpointId = first.EndpointId, AdminUp = true };
            var an2 = session.NetworkState.GetOrCreateDevice("Rack:Rack02:U35", EquipmentCategory.Switch, "AN-2");
            an2.Interfaces["Fa0/7"] = new NetworkInterfaceState { Name = "Fa0/7", EndpointId = second.EndpointId, AdminUp = true };
            Assert.IsTrue(session.TryCheckoutCable(crossover, out _));
            Assert.IsTrue(session.TryUsePort(first, out _));
            Assert.IsTrue(session.TryUsePort(second, out _));

            Assert.IsFalse(session.IsCableTypeCorrect(session.PhysicalConnections[0]));
            Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);
            Assert.IsTrue(an1.Interfaces["Fa0/7"].CarrierUp);
            Assert.IsTrue(an2.Interfaces["Fa0/7"].CarrierUp);
        }
        finally
        {
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(crossover);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SignalPathRejectsEvenCrossoverParityBetweenSwitches()
    {
        var root = new GameObject("Invalid switch parity");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var straight = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = straight, quantity = 1 } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            roleBindings = new[]
            {
                new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U35", category = EquipmentCategory.Switch },
                new NetworkRoleBinding { role = "AN-2", deviceKey = "Rack:Rack02:U35", category = EquipmentCategory.Switch }
            }
        };

        var first = EndpointObject("AN-1 Fa0/7").AddComponent<TssPortEndpoint>();
        var second = EndpointObject("AN-2 Fa0/7").AddComponent<TssPortEndpoint>();
        first.Configure("Rack:Rack01:U35:Fa07", Port("Fa07"));
        second.Configure("Rack:Rack02:U35:Fa07", Port("Fa07"));

        try
        {
            session.BeginScenario(scenario);
            Assert.IsTrue(session.TryCheckoutCable(straight, out _));
            Assert.IsTrue(session.TryUsePort(first, out _));
            Assert.IsTrue(session.TryUsePort(second, out _));
            Assert.AreEqual(0, session.GetEffectivePhysicalPaths().Count);
        }
        finally
        {
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(straight);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PhysicalCarrierSurvivesAdministrativeShutdown()
    {
        var root = new GameObject("Shutdown Layer1 path");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var straight = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = straight, quantity = 1 } };
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };

        var host = EndpointObject("Host").AddComponent<TssPortEndpoint>();
        var switchPort = EndpointObject("Switch").AddComponent<TssPortEndpoint>();
        host.Configure("Endpoint:Host:Eth01", Port("Eth01"));
        switchPort.Configure("Rack:Rack01:U35:Fa07", Port("Fa07"));

        try
        {
            session.BeginScenario(scenario);
            var networkSwitch = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
            networkSwitch.Interfaces["Fa0/7"] = new NetworkInterfaceState
            {
                Name = "Fa0/7", EndpointId = switchPort.EndpointId, AdminUp = false
            };

            Assert.IsTrue(session.TryCheckoutCable(straight, out _));
            Assert.IsTrue(session.TryUsePort(host, out _));
            Assert.IsTrue(session.TryUsePort(switchPort, out _));

            Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);
            Assert.IsTrue(networkSwitch.Interfaces["Fa0/7"].CarrierUp);
            Assert.IsFalse(session.NetworkState.IsEndpointLogicalLayer1Available(session, switchPort.EndpointId));
        }
        finally
        {
            Object.DestroyImmediate(host.gameObject);
            Object.DestroyImmediate(switchPort.gameObject);
            Object.DestroyImmediate(straight);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LogicalLayer1UsesRouterDefaultBeforeItsInterfaceIsRegistered()
    {
        var root = new GameObject("Local interface precedence");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var straight = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = straight, quantity = 1 } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true,
            roleBindings = new[]
            {
                new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U37", category = EquipmentCategory.Switch },
                new NetworkRoleBinding { role = "CN-1", deviceKey = "Rack:Rack01:U34", category = EquipmentCategory.Router }
            }
        };

        var routerPort = EndpointObject("CN-1 Fa0/0").AddComponent<TssPortEndpoint>();
        var switchPort = EndpointObject("AN-1 Fa0/6").AddComponent<TssPortEndpoint>();
        routerPort.Configure("Rack:Rack01:U34:Fa00", Port("Fa00"));
        switchPort.Configure("Rack:Rack01:U37:Fa06", Port("Fa06"));

        try
        {
            session.BeginScenario(scenario);
            Assert.IsTrue(session.TryCheckoutCable(straight, out _));
            Assert.IsTrue(session.TryUsePort(routerPort, out _));
            Assert.IsTrue(session.TryUsePort(switchPort, out _));

            Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);
            Assert.IsFalse(session.NetworkState.IsEndpointLogicalLayer1Available(session, switchPort.EndpointId));
            Assert.IsFalse(session.NetworkState.IsEndpointLogicalLayer1Available(session, routerPort.EndpointId));

            var networkSwitch = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U37", EquipmentCategory.Switch, "AN-1");
            networkSwitch.Interfaces["Fa0/6"] = new NetworkInterfaceState
            {
                Name = "Fa0/6", EndpointId = switchPort.EndpointId, AdminUp = true, CarrierUp = true
            };
            var router = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U34", EquipmentCategory.Router, "CN-1");
            router.Interfaces["Fa0/0"] = new NetworkInterfaceState
            {
                Name = "Fa0/0", EndpointId = routerPort.EndpointId, AdminUp = false, CarrierUp = true
            };
            Assert.IsFalse(session.NetworkState.IsEndpointLogicalLayer1Available(session, switchPort.EndpointId));
            Assert.IsFalse(session.NetworkState.IsEndpointLogicalLayer1Available(session, routerPort.EndpointId));

            router.Interfaces["Fa0/0"].AdminUp = true;
            Assert.IsTrue(session.NetworkState.IsEndpointLogicalLayer1Available(session, switchPort.EndpointId));
            Assert.IsTrue(session.NetworkState.IsEndpointLogicalLayer1Available(session, routerPort.EndpointId));
        }
        finally
        {
            Object.DestroyImmediate(routerPort.gameObject);
            Object.DestroyImmediate(switchPort.gameObject);
            Object.DestroyImmediate(straight);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void UnterminatedPatchPanelCableDoesNotCountAsEffectivePath()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                connectionId = "Infrastructure001",
                patchPanelEndpointId = "PatchPanel:Rack01:Port01",
                wallportEndpointId = "WallPort:Reception:Port01",
                requiresPunchDownTermination = true
            }
        };
        session.BeginScenario(scenario);

        var patch = EndpointObject("Patch").AddComponent<TssPortEndpoint>();
        var switchPort = EndpointObject("SwitchPort").AddComponent<TssPortEndpoint>();
        patch.Configure("PatchPanel:Rack01:Port01", Port("PP 01 Port 01"));
        switchPort.Configure("Rack:Rack01:U36:Fa03", Port("FastEthernet 0/3"));

        Assert.IsTrue(session.TryCheckoutCable(cable, out _));
        Assert.IsTrue(session.TryUsePort(patch, out _));
        Assert.IsTrue(session.TryUsePort(switchPort, out _));
        Assert.AreEqual(0, session.GetEffectivePhysicalPaths().Count);
        Assert.IsFalse(session.IsEndpointOnEffectivePhysicalPath(switchPort.EndpointId));

        var logicalState = new TssDeviceLogicalState("Rack:Rack01:U36", EquipmentCategory.Switch, "Switch");
        logicalState.RefreshPhysicalConnectivity(new[] { switchPort }, session);
        Assert.IsFalse(logicalState.Interfaces[0].IsPhysicalLinkUp);

        Object.DestroyImmediate(patch.gameObject);
        Object.DestroyImmediate(switchPort.gameObject);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void EffectivePathsReportOnlyDeviceLeavesThroughPermanentInfrastructure()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        var patch = EndpointObject("Patch").AddComponent<TssPortEndpoint>();
        var wall = EndpointObject("Wall").AddComponent<TssPortEndpoint>();
        var switchPort = EndpointObject("SwitchPort").AddComponent<TssPortEndpoint>();
        var accessPoint = EndpointObject("AccessPoint").AddComponent<TssPortEndpoint>();

        try
        {
            scenario.networkScenario = new NetworkScenarioConfig
            {
                roleBindings = new[]
                {
                    new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U37", category = EquipmentCategory.Switch }
                }
            };
            patch.Configure("PatchPanel:Rack01:Port03", Port("Port 03"));
            wall.Configure("WallPort:Reception:Port03", Port("Reception Port 03"));
            switchPort.Configure("Rack:Rack01:U37:Fa14", Port("FastEthernet 0/14"));
            accessPoint.Configure("Endpoint:AP-1:Eth01", Port("Ethernet Port 01"));
            scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 2 } };
            scenario.permanentConnections = new[]
            {
                new ScenarioPermanentCableConnection
                {
                    connectionId = "Infrastructure003",
                    patchPanelEndpointId = patch.EndpointId,
                    wallportEndpointId = wall.EndpointId
                }
            };

            session.BeginScenario(scenario);
            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(accessPoint, out _));
            Assert.IsTrue(session.TryUsePort(wall, out _));
            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(patch, out _));
            Assert.IsTrue(session.TryUsePort(switchPort, out _));

            var paths = session.GetEffectivePhysicalPaths();
            Assert.AreEqual(1, paths.Count);
            Assert.IsTrue(
                (paths[0].EndpointAId == accessPoint.EndpointId && paths[0].EndpointBId == switchPort.EndpointId)
                || (paths[0].EndpointAId == switchPort.EndpointId && paths[0].EndpointBId == accessPoint.EndpointId));
        }
        finally
        {
            Object.DestroyImmediate(patch.gameObject);
            Object.DestroyImmediate(wall.gameObject);
            Object.DestroyImmediate(switchPort.gameObject);
            Object.DestroyImmediate(accessPoint.gameObject);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(cable);
            Object.DestroyImmediate(scenario);
        }
    }

    [Test]
    public void PermanentScenarioConnectionsDoNotConsumeInventoryOrDisconnect()
    {
        var go = new GameObject("Session");
        var session = go.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 2 } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            roleBindings = new[]
            {
                new NetworkRoleBinding { role = "Switch", deviceKey = "Rack:Rack01:U03", category = EquipmentCategory.Switch }
            }
        };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                cableType = TssCableType.StraightThrough,
                patchPanelEndpointId = "Rack:Rack01:U02:Port01",
                wallportEndpointId = "WallPort:Office01:Port01"
            }
        };

        var endpointA = EndpointObject("A").AddComponent<TssPortEndpoint>();
        var endpointB = EndpointObject("B").AddComponent<TssPortEndpoint>();
        var endpointC = EndpointObject("C").AddComponent<TssPortEndpoint>();
        var endpointD = EndpointObject("D").AddComponent<TssPortEndpoint>();
        endpointA.Configure("Rack:Rack01:U02:Port01", Port("Port01"));
        endpointB.Configure("WallPort:Office01:Port01", Port("Port01"));
        endpointC.Configure("Rack:Rack01:U03:Port01", Port("Switch01"));
        endpointD.Configure("Endpoint:OfficePc:Eth01", Port("Eth01"));
        var visualCountBeforePermanent = Object.FindObjectsByType<TssCableVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        session.BeginScenario(scenario);

        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.IsTrue(session.PhysicalConnections[0].IsPermanent);
        Assert.IsFalse(session.PhysicalConnections[0].Visual);
        Assert.AreEqual(2, session.GetRemaining(cable));
        Assert.IsFalse(session.IsEndpointConnected(endpointA.EndpointId));
        Assert.IsFalse(session.TryUsePort(endpointA, out _));
        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.AreEqual(visualCountBeforePermanent, Object.FindObjectsByType<TssCableVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
        Assert.IsTrue(session.TryCheckoutCable(cable, out _));
        Assert.IsTrue(session.TryUsePort(endpointA, out _));
        Assert.IsTrue(session.TryUsePort(endpointC, out _));
        Assert.AreEqual(2, session.PhysicalConnections.Count);
        Assert.IsTrue(session.IsEndpointConnected(endpointA.EndpointId));
        Assert.IsFalse(session.IsEndpointConnected(endpointB.EndpointId));
        Assert.IsFalse(session.HasPhysicalPath(endpointC.EndpointId, endpointD.EndpointId));
        Assert.IsTrue(session.TryCheckoutCable(cable, out _));
        Assert.IsTrue(session.TryUsePort(endpointB, out _));
        Assert.IsTrue(session.TryUsePort(endpointD, out _));
        Assert.AreEqual(3, session.PhysicalConnections.Count);
        Assert.IsTrue(session.HasPhysicalPath(endpointC.EndpointId, endpointD.EndpointId));
        Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);

        Object.DestroyImmediate(endpointA.gameObject);
        Object.DestroyImmediate(endpointB.gameObject);
        Object.DestroyImmediate(endpointC.gameObject);
        Object.DestroyImmediate(endpointD.gameObject);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void EndpointStationUsesCategoryAnchorsBeforeFallbackAnchors()
    {
        var stationObject = new GameObject("Station");
        var pcAnchor = new GameObject("PC Anchor").transform;
        var printerAnchor = new GameObject("Printer Anchor").transform;
        var fallbackAnchor = new GameObject("Fallback Anchor").transform;
        pcAnchor.SetParent(stationObject.transform);
        printerAnchor.SetParent(stationObject.transform);
        fallbackAnchor.SetParent(stationObject.transform);

        var station = stationObject.AddComponent<EndpointPlacementStation>();
        SetPrivate(station, "placementAnchors", new[] { fallbackAnchor });
        SetPrivate(station, "desktopPlacementAnchors", new[] { pcAnchor });
        SetPrivate(station, "printerPlacementAnchors", new[] { printerAnchor });

        Assert.AreSame(pcAnchor, GetFreeAnchor(station, Equipment(EquipmentCategory.DesktopPc)));
        Assert.AreSame(printerAnchor, GetFreeAnchor(station, Equipment(EquipmentCategory.Printer)));
        Assert.AreSame(fallbackAnchor, GetFreeAnchor(station, Equipment(EquipmentCategory.Switch)));

        Object.DestroyImmediate(stationObject);
    }

    [Test]
    public void CarryingItemBlocksPlacedEndpointInspection()
    {
        var sessionObject = new GameObject("Session");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        session.BeginScenario(scenario);

        Assert.IsTrue(CanInspectPlacedEndpoint(session));
        Assert.IsTrue(session.TryCheckout(scenario.inventory[5].equipment, out _));
        Assert.IsFalse(CanInspectPlacedEndpoint(session));

        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void ProviderDemarcPathThroughPermanentInfrastructureSetsWanCarrier()
    {
        var root = new GameObject("Provider Demarc Path");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.StraightThrough);
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                connectionId = "ProviderDemarc",
                patchPanelEndpointId = "PatchPanel:Rack01:Port18",
                wallportEndpointId = "Demarc:Provider:Port01"
            }
        };

        var routerPort = EndpointObject("Router Fa0/1").AddComponent<TssPortEndpoint>();
        var patchPort = EndpointObject("Patch Port 18").AddComponent<TssPortEndpoint>();
        var demarcPort = EndpointObject("Provider Demarc").AddComponent<TssPortEndpoint>();
        routerPort.Configure("Rack:Rack01:U32:Fa01", Port("Fa01"));
        patchPort.Configure("PatchPanel:Rack01:Port18", Port("Port18"));
        demarcPort.Configure("Demarc:Provider:Port01", Port("Port01"));

        try
        {
            session.BeginScenario(scenario);
            var router = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U32", EquipmentCategory.Router, "CN-1");
            router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", EndpointId = routerPort.EndpointId, AdminUp = true };

            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(routerPort, out _));
            Assert.IsTrue(session.TryUsePort(patchPort, out _));

            Assert.IsTrue(session.IsProviderDemarcConnected(routerPort.EndpointId));
            session.NotifyNetworkStateChanged();
            Assert.IsTrue(router.Interfaces["Fa0/1"].CarrierUp);
        }
        finally
        {
            Object.DestroyImmediate(routerPort.gameObject);
            Object.DestroyImmediate(patchPort.gameObject);
            Object.DestroyImmediate(demarcPort.gameObject);
            Object.DestroyImmediate(cable);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RouterWanCarrierRequiresDemarcPath()
    {
        var root = new GameObject("Provider Demarc Missing Path");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        scenario.networkScenario = new NetworkScenarioConfig { enableLayer23Simulation = true };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                connectionId = "ProviderDemarc",
                patchPanelEndpointId = "PatchPanel:Rack01:Port18",
                wallportEndpointId = "Demarc:Provider:Port01"
            }
        };

        var routerPort = EndpointObject("Router Fa0/1").AddComponent<TssPortEndpoint>();
        var patchPort = EndpointObject("Patch Port 18").AddComponent<TssPortEndpoint>();
        var demarcPort = EndpointObject("Provider Demarc").AddComponent<TssPortEndpoint>();
        routerPort.Configure("Rack:Rack01:U32:Fa01", Port("Fa01"));
        patchPort.Configure("PatchPanel:Rack01:Port18", Port("Port18"));
        demarcPort.Configure("Demarc:Provider:Port01", Port("Port01"));

        try
        {
            session.BeginScenario(scenario);
            var router = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U32", EquipmentCategory.Router, "CN-1");
            router.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", EndpointId = routerPort.EndpointId, AdminUp = true };

            session.NotifyNetworkStateChanged();

            Assert.IsFalse(session.IsProviderDemarcConnected(routerPort.EndpointId));
            Assert.IsFalse(router.Interfaces["Fa0/1"].CarrierUp);
        }
        finally
        {
            Object.DestroyImmediate(routerPort.gameObject);
            Object.DestroyImmediate(patchPort.gameObject);
            Object.DestroyImmediate(demarcPort.gameObject);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RenamingInstalledRackDeviceInvalidatesEffectivePathCache()
    {
        var root = new GameObject("Rack Cache Invalidation");
        var session = root.AddComponent<TssTrainingSession>();
        var scenario = CreateScenario();
        var cable = Cable(TssCableType.Crossover);
        var router = Equipment(EquipmentCategory.Router);
        scenario.defaultPlayerCableType = TssCableType.StraightThrough;
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 1 } };
        scenario.expectedRackPlacements = new[] { Rack("Phase", "Rack01", 32, EquipmentCategory.Router, "CN-1") };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            roleBindings = new[] { new NetworkRoleBinding { role = "CN-1", deviceKey = "Rack:Rack01:U32", category = EquipmentCategory.Router } }
        };
        scenario.cableTypeRequirements = new[]
        {
            new ScenarioCableTypeRequirement
            {
                requirementId = "RouterPeer",
                endpointAId = "Rack:Rack01:U32:Fa01",
                endpointBId = "Endpoint:Peer:Eth01",
                requiredCableType = TssCableType.Crossover
            }
        };

        var routerPort = EndpointObject("Router Fa0/1").AddComponent<TssPortEndpoint>();
        var peerPort = EndpointObject("Peer Eth01").AddComponent<TssPortEndpoint>();
        routerPort.Configure("Rack:Rack01:U32:Fa01", Port("Fa01"));
        peerPort.Configure("Endpoint:Peer:Eth01", Port("Eth01"));

        try
        {
            session.BeginScenario(scenario);
            ((List<InstalledEquipmentRecord>)session.Installed).Add(RackRecord("Rack01", router, 32, router.rackUnits, "CN-1"));
            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(routerPort, out _));
            Assert.IsTrue(session.TryUsePort(peerPort, out _));
            Assert.AreEqual(1, session.GetEffectivePhysicalPaths().Count);

            Assert.IsTrue(session.RenameRackInstalled("Rack01", 32, "WrongName"));
            Assert.AreEqual(0, session.GetEffectivePhysicalPaths().Count);
        }
        finally
        {
            Object.DestroyImmediate(routerPort.gameObject);
            Object.DestroyImmediate(peerPort.gameObject);
            Object.DestroyImmediate(router);
            Object.DestroyImmediate(cable);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(root);
        }
    }

    private static TssScenarioDefinition CreateScenario()
    {
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.inventory = new[]
        {
            Item(EquipmentCategory.Pdu, 2),
            Item(EquipmentCategory.PatchPanel, 2),
            Item(EquipmentCategory.Switch, 2),
            Item(EquipmentCategory.Router, 1),
            Item(EquipmentCategory.Server, 1),
            Item(EquipmentCategory.DesktopPc, 4),
            Item(EquipmentCategory.Printer, 2)
        };
        scenario.expectedRackPlacements = new[]
        {
            Rack("Phase 1", "Rack01", 1, EquipmentCategory.Pdu, "PDU1"),
            Rack("Phase 1", "Rack01", 2, EquipmentCategory.PatchPanel, "Patch1"),
            Rack("Phase 1", "Rack01", 3, EquipmentCategory.Switch, "AN-1"),
            Rack("Phase 1", "Rack01", 4, EquipmentCategory.Router, "CN-1"),
            Rack("Phase 1", "Rack01", 5, EquipmentCategory.Server, "IT2"),
            Rack("Phase 2", "Rack02", 1, EquipmentCategory.Pdu, "PDU2"),
            Rack("Phase 2", "Rack02", 2, EquipmentCategory.PatchPanel, "Patch2"),
            Rack("Phase 2", "Rack02", 3, EquipmentCategory.Switch, "AN-2")
        };
        scenario.expectedEndpointPlacements = new[]
        {
            Endpoint("Phase 3", "ITDesk", EquipmentCategory.DesktopPc, "IT1"),
            Endpoint("Phase 3", "ReceptionDesk", EquipmentCategory.DesktopPc, "Reception1"),
            Endpoint("Phase 3", "ReceptionDesk", EquipmentCategory.Printer, "Reception2"),
            Endpoint("Phase 4", "Office1Desk", EquipmentCategory.DesktopPc, "Data1"),
            Endpoint("Phase 4", "Office2Desk", EquipmentCategory.DesktopPc, "Data2"),
            Endpoint("Phase 4", "Office2Desk", EquipmentCategory.Printer, "Data3")
        };
        return scenario;
    }

    private static InstalledEquipmentRecord[] CorrectRackPlacements()
    {
        return new[]
        {
            RackRecord("Rack01", Equipment(EquipmentCategory.Pdu), 1, 1, "PDU1"),
            RackRecord("Rack01", Equipment(EquipmentCategory.PatchPanel), 2, 1, "Patch1"),
            RackRecord("Rack01", Equipment(EquipmentCategory.Switch), 3, 1, "AN-1"),
            RackRecord("Rack01", Equipment(EquipmentCategory.Router), 4, 1, "CN-1"),
            RackRecord("Rack01", Equipment(EquipmentCategory.Server), 5, 1, "IT2"),
            RackRecord("Rack02", Equipment(EquipmentCategory.Pdu), 1, 1, "PDU2"),
            RackRecord("Rack02", Equipment(EquipmentCategory.PatchPanel), 2, 1, "Patch2"),
            RackRecord("Rack02", Equipment(EquipmentCategory.Switch), 3, 1, "AN-2")
        };
    }

    private static EndpointPlacementRecord[] CorrectEndpointPlacements()
    {
        return new[]
        {
            EndpointRecord("it", "ITDesk", Equipment(EquipmentCategory.DesktopPc), "IT1"),
            EndpointRecord("rec-pc", "ReceptionDesk", Equipment(EquipmentCategory.DesktopPc), "Reception1"),
            EndpointRecord("rec-printer", "ReceptionDesk", Equipment(EquipmentCategory.Printer), "Reception2"),
            EndpointRecord("office1", "Office1Desk", Equipment(EquipmentCategory.DesktopPc), "Data1"),
            EndpointRecord("office2-pc", "Office2Desk", Equipment(EquipmentCategory.DesktopPc), "Data2"),
            EndpointRecord("office2-printer", "Office2Desk", Equipment(EquipmentCategory.Printer), "Data3")
        };
    }

    private static ScenarioInventoryItem Item(EquipmentCategory category, int quantity)
    {
        return new ScenarioInventoryItem
        {
            equipment = Equipment(category),
            quantity = quantity
        };
    }

    private static EquipmentDefinition Equipment(EquipmentCategory category)
    {
        var equipment = ScriptableObject.CreateInstance<EquipmentDefinition>();
        equipment.category = category;
        equipment.useRearRackPlacement = category == EquipmentCategory.Pdu;
        equipment.equipmentId = category.ToString();
        equipment.displayName = category.ToString();
        return equipment;
    }

    private static TssCableDefinition Cable(TssCableType type)
    {
        var cable = ScriptableObject.CreateInstance<TssCableDefinition>();
        cable.cableType = type;
        cable.cableId = type.ToString();
        cable.displayName = type.ToString();
        return cable;
    }

    private static EquipmentInterface Port(string name)
    {
        return new EquipmentInterface
        {
            name = name,
            label = name,
            connectorType = "RJ45",
            supportedCableTypes = new[] { TssCableType.StraightThrough }
        };
    }

    private static GameObject EndpointObject(string name)
    {
        return GameObject.CreatePrimitive(PrimitiveType.Cube);
    }

    private static Transform GetFreeAnchor(EndpointPlacementStation station, EquipmentDefinition equipment)
    {
        var method = typeof(EndpointPlacementStation).GetMethod("GetFreeAnchor", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(EquipmentDefinition) }, null);
        return (Transform)method.Invoke(station, new object[] { equipment });
    }

    private static void SetPrivate<T>(EndpointPlacementStation station, string fieldName, T value)
    {
        var field = typeof(EndpointPlacementStation).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(station, value);
    }

    private static bool CanInspectPlacedEndpoint(TssTrainingSession session)
    {
        var method = typeof(EndpointPlacementStation).GetMethod("CanInspectPlacedEndpoint", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return (bool)method.Invoke(null, new object[] { session });
    }

    private static ExpectedRackPlacement Rack(string phaseLabel, string rackId, int startingU, EquipmentCategory category, string requiredName, int endingU = 0)
    {
        return new ExpectedRackPlacement
        {
            phaseLabel = phaseLabel,
            rackId = rackId,
            startingU = startingU,
            endingU = endingU,
            category = category,
            requiredName = requiredName
        };
    }

    private static ExpectedEndpointPlacement Endpoint(string phaseLabel, string stationId, EquipmentCategory category, string requiredName)
    {
        return new ExpectedEndpointPlacement
        {
            phaseLabel = phaseLabel,
            stationId = stationId,
            category = category,
            requiredName = requiredName
        };
    }

    private static InstalledEquipmentRecord RackRecord(string rackId, EquipmentDefinition equipment, int startingU, int rackUnits, string deviceName)
    {
        return new InstalledEquipmentRecord(rackId, equipment, startingU, rackUnits, deviceName);
    }

    private static EndpointPlacementRecord EndpointRecord(string placementId, string stationId, EquipmentDefinition equipment, string deviceName)
    {
        return new EndpointPlacementRecord(placementId, stationId, equipment, deviceName);
    }

    private static string Messages(TssPlacementEvaluationResult result)
    {
        return string.Join("\n", result.Messages);
    }
}
