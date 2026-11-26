// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Model.Dlna;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;

internal sealed class PluginDiagnostics(ILogger logger, DiagnosticOptions options, string correlation)
{
    private static readonly byte[] Salt = RandomNumberGenerator.GetBytes(32);
    internal bool Enabled(LogLevel level) => Enum.TryParse<LogLevel>(options.Level, out var threshold)
        && level >= threshold && (logger.IsEnabled(level) || (level <= LogLevel.Debug && logger.IsEnabled(LogLevel.Information)));

    internal void Write(LogLevel level, int id, string message, params object?[] values)
    {
        if (!Enabled(level)) return;
        // Serilog's global minimum cannot be lowered by a plugin's ILogger filter.
        // Preserve diagnostics at the native Information level, explicitly labeling the requested level.
        var native = logger.IsEnabled(level) ? level : LogLevel.Information;
        var arguments = new object?[] { correlation, level.ToString(), id }.Concat(values).ToArray();
#pragma warning disable CA2254 // All event templates are fixed at call sites; only the common structured prefix is added here.
        logger.Log(native, new EventId(id, "ATC" + id.ToString(CultureInfo.InvariantCulture)),
            "ATC [{CorrelationId}] {DiagnosticLevel} event={DiagnosticEventId}: " + message, arguments);
#pragma warning restore CA2254
    }

    internal void Failure(int id, string reason, Exception error, LogLevel level = LogLevel.Warning)
    {
        // No Exception.Message/ToString, file names, paths, URL values or argument values.
        var frames = new StackTrace(error, false).GetFrames() ?? [];
        var stack = string.Join(" <- ", frames.Take(10).Select(f => f.GetMethod())
            .Where(m => m is not null).Select(m => SafeSymbol((m!.DeclaringType?.FullName ?? "unknown") + "." + m.Name)));
        Write(level, id, "{Reason}; exceptionType={ExceptionType}; safeStack={SafeStack}; original-request-retained",
            reason, SafeSymbol(error.GetType().Name), stack);
    }

    internal static string Reference(string? value) => string.IsNullOrEmpty(value) ? "missing"
        : Convert.ToHexString(HMACSHA256.HashData(Salt, Encoding.UTF8.GetBytes(value)))[..12];

    private static string SafeSymbol(string value) => Regex.Replace(value[..Math.Min(value.Length, 240)], @"[^a-zA-Z0-9_+.<>`]+", "?");

    internal static string SafeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unavailable";
        if (value.Length > 100 || value.Any(char.IsControl) || value.Contains('/') || value.Contains('\\')
            || Regex.IsMatch(value, @"(?i)(token|password|authorization|api.?key|secret|https?://|[a-z]:[\\/]|@)")
            || Regex.IsMatch(value, @"[A-Za-z0-9_-]{20,}|[0-9a-fA-F]{8}-[0-9a-fA-F-]{20,}")) return "redacted-" + Reference(value);
        return value;
    }

    internal static string Client(string? name) => name is "Jellyfin Android TV" or "Jellyfin for Android TV" or "Jellyfin Web" or "Jellyfin Media Player"
        ? name : "client-" + Reference(name);

    internal static string Version(string? value) => value is not null && Regex.IsMatch(value, @"^\d{1,3}(\.\d{1,3}){1,3}([-+][a-zA-Z0-9.]{1,12})?$") ? value : "unavailable";

    internal static string Codec(string? value)
    {
        string[] allowed = ["aac", "ac3", "eac3", "truehd", "dts", "dca", "hevc", "h264", "av1", "vp9", "vp8", "mpeg2video", "mp3", "mp2", "flac", "opus", "alac", "copy", "pcm_s16le", "pcm_s24le", "vorbis"];
        return value is null ? "none" : string.Join(',', value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Take(16).Select(c => allowed.Contains(c, StringComparer.OrdinalIgnoreCase) ? c.ToLowerInvariant() : "other"));
    }

    internal static object Profile(DeviceProfile profile) => new
    {
        DirectVideo = profile.DirectPlayProfiles.Where(p => p.Type == DlnaProfileType.Video).Take(8)
            .Select(p => new { Audio = Codec(p.AudioCodec), Video = Codec(p.VideoCodec) }).ToArray(),
        TranscodingVideo = profile.TranscodingProfiles.Where(p => p.Type == DlnaProfileType.Video).Take(8)
            .Select(p => new { Audio = Codec(p.AudioCodec), Video = Codec(p.VideoCodec), Container = p.Container is "ts" or "mp4" ? p.Container : "other", Protocol = p.Protocol.ToString(), MaxChannels = int.TryParse(p.MaxAudioChannels, out var n) ? n : (int?)null }).ToArray(),
        AudioConstraints = profile.CodecProfiles.Where(p => p.Type == CodecType.VideoAudio).Take(16)
            .Select(p => new { Codec = Codec(p.Codec), Conditions = p.Conditions
                .Where(c => c.Property is ProfileConditionValue.AudioChannels or ProfileConditionValue.AudioBitrate or ProfileConditionValue.AudioSampleRate)
                .Take(8).Select(c => new { Property = c.Property.ToString(), Operator = c.Condition.ToString(), Limit = int.TryParse(c.Value, out var n) ? n : (int?)null, c.IsRequired }).ToArray() }).ToArray(),
        VideoConstraintCount = profile.CodecProfiles.Count(p => p.Type == CodecType.Video),
        SubtitleProfileCount = profile.SubtitleProfiles.Length,
    };
}
