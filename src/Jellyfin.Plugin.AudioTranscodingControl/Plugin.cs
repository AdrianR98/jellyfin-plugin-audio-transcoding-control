// SPDX-License-Identifier: GPL-3.0-only
// Adapted from jellyfin/jellyfin-plugin-template; see THIRD-PARTY-NOTICES.md.
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;

namespace Jellyfin.Plugin.AudioTranscodingControl;

/// <summary>The opt-in AC3 prototype plugin entry point.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;
    private readonly object _configurationGate = new();
    /// <summary>Initializes the plugin using services supplied by Jellyfin.</summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin configuration serializer.</param>
    /// <param name="logger">The native Jellyfin logger.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin>? logger = null)
        : base(applicationPaths, xmlSerializer)
    {
        _logger = logger ?? NullLogger<Plugin>.Instance;
        Instance = this;
        var snapshot = GetConfigurationSnapshot();
        new PluginDiagnostics(_logger, snapshot.Diagnostics, "configuration").Write(LogLevel.Information, 1000,
            "Plugin initialized; version={Version}; schema={Schema}; enabled={Enabled}; rules={Rules}; enabledRules={EnabledRules}",
            Version?.ToString(), snapshot.SchemaVersion, snapshot.IsEnabled, snapshot.Rules.Length, snapshot.Rules.Count(r => r?.IsEnabled == true));
    }

    internal static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Jellyfin Audio Transcoding Control";

    /// <inheritdoc />
    public override Guid Id => new("c48a5098-04e0-4de5-a46c-8adae788f91b");

    /// <inheritdoc />
    public override string Description => "Experimental ordered AC3 policies with native dashboard configuration and sanitized diagnostics.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() => [new() { Name = "audiotranscodingcontrol", EmbeddedResourcePath = "Jellyfin.Plugin.AudioTranscodingControl.Configuration.configPage.html" }];

    internal PluginConfiguration GetConfigurationSnapshot()
    {
        lock (_configurationGate)
        {
            var snapshot = ConfigurationMigration.Normalize(Configuration, out var migrated);
            if (migrated)
            {
                base.SaveConfiguration(snapshot);
                Configuration = snapshot;
                new PluginDiagnostics(_logger, snapshot.Diagnostics, "configuration").Write(LogLevel.Information, 1003,
                    "Legacy configuration migrated; schema={Schema}; enabled={Enabled}; rules={Rules}; selections-preserved=true", snapshot.SchemaVersion, snapshot.IsEnabled, snapshot.Rules.Length);
            }
            return snapshot;
        }
    }

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is not PluginConfiguration incoming) throw new ArgumentException("Unsupported configuration type.", nameof(configuration));
        if (incoming.SchemaVersion > ConfigurationMigration.CurrentSchema) throw new ArgumentException("Unsupported configuration schema.", nameof(configuration));
        var normalized = ConfigurationMigration.Normalize(incoming);
        var errors = ConfigurationMigration.Errors(normalized);
        var diagnostics = new PluginDiagnostics(_logger, GetConfigurationSnapshot().Diagnostics, "configuration");
        if (errors.Length != 0)
        {
            diagnostics.Write(LogLevel.Warning, 1090, "Configuration rejected; codes={Codes}", string.Join(',', errors));
            throw new ArgumentException("Configuration invalid: " + string.Join(", ", errors), nameof(configuration));
        }
        lock (_configurationGate) base.UpdateConfiguration(normalized);
        new PluginDiagnostics(_logger, normalized.Diagnostics, "configuration").Write(LogLevel.Information, 1002,
            "Configuration saved; schema={Schema}; enabled={Enabled}; rules={Rules}; enabledRules={EnabledRules}; diagnostics={DiagnosticLevelSetting}; effective-next-request=true",
            normalized.SchemaVersion, normalized.IsEnabled, normalized.Rules.Length, normalized.Rules.Count(r => r.IsEnabled), normalized.Diagnostics.Level);
    }
}
