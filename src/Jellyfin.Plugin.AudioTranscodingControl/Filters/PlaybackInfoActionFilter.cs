// SPDX-License-Identifier: GPL-3.0-only
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.MediaInfoDtos;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.AudioTranscodingControl.Filters;

/// <summary>Adapts a matched, supported, bound PlaybackInfo request before the native action.</summary>
public sealed class PlaybackInfoActionFilter : IAsyncActionFilter
{
    private static readonly byte[] DeviceSalt = RandomNumberGenerator.GetBytes(32);
    private static readonly bool HostCompatible = typeof(IUserManager).Assembly.GetName().Version == new Version(10, 11, 11, 0)
        && typeof(PlaybackInfoDto).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .EndsWith("+1fbd8739292cce610231be93daf43368733edf63", StringComparison.Ordinal) == true;
    private readonly IOptionsSnapshot<PluginConfiguration> _options;
    private readonly IUserManager _users;
    private readonly ILibraryManager _library;
    private readonly IMediaSourceManager _sources;
    private readonly IMediaEncoder _encoder;
    private readonly ILogger<PlaybackInfoActionFilter> _logger;

    /// <summary>Initializes a request-scoped policy using existing host services.</summary>
    /// <param name="options">Snapshot of opt-in settings.</param>
    /// <param name="users">Native user manager.</param>
    /// <param name="library">Native access-aware library manager.</param>
    /// <param name="sources">Native media metadata manager.</param>
    /// <param name="encoder">Native encoder capabilities.</param>
    /// <param name="logger">Structured plugin diagnostics.</param>
    public PlaybackInfoActionFilter(IOptionsSnapshot<PluginConfiguration> options, IUserManager users,
        ILibraryManager library, IMediaSourceManager sources, IMediaEncoder encoder, ILogger<PlaybackInfoActionFilter> logger)
    {
        _options = options;
        _users = users;
        _library = library;
        _sources = sources;
        _encoder = encoder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HttpMethods.IsPost(context.HttpContext.Request.Method)
            && context.ActionDescriptor is ControllerActionDescriptor descriptor
            && descriptor.ControllerTypeInfo.AsType() == typeof(MediaInfoController)
            && descriptor.MethodInfo.Name == nameof(MediaInfoController.GetPostedPlaybackInfo))
        {
            try
            {
                await PrepareAsync(context).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Exception messages/objects may contain paths, tokens or request values.
                _logger.LogWarning("ATC preparation skipped: unexpected-preparation-error. Original request retained.");
            }
        }

        // Never retry the action or treat controller failures as preparation failures.
        await next().ConfigureAwait(false);
    }

    private async Task PrepareAsync(ActionExecutingContext context)
    {
        var config = _options.Value;
        var alias = "unmatched";
        void Skip(string reason)
        {
            if (config.EnableDebugLogging)
            {
                _logger.LogDebug("ATC skipped: {Reason}; device={DeviceAlias}", reason, alias);
            }
        }

        if (!Ac3RuleMatcher.TryMatch(context.HttpContext.User, config, out var deviceId, out var userId))
        {
            Skip("disabled-or-unmatched-identity");
            return;
        }

        alias = Convert.ToHexString(HMACSHA256.HashData(DeviceSalt, Encoding.UTF8.GetBytes(deviceId)))[..12];
        if (!HostCompatible || !context.ActionArguments.TryGetValue("playbackInfoDto", out var value)
            || value is not PlaybackInfoDto dto || value.GetType() != typeof(PlaybackInfoDto) || dto.DeviceProfile is null)
        {
            Skip("unsupported-or-missing-dto-profile");
            return;
        }

        T? Argument<T>(string name) where T : struct => context.ActionArguments.TryGetValue(name, out var v) && v is T typed ? typed : null;
        string? TextArgument(string name) => context.ActionArguments.TryGetValue(name, out var v) ? v as string : null;
        var requestedUser = Argument<Guid>("userId") ?? dto.UserId;
        var maxChannels = Argument<int>("maxAudioChannels") ?? dto.MaxAudioChannels;
        if ((requestedUser.HasValue && requestedUser.Value != Guid.Empty && requestedUser != userId)
            || !(Argument<bool>("enableTranscoding") ?? dto.EnableTranscoding ?? true)
            || (maxChannels.HasValue && maxChannels < 6)
            || !string.IsNullOrEmpty(TextArgument("liveStreamId") ?? dto.LiveStreamId))
        {
            Skip("request-policy-or-scope-restriction");
            return;
        }

        var user = _users.GetUserById(userId);
        if (user is null || user.Id != userId || !user.HasPermission(PermissionKind.EnableMediaPlayback)
            || !user.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding)
            || !_encoder.CanEncodeToAudioCodec("ac3"))
        {
            Skip("audio-transcoding-denied-or-encoder-unavailable");
            return;
        }

        var itemId = Argument<Guid>("itemId");
        if (!itemId.HasValue || _library.GetItemById<BaseItem>(itemId.Value, user) is not Video video)
        {
            Skip("not-accessible-local-video");
            return;
        }

        var sourceId = TextArgument("mediaSourceId") ?? dto.MediaSourceId;
        if (string.IsNullOrEmpty(sourceId))
        {
            Skip("explicit-source-required");
            return;
        }

        // Match the native controller's probing behavior, including the first request for an unprobed local item.
        var sources = await _sources.GetPlaybackMediaSources(video, user, true, false, context.HttpContext.RequestAborted).ConfigureAwait(false);
        var candidates = sources.Where(s => string.Equals(s.Id, sourceId, StringComparison.Ordinal)).ToArray();
        if (candidates.Length != 1 || candidates[0] is not { IsRemote: false, IsInfiniteStream: false, RequiresOpening: false, SupportsTranscoding: true, Protocol: MediaProtocol.File } source)
        {
            Skip("unsupported-or-ambiguous-source");
            return;
        }

        var audioIndex = Argument<int>("audioStreamIndex") ?? dto.AudioStreamIndex ?? source.DefaultAudioStreamIndex;
        var tracks = source.MediaStreams.Where(s => s.Type == MediaStreamType.Audio && s.Index == audioIndex).ToArray();
        if (tracks.Length != 1 || tracks[0].IsExternal || tracks[0].Channels is not (6 or 8)
            || tracks[0].Codec is not ("ac3" or "aac" or "eac3" or "truehd" or "dts" or "dca"))
        {
            Skip("compatible-stereo-or-unsupported-track");
            return;
        }

        var transformed = Ac3ProfileTransformer.Transform(dto.DeviceProfile);
        if (transformed is null)
        {
            Skip("unsupported-profile-path");
            return;
        }

        if (config.EnableDebugLogging)
        {
            _logger.LogDebug("ATC applied: device={DeviceAlias}; sourceCodec={SourceCodec}; sourceChannels={SourceChannels}; previousAacOffered={PreviousAacOffered}; videoProfilesBefore={Before}; videoProfilesAfter={After}; offeredTarget=ac3; maxChannels=6; maxBitrate=640000; actualEncoder=not-yet-known",
                alias, tracks[0].Codec, tracks[0].Channels,
                dto.DeviceProfile.TranscodingProfiles.Any(p => p.Type == DlnaProfileType.Video && p.AudioCodec?.Split(',', StringSplitOptions.TrimEntries).Contains("aac", StringComparer.OrdinalIgnoreCase) == true),
                dto.DeviceProfile.DirectPlayProfiles.Count(p => p.Type == DlnaProfileType.Video),
                transformed.DirectPlayProfiles.Count(p => p.Type == DlnaProfileType.Video));
        }

        // Single, final write after all potentially failing work; all other DTO fields stay untouched.
        dto.DeviceProfile = transformed;
    }
}
