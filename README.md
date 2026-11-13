# Eaton UPS Monitor

[Italiano](README.it.md) · English

<img src="https://raw.githubusercontent.com/flavio92fs/eaton-ups-monitor/v3.4.0/assets/icon.png" alt="Eaton UPS Monitor icon" width="96">

Eaton UPS Monitor is an unofficial monitor for the Eaton 5E1600UD. The Windows app reads the UPS over USB or connects to the dashboard on an Ubuntu server. The server runs in Docker Compose and reads the UPS over USB with Network UPS Tools (NUT). Both interfaces are available in English and Italian.

| Use | Download |
| --- | --- |
| Windows portable app with USB and network modes | [Portable ZIP](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/EatonMonitor-3.4.0-windows-x64-portable.zip) |
| Windows installer with USB and network modes | [EatonMonitorSetup.exe](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/EatonMonitorSetup.exe) |
| Ubuntu server with USB and network dashboard | [Ubuntu Docker ZIP](https://github.com/flavio92fs/eaton-ups-monitor/releases/download/v3.4.0/Eaton-UPS-Server-Ubuntu-3.4.0.zip) · [Setup guide](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.md) |

[Release notes and SHA-256 checksums](https://github.com/flavio92fs/eaton-ups-monitor/releases/tag/v3.4.0)

## Supported hardware

**The only tested and currently supported model is Eaton 5E1600UD (5E 1600 G2), firmware 01.02.0009.** Testing used USB VID `0463`, PID `FFFF`, with the Eaton WinUSB driver on Windows x64. This USB ID is shared by other devices and does not establish compatibility by itself.

Other UPS models and firmware versions are **unverified**. You are welcome to test them and [open a compatibility report](https://github.com/flavio92fs/eaton-ups-monitor/issues/new?template=compatibility.yml). A report does not automatically add a device to the supported list. The app currently discovers only this USB ID and requires exactly one matching UPS; it does not select among multiple units.

## Features

- Battery percentage, UPS-estimated runtime, load, active/apparent power, voltages, frequency and available status flags.
- Disable/enable the buzzer, or mute the current alarm temporarily.
- On-screen overlay when mains power fails or returns; a lost USB connection has a distinct notification.
- Closing or minimizing the window keeps the app in the system tray. Use **Exit completely** to stop it.
- Optional per-user installation, desktop shortcut and startup at Windows sign-in in the tray.
- English/Italian UI: **Settings → Language**. Exit completely and reopen to apply a change. The first launch follows Windows language, defaulting to English outside Italian.

Monitoring and overlays require the app to be running and receiving data from its selected source. Automatic PC shutdown is outside the scope of this project.

## Install and run on Windows

1. Choose the portable ZIP or the standalone installer from [Releases](https://github.com/flavio92fs/eaton-ups-monitor/releases). Both require Windows x64 and .NET Framework 4.8.
2. For portable use, extract the ZIP and run `EatonMonitor.exe`. Preferences are saved in the current Windows user's registry. For installation, run `EatonMonitorSetup.exe` using that filename. Setup installs into `%LOCALAPPDATA%\EatonMonitor` and offers optional startup with Windows. The executables are unsigned.
3. For **Direct USB**, plug the UPS into the PC and fully exit Eaton UPS Companion. If the original WinUSB driver is missing, select **Also install the Eaton USB driver** in setup, choose your original `ups_usb.inf` alongside `ups_usb.cat`, and approve the administrator prompt.
4. For **Ubuntu server**, plug the UPS into the server and follow the [Ubuntu setup guide](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.md). The Windows app uses the dashboard URL and needs no UPS driver in this mode.

Command line options:

```powershell
.\EatonMonitor.exe --lang en
.\EatonMonitor.exe --lang it
.\EatonMonitor.exe --tray
.\EatonMonitor.exe --setup
.\EatonMonitor.exe --uninstall
```

`--lang` overrides the saved language for that process only. If an instance is already running, exit it completely before changing command line options. Uninstall is also available from Windows Installed apps and leaves the USB driver installed.

## Ubuntu server connection

Under **Settings → Data source**, select **Ubuntu server**, enter the dashboard address (for example `http://server:8099`), and select **Save connection**. The tray tooltip shows mains status, battery and watts. Connection loss is shown separately from a reported power outage. Select **Direct USB** to switch back to local USB.

If the address or port changes, edit the URL in the same settings and save it again. For telemetry, a local DNS hostname or [Tailscale MagicDNS](https://tailscale.com/docs/features/magicdns) name can keep working when the IP changes, as long as it resolves to the new address and the dashboard listens on it. See the [server guide](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/server/README.md#changing-the-address-or-port) to update the server side.

If the server administrator has enabled remote beeper commands, use its numeric Tailscale IPv4 address and enter its **Beeper key**. The key is encrypted for the current Windows user and scoped to the URL. Enter it again if you change the URL. Leaving it empty enables telemetry only. The three supported commands are enable, disable and temporary mute.

For a one-time override, run `EatonMonitor.exe --server http://server:8099` after exiting any existing instance.

## Buzzer behavior and verification

Temporary mute was acoustically confirmed on the tested UPS during a real mains outage. Disable/enable states were also confirmed by USB readback. The only write operation is the separately validated HID `AudibleAlarmControl` feature, report `0x14`, with values `1` (disable), `2` (enable), or `3` (temporary mute). The app sends no UPS shutdown, outlet switching or firmware commands.

Persistence after unplugging USB, power cycling or changing alarm conditions has **not been verified**. A successful readback is not a promise that all future alarms will remain silent. Firmware changes are neither needed nor supported.

The overlay preview is visual only and sends no buzzer command. Automated tests simulate mains transitions using offline packets; they do not reproduce a live blackout or verify sound.

## USB driver and Companion removal

Get Companion from the [official Eaton download page](https://www.eaton.com/gb/en-gb/catalog/backup-power-ups-surge-it-power-distribution/eaton-ups-companion---emea.html) if you need the original driver. Install Companion to obtain its driver, then export the package if you want to keep a copy. Its uninstaller may remove the driver. Eaton Monitor setup accepts that local copy. The public download contains the monitor, setup and documentation.

From an elevated Windows terminal, use `pnputil /enum-drivers` to identify the Eaton UPS driver's published name, for example `oem42.inf` (use your actual name, not the example). Create a backup directory and export that package:

```powershell
New-Item -ItemType Directory -Force "$env:USERPROFILE\Documents\EatonDriverBackup"
pnputil /export-driver oem42.inf "$env:USERPROFILE\Documents\EatonDriverBackup"
```

Keep the entire exported package, including its catalog. To install it, select the original exported INF in setup or in **Settings → Restore Eaton USB driver…**. Both use Windows PnPUtil. If Windows requests a restart, restart before using the UPS. If the monitor still cannot see the UPS, reconnect the USB cable and check that Windows associated the Eaton WinUSB driver with the device.

## Build and tests

No third-party libraries or package downloads are required. From Windows x64 PowerShell with .NET Framework 4.8:

```powershell
.\build.ps1 -Test
```

Output: `dist/EatonMonitor.exe` and the release ZIP. Build uses the .NET Framework C# compiler. Offline tests cover HID decoding, scaling, live status fields, malformed input, buzzer report validation, localization and installer resource policy. No UPS is required and no hardware commands are sent by the tests. GitHub Actions also builds and runs these tests.

The fixtures contain the tested model's USB report layout and selected measurement packets, with no serial numbers or USB string tables. The Windows client saves preferences for the current user and reads network data from the configured dashboard. The Ubuntu server exposes a limited set of UPS fields on its configured private interfaces.

## Contributing and license

See [CONTRIBUTING.md](https://github.com/flavio92fs/eaton-ups-monitor/blob/v3.4.0/CONTRIBUTING.md) for compatibility reports. Do not upload serial numbers, private device paths, vendor binaries or proprietary firmware. Test results should distinguish USB readback, audible behavior and persistence.

Our source and icon are licensed under [MIT](LICENSE). Eaton is a trademark of its respective owner; no affiliation or endorsement is implied.
