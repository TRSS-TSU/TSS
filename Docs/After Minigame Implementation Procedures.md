# After Minigame Implementation Procedures

## Purpose

This document hands off the current Layer 1 termination-minigame implementation state after the first script-side pass. It lists the active manual procedures, model/material work, validation steps, and suggested next development order.

## Current Implementation State

The project now has a script-side foundation for silent T568B punch-down validation and permanent-cable graph gating.

Implemented code support:

- `ScenarioPermanentCableConnection` now supports per-run punch-down requirements and endpoint A/B termination module IDs.
- `TssTerminationModuleState`, `TssTerminationTerminalState`, `TssConductorColor`, and `TssT568BValidator` represent raw rear-module state and validate exact punched T568B order.
- `TssTrainingSession` owns runtime termination snapshots and derived electrical validity.
- Permanent infrastructure links marked `requiresPunchDownTermination` are skipped until both endpoint modules validate.
- Reworking either endpoint incorrectly removes only that permanent graph edge.
- `IsEndpointOnEffectivePhysicalPath(string endpointId)` exposes Layer 1 path state for device indicators and computer availability.
- `MonitorTextureRotator` supports a connectivity override texture for offline computer screens.
- `TssDeviceLinkIndicator` provides a prefab-level endpoint-to-LED material presenter.
- `TssComputerConnectivityPresenter` drives monitor offline/online presentation from the same effective physical path query.
- `TssRuntimeUi` has owner-scoped external gameplay locks for modal rear-workbench control.
- `TssObjectInteractable` and `TssPortSelectionAdapter` respect gameplay input locks while modal UI is open.
- `TssTerminationEntryAdapter` and `TssTerminationGameController` provide blockout first-person Front View / Rear Termination View entry controls.
- `TSS > Termination > Configure Balanced Lab Punch Down` configures the Balanced lab scenario through serialized properties.
- Edit-mode tests were added for the T568B validator and graph-gating/rework behavior.

Important caveat:

- Unity batchmode launched but exited before script compilation or tests ran. Treat compile/test/play-mode validation as still required inside the Unity Editor.

## Required Manual Procedures

### 1. Run Scenario Migration

In Unity:

1. Open `C:\Unity\TSS2`.
2. Open the TSS scene that contains the Balanced lab infrastructure.
3. Run menu item: `TSS > Termination > Configure Balanced Lab Punch Down`.
4. Review the Console output.
5. Confirm all 41 `Balanced_12_Piece_Lab` permanent runs are marked for punch-down termination.
6. Confirm `Infrastructure013` is included as a patch-panel-to-patch-panel backbone run.

Manual inspection targets:

- `Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset`
- `Infrastructure001`
- `Infrastructure013`
- Any Console reports for duplicate connection IDs, blank endpoints, or endpoint IDs resolving zero/multiple times.

### 2. Create and Assign Termination UI Prefabs

Run this Unity menu item first:

1. `TSS > Termination > Create Termination UI Prefabs`

It creates these starter prefabs:

- `Assets/Prefabs/TSSInfrastructureTermination/TssTerminationFirstPersonControls.prefab`
- `Assets/Prefabs/TSSInfrastructureTermination/TssTerminationWorkbench.prefab`
- `Assets/Prefabs/TSSInfrastructureTermination/TssPunchDownAction.prefab`

The first-person controls prefab contains real UGUI buttons:

- `Front View`
- `Rear Termination View`

The workbench prefab contains real UGUI labels/buttons:

- Source endpoint label.
- Permanent-link count label.
- `Punch Down` button.
- `Close` button.

It also contains configurable model roots:

- `Patch Panel Module Root`
- `Wall Port Module Root`
- `Conductor Root`
- `Punch Tool Root`

Use those roots to add or replace rear module models, patch-panel/wall-port models, individual Ethernet wire models, and tool models.

The punch-down action prefab currently contains only a placeholder tool model and `TssPunchDownAction`. Replace the placeholder child object with the final punch-down tool model/animation/audio.

### 3. Add Scene Components

Add one `TssTerminationGameController` to the established session/infrastructure runtime root.

Assign on `TssTerminationGameController`:

- `Workbench Prefab`: `TssTerminationWorkbench.prefab`
- `Punch Down Action Prefab`: `TssPunchDownAction.prefab`
- `Workbench Parent`: fallback only; prefer per-device workbench anchors on each `TssTerminationEntryAdapter`

Add `TssTerminationEntryAdapter` only to supported device roots:

- The two interactive patch-panel roots.
- The ten interactive wall-port roots.

Assign on each `TssTerminationEntryAdapter`:

- `Controller`: the scene `TssTerminationGameController`
- `Controls Prefab`: `TssTerminationFirstPersonControls.prefab`
- `Controls Parent`: optional scene parent for the controls instance
- `Workbench Parent`: the local spawn anchor for that specific wall-port or patch-panel first-person/rear-view position

Do not add the entry adapter to:

- Individual jacks.
- Endpoint child colliders.
- Routers.
- Switches.
- Servers.
- Printers.
- Computers.

Preserve existing Hierarchy names and inspector-authored endpoint IDs/display labels.

### 4. Assign Device Indicators

Add `TssDeviceLinkIndicator` to the equipment prefabs, not scene-only runtime instances:

- Router prefab.
- Switch 1 prefab.
- Switch 2 prefab.
- Server prefab.

For each LED binding:

1. Assign the real `TssPortEndpoint` or endpoint ID.
2. Assign the matching LED mesh renderer.
3. Assign approved connected and disconnected materials.
4. Confirm the mapped endpoint matches the real equipment interface.

Use renderer instance materials through the component. Do not edit shared material assets during runtime tests.

When desktop or printer LED objects exist later, add the same component and endpoint binding pattern there.

### 5. Assign Computer Offline Presentation

On runtime-placed desktop PC visuals:

1. Add `TssComputerConnectivityPresenter`.
2. Assign or let it discover the generated `TssPortEndpoint`.
3. Assign the existing `MonitorTextureRotator`.
4. Assign the approved offline-screen texture.
5. Confirm the computer menu shows `Network unavailable.` when no effective Layer 1 path exists.

Manual asset still needed:

- Final offline computer-screen texture, or approval of the current blockout texture.

### 6. Create or Supply Rear-Termination Assets

Starter prefabs now exist after running `TSS > Termination > Create Termination UI Prefabs`, but the 3D rear-module, conductor, and final tool assets still need to be created or supplied.

The current prefab blockout is functional UI plus empty model roots:

- `TssTerminationFirstPersonControls.prefab` provides the first-person buttons.
- `TssTerminationWorkbench.prefab` provides the rear panel, labels, buttons, and model roots.
- `TssPunchDownAction.prefab` provides a placeholder punch-down action object.

This is enough to configure the scene and begin adding models, but it is not the finished punch-down interaction.

Required assets:

- Rear patch-panel module model.
- Rear wall-port module model.
- Patch-panel-to-patch-panel backbone termination module representation.
- Eight conductor visuals with distinguishable solid/striped variants.
- Punch-down tool model and short action animation.
- Molded pin labels or other non-answer-key pin identification.
- Audio feedback for place/remove/punch actions, if desired.
- Accessible non-color cues for selected, placed, and punched states.

Learning-integrity rule:

- Do not add a visible T568B answer key, correctness label, score, or final-submit button to the workbench.

### 7. License and Provenance Check

For every external model, material, texture, sound, or icon:

1. Verify the license permits redistribution in the TSS project.
2. Record source URL/path, creator, license, import date, and import settings.
3. Prefer custom blockout assets until final approved assets are available.

Suggested provenance file:

- `Assets/TSSInfrastructureTermination/Documentation/Asset-Provenance.md`

## Play Mode Validation Procedure

Run this before calling the feature complete.

### Baseline

1. Open the Balanced lab scenario.
2. Confirm Unity compiles cleanly.
3. Confirm the scenario loads.
4. Confirm the player can enter first-person view on patch panels and wall ports.
5. Confirm Front View and Rear Termination View controls appear only on eligible roots.
6. Confirm existing front patch-cable selection still works when rear workbench is closed.

### Graph Gating

For `Infrastructure001`:

1. Start with both endpoint modules empty or invalid.
2. Confirm no permanent physical edge exists for that run.
3. Set endpoint A valid and endpoint B invalid.
4. Confirm no permanent physical edge exists.
5. Set both endpoints valid.
6. Confirm the permanent physical edge appears.
7. Rework either endpoint incorrectly.
8. Confirm only that permanent edge is removed.
9. Correct both endpoints again.
10. Confirm the edge returns without scene reload.

Repeat the same pair-rule test for `Infrastructure013`.

### Runtime Evidence

For each mapped router/switch/server interface:

1. Disconnect or invalidate the relevant permanent run.
2. Confirm the LED displays the disconnected material.
3. Correct the permanent termination and required front patching.
4. Confirm the LED displays the connected material.
5. Rework a previously valid endpoint incorrectly.
6. Confirm the LED returns to disconnected live.

For each mapped computer:

1. Confirm the offline texture appears with no effective physical path.
2. Confirm the computer menu reports network unavailable.
3. Restore valid terminations and front patching.
4. Confirm the screen override clears.
5. Confirm the computer menu becomes available again.

### Input Locking

While rear workbench is open:

- Player movement is blocked.
- Background port raycast/selection is blocked.
- Escape/Return from the underlying first-person interactable is blocked.
- Closing the workbench restores the prior first-person context.

## Expected Logs

Expected:

- Migration summary from `TSS > Termination > Configure Balanced Lab Punch Down`.
- Endpoint-resolution warnings only if scene setup is incomplete or duplicate/blank IDs exist.

Not expected:

- RouteTilePreloader or RouteTileAudit logs caused by this work.
- T568B correctness logs during learner interaction.
- Repeated StateChanged/log storms during a single punch action.

## Suggested Next Steps

1. Run Unity compile and the new edit-mode tests from inside the Editor.
2. Run the Balanced lab migration command and inspect all 41 records.
3. Attach `TssTerminationGameController` and `TssTerminationEntryAdapter` components in the scene.
4. Build the first real rear workbench vertical slice for `Infrastructure001`.
5. Validate graph gating and rework behavior for `Infrastructure001`.
6. Validate backbone behavior for `Infrastructure013`.
7. Configure router, switch, server LED mappings on prefabs.
8. Configure desktop computer offline screen and menu availability.
9. Expand workbench coverage across all 41 permanent runs.
10. Polish accessibility, animation, audio, and asset provenance.
11. Only after stable Layer 1 evidence, define Layer 2/3 and Cisco IOS diagnostic outcomes.

## Explicit Deferrals

- Final scoring/submission flow.
- Learner-facing error report.
- Cisco IOS CLI behavior.
- Layer 2/3 switching/routing simulation.
- Persistence beyond the active scenario run.
- Non-T568B standards, crossover/fiber/PoE faults, tone tools, LMS reporting, or multiplayer.

## Rollback Note

To disable the feature without removing code:

1. Leave `requiresPunchDownTermination` false on scenario records, or revert it for the affected records.
2. Remove/disable `TssTerminationEntryAdapter` from device roots.
3. Remove/disable `TssDeviceLinkIndicator` and `TssComputerConnectivityPresenter` components from prefabs.

The existing individual front patch-cable path and non-terminated permanent-link behavior remain available when `requiresPunchDownTermination` is false.

## Resume Prompt

Continue the TSS2 Layer 1 termination minigame from `Docs/After Minigame Implementation Procedures.md`: run Unity compile/tests, migrate `Balanced_12_Piece_Lab`, attach the scene/prefab components, and validate `Infrastructure001` plus `Infrastructure013` before expanding to all 41 runs.
