# CmdAI Version Bumping Script for Windows
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("major", "minor", "patch")]
    [string]$BumpType,
    
    [Parameter(Mandatory=$false)]
    [string]$SpecificVersion
)

function Show-Usage {
    Write-Host "Usage: .\version-bump.ps1 <major|minor|patch> [version]" -ForegroundColor White
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor White
    Write-Host "  .\version-bump.ps1 patch              # Bump patch version (1.0.0 -> 1.0.1)" -ForegroundColor Gray
    Write-Host "  .\version-bump.ps1 minor              # Bump minor version (1.0.0 -> 1.1.0)" -ForegroundColor Gray
    Write-Host "  .\version-bump.ps1 major              # Bump major version (1.0.0 -> 2.0.0)" -ForegroundColor Gray
    Write-Host "  .\version-bump.ps1 patch -SpecificVersion 1.2.3  # Set specific version" -ForegroundColor Gray
    Write-Host ""
}

function Get-CurrentVersion {
    param([string]$ProjectFile)
    
    $content = Get-Content $ProjectFile -Raw
    if ($content -match '<Version>([^<]+)</Version>') {
        return $Matches[1]
    }
    return $null
}

function Update-Version {
    param(
        [string]$ProjectFile,
        [string]$NewVersion
    )
    
    $content = Get-Content $ProjectFile -Raw
    
    # Update Version
    $content = $content -replace '<Version>[^<]*</Version>', "<Version>$NewVersion</Version>"
    
    # Update AssemblyVersion (major.minor.0.0)
    $versionParts = $NewVersion.Split('.')
    $major = $versionParts[0]
    $minor = $versionParts[1]
    $assemblyVersion = "$major.$minor.0.0"
    $content = $content -replace '<AssemblyVersion>[^<]*</AssemblyVersion>', "<AssemblyVersion>$assemblyVersion</AssemblyVersion>"
    
    # Update FileVersion (full version + .0)
    $fileVersion = "$NewVersion.0"
    $content = $content -replace '<FileVersion>[^<]*</FileVersion>', "<FileVersion>$fileVersion</FileVersion>"
    
    # Update AssemblyInformationalVersion
    $content = $content -replace '<AssemblyInformationalVersion>[^<]*</AssemblyInformationalVersion>', "<AssemblyInformationalVersion>$NewVersion</AssemblyInformationalVersion>"
    
    Set-Content $ProjectFile $content -NoNewline
}

function Get-IncrementedVersion {
    param(
        [string]$Version,
        [string]$BumpType
    )
    
    $versionParts = $Version.Split('.')
    $major = [int]$versionParts[0]
    $minor = [int]$versionParts[1]
    $patch = [int]$versionParts[2]
    
    switch ($BumpType) {
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
    }
    
    return "$major.$minor.$patch"
}

# Main script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $scriptDir "..\cmdai\src\CmdAi.Cli\CmdAi.Cli.csproj"

# Check if project file exists
if (-not (Test-Path $projectFile)) {
    Write-Host "❌ Project file not found: $projectFile" -ForegroundColor Red
    exit 1
}

# Get current version
$currentVersion = Get-CurrentVersion $projectFile
if (-not $currentVersion) {
    Write-Host "❌ Could not find current version in project file" -ForegroundColor Red
    exit 1
}

Write-Host "Current version: $currentVersion" -ForegroundColor Blue

# Calculate new version
if ($SpecificVersion) {
    # Validate version format
    if ($SpecificVersion -notmatch '^\d+\.\d+\.\d+$') {
        Write-Host "❌ Invalid version format: $SpecificVersion" -ForegroundColor Red
        Write-Host "Version must be in format: major.minor.patch (e.g., 1.2.3)" -ForegroundColor Yellow
        exit 1
    }
    $newVersion = $SpecificVersion
} else {
    $newVersion = Get-IncrementedVersion $currentVersion $BumpType
}

Write-Host "New version: $newVersion" -ForegroundColor Yellow

# Confirm the change
$confirm = Read-Host "Update version from $currentVersion to $newVersion? (y/N)"
if ($confirm -notmatch '^[Yy]$') {
    Write-Host "Version update cancelled." -ForegroundColor Gray
    exit 0
}

# Update the version
try {
    Update-Version $projectFile $newVersion
    Write-Host "✅ Version updated successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Changes made:" -ForegroundColor White
    Write-Host "  Version: $currentVersion → $newVersion" -ForegroundColor Gray
    Write-Host "  AssemblyVersion: Updated to match major.minor" -ForegroundColor Gray
    Write-Host "  FileVersion: Updated to match full version" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor White
    Write-Host "  1. Review changes: git diff $projectFile" -ForegroundColor Gray
    Write-Host "  2. Test build: .\scripts\build-dev.ps1" -ForegroundColor Gray
    Write-Host "  3. Commit changes: git add . && git commit -m `"chore: bump version to v$newVersion`"" -ForegroundColor Gray
    Write-Host "  4. Tag release: git tag v$newVersion" -ForegroundColor Gray
    Write-Host "  5. Push changes: git push && git push --tags" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Error updating version: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}