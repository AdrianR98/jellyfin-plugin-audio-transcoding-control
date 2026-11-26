# Roadmap

## Aktueller M1-Stand

Der M1-Auftrag ist freigegeben und als eng begrenzter Prototyp implementiert; [native Server-/FFmpeg-Evidenz und Grenzen](m1-verification.md). Die zulässige Vereinfachung auf eine einzelne exakte Geräte-/Benutzerregel wurde genutzt. Globale Regeln, mehrere Prioritäten und Namensmatching sind nicht Bestandteil dieses Prototyps.

Der geforderte Einfluss auf den tatsächlichen Audioencoder ist nachgewiesen: erste PlaybackInfo-Anfrage → servergenerierte AC3-HLS-URL → echtes Segment mit Video-Copy/AC3 5.1/640000. Echte Android-TV-/Fire-TV-/ARC-/HDR-Prüfung bleibt offen. Direkte selbst erzeugte AAC-Streamingrequests umgehen den Ansatz A weiterhin. Ein eventueller zusätzlicher Streamingfilter benötigt einen eigenen Auftrag und konkrete Clientbelege.

Der folgende Ablauf ist die historische M0-Planung; er ist kein zusätzlicher Auftrag, jetzt M2/M3 umzusetzen. Nach dem lokalen Feature-Branch-Commit stoppen; kein Push, keine produktive Installation.

## M0 — Foundation und Discovery

Umfang: lokales Repository, Plugin-Template-Basis, net9.0 und exakt versionierte Jellyfin-Pakete, kompilierbarer inaktiver Plugin-/MVC-Rahmen, Quellcodeanalyse, Lizenzbefunde, gezielte Tests, lokaler Abschlusscommit. Keine produktive Installation, Veröffentlichung oder neue Systemkomponenten.

Der [Prüfstatus](build-status.md) nennt die ausgeführten Befehle und ihre Grenzen. M0 ist kein AC3-fähiges Produktrelease. **Nach M0 ist ein ausdrücklicher Auftrag für M1 erforderlich.**

## M1 — Eng begrenzter DeviceProfile-Eingriff

### Reihenfolge

1. **Realen Requestvertrag abgleichen.** In einer getrennten Testumgebung Jellyfin 10.11.11 und Android TV 0.20.0 verwenden. Bereinigtes Original-DeviceProfile, ausgewählte Quelle/Tonspur und vollständige FFmpeg-Zeile vergleichen; insbesondere die Abweichung 768000 gegenüber dem ermittelten 640000-Limit klären. Keine Roh-Header/Token-URLs in das Repository aufnehmen.
2. **DTO- und Identitätsadapter bauen.** Controller-/Action-/Parameter-Metadaten absichern, `DeviceProfile` typisiert aus der erwarteten Property lesen, Claims anhand der belegten Jellyfin-Namen auswerten. Nur POST-PlaybackInfo mit vorhandenem Bodyprofil und lokale Videomedien zunächst unterstützen. Fehlender/unbekannter Vertrag → unverändert.
3. **Konfiguration und deterministische Auswahl einführen.** Globaler Hauptschalter, separat aktivierbare globale Fallback-Regel, Geräteregeln mit expliziter Priorität, Match-Feldern und stabiler ID. Das Regelauflösungsverfahren aus [architecture.md](architecture.md) als Vertrag testen. Eine vollständige gewinnende Policy, kein unübersichtliches Zusammenmischen.
4. **Profilkopie und Transformation implementieren.** Tiefe Kopie aller betroffenen Strukturen, Audio-DirectPlay-/Codec-Bedingungen passend einschränken, Video-HLS-Transcodingprofile AC3-fähig begrenzen. Mono/Stereo und kompatible AC3-Spuren erhalten, Video-/HDR-/Untertitelgrenzen nicht lockern. Vorhandene kleinere Limits respektieren. Reines Audio, leere Profile, Live-TV/Remotequellen und unbekannte Layouts zunächst unangetastet lassen.
5. **Berechtigungen und sichere Anwendung absichern.** Host-Benutzer-/Medienrechte nicht verändern. Ohne benötigte Audio-Transcoding-Berechtigung oder verfügbaren Zielencoder keinen AC3-Plan erzwingen. Profiländerung erst nach vollständiger Validierung setzen. Action einmal aufrufen; Fehler/Abbruch nicht durch Retry im Filter verdecken.
6. **Gezielte Tests und Diagnose.** Regelauflösung, Nichttreffer, deaktiviert, Deep-Copy-Isolation, Idempotenz, widersprüchliche Query-/Body-Limits, AC3-Copy, Stereo, DTS/EAC3/AAC-Mehrkanal und verweigerte Rechte prüfen. Strukturierte Debug-Felder auf erlaubte Daten beschränken und mit Dummy-Geheimnissen auf Leaks prüfen.
7. **Testserver und Gerät durchgängig prüfen.** Plugin in isolierten Server laden; PlaybackInfo → Master → Playlist → Segment/FFmpeg prüfen. Danach Fire TV/Projektor/ARC/AVR mit einer kleinen repräsentativen Medienauswahl testen. Audio-Track-Wechsel und Seeking einschließlich neuer PlaybackInfo-Aufrufe dokumentieren. Eine produktive Installation ist ein eigener späterer Auftrag.

### Abnahmekriterien

- Ein deaktiviertes Plugin und nicht passende Geräte erhalten dieselben Entscheidungseingaben und dasselbe native Ergebnis wie ohne Eingriff.
- Die Ziel-Device-ID erhält für belegte inkompatible Mehrkanaltonfälle AC3, höchstens sechs Kanäle und höchstens 640000 bit/s; kleinere Limits bleiben wirksam.
- Kompatibles AC3 bleibt DirectPlay oder Audio-Copy, soweit Container/sonstige Anforderungen dies erlauben. Stereo wird nicht auf sechs Kanäle angehoben.
- Bei einem video-kompatiblen Prüffall ist die wirkliche FFmpeg-Zeile `-codec:v:0 copy`, Audio entsprechend AC3 oder Copy. Das ist eine Aussage für den geprüften Fall, keine allgemeine HDR-/Dolby-Vision-Garantie.
- Keine Umgehung von Transcoding-Rechten, keine gemeinsamen mutierten Capabilities, keine unbeabsichtigten Änderungen von Video-/Untertitelrestriktionen, keine Zugangsdaten in Plugin-Logs.
- Wiederholte Requests/Seeking verschärfen das Profil nicht mehrfach; Trackwechsel, die lokal erfolgen, sind als technische Grenze transparent dokumentiert.
- Bei nicht sicher verarbeitbaren Inputs unverändertes Jellyfin-Verhalten; keine pauschale Response-Manipulation als verdeckter Ersatz.

### M1-Entscheidungspunkte

Falls das Originalprofil keinen sicher nutzbaren AC3-Pfad erlaubt, zunächst Gerätetest und Konfiguration klären. Clientunterstützung darf nicht allein aus der Zielhardware vermutet werden. Falls A trotz korrektem Profilvertrag nicht genügt, den konkreten Gegenbeleg dokumentieren und B/C gezielt neu bewerten. Keine stillschweigende Ausweitung auf FFmpeg-/Core-/Clientpatches.

## M2 — Konfiguration und breitere Pfade

Dashboard-Konfigurationsseite, validierte Codec-Präferenzen und Copy-Listen, sichere Capability-Fallbacks sowie gesondert LiveStream-/Remote-/Audio-Only-Pfade. Weitere Container/Layouts nur nach Bedarf und Beleg. Fehlerbehandlung für echte Playback-Retries bekommt einen eigenen begrenzten Zustandsvertrag.

## M3 — Distribution

Erst nach reproduzierbarer lokaler Prüfung und separatem Veröffentlichungsauftrag: Paketinhalt und Lizenzen prüfen, reale Release-Artefakte erzeugen, echte ABI/Prüfsumme/Zeitstempel und Download-URL in das Manifest eintragen, Update-/Rollback-Anleitung schreiben. Release-Automation erst dann einführen. M0 enthält keine solchen Workflows oder Release-Einträge.
