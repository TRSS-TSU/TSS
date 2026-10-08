# Layer 2 / Layer 3 Device Session Completion Plan

## Goal

Finish the Layer 2 and Layer 3 training loop without changing the Layer 1 placement, cabling, termination, or effective-physical-path authorities.

## Scoped order

1. Complete session-owned profile, endpoint, device, and startup-snapshot state in `Assets/Scripts/TssNetworkSessionState.cs` and `Assets/Scripts/TssTrainingSession.cs`.
2. Complete typed switch/router configuration in `Assets/Scripts/TssCliCommandProcessor.cs`; presenters remain adapters only.
3. Complete `TssNetworkReachabilityEvaluator` before endpoint UI, monitors, or grading. It must check Layer 1 physical/admin state, Layer 2 VLAN/trunk continuity, then Layer 3 addressing/router/WAN/default-route state and return the first failed layer.
4. Route desktop `ipconfig`, `ping`, `tracert`, and static Apply through `TssEndpointCommandProcessor`; retain local edits until a complete valid Apply.
5. Complete `TssNetworkWorkPackageEvaluator` for typed evidence, running/startup persistence, Phase 15 snapshot behavior, and WP2/WP3 conditions.
6. Regenerate session canvases only after the terminal/desktop presenters and `TssDeviceSessionPrefabBuilder` are final. Assign all monitor textures and verify `DeviceSessionManager` bindings.
7. Run EditMode, compile, fresh-scenario Play Mode, Layer 1/2/3 fault, iPad layout, and scoped-Git validation before documenting actual results and committing.

## Dependencies

- CLI and endpoint commands depend on profile/state.
- Reachability depends on configured state and existing physical-path data.
- Endpoint commands, monitor state, printer status, and evaluator evidence depend on reachability.
- Prefabs depend on final presenter fields; do not hand-edit generated session prefabs.
- Hotwash and commit depend on recorded automated and Play Mode validation.

## Required proof

- Same seed reproduces generated network values; new scenario clears them.
- Malformed commands/settings leave state unchanged; syntactically valid wrong values persist.
- Every deliberate physical, VLAN/trunk, gateway/subinterface, WAN, and default-route fault reports the first broken layer without an answer reveal.
- Router-originated ping does not satisfy endpoint evidence.
- LEDs reflect Layer 1/admin state only; monitors reflect monotonic Layer 1 → Layer 2 → Layer 3 status.
- Only intended Layer 2/3 files are staged and pass `git diff --cached --check`.
