// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

/// <summary>Discovery fixtures against Jellyfin.Model 10.11.11; no plugin transformation is implemented.</summary>
public sealed class StreamBuilderFeasibilityTests
{
    [Theory]
    [InlineData("dts", 8)]
    [InlineData("aac", 6)]
    public void CandidateProfileOffersAc3ForIncompatibleMultichannelAudio(string sourceCodec, int channels)
    {
        var stream = Build(sourceCodec, channels);
        Assert.NotNull(stream);
        Assert.NotEqual(PlayMethod.DirectPlay, stream.PlayMethod);
        Assert.Equal("ac3", stream.AudioCodecs[0]);
        Assert.Equal(6, stream.TranscodingMaxAudioChannels);
        Assert.True(stream.AudioBitrate <= 640_000);
        var url = stream.ToUrl(null, null, null);
        Assert.Contains("/master.m3u8?", url);
        Assert.Contains("AudioCodec=ac3", url);
        Assert.Contains("TranscodingMaxAudioChannels=6", url);
        Assert.Contains("AudioBitrate=640000", url);
        Assert.Contains("VideoCodec=h264,hevc", url);
        Assert.DoesNotContain("Static=true", url);
    }

    [Theory]
    [InlineData("ac3", 6)]
    [InlineData("aac", 2)]
    public void CandidateProfilePreservesCompatibleAc3AndStereoDirectPlay(string sourceCodec, int channels)
    {
        var stream = Build(sourceCodec, channels);
        Assert.NotNull(stream);
        Assert.Equal(PlayMethod.DirectPlay, stream.PlayMethod);
    }

    private static StreamInfo? Build(string audioCodec, int channels)
    {
        // An explicit candidate profile, not the original Android TV profile and not a rule engine.
        var profile = new DeviceProfile
        {
            MaxStreamingBitrate = 100_000_000,
            MaxStaticBitrate = 100_000_000,
            DirectPlayProfiles = [new DirectPlayProfile { Type = DlnaProfileType.Video, Container = "mkv", VideoCodec = "h264,hevc", AudioCodec = "ac3,aac" }],
            TranscodingProfiles = [new TranscodingProfile
            {
                Type = DlnaProfileType.Video,
                Context = EncodingContext.Streaming,
                Container = "ts",
                Protocol = MediaStreamProtocol.hls,
                VideoCodec = "h264,hevc",
                AudioCodec = "ac3,aac",
                MaxAudioChannels = "6",
            }],
            CodecProfiles = [new CodecProfile
            {
                Type = CodecType.VideoAudio,
                Codec = "aac",
                Conditions = [new ProfileCondition { Property = ProfileConditionValue.AudioChannels, Condition = ProfileConditionType.LessThanEqual, Value = "2", IsRequired = true }],
            }],
        };
        var source = new MediaSourceInfo
        {
            Id = "fixture",
            Container = "mkv",
            Protocol = MediaProtocol.File,
            Bitrate = 20_000_000,
            MediaStreams =
            [
                new MediaStream { Index = 0, Type = MediaStreamType.Video, Codec = "hevc", Width = 3840, Height = 2160, BitRate = 19_000_000 },
                new MediaStream { Index = 1, Type = MediaStreamType.Audio, Codec = audioCodec, Channels = channels, BitRate = 640_000, SampleRate = 48000 },
            ],
        };
        return new StreamBuilder(new AvailableTranscoder(), NullLogger.Instance).GetOptimalVideoStream(new MediaOptions
        {
            ItemId = new Guid("e50cdbbc-2a60-4f25-8aa9-d41cc526d55c"),
            MediaSourceId = source.Id,
            AudioStreamIndex = 1,
            MediaSources = [source],
            Profile = profile,
            Context = EncodingContext.Streaming,
            // MediaInfoHelper.SetDeviceSpecificData disables HTTP direct-stream before invoking StreamBuilder.
            EnableDirectStream = false,
            AllowAudioStreamCopy = true,
            AllowVideoStreamCopy = true,
        });
    }

    private sealed class AvailableTranscoder : ITranscoderSupport
    {
        public bool CanEncodeToAudioCodec(string codec) => true;
        public bool CanEncodeToSubtitleCodec(string codec) => true;
        public bool CanExtractSubtitles(string codec) => true;
    }
}
