# Layer-1 Signal Path vs. Cable-Schedule Compliance

**Status:** Recommendation; no runtime or scene changes made.

## Purpose

Make the simulator distinguish an electrically usable physical path from the
scenario-prescribed cable schedule. A connection on the wrong switch or router
interface must be able to establish Layer-1 carrier when the complete path is
continuous, correctly terminated, and electrically compatible. It must still
be reported as a cable-schedule and work-package failure.

## Observed behavior

`TssTrainingSession.GetEffectivePhysicalPaths()` currently builds its topology
only from connections accepted by `IsCableTypeCorrect(...)`. That helper does
not mean electrically correct: it compares each player cable to
`ScenarioDefinition.cableTypeRequirements`, which describes a required endpoint
pair and cable type for assessment.

Consequently, a cable path can be physically continuous and electrically
workable but excluded from the effective-path cache solely because it uses a
different port than the schedule. The resulting false negative also propagates
to interface `CarrierUp`, Layer-1 UI, device indicators, and the Effective
Device Paths panel.

The CAPSTONE inter-closet example exposes the distinction:

```text
Required schedule:
AN-1 Fa0/1 -- crossover -- Patch Panel 1 / Port 48
Patch Panel 1 / Port 48 -- permanent terminated run -- Patch Panel 2 / Port 48
Patch Panel 2 / Port 48 -- straight-through -- AN-2 Fa0/1
```

The Patch Panel 1/48 ↔ Patch Panel 2/48 structural segment is
`Infrastructure013`. It is correctly absent until both T568B terminations pass.
Once it is present, using Fa0/7 rather than Fa0/1 should make the schedule fail,
but should not by itself erase a valid physical carrier path.

## Required semantic split

| Authority | Answers | Must not depend on |
| --- | --- | --- |
| Physical signal path | Can Ethernet signaling traverse these two active interfaces through connected and terminated infrastructure? | The scenario-required endpoint pair |
| Cable-schedule compliance | Did the student use the assigned endpoints and specified patch-cord types? | Whether carrier is physically present |
| Layer 2/3 | Is the administratively enabled interface configured to switch or route correctly? | Schedule compliance |

`IsCableTypeCorrect(...)` should remain the schedule-compliance authority. Its
existing status display and assessment callers should continue to use it.

The effective-path and carrier authority should instead use an internal
signal-path predicate that:

1. Traverses only connected player cables and active permanent infrastructure.
2. Includes a permanent punch-down run only after both ends have valid
   terminations.
3. Rejects rollover cables for Ethernet signaling.
4. Accumulates crossover parity over the complete path between the two active
   Ethernet interfaces — counting every crossover segment, player cords and
   permanent runs alike — rather than judging each patch cord against a
   schedule row.
5. Applies the MDI/MDI-X device rule to the accumulated parity, classifying
   switches as MDI-X and routers, hosts (desktop PCs), and access points as
   MDI. A valid Ethernet path requires **even** crossover parity when exactly
   one endpoint is a switch (unlike faces, e.g. switch-to-router or
   switch-to-PC) and **odd** parity otherwise (like faces, e.g.
   switch-to-switch or router-to-router).

This matches the passive nature of patch panels. For example, a crossover cord
on one side of a straight-through permanent run and straight-through cord on
the other side produces a crossover end-to-end switch-to-switch connection.

The device classification in rule 5 is not optional scope. This predicate
replaces `IsCableTypeCorrect(...)` as the filter for the **entire**
effective-path cache, not only the inter-closet switch link. Desktop runs
(PC → wallport → permanent run → patch panel → switch) are built by the same
code and pass today. If the rule omits the host and access-point categories,
those currently-correct paths lose carrier — a regression against the
acceptance criterion that existing correct-schedule paths remain unchanged.
Endpoint device category is available from `networkScenario.roleBindings`
(role → category → device key) resolved through the endpoint owner prefix.

## Implementation scope

### `Assets/Scripts/TssTrainingSession.cs`

- Preserve `IsCableTypeCorrect(...)` as schedule validation.
- Build effective physical paths from signal-valid connections instead of
  schedule-valid connections.
- Add a parity-aware path traversal. The current `HasPhysicalPath(...)` BFS is
  boolean and carries only `(endpoint, usedPermanent)` state; it does not
  accumulate cable type and has no rollover handling. The signal-path predicate
  needs to accumulate crossover parity along the traversed path, reject
  `Rollover` edges, and resolve which path's parity applies when more than one
  path connects two leaves (extend the search key with parity, or return the
  path). This is new traversal logic, not a change to the existing filter call.
- Look up the device category at each leaf endpoint to apply the MDI/MDI-X rule
  (rule 5 above).
- Keep termination gating in `ApplyPermanentConnections()` and
  `ReconcilePermanentConnection()`; no scenario format change is needed.
- Set generic interface carrier from the signal-path result.
- Retain `IsProviderDemarcConnected(...)` as the WAN/service authority, rather
  than using it to suppress physical carrier on router `Fa0/1`. Note this is a
  deliberate scope boundary: `IsProviderDemarcConnected(...)` continues to
  filter through `IsCableTypeCorrect(...)`, so the wrong-port-but-electrically-
  valid tolerance introduced here does **not** extend to the router `Fa0/1`
  demarc path. Service availability remains schedule-coupled by design.

### `Assets/Scripts/TssDeviceSessionController.cs`

- Remove the duplicate router-`Fa0/1` Demarc-only carrier assignment so this
  controller uses the same physical-carrier authority as `TssTrainingSession`.

### `Assets/Scripts/TssPhysicalConnectionPanel.cs`

- Keep the existing `[Correct]` label only if it is clarified as schedule
  compliance (for example, `[Schedule match]`). It must not imply that every
  non-matching cable lacks physical signal.

### Tests

Extend `Assets/Tests/EditMode/TssPlacementEvaluatorTests.cs` with focused
coverage:

1. A terminated, electrically valid path on the wrong scheduled interfaces
   reports effective carrier while cable-schedule status fails.
2. A path with wrong end-to-end crossover parity reports no carrier.
3. Either failed Port 48 termination prevents carrier.
4. A physically valid but administratively shut interface retains the physical
   path while Layer 2/3 remains unavailable.
5. A desktop host-to-switch run (unlike faces, even parity, e.g. a
   straight-through cord into a straight-through permanent run) continues to
   report carrier. This guards against the host and access-point categories
   being dropped from the MDI/MDI-X rule and regressing paths that pass today.

## Behavioral boundary

Carrier and administrative state remain distinct. A correct physical path may
set `CarrierUp`; Layer-2/3 availability must continue to require `AdminUp` and
valid switching/routing configuration. This preserves the existing training
feedback for shutdown ports, VLAN/trunk errors, and WAN configuration.

## Acceptance criteria

- A wrong scheduled interface can show an effective physical path only when
  both active interfaces are connected through an active, terminated
  infrastructure path and its cable parity is valid.
- The same connection remains visibly non-compliant with the scenario schedule
  and fails the appropriate work-package assessment.
- Incorrect punch-down termination blocks the structural segment and carrier.
- Rollover and invalid parity block Ethernet carrier.
- Demarc service availability remains dependent on the Demarc path and is not
  granted by an arbitrary router connection.
- Existing correct-schedule paths, AP uplink behavior, and individual cable
  compliance reporting remain unchanged.

## Rollback

Behavioral rollback is clean because no serialized data, scenario JSON, or
scene binding changes are involved: restoring the schedule-bound filter in
`TssTrainingSession` and the current controller carrier assignment returns the
simulator to existing behavior. Note, however, that this reverts the new
parity-aware traversal and leaf-category lookup, not merely a one-line filter
swap — the added code is larger than a filter substitution even though its
data footprint is zero.
