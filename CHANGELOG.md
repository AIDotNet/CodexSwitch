# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v1.0.0] - 2026-05-12

### Added

- Added repository-level SDK pinning with `global.json` for `.NET 10.0.203`.
- Added centralized package and assembly version management for the Avalonia app and tests.
- Added GitHub Actions CI validation plus multi-platform publish artifacts for `win-x64`, `linux-x64`, `osx-x64`, and `osx-arm64`.
- Added GitHub Release update checking in the Avalonia About section for the `AIDotNet/CodexSwitch` repository.
- Added release process documentation and changelog validation automation for future manual GitHub Releases.

### Fixed

- Fixed the current `CsSegmentedButton` compile error that blocked local builds and CI.
