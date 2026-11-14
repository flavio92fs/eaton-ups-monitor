# Eaton UPS Monitor

Italiano · [English](README.md)

<img src="https://raw.githubusercontent.com/flavio92fs/eaton-ups-monitor/v3.4.0/assets/icon.png" alt="Icona Eaton UPS Monitor" width="96">

Eaton UPS Monitor è uno strumento non ufficiale per l’Eaton 5E1600UD. L’app Windows legge l’UPS via USB oppure si collega alla dashboard di un server Ubuntu. Il server gira in Docker Compose e legge l’UPS via USB con Network UPS Tools (NUT). Entrambe le interfacce sono disponibili in italiano e inglese.

| Utilizzo | Download |
| --- | --- |
| App Windows portatile, via USB o via rete | [ZIP portatile](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/EatonMonitor-3.4.0-windows-x64-portable.zip) |
| Installer Windows, con modalità USB e rete | [EatonMonitorSetup.exe](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/EatonMonitorSetup.exe) |
| Pacchetto driver USB originale, con INF e CAT | [Download ufficiale Eaton UPS Companion](https://www.eaton.com/gb/en-gb/catalog/backup-power-ups-surge-it-power-distribution/eaton-ups-companion---emea.html) · [Istruzioni per il driver](#driver-usb-e-rimozione-di-companion) |
| Server Ubuntu con USB e dashboard di rete | [ZIP Ubuntu Docker](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/Eaton-UPS-Server-Ubuntu-3.4.0.zip) · [Guida all’installazione](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.it.md) |

[Note di rilascio e checksum SHA-256](https://github.com/flavio92fs/eaton-ups-monitor/releases/tag/v3.4.0)

## Hardware supportato

**L’unico modello testato e attualmente supportato è Eaton 5E1600UD (5E 1600 G2), firmware 01.02.0009.** Le prove sono state eseguite con USB VID `0463`, PID `FFFF`, driver WinUSB Eaton e Windows x64. Questi identificativi USB sono condivisi da altri dispositivi e da soli non garantiscono la compatibilità.

Altri modelli e firmware sono **non verificati**. Chi vuole può provarli e [aprire una segnalazione di compatibilità](https://github.com/flavio92fs/eaton-ups-monitor/issues/new?template=compatibility.yml), anche in italiano. Una segnalazione non aggiunge automaticamente il dispositivo ai modelli supportati. L’app cerca solo questo identificativo USB e richiede un unico UPS corrispondente; non permette di scegliere tra più unità.

## Funzioni

- Percentuale batteria, autonomia stimata dall’UPS, carico, potenza attiva/apparente, tensioni, frequenza e stati disponibili.
- Disabilitazione e riabilitazione del cicalino, oppure silenziamento temporaneo dell’allarme attuale.
- Overlay in caso di blackout e ripristino della rete; la perdita USB viene segnalata separatamente.
- Chiudere o minimizzare la finestra mantiene l’app nella barra dell’orologio. **Esci completamente** la termina.
- Installazione facoltativa per l’utente, collegamento desktop e avvio all’accesso a Windows nella barra dell’orologio.
- Lingua da **Impostazioni → Lingua / Language**. Esci completamente e riapri per applicare la scelta. Al primo avvio segue Windows: italiano per Windows italiano, inglese negli altri casi.

Letture e overlay richiedono che l’app sia in esecuzione e riceva dati dalla sorgente selezionata. Lo spegnimento automatico del PC esula dallo scopo del progetto.

## Installazione e avvio su Windows

1. Scegli lo ZIP portatile oppure l’installer dalle [Release](https://github.com/flavio92fs/eaton-ups-monitor/releases). Entrambi richiedono Windows x64 e .NET Framework 4.8.
2. Per l’uso portatile, estrai lo ZIP e avvia `EatonMonitor.exe`. Le preferenze sono salvate nel registro dell’utente Windows corrente. Per l’installazione, avvia `EatonMonitorSetup.exe` mantenendo questo nome del file. L’installer usa `%LOCALAPPDATA%\EatonMonitor` e offre l’avvio automatico con Windows. Gli eseguibili sono privi di firma digitale.
3. Per **USB diretto**, collega l’UPS al PC e chiudi completamente Eaton UPS Companion. Se manca il driver WinUSB originale, seleziona **Installa anche il driver USB Eaton**, scegli `ups_usb.inf` con `ups_usb.cat` nella stessa cartella e conferma la richiesta di amministratore.
4. Per **Server Ubuntu**, collega l’UPS al server e segui la [guida Ubuntu](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.it.md). In questa modalità l’app usa l’indirizzo della dashboard e può funzionare senza il driver dell’UPS.

Opzioni da terminale:

```powershell
.\EatonMonitor.exe --lang en
.\EatonMonitor.exe --lang it
.\EatonMonitor.exe --tray
.\EatonMonitor.exe --setup
.\EatonMonitor.exe --uninstall
```

`--lang` cambia lingua solo per quel processo, senza modificare la preferenza salvata. Se l’app è già aperta, esci completamente prima di cambiare le opzioni. Puoi disinstallarla anche dalle App installate di Windows: il driver USB resta installato.

## Collegamento al server Ubuntu

In **Impostazioni → Sorgente dati**, scegli **Server Ubuntu**, inserisci l’indirizzo della dashboard (per esempio `http://server:8099`) e premi **Salva connessione**. Passando il mouse sull’icona nell’area di notifica vedi stato della rete, batteria e watt. La perdita del collegamento al server viene segnalata separatamente da un blackout. Scegli **USB diretto** per tornare alla lettura locale.

Se cambiano indirizzo o porta, modifica l’URL nelle stesse impostazioni e salvalo di nuovo. Per le letture, un nome DNS locale o [Tailscale MagicDNS](https://tailscale.com/docs/features/magicdns) può restare valido quando cambia l’IP, purché punti al nuovo indirizzo e la dashboard ascolti su quell’interfaccia. La [guida Ubuntu](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.it.md#cambiare-indirizzo-o-porta) indica come aggiornare il server.

Se sul server hai abilitato il cicalino remoto, usa il suo indirizzo IPv4 Tailscale numerico e inserisci la **Chiave cicalino**. Windows cifra la chiave per l’utente corrente e la associa all’URL. Se cambi l’URL, inseriscila di nuovo. Con il campo vuoto l’app legge solo i dati. I comandi disponibili sono abilita, disabilita e silenzia temporaneamente.

Per provare un indirizzo senza salvare l’impostazione, esegui `EatonMonitor.exe --server http://server:8099` dopo aver chiuso completamente l’istanza precedente.

## Cicalino e prove eseguite

Il silenziamento temporaneo è stato confermato acusticamente sul modello provato durante una reale mancanza di corrente. Disabilitazione e riabilitazione sono state confermate anche rileggendo lo stato USB. L’unica scrittura è il comando HID `AudibleAlarmControl`, report `0x14`, con struttura verificata separatamente e valori `1` (disabilita), `2` (abilita), `3` (silenzia temporaneamente). L’app non invia comandi di spegnimento UPS, controllo prese o firmware.

La persistenza dopo scollegamento USB, riaccensione dell’UPS o nuove condizioni di allarme **non è stata verificata**. La conferma USB non garantisce che ogni allarme futuro resti silenzioso. Non occorre modificare il firmware e l’app non lo supporta.

L’anteprima overlay è soltanto visiva e non invia comandi al cicalino. I test automatici simulano i cambi di alimentazione su pacchetti offline; non riproducono un blackout reale e non verificano il suono.

## Driver USB e rimozione di Companion

Per ottenere `ups_usb.inf` e il corrispondente `ups_usb.cat`, usa il collegamento **Download UPS Companion** sulla pagina Eaton qui sotto. Queste istruzioni usano il driver originale fornito con Companion.

[Scarica il driver originale tramite Eaton UPS Companion](https://www.eaton.com/gb/en-gb/catalog/backup-power-ups-surge-it-power-distribution/eaton-ups-companion---emea.html)

Dopo aver installato Companion, cerca `ups_usb.inf` e `ups_usb.cat` in `C:\Program Files\Eaton\UPS Companion\bin`. Se hai scelto una cartella diversa durante l’installazione, usa la sua sottocartella `bin`. Nell’installer di Eaton Monitor seleziona **Installa anche il driver USB Eaton → Scegli file INF…** e scegli quel file INF. Conserva il CAT nella stessa cartella. I passaggi di esportazione qui sotto permettono di conservare il pacchetto completo prima di rimuovere Companion.

Se ti colleghi soltanto al server Ubuntu, togli la spunta al driver USB e installa l’app Windows.

Da terminale Windows come amministratore, usa `pnputil /enum-drivers` per identificare il nome pubblicato del driver UPS Eaton, per esempio `oem42.inf` (sostituiscilo con quello effettivo). Crea una cartella ed esporta il pacchetto:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\EatonDriverBackup"
pnputil /export-driver oem42.inf "$env:USERPROFILE\Documents\EatonDriverBackup"
```

Conserva tutto il pacchetto esportato, incluso il catalogo. Per installarlo, seleziona il file INF originale nell’installer oppure in **Impostazioni → Ripristina driver USB Eaton…**. Entrambi usano PnPUtil di Windows. Riavvia Windows se richiesto. Se il monitor non rileva ancora l’UPS, ricollega il cavo USB e verifica che Windows abbia associato il driver WinUSB Eaton al dispositivo.

## Compilazione e test

Non occorrono librerie esterne o download di pacchetti. Da PowerShell Windows x64 con .NET Framework 4.8:

```powershell
.\build.ps1 -Test
```

Risultati in `dist/EatonMonitor.exe` e nello ZIP della release. La compilazione usa il compilatore C# di .NET Framework. I test offline verificano decodifica HID, scale delle misure, stati dinamici, input malformati, validazione del report cicalino, traduzioni e risorse dell’installer. Non richiedono un UPS e non inviano comandi hardware. Anche GitHub Actions compila ed esegue i test.

Le fixture includono la struttura dei report USB del modello provato e alcuni pacchetti di misure, senza numeri di serie né tabelle di stringhe USB. Il client salva le preferenze per l’utente Windows corrente e legge i dati di rete dalla dashboard configurata. Il server Ubuntu rende disponibili solo i campi dell’UPS previsti dall’API, sulle interfacce private configurate.

## Contributi e licenza

Vedi [CONTRIBUTING.md](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/CONTRIBUTING.md) per le segnalazioni, accettate anche in italiano. Non caricare numeri di serie, percorsi privati dei dispositivi, binari del produttore o firmware proprietari. Distingui tra conferma USB, comportamento sonoro e persistenza.

Codice e icona del progetto sono disponibili con [licenza MIT](LICENSE). Eaton è un marchio del rispettivo proprietario; non è implicata alcuna affiliazione o approvazione.
