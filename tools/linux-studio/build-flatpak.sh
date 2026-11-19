#!/usr/bin/env bash
# Package an existing self-contained linux-x64 publish directory. Requires Linux, flatpak and flatpak-builder.
# Usage: bash build-flatpak.sh PUBLISH ABSOLUTE_OUTPUT.flatpak
set -euo pipefail
[[ "$(uname -s)" == Linux ]] || { echo 'Flatpak packaging requires Linux.' >&2; exit 1; }
publish="$(cd "${1:?publish directory required}" && pwd)"
output="${2:?absolute output bundle path required}"
[[ "$output" = /*.flatpak ]] || { echo 'Output must be an absolute .flatpak path.' >&2; exit 1; }
test -f "$publish/x20ctl-studio"
here="$(cd "$(dirname "$0")" && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cp "$here/io.github.AmjadAAYD.x20ctl.yml" "$here/x20ctl.desktop" "$here/x20ctl.png" "$here/x20ctl.metainfo.xml" "$work/"
mkdir -p "$work/publish"
cp -r "$publish/." "$work/publish/"
flatpak --user remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak-builder --user --assumeyes --install-deps-from=flathub --disable-rofiles-fuse \
  --repo="$work/repo" "$work/build" "$work/io.github.AmjadAAYD.x20ctl.yml"
flatpak build-bundle --arch=x86_64 --runtime-repo=https://flathub.org/repo/flathub.flatpakrepo \
  "$work/repo" "$output" io.github.AmjadAAYD.x20ctl stable
test -s "$output"
