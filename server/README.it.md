# Eaton UPS Monitor per Ubuntu

[English](README.md) · Italiano

Questo progetto Docker Compose legge un **Eaton 5E1600UD** collegato via USB a Ubuntu. La dashboard web e il client Windows mostrano batteria, autonomia stimata, potenza e altre misure disponibili tramite Network UPS Tools (NUT). Puoi scegliere **English** o **Italiano** nella dashboard. Gli altri modelli non sono stati verificati.

## Installazione e avvio

Servono un host Ubuntu con accesso a Docker Engine, Docker Compose **2.24.0 o successivo**, Bash, Python 3 (per configurare il cicalino remoto opzionale) e un solo dispositivo USB collegato con ID `0463:ffff`. Installa [Docker Engine e il plugin Compose](https://docs.docker.com/engine/install/ubuntu/) e controlla `docker compose version`. Esegui i comandi come utente con accesso a Docker.

1. Scarica [Eaton-UPS-Server-Ubuntu-3.4.0.zip](https://github.com/flavio92fs/eaton-ups-monitor/releases/tag/v3.4.0), estrailo e apri un terminale nella cartella `server` estratta. Se hai clonato il repository, usa la sua cartella `server`.
2. Collega l’UPS via USB. Con `usbutils` installato, `lsusb -d 0463:ffff` dovrebbe mostrare un solo dispositivo corrispondente. Chiudi eventuali altri programmi sul server che stanno usando l’UPS.
3. Copia il file di esempio e modifica la configurazione.

   ```sh
   cp .env.example .env
   nano .env
   ```

   Il valore predefinito `UPS_WEB_BIND=127.0.0.1` permette di accedere dal server stesso. Per l’accesso remoto aggiungi l’IPv4 LAN del server o quello restituito da `tailscale ip -4`, separando gli indirizzi con virgole. Per esempio, `UPS_WEB_BIND=127.0.0.1,192.168.1.50` usa un indirizzo LAN dimostrativo. Configura il firewall per i client scelti e mantieni la dashboard in rete privata. La telemetria in sola lettura è accessibile senza login.
4. Esegui `bash up.sh`. Lo script rileva il nodo USB corrente, costruisce l’immagine e avvia i container NUT e dashboard.
5. Con la porta predefinita e il loopback abilitato, su Ubuntu esegui `curl -fsS http://127.0.0.1:8099/api/ups`. Da un client autorizzato apri `http://<indirizzo-server>:8099/` nel browser. Nell’app Windows inserisci lo stesso URL in **Impostazioni → Sorgente dati → Server Ubuntu → Salva connessione**.

## Cambiare indirizzo o porta

La dashboard ascolta sulla rete dell’host. In `compose.yaml`, modifica `services.web.environment.UPS_WEB_PORT`. Ad esempio, `UPS_WEB_PORT: "${UPS_WEB_PORT:-8100}"` cambia la porta predefinita in 8100. Il valore `UPS_WEB_PORT` in `.env`, se impostato, ha la precedenza. Scegli una porta libera tra 1024 e 65535.

Se l’IP del server cambia, aggiorna `UPS_WEB_BIND` in `.env`. Esegui di nuovo `bash up.sh` per applicare le modifiche. Aggiorna il firewall se filtra la porta scelta, poi modifica l’URL salvato in Windows. Per le letture puoi usare un nome DNS locale o [Tailscale MagicDNS](https://tailscale.com/docs/features/magicdns), purché il nome risolva verso un indirizzo su cui la dashboard ascolta.

## Cicalino remoto opzionale

1. Collega Ubuntu e Windows a Tailscale. Includi l’IPv4 Tailscale numerico del server in `UPS_WEB_BIND`.
2. Su Ubuntu esegui `bash enable-beeper.sh`. Crea `.beeper.env` con una chiave per il client e una password distinta per NUT e riapplica la configurazione Compose. Tieni privato questo file.
3. In Windows usa l’IPv4 Tailscale numerico del server e la porta della dashboard nell’URL. Inserisci il valore `BEEPER_TOKEN` da `.beeper.env` nel campo **Chiave cicalino** e salva. Lascia il campo vuoto per la sola lettura.

Se cambia l’IP Tailscale, aggiorna `UPS_WEB_BIND` e riesegui `bash enable-beeper.sh` per aggiornare l’IP autorizzato conservando la chiave. Reinserisci la chiave quando salvi un nuovo URL nell’app Windows.

La dashboard è in sola lettura. L’app Windows può abilitare, disabilitare o silenziare temporaneamente il cicalino. Le relative richieste API sono `POST /api/beeper/enable`, `POST /api/beeper/disable` e `POST /api/beeper/mute`. Richiedono un corpo vuoto, una connessione Tailscale all’IP configurato e l’intestazione `Authorization: Bearer <chiave>`. Per abilita e disabilita il server verifica anche lo stato riportato da NUT.

## Ricollegamento USB e Home Assistant

Dopo il primo avvio, esegui `bash enable-auto-usb.sh` per controllare ogni minuto se cambia il nodo USB e ricreare il solo container NUT quando serve. Occorrono cron attivo, una cartella senza spazi e accesso a Docker per lo stesso utente. Senza cron puoi eseguire `bash up.sh` dopo aver spostato o ricollegato il cavo USB.

Per Home Assistant sulla rete dello stesso host, usa l’[integrazione Network UPS Tools](https://www.home-assistant.io/integrations/nut/) con host `127.0.0.1`, porta `3493` e credenziali vuote. NUT ascolta solo in loopback.

## Diagnostica e arresto

```sh
docker logs --tail 30 eaton-ups-nut
docker logs --tail 30 eaton-ups-web
docker exec eaton-ups-nut upsc eaton@127.0.0.1
curl -fsS http://127.0.0.1:8099/api/ups
```

`GET /healthz` controlla il processo della dashboard. `GET /api/ups` restituisce le misure o HTTP 503 quando NUT non ha dati. Se hai modificato la porta, usa quella configurata nell’ultimo comando.

Per fermare lo stack, esegui `rm -f .auto-usb` nella cartella del progetto per sospendere l’eventuale controllo cron, poi `docker stop eaton-ups-web eaton-ups-nut`. Per ripartire, esegui `bash up.sh` ed eventualmente `bash enable-auto-usb.sh`.
