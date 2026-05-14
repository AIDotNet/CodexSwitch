# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [v0.0.6] - 2026-05-14

### Added

- Added Anthropic Messages compatibility for OpenAI Chat and OpenAI Responses providers, including non-streaming and streaming conversion for messages, tool use, reasoning output, and usage accounting.
- Added a separate hover details window for the mini status panel while keeping the compact usage strip pinned in place.
- Added richer button variants, pressed/hover states, focus-visible styling, icon sizing, and danger styles.

### Changed

- Switched the built-in DeepSeek template and migration path back to the OpenAI-compatible Chat API while keeping Claude Code support enabled.
- Updated Claude Code proxy settings to write the proxy root URL instead of appending `/v1`.
- Made Windows release packaging prefer `iscc` from `PATH` and handle runners without `ProgramFiles(x86)`.

### Fixed

- Automatically enable Claude Code support when a provider is switched to the Anthropic Messages protocol.
- Signed, notarized, and Gatekeeper-validated macOS release DMGs when Apple release credentials are configured.

## [v0.0.5] - 2026-05-14

### Added

- Added outbound network proxy settings with system proxy, custom proxy, and disabled proxy modes.
- Added a shared HTTP client factory that defaults to the system proxy, prefers HTTP/2, keeps pooled connections alive, and enables HTTP/2 keep-alive pings.
- Added Claude Code provider settings that write and restore `~/.claude/settings.json`, expose provider/model controls, and route `/v1/messages` through CodexSwitch.
- Added Claude Code `sonnet`, `opus`, and `haiku` aliases for Anthropic providers, including optional 1M context handling for Sonnet models.
- Added manual Claude Code model entry with quick model buttons and preserved Anthropic extension headers when proxying `/v1/messages`.

### Changed

- Optimized the local Codex to CodexSwitch proxy listener for low-latency keep-alive connections with TCP no-delay, longer client keep-alive, disabled response buffering, and higher connection limits.
- Split active provider selection for Codex and Claude Code so the local proxy can serve either client and migrate legacy provider configs safely.
- Recreate shared networking services when proxy settings change so provider adapters, OAuth, usage queries, icon loading, and update checks use the latest network configuration.
- Moved proxy request usage logging to buffered background writes and coalesced concurrent OAuth token refreshes to reduce request-path latency.
- Bounded cached Responses conversation state with capacity and TTL pruning to prevent unbounded memory growth.

## [v0.0.4] - 2026-05-13

### Added

- Added configurable automatic update checks with system-specific installer selection, automatic download, and progress display.

### Changed

- Excluded `.pdb` files from CI release installers and disabled debug symbol generation for release publishing.

## [v0.0.3] - 2026-05-13

### Changed

- Replaced zipped CI release artifacts with platform installers: Windows setup executable, macOS DMG, and Linux AppImage.
- Made CI publish commands explicitly request Native AOT and self-contained output for release packaging.

## [v0.0.2] - 2026-05-13

### Changed

- Removed the macOS x64/AMD CI publish target and release artifact from the default release matrix.
- Automated GitHub Release publishing from tagged CI runs with write-scoped release permissions.

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
