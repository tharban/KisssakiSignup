# Kissaki Cup Anmeldung - Design

Datum: 22.07.2026

## Kontext

Die App nimmt vereinsweise Anmeldungen fuer den 4. Kissaki Kendo Cup 2026 auf. Club-Manager sollen ohne Benutzerkonto ueber einen oeffentlichen Link melden koennen. Die Turnierorganisation prueft die Daten in einem geschuetzten Admin-Bereich und exportiert anschliessend CSV-Dateien fuer den KendoTournamentManager.

Quellen aus `tharban/Kissaki`:

- `Kissaki Cup/Details/Ausschreibung/README.md`
- `Kissaki Cup/Details/Ausschreibung/4. Kissaki Kendo Cup 2026/Ausschreibung 4. Kissaki Kendo Cup 2026 (deutsch - Ankuendigung).tex`
- `Kissaki Cup/Details/Anmeldung und KendoTournamentManager.md`
- `Kissaki Cup/attachments/Re_ Kendo Tournament Manager - Competitor registration.eml`

## Ziele

- Oeffentliches Anmeldeformular ohne Login fuer Club-Manager.
- Erfassung von Club, Ansprechpartner, Kaempfern, Einzelkategorien und Teams.
- Validierung der Kissaki-Kategorien aus der aktuellen Ausschreibung.
- Speicherung der Meldungen in einer kleinen lokalen Datenbank.
- Admin-Bereich zum Pruefen, Korrigieren und Exportieren.
- KTM-kompatible Exporte fuer Clubs, Teilnehmer und Teams.
- Einfache Installation auf einer kleinen Azure-Windows-Instanz mit IIS, ohne Docker.

## Nicht Teil Von Version 1

- Keine Online-Zahlung und kein Zahlungsabgleich.
- Keine direkte API-Anbindung an KendoTournamentManager.
- Keine Benutzerkonten fuer Club-Manager.
- Kein umfangreiches Rollen- oder Rechte-System.
- Keine automatische E-Mail-Pflicht. SMTP kann spaeter ergaenzt werden.

## Technische Basis

Die App wird als ASP.NET Core Razor Pages Web-App mit .NET 10 LTS gebaut. Razor Pages reichen fuer formulargetriebene Workflows, halten die Codebasis klein und lassen sich direkt auf Windows/IIS betreiben.

Die Datenhaltung erfolgt mit SQLite. Die Datenbank liegt als Datei auf dem Server, zum Beispiel unter `App_Data/kissaki-registration.sqlite`. Fuer diese Groesse ist kein separater Datenbankserver noetig. Regelmaessige Sicherung der SQLite-Datei reicht als Backup-Konzept.

Zielbetrieb ist eine kleine Windows-VM in Azure:

- Windows Server mit IIS
- .NET Hosting Bundle
- veroeffentlichter App-Ordner als IIS-Site
- HTTPS ueber IIS-Zertifikat oder vorgeschaltete Azure-/Domain-Konfiguration

Azure App Service Windows bleibt technisch moeglich, ist aber nicht das primaere Ziel, weil eine direkte Windows-Instanz fuer Henri einfacher zu verstehen und zu warten ist.

Wichtige Werte liegen in der Server-Konfiguration und nicht fest im Code:

- Admin-Passwort
- Datenbankpfad
- Turniername fuer KTM, Standard `Kissaki Cup 2026`
- Turniertag, Standard `2026-10-25`
- Anmeldeschluss, Standard `2026-10-11`
- Oeffentliche Anmeldung offen/geschlossen

## Benutzerbereiche

### Oeffentliche Anmeldung

Pfad: `/`

Solange die Anmeldung offen ist, zeigt die Startseite das Formular. Nach dem konfigurierten Anmeldeschluss oder bei manuell geschlossener Anmeldung zeigt sie eine geschlossene Meldeseite mit Kontakt-Hinweis.

Der Club-Manager fuellt eine mehrstufige Anmeldung aus:

1. Clubdaten
2. Ansprechpartner
3. Kaempferliste
4. Teams
5. Pruefen und Absenden

Nach dem Absenden zeigt die App eine Zusammenfassung und einen privaten Aenderungslink. Dieser Link enthaelt einen zufaelligen Token und erlaubt spaetere Korrekturen ohne Login. Wenn der Link verloren geht, muss der Club-Manager die Turnierorganisation kontaktieren.

### Aenderungslink

Pfad: `/edit/{token}`

Der private Link oeffnet die gespeicherte Clubmeldung. Der Token ist lang, zufaellig und nicht erratbar. Nach jeder Aenderung bleibt derselbe Link gueltig. Der Admin sieht, wann eine Meldung zuletzt geaendert wurde.

### Admin-Bereich

Pfad: `/admin`

Der Admin-Bereich ist mit einem Passwort geschuetzt. Fuer Version 1 reicht ein einzelnes Admin-Passwort aus der Server-Konfiguration. Nach Anmeldung kann der Admin:

- alle Clubmeldungen sehen
- Meldungen suchen und filtern
- Meldungen korrigieren
- unvollstaendige oder auffaellige Meldungen markieren
- Test- oder Spam-Meldungen deaktivieren
- CSV-Dateien fuer KTM exportieren

## Erfasste Daten

### Club

- Clubname, Pflichtfeld
- Stadt, Pflichtfeld
- Land, Pflichtfeld mit Standardwert `Germany`
- Adresse, optional
- E-Mail, optional fuer KTM-Clubexport
- Telefon, optional fuer KTM-Clubexport
- Website, optional fuer KTM-Clubexport

### Ansprechpartner

- Name, Pflichtfeld
- E-Mail, Pflichtfeld
- Telefon, optional
- Bemerkung, optional

Die Ansprechpartnerdaten werden nicht in die KTM-CSV geschrieben. Sie dienen nur der Turnierorganisation.

### Kaempfer

- Vorname, Pflichtfeld
- Nachname, Pflichtfeld
- ID-Karte / Passnummer, optional mit Warnung
- Geburtsjahr, Pflichtfeld fuer Kategoriepruefung
- Graduierung, Pflichtfeld
- Bogu vorhanden, ja/nein
- Einzelkategorien, Mehrfachauswahl
- Interne Bemerkung des Club-Managers, optional

Wenn keine ID-Karte angegeben wird, erzeugt die App beim Speichern eine stabile temporaere ID. Diese ID bleibt fuer denselben Kaempfer innerhalb der Meldung gleich und wird im Admin-Bereich sichtbar als Ersatz-ID markiert.

## Kategorien

Einzelkategorien:

- Ohne Bogu, alle Altersstufen
- 7-9 Jahre
- 10-12 Jahre
- 13-15 Jahre
- 16-18 Jahre
- Offene Kyu-Kategorie Erwachsene, Ue18 und nur Kyu-Traeger

Teamkategorien:

- Team Jugend, gemischt aus Kategorien 2-5
- Team Erwachsene, Ue18 mit 1 Dan-Traeger und 2 Kyu-Traegern

Fuer 2026 prueft die App die Alterskategorien ueber das Geburtsjahr:

- 7-9 Jahre: Jahrgang 2017-2019
- 10-12 Jahre: Jahrgang 2014-2016
- 13-15 Jahre: Jahrgang 2011-2013
- 16-18 Jahre: Jahrgang 2008-2010
- Erwachsene: Jahrgang 2007 und aelter

Die App prueft die Regeln als Warnungen und Sperren:

- Dan-Traeger koennen keine Erwachsenen-Einzelkategorie waehlen.
- Erwachsenen-Teams erwarten genau drei Mitglieder: zwei Kyu-Traeger und ein Dan-Traeger an dritter Stelle.
- Jugendteams erwarten drei Mitglieder mit der Aufstellung aus der Ausschreibung: Position 1 aus 7-9, Position 2 aus 10-12 oder 13-15, Position 3 aus 13-15 oder 16-18.
- Unvollstaendige Teams duerfen gespeichert werden, werden aber im Admin-Bereich klar als unvollstaendig markiert, weil die Ausschreibung Auffuellen vor Ort erlaubt.
- Ohne-Bogu kann zusaetzlich zu einer Alterskategorie gewaehlt werden.

## Datenmodell

Die App speichert die Daten in klar getrennten Tabellen:

- `Submissions`: eine Clubmeldung mit Status, Token, Erstell- und Aenderungszeit
- `Clubs`: Clubdaten fuer die Meldung
- `Contacts`: Ansprechpartnerdaten
- `Competitors`: Kaempferdaten inklusive normalisierter ID
- `CompetitorCategories`: ausgewaehlte Einzelkategorien
- `Teams`: Teamname, Teamtyp und Reihenfolge der Mitglieder
- `AdminNotes`: interne Pruefnotizen und Statuswechsel

Statuswerte fuer Meldungen:

- `New`
- `NeedsReview`
- `Reviewed`
- `Exported`
- `Disabled`

Nur nicht deaktivierte Meldungen werden exportiert. Der Export kann wahlweise alle aktiven oder nur gepruefte Meldungen ausgeben.

## KTM-Export

Alle Exporte sind semikolon-getrennte CSV-Dateien mit Headerzeile im KTM-Format. Die Dateien verwenden UTF-8 mit BOM, damit deutsche Namen in Excel und Windows-Tools sauber erscheinen.

### Clubs

Datei: `clubs.csv`

Format:

```text
#name;country;city;address;email;phone;web
Kissaki Kendo;Germany;Lahr;;info@example.org;;
```

Pflicht fuer KTM: `name`, `city`. Optionale Spalten bleiben erhalten.

### Teilnehmer

Datei: `participants.csv`

Format:

```text
#Name;Lastname;idCard;Club;ClubCity
Max;Mustermann;A12345;Kissaki Kendo;Lahr
```

Die `idCard` wird vor dem Speichern und Export normalisiert:

- Leerzeichen entfernen
- Bindestriche entfernen
- trimmen
- uppercase

Beispiel: `a-123 45` wird zu `A12345`.

### Teams

Datei: `teams.csv`

Format:

```text
#name;tournament;member1;member2;member3;member4;member5;member6;member7;member8;member9
Kissaki-Team-1;Kissaki Cup 2026;A12345;B67890;C24680;;;;;;
```

Der Turniername ist eine Admin-Konfiguration mit dem Standardwert `Kissaki Cup 2026`, weil KTM den Namen exakt passend zur Datenbank erwartet.

## Bedienlogik

Die oeffentliche Anmeldung ist als Wizard gebaut, damit Club-Manager nicht in einer sehr langen Tabelle verloren gehen. Jeder Schritt speichert erst nach dem finalen Absenden. Im Browser koennen Kaempfer und Teams dynamisch hinzugefuegt, bearbeitet und entfernt werden.

Die Pruefseite vor dem Absenden zeigt:

- Club und Ansprechpartner
- Anzahl Kaempfer
- Einzelstarts nach Kategorie
- Teamliste
- Warnungen zu fehlenden IDs, unvollstaendigen Teams oder Regelkonflikten

Nach Absenden wird ein Zahlungs-Hinweis angezeigt, aber keine Teilnahmegebuehr erfasst und kein Zahlungsstatus gespeichert.

## Fehlerbehandlung Und Sicherheit

- Serverseitige Validierung fuer alle Pflichtfelder und Kategorien.
- CSRF-Schutz fuer Formular- und Admin-Aktionen.
- Honeypot-Feld und einfache Rate-Limits gegen automatisierten Spam.
- Keine oeffentliche Liste aller Anmeldungen.
- Private Aenderungslinks sind lange Zufallstokens.
- Admin-Passwort wird nicht im Code abgelegt, sondern in der Server-Konfiguration.
- SQLite-Datei und Exportdateien liegen ausserhalb des oeffentlich ausgelieferten Webroots.
- Admin-Aenderungen werden mit Zeitstempel protokolliert.

## Datenschutz

Die App speichert nur Daten, die fuer Anmeldung, Turnierorganisation und KTM-Import gebraucht werden. Geburtsdatum wird nicht erhoben; fuer die Kategoriepruefung reicht das Geburtsjahr. Ansprechpartnerdaten werden nicht in KTM exportiert. Nach Turnierabschluss kann die komplette SQLite-Datei archiviert und anschliessend aus der laufenden Instanz entfernt werden.

## Tests

Die Umsetzung soll folgende Tests enthalten:

- Unit-Tests fuer ID-Normalisierung.
- Unit-Tests fuer Kategorie- und Teamregeln.
- Unit-Tests fuer CSV-Ausgabe inklusive leerer optionaler Spalten.
- Integrationstest fuer Absenden einer Clubmeldung.
- Integrationstest fuer Bearbeitung ueber privaten Token.
- Integrationstest fuer Admin-Export.
- Manuelle Browser-Pruefung fuer Desktop und Smartphone.

## Abnahmekriterien

- Ein Club kann ohne Login eine vollstaendige Meldung absenden.
- Ein Club kann die Meldung ueber privaten Link bearbeiten.
- Admin kann Meldungen sehen, korrigieren, deaktivieren und als geprueft markieren.
- CSV-Export erzeugt exakt die drei KTM-Dateien `clubs.csv`, `participants.csv`, `teams.csv`.
- `idCard` ist im Export normalisiert.
- Teilnahmegebuehr erscheint nicht als Formularfeld.
- Die App laeuft lokal per .NET und ist fuer Windows/IIS-Deployment vorbereitet.