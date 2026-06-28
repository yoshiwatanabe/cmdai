$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $scriptRoot
try {
    python -m cmdai @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

