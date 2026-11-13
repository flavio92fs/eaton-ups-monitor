# Eaton UPS Monitor 3.4.0 portable

## English

Extract the ZIP into a folder and run `EatonMonitor.exe`. Windows x64 and .NET Framework 4.8 are required.

In **Settings → Data source**, select **Direct USB** when the UPS is connected to the PC, or **Ubuntu server** and save the dashboard URL when the UPS is connected to the server.

For Direct USB, the original Eaton WinUSB driver must be installed and Companion must be fully closed. See `README.md` for driver setup. For Ubuntu server, follow the server installation guide linked in `README.md`.

The portable app runs from its extracted folder. Preferences, including the selected language and server URL, are saved for the current Windows user in the registry. An optional beeper key is encrypted for that user. These settings stay on the PC when you move the folder to another PC.

Closing the window keeps the monitor in the system tray. Choose **Exit completely** to quit. For installation and automatic startup with Windows, use `EatonMonitorSetup.exe` from the release.

## Italiano

Estrai lo ZIP in una cartella e avvia `EatonMonitor.exe`. Servono Windows x64 e .NET Framework 4.8.

In **Impostazioni → Sorgente dati**, scegli **USB diretto** se l’UPS è collegato al PC, oppure **Server Ubuntu** e salva l’URL della dashboard se l’UPS è collegato al server.

Per USB diretto servono il driver WinUSB originale Eaton installato e Companion completamente chiuso. La configurazione del driver è descritta in `README.it.md`. Per il server Ubuntu segui la guida collegata nello stesso file.

L’app portatile funziona dalla cartella estratta. Le preferenze, incluse lingua e URL del server, vengono salvate nel registro per l’utente Windows corrente. L’eventuale chiave del cicalino viene cifrata per quell’utente. Queste impostazioni restano sul PC quando sposti la cartella su un altro computer.

Chiudendo la finestra il monitor resta nella barra dell’orologio. Per terminarlo scegli **Esci completamente**. Per installazione e avvio automatico con Windows, usa `EatonMonitorSetup.exe` dalla release.
