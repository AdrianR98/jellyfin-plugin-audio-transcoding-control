// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Cryptography;
using System.Text.Json;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal sealed record PreparedPolicy(Guid User, string Device, Guid Item, string Source, int AudioIndex, string Fingerprint);

/// <summary>Bounded, transient proof of a successfully validated PlaybackInfo policy, never an authorization substitute.</summary>
public sealed class PlaybackPolicySessions : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 256 });
    internal static string Fingerprint(TranscodingRule rule) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rule)));
    internal bool Remember(string session, PreparedPolicy policy)
    {
        if (!Guid.TryParse(session, out var id) || id == Guid.Empty) return false;
        var key = (policy.User, policy.Device, id);
        _cache.Set(key, policy,
            new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12) });
        return _cache.TryGetValue(key, out PreparedPolicy? stored) && stored == policy;
    }
    internal PreparedPolicy? Find(Guid user, string device, string? session) => Guid.TryParse(session, out var id)
        && _cache.TryGetValue((user, device, id), out PreparedPolicy? policy) ? policy : null;
    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();
}
