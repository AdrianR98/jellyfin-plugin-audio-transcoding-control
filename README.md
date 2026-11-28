# Jellyfin Audio Transcoding Control

Experimental server plugin for **Jellyfin 10.11.11 / .NET 9**, version **0.2.3.0**. Ordered device/user rules narrow eligible native playback audio to AC3, with configurable channel and bitrate ceilings. New installations are disabled by default.

## Install or update from the plugin catalog

1. In Jellyfin Dashboard → Plugins → Repositories, add **Audio Transcoding Control** with this repository URL:

   ```text
   https://raw.githubusercontent.com/AdrianR98/jellyfin-plugin-audio-transcoding-control/main/manifest.json
   ```

2. Refresh the catalog, select **Jellyfin Audio Transcoding Control**, and install **0.2.3.0**. For an existing installation, use Jellyfin's regular plugin update mechanism.
3. Restart Jellyfin yourself when prompted. Open the plugin's configuration page in Dashboard → Plugins.
4. Keep the master switch off while selecting the intended devices and users. Enable the rule and master switch, then save. Configuration and logging changes apply to the next PlaybackInfo request without another restart.

[Source repository](https://github.com/AdrianR98/jellyfin-plugin-audio-transcoding-control) · [Pre-release and ZIP](https://github.com/AdrianR98/jellyfin-plugin-audio-transcoding-control/releases/tag/v0.2.3) · [Changelog](CHANGELOG.md)

## Configure

Open **Audio Transcoding Control** directly under **Plugins** in the dashboard sidebar, or through the installed plugin details. Both open the same page. The native English/German dashboard page supports creating, editing, duplicating, deleting, enabling and reordering rules. Device and user lists come from authenticated Jellyfin APIs. Unavailable saved selections remain visible and selected until explicitly removed. They never become “All”. Duplicate rules start disabled.

Searchable checkbox lists replace Ctrl-based multiselection. Counts/highlighting, full wrapping names, client/version/activity details and distinct local reference labels show what is selected. Filtering retains hidden selections. Validation highlights fields with specific inline guidance; diagnostics identify the internal rule/position and selection counts without logging raw device/user identifiers.

Within each axis, selections use **OR**; device and user axes use **AND**. The **first enabled matching rule wins**, without combining policies or trying later rules after a policy safety bypass. Empty selections match nobody unless the corresponding **All devices / All users** option is explicitly enabled. Invalid or ambiguous authenticated identities always bypass the plugin.

Each rule supports AC3 only, 1/2/6 maximum channels, supported AC3 bitrates up to 640000 bit/s, compatible AC3 preservation, mono/stereo preservation and detailed diagnostics. Default limits are six channels and 640000 bit/s; both preservation options default on. Overlap and full-shadow warnings explain priority conflicts. For exceptionally large selections, an overlap warning may be shown without proving full shadowing.

Legacy 0.1 configurations migrate once to schema 2, retaining the master switch, rule switch and exact device/user selections. A partially configured legacy rule remains safely nonmatching. See [M2 behavior and diagnostics](docs/m2.md).

## Diagnostics & Logging

The page offers Error, Warning, Information (default), Debug and Trace, with separate rule matching, profile changes, read-only response analysis and performance switches. The development preset enables Debug and all four categories; per-rule detailed diagnostics must also be enabled. Trace adds evaluated rule details and controlled profile snapshots.

Logs appear in **Dashboard → Logs**, in Jellyfin's configured log directory, under `Jellyfin.Plugin.AudioTranscodingControl` categories. Each request has one correlation ID and stable event IDs. No global logging files are modified. When the native logger filters Debug/Trace but accepts Information, diagnostics are emitted as **Information with their original Debug/Trace level explicitly labeled**. Global filters stricter than Information can still suppress them. Settings are refreshed on the next request.

Device/user references are pseudonymized per server process. Tokens, authenticated URLs, media paths, full request/response objects and exception messages are excluded. Profile output uses an allowlisted representation; stack traces contain method names without file paths. A native PlaybackInfo offer does **not** prove client selection or actual FFmpeg execution.

The **Sanitized interface diagnostics** section contains a copyable `[ATC:UI]` report even when configuration loading fails. Initialization, API requests, controls, validation and saves have step codes, durations and counts. Native API requests retain actual HTTP status; events without an HTTP response explicitly use `unknown`. Field-specific validation logs the internal rule ID/position, reason and counts, while successful saving logs saved/active rule totals and restored selections. Reload remains usable after failed loading; existing settings are not replaced.

A bounded native administrator-only endpoint forwards allowlisted UI diagnostics to `Jellyfin.Plugin.AudioTranscodingControl.Controllers.UiDiagnosticsController` (event IDs 3100/3101), independently of configuration loading. These development UI lifecycle events use native Information/Warning; global logger filters still apply. The server accepts no arbitrary messages, URLs or raw stack traces. Failed forwarding never blocks the UI; the local report remains available. The visible UI build and client report identify the executed resource; the server bridge distinguishes client UI version from its own build. The native page name changes with the plugin version to refresh its route/resource key. See [M2.3 identity and cache evidence](docs/m2.3.md).

## Known limitations

- Experimental and pinned to Jellyfin 10.11.11, including the examined native API source version. Other server versions are not supported.
- Progressive device checks retain exact authenticated identity/session binding. An unequal routing query is accepted only when it matches the transport value issued by the same native PlaybackInfo context. The exact productive Fire TV mismatch is still unobserved; unexplained differences safely bypass with detailed pseudonymized diagnostics. See [M2.3](docs/m2.3.md).
- Requires an accessible local video, a unique explicit source or exactly one source when omitted, a uniquely resolved known audio track, existing AC3-capable HLS TS/MP4 or HTTP MKV profiles and native audio-transcoding permission. Live/remote sources, malformed profiles, ambiguous metadata and tighter client channel limits safely bypass the policy.
- Profiles are deeply copied. Existing video constraints, subtitle capabilities, stream-copy restrictions and user permissions remain authoritative. Disabling compatible-AC3 preservation additionally narrows native audio-copy permission; it does not enable video conversion. No upmix is requested.
- A narrowly scoped progressive MKV hook corrects the native audio-only permission fallback when video transcoding is forbidden. It requires validated PlaybackInfo for the same authenticated device/user, native session, item/source/track and unchanged rule, then rechecks permissions. Unnegotiated/static/download/remote/live requests and unrelated callers remain native. Contexts are bounded and expire after 12 hours; restart/eviction or rule changes require fresh negotiation. Existing encoder jobs are not rewritten: start fresh playback after policy changes. Compatible AC3 copy requires existing remux/copy permission; with remux denied, AC3 is re-encoded instead of granting permission.
- AC3 does not preserve Atmos metadata. Fire TV playback, receiver output, seeking, track switching, HDR and Dolby Vision preservation remain **unverified on the real playback chain**. Video copy alone proves none of these.
- The dashboard and new selection/validation behavior were exercised on an isolated native Jellyfin/Web 10.11.11 instance. The user performs production update and practical playback tests. No production installation, settings change or restart was performed by this milestone.

## Disable, uninstall and roll back

Turn off the master switch or the individual rule and save to stop applying it to new PlaybackInfo requests. In Dashboard → Plugins, uninstall this plugin and restart Jellyfin when prompted. Existing sessions may continue using already generated URLs.

Before updating, back up this plugin's own XML configuration from the server's `plugins/configurations` directory, normally `Jellyfin.Plugin.AudioTranscodingControl.xml`. To roll back, uninstall 0.2 through the native mechanism, install catalog version **0.1.0.0**, and restore your own backed-up 0.1 configuration while Jellyfin is stopped. Schema 2 is unchanged through 0.2.3. Schema 2 rules cannot be read by 0.1; restoring the old file is required to recover the previous pair. Do not modify other plugins or server configuration. Keep backups and private identifiers outside Git.

## Build and checks

The portable download supplies a version-checked compile-time API reference under ignored `.local/`; it is not a required production environment or an additional installed server. No server is started by these commands:

```powershell
./scripts/prepare-test-server.ps1
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
./scripts/package.ps1 -OutputDirectory ./work/package
```

SDK 9.0.201 and locked dependencies are retained. The release ZIP contains only the plugin DLL (including its embedded dashboard), LICENSE, THIRD-PARTY-NOTICES.md and package-manifest.json. It excludes Jellyfin core DLLs, test libraries and private configuration. MD5 is used by the Jellyfin catalog; a SHA256 sidecar is also supplied. ZIP reproducibility is checked in the same build environment.

Existing M1 controller/FFmpeg evidence is historical: [M1 verification](docs/m1-verification.md). Current checks and event definitions: [M2.2](docs/m2.2.md). No CI or browser testing framework is included.

## License

**GPL-3.0-only**, see [LICENSE](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md). Host/framework and development dependencies are not bundled in the plugin ZIP. No DirectPlayForce code was copied.
