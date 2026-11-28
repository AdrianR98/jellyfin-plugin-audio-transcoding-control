// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Api.Controllers;
using Jellyfin.Api.Constants;
using Jellyfin.Data;
using Jellyfin.Data.Enums;
using System.Diagnostics;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Dlna;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jellyfin.Plugin.AudioTranscodingControl.Filters;

/// <summary>Narrows audio on negotiated progressive MKV requests when native video transcoding is forbidden.</summary>
public sealed class ProgressiveAudioActionFilter : IAsyncActionFilter
{
    private readonly IOptionsSnapshot<PluginConfiguration> _options;
    private readonly IUserManager _users;
    private readonly ILibraryManager _library;
    private readonly IMediaSourceManager _sources;
    private readonly IMediaEncoder _encoder;
    private readonly PlaybackPolicySessions _sessions;
    private readonly ILogger<ProgressiveAudioActionFilter> _logger;
    private readonly IDeviceManager _devices;

    /// <summary>Initializes the native request filter.</summary>
    /// <param name="options">Current configuration.</param>
    /// <param name="users">Native permissions.</param>
    /// <param name="library">Native item access.</param>
    /// <param name="sources">Native media sources.</param>
    /// <param name="encoder">Native codec capabilities.</param>
    /// <param name="sessions">Bounded validated PlaybackInfo contexts.</param>
    /// <param name="logger">Native logger.</param>
    /// <param name="devices">Native credential registry.</param>
    public ProgressiveAudioActionFilter(IOptionsSnapshot<PluginConfiguration> options, IUserManager users, ILibraryManager library,
        IMediaSourceManager sources, IMediaEncoder encoder, PlaybackPolicySessions sessions, ILogger<ProgressiveAudioActionFilter> logger, IDeviceManager devices)
    { _options = options; _users = users; _library = library; _sources = sources; _encoder = encoder; _sessions = sessions; _logger = logger; _devices = devices; }

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!(HttpMethods.IsGet(context.HttpContext.Request.Method) || HttpMethods.IsHead(context.HttpContext.Request.Method))
            || context.ActionDescriptor is not ControllerActionDescriptor descriptor || descriptor.ControllerTypeInfo.AsType() != typeof(VideosController)
            || descriptor.MethodInfo.Name is not (nameof(VideosController.GetVideoStream) or nameof(VideosController.GetVideoStreamByContainer)))
        { await next().ConfigureAwait(false); return; }
        var correlation = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object> { ["AtcCorrelationId"] = correlation });
        var log = new PluginDiagnostics(_logger, new(), correlation);
        var started = Stopwatch.GetTimestamp();
        var measure = false;
        try
        {
            var config = ConfigurationMigration.Normalize(_options.Value);
            log = new PluginDiagnostics(_logger, config.Diagnostics, correlation);
            measure = config.Diagnostics.PerformanceMeasurements;
            await Prepare(context, config, log).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception error) { log.Failure(3290, "progressive-preparation-error", error); }
        if (measure) log.Write(LogLevel.Debug, 3299, "Progressive plugin preparation durationMs={DurationMs}; nativeControllerExcluded=true", Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        // Never retry or swallow the native controller, and never modify its result.
        try { await next().ConfigureAwait(false); }
        catch (Exception error) { log.Failure(3291, "native-progressive-controller-failure", error, LogLevel.Error); throw; }
    }

    private async Task Prepare(ActionExecutingContext context, PluginConfiguration config, PluginDiagnostics log)
    {
        void Skip(string reason) => log.Write(LogLevel.Debug, 3201, "Progressive policy skipped; reason={Reason}; original-request-retained=true", reason);
        T? Arg<T>(string name) where T : struct => context.ActionArguments.TryGetValue(name, out var value) && value is T typed ? typed : null;
        string? Text(string name) => context.ActionArguments.TryGetValue(name, out var value) ? value as string : null;
        if (!PlaybackInfoActionFilter.IsHostCompatible) { Skip("progressive-host-unsupported"); return; }
        if (Text("container") is { Length: > 0 } container && container is not ("mkv" or "matroska")) { Skip("progressive-container-not-mkv"); return; }
        if (Arg<bool>("static") == true || !string.IsNullOrEmpty(Text("params")) || !string.IsNullOrEmpty(Text("liveStreamId")))
        { Skip("progressive-static-packed-or-live-request"); return; }
        if (Arg<EncodingContext>("context") is { } requestedContext && requestedContext != EncodingContext.Streaming)
        { Skip("progressive-non-streaming-context"); return; }
        if (Arg<int>("audioSampleRate") is { } sampleRate && sampleRate is not (32000 or 44100 or 48000))
        { Skip("progressive-audio-sample-rate-unsupported"); return; }
        var queryDevices = context.HttpContext.Request.Query["DeviceId"];
        var requestedDevice = Text("deviceId");
        var deviceClaims = context.HttpContext.User.FindAll(InternalClaimTypes.DeviceId).ToArray();
        var userClaims = context.HttpContext.User.FindAll(InternalClaimTypes.UserId).ToArray();
        var identityValid = RuleEvaluator.Identity(context.HttpContext.User, out var device, out var userId);
        log.Write(LogLevel.Debug, 3205, "Progressive identity fields; authenticatedIdentityValid={IdentityValid}; deviceClaimCount={DeviceClaimCount}; userClaimCount={UserClaimCount}; authenticatedDeviceOrigin=native-authentication-claim-header-or-token-record; authenticatedDeviceRef={AuthenticatedDeviceRef}; queryDeviceOrigin=ASP.NET-decoded-query; queryDeviceCount={QueryDeviceCount}; queryDeviceRef={QueryDeviceRef}; boundDeviceOrigin=native-VideosController-FromQuery; boundDevicePresent={BoundDevicePresent}; boundDeviceRef={BoundDeviceRef}; authorizationHeaderPresent={AuthorizationHeaderPresent}; legacyAuthorizationHeaderPresent={LegacyAuthorizationHeaderPresent}; negotiationRef={NegotiationRef}",
            identityValid, deviceClaims.Length, userClaims.Length, PluginDiagnostics.Reference(deviceClaims.Length == 1 ? deviceClaims[0].Value : null), queryDevices.Count, PluginDiagnostics.Reference(queryDevices.Count == 1 ? queryDevices[0] : null),
            !string.IsNullOrEmpty(requestedDevice), PluginDiagnostics.Reference(requestedDevice), context.HttpContext.Request.Headers.ContainsKey("Authorization"),
            context.HttpContext.Request.Headers.ContainsKey("X-Emby-Authorization"), PlaybackPolicySessions.SessionReference(Text("playSessionId")));
        if (!identityValid)
        {
            Skip(context.HttpContext.User.Identity?.IsAuthenticated != true ? "progressive-request-unauthenticated"
                : deviceClaims.Length == 0 ? "progressive-authenticated-device-missing"
                : deviceClaims.Length > 1 ? "progressive-authenticated-device-ambiguous"
                : userClaims.Length == 0 ? "progressive-authenticated-user-missing"
                : userClaims.Length > 1 ? "progressive-authenticated-user-ambiguous" : "progressive-authenticated-identity-invalid");
            return;
        }
        var credential = PlaybackCredential.Resolve(context.HttpContext.User, _devices, out var credentialReason);
        log.Write(LogLevel.Debug, 3208, "Progressive credential verification; verified={Verified}; reason={Reason}; credentialRef={CredentialRef}; credentialDeviceRef={CredentialDeviceRef}; nativeClaimEqualsCredentialDevice={DeviceMatch}; negotiationRef={NegotiationRef}",
            credential is not null, credentialReason, PluginDiagnostics.Reference(credential?.Key), PluginDiagnostics.Reference(credential?.Device), credential?.Device == device, PlaybackPolicySessions.SessionReference(Text("playSessionId")));
        if (credential is null) { Skip(credentialReason); return; }
        var proof = _sessions.Find(userId, credential.Key, Text("playSessionId"));
        var associated = proof is not null && !proof.Ambiguous && proof.CredentialDevice == credential.Device && (device == proof.Device || device == credential.Device);
        var rule = associated ? RuleEvaluator.SelectVerified(userId, proof!.Device, config) : null;
        log.Write(LogLevel.Debug, 3206, "Progressive context comparisons; negotiationRef={NegotiationRef}; lookupOrigin=authenticated-user-verified-credential-and-native-play-session; contextFound={Found}; authenticatedUserEqualsContext={UserMatch}; authenticatedDeviceEqualsContext={DeviceMatch}; itemEqualsContext={ItemMatch}; currentRuleEqualsContext={RuleMatch}; offeredDeviceState={OfferedDeviceState}; offeredDeviceRef={OfferedDeviceRef}; boundEqualsAuthenticated={ClaimMatch}; boundEqualsNativeOffer={OfferMatch}; queryEqualsBound={QueryMatch}",
            PlaybackPolicySessions.SessionReference(Text("playSessionId")), proof is not null,
            proof is null ? "not-evaluated" : (proof.User == userId).ToString(), proof is null ? "not-evaluated" : string.Equals(proof.Device, device, StringComparison.Ordinal).ToString(),
            proof is null ? "not-evaluated" : (proof.Item == Arg<Guid>("itemId")).ToString(), proof is null || rule is null ? "not-evaluated" : (proof.Fingerprint == PlaybackPolicySessions.Fingerprint(rule)).ToString(),
            proof?.OfferedDeviceState ?? "unavailable", PluginDiagnostics.Reference(proof?.OfferedDevice),
            requestedDevice is null ? "not-evaluated" : string.Equals(requestedDevice, device, StringComparison.Ordinal).ToString(),
            requestedDevice is null || proof?.OfferedDevice is null ? "not-evaluated" : string.Equals(requestedDevice, proof.OfferedDevice, StringComparison.Ordinal).ToString(),
            queryDevices.Count != 1 ? "not-evaluated" : string.Equals(queryDevices[0], requestedDevice, StringComparison.Ordinal).ToString());
        if (string.IsNullOrEmpty(Text("playSessionId"))) { Skip("progressive-play-session-missing"); return; }
        if (!Guid.TryParse(Text("playSessionId"), out var sessionId) || sessionId == Guid.Empty) { Skip("progressive-play-session-invalid"); return; }
        if (proof is null) { Skip("progressive-playbackinfo-context-not-found"); return; }
        if (proof.Ambiguous) { Skip("progressive-context-ambiguous"); return; }
        if (!associated) { Skip("progressive-native-device-not-associated-with-credential-context"); return; }
        if (rule is null) { Skip("progressive-no-current-matching-rule"); return; }
        if (proof.Item != Arg<Guid>("itemId")) { Skip("progressive-context-item-mismatch"); return; }
        log.Write(LogLevel.Debug, 3209, "Verified playback association; nativeDeviceEqualsNegotiated={SameDevice}; credentialDeviceVerified=true; currentRuleId={RuleId}; ruleChanged={RuleChanged}; negotiationRef={NegotiationRef}",
            device == proof.Device, rule.Id, proof.Fingerprint != PlaybackPolicySessions.Fingerprint(rule), PlaybackPolicySessions.SessionReference(Text("playSessionId")));
        if (queryDevices.Count > 1) { Skip("progressive-query-device-ambiguous"); return; }
        if (queryDevices.Count == 1 && (string.IsNullOrWhiteSpace(queryDevices[0]) || queryDevices[0]!.Length > 512 || queryDevices[0]!.Any(char.IsControl)))
        { Skip("progressive-query-device-invalid"); return; }
        if (queryDevices.Count == 0 ? requestedDevice is not null : !string.Equals(queryDevices[0], requestedDevice, StringComparison.Ordinal))
        { Skip("progressive-device-argument-query-mismatch"); return; }
        // The optional native job-routing DeviceId is not an authenticated claim. Accept only the exact
        // negotiated device ID or the exact transport value issued in THIS verified credential context.
        // No case folding, trimming, second decoding, query-based context lookup or claim substitution.
        if (requestedDevice is not null && !string.Equals(requestedDevice, proof.Device, StringComparison.Ordinal)
            && !(proof.OfferedDeviceState == "single" && string.Equals(requestedDevice, proof.OfferedDevice, StringComparison.Ordinal)))
        { Skip("progressive-device-neither-authenticated-nor-issued"); return; }
        log.Write(LogLevel.Debug, 3207, "Progressive device check accepted; negotiationRef={NegotiationRef}; reason={DeviceCheck}; authenticated-context-required=true; deviceArgumentsUnchanged=true",
            PlaybackPolicySessions.SessionReference(Text("playSessionId")), requestedDevice is null ? "optional-routing-device-absent" : string.Equals(requestedDevice, proof.Device, StringComparison.Ordinal) ? "exact-negotiated-device" : "exact-native-issued-transport-device");
        var user = _users.GetUserById(userId);
        log.Write(LogLevel.Debug, 3204, "Progressive permissions and request; playback={Playback}; audioTranscode={Audio}; videoTranscode={Video}; remux={Remux}; sourceIdPresent={Source}; audioIndexPresent={AudioIndex}; allowAudioCopy={AudioCopy}; allowVideoCopy={VideoCopy}",
            user?.HasPermission(PermissionKind.EnableMediaPlayback) ?? false, user?.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding) ?? false,
            user?.HasPermission(PermissionKind.EnableVideoPlaybackTranscoding) ?? false, user?.HasPermission(PermissionKind.EnablePlaybackRemuxing) ?? false,
            !string.IsNullOrEmpty(Text("mediaSourceId")), Arg<int>("audioStreamIndex").HasValue, Arg<bool>("allowAudioStreamCopy") ?? true, Arg<bool>("allowVideoStreamCopy") ?? true);
        if (user is null || !user.HasPermission(PermissionKind.EnableMediaPlayback) || !user.HasPermission(PermissionKind.EnableAudioPlaybackTranscoding))
        { Skip("progressive-audio-permission-denied"); return; }
        // In this fallback native EncodingHelper forces video copy. Do not make video capability guesses or expand permission.
        if (user.HasPermission(PermissionKind.EnableVideoPlaybackTranscoding) || Arg<bool>("allowVideoStreamCopy") == false)
        { Skip("progressive-native-video-copy-policy-required"); return; }
        if (!_encoder.CanEncodeToAudioCodec("ac3")) { Skip("progressive-ac3-encoder-unavailable"); return; }
        if (_library.GetItemById<BaseItem>(proof.Item, user) is not Video video) { Skip("progressive-item-not-accessible-video"); return; }
        var sources = await _sources.GetPlaybackMediaSources(video, user, false, false, context.HttpContext.RequestAborted).ConfigureAwait(false);
        var source = SourceSelection.Resolve(sources, Text("mediaSourceId"), out var sourceReason);
        if (source is null) { Skip(sourceReason); return; }
        if (string.IsNullOrEmpty(Text("container")) && source.Container is not ("mkv" or "matroska")) { Skip("progressive-inferred-container-not-mkv"); return; }
        if (!string.Equals(source.Id, proof.Source, StringComparison.OrdinalIgnoreCase)) { Skip("progressive-negotiated-source-mismatch"); return; }
        var audio = SourceSelection.Audio(source, Arg<int>("audioStreamIndex"), out var audioReason);
        if (audio is null) { Skip(audioReason); return; }
        log.Write(LogLevel.Debug, 3210, "Progressive selected track revalidated; initialIndex={InitialIndex}; currentIndex={CurrentIndex}; changed={Changed}; sourceBound=true; codec={Codec}; channels={Channels}; negotiationRef={NegotiationRef}",
            proof.AudioIndex, audio.Index, proof.AudioIndex != audio.Index, PluginDiagnostics.Codec(audio.Codec), audio.Channels, PlaybackPolicySessions.SessionReference(Text("playSessionId")));
        if (rule.PreserveStereo && audio.Channels <= 2) { Skip("progressive-stereo-preserved"); return; }
        var requestedCodecs = Text("audioCodec")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (requestedCodecs is not null && requestedCodecs.Any(c => c is not ("aac" or "ac3")))
        { Skip("progressive-audio-request-not-supported"); return; }
        var ceiling = Arg<int>("audioBitRate") ?? rule.MaxAudioBitrate;
        var bitrate = ConfigurationMigration.Bitrates.Where(n => n <= ceiling && n <= rule.MaxAudioBitrate).DefaultIfEmpty(0).Max();
        var channels = new[] { rule.MaxAudioChannels, audio.Channels!.Value, Arg<int>("audioChannels") ?? 8,
            Arg<int>("maxAudioChannels") ?? 8, Arg<int>("transcodingMaxAudioChannels") ?? 8 }.Min();
        if (bitrate == 0 || channels < 1) { Skip("progressive-invalid-or-too-small-audio-limits"); return; }
        var compatible = audio.Codec == "ac3" && audio.Channels <= channels && audio.BitRate is > 0 && audio.BitRate <= bitrate;
        var keepCopy = compatible && rule.PreserveCompatibleAudio && user.HasPermission(PermissionKind.EnablePlaybackRemuxing)
            && Arg<bool>("allowAudioStreamCopy") != false && Arg<bool>("enableAutoStreamCopy") != false;
        log.Write(LogLevel.Debug, 3200, "Negotiated MKV audio policy; sourceCodec={SourceCodec}; sourceChannels={SourceChannels}; requestedCodec={Requested}; target=ac3; channels={Channels}; bitrate={Bitrate}; preserveCopy={PreserveCopy}; remuxPermission={Remux}; nativeVideoTranscodePermission=false; videoArgumentsUnchanged=true; ruleId={RuleId}",
            PluginDiagnostics.Codec(audio.Codec), audio.Channels, PluginDiagnostics.Codec(Text("audioCodec")), channels, bitrate, keepCopy,
            user.HasPermission(PermissionKind.EnablePlaybackRemuxing), rule.Id);
        // All checks precede the request-local writes. Native authorization/encoder/muxer remain responsible for execution.
        context.ActionArguments["audioCodec"] = "ac3";
        context.ActionArguments["audioBitRate"] = bitrate;
        context.ActionArguments["audioChannels"] = channels;
        context.ActionArguments["maxAudioChannels"] = channels;
        context.ActionArguments["transcodingMaxAudioChannels"] = channels;
        if (!keepCopy) context.ActionArguments["allowAudioStreamCopy"] = false;
        log.Write(LogLevel.Information, 3202, "ATC applied to negotiated progressive MKV; target=ac3; channels={Channels}; bitrate={Bitrate}; encoderOutputNotYetObserved=true", channels, bitrate);
    }
}
