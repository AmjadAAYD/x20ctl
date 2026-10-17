#!/usr/bin/env bash
# Builds the Linux Studio packages from a checkout: a tarball and an AppImage.
#   tools/linux-studio/build.sh VERSION OUTDIR
# Needs the .NET 10 SDK. The AppImage step also needs curl and a FUSE-less run (appimagetool is extracted, not mounted).
set -euo pipefail
version="${1:?usage: build.sh VERSION OUTDIR}"; out="$(mkdir -p "${2:?usage: build.sh VERSION OUTDIR}" && cd "$2" && pwd)"
here="$(cd "$(dirname "$0")" && pwd)"; root="$(cd "$here/../.." && pwd)"
work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT

dotnet publish "$root/desktop-dotnet/X20Ctl.Linux/X20Ctl.Linux.csproj" -c Release -r linux-x64 --self-contained true \
  -p:Version="$version" -p:DebugType=none -o "$work/publish"

# --- tarball: run in place, or install with ./install.sh
tar_dir="$work/x20ctl-studio-$version"; mkdir -p "$tar_dir"
cp -r "$work/publish/." "$tar_dir/"; cp "$here/x20ctl.desktop" "$here/x20ctl.png" "$root/LICENSE" "$root/NOTICE" "$tar_dir/"
cat > "$tar_dir/install.sh" <<'INSTALL'
#!/usr/bin/env sh
# Installs X20CTL for your user only: ~/.local/opt/x20ctl, a launcher and a menu entry. Remove it with: rm -r ~/.local/opt/x20ctl ~/.local/bin/x20ctl-studio ~/.local/share/applications/x20ctl.desktop
set -e
here="$(cd "$(dirname "$0")" && pwd)"; dest="$HOME/.local/opt/x20ctl"
mkdir -p "$dest" "$HOME/.local/bin" "$HOME/.local/share/applications" "$HOME/.local/share/icons/hicolor/512x512/apps"
cp -r "$here/." "$dest/"; chmod 755 "$dest/x20ctl-studio"
ln -sf "$dest/x20ctl-studio" "$HOME/.local/bin/x20ctl-studio"
cp "$here/x20ctl.png" "$HOME/.local/share/icons/hicolor/512x512/apps/x20ctl.png"
sed "s|^Exec=.*|Exec=$dest/x20ctl-studio|" "$here/x20ctl.desktop" > "$HOME/.local/share/applications/x20ctl.desktop"
echo "Installed. Start X20CTL from your menu, or run x20ctl-studio."
INSTALL
chmod 755 "$tar_dir/install.sh" "$tar_dir/x20ctl-studio"
tar -C "$work" -czf "$out/X20CTL-$version-linux-x64.tar.gz" "x20ctl-studio-$version"

# --- the same for musl distros (Alpine, Chimera, Void-musl): a second self-contained build of the same tarball
dotnet publish "$root/desktop-dotnet/X20Ctl.Linux/X20Ctl.Linux.csproj" -c Release -r linux-musl-x64 --self-contained true   -p:Version="$version" -p:DebugType=none -o "$work/publish-musl"
musl_dir="$work/x20ctl-studio-$version-musl"; mkdir -p "$musl_dir"
cp -r "$work/publish-musl/." "$musl_dir/"; cp "$tar_dir/install.sh" "$tar_dir/x20ctl.desktop" "$tar_dir/x20ctl.png" "$tar_dir/LICENSE" "$tar_dir/NOTICE" "$musl_dir/"
chmod 755 "$musl_dir/install.sh" "$musl_dir/x20ctl-studio"
tar -C "$work" -czf "$out/X20CTL-$version-linux-musl-x64.tar.gz" "x20ctl-studio-$version-musl"

# --- AppImage, with appimagetool pinned by hash
tool_url="https://github.com/AppImage/appimagetool/releases/download/1.9.0/appimagetool-x86_64.AppImage"
tool_sha="46fdd785094c7f6e545b61afcfb0f3d98d8eab243f644b4b17698c01d06083d1"
curl -fsSL -o "$work/appimagetool" "$tool_url"
echo "$tool_sha  $work/appimagetool" | sha256sum -c -
chmod 755 "$work/appimagetool"; (cd "$work" && ./appimagetool --appimage-extract >/dev/null)
appdir="$work/X20CTL.AppDir"; mkdir -p "$appdir/usr/bin"
cp -r "$work/publish/." "$appdir/usr/bin/"; chmod 755 "$appdir/usr/bin/x20ctl-studio"
cp "$here/x20ctl.desktop" "$appdir/x20ctl.desktop"; cp "$here/x20ctl.png" "$appdir/x20ctl.png"
cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
here="$(dirname "$(readlink -f "$0")")"
exec "$here/usr/bin/x20ctl-studio" "$@"
APPRUN
chmod 755 "$appdir/AppRun"
ARCH=x86_64 "$work/squashfs-root/AppRun" "$appdir" "$out/X20CTL-$version-linux-x86_64.AppImage"
ls -la "$out"
