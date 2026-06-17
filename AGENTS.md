# Repository Guidelines

## Project Structure & Module Organization

`CodexSwitch/` contains the Avalonia desktop app targeting .NET 10 and using MVVM. Keep UI under `Views/` and `Controls/`, state and commands under `ViewModels/`, and business logic in `Services/`, `Proxy/`, `Models/`, and `I18n/`. Static assets live in `CodexSwitch/Assets/`.

`CodexSwitch.Tests/` holds xUnit coverage for pricing, routing, config writers, usage parsing, i18n, and update flows. Match new tests to the production subject, for example `PriceCalculatorTests.cs`.

`CodexSwitch.AdminWeb/` is the React + Vite admin UI. Source is in `src/`, public assets in `public/`, and production output in `dist/`. `build/` contains release helpers; `docs/` and `docs-site/` hold documentation.

## Build, Test, and Development Commands

- `dotnet restore CodexSwitch.Tests/CodexSwitch.Tests.csproj` — restore .NET dependencies.
- `dotnet run --project CodexSwitch/CodexSwitch.csproj` — start the desktop app locally.
- `dotnet build CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --no-restore` — build the app and test project.
- `dotnet test CodexSwitch.Tests/CodexSwitch.Tests.csproj -c Release --no-build --no-restore` — run the full xUnit suite.
- In `CodexSwitch.AdminWeb/`: `npm run dev`, `npm run build`, and `npm run lint` — develop, bundle, and lint the admin UI. Release builds of the desktop app invoke the admin web build when `package.json` is present.

## Coding Style & Naming Conventions

Use 4 spaces in C# and XAML. Follow existing formatting in TS/React files. Public types, properties, and methods use PascalCase; private fields use `_camelCase`. Keep nullable annotations enabled and prefer Avalonia compiled bindings. Comments should explain intent, invariants, or non-obvious constraints—not restate code. Use `eslint` for AdminWeb changes and keep imports clean.

## Testing Guidelines

Write xUnit tests in `CodexSwitch.Tests/` for any behavior change. Name test files after the subject under test and test methods in `Action_ExpectedResult` style, for example `Calculate_AppliesProgressiveTiersAndFastOverride`. Cover routing, protocol conversion, pricing, config writing, usage accounting, and UI-facing service behavior.

## Commit & Pull Request Guidelines

Recent history mixes conventional commits (`feat:`, `fix:`, `chore:`) with a few free-form messages. Prefer conventional prefixes for all new commits. Keep PRs focused, describe the user-visible impact, link related issues, and include screenshots for UI changes. Before opening a PR, run the relevant .NET tests and `npm run lint` for AdminWeb work.

## Security & Configuration Tips

Treat config files and exported settings as sensitive; they may contain API keys or OAuth tokens. The local proxy defaults to `127.0.0.1:12785`. Managed config writes create `.bak` backups—preserve them unless the change explicitly replaces the managed file flow.
