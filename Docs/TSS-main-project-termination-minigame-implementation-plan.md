# TSS Main-Project Infrastructure Termination Minigame — Implementation Plan

## Decision and scope

Implement the punch-down activity directly inside the real TSS Unity project at `C:\Users\micha\Documents\Unity\TSS`. There is no standalone minigame project, no custom Unity package, and no package-import or cross-project assembly boundary. This is a planning-only deliverable; implementation begins later.

The MVP adds learner-performed **TIA/EIA-568B straight-through** punch-down termination for every authored permanent infrastructure run in the active TSS scenario. It covers both patch-panel-to-wall-port horizontal runs and patch-panel-to-patch-panel backbone runs. The learner can enter rear termination views or rework front patch cables at any time from a supported first-person device view.

The electrical rule is deliberately different from later grading:

- The game silently validates every rear module as the learner works.
- A permanent cable run is present in runtime connectivity only when both of its named endpoints are fully punched and electrically valid T568B.
- An incorrect conductor, wrong terminal position, missing conductor, or unpunched terminal leaves—or returns—the run to a disconnected state.
- The minigame does not disclose correctness, show a 568B answer key, or offer a final-submit button.
- Raw incorrect state is preserved for the later comprehensive scenario evaluator, which will report the incorrect terminal position and wrongly placed wire color without exposing the ordered answer.

## Confirmed main-project foundation

The implementation extends confirmed TSS systems rather than creating parallel topology or session systems:

| Existing TSS boundary | Planned use |
| --- | --- |
| `TssScenarioDefinition.permanentConnections` | Authored source of every permanent run and its stable `connectionId`. |
| `ScenarioPermanentCableConnection.patchPanelEndpointId` / `wallportEndpointId` | Existing endpoint fields. Treat them generically as endpoint A/B because the backbone stores a patch-panel endpoint in the latter field. |
| `TssTrainingSession` | Authoritative owner of runtime termination snapshots and `TssPhysicalConnectionRecord`s. |
| `ApplyPermanentConnections()` and `GetEffectivePhysicalPaths()` | Existing permanent-edge creation and runtime device-path calculation to gate; do not create a second graph. |
| `TssPortEndpoint.EndpointId` | Stable endpoint identity used for scenario lookup and module restoration. |
| `TssObjectInteractable` | Existing first-person device entry and return events. |
| `TssPortSelectionAdapter` | Existing front-port selection; it remains available independently of termination progress. |
| `TssRuntimeUi` | Existing cursor/player-control modal lock to reuse while the rear workbench is open. |
| `RackMountController` / `TssPortEndpointBinder` | Dynamically instantiates rack equipment and configures its interface endpoints. Network-device LED behavior must therefore live on the prefab/runtime instance, not be assigned only to a static scene object. |
| `RackRouter_1U_RearFourLED_19in` prefab | Contains four individually addressable rear LED `MeshRenderer`s with on/off status materials; it has no `Light` components. |
| `MonitorTextureRotator` / `TssRuntimeUi` | Existing computer-screen material-property-block rotation and computer menu. Connectivity presentation must extend these paths rather than write a renderer once and let the rotator overwrite it. |
| Network-device indicators confirmed by the project owner | Router, both switches, and server interface LEDs are display targets. Future desktop and printer LED objects use the same presentation contract after they are added to their prefabs. |

`Balanced_12_Piece_Lab` currently contains 41 permanent records: 40 patch-panel-to-wall-port runs and `Infrastructure013`, a patch-panel-to-patch-panel backbone. All 41 are in scope.

### Live-scene review findings that affect implementation

- The open `TSS` scene has two interactive patch panels (48 endpoints each) and ten interactive wall-port roots (four endpoints each). Each already has both `TssObjectInteractable` and `TssPortSelectionAdapter`; entry adapters can be limited to these 12 roots.
- The scene has 13 `MonitorTextureRotator` screens and 12 `EndpointPlacementStation`s. Placed computers and installed rack equipment are runtime-created, so their connectivity presentation cannot rely on scene-instance references alone.
- The live scene does not contain a ready-made generic network LED component or `Light`-based port indicator. The reusable router prefab does contain `LEDs/Router_Back_LED_01_switch_style` through `04` as mesh renderers with status materials.
- The current `MonitorTextureRotator` rewrites `_BaseMap`, `_MainTex`, and `_EmissionMap` every rotation interval. Any disconnected-screen state must be an explicit override in that component, not a competing material-property-block writer.
- `TssObjectInteractable`'s first-person `Update()` currently handles its return key without consulting `TssRuntimeUi.IsGameplayInputBlocked`. The modal guard remains required. All 12 eligible scene objects have `canvasPrefab` unset and therefore use the temporary `OnGUI` Return fallback; the termination controls need one shared overlay instantiated by the entry adapter, not an assumed existing per-object canvas.

## Learner workflow

1. The scenario starts with no termination state and no electrically gated permanent links.
2. In first-person view of a supported patch panel or wall-port faceplate, **Front View** and **Rear Termination View** are available immediately. Neither is gated by an existing front patch cable or by prior rear work.
3. In rear mode, the current device is the source. The learner chooses a distant wall port or patch panel with an unfinished endpoint, then chooses its unfinished port.
4. A separate **Rework** route exposes every previously touched module, including modules that are fully punched. This allows any front or rear connection to be redone at any point.
5. The rear workbench displays the two physical rear modules for the selected permanent run. The learner may place any of eight conductors in any terminal and use the punch-down tool. Wrong placement is permitted.
6. Every placement, removal, and punch sends the full module state to the session. The system silently evaluates it; the workbench displays only placement and punched state.
7. When both endpoints of the same run are valid, the existing physical graph gains that permanent edge. Reworking either side incorrectly removes it immediately.
8. Learners observe the consequence through normal runtime behavior: dynamically installed network-device LED meshes and placed-computer screen/menu availability follow the effective path. Router and switch CLI inspection will later read that same runtime connectivity state.
9. Closing the workbench restores the existing first-person device view and input state. Opening it again restores all work from the active scenario run. Restarting the scenario resets termination state and connectivity to baseline.

## T568B electrical validation

Create one pure, edit-mode-testable validator. It accepts a rear module only when all eight pins are punched and contain the expected T568B conductor:

| Pin | Required conductor |
| --- | --- |
| 1 | White/Orange |
| 2 | Orange |
| 3 | White/Green |
| 4 | Blue |
| 5 | White/Blue |
| 6 | Green |
| 7 | White/Brown |
| 8 | Brown |

The validator returns a derived `isElectricallyCorrect` value to runtime code. It must not:

- correct a learner's selection;
- prevent an incorrect punch-down;
- play a success/failure cue;
- expose individual failure reasons to the workbench UI; or
- discard raw wire, terminal, or punch-state data.

The later evaluator uses the raw state plus the same standard definition to generate its learner-facing error report. It is not responsible for runtime activation.

## Runtime data and graph design

### Scenario record extension

Extend `ScenarioPermanentCableConnection` in `Assets/Scripts/TssScenarioDefinition.cs` with only the data the existing scenario cannot already express:

```csharp
public bool requiresPunchDownTermination;
public string endpointATerminationModuleId;
public string endpointBTerminationModuleId;
```

Use `connectionId` as the run identity. Use generic endpoint-A/B module fields; do not encode physical type in a field name. Module IDs may initially follow `Infrastructure001:A` and `Infrastructure001:B`, while the explicit fields support future exceptions without separate per-run assets.

Create an editor-only migration and validation command that:

1. Sets the flag and module IDs for all 41 `Balanced_12_Piece_Lab` records.
2. Includes `Infrastructure013` with two patch-panel modules.
3. Reports duplicate connection IDs, blank module IDs, missing endpoint IDs, and endpoints that do not resolve exactly once in the loaded scene.
4. Uses serialized properties; it must not mass-edit Unity YAML text.

Leave the flag false by default so future permanent records can retain current behavior until explicitly converted.

### Session-owned termination state

Add a private `TssTrainingSession` dictionary keyed by `connectionId` and endpoint ID. Each module snapshot retains:

- connection ID, endpoint ID, module ID, and endpoint role;
- all eight terminal/pin identities;
- selected conductor color for each terminal;
- punched/unpunched state for each terminal; and
- optional operation order/timestamp for later reporting.

Store the raw snapshot and derived electrical result separately. The raw snapshot is authoritative for later evaluation; the Boolean is a disposable runtime cache that controls the graph.

Expose a minimal API, keeping DTO types in the existing TSS runtime assembly:

```csharp
public bool TryGetTerminationLinks(string endpointId,
    out IReadOnlyList<ScenarioPermanentCableConnection> links);
public bool SetTerminationModuleState(TssTerminationModuleState state,
    bool isElectricallyCorrect, out string message);
public bool TryGetTerminationModuleState(string connectionId, string endpointId,
    out TssTerminationModuleState state);
public IReadOnlyList<TssTerminationModuleState> GetTerminationModuleStates();
```

`SetTerminationModuleState` must validate the connection, endpoint, module ID, and exact eight-slot payload before replacing state. It must preserve invalid learner choices exactly, then raise `StateChanged` after reconciling the affected run.

Expose one read-only presentation query on the existing session, for example `IsEndpointOnEffectivePhysicalPath(string endpointId)`. It derives its answer solely from the existing physical graph / `GetEffectivePhysicalPaths()` semantics and lets a dynamic device presentation ask whether its own generated endpoint participates in an end-to-end path that uses permanent infrastructure. Do not let LED or computer presentation classes inspect termination snapshots directly.

### Gate the existing permanent record

Modify the existing permanent-link flow rather than adding a minigame connection registry:

1. `ApplyPermanentConnections()` skips a record marked `requiresPunchDownTermination` unless both named endpoint modules are electrically correct.
2. Add a private `ReconcilePermanentConnection(connectionId)` called after a successful module-state update.
3. The reconciler adds the normal existing `TssPhysicalConnectionRecord` when both endpoints validate, and removes only that permanent record when either endpoint becomes incomplete or invalid.
4. It never removes, alters, or validates normal player-created patch-cable records.
5. `GetEffectivePhysicalPaths()`, the infrastructure panel, LEDs, and computers continue to consume the existing physical graph.

This is the implementation-critical behavior: a wrong punch-down is not merely a future grading result; it is a missing physical-layer edge during the current scenario.

## Rear-workbench UI and scene components

Build the workbench directly in the TSS project under an isolated feature folder, for example:

```text
Assets/TSSInfrastructureTermination/
  Runtime/       # state DTOs, validator, controller, TSS adapters
  Presentation/  # workbench UI, 3D rear modules, conductor and tool views
  Data/          # T568B and rear-layout ScriptableObjects
  Prefabs/       # workbench/modal and reusable module prefabs
  Art/           # created or properly licensed models/materials/audio
  Editor/        # scenario migration and validation command
  Tests/         # edit-mode and play-mode tests
  Documentation/ # inspector setup and asset provenance
```

Use TSS's installed Unity/URP/Input System stack. Do not add a package, duplicate a player controller, create a second camera-management system, or create a separate save system.

### Required presentation elements

- Rear patch-panel and wall-port module presentations with clear molded pin labels.
- Eight individually selectable conductors, including distinguishable striped/solid variants.
- A punch-down tool animation that seats/cuts after a short action and commits state only after the action completes.
- `Place`, `Remove/Rework`, `Punch Down`, `Close`, `Front View`, and `Rear Termination View` controls with mouse/keyboard focus states.
- No color-order cheat sheet; the header may state `T568B` only.
- Reduced-motion support that shortens the animation but preserves the state transition.
- An accessible non-color cue for selected, placed, and punched state—without revealing electrical correctness.

Start with custom technical blockout models. Before importing any external model, verify that its license permits redistribution in TSS and record source, license, creator, and import settings in asset provenance.

## Entry, selection, and input integration

Add one `TssTerminationEntryAdapter` to each supported patch-panel and wall-port **device root**. Do not add it to individual jacks, colliders, routers, switches, printers, or computers.

The adapter subscribes to the device's existing `TssObjectInteractable` first-person events and exposes front/rear controls only when the device has child `TssPortEndpoint` objects participating in permanent scenario links. Since the eligible scene objects have no assigned `canvasPrefab`, give the adapter one shared `TssTerminationFirstPersonControls` overlay prefab. It instantiates the overlay on first-person entry and destroys it on return/handoff. The overlay visibly offers **Front View** and **Rear Termination View**; the existing temporary Return control can remain until it is deliberately consolidated. Do not place the controls only in `TssRuntimeUi`'s global canvas. The adapter asks `TssTrainingSession` for candidate links rather than scanning arbitrary scene objects.

Add one `TssTerminationGameController`/coordinator on the established session or infrastructure root. It:

1. receives the source endpoint from the entry adapter;
2. filters distant devices and ports by the selected link and unfinished endpoint state;
3. exposes the explicit rework list for touched/complete modules;
4. loads state for both named modules;
5. calls the pure validator on each module update, then `SetTerminationModuleState`; and
6. owns workbench open/close and only this feature's modal lifetime.

Extend `TssRuntimeUi` with owner-scoped external modal locks, for example `PushExternalGameplayLock(object owner)` and `PopExternalGameplayLock(object owner)`. Reuse its existing cursor/player-control behavior. While locked:

- `TssPortSelectionAdapter` must not raycast or select ports;
- `TssObjectInteractable` must not process Escape-to-return; and
- the workbench is the only active interaction surface.

On every close, cancel, disable, or failed-open path, pop the lock exactly once and restore the existing first-person view.

## LED and computer runtime evidence

The implementation must wire live runtime evidence to the **same** effective physical-path result used by normal connectivity, not to a Boolean on the minigame UI.

### Device-interface indicators

The open scene does not hold static router/switch instances; `RackMountController` instantiates `EquipmentDefinition.rackPrefab` under each rack's `Runtime Installed Equipment` root and then `TssPortEndpointBinder` creates/configures interface endpoints. Add one small `TssDeviceLinkIndicator` to each applicable router, switch, and server prefab—not to the scene root. It holds serialized references to its named LED mesh renderers and their existing connected/disconnected materials. When desktop and printer prefabs gain dedicated LED objects, add the same component and serialized endpoint-to-renderer mapping there.

On enable and on `TssTrainingSession.StateChanged`, the indicator resolves its own configured `TssPortEndpoint` interface(s) and calls `IsEndpointOnEffectivePhysicalPath`. It changes only the instance renderer material/property block, never the shared asset material. The initial vertical slice may drive one mapped data interface LED per installed router and each switch; define the remaining interface mapping from `EquipmentDefinition.interfaces` and `portAnchorPath` before expanding coverage.

### Layer ownership and status precedence

The termination minigame owns **Layer 1** only. Its physical result is the necessary condition for any later network service, but it does not pretend to determine switching or routing health.

| Status level | Authority | Indicator/diagnostic behavior |
| --- | --- | --- |
| Layer 1 absent | Effective physical graph has no path because a permanent termination or front patch is missing/incorrect. | Interface indicator is disconnected/off; computer network function is unavailable; later IOS reports physical link down. |
| Layer 1 present | `IsEndpointOnEffectivePhysicalPath` confirms an end-to-end physical path that includes permanent infrastructure. | Physical-link indication is available, but it does not by itself claim Layer 2 or Layer 3 success. |
| Layer 2/3 result (later) | The future switching/routing simulator and IOS command model. | It overlays its own up/down/fault status on the same device interface and computer presentation, and its IOS output explains the relevant protocol/configuration state. |

Do not add a speculative generic Layer 2/3 simulator in this termination implementation. Instead, make the device indicator accept a future optional network-status input with Layer 1 as its baseline. This keeps physical evidence immediately useful and makes later IOS/Layer 2/3 work an extension of the same endpoint identities rather than a second display system.

### Computer screens and menu

Extend the existing `MonitorTextureRotator` with a narrow connectivity override API, such as `SetConnectivityOverride(Texture2D offlineScreen)` and `ClearConnectivityOverride()`. While an override is active, its existing `Apply()` must keep the offline texture in `_BaseMap`, `_MainTex`, and `_EmissionMap` and suppress rotation; clearing it resumes the normal screen list. Use a `TssComputerConnectivityPresenter` on runtime-placed desktop-PC visuals to find their generated `TssPortEndpoint`, call `IsEndpointOnEffectivePhysicalPath`, and control that existing override.

For the existing `TssRuntimeUi` computer menu, add a narrow `EndpointPlacementStation.TryGetPlacedEndpoint(placementId, out TssPortEndpoint endpoint)` helper over its existing placed-visual dictionary. `ClickPlaced` uses it to make the Access Computer action resolve the same effective-path query first. If unavailable, show a neutral network-unavailable message in the existing panel rather than pretending the computer application works. This is runtime evidence, not a minigame correctness message.

For every consumer, define:

| Runtime state | Required presentation |
| --- | --- |
| No effective path because a required permanent run is unfinished or incorrect | The mapped installed-device LED mesh displays its disconnected material; the mapped placed computer holds its offline screen and its existing computer menu reports network unavailable. |
| Effective path restored through valid rear terminations and required front patching | The mapped LED mesh displays its connected material; the computer clears its screen override and its network-dependent menu action becomes available. |
| A previously valid module is reworked incorrectly | The graph, LED, and computer result return to disconnected without scene reload. |

Do not create a separate LED truth table in the minigame. Cisco IOS console work is deferred, but it must inspect the same endpoint identities and layered status: physical path first, then Layer 2 and Layer 3 state supplied by their future systems.

## Inspector and scenario setup plan

1. Work from a clean/main-project branch and preserve unrelated current Unity changes.
2. Add the feature controller under the existing session/infrastructure root; assign `TssTrainingSession`, `TssRuntimeUi`, workbench prefab/canvas, T568B definition, generic patch layout, and generic wall-port layout.
3. Add the entry adapter only to the two patch-panel and ten wall-port roots. Reuse their existing `TssObjectInteractable`, `TssPortSelectionAdapter`, and child endpoints. Assign the one shared first-person-controls overlay prefab on the adapter/coordinator; do not set twelve separate `canvasPrefab` references.
4. Run the editor migration for `Balanced_12_Piece_Lab`; inspect `Infrastructure001` and `Infrastructure013` as the horizontal and backbone vertical slices.
5. Configure LED targets on router, both switch, and server equipment prefabs, plus computer presentation on runtime-placed desktop prefabs. Add the same mapping to desktop/printer prefabs only when their LED objects exist. Do not make scene-only LED assignments. Document every `EquipmentDefinition.interfaces` to LED/screen mapping in `Documentation/Inspector-Setup.md`.
6. Run endpoint resolution in the loaded scene before testing. Do not alter prefab default endpoint IDs without validating instantiated objects and dynamic `TssPortEndpointBinder` configuration.

## Implementation phases for the next work session

### Phase 1 — source confirmation and contract

Confirm the `EquipmentDefinition.interfaces` to LED mapping for the router, both switches, and server, plus generated endpoint IDs for the initial desktop-PC placement. Extend `MonitorTextureRotator` with its override API and add the prefab-level LED presenter. Create data DTOs, T568B definition, pure validator, scenario-field migration command, and edit-mode tests.

**Exit:** Valid/invalid raw module state is representable and tested; all 41 scenario records have resolvable endpoint/module configuration.

### Phase 2 — graph gating vertical slice

Implement session storage, `ApplyPermanentConnections()` gating, and reconciliation. Test `Infrastructure001` with two module endpoints: empty/wrong, one valid endpoint, both valid endpoints, and rework after success. Verify the normal effective path changes without duplicate or altered player cables.

**Exit:** A wrong punch-down objectively removes the run from runtime connectivity; correcting both ends restores it.

### Phase 3 — rear workbench vertical slice

Create blockout rear modules, conductor placement, punch animation, state restore, and first-person entry/exit. Add modal input ownership and confirm front/rear controls remain available for rework.

**Exit:** A learner can independently modify both endpoints, close/reopen without loss, and cannot interact with underlying ports while the workbench is open.

### Phase 4 — runtime evidence and backbone coverage

Bind the dynamically instantiated router, both switch, and server LED meshes and placed-computer monitor/menu behavior to effective Layer 1 connectivity. Validate both the horizontal `Infrastructure001` and patch-panel-to-patch-panel `Infrastructure013` cases. Add rework regression coverage and document the Layer 2/3 indicator extension point for later IOS work.

**Exit:** Invalid work creates observable disconnected device behavior; rework updates it live; the backbone behaves the same as a horizontal run.

### Phase 5 — full scenario, polish, and handoff

Expand to all 41 runs, polish accessibility/art/audio, document Inspector assignments and assets, and run edit-mode/play-mode regression checks. Keep final submission, grading UI, and CLI implementation out of this phase.

**Exit:** `Balanced_12_Piece_Lab` is consistently gated by silent T568B termination state and is ready for later scenario-wide evaluation work.

## Test matrix

| Area | Required check |
| --- | --- |
| Validator | Exact eight punched T568B pins pass; every wrong color, wrong position, duplicate, missing, or unpunched pin fails. |
| Raw state | Wrong state survives close/open and is never normalised or rejected merely for being wrong. |
| Pair rule | One correct endpoint does not create a permanent edge; two correct endpoints do. |
| Rework | Turning either endpoint wrong removes only its associated permanent edge immediately. |
| Existing topology | Player patch-cable records, visuals, and routing remain unaffected by termination reconciliation. |
| Backbone | `Infrastructure013` supports two patch-panel endpoints and the same pair rule. |
| Runtime evidence | Dynamically instantiated router, both switch, and server LED mesh renderers plus placed-computer monitor/menu behavior switch through `IsEndpointOnEffectivePhysicalPath`, including live invalid rework. |
| Layer boundary | Layer 1 presentation never reports Layer 2/3 success by implication; the documented future IOS/Layer 2/3 status input uses the same endpoint identities. |
| Input | The shared first-person overlay appears only for the 12 eligible device roots; the workbench modal blocks background port/Escape input and restores the normal first-person view on all exit paths. |
| Reset | Restarting the scenario clears raw termination state and returns gated links to baseline. |
| Learning integrity | The UI exposes no ordered answer, correctness label, score, or submit action. |

## Explicit deferrals

- Comprehensive final submission, scoring, and learner-facing error report.
- Router/switch CLI inspection UI; it will later query the same effective physical graph.
- Progress persistence beyond the active TSS scenario run.
- 568A, crossover, fiber, PoE faults, tone/testing tools, multiplayer, LMS reporting, and non-permanent cable types.
- Any standalone project or exportable package.
