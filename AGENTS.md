# Projektkontext

- Name/Namespace: Jellyfin Audio Transcoding Control / `Jellyfin.Plugin.AudioTranscodingControl`.
- Stand: M1.1-Praxistestvorbereitung, 0.1.0, unveröffentlicht. Opt-in-Regel; exakte Device-ID plus Benutzer-ID, beide Schalter erforderlich. Aktueller Nachweis: `docs/m1-verification.md`; M0-Basis `202fd1e` bleibt auf main.
- Ziel: Jellyfin 10.11.11 (`net9.0`), Android TV 0.20.0, Windows. Gepinnte Quellen und Navigationslinks: `docs/sources.md`, `docs/source-lock.json`.
- Einstieg für Folgeschritte: zuerst die zum Auftrag passende Dokumentation lesen; kein erneutes Vollscan aller Upstreams ohne Versionswechsel oder konkreten Widerspruch.
- Ansatz A implementiert: tatsächliches `Jellyfin.Api.Models.MediaInfoDtos.PlaybackInfoDto`, globaler `IAsyncActionFilter` vor nativer Action; Registrierung über `IPluginServiceRegistrator`/`PostConfigure<MvcOptions>`. API-Assembly aus SHA256-geprüftem portablem Server in `.local/`; keine Kopie im Plugin-Ausgabeverzeichnis. Host-Controller-Version und API-Source-Commit müssen passen; API-Assembly trägt im offiziellen Archiv überraschend Version 12.0.0.0, daher nicht mit Serverversion gleichsetzen.
- Enger Scope: lokale explizite Quelle, ausgewählte bekannte 6/8-Kanalspur, AC3/AAC/EAC3/TrueHD/DTS/DCA, vorhandene AC3-HLS-Angebote. Stereo/Mono, Remote/Live, fehlende/mehrdeutige Daten, kleinere Kanallimits und verweigerte Rechte unangetastet lassen. Native Metadatenabfrage darf wie der Controller probieren, damit der erste PlaybackInfo-Aufruf funktioniert.
- Direkte Streamingaufrufe ohne PlaybackInfo bleiben unverändert (native AAC-Ausgabe praktisch belegt). Keine Aussage, dass die echte Fire-TV-App geprüft wurde. Keine Garantie für DV/HDR/Atmos.
- Nicht passende Geräte, deaktiviertes Plugin, unbekannte DTO-Formen und interne Vorbereitungsfehler müssen unverändert durchlaufen. Vor Mutation vollständig vorbereiten und validieren. `next()` exakt einmal; Controllerfehler nicht verschlucken oder durch erneuten Aufruf wiederholen.
- Profile tief kopieren; keine global gespeicherten Capabilities mutieren. Keine Rechte, Video-/HDR- oder Untertitelgrenzen lockern. Stereo nicht hochmischen. Codec-Reihenfolge allein genügt nicht, siehe Analyse.
- Keine Core-/Client-/FFmpeg-Manipulation, kein Harmony. Keine produktive Installation oder Veröffentlichung ohne passenden Auftrag. M1.1 erlaubt Push des Feature-Branches und Draft-PR, keinen Merge/Release; main unverändert lassen. Keine Release-Workflows.
- Logging nur strukturierte erlaubte Diagnosefelder; niemals Header, Claims-Dumps, URLs, Tokens, Pfade oder komplette DTOs protokollieren.
- GPL-3.0-only. Template-Herkunft in `THIRD-PARTY-NOTICES.md` erhalten. DirectPlayForce ohne vollständige Lizenzgrundlage nur als gelesene Referenz; keinen Code kopieren.

## Lokale Befehle

```powershell
./scripts/prepare-test-server.ps1
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
python scripts/verify-streaming.py # optional, nativer Server nur auf Loopback, synthetische Medien, finally beendet eigenen Prozess
# Einzelbefehle:
dotnet restore Jellyfin.Plugin.AudioTranscodingControl.sln --locked-mode
dotnet build Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-restore
dotnet test Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-build --no-restore
```

SDK 9.0.201 laut `global.json`; Lockfiles versionieren. Abhängigkeiten nur gezielt aktualisieren. Tests auf riskante Logik/Host-Verträge konzentrieren. Echte-Controller-Tests verwenden Stub-Metadaten; nur das separate Streaming-Skript belegt Plugin-Laden bis FFmpeg. Vorhandene Prüfungen nach relevanten Änderungen laufen lassen. `bin/`, `obj/`, `TestResults/`, `.local/`, `work/` und native Logs nicht committen. Nur bereinigte Evidenz-JSONs in `docs/evidence/` versionieren.

Praxistest: docs/practice-test.md. scripts/manage-practice.ps1: Status/WhatIf lesend; tatsächliche Dateiarbeiten nur nach Freigabe und bei gestopptem Zielserver. Private Geräte-/Benutzerkonfiguration und Installationsplan ausschließlich unter .local/practice; niemals nach GitHub/Fixtures übernehmen. Neuer logging.json-Override betrifft nur die eigene Kategorie, bestehende Dateien niemals überschreiben. Paket 0.1.0 enthält nur eigene DLL plus Lizenz/Notice/Manifest. Keine Core-DLLs.
