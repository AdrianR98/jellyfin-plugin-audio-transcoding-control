// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Text.Json;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Model.Dlna;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal static class Ac3ProfileTransformer
{
    internal static DeviceProfile? Transform(DeviceProfile original) => Transform(original, new TranscodingRule());

    internal static DeviceProfile? Transform(DeviceProfile original, TranscodingRule rule, bool forceReencode = false)
        => Transform(original, rule, forceReencode, out _);

    internal static DeviceProfile? Transform(DeviceProfile original, TranscodingRule rule, bool forceReencode, out string reason)
    {
        reason = "invalid-ac3-rule";
        if (ConfigurationMigration.RuleError(rule) is not null) return null;
        reason = "profile-derived-or-malformed";
        // Reject derived or malformed profiles rather than losing unknown state during cloning.
        if (original.GetType() != typeof(DeviceProfile) || original.TranscodingProfiles is null
            || original.DirectPlayProfiles is null || original.CodecProfiles is null
            || original.ContainerProfiles is null || original.SubtitleProfiles is null
            || original.TranscodingProfiles.Any(p => p is null || p.GetType() != typeof(TranscodingProfile))
            || original.DirectPlayProfiles.Any(p => p is null || p.GetType() != typeof(DirectPlayProfile))
            || original.CodecProfiles.Any(p => p is null || p.GetType() != typeof(CodecProfile) || !PlainConditions(p.Conditions) || !PlainConditions(p.ApplyConditions))
            || original.TranscodingProfiles.Any(p => !PlainConditions(p.Conditions))
            || original.ContainerProfiles.Any(p => p is null || p.GetType() != typeof(ContainerProfile) || !PlainConditions(p.Conditions))
            || original.SubtitleProfiles.Any(p => p is null || p.GetType() != typeof(SubtitleProfile)))
        {
            return null;
        }

        reason = "profile-ac3-channel-cap-below-rule";
        // A client that restricts AC3 to fewer channels has not offered the multichannel path required here.
        if (original.CodecProfiles.Any(p => p.Type == CodecType.VideoAudio && (string.IsNullOrEmpty(p.Codec) || ContainsAc3(p.Codec))
            && p.Conditions.Any(c => c.Property == ProfileConditionValue.AudioChannels
                && c.Condition is ProfileConditionType.Equals or ProfileConditionType.LessThanEqual
                && int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) && cap < rule.MaxAudioChannels)))
        {
            return null;
        }

        var paths = original.TranscodingProfiles.Where(p => p.Type == DlnaProfileType.Video && p.Context == EncodingContext.Streaming).ToArray();
        reason = "profile-no-video-streaming-path";
        if (paths.Length == 0) return null;
        foreach (var path in paths)
        {
            reason = path.Protocol == MediaStreamProtocol.hls ? path.Container is "ts" or "mp4" ? "profile-ac3-not-offered" : "profile-hls-container-unsupported"
                : path.Protocol == MediaStreamProtocol.http && path.Container is "mkv" or "matroska" ? "profile-ac3-not-offered" : "profile-protocol-unsupported";
            if (!((path.Protocol == MediaStreamProtocol.hls && path.Container is "ts" or "mp4")
                || (path.Protocol == MediaStreamProtocol.http && path.Container is "mkv" or "matroska")) || !ContainsAc3(path.AudioCodec)) return null;
            reason = "profile-transcode-channel-cap-below-rule";
            if (!string.IsNullOrEmpty(path.MaxAudioChannels)
                && (!int.TryParse(path.MaxAudioChannels, NumberStyles.None, CultureInfo.InvariantCulture, out var cap) || cap < rule.MaxAudioChannels)) return null;
        }

        var copy = JsonSerializer.Deserialize<DeviceProfile>(JsonSerializer.SerializeToUtf8Bytes(original))!;
        foreach (var path in copy.TranscodingProfiles.Where(p => p.Type == DlnaProfileType.Video && p.Context == EncodingContext.Streaming))
        {
            path.AudioCodec = "ac3";
            path.MaxAudioChannels = rule.MaxAudioChannels.ToString(CultureInfo.InvariantCulture);
        }

        // Empty AudioCodec means wildcard in Jellyfin. Remove exhausted profiles rather than creating one.
        copy.DirectPlayProfiles = copy.DirectPlayProfiles.Where(p => p.Type != DlnaProfileType.Video
            || (!forceReencode && (string.IsNullOrEmpty(p.AudioCodec) || ContainsAc3(p.AudioCodec)))).ToArray();
        foreach (var path in copy.DirectPlayProfiles.Where(p => p.Type == DlnaProfileType.Video))
        {
            path.AudioCodec = "ac3";
        }

        // Codec profiles apply even for pure audio failures; TranscodingProfile.Conditions alone do not.
        // First profile is applied last by StreamBuilder; LessThanEqual preserves preceding smaller limits.
        var existingGuard = copy.CodecProfiles.FirstOrDefault(p => IsGuard(p, rule));
        if (existingGuard is not null)
        {
            copy.CodecProfiles = [existingGuard, .. copy.CodecProfiles.Where(p => !ReferenceEquals(p, existingGuard))];
        }
        else
        {
            copy.CodecProfiles = [new CodecProfile
            {
                Type = CodecType.VideoAudio,
                Codec = "ac3",
                Conditions =
                [
                    new ProfileCondition(ProfileConditionType.LessThanEqual, ProfileConditionValue.AudioChannels, rule.MaxAudioChannels.ToString(CultureInfo.InvariantCulture), true),
                    new ProfileCondition(ProfileConditionType.LessThanEqual, ProfileConditionValue.AudioBitrate, rule.MaxAudioBitrate.ToString(CultureInfo.InvariantCulture), true),
                ],
            }, .. copy.CodecProfiles];
        }

        reason = "profile-transformed";
        return copy;
    }

    private static bool PlainConditions(ProfileCondition[]? conditions) => conditions is not null
        && conditions.All(p => p is not null && p.GetType() == typeof(ProfileCondition));

    private static bool IsGuard(CodecProfile profile, TranscodingRule rule) => profile.Type == CodecType.VideoAudio && profile.Codec == "ac3"
        && string.IsNullOrEmpty(profile.Container) && string.IsNullOrEmpty(profile.SubContainer) && profile.ApplyConditions.Length == 0
        && profile.Conditions.Length == 2 && profile.Conditions.All(p => p.Condition == ProfileConditionType.LessThanEqual && p.IsRequired)
        && profile.Conditions.Any(p => p.Property == ProfileConditionValue.AudioChannels && p.Value == rule.MaxAudioChannels.ToString(CultureInfo.InvariantCulture))
        && profile.Conditions.Any(p => p.Property == ProfileConditionValue.AudioBitrate && p.Value == rule.MaxAudioBitrate.ToString(CultureInfo.InvariantCulture));

    private static bool ContainsAc3(string? value) => value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Contains("ac3", StringComparer.OrdinalIgnoreCase) == true;
}
