# Completion Plan: SOP Rendering and Work Package Evaluation

## Outcome

Render one deterministic per-run learner profile into the existing SOP/Binder and evaluate Work Packages 2 and 3 from structured network state plus typed evidence. The evaluator reports unmet categories, not the answers.

## References and Current Starting Point

- [Master implementation plan — generated profile and SOP](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#31-generated-run-profile-and-bindersop-values)
- [Master implementation plan — Work Package 2](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#32-work-package-2-layer-2)
- [Master implementation plan — Work Package 3](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#33-work-package-3-layer-3)
- `Assets/Data/TSS/Scenarios/Balanced_12_Piece_Lab.asset` — scenario configuration source; preserve inspector-authored values.
- `Assets/Scripts/TssScenarioDefinition.cs` and `Assets/Scripts/TssTrainingSession.cs` — scenario and per-run profile lifecycle.
- `Assets/Scripts/TssNetworkWorkPackageEvaluator.cs` — current evaluator has only a basic boundary and must be expanded to structured WP2/WP3 checks.
- `Assets/Scripts/TssNetworkSessionState.cs`, `Assets/Scripts/TssCliCommandProcessor.cs`, `Assets/Scripts/TssEndpointCommandProcessor.cs` — authoritative state and evidence inputs.

Past work established a session-owned profile/state path but left the Phase 9–15 and 16–19 placeholders, full evidence checks, and durable completion snapshots unfinished. This plan closes that gap without creating a separate instructor/reporting system.

## Dependencies and Scope

Complete Plans [01](01-network-state-and-cli.md), [02](02-deterministic-reachability.md), and [03](03-endpoint-desktop-and-printer-ui.md) first. Do not add timers, phase jumps, instructor signatures, scoring dashboards, or UI-text parsing.

## Implementation Steps

### 0. Update Graphify graphs

Use the graphify skill. Evaluate the project and update the deterministic code-only graph of files. The start this implementation guidance.

### 1. Render the generated profile exactly once per scenario run

Use the profile retained by `TssTrainingSession` as the sole source for the Balanced lab's Phase 9–15 and Phase 16–19 SOP sections. Replace only the existing placeholders at runtime:

- render the generated VLAN/network/WAN/endpoint values needed for that learner's tasks;
- preserve non-network SOP content and current Binder structure;
- never log credentials or answer values;
- do not regenerate while a terminal/desktop canvas opens, closes, or refreshes.

On a new `BeginScenario`, reset profile, network state, evidence, and checkpoint snapshots together. Reopening a panel must use the already-active profile.

### 2. Establish evaluator input types

Make `TssNetworkWorkPackageEvaluator` accept session/profile/state/evidence and return a small structured result:

- `Complete`;
- an ordered collection of unmet category IDs;
- optional learner-safe label for each category;
- a checkpoint snapshot payload when complete.

Keep expected values inside the profile/state comparison. Presenters may render labels, but must not search SOP text or derive expected values.

### 3. Define WP2 completion checks

The WP2 evaluator should verify only the declared Balanced-lab requirements:

1. AN-1 and AN-2 hardening state.
2. Required VLAN database entries and active status.
3. Required endpoint-facing access-port roles/VLANs.
4. Required trunks, allowed VLAN carriage, and native-VLAN agreement.
5. Required typed `show vlan brief` / `show interfaces trunk` evidence from the correct device contexts.
6. Startup configuration snapshots match the relevant running configuration.

Use the structured command/evidence records created by the CLI processor. A router command, a button click, or copied UI output must not satisfy switch-show evidence.

### 4. Define WP3 completion checks

After WP2 is evaluable, check CN-1:

1. Router hardening state.
2. Parent interface, subinterfaces, dot1q VLANs, and gateway addressing/masks.
3. Provider-facing WAN address/mask, interface state, and default route.
4. Typed `show ip route` evidence from CN-1.
5. Required endpoint-originated diagnostics recorded by the endpoint command processor.
6. Saved startup configuration matches running configuration.

Router-originated ping must never satisfy endpoint evidence. Valid-but-wrong commands persist as configuration but leave the relevant unmet category in the result.

### 5. Snapshot successful checkpoints

When WP2/WP3 first pass, write a session-owned completion snapshot recording the profile identity, completion time/order, and evaluated required state/evidence. Later authorized topology changes—such as legitimate AN-1 Fa0/7 reuse—must not retroactively revoke an already-passed Phase 15 snapshot.

Do not create a second network snapshot schema if `TssNetworkSessionState` can hold the completion record. Clear all snapshots only on the scenario lifecycle reset.

## Tests

Add focused edit-mode coverage for:

- identical seed/profile values remain stable across panel open/close;
- new scenario reset clears evidence and snapshots;
- one missing WP2 condition yields its category without expected answer data;
- WP2 passes only with typed switch evidence and saved startup state;
- WP3 rejects router-originated diagnostic evidence;
- WP3 passes with the required endpoint source/target evidence and saved router state;
- a successful WP2 snapshot survives later authorized interface reuse.

Use direct structured-state setup. SOP formatting itself can have a small rendering test, but never become grading input.

## Manual Play Mode Proof

Start a fresh Balanced scenario and record the generated profile seed. Confirm Phase 9–19 shows a coherent value set. Open/close all device canvases and confirm the values do not change. Complete WP2 with typed commands/evidence, save, and capture the completion result. Repeat for WP3 using endpoint diagnostics. Deliberately omit one show command and one save operation to verify completion remains blocked with a neutral category.

## Exit Criteria

- SOP and evaluator use one profile for the lifetime of the run.
- WP2/WP3 are derived from structured state and typed evidence only.
- Completion snapshots survive intended later changes and reset only with a new run.
- Unmet feedback is actionable but never reveals the intended VLAN, address, password, or exact repair command.

## Rollback

Profile rendering and evaluation are additive. If a Binder binding is missing, retain session/profile state, restore only the placeholder rendering hookup, and log the unresolved UI reference once; do not duplicate generated values in a UI-local cache.
