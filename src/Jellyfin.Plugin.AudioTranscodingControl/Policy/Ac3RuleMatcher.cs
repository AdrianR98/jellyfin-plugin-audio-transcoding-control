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
        return RuleEvaluator.Select(principal, ConfigurationMigration.Normalize(config)) is not null
            && RuleEvaluator.Identity(principal, out deviceId, out userId);
    }
}
