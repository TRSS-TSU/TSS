# Layer 2/Layer 3 Device Session UI and Network State Implementation Plan

## 1. Purpose and Implementation Outcome

Replace the current mostly presentational Layer 2/Layer 3 device-session behavior with one deterministic network state shared by:

- The router and switch CLI canvases.
- The desktop, server, and printer session canvas.
- Physical link LEDs.
- Third-person workstation monitor screens.
- Ping and traceroute diagnostics.
- Layer 2 and Layer 3 work-package evaluation.

Router and switch work must be completed by typing commands into `TssTerminalSessionCanvas`. Remove the clickable Quick Commands and live answer-revealing interface status from its `SidebarPanel`. Retain only a non-interactive, mode-aware syntax reference.

The implementation must preserve the existing Layer 1 placement, cable, termination, first-person inspection, and effective-path systems. Valid but incorrect student configuration must remain possible; only malformed commands and invalid addresses are rejected.

Requirements come from:

- [Student capstone instructions (3).pdf](../Docs/Student%20capstone%20instructions%20(3).pdf), especially Work Packages 2 and 3 and the command reference.
- [Net Capstone Instructor_Guide (2).pdf](../Docs/Net%20Capstone%20Instructor_Guide%20(2).pdf), especially Phases 9-19, checkpoint behavior, seeded values, and failure diagnostics.

Scope touches Scenario/Data, network intent/state, evaluation, and display. It does not replace cable routing, termination, equipment placement, world scale, or the existing first-person interaction model. Wireless configuration, checkpoint-signature UI, completion reports, and application-restart persistence remain follow-on work.

## 2. Confirmed Current Project State

### 2.1 Live Unity baseline

- Unity `6000.3.21f1` is connected to `C:\Unity\TSS2`, idle, and compiling cleanly during planning.
- The active scene is `Assets/Scenes/TSS.unity`; it is loaded and not dirty.
- `DeviceSessionManager` exists and its `TssDeviceSessionController` currently references:
  - `TssTerminalSessionCanvas`
  - `TssDesktopSessionCanvas`
  - `TssTerminalFirstPersonControls`
  - `TssDesktopFirstPersonControls`
  - The inactive scene instances of both presenters
- Both session canvases exist as inactive scene objects and as prefabs. Their principal serialized UI references are currently populated.
- The scene contains 13 `TssDeviceLinkIndicator` components, 13 `TssComputerConnectivityPresenter` components, and 13 `MonitorTextureRotator` components.
- All 13 monitor rotators currently have empty `screens` arrays. All 13 connectivity presenters currently have no serialized endpoint reference, although their offline texture is assigned.
- The Balanced lab scenario contains five SOP sections, but the Phase 9-15 and Phase 16-19 sections are placeholder text stating that Layer 2/3 instructions will be added later.
- The worktree already contains extensive unrelated modified and untracked Unity work. Implementation must preserve it and stage only the intended Layer 2/3 work.
- Existing Console warnings observed during inspection are from the Unity MCP serializer and unavailable prefab previews, not project compile errors. Do not treat those tool warnings as gameplay faults.

### 2.2 Current terminal behavior that must be replaced

- `TssTerminalSessionPresenter` owns the CLI mode and directly interprets commands.
- `hostname` changes only `TssDeviceLogicalState.DeviceName`.
- `shutdown`, `no shutdown`, `ip address`, VLAN, switchport, trunk, route, hardening, and save commands do not mutate authoritative session state.
- `show running-config` is synthesized from shallow defaults rather than the player’s full configuration.
- `show vlan brief` always reports VLAN 1.
- `show ip route` infers `/24` connected routes from physically connected interfaces rather than configured routing state.
- CLI ping succeeds against any target whenever any interface has a direct physical connection.
- The right sidebar displays live interface/peer information and exposes seven buttons that execute commands without typing them.
- The prefab builder creates this sidebar and wires all seven Quick Command fields, so changing only the prefab would be overwritten by the next rebuild.

### 2.3 Current desktop/server/printer behavior that must be replaced

- `TssDesktopSessionPresenter` edits a static `TssDeviceLogicalState` cache rather than `TssTrainingSession` state.
- DHCP/static toggle callbacks mutate state before Apply.
- Apply accepts unvalidated IP addresses, masks, gateways, and DNS values.
- Ping checks only the first interface’s direct physical-link flag and ignores the requested target, VLANs, gateways, trunks, subinterfaces, WAN state, and routes.
- Server roles, DHCP leases, printer readiness, and test-print success contain hard-coded values.
- The Physical Path card reveals cable peers and effective infrastructure paths instead of requiring the player to diagnose through endpoint commands.
- The current UI provides ping, gateway, and ipconfig buttons rather than the command-prompt workflow required by the instructor guide.

### 2.4 State and execution-workflow defects

- `TssDeviceLogicalState` uses a static cache that `TssTrainingSession.BeginScenario` never clears, allowing state to leak between runs.
- State keys are based on rack position or placement ID. Without explicit cleanup, picking up one device and installing another at the same position can inherit stale state.
- `string.GetHashCode()` is used for MAC generation and is not a suitable stable cross-run hash.
- Logical interface link state is based on any direct connection record, while scene LEDs use `IsEndpointOnEffectivePhysicalPath`; the canvas and scene can disagree.
- `TssPortEndpointBinder.ConfigureEndpoints` dynamically adds/configures endpoints after instantiated components may already have run `Awake` and `OnEnable`. It does not notify consumers afterward.
- `TssComputerConnectivityPresenter` therefore remains unbound on the current 13 workstation objects unless an endpoint happened to exist earlier.
- Runtime endpoint IDs are prefixed, for example `Rack:Rack01:U35:Fa01`, but LED bindings store suffixes such as `Fa01`. `TssDeviceLinkIndicator` compares exact strings and fails to resolve these bindings.
- The switch LED prefab has the expected Fa01-Fa24 bindings, the router has paired GE00/GE01 bindings, and the printer uses Eth01. The server prefab’s 12 LED renderer bindings all reference GE00 while `EQ_SERVER_2U` declares its data endpoint as GE01; this binding mismatch must be corrected to GE01 while preserving its renderer/material assignments.
- `TssDeviceLinkIndicator` assigns `renderer.material`, which creates material instances.
- `MonitorTextureRotator` already uses `MaterialPropertyBlock` correctly but has no layer-aware screen API.
- `TssDeviceSessionController.ResolveContext` reads private fields from `EndpointPlacedEquipmentTarget` through reflection. This is fragile and unnecessary; expose read-only context properties instead.
- The router equipment definition intentionally resolves physical endpoints named `GE00` and `GE01`, while the curriculum calls them Fa0/0 and Fa0/1. The GE01 anchor currently resolves to a child named `GE00` under the GE01 cavity. It resolves successfully and must not be renamed casually; use an explicit logical alias map and verify the binding.
- Existing edit-mode tests confirm basic presenter prompts and default UI values but do not verify authoritative configuration, reset behavior, routing, or failure layers.

## 3. Capstone Requirements to Encode

### 3.1 Generated run profile and Binder/SOP values

Generate one network profile when `BeginScenario` starts the Balanced lab and retain it for that run. Reopening a canvas must not regenerate it. Starting the scenario again creates a new profile.

The profile contains:

- Five unique VLAN IDs for Management, Reception, Data, Native, and Dead.
- Exact VLAN names for the run; `Dead_Vlan` is always exact and case-sensitive.
- Three unique private `/24` user subnets with `.1` gateways.
- Deterministic host addresses for IT1, IT2, Reception1, Reception2, Data1, Data2, and Data3.
- A private `/30` WAN with separate customer-side and provider-side addresses.
- One enable secret, console password, VTY password, and MOTD value shared across AN-1, AN-2, and CN-1 for that run.
- A stored integer seed so tests can reproduce a profile and the active run can display the same values consistently.

Use `System.Random` with the stored seed, not Unity global random state. Provide a fixed-seed generation method for tests. Do not log credentials or correct network answers.

The project decision remains “generated per run,” not student-ID-derived. Record this as a known deviation from the instructor guide’s last-name/computer-number seed model in the implementation pass-off.

Render the profile into the existing SOP/Binder panel by replacing the Phase 9-15 and Phase 16-19 placeholders at runtime. The visible documentation must include the generated values needed to perform the tasks, while the terminal sidebar remains syntax-only.

### 3.2 Work Package 2: Layer 2

AN-1 must satisfy:

- Hostname `AN-1`.
- Correct generated enable secret, console password, and VTY password.
- `login` on console and VTY lines.
- `service password-encryption`.
- `no ip domain-lookup`.
- Exact generated MOTD/banner requirement.
- All five VLANs present with exact names; Dead is suspended.
- Fa0/2 and Fa0/5: Management access VLAN.
- Fa0/3 and Fa0/4: Reception access VLAN.
- Fa0/1: inter-switch trunk.
- Fa0/6: router trunk.
- Both trunks: allowed list contains Management, Reception, and Data only.
- Both trunks: generated Native VLAN configured as native and not added to the tagged allowed list.
- Fa0/7 and Fa0/8: Dead access VLAN and administratively shut at the Phase 15 checkpoint.

AN-2 must satisfy:

- The same hardening baseline with hostname `AN-2`.
- The same complete five-VLAN database; trunks do not copy VLAN definitions.
- Fa0/2 through Fa0/4: Data access VLAN.
- Fa0/1: inter-switch trunk with the same allowed list and native VLAN as AN-1.
- Fa0/5 through Fa0/8: Dead access VLAN and administratively shut.

Phase 15 evidence requires, on both switches:

- `show vlan brief`
- `show interfaces trunk`
- `copy running-config startup-config`
- Startup configuration matching the current running state at checkpoint evaluation.

Only Fa0/1 through Fa0/8 participate in this Layer 2 grading pass. Fa0/9 through Fa0/24 remain valid physical ports but are outside the current work-package evaluator.

The future wireless phase deliberately reuses AN-1 Fa0/7 and re-enables it in Reception. Record WP2 completion as a Phase 15 checkpoint snapshot so a correct later Phase 20 change does not retroactively invalidate completed Layer 2 work.

### 3.3 Work Package 3: Layer 3

CN-1 must satisfy:

- The same hardening baseline with hostname `CN-1`.
- Physical Fa0/0 administratively up.
- One Fa0/0 subinterface per user VLAN: Management, Reception, and Data.
- Each subinterface uses the correct `encapsulation dot1q` VLAN and generated `/24` gateway.
- Fa0/1 uses the customer-side address with mask `255.255.255.252` and is administratively up.
- A default route `0.0.0.0 0.0.0.0` points at the provider-side address.

Phase 19 evidence requires:

- IT1 ping to the Management gateway.
- IT1 ping to a Data host.
- Reception1 ping to a Data host across the inter-switch trunk.
- A workstation ping to the provider-side address.
- `show ip route` on CN-1.
- `copy running-config startup-config` on CN-1.
- The routing table shows the three connected user subnets and the static default route.

Router-originated pings may be supported for diagnosis but do not satisfy endpoint verification evidence.

## 4. Runtime Architecture and Data Model

### 4.1 Scenario configuration

Extend `TssScenarioDefinition` with one optional serialized `NetworkScenarioConfig` field rather than creating a separate ScriptableObject asset. The field contains:

- `enableLayer23Simulation` rollout flag.
- VLAN role ranges and naming rules.
- LAN and WAN address-generation ranges.
- Device-role bindings for AN-1, AN-2, CN-1, IT1, IT2, Reception1/2, and Data1/2/3.
- CLI-name-to-physical-endpoint mappings.
- Expected switch port roles and work-package evidence requirements.

Configure this field only on `Balanced_12_Piece_Lab.asset` for the first implementation. A scenario without network configuration keeps the existing Layer 1 behavior and does not create network state.

Resolve device roles from the scenario’s expected physical placement plus equipment category, not from a player-entered name. A wrong displayed name or CLI hostname must remain a gradeable error without making the underlying device impossible to identify.

### 4.2 Session-owned state

Replace the static cache in `TssDeviceLogicalState` with a `TssNetworkSessionState` owned by `TssTrainingSession`.

`TssTrainingSession` exposes:

```csharp
public TssNetworkRunProfile NetworkProfile { get; }
public TssNetworkSessionState NetworkState { get; }
```

The runtime state contains the minimum structured truth needed to render configuration and evaluate the lab:

- `NetworkDeviceState`: stable runtime key, expected role, equipment category, configured hostname, hardening values/flags, VLAN database, interfaces, routes, startup snapshot, and command evidence.
- `NetworkInterfaceState`: canonical CLI name, physical endpoint suffix/full ID, admin state, carrier state, switchport mode, access VLAN, allowed VLAN set, native VLAN, IPv4/mask, and dot1q VLAN.
- `EndpointNetworkState`: stable endpoint role/key, MAC, DHCP/static selection, IPv4 address, mask, gateway, and DNS.
- A compact `NetworkCommandEvidence` set keyed by device and required command type.

Structured fields are the source of truth. Do not maintain a separate editable running-config string. Render `show running-config` from structured state and deep-copy structured state into the startup snapshot when the save command succeeds.

Defaults:

- Switch physical ports are administratively up until the player shuts them.
- Router physical interfaces are administratively down until `no shutdown`.
- Newly created switchports start as access VLAN 1.
- Endpoint NICs start enabled and receive their scenario-profile address, mask, and gateway.
- No general DHCP-server simulation is required for WP2/WP3. Preserve the DHCP/static UI choice, but a DHCP choice must not fabricate leases or hard-coded server clients. Treat scenario-provided endpoint addresses as the initial effective configuration.

Reset and lifecycle rules:

- `BeginScenario` creates exactly one profile and fresh network state after clearing the previous run.
- Closing/reopening a device canvas preserves current run state and CLI configuration.
- Renaming a placed/racked object updates display metadata without changing its stable network key or expected role.
- The `hostname` command changes configured network hostname only; it does not silently fix the physical placement name.
- Successfully picking up equipment clears that device’s network state before the slot/placement can be reused.
- Starting a new scenario clears running configuration, startup snapshots, evidence, addresses edited by the player, and checkpoint results.
- Use a small stable hash such as FNV-1a over the run seed plus device/interface key to generate locally administered MAC addresses; do not use `string.GetHashCode()`.

Reuse `TssTrainingSession.StateChanged` as the single refresh notification for physical and network changes. At the current project scale, a payload-less refresh is simpler than a parallel network event. Emit it once per accepted state-changing command or settings Apply, never per frame.

### 4.3 Device context and interface aliases

- Add read-only `Station`, `PlacementId`, and `Equipment` properties to `EndpointPlacedEquipmentTarget` and remove the reflection lookup from `TssDeviceSessionController`.
- Preserve all existing endpoint IDs, port anchor paths, and Hierarchy names.
- Canonicalize CLI input case-insensitively and accept expected abbreviations:
  - Switch: `FastEthernet0/1`, `Fa0/1`, and `fa0/1` map to endpoint suffix `Fa01`.
  - Router: curriculum `FastEthernet0/0`/`Fa0/0` map to protected endpoint `GE00`.
  - Router: curriculum `FastEthernet0/1`/`Fa0/1` map to protected endpoint `GE01`.
- Keep console and auxiliary endpoints out of data-plane evaluation.
- Validate every declared alias against a resolved physical endpoint when equipment is installed. Warn once for an unresolved mapping without changing the protected asset automatically.

## 5. CLI Command Processor

### 5.1 Separation from the presenter

Move command interpretation out of `TssTerminalSessionPresenter`. The presenter becomes an adapter that submits text, appends returned output, renders the prompt, maintains UI history/focus, and closes the canvas.

Add `TssCliCommandProcessor` with the functional boundary:

```csharp
TssCliResult Execute(string deviceKey, string input)
```

`TssCliResult` includes:

- Output text.
- Resulting prompt/mode.
- Whether authoritative state changed.
- Whether a required verification/save evidence flag was recorded.
- Parse/validation status suitable for tests without requiring UI text matching.

Maintain mode and current selections per open device session:

- User EXEC
- Privileged EXEC
- Global configuration
- Interface or interface-range configuration
- Line configuration
- VLAN configuration

### 5.2 Required command surface

Navigation and help:

- `enable`, `disable`
- `configure terminal` and common `conf t` abbreviation
- `exit`, `end`
- `do <exec-command>` from configuration modes
- `?` and `<partial>?` contextual help

Hardening:

- `hostname <name>`
- `enable secret <password>`
- `service password-encryption`
- `no ip domain-lookup`
- Delimiter-based `banner motd`
- `line console 0`
- `line vty 0 4`
- `password <password>`
- `login`

VLANs and switchports:

- `vlan <id>`
- `name <name>`
- `state suspend`
- `interface <name>`
- `interface range <interface> - <end>` for the capstone’s contiguous ranges
- `switchport mode access`
- `switchport access vlan <id>`
- `switchport mode trunk`
- `switchport trunk allowed vlan <comma-list>`
- `switchport trunk native vlan <id>`
- `shutdown`, `no shutdown`

Router configuration:

- Physical and subinterface selection.
- `encapsulation dot1q <vlan>`
- `ip address <address> <mask>`
- `ip route <network> <mask> <next-hop>` including the default-route form.

Verification and persistence:

- `show running-config`
- `show startup-config`
- `show vlan brief`
- `show interfaces trunk`
- `show ip interface brief`
- `show interfaces`
- `show mac address-table`
- `show ip route`
- `ping <address>`
- `copy running-config startup-config` and common `copy run start` abbreviation

### 5.3 Validation and player-error policy

- Enforce command availability by device category and CLI mode.
- Distinguish invalid, incomplete, and ambiguous input.
- Reject malformed IPv4 strings, octets outside 0-255, and non-contiguous masks without changing state.
- Reject VLAN IDs outside 1-4094 and structurally invalid/empty VLAN lists.
- Accept syntactically valid but incorrect VLAN IDs, names, IPs, masks, next hops, hostnames, banners, and credentials.
- For valid but incorrect credentials, store the value and print a neutral capstone note that the requirement is not satisfied; never reveal the expected value.
- Do not implement a real login lockout in this phase. Configured credentials are evaluated configuration state, not a mechanism that can block the learner from reopening the terminal.
- Applying a command to an interface range updates the whole selected range atomically after the range is validated.
- A failed parse or validation must leave running state, startup state, and evidence unchanged.

### 5.4 State-derived show and ping output

- `show running-config` and `show startup-config` render their respective structured snapshots.
- `show vlan brief` lists every configured VLAN, exact status, and access ports; trunk ports are not shown as access members.
- `show interfaces trunk` displays locally configured/operational trunks, native VLAN, and allowed VLANs from state.
- `show ip interface brief` distinguishes administratively down, physical down, and up/up states.
- `show ip route` displays connected/local routes only for operational configured interfaces/subinterfaces and displays `S*` for the configured default route. A syntactically valid but unreachable next hop may still appear in configuration while reachability fails.
- MAC-table output uses deterministic MAC values and only learned/reachable local Layer 2 peers; no random hashes.
- Ping validates the requested target and calls the shared reachability evaluator. It may succeed only when that source-to-destination path succeeds.
- Required command evidence is recorded only when the player typed and executed the command in the correct device context. Sidebar rendering, initialization, and automated evaluator calls do not count.

## 6. Deterministic Reachability and Work-Package Evaluation

### 6.1 Reachability API

Add `TssNetworkReachabilityEvaluator` with:

```csharp
TssReachabilityResult Evaluate(string sourceKey, string destinationAddress)
```

The result contains:

- Success/failure.
- First failed layer: 1, 2, or 3.
- A short diagnostic reason that does not disclose the correct value.
- Resolved source and destination roles.
- Ordered trace hops for endpoint `tracert`.

This is a deterministic topology evaluator, not packet simulation, ARP timing, spanning-tree simulation, or a routing-protocol engine.

### 6.2 Evaluation order

Layer 1 checks:

- Source endpoint exists and has a usable data endpoint.
- A complete effective physical path exists through the current player/permanent connection graph.
- Every participating configured network interface is administratively up.
- Router subinterfaces inherit the parent physical interface’s carrier/admin state.

Layer 2 checks:

- The source access VLAN exists and is active on its switch.
- Each access port is assigned to the required VLAN for the endpoint role.
- Each traversed trunk is configured as a trunk on both sides.
- The source VLAN is present in every required allowed list.
- The native VLAN agrees across each trunk. Treat a mismatch as a Layer 2 failure for this training model, matching the instructor guide’s expected Phase 19 behavior.
- The destination-side access VLAN and switch continuity are valid.
- Suspended Dead VLAN ports do not forward.

Layer 3 checks:

- Source/destination addresses and masks are valid and non-conflicting.
- The source default gateway is in the source subnet and matches the configured gateway subinterface for routed traffic.
- The router parent interface is operational.
- Required subinterface encapsulation VLAN and gateway address/mask are correct.
- Inter-VLAN destinations have a valid destination-side return path.
- Provider reachability requires correct WAN `/30` addressing, operational Fa0/1, and the default route pointing at the provider.
- Valid unknown addresses that are not declared profile endpoints/provider targets time out; do not invent an internet host.

Failure output names the layer and fault category, for example “Layer 2: VLAN is not carried across the trunk,” but never prints the VLAN or address the student should enter.

### 6.3 Endpoint commands and monitor health

- Endpoint `ping` uses the same evaluator and records evidence only when the source endpoint and destination match a required Phase 19 test.
- Endpoint `tracert` prints only the hops reached before success/failure and then the same first-failure layer clue.
- Define workstation monitor state consistently:
  - L1 connected: the workstation NIC has a complete physical path to its access switch and relevant interfaces are administratively up.
  - L2 connected: its assigned VLAN can traverse the required switching path to the router trunk.
  - L3 connected: the workstation can reach its own configured gateway.
- Enforce monotonic display state: an L1 failure forces L2/L3 disconnected; an L2 failure forces L3 disconnected.

### 6.4 Work-package evaluator

Add a `TssNetworkWorkPackageEvaluator` that returns structured unmet conditions for Layer 2 and Layer 3 without displaying correct answers to the learner.

Layer 2 completion requires:

- Exact AN-1/AN-2 hardening state.
- Exact VLAN databases and port roles listed in Section 3.2.
- Correct trunk agreement and allowed lists.
- Required typed show-command evidence.
- Saved startup snapshots matching running configuration.

Layer 3 completion requires:

- Exact CN-1 hardening and router configuration listed in Section 3.3.
- Successful endpoint-originated evidence for every required ping category.
- Typed `show ip route` evidence.
- Saved startup snapshot matching running configuration.

Expose evaluator results for future phase/checkpoint UI, but do not build instructor initials, timers, phase jumping, or completion reports in this implementation. When a work package is confirmed at its checkpoint, store a completion snapshot so later authorized changes do not invalidate it.

## 7. UI and Prefab Changes

### 7.1 Terminal canvas

Update `TssDeviceSessionPrefabBuilder` first and rebuild `TssTerminalSessionCanvas`; do not hand-edit a prefab that the builder will overwrite.

- Remove `QuickCommandsTitle`, `QuickCommands`, all seven quick buttons, their serialized presenter fields, and click listeners.
- Remove the live interface list and live routing/VLAN summary that reveal state outside typed show commands.
- Retain `SidebarPanel` as a narrow, non-interactive Command Reference.
- Populate the reference from the current device type and CLI mode. Show command syntax only; never show profile values, configured state, peers, routes, or correctness.
- Expand the terminal viewport into the freed width while retaining enough sidebar width for readable syntax.
- Add command history with Up/Down navigation.
- Keep scroll-to-bottom and restore input focus after each command.
- Keep a touch-friendly Submit/Enter button that submits exactly what the player typed and does not inject commands.
- Preserve Escape/close behavior and the gameplay input lock.
- Do not clear console history when the canvas merely loses focus during the same open session.
- Validate layout at 1280x800 and representative 4:3 iPad resolutions with no clipped prompt, command input, scrollbar, or reference content.

### 7.2 Desktop session canvas

Keep one `TssDesktopSessionCanvas` prefab with category-specific content.

Common network settings:

- Load current endpoint state when opened.
- Toggle changes remain local UI edits until Apply.
- Apply validates the complete candidate configuration first; any invalid field rejects the whole update and leaves authoritative state unchanged.
- A successful Apply updates session state once, emits one `StateChanged`, refreshes visible status, and preserves the values after close/reopen.
- Remove detailed physical/effective path disclosure.

Desktop PC:

- Replace the ping/gateway/ipconfig quick buttons with a small command-prompt input, Submit button, scrollable output, and command history.
- Support `ipconfig`, `ping <address>`, `tracert <address>`, and `cls` exactly as the student command reference specifies.
- Route these commands through a small `TssEndpointCommandProcessor.Execute(sourceKey, input)` so the presenter formats/displays results but does not calculate networking.
- Use current endpoint state and shared reachability; do not provide a “ping gateway” shortcut.

Server:

- Show adapter addressing and physical/layer reachability from authoritative state.
- Remove fabricated DHCP leases and hard-coded service clients.
- Do not build a general DHCP/DNS/HTTP service simulator for WP2/WP3. Display only explicitly configured/derived availability.

Printer:

- Show its actual address and layered online/offline state.
- A test page completes only if the printer itself is locally operational and the initiating workstation can reach it; otherwise retain/mark the failed job with a layer clue.
- Remove unconditional success and fixed toner/network claims not backed by state.

### 7.3 Prefab and scene binding preservation

- Treat `TssDeviceSessionPrefabBuilder` as the source of truth for both session canvases and the two first-person control prefabs.
- Regenerate only these four prefabs after builder changes.
- Preserve `DeviceSessionManager` references to both canvases, both first-person controls, and both scene presenters.
- Keep the canvas scene instances inactive until opened.
- Preserve current EventSystem, canvas sorting, gameplay-lock, rename, pickup, and return-to-third-person behavior.
- Verify all presenter serialized references after regeneration; do not rely on object-name lookup as a substitute for required bindings.

## 8. Endpoint Binding, LEDs, and Third-Person Screens

### 8.1 Post-bind notification

Change `TssPortEndpointBinder.ConfigureEndpoints` to collect every configured endpoint, then notify interested components after the loop completes.

Use a small `ITssEndpointBindingConsumer` interface because both `TssDeviceLinkIndicator` and `TssComputerConnectivityPresenter` require the callback. The callback receives the resolved endpoint list and owner prefix. Do not make consumers search before binding is complete.

Binding rules:

- Prefer an explicitly serialized endpoint reference when valid.
- Otherwise resolve a binding suffix by exact endpoint segment: full ID equals the suffix or ends with `":" + suffix`.
- Never rewrite the serialized endpoint ID or physical Hierarchy name during resolution.
- `TssComputerConnectivityPresenter` selects the declared data interface, not Aux or Console.
- Warn once per unresolved declared binding and remain safely disconnected.
- Change the server prefab’s demonstrably inconsistent GE00 LED binding suffixes to GE01, matching `EQ_SERVER_2U`; do not alter its renderer or material references.

### 8.2 LED behavior

- Off when there is no effective Layer 1 path.
- Off when that interface is administratively shut.
- On when carrier and admin state are up.
- Remain on for a valid physical link with wrong VLAN, trunk, IP, gateway, or route configuration.
- Refresh on the existing session state event after cable and CLI changes.
- Replace `renderer.material` with `renderer.sharedMaterial` when swapping the existing connected/disconnected material assets. Use `MaterialPropertyBlock` only for per-renderer color/texture overrides; do not copy full materials into new runtime instances.

### 8.3 Six workstation monitor images

Create six consistent `1024x576` PNG assets during implementation:

- `monitor_layer1_connected.png`
- `monitor_layer1_disconnected.png`
- `monitor_layer2_connected.png`
- `monitor_layer2_disconnected.png`
- `monitor_layer3_connected.png`
- `monitor_layer3_disconnected.png`

Art direction:

- High-contrast, in-world operating-system diagnostic screens readable in third-person view.
- One consistent visual family, with a clear layer number/name, connected/disconnected iconography, and restrained status text.
- No generated VLAN IDs, addresses, credentials, exact command solutions, or step-by-step repair instructions.
- Connected and disconnected variants must remain distinguishable at distance without relying only on red/green color.

Import settings:

- Sprite (2D and UI), Single mode.
- sRGB enabled.
- Clamp wrap mode.
- Bilinear filtering.
- Mipmaps enabled for the 3D monitor use.
- Maximum size 1024 and high-quality compression appropriate for the active render pipeline.

Extend `MonitorTextureRotator` without replacing its existing renderer/material-index path:

- Keep legacy `screens` behavior for non-network uses.
- Add six serialized layer textures and `SetLayerStates(bool layer1, bool layer2, bool layer3)`.
- Build the active three-screen sequence by selecting one texture from each connected/disconnected pair.
- Continue cycling at the configured interval using the existing `MaterialPropertyBlock`.
- Apply immediately when state changes; do not require reopening the canvas or re-instantiating the workstation.
- Assign the six textures on the source workstation prefab/scene configuration that feeds all 13 monitors, then verify no monitor instance is left with an empty required set.

The six rotating diagnostic screens apply to workstation monitors. Server and printer third-person indicators use their existing appropriate hardware displays/LEDs rather than pretending they have desktop monitors.

## 9. Implementation Sequence

1. Create `codex/layer-2-layer-3-device-sessions` from the current `main` HEAD before editing, preserving every existing worktree change.
2. Capture a pre-change binding inventory for `DeviceSessionManager`, both session canvases, both first-person controls, Balanced lab scenario, equipment endpoint mappings, LED bindings, and workstation monitor components.
3. Add `NetworkScenarioConfig` to `TssScenarioDefinition`, configure the Balanced lab, and implement deterministic per-run profile generation plus SOP/Binder rendering.
4. Add session-owned network state, stable MAC generation, reset/pickup cleanup, role resolution, and startup snapshot behavior. Remove the static logical-state cache from runtime use.
5. Add read-only placed-equipment context properties and remove reflection from `TssDeviceSessionController`.
6. Implement the shared CLI parser/processor, command modes, configuration mutation, show rendering, save behavior, and required command evidence.
7. Implement deterministic Layer 1/2/3 reachability and work-package evaluators, including Phase 15/19 checkpoint snapshots.
8. Refactor terminal and desktop presenters into UI adapters over the shared state/evaluators.
9. Update the prefab builder, remove terminal Quick Commands/live sidebar state, add the static command reference, add the desktop command prompt, and regenerate the four affected UI prefabs.
10. Add post-bind endpoint notification, suffix-aware LED resolution, admin-aware link state, and material-instance-safe LED updates.
11. Generate/import the six monitor images, extend the rotator, and assign/verify all monitor bindings.
12. Run automated, compile, Play Mode, visual, and regression validation from Section 10.
13. Create the implementation pass-off/hotwash document from actual results using Section 11.
14. Review the final scoped diff, commit, and push using Section 12.

## 10. Test and Acceptance Plan

### 10.1 Edit-mode tests

Profile and lifecycle:

- Same supplied seed yields the same VLANs, networks, WAN values, credentials, endpoint addresses, and MAC addresses.
- Different run seeds yield valid unique values within the configured ranges.
- `BeginScenario` produces exactly one profile and clears all previous network state/evidence/checkpoint snapshots.
- Canvas close/reopen does not regenerate or reset state.
- Pickup clears the removed device state; reusing the same rack/placement position cannot inherit it.
- Rename does not change stable device identity or expected role.

CLI parsing/state:

- Required mode transitions, `do`, help, abbreviations, interface aliases, and interface ranges work.
- Router Fa0/0 and Fa0/1 aliases update GE00 and GE01 respectively without renaming physical endpoints.
- Every required hardening, VLAN, switchport, trunk, router, route, and shutdown command mutates the correct structured field.
- Valid but incorrect values persist.
- Invalid IPs, out-of-range octets, non-contiguous masks, invalid VLAN IDs, malformed lists, invalid ranges, and wrong-mode commands leave state unchanged.
- Running/startup state diverges before save and matches after save.
- All show commands reflect current running/startup state.
- Required evidence records only typed commands in the correct source context.

Reachability/evaluation:

- Correct WP2/WP3 topology reaches gateway, cross-VLAN host, cross-switch host, and provider.
- Individual faults return the correct first failed layer for:
  - Missing cable or incomplete permanent path.
  - Administratively shut switch/router interface.
  - Missing/suspended VLAN.
  - Wrong access VLAN.
  - Missing allowed VLAN.
  - Native VLAN mismatch.
  - Missing/wrong endpoint gateway.
  - Wrong subinterface dot1q VLAN.
  - Wrong subinterface gateway/mask.
  - Parent Fa0/0 down.
  - Wrong WAN customer address/mask.
  - Fa0/1 down.
  - Missing/wrong default route.
- Router-originated ping does not satisfy endpoint evidence.
- Phase 15 completion remains recorded when AN-1 Fa0/7 is later legitimately repurposed.

Bindings and presentation:

- Post-bind notification resolves dynamically added endpoints.
- Runtime-prefixed IDs resolve exact suffix segments without false matches.
- The server’s GE01 endpoint drives all intended server link-light renderers after its binding correction.
- Link LED status follows effective physical/admin state but ignores higher-layer errors.
- Monitor screen selection follows the monotonic L1/L2/L3 precedence.
- No runtime material instances are created by repeated LED refreshes.

### 10.2 Unity compilation and existing tests

- Unity recompiles with zero new errors.
- Run all existing EditMode tests, not only new device-session tests.
- Run the new focused network-state/CLI/evaluator tests.
- Inspect Console after compilation and test runs for new errors, repeated warnings, null bindings, or refresh/log storms.
- Treat known Unity MCP serializer/preview warnings separately from project warnings.
- Because the pre-existing worktree has unrelated Unity YAML whitespace findings, run `git diff --check` on the staged/scoped paths and report unrelated failures rather than rewriting them.

### 10.3 Play Mode acceptance walkthrough

Fresh run and SOP:

- Start a fresh Balanced lab and record the generated profile seed.
- Confirm Phase 9-19 SOP/Binder sections display one coherent set of values and remain unchanged after opening/closing every session canvas.
- Confirm a new scenario run resets configuration and generates a new profile.

Layer 2 correct path:

- Install/name/cable AN-1 and AN-2 using the existing Layer 1 workflow.
- Configure both switches entirely by typed terminal commands.
- Confirm the terminal contains no clickable Quick Commands or live answer-revealing sidebar state.
- Read `show vlan brief` and `show interfaces trunk` on both switches and save both configurations.
- Confirm the evaluator reports every Phase 15 condition complete.

Layer 2 error path:

- Deliberately enter a wrong VLAN name/ID, access VLAN, allowed list, native VLAN, and shutdown state.
- Confirm valid wrong values are retained, show output exposes the actual state, and the relevant endpoint trace reports Layer 2 without giving the correction.
- Repair each error without restarting the scene.

Layer 3 correct path:

- Configure CN-1 hardening, Fa0/0 subinterfaces, Fa0/1 WAN, default route, and save entirely through typed terminal commands.
- From IT1, ping its Management gateway and a Data host.
- From Reception1, ping a Data host across the inter-switch trunk.
- From a workstation, ping the provider side.
- Run endpoint `tracert` and router `show ip route`.
- Confirm router-originated pings do not satisfy workstation evidence.
- Confirm the evaluator reports every Phase 19 condition complete.

Layer 3 error path:

- Test missing gateway, wrong mask, wrong dot1q VLAN, missing subinterface, parent interface down, swapped WAN addresses, and missing/wrong default route.
- Confirm each failure reports the first broken layer and repairs take effect immediately.

Desktop/server/printer and visuals:

- Confirm desktop Apply does not mutate state before submission and rejects invalid input atomically.
- Confirm `ipconfig`, `ping`, `tracert`, and `cls` are typed commands with no gateway shortcut.
- Confirm server and printer displays contain no fabricated leases or unconditional success.
- Confirm rack/device LEDs change for cable/admin state and stay lit during VLAN/routing faults.
- Confirm every workstation monitor rotates through the correct selected Layer 1, 2, and 3 images and updates without reopening UI.
- Inspect monitor legibility from normal third-person camera distance.

Regression and device UX:

- Confirm equipment checkout, placement, naming, pickup, cabling, permanent termination, first-person entry/exit, session open/close, and gameplay input locking still work.
- Confirm the physical connection/effective-path systems remain intact.
- Test the canvases at 1280x800 and representative 4:3 iPad resolutions for readable text, touch targets, scrolling, focus, and no clipping.
- Confirm no per-frame allocations/logs or repeated state refresh loop is introduced.

## 11. Implementation Pass-Off and Hotwash Documentation

Create:

`C:\Unity\TSS2\Docs\After Layer 2 Layer 3 Device Session Implementation.md`

Write it from the implementation and verification results that actually occurred. Do not copy future-tense claims from this plan.

### 11.1 Plain-language status

State:

- The implementation goal.
- What was completed.
- What is partial, deferred, failed, or not tested.
- Unity version and scene tested.
- Branch used.
- Compile, automated-test, and Play Mode status.
- Relevant dirty-worktree or environment caveats.

Do not embed the implementation commit SHA in the committed document because a commit cannot contain its own final SHA. Report the final branch and SHA in the implementation handoff response after pushing.

### 11.2 Implemented behavior

Describe plainly:

- Generated run profile and reset behavior.
- SOP/Binder changes.
- Authoritative terminal configuration behavior.
- Supported typed terminal and endpoint commands.
- Player-error and malformed-input handling.
- Running/startup save behavior.
- Reachability and first-failure diagnostics.
- WP2/WP3 evaluation and evidence.
- LED behavior.
- Monitor screen behavior.
- Quick Command removal and static reference sidebar.

### 11.3 Changed assets and bindings

List the scripts, generated UI prefabs, equipment/device prefabs, scenario/scene assets, tests, and six image assets actually changed. Note any expected assets that were intentionally left unchanged.

### 11.4 Verification table

Use columns:

| Check | Result | Evidence | Follow-up |
|---|---|---|---|

Use only `Passed`, `Failed`, `Partial`, or `Not Run` in Result.

Include rows for:

- Unity compilation.
- Existing and new EditMode tests.
- Correct WP2 path.
- Correct WP3 path.
- Representative Layer 1, Layer 2, and Layer 3 failures.
- Running/startup persistence and scenario reset.
- Terminal and desktop canvases.
- Endpoint commands.
- LED response.
- All six monitor screens.
- iPad layout/touch checks.
- Layer 1 placement/cabling/termination regression.

### 11.5 Manual Unity procedures required

For every remaining manual item, provide:

- Exact scene or prefab path.
- GameObject and component.
- Field or value to inspect.
- Expected value.
- How the user recognizes success.
- Whether it is mandatory before play, mandatory before build, or recommended.

Explicitly inspect and document:

- `DeviceSessionManager` references to both canvases, both first-person controls, and both presenters.
- All serialized fields on `TssTerminalSessionPresenter` and `TssDesktopSessionPresenter` after prefab regeneration.
- `TssTrainingSession` and Balanced lab network configuration/profile wiring.
- AN-1, AN-2, CN-1, IT2, Reception2, Data3, and workstation script attachments.
- Interface aliases and endpoint mappings, especially GE00/GE01 to Fa0/0/Fa0/1.
- The unusual existing GE01 anchor path resolution without renaming protected objects.
- LED renderers and connected/disconnected shared materials.
- Workstation monitor renderer, material index, connectivity presenter, and all six textures.
- Texture import settings.
- Canvas sorting, EventSystem, keyboard navigation, touch submission, and iPad scaling.
- Scene/prefab overrides applied to the intended source assets.

If an item was verified automatically and no user action remains, write exactly: `No manual action required; verified automatically.`

### 11.6 Known issues, deviations, and next work

Record:

- Any unverified behavior or failed check.
- Deviations from the two capstone PDFs, including per-run rather than student-ID-derived generation.
- Unrelated dirty changes and known Unity/MCP warnings.
- Deliberate MVP limits: no packet simulation, routing protocols, real authentication lockout, full DHCP service simulation, wireless configuration, instructor console, checkpoint initials/timers, completion report, or application-restart persistence.

Rank recommended next work:

1. Fix failed or unverified acceptance checks.
2. Complete remaining Inspector/prefab assignments.
3. Run the complete WP2/WP3 workflow from a fresh generated profile.
4. Add phase progression and instructor checkpoint/sign-off UI.
5. Add per-part timing and completion-report generation.
6. Implement wireless Phases 20-22 after the wired workflow is stable.
7. Add student-ID-derived seeds and application-restart persistence only after the active-run model is validated.

Finish with a resume point naming the next task, the first scene/prefab/test to inspect, and any manual prerequisite.

## 12. GitHub Commit and Push Closeout

After implementation, pass-off documentation, and validation:

1. Confirm the branch is `codex/layer-2-layer-3-device-sessions` and inspect the full working-tree status.
2. Review diffs only for the intended Layer 2/3 implementation paths.
3. Preserve all unrelated existing modifications and untracked files.
4. Stage only:
   - The final combined implementation plan.
   - Layer 2/3 scripts and tests.
   - The Balanced lab network configuration/SOP changes.
   - The four affected device-session/first-person UI prefabs and any intentionally changed scene/device prefabs.
   - The six monitor images and their `.meta` files.
   - The implementation pass-off document.
5. Do not stage the two superseded numbered source plans unless explicitly requested.
6. Run scoped `git diff --cached --check` and inspect the staged diff in context.
7. Commit with:

   `Implement Layer 2 and Layer 3 device sessions`

8. Push the branch to `origin` and set upstream tracking if needed.
9. Report the branch name, final commit SHA, push result, tests run, and remaining manual tasks.
10. Do not create a pull request unless separately requested.

## 13. Implementation Assumptions and Guardrails

- Per-run generation is the accepted seed policy for this implementation.
- The terminal sidebar remains only as a non-interactive syntax reference.
- All router and switch configuration and verification actions are typed in the terminal.
- Endpoint verification is typed in the desktop command prompt.
- Six workstation images represent Layer 1, Layer 2, and Layer 3 connected/disconnected states.
- Existing endpoint IDs, Hierarchy names, device anchor paths, inspector-authored fields, and unrelated worktree changes are protected.
- The existing Layer 1 system remains the physical-path authority.
- The simulation remains deterministic and topology-based rather than packet-based.
- No new third-party package is required.
- The implementation should use the fewest new types/files that keep scenario data, network state/evaluation, and UI presentation separated.
- No pull request is created unless explicitly requested.
