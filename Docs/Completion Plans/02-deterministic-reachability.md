# Completion Plan: Deterministic L1/L2/L3 Reachability

## Outcome

Make `TssNetworkReachabilityEvaluator` the one read-only authority for endpoint `ping`/`tracert`, workstation diagnostic displays, printer eligibility, and grading checks. It must return the _first_ broken layer without revealing a corrective value.

This is deliberately a deterministic topology check, not a packet, ARP, STP, or routing-protocol simulator.

## References and Current Starting Point

- [Master implementation plan — reachability and evaluation](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#6-deterministic-reachability-and-work-package-evaluation)
- [Master implementation plan — acceptance walkthrough](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#102-play-mode-acceptance-walkthrough)
- [Network state and CLI prerequisite](01-network-state-and-cli.md)
- `Assets/Scripts/TssNetworkReachabilityEvaluator.cs` — partial evaluator already exists; it handles basic endpoint/profile lookup and some route checks, but does not yet traverse the complete physical, VLAN, and router model.
- `Assets/Scripts/TssNetworkSessionState.cs` — session-owned endpoint, interface, route, and profile state.
- `Assets/Scripts/TssTrainingSession.cs` — owns the active profile/session state and reset lifetime.
- `Assets/Scripts/TssPortEndpoint.cs`, `Assets/Scripts/TssPhysicalConnectionPanel.cs` — existing Layer 1 endpoint/effective-path inputs; consume them, do not recreate their graph.

The previous implementation pass intentionally stopped short of a full evaluator: UI adapters can request a result, but a valid result cannot yet prove every required Layer 2 or Layer 3 condition. Complete this plan before treating any ping, monitor, or work-package result as authoritative.

## Dependency Boundary

Ensure [Plan 01](01-network-state-and-cli.md) is completed first. The evaluator reads state; it must not parse CLI text, mutate state, generate profile values, or own UI formatting. Plans 03–05 consume this result and therefore remain downstream.

## Implementation Steps

### 1. Define the result contract before adding rules

In `TssNetworkReachabilityEvaluator.cs`, keep one `Evaluate(string sourceKey, string destinationAddress)` entry point and return a `TssReachabilityResult` containing:

- `Success`.
- `FailedLayer` (`0` on success, otherwise `1`, `2`, or `3`).
- A neutral fault category such as `PhysicalPathUnavailable`, `VlanNotCarried`, or `GatewayUnavailable`.
- Resolved source/destination role keys and only the hops actually reached.

Do not return expected VLANs, addresses, passwords, command strings, or a suggested repair. Keep display wording outside the evaluator, mapping categories to short learner-safe messages in the presenters/processors.

### 2. Implement Layer 1 as a complete precondition

Resolve the source and declared destination by stable profile role/address, never by a display name. Then check, in this order:

1. Source endpoint exists and maps to a declared data interface.
2. The existing effective physical-path service reports a complete source-to-access-switch path.
3. Each participating interface has carrier and is administratively up.
4. Router subinterfaces inherit the parent interface's Layer 1 state.

Return immediately for the first Layer 1 failure. Do not let a missing cable fall through to a VLAN or routing error.

### 3. Implement Layer 2 continuity only after Layer 1 passes

Use the structured switch/interface state populated by the CLI processor. For the source VLAN path, verify:

1. The access VLAN exists and is active.
2. The source and destination access ports are in their required access VLANs.
3. Each required inter-switch/router connection is trunking at both ends.
4. The VLAN appears in each required allowed list.
5. Native VLANs agree across every traversed trunk.
6. Ports placed in the suspended Dead VLAN do not forward.

Model only paths required by the scenario profile; do not add a general graph/routing engine. The first VLAN continuity failure returns Layer 2 even if Layer 3 also has mistakes.

### 4. Implement Layer 3 after switching is known-good

For local-gateway, inter-VLAN, and provider destinations, validate the minimum path required by the profile:

1. Source and destination addresses/masks are syntactically valid, non-conflicting, and belong to their declared networks.
2. A routed source has a gateway in its subnet that matches the appropriate router subinterface address/mask.
3. The router parent interface is operational and the subinterface dot1q VLAN matches the Layer 2 path.
4. Inter-VLAN targets have a destination-side return path.
5. Provider targets require operational Fa0/1, valid WAN `/30` addressing, and a default route to the provider next hop.

An otherwise valid address not declared as a profile endpoint or provider target should time out; it must not become simulated internet reachability.

### 5. Make trace and health consumers use the exact same facts

Add small read-only helpers only when they eliminate duplicated conditions:

- Endpoint `tracert` receives the successful/reached hop sequence plus the same first-failure category.
- Workstation health derives L1 from physical/admin state, L2 from access-VLAN-to-router continuity, and L3 from gateway reachability.
- Enforce monotonic health: failed L1 forces L2/L3 false; failed L2 forces L3 false.

Do not add a second evaluator in `TssEndpointCommandProcessor`, `TssComputerConnectivityPresenter`, or `TssNetworkWorkPackageEvaluator`.

### 6. Add focused edit-mode coverage

Extend `Assets/Tests/EditMode/TssDeviceSessionTests.cs` (or add one focused evaluator test file only if it keeps the existing tests clearer). Cover the correct route and one isolated defect for each required category:

- missing physical path and administratively shut interface → Layer 1;
- missing/suspended VLAN, wrong access VLAN, allowed-list omission, native mismatch → Layer 2;
- endpoint gateway/mask, dot1q, parent interface, WAN, and default-route defects → Layer 3;
- a multi-fault state returns the lowest-numbered broken layer;
- unknown but valid destination times out.

Use structured state builders already present in the test suite. Do not instantiate a scene or match formatted UI output for evaluator tests.

## Manual Play Mode Proof

Start a fresh Balanced lab run and capture its generated seed. Configure the correct WP2/WP3 path, then run endpoint-originated gateway, cross-VLAN, cross-switch, and provider checks. For each listed fault category, introduce one valid-but-wrong configuration value, verify the first failure is reported, repair it without restarting, and repeat the endpoint test.

Expected behavior: link/physical failures appear as Layer 1; switching continuity failures as Layer 2; gateway/router/WAN/default-route faults as Layer 3. No message supplies the missing address, VLAN, or command.

## Exit Criteria

- One evaluator is used by commands, displays, printing, and grading inputs.
- Every planned fault category is covered by an isolated edit-mode test and a recorded Play Mode spot-check.
- Successful and failed traces show only reached hops and neutral first-failure output.
- Console remains free of new null-reference, refresh-loop, and repeated-warning faults.

## Rollback

The evaluator is additive and read-only. If it blocks an existing UI flow, retain the structured state changes from Plan 01, revert only the evaluator/consumer wiring, and do not restore fabricated reachability shortcuts.
