# Network CLI and UI Follow-up Tasks

**Status:** Open
**Source:** Consolidated implementation notes
**Scope:** Router/switch CLI behavior, FMS training UI, binder cable scheduling, and Layer-1 device feedback

## Recommended order

1. Complete CLI configuration-mode and command behavior.
2. Correct router operational output and running-configuration rendering.
3. Repair terminal and binder interaction paths.
4. Fix cable schedule naming and port hover feedback.
5. Validate the desktop-to-switch Layer-1 indicator.

## 1. CLI command aliases and configuration modes

### 1.1 Add common command aliases

Support the following aliases in the router/switch CLI parser, while preserving the existing canonical commands and training feedback:

- `sh ip route` → `sh ip ro`
- `no ip address` → `no ip add`

**Acceptance criteria**

- Each alias is accepted in the same mode as its canonical command.
- The alias produces the same state change or output as the canonical command.
- Invalid or incomplete forms still receive the normal CLI error response.
- Existing command-history, scratchpad, and completion behavior is unchanged unless already supported.

### 1.2 Add sub-interface configuration mode

Add a `config-subif` mode for configuring router sub-interfaces. Entering a sub-interface from global configuration should place the CLI in this mode and return the correct prompt/context.

The mode should support the existing sub-interface commands, including encapsulation/VLAN configuration, IP addressing, and shutdown state changes where applicable.

**Acceptance criteria**

- A sub-interface can be selected from configuration mode.
- The prompt identifies `config-subif`.
- Commands are validated against the selected parent interface and sub-interface.
- `exit` returns to the expected configuration mode.
- `end` returns to privileged EXEC mode.

## 2. Line configuration and running-config output

Combine the console and auxiliary-line notes into one line-configuration work package.

### 2.1 Render console and auxiliary lines as `line` sections

Console configuration must not be reported as a normal network interface. The running configuration should render it in a line section near the end of the output, for example:

```text
line con 0
 password cisco
 login
 logging synchronous
```

The auxiliary line should render similarly:

```text
line aux 0
 password cisco
 login
```

For the MVP, support:

- Login enabled/disabled state.
- Plain-text or encrypted password representation, according to the existing device configuration policy.
- `logging synchronous` for the console line where configured.

**Acceptance criteria**

- Console and auxiliary settings appear under `line con 0` and `line aux 0`, not as interfaces.
- Running-config output preserves the configured order and indentation convention.
- The terminal authentication behavior matches the rendered configuration.
- Unconfigured line settings are omitted or rendered according to the project’s existing IOS-style convention.

## 3. Sub-interface administrative state

Ensure creation and display of a sub-interface correctly reflect the parent interface’s administrative state.

### Required behavior

- A sub-interface is shown as `shutdown`/`administratively down` when its parent interface is shut down.
- A sub-interface is operationally eligible only when the parent interface is enabled.
- `running-config` and `ip interface brief` report the resulting state consistently.
- Parent-interface shutdown changes are reflected by existing sub-interfaces without duplicating state incorrectly on every child.

**Implementation suggestion:** Treat parent-interface administrative state as the authority and derive the effective sub-interface state from it. Store a child-specific shutdown setting only if the CLI already requires independently shutting down a sub-interface.

## 4. Router routing-table display

Refine the router terminal’s routing-table output to better match the training reference and improve teachability.

Add:

- A `Gateway of Last Resort` message when a default route is present, and the corresponding no-default message when appropriate.
- Grouping of routes by route family/source as supported by the simulator.
- Connected/local route entries where applicable.
- The `L` route code for local host routes.

**Acceptance criteria**

- The displayed route codes match the routes actually present in runtime state.
- Local routes are shown for configured interface addresses where the simulator models them.
- The gateway message agrees with the default route state.
- Existing route output remains readable on the terminal panel and does not overflow the intended display area.

## 5. Terminal and binder interaction fixes

### 5.1 Restore Binder access from the terminal console panel

The terminal display console panel currently prevents the player from clicking the Binder button.

Check the panel’s raycast/interactable state, overlay ordering, and input-routing path. The fix should preserve terminal interaction while allowing the Binder button to receive a click when visible.

**Acceptance criteria**

- Binder opens from the terminal console panel.
- Terminal text input and command controls continue to work.
- No invisible overlay captures the Binder button’s pointer events.

### 5.2 Restore missing switch-terminal commands

The switch terminal session is missing commands in the right-side command area. Compare the switch session’s command registration/rendering path with the router session and restore only the commands supported by the switch model.

**Acceptance criteria**

- The expected switch commands are visible in the right-side command area.
- Unsupported router-only commands are not added accidentally.
- Command buttons and typed commands resolve to the same parser behavior.

## 6. Binder cable schedule naming

The Binder Cable Schedule currently displays `Port 1` for network-device connections that should show the device interface name.

### Required display rule

- Network devices such as switches `AN-1`/`AN-2` and router `CN-1`: show interface names such as `Fa0/1`.
- Patch panels: continue to show physical labels such as `Port 1`.

**Implementation suggestion:** Use the connected endpoint’s device category/type to select the display label. Do not change the underlying port identifier or patch-panel naming data just to fix presentation.

**Acceptance criteria**

- Both ends of a network-device cable show interface identifiers where available.
- Patch-panel entries remain `Port` labels.
- Existing schedule sorting, filtering, and cable identity remain unchanged.

## 7. Rack-mounted port hover highlighting

Enable hover highlighting for interactive ports/interfaces on rack-mounted devices.

The highlight should activate only for valid interactive port targets and should not make the entire rack or non-interactive geometry respond as a port.

**Acceptance criteria**

- Hovering a rack-mounted device port produces the expected highlight.
- Moving away clears the highlight reliably.
- Multiple nearby ports do not leave stale highlights.
- Existing highlighting for other interactive equipment remains unchanged.

## 8. Desktop-to-switch Layer-1 indicator

The desktop `Table_Computer_01_Setup (5)` remains visually disconnected after connecting `IT1` to `AN-1 Fa0/2`.

Trace the connection from the authored cable/endpoint through runtime signal state to the monitor image selection. Confirm that:

- `IT1` is connected to `AN-1` interface `Fa0/2`.
- Both endpoint identities are resolved correctly.
- The effective Layer-1 path is considered connected.
- The desktop monitor/display controller listens to effective Layer-1 state rather than only raw cable or schedule state.
- The connected image is selected after connection/reconnect and after the relevant runtime refresh.

**Acceptance criteria**

- Connecting `IT1` to `AN-1 Fa0/2` changes the screen from `monitor_layer1_disconnected` to the expected connected state.
- Disconnecting either endpoint restores the disconnected state.
- The result is deterministic after scene load, reconnect, and Play Mode restart.
- No unrelated endpoint indicator changes.

## Deliberate non-goals

- Do not redesign the CLI parser or introduce a new command framework for these fixes.
- Do not rename serialized scene fields or rewire prefabs unless a focused defect proves it necessary.
- Do not change patch-panel labels to make network interfaces look consistent.
- Do not make UI state the source of truth for routing, connectivity, or device configuration.
