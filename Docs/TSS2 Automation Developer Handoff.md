# TSS2 Automation Developer Handoff

Date: 2026-10-28

## Purpose

The TSS Developer Automation Panel is an instructor/developer shortcut for running the physical-installation portion of a scenario deterministically. It must read the active scenario requirements and perform the same ordered work a student would perform:

```text
Place expected equipment
→ connect authored player cables with the required cable types
→ terminate every required patch-panel and wallport end
→ refresh the runtime state once the batch is complete
```

The automation must remain idempotent. Running an action again must skip completed work rather than create duplicate equipment, cables, or termination records.

## Current UI actions

The runtime developer panel exposes:

- `PLACE ALL EQUIPMENT`
- `CONNECT ALL CABLES`
- `TERMINATE ALL`
- `RUN ALL`

`RUN ALL` must execute the stages in that order. The individual buttons are useful for diagnosis, but the expected normal developer flow is `RUN ALL` or the three individual actions in sequence.

## Equipment placement requirements

The automation reads `scenario.expectedRackPlacements` and `scenario.expectedEndpointPlacements`.

For rack equipment:

- Place the required category and assigned name in the authored rack and U range.
- Choose the first available U in the inclusive range.
- Preserve the normal checkout/install path so rack ownership, endpoint bindings, name plaques, network state, and session records are created normally.
- Do not overwrite an existing correct placement.

For endpoint equipment:

- Place the required category and assigned name at the authored station.
- Preserve the normal placement visuals, endpoint bindings, interaction triggers, and session records.
- Do not create a second placement when the expected placement already exists.

### Capstone placement expectation

Capstone has six rack placements and eight endpoint placements. CN-1 is authored for Rack01 U30–U34. In the current scene U30 and U31 are blocked, so the expected result is:

```text
CN-1 Router → Rack01 U32
```

The automation must resolve later rack endpoint IDs from the actual installed U rather than assuming the authored U.

## Capstone cable requirements

The authoritative player-cable list is `Assets/StreamingAssets/Scenarios/capstone.json`, under `cableTypeRequirements`. It currently contains ten player cable requirements:

| Requirement | Connection | Type |
|---|---|---|
| InterClosetRack01 | AN-1 Fa0/1 → Rack01 PatchPanel Port48 | Crossover |
| InterClosetRack02 | AN-2 Fa0/1 → Rack02 PatchPanel Port48 | Straight-through |
| DemarcWan | CN-1 Fa0/1 → Rack01 PatchPanel Port18 | Straight-through |
| IT1ToAN1 | IT1 Ethernet → AN-1 Fa0/2 | Straight-through |
| IT2ToAN1 | IT2 Ethernet → AN-1 Fa0/5 | Straight-through |
| Reception1ToWallport01 | Reception1 Ethernet → Reception WallPort 01 | Straight-through |
| Reception2ToWallport02 | Reception2 Ethernet → Reception WallPort 02 | Straight-through |
| Data1ToWallport01 | Data1 Ethernet → Floor02 Office01 WallPort 01 | Straight-through |
| Data2ToWallport01 | Data2 Ethernet → Floor02 Office02 WallPort 01 | Straight-through |
| Data3ToWallport02 | Data3 Ethernet → Floor02 Office02 WallPort 02 | Straight-through |

The endpoint-to-wallport cables are intentional. Their permanent infrastructure links connect the wallports to the scenario-selected patch-panel ports after both ends are correctly terminated. The automation must not substitute a direct switch-to-desktop cable for an authored wallport path.

Runtime endpoint IDs are generated after placement. Authored aliases such as `IT1`, `Reception1`, and `Data1` must resolve to the actual placed equipment Ethernet endpoint. Authored rack IDs such as `Rack:Rack01:U35:Fa02` must resolve to the actual installed U when placement moved within its allowed range.

The expected Capstone player cable count is ten. With all 42 permanent infrastructure links present, the expected total physical connection count is 52.

## Termination requirements

The automation reads `scenario.permanentConnections`.

For every permanent connection with `requiresPunchDownTermination`:

- Generate valid T568B state for the patch-panel endpoint.
- Generate valid T568B state for the wallport endpoint.
- Submit both states through `TssTrainingSession.SetTerminationModuleState`.
- Skip a termination that is already correct.
- Batch notifications and perform one final state refresh.

Permanent infrastructure connections are not player cables and must not require a separate cable checkout. Once the required terminations are correct, the session can materialize the permanent physical connection.

## Observed Play Mode behavior

`PLACE ALL EQUIPMENT` works after a slight delay. The delay is expected because it creates the normal equipment visuals, bindings, and state records.

`CONNECT ALL CABLES` has been observed to stop or effectively freeze the Unity main thread after the button is clicked. The editor required closing and reopening during diagnosis. This is not an acceptable final behavior.

## Required connection fix

The connection path must be batched in the same way as termination:

1. Checkout the required cable.
2. Select the two required endpoints without triggering a full UI/network refresh for each port.
3. Create the cable record and visual.
4. Continue through all ten requirements.
5. Perform one `NotifyNetworkStateChanged()` at the end.

The current worktree contains the intended batching change:

- `TssTrainingSession.TryUsePort` has an optional `notifyStateChanged` parameter.
- `TssScenarioAutomation.ConnectAllCables` passes `false` for both endpoint selections.
- `ConnectAllCables` performs one final network/UI refresh.

This change must be verified in a fresh Play Mode run after the editor restart. If the main thread still stalls, profile the next likely synchronous cost: `TssCableVisual.Create` and `TssCableRouteProvider.GetRoute` for endpoint-to-wallport routes. Do not remove cable visuals or route correctness as a workaround; make route/visual creation incremental or batch-safe while preserving the recorded connection and authored route behavior.

## Validation target

The dedicated Capstone Play Mode test should verify:

- 6 rack placements and 8 endpoint placements.
- CN-1 at Rack01 U32 in the current Capstone scene.
- 10 player cables connected.
- Every generated cable reports `IsConnected` and `IsCorrect`.
- All required terminations are valid.
- 42 permanent connections exist.
- 52 total physical connections exist.
- Re-running the automation adds zero duplicate equipment, cables, or terminations.
- The Unity console contains no automation errors.

The existing broad test suite has unrelated historical failures. Use the dedicated Capstone automation test and an isolated Play Mode walkthrough as the primary verification for this work.

## Files in scope

- `Assets/Scripts/TssScenarioAutomation.cs`
- `Assets/Scripts/TssTrainingSession.cs`
- `Assets/Scripts/TssExternalScenarioValidator.cs`
- `Assets/Scripts/TssRuntimeUi.cs`
- `Assets/StreamingAssets/Scenarios/capstone.json`
- `Assets/Tests/PlayMode/TssScenarioAutomationPlayModeTests.cs`

Preserve unrelated scene and worktree changes. Do not replace the normal placement, cable, termination, network, or display systems with a second parallel implementation.
