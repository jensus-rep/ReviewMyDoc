# Betrieb

Stand 22.09.2026, Version 1. Diese Datei beschreibt, wie ReviewMyDoc konfiguriert wird und wo die
Daten liegen. Sie ist für jemanden geschrieben, der dieses Projekt nicht kennt. Wer die Anwendung
nur lokal starten will, braucht aus dieser Datei gar nichts zu tun: ohne jede Konfiguration
speichert ReviewMyDoc in ein Verzeichnis auf der eigenen Festplatte und läuft.

Zur Zeit beschreibt diese Datei die Ablage. Weitere Abschnitte kommen dazu, sobald Anmeldung, AI
und Mail gebaut sind.

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
