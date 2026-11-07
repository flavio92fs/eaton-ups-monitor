# Changelog

## 3.4.0

- First public release to include the Ubuntu Docker Compose server and both Windows data sources.
- The dashboard and setup guides are available in English and Italian. The dashboard port is configurable in Compose.
- Added authenticated beeper enable, disable and temporary mute over Tailscale.
- The optional beeper key is stored using Windows per-user encryption and scoped to the selected server address.

## 3.3.0

- Added an optional Ubuntu dashboard data source over LAN or Tailscale, with saved connection settings.
- The tray icon tooltip displays mains status, battery and watts. Remote readings use the same dashboard and power overlays as USB mode.
- The server connection is read-only. Direct USB continues to support the buzzer controls.

## 3.2.0

- Added a standalone setup entry point and optional installation of a user-supplied Eaton WinUSB package.
- Added a link to Eaton's official download page and separate reporting of app installation, driver errors and restart requirements.
- The public installer remains free of vendor driver files.

## 3.1.0

- Initial public source release under MIT.
- English and Italian UI, installer and documentation; saved language selection in Settings.
- Battery, estimated runtime, load, electrical readings and buzzer controls over USB.
- Power outage/restoration overlays, tray icon, per-user install and optional Windows startup.
- Vendor driver files are not redistributed; restore accepts an original INF supplied by the user.
- Offline protocol regression tests and Windows CI.
- Tested model only: Eaton 5E1600UD, firmware 01.02.0009. Other models remain unverified.
