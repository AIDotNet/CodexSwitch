# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.0.1] - 2026-05-13

### Added

- Initial public release of CodexSwitch.
- Multi-provider proxy support for OpenAI, Anthropic, Gemini, DeepSeek, Codex, and Xiaomi-compatible endpoints.
- Provider templates, routing resolution, request parsing, and response conversion for the supported provider styles.
- Usage ingestion, query, and trend visualization for local logs, plus dashboard formatting and pricing helpers.
- Startup registration, tray menu controls, and local proxy lifecycle management for desktop use.
- Localized Avalonia UI updates across the settings, providers, add-provider, and usage pages.
- Expanded test coverage for config writing, payload building, startup registration, provider usage queries, and UI infrastructure.
- Bundled provider icons and a sample auth fixture for local development and testing.

### Changed

- Reworked the proxy adapters and model-conversion pipeline to support the expanded routing and usage flow.

### Fixed

- Fixed migration, validation, and UI infrastructure issues uncovered while assembling the release.
