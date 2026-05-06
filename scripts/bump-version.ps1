<#
.SYNOPSIS
    Bumps the PackTracker application version atomically across all source-of-truth files.

.DESCRIPTION
    Updates the version in every place it lives so the values cannot drift apart:
      - Directory.Build.props (Version, FileVersion, AssemblyVersion)
      - PackTracker.Presentation\PackTracker.Presentation.csproj
        (Version, AssemblyVersion, FileVersion, InformationalVersion — these override
        Directory.Build.props for the WPF host so they MUST be kept in sync)
      - installer\PackTrackerSetup.iss (default AppVersion)

    A leading 'v' on the input is silently stripped (so 'v0.7.0' and '0.7.0' both work).

    Optionally creates the matching annotated git tag (vX.Y.Z) so a single push triggers
    the release workflow with a guaranteed-matching version gate.

.PARAMETER Version
    The semantic version to set (e.g. 0.7.0 or v0.7.0). Must match X.Y.Z after the leading v is stripped.

.PARAMETER Tag
    If specified, also creates an annotated git tag vX.Y.Z pointing at HEAD.
    The tag is NOT pushed automatically — review with `git show vX.Y.Z` then `git push origin vX.Y.Z`.

.EXAMPLE
    .\scripts\bump-version.ps1 0.7.0

.EXAMPLE
    .\scripts\bump-version.ps1 v0.7.0 -Tag
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [switch]$Tag
)

$ErrorActionPreference = 'Stop'

# Strip a leading 'v' or 'V' so callers can pass either '0.7.0' or 'v0.7.0'.
$Version = $Version.Trim() -replace '^[vV]', ''

if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    Write-Error "Version must be in X.Y.Z form after stripping any leading 'v' (got '$Version')."
    exit 1
}

$repoRoot     = Resolve-Path (Join-Path $PSScriptRoot '..')
$propsPath    = Join-Path $repoRoot 'Directory.Build.props'
$presentation = Join-Path $repoRoot 'PackTracker.Presentation\PackTracker.Presentation.csproj'
$issPath      = Join-Path $repoRoot 'installer\PackTrackerSetup.iss'

foreach ($p in @($propsPath, $presentation, $issPath)) {
    if (-not (Test-Path $p)) { Write-Error "Not found: $p"; exit 1 }
}

# --- Directory.Build.props ---
$propsXml = Get-Content $propsPath -Raw
$propsXml = [regex]::Replace($propsXml, '<Version>[^<]+</Version>',                 "<Version>$Version</Version>")
$propsXml = [regex]::Replace($propsXml, '<FileVersion>[^<]+</FileVersion>',         "<FileVersion>$Version</FileVersion>")
$propsXml = [regex]::Replace($propsXml, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$Version</AssemblyVersion>")
Set-Content -Path $propsPath -Value $propsXml -Encoding utf8 -NoNewline
Write-Host "Updated Directory.Build.props -> $Version"

# --- PackTracker.Presentation.csproj (these override Directory.Build.props for the WPF host) ---
$csproj = Get-Content $presentation -Raw
$csproj = [regex]::Replace($csproj, '<Version>[^<]+</Version>',                             "<Version>$Version</Version>")
$csproj = [regex]::Replace($csproj, '<AssemblyVersion>[^<]+</AssemblyVersion>',             "<AssemblyVersion>$Version</AssemblyVersion>")
$csproj = [regex]::Replace($csproj, '<FileVersion>[^<]+</FileVersion>',                     "<FileVersion>$Version</FileVersion>")
$csproj = [regex]::Replace($csproj, '<InformationalVersion>[^<]+</InformationalVersion>',   "<InformationalVersion>$Version</InformationalVersion>")
Set-Content -Path $presentation -Value $csproj -Encoding utf8 -NoNewline
Write-Host "Updated PackTracker.Presentation\PackTracker.Presentation.csproj -> $Version"

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
    Write-Host "  git add Directory.Build.props PackTracker.Presentation\PackTracker.Presentation.csproj installer\PackTrackerSetup.iss"
    Write-Host "  git commit -m `"chore: bump version to $Version`""
    Write-Host "  git tag -a v$Version -m `"PackTracker v$Version`""
    Write-Host "  git push && git push origin v$Version"
}
