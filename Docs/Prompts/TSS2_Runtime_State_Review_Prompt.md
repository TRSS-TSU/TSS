# TSS2 Runtime State Review Prompt

Review the current GitHub repository as a senior Unity network-training-simulation engineer.

Repository: `https://github.com/TRSS-TSU/TSS`

This is the TSS2 Unity project: a network-training simulator covering equipment placement, rack installation, structured cabling, patch-panel and wallport terminations, Layer 2 configuration, device sessions, reachability, work-package evaluation, binder guidance, and results reporting.

Do not modify the repository. Review the actual source code, scenes, prefabs, ScriptableObjects, external scenario JSON, tests, and documentation. Do not rely only on filenames, README files, or class names. Trace real initialization, ownership, mutation, notification, evaluation, and display paths.

The primary question is:

> What runtime state does TSS2 track, where is that state stored, which system owns it, and which systems can manipulate it?

Also determine whether the current state model is deterministic, understandable, and safe for the training workflow.

## Core training workflow

Trace the complete runtime flow:

Scenario selection
→ external or serialized scenario loading
→ scenario handoff into the training scene
→ equipment and rack placement
→ cable checkout and physical connections
→ patch-panel and wallport terminations
→ device configuration through CLI or endpoint sessions
→ VLAN, trunk, addressing, and carrier state changes
→ connectivity and reachability evaluation
→ work-package evaluation
→ HUD, binder, checklist, and Results display

Include the developer/automation workflow where applicable:

Place
→ Connect
→ Terminate
→ Evaluate

Pay particular attention to the Capstone scenario and the Work Package 1–4 assessment flow.

## Systems to inspect

Start with these systems and follow their callers and dependencies.

### Scenario and data

- `TssScenarioDefinition`
- `TssExternalScenarioData`
- `TssExternalScenarioLoader`
- `TssExternalScenarioValidator`
- `TssScenarioCatalog`
- `TssScenarioCatalogResolver`
- Scenario manifest and JSON files under `Assets/StreamingAssets/Scenarios`
- Serialized scenario assets and any fallback scenario configuration

Determine which scenario source is authoritative when serialized scenarios, external JSON, fallback data, and runtime-generated objects overlap.

### Runtime session state

- `TssTrainingSession`
- `TssNetworkSessionState`
- `TssDeviceLogicalState`
- `TssDeviceSessionController`
- `TssTerminationModuleState`
- `TssTerminationGameController`

Trace every meaningful state field and determine whether it represents:

- Scenario configuration
- Student progress
- Physical installation state
- Logical network state
- Device-session state
- Connectivity state
- Completion history
- UI-only or cached presentation state

### Placement, cabling, and physical installation

- `EndpointPlacementStation`
- `RackMountController`
- `TssPortEndpoint`
- `TssPortEndpointBinder`
- `TssCableDefinition`
- `TssCableRouteProvider`
- `TssCableRouteNode`
- `TssCableRouteResult`
- `TssCableRouteTopologyAudit`
- `TssCableVisual`
- `TssPhysicalWorkPackageEvaluator`
- `TssPlacementEvaluator`
- `TssScenarioAutomation`

Trace how the runtime represents:

- Placed equipment
- Rack positions
- Endpoint identity
- Port usage
- Player cable inventory
- Cable checkout
- Physical connections
- Permanent infrastructure links
- Effective device paths
- Patch-panel and wallport relationships
- Termination state

Identify whether authored scenario requirements or runtime compatibility checks decide what may be connected.

### Network configuration and behavior

- `TssCliCommandProcessor`
- `TssEndpointCommandProcessor`
- `TssNetworkReachabilityEvaluator`
- `TssNetworkWorkPackageEvaluator`
- `TssDeviceLinkIndicator`
- `TssComputerConnectivityPresenter`
- `TssDesktopSessionPresenter`
- `TssTerminalSessionPresenter`
- Wireless device and access-point state classes
- Any router, switch, VLAN, trunk, carrier, gateway, or provider-reachability logic

Trace how commands mutate state and how those mutations propagate to:

- Interface and port status
- VLAN and trunk configuration
- Router subinterfaces
- Carrier state
- Addressing and gateways
- Device reachability
- Provider or upstream reachability
- Work-package completion criteria
- Visual indicators and monitor displays

Distinguish simulated connectivity from actual network behavior.

### Evaluation, results, and completion state

- `TssScenarioAssessment`
- `TssScenarioAssessmentCoordinator`
- `TssPhysicalWorkPackageEvaluator`
- `TssNetworkWorkPackageEvaluator`
- `TssResultsHandoff`
- `TssResultsSnapshot`
- Completion-history classes and snapshots
- `TssResultsView`
- `TssResultsCard`

Determine whether evaluation reads live state, cached state, completion history, or copied snapshots. Identify when snapshots are created and whether they can become stale.

### UI and presentation

- `TssRuntimeUi`
- `TssBinderReferenceTabPresenter`
- `TssCableScheduleTabPresenter`
- `TssIpPlanTabPresenter`
- `TssNamingTabPresenter`
- `TssVlanTabPresenter`
- `TssPhysicalConnectionPanel`
- `TssPlacementTrackerPanel`
- `TssWallportTerminationView`
- `TssDeviceLinkIndicator`
- `MonitorTextureRotator`
- Other presenters subscribed to `TssTrainingSession.StateChanged`

Determine whether UI classes only display state or whether any of them also create, own, cache, or mutate training state.

## State inventory

Create a table containing every important runtime state object or state group:

| State or state group | Authoritative owner | Initialization path | Readers | Mutators | Lifetime | Notification or refresh path | Persistence/snapshot behavior | Risk |
| -------------------- | ------------------- | ------------------- | ------- | -------- | -------- | ---------------------------- | ----------------------------- | ---- |

Include file paths and relevant method names for every row.

At minimum, cover:

- Active scenario
- Scenario data and requirements
- Placed equipment
- Rack occupancy
- Endpoint identity and bindings
- Cable inventory
- Cable checkout state
- Physical connections
- Permanent links
- Termination state
- Device configuration
- CLI/session state
- VLAN and trunk state
- Interface/carrier state
- Reachability state
- Completion history
- Assessment results
- UI display state
- Developer automation state

## State manipulation map

List every meaningful way state can change.

Include:

- Scenario menu actions
- Scenario loading and fallback behavior
- `BeginScenario`
- Equipment placement
- Rack mounting
- Port selection
- Cable checkout
- Physical cable connection
- Permanent-link connection
- Termination drag/drop or terminal actions
- CLI commands
- Endpoint commands
- Device-session commands
- Wireless configuration
- Developer buttons in `TssRuntimeUi`
- `TssScenarioAutomation`
- `ConnectAllCables`
- `TerminateAll`
- Any “complete all” or preview actions
- Direct calls to `NotifyStateChanged`
- Direct calls to `NotifyNetworkStateChanged`
- Scene lifecycle methods
- Inspector-assigned serialized state
- External JSON reloads
- Assessment and Results actions

For each mutation path, identify:

1. The initiating caller.
2. The method that changes state.
3. The authoritative state object changed.
4. The notification or refresh that follows.
5. Which displays and evaluators observe the change.
6. Whether the operation is reversible.
7. Whether the operation can bypass scenario requirements or normal training rules.

## Notification and refresh analysis

Trace every state-change mechanism, especially:

- `TssTrainingSession.StateChanged`
- `NotifyStateChanged`
- `NotifyNetworkStateChanged`
- Direct presenter refresh calls
- Polling or per-frame checks
- Cached effective endpoint/path calculations
- Recalculation of reachability
- Evaluation refreshes
- Scene reload or object-enable behavior

Determine whether notifications are:

- Complete
- Duplicated
- Missing
- Fired too early
- Fired too often
- Fired while state is only partially updated
- Able to trigger expensive repeated recomputation
- Able to produce stale UI or stale evaluator results

## Ownership and source-of-truth review

Identify:

- Multiple sources of truth for the same state.
- Runtime state duplicated in presenters.
- Cached state that is not invalidated.
- Serialized fields that compete with runtime values.
- External scenario data that is copied incorrectly.
- Evaluators that reconstruct state differently from the runtime session.
- UI code that silently mutates state.
- Physics, visual, or interaction components that bypass the session owner.
- Developer automation that uses different rules from normal user interaction.
- Any state that is public and freely mutable without validation.
- Any state that is expected to survive scene transitions but does not.

For each issue, explain which source appears authoritative in practice and how divergence can occur.

## Training-workflow risks

Rank each risk as High, Medium, or Low and cite the relevant files and methods.

Check specifically for:

- Scenario data being replaced or lost during scene handoff.
- Runtime-generated objects not receiving correct endpoint bindings.
- Endpoint IDs, display labels, wallport names, or Hierarchy bindings being overwritten.
- Cable paths being chosen merely because ports are compatible instead of following authored scenario requirements.
- Cable checkout and physical connection state becoming inconsistent.
- Termination state not matching the connected physical path.
- CLI state changing without notifying dependent displays.
- Carrier state and subinterface state being confused.
- Gateway reachability being treated as provider reachability.
- Layer 2 evaluation depending on unrelated Layer 1 or Layer 4 state.
- Work-package completion being based on stale or overly coarse state.
- Results snapshots being created before the final state refresh.
- `CONNECT ALL CABLES` or similar automation causing repeated refresh storms or stalls.
- UI presenters becoming hidden state owners.
- Scene or prefab overrides masking code behavior.
- Developer automation bypassing the same validation rules used by manual training.
- State being lost or duplicated on scene reload.
- Multiple mutations occurring before one final coherent refresh.

## Runtime transition walkthrough

Describe the actual state transitions from:

1. Menu startup.
2. Scenario selection.
3. External or fallback scenario resolution.
4. `TssTrainingSession` initialization.
5. Equipment placement.
6. Rack installation.
7. Cable checkout.
8. Physical connection.
9. Termination.
10. CLI/device configuration.
11. Connectivity refresh.
12. Work-package evaluation.
13. Results generation.

For each transition, provide:

- The initiating action.
- The method or event involved.
- The state changed.
- The notification issued.
- The dependent systems refreshed.
- Any ordering or timing risks.

## Guidance

Recommend the smallest safe improvements for making runtime state tracking and manipulation clearer and more deterministic.

Preserve:

- Existing scene and prefab bindings.
- Unity Hierarchy names.
- Inspector-assigned endpoint IDs and display labels.
- Wallport names.
- Existing scenario-driven requirements.
- Existing manual training behavior.
- Existing individual user workflows.
- Existing work-package boundaries.
- Existing fallback and external-scenario behavior unless a defect is proven.

Prefer:

- Reusing existing session methods and evaluators.
- Explicit ownership at the current session layer.
- Narrow adapters or coordinators only where necessary.
- Batching notifications when multiple related mutations occur.
- One final refresh after a coherent operation.
- Small diagnostics that expose ownership and transitions.
- Additive, reversible changes.

Avoid recommending broad architecture rewrites, new frameworks, or speculative abstractions.

For every recommendation, include:

- The concrete problem.
- The current owner and mutation path.
- The smallest practical change.
- The affected file/class/method.
- Whether it affects Data, Runtime State, Interaction, Evaluation, or Display.
- How to validate it in Unity.
- What behavior must remain unchanged.

## Validation plan

Provide a practical validation plan that separates static review from live Unity verification.

Cover:

- Clean compilation.
- Scenario loading from the menu.
- External JSON scenario loading.
- Fallback scenario behavior.
- Equipment placement.
- Rack installation.
- Cable checkout.
- Scenario-authoritative cable routing.
- Permanent connections.
- Patch-panel and wallport terminations.
- CLI configuration.
- VLAN and trunk behavior.
- Carrier and subinterface behavior.
- Gateway reachability.
- Provider reachability.
- Binder and HUD updates.
- WP1 physical evaluation.
- WP2 Layer 2 evaluation.
- WP3/WP4 evaluation if present.
- Results snapshot correctness.
- Manual workflow versus developer automation.
- `CONNECT ALL CABLES` and `TERMINATE ALL`.
- Scene reload and state lifetime.
- Notification batching and responsiveness.

Clearly label:

- Proven by code inspection.
- Proven by unit or Edit Mode tests.
- Proven by live Unity Play Mode.
- Not yet verified.

The final deliverable should be a precise, evidence-based map of TSS2 runtime state ownership, tracking, and manipulation, followed by a short list of the highest-value, lowest-risk improvements.
