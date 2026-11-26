// SPDX-License-Identifier: GPL-3.0-only
// Adapted from jellyfin/jellyfin-plugin-template; see THIRD-PARTY-NOTICES.md.
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AudioTranscodingControl;

/// <summary>The plugin entry point. M0 does not change playback decisions.</summary>
public sealed class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>Initializes the plugin using services supplied by Jellyfin.</summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="xmlSerializer">Jellyfin configuration serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Audio Transcoding Control";

    /// <inheritdoc />
    public override Guid Id => new("c48a5098-04e0-4de5-a46c-8adae788f91b");

    /// <inheritdoc />
    public override string Description => "Foundation for device-specific audio transcoding policies. Inactive in M0.";
}
