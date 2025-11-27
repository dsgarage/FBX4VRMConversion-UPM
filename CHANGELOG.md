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

## [0.1.0] - TBD

### Planned
- Phase 0: Baseline
  - RepoA (UniVRM-fork) integration
  - Minimal GUI for Prefab instance export
