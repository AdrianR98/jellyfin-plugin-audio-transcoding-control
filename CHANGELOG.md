# Changelog

## 0.1.0 — M1.1, lokal paketiert, unveröffentlicht

- Installierbares lokales ZIP mit SHA256, ohne Host-/Entwicklungsbibliotheken.
- Sichere Installations-/Deaktivierungs-/Rollback-Werkzeuge mit WhatIf und Eigentumsprotokoll; Praxistestmatrix und private lokale Zielkonfiguration.
- Produktiver Server nur lesend untersucht; keine Installation und kein Neustart. Audio-Regellogik unverändert gegenüber M1.

## 0.0.1 — M1-Prototyp, weiterhin unveröffentlicht

- Echte PlaybackInfoDto-/Controller-Anbindung mit exakter Device-/Benutzerregel und standardmäßig deaktivierter Verarbeitung.
- Anfrageisolierte AC3-HLS-Profiltransformation mit Kanal-/Bitratengrenzen, Erhaltung von Video- und Benutzerbeschränkungen und sicherem Fallback.
- Sparsame strukturierte Diagnose mit pro Prozess anonymisierter Gerätezuordnung; keine Token-/URL-/Profil-Dumps.
- Tests gegen echten Jellyfin-Controller und MediaInfoHelper sowie isolierter nativer Server-/FFmpeg-Test mit synthetischen 4K-Medien.
- Serverseitig nachgewiesen: Video-Copy plus AC3 5.1/640000; AC3-/Stereo-DirectPlay bleibt erhalten. Direkte AAC-Streamingaufrufe umgehen weiterhin den PlaybackInfo-Eingriff.
- Keine produktive Installation, Veröffentlichung oder automatischer Push.

## 0.0.1 — M0, unveröffentlicht

- Lokale Repository-Struktur und Plugin-Rahmen auf Basis des offiziellen Templates.
- .NET 9 und Jellyfin-10.11.11-Paketreferenzen mit NuGet-Lockfiles.
- Inaktiver Plugin-Einstiegspunkt, Konfiguration und durchleitender globaler MVC-Filter.
- Versionsgebundene Analyse von Server 10.11.11 und Android TV 0.20.0.
- Architekturentscheidung für vorbereitende DeviceProfile-Anpassung in M1.
- Isolierte Tests für MVC-Anbindung und vier StreamBuilder-Discovery-Fälle.
- Lokale Build-/Testskripte, keine Release-Automation oder Serverinstallation.

Dieser Eintrag dokumentiert den Entwicklungsstand und bezeichnet kein veröffentlichtes Paket.
