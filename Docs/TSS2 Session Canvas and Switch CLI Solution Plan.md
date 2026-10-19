# TSS2 Session Canvas and Switch CLI Solution Plan

This plan addresses the runtime UI and switch CLI display issues found during the Endpoint IP Address and Ping Validation Checkpoint. The goal is to preserve the working network-state model while making the player’s configuration work visible and deterministic.

## Current findings

The issues map to existing sources of truth rather than a new subsystem:

- `TssTerminalSessionCanvas`, `TssDesktopSessionCanvas`, and `TssLaptopSessionCanvas` use different authored layouts. Their session panels need one consistent footprint so `TssRuntimeCanvas > HUD > SopButton` and `WP1Button` remain visible while a session is open.
- `TssDeviceLogicalState.AssignDefaults()` currently pre-populates several device addresses, including switch, server, printer, and desktop values. This conflicts with the requirement that player-owned endpoint addressing be configured during play.
- `EndpointNetworkState` also initializes `Address` from the scenario address table. That value is useful as an expected/evaluation value, but it should not automatically appear as a player-committed endpoint setting when the scenario requires player configuration.
- `BuildShowVlanBrief()` currently appends every interface to VLAN 1, regardless of the interface’s current VLAN state.
- `BuildShowRunningConfig()` already displays a switchport mode line from `PortMode`, but its output must be verified against the live network state and extended so VLAN definitions appear after the interface sections.
- `TssCliCommandProcessor.Running()` has a separate running-configuration formatter. The two display paths must not disagree about interface mode, VLAN, or output order.

## Implementation order

### 1 Make the session canvas footprint consistent

Use the authored `TssTerminalSessionCanvas` presentation as the reference and update only the presentation geometry of:

- `Assets/Prefabs/UI/TssTerminalSessionCanvas.prefab`
- `Assets/Prefabs/UI/TssDesktopSessionCanvas.prefab`
- `Assets/Prefabs/UI/TssLaptopSessionCanvas.prefab`

First compare the root canvas, main panel, header, and content panel RectTransforms. Match the desktop and laptop panel anchors, offsets, and size to the terminal canvas without changing serialized presenter references, close behavior, or runtime state bindings.

Check `Assets/Scenes/TSS.unity` for scene-instance overrides before changing the prefab. If a scene override is the reason runtime differs from the prefab, correct the smallest override rather than regenerating the scene.

Acceptance test:

- Open each session in Play Mode.
- Confirm the session is fully usable and does not cover the `SopButton` or `WP1Button`.
- Confirm those HUD controls remain visible and clickable.
- Confirm close via Log Off / Disconnect and Escape still works.
- Confirm no session-specific content is clipped at the reference resolution.

### 2 Remove unintended player-device address prefill

Keep scenario expectations separate from live endpoint configuration.

Minimum change:

1. Preserve the scenario address table used to decide what a correct result is.
2. Initialize a player-configured endpoint’s live `Address`, `Gateway`, and related committed values as unassigned until the player applies static settings or DHCP returns a lease.
3. Keep the endpoint’s expected address available to validation code; do not use it as the displayed committed address.
4. Keep networks explicitly designated for DHCP in the existing scenario DHCP list.
5. Continue mirroring the committed `EndpointNetworkState` into `TssDeviceLogicalState` and the device UI after successful apply or lease assignment.
6. On failed apply or failed DHCP, retain the last committed state and show the diagnostic.

Do not blank addresses that are intentionally authored as device-management configuration or router interface configuration. The change applies to settings that the player is expected to enter during this checkpoint.

Likely files:

- `Assets/Scripts/TssDeviceLogicalState.cs`
- `Assets/Scripts/TssNetworkSessionState.cs`
- `Assets/Scripts/TssEndpointCommandProcessor.cs`
- `Assets/Scripts/TssDesktopSessionPresenter.cs`
- `Assets/Scripts/TssDeviceSessionController.cs`
- existing addressing tests under `Assets/Tests/EditMode`

Acceptance test:

- Start a fresh scenario and open the endpoint session.
- Confirm player-owned fields are unassigned rather than pre-filled.
- Apply a valid static address and confirm it appears in the UI, `ipconfig`, and session state.
- Enable DHCP on a designated DHCP network and confirm the lease appears in all three places.
- Submit invalid static settings and break DHCP prerequisites; confirm no false committed address is created.

### 3 Increase TssEquipmentCasePanel button text

Update the text components in `Assets/Prefabs/UI/TssEquipmentCasePanel.prefab` after checking all case-panel buttons, including `ReturnHeldButton` and `CancelButton`. Increase the serialized font size only as far as the button bounds allow; keep the button dimensions, navigation, and event bindings unchanged unless the larger text clips.

If `Assets/Editor/TssUiPrefabBuilder.cs` regenerates this prefab, make the same single source change there or confirm that the prefab is the presentation authority and does not get regenerated.

Acceptance test:

- Open the equipment case at the target runtime resolution.
- Confirm every button label is readable without clipping.
- Confirm hover, press, selection, and existing actions remain unchanged.

### 4 Reconcile show vlan brief with interface state

Change the formatter used by the visible switch CLI so the Ports column is derived from each interface’s effective VLAN assignment.

Rules:

- New switch interfaces retain `AccessVlan = 1`, so the initial output lists every unconfigured access interface under VLAN 1.
- An access VLAN command moves that interface to the selected VLAN row.
- `no switchport access vlan` returns it to VLAN 1.
- Interface names are sorted deterministically within each VLAN row.
- VLAN rows are sorted numerically and include configured VLAN names.
- Once an interface is explicitly a trunk, do not falsely present it as an access port. Use one documented simulator convention consistently: either omit it from the access-port column or list only its native VLAN when the training display requires every port to remain visible. The preferred convention is omission after trunk mode is active; before trunk configuration, the port remains in VLAN 1.

The formatter must consume the live `NetworkInterfaceState` / `TssDeviceLogicalState` values rather than reconstructing VLAN membership from command history.

Acceptance test:

- Before configuration, run `show vlan brief` and verify all switch interfaces appear under VLAN 1.
- Configure `Fa0/2` as access VLAN 80 and verify only `Fa0/2` moves to VLAN 80.
- Configure an uplink as a trunk and verify the documented trunk display convention.
- Use `no` commands and confirm the port returns to the correct default state.

### 5 Make show running config reflect configured interface modes

Keep the current logical-state mapping that derives `PortMode` from `NetworkInterfaceState.IsTrunk`, then ensure the visible formatter emits the correct commands:

- access interface: `switchport mode access` and `switchport access vlan <id>`;
- trunk interface: `switchport mode trunk`, 802.1Q encapsulation when enabled, allowed VLANs, and native VLAN;
- administrative state: `shutdown` when disabled;
- router sub-interface: matching `encapsulation dot1q <vlan>` and IP address.

Do not infer mode from the interface name or from the last command. The output must be generated from the current session state after every successful CLI mutation.

Acceptance test:

- Run `show run` before and after access-mode configuration.
- Run it before and after trunk-mode configuration.
- Apply the corresponding `no` commands and verify the display returns to the default state.
- Confirm the output survives closing and reopening the CLI session.

### 6 Move VLAN definitions after all interface blocks

The visible switch `show running-config` output should be ordered as:

1. global configuration;
2. all interface blocks;
3. line configuration;
4. VLAN definitions;
5. `end`.

Implement the order in the formatter actually used by the session UI. Then align the alternate `TssCliCommandProcessor.Running()` formatter so a command path cannot produce a contradictory order. Keep VLANs numerically sorted and preserve configured names.

Acceptance test:

- Create and name at least two VLANs.
- Configure one access port and one trunk port.
- Run `show running-config`.
- Verify every interface block finishes before the first `vlan <id>` block.
- Verify VLAN names and IDs are present and stable after reopening the switch CLI.

## Verification sequence

Run the changes in this order so a UI defect is not confused with a network-state defect:

1. Unity compile and prefab serialization check.
2. Play Mode canvas visibility and close controls.
3. Equipment case button readability and interaction.
4. Fresh-session endpoint address is unassigned.
5. Static apply success and failure.
6. DHCP success and prerequisite failures.
7. Switch VLAN display before and after access/trunk configuration.
8. `show running-config` mode and VLAN ordering.
9. Desktop gateway and target ping validation.
10. AP uplink and laptop follow-on checkpoint.

## Scope and rollback

This is a narrow presentation and state-display correction. Do not rename serialized fields, regenerate the scene broadly, remove the individual-tile or fallback network paths, or change authored endpoint placement. Keep changes limited to the three session prefabs, the equipment-case presentation, the live-versus-expected endpoint addressing path, and the two CLI output formatters plus focused tests.

Rollback is straightforward: revert the named prefab and formatter hunks while preserving the existing network-state and addressing tests. The scenario’s expected address data remains intact throughout, so evaluation behavior can be restored without rebuilding scenario assets.
