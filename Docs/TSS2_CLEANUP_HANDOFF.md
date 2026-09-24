# TSS2 Cleanup Handoff Plan

Date: 2026-09-24  
Project: `C:\Unity\TSS2`  
Git remote: `https://github.com/TRSS-TSU/TSS.git`  
Baseline: `main` at `d50442451c487d4f41b416d2f34541793ddc0ae3`

## 1. Start with the current-state evaluation in Unity MCP

Before changing files, connect to the active Unity Editor through Unity MCP and record:

- Editor state, project root, active scene, build settings, and compile status.
- Current console errors and warnings.
- Installed package list and direct/transitive package relationships.
- Project asset inventory and scene hierarchy for `MainMenu`, `SampleScene`, and any replacement scene.
- Which imported prefabs, models, materials, textures, animations, and scripts are referenced by scenes, prefabs, ScriptableObjects, serialized fields, and build settings.
- A baseline Play Mode smoke test: open the main training scene, enter the equipment workflow, interact with a rack/case, and confirm the project still compiles and runs before cleanup.

The current MCP evaluation found Unity 6000.3.21f1 open at `C:\Unity\TSS2`, idle, not compiling, with 1,170 indexed assets. The console already reports imported-package problems, including missing nested prefabs under `Assets/Imported/Brick Project Studio/...`, a moved Starter Assets URI, and mesh tangent warnings. Treat the project as needing cleanup and dependency repair; do not assume every imported package is valid or required.

## 2. Use Graphify to index the checkout and locate dependencies

Use the Graphify skill after the Unity baseline. Index the repository deliberately:

1. Inventory the full `C:\Unity\TSS2` folder, but exclude generated/runtime-heavy folders such as `Library`, `Temp`, `Logs`, and `UserSettings` from dependency conclusions.
2. Run a full asset/code graph over `Assets` once a Graphify backend is configured. The attempted full scan found 27 code files, 4 documents, and 263 images but could not complete semantic extraction because the OpenAI backend dependency/configuration was unavailable.
3. Use the completed script graph at `C:\Unity\TSS2\graphify-script-index\graphify-out\graph.json` as the starting code map. It contains 201 nodes, 428 edges, and 11 communities.
4. Query the graph for scene/prefab/script/material/texture references and use the reported source locations to build the keep list. Pay special attention to the bridge scripts `TssObjectInteractable`, `RackMountController`, and `TssRuntimeUi`.
5. Re-run/update the graph after each migration stage and compare the graph against Unity MCP console errors and serialized references.

## 3. Current checkout map

The clone currently contains approximately 67,315 files, including Unity-generated content. Relevant source content is concentrated here:

| Location | Approx. files | Role |
|---|---:|---|
| `Assets/Imported` | 1,821 | Imported packages to audit, reduce, and eventually ignore where safe |
| `Assets/Generated/Blender` | 60 | Project-generated rack/equipment assets; preserve as project-owned output |
| `Assets/Scripts` | 30 | Project runtime scripts |
| `Assets/Prefabs` | 11 | Project prefabs |
| `Assets/Scenes` | 4 | MainMenu, SampleScene, and scene variants |
| `Assets/Data` | 17 | Equipment and scenario definitions |
| `Assets/Materials` | 113 | Project materials plus imported-surface derivatives |
| `Assets/Animations` | 29 | Ready Player Me animation assets |
| `Assets/Tests` | 3 | Edit Mode tests |
| `Docs` | 87 | Project documentation and references |

Imported package groups currently include:

- `Assets/Imported/Brick Project Studio` — approximately 851 files.
- `Assets/Imported/ScifiOfficeLite` — approximately 490 files.
- `Assets/Imported/iPoly3D` — approximately 143 files.
- `Assets/Imported/Starter Assets` — approximately 192 files.
- `Assets/Imported/OfficePack` — approximately 71 files.
- `Assets/Imported/Scalable Grid Prototype Materials` — approximately 54 files.
- `Assets/Imported/CyberSoldier` — approximately 13 files.

## 4. Create the used-assets migration boundary

Create this project-owned folder:

`Assets/used_imported_assets/`

The goal is to stop treating entire imported packages as project dependencies. Do not bulk-copy packages. Instead:

1. Build an evidence-based keep list from Unity MCP, Graphify, `rg`/serialized YAML references, scene/prefab inspection, and Play Mode use.
2. Copy only the imported assets actually required by the project into `Assets/used_imported_assets/`, preserving enough subfolder structure and `.meta` files for Unity references to remain understandable.
3. Copy only imported scripts that are genuinely required at runtime or in the Editor. Review namespaces, assembly references, package assumptions, licenses, and hidden dependencies before copying.
4. Prefer project-owned derivatives already under `Assets/Generated/Blender`, `Assets/Materials`, `Assets/Prefabs`, and `Assets/Scripts` when they replace an imported source asset.
5. Record each copied item in a small inventory table containing original path, new path, reason retained, direct consumers, and validation status.
6. Keep the original imported packages intact during the audit. Do not delete or ignore them until the copied assets compile, reimport, render, and work in the baseline Play Mode flow.

## 5. Validate, then ignore imported packages

After migration, test in this order:

1. Unity reimport and compile with no new errors.
2. Open every build scene and verify missing-object, missing-script, missing-material, and missing-prefab references.
3. Run the equipment case, carry, rack install, interface, door, menu, and scenario workflows.
4. Verify generated Blender assets and project-owned materials still render correctly.
5. Run Edit Mode tests and a clean checkout/open test if practical.
6. Compare the Graphify graph before and after migration; any removed node with a live consumer is a stop condition.
7. Only after the above passes, add the imported package paths to the project’s ignore/cleanup strategy or remove unused package content in small, reversible batches.

The desired end state is that `Assets/used_imported_assets` is the explicit, auditable subset of imported content, while unused package trees are no longer carried as active project dependencies. Keep a rollback copy or Git commit before each deletion batch.

## 6. Package cleanup notes from prior sessions

The earlier TSS cleanup session removed several explicitly requested asset paths and direct package entries, but it also found that Timeline remained transitively referenced by Recorder. Preserve that lesson: remove direct dependencies only after checking the lock file and dependent packages. Do not remove a package merely because its name looks unused.

The older `C:\Unity\TSS` checkout contained substantial uncommitted feature work and is not the authoritative clean baseline. `C:\Unity\TSS2` was cloned cleanly from GitHub, but Unity has since written local `ProjectSettings` changes while opening the project. Review those changes before committing any cleanup work.

## 7. Resume prompt

> Start by evaluating `C:\Unity\TSS2` through Unity MCP. Then use Graphify to index the repository and trace which imported assets and scripts are actually referenced. Create `Assets/used_imported_assets`, copy only the proven-used imported content there, validate Unity compile and Play Mode workflows, and only then plan how to ignore or remove the unused imported packages. Preserve the project’s generated Blender assets, scene bindings, and rollback path.

