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

## macOS signing and notarization

Tagged releases only publish macOS DMG artifacts when CI has a Developer ID Application certificate and Apple notarization credentials. Without this, Gatekeeper can report the downloaded app as damaged and refuse to open it, so tagged CI runs skip macOS DMG packaging instead of uploading unsigned public artifacts. Windows and Linux release artifacts still publish normally.

Configure these GitHub repository secrets before pushing a release tag:

- `MACOS_CERTIFICATE_BASE64`: base64-encoded `.p12` export for the Developer ID Application certificate.
- `MACOS_CERTIFICATE_PASSWORD`: password for the `.p12` export.
- `MACOS_KEYCHAIN_PASSWORD`: password used for the temporary CI keychain. If omitted, CI generates one for the job.
- `MACOS_SIGNING_IDENTITY`: optional explicit identity name, for example `Developer ID Application: Example LLC (TEAMID)`. If omitted, CI uses the first imported Developer ID Application identity.
- `MACOS_NOTARY_APPLE_ID`: Apple ID used for notarization.
- `MACOS_NOTARY_TEAM_ID`: Apple Developer Team ID.
- `MACOS_NOTARY_PASSWORD`: app-specific password for the Apple ID.

The CI workflow also accepts the Electron Builder/OpenCowork-style aliases `CSC_LINK`, `CSC_KEY_PASSWORD`, `APPLE_ID`, `APPLE_TEAM_ID`, and `APPLE_APP_SPECIFIC_PASSWORD`. `CSC_LINK` can be a certificate URL, local `.p12` path, or base64-encoded `.p12` data.

Pull requests and branch builds can still produce ad-hoc signed macOS artifacts for CI validation, but those artifacts are not suitable for public download.

## Artifact naming

- `CodexSwitch-vX.Y.Z-win-x64-setup.exe`
- `CodexSwitch-vX.Y.Z-linux-x64.AppImage`
- `CodexSwitch-vX.Y.Z-osx-arm64.dmg`
