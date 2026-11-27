// SPDX-License-Identifier: GPL-3.0-only
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.MediaInfoDtos;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

/// <summary>Real DTO, controller, MediaInfoHelper and StreamBuilder; metadata/authentication are isolated fixtures.</summary>
public sealed class PlaybackInfoIntegrationTests
{
    [Theory]
    [InlineData("ac3", 6, true)]
    [InlineData("eac3", 6, false)]
    [InlineData("truehd", 8, false)]
    [InlineData("dts", 6, false)]
    [InlineData("dts", 8, false)]
    [InlineData("aac", 6, false)]
    [InlineData("aac", 2, true)]
    public async Task RegisteredFilterInfluencesRealPlaybackInfo(string codec, int channels, bool directPlay)
    {
        var fixture = new HostFixture(codec, channels);
        using var server = CreateServer(fixture);
        var response = await Playback(server, fixture, Request(fixture));
        var source = Assert.Single(response.MediaSources);
        Assert.Equal(directPlay, source.SupportsDirectPlay);
        if (!directPlay)
        {
            Assert.Contains("AudioCodec=ac3&", source.TranscodingUrl);
            Assert.DoesNotContain("AudioCodec=aac", source.TranscodingUrl);
            Assert.Contains("TranscodingMaxAudioChannels=6", source.TranscodingUrl);
            Assert.Contains("AudioBitrate=640000", source.TranscodingUrl);
            Assert.Contains("VideoCodec=hevc,h264", source.TranscodingUrl);
        }
    }

    [Theory]
    [InlineData("device")]
    [InlineData("duplicate")]
    [InlineData("disabled")]
    [InlineData("missing-profile")]
    [InlineData("no-permission")]
    [InlineData("no-encoder")]
    [InlineData("transcoding-false")]
    [InlineData("unsupported-profile")]
    [InlineData("metadata-failure")]
    public async Task UnsupportedOrUnmatchedRequestsRetainNativeDecision(string reason)
    {
        var fixture = new HostFixture();
        var request = Request(fixture);
        Action<PluginConfiguration>? alter = null;
        switch (reason)
        {
            case "device": fixture.ClaimDevice = "different-device"; break;
            case "duplicate": fixture.DuplicateDeviceClaim = true; break;
            case "disabled": alter = c => c.IsEnabled = false; break;
            case "missing-profile": request.DeviceProfile = null; break;
            case "no-permission": fixture.User.SetPermission(PermissionKind.EnableAudioPlaybackTranscoding, false); break;
            case "no-encoder": fixture.EncoderAvailable = false; break;
            case "transcoding-false": request.EnableTranscoding = false; break;
            case "unsupported-profile": request.DeviceProfile!.TranscodingProfiles[0].AudioCodec = "aac"; break;
            case "metadata-failure": fixture.ThrowOnMetadata = true; break;
        }

        // The native action needs metadata too: fail only the additional pre-action read.
        if (reason == "metadata-failure")
        {
            await DirectFilterTests.PreparationFailureRetainsOriginal(fixture, request);
            return;
        }

        using var server = CreateServer(fixture, alter);
        var response = await Playback(server, fixture, request);
        if (reason == "missing-profile")
        {
            Assert.Null(Assert.Single(response.MediaSources).TranscodingUrl);
        }
        else
        {
            // EAC3 was declared directly compatible before the policy. Native rights/flags remain authoritative.
            Assert.True(Assert.Single(response.MediaSources).SupportsDirectPlay);
        }
    }

    [Fact]
    public async Task TrackChangeAndRepeatedPlaybackInfoReevaluateWithoutSharedMutation()
    {
        var fixture = new HostFixture();
        fixture.Source.MediaStreams = [.. fixture.Source.MediaStreams, new() { Index = 2, Type = MediaBrowser.Model.Entities.MediaStreamType.Audio, Codec = "aac", Channels = 2, BitRate = 128000, SampleRate = 48000 }];
        var profile = HostFixture.Profile();
        var before = JsonSerializer.Serialize(profile);
        using var server = CreateServer(fixture);
        var request = Request(fixture); request.DeviceProfile = profile;
        var first = await Playback(server, fixture, request);
        Assert.Contains("AudioCodec=ac3&", first.MediaSources[0].TranscodingUrl);
        request.AudioStreamIndex = 2;
        Assert.True((await Playback(server, fixture, request)).MediaSources[0].SupportsDirectPlay);
        request.AudioStreamIndex = 1;
        request.StartTimeTicks = 10_000_000;
        var repeated = await Playback(server, fixture, request);
        Assert.Contains("AudioCodec=ac3&", repeated.MediaSources[0].TranscodingUrl);
        Assert.Equal(before, JsonSerializer.Serialize(profile));
    }

    [Fact]
    public void TransformIsIsolatedIdempotentAndPreservesVideoAndAudioOnlyProfiles()
    {
        var original = HostFixture.Profile();
        var before = JsonSerializer.Serialize(original);
        var transformed = Ac3ProfileTransformer.Transform(original)!;
        Assert.Equal(before, JsonSerializer.Serialize(original));
        Assert.NotSame(original, transformed);
        Assert.NotSame(original.CodecProfiles[0].Conditions, transformed.CodecProfiles[1].Conditions);
        Assert.Equal(original.TranscodingProfiles[0].VideoCodec, transformed.TranscodingProfiles[0].VideoCodec);
        Assert.Equal(JsonSerializer.Serialize(original.CodecProfiles[0]), JsonSerializer.Serialize(transformed.CodecProfiles[1]));
        Assert.Equal("aac", transformed.TranscodingProfiles.Single(p => p.Type == DlnaProfileType.Audio).AudioCodec);
        Assert.Equal(JsonSerializer.Serialize(transformed), JsonSerializer.Serialize(Ac3ProfileTransformer.Transform(transformed)));
    }

    [Fact]
    public async Task SmallerExistingBitrateAndVideoCopyProhibitionAreRespected()
    {
        var fixture = new HostFixture();
        var request = Request(fixture);
        request.AllowVideoStreamCopy = false;
        request.DeviceProfile!.CodecProfiles = [.. request.DeviceProfile.CodecProfiles, new()
        {
            Type = CodecType.VideoAudio, Codec = "ac3",
            Conditions = [new(ProfileConditionType.LessThanEqual, ProfileConditionValue.AudioBitrate, "384000", true)],
        }];
        using var server = CreateServer(fixture);
        var response = await Playback(server, fixture, request);
        var url = response.MediaSources[0].TranscodingUrl;
        Assert.Contains("AudioBitrate=384000", url);
        Assert.Contains("allowVideoStreamCopy=false", url);
        Assert.Contains("AudioCodec=ac3&", url);
    }

    [Fact]
    public void UnsupportedAc3ChannelRestrictionAndMalformedProfilesFailClosed()
    {
        var profile = HostFixture.Profile();
        profile.CodecProfiles = [new() { Type = CodecType.VideoAudio, Codec = "ac3", Conditions = [new(ProfileConditionType.LessThanEqual, ProfileConditionValue.AudioChannels, "2", true)] }];
        Assert.Null(Ac3ProfileTransformer.Transform(profile));
        profile = HostFixture.Profile(); profile.TranscodingProfiles[0].Protocol = Jellyfin.Data.Enums.MediaStreamProtocol.http;
        Assert.Null(Ac3ProfileTransformer.Transform(profile));
        profile = HostFixture.Profile(); profile.CodecProfiles[0].Conditions = null!;
        Assert.Null(Ac3ProfileTransformer.Transform(profile));
    }

    internal static PlaybackInfoDto Request(HostFixture fixture) => new()
    {
        DeviceProfile = HostFixture.Profile(), MediaSourceId = fixture.Source.Id, AudioStreamIndex = 1,
        EnableDirectPlay = true, EnableDirectStream = true, EnableTranscoding = true,
        AllowAudioStreamCopy = true, AllowVideoStreamCopy = true,
    };

    internal static TestServer CreateServer(HostFixture fixture, Action<PluginConfiguration>? alter = null) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            fixture.Register(services);
            new PluginServiceRegistrator().RegisterServices(services, null!);
            services.PostConfigure<PluginConfiguration>(options =>
            {
                var c = fixture.Configuration(); options.IsEnabled = c.IsEnabled; options.EnableDebugLogging = c.EnableDebugLogging; options.Ac3Rule = c.Ac3Rule;
                alter?.Invoke(options);
            });
            services.AddAuthorization();
            services.AddControllers().ConfigureApplicationPartManager(parts =>
            {
                parts.ApplicationParts.Clear();
                parts.ApplicationParts.Add(new SingleControllerPart());
            });
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.Use(async (context, next) => { context.User = fixture.Principal(); context.Connection.RemoteIpAddress = IPAddress.Loopback; await next(); });
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));

    internal static async Task<PlaybackInfoResponse> Playback(TestServer server, HostFixture fixture, PlaybackInfoDto request)
    {
        using var client = server.CreateClient();
        using var response = await client.PostAsJsonAsync($"/Items/{fixture.Video.Id}/PlaybackInfo", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PlaybackInfoResponse>())!;
    }

    private sealed class SingleControllerPart : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "RealJellyfinMediaInfoController";
        public IEnumerable<TypeInfo> Types => [typeof(MediaInfoController).GetTypeInfo()];
    }
}
