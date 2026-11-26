// SPDX-License-Identifier: GPL-3.0-only
using System.Security.Claims;
using System.Text.Json;
using System.Xml.Serialization;
using Jellyfin.Api.Constants;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

public sealed class MultiRuleTests
{
    private static readonly Guid User = new("ef6ce028-64e1-427b-b897-5bf5908992dd");
    private static ClaimsPrincipal Principal(string device = "test-device-b", Guid? user = null) => new(new ClaimsIdentity([
        new(InternalClaimTypes.DeviceId, device), new(InternalClaimTypes.UserId, (user ?? User).ToString())], "synthetic"));
    private static TranscodingRule Rule() => new() { IsEnabled = true, DeviceIds = ["test-device-a", "test-device-b"], UserIds = [Guid.NewGuid(), User] };
    private static PluginConfiguration Config(params TranscodingRule[] rules) => new() { SchemaVersion = 2, IsEnabled = true, Rules = rules };

    [Fact]
    public void SelectionUsesOrWithinAxesAndAndBetweenAxesAndFirstEnabledMatch()
    {
        var first = Rule(); var second = Rule(); var config = Config(first, second);
        Assert.Same(first, RuleEvaluator.Select(Principal(), config));
        Assert.Null(RuleEvaluator.Select(Principal(user: Guid.NewGuid()), config));
        Assert.Null(RuleEvaluator.Select(Principal("different-device"), config));
        first.IsEnabled = false;
        Assert.Same(second, RuleEvaluator.Select(Principal(), config));
        config.Rules = [second, first]; first.IsEnabled = true;
        Assert.Same(second, RuleEvaluator.Select(Principal(), config));
        config.IsEnabled = false;
        Assert.Null(RuleEvaluator.Select(Principal(), config));
    }

    [Fact]
    public void AllRequiresExplicitFlagsAndNeverAcceptsInvalidOrAmbiguousIdentity()
    {
        var rule = Rule(); rule.DeviceIds = []; rule.UserIds = [];
        var config = Config(rule);
        Assert.Null(RuleEvaluator.Select(Principal(), config));
        rule.AllDevices = true;
        Assert.Null(RuleEvaluator.Select(Principal(), config));
        rule.AllUsers = true;
        Assert.Same(rule, RuleEvaluator.Select(Principal(), config));
        Assert.Null(RuleEvaluator.Select(Principal(user: Guid.Empty), config));
        var ambiguous = Principal(); ((ClaimsIdentity)ambiguous.Identity!).AddClaim(new(InternalClaimTypes.DeviceId, "second"));
        Assert.Null(RuleEvaluator.Select(ambiguous, config));
        Assert.Null(RuleEvaluator.Select(new(new ClaimsIdentity()), config));
        config.Rules = [rule, rule];
        Assert.Null(RuleEvaluator.Select(Principal(), config));
    }

    [Fact]
    public void LegacyMigrationAndJsonXmlRoundTripsPreserveSelectionAndActivation()
    {
        var legacy = new PluginConfiguration { IsEnabled = true, EnableDebugLogging = true,
            Ac3Rule = new() { IsEnabled = true, DeviceId = "synthetic-legacy-device", UserId = User } };
        var migrated = ConfigurationMigration.Normalize(legacy, out var changed);
        Assert.True(changed); Assert.True(migrated.IsEnabled); Assert.True(Assert.Single(migrated.Rules).IsEnabled);
        Assert.Equal(legacy.Ac3Rule.DeviceId, migrated.Rules[0].DeviceIds[0]);
        Assert.Equal(User, migrated.Rules[0].UserIds[0]);
        Assert.False(migrated.Rules[0].AllDevices); Assert.False(migrated.Rules[0].AllUsers);
        Assert.Equal("Debug", migrated.Diagnostics.Level);
        Assert.Empty(ConfigurationMigration.Errors(migrated));
        var serializer = new XmlSerializer(typeof(PluginConfiguration));
        using var writer = new StringWriter(); serializer.Serialize(writer, migrated);
        Assert.DoesNotContain("<Ac3Rule>", writer.ToString());
        using var reader = new StringReader(writer.ToString());
        var restored = (PluginConfiguration)serializer.Deserialize(reader)!;
        var json = JsonSerializer.Serialize(restored);
        var again = ConfigurationMigration.Normalize(JsonSerializer.Deserialize<PluginConfiguration>(json)!, out var repeated);
        Assert.False(repeated); Assert.Equal(migrated.Rules[0].Id, Assert.Single(again.Rules).Id);
        Assert.Equal(migrated.Rules[0].DeviceIds, again.Rules[0].DeviceIds);
        Assert.True(again.IsEnabled); Assert.True(again.Rules[0].IsEnabled);
        Assert.False(ConfigurationMigration.Normalize(new()).IsEnabled);
        Assert.Empty(ConfigurationMigration.Normalize(new()).Rules);
        Assert.False(ConfigurationMigration.Normalize(new() { SchemaVersion = 99, IsEnabled = true }).IsEnabled);
    }

    [Fact]
    public void RuleLimitsAreAppliedToCopyAndInvalidPoliciesAreRejected()
    {
        var source = HostFixture.Profile(); var before = JsonSerializer.Serialize(source);
        var rule = Rule(); rule.MaxAudioChannels = 2; rule.MaxAudioBitrate = 192000;
        var transformed = Ac3ProfileTransformer.Transform(source, rule)!;
        Assert.NotNull(transformed); Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.All(transformed.TranscodingProfiles.Where(p => p.Type == MediaBrowser.Model.Dlna.DlnaProfileType.Video), p => { Assert.Equal("ac3", p.AudioCodec); Assert.Equal("2", p.MaxAudioChannels); });
        Assert.Contains(transformed.CodecProfiles.SelectMany(p => p.Conditions), c => c.Property == MediaBrowser.Model.Dlna.ProfileConditionValue.AudioBitrate && c.Value == "192000");
        var forced = Ac3ProfileTransformer.Transform(source, rule, true)!;
        Assert.DoesNotContain(forced.DirectPlayProfiles, p => p.Type == MediaBrowser.Model.Dlna.DlnaProfileType.Video);
        rule.MaxAudioBitrate = 640001;
        Assert.Null(Ac3ProfileTransformer.Transform(source, rule));
    }

    [Fact]
    public void DebugTraceReachInfoOnlyNativeLoggerWithoutLeakingExceptionsOrIdentities()
    {
        var logger = new InfoLogger(); var log = new PluginDiagnostics(logger, new() { Level = "Trace" }, "synthetic-correlation");
        log.Write(LogLevel.Debug, 2400, "debug-check"); log.Write(LogLevel.Trace, 2401, "trace-check");
        try { throw new InvalidOperationException("sentinel-token C:\\private\\media.mkv https://host/?api_key=sentinel-token"); }
        catch (Exception error) { log.Failure(2900, "safe-failure", error); }
        Assert.Contains(logger.Messages, m => m.Contains("Debug event=2400"));
        Assert.Contains(logger.Messages, m => m.Contains("Trace event=2401"));
        Assert.Contains(logger.Messages, m => m.Contains(nameof(InvalidOperationException)) && m.Contains("safeStack="));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("sentinel-token") || m.Contains("media.mkv") || m.Contains("https://"));
        Assert.Equal(PluginDiagnostics.Reference("synthetic-device"), PluginDiagnostics.Reference("synthetic-device"));
        Assert.NotEqual("synthetic-device", PluginDiagnostics.Reference("synthetic-device"));
        Assert.StartsWith("redacted-", PluginDiagnostics.SafeLabel("C:\\private\\media.mkv"));
    }

    private sealed class InfoLogger : ILogger
    {
        internal List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) Messages.Add(formatter(state, exception)); }
    }
}
