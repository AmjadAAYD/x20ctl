# Native app on every Linux distro

Owner request, 10 Oct 2026: X20CTL is public software, so it must run on Linux, Ubuntu, Arch, Nobara, Garuda and every other well-known distro.

## Where things stand

- The native app (`desktop-dotnet/X20Ctl.Product`) is WPF. WPF exists only on Windows, so this app cannot run on Linux as it is. Wine is not an answer for a controller app.
- What already works on Linux is the older React + Python line (`docs/linux-desktop-validation.md`: GTK/WebKitGTK, Ubuntu 22.04 baseline, `.run` and tar.gz). It is not the new design.
- `X20Ctl.Zone` (drafts, catalog, layout, Zone state; 71 tests) is plain .NET and runs on Linux unchanged.
- Windows-only pieces besides WPF are the input and scan backends: XInput, SetupAPI/HID inventory and Raw Input (Controller Check, Macros Record, live input), plus the tray icon and the game library (Steam/Epic paths).

## The path

1. **UI toolkit: Avalonia.** It is the cross-platform .NET UI closest to WPF (XAML, styles, bindings, transforms, animations), and it renders the same on Windows, Linux and macOS. The screens are rebuilt in Avalonia with the same design. Code-behind moves over mostly as-is; effects (drop shadows, opacity masks) and storyboards need Avalonia equivalents.
2. **Input on Linux.** Use evdev (`/dev/input/event*`) for live input, Macros Record and the Controller Check's guided steps. Use hidraw and udev for the device list, HID descriptors (stick bit depth) and report rate. Everything stays read-only, as on Windows. Access needs the usual udev rule for the user's seat; the app explains it instead of changing permissions.
3. **One codebase, two shells.** For a time, Windows keeps WPF while the Avalonia build grows, or both switch to Avalonia once it matches.
4. **Packages that cover every distro:**
   - **AppImage:** one file that runs on practically any glibc distro (Ubuntu, Debian, Mint, Pop!_OS, Fedora, Nobara, openSUSE, Arch, Garuda, Manjaro, EndeavourOS and others).
   - **Flatpak (Flathub):** the universal option, including immutable systems (SteamOS, Bazzite, Fedora Silverblue).
   - **Native packages for convenience:** `.deb` (Ubuntu/Debian family), `.rpm` (Fedora/Nobara/openSUSE) and an AUR `PKGBUILD` (Arch/Garuda/Manjaro).
   - **Edge cases:** musl distros (Alpine) get a `linux-musl-x64` self-contained build; NixOS gets a flake or the Flatpak.
5. **Verification.** Build once in a clean container, then smoke-test the AppImage and Flatpak in containers or VMs of Ubuntu LTS, Fedora/Nobara, Arch/Garuda, Debian and openSUSE, with Xvfb for the window. Physical controller testing on Linux stays separate and is reported honestly.

## Not claimed until done

No Linux build of the native app exists yet. Nothing here is released.
