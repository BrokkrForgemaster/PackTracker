<#
.SYNOPSIS
    Bumps the PackTracker application version atomically across all source-of-truth files.

.DESCRIPTION
    Updates Directory.Build.props (Version, FileVersion, AssemblyVersion) and the default
    AppVersion in installer\PackTrackerSetup.iss so they cannot drift apart.

    Optionally creates the matching annotated git tag (vX.Y.Z) so a single push triggers
    the release workflow with a guaranteed-matching version gate.

.PARAMETER Version
    The semantic version to set (e.g. 0.7.0). Must match X.Y.Z.

.PARAMETER Tag
    If specified, also creates an annotated git tag vX.Y.Z pointing at HEAD.
    The tag is NOT pushed automatically — review with `git show vX.Y.Z` then `git push origin vX.Y.Z`.

.EXAMPLE
    .\scripts\bump-version.ps1 0.7.0

.EXAMPLE
    .\scripts\bump-version.ps1 0.7.0 -Tag
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [switch]$Tag
)

$ErrorActionPreference = 'Stop'

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in X.Y.Z form (got '$Version')."
    exit 1
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$propsPath = Join-Path $repoRoot 'Directory.Build.props'
$issPath   = Join-Path $repoRoot 'installer\PackTrackerSetup.iss'

if (-not (Test-Path $propsPath)) { Write-Error "Not found: $propsPath"; exit 1 }
if (-not (Test-Path $issPath))   { Write-Error "Not found: $issPath";   exit 1 }

# --- Directory.Build.props ---
$propsXml = Get-Content $propsPath -Raw
$propsXml = [regex]::Replace($propsXml, '<Version>[^<]+</Version>',                "<Version>$Version</Version>")
$propsXml = [regex]::Replace($propsXml, '<FileVersion>[^<]+</FileVersion>',         "<FileVersion>$Version</FileVersion>")
$propsXml = [regex]::Replace($propsXml, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>")
Set-Content -Path $propsPath -Value $propsXml -Encoding utf8 -NoNewline
Write-Host "Updated Directory.Build.props -> $Version"

# --- installer/PackTrackerSetup.iss ---
$issText = Get-Content $issPath -Raw
$issText = [regex]::Replace($issText, '#define\s+AppVersion\s+"[^"]+"', "#define AppVersion `"$Version`"")
Set-Content -Path $issPath -Value $issText -Encoding utf8 -NoNewline
Write-Host "Updated installer\PackTrackerSetup.iss -> $Version"

if ($Tag) {
    $tagName = "v$Version"
    $existing = & git tag --list $tagName
    if ($existing) {
        Write-Error "Git tag '$tagName' already exists. Delete it first or pick a new version."
        exit 1
    }
    & git tag -a $tagName -m "PackTracker $tagName"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Host "Created annotated tag $tagName at HEAD."
    Write-Host "Review with: git show $tagName"
    Write-Host "Push with:   git push origin $tagName"
} else {
    Write-Host ""
    Write-Host "Files updated. Commit them, then create the tag with:"
    Write-Host "  git add Directory.Build.props installer\PackTrackerSetup.iss"
    Write-Host "  git commit -m `"chore: bump version to $Version`""
    Write-Host "  git tag -a v$Version -m `"PackTracker v$Version`""
    Write-Host "  git push && git push origin v$Version"
}
