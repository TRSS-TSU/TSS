using NUnit.Framework;
using UnityEngine;

public sealed class TssTerminationTests
{
    [Test]
    public void T568BValidatorRequiresExactPunchedOrder()
    {
        var valid = Module("Infrastructure001", "Patch:01", "Infrastructure001:A", true);

        Assert.IsTrue(TssT568BValidator.IsElectricallyCorrect(valid));

        valid.terminals[5].conductor = TssConductorColor.WhiteGreen;
        Assert.IsFalse(TssT568BValidator.IsElectricallyCorrect(valid));

        valid = Module("Infrastructure001", "Patch:01", "Infrastructure001:A", true);
        valid.terminals[0].isPunched = false;
        Assert.IsFalse(TssT568BValidator.IsElectricallyCorrect(valid));

        valid = Module("Infrastructure001", "Patch:01", "Infrastructure001:A", true);
        valid.terminals[7].pin = 7;
        Assert.IsFalse(TssT568BValidator.HasExactEightPins(valid));
    }

    [Test]
    public void FlipChangesTheAcceptableIdcBank()
    {
        var board = new GameObject("Board", typeof(RectTransform));
        var wireObject = new GameObject("Wire", typeof(RectTransform), typeof(TssTerminationDraggable));
        wireObject.transform.SetParent(board.transform, false);
        var wire = wireObject.GetComponent<TssTerminationDraggable>();
        wire.Bind(null, board.GetComponent<RectTransform>(), new Vector2(370f, -305f),
            TssConductorColor.WhiteOrange, false, Color.white, new Vector2(586f, -205f));
        wire.SetPlacement(new Vector2(370f, -305f), 1, false, false);

        Assert.IsTrue(wire.CanEnterBank(false));
        Assert.IsFalse(wire.CanEnterBank(true));
        wire.Flip();
        Assert.IsTrue(wire.CanEnterBank(true));
        Assert.IsFalse(wire.CanEnterBank(false));
        wire.Flip();
        Assert.IsTrue(wire.CanEnterBank(false));
        Assert.IsFalse(wire.CanEnterBank(true));

        Object.DestroyImmediate(board);
    }

    [Test]
    public void PunchDownGatesPermanentEdgeAndSupportsRework()
    {
        var sessionObject = new GameObject("Session");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        var endpointA = Endpoint("Patch:01");
        var endpointB = Endpoint("Wall:01");
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                connectionId = "Infrastructure001",
                patchPanelEndpointId = endpointA.EndpointId,
                wallportEndpointId = endpointB.EndpointId,
                requiresPunchDownTermination = true,
                endpointATerminationModuleId = "Infrastructure001:A",
                endpointBTerminationModuleId = "Infrastructure001:B"
            }
        };

        session.BeginScenario(scenario);

        Assert.AreEqual(0, session.PhysicalConnections.Count);
        Assert.IsTrue(session.SetTerminationModuleState(
            Module("Infrastructure001", endpointA.EndpointId, "Infrastructure001:A", true),
            true,
            out _));
        Assert.AreEqual(0, session.PhysicalConnections.Count);
        Assert.IsTrue(session.SetTerminationModuleState(
            Module("Infrastructure001", endpointB.EndpointId, "Infrastructure001:B", true),
            true,
            out _));
        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.IsTrue(session.PhysicalConnections[0].IsPermanent);

        Assert.IsTrue(session.SetTerminationModuleState(
            Module("Infrastructure001", endpointA.EndpointId, "Infrastructure001:A", false),
            false,
            out _));
        Assert.AreEqual(0, session.PhysicalConnections.Count);

        Object.DestroyImmediate(endpointA.gameObject);
        Object.DestroyImmediate(endpointB.gameObject);
        Object.DestroyImmediate(sessionObject);
    }

    [Test]
    public void CompletingPunchDownRefreshesSwitchCarrier()
    {
        var sessionObject = new GameObject("Session");
        var session = sessionObject.AddComponent<TssTrainingSession>();
        var switchPort = Endpoint("Rack:Rack01:U35:Fa01");
        var patch = Endpoint("Patch:01");
        var wall = Endpoint("Wall:01");
        var host = Endpoint("Endpoint:Host:Eth01");
        var cable = ScriptableObject.CreateInstance<TssCableDefinition>();
        cable.cableType = TssCableType.StraightThrough;
        var scenario = ScriptableObject.CreateInstance<TssScenarioDefinition>();
        scenario.cableInventory = new[] { new ScenarioCableInventoryItem { cable = cable, quantity = 2 } };
        scenario.networkScenario = new NetworkScenarioConfig
        {
            enableLayer23Simulation = true,
            roleBindings = new[] { new NetworkRoleBinding { role = "AN-1", deviceKey = "Rack:Rack01:U35", category = EquipmentCategory.Switch } }
        };
        scenario.permanentConnections = new[]
        {
            new ScenarioPermanentCableConnection
            {
                connectionId = "Infrastructure001", patchPanelEndpointId = patch.EndpointId, wallportEndpointId = wall.EndpointId,
                requiresPunchDownTermination = true, endpointATerminationModuleId = "Infrastructure001:A", endpointBTerminationModuleId = "Infrastructure001:B"
            }
        };

        try
        {
            session.BeginScenario(scenario);
            var device = session.NetworkState.GetOrCreateDevice("Rack:Rack01:U35", EquipmentCategory.Switch, "AN-1");
            device.Interfaces["Fa0/1"] = new NetworkInterfaceState { Name = "Fa0/1", EndpointId = switchPort.EndpointId, AdminUp = true };
            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(switchPort, out _));
            Assert.IsTrue(session.TryUsePort(patch, out _));
            Assert.IsTrue(session.TryCheckoutCable(cable, out _));
            Assert.IsTrue(session.TryUsePort(wall, out _));
            Assert.IsTrue(session.TryUsePort(host, out _));
            Assert.IsFalse(device.Interfaces["Fa0/1"].CarrierUp);

            Assert.IsTrue(session.SetTerminationModuleState(Module("Infrastructure001", patch.EndpointId, "Infrastructure001:A", true), true, out _));
            Assert.IsFalse(device.Interfaces["Fa0/1"].CarrierUp);
            Assert.IsTrue(session.SetTerminationModuleState(Module("Infrastructure001", wall.EndpointId, "Infrastructure001:B", true), true, out _));
            Assert.IsTrue(device.Interfaces["Fa0/1"].CarrierUp);
            Assert.IsTrue(session.NetworkState.IsEndpointLogicalLayer1Available(session, switchPort.EndpointId));
            Assert.IsTrue(session.NetworkState.IsEndpointLogicalLayer2Available(session, switchPort.EndpointId));
        }
        finally
        {
            Object.DestroyImmediate(switchPort.gameObject);
            Object.DestroyImmediate(patch.gameObject);
            Object.DestroyImmediate(wall.gameObject);
            Object.DestroyImmediate(host.gameObject);
            Object.DestroyImmediate(cable);
            Object.DestroyImmediate(scenario);
            Object.DestroyImmediate(sessionObject);
        }
    }

    private static TssTerminationModuleState Module(string connectionId, string endpointId, string moduleId, bool correct)
    {
        var colors = new[]
        {
            TssConductorColor.WhiteOrange,
            TssConductorColor.Orange,
            TssConductorColor.WhiteGreen,
            TssConductorColor.Blue,
            TssConductorColor.WhiteBlue,
            correct ? TssConductorColor.Green : TssConductorColor.Brown,
            TssConductorColor.WhiteBrown,
            TssConductorColor.Brown
        };

        var terminals = new TssTerminationTerminalState[8];
        for (var i = 0; i < terminals.Length; i++)
        {
            terminals[i] = new TssTerminationTerminalState
            {
                pin = i + 1,
                conductor = colors[i],
                hasConductor = true,
                isPunched = true,
                operationOrder = i + 1
            };
        }

        return new TssTerminationModuleState
        {
            connectionId = connectionId,
            endpointId = endpointId,
            moduleId = moduleId,
            terminals = terminals
        };
    }

    private static TssPortEndpoint Endpoint(string endpointId)
    {
        var endpoint = new GameObject(endpointId).AddComponent<TssPortEndpoint>();
        endpoint.Configure(endpointId, new EquipmentInterface
        {
            name = endpointId,
            label = endpointId,
            connectorType = "RJ45",
            supportedCableTypes = new[] { TssCableType.StraightThrough }
        });
        return endpoint;
    }
}
