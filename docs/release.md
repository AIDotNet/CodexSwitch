# Release Guide

This repository uses a single in-repo version source plus an automated GitHub Release flow driven by tagged CI runs.

## Version source

- Update `Version`, `AssemblyVersion`, `FileVersion`, and `InformationalVersion` in `Directory.Build.props`.
- Keep Git tags and GitHub Release tags in the `vX.Y.Z` format.
- Keep the first released section in `CHANGELOG.md` aligned with the current version, for example `## [v1.2.3] - 2026-05-12`.

## Release steps

1. Update the version in `Directory.Build.props`.
2. Add the matching `vX.Y.Z` section to `CHANGELOG.md`.
3. Push the branch and wait for the `ci` workflow to finish successfully.
4. Create and push a Git tag named `vX.Y.Z`. The release job checks that the tag matches `Directory.Build.props` before publishing.
5. Wait for the tagged `ci` run to finish. It will publish the platform installers and create the GitHub Release automatically.

## Artifact naming

- `CodexSwitch-vX.Y.Z-win-x64-setup.exe`
- `CodexSwitch-vX.Y.Z-linux-x64.AppImage`
- `CodexSwitch-vX.Y.Z-osx-arm64.dmg`
