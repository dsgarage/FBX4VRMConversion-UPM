# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2025-12-01

### Added
- Bug Report System
  - Multi-angle screenshot capture (10 directions: Front, Back, Left, Right, Top, Bottom + 4 diagonals)
  - Automatic model information collection (skeleton, meshes, materials, dynamics)
  - Server submission with screenshot embedding in GitHub Issues
  - Bug Report button in Export Report Window
- Platform & Avatar Management
  - Platform identification (FBX4VRM, VRMLoader, ARApp)
  - Avatar-based issue grouping
  - Avatar list API integration
  - Auto-match avatars by model name
  - Avatar selection UI in Bug Report Window
- Bone Check Window (`Tools > FBX4VRM > Bone Check Window`)
  - Apply UnityChan AnimatorController to verify bone functionality
  - Support for Locomotions, ActionCheck, and ARPose animations
  - Required/Recommended bone status display
  - Custom AnimatorController support
  - Bone Check button in Export Report Window
  - VRM import and instant bone check workflow
  - **Play Mode Auto Check**: Automatically load VRM, apply A-Pose or AnimatorController, and capture screenshot
  - Runtime VRM loader (`BoneCheckRunner`) for Play mode verification
  - **AnimatorController Screenshot**: Capture screenshot with custom AnimatorController animation
  - AnimatorController selection UI with wait time configuration
- New Sample: Bone Check
  - Includes UnityChanLocomotions AnimatorController and animation files
  - Ready to use after importing from Package Manager

### Changed
- Export Report Window layout improved with Bug Report and Bone Check buttons

### Fixed
- Multi-angle camera positioning for full-body capture

---

## [0.1.1] - 2025-11-28

### Changed
- Default VRM version changed to 0.x (VRM 1.0 has Avatar construction bug)
- Default preset changed to VRChat
- VRM 1.0 option now shows warning about Avatar construction bug

### Fixed
- VRM export now overwrites existing files without confirmation dialog
- Added overwrite logging for transparency

---

## [0.1.0] - 2025-11-28

### Phase 4: UX Polish
- Preset Management System
  - `ExportPreset` ScriptableObject for saving export settings
  - Save/load presets per project or platform (VRChat, Cluster, etc.)
  - VRM metadata presets (author, license, permissions)
  - Per-processor enable/disable settings
  - Custom expression mappings
  - Tags for organizing presets
- `PresetManager` utility class
  - Find all presets (built-in and user-created)
  - Filter presets by tags
  - Save/delete presets with confirmation
  - Built-in preset creation menu
- Quick Export Window (`Tools > FBX4VRM > Quick Export` or `Ctrl+Shift+E`)
  - One-button VRM export workflow
  - Auto-select from Hierarchy selection
  - Preset dropdown for quick switching
  - Streamlined UI with export info preview
  - Success dialog with Show in Finder / View Report options
- Hierarchy Context Menu
  - Right-click on GameObject → `FBX4VRM > Quick Export VRM`
  - Right-click on GameObject → `FBX4VRM > Export with Settings...`

### Phase 3: Expression/Dynamics
- `ExpressionsSetupProcessor`
  - Detects BlendShapes from SkinnedMeshRenderer
  - Maps BlendShape names to VRM Expression names automatically
  - Supports multiple naming conventions (English, Japanese, VRChat)
  - Maps 17 standard VRM expressions (emotions, lip-sync, blink, look)
  - Reports missing recommended expressions as warnings
  - Handles duplicate mapping conflicts
- `SpringBoneConvertProcessor`
  - Detects existing VRM SpringBone (0.x and 1.0)
  - Converts VRChat PhysBone to VRM SpringBone parameters
  - Converts DynamicBone to VRM SpringBone parameters
  - Uses reflection for SDK detection (works without VRChat SDK installed)
  - Maps physics parameters: stiffness, gravity, drag, radius
  - Warns about approximate conversion (different physics models)
- Processor pipeline order updated:
  - ... → GltfValueClamp (30) → ExpressionsSetup (40) → SpringBoneConvert (50)

### Phase 2: Safety
- `HumanoidValidationProcessor`
  - Validates all 15 VRM-required Humanoid bones (Hips, Spine, Head, Arms, Legs)
  - Checks 9 recommended bones (Neck, Chest, Shoulders, Toes, Eyes, Jaw)
  - T-Pose detection with angle-based arm position check
  - Bone hierarchy validation (parent-child relationships)
  - Detailed warnings for missing recommended bones and hierarchy issues
- `GltfValueClampProcessor`
  - Clamps HDR and out-of-range color values to glTF spec (0-1)
  - Clamps float properties (Metallic, Smoothness, Cutoff, etc.)
  - Generates Warning for every clamped value (no silent modifications)
  - Per-material grouped warnings with before/after values
  - Supports 18 color properties and 8 float properties
- Processor pipeline order updated:
  - RootValidation (0) → HumanoidValidation (5) → LilToonDetect (10) → LilToonToMToon (20) → GltfValueClamp (30)

### Phase 1: lilToon Support
- `LilToonDetectProcessor`
  - Detects lilToon materials in the model
  - Identifies shader variants (Cutout, Transparent, Outline, etc.)
  - Stores detection results in SharedData for downstream processors
- `LilToonToMToonProcessor`
  - Converts lilToon materials to MToon format
  - Maps properties: base color, normal map, emission, rim light, outline
  - Handles HDR color clamping with warnings
  - Non-destructive (creates material copies)
- `LilToonToMToonConverter` utility class
  - Property mapping between lilToon and MToon10
  - Support for VRM 0.x MToon and VRM 1.0 MToon10
- Export Preview Window (`ExportPreviewWindow`)
  - Pre-export analysis showing detected issues
  - Color-coded notifications (Info/Warning/Error)
  - Ability to proceed or cancel based on analysis
- Export Report Window (`ExportReportWindow`)
  - Post-export summary with all notifications
  - Filter by notification level
  - Save report as JSON
  - Copy to clipboard
  - Show exported file in Finder
- Export Window now shows Preview button alongside Export button
- Export completion now opens Report Window instead of dialog

### Phase 0: Baseline
- UniVRM-fork integration (v0.130.1-f1)
  - Dependencies configured in package.json
  - Support for both VRM 0.x and VRM 1.0 export
- Minimal Export GUI (`Tools > FBX4VRM > Export Window`)
  - Root object selection from scene
  - VRM version selection (0.x / 1.0)
  - Output folder configuration
  - Non-destructive export (clones object before processing)
- `RootValidationProcessor`
  - Validates Humanoid Animator
  - Checks for mesh renderers
  - Warns about missing VRM metadata components
