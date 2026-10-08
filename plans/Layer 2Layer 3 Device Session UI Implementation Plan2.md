# Layer 2/Layer 3 Device Session UI Implementation Plan

## Summary

Convert the terminal and desktop session canvases from mostly simulated UI into deterministic training interfaces backed by the active game session.

The implementation will:

- Require router and switch work to be completed by typing commands in the terminal.
- Remove interactive Quick Commands from the terminal sidebar.
- Preserve valid player mistakes while rejecting malformed commands and addresses.
- Support the Layer 2 and Layer 3 tasks defined by the student and instructor capstone documents.
- Drive link LEDs and desktop monitor screens from evaluated connection state.
- Preserve existing Layer 1 behavior, prefab bindings, and unrelated worktree changes.

## Implementation Changes

### 1. Authoritative network session state

- Move mutable router, switch, server, printer, and desktop configuration into session-owned network state associated with `TssTrainingSession`.
- Reset the network state when a scenario begins so configuration cannot leak between runs.
- Generate one stable set of student-specific values per scenario run:
  - Management, Reception, Data, Native, and Dead VLAN IDs.
  - Three `/24` LAN networks and gateway addresses.
  - Customer/provider `/30` WAN addresses.
  - Device credentials and MOTD values.
- Keep these generated values unchanged while the active run remains open, including when a canvas is closed and reopened.
- Replace nondeterministic MAC address generation with a stable project-local calculation.
- Keep existing serialized endpoint IDs and Hierarchy names as source-of-truth configuration.
- For the initial implementation, picking up and relocating configured equipment clears its device configuration to prevent configuration from silently following the wrong physical slot.

### 2. Terminal CLI and router/switch configuration

- Add one shared command-processing path used by router and switch terminal sessions. It receives the active device and typed command, updates authoritative state when valid, and returns output for the terminal.
- Support the minimum Cisco-style command set required by the capstone:
  - EXEC, privileged EXEC, global configuration, interface, interface-range, VLAN, and subinterface modes.
  - `enable`, `configure terminal`, `exit`, `end`, and `hostname`.
  - Required password, login, encryption, banner, and access hardening commands.
  - VLAN creation, naming, suspension, and access-port assignment.
  - Trunk mode, native VLAN, and allowed VLAN configuration.
  - `shutdown` and `no shutdown`.
  - Router physical-interface and 802.1Q subinterface configuration.
  - IPv4 address and subnet-mask configuration.
  - Static and default routes.
  - `show running-config`, `show startup-config`, `show vlan brief`, `show interfaces trunk`, `show ip interface brief`, and `show ip route`.
  - `ping`.
  - `copy running-config startup-config`.
- Reject malformed syntax, invalid IPv4 addresses, invalid masks, and commands that are unavailable in the current mode.
- Accept syntactically valid but incorrect values so player errors remain possible and observable.
- Keep running and startup configuration separate. A save command copies the current running configuration into startup state.
- Make show-command output derive from authoritative state rather than fixed text.
- Make ping results depend on the requested destination and evaluated network path rather than the presence of any active physical interface.
- Retain command history, terminal scrolling, keyboard focus, and an iPad-friendly Enter/Submit control.
- Preserve existing Gigabit endpoint identities while presenting appropriate CLI names such as `Fa0/0` and `Fa0/1` through an explicit device-interface mapping.

### 3. Terminal and desktop canvas UI

- Convert `TssTerminalSessionPresenter` into a display/input adapter; it must not own authoritative configuration.
- Remove the Quick Commands buttons and any sidebar controls that execute commands.
- Replace the sidebar with a non-interactive, contextual command-reference panel containing syntax guidance only. It must not display correct scenario values or live configuration answers.
- Keep all router and switch work—including verification—inside the terminal.
- Update `TssDesktopSessionPresenter` so address changes are validated and committed only when the player selects Apply.
- Provide endpoint command support for:
  - `ipconfig`
  - `ping`
  - `tracert`
  - `cls`
- Derive desktop, printer, and server status from the same session state and reachability evaluation.
- Remove hardcoded successful printing, hardcoded online states, and topology details that reveal the solution.
- Ensure desktop controls remain usable with touch input and the existing first-person interaction workflow.

### 4. Connectivity evaluation and visual feedback

- Add a deterministic evaluator that reports:
  - Layer 1 physical/admin state.
  - Layer 2 VLAN and trunk continuity.
  - Layer 3 addressing, gateway, route, and destination reachability.
  - The first failed layer and a short player-facing failure reason.
  - Route hops for `tracert`.
- Reuse the evaluator for terminal ping, desktop ping/tracert, monitor state, and work-package assessment.
- Keep physical LEDs limited to physical link and administrative state. A link light must not imply correct VLAN or routing configuration.
- Repair runtime endpoint binding so dynamically created endpoint components notify dependent LED and monitor presenters after configuration.
- Resolve rack LED bindings through the existing endpoint suffix mapping so serialized values such as `Fa01` bind to runtime-prefixed endpoint IDs.
- Update LED materials with `MaterialPropertyBlock` to avoid creating per-renderer material instances.
- Subscribe visual presenters to physical-connection and network-state changes rather than requiring canvas refreshes.

### 5. Desktop monitor sprites

Create six `1024×576` in-world monitor images:

- `monitor_layer1_connected.png`
- `monitor_layer1_disconnected.png`
- `monitor_layer2_connected.png`
- `monitor_layer2_disconnected.png`
- `monitor_layer3_connected.png`
- `monitor_layer3_disconnected.png`

Use a consistent simulated operating-system diagnostic style with high contrast and legible status cues. The images may indicate the failed layer but must not reveal generated VLAN IDs, IP addresses, passwords, or exact repair commands.

- Assign all six images to the monitor presentation component.
- Select the connected or disconnected image for each layer from evaluated state.
- Cycle or transition among the three layer views using the existing monitor texture presentation path.
- Verify renderer, material index, texture import, filtering, and aspect ratio in third-person view.

## Capstone Task Evaluation

### Work Package 2: Switching

Evaluate the resulting state against these requirements:

- Both switches receive the required hardening configuration.
- Management, Reception, Data, Native, and suspended Dead VLANs exist with generated run-specific IDs.
- AN-1:
  - Fa0/2 and Fa0/5 use Management.
  - Fa0/3 and Fa0/4 use Reception.
  - Fa0/1 is the inter-switch trunk.
  - Fa0/6 is the router trunk.
  - Fa0/7 and Fa0/8 use Dead VLAN and are shut down.
- AN-2:
  - Fa0/2 through Fa0/4 use Data.
  - Fa0/1 is the inter-switch trunk.
  - Fa0/5 through Fa0/8 use Dead VLAN and are shut down.
- Trunks use the generated Native VLAN and permit only the required user VLANs.
- The player runs `show vlan brief` and `show interfaces trunk`.
- Both switch running configurations are saved to startup configuration.

Ports outside the capstone’s Fa0/1–Fa0/8 assessment remain available for Layer 1 visuals but are not graded for Layer 2/3 configuration.

### Work Package 3: Routing

Evaluate the resulting state against these requirements:

- Router hardening is complete.
- Fa0/0 is enabled.
- Three 802.1Q subinterfaces use the correct generated encapsulation, addressing, and gateway values.
- Fa0/1 uses the generated customer WAN `/30` address and is enabled.
- A correct default route points to the provider.
- The player uses endpoints—not only the router—to ping:
  - The endpoint’s gateway.
  - A host in another VLAN.
  - The provider destination.
- The player runs `show ip route`.
- The router running configuration is saved to startup configuration.

Store command-evidence flags only for commands explicitly required by the capstone. Correct configuration without the required verification/save workflow does not fully complete the work package.

## Implementation Order and Interfaces

1. Preserve and document the current prefab and scene bindings before modifying scripts.
2. Add generated run values and authoritative network state to the existing training-session lifecycle.
3. Implement the shared CLI parser and running/startup configuration behavior.
4. Implement Layer 1/2/3 reachability evaluation.
5. Connect the terminal presenter to the command processor and replace the interactive sidebar.
6. Connect desktop/server/printer controls and commands to authoritative state.
7. Repair runtime endpoint notifications and connect LEDs and monitor presenters.
8. Create and assign the six monitor images.
9. Add WP2/WP3 assessment and command-evidence tracking.
10. Update the prefab builder and regenerate only the affected session prefabs if that builder remains their source of truth.
11. Run automated and Play Mode acceptance checks.
12. Create the implementation pass-off document from the behavior actually completed and verified.
13. Commit and push the completed work to GitHub:
    - Inspect the final diff and preserve unrelated existing changes.
    - Stage only the Layer 2/Layer 3 implementation, monitor assets, tests, plan, and pass-off documentation.
    - Use the current feature branch; if no suitable branch exists, create `codex/layer-2-layer-3-device-sessions` from the current HEAD.
    - Commit with `Implement Layer 2 and Layer 3 device sessions`.
    - Push the branch to the configured `origin` and set upstream tracking when needed.
    - Report the branch name and commit SHA. Do not create a pull request unless separately requested.

Minimum public interfaces:

- Training session exposes read-only access to the current generated network profile and mutable session-owned network state.
- The command processor accepts device identity plus command text and returns terminal output and success/failure information.
- The reachability evaluator accepts source and destination identity/address and returns reachability, first failed layer, reason, and trace hops.
- Network-state changes raise one event consumed by canvases, LEDs, monitors, and assessment.
- Dynamically configured endpoint binders notify interested components after endpoints are available.

Do not introduce factories, persistence services, packet simulation, or extensibility layers that are not required for this implementation.

## Test and Acceptance Plan

### Automated checks

- Generated profile values remain stable within one run and change after a new scenario begins.
- Network state resets between scenario runs.
- Deterministic MAC values remain stable.
- CLI mode transitions and supported command parsing work.
- Malformed addresses and masks are rejected without mutating state.
- Valid-but-wrong configurations are retained.
- Running and startup configurations remain separate until copied.
- Show commands accurately represent state.
- Layer 1, Layer 2, and Layer 3 failures are classified correctly.
- Ping and traceroute results depend on the requested source and destination.
- WP2 and WP3 evaluators require both correct configuration and required command/save evidence.
- Runtime-prefixed endpoint IDs bind correctly to serialized LED mappings.

### Unity Play Mode checks

- Start a fresh Balanced lab run and record the generated instructions.
- Complete WP2 correctly through typed terminal commands and verify VLAN/trunk output.
- Introduce wrong access VLAN, native VLAN, allowed-VLAN, and shutdown configurations and confirm they remain possible and produce the correct failure layer.
- Complete WP3 correctly and verify endpoint gateway, cross-VLAN, and provider tests.
- Test missing gateway, incorrect subnet, disabled interface, missing subinterface, and missing default route.
- Confirm router-originated pings do not satisfy endpoint-originated assessment requirements.
- Save, close, and reopen each canvas; active-run state must remain.
- Restart the scenario; prior state must be gone and a new generated profile must be used.
- Verify terminal interaction with keyboard and iPad-style touch controls.
- Verify desktop Apply does not mutate state before submission.
- Verify printer/server feedback reflects actual reachability.
- Verify rack LEDs react to cable and administrative changes.
- Verify desktop monitors show the appropriate six layer-state images in third-person view.
- Confirm existing Layer 1 placement, cabling, first-person controls, and canvases still work.
- Confirm Unity compiles without new errors.

Because the repository already contains unrelated Unity YAML whitespace findings, validate touched files and the staged diff separately rather than claiming the entire pre-existing worktree is clean.

## Implementation Pass-Off and Hotwash Documentation

Create:

`C:\Unity\TSS2\Docs\After Layer 2 Layer 3 Device Session Implementation.md`

The document must be written from the final verified implementation, not copied speculatively from this plan.

Include:

- A plain-language summary of the intended goal, what was completed, what remains partial or deferred, and the compile/test/Play Mode result.
- Unity version, scene tested, branch, commit SHA, and relevant dirty-worktree caveats.
- An “Implemented Behavior” section covering generated instructions, reset behavior, CLI state changes, endpoint commands, player-error handling, save behavior, assessment, LEDs, monitors, and removal of Quick Commands.
- An “Assets and Bindings Changed” section listing affected scripts, canvases, device prefabs, scene/scenario assets, and monitor images.
- A verification table with columns for check, result, evidence, and follow-up. Results must use Passed, Failed, Partial, or Not Run.
- Verification entries for compilation, automated tests, correct WP2/WP3 flows, representative failures at each layer, state persistence/reset, both canvas types, LEDs, monitor screens, iPad layout, and Layer 1 regression.
- A “Manual Unity Procedures Required” checklist that identifies the exact asset or scene, component, expected field/value, how success is recognized, and whether the check is mandatory or recommended.
- Explicit Inspector checks for:
  - `DeviceSessionManager` references to both canvas prefabs and both first-person control prefabs.
  - Terminal and desktop presenter UI references.
  - Training-session/network-state references.
  - Balanced lab network template or feature setting.
  - AN-1, AN-2, CN-1, server, printer, and desktop script attachments.
  - Endpoint-to-CLI interface mappings, including GE00/GE01 to Fa0/0/Fa0/1.
  - LED renderer and material assignments.
  - Desktop monitor renderer, material index, rotator, connectivity presenter, and all six textures.
  - Texture import settings.
  - Canvas sorting, EventSystem, navigation, touch interaction, and iPad scaling.
  - Scene and prefab overrides being applied to the correct assets.
- If an item was verified automatically and needs no user action, state: “No manual action required; verified automatically.”
- A “Known Issues and Deviations” section covering unverified behavior, deviations from the PDFs, unrelated dirty changes, Unity/MCP warnings, and deliberate MVP limitations.
- A ranked “Recommended Next Work” section:
  1. Fix failed or unverified acceptance checks.
  2. Complete remaining Inspector or prefab assignments.
  3. Run the complete WP2/WP3 workflow from a fresh generated profile.
  4. Add instructor checkpoint/sign-off presentation.
  5. Add completion reporting and time tracking.
  6. Begin wireless phases after the wired workflow is stable.
  7. Consider persistence across application restarts only after active-run behavior is validated.
- Finish with a concise resume point naming the next task, the first scene/prefab/test to inspect, and any manual prerequisite.

## Assumptions

- Generated values are created per scenario run and are not persisted across application restarts.
- The terminal sidebar remains visible only as a non-interactive syntax reference.
- All router and switch configuration and verification is performed through typed terminal commands.
- The six monitor images represent connected/disconnected status for Layers 1, 2, and 3.
- Existing serialized endpoint IDs, Hierarchy names, scene bindings, and unrelated uncommitted work are preserved.
- No pull request is created unless explicitly requested.
