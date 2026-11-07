#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
ts_ip=$(tailscale ip -4)
python3 - "$ts_ip" <<'PY'
import ipaddress
import os
import re
import secrets
import sys
import tempfile
from pathlib import Path

address = ipaddress.IPv4Address(sys.argv[1])
if address not in ipaddress.IPv4Network("100.64.0.0/10"):
    raise SystemExit("A Tailscale IPv4 address is required.")

path = Path(".beeper.env")
if path.is_symlink() or (path.exists() and not path.is_file()):
    raise SystemExit("Expected .beeper.env to be a regular file.")
if path.exists():
    contents = path.read_text(encoding="utf-8")
    previous = re.fullmatch(
        r"BEEPER_TAILSCALE_IP=([^\n]+)\nBEEPER_TOKEN=([0-9a-f]{64})\n"
        r"NUT_BEEPER_PASSWORD=([0-9a-f]{64})\n?", contents,
    )
    if not previous:
        raise SystemExit("Invalid .beeper.env. Check the existing configuration.")
    if previous[1] == str(address):
        path.chmod(0o600)
        sys.exit()
    token, password = previous[2], previous[3]
else:
    token, password = secrets.token_hex(32), secrets.token_hex(32)

with tempfile.NamedTemporaryFile(mode="w", dir=".", prefix=".beeper.env.", delete=False) as config:
    try:
        config.write(f"BEEPER_TAILSCALE_IP={address}\n")
        config.write(f"BEEPER_TOKEN={token}\n")
        config.write(f"NUT_BEEPER_PASSWORD={password}\n")
        config.close()
        os.replace(config.name, path)
    finally:
        Path(config.name).unlink(missing_ok=True)
PY
exec bash up.sh
