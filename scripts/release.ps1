param(
    [string]$Level = "patch"
)

function Stop-IfFailed {
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed."
    }
}

git checkout main
Stop-IfFailed

git pull
Stop-IfFailed

$status = git status --porcelain
if ($status) {
    throw "Working tree is not clean. Commit or stash changes first."
}

$currentVersion = Select-String `
    -Path "Directory.Build.props" `
    -Pattern "<Version>(.*?)</Version>" |
        ForEach-Object { $_.Matches[0].Groups[1].Value }

if (-not $currentVersion) {
    throw "Could not find current version in Directory.Build.props"
}

$currentVersion = $currentVersion.TrimStart("v")
$parts = $currentVersion.Split(".")

$major = [int]$parts[0]
$minor = [int]$parts[1]
$patch = [int]$parts[2]

switch ($Level) {
    "major" {
        $major++
        $minor = 0
        $patch = 0
    }
    "minor" {
        $minor++
        $patch = 0
    }
    "patch" {
        $patch++
    }
    default {
        throw "Invalid level. Use: major, minor, or patch."
    }
}

$newVersion = "v$major.$minor.$patch"

Write-Host "Releasing $newVersion..."

.\scripts\bump-version.ps1 $newVersion
Stop-IfFailed

git diff

git add `
    Directory.Build.props `
    PackTracker.Presentation\PackTracker.Presentation.csproj `
    installer\PackTrackerSetup.iss

git commit -m "chore: bump version to $newVersion"
Stop-IfFailed

git push
Stop-IfFailed

git tag -a $newVersion -m "PackTracker $newVersion"
Stop-IfFailed

git push origin $newVersion
Stop-IfFailed

Write-Host "Release tag pushed: $newVersion"