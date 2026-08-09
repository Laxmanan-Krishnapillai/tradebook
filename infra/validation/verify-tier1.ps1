[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$terraformDirectory = Join-Path $repositoryRoot 'infra\terraform'
$terraformFiles = Get-ChildItem -LiteralPath $terraformDirectory -Filter '*.tf' -File

if ($terraformFiles.Count -eq 0) {
    throw 'No Terraform files were found.'
}

$hcl = ($terraformFiles | ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw }) -join "`n"
$hclWithoutComments = $hcl -replace '(?ms)/\*.*?\*/', '' -replace '(?m)^\s*(#|//).*$\r?\n?', ''

$forbiddenTechnologyPatterns = @(
    '(?i)\bredis\b',
    '(?i)\bnats\b',
    '(?i)\btimescale(db)?\b',
    '(?i)\bscylla(db)?\b',
    '(?i)\bmerkle\b',
    '(?i)azurerm_storage_container_immutability_policy'
)

foreach ($pattern in $forbiddenTechnologyPatterns) {
    if ($hclWithoutComments -match $pattern) {
        throw "Forbidden Tier-2/Tier-3 or deferred-compliance technology matched Terraform pattern: $pattern"
    }
}

if ($hclWithoutComments -match '(?i)tier_to_archive_after_days_since_modification_greater_than') {
    throw 'Backup blobs must remain in an online tier so the shipped restore job can read every retained backup directly.'
}

$requiredPatterns = @(
    'resource\s+"azurerm_container_app"',
    'resource\s+"azurerm_postgresql_flexible_server"',
    'version\s*=\s*"17"',
    'resource\s+"azurerm_container_app_job"\s+"migration"',
    'resource\s+"azurerm_container_app_job"\s+"backup"',
    'resource\s+"azurerm_container_app_job"\s+"restore"',
    'logs_destination\s*=\s*"log-analytics"',
    'versioning_enabled\s*=\s*true',
    'skip_query_validation\s*=\s*true',
    'ephemeral\s+"random_password"',
    'administrator_password_wo\s*=',
    'value_wo\s*='
)

foreach ($pattern in $requiredPatterns) {
    if ($hclWithoutComments -notmatch $pattern) {
        throw "Required Tier-1 control is missing from Terraform: $pattern"
    }
}

$forbiddenSecretPatterns = @(
    'resource\s+"random_password"',
    '(?m)^\s*administrator_password\s*=',
    '(?m)^\s*value\s*=.*password'
)

foreach ($pattern in $forbiddenSecretPatterns) {
    if ($hclWithoutComments -match $pattern) {
        throw "Terraform persists or embeds a secret through forbidden pattern: $pattern"
    }
}

$namePrefixContractPatterns = @(
    'can\(regex\("\^\[a-z0-9\]\[a-z0-9-\]\{1,11\}\[a-z0-9\]\$",\s*var\.name_prefix\)\)',
    '!strcontains\(var\.name_prefix,\s*"--"\)',
    'var\.environment\s*!=\s*"prod"\s*\|\|\s*var\.alert_email\s*!=\s*null'
)
foreach ($pattern in $namePrefixContractPatterns) {
    if ($hclWithoutComments -notmatch $pattern) {
        throw "Terraform name_prefix validation is missing Azure Key Vault boundary control: $pattern"
    }
}

function Test-TradebookNamePrefix {
    param([Parameter(Mandatory)][string] $Value)

    return $Value -cmatch '^[a-z0-9][a-z0-9-]{1,11}[a-z0-9]$' -and -not $Value.Contains('--')
}

foreach ($validPrefix in @('abc', 'abc-def', 'abcdefghijklm')) {
    if (-not (Test-TradebookNamePrefix -Value $validPrefix)) {
        throw "Expected valid name_prefix was rejected by the verification contract: $validPrefix"
    }
    foreach ($environmentName in @('dev', 'staging', 'prod')) {
        $keyVaultName = "kv-$validPrefix-$environmentName"
        if ($keyVaultName.Length -gt 24 -or $keyVaultName -cnotmatch '^[a-z0-9-]+$' -or $keyVaultName.Contains('--')) {
            throw "Valid name_prefix produces an invalid Azure Key Vault name: $keyVaultName"
        }
    }
}

foreach ($invalidPrefix in @('ab', 'abcdefghijklmn', 'abc--def', 'Abc', 'abc-')) {
    if (Test-TradebookNamePrefix -Value $invalidPrefix) {
        throw "Expected invalid name_prefix passed the verification contract: $invalidPrefix"
    }
}

$dockerfile = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Dockerfile') -Raw
if ($dockerfile -notmatch 'HEALTHCHECK[\s\S]*/health/live') {
    throw 'The runtime container does not define an executable /health/live HEALTHCHECK.'
}
if ($dockerfile -notmatch 'FROM postgres:17-bookworm AS database-ops') {
    throw 'The PostgreSQL 17 database-ops image target is missing.'
}

$shellMigrator = Get-Content -LiteralPath (Join-Path $repositoryRoot 'infra\database-ops\run-migrations.sh') -Raw
$csharpMigrator = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src\Backend\src\Tradebook.Infrastructure\Migrations\MigrationRunner.cs') -Raw
if ($shellMigrator -notmatch 'Tradebook.Migrations.dll' -or
    $csharpMigrator -notmatch 'JournalToPostgresqlTable\("public", "schema_journal"\)') {
    throw 'The operations image and application must use the shared DbUp migration runner.'
}

$previousPostgresPassword = $env:POSTGRES_PASSWORD
try {
    $env:POSTGRES_PASSWORD = 'tier1-topology-validation-only'
    $composeServices = @(& docker compose --file (Join-Path $repositoryRoot 'docker-compose.yml') config --services)
    if ($LASTEXITCODE -ne 0) {
        throw 'docker compose config failed.'
    }
}
finally {
    if ($null -eq $previousPostgresPassword) {
        Remove-Item -LiteralPath Env:POSTGRES_PASSWORD
    }
    else {
        $env:POSTGRES_PASSWORD = $previousPostgresPassword
    }
}

if ($composeServices.Count -ne 1 -or $composeServices[0] -ne 'postgres') {
    throw "D9 requires default Compose to expose only postgres; found: $($composeServices -join ', ')"
}

Write-Host 'Tier-1 infrastructure, state-safe secrets, D6 backup, and D9 Compose policies passed.'
