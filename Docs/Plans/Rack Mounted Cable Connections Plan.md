# Rack-Mounted Cable Connections Plan

## Summary

The goal is to bring rack-mounted equipment up to the same connection standard as wallports, desktop PCs, and printers: rack devices must support first-person port selection, visible cable connections, and permanent patch-panel-to-wallport records. Current code already binds rack equipment ports during install, but installed rack equipment still opens the correction panel instead of entering cable-view.

## Key Changes

- Add rack cable-view entry:
  - Update `RackMountController.ClickInstalled(...)` so when the player is holding a cable and the installed rack item contains `TssObjectInteractable`, it enters first-person cable selection instead of opening the correction panel.
  - Keep existing correction/pickup behavior when no cable is held.
- Verify and configure rack equipment ports:
  - Confirm router, switch, server, and patch panel equipment assets have accurate `interfaces`, `connectorType`, `supportedCableTypes`, and `portAnchorPath`.
  - Ensure each rack prefab has `TssObjectInteractable`, `TssPortSelectionAdapter`, usable port colliders, and a tuned `FirstPersonView`.
  - Restore/create the active patch panel equipment definition if needed, using `TSS_PatchPanel_1U.prefab` and explicit patch panel port interfaces.
  - Confirm rack equipment cannot be picked up until connected cables are removed.
- Add permanent patch-panel-to-wallport records:
  - Add a scenario-level permanent connection list containing cable type, patch-panel endpoint ID, and wallport endpoint ID.
  - On scenario start and after rack installs, resolve those endpoint IDs and create non-inventory physical connection records.
  - Permanent records appear in the connection HUD within their own permanent infrastructure panel.
- Cable path routing:
  - Use the existing `TssCableRouteProvider` and `TssCableRouteNode` graph.
  - Manually place route nodes around racks, wall runs, and desk areas so cables route through clean anchor paths instead of direct point-to-point segments.
  - Provide instructional guidance for manual route node setup, allowing manual configuration of each possible cable run.

## Suggested Unity Project Usage

- Connecting to Unity
  - Do not use the Unity CLI, as the Unity editor is opened.
  - Use the connected unity_mcp server for examining and changing the objects/assets in the project.

## Test Plan

- Place rack mounted switch/router/server, then pick up a cable and click installed rack equipment.
- Confirm first-person camera enters the rack device view and only valid port objects highlight/select.
- Connect rack mounted equipment to wallport endpoints via the patch panel and verify cable visual starts/ends at port centers.
- Confirm permanent patch-panel-to-wallport records exist.
- Confirm connected rack equipment cannot be picked up until cables are removed.
- Confirm route nodes improve cable path shape and fallback still draws direct segments when no provider exists.

## Assumptions

- Keep implementation small: reuse `TssPortEndpointBinder`, `TssPortSelectionAdapter`, `TssObjectInteractable`, and `TssCableRouteProvider`.
- Treat patch-panel-to-wallport links as scenario infrastructure connections, not player inventory cables.
- Do not remove correction-panel behavior; only bypass it when the player is holding a cable and cable-view is possible.
- Manual work includes tuning first-person camera transforms and route-node placement in the scene.

## Final Feedback

- Provide clear summary of changes.
- Provide suggested next steps.
- Provide guidance about configuring cable path routing nodes.
