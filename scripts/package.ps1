[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
$zipPath = Join-Path $output 'Jellyfin.Plugin.AudioTranscodingControl-0.1.0.zip'
if (Test-Path -LiteralPath $zipPath) { throw 'Package already exists; no overwrite performed.' }
$dll = Join-Path $repositoryRoot 'src/Jellyfin.Plugin.AudioTranscodingControl/bin/Release/net9.0/Jellyfin.Plugin.AudioTranscodingControl.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version -ne [Version]'0.1.0.0') { throw 'Build Release version 0.1.0 first.' }
$staging = Join-Path $repositoryRoot ('.local/package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -LiteralPath $dll -Destination $staging
foreach ($name in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $staging }
$manifest = [ordered]@{
    PluginId = 'c48a5098-04e0-4de5-a46c-8adae788f91b'; Version = '0.1.0'; AssemblyVersion = '0.1.0.0'
    TargetServer = '10.11.11'; DllName = 'Jellyfin.Plugin.AudioTranscodingControl.dll'
    DllSHA256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    RuntimeFiles = @('Jellyfin.Plugin.AudioTranscodingControl.dll')
    Published = $false
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging 'package-manifest.json') -Encoding utf8
Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $staging -File | Select-Object -ExpandProperty FullName) -DestinationPath $zipPath
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actual = @($archive.Entries.FullName | Sort-Object)
    $expected = @('Jellyfin.Plugin.AudioTranscodingControl.dll', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'package-manifest.json') | Sort-Object
    if (@(Compare-Object $actual $expected).Count) { throw 'Unexpected ZIP entries.' }
}
finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($zipPath))" | Set-Content -LiteralPath ($zipPath + '.sha256') -Encoding ascii
[PSCustomObject]@{ Package = $zipPath; SHA256 = $hash; Entries = $actual; CoreDllsIncluded = $false }
