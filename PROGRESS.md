# TSS2 implementation checkpoint

Updated: 2026-10-20. Scope: `Docs/Plans/External Scenario Configuration Implementation Plan.md`.

**Current status:** implementation and focused Editor acceptance checks complete. No operation is interrupted or running. Remaining verification limitations and broader-suite failures are listed below; do not mistake them for a clean full-project test suite.

## Completed implementation

- Plan steps 1–9 are present: versioned external DTOs, Unity equipment/cable/facility catalog, validated UnityWebRequest loader, transient scenario adapter, four-entry manifest and samples, four-choice menu, existing launch handoff with runtime ownership cleanup, deterministic student/scenario network profiles, serialized fallback.
- VLAN binder names now use the loaded profile; other existing consumers still receive `TssScenarioDefinition`.
- Added editor catalog/export utilities, Edit Mode and Play Mode tests, and `Docs/External Scenario Configuration.md`.
- No authored scene, prefab, equipment or baseline scenario changes are intended. Existing scene bindings and singleton remain authoritative.

## Companion scenario editor

- Added a dependency-free local editor at `sites/tss-scenario-config-editor/dist/`. It loads the shipped four-file pack or instructor-selected JSON files, edits one scenario at a time, checks the browser-verifiable version-1 contract, previews authored binder text, recovers a local draft, and downloads scenario/manifest JSON.
- This editor deliberately does not try to replace Unity's asset/facility validation or publish/deploy JSON. Import its exports into `StreamingAssets/Scenarios` and run the existing Unity validation commands before release.
- Plan step 10 intentionally defers the instructor web editor and remote delivery. Flight/FMS/map fields are outside this TSS contract.

## Historical state at resume (superseded by results below)

- Saved Edit Mode run: 34/34 new tests pass; full suite 101 pass / 18 fail. Existing-suite failures have not been proved pre-existing with a baseline run.
- An earlier Play Mode run passed 5/5 (four scenario launches, binder tabs, inventory, handoff, deterministic profiles and fallback cases).
- Latest saved Play Mode run: 4/5 pass; expanded installation check failed when selecting router U30. Existing rack logic blocks U30/U31, while authored requirement allows U30–U34.
- The installation test was corrected to select a free U within the authored range. That correction compiled but its rerun was interrupted by the previous pause.
- No test was running when resuming. Changes are uncommitted; new files are untracked.

## Resume findings

- Corrected installation suite rerun passed **5/5** at 2026-10-20 23:05 UTC, including actual rack and endpoint placement/evaluation for all four external scenarios.
- Confirmed a real cabling mismatch beyond the old test assumption: `DemarcWan` references router U30 while installation uses an allowed U32. Added bounded scenario-requirement resolution in `TssTrainingSession` using existing role bindings and expected placement ranges. It changes matching only, not authored data or physical endpoint IDs.
- Added six focused matching tests (valid bounds, wrong slot/name/rack/category) and extended Play Mode acceptance to connect all required cables and check the network evaluator's cable conditions.
- Latest Edit Mode run (2026-10-20 23:08 UTC): **40/40 focused scenario tests pass**; full suite **107 pass / 18 fail**, the same existing-test failure count as before this resume.
- Extended Play Mode run (2026-10-20 23:09 UTC): **5/5 pass**. All four scenarios passed real menu launch, student-ID normalization, single-session identity, inventory/permanent-link configuration, all required rack and endpoint installations, placement evaluation, actual required cable connections, network evaluator cable conditions, and seven binder tabs. Four failure-path tests confirm fallback and rejection of partially invalid launches.
- Visually inspected `Temp/TssScenarioTests/MainMenu.png`: all four buttons and both inputs are readable and inside the panel. Live menu was ready with no errors; zero transient external scenario objects remained after startup validation.
- Confirmed all four files and the manifest were restored and identity-matched after failure tests. No Unity GUIDs or instance IDs occur in the external JSON.
- Changes remain uncommitted. No scene, prefab, baseline scenario, equipment asset, package or project-settings changes remain in the intended diff.

## Remaining tasks and next steps

1. No further external-scenario implementation change is currently identified by the focused tests. Preserve this verified state.
2. Before claiming full-project regression readiness, triage the 18 existing-suite failures below and establish an unchanged-baseline comparison. They were not fixed as part of external configuration.
3. Full manual completion of all network work packages (CLI configuration, routing, wireless, diagnostics) is not demonstrated by the scenario acceptance suite. It verifies generated profiles, binder values and the actual cable/evaluation path, not every network-training action.
4. Validate packaging, loading, UI and performance on the intended device build. Editor success does not prove device behavior.
5. Web editor/remote delivery and flight/FMS/map fields remain deferred by the original plan. Do not start them implicitly.

## Broader Edit Mode failures to investigate separately

The following 18 cases failed both before and after the resumed work. Their baseline status has not been proven with an unchanged checkout:

- `TssCableRouteProviderTests.FrontRackEndpointsChooseShortestAuthoredSide`
- `TssCableRouteProviderTests.RearRackEndpointsUseRearNodes`
- `TssDeviceSessionTests.BalancedScenarioAuthorsEveryNetworkScenarioList`
- `TssDeviceSessionTests.EndpointApplyCommitsValidButWrongGatewayAndReachabilityReportsLayer3`
- `TssDeviceSessionTests.EndpointDiagnosticsUseAuthoritativeReachability`
- `TssDeviceSessionTests.FirstPersonDeviceTitleRefreshesWhenNetworkHostnameChanges`
- `TssDeviceSessionTests.LongShowOutputStartsAtTopAndRequiresMorePages`
- `TssDeviceSessionTests.PreinstalledCableTraysExistInBothRacks`
- `TssDeviceSessionTests.PrinterTestPageRequiresRecordedWorkstationReachability`
- `TssDeviceSessionTests.ProviderAliasRecordsEndpointEvidence`
- `TssDeviceSessionTests.ShowCommandHistoryKeepsEachOutputBlockIndependent`
- `TssDeviceSessionTests.SwitchCliDisplaysVlanPortsAndInterfaceModesFromState`
- `TssDeviceSessionTests.WirelessStateBackedWorkPackageCompletesAfterLeaseDiagnosticsAndPage`
- `TssPhysicalConnectionPanelTests.ConnectionRowsAppendInstalledDeviceNames`
- `TssPlacementEvaluatorTests.AcceptsRequiredRackAndEndpointPlacements`
- `TssPlacementEvaluatorTests.PermanentScenarioConnectionsDoNotConsumeInventoryOrDisconnect`
- `TssPlacementEvaluatorTests.RejectsWrongRackNameSlotAndCategory`
- `TssPlacementEvaluatorTests.RenameChangesValidationResult`

## Recovery and safeguards

- Test entry points: `TssExternalScenarioTestRunner.RunEditMode()` / `RunPlayMode()`; menu commands under **TSS / External Scenarios**. XML results: `Temp/TssScenarioTests/`.
- Play Mode failure tests temporarily alter `Assets/StreamingAssets/Scenarios/manifest.json` and `balanced-lab.json`, restoring them in teardown. Confirm valid JSON after an interrupted test run.
- Do not rerun sample export over instructor edits; it rewrites all four samples.
- Final editor state: stopped in `Assets/Scenes/MainMenu.unity`; no active test or compilation. Test XML and screenshot files in `Temp/` are disposable; this checkpoint preserves their essential results.
- Current uncertainties: full network-workflow completion not demonstrated, 18 broader-suite failures untriaged, device build not tested. No flight-system behavior was exercised in this TSS project.
- Preserve all existing in-progress work. Do not commit/push unless requested.
