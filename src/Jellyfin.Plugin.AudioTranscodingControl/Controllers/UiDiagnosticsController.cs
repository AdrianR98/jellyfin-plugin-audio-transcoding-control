// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Plugin.AudioTranscodingControl.Diagnostics;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioTranscodingControl.Controllers;

/// <summary>Receives bounded, allowlisted UI diagnostics through native administrator authentication.</summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("AudioTranscodingControl/UiDiagnostics")]
public sealed class UiDiagnosticsController(ILogger<UiDiagnosticsController> logger) : ControllerBase
{
    private static readonly HashSet<string> Events = ["script-start", "handlers-registered", "handlers-removed", "page-show", "page-hide", "api-available", "api-start", "api-complete", "error", "configuration-processed", "rendered", "control", "changed", "validation", "save-complete", "reload", "initialization-start", "initialization-complete", "initialization-aborted", "duplicate-initialization-suppressed"];
    private static readonly HashSet<string> Steps = ["interface", "configuration", "devices", "users", "save", "save-configuration", "rule", "settings", "lifecycle", "diagnostics"];
    private static readonly HashSet<string> Codes = ["none", "script", "pageshow", "reload", "hidden", "disposed", "duplicate", "api-unavailable", "missing-element", "invalid-schema", "invalid-configuration", "invalid-response", "invalid-log-level", "http-error", "network-error", "javascript-error", "invalid", "ready", "loading", "saved", "add", "edit", "duplicate-rule", "delete", "enable", "disable", "move-up", "move-down", "rule-action", "master", "logging", "language", "debug-preset", "copy-report", "save", "input", "change", "empty-selection", "unsaved-retained"];
    private static readonly HashSet<string> Kinds = ["none", "Error", "TypeError", "ReferenceError", "SyntaxError", "RangeError", "AbortError", "NetworkError"];
    private static readonly HashSet<string> FrameNames = ["initialize", "load", "request", "save", "normalize", "renderRules", "renderEditor", "collectEditor", "validate", "find", "apiClient", "listen", "external"];
    private static readonly HashSet<string> Elements = ["none", "ATCForm", "atcLanguage", "atcMaster", "atcStatus", "atcAdd", "atcReload", "atcRules", "atcWarnings", "atcEditor", "atcName", "atcEnabled", "atcAllDevices", "atcAllUsers", "atcDevices", "atcUsers", "atcCodec", "atcChannels", "atcBitrate", "atcPreserve", "atcStereo", "atcRuleDiagnostics", "atcLogging", "atcLogLevel", "atcLogMatching", "atcLogProfiles", "atcLogResponse", "atcLogPerformance", "atcDebugPreset", "atcSave", "atcReport", "atcCopyReport"];

    /// <summary>Writes only validated scalar codes; arbitrary client messages, payloads and URLs are never logged.</summary>
    /// <param name="batch">A bounded diagnostic batch.</param>
    /// <returns>No content, or a payload validation error.</returns>
    [HttpPost]
    [RequestSizeLimit(16384)]
    public IActionResult Post([FromBody] UiDiagnosticBatch batch)
    {
        if (batch.SessionId == Guid.Empty || batch.Events is null || batch.Events.Length is < 1 or > 32
            || batch.Events.Any(e => e is null || !Events.Contains(e.Event) || !Steps.Contains(e.Step) || !Codes.Contains(e.Code)
                || !Kinds.Contains(e.Kind) || !Elements.Contains(e.Element) || e.Sequence is < 1 or > 1000000 || e.Status is < 0 or > 599
                || e.DurationMs is < 0 or > 3600000 || e.Count is < 0 or > 100000 || e.Schema is < 0 or > 1000
                || e.Frames is null || e.Frames.Length > 6 || e.Frames.Any(f => !ValidFrame(f))))
            return BadRequest();

        // Independent of persisted config: bootstrap errors must be visible before configuration can load.
        var diagnostics = new PluginDiagnostics(logger, new() { Level = "Information" }, batch.SessionId.ToString("N"));
        foreach (var entry in batch.Events)
            diagnostics.Write(entry.Event == "error" ? LogLevel.Warning : LogLevel.Information, entry.Event == "error" ? 3101 : 3100,
                "[ATC:UI] event={UiEvent}; step={Step}; code={Code}; sequence={Sequence}; status={HttpStatus}; durationMs={DurationMs}; count={Count}; schema={Schema}; uiVersion=0.2.1.0; errorKind={ErrorKind}; missingElement={MissingElement}; safeStack={SafeStack}",
                entry.Event, entry.Step, entry.Code, entry.Sequence, entry.Status, entry.DurationMs, entry.Count, entry.Schema, entry.Kind, entry.Element, string.Join(" <- ", entry.Frames));
        return NoContent();
    }

    private static bool ValidFrame(string? frame)
    {
        if (frame is null || frame.Length > 60) return false;
        var parts = frame.Split(':');
        return parts.Length == 3 && FrameNames.Contains(parts[0])
            && int.TryParse(parts[1], out var line) && line is >= 0 and <= 999999
            && int.TryParse(parts[2], out var column) && column is >= 0 and <= 999999;
    }
}

/// <summary>A bounded diagnostics envelope. Contains no identity, credentials or arbitrary text.</summary>
public sealed class UiDiagnosticBatch
{
    /// <summary>Gets or sets the random page correlation UUID.</summary>
    public Guid SessionId { get; set; }
    /// <summary>Gets or sets at most 32 events.</summary>
    public UiDiagnosticEvent[] Events { get; set; } = [];
}

/// <summary>Controlled event codes and numeric metadata; no raw exception or HTTP payload fields.</summary>
public sealed class UiDiagnosticEvent
{
    /// <summary>Gets or sets the allowlisted event.</summary>
    public string Event { get; set; } = "";
    /// <summary>Gets or sets the allowlisted step.</summary>
    public string Step { get; set; } = "interface";
    /// <summary>Gets or sets the allowlisted reason/control code.</summary>
    public string Code { get; set; } = "none";
    /// <summary>Gets or sets the controlled exception kind.</summary>
    public string Kind { get; set; } = "none";
    /// <summary>Gets or sets an allowlisted missing DOM ID, or none.</summary>
    public string Element { get; set; } = "none";
    /// <summary>Gets or sets the page sequence.</summary>
    public int Sequence { get; set; }
    /// <summary>Gets or sets a status, or zero when unavailable.</summary>
    public int Status { get; set; }
    /// <summary>Gets or sets the elapsed request time.</summary>
    public int DurationMs { get; set; }
    /// <summary>Gets or sets a scalar item count.</summary>
    public int Count { get; set; }
    /// <summary>Gets or sets the configuration schema.</summary>
    public int Schema { get; set; }
    /// <summary>Gets or sets allowlisted function names with numeric line/column positions.</summary>
    public string[] Frames { get; set; } = [];
}
