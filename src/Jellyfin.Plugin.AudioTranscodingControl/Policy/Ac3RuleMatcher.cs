// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Claims;
using Jellyfin.Api.Constants;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal static class Ac3RuleMatcher
{
    internal static bool TryMatch(ClaimsPrincipal principal, PluginConfiguration config, out string deviceId, out Guid userId)
    {
        deviceId = string.Empty;
        userId = Guid.Empty;
        var rule = config.Ac3Rule;
        if (!config.IsEnabled || rule is not { IsEnabled: true } || string.IsNullOrWhiteSpace(rule.DeviceId)
            || rule.UserId == Guid.Empty || principal.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var devices = principal.FindAll(InternalClaimTypes.DeviceId).ToArray();
        var users = principal.FindAll(InternalClaimTypes.UserId).ToArray();
        if (devices.Length != 1 || users.Length != 1 || string.IsNullOrWhiteSpace(devices[0].Value)
            || !Guid.TryParse(users[0].Value, out var authenticatedUser) || authenticatedUser == Guid.Empty
            || !string.Equals(rule.DeviceId, devices[0].Value, StringComparison.Ordinal) || rule.UserId != authenticatedUser)
        {
            return false;
        }

        deviceId = devices[0].Value;
        userId = authenticatedUser;
        return true;
    }
}
