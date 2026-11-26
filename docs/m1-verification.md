# M1: Implementierung und tatsächlicher Nachweis

Prüfdatum: **26.11.2026**. Ausgangspunkt `202fd1eda93f978b2452adaa0eeb28837d46f62a`, Entwicklungsbranch `feature/m1-ac3-prototype`. `main` bleibt unverändert. Version 0.0.1 ist weiterhin unveröffentlicht.

## Ergebnis und Abnahmegrenze

**Der serverseitige Funktionsnachweis ist erbracht.** Ein unveränderter nativer Jellyfin-10.11.11-Server hat die Plugin-DLL über seinen PluginManager geladen, den Filter über dessen Service-Registrierung ausgeführt und nach einem echten PlaybackInfo-POST eine AC3-TranscodingUrl erzeugt. Ein HTTP-Testclient hat genau diese URL über Master-Playlist, Media-Playlist und Segment abgerufen. Der von Jellyfin gestartete FFmpeg-Prozess verwendete für eine geeignete synthetische 4K-Datei tatsächlich:

```text
-codec:v:0 copy -codec:a:0 ac3 -ac 6 -ab 640000
```

FFprobe des ausgelieferten Segments bestätigt **HEVC 3840×2160, AC3, sechs Kanäle, 640000 bit/s**. Diese Werte sind aus dem echten Prozess/Segment ermittelt, nicht aus einem nachgebauten FFmpeg-Kommando abgeleitet. [Bereinigte maschinenlesbare Evidenz](evidence/m1-native-streaming.json).

**Nicht nachgewiesen:** Verhalten der tatsächlich installierten Android-TV-/Fire-TV-App, ARC-/AVR-Mehrkanalausgabe, echte Atmos-/DTS-HD-Bitstreams und HDR/Dolby Vision. Der Test verwendet ein selbst erzeugtes sechs Sekunden langes SDR-Signal und einen HTTP-Testclient. Es ist ein vollständiger Server-/FFmpeg-Integrationstest, kein vollständiger Geräte-End-to-End-Test.

## Implementierte Dateien und Verantwortlichkeiten

| Datei | Verhalten |
| --- | --- |
| `Configuration/PluginConfiguration.cs`, `Ac3DeviceRule.cs` | Hauptschalter, Debug-Schalter und eine aktivierbare Regel mit exakter Device-ID und verpflichtender Benutzer-ID; standardmäßig aus |
| `Plugin.cs`, `PluginServiceRegistrator.cs` | Plugin-Instanz und Snapshot der aktuellen gespeicherten Konfiguration; scoped Servicefilter über PostConfigure/MvcOptions |
| `Policy/Ac3RuleMatcher.cs` | Exakter ordinaler Devicevergleich und Benutzerabgleich aus authentifizierten Claims; fehlende/doppelte Claims und unvollständige Konfiguration treffen nicht |
| `Filters/PlaybackInfoActionFilter.cs` | Nur POST der echten `MediaInfoController.GetPostedPlaybackInfo`-Action, echtes gebundenes PlaybackInfoDto, native Rechte-/Quellenauswertung vor Action; einmaliger abschließender Profiltausch |
| `Policy/Ac3ProfileTransformer.cs` | Tiefe JSON-Kopie des geprüften konkreten Profilgraphen, reine Audioänderungen und Ablehnung nicht unterstützter Pfade |
| `scripts/prepare-test-server.ps1` | Einmaliger, hashgeprüfter portabler Serverdownload, keine Dienst-/Systeminstallation |
| `scripts/verify-streaming.py` | Separater nativer Loopback-Testserver, synthetische Medien, HLS-/FFmpeg-/Segmentnachweis; beendet ausschließlich den selbst gestarteten Server |
| Neue Testdateien | Deterministische Regelauflösung, Objekt-/Rechteintegrität, echte DTO-/Controller-/Helper-Antworten, Fallback und Datenschutz |

Die Quelldateien liegen unter `src/Jellyfin.Plugin.AudioTranscodingControl/`; Tests unter `tests/Jellyfin.Plugin.AudioTranscodingControl.Tests/`. Es wurden keine neuen NuGet-Pakete benötigt. Die einzige zusätzliche Assembly-Referenz ist die tatsächlich benötigte Jellyfin.Api-Assembly, zur Laufzeit vom Host bereitgestellt.

## Tatsächlicher Eingriffspunkt und Vertrag

1. Globale Registrierung bleibt `IPluginServiceRegistrator` → `PostConfigure<MvcOptions>` → scoped Servicefilter. Der native Test bestätigt Pluginstatus `Active`, wirksame Konfigurationsänderungen über die bestehende Plugin-API und `ATC applied`-Diagnose; der Test trägt den Filter nicht selbst in den nativen Server ein.
2. Die Action wird über konkrete Controller-/Methodenmetadaten und HTTP POST identifiziert. Keine Pfad-Substring-Erkennung, kein Headerparser, kein Rewrite des HTTP-Bodys oder der Query.
3. Verwendet wird die wirkliche Klasse `Jellyfin.Api.Models.MediaInfoDtos.PlaybackInfoDto`, keine Kopie und kein gleichnamiger Ersatz. Die Version des Controller-Hostpakets und der API-Source-Commit werden zur Laufzeit geprüft; andere Formen laufen unverändert durch.
4. Die einzige Änderung am gebundenen Request ist `dto.DeviceProfile = transformed`. Query-vor-Body-Semantik, Benutzer-ID, ausgewählte Indizes, Copy-/Transcoding-Schalter, Authentifizierung und Rechte werden nicht verändert.
5. Die Regel verlangt die exakte konfigurierte Device-ID **und** Benutzer-ID aus dem authentifizierten Kontext. Es gibt im Prototyp genau eine Regel, daher keine Prioritätskonflikte. Globale Regeln, Namensmatching und Mehrregelauflösung bleiben spätere Arbeit.
   Die Device-ID bleibt eine vom Client gemeldete Kennung und ist kein kryptografischer Hardwarebeweis oder neues Berechtigungsmerkmal.
6. Das Native-User-Objekt muss Medienwiedergabe und Audio-Transcoding erlauben; `CanEncodeToAudioCodec("ac3")` muss wahr sein. Effektiv deaktiviertes Transcoding, ein abweichender angeforderter Benutzer oder ein kleineres Request-Kanallimit führen zum Fallback.
7. Medien- und Quellendaten werden mit den nativen, zugriffsgeschützten Diensten gelesen. Die Quellabfrage erlaubt wie der Controller das Probing, damit auch der erste PlaybackInfo-Aufruf nach einem noch unvollständigen Scan funktioniert. Nur eine exakt bezeichnete lokale File-Quelle ohne Live-/Remote-/Öffnungszustand wird behandelt. Keine Mediendatei wird verändert.
8. Nur eindeutig ausgewählte 6-/8-Kanalspuren mit Codec `ac3`, `aac`, `eac3`, `truehd`, `dts` oder `dca` werden transformiert. Mono/Stereo und ungewöhnliche/unbekannte Layouts bleiben unverändert. Fehlende Quellenauswahl bzw. fehlender Default-Audioindex führt zum unveränderten Core-Pfad.

Quellcodegrundlagen sind weiterhin [S02–S12](sources.md#s02), [S15–S16](sources.md#s15) und [S24](sources.md#s24); die M0-Recherche wurde nicht wiederholt. Zusätzliche Runtime-Belege stehen unten.

## Warum diese Profilfelder geändert werden

- **TranscodingProfiles:** Alle Video-/Streamingangebote müssen bereits explizit AC3 unterstützen und HLS mit TS oder MP4 anbieten. Sonst wird die gesamte Anpassung abgelehnt; es wird keine neue Decoderfähigkeit behauptet. Die Audio-Codecliste dieser Angebote wird auf ausschließlich `ac3` verengt. Dadurch kann die Serverauswahl nicht unbemerkt auf AAC zurückfallen. Video-Codeclisten, Container, Protokolle und übrige Optionen bleiben identisch.
- **MaxAudioChannels:** Die Zeichenkette in den betroffenen Transcodingprofilen wird auf `6` begrenzt. Bereits kleinere/ungültige Profilgrenzen sowie AC3-Codec-Kanalgrenzen unter sechs werden abgelehnt. Kleinere Request-Grenzen werden nicht überschrieben.
- **DirectPlayProfiles:** Bei einem passenden Mehrkanalrequest werden Video-DirectPlay-Angebote auf bereits erlaubtes AC3 verengt. Ein Eintrag ohne verbleibendes AC3 wird entfernt, anstatt seinen Codec-String zu leeren: leer bedeutet in Jellyfin Wildcard. So kann inkompatibles AAC/EAC3/TrueHD/DTS nicht weiterhin als direkt kompatibel gelten. Andere Medientypen bleiben erhalten. Die vorhandenen AC3-/Stereo-Pfade wurden separat geprüft.
- **CodecProfiles:** Ein `VideoAudio`-Profil für AC3 setzt `AudioChannels <= 6` und `AudioBitrate <= 640000`. Diese Bedingungen gelten auch bei reinem Audiofehler in einem Video. Nur `TranscodingProfile.Conditions` zu setzen würde diesen Fall nicht zuverlässig abdecken. Der Guard wird vorn eingefügt, vom StreamBuilder damit zuletzt angewandt und kann kleinere schon berechnete Grenzen nur weiter begrenzen. Wiederholte Transformation dupliziert ihn nicht. Ein vorhandenes 384000-Limit ist in einer echten Helper-Antwort erhalten geblieben.
- **Keine Videoänderung:** Video-Codecprofile einschließlich verschachtelter Bedingungen, Auflösung/HDR-/Untertitelangaben und Copy-Verbote werden übernommen. Es wird weder `VideoCodec=copy` erzwungen noch eine Videorestriktion gelockert. Ob Video kopiert werden kann, entscheidet Jellyfin; im geeigneten 4K-Test wurde Copy tatsächlich beobachtet.

Der gesamte Profilgraph wird isoliert geklont, einschließlich Container-/Untertitelprofilen und verschachtelten Bedingungen. Abgeleitete/unbekannte Profiltypen, null-Arrays/-Elemente oder unerwartete Strukturen werden abgelehnt. Keine Mutation gespeicherter Capabilities. Kein Schreibzugriff auf Benutzerobjekte. Bei Vorbereitungsfehlern bleibt das alte Profil erhalten; `next()` liegt außerhalb des Catch-Blocks und wird genau einmal ausgeführt. Controllerfehler und Request-Abbrüche werden nicht durch einen zweiten Aufruf verdeckt.

## Prüfmatrix und Beweisstärke

| Fall | Tatsächlich ausgeführter Nachweis |
| --- | --- |
| AC3 5.1 original | Native Testserver-PlaybackInfo bleibt DirectPlay; zusätzlich echter Controller mit Testmetadaten |
| EAC3 5.1 | Echtes DTO → echter Controller/MediaInfoHelper/StreamBuilder im MVC-TestHost, Metadatenstub → AC3-URL |
| TrueHD 7.1 / Atmos-Pfad | Derselbe Controllerpfad mit `truehd`/8-Kanal-Metadaten → AC3-URL; keine echte Atmos-Datei oder Atmos-Wiedergabe |
| DTS-HD-MA-Pfad 5.1/7.1 | Controllerpfad mit `dts`/6-/8-Kanal-Metadaten → AC3-URL; keine echten DTS-HD-Decodiertests |
| AAC 5.1 | Nativer Server und echte synthetische AAC-6ch-Datei → AC3-URL → echter FFmpeg-Prozess → AC3-Segment |
| Stereo | Native synthetische AAC-Stereospur bleibt DirectPlay; keine Kanalvergrößerung |
| Nicht passendes Gerät | Nativer Server behält seine ursprüngliche DirectPlay-Entscheidung; zusätzlicher deterministischer Matcher-Test |
| Fehlendes DeviceProfile | Echter Controller-TestHost nutzt unveränderten nativen Fallback; keine Profilinjektion |
| Transcoding deaktiviert | Native Request-Einstellung `EnableTranscoding=false` bleibt unverändert; Benutzerberechtigung im echten Controller-TestHost geprüft, kein ACL-/DB-End-to-End-Test |
| Trackwechsel / Wiederholung | AC3-/Stereo-/Mehrkanalindizes und wiederholter PlaybackInfo-POST geprüft; bei lokalem Trackwechsel ohne Request kein Eingriff |
| Video-/kleinere Bitratenlimits | Unveränderte Video-Profile, weitergereichtes `allowVideoStreamCopy=false`, 384000-Limit erhalten |
| Fehler/Integrität/Logging | Unverändertes Originalprofil, tiefe Kopien, idempotente Transformation, fehlende/mehrdeutige Identität, Sanitizing und einmalige Action-Ausführung getestet |

## HLS, direkte Requests und die echte App

Der positive native Test ruft **die vom Server gelieferte** TranscodingUrl ab; er setzt nicht nachträglich AC3 in die Streaming-Query ein. Master, Media-Playlist und Segment transportieren den gewünschten Audiovertrag, und FFprobe bestätigt das Ergebnis. Damit ist der Einfluss der Profiländerung auf den tatsächlichen Serverencoder nachgewiesen.

Der negative Kontrolltest ruft den HLS-Master dagegen direkt mit `AudioCodec=aac` und deaktivierter Audio-Copy-Erlaubnis auf, ohne zuvor dessen Profilentscheidung zu verwenden. Das echte Segment enthält weiterhin **AAC mit sechs Kanälen**. Mechanismus: `StreamingHelpers.GetStreamingState` liest die Query; ein neuer Streamingrequest besitzt kein ursprüngliches DeviceProfile und durchläuft nicht den PlaybackInfo-Filter. [S08](sources.md#s08).

Die untersuchten Android-TV-0.20.0-Pfade verwenden laut M0 normalerweise die zurückgegebene TranscodingUrl, wenn die Supportflags keinen DirectPlay-Weg zulassen. Das wurde hier nicht auf dem echten Gerät beobachtet. Sollte die reale App trotzdem eine eigene AAC-URL erzeugen, reicht dieser Prototyp für diesen Pfad nicht aus. Zuerst bereinigte Requestfolge und Playerpfad erfassen. Eine mögliche spätere Korrektur wäre ein zusätzlicher, streng geräte-/benutzer-/sitzungsgebundener Filter auf die **gebundenen Streaming-Action-Parameter**, konsistent über HLS-Master/Playlist/Segmente; das wäre ein separater Erweiterungsauftrag. Kein pauschales Query-Rewrite oder fingierte DirectPlay-Flags.

## Diagnostik

Optionales Debug-Logging enthält Entscheidung/Skip-Grund, einen HMAC-basierten Gerätealias mit zufälligem prozesslokalem Schlüssel, den bekannten Quellcodec/-kanalwert, relevante Angebotsmerkmale und die angebotenen AC3-Limits. `actualEncoder=not-yet-known` unterscheidet Angebot von tatsächlichem Encoding. Keine vollständigen Profile, Header, URLs, Tokens, Benutzer-/Device-IDs oder Pfade. Unerwartete Fehler erzeugen einen konstanten Warning-Code ohne Exception-Dump. Native Jellyfin-/FFmpeg-Logs sind separat und bleiben ausschließlich im ignorierten Testverzeichnis; nur bereinigte Evidenz wird versioniert.

## Abhängigkeiten und reproduzierbare Befehle

Das offizielle Paket stammt von [Jellyfins festem 10.11.11-Windows-Download](https://repo.jellyfin.org/files/server/windows/stable/v10.11.11/amd64/jellyfin_10.11.11-amd64.zip). Die beim Abruf festgestellte SHA256 ist im Vorbereitungsskript gepinnt. Das ist ein Integritätspin des untersuchten Downloads, keine separat verifizierte Herausgebersignatur.

| Artefakt | Festgestellter Wert |
| --- | --- |
| Archiv-SHA256 | `3B5D3E904415AC6BD2C0EE9FE93235B4145D41C4C85AE782D244AC9767F77374` |
| Jellyfin.Api.dll-SHA256 | `7841D8E03F974EB89BF7DF4A81F3DEF9CF38C094905EA7EBAB2699FEDC695E1A` |
| API-AssemblyVersion | `12.0.0.0` im offiziellen 10.11.11-Archiv; nicht gleichbedeutend mit Server-/Plugin-ABI |
| API-InformationalVersion | `12+1fbd8739292cce610231be93daf43368733edf63` |
| Controller-Hostassembly / Server | `10.11.11.0` / `10.11.11` |
| Enthaltenes FFmpeg | `7.1.4-Jellyfin` |
| SDK / Testlaufzeit | `9.0.201` / `.NET 9.0.12` |

Der abweichende API-Assemblyversionswert war ein konkreter Anlass für den zusätzlichen Binär-/Commitabgleich. Die API trägt trotzdem exakt den analysierten Source-Commit. Das Plugin prüft deshalb Hostversion plus Source-Commit, statt API- und Serverversion fälschlich gleichzusetzen.

```powershell
./scripts/prepare-test-server.ps1
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
python scripts/verify-streaming.py
```

Release-Build: **0 Warnungen / 0 Fehler**. Testlauf: **36 bestanden, 0 fehlgeschlagen, 0 übersprungen**, einschließlich aller zehn bisherigen Tests. Ein Clean-Build bestätigt, dass keine ungenutzten zusätzlichen Server-Assembly-Referenzen benötigt werden. Die Plugin-Ausgabe enthält nur die eigene Plugin-DLL; die API-Assembly wird nicht mitgeliefert. Wiederholter Rebuild im selben Checkout erzeugte dieselbe Plugin-SHA256:

```text
F0C0EEBE936630F7A5E756B04FA323691A0013CE3C70C6EE3EC0CF4B6860DB0F
```

Diese Prüfsumme bezeichnet die tatsächlich geladene lokale Test-DLL vor dem Abschlusscommit. Ein Build einer anderen Git-Revision kann durch eingebettete SourceLink-/PDB-Metadaten eine andere Prüfsumme haben, auch wenn der Programmcode gleich bleibt. Die Gleichheitsprüfung bezieht sich auf zwei Builds derselben Revision und Umgebung.

Das Integrationsskript verwendet nur die vorhandene Python-Standardbibliothek. Es erstellt einen neuen eindeutigen `.local/integration-*`-Ordner, bindet den Server nur an Loopback, schaltet AutoDiscovery und Remotezugriff ab, erzeugt nur neue Testmedien und beendet seinen Prozess in `finally`. Zugangsdaten existieren nur für die Wegwerf-Testinstanz. Keine produktiven Daten, keine Installation als Dienst, kein Push und keine Releases.
