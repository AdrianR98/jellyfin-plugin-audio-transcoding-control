# Changelog

## 0.2.2 — M2.2, real-device path compatibility pre-release

- Fix the video-source flag/audio-permission mismatch; resolve omitted source IDs only for a unique native source and distinguish source/track/profile reasons at Debug.
- Support offered HTTP MKV AC3 profiles and a bounded negotiated progressive-MKV audio hook for the native video-transcoding-denied fallback. Recheck rule/session/source/track/permissions; no global AAC replacement or video argument changes.
- Preserve compatible AC3 copy only within native remux/copy permission; support seven-channel source metadata for AC3 downmix and retain stereo preservation.
- Replace multiselects with searchable checkbox lists, counts/highlighting, client/version/activity details and retained unavailable selections.
- Add inline field errors, specific reasons, internal rule position/ID and selection counts. Capture actual HTTP status or explicit unknown, and saved/active rule totals.
- Native isolated MKV proof: HEVC copy, AC3 six-channel/640000 output, generated progressive URL, unmatched/unnegotiated AAC controls and permitted AC3 copy. Synthetic TrueHD input was six-channel; eight-channel TrueHD and seven-channel DTS use focused fixtures.
- Preserve schema 2/GUID/earlier releases. No production changes, new dependencies or CI.

## 0.2.1 — M2.1, dashboard hotfix pre-release

- Fix actual Jellyfin Web 10.11.11 incompatibility: the global legacy ApiClient has no getDevices method; use its authenticated ajax/getUrl transport for GET /Devices.
- Recoverable, step-specific initialization with independent handlers, stale-request guards, visible disabled states and Reload after failure. Preserve schema 2 configurations and rules.
- Add the native Audio Transcoding Control dashboard entry under Plugins using PluginPageInfo.
- Add early [ATC:UI] console/report diagnostics and a bounded native administrator-only logging bridge, with controlled codes and sanitized method frames rather than raw messages, HTTP payloads or identifiers.
- Verify native Web 10.11.11 initialization, rule controls, selections, save/reload, sidebar navigation, reopen, copy report and injected request failures on a disposable local server. Existing tests retained; one focused endpoint validation check added.
- No production server changes, dependency additions or CI/browser framework.

## 0.2.0 — M2, experimental pre-release

- Ordered independent AC3 rules with device/user multiselection, explicit All options, first-match priority and safe identity validation.
- Schema 2 and one-time migration preserving legacy switches and exact selections; new installations remain disabled.
- Native English/German dashboard: rule editing, duplication, reordering, missing selections, overlap warnings and save validation.
- Configurable AC3 channel/bitrate limits and original AC3/mono/stereo preservation, retaining native permission and video constraints.
- Hot-reloaded structured native diagnostics, request correlation, stable event IDs, category controls, safe profile snapshots and read-only native offer analysis.
- Debug/Trace diagnostics remain visible through labeled Information entries when the global logger accepts only Information.
- Reproducible versioned package and public catalog entry, retaining 0.1.0.0 as a fallback.
- Release build and existing tests plus five focused checks passed. Native update/dashboard and hardware playback validation remain user-owned; no production changes were performed.

## 0.1.0 — M1 / M1.1, experimental pre-release

- Native PlaybackInfo integration and opt-in exact device/user AC3 policy.
- Deep profile copying, native user/video constraints and safe bypass paths.
- Isolated native server/FFmpeg evidence: HEVC video copy with AC3 six-channel / 640000 bit/s output; direct AAC streaming bypass also demonstrated.
- Private controlled installation/rollback preparation and practical test matrix.
- Published package and catalog entry retained unchanged by M2. Real Fire TV/receiver/HDR/Dolby Vision behavior was not established.

## M0 — project foundation

- GPL plugin template, pinned Jellyfin 10.11.11 / .NET 9 references, source analysis and isolated MVC/StreamBuilder discovery tests.
