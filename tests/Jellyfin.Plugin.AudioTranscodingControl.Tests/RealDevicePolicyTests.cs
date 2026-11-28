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
using Microsoft.AspNetCore.WebUtilities;
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

    [Fact]
    public async Task NativeIssuedDeviceTransportValueIsBoundToAuthenticatedNegotiation()
    {
        // StreamInfo.ToUrl appends DeviceId verbatim; ASP.NET decodes '+' to space in its query.
        // This reproduces a native transport difference without treating either value as a second identity.
        var fixture = new HostFixture("truehd", 8) { ClaimDevice = "synthetic+opaque%2Bdevice" };
        fixture.User.SetPermission(PermissionKind.EnableVideoPlaybackTranscoding, false);
        fixture.User.SetPermission(PermissionKind.EnablePlaybackRemuxing, false);
        fixture.Source.SupportsTranscoding = false; fixture.Source.SupportsDirectStream = false;
        using var server = PlaybackInfoIntegrationTests.CreateServer(fixture, c => c.Ac3Rule.DeviceId = fixture.ClaimDevice);
        var response = await PlaybackInfoIntegrationTests.Playback(server, fixture, PlaybackInfoIntegrationTests.Request(fixture));
        var url = Assert.Single(response.MediaSources).TranscodingUrl!;
        var issued = QueryHelpers.ParseQuery(url[url.IndexOf('?')..])["DeviceId"].ToString();
        Assert.NotEqual(fixture.ClaimDevice, issued);
        using var scope = server.Services.CreateScope();
        var args = new Dictionary<string, object?> { ["itemId"] = fixture.Video.Id, ["container"] = "mkv", ["mediaSourceId"] = fixture.Source.Id,
            ["playSessionId"] = response.PlaySessionId, ["audioStreamIndex"] = 1, ["audioCodec"] = "aac", ["audioBitRate"] = 768000,
            ["audioChannels"] = 6, ["videoCodec"] = "hevc", ["allowVideoStreamCopy"] = true, ["deviceId"] = issued };
        var rights = JsonSerializer.Serialize(fixture.User.Permissions);
        await Run(scope.ServiceProvider, fixture, args);
        Assert.Equal("ac3", args["audioCodec"]); Assert.Equal(640000, args["audioBitRate"]); Assert.Equal(6, args["audioChannels"]);
        Assert.Equal("hevc", args["videoCodec"]); Assert.Equal(true, args["allowVideoStreamCopy"]);
        Assert.Equal(issued, args["deviceId"]); Assert.Equal(rights, JsonSerializer.Serialize(fixture.User.Permissions));

        foreach (var device in new[] { "unissued-device", issued.ToUpperInvariant(), System.Net.WebUtility.UrlDecode(issued) })
        {
            args["deviceId"] = device; args["audioCodec"] = "aac";
            await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        }
        args["deviceId"] = issued;
        await Run(scope.ServiceProvider, fixture, args, QueryString.Create("DeviceId", "different-query-value"));
        Assert.Equal("aac", args["audioCodec"]);
        await Run(scope.ServiceProvider, fixture, args, new QueryString("?DeviceId=a&deviceId=b"));
        Assert.Equal("aac", args["audioCodec"]);
        args["playSessionId"] = Guid.NewGuid().ToString("N");
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        args["playSessionId"] = response.PlaySessionId; fixture.DuplicateDeviceClaim = true;
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        fixture.DuplicateDeviceClaim = false; fixture.ClaimDevice = "";
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("aac", args["audioCodec"]);
        fixture.ClaimDevice = "synthetic+opaque%2Bdevice"; args["deviceId"] = fixture.ClaimDevice;
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("ac3", args["audioCodec"]);
        args["audioCodec"] = "aac"; args.Remove("deviceId");
        await Run(scope.ServiceProvider, fixture, args); Assert.Equal("ac3", args["audioCodec"]);
    }

    [Fact]
    public void NativeOfferParsingDoesNotInventOrNormalizeIdentities()
    {
        var proof = new PreparedPolicy(Guid.NewGuid(), "opaque+device", Guid.NewGuid(), "source", 1, "rule") { CredentialKey = new string('A', 64), CredentialDevice = "registered-device" };
        Assert.Equal("opaque device", proof.WithNativeOffer("/Videos/synthetic/stream?DeviceId=opaque+device").OfferedDevice);
        Assert.Equal("opaque+device", proof.WithNativeOffer("/Videos/synthetic/stream?DeviceId=opaque%2Bdevice").OfferedDevice);
        Assert.Equal("ambiguous", proof.WithNativeOffer("/stream?DeviceId=a&deviceId=b").OfferedDeviceState);
        Assert.Equal("invalid", proof.WithNativeOffer("/stream?DeviceId=%0A").OfferedDeviceState);
        Assert.Equal("missing", proof.WithNativeOffer("/stream?AudioCodec=aac").OfferedDeviceState);
        Assert.Equal("unavailable", proof.WithNativeOffer("https://synthetic.invalid/stream?DeviceId=other").OfferedDeviceState);
        var session = Guid.NewGuid();
        Assert.Equal(PlaybackPolicySessions.SessionReference(session.ToString("N")), PlaybackPolicySessions.SessionReference(session.ToString("D")));
        Assert.DoesNotContain(session.ToString("N"), PlaybackPolicySessions.SessionReference(session.ToString("N")));
        using var sessions = new PlaybackPolicySessions();
        Assert.True(sessions.Remember(session.ToString("N"), proof));
        Assert.Same(proof, sessions.Find(proof.User, proof.CredentialKey!, session.ToString("D")));
        Assert.Null(sessions.Find(Guid.NewGuid(), proof.CredentialKey!, session.ToString("N")));
        Assert.Null(sessions.Find(proof.User, "other-authenticated-device", session.ToString("N")));
        Assert.Null(sessions.Find(proof.User, proof.CredentialKey!, Guid.NewGuid().ToString("N")));
        Assert.False(sessions.Remember(session.ToString("N"), proof with { Device = "different-negotiating-device" }));
        Assert.True(sessions.Find(proof.User, proof.CredentialKey!, session.ToString("N"))!.Ambiguous);
        Assert.False(sessions.Remember(session.ToString("N"), proof)); // Conflicting contexts never become usable again.
    }

    [Fact]
    public async Task CredentialAssociationAllowsTokenRecordIdentityAndMutableTracksButRejectsUnrelatedCallers()
    {
        var fixture = new HostFixture("ac3", 6) { TokenDevice = "registered-at-login", ClaimDevice = "negotiating-device" };
        fixture.User.SetPermission(PermissionKind.EnableVideoPlaybackTranscoding, false);
        fixture.User.SetPermission(PermissionKind.EnablePlaybackRemuxing, false);
        fixture.Source.SupportsTranscoding = false; fixture.Source.SupportsDirectStream = false;
        fixture.Source.MediaStreams = [..fixture.Source.MediaStreams,
            new() { Index = 2, Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "truehd", Channels = 8, BitRate = 4000000 },
            new() { Index = 3, Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "dts", Channels = 7, Profile = "DTS-HD MA", BitRate = 2000000 },
            new() { Index = 4, Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "dts", Channels = 7, Profile = "DTS-ES", BitRate = 1500000 },
            new() { Index = 5, Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "aac", Channels = 2, BitRate = 128000 }];
        var maxBitrate = 640000; var enabled = true;
        using var server = PlaybackInfoIntegrationTests.CreateServer(fixture, c =>
        {
            c.Ac3Rule.DeviceId = "negotiating-device";
            var normalized = Configuration.ConfigurationMigration.Normalize(c);
            c.SchemaVersion = 2; c.Rules = normalized.Rules; c.Diagnostics = normalized.Diagnostics;
            c.Rules[0].MaxAudioBitrate = maxBitrate; c.IsEnabled = enabled;
        });
        var response = await PlaybackInfoIntegrationTests.Playback(server, fixture, PlaybackInfoIntegrationTests.Request(fixture));
        fixture.ClaimDevice = fixture.TokenDevice; // No API authorization header: native authentication uses the token's registration.
        async Task<Dictionary<string, object?>> Stream(int index, string? routing = "negotiating-device", string? session = null)
        {
            using var scope = server.Services.CreateScope();
            var args = new Dictionary<string, object?> { ["itemId"] = fixture.Video.Id, ["container"] = "mkv", ["mediaSourceId"] = fixture.Source.Id,
                ["playSessionId"] = session ?? response.PlaySessionId, ["audioStreamIndex"] = index, ["audioCodec"] = "aac", ["audioBitRate"] = 768000,
                ["audioChannels"] = 6, ["videoCodec"] = "hevc", ["allowVideoStreamCopy"] = true, ["deviceId"] = routing };
            await Run(scope.ServiceProvider, fixture, args);
            Assert.Equal("hevc", args["videoCodec"]); Assert.Equal(true, args["allowVideoStreamCopy"]); return args;
        }
        foreach (var index in new[] { 1, 2, 3, 4, 1, 2 }) Assert.Equal("ac3", (await Stream(index))["audioCodec"]);
        maxBitrate = 384000; Assert.Equal(384000, (await Stream(2))["audioBitRate"]); // Current policy, not a frozen fingerprint.
        Assert.Equal("aac", (await Stream(5))["audioCodec"]); // Preserve stereo.
        Assert.Equal("aac", (await Stream(99))["audioCodec"]);
        Assert.Equal("aac", (await Stream(2, "unissued-routing-device"))["audioCodec"]);
        Assert.Equal("aac", (await Stream(2, session: Guid.NewGuid().ToString("N")))["audioCodec"]);
        fixture.ClaimDevice = "unrelated-device"; Assert.Equal("aac", (await Stream(2))["audioCodec"]);
        fixture.ClaimDevice = fixture.TokenDevice;
        fixture.Token = "different-valid-credential"; Assert.Equal("aac", (await Stream(2))["audioCodec"]);
        fixture.Token = "sentinel-token-never-log";
        fixture.CredentialUser = Guid.NewGuid(); Assert.Equal("aac", (await Stream(2))["audioCodec"]); fixture.CredentialUser = null;
        fixture.User.SetPermission(PermissionKind.EnableAudioPlaybackTranscoding, false); Assert.Equal("aac", (await Stream(2))["audioCodec"]);
        fixture.User.SetPermission(PermissionKind.EnableAudioPlaybackTranscoding, true); enabled = false; Assert.Equal("aac", (await Stream(2))["audioCodec"]);
    }

    private static async Task Run(IServiceProvider services, HostFixture fixture, Dictionary<string, object?> args, QueryString? rawQuery = null)
    {
        var http = new DefaultHttpContext { User = fixture.Principal() }; http.Request.Method = "GET";
        http.Request.QueryString = rawQuery ?? (args.TryGetValue("deviceId", out var device) && device is string id ? QueryString.Create("DeviceId", id) : QueryString.Empty);
        var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = typeof(VideosController).GetTypeInfo(),
            MethodInfo = typeof(VideosController).GetMethod(nameof(VideosController.GetVideoStreamByContainer))! };
        var action = new ActionContext(http, new RouteData(), descriptor);
        var context = new ActionExecutingContext(action, [], args, new object()); var calls = 0;
        await services.GetRequiredService<ProgressiveAudioActionFilter>().OnActionExecutionAsync(context, () =>
        { calls++; return Task.FromResult(new ActionExecutedContext(action, [], new object())); });
        Assert.Equal(1, calls);
    }
}
