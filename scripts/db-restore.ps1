param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile,
    [string]$ConnectionString = $env:BOOKSHELF_CONNECTION_STRING,
    [switch]$DropExistingObjects
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString))
{
    throw "Connection string is required. Set -ConnectionString or BOOKSHELF_CONNECTION_STRING."
}

if (-not (Test-Path $BackupFile))
{
    throw "Backup file was not found: $BackupFile"
}

if (-not (Get-Command pg_restore -ErrorAction SilentlyContinue))
{
    throw "pg_restore is not installed or not available in PATH."
}

$resolvedBackupFile = (Resolve-Path $BackupFile).Path
$arguments = @(
    "--no-owner",
    "--no-privileges"
)

if ($DropExistingObjects)
{
    $arguments += "--clean"
    $arguments += "--if-exists"
}

$arguments += "--dbname=$ConnectionString"
$arguments += $resolvedBackupFile

& pg_restore @arguments
if ($LASTEXITCODE -ne 0)
{
    throw "pg_restore failed with exit code $LASTEXITCODE."
}

Write-Host "Restore completed from: $resolvedBackupFile"
