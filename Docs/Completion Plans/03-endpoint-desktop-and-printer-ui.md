# Completion Plan: Endpoint, Desktop, Server, and Printer UI

## Objective

Turn the desktop canvas into an adapter over endpoint state and reachability; remove shortcut and fabricated network behavior.

## Dependencies

`01-network-state-and-cli.md` and `02-deterministic-reachability.md`.

## Implementation

1. Complete `TssEndpointCommandProcessor.Execute(sourceKey, input)` for `ipconfig`, `ping <address>`, `tracert <address>`, and `cls`.
2. Update `Assets/Scripts/TssDesktopSessionPresenter.cs` so DHCP/static controls are local edits until Apply. Validate every field first; on failure change no authoritative field; on success emit exactly one `StateChanged`.
3. Update `Assets/Editor/TssDeviceSessionPrefabBuilder.cs` to replace legacy ping/gateway/ipconfig controls with a command input, submit button, output area, and history controls.
4. Render server state only from derived adapter/layer status. Do not simulate DHCP leases, DNS, HTTP, or hard-coded clients.
5. Permit printer test pages only when printer and source workstation are reachable; retain failed job state with the evaluator's neutral layer clue.
6. Remove peer/path disclosure from the desktop physical-path card.

## Acceptance

- Invalid Apply is atomic; a valid wrong gateway persists and produces Layer 3 failure.
- Reopen preserves endpoint state.
- Endpoint-originated diagnostics, not router pings, record Phase 19 evidence.
- No gateway shortcut, fabricated lease, or unconditional print success remains.
