# Completion Plan: Bindings, LEDs, Monitors, and Generated Prefabs

## Objective

Finish the display adapters and serialized UI bindings after authoritative behavior is stable.

## Dependencies

`02-deterministic-reachability.md` and `03-endpoint-desktop-and-printer-ui.md`.

## Implementation

1. Keep `TssPortEndpointBinder` post-bind notifications and exact suffix matching. Resolve only declared data interfaces and warn once for unresolved bindings.
2. Verify every `TssDeviceLinkIndicator` uses effective Layer 1 plus administrative state, `sharedMaterial`, and GE01 server bindings. Higher-layer faults must not turn LEDs off.
3. Complete `MonitorTextureRotator.SetLayerStates`; assign all six imported textures to the shared workstation source prefab/scene configuration and preserve MaterialPropertyBlock rendering.
4. Update `TssComputerConnectivityPresenter` to use the shared evaluator and enforce monotonic L1 → L2 → L3 screen status.
5. Finalize `TssDeviceSessionPrefabBuilder` first, then regenerate terminal/desktop canvas and both first-person-control prefabs only.
6. Inspect scene `DeviceSessionManager` references, inactive-default canvases, EventSystem, sorting order, locks, and presenter serialized fields after regeneration.

## Acceptance

- All dynamic endpoints bind correctly after configuration.
- Every required monitor has six textures and updates without reopening its UI.
- Layer 1 failure forces L2/L3 disconnected; Layer 2 failure forces L3 disconnected.
- LEDs remain on during valid physical link plus VLAN/routing faults.
- Layout works at 1280×800 and representative 4:3 iPad resolutions.
