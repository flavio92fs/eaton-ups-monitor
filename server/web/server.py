import ipaddress
import hmac
import json
import os
import re
import shlex
import socket
import threading
import time
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


UPS = "eaton"
TAILNET = ipaddress.IPv4Network("100.64.0.0/10")
BEEPER_IP = os.environ.get("BEEPER_TAILSCALE_IP", "")
BEEPER_TOKEN = os.environ.get("BEEPER_TOKEN", "")
NUT_PASSWORD = os.environ.get("NUT_BEEPER_PASSWORD", "")
BEEPER_LOCK = threading.Lock()
BEEPER_COMMANDS = {"enable": "enabled", "disable": "disabled", "mute": "muted"}
PUBLIC_VARS = frozenset("""
    device.mfr device.model ups.model ups.firmware ups.status ups.load
    ups.realpower ups.realpower.nominal ups.power ups.power.nominal ups.beeper.status
    battery.charge battery.runtime battery.voltage input.voltage output.voltage
    input.frequency output.frequency
""".split())
PAGE = Path(__file__).with_name("index.html").read_bytes()


def read_ups():
    with socket.create_connection(("127.0.0.1", 3493), timeout=1.5) as connection:
        connection.sendall(f"LIST VAR {UPS}\n".encode("ascii"))
        with connection.makefile("r", encoding="utf-8", errors="replace") as response:
            first = response.readline(4096).strip()
            if first != f"BEGIN LIST VAR {UPS}":
                raise ValueError(first if first.startswith("ERR ") else "NUT unavailable")
            data = {}
            for _ in range(256):
                line = response.readline(4096)
                if line.strip() == f"END LIST VAR {UPS}":
                    status = set(data.get("ups.status", "").split())
                    if not status or status.intersection({"WAIT", "OFFLINE"}):
                        raise ValueError("UPS data not ready")
                    return data
                if not line.endswith("\n"):
                    break
                row = shlex.split(line)
                if len(row) != 4 or row[:2] != ["VAR", UPS]:
                    raise ValueError("Invalid NUT response")
                if row[2] in PUBLIC_VARS:
                    data[row[2]] = row[3]
            raise ValueError("Incomplete NUT response")


def set_beeper(action):
    if read_ups().get("ups.model") != "Eaton 5E 1600 G2":
        raise ValueError("Unexpected UPS")
    with socket.create_connection(("127.0.0.1", 3493), timeout=1.5) as connection:
        with connection.makefile("r", encoding="ascii") as response:
            def request(command):
                connection.sendall((command + "\n").encode("ascii"))
                return response.readline(256).strip()

            if (request("USERNAME beeper") != "OK"
                    or request(f"PASSWORD {NUT_PASSWORD}") != "OK"
                    or request("SET TRACKING ON") != "OK"):
                raise ValueError("NUT credentials or tracking rejected")
            tracking = request(f"INSTCMD {UPS} beeper.{action}")
            match = re.fullmatch(r"OK TRACKING ([0-9a-fA-F-]{36})", tracking)
            if not match:
                raise ValueError("NUT rejected command")
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                result = request(f"GET TRACKING {match[1]}")
                if result == "SUCCESS":
                    break
                if result != "PENDING":
                    raise ValueError("NUT command failed")
                time.sleep(0.25)
            else:
                raise TimeoutError("NUT command not confirmed")
    if action != "mute":
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            if read_ups().get("ups.beeper.status") == BEEPER_COMMANDS[action]:
                return
            time.sleep(0.5)
        raise TimeoutError("Beeper state not confirmed")


class Handler(BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(5)

    def do_GET(self):
        path = urlsplit(self.path).path
        if path == "/":
            self.reply(200, "text/html; charset=utf-8", PAGE)
        elif path == "/healthz":
            self.json_reply(200, {"ok": True})
        elif path == "/api/ups":
            try:
                data = read_ups()
            except (OSError, ValueError):
                self.json_reply(503, {"ok": False, "error": "ups_unavailable"})
                return
            self.json_reply(200, {
                "ok": True,
                "updated_at": datetime.now(timezone.utc).isoformat(),
                "values": data,
            })
        else:
            self.json_reply(404, {"ok": False, "error": "not_found"})

    def do_POST(self):
        actions = {f"/api/beeper/{action}": action for action in BEEPER_COMMANDS}
        action = actions.get(self.path)
        if action is None:
            self.json_reply(404, {"ok": False, "error": "not_found"})
            return
        if (self.server.server_address[0] != BEEPER_IP
                or ipaddress.IPv4Address(self.client_address[0]) not in TAILNET
                or not re.fullmatch(r"[0-9a-f]{64}", BEEPER_TOKEN)
                or not re.fullmatch(r"[0-9a-f]{64}", NUT_PASSWORD)):
            self.json_reply(403, {"ok": False, "error": "tailscale_required"})
            return
        authorization = self.headers.get_all("Authorization", [])
        if len(authorization) != 1 or not hmac.compare_digest(
                authorization[0].encode(), ("Bearer " + BEEPER_TOKEN).encode()):
            self.json_reply(401, {"ok": False, "error": "unauthorized"})
            return
        if ("Origin" in self.headers or "Transfer-Encoding" in self.headers
                or self.headers.get("Content-Length", "0") != "0"):
            self.json_reply(400, {"ok": False, "error": "invalid_request"})
            return
        if not BEEPER_LOCK.acquire(blocking=False):
            self.json_reply(409, {"ok": False, "error": "command_in_progress"})
            return
        try:
            set_beeper(action)
        except (OSError, ValueError):
            self.json_reply(503, {"ok": False, "error": "command_unconfirmed"})
        else:
            self.json_reply(200, {"ok": True, "action": action})
        finally:
            BEEPER_LOCK.release()

    def json_reply(self, code, payload):
        self.reply(code, "application/json; charset=utf-8", json.dumps(payload).encode())

    def reply(self, code, content_type, body):
        self.send_response(code)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Content-Security-Policy",
                         "default-src 'none'; connect-src 'self'; "
                         "style-src 'unsafe-inline'; script-src 'unsafe-inline'; "
                         "base-uri 'none'; frame-ancestors 'none'")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, *_):
        pass


def listen(address, port):
    while True:
        try:
            with ThreadingHTTPServer((address, port), Handler) as server:
                print(f"UPS web listening on {address}:{port}")
                server.serve_forever()
        except OSError as error:
            print(f"Cannot listen on {address}:{port}: {error}")
            time.sleep(10)


if __name__ == "__main__":
    port = int(os.environ.get("UPS_WEB_PORT", "8099"))
    if not 1024 <= port <= 65535:
        raise SystemExit("UPS_WEB_PORT must be between 1024 and 65535")
    addresses = dict.fromkeys(
        ipaddress.IPv4Address(value.strip())
        for value in os.environ.get("UPS_WEB_BIND", "127.0.0.1").split(",")
    )
    for address in addresses:
        if not address.is_private and address not in TAILNET:
            raise SystemExit(f"Use a LAN, loopback or Tailscale address: {address}")
        if address.is_unspecified:
            raise SystemExit("Specify explicit interfaces, not 0.0.0.0")
        threading.Thread(target=listen, args=(str(address), port), daemon=True).start()
    threading.Event().wait()
