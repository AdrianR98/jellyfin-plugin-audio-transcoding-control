[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null
[xml]$properties = Get-Content -LiteralPath (Join-Path $repositoryRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
$assemblyVersion = [string]$properties.Project.PropertyGroup.AssemblyVersion
$zipPath = Join-Path $output "Jellyfin.Plugin.AudioTranscodingControl-$version.zip"
if (Test-Path -LiteralPath $zipPath) { throw 'Package already exists; no overwrite performed.' }
$dll = Join-Path $repositoryRoot 'src/Jellyfin.Plugin.AudioTranscodingControl/bin/Release/net9.0/Jellyfin.Plugin.AudioTranscodingControl.dll'
if ([Reflection.AssemblyName]::GetAssemblyName($dll).Version -ne [Version]$assemblyVersion) { throw "Build Release version $version first." }
$foreignDlls = @(Get-ChildItem -LiteralPath (Split-Path $dll) -Filter '*.dll' | Where-Object Name -ne 'Jellyfin.Plugin.AudioTranscodingControl.dll')
if ($foreignDlls.Count) { throw 'Unexpected dependency DLL in build output.' }
$staging = Join-Path $repositoryRoot ('.local/package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
Copy-Item -LiteralPath $dll -Destination $staging
foreach ($name in @('LICENSE', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $repositoryRoot $name) -Destination $staging }
$manifest = [ordered]@{
    PluginId = 'c48a5098-04e0-4de5-a46c-8adae788f91b'; Version = $version; AssemblyVersion = $assemblyVersion
    TargetServer = '10.11.11'; DllName = 'Jellyfin.Plugin.AudioTranscodingControl.dll'
    DllSHA256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    RuntimeFiles = @('Jellyfin.Plugin.AudioTranscodingControl.dll')
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging 'package-manifest.json') -Encoding utf8
# Fixed entry order, timestamps and compression; no configuration, PDB or host binaries.
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($name in @('Jellyfin.Plugin.AudioTranscodingControl.dll', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'package-manifest.json')) {
        $entry = $zip.CreateEntry($name, [IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
        $destination = $entry.Open()
        try {
            $bytes = [IO.File]::ReadAllBytes((Join-Path $staging $name))
            $destination.Write($bytes, 0, $bytes.Length)
        } finally { $destination.Dispose() }
    }
} finally { $zip.Dispose(); $stream.Dispose() }
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
$md5 = (Get-FileHash -LiteralPath $zipPath -Algorithm MD5).Hash.ToLowerInvariant()
[PSCustomObject]@{ Package = $zipPath; MD5 = $md5; SHA256 = $hash.ToLowerInvariant(); Entries = $actual; CoreDllsIncluded = $false }
