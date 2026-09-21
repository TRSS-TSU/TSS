# Implementation Prompt: TSS Rack Installation and Scenario MVP

Implement `Docs/Plans/TSS-Rack-Installation-Scenario-MVP.md` in `C:\Unity\TSS`.

Start by inspecting the current scene, `Assets/Scripts`, equipment-case placement, the two rack roots, and the existing `TssObjectInteractable` flow. Preserve unrelated assets and scene bindings.

## Required behavior

1. Add a startup UGUI scenario menu. It initially offers one Balanced 12-piece lab scenario and loads `SampleScene` after selection.
2. Define the scenario as a ScriptableObject. It owns the inventory, SOP-guide content, scene target, and rack configuration.
3. Define equipment as ScriptableObjects. The starting scenario provides 2 routers, one Cisco 3550 switch, one Cisco 2960 PoE switch, 2 UPSs, 2 PDUs, and 4 servers. Reuse the existing generated/imported models, including the two existing switch models.
4. Add a runtime session owner that persists only while the scenario is running. It owns remaining inventory, one held item, and installed-equipment records. UI must not be the state authority.
5. Configure both student racks for U1 through U40. Only U3 through U39 are installable; U1/U2 are blocked and U40 is permanently reserved for the existing patch panel. A 2U item can start only at U3 through U38.
6. Remove existing student equipment/interactions from the two racks, but do not move, delete, or regenerate the permanent purple wiring, patch panels, rack frames, or unrelated scene objects.
7. Make both equipment cases interactive. Reuse `TssObjectInteractable` for its first-person camera transition, then show a case-specific inventory panel. Selecting equipment decrements stock, attaches a non-physical visual to a serialized right-hand carry anchor, closes the panel, and returns the player to third person. Do not allow checkout of a second item while one is carried; allow returning the carried item at a case.
8. While holding equipment and within a rack interaction area, highlight only valid starting U positions. A click/tap on a highlighted position installs the item at that starting U. Validate the complete 1U/2U span before changing inventory or creating a model. Invalid positions must not change state.
9. Add UGUI for the carried-item HUD, case inventory, scenario selection, and an expandable SOP guide. The SOP guide is reference-only, scenario-derived, and not graded.
10. Keep future ports/interfaces data-ready in the equipment definition and installed record, but do not add cabling, port UI, grading, persistence, or multi-scenario authoring in this implementation.

## Constraints

- Preserve the existing third-person controller and use the existing first-person interaction transition only for equipment-case inspection.
- Keep world scale and scene placement unchanged; calibrate equipment placement through serialized rack/carry anchors and offsets.
- Keep responsibilities separate: scenario assets define data; session/rack scripts own runtime state and validation; UI displays state and sends requests; visuals do not become state.
- Prefer the smallest additive change. Do not introduce a new package, broad refactor, or replacement interaction framework.
- Add one focused Edit Mode test for rack-span validation: valid 1U/2U placement, overlap, U1/U2 rejection, U40 rejection, and 2U overflow at U39.

## Completion checks

- Unity compiles with no new errors.
- The Balanced scenario can be selected and starts with both racks empty except for permanent fixtures.
- Each equipment case shows available finite inventory; carrying and returning an item update the count correctly.
- 1U and 2U equipment install in valid U positions on either rack; invalid positions do nothing except show feedback.
- U40 remains unavailable and visibly occupied by the patch panel.
- The SOP guide, held-item HUD, existing doors, and existing non-rack inspection interactions work without camera or player-control lockups.

Report the files changed, the created ScriptableObject assets, the Play Mode checks performed, and any asset/reference that needs manual Unity Inspector assignment.
