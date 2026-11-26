# Testplan

## M1: aktueller Prüfstand

**36 Tests bestanden**, einschließlich aller zehn vorhandenen M0-Fälle. Neue Tests verwenden das reale Jellyfin.Api-DTO, den echten Controller, MediaInfoHelper und StreamBuilder in MVC; Benutzer-/Medien-/Encoderinformationen sind dabei isolierte Stubs. Weitere Tests decken Match-Determinismus, doppelte/fehlende Identität, Originalprofil-/Rechteintegrität, Objektkopien, Idempotenz, Query-Vorrang, kleinere Bitratenlimits, Video-Copy-Verbote und sanitisiertes Fehlerlogging ab.

Ein gesonderter nativer Test lädt das Plugin über Jellyfins eigenen PluginManager, konfiguriert eine Wegwerf-Instanz auf Loopback und folgt ihrer TranscodingUrl bis zum realen FFmpeg-Output. Er prüft auch den ersten PlaybackInfo-Aufruf nach frischem Scan, AC3-/Stereo-DirectPlay, Wiederholung, Nichttreffer, `EnableTranscoding=false` und eine ausdrücklich direkte AAC-HLS-Kontrollanfrage. Das ist ein Server-/FFmpeg-Test; kein Test des tatsächlichen Fire-TV-Players, Dolby Vision oder Atmos. Vollständige Matrix: [m1-verification.md](m1-verification.md).

```powershell
./scripts/prepare-test-server.ps1 # einmalig, SHA256-geprüftes portables Testpaket
./scripts/test.ps1              # schreibt TestResults/tests.trx
python scripts/verify-streaming.py # expliziter nativer Test mit neu erzeugten Medien
```

Die folgenden M0-Ergebnisse und damaligen M1-Testziele bleiben als historische Ausgangsbasis erhalten.

## M0: ausgeführte Prüfungen

Ziel ist die riskante technische Anbindung, keine Getter-/Setter-Abdeckung. Die vorhandenen Tests laufen ohne Serverinstallation, Netzwerk-Listener oder FFmpeg. NuGet wird nur beim Restore benötigt.

| Gruppe | Testumfang | Aussagegrenze |
| --- | --- | --- |
| MVC-/DI-Vertrag | PostConfigure registriert den Filter trotz späterer regulärer MVC-Konfiguration; Servicefilter ist scoped | Kein vollständiger Jellyfin-Startup |
| Durchleitung | PlaybackInfo-ähnliche und fremde Route behalten Profil und Query-vor-Body-Limit | Synthetischer Controller, keine Jellyfin-Response |
| Gebundene Argumente | Testfilter ersetzt echtes DeviceProfile im synthetischen DTO und einen gebundenen Querywert vor Action-Aufruf | Testfilter wird nicht in die Plugin-DLL kompiliert; echte API-DTO-Adaption folgt in M1 |
| Fehlerverhalten | Ungültiger Body bleibt HTTP 400; Downstream-Exception wird unverändert weitergereicht, `next` exakt einmal | Kein Rechte-/Authentifizierungstest |
| Native StreamBuilder-Discovery | DTS 8ch und AAC 6ch → AC3-priorisierte HLS-URL mit 6/640000; AC3 6ch und AAC 2ch → DirectPlay | Vier manuelle Profilfixtures; Transcoder-Dummy meldet Verfügbarkeit; kein tatsächlicher Encoder-/Muxer- oder Videocopy-Nachweis |

Sechs MVC-/Fehler-Testfälle und vier StreamBuilder-Fälle, insgesamt zehn. Die Discovery-Fixtures setzen `EnableDirectStream=false` wie der untersuchte MediaInfoHelper. Ohne diese hostnahe Vorgabe kann die unabhängige StreamBuilder-API einen anderen DirectStream-Pfad wählen. Die Tests erzeugen kein Originalprofil aus einem Fire TV und implementieren keine Transformation.

Buildskript: Locked Restore, Release-Build mit Warnungen als Fehler und Prüfung, dass nur die eigene Plugin-DLL im Plugin-Ausgabeverzeichnis liegt. Optionaler zweiter Rebuild prüft identische SHA256 im selben Checkout mit demselben SDK. Das beweist keine Bytegleichheit über andere SDKs/Betriebssysteme hinweg.

Ausführen:

```powershell
./scripts/build.ps1 -VerifyDeterminism
./scripts/test.ps1
```

TRX-Datei: `tests/Jellyfin.Plugin.AudioTranscodingControl.Tests/TestResults/m0.trx` (ignoriertes Build-Artefakt). Der geprüfte Zustand wird in [build-status.md](build-status.md) festgehalten.

## M1: zuerst automatisieren

1. **Nicht-Eingreifen und Fehlerisolation:** deaktiviert, kein Match, fremde Route, unbekannter DTO-Typ, fehlendes Profil, ungültige Regel, fehlender Encoder. Identische Eingaben/Objekte bleiben erhalten. Ein interner Transformationsfehler darf keine Teilmutation und keinen zweiten Controller-Aufruf auslösen.
2. **Regelauswahl:** höhere Priorität, stabile Gleichstandsauflösung, UND-Matching, fehlende Identitätsfelder, globale Fallback-Regel ohne Geräteregeltreffer. Eine kleine gezielte Tabelle genügt.
3. **Profilvertrag:** Originalprofil unverändert, keine geteilten verschachtelten Arrays, wiederholte Transformation idempotent; nur notwendige Audiofelder unterscheiden sich. Query-/Body-Konflikte und niedrigere Clientgrenzen gesondert prüfen.
4. **Entscheidung und URL:** gematchtes Mehrkanal-AAC/DTS/EAC3, kompatibles AC3 und Stereo gegen echte Jellyfin-Model-API; Zielcodec, Limits, Audioindex, Flags und qualifizierte Optionen prüfen. Multi-Audioquelle nur einmal als gezielter Track-Auswahlfall.
5. **Rechte und Datenschutz:** Audio-Transcoding verboten, Video-Copy verboten und zu niedrige Bitrate bleiben wirksam. Sentinel-Tokens dürfen weder bei Erfolgsdiagnose noch Fehlern in Logs gelangen.

## M1: kompakte Integration und Hardwareprüfung

Keine kartesische Vollmatrix. Nacheinander ein kleiner Kernbestand, dessen Quelle, Audiolayout und Videoprofil bekannt sind:

| Fall | Erwartung / Messung |
| --- | --- |
| AC3 5.1, kompatibles Video | DirectPlay oder Audio-Copy; kein unnötiger AC3-Re-Encode |
| AAC Stereo, kompatibles Video | Stereo bleibt erhalten, unveränderte Audioentscheidung soweit kompatibel |
| Inkompatibler Mehrkanalton, 5.1 und einmal 7.1 | AC3 ≤6 Kanäle, ≤640000; Mapping am AVR prüfen |
| Einer der Mehrkanalfälle mit 4K HDR/DV | Tatsächliche Video-Copy-Zeile, Container und Bild-/HDR-/DV-Erkennung getrennt dokumentieren |
| Gleiches Medium, unpassendes Gerät oder Plugin aus | Vergleich zur unveränderten Jellyfin-Entscheidung |
| Audio-Transcoding-Recht entzogen | Keine Rechteumgehung; Ergebnis/Fehler/Copy des Core dokumentieren |

Für den Mehrkanalfall einmal Start, Seek vor/zurück, Tonspurwechsel AC3 ↔ inkompatibel und erneuten PlaybackInfo-Request durchführen. Getrennt erfassen, ob der Client lokal wechselt oder neu verhandelt. HLS-Master, Media-Playlist und Segmentanfrage müssen denselben Audiovertrag weiterführen. TS/fMP4 nur dann beide testen, wenn das reale Profil beide wählt oder ein konkreter Fehler dies verlangt.

Aufzuzeichnen sind ausschließlich bereinigte Eingangs-/Entscheidungsdaten, verwendete Regel, Quell-/Zielcodec, Kanalzahl, Bitrate, tatsächliche FFmpeg-Ausgabeparameter und Gerätebeobachtung. Kein API-Key oder Authorization-Header. Displaymeldung „Dolby Vision“ und Bildprüfung sind separate Nachweise von `codec:v copy`; Audioanzeige und hörbares Kanalrouting ebenfalls separat prüfen.

**Nicht in M0 ausgeführt:** Plugin-Laden im vollständigen Jellyfin-Server, echte API-DTO-Adaption, End-to-End-HTTP bis FFmpeg, Rechteintegration, Android-/Fire-TV-/Projektor-/AVR-Tests und Dolby-Vision-/HDR-Prüfungen.
