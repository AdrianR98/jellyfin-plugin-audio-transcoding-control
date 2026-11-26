[CmdletBinding(SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [ValidateSet('Status', 'Install', 'Disable', 'Uninstall')][string]$Action = 'Status',
    [Parameter(Mandatory)][string]$PlanPath,
    [string]$PackagePath,
    [string]$ExpectedSHA256
)

$ErrorActionPreference = 'Stop'
$pluginName = 'Jellyfin.Plugin.AudioTranscodingControl'
$pluginId = 'c48a5098-04e0-4de5-a46c-8adae788f91b'
$planFile = (Resolve-Path -LiteralPath $PlanPath).ProviderPath
$plan = Get-Content -LiteralPath $planFile -Raw | ConvertFrom-Json
$serverDirectory = (Resolve-Path -LiteralPath $plan.ServerDirectory).ProviderPath
$dataDirectory = (Resolve-Path -LiteralPath $plan.DataDirectory).ProviderPath
$plugins = (Resolve-Path -LiteralPath $plan.PluginDirectory).ProviderPath
if ($plugins -ne (Join-Path $dataDirectory 'plugins')) { throw 'Unexpected plugin root.' }
$destination = [IO.Path]::GetFullPath((Join-Path $plugins ($pluginName + '_0.1.0.0')))
if ((Split-Path -Parent $destination) -ne $plugins) { throw 'Unsafe plugin destination.' }
$configDirectory = Join-Path $plugins 'configurations'
$configurationPath = Join-Path $configDirectory ($pluginName + '.xml')
$loggingPath = Join-Path $plan.ConfigDirectory 'logging.json'
$statePath = Join-Path (Split-Path -Parent $planFile) 'installation-state.private.json'
$serverExe = Join-Path $serverDirectory 'jellyfin.exe'
$running = @(Get-CimInstance Win32_Process -Filter "Name='jellyfin.exe'" | Where-Object { $_.ExecutablePath -eq $serverExe })
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $serverDirectory 'MediaBrowser.Controller.dll')).Version -ne [Version]'10.11.11.0') { throw 'Unexpected server version.' }
if ((Get-FileHash -LiteralPath (Join-Path $serverDirectory 'Jellyfin.Api.dll') -Algorithm SHA256).Hash -ne '7841D8E03F974EB89BF7DF4A81F3DEF9CF38C094905EA7EBAB2699FEDC695E1A') { throw 'API binary differs from the tested server.' }

function Assert-NoReparse([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force -ErrorAction SilentlyContinue
    if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Reparse points are not supported for installation paths.' }
}
foreach ($path in @($plugins, $destination, $configDirectory, $configurationPath, $loggingPath)) { Assert-NoReparse $path }

function Write-NewText([string]$Path, [string]$Text) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
        try { $writer.Write($Text) } finally { $writer.Dispose() }
    }
    finally { $stream.Dispose() }
}

if ($Action -eq 'Status') {
    [PSCustomObject]@{ ServerRunning = ($running.Count -gt 0); PluginFolderPresent = (Test-Path -LiteralPath $destination); PluginConfigPresent = (Test-Path -LiteralPath $configurationPath); LocalInstallStatePresent = (Test-Path -LiteralPath $statePath) }
    if (Test-Path -LiteralPath $configurationPath) {
        [xml]$configuration = Get-Content -LiteralPath $configurationPath -Raw
        [PSCustomObject]@{ Enabled = $configuration.PluginConfiguration.IsEnabled; RuleEnabled = $configuration.PluginConfiguration.Ac3Rule.IsEnabled; DebugEnabled = $configuration.PluginConfiguration.EnableDebugLogging; Identifiers = 'withheld' }
    }
    Get-ChildItem -LiteralPath $plan.LogDirectory -Filter '*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 2 | ForEach-Object {
        Get-Content -LiteralPath $_.FullName -Tail 1000 | Where-Object { $_ -match 'Loaded assembly Jellyfin\.Plugin\.AudioTranscodingControl|ATC (applied|skipped|preparation)' } | Select-Object -Last 12
    }
    return
}

# -WhatIf performs validation only and may run while the server remains online.
if ($running.Count -gt 0 -and -not $WhatIfPreference) { throw 'Stop this server through its existing tray controls before changing plugin files. No process was stopped.' }

if ($Action -eq 'Install') {
    if (-not $PackagePath -or -not $ExpectedSHA256) { throw 'PackagePath and the separately recorded ExpectedSHA256 are required.' }
    $package = (Resolve-Path -LiteralPath $PackagePath).ProviderPath
    $packageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
    if ($packageHash -ne $ExpectedSHA256) { throw 'Package SHA256 mismatch.' }
    if ((Test-Path -LiteralPath $destination) -or (Test-Path -LiteralPath $configurationPath) -or (Test-Path -LiteralPath $statePath)) { throw 'Own plugin/config/state already exists; nothing overwritten.' }
    if (@(Get-ChildItem -LiteralPath $plugins -Directory | Where-Object { $_.Name -like '*AudioTranscodingControl*' }).Count) { throw 'An existing version of this plugin needs separate review; no automatic upgrade.' }
    $config = Get-Content -LiteralPath $plan.ConfigurationFile -Raw | ConvertFrom-Json
    if (-not $config.IsEnabled -or -not $config.Ac3Rule.IsEnabled -or [string]::IsNullOrWhiteSpace($config.Ac3Rule.DeviceId) -or [Guid]$config.Ac3Rule.UserId -eq [Guid]::Empty) { throw 'A complete, explicitly enabled device/user configuration is required.' }
    if ($config.EnableDebugLogging -and (Test-Path -LiteralPath $loggingPath)) { throw 'Existing logging.json will not be overwritten; review diagnostics separately.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $entries = @($archive.Entries.FullName | Sort-Object)
        $expectedEntries = @(($pluginName + '.dll'), 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'package-manifest.json') | Sort-Object
        if (@(Compare-Object $entries $expectedEntries).Count) { throw 'Unexpected/unsafe package entries.' }
        $reader = [IO.StreamReader]::new($archive.GetEntry('package-manifest.json').Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
        if ($manifest.PluginId -ne $pluginId -or $manifest.AssemblyVersion -ne '0.1.0.0') { throw 'Unexpected package identity.' }
    }
    finally { $archive.Dispose() }
    if (-not $PSCmdlet.ShouldProcess($destination, 'Install only this plugin, its new XML configuration and a new plugin-scoped logging override')) { return }

    $staging = Join-Path $plugins ('.atc-stage-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $staging | Out-Null
    Expand-Archive -LiteralPath $package -DestinationPath $staging
    $dll = Join-Path $staging ($pluginName + '.dll')
    if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $manifest.DllSHA256 -or [Reflection.AssemblyName]::GetAssemblyName($dll).Version -ne [Version]'0.1.0.0') { throw 'Staged DLL verification failed; no active plugin files changed.' }
    $xml = [xml]'<PluginConfiguration><IsEnabled>true</IsEnabled><EnableDebugLogging>false</EnableDebugLogging><Ac3Rule><IsEnabled>true</IsEnabled><DeviceId/><UserId/></Ac3Rule></PluginConfiguration>'
    $xml.PluginConfiguration.EnableDebugLogging = $config.EnableDebugLogging.ToString().ToLowerInvariant()
    $xml.PluginConfiguration.Ac3Rule.DeviceId = [string]$config.Ac3Rule.DeviceId
    $xml.PluginConfiguration.Ac3Rule.UserId = [string]$config.Ac3Rule.UserId
    $loggingText = '{"Serilog":{"MinimumLevel":{"Override":{"Jellyfin.Plugin.AudioTranscodingControl":"Debug"}}}}'
    $state = [ordered]@{ PluginDirectory = $destination; PluginDirectoryCreated = $false; ConfigurationPath = $configurationPath; ConfigurationCreated = $false; ConfigDirectoryCreated = $false; LoggingPath = $loggingPath; LoggingCreated = $false; PackageSHA256 = $packageHash; InstalledDllSHA256 = $manifest.DllSHA256; InstalledAtUtc = [DateTime]::UtcNow.ToString('O') }
    # Record ownership before activation so a partial installation can be rolled back.
    Write-NewText $statePath ($state | ConvertTo-Json)
    [IO.Directory]::Move($staging, $destination)
    $state.PluginDirectoryCreated = $true
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
    if (-not (Test-Path -LiteralPath $configDirectory)) { New-Item -ItemType Directory -Path $configDirectory | Out-Null; $state.ConfigDirectoryCreated = $true }
    Write-NewText $configurationPath $xml.OuterXml
    $state.ConfigurationCreated = $true
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
    if ($config.EnableDebugLogging) { Write-NewText $loggingPath $loggingText; $state.LoggingCreated = $true }
    $state | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
    Write-Output 'Own plugin and exact local rule installed. Server was not started or restarted.'
    return
}

if (-not (Test-Path -LiteralPath $statePath)) { throw 'No ownership state from this installer; refusing to modify existing data.' }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.PluginDirectory -ne $destination -or $state.ConfigurationPath -ne $configurationPath -or $state.LoggingPath -ne $loggingPath) { throw 'Installation state does not match this plan.' }
if ($Action -eq 'Disable') {
    if (-not $state.ConfigurationCreated -or -not (Test-Path -LiteralPath $configurationPath)) { throw 'Owned configuration is missing or unverified.' }
    if (-not $PSCmdlet.ShouldProcess($configurationPath, 'Disable only this plugin rule and its diagnostic switch')) { return }
    [xml]$xml = Get-Content -LiteralPath $configurationPath -Raw
    $xml.PluginConfiguration.IsEnabled = 'false'
    $xml.PluginConfiguration.Ac3Rule.IsEnabled = 'false'
    $xml.PluginConfiguration.EnableDebugLogging = 'false'
    $xml.Save($configurationPath)
    Write-Output 'Rule disabled in its own configuration. Use the tray to start the already stopped server.'
    return
}

if ($Action -eq 'Uninstall') {
    if ((Test-Path -LiteralPath $destination) -and -not $state.PluginDirectoryCreated) { throw 'Incomplete ownership record; inspect the partial installation before rollback.' }
    # Refuse recursive removal if any unrecognized content is present.
    if (Test-Path -LiteralPath $destination) {
        $files = @(Get-ChildItem -LiteralPath $destination -Force -Recurse)
        if (@($files | Where-Object { $_.PSIsContainer -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $_.Name -notin @(($pluginName + '.dll'), 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'package-manifest.json', 'meta.json') }).Count) { throw 'Unknown plugin content present; refusing recursive deletion.' }
    }
    if ($state.LoggingCreated -and (Test-Path -LiteralPath $loggingPath)) {
        $current = Get-Content -LiteralPath $loggingPath -Raw | ConvertFrom-Json
        $canonical = $current | ConvertTo-Json -Depth 10 -Compress
        if ($canonical -ne '{"Serilog":{"MinimumLevel":{"Override":{"Jellyfin.Plugin.AudioTranscodingControl":"Debug"}}}}') { throw 'Logging file was modified; it will not be removed.' }
    }
    if (-not $PSCmdlet.ShouldProcess($destination, 'Remove only the owned plugin directory, own XML configuration and unchanged owned logging override')) { return }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse }
    if ($state.ConfigurationCreated -and (Test-Path -LiteralPath $configurationPath)) { Remove-Item -LiteralPath $configurationPath }
    if ($state.LoggingCreated -and (Test-Path -LiteralPath $loggingPath)) { Remove-Item -LiteralPath $loggingPath }
    if ($state.ConfigDirectoryCreated -and (Test-Path -LiteralPath $configDirectory) -and @(Get-ChildItem -LiteralPath $configDirectory -Force).Count -eq 0) { Remove-Item -LiteralPath $configDirectory }
    $archiveState = $statePath + '.removed-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss')
    Move-Item -LiteralPath $statePath -Destination $archiveState
    Write-Output 'Owned plugin files removed; other plugins and existing server settings were left unchanged. Server remains stopped.'
}
