[CmdletBinding()]
param(
    [string]$ImageName = "bookshelf-devcontainer",
    [string]$ContainerName = "bookshelf-devcontainer",
    [string]$VolumeName = "bookshelf-devcontainer-workspace",
    [switch]$Detached
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$workspacePath = "/workspaces/book-shelf"

$dockerArgs = @(
    "run",
    "--name", $ContainerName,
    "--mount", "type=volume,source=$VolumeName,target=$workspacePath",
    "--workdir", $workspacePath
)

if ($Detached)
{
    $dockerArgs += @("-d")
}
else
{
    $dockerArgs += @("--rm", "-it")
}

if ($Detached)
{
    # Keep container running for attach/exec workflows.
    $dockerArgs += @($ImageName, "sleep", "infinity")
}
else
{
    # Start an interactive dev shell in the mounted workspace.
    $dockerArgs += @($ImageName, "bash")
}

Write-Host ("Running: docker " + ($dockerArgs -join " "))
& docker @dockerArgs
exit $LASTEXITCODE
