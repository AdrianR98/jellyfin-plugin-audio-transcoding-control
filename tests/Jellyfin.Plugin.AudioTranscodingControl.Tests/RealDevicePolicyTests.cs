// SPDX-License-Identifier: GPL-3.0-only
using System.Reflection;
using System.Text.Json;
using Jellyfin.Api.Controllers;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

public sealed class RealDevicePolicyTests
{
    [Fact]
    public async Task NativeHttpMkvOfferUsesAc3WithoutChangingOriginalProfile()
    {
        var fixture = new HostFixture();
        using var server = PlaybackInfoIntegrationTests.CreateServer(fixture);
        var request = PlaybackInfoIntegrationTests.Request(fixture);
        request.DeviceProfile!.TranscodingProfiles = [new() { Type = MediaBrowser.Model.Dlna.DlnaProfileType.Video,
            Context = MediaBrowser.Model.Dlna.EncodingContext.Streaming, Protocol = Jellyfin.Data.Enums.MediaStreamProtocol.http,
            Container = "mkv", AudioCodec = "aac,ac3", VideoCodec = "hevc,h264" }];
        var original = JsonSerializer.Serialize(request.DeviceProfile);
        var response = await PlaybackInfoIntegrationTests.Playback(server, fixture, request);
        Assert.Contains("stream.mkv", response.MediaSources[0].TranscodingUrl);
        Assert.Contains("AudioCodec=ac3", response.MediaSources[0].TranscodingUrl);
        Assert.Equal(original, JsonSerializer.Serialize(request.DeviceProfile));
    }

    [Fact]
    public void SourceResolutionDistinguishesAmbiguityFromVideoPermissionAndAcceptsSevenChannels()
    {
        var fixture = new HostFixture("dts", 7);
        fixture.Source.SupportsTranscoding = false; fixture.Source.SupportsDirectStream = false;
        Assert.Same(fixture.Source, SourceSelection.Resolve([fixture.Source], null, out _));
        Assert.Same(fixture.Source, SourceSelection.Resolve([fixture.Source], fixture.Source.Id.ToUpperInvariant(), out _));
        Assert.Null(SourceSelection.Resolve([fixture.Source, fixture.Source], null, out var ambiguous));
        Assert.Equal("source-id-missing-multiple-sources", ambiguous);
        Assert.Null(SourceSelection.Resolve([fixture.Source], "not-present", out var missing)); Assert.Equal("source-id-not-found", missing);
        Assert.NotNull(SourceSelection.Audio(fixture.Source, null, out _));
        fixture.Source.RequiresOpening = true;
        Assert.Null(SourceSelection.Resolve([fixture.Source], null, out var opening)); Assert.Equal("source-requires-opening", opening);
    }

    [Theory]
    [InlineData("truehd", 8)]
    [InlineData("dts", 7)]
    public async Task NativeAudioOnlyPermissionNegotiatesAndScopesMkvAudioChange(string codec, int channels)
    {
        var fixture = new HostFixture(codec, channels);
        fixture.User.SetPermission(PermissionKind.EnableVideoPlaybackTranscoding, false);
        fixture.User.SetPermission(PermissionKind.EnablePlaybackRemuxing, false);
        fixture.Source.SupportsTranscoding = false; fixture.Source.SupportsDirectStream = false;
        var rights = JsonSerializer.Serialize(fixture.User.Permissions);
        using var server = PlaybackInfoIntegrationTests.CreateServer(fixture);
        var request = PlaybackInfoIntegrationTests.Request(fixture); request.MediaSourceId = null;
        var response = await PlaybackInfoIntegrationTests.Playback(server, fixture, request);
        var source = Assert.Single(response.MediaSources);
        // Native StreamBuilder cannot select a profile when both video-source flags are false; a scoped hook is needed.
        Assert.Equal("http", source.TranscodingSubProtocol.ToString());
        Assert.Contains("/stream?", source.TranscodingUrl);
        using var scope = server.Services.CreateScope();
        var args = new Dictionary<string, object?> { ["itemId"] = fixture.Video.Id, ["container"] = "mkv", ["mediaSourceId"] = fixture.Source.Id,
            ["playSessionId"] = response.PlaySessionId, ["audioStreamIndex"] = 1, ["audioCodec"] = "aac", ["audioBitRate"] = 768000,
            ["audioChannels"] = 6, ["videoCodec"] = "hevc", ["allowVideoStreamCopy"] = true };
        await Run(scope.ServiceProvider, fixture, args);
        Assert.Equal("ac3", args["audioCodec"]); Assert.Equal(640000, args["audioBitRate"]); Assert.Equal(6, args["audioChannels"]);
        Assert.Equal("hevc", args["videoCodec"]); Assert.Equal(true, args["allowVideoStreamCopy"]);
        Assert.Equal(rights, JsonSerializer.Serialize(fixture.User.Permissions));
        args["audioCodec"] = "aac"; args["playSessionId"] = Guid.NewGuid().ToString("N");
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        args["playSessionId"] = response.PlaySessionId; args["static"] = true;
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        args["static"] = false; args["audioStreamIndex"] = 2;
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
    }

    private static async Task Run(IServiceProvider services, HostFixture fixture, Dictionary<string, object?> args)
    {
        var http = new DefaultHttpContext { User = fixture.Principal() }; http.Request.Method = "GET";
        var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = typeof(VideosController).GetTypeInfo(),
            MethodInfo = typeof(VideosController).GetMethod(nameof(VideosController.GetVideoStreamByContainer))! };
        var action = new ActionContext(http, new RouteData(), descriptor);
        var context = new ActionExecutingContext(action, [], args, new object()); var calls = 0;
        await services.GetRequiredService<ProgressiveAudioActionFilter>().OnActionExecutionAsync(context, () =>
        { calls++; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
        Assert.Equal(1, calls);
    }
}
