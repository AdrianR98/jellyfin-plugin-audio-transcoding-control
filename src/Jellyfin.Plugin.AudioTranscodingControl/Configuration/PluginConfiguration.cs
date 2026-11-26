// SPDX-License-Identifier: GPL-3.0-only
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

/// <summary>Persisted settings. Rule configuration will be introduced in M1.</summary>
public sealed class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Gets or sets whether future policy processing is enabled. Has no effect in M0.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets whether future sanitized diagnostics are enabled. Has no effect in M0.</summary>
    public bool EnableDebugLogging { get; set; }
}
