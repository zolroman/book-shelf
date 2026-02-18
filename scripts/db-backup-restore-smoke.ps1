param(
    [string]$ConnectionString = $env:BOOKSHELF_CONNECTION_STRING,
    [string]$WorkingDirectory = "artifacts/backup-smoke"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString))
{
    throw "Connection string is required. Set -ConnectionString or BOOKSHELF_CONNECTION_STRING."
}

if (-not (Get-Command pg_restore -ErrorAction SilentlyContinue))
{
    throw "pg_restore is not installed or not available in PATH."
}

if (-not (Test-Path $WorkingDirectory))
{
    New-Item -Path $WorkingDirectory -ItemType Directory -Force | Out-Null
}

$backupPath = Join-Path $WorkingDirectory "bookshelf-smoke.dump"

& "$PSScriptRoot/db-backup.ps1" -ConnectionString $ConnectionString -OutputPath $backupPath

if (-not (Test-Path $backupPath))
{
    throw "Backup file was not created: $backupPath"
}

& pg_restore --list "$backupPath" | Out-Null
if ($LASTEXITCODE -ne 0)
{
    throw "pg_restore list validation failed with exit code $LASTEXITCODE."
}

Write-Host "Backup/restore smoke check completed: $backupPath"
