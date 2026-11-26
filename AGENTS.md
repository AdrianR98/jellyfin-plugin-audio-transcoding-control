# Projektkontext

- Name/Namespace: Jellyfin Audio Transcoding Control / `Jellyfin.Plugin.AudioTranscodingControl`.
- Stand: M0, 0.0.1, unveröffentlicht. Keine aktive Regelengine; Filter leitet ausnahmslos durch. M1 braucht ausdrückliche Freigabe.
- Ziel: Jellyfin 10.11.11 (`net9.0`), Android TV 0.20.0, Windows. Gepinnte Quellen und Navigationslinks: `docs/sources.md`, `docs/source-lock.json`.
- Einstieg für Folgeschritte: zuerst die zum Auftrag passende Dokumentation lesen; kein erneutes Vollscan aller Upstreams ohne Versionswechsel oder konkreten Widerspruch.
- Entscheidung: A bevorzugt, anfrageeigenes DeviceProfile vor `GetPostedPlaybackInfo` in globalem `IAsyncActionFilter`; Registrierung über `IPluginServiceRegistrator` und `PostConfigure<MvcOptions>`. Keine direkten Jellyfin.Api-Referenzen im M0-Projekt; M1 braucht einen abgesicherten DTO-Adapter.
- Nicht passende Geräte, deaktiviertes Plugin, unbekannte DTO-Formen und interne Vorbereitungsfehler müssen unverändert durchlaufen. Vor Mutation vollständig vorbereiten und validieren. `next()` exakt einmal; Controllerfehler nicht verschlucken oder durch erneuten Aufruf wiederholen.
- Profile tief kopieren; keine global gespeicherten Capabilities mutieren. Keine Rechte, Video-/HDR- oder Untertitelgrenzen lockern. Stereo nicht hochmischen. Codec-Reihenfolge allein genügt nicht, siehe Analyse.
- Keine Core-/Client-/FFmpeg-Manipulation, kein Harmony. Keine produktive Installation, zusätzlichen Komponenten oder Veröffentlichung ohne passenden Auftrag. Keine Release-Workflows in M0.
- Logging nur strukturierte erlaubte Diagnosefelder; niemals Header, Claims-Dumps, URLs, Tokens, Pfade oder komplette DTOs protokollieren.
- GPL-3.0-only. Template-Herkunft in `THIRD-PARTY-NOTICES.md` erhalten. DirectPlayForce ohne vollständige Lizenzgrundlage nur als gelesene Referenz; keinen Code kopieren.

## Lokale Befehle

```powershell
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
# Einzelbefehle:
dotnet restore Jellyfin.Plugin.AudioTranscodingControl.sln --locked-mode
dotnet build Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-restore
dotnet test Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-build --no-restore
```

SDK 9.0.201 laut `global.json`; Lockfiles versionieren. Abhängigkeiten nur gezielt aktualisieren. Tests auf risikobehaftete Logik und Host-Verträge konzentrieren, keine Getter-/Setter-Matrix. MVC-Probe-DTO und Transcoder-Dummy sind kein vollständiger Server-/FFmpeg-Nachweis. Nach relevanten Änderungen vorhandene gezielte Prüfungen laufen lassen. `bin/`, `obj/`, `TestResults/` nicht committen.
