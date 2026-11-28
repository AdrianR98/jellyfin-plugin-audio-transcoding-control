// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.WebUtilities;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal sealed record PreparedPolicy(Guid User, string Device, Guid Item, string Source, int AudioIndex, string Fingerprint)
{
    internal string? OfferedDevice { get; init; }
    internal string OfferedDeviceState { get; init; } = "unavailable";
    internal string? CredentialKey { get; init; }
    internal string? CredentialDevice { get; init; }
    internal bool Ambiguous { get; init; }

    // This is the native response's transport field, never a replacement authentication identity.
    // Parse once with ASP.NET query semantics, including '+' and percent escapes. Do not normalize opaque IDs.
    internal PreparedPolicy WithNativeOffer(string? url)
    {
        if (url is null || url.Length > 32768 || !url.StartsWith("/", StringComparison.Ordinal)
            || url.StartsWith("//", StringComparison.Ordinal) || url.Contains('#')) return this;
        var start = url.IndexOf('?');
        var query = QueryHelpers.ParseQuery(start < 0 ? "" : url[start..]);
        if (!query.TryGetValue("DeviceId", out var values)) return this with { OfferedDeviceState = "missing" };
        if (values.Count != 1) return this with { OfferedDeviceState = "ambiguous" };
        var device = values[0];
        return string.IsNullOrWhiteSpace(device) || device.Length > 512 || device.Any(char.IsControl)
            ? this with { OfferedDeviceState = "invalid" }
            : this with { OfferedDevice = device, OfferedDeviceState = "single" };
    }
}

/// <summary>Bounded, transient proof of a successfully validated PlaybackInfo policy, never an authorization substitute.</summary>
public sealed class PlaybackPolicySessions : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 256 });
    private readonly object _gate = new();
    internal static string Fingerprint(TranscodingRule rule) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rule)));
    internal static string SessionReference(string? session) => Guid.TryParse(session, out var id) && id != Guid.Empty
        ? Diagnostics.PluginDiagnostics.Reference(id.ToString("N")) : "missing-or-invalid";
    internal bool Remember(string session, PreparedPolicy policy)
    {
        if (!Guid.TryParse(session, out var id) || id == Guid.Empty || policy.CredentialKey?.Length != 64 || string.IsNullOrEmpty(policy.CredentialDevice)) return false;
        var key = (policy.User, policy.CredentialKey, id);
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out PreparedPolicy? previous) && previous is not null
                && (previous.Ambiguous || previous.Device != policy.Device || previous.Item != policy.Item || previous.Source != policy.Source
                    || previous.CredentialDevice != policy.CredentialDevice)) policy = policy with { Ambiguous = true };
            _cache.Set(key, policy, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12) });
            return !policy.Ambiguous && _cache.TryGetValue(key, out PreparedPolicy? stored) && stored == policy;
        }
    }
    internal PreparedPolicy? Find(Guid user, string credentialKey, string? session) => Guid.TryParse(session, out var id)
        && _cache.TryGetValue((user, credentialKey, id), out PreparedPolicy? policy) ? policy : null;
    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();
}
