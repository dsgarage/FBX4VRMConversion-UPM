# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.3] - 2025-12-03

### Fixed
- Remove Git URL dependencies from package.json (UPM doesn't support Git URLs in dependencies)
- UniVRM-fork must now be installed separately before this package

---

## [0.0.2] - 2025-12-03

### Added
- Avatar Management APIs in BugReportService
  - `CreateAvatar()`: Create new avatar with optional GitHub parent issue
  - `CreateVersion()`: Create new version for an avatar with optional GitHub child issue
  - Response models: `CreateAvatarResponse`, `CreateVersionResponse`
  - Request models: `CreateAvatarRequest`, `CreateVersionRequest`, `AvatarVersionInfo`
- Queue-based bug report submission for improved reliability

### Changed
- Removed unused Sample Presets from package configuration

### Fixed
- Removed empty Tests folder references

---

## [0.0.1] - 2025-12-01

### Added
- Initial UPM package release
- FBX/Prefab to VRM conversion with lilToon support
- Bug Report System with multi-angle screenshot capture
- Bone Check Window for skeleton verification
- Export Preview/Report Windows
- Preset Management System
- Quick Export workflow
