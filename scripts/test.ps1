[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Push-Location -LiteralPath (Split-Path -Parent $PSScriptRoot)
try {
    & (Join-Path $PSScriptRoot 'build.ps1')
    dotnet test Jellyfin.Plugin.AudioTranscodingControl.sln --configuration Release --no-build --no-restore --logger 'trx;LogFileName=tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
}
finally {
    Pop-Location
}
