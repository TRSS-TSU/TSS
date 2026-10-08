# Completion Plan: Validation, Hotwash, and Git Closeout

## Objective

Verify the completed training loop, document actual results, and commit only scoped Layer 2/3 work.

## Dependencies

All completion plans 01–05.

## Implementation

1. Run all existing EditMode tests plus focused profile/lifecycle, CLI, evaluator, endpoint, binding, LED, and monitor tests.
2. Run fresh Balanced-scenario Play Mode walkthroughs: correct WP2, incorrect WP2, correct WP3, incorrect WP3, endpoint Apply, printer/server behavior, and Layer 1 placement/cabling/termination regression.
3. Inspect Console for compile errors, null bindings, repeated warnings, and refresh/log storms; distinguish known MCP serializer/preview warnings from project faults.
4. Create `Docs/After Layer 2 Layer 3 Device Session Implementation.md` using actual results, including verification table, manual inspector checks, deviations, and resume point.
5. Review only intended files; stage only scoped implementation assets/scripts/tests/docs; run `git diff --cached --check`; commit `Implement Layer 2 and Layer 3 device sessions`; push without creating a PR.

## Acceptance

- Compile and test results are recorded, not inferred.
- Correct and intentional-fault workflows prove first-failure diagnostics.
- Hotwash lists every manual remaining item or explicitly says none.
- No unrelated existing dirty work is staged or rewritten.
