#!/usr/bin/env bash
# Runs the phone app on an Android emulator and records what it shows: a screen recording of the first seconds,
# timed screenshots through the intro, then screenshots after each step in steps.txt. Used by
# .github/workflows/android.yml so the app can be looked at without a phone. Usage: capture.sh APK OUT_DIR
set -u
apk="$1"; out="$2"; here="$(cd "$(dirname "$0")" && pwd)"; pkg=io.github.amjadaayd.x20ctl
mkdir -p "$out"
a() { adb "$@" </dev/null; }
a wait-for-device
a shell input keyevent 82 || true
# the one-time "Viewing full screen" notice would cover the first screens
a shell settings put secure immersive_mode_confirmations confirmed || true
# a busy emulator's "isn't responding" dialogs would cover the app; let the system settle before starting
a shell settings put global hide_error_dialogs 1 || true
sleep 20
a install -r "$apk" || exit 1
size=$(a shell wm size | tr -d '\r' | awk '{print $NF}'); echo "screen $size" | tee "$out/screen.txt"
w=${size%x*}; h=${size#*x}; if [ "$w" -lt "$h" ]; then t=$w; w=$h; h=$t; fi
a logcat -c
a shell screenrecord --bit-rate 6000000 --time-limit 30 /sdcard/run.mp4 & rec=$!
sleep 1
a shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1 >/dev/null
t0=$(date +%s.%N)
shot() { a exec-out screencap -p > "$out/$1.png"; }
for at in 0.5 1.0 1.5 2.0 2.5 3.0 3.5 4.0 4.5 5.0 5.5 6.0 6.5 7.0 7.5 8.0 8.5 9.0 9.5 10 11 12 13 14 16 18 20; do
  now=$(date +%s.%N); wait_s=$(echo "$t0 + $at - $now" | bc); [ "${wait_s:0:1}" != "-" ] && sleep "$wait_s"
  shot "t$(printf '%05.1f' "$at")s"
done
# finds a control by its text in the accessibility tree and taps its middle; prints nothing when it isn't there
tap_text() {
  a shell uiautomator dump /sdcard/ui.xml >/dev/null 2>&1; a shell cat /sdcard/ui.xml > "$out/ui-last.xml" 2>/dev/null
  python3 - "$out/ui-last.xml" "$1" <<'PY'
import re,sys
xml=open(sys.argv[1],encoding='utf-8',errors='replace').read(); want=sys.argv[2]
for m in re.finditer(r'<node [^>]*>',xml):
    n=m.group(0); t=re.search(r' text="([^"]*)"',n); d=re.search(r' content-desc="([^"]*)"',n); b=re.search(r'bounds="\[(\d+),(\d+)\]\[(\d+),(\d+)\]"',n)
    if b and want in ((t and t.group(1)) or '', (d and d.group(1)) or ''):
        x1,y1,x2,y2=map(int,b.groups()); print((x1+x2)//2,(y1+y2)//2); break
PY
}
step() { # name kind arg [fx fy]
  local name=$1 kind=$2 arg=$3
  case $kind in
    key) a shell input keyevent "$arg" ;;
    at) a shell input tap $(echo "$w * $arg / 1" | bc) $(echo "$h * $4 / 1" | bc) ;;
    text) arg=${arg//_/ }; xy=$(tap_text "$arg"); if [ -n "$xy" ]; then a shell input tap $xy; else echo "$name: no control showing \"$arg\"" >> "$out/steps-missed.txt"; [ -n "${4:-}" ] && a shell input tap $(echo "$w * $4 / 1" | bc) $(echo "$h * $5 / 1" | bc); fi ;;
    swipe) a shell input swipe $(echo "$w * $arg / 1" | bc) $(echo "$h * $4 / 1" | bc) $(echo "$w * $5 / 1" | bc) $(echo "$h * $6 / 1" | bc) 250 ;;
  esac
  sleep 0.25; shot "$name-a"; sleep 1.2; shot "$name-b"
}
n=0
while read -r name kind arg rest; do
  [ -z "$name" ] || [ "${name:0:1}" = "#" ] && continue
  n=$((n+1)); step "$(printf 's%02d' $n)-$name" "$kind" "$arg" $rest
done < "$here/steps.txt"
wait $rec; a pull /sdcard/run.mp4 "$out/run.mp4" >/dev/null
a shell dumpsys gfxinfo "$pkg" > "$out/gfxinfo.txt" 2>&1
a shell pidof "$pkg" > "$out/running.txt" || echo "not running" > "$out/running.txt"
a logcat -d > "$out/logcat.txt"
grep -E " X20CTL|AndroidRuntime|mono-rt|DOTNET" "$out/logcat.txt" > "$out/app-log.txt" || true
exit 0
