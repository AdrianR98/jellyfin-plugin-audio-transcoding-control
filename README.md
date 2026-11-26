# Jellyfin Audio Transcoding Control

Eigenständiger Server-Plugin-Prototyp zur gerätespezifischen Steuerung von Audio-Transcoding. Unterstützte Mehrkanaltonspuren können serverseitig auf AC3 mit höchstens sechs Kanälen und 640 kbit/s begrenzt werden.

**Stand: M1-Prototyp, Version 0.0.1, unveröffentlicht.** Die Regel ist standardmäßig aus und gilt ausschließlich für ein exakt konfiguriertes Device-ID-/Benutzer-ID-Paar. Ein isolierter nativer Jellyfin-Server hat den vollständigen Pfad bis zu einem echten 4K-HEVC/AC3-HLS-Segment nachgewiesen. Die echte Android-TV-/Fire-TV-/ARC-Kette wurde nicht getestet. [Nachweise und Grenzen](docs/m1-verification.md).

## Zielumgebung

- Jellyfin Server **10.11.11**, nativ unter Windows; Plugin-Zielframework **net9.0**.
- Jellyfin Android TV **0.20.0** auf Amazon Fire TV Stick.
- XGIMI Titan Noir Max → HDMI ARC → Sony STR-DH520, laut Projektvorgabe; Hardwarewirkung nicht überprüft.
- SDK-Basis **9.0.201**, NuGet `Jellyfin.Controller` und `Jellyfin.Model` exakt **10.11.11**. [Versionsbelege](docs/sources.md#s01).

## Lokal bauen und prüfen

Ein vorhandenes .NET-9-SDK und Zugriff auf NuGet bzw. ein gefüllter Paketcache werden vorausgesetzt. Der zusätzliche Assembly-Verweis verwendet das **echte** `PlaybackInfoDto` aus dem offiziellen portablen Windows-Paket 10.11.11. Dieses wird SHA256-geprüft ausschließlich unter `.local/` entpackt, ohne Dienst- oder Systeminstallation. Das Paket enthält auch FFmpeg; es wird kein vorhandenes FFmpeg verändert. Es werden keine neuen NuGet-Pakete eingeführt.

```powershell
# Einmalig ca. 168 MB; alternativ vorhandenes Paket mit -ArchivePath übergeben:
./scripts/prepare-test-server.ps1
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
# Optionaler echter Server-/FFmpeg-Test, ausschließlich mit synthetischen Medien:
python scripts/verify-streaming.py
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
- [Aktuelle M1-Implementierung, Build und Laufzeitnachweise](docs/m1-verification.md)
- [Historischer M0-Build- und Prüfstatus](docs/build-status.md)
- [Quellcodebelege mit Commit- und Zeilenlinks](docs/sources.md)

Der global registrierte Action-Filter passt **vor** `MediaInfoController.GetPostedPlaybackInfo` ausschließlich eine tiefe Kopie des anfrageeigenen `DeviceProfile` an. Jellyfin berechnet Entscheidung, Antwort und Streaming-URLs selbst. Der native Testserver hat die Plugin-Registrierung, den ersten PlaybackInfo-Aufruf, HLS-Master/Playlist/Segment und einen echten FFmpeg-Prozess ausgeführt:

```text
-codec:v:0 copy -codec:a:0 ac3 -ac 6 -ab 640000
```

## Regel konfigurieren

Keine Dashboard-Oberfläche. In einer ausdrücklich freigegebenen Testinstanz über Jellyfins vorhandene Plugin-Konfigurations-API `POST /Plugins/c48a5098-04e0-4de5-a46c-8adae788f91b/Configuration` oder deren XML-Konfigurationsdatei einstellen:

```json
{
  "IsEnabled": true,
  "EnableDebugLogging": false,
  "Ac3Rule": {
    "IsEnabled": true,
    "DeviceId": "EXAKTE-DEVICE-ID",
    "UserId": "00000000-0000-0000-0000-000000000000"
  }
}
```

Die leere Beispiel-Benutzer-ID muss durch die gewünschte echte ID ersetzt werden; sie trifft absichtlich niemanden. Beide Schalter und beide Identitätsfelder sind erforderlich. Device-IDs werden exakt und mit Groß-/Kleinschreibung verglichen; doppelte/fehlende Claims treffen nicht. Konfigurationsänderungen wirken mit dem nächsten Request-Snapshot.

Unterstützt: eindeutig ausgewählte lokale Videoquelle, explizite MediaSource-ID, bekannte 6-/8-Kanalspuren mit Codec AC3/AAC/EAC3/TrueHD/DTS/DCA und bereits AC3-fähige HLS-TS-/MP4-Transcodingprofile. Stereo/Mono, unbekannte Layouts, Live-/Remotequellen, fehlende Profile, nicht passende Geräte/Benutzer und deaktiviertes Transcoding laufen unverändert durch. Kompatibles AC3 bleibt direkt abspielbar bzw. kopierbar; vorhandene Video- und Rechtebeschränkungen bleiben erhalten.

**Grenze:** Direkte Streaming-URLs werden nicht umgeschrieben. Ein Client, der PlaybackInfo oder dessen `TranscodingUrl` umgeht und ausdrücklich AAC verlangt, kann weiterhin AAC bekommen; dieser Pfad wurde ebenfalls getestet. M1 garantiert keine Codec-Erzwingung für beliebige Clients/Requests.

`-codec:v:0 copy` allein beweist weder HDR- noch Dolby-Vision-Erhaltung. Container, Metadaten, Segmentierung und die gesamte Wiedergabekette bleiben zu prüfen.

## Struktur und Lizenz

`src/` enthält das Plugin, `tests/` isolierte MVC- und StreamBuilder-Tests, `docs/` die Analyse, `scripts/` lokale Prüfungen und `assets/` den vorgesehenen Platz für eigene Grafiken.

GPL-3.0-only; technische Grundlage ist das offizielle Jellyfin-Plugin-Template. [Herkunft und Lizenzprüfung](THIRD-PARTY-NOTICES.md). Aus DirectPlayForce wurde kein Code übernommen. `manifest.json` ist eine unveröffentlichte Katalogvorlage mit leerem `versions`-Array; kein installierbares Release.

M1 wird ausschließlich lokal auf einem Feature-Branch abgeschlossen. Kein automatisches Pushen, keine produktive Installation, kein Release. Die M0-Dokumente bleiben als gekennzeichnete historische Analyse erhalten; aktuelle M1-Nachweise stehen in `docs/m1-verification.md`.
