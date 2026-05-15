param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$RuntimeIdentifier,

    [string]$MacSigningIdentity = $env:MACOS_SIGNING_IDENTITY,

    [string]$MacNotaryAppleId = $env:MACOS_NOTARY_APPLE_ID,

    [string]$MacNotaryTeamId = $env:MACOS_NOTARY_TEAM_ID,

    [string]$MacNotaryPassword = $env:MACOS_NOTARY_PASSWORD
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $encoding = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $Value, $encoding)
}

function Write-GitHubOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT)) {
        "$Name=$Value" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    }
}

function Invoke-NativeTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments 2>&1 | ForEach-Object {
        Write-Host $_
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Command '$FilePath' failed with exit code $LASTEXITCODE."
    }
}

function Get-RepositoryRoot {
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}

function Test-HasText {
    param(
        [AllowNull()]
        [string]$Value
    )

    return -not [string]::IsNullOrWhiteSpace($Value)
}

function New-WindowsInstaller {
    param(
        [string]$PublishDirectory,
        [string]$OutputDirectory,
        [string]$Version,
        [string]$RuntimeIdentifier
    )

    $command = Get-Command "iscc" -CommandType Application -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        $innoCompiler = $command.Source
    }
    else {
        $programFilesX86 = ${env:ProgramFiles(x86)}
        if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
            $programFilesX86 = $env:ProgramFiles
        }

        if ([string]::IsNullOrWhiteSpace($programFilesX86)) {
            throw "Inno Setup compiler was not found because no Program Files directory is available on this runner."
        }

        $innoCompiler = Join-Path $programFilesX86 "Inno Setup 6\ISCC.exe"
        if (-not (Test-Path -LiteralPath $innoCompiler)) {
            throw "Inno Setup compiler was not found. Install Inno Setup before packaging Windows artifacts."
        }
    }

    $repositoryRoot = Get-RepositoryRoot
    $innoScript = Join-Path $PSScriptRoot "installers\windows\CodexSwitch.iss"
    $iconPath = Join-Path $repositoryRoot "CodexSwitch\Assets\favicon.ico"
    $outputBaseName = "CodexSwitch-v$Version-$RuntimeIdentifier-setup"
    $artifactPath = Join-Path $OutputDirectory "$outputBaseName.exe"

    Invoke-NativeTool -FilePath $innoCompiler -Arguments @(
        $innoScript,
        "/DAppVersion=$Version",
        "/DSourceDir=$PublishDirectory",
        "/DOutputDir=$OutputDirectory",
        "/DOutputBaseFilename=$outputBaseName",
        "/DIconPath=$iconPath"
    )

    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected Windows installer was not created: $artifactPath"
    }

    return $artifactPath
}

function Sign-MacAppBundle {
    param(
        [string]$BundlePath,
        [string]$SigningIdentity
    )

    $developerIdSigning = Test-HasText $SigningIdentity
    if (-not $developerIdSigning) {
        Write-Warning "No macOS Developer ID signing identity was provided; using an ad-hoc signature for CI packaging only."
        $SigningIdentity = "-"
    }

    $arguments = @("--force", "--deep", "--sign", $SigningIdentity)
    if ($developerIdSigning) {
        $arguments += @("--options", "runtime", "--timestamp")
    }

    $arguments += $BundlePath
    Invoke-NativeTool -FilePath "codesign" -Arguments $arguments
    Invoke-NativeTool -FilePath "codesign" -Arguments @("--verify", "--deep", "--strict", "--verbose=2", $BundlePath)
}

function Submit-MacNotarization {
    param(
        [string]$ArtifactPath,
        [string]$StaplePath,
        [string]$NotaryAppleId,
        [string]$NotaryTeamId,
        [string]$NotaryPassword
    )

    Invoke-NativeTool -FilePath "xcrun" -Arguments @(
        "notarytool",
        "submit",
        $ArtifactPath,
        "--apple-id",
        $NotaryAppleId,
        "--team-id",
        $NotaryTeamId,
        "--password",
        $NotaryPassword,
        "--wait"
    )

    Invoke-NativeTool -FilePath "xcrun" -Arguments @("stapler", "staple", $StaplePath)
    Invoke-NativeTool -FilePath "xcrun" -Arguments @("stapler", "validate", $StaplePath)
}

function New-MacDmg {
    param(
        [string]$PublishDirectory,
        [string]$OutputDirectory,
        [string]$Version,
        [string]$RuntimeIdentifier,
        [string]$SigningIdentity,
        [string]$NotaryAppleId,
        [string]$NotaryTeamId,
        [string]$NotaryPassword
    )

    $bundlePath = Join-Path $OutputDirectory "CodexSwitch.app"
    $contentsPath = Join-Path $bundlePath "Contents"
    $macOsPath = Join-Path $contentsPath "MacOS"
    $resourcesPath = Join-Path $contentsPath "Resources"
    $artifactPath = Join-Path $OutputDirectory "CodexSwitch-v$Version-$RuntimeIdentifier.dmg"
    $appZipPath = Join-Path $OutputDirectory "CodexSwitch-v$Version-$RuntimeIdentifier.app.zip"

    Remove-Item -LiteralPath $bundlePath -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $artifactPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $appZipPath -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $macOsPath, $resourcesPath | Out-Null

    Copy-Item -Path (Join-Path $PublishDirectory "*") -Destination $macOsPath -Recurse -Force
    Invoke-NativeTool -FilePath "chmod" -Arguments @("+x", (Join-Path $macOsPath "CodexSwitch"))

    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>CodexSwitch</string>
    <key>CFBundleIdentifier</key>
    <string>net.aidotnet.codexswitch</string>
    <key>CFBundleName</key>
    <string>CodexSwitch</string>
    <key>CFBundleDisplayName</key>
    <string>CodexSwitch</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$Version</string>
    <key>CFBundleVersion</key>
    <string>$Version</string>
    <key>LSMinimumSystemVersion</key>
    <string>12.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@

    Write-Utf8NoBomFile -Path (Join-Path $contentsPath "Info.plist") -Value $plist
    Sign-MacAppBundle -BundlePath $bundlePath -SigningIdentity $SigningIdentity

    $canNotarize = (Test-HasText $SigningIdentity) -and
        (Test-HasText $NotaryAppleId) -and
        (Test-HasText $NotaryTeamId) -and
        (Test-HasText $NotaryPassword)

    if ($canNotarize) {
        Invoke-NativeTool -FilePath "ditto" -Arguments @("-c", "-k", "--keepParent", $bundlePath, $appZipPath)
        Submit-MacNotarization `
            -ArtifactPath $appZipPath `
            -StaplePath $bundlePath `
            -NotaryAppleId $NotaryAppleId `
            -NotaryTeamId $NotaryTeamId `
            -NotaryPassword $NotaryPassword
        Remove-Item -LiteralPath $appZipPath -Force
    }
    elseif (Test-HasText $SigningIdentity) {
        Write-Warning "macOS app bundle was signed but not notarized; downloaded release artifacts may still be blocked by Gatekeeper."
    }

    Invoke-NativeTool -FilePath "hdiutil" -Arguments @(
        "create",
        "-volname",
        "CodexSwitch",
        "-srcfolder",
        $bundlePath,
        "-ov",
        "-format",
        "UDZO",
        $artifactPath
    )

    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected macOS DMG was not created: $artifactPath"
    }

    if (Test-HasText $SigningIdentity) {
        Invoke-NativeTool -FilePath "codesign" -Arguments @("--force", "--sign", $SigningIdentity, "--timestamp", $artifactPath)
        Invoke-NativeTool -FilePath "codesign" -Arguments @("--verify", "--verbose=2", $artifactPath)
    }

    if ($canNotarize) {
        Submit-MacNotarization `
            -ArtifactPath $artifactPath `
            -StaplePath $artifactPath `
            -NotaryAppleId $NotaryAppleId `
            -NotaryTeamId $NotaryTeamId `
            -NotaryPassword $NotaryPassword
        Invoke-NativeTool -FilePath "spctl" -Arguments @("--assess", "--type", "open", "--context", "context:primary-signature", "--verbose=4", $artifactPath)
    }

    return $artifactPath
}

function New-LinuxAppImage {
    param(
        [string]$PublishDirectory,
        [string]$OutputDirectory,
        [string]$Version,
        [string]$RuntimeIdentifier
    )

    $repositoryRoot = Get-RepositoryRoot
    $appDir = Join-Path $OutputDirectory "CodexSwitch.AppDir"
    $usrPath = Join-Path $appDir "usr"
    $usrBinPath = Join-Path $usrPath "bin"
    $artifactPath = Join-Path $OutputDirectory "CodexSwitch-v$Version-$RuntimeIdentifier.AppImage"
    $appImageTool = Join-Path $OutputDirectory "appimagetool-x86_64.AppImage"

    Remove-Item -LiteralPath $appDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $artifactPath -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $usrBinPath | Out-Null

    Copy-Item -Path (Join-Path $PublishDirectory "*") -Destination $usrBinPath -Recurse -Force
    Invoke-NativeTool -FilePath "chmod" -Arguments @("+x", (Join-Path $usrBinPath "CodexSwitch"))

    $appRun = @'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/CodexSwitch" "$@"
'@
    Write-Utf8NoBomFile -Path (Join-Path $appDir "AppRun") -Value $appRun
    Invoke-NativeTool -FilePath "chmod" -Arguments @("+x", (Join-Path $appDir "AppRun"))

    $desktopFile = @"
[Desktop Entry]
Type=Application
Name=CodexSwitch
Exec=CodexSwitch
Icon=CodexSwitch
Categories=Utility;Development;
Terminal=false
Comment=Local AI provider switcher for Codex
"@
    Write-Utf8NoBomFile -Path (Join-Path $appDir "CodexSwitch.desktop") -Value $desktopFile

    $iconSource = Join-Path (Join-Path (Join-Path (Join-Path $repositoryRoot "CodexSwitch") "Assets") "icons") "logo.png"
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $appDir "CodexSwitch.png") -Force
    Copy-Item -LiteralPath $iconSource -Destination (Join-Path $appDir ".DirIcon") -Force

    if (-not (Test-Path -LiteralPath $appImageTool)) {
        Invoke-WebRequest `
            -Uri "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage" `
            -OutFile $appImageTool
        Invoke-NativeTool -FilePath "chmod" -Arguments @("+x", $appImageTool)
    }

    $previousExtractAndRun = $env:APPIMAGE_EXTRACT_AND_RUN
    $previousArch = $env:ARCH
    try {
        $env:APPIMAGE_EXTRACT_AND_RUN = "1"
        $env:ARCH = "x86_64"
        Invoke-NativeTool -FilePath $appImageTool -Arguments @($appDir, $artifactPath)
    }
    finally {
        $env:APPIMAGE_EXTRACT_AND_RUN = $previousExtractAndRun
        $env:ARCH = $previousArch
    }

    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Expected Linux AppImage was not created: $artifactPath"
    }

    return $artifactPath
}

$PublishDirectory = (Resolve-Path -LiteralPath $PublishDirectory).Path
$OutputDirectory = (New-Item -ItemType Directory -Force -Path $OutputDirectory).FullName

if (-not (Test-HasText $MacNotaryAppleId)) {
    $MacNotaryAppleId = $env:APPLE_ID
}

if (-not (Test-HasText $MacNotaryTeamId)) {
    $MacNotaryTeamId = $env:APPLE_TEAM_ID
}

if (-not (Test-HasText $MacNotaryPassword)) {
    $MacNotaryPassword = $env:APPLE_APP_SPECIFIC_PASSWORD
}

Get-ChildItem -LiteralPath $PublishDirectory -Filter "*.pdb" -Recurse -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

$artifactPath = switch ($RuntimeIdentifier) {
    "win-x64" { New-WindowsInstaller -PublishDirectory $PublishDirectory -OutputDirectory $OutputDirectory -Version $Version -RuntimeIdentifier $RuntimeIdentifier }
    "osx-arm64" {
        New-MacDmg `
            -PublishDirectory $PublishDirectory `
            -OutputDirectory $OutputDirectory `
            -Version $Version `
            -RuntimeIdentifier $RuntimeIdentifier `
            -SigningIdentity $MacSigningIdentity `
            -NotaryAppleId $MacNotaryAppleId `
            -NotaryTeamId $MacNotaryTeamId `
            -NotaryPassword $MacNotaryPassword
    }
    "osx-x64" {
        New-MacDmg `
            -PublishDirectory $PublishDirectory `
            -OutputDirectory $OutputDirectory `
            -Version $Version `
            -RuntimeIdentifier $RuntimeIdentifier `
            -SigningIdentity $MacSigningIdentity `
            -NotaryAppleId $MacNotaryAppleId `
            -NotaryTeamId $MacNotaryTeamId `
            -NotaryPassword $MacNotaryPassword
    }
    "linux-x64" { New-LinuxAppImage -PublishDirectory $PublishDirectory -OutputDirectory $OutputDirectory -Version $Version -RuntimeIdentifier $RuntimeIdentifier }
    default { throw "Unsupported runtime identifier for release packaging: $RuntimeIdentifier" }
}

$artifactName = [System.IO.Path]::GetFileNameWithoutExtension($artifactPath)
Write-GitHubOutput -Name "name" -Value $artifactName
Write-GitHubOutput -Name "path" -Value $artifactPath

Write-Host "Created release artifact: $artifactPath"
