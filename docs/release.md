# Release Guide

This repository uses a single in-repo version source plus a manual GitHub Release flow.

## Version source

- Update `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` in `Directory.Build.props`.
- Keep Git tags and GitHub Release tags in the `vX.Y.Z` format.
- Keep the first released section in `CHANGELOG.md` aligned with the current version, for example `## [v1.2.3] - 2026-05-12`.

## Manual release steps

1. Update the version in `Directory.Build.props`.
2. Add the matching `vX.Y.Z` section to `CHANGELOG.md`.
3. Push the branch and wait for the `ci` workflow to finish successfully.
4. Download the workflow artifacts named `CodexSwitch-vX.Y.Z-<rid>`.
5. Create a Git tag named `vX.Y.Z`.
6. Create a GitHub Release in `https://github.com/AIDotNet/CodexSwitch`.
7. Paste the matching changelog section into the Release notes.
8. Upload all generated zip artifacts to the Release.

## Artifact naming

- `CodexSwitch-vX.Y.Z-win-x64.zip`
- `CodexSwitch-vX.Y.Z-linux-x64.zip`
- `CodexSwitch-vX.Y.Z-osx-x64.zip`
- `CodexSwitch-vX.Y.Z-osx-arm64.zip`
