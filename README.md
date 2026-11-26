# Jellyfin Audio Transcoding Control

Eigenständiges Server-Plugin zur geplanten gerätespezifischen Steuerung von Audio-Transcoding. Ziel ist unter anderem AC3 5.1 bis 640 kbit/s für inkompatible Mehrkanaltonspuren bei möglichst kopiertem Video und unveränderten offiziellen Clients.

**Stand: M0, Version 0.0.1, unveröffentlicht.** Das Plugin enthält einen kompilierbaren Einstiegspunkt, deaktivierte Konfiguration und einen vollständig durchleitenden MVC-Filter. Es verändert noch keine Wiedergabe, auch wenn `IsEnabled` gesetzt wird. Es gibt keine Konfigurationsoberfläche, Regelengine, Installation oder Veröffentlichung.

## Zielumgebung

- Jellyfin Server **10.11.11**, nativ unter Windows; Plugin-Zielframework **net9.0**.
- Jellyfin Android TV **0.20.0** auf Amazon Fire TV Stick.
- XGIMI Titan Noir Max → HDMI ARC → Sony STR-DH520, laut Projektvorgabe; Hardwarewirkung nicht überprüft.
- SDK-Basis **9.0.201**, NuGet `Jellyfin.Controller` und `Jellyfin.Model` exakt **10.11.11**. [Versionsbelege](docs/sources.md#s01).

## Lokal bauen und prüfen

Ein vorhandenes .NET-9-SDK und Zugriff auf NuGet bzw. ein gefüllter Paketcache werden vorausgesetzt. Die Skripte installieren kein SDK, keinen Server und kein FFmpeg. Restore lädt ausschließlich Build-/Testabhängigkeiten in den normalen NuGet-Cache.

```powershell
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
```

Gleichwertige Einzelbefehle im Repository-Verzeichnis:

```powershell
dotnet restore Jellyfin.Plugin.AudioTranscodingControl.sln --locked-mode
dotnet build Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-restore
dotnet test Jellyfin.Plugin.AudioTranscodingControl.sln -c Release --no-build --no-restore
```

Die Plugin-DLL entsteht unter `src/Jellyfin.Plugin.AudioTranscodingControl/bin/Release/net9.0/`. Jellyfin-Laufzeit-DLLs werden vom Plugin-Ausgabeverzeichnis ausgeschlossen. Das Build-Skript prüft dies. Keine dieser Dateien wird automatisch in einen Server kopiert.

## Ergebnis und Dokumentation

- [Architektur und Regelvertrag](docs/architecture.md)
- [Versionsgebundene Playback-Pipeline](docs/playback-pipeline.md)
- [Machbarkeit: Ansätze A, B und C](docs/technical-feasibility.md)
- [M1-Plan und spätere Meilensteine](docs/roadmap.md)
- [Gezielter Testplan](docs/test-plan.md)
- [Tatsächlicher Build- und Prüfstatus](docs/build-status.md)
- [Quellcodebelege mit Commit- und Zeilenlinks](docs/sources.md)

Bevorzugter Ansatz für M1: ein global registrierter Action-Filter passt **vor** `MediaInfoController.GetPostedPlaybackInfo` ausschließlich das anfrageeigene `DeviceProfile` eines passenden Geräts an. Jellyfin berechnet Entscheidung, Antwort und Streaming-URLs weiter selbst. Die Schlussfolgerung ist durch Quellcode und isolierte Tests gestützt; eine vollständige Jellyfin-/Geräteintegration wurde nicht durchgeführt.

`-codec:v:0 copy` allein beweist weder HDR- noch Dolby-Vision-Erhaltung. Container, Metadaten, Segmentierung und die gesamte Wiedergabekette bleiben zu prüfen.

## Struktur und Lizenz

`src/` enthält das Plugin, `tests/` isolierte MVC- und StreamBuilder-Tests, `docs/` die Analyse, `scripts/` lokale Prüfungen und `assets/` den vorgesehenen Platz für eigene Grafiken.

GPL-3.0-only; technische Grundlage ist das offizielle Jellyfin-Plugin-Template. [Herkunft und Lizenzprüfung](THIRD-PARTY-NOTICES.md). Aus DirectPlayForce wurde kein Code übernommen. `manifest.json` ist eine unveröffentlichte Katalogvorlage mit leerem `versions`-Array; kein installierbares Release.

M0 endet mit dem Foundation-Commit. M1 beginnt erst nach ausdrücklicher Freigabe.
