// SPDX-License-Identifier: GPL-3.0-only
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

/// <summary>Persisted prototype settings; all processing is opt-in.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets whether policy processing is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets whether sanitized decision diagnostics are enabled.</summary>
    public bool EnableDebugLogging { get; set; }

    /// <summary>Gets or sets the single explicitly scoped prototype rule.</summary>
    public Ac3DeviceRule Ac3Rule { get; set; } = new();
}
