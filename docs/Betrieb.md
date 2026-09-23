# Betrieb

Stand 22.09.2026, Version 1. Diese Datei beschreibt, wie ReviewMyDoc konfiguriert wird und wo die
Daten liegen. Sie ist für jemanden geschrieben, der dieses Projekt nicht kennt. Wer die Anwendung
nur lokal starten will, braucht aus dieser Datei gar nichts zu tun: ohne jede Konfiguration
speichert ReviewMyDoc in ein Verzeichnis auf der eigenen Festplatte und läuft.

Zur Zeit beschreibt diese Datei die Ablage, die Security-Header, die Data Protection, die
Ratenbegrenzung und die Anmeldung des Eigentümers. Weitere Abschnitte kommen dazu, sobald AI und
Mail gebaut sind.

## Wo die Daten liegen

ReviewMyDoc hat keine Datenbank. Jedes Dokument, jeder Reviewauftrag und jede Rückmeldung ist eine
Datei. Welche Dateien das sind, steht in [Datenmodell.md](Datenmodell.md). Für diese Dateien gibt es
zwei Ablagen, und beide verhalten sich gegenüber der Anwendung gleich:

- **Verzeichnis.** Die Dateien liegen in einem Ordner auf der Festplatte des Rechners, auf dem die
  Anwendung läuft. Gedacht für die Entwicklung und zum Ausprobieren.
- **Azure Blob Storage.** Die Dateien liegen in einem Container eines Azure-Speicherkontos. Das ist
  die Ablage für den echten Betrieb.

Welche der beiden verwendet wird, entscheidet allein die Konfiguration. Es gibt dafür keine zwei
Bauarten der Anwendung: dieselbe kompilierte Anwendung läuft mit beiden.

## Die Konfigurationsschlüssel

Alle Schlüssel stehen unter `Storage` und sind in `src/ReviewMyDoc.Web/appsettings.json` mit ihrem
Entwicklungswert eingetragen. So sieht der Abschnitt dort aus:

```json
"Storage": {
  "Provider": "Auto",
  "Directory": {
    "RootPath": "App_Data/storage"
  },
  "Blob": {
    "ServiceUri": "",
    "ConnectionString": "",
    "ContainerName": "reviewmydoc",
    "ManagedIdentityClientId": ""
  }
}
```

| Schlüssel | Bedeutung | Entwicklungswert |
| --- | --- | --- |
| `Storage:Provider` | Welche der beiden Ablagen verwendet wird. Erlaubt sind `Auto`, `Directory` und `Blob`. Siehe den nächsten Abschnitt. | `Auto` |
| `Storage:Directory:RootPath` | Der Ordner, in dem die Dateien liegen, wenn die Verzeichnisablage verwendet wird. Ein relativer Pfad wird vom Arbeitsverzeichnis der Anwendung aus gelesen. Der Ordner wird angelegt, wenn es ihn noch nicht gibt. | `App_Data/storage` |
| `Storage:Blob:ServiceUri` | Die Adresse des Blob-Dienstes des Speicherkontos, zum Beispiel `https://meinkonto.blob.core.windows.net`. Das ist der Weg, den die Anwendung in Azure geht. Er enthält kein Geheimnis. | leer |
| `Storage:Blob:ConnectionString` | Die Verbindungszeichenfolge eines Speicherkontos oder eines Emulators. Sie enthält einen Schlüssel und ist damit ein Geheimnis. Sie steht deshalb niemals mit einem Wert in `appsettings.json` und niemals im Repository. | leer |
| `Storage:Blob:ContainerName` | Der Name des einen Containers, in dem alles liegt. Nur Kleinbuchstaben, Ziffern und Bindestriche. | `reviewmydoc` |
| `Storage:Blob:ManagedIdentityClientId` | Nur nötig, wenn die Web App eine benutzerseitig zugewiesene verwaltete Identität benutzt. Dann steht hier deren Client-ID. Bleibt der Wert leer, wird die systemseitig zugewiesene Identität verwendet, und das ist der Normalfall. | leer |

Sind `ServiceUri` und `ConnectionString` beide gesetzt, gewinnt `ServiceUri`. So kann eine
Verbindungszeichenfolge, die von einem Versuch übrig geblieben ist, eine laufende Anwendung nicht
von ihrer verwalteten Identität wegziehen.

### Wie dieselben Schlüssel in Azure heißen

In Azure werden dieselben Schlüssel als App Settings der Web App gesetzt, mit zwei Unterstrichen
statt des Doppelpunktes:

```
Storage__Provider              = Auto
Storage__Blob__ServiceUri      = https://meinkonto.blob.core.windows.net
Storage__Blob__ContainerName   = reviewmydoc
```

`Storage__Blob__ConnectionString` wird in Azure nicht gesetzt. Genau darum geht es bei der
verwalteten Identität: es gibt dort keinen Schlüssel, den jemand verlieren könnte.

### Wo die Verbindungszeichenfolge lokal hingehört

Wer lokal gegen ein echtes Speicherkonto oder gegen einen Emulator arbeiten will, trägt die
Verbindungszeichenfolge nicht in eine Datei des Repositorys ein, sondern in die Benutzergeheimnisse
des eigenen Rechners:

```
dotnet user-secrets --project src/ReviewMyDoc.Web set "Storage:Blob:ConnectionString" "<die Zeichenfolge>"
```

Diese Werte liegen im Benutzerprofil, nicht im Projektordner, und können deshalb nicht versehentlich
eingecheckt werden. Ebenso möglich ist eine Umgebungsvariable mit dem Namen
`Storage__Blob__ConnectionString`.

## Wie die Auswahl zwischen den beiden Ablagen funktioniert

`Storage:Provider` kennt drei Werte:

- **`Auto`** (der Standard). Ist eine Verbindung nach Azure konfiguriert, also `ServiceUri` oder
  `ConnectionString` gesetzt und ein Containername vorhanden, dann wird Azure verwendet. Ist keine
  konfiguriert, wird das Verzeichnis verwendet. Das ist der Grund, warum ein frisch geklontes
  Repository ohne jede Einrichtung startet und funktioniert, und warum in Azure das Setzen von
  `Storage__Blob__ServiceUri` genügt.
- **`Directory`**. Immer das Verzeichnis, auch wenn eine Verbindung konfiguriert ist. Nützlich, um
  lokal kurz ohne Azure zu arbeiten, ohne die Verbindung wegzunehmen.
- **`Blob`**. Immer Azure. Ist dabei keine Verbindung konfiguriert, meldet die Anwendung das beim
  ersten Zugriff auf die Ablage als Fehler, statt ersatzweise in ein Verzeichnis zu schreiben. Das
  ist Absicht: wer diesen Wert setzt, meint ihn, und Dokumente, die unbemerkt auf der Festplatte
  einer Web App landen, wären beim nächsten Deployment weg.

Die Anwendung startet in allen drei Fällen. Sie prüft die Ablage nicht beim Start, sondern beim
ersten Zugriff, damit eine unvollständige Konfiguration nicht den ganzen Dienst am Hochfahren
hindert.

## Der Container und die Rechte der verwalteten Identität

**Der Container muss vorhanden sein.** Die Anwendung legt ihn nicht an. Einmalig, zum Beispiel mit
der Azure-Befehlszeile:

```
az storage container create --account-name meinkonto --name reviewmydoc --auth-mode login
```

**Die Rechte.** Die Web App braucht eine verwaltete Identität, und diese Identität braucht am
Container die Rolle **Storage Blob Data Contributor** (deutsch: Mitwirkender an Storage-Blobdaten).
Weniger genügt nicht, denn die Anwendung liest, schreibt und löscht Blobs. Mehr ist nicht nötig:
insbesondere braucht sie keine Rolle, die den Zugriff auf die Kontoschlüssel erlaubt, und sie soll
sie auch nicht haben, weil ein Kontoschlüssel alles im ganzen Speicherkonto öffnet.

Die Rolle wird so eng wie möglich vergeben, also am Container und nicht am Speicherkonto oder gar an
der Ressourcengruppe:

```
az role assignment create \
  --assignee <Objekt-ID der verwalteten Identität> \
  --role "Storage Blob Data Contributor" \
  --scope "/subscriptions/<Abonnement>/resourceGroups/<Gruppe>/providers/Microsoft.Storage/storageAccounts/meinkonto/blobServices/default/containers/reviewmydoc"
```

Zwei Hinweise, die erfahrungsgemäß Zeit kosten:

- Die Zuweisung einer Rolle braucht einige Minuten, bis sie wirkt. Ein Zugriff, der unmittelbar
  danach mit „keine Berechtigung“ scheitert, ist oft nur zu früh.
- Die Rolle **Contributor** auf dem Speicherkonto reicht nicht aus. Sie erlaubt das Verwalten des
  Kontos, nicht das Lesen und Schreiben der Daten darin. Gebraucht wird eine Rolle mit `Data` im
  Namen.

Lokal, also ohne verwaltete Identität, verwendet die Anwendung bei gesetzter `ServiceUri` die
Anmeldung des Rechners, zum Beispiel die der Azure-Befehlszeile nach `az login`. Die angemeldete
Person braucht dann dieselbe Rolle am Container.

## Tests gegen echtes Blob Storage

Die Tests des Objektspeichers prüfen beide Ablagen gegen dieselbe Liste von Zusicherungen. Gegen das
Verzeichnis laufen sie immer. Gegen Blob Storage laufen sie nur, wenn eine Verbindung konfiguriert
ist; sonst werden sie mit genau diesem Grund übersprungen und sind nicht rot. Dafür gibt es zwei
Umgebungsvariablen, und es genügt eine davon:

| Variable | Bedeutung |
| --- | --- |
| `REVIEWMYDOC_TEST_BLOB_CONNECTIONSTRING` | Verbindungszeichenfolge eines Speicherkontos oder eines Emulators. |
| `REVIEWMYDOC_TEST_BLOB_SERVICEURI` | Adresse des Blob-Dienstes. Die Tests melden sich dann mit der Anmeldung des Rechners an, wie die Anwendung in Azure. |

Diese Variablen heißen absichtlich anders als die Konfigurationsschlüssel der Anwendung. So kann
niemand versehentlich einen Testlauf gegen den Container starten, in dem die echten Dokumente
liegen.

Ein Lauf sieht so aus:

```
set REVIEWMYDOC_TEST_BLOB_SERVICEURI=https://meinkonto.blob.core.windows.net
dotnet test
```

**Was der Testlauf mit dem Speicherkonto macht.** Jeder einzelne Testfall legt einen eigenen
Container mit zufälligem Namen an und löscht ihn danach wieder. Er fasst den Container der Anwendung
nicht an. Das Konto, gegen das die Tests laufen, sollte trotzdem ein eigenes Testkonto sein, und die
angemeldete Identität braucht dort das Recht, Container anzulegen und zu löschen, also zusätzlich
zur Rolle für die Daten eine Rolle wie **Contributor** auf dem Speicherkonto.

**Mit einem Emulator.** Statt eines echten Kontos geht auch Azurite, der Emulator von Microsoft. Er
wird mit `npm install -g azurite` installiert, mit `azurite-blob` gestartet und über die
Verbindungszeichenfolge `UseDevelopmentStorage=true` angesprochen:

```
set REVIEWMYDOC_TEST_BLOB_CONNECTIONSTRING=UseDevelopmentStorage=true
dotnet test
```

Azurite ist ein Nachbau und kein Ersatz für einen Lauf gegen den echten Dienst. Er spricht dasselbe
Protokoll und verhält sich bei Bedingungen, ETags und Append-Blobs wie der Dienst, aber er kennt
keine verwalteten Identitäten und keine Rollen, er ahmt das Verhalten unter Last nicht nach, und
seine Fehlermeldungen stammen nicht aus derselben Quelle. Was nur ein echtes Speicherkonto zeigt,
ist deshalb vor allem die Anmeldung mit verwalteter Identität und alles, was mit Rechten zu tun hat.

## Die Security-Header

Alle Security-Header werden an genau einer Stelle gesetzt, in einer Middleware, die als erste im
Ablauf steht: `src/ReviewMyDoc.Web/Security/SecurityHeaders.cs`. Sie stehen deshalb auf jeder
Antwort, auch auf einer Weiterleitung, auf einer Stildatei, auf einer 404 und auf einer Fehlerseite.
Wer später eine Seite baut, muss an keinen einzigen von ihnen denken.

Es gibt dafür keine Konfigurationsschlüssel. Ein Header, den man pro Umgebung abschalten kann, ist
am Ende in der Umgebung abgeschaltet, in der es darauf ankommt.

| Header | Wert | Wofür er da ist |
| --- | --- | --- |
| `Content-Security-Policy` | siehe unten | Legt fest, was der Browser überhaupt laden und ausführen darf. |
| `X-Content-Type-Options` | `nosniff` | Eine Datei ist das, was ihr Content-Type sagt. Ohne diesen Header kann ein Browser ein hochgeladenes Dokument in etwas hineinraten, das er ausführt. |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Ein Reviewlink trägt seinen Token im Pfad. Nach außen geht nur noch die nackte Herkunft, nie die vollständige Adresse, sonst wäre der Token beim nächsten Klick verschenkt. |
| `X-Frame-Options` | `DENY` | Sagt dasselbe wie `frame-ancestors 'none'`, für die Zwischenstellen und eingebetteten Ansichten, die nur den älteren Header lesen. Beide verbieten dasselbe und können sich deshalb nicht widersprechen. |
| `Cross-Origin-Opener-Policy` | `same-origin` | Ein fremdes Fenster bekommt keinen Griff auf den Browserkontext dieser Anwendung. |
| `Cross-Origin-Resource-Policy` | `same-origin` | Keine fremde Seite darf etwas aus dieser Anwendung einbinden. `/components/` ist der Bausteinordner dieser Anwendung und kein CDN. |
| `X-Robots-Tag` | `noindex, nofollow` | Die ganze Anwendung ist privat. Ein Reviewlink in einem Suchindex wäre ein verlorener Token, und deshalb gilt der Header für jede Antwort statt nur für die Reviewpfade. |
| `Permissions-Policy` | alle Fähigkeiten aus | Die Anwendung braucht weder Kamera noch Mikrofon, weder Standort noch Bezahlung. Was ausgeschaltet ist, kann auch eingeschleuster Code nicht anfordern. |

`Strict-Transport-Security` steht nicht in dieser Liste. Es wird von `UseHsts()` gesetzt, und
absichtlich nur außerhalb der Entwicklung, weil der Header sonst `localhost` im Browser des
Entwicklers monatelang auf HTTPS festnageln würde.

### Die Content Security Policy im Einzelnen

```
default-src 'none'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self';
connect-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'
```

Die Richtlinie ist eng, weil die Anwendung es zulässt: ReviewMyDoc rendert auf dem Server, lädt
seine Stile aus `/components/` und `/css/` der eigenen Herkunft und verwendet kein Framework, keine
UI-Bibliothek und keine Fremdquelle.

- `default-src 'none'` statt `'self'`. Jede Art von Abruf muss einzeln genannt werden, also wird
  eine Art, an die niemand gedacht hat, abgelehnt statt stillschweigend erlaubt. Damit sind auch
  `object-src`, `frame-src`, `media-src` und `worker-src` abgedeckt.
- `style-src 'self'` **ohne** `'unsafe-inline'`. Das ist die eine Stelle, an der ReviewMyDoc
  strenger ist als Atelier. Der Rahmen und die Partials unter `Pages/Shared/Ui/` tragen nur Klassen
  und kein einziges `style`-Attribut, und jeder Baustein bringt seine eigene Datei mit. **Folge für
  die Entwicklung:** die Entwickler-Fehlerseite von ASP.NET Core bringt ihre Stile inline mit und
  erscheint deshalb unformatiert. Ihr Text bleibt lesbar. Wer eine Seite baut, hält sich an Dateien;
  ein Test durchsucht das gerenderte Markup nach `style=`, `<style` und `<script`.
- `img-src 'self'` ohne `data:`, weil heute weder eine Seite noch ein Stylesheet eine Daten-URL
  verwendet. Wer eine braucht, ändert diese Zeile und begründet es.
- `font-src 'self'` wird gebraucht: `components/tokens/website.css` lädt drei woff2-Dateien.
- `base-uri 'none'` statt `'self'`: die Anwendung schreibt nie ein `<base>`-Element, und ein
  eingeschleustes würde jede relative Adresse der Seite umlenken.

## Data Protection: die Schlüssel für Cookies und Formulare

Diese Schlüssel verschlüsseln das Anmeldecookie des Eigentümers, die Link-Session eines Reviewers
und jeden Antiforgery-Token. Ohne einen Schlüsselbund, der den Prozess überlebt, erzeugt ASP.NET
Core bei jedem Start einen neuen: **jede Anmeldung ist dann nach einem Neustart ungültig, und jedes
gerade offene Formular scheitert.** In Azure wiegt das schwerer als ein Neustart auf dem eigenen
Rechner, denn eine Web App wird verschoben, neu gestartet und skaliert, ohne dass jemand fragt, und
eine zweite Instanz könnte nicht lesen, was die erste geschrieben hat.

**Wo die Schlüssel liegen, entscheidet `Storage:Provider`, also genau die Einstellung, die auch über
die Dokumente entscheidet.** Es gibt dafür keinen zweiten Schalter: wer eine Ablage einrichtet,
richtet eine Ablage ein.

- Verzeichnis: die Schlüssel liegen als `key-*.xml` in `DataProtection:KeysPath`.
- Azure: die Schlüssel liegen im Blob `DataProtection:BlobName` **im selben Container** wie die
  Dokumente, erreicht über dieselbe Verbindung und dieselbe verwaltete Identität. Es ist kein
  zweiter Container, keine zweite Rolle und kein zusätzlicher Einrichtungsschritt nötig.

```json
"DataProtection": {
  "ApplicationName": "ReviewMyDoc",
  "KeysPath": "App_Data/keys",
  "BlobName": "data-protection/keys.xml"
}
```

| Schlüssel | Bedeutung | Entwicklungswert |
| --- | --- | --- |
| `DataProtection:ApplicationName` | Der Name, unter dem der Schlüsselbund isoliert wird. Er muss fest sein: ohne ihn leitet Data Protection den Namen aus dem Pfad der Anwendung ab, und dieselbe Anwendung in einem zweiten Bereitstellungsslot, einem zweiten Container oder einem anderen Ordner hätte einen eigenen Schlüsselbund. Dieser Wert wird im Normalfall nie geändert. | `ReviewMyDoc` |
| `DataProtection:KeysPath` | Das Verzeichnis der Schlüssel, wenn die Verzeichnisablage verwendet wird. Ein relativer Pfad wird vom Arbeitsverzeichnis aus gelesen. Das Verzeichnis wird beim Start angelegt. | `App_Data/keys` |
| `DataProtection:BlobName` | Der Blob im Container aus `Storage:Blob:ContainerName`, der den Schlüsselbund hält, wenn Azure verwendet wird. | `data-protection/keys.xml` |

In Azure heißen dieselben Schlüssel `DataProtection__ApplicationName` und so weiter. Normalerweise
muss dort keiner von ihnen gesetzt werden: die Standardwerte stimmen, sobald
`Storage__Blob__ServiceUri` gesetzt ist.

**Was zu sichern ist.** Das Verzeichnis beziehungsweise der Blob mit den Schlüsseln. Gehen sie
verloren, ist kein Dokument verloren, aber jede Anmeldung und jede offene Reviewsitzung ist es.

**Was passiert, wenn Azure verlangt wird, aber keine Verbindung konfiguriert ist:** die Anwendung
startet nicht und nennt den fehlenden Schlüssel. Das ist bewusst anders als bei der Dokumentablage,
die einen solchen Fehler erst beim ersten Zugriff meldet. Den Schlüsselbund braucht schon die erste
Anfrage, die ein Formular rendert; eine Anwendung, die startet und danach jede einzelne Seite mit
einem Fehler beantwortet, wäre der schlechtere Weg zur Lösung.

## Die Ratenbegrenzung

Es gibt drei benannte Limiter. Sie sind **keine** Policies der RateLimiter-Middleware, sondern
Dienste, die die jeweilige Seite selbst fragt. Der Unterschied ist der ganze Punkt: die Middleware
würde die Anfrage beenden, bevor die Seite überhaupt läuft, und der Benutzer bekäme eine leere 429
und verlöre, was er getippt hat. Als Dienst gefragt, antwortet die Seite auf eine Ablehnung mit
**429, `Retry-After` und ihrem eigenen Rahmen samt den eingegebenen Werten.**

Gezählt wird je Limiter und je **gehashter Clientadresse** in einem festen Fenster von **einer
Minute**. Die Adresse selbst wird nirgends behalten, wie es `docs/Konventionen.md` verlangt. Es gibt
keine Warteschlange: eine abgelehnte Anfrage wird sofort beantwortet, statt eine Verbindung
offenzuhalten.

| Name | Was er schützt | Grenzwert | Was zählt |
| --- | --- | --- | --- |
| `login` | Die Passwortanmeldung des Eigentümers. Es gibt genau ein Konto und keine Kontosperre, also ist dieser Limiter das Einzige zwischen dem Passworthash und einem Wörterbuch. | 10 Versuche pro Minute | nur `POST`; die Seite zu lesen kostet nichts |
| `review-link` | Das Einlösen eines Reviewlinks. Der Token in der Adresse ist der ganze Nachweis, also ist Raten der einzige Weg hinein. | 20 Aufrufe pro Minute | Einlösen per `POST` |
| `ai` | Die AI-Endpunkte. Dieser Limiter schützt kein Geheimnis, sondern eine Rechnung: jeder Aufruf geht an einen externen Anbieter und wird bezahlt. | 20 Aufrufe pro Minute | jede Methode |

`review-link` ist absichtlich großzügiger als `login`. Ein Reviewer öffnet den Link, lädt ihn neu,
öffnet ihn aus einer zweiten Mail und sitzt vielleicht hinter derselben Firmenadresse wie drei
Kolleginnen. Ein zu enger Wert sperrt dort ehrliche Reviewer aus, und zwanzig Aufrufe pro Minute
machen das Raten eines Tokens trotzdem aussichtslos.

```json
"RateLimits": {
  "Login": { "PermitLimit": 10 },
  "ReviewLink": { "PermitLimit": 20 },
  "Ai": { "PermitLimit": 20 }
}
```

| Schlüssel | Bedeutung | Entwicklungswert |
| --- | --- | --- |
| `RateLimits:Login:PermitLimit` | Anmeldeversuche pro Minute und Clientadresse. | `10` |
| `RateLimits:ReviewLink:PermitLimit` | Einlösungen eines Reviewlinks pro Minute und Clientadresse. | `20` |
| `RateLimits:Ai:PermitLimit` | Aufrufe der AI-Endpunkte pro Minute und Clientadresse. | `20` |

Die Länge des Fensters ist absichtlich nicht einstellbar. Sie ist der Bezug, gegen den die
Grenzwerte oben gelesen werden, und zwei Stellschrauben für dieselbe Rate machen die Werte
unbesprechbar. Ein fehlender, leerer oder unbrauchbarer Wert bedeutet nicht „keine Grenze“, sondern
den oben genannten Standardwert: eine fehlende Ablage ist ein Fehler, eine fehlende Grenze wäre eine
offene Tür.

In Azure heißen dieselben Schlüssel `RateLimits__Login__PermitLimit` und so weiter.

### Wie eine Ablehnung aussieht

Den Limiter `login` fragt die Anmeldeseite unter `/anmeldung`. Wer es elfmal in einer Minute
versucht, bekommt beim elften Mal die Anmeldeseite zurück, im Rahmen der Anwendung, mit dem Satz
„Zu viele Anmeldeversuche“, mit dem Statuscode 429 und mit `Retry-After`. Nie eine leere Antwort.
Bis zur Anmeldung stand dafür eine eigene Prüfseite unter `/pruefung/ratenbegrenzung`; sie ist mit
der Anmeldung entfallen, weil die Anmeldung dasselbe an der Stelle zeigt, an der es zählt.

## Die Anmeldung des Eigentümers

ReviewMyDoc hat genau ein Konto. Es gibt keine Benutzerverwaltung, keine Registrierung, kein
ASP.NET Core Identity und keine Datenbank, in der ein Benutzer stünde; die Begründung steht in
[Konventionen.md](Konventionen.md), Abschnitt Stack. Die ganze Anmeldung ist deshalb: ein Passwort,
geprüft gegen einen Hash aus der Konfiguration, und danach ein Cookie mit der Rolle `owner`.

- Angemeldet wird unter `/anmeldung`, abgemeldet über die Schaltfläche im Kopf jeder Seite, die auf
  `/abmelden` schickt.
- Die Sitzung gilt acht Stunden und verlängert sich nicht von selbst. Das Cookie ist ein
  Sitzungscookie: wer den Browser schließt, ist abgemeldet.
- **Jede Seite verlangt die Rolle `owner`, und zwar als Vorgabe, nicht als Liste.** Wer später eine
  Seite hinzufügt, findet sie geschützt vor. Offen ist nur, was ausdrücklich `[AllowAnonymous]`
  trägt: heute die Anmeldeseite und die Fehlerseite, die Reviewansicht unter
  `/review/{documentId}/{reviewId}`, die sich mit einem persönlichen Link ausweist statt mit einer Anmeldung. Der Test
  `AuthorizationDefaultTests` führt diese Ausnahmen namentlich und schlägt fehl, sobald eine
  hinzukommt, die dort nicht steht.
- Stimmt das Passwort nicht, antwortet die Seite mit einem einzigen Satz, der nicht verrät, was
  falsch war, und der Versuch zählt auf den Limiter `login`, also zehn Versuche pro Minute und
  Clientadresse.
- Weder das Passwort noch sein Hash wird jemals in ein Log geschrieben.

### Der Konfigurationsschlüssel

| Schlüssel | Bedeutung | Entwicklungswert |
| --- | --- | --- |
| `Owner:PasswordHash` | Der Hash des Passworts des Eigentümers, erzeugt mit dem Befehl aus dem nächsten Abschnitt. Niemals das Passwort selbst. | leer in `appsettings.json` |

In Azure heißt derselbe Schlüssel als App Setting `Owner__PasswordHash`.

**Ohne diesen Wert kommt niemand hinein.** Außerhalb der Entwicklung startet die Anwendung erst gar
nicht, wenn er fehlt, und nennt beim Start den fehlenden Schlüssel. Das ist dieselbe Wahl wie bei
den Data-Protection-Schlüsseln und aus demselben Grund: eine Anwendung, die läuft und in die sich
niemand anmelden kann, meldet ihren Fehler erst dem, der ihn ohnehin schon hat.

**In der Entwicklung** steht in `src/ReviewMyDoc.Web/appsettings.Development.json` der Hash des
Passworts `entwicklung`. Damit lässt sich ein frisch geklontes Repository ohne jede Einrichtung
starten und benutzen. Diese Datei wird nur gelesen, wenn `ASPNETCORE_ENVIRONMENT` auf `Development`
steht, also niemals in Azure. Wer trotzdem lokal ein eigenes Passwort will, setzt es wie unten
beschrieben in den Benutzergeheimnissen; die gewinnen gegen die Datei.

### Den Hash erzeugen

Die Anwendung bringt den Befehl dafür selbst mit. Er fragt nach dem Passwort, zeigt es beim Tippen
nicht an, fragt es zur Sicherheit ein zweites Mal ab und gibt den Hash aus. Er speichert das
Passwort nirgends, schreibt es in keine Datei und nimmt es **nicht als Argument** entgegen, denn ein
Argument stünde in der Verlaufsdatei der Kommandozeile, unter Windows in `ConsoleHost_history.txt`,
und wäre während des Laufs in der Prozessliste lesbar.

```
dotnet run --project src/ReviewMyDoc.Web -- passwort-hash
```

Der Lauf sieht so aus:

```
Passwort:
Passwort wiederholen:

Der Hash für Owner:PasswordHash, siehe docs/Betrieb.md:
AQAAAAIAAYagAAAAEL... (eine lange Zeichenfolge)
```

Die Eingabeaufforderungen gehen auf die Fehlerausgabe, der Hash allein auf die Standardausgabe. Wer
den Hash direkt weiterverarbeiten will, kann ihn deshalb umleiten. Für ein Skript nimmt der Befehl
das Passwort auch als eine Zeile auf der Standardeingabe entgegen; dann entfällt die zweite Abfrage.

Jeder Lauf erzeugt einen anderen Hash, auch für dasselbe Passwort. Das ist richtig so: zu jedem Hash
gehört ein eigener Zufallswert. Es genügt ein Lauf, und sein Ergebnis ist der Wert, der gesetzt
wird.

### Den Hash setzen

**Lokal**, in den Benutzergeheimnissen des eigenen Rechners, nicht in einer Datei des Repositorys:

```
dotnet user-secrets --project src/ReviewMyDoc.Web set "Owner:PasswordHash" "<der ausgegebene Hash>"
```

Der Hash ist kein Geheimnis in dem Sinne, dass er ein Passwort wäre, aber er gehört trotzdem nicht
ins Repository: aus ihm lässt sich ein schwaches Passwort erraten, und das Repository ist öffentlich.

**In Azure**, als App Setting der Web App:

```
Owner__PasswordHash = <der ausgegebene Hash>
```

Oder mit der Azure-Befehlszeile:

```
az webapp config appsettings set --name <web-app> --resource-group <gruppe> \
  --settings "Owner__PasswordHash=<der ausgegebene Hash>"
```

Nach dem Setzen startet die Web App neu, und das neue Passwort gilt. Bestehende Anmeldungen bleiben
dabei gültig, denn sie hängen am Cookie und nicht am Hash. Wer sie beenden will, tauscht zusätzlich
die Data-Protection-Schlüssel aus; dann ist jede Sitzung und jedes offene Formular ungültig.

**Passwort ändern:** denselben Befehl noch einmal laufen lassen und den neuen Hash setzen. Es gibt
nichts weiter zu tun, weil es nichts gibt, wo ein altes Passwort noch stünde.

## Reviewlinks im Schreibworkflow

Eine markierte Passage kann gesammelt und später zusammen mit weiteren Passagen einer Person
zugewiesen werden. Beim Erteilen wird ein persönlicher Link angezeigt. Er wird einmalig angezeigt
und vom Eigentümer selbst weitergegeben; es ist kein Maildienst nötig und die Anwendung verschickt
keine Nachrichten. Der Link enthält sein Geheimnis im Fragment nach `#token=`. Das Frontend
entfernt dieses Fragment aus der Browserhistorie und löst es per antiforgery-geschütztem POST ein.
Der Server speichert nur den Hash. Die Sitzung liegt in einem HttpOnly-Cookie, das auf genau den
Auftrag begrenzt und mit Data Protection geschützt ist.

Der Zugang endet 14 Tage nach der gewählten Rückmeldefrist oder sofort nach Widerruf oder Abnahme.
Jede Anfrage prüft den aktuellen Auftrag; ein bereits gesetztes Cookie umgeht die Sperre nicht.
Standardmäßig sehen Empfänger nur gesammelte Ausschnitte. Bei ausdrücklich gewähltem
`WholeDocument` dürfen sie zusätzlich die ganze eingefrorene Fassung lesen. Rückmeldungen bleiben
auf zugewiesene Passagen beschränkt. Kommentare, Vorschläge und Rückfragen werden gemeinsam
zurückgegeben. Antworten erscheinen über den bestehenden Zugang bis zur Abnahme.

Ohne JavaScript ist der Link vollständig kopierbar. Der Empfänger trägt die 64 Zeichen hinter
`#token=` ins Zugangsformular ein. Der Eigentümer kann ganze Abschnitte per Formular sammeln.
Bei Speicherkonflikten bleibt die Eingabe in einer HTML-Seite erhalten; ein Link öffnet den
aktuellen Stand zum Abgleich. Ein freigegebenes Dokument ist bis zum ausdrücklichen Wiederöffnen
schreibgeschützt. Für diese Funktionen sind keine weiteren Einstellungen oder AI-Schlüssel nötig.

Eine unterbrochene Vorschlagsübernahme wird in der Auftragsansicht mit „Übernahme fortsetzen“
abgeschlossen. Der Dienst erkennt bereits geschriebenen Zieltext anhand seines Hashes.

### Blob-Vertragstests lokal

Mit Azurite 3.35.0 lassen sich die 28 Blob-Vertragstests auch ohne Azure-Konto ausführen:
`npm exec --yes --package=azurite@3.35.0 -- azurite-blob --blobHost 127.0.0.1 --skipApiVersionCheck`.
Im Testprozess `REVIEWMYDOC_TEST_BLOB_CONNECTIONSTRING=UseDevelopmentStorage=true` setzen.
Der Emulator verwendet Port 10000; die Anwendung bleibt auf 5071. Jeder Test legt einen eigenen
Container an und räumt ihn auf. Dies prüft das Blob-Protokoll, ersetzt aber keinen Nachweis der
Managed Identity und Rollenvergabe im späteren Azure-Betrieb.
