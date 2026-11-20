#!/bin/sh
# Create a per-run private X11 display; never alter the shared WSL display.
set -eu
workspace=$1
channel=$2
seed=$3
mode=${4:-realtime}
speed=${5:-1}
evaluation=${6:-standard}
case "$mode:$speed" in realtime:1|stepped:0.25|stepped:0.5|stepped:1) ;; *) echo 'Invalid training mode or speed.' >&2; exit 2 ;; esac
case "$mode:$evaluation" in realtime:standard|stepped:standard|stepped:endurance) ;; *) echo 'Invalid evaluation mode.' >&2; exit 2 ;; esac
base=$(mktemp -d /dev/shm/maelstrom-wsl.XXXXXX)
cleanup() {
    case "$base" in /dev/shm/maelstrom-wsl.*) rm -rf -- "$base" ;; esac
}
trap cleanup EXIT HUP INT TERM
unshare --user --map-root-user --mount --net sh "$workspace/tools/wsl_dodge_session.sh" "$workspace" "$channel" "$base" "$seed" "$mode" "$speed" "$evaluation"
