#!/usr/bin/env bash
set -euo pipefail

if [[ -n "${NUT_BEEPER_PASSWORD:-}" ]]; then
    [[ "$NUT_BEEPER_PASSWORD" =~ ^[0-9a-f]{64}$ ]] || exit 1
    umask 077
    export NUT_CONFPATH=/run/nut/config
    mkdir -p "$NUT_CONFPATH"
    cp /etc/nut/{nut.conf,ups.conf,upsd.conf,upsd.users} "$NUT_CONFPATH/"
    printf '\n[beeper]\n    password = %s\n    instcmds = beeper.enable\n    instcmds = beeper.disable\n    instcmds = beeper.mute\n' \
        "$NUT_BEEPER_PASSWORD" >> "$NUT_CONFPATH/upsd.users"
fi

/lib/nut/usbhid-ups -F -u root -a eaton &
driver=$!
/usr/sbin/upsd -F -u root &
server=$!

cleanup() {
    kill "$driver" "$server" 2>/dev/null || true
    wait "$driver" "$server" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 0' TERM INT
wait -n "$driver" "$server"
exit 1
