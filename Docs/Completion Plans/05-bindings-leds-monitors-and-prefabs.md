# Completion Plan: Bindings, LEDs, Monitors, and Generated Prefabs

## Outcome

Finish the visual adapters only after state and reachability are authoritative: endpoint bindings resolve deterministically, LEDs report physical/admin link only, monitors show monotonic L1/L2/L3 status, and generated UI prefabs retain their scene bindings.

## References and Current Starting Point

- [Master implementation plan — endpoint binding, LEDs, and screens](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#8-endpoint-binding-leds-and-third-person-screens)
- [Master implementation plan — prefab preservation](../../plans/layer-2-layer-3-device-session-ui-implementation-plan.md#73-prefab-and-scene-binding-preservation)
- `Assets/Scripts/TssPortEndpointBinder.cs` — now exposes post-bind consumer notification; preserve the existing endpoint IDs rather than rewriting them.
- `Assets/Scripts/TssDeviceLinkIndicator.cs` — has suffix resolution and shared-material work started.
- `Assets/Scripts/TssComputerConnectivityPresenter.cs` — needs final evaluator-based layer derivation.
- `Assets/Scripts/MonitorTextureRotator.cs` — has serialized layer texture fields and `SetLayerStates`, but the shared prefab/scene assignments still need verification.
- `Assets/Prefabs/Server_Prefab.prefab` — GE00 LED suffixes were corrected to GE01; retain renderer/material references.
- `Assets/Art/MonitorDiagnostics/monitor_layer[1-3]_(connected|disconnected).png` — six imported diagnostic textures.
- `Assets/Editor/TssDeviceSessionPrefabBuilder.cs` — builder source for the generated UI prefabs.

The prior pass created the data/API surfaces and imported monitor art, but no evidence yet proves all runtime monitors have populated serialized textures, all dynamic bindings resolve, or regenerated prefabs preserve their scene references.

## Dependencies and Guardrails

Complete Plans [02](02-deterministic-reachability.md) and [03](03-endpoint-desktop-and-printer-ui.md) first. Existing `TssPortEndpoint` inspector fields, Endpoint IDs, display labels, Hierarchy names, wallport names, renderer references, and material references are protected configuration; consume them rather than renaming/rebuilding them.

## Implementation Steps

### 1. Finish post-bind endpoint resolution

In `TssPortEndpointBinder.ConfigureEndpoints`:

1. Collect resolved endpoints first.
2. Notify `ITssEndpointBindingConsumer` listeners after the loop is complete.
3. Prefer an explicitly serialized valid endpoint; otherwise match only an exact suffix segment (`id == suffix` or `id.EndsWith(":" + suffix)`).
4. Resolve the declared data interface only; never silently select Aux/Console.
5. Warn once for each unresolved declared binding and leave that consumer safely disconnected.

Do not have link/monitor consumers search before binding completes or mutate a serialized endpoint ID to make a suffix match.

### 2. Define LED behavior narrowly

`TssDeviceLinkIndicator` should be on only when the effective Layer 1 path and the associated interface's administrative/carrier state are valid. It should be off for missing cabling/path or an administratively shut interface, and remain on for wrong VLAN, trunk, IP, gateway, or route configuration.

Refresh from the existing session state event after cabling/CLI changes. Continue using `sharedMaterial` for the shared connected/disconnected assets; do not create a material instance during each refresh. Confirm the corrected server GE01 suffix drives every intended server link-light renderer.

### 3. Finish monitor texture selection

Retain `MonitorTextureRotator`'s existing MaterialPropertyBlock path and legacy `screens` behavior. `SetLayerStates(bool l1, bool l2, bool l3)` must select exactly three textures—one connected/disconnected choice per layer—and apply immediately.

Guard against incomplete serialized texture sets: retain the legacy screen sequence (or a neutral safe fallback) rather than replacing it with null textures. Assign the six imported 1024×576 assets at the workstation source prefab/scene level that supplies the required monitors. Verify Sprite/Single, sRGB, Clamp, Bilinear, mipmaps, and max-size 1024 import settings rather than recreating the images.

### 4. Drive monitor state from the evaluator

`TssComputerConnectivityPresenter` reads the shared evaluator/profile and sends only booleans to `SetLayerStates`:

- L1: complete physical path plus interface state.
- L2: active access VLAN continuity to the router trunk.
- L3: endpoint reaches its configured gateway.

Enforce monotonic state before rendering: `!L1` implies `!L2 && !L3`; `!L2` implies `!L3`. Update on post-bind and session-state changes; do not require opening a device UI.

### 5. Regenerate UI only from the builder

Finish `TssDeviceSessionPrefabBuilder.cs`, then regenerate the terminal and desktop session canvases and their two related first-person-control prefabs—no broad prefab rebuild. Inspect the scene afterward:

- `DeviceSessionManager` points to the regenerated canvas/control prefabs.
- Canvas instances remain inactive by default.
- EventSystem, sort order, close/Escape, gameplay lock, and presenter serialized references remain intact.
- Terminal has no legacy quick-command/live-answer sidebar controls.

## Tests

Extend `Assets/Tests/EditMode/TssDeviceSessionTests.cs` to cover:

- post-bind notification after all endpoints resolve;
- exact-suffix matching avoids a false partial-ID match;
- unresolved binding stays disconnected and warns once;
- GE01 controls the server's expected LED bindings;
- LED response changes for physical/admin state but not higher-layer errors;
- monitor health monotonicity and complete texture-set selection;
- repeated refreshes do not instantiate materials.

## Manual Play Mode Proof

In the Balanced lab, cable/unplug a workstation and administratively shut/no-shut an interface. Observe the LED and all three monitor layer screens. Then introduce a VLAN fault and a routing fault: LEDs stay lit while L2/L3 monitor status changes monotonically. Repeat after opening/closing sessions and after a regenerated-prefab scene reload.

Inspect the 13 monitor instances from normal third-person distance, verify legibility, and test session canvases at 1280×800 and a representative 4:3 iPad size.

## Exit Criteria

- Dynamic endpoints bind only after configuration is complete and no protected endpoint metadata is rewritten.
- Every required monitor has all six textures assigned and updates live without null textures or reopening UI.
- LEDs convey Layer 1/admin status exclusively; monitors convey monotonic L1/L2/L3 health.
- Generated prefabs retain scene-level references, input behavior, and layout.

## Rollback

If a visual assignment is missing, restore only that source prefab/scene serialized assignment and re-run the targeted builder. Do not edit every runtime monitor instance or duplicate material assets as a workaround.
