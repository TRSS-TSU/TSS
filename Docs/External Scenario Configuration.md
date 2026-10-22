# External scenario configuration

## Intent and scope

Four local JSON scenarios can configure the existing TSS facility without changing scene geometry, Unity asset references, endpoint labels, or the training-session singleton. This is the Data and launch/UI boundary. Navigation, aircraft, map and FMS systems are outside this TSS implementation.

The existing `Balanced_12_Piece_Lab.asset` remains the built-in fallback. No authored scenario is modified by loading JSON.

## Files to edit

`Assets/StreamingAssets/Scenarios/manifest.json` lists exactly four entries. Each entry has `scenarioId`, `displayName`, `sceneName` (`TSS`), `jsonFile`, and optional `description`. The ID, display name and scene name must match the scenario file.

The supplied files are:

| File | Exercise |
| --- | --- |
| `balanced-lab.json` | Export of the existing balanced scenario |
| `installation-practice.json` | Extra equipment/cables and second-rack-first placement phases |
| `network-verification.json` | Different VLAN bounds/names and diagnostic evidence |
| `wireless-readiness.json` | Wireless-focused instructions, spares and diagnostic evidence |

The manifest identity is `"format": "tss-scenario-manifest"`, `"schemaVersion": 1`. Scenario files use `"format": "tss-scenario"`, `"schemaVersion": 1`. Unsupported versions fail before scene launch. Filenames may contain ASCII letters, digits, hyphens and underscores followed by `.json`; paths, remote URLs and traversal are rejected. Files are resolved inside the `Scenarios` folder.

## Version 1 contract

`TssExternalScenarioData.cs` is the serializable contract. Start from a supplied file; required lists must be present (use `[]` for an empty list), and list rows cannot be null.

| Field | Contents |
| --- | --- |
| `scenarioId`, `displayName`, `sceneName` | File identity and menu destination |
| `inventory` | `equipmentId`, non-negative `quantity` |
| `cableInventory` | `cableId`, non-negative `quantity` |
| `defaultPlayerCableType` | Cable enum value |
| `cableTypeRequirements` | Unique `requirementId`, two endpoint IDs, required cable type |
| `permanentConnections` | Unique `connectionId`, `cableId`, cable type, fixed endpoint IDs, punch-down flag and unique termination module IDs |
| `cableSchedule` | Display rows: from/to endpoint labels, ports and cable type |
| `namingRows` | Existing rack ID, slot description, device description, assigned name and rationale |
| `racks` | Existing rack ID, U bounds, installable bounds and reserved U positions |
| `expectedRackPlacements` | Phase, rack ID, starting/ending U, category and required name; ending U `0` means exact placement |
| `expectedEndpointPlacements` | Phase, station ID, category and required name |
| `networkScenario` | Existing network configuration: enable flag, baseline profile seed, VLAN names/bounds, DHCP network names, aliases, role bindings, required ports and evidence |
| `binderReference` | All seven current binder sections, including configured paragraphs, headings and rows |

Unity enum values are numeric in version 1:

- Cable: `0` straight-through, `1` crossover, `2` rollover.
- Equipment: `0` router, `1` switch, `2` UPS, `3` PDU, `4` server, `5` patch panel, `6` desktop, `7` printer, `8` access point, `9` laptop.

Unity GUIDs, instance IDs, prefabs, transforms and generated student data are not part of the DTO. The Unity catalog resolves equipment and cable IDs to assets. Pure authored rows are copied by value into a transient `TssScenarioDefinition` so existing consumers continue using the same model.

## Facility and simulation constraints

`Assets/Resources/TssScenarioCatalog.asset` contains the shipped equipment/cable references and a snapshot of the existing TSS rack, station and fixed endpoint IDs. The editor command **TSS → External Scenarios → Rebuild Asset and Facility Catalog** reads the facility in a preview scene and updates only the catalog. Rebuild this catalog after intentionally changing facility IDs or equipment assets. It does not rename or rewrite the scene.

This format configures the current simulator, not an arbitrary network topology engine. Keep its supported roles (`AN-1`, `AN-2`, `CN-1`), endpoint role names and interface conventions. VLAN bounds must be multiples of ten between 10 and 390, providing at least four distinct values; Dead VLAN remains 888. Required ports reference the existing access-switch roles and real equipment interfaces. Placement requirements must reference available equipment categories and sufficient inventory. Cable-schedule endpoints are human-readable labels, whereas permanent connections and cable requirements use stable physical IDs.

The existing evidence processor matches diagnostic commands. An evidence ID is an identifier, not an enforcement rule for a particular source device. Instructors should describe source-specific checks in the instructions; use commands supported by the current endpoint processor.

Cable requirements tied to a rack role follow that correctly named/category-matched device when it is installed within its authored U range. For example, the baseline CN-1 role references U30, but the rack blocks U30/U31 and permits CN-1 at U32–U34. Requirement matching resolves the actual installed port; the JSON, original ScriptableObject, permanent facility endpoints and physical endpoint IDs remain unchanged. A wrong rack, name, category or position outside the allowed range does not satisfy the requirement.

## Student-specific values and binder content

The menu retains the last-name and positive computer-number inputs. For example, ` Smith ` and `017` become `SMITH-17`. `BeginScenario` still derives the seed from normalized student ID plus scenario ID for generated addresses, VLANs, and other topology values. The instructor-authored circuit ID, SSID, passphrases, channel, device passwords, and MOTD come from `networkScenario` in JSON and are not generated from that seed.

Do not put generated per-student addresses or topology values in JSON. Keep binder placeholders such as `{ScenarioId}`, `{ManagementVlan}`, `{ReceptionVlan}`, `{DataVlan}`, `{NativeVlan}`, `{DeadVlan}`, `{WanProviderAddress}`, `{WanCustomerAddress}`, `{ApManagementAddress}` in the sections that already support them. The instructor-authored values `{CircuitId}`, `{Ssid}`, `{WifiPassphrase}`, `{WirelessChannel}`, `{ApAdminPassword}`, `{EnableSecret}`, `{ConsolePassword}`, `{VtyPassword}` and `{Motd}` are configured under `networkScenario` and resolved into the binder from that scenario data. Static instructions and fixed authored training values remain ordinary text.

## Loading and failure behavior

`TssExternalScenarioLoader.LoadManifestAsync()` reads the manifest with `UnityWebRequest`. Duplicate or invalid entries are excluded with visible menu warnings. Startup validates each candidate scenario before adding its button. Selecting a button reloads and revalidates the file before setting the existing launch handoff and loading the scene. Buttons are disabled during loading to prevent duplicate launches.

If the manifest fails, the menu lists every non-null serialized scenario, or the session fallback if no serialized choices exist. If only some external scenarios fail, valid choices remain and the built-in fallback is added. A file that becomes invalid after menu loading cannot partially launch; the student remains in the menu with fallback available. Detailed validation messages use the `[TSS Scenarios]` prefix in the Unity Console.

Runtime scenario ownership moves from the loader to launch handoff to the existing training session. Replaced/unloaded runtime definitions are destroyed. Authored ScriptableObjects are never destroyed by this cleanup.

## Validation

Use **TSS → External Scenarios → Run Edit Mode Tests** and **Run Play Mode Tests**. Results are written under `Temp/TssScenarioTests/`. The Edit Mode command includes the existing regression suite; unrelated existing failures should be recorded separately from the new `TssExternalScenarioTests` results.

The Play Mode tests drive real MainMenu buttons and scene loads, verify four choices, normalized student ID, single active session, inventory/cables/permanent connections, deterministic profiles and binder tabs. They install every required rack and endpoint device, require placement evaluation to pass, connect every required cable, and require the network evaluator's cable conditions to clear. Missing/malformed manifest, invalid scenario, and a file changed after menu load exercise the fallback paths. These file-failure tests temporarily alter the two supplied baseline files and restore them in teardown; run them against the supplied samples in the Editor.

Manual visual check: enter Play Mode in MainMenu, choose each scenario, inspect all seven binder tabs, place equipment following its phases, and confirm cable/evaluation behavior. Return to MainMenu between choices. Test the intended device build separately; Editor testing does not prove device-specific packaging or performance.

Expected successful state: four enabled external buttons, `Choose a scenario to begin.`, one `TSS Scenario Runtime` session after launch, and no scenario-loader warnings. Failure tests intentionally produce `[TSS Scenarios]` warnings and display a built-in fallback.

## Export and rollback

Verification checkpoint (2026-10-20): 40/40 focused Edit Mode tests and 5/5 Play Mode tests passed, including actual required equipment installation and cable connections. The menu was visually inspected. The broader Edit Mode suite has 18 unresolved failures (107 passes); see `PROGRESS.md` for their names, remaining device/full-network verification limits and recovery details.

**TSS → External Scenarios → Export Four Sample Scenarios** explicitly rewrites the supplied four samples and manifest from the baseline asset. Back up instructor edits before running it. Nothing exports automatically during play, import or build.

To use the original behavior without removing code, remove/rename the external manifest and launch the built-in fallback. To roll back the feature, revert the scenario/menu/session changes and remove the new catalog, external-data/loading utilities, samples and tests. Scene and prefab bindings do not need rollback because this feature does not edit them.

The instructor web editor and remote HTTPS delivery remain deferred. A future editor should emit this versioned contract and validate the same facility/simulator constraints rather than editing Unity assets.
