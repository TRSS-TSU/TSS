# Completion Plan: SOP Rendering and Work Package Evaluation

## Objective

Render one generated learner profile into the SOP/Binder and grade WP2/WP3 from structured state and typed evidence.

## Dependencies

`01-network-state-and-cli.md`, `02-deterministic-reachability.md`, and `03-endpoint-desktop-and-printer-ui.md`.

## Implementation

1. Replace the Phase 9–15 and Phase 16–19 SOP placeholders at runtime using the profile already retained by `TssTrainingSession`.
2. Implement `TssNetworkWorkPackageEvaluator` with structured unmet conditions only; it must not calculate or display answers through UI text matching.
3. WP2 checks AN-1/AN-2 hardening, full VLAN database, access ports, trunks, native/allowed VLAN agreement, typed show evidence, and saved startup state.
4. WP3 checks CN-1 hardening, parent/subinterface/WAN/default route configuration, typed route evidence, saved startup state, and required endpoint-originated diagnostics.
5. Save Phase 15/19 completion snapshots after evaluation passes; later authorized changes must not invalidate recorded checkpoints.

## Acceptance

- SOP values stay unchanged while canvases open/close and regenerate only on new scenario runs.
- Router-originated ping cannot satisfy endpoint evidence.
- A later legitimate Fa0/7 reuse does not revoke completed Phase 15.
- Evaluator returns actionable unmet categories without answer-revealing values.
