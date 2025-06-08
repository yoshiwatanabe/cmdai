# CmdAI Development Build and Install Script for Windows
param(
    [switch]$SkipInstall
)

Write-Host "🔨 Building CmdAI for development..." -ForegroundColor Green

# Navigate to project directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Join-Path $scriptDir "..\cmdai\src\CmdAi.Cli"
Set-Location $projectDir

try {
    # Clean previous builds
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "./nupkg") { Remove-Item -Recurse -Force "./nupkg" }
    if (Test-Path "./bin") { Remove-Item -Recurse -Force "./bin" }
    if (Test-Path "./obj") { Remove-Item -Recurse -Force "./obj" }

    # Build and pack the tool
    Write-Host "📦 Building and packing CmdAI..." -ForegroundColor Cyan
    dotnet pack --configuration Release --output ./nupkg
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }

    # Get the package file
    $packageFile = Get-ChildItem "./nupkg/CmdAi.Cli.*.nupkg" | Select-Object -First 1
    if (-not $packageFile) {
        throw "Package file not found!"
    }

    Write-Host "📄 Package created: $($packageFile.FullName)" -ForegroundColor Green

    if (-not $SkipInstall) {
        # Uninstall existing version (ignore errors)
        Write-Host "🗑️ Uninstalling existing version..." -ForegroundColor Yellow
        dotnet tool uninstall --global CmdAi.Cli 2>$null

        # Install the new version
        Write-Host "⚡ Installing new version..." -ForegroundColor Cyan
        dotnet tool install --global --add-source ./nupkg CmdAi.Cli
        if ($LASTEXITCODE -ne 0) { throw "Installation failed" }
    }

    Write-Host "✅ CmdAI development build complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Test the installation:" -ForegroundColor White
    Write-Host "  cmdai --version" -ForegroundColor Gray
    Write-Host "  cmdai ask git `"check status`"" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To uninstall: dotnet tool uninstall --global CmdAi.Cli" -ForegroundColor Gray
}
catch {
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}