# Contributing / Contribuire

English and Italian issues and pull requests are welcome.
Segnalazioni e pull request sono accettate in italiano e inglese.

Only Eaton 5E1600UD is currently supported. For another device, use the compatibility issue template and report exact model, firmware, Windows version and driver, which readings work, and how each buzzer action was verified. Mark anything you did not test as **not tested**. Distinguish a successful USB response from an audible result, and distinguish both from persistence after USB disconnection or an UPS restart. Do not disconnect mains just to fill out a report; report only observations you can obtain safely in your setup.

È supportato soltanto Eaton 5E1600UD. Per altri dispositivi usa il modello di segnalazione e indica modello esatto, firmware, versione Windows e driver, letture funzionanti e modalità di verifica di ciascun comando cicalino. Scrivi **non provato** per ogni verifica non eseguita. Distingui risposta USB, risultato sonoro e persistenza dopo scollegamento USB o riavvio UPS. Non simulare un blackout soltanto per compilare la segnalazione: riporta le osservazioni ottenibili in sicurezza nel tuo impianto.

Remove serial numbers, usernames and private device paths from screenshots and reports. Do not contribute vendor binaries, extracted Companion code, driver packages or firmware. Keep writes restricted to the validated dedicated buzzer report; expanding write support needs an independently reviewed implementation and clear device-specific evidence. New model support is a deliberate maintainer change, not inferred from matching VID/PID.

Rimuovi numeri di serie, nomi utente e percorsi privati da immagini e segnalazioni. Non includere binari del produttore, codice estratto da Companion, driver o firmware. Mantieni le scritture limitate al report dedicato del cicalino verificato; ogni ampliamento richiede revisione ed evidenze specifiche. Un VID/PID corrispondente non basta per dichiarare un nuovo modello supportato.

Run `./build.ps1 -Test` on Windows before submitting code changes. Keep both READMEs and the localization catalog consistent. Contributions to this repository are under the MIT license.
