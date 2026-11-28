# Project context

- Jellyfin Audio Transcoding Control, namespace `Jellyfin.Plugin.AudioTranscodingControl`, GPL-3.0-only. Keep template attribution and THIRD-PARTY-NOTICES.md.
- Current milestone M2.4 / version 0.2.4.0. Read docs/m2.4.md and README.md for current source resolution, streaming context and checkbox/validation diagnostics. Historical evidence remains milestone-specific. Pinned sources: docs/source-lock.json.
- Jellyfin 10.11.11 / net9.0, SDK 9.0.201. Real Jellyfin.Api reference from verified ignored .local/server; no host/test DLLs in release output. Native API assembly version differs from server version; exact source/host checks are intentional.
- Ordered AND/OR explicit-scope rules; first enabled match wins, no later fallback or implicit All. New installs disabled. Preserve migration selections and activation. Unknown identities/schema/profiles must safely bypass.
- Keep native PlaybackInfo POST action filter and request-local deep copying. Do not loosen user/video/subtitle restrictions. Native next() exactly once; never swallow controller errors. No Core/client/FFmpeg patching.
- Diagnostics use native ILogger and safe structured fields only. Never log tokens, URLs, raw device/user IDs, media paths, full DTOs or exception messages. Do not change global logging files. Read docs/m2.md for the labeled Information fallback for Debug/Trace.
- User authorized the public v0.2.4 playback/dashboard pre-release/catalog update; preserve all earlier tags/assets/catalog entries. No Actions/CI or new browser framework. Retain existing tests and add only focused checks. User performs production update and hardware tests.
- Native video-source SupportsTranscoding reflects video permission, not audio permission. The progressive MKV hook requires a validated bounded PlaybackInfo context, current matching rule/identities/permissions and native video-transcoding denial. No broad AAC replacement or permission/source-flag expansion. Existing native encoder jobs stay native.
- Do not install, configure or restart the production Jellyfin server. Private configuration/logs and generated artifacts stay ignored under .local/ or work/.

```powershell
./scripts/prepare-test-server.ps1
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
./scripts/package.ps1 -OutputDirectory ./work/package
```

The build's portable reference is not a separately installed production environment. Do not repeat full upstream analysis or native FFmpeg tests without a concrete need. Legacy manual practice scripts/docs describe 0.1; prefer the current native dashboard/catalog for 0.2.

- Playback contexts bind exact native user and verified credential HMAC/session to negotiated device. Allow only negotiated or the same credential’s verified registration claim; no raw credentials cached. Re-evaluate current ordered rules and actual source/track. Device grouping/names/activity are presentation only. Keep playback/UI changes in separate commits; no video-processing changes. Keep verification focused and avoid redundant runs (explicit user request).
