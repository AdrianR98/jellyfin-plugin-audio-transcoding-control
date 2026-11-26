// SPDX-License-Identifier: GPL-3.0-only
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

/// <summary>Persisted prototype settings; all processing is opt-in.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets the persisted schema. Zero identifies an unmigrated legacy file.</summary>
    public int SchemaVersion { get; set; }

    /// <summary>Gets or sets whether policy processing is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets whether sanitized decision diagnostics are enabled.</summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>Gets or sets the single explicitly scoped prototype rule.</summary>
    public Ac3DeviceRule Ac3Rule { get; set; } = new();

    /// <summary>Gets or sets the ordered independent policies. First enabled match wins.</summary>
    public TranscodingRule[] Rules { get; set; } = [];

    /// <summary>Gets or sets native diagnostic options.</summary>
    public DiagnosticOptions Diagnostics { get; set; } = new();

    /// <summary>Gets or sets the dashboard language (en or de).</summary>
    public string UiLanguage { get; set; } = "en";

    /// <summary>Suppresses obsolete XML fields after migration without breaking legacy deserialization.</summary>
    public bool ShouldSerializeAc3Rule() => SchemaVersion < ConfigurationMigration.CurrentSchema;

    /// <summary>Suppresses the legacy diagnostic switch after migration.</summary>
    public bool ShouldSerializeEnableDebugLogging() => SchemaVersion < ConfigurationMigration.CurrentSchema;
}
