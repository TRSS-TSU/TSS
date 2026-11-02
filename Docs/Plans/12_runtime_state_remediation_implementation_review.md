# Runtime State Remediation — Implementation Review

Implemented from `12_runtime_state_remediation_plan_rev2.md` on `main`.

## Runtime changes

- Restored provider-demarc reachability in `TssTrainingSession.IsProviderDemarcConnected` by using the existing permanent-aware physical path search. The leaf-only effective-path cache remains the hot-path source for ordinary endpoint connectivity.
- Invalidated the effective-path cache after successful rack installation, rack pickup, and rack-device rename, because rack records participate in required cable-type resolution.
- Made WP2, WP3, and WP4 evaluation live-only. The obsolete completion-history flag, completion snapshot state, result wrapper support, and Results snapshot history model were removed.
- Updated the HUD and Results assessment to call the same parameterless, live evaluator methods.

## Regression coverage

- Added a permanent patch-panel-to-demarc path test that verifies router Fa0/1 carrier state.
- Added a cache-invalidation test covering a valid installed CN-1 rename that makes the cable requirement no longer resolve.
- Strengthened the AP uplink test with a configured operational access-switch port and explicit cache refreshes after its test-only connection mutations.
- Added a HUD/Results current-state agreement test for WP3 and WP4.
- Removed or revised tests that exercised the deleted sticky completion-snapshot behavior.

## Validation

- Unity Editor compilation completed with no console errors.
- Static source sweep found no remaining completion-snapshot types, history fields, or evaluator boolean arguments.
- The headless Edit Mode runner could not open the project because the active Unity Editor owns its lock; no batch test-result XML was produced. Run the Edit Mode suite from the open editor, then perform the Capstone WAN, live WP, AP, and rack-rename Play Mode checks specified in the remediation plan.

## Scope control

Only runtime-state remediation scripts and their tests are included in this commit. Existing rack-routing, scene, and prompt changes were deliberately left uncommitted.
