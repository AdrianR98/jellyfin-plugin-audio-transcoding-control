// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTranscodingControl.Configuration;

internal static class ConfigurationMigration
{
    internal const int CurrentSchema = 2;
    private static readonly Guid LegacyRuleId = new("a566031e-ce6c-49f7-b2d8-715d709b8d7b");
    internal static readonly int[] Bitrates = [32000, 40000, 48000, 56000, 64000, 80000, 96000, 112000, 128000, 160000, 192000, 224000, 256000, 320000, 384000, 448000, 512000, 576000, 640000];

    internal static PluginConfiguration Normalize(PluginConfiguration original) => Normalize(original, out _);

    internal static PluginConfiguration Normalize(PluginConfiguration original, out bool migrated)
    {
        var copy = JsonSerializer.Deserialize<PluginConfiguration>(JsonSerializer.SerializeToUtf8Bytes(original))!;
        migrated = copy.SchemaVersion < CurrentSchema;
        copy.Rules ??= [];
        copy.Diagnostics ??= new();
        if (copy.SchemaVersion > CurrentSchema)
        {
            copy.IsEnabled = false;
            return copy; // Caller must never persist this downgraded view over a future configuration.
        }

        if (migrated)
        {
            var legacy = copy.Ac3Rule;
            if (legacy is not null && (legacy.IsEnabled || !string.IsNullOrEmpty(legacy.DeviceId) || legacy.UserId != Guid.Empty))
            {
                copy.Rules = [.. copy.Rules, new()
                {
                    Id = LegacyRuleId, Name = "Migrated AC3 rule", IsEnabled = legacy.IsEnabled,
                    DeviceIds = string.IsNullOrEmpty(legacy.DeviceId) ? [] : [legacy.DeviceId],
                    UserIds = legacy.UserId == Guid.Empty ? [] : [legacy.UserId],
                    EnableDiagnostics = copy.EnableDebugLogging,
                }];
            }

            if (copy.EnableDebugLogging)
            {
                copy.Diagnostics = new() { Level = "Debug", DetailedRuleMatching = true, DeviceProfileChanges = true, PlaybackResponseAnalysis = true, PerformanceMeasurements = true };
            }
            copy.SchemaVersion = CurrentSchema;
            copy.EnableDebugLogging = false;
            copy.Ac3Rule = new();
        }
        return copy;
    }

    internal static void CopyInto(PluginConfiguration source, PluginConfiguration destination)
    {
        destination.SchemaVersion = source.SchemaVersion;
        destination.IsEnabled = source.IsEnabled;
        destination.Rules = source.Rules;
        destination.Diagnostics = source.Diagnostics;
        destination.UiLanguage = source.UiLanguage;
        destination.Ac3Rule = source.Ac3Rule;
        destination.EnableDebugLogging = source.EnableDebugLogging;
    }

    internal static string? RuleError(TranscodingRule? rule)
    {
        if (rule is null || rule.Id == Guid.Empty) return "missing-rule-id";
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 80) return "invalid-rule-name";
        if (rule.TargetCodec != "ac3") return "unsupported-target-codec";
        if (rule.MaxAudioChannels is not (1 or 2 or 6) || !Bitrates.Contains(rule.MaxAudioBitrate)) return "unsupported-ac3-limits";
        if (rule.DeviceIds is null || rule.UserIds is null || rule.DeviceIds.Length > 256 || rule.UserIds.Length > 256) return "invalid-selection-list";
        if (rule.DeviceIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 512 || id.Any(char.IsControl)) || rule.UserIds.Contains(Guid.Empty)) return "invalid-selection-identity";
        if (rule.IsEnabled && ((!rule.AllDevices && rule.DeviceIds.Length == 0) || (!rule.AllUsers && rule.UserIds.Length == 0))) return "empty-explicit-selection";
        return null;
    }

    internal static string[] Errors(PluginConfiguration config)
    {
        var errors = new List<string>();
        if (config.SchemaVersion != CurrentSchema) errors.Add("unsupported-schema");
        if (config.Rules is null || config.Rules.Length > 64) errors.Add("too-many-or-missing-rules");
        else
        {
            errors.AddRange(config.Rules.Select(RuleError).OfType<string>());
            if (config.Rules.Where(r => r is not null).GroupBy(r => r.Id).Any(g => g.Count() > 1)) errors.Add("duplicate-rule-id");
        }
        if (config.Diagnostics is null || !Enum.TryParse<LogLevel>(config.Diagnostics.Level, out var level) || level is < LogLevel.Trace or > LogLevel.Error) errors.Add("invalid-log-level");
        if (config.UiLanguage is not ("en" or "de")) errors.Add("invalid-ui-language");
        return errors.Distinct().ToArray();
    }
}
