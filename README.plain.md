# X20CTL

**A controller studio for EasySMX pads.** Remap, shape curves, build macros, customize X20 Pro lighting & screen, talk to offline Agent XCTL, and use PC mode on Windows and Linux.

**V0.026 - Windows & Linux Studio.**

[Download V0.026](https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.026) · [FAQ](docs/FAQ.md) · [Updates (@AmjadCTL)](https://x.com/AmjadCTL) · [Buy EasySMX Controllers (Referral)](https://www.easysmx.com/?ref=X20CTL) · [Support Me on Ko-fi](https://ko-fi.com/x20ctl) · [Changelog](CHANGELOG.md) · [Controller not working?](IF-YOUR-CONTROLLER-ISNT-WORKING.md) · [Discussions](https://github.com/AmjadAAYD/x20ctl/discussions) · [Issues](https://github.com/AmjadAAYD/x20ctl/issues)

**Thank you, EasySMX Kiin & Team EasySMX for supplying an EasySMX X20 Pro to build and test X20CTL.**

*This is the plain-text version of the README. The main README is an illustrated page; this copy keeps everything selectable and copyable and backs Agent XCTL's offline help search.*

> **Independent project.** X20CTL is open-source software. EasySMX supplied an X20 Pro to help develop it, but the app is not made, endorsed, sponsored or supported by EasySMX. Product names belong to their owners and are used only to say which controllers X20CTL works with.

## What it is

X20CTL is a desktop studio for EasySMX controllers on Windows (.NET 10 WPF + native C++ engine) and Linux (Avalonia). It gives every pad a console-style studio: remap buttons, shape stick and trigger curves, build macros for the back paddles, preview 4-zone vibration, control X20 Pro RGB lighting and its built-in `160x80` screen, ask offline Agent XCTL, check controller hardware, or reshape any pad through PC mode. It runs entirely on your computer with no account and no telemetry. Up to four players each pick their own controller, and every page can be driven with a gamepad.

## Reverse engineering

EasySMX configures its pads from a phone app (KeyLinker), and the protocol was never documented. X20CTL recovers those commands from real hardware, checks them against physical controllers, and encodes each 20-byte BLE GATT packet (`opcode`, `length`, `serial`, `nonce`, `payload`, `CRC-8` reflected polynomial `0xEB` initial `0x00`).

- **Transport:** Bluetooth LE GATT (+ USB serial for X20 Pro `160x80` screen uploads). X20CTL does not flash firmware and leaves OTA services alone.
- **Fingerprinting:** Matches real BLE address prefixes and USB descriptors while avoiding ambiguous `045E:028E` and `STK-7024X` (`0079:181C`) DirectInput traps.
- **Verified writes only:** Nothing is written to hardware memory unless read-back has been verified; everything else stays as a local draft or runs inside PC mode.
- **Read more:** [protocol reference](docs/01-protocol.md) · [reverse-engineering log](docs/00-findings.md) · [controller scan kit](docs/controller-scan-kit/) · [research policy](docs/research-policy.md)

## Studio features (V0.026)

### Controller Zone

The home screen shows up to four player cards; the active controller sits in the hero slot on the left while the others wait on the right. Select a card to switch pads, inspect battery and connection state, or press **+** to pick a controller model. X20CTL opens with an optional brief intro (**Esc** skips, **M** mutes, or disable it under **Tools → Opening intro**).

### Agent XCTL

Built-in handheld assistant rendered inside your chosen controller frame in the title bar. Ask in plain words (100% offline with 3,048 handwritten phrases), **Tab** autocomplete, `+ New chat` cancellation, live **Check-up** drift audits, interactive RGB cards, help doc search, and multi-step **Undo**. It checks every edit against fixed safety rules before applying changes to PC mode, and nothing you type leaves your machine. Type **help** to see everything it can do.

### Studio

#### Dashboard

Shows the active controller, player slot, an **Enable battery saving mode?** hardware switch with a collapsible **What's done in Eco mode?** breakdown (shuts off controller LEDs via BLE, kills vibration and PC game rumble, locks Vibration, RGB & Screen until Eco mode is turned off, stops audio FFT capture, and pauses background BLE polling), a live **Community Pulse & Alerts** bar (online user count + unread community notifications), and your local game library. The game shelf scans all fixed drives and local launcher manifests across Steam, Epic Games, Xbox / Game Pass, EA App, Ubisoft Connect, Battle.net, GOG, Riot, Heroic, and Lutris strictly offline so each game launches with its own controller setup.

#### RGB & Live Effects (X20 Pro)

Two-column PS5 console workspace with a single scrollable Left editor (**Color & Mode**, **Live Effects**, **Match App Theme**, **Player Color (P1-P4)**, and an expandable **Advanced** drawer for saved presets & foreground game auto-switch) paired with a compact Right preview column stacking the relit X20 Pro controller on top and a synchronized 2-strip HTML5 Canvas LED diffuser visualizer (**Left LED Strip** & **Right LED Strip**) below. Covers solid/native LEDs, live effects, Audio Studio (1024-pt Mid-Side FFT with 8 live spectrum bands, harmonic vocal separation, immediate silence decay, **Beat Flash Boost**, 6 reactive styles including **Auto** vibe detection, and per-motor audio role assignment), and Battery gauges. Simultaneous **2.4 GHz dongle (`413D:2131`) + unpaired BLE sync** applies lighting and settings whenever PC Bluetooth is enabled.

#### Screen (`160x80`, X20 Pro)

Pure OLED `#000000` preview embedded inside the EasySMX X20 Pro frame with built-in image, animated GIF, and offline **Animated Templates** (`Gamertag`, `Retro Grid`, `Acoustic Wave`, `Minimal Emblem`, `Starfield Warp`, `Plasma Ring`), accent swatches, brightness, contrast, rotation, `.bin` export, and **Browse Community** / **Share to Community** shortcuts for White flash mode upload over USB serial (`Controller off -> hold M -> plug USB`). Automatically locked while Eco Mode is enabled.

#### Buttons

Pick any front or rear button (`M1–M4` / `M1–M6`) using the interactive visualizer and press what it should send. Includes **Test mapping**, **All assignments**, and draft preview before writing.

#### Curves & Stick Lab Zone

Inline 3-zone workspace with the 10-point response curve editor & deadzones on the left, live hardware **Stick & Trigger Diagnostics** on top right, and quick controls + **Go Advanced?** full-screen PS5-inspired **Stick Lab Zone** on bottom right: Fritsch-Carlson monotonic cubic splines, Smart Curve Maker, Radial/Axial/Hybrid deadzones, anti-deadzones, gate morphing (`Circular <-> Square`), axis rotation, 4/8-way angle snapping, adaptive smoothing, trigger splines, snapback detector, and `X20C1.` share codes.

#### Community Hub

Available in both Native Mode (`X20`, `X20 Pro`) and PC Mode (`X20 Pro`, `X20`, `X05 Pro`, `X10`, `X15`, `D10`, `Dune 8K`, `Generic XInput`) with strict per-device capability filtering. Features local ECDSA P-256 Creator Identities (`X20-XXXX-XXXX` UIDs, `X20K1.` Recovery Keys, `64x64` PNG/GIF avatars, 27+ earned badges, **Watchers / Watching**, upvotes, Works Great verification, Compare & Cherry-Pick diff drawer with 5s live sandbox, Setup Requests, Slow-Mode Lounge, Owner Notes & Polls, and Pixel-Disarmed OLED Screen Image/GIF templates). Can be paused anytime via **Tools -> Offline mode**.

#### Macros

Step-by-step macro timeline for rear paddles (`M1–M4` on X20, `M1–M6` on X20 Pro) with button presses, stick directions, timed pauses, one-click **Quick presets** (`Rapid Fire`, `Drop Shot`, `Auto Sprint`, `Jump Shot`), and slot Copy/Paste actions.

#### Vibration

Structured multi-square Bento workspace on the Right (**Active Mode**, **Mood & Comfort**, **Quick Check + Open Advanced** panel, and a full-width bottom **Master Intensity** slider) with an expandable **Advanced Lab** toggle for blending, per-app calibration, fine feel sliders, motor tiles, waveform patterns, and per-motor audio roles. Paired on the Left with a real-time **3D Kinetic Acoustic Orb & Segmented 4-Motor VU Stage** (`STANDBY 0%` when idle) and the single **Enable Universal Haptics (`Ctrl+Alt+H`)** control (locked while Eco Mode is active):

- **Universal Haptics Engine (Games, Racing, FPS, Cinema & Music):** Always starts off on app launch until explicitly enabled from the Vibration tab, system tray, or global `Ctrl+Alt+H` hotkey. Embedded pure-C# classifier weights trained on real studio multitrack stems (`MUSDB18`) and real environmental audio (`ESC-50`) plus game SFX (`<0.2 ms` inference, 0% GPU), fused with direct 50 Hz XInput controller buttons/sticks/triggers (`A`, `B`, `X`, `Y`, `LB`, `RB`, `RT`, `LT`) even when behind fullscreen games, foreground auto-detection across 115+ built-in games/media apps, automatic audio-device hot-switch recovery, **Music Mode Sub-Presets** (`Chill`, `Vocals`, `Club`), **Per-App Surface & Combat Calibration** (`0-150%`), **Accessibility Stereo Cues** (directional Left/Right audio vibration), **Weapon Recoil Trigger Feel** presets (`Default`, `Pistol`, `SMG`, `Shotgun`, `Sniper`), `.x20hap` community export/import, and live per-profile tuning. Game profiles use un-AGC'd raw loudness with Right-Grip-first micro-taps (`walk`, `grass`, `jump`, `dash`, `light attack`) while reserving the Left Grip and `LT`/`RT` triggers for heavy impacts, gunshots, ABS braking, and loud peaks, automatically silencing menus (`OS cursor`), talking cutscenes, and background music themes. Music Mode calibrates against real mastered loudness (`0.34` anchor) with rolling baseline subtraction and a strict neural vocal gate so faint song intros stay whisper-light on the Right Grip with silent triggers while singing drives expressive fluctuating triggers.
- **Motors & Test (Advanced):** Independent grip and trigger motor sliders (`Left/Right grip`, `Left/Right trigger`), per-motor role badges, isolated hardware tests, instant tactile effect previews, and PC Mode game rumble amplification up to `200%`. On 2.4 GHz dongle or wired USB, open **Trigger Setup (M + Back)** and hold **M + BACK** on the controller to toggle LT/RT triggers ON, or use Bluetooth mode (`Screen -> Mode -> BT`) for independent 4-motor trigger control.
- **Unique Custom Pattern Composer & Audio Roles (Advanced):** 8 built-in signature waves plus an interactive 8-step x 4-motor sequencer (`50-400 ms`), smooth/crisp waveforms, local saved patterns, and a live 8-band 1024-pt FFT spectrum (`SUB..AIR` + harmonic `VOCAL` detector).

#### PC mode

PC mode sits between your physical controller and games on Windows and Linux, applying remaps, stick/trigger curves, turbo, keyboard sequences, mouse mode, `200%` rumble boost, and macros in software. Your settings stay on your PC and never alter controller memory while in PC mode.

- **Virtual controllers:** Outputs virtual **Xbox 360** or **PlayStation 4 (DualShock 4)** controllers on Windows, and virtual Xbox 360 controllers on Linux (`/dev/uinput` with exclusive `evdev` source grab via `EVIOCGRAB`).
- **Up to four controllers at once:** Each of the 4 player slots keeps its own settings, profiles, and virtual controller.
- **Settings & rules per game:** Assign a PC mode profile, Eco mode rule, RGB brightness/preset, or Universal Haptics genre to any focused or installed app so X20CTL switches automatically while that app is in front and returns gracefully otherwise.
- **Competitive, safety & co-op controls:** Configure D-Pad & Left Stick **SOCD Cleaning**, **Rapid-Trigger**, force-curve presets & **Dual-Stage** trigger outputs, **Hold-to-Aim Sniper Curve**, auto-drift compensation, **A/B Curve Blind Test**, **Launch Aim Trainer**, virtual pad output rate (`250 / 500 / 750 / 1000 Hz`), conditional macro loops with humanized jitter, **Co-Pilot Mirroring**, **Auto-Pause on Disconnect**, and one-click **Tournament / Clean Mode** (`Ctrl+Alt+T`).
- **Overlays & radial wheel:** Toggle a click-through **OBS Input Overlay** (`Ctrl+Alt+O`) or hold **View + LB** with the Right Stick to trigger the 8-slice **Radial Shortcut Wheel** (`Mic Mute`, `Save Clip`, `App Vol +/-`, `Play/Pause`, `Tournament`, `Eco`, `OBS Overlay`).
- **Switch settings off mid-game:** Hold **View + Menu** for a second to pause/disable PC mode in the middle of a game, and hold it again to turn settings back on. Closing X20CTL restores your pad immediately.
- **Share, bundle or auto-backup:** Export any profile as `.x20pc` or `X20PC1.` code, share full setups (`PC mode + RGB + Haptics`) via `X20B1.` bundles, keep rolling **10-snapshot automatic backups** before imports/resets, or import **DS4Windows (`.xml`)** and **Steam Input (`.vdf`)** controller configs directly.
- **Drivers on Windows:** On first use, PC mode installs two signed Nefarius drivers from hashes verified inside the installer: [ViGEmBus](https://github.com/nefarius/ViGEmBus) (`1.22.0`, virtual controller) and [HidHide](https://github.com/nefarius/HidHide) (`1.5.230`, hides the physical controller from double input). Remove either driver anytime in Windows Settings → Apps.
- **Back paddles and turbo warning:** On pads whose rear paddles duplicate face/shoulder inputs, bind an unused button to play the macro slot in PC mode. **Turbo and macros can get you banned in online games** - check each game's rules before using them.

#### Keyboard

Inside PC mode, bind controller inputs (`M1–M7`) to real keyboard keys or mouse actions, or build custom sequences of up to 64 keys with per-step hold and pause timings.

#### Mouse

Use the controller as a mouse around the desktop or launchers even with PC mode turned off: one stick steers the cursor, the other scrolls horizontally/vertically with adjustable **Scroll Acceleration**, bind `M1-M7` with adjustable `2-30 Hz` turbo, slow-pointer precision hold, volume/window keys, **Mic Mute**, **Instant Replay**, per-app volume controls, or hold **View + Y** to toggle mouse mode anytime.

#### Device, Diagnostics, Setups & Tester

- **Device:** Battery, firmware/hardware revision, active transport (`Dongle`, `BLE`, `Wired`), supported capability table, and collapsible **Hardware Internals & Watchdog** (`BLE RSSI` signal chart, read-only KeyLinker config diff snapshot explorer, and battery drain session graph + `1/4` low-battery alert).
- **Diagnostics:** Controller **Health Score (0-100)** badge, range trace & circularity test with 8-point compass symmetry, center drift check, trigger linearity & dead-band mapping, button chatter detection, actual report rate (Hz), and collapsible **Hardware Lab** (`Raw HID` byte sniffer & descriptors, 12-bucket packet timing jitter histogram, 10s resting noise floor zoom with one-click dead zone apply, `Wired vs 2.4 GHz vs Bluetooth` transport benchmarks, and live `16x16` stick dwell heatmaps + APM stats).
- **Setups & Tester:** Save full-controller local profiles (`Buttons + Curves + Macros + Vibration`) and inspect live front/rear input state received by the OS.

### Tools

Help, maintenance, and app settings: open **PC mode**, run **Controller Check**, replay the opening intro, manage PC Bluetooth devices, toggle **Keep X20CTL up to date** (checks stable GitHub releases only and verifies `SHA256SUMS.txt`), pick from **6 native themes** (`Default`, `Channel`, `Undertow`, `Alert`, `Slate`, `Multiverse`) and **10 UI languages** (English, 中文, Français, Deutsch, Polski, Español, 日本語, Русский, Italiano, Tiếng Việt), choose minimize-to-tray behavior (**Quit** from tray closes completely), inspect **About** (including active Standard vs **Portable `./data` USB-Stick Mode** status and the collapsible **Controller Firmware & Hardware Quirk Guide**), open GitHub/Support, or **Send feedback**. When a new stable release exists, the top bar shows **Update to version V...** and verifies checksums before updating in place.

#### Controller Check

Walks through every button, stick, trigger, report rate, and identity check locally, saving a diagnostic ZIP report to `Documents\X20CTLInputReports`. It never changes hardware memory or firmware unless you explicitly start an opt-in Write test confirmation. Shows a clear identity dialog (`Detected <other>, not <chosen>`) with **Enter Studio as ...**, **Continue as ...**, or **Cancel**, while native BLE writes always re-verify identity first.

## Supported controllers

| Controller | On-device configuration | PC mode | Notes |
|---|---|---|---|
| **EasySMX X20** | Buttons, curves, macros (`M1–M4`), vibration | Yes | Full BLE GATT configuration + read-back verification |
| **EasySMX X20 Pro** | Buttons, curves, macros (`M1–M6`), 4-motor vibration, RGB Studio, `160x80` screen | Yes | Simultaneous 2.4 GHz dongle (`413D:2131`) + unpaired BLE when PC Bluetooth is on |
| **EasySMX X15** | Read-only preview | Yes | Full Windows/Linux PC mode support |
| **EasySMX X10** | Experimental read-only Bluetooth (KRLY) | Yes | Ported from EasyGamepad; awaiting physical X10 test |
| **EasySMX X05 Pro** | Read-only preview | Yes | DInput recognition helper in Write scan |
| **EasySMX D10** | Read-only preview | Yes | Full PC mode profiles, curves, keyboard & mouse |
| **EasySMX Dune 8K** | 4-motor haptic visualizer & `M1–M4` layout | Yes | PC mode remapping & trigger rumble; native protocol unverified |

- **DInput tip (`STK-7024X` / `0079:181C`):** If Steam or Windows labels your pad `STK-7024X`, switch it back to XInput mode for full PC mode and X20 Pro identification.
- **Shopping for an EasySMX controller?** Use the [EasySMX referral link (`?ref=X20CTL`)](https://www.easysmx.com/?ref=X20CTL) (10% affiliate commission supports X20CTL at no extra cost to you), or [Support me on Ko-fi](https://ko-fi.com/x20ctl).

## Download

Official builds from [**Release V0.026**](https://github.com/AmjadAAYD/x20ctl/releases/tag/v0.026) (`SHA256SUMS.txt`: [`SHA256SUMS.txt`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/SHA256SUMS.txt)):

- **Windows 10/11 (x64):** Everything is bundled: you don't need to install .NET, Python, or a browser runtime.
  - Installer: [`X20CTL-V0.026-windows-x64-setup.exe`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-windows-x64-setup.exe) (Start menu entry & Windows Apps uninstaller)
  - Portable single exe: [`X20CTL-V0.026-windows-x64.exe`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-windows-x64.exe) (unpacks engine to temp folder on first launch)
- **Linux (x86_64):**
  - AppImage: [`X20CTL-V0.026-linux-x86_64.AppImage`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-linux-x86_64.AppImage)
  - Flatpak bundle: [`X20CTL-V0.026-linux-x86_64.flatpak`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-linux-x86_64.flatpak)
  - glibc tarball: [`X20CTL-V0.026-linux-x64.tar.gz`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-linux-x64.tar.gz)
  - musl tarball: [`X20CTL-V0.026-linux-musl-x64.tar.gz`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-linux-musl-x64.tar.gz)
  - Controller Check Python ZIP: [`X20CTL-V0.026-linux-check.zip`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/X20CTL-V0.026-linux-check.zip)

**Code signing:** X20CTL has applied for free code signing through the [SignPath Foundation](https://signpath.org/); until active, executables are **unsigned**, so Windows SmartScreen may warn you. Verify downloads against [`SHA256SUMS.txt`](https://github.com/AmjadAAYD/x20ctl/releases/download/v0.026/SHA256SUMS.txt) and never turn off your antivirus. Linux bundles compile on CI and pass headless checks, awaiting physical Linux runtime verification (see [docs/linux-user-guide.md](docs/linux-user-guide.md)). Version rules: [docs/versions.md](docs/versions.md).

## Local first

- No account and no telemetry. Handheld Agent XCTL, setups, drafts, and PC mode profiles run locally on your machine.
- Game shelf scans only local launcher manifests. Every change stays a **local draft** until sent to a connected, verified controller.
- Uploads happen only when you explicitly confirm **Scan and send** in Controller Check or **Send to X20CTL** feedback. See [PRIVACY.md](PRIVACY.md) and [IF-YOUR-CONTROLLER-ISNT-WORKING.md](IF-YOUR-CONTROLLER-ISNT-WORKING.md).
- X20CTL does not flash firmware.

## Credits

- **Special thanks to Kiin from Team EasySMX** for supplying a free X20 Pro controller.
- **Thanks to [Edgard (edgardmessias)](https://github.com/edgardmessias)** for decoding the X10 Bluetooth protocol in [EasyGamepad](https://github.com/edgardmessias/easygamepad).
- **Thanks to [Monratis](https://github.com/Monratis)** for resolving simultaneous mouse/gamepad focus and testing X20 Pro hardware support.
- **Thanks to [shazzaam7](https://github.com/shazzaam7)** for audio-reactive RGB envelope design inspiration from [DualSenseClient](https://github.com/DualSenseClient/DualSenseClient) (GPL-3.0).
- **Thanks to [hifihedgehog](https://github.com/hifihedgehog)** for external profile import UX inspiration from [PadForge](https://github.com/hifihedgehog/PadForge).
- Console layout inspired by [ApexSenseBridge (Kayn arts)](https://github.com/ReynArts/ApexSenseBridge); mouse layout defaults inspired by [Gopher360](https://github.com/Tylemagne/Gopher360). Support development on [Ko-fi](https://ko-fi.com/x20ctl).

## License

From V0.012 (4.1.0 Preview 3), X20CTL is licensed under **[GNU General Public License v3.0 or later](LICENSE)**. Earlier MIT releases keep their headers ([licenses/](licenses/)). See [NOTICE](NOTICE) and [THIRD_PARTY.md](THIRD_PARTY.md).
