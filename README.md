# Eaton UPS Monitor

[Italiano](README.it.md) · English

<img src="assets/icon.png" alt="Eaton UPS Monitor icon" width="96">

A small, unofficial Windows utility for monitoring an Eaton UPS over USB and controlling its buzzer. English and Italian are included in the same application. This is an independent community project, not an Eaton product.

## Supported hardware

**The only tested and currently supported model is Eaton 5E1600UD (5E 1600 G2), firmware 01.02.0009.** Testing used USB VID `0463`, PID `FFFF`, with the Eaton WinUSB driver on Windows x64. This USB ID is shared by other devices and does not establish compatibility by itself.

Other UPS models and firmware versions are **unverified**. You are welcome to test them and [open a compatibility report](../../issues/new?template=compatibility.yml). A report does not automatically add a device to the supported list. The app currently discovers only this USB ID and requires exactly one matching UPS; it does not select among multiple units.

## Features

- Battery percentage, UPS-estimated runtime, load, active/apparent power, voltages, frequency and available status flags.
- Disable/enable the buzzer, or mute the current alarm temporarily.
- On-screen overlay when mains power fails or returns; a lost USB connection has a distinct notification.
- Closing or minimizing the window keeps the app in the system tray. Use **Exit completely** to stop it.
- Optional per-user installation, desktop shortcut and startup at Windows sign-in in the tray.
- English/Italian UI: **Settings → Language**. Exit completely and reopen to apply a change. The first launch follows Windows language, defaulting to English outside Italian.

**There is no automatic PC shutdown.** This app does not replace Companion's shutdown policies or a complete UPS protection system. Monitoring and overlays require the app to be running with USB connected. A Raspberry Pi or network/Bluetooth bridge is not supported by this Windows app.

## Download and run

1. Download `EatonMonitor-3.1.0-windows-x64.zip` from [Releases](../../releases).
2. Extract the ZIP and run `EatonMonitor.exe` on Windows x64 with .NET Framework 4.8.
3. Connect the UPS by USB and fully exit Eaton UPS Companion so it does not compete for the device. The original Eaton WinUSB driver must already be installed.
4. The app is portable. For installation, use **Settings → Install for current user**, then optionally enable **Start with Windows**. Installation uses `%LOCALAPPDATA%\EatonMonitor`; normal operation needs no administrator rights. The executable is not code-signed.

Command line options:

```powershell
.\EatonMonitor.exe --lang en
.\EatonMonitor.exe --lang it
.\EatonMonitor.exe --tray
.\EatonMonitor.exe --setup
.\EatonMonitor.exe --uninstall
```

`--lang` overrides the saved language for that process only. If an instance is already running, exit it completely before changing command line options. Uninstall is also available from Windows Installed apps and leaves the USB driver installed.

## Buzzer behavior and verification

Temporary mute was acoustically confirmed on the tested UPS during a real mains outage. Disable/enable states were also confirmed by USB readback. The only write operation is the separately validated HID `AudibleAlarmControl` feature, report `0x14`, with values `1` (disable), `2` (enable), or `3` (temporary mute). The app sends no UPS shutdown, outlet switching or firmware commands.

Persistence after unplugging USB, power cycling or changing alarm conditions has **not been verified**. A successful readback is not a promise that all future alarms will remain silent. Firmware changes are neither needed nor supported.

The overlay preview is visual only and sends no buzzer command. Automated tests simulate mains transitions using offline packets; they do not reproduce a live blackout or verify sound.

## USB driver and Companion removal

The source and release contain **no Eaton driver binaries or Companion files**. Keep a copy of the original official driver before removing Companion: its uninstaller may remove the USB driver needed by this app.

From an elevated Windows terminal, use `pnputil /enum-drivers` to identify the Eaton UPS driver's published name, for example `oem42.inf` (use your actual name, not the example). Create a backup directory and export that package:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\EatonDriverBackup"
pnputil /export-driver oem42.inf "$env:USERPROFILE\Documents\EatonDriverBackup"
```

Keep the entire exported package, including its catalog. If the driver is removed, use **Settings → Restore Eaton USB driver…** and select the original exported INF, or reinstall the official driver through Eaton's installer. Windows requests administrator permission only for driver installation. The restore button runs `pnputil /add-driver "selected.inf" /install`; it does not download or modify drivers.

## Build and tests

No third-party libraries or package downloads are required. From Windows x64 PowerShell with .NET Framework 4.8:

```powershell
.\build.ps1 -Test
```

Output: `dist/EatonMonitor.exe` and the release ZIP. Build uses the .NET Framework C# compiler. Offline tests cover HID decoding, scaling, live status fields, malformed input, buzzer report validation, localization and installer resource policy. No UPS is required and no hardware commands are sent by the tests. GitHub Actions also builds and runs these tests.

The fixtures contain the tested model's USB report layout and selected measurement packets, with no serial numbers or USB string tables. The app keeps session events in memory; Windows registry preferences and installation/startup entries are stored under the current user. The app has no network reporting or telemetry service.

## Contributing and license

See [CONTRIBUTING.md](CONTRIBUTING.md) for compatibility reports. Do not upload serial numbers, private device paths, vendor binaries or proprietary firmware. Test results should distinguish USB readback, audible behavior and persistence.

Our source and icon are licensed under [MIT](LICENSE). Eaton is a trademark of its respective owner; no affiliation or endorsement is implied.
