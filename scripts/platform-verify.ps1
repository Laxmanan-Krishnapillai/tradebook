param(
    [switch]$FoundationOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $PSScriptRoot 'platform-verify.sh'
$gitBashCandidates = @(
    (Join-Path $env:ProgramFiles 'Git\bin\bash.exe'),
    (Join-Path $env:ProgramFiles 'Git\usr\bin\bash.exe')
)
$gitBash = $gitBashCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $gitBash) {
    throw 'Git for Windows Bash is required to run scripts/platform-verify.sh with the Windows toolchain.'
}

$arguments = @($scriptPath)
if ($FoundationOnly) {
    $arguments += '--foundation-only'
}

Push-Location $repositoryRoot
try {
    & $gitBash @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Platform verification failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
