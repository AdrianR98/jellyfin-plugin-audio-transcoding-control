// SPDX-License-Identifier: GPL-3.0-only
using System.Reflection;
using System.Security.Claims;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Helpers;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

internal sealed class HostFixture
{
    internal const string DeviceId = "synthetic-exact-device";
    internal User User { get; } = new("synthetic", "provider", "reset") { Id = new Guid("a6055b17-e34c-49cb-a3ad-b996243faed9") };
    internal Video Video { get; } = new() { Id = new Guid("e50cdbbc-2a60-4f25-8aa9-d41cc526d55c"), Path = "synthetic.mkv" };
    internal MediaSourceInfo Source { get; }
    internal bool EncoderAvailable { get; set; } = true;
    internal bool ThrowOnMetadata { get; set; }
    internal string ClaimDevice { get; set; } = DeviceId;
    internal bool DuplicateDeviceClaim { get; set; }
    internal bool DebugLogging { get; set; } = true;
    internal string Token { get; set; } = "sentinel-token-never-log";
    internal string TokenDevice { get; set; } = DeviceId;
    internal Guid? CredentialUser { get; set; }

    internal HostFixture(string codec = "eac3", int channels = 6)
    {
        User.SetPermission(PermissionKind.EnableMediaPlayback, true);
        User.SetPermission(PermissionKind.EnableAudioPlaybackTranscoding, true);
        User.SetPermission(PermissionKind.EnableVideoPlaybackTranscoding, true);
        User.SetPermission(PermissionKind.EnablePlaybackRemuxing, true);
        Source = new MediaSourceInfo
        {
            Id = Video.Id.ToString("N"), Path = Video.Path, Container = "mkv", Protocol = MediaProtocol.File,
            Bitrate = 20_000_000, DefaultAudioStreamIndex = 1,
            MediaStreams =
            [
                new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "hevc", Width = 3840, Height = 2160, BitRate = 19_000_000 },
                new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = codec, Channels = channels, BitRate = 640_000, SampleRate = 48000 },
            ],
        };
    }

    internal ClaimsPrincipal Principal()
    {
        var claims = new List<Claim>
        {
            new(InternalClaimTypes.DeviceId, ClaimDevice), new(InternalClaimTypes.UserId, User.Id.ToString("N")),
            new(InternalClaimTypes.Token, Token), new(InternalClaimTypes.IsApiKey, "False"), new(ClaimTypes.Role, "Administrator"),
        };
        if (DuplicateDeviceClaim) claims.Add(new(InternalClaimTypes.DeviceId, ClaimDevice));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    internal PluginConfiguration Configuration() => new()
    {
        IsEnabled = true, EnableDebugLogging = DebugLogging,
        Ac3Rule = new() { IsEnabled = true, DeviceId = DeviceId, UserId = User.Id },
    };

    internal static DeviceProfile Profile() => new()
    {
        Name = "AndroidTV-shaped-test-profile", MaxStaticBitrate = 100_000_000, MaxStreamingBitrate = 100_000_000,
        DirectPlayProfiles = [new() { Type = DlnaProfileType.Video, Container = "mkv,mp4,ts", VideoCodec = "hevc,h264", AudioCodec = "aac,ac3,eac3,dts,truehd" }],
        TranscodingProfiles =
        [
            new() { Type = DlnaProfileType.Video, Context = EncodingContext.Streaming, Container = "ts", Protocol = MediaStreamProtocol.hls, VideoCodec = "hevc,h264", AudioCodec = "aac,ac3,eac3,mp3" },
            new() { Type = DlnaProfileType.Video, Context = EncodingContext.Streaming, Container = "mp4", Protocol = MediaStreamProtocol.hls, VideoCodec = "hevc,h264", AudioCodec = "aac,ac3,eac3,mp3,flac,opus,dts" },
            new() { Type = DlnaProfileType.Audio, Context = EncodingContext.Streaming, Container = "ts", Protocol = MediaStreamProtocol.hls, AudioCodec = "aac" },
        ],
        CodecProfiles = [new() { Type = CodecType.Video, Codec = "hevc", Conditions = [new(ProfileConditionType.LessThanEqual, ProfileConditionValue.Width, "3840", true)] }],
        SubtitleProfiles = [new() { Format = "srt", Method = SubtitleDeliveryMethod.External }],
    };

    internal void Register(IServiceCollection services)
    {
        services.AddLogging();
        var users = Stub<IUserManager>((m, a) => m.Name == "GetUserById" && (Guid)a![0]! == User.Id ? User : null);
        var library = Stub<ILibraryManager>((m, a) => m.Name == "GetItemById" ? Video : throw new NotSupportedException(m.Name));
        var sources = Stub<IMediaSourceManager>((m, a) => m.Name == "GetPlaybackMediaSources"
            ? ThrowOnMetadata ? throw new InvalidOperationException("sentinel-token-never-log private-path") : Task.FromResult<IReadOnlyList<MediaSourceInfo>>([Source])
            : throw new NotSupportedException(m.Name));
        var encoder = Stub<IMediaEncoder>((m, a) => m.Name.StartsWith("Can", StringComparison.Ordinal) ? EncoderAvailable : throw new NotSupportedException(m.Name));
        var config = Stub<IServerConfigurationManager>((m, a) => m.Name == "get_Configuration" ? new ServerConfiguration() : throw new NotSupportedException(m.Name));
        var network = Stub<INetworkManager>((m, a) => m.Name == "IsInLocalNetwork" ? true : throw new NotSupportedException(m.Name));
        var devices = Stub<IDeviceManager>((m, a) => m.Name switch
        {
            "GetCapabilities" => new ClientCapabilities(),
            "GetDevices" => new MediaBrowser.Model.Querying.QueryResult<Jellyfin.Database.Implementations.Entities.Security.Device>
            { Items = [new(CredentialUser ?? User.Id, "Jellyfin for Android TV", "0.20.1", "Synthetic", TokenDevice) { AccessToken = Token }], TotalRecordCount = 1 },
            _ => throw new NotSupportedException(m.Name),
        });
        services.AddSingleton(users).AddSingleton(library).AddSingleton(sources).AddSingleton(encoder).AddSingleton(config).AddSingleton(network).AddSingleton(devices);
        services.AddSingleton(new MediaInfoHelper(users, library, sources, encoder, config, NullLogger<MediaInfoHelper>.Instance, network, devices));
    }

    internal static void RegisterUnusedHostServices(IServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(Stub<IUserManager>((m, a) => throw new NotSupportedException()));
        services.AddSingleton(Stub<ILibraryManager>((m, a) => throw new NotSupportedException()));
        services.AddSingleton(Stub<IMediaSourceManager>((m, a) => throw new NotSupportedException()));
        services.AddSingleton(Stub<IMediaEncoder>((m, a) => throw new NotSupportedException()));
        services.AddSingleton(Stub<IDeviceManager>((m, a) => throw new NotSupportedException()));
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TestServiceProxy>();
        ((TestServiceProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

public class TestServiceProxy : DispatchProxy
{
    internal Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}
