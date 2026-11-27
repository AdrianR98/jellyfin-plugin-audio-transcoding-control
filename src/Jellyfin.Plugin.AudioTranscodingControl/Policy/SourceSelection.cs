// SPDX-License-Identifier: GPL-3.0-only
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.AudioTranscodingControl.Policy;

internal static class SourceSelection
{
    internal static MediaSourceInfo? Resolve(IReadOnlyList<MediaSourceInfo> sources, string? id, out string reason)
    {
        reason = "source-resolved";
        if (sources.Count == 0) { reason = "source-list-empty"; return null; }
        var matches = string.IsNullOrEmpty(id) ? sources.ToArray()
            : sources.Where(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length == 0) { reason = "source-id-not-found"; return null; }
        if (matches.Length != 1) { reason = string.IsNullOrEmpty(id) ? "source-id-missing-multiple-sources" : "source-id-ambiguous"; return null; }
        var source = matches[0];
        if (source.IsRemote) reason = "source-remote";
        else if (source.IsInfiniteStream) reason = "source-infinite";
        else if (source.RequiresOpening) reason = "source-requires-opening";
        else if (source.Protocol != MediaProtocol.File) reason = "source-protocol-not-file";
        else if (string.IsNullOrEmpty(source.Id)) reason = "source-id-empty";
        // SupportsTranscoding on a video source is set from VIDEO permission by MediaSourceManager.
        // Audio permission must be checked independently. Do not mutate these native source flags.
        return reason == "source-resolved" ? source : null;
    }

    internal static MediaStream? Audio(MediaSourceInfo source, int? requested, out string reason)
    {
        var index = requested ?? source.DefaultAudioStreamIndex;
        var candidates = source.MediaStreams.Where(s => s.Type == MediaStreamType.Audio && (!index.HasValue || s.Index == index)).ToArray();
        reason = candidates.Length == 0 ? "audio-track-not-found" : candidates.Length != 1 ? "audio-track-ambiguous" : "audio-resolved";
        if (candidates.Length != 1) return null;
        var audio = candidates[0];
        if (audio.IsExternal) reason = "audio-track-external";
        else if (audio.Channels is not (>= 1 and <= 8)) reason = "audio-channel-layout-unsupported";
        else if (audio.Codec is not ("ac3" or "aac" or "eac3" or "truehd" or "dts" or "dca")) reason = "audio-codec-unsupported";
        return reason == "audio-resolved" ? audio : null;
    }

    internal static string Container(string? value) => value is "mkv" or "matroska" or "mp4" or "ts" or "mpegts" ? value : "other";
}
