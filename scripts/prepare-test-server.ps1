[CmdletBinding()]
param([string]$ArchivePath)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local'
$destination = Join-Path $localRoot 'server'
$apiPath = Join-Path $destination 'jellyfin/Jellyfin.Api.dll'
$expectedHash = '3B5D3E904415AC6BD2C0EE9FE93235B4145D41C4C85AE782D244AC9767F77374'
if (Test-Path -LiteralPath $apiPath) {
    if ((Get-FileHash -LiteralPath $apiPath -Algorithm SHA256).Hash -ne '7841D8E03F974EB89BF7DF4A81F3DEF9CF38C094905EA7EBAB2699FEDC695E1A') { throw 'Unexpected Jellyfin API binary.' }
    Write-Output 'Portable Jellyfin 10.11.11 is already available.'
    return
}
if (Test-Path -LiteralPath $destination) { throw 'Existing server directory is incomplete; no files overwritten.' }
New-Item -ItemType Directory -Force -Path $localRoot | Out-Null
if (-not $ArchivePath) {
    $ArchivePath = Join-Path $localRoot 'jellyfin_10.11.11-amd64.zip'
    if (-not (Test-Path -LiteralPath $ArchivePath)) {
        Invoke-WebRequest -Uri 'https://repo.jellyfin.org/files/server/windows/stable/v10.11.11/amd64/jellyfin_10.11.11-amd64.zip' -OutFile $ArchivePath -UseBasicParsing
    }
}
if ((Get-FileHash -LiteralPath $ArchivePath -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Portable archive does not match the pinned SHA256.' }
Expand-Archive -LiteralPath $ArchivePath -DestinationPath $destination
if ((Get-FileHash -LiteralPath $apiPath -Algorithm SHA256).Hash -ne '7841D8E03F974EB89BF7DF4A81F3DEF9CF38C094905EA7EBAB2699FEDC695E1A') { throw 'Unexpected Jellyfin API binary.' }
Write-Output 'Prepared isolated portable Jellyfin 10.11.11. No service installed or started.'
