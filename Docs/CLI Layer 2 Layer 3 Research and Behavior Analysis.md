# Layer 2/3 CLI Research and Behavior Analysis

Status: research baseline only. This note records the current implementation and the behavior contract to use before changing the CLI. No runtime or prefab changes are included.

## Authoritative sources

- `Docs/Student capstone instructions (3).pdf`
- `Docs/TSS Networks Capstone - 336 TRS.docx`
- `Docs/Network Capstone Student Task Description.docx`
- `Docs/Network Capstone Development Plan.docx`
- `Docs/Network Capstone Equipment and Port Listing - polished.docx`
- `Docs/Completion Plans/01-network-state-and-cli.md` through `06-validation-hotwash-and-git-closeout.md`
- `Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset`
- `Assets/Scripts/TssCliCommandProcessor.cs`
- `Assets/Scripts/TssTerminalSessionPresenter.cs`
- `Assets/Scripts/TssNetworkSessionState.cs`
- `Assets/Scripts/TssNetworkReachabilityEvaluator.cs`
- `Assets/Scripts/TssNetworkWorkPackageEvaluator.cs`
- `Assets/Tests/EditMode/TssDeviceSessionTests.cs`

The capstone documents define the learner workflow and seeded values. The Unity session state, typed CLI processor, reachability evaluator, and work-package evaluator are the runtime sources of truth. UI output is evidence/presentation only.

## Phase-to-command mapping

| Phases | Required behavior | Required evidence | Current status |
|---|---|---|---|
| 9, 12 | Switch/router hardening: hostname, enable secret, password encryption, domain lookup, banner, console/VTY login/password | Device hardening state | Core commands exist; evaluator coverage exists; command-level tests are missing. |
| 10, 13 | Create and name management, reception, data, native, and suspended dead VLANs on both switches | VLAN database and active/suspended state | VLAN/name/state commands exist; `state suspend` is represented by appending text to the VLAN name, which needs dedicated behavior analysis. |
| 11, 14 | Select interfaces/ranges, access VLANs, trunks, allowed VLANs, native VLANs, shutdown/no shutdown | Structured interface state | Core commands exist; port-role and malformed-range tests are missing. |
| 15 | Run `show vlan brief`, `show interfaces trunk`, then save | Per-switch typed evidence and startup snapshot | Evidence and save paths exist; command source/device-context tests are missing. |
| 16 | Router hardening | Router state | Shared global/line commands apply; router-specific acceptance needs tests. |
| 17 | Bring up parent interface and configure tagged subinterfaces | Parent/admin state, dot1q, gateway address/mask | Router runtime keys and accepted CLI names are `Fa0/0` and `Fa0/1`; internal `Fa00`/`Fa01` forms are retained only for binding. |
| 18 | Configure WAN interface and default route | WAN state and static default route | `ip address`, `ip route`, and `show ip route` exist; default-route semantics need focused tests. |
| 19 | Verify from endpoints and inspect router route table | Endpoint-originated diagnostics plus router route evidence | Router `ping` intentionally returns a neutral zero-success response and does not satisfy endpoint evidence. |

## Current CLI behavior inventory

### Modes and transitions

Supported modes are user EXEC, privileged EXEC, global config, interface config, line config, and VLAN config.

- `enable` / `en`: user EXEC → privileged EXEC.
- `disable`: privileged EXEC → user EXEC.
- `configure terminal`, `conf t`, `config t`, `configure`: privileged EXEC → global config.
- `interface`, `interface range`: global config → interface config.
- `line console 0`, `line con 0`, `line vty 0 4`: global config → line config.
- `vlan <id>`: global config → VLAN config.
- `end`: any mode → privileged EXEC.
- `exit`: steps back one configuration level.
- `do <exec-command>`: accepted only from configuration modes.

The presenter maps `TssNetworkCliMode` to its legacy `CiscoCliMode` for prompt rendering. The processor is authoritative when network state is available; the presenter still contains a legacy fallback path using `TssDeviceLogicalState`.

### Supported command families

| Family | Current commands/aliases | State/evidence impact | Gap to verify |
|---|---|---|---|
| Help | `?`, `help`, any input ending in `?` | No state change | Output is broad mode help, not token-specific completion. Decide whether that is sufficient for training. |
| Hardening | `hostname`, `enable secret`, `service password-encryption`, `no ip domain-lookup`, `banner motd`, line password, `login` | Mutates device state | Validate malformed input and exact evaluator fields. |
| VLAN | `vlan`, `name`, `state suspend` | Mutates VLAN database | Confirm suspended VLAN representation and evaluator matching. |
| Interfaces | `interface`, `interface range`, `shutdown`, `no shutdown` | Mutates selected interfaces | Validate range syntax, selected-port boundaries, and router aliases. |
| Switching | `switchport mode access`, `switchport mode trunk`, access VLAN, native VLAN, allowed VLAN list | Mutates Layer 2 interface state | No `add`/`remove` allowed-list syntax; confirm documented scope does not require it. |
| Routing | `ip address`, `encapsulation dot1q`, `ip route` | Mutates Layer 3 interface/routes | Subinterface syntax such as `Fa0/0.<vlan>` is not currently accepted as a distinct interface name. |
| Verification | `show running-config`, `show startup-config`, `show vlan brief`, `show interfaces trunk`, `show interfaces`, `show ip interface brief`, `show mac address-table`, `show ip route` | Records selected switch/router evidence for some commands | `show interfaces` and `show ip interface brief` do not currently record required evidence; verify whether that is intentional. |
| Save | `copy running-config startup-config`, `copy run start`, `write memory`, `wr mem` | Clones running state into startup snapshot and records `save` | Validate snapshot equivalence after every supported mutation. |
| Diagnostics | `ping <address>` | Neutral output only | Must remain distinct from endpoint-originated evidence. |

### Alias and interface findings

- Router evaluator state uses `Fa0/0` and `Fa0/1`.
- The router parser rejects `GE00`/`GE01` and `Gi0/0`/`Gi0/1`; those interfaces do not exist on the Fast Ethernet router.
- The capstone documents, runtime keys, and user-facing router CLI now use `Fa0/0` and `Fa0/1`.
- Switch ports accept `Fa0/1` through `Fa0/24`, plus `FastEthernet` spelling.
- `interface range` currently parses one hyphen-delimited range and expands it numerically.
- `interface range` does not currently support comma-separated ranges or multiple range segments.

## Validation and atomicity findings

Current malformed-input handling generally returns `Accepted = false` without calling `Changed`, so state should remain unchanged. This must be tested rather than assumed for:

- invalid VLAN IDs;
- invalid IPv4 addresses/masks;
- invalid VLAN lists;
- reversed/out-of-range interface ranges;
- malformed banners;
- incomplete `ip route` commands;
- invalid router interface aliases;
- malformed subinterface commands.

Valid-but-wrong values are intentionally retained so reachability and work-package evaluation can report the first failed layer without revealing the answer.

## Command-history behavior contract

The terminal currently stores rendered console text in `_consoleHistory`; it does not store submitted command lines or handle Up/Down navigation. The implementation contract for the next CLI behavior change is:

1. Enter submits the current line once.
2. Non-empty submitted commands are added to a bounded per-terminal-session command list.
3. Empty submissions are not added.
4. Up recalls the previous command, then older commands in reverse submission order.
5. Down moves toward newer commands; moving past the newest command restores a blank input.
6. The first history navigation saves the learner's unsubmitted draft and restores it when Down moves past the newest command.
7. Recalled text remains editable and does not mutate network state until Enter.
8. Duplicate commands are retained in order because repeated verification commands are meaningful training actions.
9. Closing/reopening the terminal resets command history but preserves network/session state.
10. `clear` clears rendered output only; it does not erase command history.
11. Keyboard arrows and any retained Up/Down buttons use the same presenter history methods.

This is intentionally per-session and bounded. Persistent command history is unnecessary for the current training loop.

## Layer 2/3 fault and evidence matrix

| Fault | Expected first failed layer | Required repair/retest | Evidence boundary |
|---|---:|---|---|
| Missing physical path or interface shutdown | 1 | Restore cabling/admin state, then rerun endpoint test | LEDs/physical state only; no VLAN/routing claim. |
| Wrong access VLAN | 2 | Correct endpoint-facing switchport VLAN, then rerun endpoint test | Switch typed state and endpoint reachability. |
| VLAN missing/suspended | 2 | Create/name/activate the VLAN on the required switch, then retest | `show vlan brief` from the correct switch. |
| Allowed VLAN omitted from trunk | 2 | Add VLAN to every required trunk, then retest | `show interfaces trunk` from required switch contexts. |
| Native VLAN mismatch | 2 | Match native VLAN on both trunk ends, then retest | Structured trunk state plus typed show evidence. |
| Invalid endpoint address/mask/gateway | 3 | Apply a complete valid endpoint configuration, then retest | Endpoint processor state; malformed Apply must be atomic. |
| Missing/wrong router dot1q VLAN | 3 | Correct subinterface encapsulation, then retest | Router state and endpoint-originated diagnostics. |
| Router parent interface down | 3 | Enable parent interface, then retest | Router interface state; lower layers must already pass. |
| Wrong/missing WAN address or mask | 3 | Correct WAN interface, then retest provider reachability | Router `show ip route` plus endpoint diagnostic. |
| Missing/wrong default route | 3 | Correct static default route, then retest provider reachability | Router typed route evidence; router-originated ping is insufficient. |

## Evaluator dependency map

- `TssNetworkReachabilityEvaluator` must evaluate Layer 1, then Layer 2, then Layer 3 and return the first failed layer.
- `TssNetworkWorkPackageEvaluator` must use structured device state and typed evidence, not console text or button clicks.
- WP2 requires both switches' VLAN/access/trunk state, typed VLAN/trunk evidence, and saved startup snapshots.
- WP3 requires router hardening, parent/subinterface/WAN/default-route state, typed `show ip route`, endpoint-originated diagnostics, and a saved router snapshot.
- Router-originated `ping` must not satisfy endpoint evidence.
- Neutral unmet messages must not reveal seeded VLAN IDs, addresses, passwords, or exact repair commands.
- Completion snapshots are sticky after first successful WP2/WP3 completion.

## Prioritized implementation backlog

1. Add a small command-history model to `TssTerminalSessionPresenter` with Up/Down keyboard and button entry points.
2. Add focused EditMode tests for history navigation, draft restoration, duplicate commands, clear behavior, and session reset.
3. Add direct `TssCliCommandProcessor` tests for modes, aliases, ranges, malformed-input atomicity, valid-wrong persistence, `do`, help, show commands, and save snapshots.
4. Keep the documented `Fa0/0`/`Fa0/1` names aligned with evaluator runtime keys; reject obsolete Gigabit aliases.
5. Add router subinterface syntax coverage without changing protected endpoint names.
6. Add WP2/WP3 evidence-context tests and the documented Layer 2/3 fault matrix.
7. Run the fresh-scenario Play Mode matrix only after the focused EditMode tests pass.

## Validation checklist

- [ ] Same-seed state and close/reopen persistence remain stable.
- [ ] Empty and malformed CLI input leave authoritative state unchanged.
- [ ] Valid-but-wrong values persist and appear in typed show output.
- [ ] Running/startup snapshots diverge after unsaved edits and match after save.
- [ ] Switch evidence is recorded only from switch CLI contexts.
- [ ] Router route evidence is recorded only from the router CLI context.
- [ ] Endpoint diagnostics, not router ping, satisfy WP3 endpoint evidence.
- [ ] Each intentional fault returns the documented first failed layer.
- [ ] Up/Down history never mutates network state before Enter.
- [ ] Existing dirty files remain untouched and unstaged.
