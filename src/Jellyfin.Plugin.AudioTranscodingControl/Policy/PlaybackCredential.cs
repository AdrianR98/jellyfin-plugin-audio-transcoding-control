// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Api.Constants;
using Jellyfin.Data.Queries;
using MediaBrowser.Controller.Devices;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

// Only non-reversible process-local credential binding and a verified native registration are returned.
internal sealed record PlaybackCredential(string Key, string Device)
{
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(32);
    internal static PlaybackCredential? Resolve(ClaimsPrincipal principal, IDeviceManager devices, out string reason)
    {
        reason = "credential-identity-invalid";
        if (!RuleEvaluator.Identity(principal, out _, out var user)) return null;
        var tokens = principal.FindAll(InternalClaimTypes.Token).ToArray();
        var kinds = principal.FindAll(InternalClaimTypes.IsApiKey).ToArray();
        if (tokens.Length != 1 || string.IsNullOrWhiteSpace(tokens[0].Value) || tokens[0].Value.Length > 4096
            || kinds.Length != 1 || !bool.TryParse(kinds[0].Value, out var apiKey) || apiKey)
        { reason = "credential-user-token-required"; return null; }
        // Read the same native registry used by AuthorizationContext. Never parse credentials from query/header.
        var records = devices.GetDevices(new DeviceQuery { AccessToken = tokens[0].Value, Limit = 2 }).Items;
        if (records.Count != 1) { reason = "credential-record-missing-or-ambiguous"; return null; }
        var record = records[0];
        if (record.UserId != user || !string.Equals(record.AccessToken, tokens[0].Value, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(record.DeviceId) || record.DeviceId.Length > 512 || record.DeviceId.Any(char.IsControl))
        { reason = "credential-record-user-or-device-invalid"; return null; }
        reason = "native-user-token-record-verified";
        var input = Encoding.UTF8.GetBytes(user.ToString("N") + ":" + tokens[0].Value);
        try { return new(Convert.ToHexString(HMACSHA256.HashData(Salt, input)), record.DeviceId); }
        finally { CryptographicOperations.ZeroMemory(input); }
    }
}
