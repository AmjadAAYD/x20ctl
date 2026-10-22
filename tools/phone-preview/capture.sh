#!/usr/bin/env bash
# Runs the phone app on an Android emulator and records what it shows: a screen recording of the first seconds,
# timed screenshots through the intro, then screenshots after a few taps. Used by .github/workflows/android.yml so the
# app can be looked at without a phone. Usage: capture.sh APK OUT_DIR
set -u
apk="$1"; out="$2"; pkg=io.github.amjadaayd.x20ctl
mkdir -p "$out"
adb wait-for-device
adb shell input keyevent 82 || true
adb install -r "$apk" || exit 1
size=$(adb shell wm size | tr -d '\r' | awk '{print $NF}'); echo "screen $size" | tee "$out/screen.txt"
adb logcat -c
adb shell screenrecord --bit-rate 6000000 --time-limit 25 /sdcard/run.mp4 & rec=$!
sleep 1
adb shell monkey -p "$pkg" -c android.intent.category.LAUNCHER 1 >/dev/null
t0=$(date +%s.%N)
shot() { adb exec-out screencap -p > "$out/$1.png"; }
# timed shots through the intro (the Windows intro runs about 7.4 s)
for at in 0.5 1.0 1.5 2.0 2.5 3.0 3.5 4.0 4.5 5.0 5.5 6.0 6.5 7.0 7.5 8.0 9.0 10 12 15; do
  now=$(date +%s.%N); wait_s=$(echo "$t0 + $at - $now" | bc); [ "${wait_s:0:1}" != "-" ] && sleep "$wait_s"
  shot "t$(printf '%05.1f' "$at")s"
done
wait $rec; adb pull /sdcard/run.mp4 "$out/run.mp4" >/dev/null
# after the intro: a few touches at screen fractions (or "key KEYCODE"), each followed by shots while it settles
w=${size%x*}; h=${size#*x}; if [ "$w" -lt "$h" ]; then t=$w; w=$h; h=$t; fi
tap() { if [ "$2" = key ]; then adb shell input keyevent "$3"; else adb shell input tap $(echo "$w * $2 / 1" | bc) $(echo "$h * $3 / 1" | bc); fi; sleep 0.3; shot "$1-a"; sleep 1.2; shot "$1-b"; }
[ -f "$(dirname "$0")/taps.txt" ] && while read -r name fx fy; do [ -n "$name" ] && [ "${name:0:1}" != "#" ] && tap "$name" "$fx" "$fy"; done < "$(dirname "$0")/taps.txt"
adb shell dumpsys gfxinfo "$pkg" > "$out/gfxinfo.txt" 2>&1
adb shell pidof "$pkg" > "$out/running.txt" || echo "not running" > "$out/running.txt"
adb logcat -d > "$out/logcat.txt"
exit 0
