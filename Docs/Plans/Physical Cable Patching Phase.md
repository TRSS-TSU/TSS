# Physical Cable Patching Phase

## Summary

Add a physical connectivity layer for wall ports, patch panels, installed network devices, servers, and endpoint devices.

The phase will:

- Let the trainee select a cable type, carry it, and connect two compatible ports.
- Reuse the existing first-person interaction flow in `TssObjectInteractable`.
- Track connections in the runtime `TssTrainingSession` list.
- Render connected cables as thin cylinder segments.
- Use an authored waypoint graph with deterministic shortest-path selection.
- Leave switch/router configuration and logical connectivity for later phases.

## Implementation Changes

### 1. Cable and port data

Extend `EquipmentInterface` without renaming existing serialized fields:

- Interface ID/name
- Display label
- Connector type
- Supported cable types
- Port anchor path on the equipment prefab

Add:

- `TssCableDefinition` ScriptableObject
- `TssCableType`: StraightThrough, Crossover, Rollover
- Scenario cable inventory entries
- Optional cable carry prefab and cable visual material

The existing equipment definitions will be updated from placeholder `TBD` connectors to authored port metadata.

### 2. Port identity and interaction

Add `TssPortEndpoint` to:

- Wall-port child objects
- Patch-panel port objects
- Printer/server/switch/router port anchors
- Dynamically spawned installed equipment

Each endpoint receives a stable runtime ID such as:

```text
WallPort:Office01:Port04
Rack01:U03:Eth01
Endpoint:ITDesk:IT1:Eth01
PatchPanel:Rack01:Front01
```

The existing `TssObjectInteractable` will remain responsible for camera movement. A small port-selection adapter will:

1. Enter first-person at the device’s authored interface view.
2. Enable raycast selection only for port endpoints.
3. Highlight selectable ports.
4. Preserve the carried cable and selected first endpoint when the trainee returns to third person.
5. Permit the second port to be on the same device or another device.

Connections will require:

- Both ports to be free.
- Matching connector types.
- The selected cable type to be supported by both ports.

Equipment pickup will be blocked while any of its ports are connected.

### 3. Connection tracking

Add a runtime physical connection record containing:

- Connection ID
- Cable definition/type
- Endpoint A ID
- Endpoint B ID
- Runtime visual reference

`TssTrainingSession` remains the authoritative owner of active physical connections and exposes:

```csharp
IReadOnlyList<TssPhysicalConnectionRecord> PhysicalConnections
```

Operations will include:

- Check out cable
- Return cable
- Connect cable
- Disconnect cable
- Query connections for an endpoint
- Clear all connections on scenario reset

The system will use the existing `StateChanged` event so current UI and future evaluators can refresh without owning connection state.

### 4. Cable rendering and routing

Add `TssCableVisual`:

- Creates a parent object for each cable.
- Builds one thin cylinder per route segment.
- Rotates and scales each cylinder between adjacent route points.
- Uses the material associated with the selected cable type.
- Adds small endpoint boots only if the cylinder ends need visual cleanup.

Add authored cable-route nodes in the scene near:

- Rack fronts and rears
- Patch-panel fan-out areas
- Cable trays
- Wall-port corridors
- Endpoint device areas

The route provider will select the shortest valid path through this authored waypoint graph. This gives deterministic routing while still allowing multiple corridors and avoiding cable crossing where the scene author provides alternate paths.

The four supported connection patterns will use the same system:

- Wall port ↔ printer or installed endpoint
- Patch-panel port ↔ patch-panel port
- Patch-panel port ↔ router/switch port
- Server port ↔ switch port

### 5. UI and scenario integration

Extend the existing equipment-case UI to include cable selection and carried-cable state.

Add a compact physical-connection display to the existing runtime/debug UI showing:

```text
CableType — EndpointA ↔ EndpointB
```

Add physical connection requirements to the scenario definition only when task grading is needed. The existing `TssPlacementEvaluator` can then evaluate physical cabling separately from equipment placement.

No save-file database will be added in this phase; runtime records are sufficient and preserve the current session architecture.

## Test Plan

Play Mode validation must cover:

1. Scenario loads with no physical connections.
2. Each cable type can be selected and carried.
3. First-person views work for wall ports, patch panels, printers, servers, switches, and routers.
4. A valid connection can be made on the same device.
5. A valid connection can be completed on a different device after travelling.
6. Invalid cable type, occupied port, and incompatible connector attempts are rejected.
7. All four required connection patterns create visible cable geometry.
8. Cables follow authored waypoint routes without crossing major equipment.
9. Connection records update when cables are created or removed.
10. Connected equipment cannot be picked up until its cable is disconnected.
11. Scenario reset removes cable visuals and clears runtime records.
12. Existing placement, first-person, and equipment workflows remain functional.
13. Unity compiles without new errors.

## Assumptions

- Cable type is selected and physically carried before connection.
- Cable length is not graded initially.
- Physical connection records are runtime-only.
- Logical switch/router configuration is explicitly out of scope.
- General 3D obstacle A* or NavMesh routing is deferred; add it when authored route nodes cannot handle dynamic scene layouts.
