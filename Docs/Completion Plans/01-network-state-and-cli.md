# Completion Plan: Network State and Typed CLI

## Objective

Make `TssTrainingSession.NetworkState` the sole runtime authority for switch, router, and endpoint configuration. Remove runtime dependence on the static `TssDeviceLogicalState` cache.

## Dependencies

None. Complete this before reachability, endpoint diagnostics, or Work Package grading.

## Implementation

1. Expand `NetworkScenarioConfig` in `Assets/Scripts/TssScenarioDefinition.cs` with Balanced-lab role bindings, VLAN names/ranges, endpoint aliases, required ports, and evidence requirements.
2. Complete `TssNetworkRunProfile` and `TssNetworkSessionState` in `Assets/Scripts/TssNetworkSessionState.cs`: generated VLANs/subnets/WAN/credentials, FNV-1a MACs, endpoint state, device state, deep startup snapshots, and checkpoint snapshots.
3. In `Assets/Scripts/TssTrainingSession.cs`, reset profile/state/evidence/checkpoints on `BeginScenario`; clear device and endpoint state on successful pickup; preserve state across canvas close/reopen and rename.
4. In `Assets/Scripts/TssDeviceSessionController.cs`, build presenter adapters from network state. Do not call `TssDeviceLogicalState.GetOrCreate` at runtime.
5. Complete `Assets/Scripts/TssCliCommandProcessor.cs` for every required mode, command, alias, range, `do`, help, show command, save operation, and neutral validation result.

## Acceptance

- Same seed reproduces profile values and MACs; different seeds remain valid and unique.
- Invalid IP/mask/VLAN/range input does not change running or startup state.
- Valid wrong configuration persists and renders in show output.
- Startup diverges before save and equals a deep running snapshot after save.
- Router aliases map to GE00/GE01 without renaming protected endpoints.
