// SPDX-License-Identifier: GPL-3.0-only
namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

/// <summary>A single AC3 policy scoped to an exact authenticated device/user pair.</summary>
public sealed class Ac3DeviceRule
{
    /// <summary>Gets or sets whether this rule is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets the opaque, case-sensitive device identifier.</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Gets or sets the user identifier. Empty identifiers never match.</summary>
    public Guid UserId { get; set; }
}
