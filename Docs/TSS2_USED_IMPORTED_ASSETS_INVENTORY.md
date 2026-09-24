# TSS2 Used Imported Assets Inventory

Date: 2026-09-24
Branch: `codex/tss2-cleanup-recovery`
Baseline commit: `d50442451c487d4f41b416d2f34541793ddc0ae3`

## Migration Summary

`Assets/used_imported_assets/` is the project-owned boundary for imported content that is proven live in the TSS2 training scene.

Moved:

| Source package | Files retained |
|---|---:|
| Brick Project Studio | 56 |
| CyberSoldier | 6 |
| Scalable Grid Prototype Materials | 5 |
| ScifiOfficeLite | 54 |
| Starter Assets | 19 |
| **Total** | **140** |

Moved file types:

| Type | Count |
|---|---:|
| `.asmdef` | 1 |
| `.controller` | 1 |
| `.cs` | 7 |
| `.fbx` | 17 |
| `.inputactions` | 1 |
| `.mat` | 23 |
| `.png` | 60 |
| `.prefab` | 25 |
| `.shadergraph` | 1 |
| `.tga` | 4 |

Each retained asset was moved with its `.meta` file so Unity GUID references remain stable. The old `Assets/Imported/` package folders were not bulk-deleted.

## Direct Scene Consumers

These 23 imported assets were directly referenced by project-owned serialized files before migration. All direct consumers were `Assets/Scenes/SampleScene.unity`.

| Original path | New path | Reason retained | Validation |
|---|---|---|---|
| `Assets/Imported/Brick Project Studio/_BPS Basic Assets/_Prefabs/Basic Asset/Furniture_Props/BPS_Table_01.prefab` | `Assets/used_imported_assets/Brick Project Studio/_BPS Basic Assets/_Prefabs/Basic Asset/Furniture_Props/BPS_Table_01.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Sofa_Apt_02.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Sofa_Apt_02.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Table_Coffee_01.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Table_Coffee_01.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Table_Computer_01_Setup.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/Table_Computer_01_Setup.prefab` | Scene workstation reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/WallShelf_Apt_02.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Living Room/WallShelf_Apt_02.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Misc/Bench_Apt_02.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture/Misc/Bench_Apt_02.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Props/Art/WallArt_Apt_01.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Props/Art/WallArt_Apt_01.prefab` | Scene art reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/_Prefabs/Props/Art/WallArt_Apt_02.prefab` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/_Prefabs/Props/Art/WallArt_Apt_02.prefab` | Scene art reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Brick Project Studio/Apartment Kit/Common/Materials/Surface/Fabric_01_White.mat` | `Assets/used_imported_assets/Brick Project Studio/Apartment Kit/Common/Materials/Surface/Fabric_01_White.mat` | Scene material reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/CyberSoldier/CyberSoldier.fbx` | `Assets/used_imported_assets/CyberSoldier/CyberSoldier.fbx` | Player model reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/CyberSoldier/Materials/CyberMat.mat` | `Assets/used_imported_assets/CyberSoldier/Materials/CyberMat.mat` | Player model material | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Scalable Grid Prototype Materials/Materials/Ground/Blue_Ground_Prototype.mat` | `Assets/used_imported_assets/Scalable Grid Prototype Materials/Materials/Ground/Blue_Ground_Prototype.mat` | Prototype floor material | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light Bright.prefab` | `Assets/used_imported_assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light Bright.prefab` | Scene lighting reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light.prefab` | `Assets/used_imported_assets/ScifiOfficeLite/Prefabs/Lighting/Ceiling Lights/2D/Ceiling Light.prefab` | Scene lighting reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/ScifiOfficeLite/Prefabs/Shelf with Crates.prefab` | `Assets/used_imported_assets/ScifiOfficeLite/Prefabs/Shelf with Crates.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/ScifiOfficeLite/Prefabs/Shelf without Crates.prefab` | `Assets/used_imported_assets/ScifiOfficeLite/Prefabs/Shelf without Crates.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/ScifiOfficeLite/Prefabs/Tables/Metal/Table Metal Variant.prefab` | `Assets/used_imported_assets/ScifiOfficeLite/Prefabs/Tables/Metal/Table Metal Variant.prefab` | Scene furniture reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions` | `Assets/used_imported_assets/Starter Assets/Runtime/InputSystem/StarterAssets.inputactions` | Player input asset | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/InputSystem/StarterAssetsInputs.cs` | `Assets/used_imported_assets/Starter Assets/Runtime/InputSystem/StarterAssetsInputs.cs` | Player input script | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller` | `Assets/used_imported_assets/Starter Assets/Runtime/ThirdPersonController/Character/Animations/StarterAssetsThirdPerson.controller` | Player animation controller | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/ThirdPersonController/Prefabs/MainCamera.prefab` | `Assets/used_imported_assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/MainCamera.prefab` | Scene camera prefab reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab` | `Assets/used_imported_assets/Starter Assets/Runtime/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab` | Scene camera prefab reference | GUID moved; no remaining project reference to old import path |
| `Assets/Imported/Starter Assets/Runtime/ThirdPersonController/Scripts/ThirdPersonController.cs` | `Assets/used_imported_assets/Starter Assets/Runtime/ThirdPersonController/Scripts/ThirdPersonController.cs` | Player controller script | GUID moved; no remaining project reference to old import path |

## Dependency Closure

The migration also retained 117 supporting files discovered by serialized GUID references and Unity compile validation. These include required materials, meshes, textures, animation clips, nested prefabs, one shader graph, and the remaining Starter Assets runtime assembly files needed to keep `StarterAssetsInputs` visible to sibling scripts.

Validation checks completed:

| Check | Result |
|---|---|
| Project-owned references to remaining `Assets/Imported` assets | 0 |
| Duplicate Unity GUID groups under `Assets` | 0 |
| Unity console after migration | 0 errors, 0 warnings |
| Unity editor state after migration | Idle, not compiling, not updating |

Rollback:

- Revert this branch or restore the moved paths from Git.
- Because GUIDs were preserved, rollback is a file move/revert rather than scene rebinding.
