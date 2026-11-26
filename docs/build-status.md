# Build- und Prüfstatus M0

Prüfdatum: **26.11.2026**, lokale Windows-Umgebung. Dieser Bericht beschreibt ausgeführte Prüfungen, keine geplanten oder angenommenen Testerfolge.

## Umgebung und Paketnachweis

| Bestandteil | Tatsächlich festgestellt |
| --- | --- |
| Betriebssystem | Windows 10.0.19045, win-x64 |
| .NET SDK | 9.0.201, MSBuild 17.13.13 |
| Testlaufzeit | .NET 9.0.12, VSTest 17.13.0 |
| Plugin-TFM | net9.0 |
| Direkte Hostpakete | Jellyfin.Controller **10.11.11**, Jellyfin.Model **10.11.11**, exakte Versionsintervalle |
| Framework | Microsoft.AspNetCore.App, im System bereits vorhanden |
| Paketintegrität | Beide NuGet-Lockfiles erzeugt und mit `--locked-mode` erfolgreich wiederhergestellt |
| Quellenzuordnung | Die heruntergeladenen Controller-/Model-Nuspecs verweisen auf Server-Commit `1fbd8739292cce610231be93daf43368733edf63` |

Kein SDK, Workload, Jellyfin-Server, FFmpeg oder Docker installiert. Restore hat die erforderlichen Build-/Testpakete aus NuGet bezogen. Produktive Jellyfin-Verzeichnisse wurden weder gelesen noch verändert. Die vier Referenzrepositories wurden getrennt vom Ergebnisrepository als Analyse-Checkouts abgelegt.

## Ausgeführte Abschlussprüfungen

```powershell
./scripts/build.ps1 -VerifyDeterminism
dotnet test Jellyfin.Plugin.AudioTranscodingControl.sln --configuration Release --no-build --no-restore --logger 'trx;LogFileName=m0.trx'
```

- Locked Restore: erfolgreich.
- Release-Build von Plugin und Tests: erfolgreich, **0 Warnungen / 0 Fehler**.
- Testlauf: **10 bestanden, 0 fehlgeschlagen, 0 übersprungen**. Sechs MVC-/Fehlerfälle plus vier Discovery-Fälle gegen echten StreamBuilder.
- Plugin-Ausgabeverzeichnis enthält genau eine DLL: `Jellyfin.Plugin.AudioTranscodingControl.dll`. Keine beigepackten Jellyfin-/ASP.NET-Host-DLLs.
- Zweiter vollständiger Plugin-Rebuild im selben Checkout mit identischer SHA256 erfolgreich:

```text
1AFD5D6CACEE081CFF160C8ED172769E90B9D48CA730C3D1F3E600C0A70AF006
```

Diese DLL-Prüfsumme ist ein lokaler Buildnachweis, **kein Release-Manifest-Checksum und kein veröffentlichtes Artefakt**. `IncludeSourceRevisionInInformationalVersion=false` hält die Informationsversion bei 0.0.1; Assembly-/Dateiversion sind 0.0.1.0. Die Prüfung behauptet keine Reproduzierbarkeit mit anderen SDKs oder Betriebssystemen.

Die separaten Skripte `build.ps1` und `test.ps1` wurden ebenfalls lokal ausgeführt. Die TRX-Datei liegt unter `tests/Jellyfin.Plugin.AudioTranscodingControl.Tests/TestResults/m0.trx` und ist wie alle Buildausgaben von Git ausgeschlossen. Beim erneuten Lauf meldet VSTest lediglich das Überschreiben dieser vorhandenen Ergebnisdatei.

## Was die Tests belegen

MVC/DI: globale Filterregistrierung über PostConfigure, scoped Auflösung, unveränderte Durchleitung für relevante und fremde Route, Erhaltung der normalen Modellvalidierung und von Downstream-Exceptions. Ein Testfilter zeigt die Veränderbarkeit gebundener Argumente vor einer synthetischen Controller-Action.

StreamBuilder: Ein manuell entworfenes Profil lässt AC3 5.1 und AAC-Stereo direkt spielen und erzeugt für die zwei inkompatiblen Mehrkanalfälle eine AC3-priorisierte HLS-URL mit sechs Kanälen und 640000 bit/s. Die Paketbibliothek ist echt; Profil, Medienquelle und Transcoderverfügbarkeit sind Testfixtures. Kein produktiver Profiltransformer wurde implementiert.

## Nicht ausgeführt / weiter offen

- Laden des Plugins im vollständigen Jellyfin-Server und Zugriff auf dessen echtes PlaybackInfoDto.
- Ausführung der gesamten HLS-/Progressiv-Kette bis zum tatsächlichen FFmpeg-Prozess.
- Native Rechteintegration, reale Encoder-/Muxer-Verfügbarkeit und Plugin-Reihenfolge im Host.
- Client-/Fire-TV-/Projektor-/AVR-Tests, ARC-Mehrkanalrouting, Audio-/Video-Copy und Dolby-Vision-/HDR-Erhaltung.

Die Entscheidung für Ansatz A ist eine belegte technische Grundlage mit isolierter experimenteller Unterstützung. Sie ist kein Nachweis eines bereits funktionierenden AC3-Plugins für die Zielhardware.

## Git und Veröffentlichung

Das Repository wird lokal auf `main` mit einem M0-Abschlusscommit abgeschlossen; der konkrete Commit-Hash und der anschließend geprüfte saubere Status stehen im Abschlussbericht der Unterhaltung. Es wird kein Remote eingerichtet und nichts gepusht. Kein Tag, GitHub-Release, automatischer Release-Workflow oder installierbarer Manifest-Eintrag wurde erstellt; `manifest.json` enthält `versions: []`.
