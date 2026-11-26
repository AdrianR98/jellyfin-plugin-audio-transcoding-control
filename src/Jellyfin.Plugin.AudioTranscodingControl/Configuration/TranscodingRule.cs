// SPDX-License-Identifier: GPL-3.0-only
namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

/// <summary>One ordered policy; explicit all selections never follow from empty arrays.</summary>
public sealed class TranscodingRule
{
    /// <summary>Gets or sets the stable internal identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Gets or sets a user-editable display name.</summary>
    public string Name { get; set; } = "AC3 rule";
    /// <summary>Gets or sets whether this policy participates in matching.</summary>
    public bool IsEnabled { get; set; }
    /// <summary>Gets or sets exact opaque device identifiers.</summary>
    public string[] DeviceIds { get; set; } = [];
    /// <summary>Gets or sets whether any valid authenticated device may match.</summary>
    public bool AllDevices { get; set; }
    /// <summary>Gets or sets user identifiers.</summary>
    public Guid[] UserIds { get; set; } = [];
    /// <summary>Gets or sets whether any valid authenticated user may match.</summary>
    public bool AllUsers { get; set; }
    /// <summary>Gets or sets the output codec. Only ac3 is currently supported.</summary>
    public string TargetCodec { get; set; } = "ac3";
    /// <summary>Gets or sets a supported AC3 channel ceiling (1, 2 or 6).</summary>
    public int MaxAudioChannels { get; set; } = 6;
    /// <summary>Gets or sets an AC3 bitrate ceiling in bits per second.</summary>
    public int MaxAudioBitrate { get; set; } = 640000;
    /// <summary>Gets or sets whether compatible AC3 may remain direct/copy.</summary>
    public bool PreserveCompatibleAudio { get; set; } = true;
    /// <summary>Gets or sets whether mono/stereo sources bypass this policy.</summary>
    public bool PreserveStereo { get; set; } = true;
    /// <summary>Gets or sets whether selected-rule detail diagnostics are allowed.</summary>
    public bool EnableDiagnostics { get; set; } = true;
}

/// <summary>Plugin-side diagnostic gates, independent of global server configuration.</summary>
public sealed class DiagnosticOptions
{
    /// <summary>Gets or sets Error, Warning, Information, Debug or Trace.</summary>
    public string Level { get; set; } = "Information";
    /// <summary>Gets or sets detailed ordered match diagnostics.</summary>
    public bool DetailedRuleMatching { get; set; }
    /// <summary>Gets or sets sanitized before/after profile diagnostics.</summary>
    public bool DeviceProfileChanges { get; set; }
    /// <summary>Gets or sets read-only native response analysis.</summary>
    public bool PlaybackResponseAnalysis { get; set; }
    /// <summary>Gets or sets plugin-only performance measurements.</summary>
    public bool PerformanceMeasurements { get; set; }
}
