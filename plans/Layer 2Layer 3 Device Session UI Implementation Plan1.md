# Layer 2/3 Device Session UI and Network Simulation Plan

## Summary

- Target plan file: `C:\Unity\TSS2\plans\layer-2-layer-3-device-session-ui-implementation-plan.md`. Plan Mode kept repository files unchanged; implementation step 0 is to save this plan there.
- Replace the current UI-only simulation with authoritative Layer 2/3 session state shared by the CLI, desktop tools, reachability evaluation, LEDs, monitor screens, and future work-package grading.
- Preserve the current Layer 1 physical graph, endpoint IDs, device prefabs, first-person workflow, and player-error model.
- Requirements are derived from :codex-file-citation{path="C:/Unity/TSS2/Docs/Student capstone instructions (3).pdf" purpose="source"} and :codex-file-citation{path="C:/Unity/TSS2/Docs/Net Capstone Instructor_Guide (2).pdf" purpose="source"}.
- Scope touches Scenario/Data, Network Intent/State, and Display. It does not change physics, world scale, cable routing, or termination rules.

## Current-System Findings to Correct

- `TssTerminalSessionPresenter` currently changes only its local prompt and, for `hostname`, its cached device name. VLAN, switchport, shutdown, IP, subinterface, route, hardening, and save commands do not alter session state.
- Terminal `show` output is mostly hard-coded. `show vlan brief` always reports VLAN 1, and `ping` succeeds against any destination whenever any interface has a physical connection.
- `TssDesktopSessionPresenter` edits a static in-memory `TssDeviceLogicalState`, not `TssTrainingSession`. DHCP toggles mutate state before Apply, inputs are not validated, and ping ignores VLANs, gateways, routes, and the requested target.
- Server services, printer readiness, client leases, and test-print success are hard-coded. The physical-path card exposes internal topology instead of requiring diagnosis.
- The static logical-state cache is not cleared by `BeginScenario`, can leak configuration between runs, and is keyed to a rack position or placement rather than session-owned state.
- Logical interfaces consider any direct cable attachment “up,” while LEDs use effective physical paths. The UI and scene indicators can therefore disagree.
- Runtime endpoint IDs are prefixed, such as `Rack:Rack01:U35:Fa01`, but LED bindings contain suffixes such as `Fa01`. Exact matching prevents the current LEDs from resolving their endpoints.
- Desktop connectivity components awaken before `TssPortEndpointBinder` dynamically adds endpoints, so their endpoint reference remains null. The monitor screen list is empty and its “offline” asset is only a generic black gradient.
- The terminal SidebarPanel provides clickable quick commands and live interface state, bypassing the required CLI verification workflow.
- Unity is presently idle and compiling cleanly. Inspector warnings observed during investigation came from the MCP GameObject serializer, not project scripts. The dirty worktree already contains unrelated YAML whitespace failures, so verification must remain scoped.

## State and Public Interface Changes

### Scenario and generated SOP profile

- Extend [TssScenarioDefinition.cs](C:/Unity/TSS2/Assets/Scripts/TssScenarioDefinition.cs) with an optional `NetworkScenarioTemplate` containing:
  - Device-to-role mappings for AN-1, AN-2, CN-1, IT1, IT2, Reception1/2, and Data1/2/3.
  - CLI-interface-to-physical-endpoint mappings. Preserve `GE00`/`GE01` endpoint IDs while presenting the curriculum-required `FastEthernet0/0` and `FastEthernet0/1` names.
  - Managed switch interfaces Fa0/1-Fa0/8 for this capstone. Visual ports Fa0/9-Fa0/24 remain Layer 1 assets but are excluded from Layer 2/3 grading.
  - VLAN roles, expected access/trunk assignments, required hardening fields, host roles, and WAN topology.
  - `enableLayer23Simulation` for reversible rollout.
- At every `BeginScenario`, generate one `NetworkRunProfile` and retain its seed for the active run:
  - Five unique VLAN IDs: Management, Reception, Data, Native, and Dead.
  - Exact case-sensitive names, with `Dead_Vlan` suspended.
  - Three unique private /24 user networks and `.1` gateways.
  - Host addresses derived consistently by device role.
  - A private /30 WAN with provider and customer addresses.
  - Enable secret, console password, VTY password, and MOTD values.
- Generation must be pure and repeatable for a supplied seed. Reopening a canvas must not regenerate values. Restarting the scenario creates a new run/profile.
- Display the generated values through the existing SOP interface. Never print credentials in logs.

### Authoritative runtime state

- Replace the static cache in `TssDeviceLogicalState` with a session-owned `TssNetworkSessionState`, exposed read-only as `TssTrainingSession.NetworkState`.
- Add:
  - `NetworkDeviceState`: hostname, hardening fields, running config, startup snapshot, VLAN database, interfaces, subinterfaces, and static routes.
  - `NetworkInterfaceState`: canonical CLI name, endpoint suffix/full ID, admin state, physical carrier, switchport mode, access VLAN, allowed VLANs, native VLAN, IP/mask, and dot1q VLAN.
  - `EndpointNetworkState`: DHCP/static settings and current effective address.
  - `NetworkStateChanged` event carrying affected device and endpoint keys.
- Use a stable deterministic hash instead of `string.GetHashCode()` for generated MAC addresses.
- Clear the network state during `BeginScenario`. For this MVP, picking up configured equipment clears that device’s configuration so state cannot leak to another item installed in the same slot.

### Command and reachability services

- Add a pure `TssCliCommandProcessor.Execute(deviceKey, input)` returning a `TssCliResult` with output, prompt mode, whether state changed, and validation errors.
- Support the curriculum command set:
  - Modes: `enable`, `configure terminal`, `exit`, `end`, `do`, contextual `?`, and partial-command help.
  - Hardening: `hostname`, `enable secret`, `service password-encryption`, `no ip domain-lookup`, delimiter-based `banner motd`, `line console 0`, `line vty 0 4`, `password`, and `login`.
  - VLANs: `vlan`, `name`, and `state suspend`.
  - Interfaces/ranges: access mode/VLAN, trunk mode, allowed VLAN list, native VLAN, `shutdown`, and `no shutdown`.
  - Router: physical and subinterface selection, `encapsulation dot1q`, `ip address`, and static/default routes.
  - Verification: `show running-config`, `show startup-config`, `show vlan brief`, `show interfaces trunk`, `show ip interface brief`, `show interfaces`, `show mac address-table`, `show ip route`, and `ping`.
  - Persistence: `copy running-config startup-config`.
- Enforce command mode and syntax. Reject malformed IPs, invalid masks, duplicate/out-of-range VLAN IDs, and structurally invalid commands. Accept syntactically valid but incorrect student values without correction.
- Maintain separate running and startup configurations. The save command copies the running snapshot; checkpoint validation requires them to match.
- Add `TssNetworkReachabilityEvaluator.Evaluate(source, destination)` returning reachability, failed layer, trace reason, and hops:
  - Layer 1: effective physical path and administrative interface state.
  - Layer 2: VLAN existence/state, access membership, trunk allowed list, native agreement, and switch continuity.
  - Layer 3: source addressing, subnet/gateway, router subinterface/dot1q/addressing, WAN /30, and default route.
- This is deterministic topology evaluation, not a packet-level simulator. Failed pings report the first broken layer without revealing the correct SOP value.

## UI, Prefab, and Scene Changes

### Terminal canvas

- Update [TssTerminalSessionPresenter.cs](C:/Unity/TSS2/Assets/Scripts/TssTerminalSessionPresenter.cs) so it only submits typed commands and renders processor results; it must not own network truth.
- Replace the SidebarPanel’s quick-command buttons and live interface/status rows with a non-interactive command reference grouped by the current CLI mode.
- Keep all configuration and verification actions in the terminal. The sidebar may show syntax, but never live VLAN, interface, peer, route, or correctness data.
- Widen the console while retaining a narrow reference panel.
- Add:
  - Command history with Up/Down navigation.
  - Persistent focus and scroll-to-bottom behavior.
  - A typed-line Enter button for iPad use; it executes the input but inserts no command automatically.
  - Cisco-like invalid/incomplete/ambiguous command responses.
- Remove serialized quick-command button references and rebuild the prefab through [TssDeviceSessionPrefabBuilder.cs](C:/Unity/TSS2/Assets/Editor/TssDeviceSessionPrefabBuilder.cs), preserving the scene’s existing controller/presenter references.

### Desktop session canvas

- Preserve one prefab with category-specific content:
  - Desktop PC: command-prompt application supporting `ipconfig`, `ping`, `tracert`, and `cls`.
  - Server: adapter and service state derived from authoritative network state.
  - Printer: address/status/test-page UI derived from actual Layer 1/2/3 availability.
- Endpoint settings must commit only on Apply and pass address/mask validation. DHCP/static changes update session state and raise `NetworkStateChanged`.
- Remove the detailed physical-path disclosure. Diagnostic information must come from `ipconfig`, `ping`, `tracert`, and layered screen clues.
- Remove hard-coded success:
  - Pings use the reachability evaluator and target address.
  - `tracert` displays only reachable hops and the first failed layer.
  - Printer jobs fail or queue when connectivity is insufficient.
  - Server client/service displays derive from reachable configured endpoints.

### Runtime binding and indicators

- Change `TssPortEndpointBinder.ConfigureEndpoints` to collect configured endpoints and notify components implementing a small `ITssEndpointBindingConsumer` interface after binding finishes.
- Have `TssDeviceLinkIndicator` and `TssComputerConnectivityPresenter` bind from that callback, using endpoint suffix mappings without altering serialized endpoint IDs.
- Change LED rendering to `MaterialPropertyBlock`; do not instantiate materials through `renderer.material`.
- LED behavior remains realistic:
  - Off/disconnected when Layer 1 is absent or the interface is administratively shut.
  - On when physical carrier and administrative state are up.
  - Wrong VLAN or routing configuration does not extinguish a valid physical link LED.
- Both indicators subscribe to physical-session and network-state events and update without closing/reopening the device canvas.

### Third-person monitor art

- During implementation, use the image-generation workflow to create six consistent 1024×576 high-contrast diagnostic screen assets:
  - `monitor_layer1_connected.png`
  - `monitor_layer1_disconnected.png`
  - `monitor_layer2_connected.png`
  - `monitor_layer2_disconnected.png`
  - `monitor_layer3_connected.png`
  - `monitor_layer3_disconnected.png`
- Use an in-world operating-system diagnostic style with clear layer labels/icons but no VLAN IDs, IP answers, passwords, or corrective instructions.
- Extend `MonitorTextureRotator` with a layered sequence API. Each desktop cycles through three images—one selected from each connected/disconnected pair—so every layer’s current state is visible:
  - L1 failure: all three disconnected.
  - L1 up/L2 failure: L1 connected, L2/L3 disconnected.
  - L2 up/L3 failure: L1/L2 connected, L3 disconnected.
  - Fully operational: all three connected.
- Import with sRGB, clamp wrapping, mipmaps suitable for a 3D monitor, and assign the assets to placed desktop prefabs. Preserve the existing material slots and renderer references.

## Work-Package Evaluation

- Add `TssNetworkWorkPackageEvaluator` with structured results for future checkpoint/sign-off UI.
- WP2 requires:
  - AN-1 and AN-2 exact hardening values.
  - All five VLANs on both switches, exact names, Dead suspended.
  - AN-1: Fa0/2 and Fa0/5 Management; Fa0/3-4 Reception; Fa0/1 and Fa0/6 trunks; Fa0/7-8 Dead and shut.
  - AN-2: Fa0/2-4 Data; Fa0/1 trunk; Fa0/5-8 Dead and shut.
  - Trunks carry the three user VLANs only and agree on native VLAN.
  - Both startup configurations match running state.
  - Command evidence records `show vlan brief`, `show interfaces trunk`, and save on both switches.
- WP3 requires:
  - CN-1 exact hardening.
  - Fa0/0 administratively up.
  - One correctly tagged/addressed subinterface for each user VLAN.
  - Fa0/1 configured with the customer /30 address and up.
  - Default route pointing to the provider.
  - Endpoint-sourced successful pings to own gateway, cross-VLAN host, and provider side.
  - `show ip route` evidence and saved router configuration.
- Router-originated pings do not satisfy endpoint verification.
- Evaluators return unmet conditions for instructor/system use but do not auto-correct or expose answers to the learner.

## Implementation Sequence

1. Save this plan and protect the dirty worktree; do not rewrite unrelated Unity YAML or current termination/panel work.
2. Add scenario template/profile generation, session-owned state, deterministic IDs/MACs, reset behavior, and unit tests.
3. Implement CLI parsing and real configuration mutation, including running/startup snapshots and command evidence.
4. Implement Layer 1/2/3 reachability and work-package evaluators with deliberate fault cases.
5. Refactor terminal and desktop presenters into display/input adapters.
6. Fix endpoint-binding lifecycle, LED updates, and monitor connectivity updates.
7. Generate and import the six monitor images.
8. Update the prefab builder, regenerate the four device-session/first-person prefabs, and verify existing scene bindings.
9. Configure the Balanced lab network template and generated SOP display.
10. Run full Unity compilation, edit-mode tests, Play Mode validation, and visual inspection.

## Test and Acceptance Plan

### Edit-mode tests

- Same seed produces the same profile; different seeds produce valid unique VLANs, subnets, WAN addresses, and credentials.
- `BeginScenario` clears prior network state and generates exactly one new profile.
- Required CLI commands mutate only valid state and respect CLI modes.
- Valid-but-wrong values persist; malformed values are rejected.
- Running/startup configurations diverge before save and match after copy.
- Show commands exactly reflect current state.
- Router aliases map to preserved physical `GE00`/`GE01` endpoints.
- Reachability succeeds for the correct WP2/WP3 topology.
- Individual faults identify the expected first failed layer: cable, shutdown, missing VLAN, wrong access VLAN, missing trunk VLAN, native mismatch, missing gateway, wrong dot1q, wrong WAN address, and missing default route.
- Endpoint binding resolves runtime-prefixed IDs after dynamic installation.
- LED and monitor snapshots follow the defined precedence.

### Play Mode validation

- Install/name/cable AN-1, AN-2, CN-1, IT1/IT2, Reception1/2, and Data1/2/3.
- Configure both switches entirely through typed terminal commands.
- Confirm no quick-command controls or live answer-revealing sidebar values remain.
- Verify both switch states with typed show commands and save them.
- Configure router hardening, subinterfaces, WAN, and default route through typed commands.
- From IT1 and Reception1, run the required endpoint pings and `tracert`; verify router-sourced pings do not count.
- Introduce and repair representative Layer 2 and Layer 3 mistakes without scene reload.
- Confirm LEDs react to cable/admin changes while remaining lit for higher-layer errors.
- Confirm desktop monitors rotate through the correct three chosen layer images after every physical or logical state change.
- Close and reopen canvases: state persists. Restart the scenario: state resets and a new profile is generated.
- Verify Desktop, Server, and Printer category-specific canvases use real session state.
- Inspect at 1280×800 and representative 4:3 iPad resolutions for readable terminal text, input focus, touch targets, and no clipping.
- Confirm Unity compiles, existing edit-mode tests still pass, the scenario loads, physical termination remains functional, and no repeated state-change/log storm occurs.

## Expected Logs and Rollback

- Expected logs: one sanitized network-profile/run identifier at scenario start and actionable warnings for unresolved interface mappings.
- Never log credentials, generated answers, every command, or per-frame status.
- If rollout must be disabled, set `enableLayer23Simulation` false on the scenario. The existing Layer 1 physical graph and first-person device access remain intact while the new state service, evaluators, and layered presentation stay dormant.
- Do not delete the existing Layer 1 fallback, rename protected endpoint IDs, or modify inspector-authored device/hierarchy names during implementation.
