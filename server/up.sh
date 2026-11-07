#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

mode="${1:-}"
exec 9> .usb.lock
if [[ "$mode" == --reconcile ]]; then
    flock -n 9 || exit 0
    [[ -f .auto-usb ]] || exit 0
else
    flock 9
fi

devices=()
for entry in /sys/bus/usb/devices/*; do
    [[ -r "$entry/idVendor" && -r "$entry/idProduct" ]] || continue
    [[ $(<"$entry/idVendor") == 0463 && $(<"$entry/idProduct") == ffff ]] || continue
    read -r bus < "$entry/busnum"
    read -r device < "$entry/devnum"
    printf -v path '/dev/bus/usb/%03d/%03d' "$((10#$bus))" "$((10#$device))"
    [[ -c "$path" ]] && devices+=("$path")
done
if (( ${#devices[@]} != 1 )); then
    [[ "$mode" == --reconcile ]] && exit 0
    printf 'Expected one UPS with USB ID 0463:ffff, found %d.\n' "${#devices[@]}" >&2
    exit 1
fi

export UPS_USB_DEVICE="${devices[0]}"
if [[ -d "${DOCKER_CONFIG:-$HOME/.docker}" && ! -w "${DOCKER_CONFIG:-$HOME/.docker}" ]]; then
    mkdir -p .docker-config
    export DOCKER_CONFIG="$PWD/.docker-config"
fi
if [[ "$mode" == --reconcile ]]; then
    mapped=$(docker inspect -f '{{.State.Status}} {{range .HostConfig.Devices}}{{.PathOnHost}}{{end}}' eaton-ups-nut 2>/dev/null || true)
    case "$mapped" in
        "running $UPS_USB_DEVICE"|"restarting $UPS_USB_DEVICE") exit 0 ;;
    esac
    printf '%s · UPS USB: %s\n' "$(date -Is)" "$UPS_USB_DEVICE"
    docker compose up -d --no-build --no-deps --pull never nut
else
    printf 'UPS USB: %s\n' "$UPS_USB_DEVICE"
    docker compose up -d --build --pull never
fi
