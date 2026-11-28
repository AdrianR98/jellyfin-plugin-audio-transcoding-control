// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Claims;
using Jellyfin.Api.Constants;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal sealed record RuleDecision(int Position, TranscodingRule Rule, bool DeviceMatch, bool UserMatch, string Reason);

internal static class RuleEvaluator
{
    internal static bool Identity(ClaimsPrincipal principal, out string deviceId, out Guid userId)
    {
        deviceId = string.Empty; userId = Guid.Empty;
        var devices = principal.FindAll(InternalClaimTypes.DeviceId).ToArray();
        var users = principal.FindAll(InternalClaimTypes.UserId).ToArray();
        if (principal.Identity?.IsAuthenticated != true || devices.Length != 1 || users.Length != 1
            || string.IsNullOrWhiteSpace(devices[0].Value) || devices[0].Value.Length > 512 || devices[0].Value.Any(char.IsControl)
            || !Guid.TryParse(users[0].Value, out userId) || userId == Guid.Empty) return false;
        deviceId = devices[0].Value;
        return true;
    }

    internal static TranscodingRule? Select(ClaimsPrincipal principal, PluginConfiguration config, Action<RuleDecision>? observe = null)
    {
        return Identity(principal, out var device, out var user) ? SelectVerified(user, device, config, observe) : null;
    }

    // Call only after verifying the native credential association to a retained negotiation device.
    internal static TranscodingRule? SelectVerified(Guid user, string device, PluginConfiguration config, Action<RuleDecision>? observe = null)
    {
        if (!config.IsEnabled || config.SchemaVersion != ConfigurationMigration.CurrentSchema || user == Guid.Empty || string.IsNullOrWhiteSpace(device)) return null;
        if (config.Rules is null || config.Rules.Length > 64) return null;
        var duplicates = config.Rules.Where(r => r is not null).GroupBy(r => r.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        for (var index = 0; index < config.Rules.Length; index++)
        {
            var rule = config.Rules[index];
            if (rule is null) continue;
            var error = ConfigurationMigration.RuleError(rule);
            var deviceMatch = error is null && (rule.AllDevices || rule.DeviceIds.Contains(device, StringComparer.Ordinal));
            var userMatch = error is null && (rule.AllUsers || rule.UserIds.Contains(user));
            var reason = !rule.IsEnabled ? "disabled" : error ?? (duplicates.Contains(rule.Id) ? "duplicate-rule-id" : !deviceMatch ? "device-mismatch" : !userMatch ? "user-mismatch" : "matched");
            observe?.Invoke(new(index, rule, deviceMatch, userMatch, reason));
            if (reason == "matched") return rule;
        }
        return null;
    }
}
