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
        Assert.AreEqual(1, session.GetRemaining(cable));

        Object.DestroyImmediate(endpointA.gameObject);
        Object.DestroyImmediate(endpointB.gameObject);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void PermanentScenarioConnectionsDoNotConsumeInventoryOrDisconnect()
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
                cableType = TssCableType.StraightThrough,
                patchPanelEndpointId = "Rack:Rack01:U02:Port01",
                wallportEndpointId = "WallPort:Office01:Port01"
            }
        };

        var endpointA = EndpointObject("A").AddComponent<TssPortEndpoint>();
        var endpointB = EndpointObject("B").AddComponent<TssPortEndpoint>();
        endpointA.Configure("Rack:Rack01:U02:Port01", Port("Port01"));
        endpointB.Configure("WallPort:Office01:Port01", Port("Port01"));

        session.BeginScenario(scenario);

        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.IsTrue(session.PhysicalConnections[0].IsPermanent);
        Assert.IsFalse(session.PhysicalConnections[0].Visual);
        Assert.AreEqual(1, session.GetRemaining(cable));
        Assert.IsFalse(session.TryUsePort(endpointA, out _));
        Assert.AreEqual(1, session.PhysicalConnections.Count);
        Assert.AreEqual(0, Object.FindObjectsByType<TssCableVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

        Object.DestroyImmediate(endpointA.gameObject);
        Object.DestroyImmediate(endpointB.gameObject);
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
