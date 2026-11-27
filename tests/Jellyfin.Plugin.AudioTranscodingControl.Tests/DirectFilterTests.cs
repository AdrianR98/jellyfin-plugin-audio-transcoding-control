// SPDX-License-Identifier: GPL-3.0-only
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.MediaInfoDtos;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

public sealed class DirectFilterTests
{
    [Fact]
    public void ExactPairMatchingIsDeterministicAndRejectsAmbiguityOrOtherUsers()
    {
        var fixture = new HostFixture();
        var config = fixture.Configuration();
        Assert.True(Ac3RuleMatcher.TryMatch(fixture.Principal(), config, out var firstDevice, out var firstUser));
        Assert.True(Ac3RuleMatcher.TryMatch(fixture.Principal(), config, out var secondDevice, out var secondUser));
        Assert.Equal((firstDevice, firstUser), (secondDevice, secondUser));
        fixture.ClaimDevice = HostFixture.DeviceId.ToUpperInvariant();
        Assert.False(Ac3RuleMatcher.TryMatch(fixture.Principal(), config, out _, out _));
        fixture.ClaimDevice = HostFixture.DeviceId; fixture.DuplicateDeviceClaim = true;
        Assert.False(Ac3RuleMatcher.TryMatch(fixture.Principal(), config, out _, out _));
        var otherUser = new ClaimsPrincipal(new ClaimsIdentity([new(InternalClaimTypes.DeviceId, HostFixture.DeviceId), new(InternalClaimTypes.UserId, Guid.NewGuid().ToString())], "test"));
        Assert.False(Ac3RuleMatcher.TryMatch(otherUser, config, out _, out _));
        Assert.False(Ac3RuleMatcher.TryMatch(new ClaimsPrincipal(new ClaimsIdentity()), config, out _, out _));
    }

    [Fact]
    public async Task RealBoundDtoIsReplacedOnlyAtProfileAndUserRightsAreUnchanged()
    {
        var fixture = new HostFixture();
        var request = PlaybackInfoIntegrationTests.Request(fixture);
        var original = request.DeviceProfile;
        var originalJson = JsonSerializer.Serialize(original);
        var rights = JsonSerializer.Serialize(fixture.User.Permissions);
        var logger = new CaptureLogger();
        await Run(fixture, request, logger);
        Assert.NotSame(original, request.DeviceProfile);
        Assert.Equal(originalJson, JsonSerializer.Serialize(original));
        Assert.Equal(rights, JsonSerializer.Serialize(fixture.User.Permissions));
        Assert.True(request.EnableTranscoding);
        Assert.True(request.AllowVideoStreamCopy);
        Assert.Equal(1, request.AudioStreamIndex);
        Assert.Contains(logger.Messages, m => m.Contains("ATC applied", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(HostFixture.DeviceId, StringComparison.Ordinal) || m.Contains("sentinel-token", StringComparison.Ordinal) || m.Contains("synthetic.mkv", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("enableTranscoding", false)]
    [InlineData("maxAudioChannels", 2)]
    public async Task BoundQueryRestrictionsTakePrecedence(string key, object value)
    {
        var fixture = new HostFixture();
        var request = PlaybackInfoIntegrationTests.Request(fixture);
        var original = request.DeviceProfile;
        await Run(fixture, request, new(), new Dictionary<string, object?> { [key] = value });
        Assert.Same(original, request.DeviceProfile);
    }

    [Fact]
    public async Task UnsupportedDtoRemainsUntouchedAndSingleSourceCanBeImplicit()
    {
        var fixture = new HostFixture();
        var synthetic = new ProbeRequest { DeviceProfile = HostFixture.Profile() };
        await Run(fixture, synthetic, new());
        Assert.Equal("aac,ac3,eac3,mp3", synthetic.DeviceProfile.TranscodingProfiles[0].AudioCodec);
        var request = PlaybackInfoIntegrationTests.Request(fixture); request.MediaSourceId = null;
        var original = request.DeviceProfile;
        await Run(fixture, request, new());
        Assert.NotSame(original, request.DeviceProfile);
    }

    [Fact]
    public async Task InternalFailureDoesNotLeakDetailsOrRunActionTwice()
    {
        var fixture = new HostFixture { ThrowOnMetadata = true };
        await PreparationFailureRetainsOriginal(fixture, PlaybackInfoIntegrationTests.Request(fixture));
    }

    internal static async Task PreparationFailureRetainsOriginal(HostFixture fixture, PlaybackInfoDto request)
    {
        var original = request.DeviceProfile;
        var logger = new CaptureLogger();
        await Run(fixture, request, logger);
        Assert.Same(original, request.DeviceProfile);
        Assert.Contains(logger.Messages, m => m.Contains("unexpected-preparation-error", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, m => m.Contains("sentinel-token", StringComparison.Ordinal) || m.Contains("private-path", StringComparison.Ordinal));
    }

    private static async Task Run(HostFixture fixture, object request, CaptureLogger logger, Dictionary<string, object?>? overrides = null)
    {
        var services = new ServiceCollection(); fixture.Register(services);
        new PluginServiceRegistrator().RegisterServices(services, null!);
        services.PostConfigure<PluginConfiguration>(options =>
        {
            var c = fixture.Configuration(); options.IsEnabled = c.IsEnabled; options.EnableDebugLogging = c.EnableDebugLogging; options.Ac3Rule = c.Ac3Rule;
        });
        services.AddSingleton<ILogger<PlaybackInfoActionFilter>>(logger);
        using var provider = services.BuildServiceProvider(); using var scope = provider.CreateScope();
        var http = new DefaultHttpContext { User = fixture.Principal() }; http.Request.Method = "POST";
        var descriptor = new ControllerActionDescriptor
        {
            ControllerTypeInfo = typeof(MediaInfoController).GetTypeInfo(),
            MethodInfo = typeof(MediaInfoController).GetMethod(nameof(MediaInfoController.GetPostedPlaybackInfo))!,
        };
        var arguments = new Dictionary<string, object?> { ["itemId"] = fixture.Video.Id, ["playbackInfoDto"] = request };
        if (overrides is not null) foreach (var pair in overrides) arguments[pair.Key] = pair.Value;
        var action = new ActionContext(http, new RouteData(), descriptor);
        var context = new ActionExecutingContext(action, [], arguments, new object());
        var calls = 0;
        await scope.ServiceProvider.GetRequiredService<PlaybackInfoActionFilter>().OnActionExecutionAsync(context, () =>
        {
            calls++;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        Assert.Equal(1, calls);
    }

    private sealed class CaptureLogger : ILogger<PlaybackInfoActionFilter>
    {
        internal List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
