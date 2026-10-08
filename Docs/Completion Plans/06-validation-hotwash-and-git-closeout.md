# Completion Plan: Validation, Hotwash, and Git Closeout

## Outcome

Prove the finished Layer 2/3 training loop in the Unity Editor, record actual evidence and remaining gaps, then create a narrow commit containing only this work. Nothing is “validated” merely because a script imports.

## References and Current Starting Point

- [Master implementation plan — complete acceptance plan](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#10-test-and-acceptance-plan)
- [Master implementation plan — implementation pass-off](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#11-implementation-pass-off-and-hotwash-documentation)
- [Plan 02 — evaluator validation](02-deterministic-reachability.md#tests)
- [Plan 03 — desktop validation](03-endpoint-desktop-and-printer-ui.md#tests)
- [Plan 04 — SOP/evaluation validation](04-work-package-evaluation-and-sop.md#tests)
- [Plan 05 — visual/binding validation](05-bindings-leds-monitors-and-prefabs.md#tests)
- `Assets/Tests/EditMode/TssDeviceSessionTests.cs` — focused test home.
- `Docs/After Layer 2 Layer 3 Device Session Implementation.md` — required evidence-based handoff document to create at completion.

Prior work imported newly added scripts and had a clean immediate Console, but did not execute the complete evaluator/work-package test matrix or a full Play Mode acceptance walkthrough. Treat those items as outstanding until results are recorded.

## Dependencies and Safety Rules

Run this only after Plans 01–05 are implemented. The worktree is already dirty: identify the exact Layer 2/3 scripts, tests, generated UI prefabs, server prefab, scenario asset, monitor images/import metadata, and documentation before staging. Do not revert, reformat, or stage unrelated user work.

## Validation Sequence

### 1. Establish a reproducible baseline

1. Record Unity version, active scene, branch, current commit, and whether the worktree contains pre-existing unrelated changes.
2. Trigger Unity import/compile and inspect the Console after it settles.
3. Record project errors/warnings separately from known tool-side serializer/preview notices; do not discard a project warning merely because other warnings are known.

Pass condition: zero new compile errors and no unexplained repeated null/binding/refresh errors introduced by this scope.

### 2. Run automated tests

Run all existing EditMode tests, then the focused Layer 2/3 tests. The evidence record must include count, pass/fail status, and any skipped tests/reason. At minimum, cover:

| Area | Minimum automated proof |
| --- | --- |
| Profile/lifecycle | same seed stability; reset clears state/evidence/snapshots; device reuse does not inherit state |
| CLI | modes, aliases/ranges, valid-wrong persistence, malformed input atomicity, running/startup save behavior |
| Reachability | correct path; every named L1/L2/L3 fault; lowest-layer first-failure rule; unknown-address timeout |
| Endpoint/printer | atomic Apply; persisted reopen; endpoint-originated evidence; source-dependent print job |
| Work packages | required typed evidence, save checks, checkpoint snapshot persistence |
| Bindings/visuals | post-bind suffix matching, LED L1-only behavior, monotonic monitor state, no material churn |

If automated Play Mode tests do not already exist, do not add a broad framework just to close out. Record the manual Play Mode walkthrough below.

### 3. Run the fresh-scenario Play Mode matrix

For every row, start with a recorded new Balanced-scenario seed and capture observed result/Console status:

| Workflow | Proof required |
| --- | --- |
| Profile/SOP | Phase 9–19 values are coherent and unchanged after all session canvases open/close; a new run resets them. |
| WP2 correct | typed switch configuration, typed show evidence, save, evaluator passes. |
| WP2 faults | one wrong access VLAN, allowed list, native VLAN, and shutdown-state repair each produces Layer 2 after L1 passes. |
| WP3 correct | typed router/subinterface/WAN/default route, route evidence, endpoint diagnostics, save, evaluator passes. |
| WP3 faults | gateway/mask, dot1q, parent interface, WAN, and default route each return Layer 3 after lower layers pass. |
| Endpoint UI | invalid Apply changes nothing; valid-wrong value persists; `ipconfig`, `ping`, `tracert`, and `cls` are typed. |
| Server/printer | no fabricated leases/clients; failed and successful jobs reflect source-to-printer reachability. |
| Layer 1 regression | checkout, placement, naming, pickup, cabling, termination, and effective-path behavior still work. |
| Visual/UI | LEDs, six monitor screens, input lock, close behavior, 1280×800, and 4:3 canvas layout work. |

For intentional faults, repair in place without restarting. This proves state updates and first-failure precedence rather than only a clean-start happy path.

### 4. Inspect assets and bindings in the Editor

Inspect the generated terminal/desktop canvas prefabs, two first-person-control prefabs, `DeviceSessionManager`, `Server_Prefab`, monitor source prefab/scene, and `Balanced_12_Piece_Lab.asset`. Confirm the expected serialized references and inactive-default canvas state. Preserve object names, endpoint IDs, endpoint display labels, wallport names, renderer references, and material references.

### 5. Write the evidence-based hotwash

Create `Docs/After Layer 2 Layer 3 Device Session Implementation.md` only after validation. Include:

1. Plain-language completed/partial/not-tested status.
2. Unity version, scene, branch, seed(s), and dirty-worktree caveats.
3. Changed scripts/assets/prefabs/tests and intentionally untouched assets.
4. A verification table listing each automated/manual check with actual result.
5. Console findings, known non-project notices, deviations, and an exact resume point.

Do not copy future-tense claims from a plan or include a commit SHA that does not exist yet. Report the eventual commit SHA only after the commit/push succeeds.

### 6. Perform scoped Git closeout

1. Review status and diffs for only the intended paths.
2. Stage only approved Layer 2/3 scripts, tests, generated prefab assets/meta, scenario asset/meta, monitor images/meta, and docs.
3. Inspect the staged diff, then run `git diff --cached --check`.
4. Commit with `Implement Layer 2 and Layer 3 device sessions`.
5. Push the current scoped branch without creating a pull request unless explicitly requested.

If an unrelated dirty file is needed to compile, stop and report it rather than silently absorbing it into this commit.

## Exit Criteria

- Unity compilation and all executed tests have recorded results.
- Correct and intentional-fault workflows demonstrate first-failure diagnostics in Play Mode.
- Hotwash distinguishes completed, deferred, failed, and untested items.
- The commit contains only scoped assets, passes staged whitespace validation, and push status is recorded.

## Rollback

Validation writes only the hotwash document and the intentional commit. If a regression appears before commit, keep diagnostic evidence, revert only the responsible scoped change, and rerun the affected automated plus Play Mode checks. If push fails, retain the local commit and report the remote error; do not rewrite unrelated history.
