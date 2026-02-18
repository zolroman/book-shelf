param(
    [string]$ConnectionString = $env:BOOKSHELF_CONNECTION_STRING,
    [string]$OutputPath = "artifacts/backups/bookshelf-$(Get-Date -Format 'yyyyMMdd-HHmmss').dump"
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ConnectionString))
{
    throw "Connection string is required. Set -ConnectionString or BOOKSHELF_CONNECTION_STRING."
}

if (-not (Get-Command pg_dump -ErrorAction SilentlyContinue))
{
    throw "pg_dump is not installed or not available in PATH."
}

$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath))
{
    $OutputPath
}
else
{
    Join-Path (Get-Location) $OutputPath
}

$outputDirectory = Split-Path -Parent $resolvedOutput
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path $outputDirectory))
{
    New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
}

& pg_dump --format=custom --no-owner --no-privileges --file="$resolvedOutput" --dbname="$ConnectionString"
if ($LASTEXITCODE -ne 0)
{
    throw "pg_dump failed with exit code $LASTEXITCODE."
}

Write-Host "Backup created: $resolvedOutput"
