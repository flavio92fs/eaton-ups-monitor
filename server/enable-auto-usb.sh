#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
[[ "$PWD" =~ ^[a-zA-Z0-9_./-]+$ ]] || { echo 'Use a path without spaces for cron.' >&2; exit 1; }
entry="* * * * * /bin/bash $PWD/up.sh --reconcile >> $PWD/usb-reconnect.log 2>&1 # eaton-ups-auto-usb"
old=$(crontab -l 2>/dev/null || true)
if ! grep -Fxq "$entry" <<< "$old"; then
    { printf '%s\n' "$old" | sed '/# eaton-ups-auto-usb$/d'; printf '%s\n' "$entry"; } | crontab -
fi
touch .auto-usb
