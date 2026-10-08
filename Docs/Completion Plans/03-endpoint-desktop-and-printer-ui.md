# Completion Plan: Endpoint, Desktop, Server, and Printer UI

## Outcome

Turn the desktop session canvas into a thin adapter over session-owned endpoint state and the shared reachability evaluator. Remove legacy shortcut controls and fabricated service behavior so students must demonstrate the same workflow the evaluator grades.

## References and Current Starting Point

- [Master implementation plan — desktop session canvas](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#72-desktop-session-canvas)
- [Master implementation plan — endpoint commands and monitor health](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#63-endpoint-commands-and-monitor-health)
- [Deterministic reachability prerequisite](02-deterministic-reachability.md)
- `Assets/Scripts/TssDesktopSessionPresenter.cs` — already has local DHCP/static fields and a partial command-processor path; finish its atomic apply, service, printer, and error-state behavior.
- `Assets/Scripts/TssEndpointCommandProcessor.cs` — partial implementation for `cls`, `ipconfig`, `ping`, `tracert`, and candidate application.
- `Assets/Scripts/TssDeviceSessionController.cs` — opens device sessions and must remain an adapter, not a new store of endpoint state.
- `Assets/Editor/TssDeviceSessionPrefabBuilder.cs` — source of truth for regenerated terminal/desktop canvases and first-person-control prefabs.

The prior pass removed several legacy quick-command bindings but did not yet prove all controls, persisted endpoint state, printer origin, or desktop layout. Do not hand-edit generated session prefabs before updating their builder.

## Dependencies and Non-Goals

Complete [Plan 01](01-network-state-and-cli.md) and [Plan 02](02-deterministic-reachability.md) first. This plan does not add DHCP/DNS/HTTP simulation, an operating-system emulator, or another network evaluator.

## Implementation Steps

### 0. Update Graphify graphs

Use the graphify skill. Evaluate the project and update the graph of files. The start this implementation guidance.

### 1. Complete endpoint command processing

Keep `TssEndpointCommandProcessor.Execute(sourceKey, input)` as the only parser for the desktop prompt:

| Command             | Required behavior                                                                                    |
| ------------------- | ---------------------------------------------------------------------------------------------------- |
| `ipconfig`          | Render only current endpoint adapter state.                                                          |
| `ping <address>`    | Call the shared evaluator; append endpoint-originated evidence only for the required profile target. |
| `tracert <address>` | Render reached hops followed by the same learner-safe first-failure result.                          |
| `cls`               | Clear desktop output/history display only; do not alter session state or evidence.                   |

Normalize whitespace and reject unsupported/malformed commands without changing state. Never route a desktop command through a router device key merely because the router is configured.

### 2. Make Apply atomic and session-owned

In `TssDesktopSessionPresenter.cs`, load the authoritative endpoint configuration when a desktop opens, but keep text/toggle changes as local candidate state. On Apply:

1. Parse the complete candidate: DHCP/static selection, address, mask, and gateway.
2. Validate every supplied field before touching `TssNetworkSessionState`.
3. If any field is invalid, keep the authoritative state and evidence untouched and display a local validation message.
4. If the candidate is valid, commit exactly once, call the session's network-state notification once, and refresh from state.

A syntactically valid but wrong gateway is a valid Apply. Preserve it so Plan 02 can correctly report a Layer 3 failure. Close/reopen must reload that committed result, not reset it from UI defaults.

### 3. Replace shortcut UI through the prefab builder

Update `TssDeviceSessionPrefabBuilder.cs` before regeneration:

- Remove the ping-gateway, gateway, and ipconfig quick-action path.
- Build one command input, Submit/Enter button, scrollable output, and Up/Down command history for desktop PCs.
- Retain touch-friendly submission, focus restoration, close/Escape behavior, and gameplay input lock.
- Preserve serialized presenter references by assigning them in the builder; do not locate important controls by object name at runtime.

Regenerate only the desktop/terminal session canvases and their associated first-person-control prefabs after the builder is stable. Then inspect `DeviceSessionManager` references, inactive defaults, EventSystem, sorting, and locks.

### 4. Keep server and printer display state derived

For server sessions, display adapter configuration and layered online/offline state derived from the endpoint/session/evaluator state. Remove generated DHCP leases, DNS/HTTP claims, and hard-coded client lists unless a profile field actually owns that fact.

For printer sessions, store a small job result in session state. A test page succeeds only when:

1. The printer's local endpoint is operational.
2. The initiating workstation is the recorded source key.
3. The evaluator succeeds from that workstation to the printer address.

On failure, retain a failed job with the neutral layer clue. Do not call `ping` on the printer's own address, and do not report unconditional success.

### 5. Remove answer-revealing information

Remove detailed peer/effective-path disclosure from the desktop card. The terminal command reference is syntax-only; desktop output should report entered values and the allowed neutral failure category, never profile answers or a repair instruction.

## Tests

Add/extend `Assets/Tests/EditMode/TssDeviceSessionTests.cs` to prove:

- invalid address, mask, or gateway leaves all authoritative fields and evidence unchanged;
- valid-but-wrong gateway commits once and causes a Layer 3 result;
- close/reopen reads committed endpoint state;
- endpoint ping records required evidence only with the endpoint source;
- unsupported command leaves state unchanged;
- printer success/failure depends on initiating workstation reachability.

## Manual Play Mode Proof

Open a desktop PC in a fresh run; enter an invalid static configuration and confirm no state changes. Apply a valid configuration, reopen the session, and run `ipconfig`, local gateway `ping`, cross-VLAN `ping`, `tracert`, and `cls`. Introduce one routing error and confirm the result is a neutral Layer 3 clue.

Then run a printer test first with reachability unavailable and then after repair. Verify server/printer displays do not fabricate leases, clients, or successful jobs. Test canvas layout at 1280×800 and one 4:3 iPad resolution.

## Exit Criteria

- No shortcut buttons or unconditional network outcomes remain.
- Every desktop command and print job is traceable to the active endpoint and shared evaluator.
- Apply is all-or-nothing, committed state survives reopen, and UI does not own networking state.
- Generated prefab bindings work without null-reference errors or broken input locks.

## Rollback

Keep the builder change and presenter wiring as one scoped unit. If a regenerated prefab is missing a serialized reference, restore the prior generated prefab, fix the builder assignment, regenerate again, and leave unrelated prefabs untouched.
