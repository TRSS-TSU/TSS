# Full Capstone Playthrough and Review Plan

## Objective

Run the generated `Balanced_12_Piece_Lab` scenario from the main menu through all Work Packages, verify the complete physical, Layer 2, and Layer 3 network, document issues and remaining work in a hosted HTML/CSS review page, email the link, and leave Unity running at a useful review point.

The final-results scene is intentionally deferred to the next work session.

## Test Identity

- Last name: `TSSQA`
- Computer number: `1`
- Blocker policy: repair the smallest root cause, validate it, then continue the playthrough.

## Guardrails

- Use the generated scenario and player-seeded addressing/configuration as the source of truth.
- Preserve Unity Hierarchy names, inspector endpoint IDs, display labels, serialized fields, prefab bindings, and existing scene wiring.
- Reuse the current placement, termination, cable, endpoint, CLI, DHCP, wireless, and Work Package systems.
- Make only small, reversible fixes required to complete the walkthrough.
- Do not refactor unrelated systems, commit or push changes, or build the final-results scene.

## Phase 1 — Repair Known Completion Blockers

Before the full run, address only issues that prevent valid completion:

1. Correct WP2 VLAN evaluation so the configured suspended dead VLAN is accepted.
2. Make unused-port dead-VLAN assignment and shutdown state part of actual WP2 evaluation rather than a permanently unchecked display item.
3. Add state-backed WP4 completion using existing endpoint/network evidence instead of a parallel state system.
4. Add expected Guest Lounge placements for `AP-1` and `TestLaptop` at `LobbyTable01` so placement grading matches the scenario.
5. Provide the laptop with the minimum player workflow needed to enter SSID/passphrase, join WiFi, request DHCP, run diagnostics, and verify the RedWolf page.
6. Prevent the printer's early unresolved `Eth01` LED warning from firing before the dynamic endpoint binder has completed.
7. Add or adjust only the smallest runnable tests needed to protect these fixes.

## Phase 2 — Start the Scenario

1. Enter `TSSQA` and `1` in the main menu.
2. Load `Balanced_12_Piece_Lab`.
3. Record the generated addressing, VLAN, SSID, credentials, and device values shown by the scenario binder.
4. Confirm the aircraft/FMS-specific project instructions do not alter this network capstone's established scene bindings or scale.

## Phase 3 — Place All Equipment

Use the scenario's expected locations:

- Rack01 rear U35–U38: PDU1
- Rack01 front U35–U38: AN-1 switch
- Rack01 front U30–U34: CN-1 router
- Rack01 rear U3–U8: IT2 server
- Rack02 rear U35–U38: PDU2
- Rack02 front U35–U38: AN-2 switch
- IT Desk: IT1 desktop
- Reception Desk: Reception1 desktop and Reception2 printer
- Office 1 Desk: Data1 desktop
- Office 2 Desk: Data2 desktop and Data3 printer
- Guest Lounge / LobbyTable01: AP-1 and TestLaptop

Treat the two existing U40 patch panels as permanent infrastructure; do not place duplicates unless the live scenario explicitly requires it.

## Phase 4 — Complete the Physical Layer

1. Complete all required rack power and device power connections.
2. Terminate both ends of the 41 punchdown-required permanent links, for 82 termination modules total.
3. Connect every required wall port to its patch-panel endpoint.
4. Patch the endpoint access links, inter-switch links, router uplink, and server links specified by the generated scenario.
5. Connect the wireless path:
   - AP-1 to Reception Wall Port05
   - Rack01 Patch Panel 17 to AN-1 Fa0/7
6. Complete the DEMARC path:
   - CN-1 Fa0/1 to Rack01 Patch Panel 18
   - Rack01 Patch Panel 18 to Provider DEMARC Port01
7. Confirm the completed DEMARC path unlocks provider reachability and DHCP services.
8. Verify cable LEDs, link indicators, and physical Work Package evidence after the binder has settled.

## Phase 5 — Configure Layer 2

Configure both access switches from the generated scenario values:

1. Apply hostname, enable secret, password encryption, no domain lookup, MOTD banner, console password/login, and VTY password/login.
2. Create and name all required VLANs.
3. Configure the dead VLAN in suspended state.
4. Configure required access ports:
   - AN-1 Fa0/2: IT1
   - AN-1 Fa0/3: Reception1
   - AN-1 Fa0/4: Reception2
   - AN-1 Fa0/5: IT2
   - AN-1 Fa0/7: AP-1 / Reception VLAN
   - AN-2 Fa0/2: Data1
   - AN-2 Fa0/3: Data2
   - AN-2 Fa0/4: Data3
5. Configure trunks, allowed VLANs, and native VLAN exactly as generated.
6. Assign unused ports to the dead VLAN and shut them down.
7. Save each running configuration.
8. Verify with `show vlan brief` and `show interfaces trunk`.

## Phase 6 — Configure Layer 3

Configure CN-1 from the generated scenario values:

1. Apply base device hardening and access configuration.
2. Configure the provider-facing interface.
3. Configure router-on-a-stick subinterfaces and 802.1Q encapsulation for every required VLAN.
4. Apply each generated gateway address and subnet mask.
5. Configure the required default route.
6. Enable required interfaces and save the configuration.
7. Verify with `show ip route` and interface status.

## Phase 7 — Configure End Devices

1. Configure IT, Reception, and Data desktops with their generated static IPv4 settings.
2. Configure both printers with their required static-only addresses; do not use DHCP for printers.
3. Verify the IT desk desktop and all office desktops are placed, linked, and addressed.
4. Configure AP-1 with the generated admin settings, SSID, security/passphrase, channel, and WPS state.
5. On the Guest Lounge laptop:
   - enter the generated SSID and passphrase;
   - join AP-1 over WiFi;
   - select DHCP addressing;
   - renew the lease after the DEMARC is complete;
   - confirm it receives the generated Reception-network lease, expected to end in `.60` for this scenario unless the live binder specifies otherwise.

## Phase 8 — Complete All Work Packages

1. WP1: confirm placement, rack occupancy, power, terminations, permanent infrastructure, and patching are complete.
2. WP2: confirm VLANs, trunks, access ports, dead VLAN, unused-port shutdown, and saved switch configurations.
3. WP3: confirm router subinterfaces, gateways, default route, endpoint addressing, and deterministic reachability.
4. WP4: confirm AP configuration, laptop WiFi association, DHCP lease, diagnostics, page access, and required evidence.
5. Run the required IT1-to-provider ping and retain its evidence.
6. Run endpoint diagnostics and open the RedWolf page from the Guest Lounge laptop.
7. Confirm every Work Package requirement is actually evaluated as complete, not merely displayed as checked text.

## Phase 9 — Validation

1. Run the full Unity Edit Mode test suite, including rack span, cable routing, device sessions, addressing, placement, physical panel, termination, and wireless route tests.
2. Start a fresh Play Mode run with `TSSQA / 1` and repeat the completion-critical path.
3. Confirm there are no unexpected exceptions, duplicate equipment requirements, stale bindings, false Work Package failures, or unresolved endpoint warnings.
4. Confirm DHCP remains locked before the DEMARC path and succeeds after it.
5. Confirm the laptop is wireless and DHCP-based while printers remain wired and static.
6. Capture the final Work Package state, key configuration outputs, and any remaining issues.

## Phase 10 — Review Website and Email

Create a new static HTML/CSS report under `sites/tss2-capstone-playtest/dist/` containing:

- run identity and generated scenario seed;
- playthrough sequence;
- equipment placement matrix;
- physical connection and termination summary;
- Layer 2 and Layer 3 configuration summary;
- endpoint, printer, AP, and laptop results;
- Work Package completion matrix;
- issues found, repairs made, and verification evidence;
- remaining completion items and risks;
- screenshots where they materially help review;
- the exact Unity resume/review point.

Publish the report as a new private Sites project, then send the hosted link through Gmail to the authenticated account (`me`).

## Final Review State

Leave Unity in Play Mode at the Guest Lounge with the completed scenario loaded and the WP4 Work Package panel open, so the user can immediately review the laptop, WiFi/DHCP result, diagnostics, RedWolf page evidence, and overall completion state.

## Deferred Work

- Final-results scene and presentation flow.
- Broad architecture cleanup or refactoring.
- Removal of fallback or diagnostic systems not required for this playthrough.
- Git commit, push, or pull request unless separately requested.

