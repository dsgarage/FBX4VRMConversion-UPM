# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - In Development

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
