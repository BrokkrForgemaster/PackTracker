<#
.SYNOPSIS
    Builds a PackTracker release end to end with one command.

.DESCRIPTION
    This script mirrors the local release workflow expected by the repository:
      1. bump the version across source-of-truth files
      2. restore dependencies
      3. run integration, API, and unit tests in Release
      4. publish the desktop app to the repo-root publish\ folder
      5. build the Inno Setup installer
      6. optionally commit, tag, and push the release after everything passes

    The installer consumes ..\publish\* relative to installer\PackTrackerSetup.iss,
    so the publish output path is intentional and should not be changed casually.

    Git automation is intentionally strict. If -AutomateGit is used, the working tree
    must already be clean before the script starts so the release commit only captures
    the version bump files produced by this script.

.PARAMETER Version
    The semantic version to build, for example 0.8.3 or v0.8.3.

.PARAMETER AutomateGit
    After a successful build, create the release commit, create tag vX.Y.Z,
    push the current branch, and push the tag.

.PARAMETER Remote
    The git remote to push to when -AutomateGit is used. Defaults to origin.

.EXAMPLE
    .\scripts\build-release.ps1 0.8.3

.EXAMPLE
    .\scripts\build-release.ps1 0.8.3 -AutomateGit
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version,

    [switch]$AutomateGit,

    [string]$Remote = 'origin'
)

$ErrorActionPreference = 'Stop'

$Version = $Version.Trim() -replace '^[vV]', ''
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Version must be in X.Y.Z form after stripping any leading 'v' (got '$Version')."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$publishDir = Join-Path $repoRoot 'publish'
$installerScript = Join-Path $repoRoot 'installer\PackTrackerSetup.iss'
$installerOutput = Join-Path $repoRoot "installer\output\PackTrackerSetup-$Version.exe"
$bumpScript = Join-Path $repoRoot 'scripts\bump-version.ps1'
$versionFiles = @(
    'Directory.Build.props',
    'PackTracker.Presentation\PackTracker.Presentation.csproj',
    'installer\PackTrackerSetup.iss'
)

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-InnoSetupCompiler {
    $defaultPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (Test-Path $defaultPath) {
        return $defaultPath
    }

    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw "Inno Setup compiler not found. Install Inno Setup 6 or ensure ISCC.exe is on PATH."
}

function Get-GitStatusLines {
    $output = & git status --short
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to read git status.'
    }

    return @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Assert-CleanGitWorktreeForAutomation {
    $statusLines = Get-GitStatusLines
    if ($statusLines.Count -gt 0) {
        throw "Git automation requires a clean working tree before the release starts. Commit or stash existing changes, then rerun with -AutomateGit."
    }
}

function Get-CurrentGitBranch {
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($branch)) {
        throw 'Git automation requires a checked-out branch.'
    }

    return $branch
}

function Assert-ReleaseTagDoesNotExist {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $existing = (& git tag --list $TagName).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect existing git tags.'
    }

    if (-not [string]::IsNullOrWhiteSpace($existing)) {
        throw "Git tag '$TagName' already exists."
    }
}

function Invoke-GitReleaseAutomation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ReleaseVersion,

        [Parameter(Mandatory = $true)]
        [string]$GitRemote
    )

    $branch = Get-CurrentGitBranch
    $tagName = "v$ReleaseVersion"

    Assert-ReleaseTagDoesNotExist -TagName $tagName

    foreach ($path in $versionFiles) {
        Invoke-Native -FilePath 'git' -Arguments @('add', $path)
    }

    Invoke-Native -FilePath 'git' -Arguments @(
        'commit',
        '-m', "chore: bump version to $ReleaseVersion"
    )

    Invoke-Native -FilePath 'git' -Arguments @(
        'tag',
        '-a', $tagName,
        '-m', "PackTracker $tagName"
    )

    Invoke-Native -FilePath 'git' -Arguments @('push', $GitRemote, $branch)
    Invoke-Native -FilePath 'git' -Arguments @('push', $GitRemote, $tagName)
}

function Assert-ReleaseArtifactsUnlocked {
    $runningProcesses = @(
        Get-Process -Name 'PackTracker.Presentation' -ErrorAction SilentlyContinue
        Get-Process -Name 'PackTracker.Api' -ErrorAction SilentlyContinue
    ) | Where-Object { $_ }

    if ($runningProcesses.Count -gt 0) {
        $names = $runningProcesses | Select-Object -ExpandProperty ProcessName -Unique
        throw "Close the running PackTracker processes before building a release: $($names -join ', ')"
    }
}

if ($AutomateGit) {
    Invoke-Step 'Verify git worktree is clean for automation' {
        Assert-CleanGitWorktreeForAutomation
    }
}

Invoke-Step 'Sync version files' {
    Invoke-Native -FilePath 'powershell.exe' -Arguments @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $bumpScript,
        $Version
    )
}

Invoke-Step 'Restore solution' {
    Invoke-Native -FilePath 'dotnet' -Arguments @(
        'restore',
        (Join-Path $repoRoot 'PackTracker.sln')
    )
}

Invoke-Step 'Run integration tests' {
    Invoke-Native -FilePath 'dotnet' -Arguments @(
        'test',
        (Join-Path $repoRoot 'tests\PackTracker.IntegrationTests\PackTracker.IntegrationTests.csproj'),
        '-c', 'Release',
        '--no-restore'
    )
}

Invoke-Step 'Run API tests' {
    Invoke-Native -FilePath 'dotnet' -Arguments @(
        'test',
        (Join-Path $repoRoot 'tests\PackTracker.ApiTests\PackTracker.ApiTests.csproj'),
        '-c', 'Release',
        '--no-restore'
    )
}

Invoke-Step 'Run unit tests' {
    Invoke-Native -FilePath 'dotnet' -Arguments @(
        'test',
        (Join-Path $repoRoot 'tests\PackTracker.UnitTests\PackTracker.UnitTests.csproj'),
        '-c', 'Release',
        '--no-restore'
    )
}

Invoke-Step 'Verify release files are not locked' {
    Assert-ReleaseArtifactsUnlocked
}

Invoke-Step 'Prepare publish folder' {
    if (Test-Path $publishDir) {
        $resolvedPublishDir = (Resolve-Path $publishDir).Path
        if ($resolvedPublishDir -ne $publishDir) {
            $publishDir = $resolvedPublishDir
        }

        try {
            Remove-Item -LiteralPath $publishDir -Recurse -Force
        }
        catch {
            throw "Unable to clear '$publishDir'. Close PackTracker or anything using files in that folder, then rerun the script."
        }
    }

    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

Invoke-Step 'Publish desktop app' {
    Invoke-Native -FilePath 'dotnet' -Arguments @(
        'publish',
        (Join-Path $repoRoot 'PackTracker.Presentation\PackTracker.Presentation.csproj'),
        '--configuration', 'Release',
        '--runtime', 'win-x64',
        '--self-contained', 'true',
        "-p:Version=$Version",
        '--output', $publishDir
    )
}

Invoke-Step 'Build installer' {
    $iscc = Get-InnoSetupCompiler
    Invoke-Native -FilePath $iscc -Arguments @(
        "/DAppVersion=$Version",
        $installerScript
    )
}

if (-not (Test-Path $installerOutput)) {
    throw "Installer build completed, but expected artifact was not found: $installerOutput"
}

if ($AutomateGit) {
    Invoke-Step 'Commit, tag, and push release' {
        Invoke-GitReleaseAutomation -ReleaseVersion $Version -GitRemote $Remote
    }
}

Write-Host ""
Write-Host "Release build complete." -ForegroundColor Green
Write-Host "Installer: $installerOutput"
