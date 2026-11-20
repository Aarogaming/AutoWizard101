#!/bin/sh
set -eu
workspace=$1
channel=$2
base=$3
export MAELSTROM_DODGE_SEED=$4
mode=$5
speed=$6
evaluation=$7
tools="$workspace/.local/wsl-dodge-tools"
game="$workspace/.local/wsl-dodge-game-check"
if [ "$mode" = stepped ]; then
    game="$workspace/.local/wsl-dodge-step-game"
    export MAELSTROM_STEP_CHANNEL="$base/step" MAELSTROM_STEP_SPEED="$speed"
    if [ "$evaluation" = endurance ]; then export MAELSTROM_DODGE_EVAL=1; fi
fi
mount --make-rprivate /
mount -t tmpfs -o mode=1777 tmpfs /tmp
mkdir "$base/upper" "$base/work" "$base/fb" "$base/cache" "$base/data" "$base/step"
mount -t overlay overlay -o "lowerdir=/usr/bin,upperdir=$base/upper,workdir=$base/work" /usr/bin
mkdir /tmp/.X11-unix
chmod 1777 /tmp/.X11-unix
ln -s "$tools/root-current/usr/bin/xkbcomp" /usr/bin/xkbcomp
export LD_LIBRARY_PATH="$tools/root-current/usr/lib/x86_64-linux-gnu"
export DISPLAY=:88 LIBGL_ALWAYS_SOFTWARE=1 LP_NUM_THREADS=1 MESA_SHADER_CACHE_DISABLE=true
export XDG_CACHE_HOME="$base/cache" XDG_DATA_HOME="$base/data"
"$tools/root-current/usr/bin/Xvfb" :88 -screen 0 480x720x24 -nolisten tcp -ac -noreset -fbdir "$base/fb" > "$base/xvfb.log" 2>&1 &
server=$!
gamepid=''
cleanup() {
    if [ -n "$gamepid" ]; then kill "$gamepid" 2>/dev/null || true; wait "$gamepid" 2>/dev/null || true; fi
    kill "$server" 2>/dev/null || true
    wait "$server" 2>/dev/null || true
    if [ -f "$base/game.log" ]; then tail -n 80 "$base/game.log" > "$channel/guest-game.log" 2>/dev/null || true; fi
    if [ -f "$base/step/evaluation-audit.jsonl" ]; then cp "$base/step/evaluation-audit.jsonl" "$channel/evaluation-audit.jsonl" 2>/dev/null || true; fi
}
trap cleanup EXIT HUP INT TERM
i=0
while ! "$tools/root-current/usr/bin/xdotool" getmouselocation >/dev/null 2>&1; do
    kill -0 "$server" 2>/dev/null || { cat "$base/xvfb.log" >&2; exit 1; }
    i=$((i + 1)); [ "$i" -lt 40 ] || exit 1; sleep 0.1
done
if [ "$mode" = stepped ]; then
    nice -n 15 "$tools/godot/Godot_v4.7.2-stable_linux.x86_64" --path "$game" --audio-driver Dummy --display-driver x11 --fixed-fps 30 > "$base/game.log" 2>&1 &
else
    "$tools/godot/Godot_v4.7.2-stable_linux.x86_64" --path "$game" --audio-driver Dummy --display-driver x11 > "$base/game.log" 2>&1 &
fi
gamepid=$!
i=0
window=''
while [ "$i" -lt 80 ]; do
    windows=$("$tools/root-current/usr/bin/xdotool" search --onlyvisible --name 'Dodge' 2>/dev/null || true)
    if [ -n "$windows" ]; then
        [ "$(printf '%s\n' "$windows" | wc -l)" -eq 1 ] || { echo 'Multiple game windows.' >&2; exit 1; }
        window=$windows
        break
    fi
    kill -0 "$gamepid" 2>/dev/null || { cat "$base/game.log" >&2; exit 1; }
    i=$((i + 1)); sleep 0.1
done
[ -n "$window" ] || { echo 'Game window did not appear.' >&2; exit 1; }
"$tools/root-current/usr/bin/xdotool" windowfocus "$window"
python3 "$workspace/tools/wsl_dodge_guest.py" "$channel" "$base/fb/Xvfb_screen0" "$window" "$tools/root-current/usr/bin/xdotool" "$mode" "$speed" "$base/step"
