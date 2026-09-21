# TSS Rack Installation and Scenario MVP

## Summary

Build one scenario-driven lab loop: select the Balanced 12-piece scenario, choose equipment from cases, carry it in third person, and install it into valid highlighted rack positions. Runtime state remains the authority; UI only presents and actions it.

## Key Changes

- Add `TssScenarioDefinition` ScriptableObject for the scene, inventory, SOP-guide sections, and two rack configurations: `Rack01` and `Rack02`.
- Model each rack as U1 through U40:
  - U3 is the lowest installable position.
  - U3 through U39 accept student equipment.
  - U40 is permanently occupied by the patch panel.
  - U1 and U2 are non-installable.
  - A 2U item may start only at U3 through U38.
- Add `EquipmentDefinition` ScriptableObjects for the initial 12-piece loadout: 2 routers, a Cisco 3550 switch, a Cisco 2960 PoE switch, 2 UPSs, 2 PDUs, and 4 servers.
- Add `TssTrainingSession` to initialize scenario inventory, track one carried item, and record installed equipment by rack, definition, and starting U.
- Add `RackMountController` to both existing racks. It validates occupied/reserved spans, highlights valid direct-install positions, and places models using calibrated rack anchors.
- Remove current student equipment/interactions from both racks; retain rack frames, purple wiring, and permanent patch panels.
- Reuse `TssObjectInteractable` for equipment-case first-person inspection only. Add `EquipmentCaseStation` to open the case catalog, issue/return finite inventory, and return the student to third-person carrying mode.
- Add a right-hand carry anchor and HUD. The carried model is visible in third person and has no physics collision.
- Add UGUI scenario selection, case catalog, carried-item HUD, and expandable scenario SOP guide. The guide is reference-only and does not grade placements.

## Interfaces and State

- `EquipmentDefinition`: identity, category, U height, display/carry prefabs, offsets, and future interface metadata.
- `TssScenarioDefinition`: inventory quantities, rack U-range/reservations, SOP content, and destination scene.
- `TssTrainingSession`: selected scenario, held item, remaining stock, and installed-equipment records.
- `RackMountController`: accepts `TryInstall(definition, startingU)` and rejects any span outside U3 through U39 or intersecting existing equipment/U40.

## Test Plan

- Edit Mode test: 1U/2U fit validation, overlap rejection, U1/U2 rejection, U40 rejection, and 2U overflow at U39.
- Play Mode: select the scenario, withdraw/return equipment, carry it, install 1U and 2U items across both racks, verify invalid-slot feedback, inventory counts, SOP-guide access, and scene-reset behavior.
- Confirm existing door and inspection interactions continue working and no camera/control lock remains after closing a case.

## Assumptions

- U40's permanent patch panel remains visible and non-removable.
- Port/interface metadata is stored only; cabling, port interaction, grading, save/load, and additional scenarios are later milestones.
