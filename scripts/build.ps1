[CmdletBinding()]
param([switch]$VerifyDeterminism)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location -LiteralPath $repositoryRoot
try {
    $apiPath = Join-Path $repositoryRoot '.local/server/jellyfin/Jellyfin.Api.dll'
    if (-not (Test-Path -LiteralPath $apiPath)) { throw 'Run scripts/prepare-test-server.ps1 first.' }
    & (Join-Path $PSScriptRoot 'prepare-test-server.ps1')
    dotnet restore Jellyfin.Plugin.AudioTranscodingControl.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed.' }
    dotnet build Jellyfin.Plugin.AudioTranscodingControl.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

    $pluginDirectory = Join-Path $repositoryRoot 'src/Jellyfin.Plugin.AudioTranscodingControl/bin/Release/net9.0'
    $pluginPath = Join-Path $pluginDirectory 'Jellyfin.Plugin.AudioTranscodingControl.dll'
    $foreignDlls = @(Get-ChildItem -LiteralPath $pluginDirectory -Filter '*.dll' | Where-Object Name -ne 'Jellyfin.Plugin.AudioTranscodingControl.dll')
    if ($foreignDlls.Count -ne 0) { throw 'Plugin output contains unexpected host/dependency DLLs.' }

    if ($VerifyDeterminism) {
        $firstHash = (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash
        dotnet build src/Jellyfin.Plugin.AudioTranscodingControl/Jellyfin.Plugin.AudioTranscodingControl.csproj --configuration Release --no-restore --target:Rebuild
        if ($LASTEXITCODE -ne 0) { throw 'Determinism rebuild failed.' }
        $secondHash = (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash
        if ($firstHash -ne $secondHash) { throw 'Plugin DLL changed across builds in the same environment.' }
        Write-Output "Deterministic plugin DLL SHA256: $secondHash"
    }
}
finally {
    Pop-Location
}
