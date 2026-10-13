# Player Seeded Binder and Wireless Capstone

## Summary

Use the player's last name and computer number to create one repeatable network profile when the Balanced lab starts. The Binder shows that profile alongside the scenario's authored rack and cable instructions. Evaluators read the same profile, so the values shown to the player are the values required for success. Add the wireless AP and test laptop needed to complete and grade WP4.

## Implementation

- **Menu and run state:** Add separate last-name and computer-number inputs to the scenario menu. Require a nonempty name and positive number; trim and normalize the inputs so case and surrounding spaces do not change the result. Pass the resulting student ID with the selected scenario into `TssTrainingSession`. Derive a stable seed from the student ID and scenario ID. A new run with the same ID gets the same answers but fresh gameplay state.
- **Scenario data and grading:** Extend the existing `TssNetworkRunProfile`; keep `NetworkScenarioConfig.profileSeed` as the direct-scene editor fallback. Generate four distinct VLAN IDs in tens from 10–390, keep Dead VLAN 888, generate three distinct `192.168.x.0/24` LANs, a `10.x.x.0/30` circuit, host addresses, circuit ID, credentials, SSID, passphrase, AP admin password, and channel 1, 6, or 11. Keep the ScriptableObject unchanged at runtime. Update hardcoded address assumptions so WP2, WP3, commands, and reachability all grade against this profile.
- **Binder display:** Turn the current SOP presentation into an independent, scrollable Binder above device-session canvases. Provide the seven storyboard tabs: Naming & Racks, VLANs, IP Plan, Cable Schedule, Circuit Letter, Wireless, and Credentials. Draw static rack and cable instructions from the current playable scenario, whose room and rack layout differs from the screenshots; draw assigned values from the active profile. Open it with a HUD button or Tab, and allow it to remain visible while using a terminal or desktop.
- **Wireless equipment and play:** Create AP and laptop 3D prefabs consistent with existing equipment, with readable ports, status, and laptop screen. Append their equipment categories without changing existing serialized category numbers. Add both to the equipment box. Use the seating area near `ReceptionDesk` as the Guest Lounge: add a ceiling AP mount and laptop table without rewiring existing desks. Give the AP a ceiling drop through unused Rack01 patch-panel Port17, then require a patch cord to AN-1 Fa0/7 in the seeded Reception VLAN. Add an IT1 browser view for AP configuration and a dedicated laptop desktop prefab for Wi-Fi join, CMD, and a simulated RedWolf Search page.
- **Wireless evaluation:** Add AP configuration and laptop association to session-owned network state. The AP management address is the seeded Reception subnet's `.50` address so IT1 can reach it. A laptop may associate while the uplink is down, but receives its deterministic Reception address only when the AP's wired path is usable. WP4 completes only after correct placement/name, structured uplink and Fa0/7 configuration, seeded AP security settings, laptop association and addressing, successful gateway/provider diagnostics, and the page load. Preserve the existing WP2 completion snapshot when Fa0/7 is later reopened.

## Validation

- Edit Mode checks cover repeatable IDs, distinct assigned values, Binder/evaluator agreement, wrong seeded answers, AP security mistakes, broken uplink, incorrect Wi-Fi passphrase, missing lease, and WP4 completion.
- In Play Mode, start as two different students and confirm different Binder answers; restart one ID and confirm identical answers with reset progress. Complete WP1–WP3, then mount and configure the AP from IT1, join from the laptop, run `ipconfig` and both pings, and load the page. Repeat with each wireless fault to confirm the correct layer fails.
- Confirm the Binder remains usable during terminal and desktop interactions, existing scene bindings remain intact, Unity compiles, and no credentials or correct answers appear in debug logs.

## Assumptions and rollback

The document's pictured values are examples; the generated values control grading. Save, Export Code, Load From Code, instructor sign-offs, and completion reports remain a later phase. Keep the current scenario asset and prefabs intact where possible, review existing dirty changes before editing, and make the new profile, Binder, equipment, and lounge additions reversible as a scoped change.
