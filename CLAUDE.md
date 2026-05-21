# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

- Restore dependencies: `dotnet restore CodexSwitch.Tests/CodexSwitch.Tests.csproj`
- Build the app and tests: `dotnet build CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --no-restore`
- Run all tests after a build: `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --no-build --no-restore`
- Run tests without a prior build: `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release`
- Run a single test class: `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --filter FullyQualifiedName~PriceCalculatorTests`
- Run a single test method: `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --filter FullyQualifiedName~PriceCalculatorTests.CalculatesCachedTokens`
- Run from source: `dotnet run --project CodexSwitch/CodexSwitch.csproj`
- Validate changelog/version alignment: `pwsh ./build/Validate-Changelog.ps1`
- Publish a Native AOT build: `dotnet publish CodexSwitch/CodexSwitch.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=true`

CI uses .NET SDK `10.0.203`, validates the changelog, restores `CodexSwitch.Tests/CodexSwitch.Tests.csproj`, builds it in Release, then runs xUnit tests with `--no-build --no-restore`. Package versions are centrally managed in `Directory.Packages.props`; repository version metadata is in `Directory.Build.props`.

## Architecture

CodexSwitch is a .NET 10 Avalonia desktop app plus an in-process ASP.NET Core/Kestrel local proxy. The UI manages providers, models, usage, settings, and local client integration; the proxy exposes OpenAI Responses-style local endpoints and routes each request to the selected upstream provider protocol.

`Program.cs` starts the Avalonia desktop lifetime, with a special bootstrap mode for Claude Code config. `App.axaml.cs` wires the main window, tray menu, start-hidden behavior, macOS dock visibility, and shutdown disposal. `MainWindowViewModel` is the central application coordinator: it owns app configuration, provider state, proxy lifecycle, usage dashboard state, pricing, updates, OAuth login, startup registration, and commands used by the XAML pages.

Configuration is rooted in `Models/AppConfig.cs` and persisted by `Services/ConfigurationStore.cs` under the user's application data directory from `Services/AppPaths.cs`. Defaults and migrations seed built-in providers from `ProviderTemplateCatalog`, normalize UI/network settings, ensure Codex and Claude Code active provider ids, and populate provider/model defaults. Pricing and usage live beside config as local JSON/JSONL files; usage logs are written/read by `UsageLogWriter` and `UsageLogReader`, and cost estimates are calculated by `PriceCalculator` from the local model pricing catalog.

`Proxy/ProxyHostService.cs` owns the local server. It binds to `ProxySettings.Host`/`Port`, maps `/health`, `/v1/models`, `/v1/responses`, and `/v1/messages`, applies managed Codex/Claude Code client config when running, and restores backups when stopped or disabled. Requests are resolved through `ProviderRoutingResolver`, which chooses the active provider for Codex or Claude Code, applies model route/conversion rules, and can fall through to another provider that supports the requested model.

Protocol support is adapter-based through `IProviderProtocolAdapter`. `OpenAiResponsesAdapter` passes Responses requests upstream with model/service-tier/request overrides, `OpenAiChatAdapter` converts Responses to chat completions and back, and `AnthropicMessagesAdapter` converts Responses/Messages traffic for Anthropic-compatible upstreams. Shared request shaping and response mapping are split across payload builders and common helpers in `Proxy/`, including `ResponsesPayloadBuilder`, `AnthropicMessagesToResponsesPayloadBuilder`, `ResponsesUsageParser`, and `ProtocolAdapterCommon`. When changing routing, streaming, protocol conversion, usage normalization, or pricing, add focused tests in `CodexSwitch.Tests`.

Managed client configuration is deliberately reversible. `CodexConfigWriter` merges managed entries into `~/.codex/config.toml` and writes or preserves `auth.json` depending on proxy auth settings; `ClaudeCodeConfigWriter` updates `~/.claude/settings.json` for Anthropic base URL/token/model settings. Both use `ManagedFileBackup` to preserve and restore originals.

UI is Avalonia XAML under `Views/`, reusable controls in `Controls/`, and styles in `Styles/`. Localization uses JSON resources in `Assets/i18n/*.json`, loaded through `I18nService` and the `TrExtension` markup extension. Keep UI text changes synchronized across supported locale files.

The `CodexSwitchUI` directory is a referenced UI component submodule/project; the app project references `CodexSwitchUI` and `CodexSwitchUI.ECharts`. CI checks out submodules recursively, so local development may also need submodules initialized.

## Release notes

Release version state is centralized in `Directory.Build.props`; the first `CHANGELOG.md` release heading must match `vX.Y.Z`. Tagged `vX.Y.Z` CI runs publish self-contained Native AOT artifacts for Windows, Linux, and macOS and create the GitHub Release automatically. Packaging details and optional macOS signing/notarization inputs are documented in `docs/release.md`.
