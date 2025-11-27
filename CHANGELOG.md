# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial UPM package structure
- Processor pipeline architecture
  - `IExportProcessor` interface
  - `ExportProcessorBase` base class
  - `ProcessorPipeline` for managing processor execution
  - `ExportContext` for non-destructive processing
- Notification system
  - `NotificationLevel` (Info, Warning, Error)
  - `ProcessorNotification` for individual notifications
  - `ProcessorResult` for processor execution results
- Report system
  - `ExportReport` with JSON serialization
  - `ReportManager` for saving and logging reports

## [0.1.0] - 2024-11-27

### Added (Phase 0: Baseline)
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
