// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Jellyfin.Api.Constants;
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Models.MediaInfoDtos;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.AudioTranscodingControl.Filters;

/// <summary>Applies one ordered policy before native PlaybackInfo, then optionally inspects its result read-only.</summary>
public sealed class PlaybackInfoActionFilter : IAsyncActionFilter
{
    private static readonly bool HostCompatible = typeof(IUserManager).Assembly.GetName().Version == new Version(10, 11, 11, 0)
        && typeof(PlaybackInfoDto).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .EndsWith("+1fbd8739292cce610231be93daf43368733edf63", StringComparison.Ordinal) == true;
    internal static bool IsHostCompatible => HostCompatible;
    private readonly PlaybackPolicySessions _sessions;
    private readonly IOptionsSnapshot<PluginConfiguration> _options;
    private readonly IUserManager _users;
    private readonly ILibraryManager _library;
    private readonly IMediaSourceManager _sources;
    private readonly IMediaEncoder _encoder;
    private readonly ILogger<PlaybackInfoActionFilter> _logger;
    private readonly IDeviceManager _devices;

    /// <summary>Initializes the request-scoped filter with existing Jellyfin services.</summary>
    /// <param name="options">Configuration snapshot.</param>
    /// <param name="users">Native users.</param>
    /// <param name="library">Access-aware native library.</param>
    /// <param name="sources">Native source metadata.</param>
    /// <param name="encoder">Native encoder capabilities.</param>
    /// <param name="sessions">Bounded proof of validated playback negotiation.</param>
    /// <param name="logger">Native logging category.</param>
    /// <param name="devices">Native credential registry.</param>
    public PlaybackInfoActionFilter(IOptionsSnapshot<PluginConfiguration> options, IUserManager users,
        ILibraryManager library, IMediaSourceManager sources, IMediaEncoder encoder, ILogger<PlaybackInfoActionFilter> logger, PlaybackPolicySessions sessions, IDeviceManager devices)
    {
        _options = options; _users = users; _library = library; _sources = sources; _encoder = encoder; _logger = logger; _sessions = sessions; _devices = devices;
    }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!HttpMethods.IsPost(context.HttpContext.Request.Method)
            || context.ActionDescriptor is not ControllerActionDescriptor descriptor
            || descriptor.ControllerTypeInfo.AsType() != typeof(MediaInfoController)
            || descriptor.MethodInfo.Name != nameof(MediaInfoController.GetPostedPlaybackInfo))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var correlation = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["AtcCorrelationId"] = correlation });
        var config = new PluginConfiguration();
        var diagnostics = new PluginDiagnostics(_logger, new(), correlation);
        TranscodingRule? selected = null;
        var applied = false;
        var started = Stopwatch.GetTimestamp();
        try
        {
            config = ConfigurationMigration.Normalize(_options.Value);
            diagnostics = new(_logger, config.Diagnostics, correlation);
            var principal = context.HttpContext.User;
            diagnostics.Write(LogLevel.Debug, 2000,
                "PlaybackInfo received; client={Client}; version={ClientVersion}; deviceRef={DeviceRef}; userRef={UserRef}; masterEnabled={MasterEnabled}; schema={Schema}",
                PluginDiagnostics.Client(principal.FindFirst(InternalClaimTypes.Client)?.Value),
                PluginDiagnostics.Version(principal.FindFirst(InternalClaimTypes.Version)?.Value),
                PluginDiagnostics.Reference(principal.FindFirst(InternalClaimTypes.DeviceId)?.Value),
                PluginDiagnostics.Reference(principal.FindFirst(InternalClaimTypes.UserId)?.Value), config.IsEnabled, config.SchemaVersion);
            var matchingStart = Stopwatch.GetTimestamp();
            selected = RuleEvaluator.Select(principal, config, decision =>
            {
                if (config.Diagnostics.DetailedRuleMatching)
                    diagnostics.Write(LogLevel.Trace, 2100,
                        "Rule evaluated; position={Position}; ruleId={RuleId}; ruleName={RuleName}; enabled={Enabled}; deviceMatch={DeviceMatch}; userMatch={UserMatch}; reason={Reason}",
                        decision.Position, decision.Rule.Id, PluginDiagnostics.SafeLabel(decision.Rule.Name), decision.Rule.IsEnabled,
                        decision.DeviceMatch, decision.UserMatch, decision.Reason);
            });
            if (config.Diagnostics.PerformanceMeasurements)
                diagnostics.Write(LogLevel.Debug, 2991, "Rule evaluation durationMs={DurationMs}", Stopwatch.GetElapsedTime(matchingStart).TotalMilliseconds);
            if (selected is null)
                diagnostics.Write(LogLevel.Debug, 2102, "No rule selected; reason={Reason}; original-request-retained",
                    !config.IsEnabled ? "master-disabled" : !RuleEvaluator.Identity(principal, out _, out _) ? "invalid-or-ambiguous-identity" : "no-valid-enabled-match");
            else
            {
                diagnostics.Write(LogLevel.Debug, 2101, "Rule selected; ruleId={RuleId}; ruleName={RuleName}; first-match=true; no-rule-combination=true",
                    selected.Id, PluginDiagnostics.SafeLabel(selected.Name));
                applied = await PrepareAsync(context, selected, config.Diagnostics, diagnostics).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception error) { diagnostics.Failure(2900, "unexpected-preparation-error", error); }
        var preparationMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        // Controller errors retain native behavior. Never retry, change the result or set ExceptionHandled.
        ActionExecutedContext result;
        try { result = await next().ConfigureAwait(false); }
        catch (Exception error)
        {
            diagnostics.Failure(2901, "native-controller-failure-not-handled-by-plugin", error, LogLevel.Error);
            throw;
        }
        var analysisStarted = Stopwatch.GetTimestamp();
        try
        {
            if (context.HttpContext.Items.TryGetValue(typeof(PreparedPolicy), out var prepared) && prepared is PreparedPolicy proof
                && result.Result is ObjectResult { Value: PlaybackInfoResponse { ErrorCode: null, PlaySessionId: not null } info }
                && info.MediaSources.Count(source => string.Equals(source.Id, proof.Source, StringComparison.OrdinalIgnoreCase)) == 1)
            {
                var offer = info.MediaSources.Single(source => string.Equals(source.Id, proof.Source, StringComparison.OrdinalIgnoreCase));
                proof = proof.WithNativeOffer(offer.TranscodingUrl);
                diagnostics.Write(LogLevel.Debug, 2404, "Playback credential binding; origin=native-authenticated-token-and-device-registry; credentialRef={CredentialRef}; credentialDeviceRef={CredentialDeviceRef}; negotiationDeviceEqualsCredentialDevice={DeviceMatch}; negotiationRef={NegotiationRef}",
                    PluginDiagnostics.Reference(proof.CredentialKey), PluginDiagnostics.Reference(proof.CredentialDevice), proof.Device == proof.CredentialDevice, PlaybackPolicySessions.SessionReference(info.PlaySessionId));
                var retained = _sessions.Remember(info.PlaySessionId, proof);
                diagnostics.Write(LogLevel.Debug, 2403, "Validated PlaybackInfo context; retained={Retained}; scoped-progressive-MKV=true; negotiationRef={NegotiationRef}; authenticatedDeviceOrigin=native-authentication-claim; authenticatedDeviceRef={AuthenticatedDeviceRef}; offeredDeviceOrigin=native-PlaybackInfo-TranscodingUrl-query; offeredDeviceState={OfferedDeviceState}; offeredDeviceRef={OfferedDeviceRef}; offeredEqualsAuthenticated={OfferedEqualsAuthenticated}",
                    retained, PlaybackPolicySessions.SessionReference(info.PlaySessionId), PluginDiagnostics.Reference(proof.Device),
                    proof.OfferedDeviceState, PluginDiagnostics.Reference(proof.OfferedDevice), proof.OfferedDevice is null ? "not-evaluated" : string.Equals(proof.OfferedDevice, proof.Device, StringComparison.Ordinal).ToString());
            }
            if (selected?.EnableDiagnostics == true && config.Diagnostics.PlaybackResponseAnalysis)
                AnalyzeResponse(result, selected, applied, diagnostics);
        }
        catch (Exception error) { diagnostics.Failure(2902, "response-analysis-failed-response-unchanged", error); }
        if (config.Diagnostics.PerformanceMeasurements)
            diagnostics.Write(LogLevel.Debug, 2990, "Plugin durationMs={DurationMs}; nativeControllerExcluded=true; applied={Applied}",
                preparationMs + Stopwatch.GetElapsedTime(analysisStarted).TotalMilliseconds, applied);
    }

    private async Task<bool> PrepareAsync(ActionExecutingContext context, TranscodingRule rule, DiagnosticOptions options, PluginDiagnostics diagnostics)
    {
        bool Skip(string reason)
        {
            diagnostics.Write(LogLevel.Debug, 2201, "Policy skipped; reason={Reason}; ruleId={RuleId}; original-request-retained", reason, rule.Id);
            return false;
        }
        if (!HostCompatible) return Skip("host-version-unsupported");
        if (!context.ActionArguments.TryGetValue("playbackInfoDto", out var value) || value is not PlaybackInfoDto dto || value.GetType() != typeof(PlaybackInfoDto)) return Skip("playback-dto-missing-or-derived");
        if (dto.DeviceProfile is null) return Skip("device-profile-missing");
        if (!RuleEvaluator.Identity(context.HttpContext.User, out _, out var userId)) return Skip("invalid-identity");
        T? Argument<T>(string name) where T : struct => context.ActionArguments.TryGetValue(name, out var v) && v is T typed ? typed : null;
        string? TextArgument(string name) => context.ActionArguments.TryGetValue(name, out var v) ? v as string : null;
        var requestedUser = Argument<Guid>("userId") ?? dto.UserId;
        var maxChannels = Argument<int>("maxAudioChannels") ?? dto.MaxAudioChannels;
        var enableTranscoding = Argument<bool>("enableTranscoding") ?? dto.EnableTranscoding ?? true;
        diagnostics.Write(LogLevel.Debug, 2202, "Requested playback limits; enableTranscoding={Transcoding}; allowVideoCopy={VideoCopy}; allowAudioCopy={AudioCopy}; maxChannels={MaxChannels}; liveStreamPresent={Live}",
            enableTranscoding, Argument<bool>("allowVideoStreamCopy") ?? dto.AllowVideoStreamCopy ?? true,
            Argument<bool>("allowAudioStreamCopy") ?? dto.AllowAudioStreamCopy ?? true, maxChannels, !string.IsNullOrEmpty(TextArgument("liveStreamId") ?? dto.LiveStreamId));
        if (requestedUser.HasValue && requestedUser.Value != Guid.Empty && requestedUser != userId) return Skip("request-user-mismatch");
        if (!enableTranscoding) return Skip("request-transcoding-disabled");
        if (maxChannels.HasValue && maxChannels < rule.MaxAudioChannels) return Skip("request-channel-cap-below-rule");
        if (!string.IsNullOrEmpty(TextArgument("liveStreamId") ?? dto.LiveStreamId)) return Skip("request-live-stream-unsupported");
        var user = _users.GetUserById(userId);
        diagnostics.Write(LogLevel.Debug, 2200, "Effective native permissions; playback={Playback}; audioTranscode={AudioTranscode}; videoTranscode={VideoTranscode}; remux={Remux}",
            user?.HasPermission(PermissionKind.EnableMediaPlayback) ?? false, user?.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding) ?? false,
            user?.HasPermission(PermissionKind.EnableVideoPlaybackTranscoding) ?? false, user?.HasPermission(PermissionKind.EnablePlaybackRemuxing) ?? false);
        if (user is null || user.Id != userId) return Skip("user-not-found");
        if (!user.HasPermission(PermissionKind.EnableMediaPlayback)) return Skip("media-playback-denied");
        if (!user.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding)) return Skip("audio-transcoding-denied");
        if (!_encoder.CanEncodeToAudioCodec("ac3")) return Skip("ac3-encoder-unavailable");
        var itemId = Argument<Guid>("itemId");
        if (!itemId.HasValue || _library.GetItemById<BaseItem>(itemId.Value, user) is not Video video) return Skip("not-accessible-local-video");
        var querySource = TextArgument("mediaSourceId");
        var sourceId = querySource ?? dto.MediaSourceId;
        var sources = await _sources.GetPlaybackMediaSources(video, user, true, false, context.HttpContext.RequestAborted).ConfigureAwait(false);
        var source = SourceSelection.Resolve(sources, sourceId, out var sourceReason);
        diagnostics.Write(LogLevel.Debug, 2210, "Source resolution; idPresent={IdPresent}; selectionOrigin={Origin}; available={Available}; unique={Unique}; reason={Reason}",
            !string.IsNullOrEmpty(sourceId), querySource is not null ? "query" : !string.IsNullOrEmpty(dto.MediaSourceId) ? "body" : "single-source-fallback", sources.Count, source is not null, sourceReason);
        if (source is null) return Skip(sourceReason);
        diagnostics.Write(LogLevel.Debug, 2211, "Resolved local source; container={Container}; protocol={Protocol}; nativeVideoTranscodeFlag={NativeTranscode}; nativeDirectStreamFlag={NativeDirectStream}; audioPermissionCheckedSeparately=true",
            SourceSelection.Container(source.Container), source.Protocol.ToString(), source.SupportsTranscoding, source.SupportsDirectStream);
        var queryAudio = Argument<int>("audioStreamIndex");
        var audioIndex = queryAudio ?? dto.AudioStreamIndex;
        var audio = SourceSelection.Audio(source, audioIndex, out var audioReason);
        diagnostics.Write(LogLevel.Debug, 2212, "Audio resolution; indexPresent={IndexPresent}; origin={Origin}; resolvedIndex={Index}; codec={Codec}; channels={Channels}; reason={Reason}",
            audioIndex.HasValue, queryAudio.HasValue ? "query" : dto.AudioStreamIndex.HasValue ? "body" : source.DefaultAudioStreamIndex.HasValue ? "native-default" : "single-track-fallback",
            audio?.Index, PluginDiagnostics.Codec(audio?.Codec), audio?.Channels, audioReason);
        if (audio is null) return Skip(audioReason);
        if (rule.EnableDiagnostics)
        {
            var inputVideo = source.MediaStreams.FirstOrDefault(s => s.Type == MediaStreamType.Video);
            diagnostics.Write(LogLevel.Debug, 2300, "Selected audio; index={AudioIndex}; codec={Codec}; channels={Channels}; layout={Layout}; bitrate={Bitrate}; sampleRate={SampleRate}; preserveStereo={PreserveStereo}; preserveCompatible={PreserveCompatible}",
                audio.Index, PluginDiagnostics.Codec(audio.Codec), audio.Channels, PluginDiagnostics.SafeLabel(audio.ChannelLayout), audio.BitRate, audio.SampleRate, rule.PreserveStereo, rule.PreserveCompatibleAudio);
            diagnostics.Write(LogLevel.Debug, 2301, "Source video; codec={VideoCodec}; width={Width}; height={Height}; range={VideoRange}; dvProfile={DvProfile}; dvLevel={DvLevel}; dvCompatibilityId={DvCompatibilityId}; source-metadata-only=true",
                PluginDiagnostics.Codec(inputVideo?.Codec), inputVideo?.Width, inputVideo?.Height, inputVideo?.VideoRangeType.ToString(), inputVideo?.DvProfile, inputVideo?.DvLevel, inputVideo?.DvBlSignalCompatibilityId);
        }
        var forceReencode = !rule.PreserveCompatibleAudio && audio.Codec == "ac3";
        var transformStarted = Stopwatch.GetTimestamp();
        diagnostics.Write(LogLevel.Debug, 2399, "Native video streaming profiles; count={Count}; offeredAc3={Ac3Offered}",
            dto.DeviceProfile.TranscodingProfiles?.Count(p => p?.Type == DlnaProfileType.Video),
            dto.DeviceProfile.TranscodingProfiles?.Any(p => p?.AudioCodec?.Split(',').Any(c => c.Trim().Equals("ac3", StringComparison.OrdinalIgnoreCase)) == true));
        var position = 0;
        foreach (var path in dto.DeviceProfile.TranscodingProfiles?.Where(p => p is not null && p.Type == DlnaProfileType.Video).Take(8) ?? [])
            diagnostics.Write(LogLevel.Debug, 2398, "Offered video streaming profile; position={Position}; container={Container}; protocol={Protocol}; audio={Audio}; video={Video}; channelLimit={Channels}",
                ++position, SourceSelection.Container(path.Container), path.Protocol.ToString(), PluginDiagnostics.Codec(path.AudioCodec), PluginDiagnostics.Codec(path.VideoCodec), int.TryParse(path.MaxAudioChannels, out var cap) ? cap : (int?)null);
        var transformed = Ac3ProfileTransformer.Transform(dto.DeviceProfile, rule, forceReencode, out var profileReason);
        if (options.PerformanceMeasurements && rule.EnableDiagnostics)
            diagnostics.Write(LogLevel.Debug, 2992, "Profile transformation durationMs={DurationMs}", Stopwatch.GetElapsedTime(transformStarted).TotalMilliseconds);
        if (transformed is null) return Skip(profileReason);
        var credential = PlaybackCredential.Resolve(context.HttpContext.User, _devices, out var credentialReason);
        diagnostics.Write(LogLevel.Debug, 2405, "Negotiation credential verification; verified={Verified}; reason={Reason}; credentialRef={CredentialRef}", credential is not null, credentialReason, PluginDiagnostics.Reference(credential?.Key));
        if (credential is null) return Skip(credentialReason);
        RuleEvaluator.Identity(context.HttpContext.User, out var device, out _);
        context.HttpContext.Items[typeof(PreparedPolicy)] = new PreparedPolicy(userId, device, itemId.Value, source.Id, audio.Index, PlaybackPolicySessions.Fingerprint(rule))
            { CredentialKey = credential.Key, CredentialDevice = credential.Device };
        // Retain an authenticated context even when this track is preserved; later track selection is mutable.
        if (rule.PreserveStereo && audio.Channels <= 2) return Skip("stereo-preserved");
        if (rule.PreserveCompatibleAudio && audio.Codec == "ac3" && audio.Channels <= rule.MaxAudioChannels
            && audio.BitRate is > 0 && audio.BitRate <= rule.MaxAudioBitrate)
            return Skip("compatible-ac3-preserved-context-retained");

        if (rule.EnableDiagnostics && options.DeviceProfileChanges && diagnostics.Enabled(LogLevel.Debug))
        {
            var videoConstraintsSame = JsonSerializer.Serialize(dto.DeviceProfile.CodecProfiles.Where(p => p.Type == CodecType.Video))
                == JsonSerializer.Serialize(transformed.CodecProfiles.Where(p => p.Type == CodecType.Video));
            var videoOffersSame = dto.DeviceProfile.TranscodingProfiles!.Select(p => p.VideoCodec).SequenceEqual(transformed.TranscodingProfiles.Select(p => p.VideoCodec));
            diagnostics.Write(LogLevel.Debug, 2400, "Profile change; directProfilesBefore={Before}; directProfilesAfter={After}; addedAc3Guard=true; target={Target}; maxChannels={MaxChannels}; maxBitrate={MaxBitrate}; videoConstraintsUnchanged={VideoConstraints}; videoOffersUnchanged={VideoOffers}; forceAudioReencode={ForceAudioReencode}",
                dto.DeviceProfile.DirectPlayProfiles.Length, transformed.DirectPlayProfiles.Length, rule.TargetCodec, rule.MaxAudioChannels, rule.MaxAudioBitrate, videoConstraintsSame, videoOffersSame, forceReencode);
            if (diagnostics.Enabled(LogLevel.Trace))
                diagnostics.Write(LogLevel.Trace, 2401, "Sanitized profiles; before={Before}; after={After}", JsonSerializer.Serialize(PluginDiagnostics.Profile(dto.DeviceProfile)), JsonSerializer.Serialize(PluginDiagnostics.Profile(transformed)));
        }
        diagnostics.Write(LogLevel.Information, 2402, "ATC applied; ruleId={RuleId}; offeredTarget={Target}; maxChannels={MaxChannels}; maxBitrate={MaxBitrate}; actualEncoder=not-yet-observed",
            rule.Id, rule.TargetCodec, rule.MaxAudioChannels, rule.MaxAudioBitrate);
        // Only narrow native audio-copy permission when the user explicitly requests re-encoding of AC3.
        // Video permissions and all other action arguments remain untouched.
        if (forceReencode) context.ActionArguments["allowAudioStreamCopy"] = false;
        dto.DeviceProfile = transformed;
        return true;
    }

    private static void AnalyzeResponse(ActionExecutedContext context, TranscodingRule rule, bool applied, PluginDiagnostics diagnostics)
    {
        if (context.Exception is not null)
        {
            diagnostics.Failure(2502, "controller-returned-exception-response-not-modified", context.Exception);
            return;
        }
        if (context.Result is not ObjectResult { Value: PlaybackInfoResponse response })
        {
            diagnostics.Write(LogLevel.Debug, 2500, "Native response unavailable for analysis; result-not-modified=true");
            return;
        }
        diagnostics.Write(LogLevel.Debug, 2500, "Native PlaybackInfo offer; sources={Sources}; error={Error}; client-use-and-ffmpeg-not-observed=true",
            response.MediaSources.Count, response.ErrorCode?.ToString() ?? "none");
        foreach (var source in response.MediaSources.Take(8))
        {
            var query = QueryHelpers.ParseQuery(source.TranscodingUrl is null ? string.Empty : new Uri("http://local.invalid" + (source.TranscodingUrl.StartsWith('/') ? "" : "/") + source.TranscodingUrl).Query);
            string Get(string key) => query.TryGetValue(key, out var v) ? v.ToString() : string.Empty;
            int? Number(string key) => int.TryParse(Get(key), out var n) ? n : null;
            var audio = PluginDiagnostics.Codec(Get("AudioCodec"));
            var video = PluginDiagnostics.Codec(Get("VideoCodec"));
            var reasons = Enum.TryParse<TranscodeReason>(Get("TranscodeReasons"), out var r) ? r.ToString() : "unavailable";
            diagnostics.Write(LogLevel.Debug, 2501, "Native source offer; directPlay={DirectPlay}; directStream={DirectStream}; transcoding={Transcoding}; audioOffer={Audio}; videoOffer={Video}; channelLimit={Channels}; bitrate={Bitrate}; reasons={Reasons}; response-unchanged=true",
                source.SupportsDirectPlay, source.SupportsDirectStream, source.SupportsTranscoding, audio, video, Number("TranscodingMaxAudioChannels"), Number("AudioBitrate"), reasons);
            foreach (var track in source.MediaStreams.Where(s => s.Type is MediaStreamType.Audio or MediaStreamType.Video).Take(16))
                diagnostics.Write(LogLevel.Trace, 2504, "Native source metadata; type={Type}; index={Index}; codec={Codec}; channels={Channels}; bitrate={Bitrate}; metadata-not-encoded-output=true",
                    track.Type.ToString(), track.Index, PluginDiagnostics.Codec(track.Codec), track.Channels, track.BitRate);
            if (applied && !string.IsNullOrEmpty(source.TranscodingUrl) && (audio != "ac3" || Number("AudioBitrate") > rule.MaxAudioBitrate || Number("TranscodingMaxAudioChannels") > rule.MaxAudioChannels))
                diagnostics.Write(LogLevel.Warning, 2503, "Native offer differs from intended AC3 policy; ruleId={RuleId}; inspect-client-and-native-constraints; response-not-modified=true", rule.Id);
        }
    }
}
