# Eaton UPS Monitor for Ubuntu

English · [Italiano](README.it.md)

This Docker Compose stack reads an **Eaton 5E1600UD** connected to the Ubuntu host by USB. The web dashboard and the Windows client display battery, estimated runtime, power and other readings exposed by Network UPS Tools (NUT). Choose **English** or **Italiano** in the dashboard. Other UPS models are unverified.

## Install and start

Requirements are an Ubuntu host with access to Docker Engine, Docker Compose **2.24.0 or later**, Bash, Python 3 (for optional remote beeper setup) and one connected USB device with ID `0463:ffff`. Install [Docker Engine and the Compose plugin](https://docs.docker.com/engine/install/ubuntu/) and check `docker compose version`. Run the commands as a user with access to the Docker daemon.

1. Download [Eaton-UPS-Server-Ubuntu-3.4.0.zip](https://github.com/flavio92fs/eaton-ups-monitor/releases/tag/v3.4.0), extract it and open a terminal in the extracted `server` directory. If you cloned the repository, use its `server` directory.
2. Connect the UPS by USB. With `usbutils` installed, `lsusb -d 0463:ffff` should show one matching device. Stop any other UPS software on this host that is already using the device.
3. Copy the example configuration and edit it.

   ```sh
   cp .env.example .env
   nano .env
   ```

   `UPS_WEB_BIND=127.0.0.1` is the default and works from the server itself. For remote access, append the server's actual LAN IPv4 or its IPv4 from `tailscale ip -4`, separated by commas. For example, `UPS_WEB_BIND=127.0.0.1,192.168.1.50` uses an example LAN address. Configure your firewall to allow the chosen clients and keep the dashboard on a private network. Its read-only telemetry has no login.
4. Run `bash up.sh`. It finds the current USB device node, builds the image and starts the NUT and dashboard containers.
5. With the default port and loopback enabled, run `curl -fsS http://127.0.0.1:8099/api/ups` on Ubuntu. From an allowed client, open `http://<server-address>:8099/` in a browser. To use Eaton Monitor on Windows, set that URL under **Settings → Data source → Ubuntu server → Save connection**.

## Changing the address or port

The dashboard listens on the host network. In `compose.yaml`, edit `services.web.environment.UPS_WEB_PORT`. For example, `UPS_WEB_PORT: "${UPS_WEB_PORT:-8100}"` changes the default to 8100. An explicit `UPS_WEB_PORT` in `.env` overrides that default. Choose an unused port from 1024 to 65535.

If the host IP changes, update `UPS_WEB_BIND` in `.env`. Run `bash up.sh` again to apply the changes. Update the firewall if it filters the chosen port, and edit the saved URL in Windows. For telemetry you can use a resolvable local hostname or [Tailscale MagicDNS](https://tailscale.com/docs/features/magicdns) instead of the IP, provided the dashboard listens on the address that the name resolves to.

## Optional remote beeper

1. Connect Ubuntu and Windows to Tailscale. Include the server's numeric Tailscale IPv4 in `UPS_WEB_BIND`.
2. On Ubuntu run `bash enable-beeper.sh`. It creates `.beeper.env` with a client key and a separate NUT password and reapplies the Compose configuration. Keep this file private.
3. On Windows use the server's numeric Tailscale IPv4 and dashboard port as the server URL. Enter the value of `BEEPER_TOKEN` from `.beeper.env` into **Beeper key** and save. Leave the field empty for telemetry only.

If the Tailscale IP changes, update `UPS_WEB_BIND` and rerun `bash enable-beeper.sh` to update the authorized IP while retaining the key. Enter the key again when you save a changed URL in Windows.

The dashboard is read-only. The Windows app can enable, disable or temporarily mute the beeper. The corresponding API routes are `POST /api/beeper/enable`, `POST /api/beeper/disable` and `POST /api/beeper/mute`. They require an empty body, a Tailscale connection to the configured IP and `Authorization: Bearer <key>`. Enable and disable are checked against the state reported by NUT.

## USB reconnect and Home Assistant

Run `bash enable-auto-usb.sh` after the first start to check for USB address changes once per minute and recreate just the NUT container when needed. This requires a running cron service, a stack path without spaces, and Docker access for the same user. Without cron, run `bash up.sh` after moving or reconnecting the USB cable.

For Home Assistant running on the same host network, use the [Network UPS Tools integration](https://www.home-assistant.io/integrations/nut/) with host `127.0.0.1`, port `3493` and blank credentials. NUT listens only on loopback.

## Diagnostics and stopping the stack

```sh
docker logs --tail 30 eaton-ups-nut
docker logs --tail 30 eaton-ups-web
docker exec eaton-ups-nut upsc eaton@127.0.0.1
curl -fsS http://127.0.0.1:8099/api/ups
```

`GET /healthz` checks the dashboard process. `GET /api/ups` returns the readings or HTTP 503 while NUT has no data. If you changed the port, use your configured port in the last command.

To stop the stack, run `rm -f .auto-usb` from the stack directory to pause the optional cron check, then `docker stop eaton-ups-web eaton-ups-nut`. To resume, run `bash up.sh` and optionally `bash enable-auto-usb.sh`.
