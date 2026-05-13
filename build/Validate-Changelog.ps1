param(
    [string]$PropsPath = "Directory.Build.props",
    [string]$ChangelogPath = "CHANGELOG.md"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PropsPath)) {
    throw "Version props file was not found: $PropsPath"
}

if (-not (Test-Path -LiteralPath $ChangelogPath)) {
    throw "Changelog file was not found: $ChangelogPath"
}

[xml]$props = Get-Content -LiteralPath $PropsPath
$version = $props.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Directory.Build.props does not define a Version value."
}

$expectedHeading = "## [v$version]"
$heading = Select-String -LiteralPath $ChangelogPath -Pattern '^## \[(?<tag>v\d+\.\d+\.\d+)\]' | Select-Object -First 1

if ($null -eq $heading) {
    throw "CHANGELOG.md does not contain any version heading in the format ## [vX.Y.Z]."
}

if (-not $heading.Line.Trim().StartsWith($expectedHeading, [System.StringComparison]::Ordinal)) {
    throw "Expected first changelog heading '$expectedHeading', but found '$($heading.Line.Trim())'."
}

Write-Host "Validated changelog heading $expectedHeading."
