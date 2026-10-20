# Changelog

## [Unreleased]

### Added

- **Save your settings as a setup.**
  - Say "save this as Rocket League" to keep the current settings under that name.
  - "load Rocket League" brings them back, and it can be undone like any other change.
  - "my setups" lists them; "delete the Rocket League setup" removes one.
  - The agent settings page has a Load button for each setup.
  - Setups are saved in `agent-setups.json`, next to the PC mode settings.
- **Undo goes further back than the last change.**
  - "undo" takes back the last change.
  - "undo everything" goes back to before the first change.
  - The agent settings page lists every change made since X20CTL opened, newest first. Each one can be taken back to how things were before it.
  - An answer whose change was undone says "Undone".
- **Try a new curve with your real stick.** When an answer changes a stick curve, a dot on the Current and Proposed cards follows your stick. It shows what the game gets before and after the change.

### Changed

- **Agent XCTL has a new look.** The agent is now a large handheld-style controller, with its screen filling the middle. The drawn controls work:
  - LB opens the ideas.
  - RB opens the settings.
  - The D-pad scrolls the conversation and steps through the ideas.
  - A sends, B puts the agent away, X starts a new conversation and Y searches the ideas.
  - FRONT / BACK on the bar under the screen puts the agent away and shows the back of your controller, where its paddles are.

  The sticks are its eyes.
- **A clearer agent screen.**
  - A rail on the left leads to the conversation, the ideas, another controller, the settings as they are now and PC mode.
  - Topic pills sit across the top: Aim & sticks, Triggers, Buttons and PC mode.
  - The chat bar along the bottom has a round send button.
- **Answers that change a stick show it.** The answer has a headline with your words under it. Current and Proposed curve cards come next, then one line on why, then Undo.
- **The Write scan gives a verdict.** Results now open with what was learned and how sure the scan is: High, Medium or Low confidence, or Nothing learned. Each point the verdict rests on is listed, along with what to do differently. For example:
  - The app changed the setting and changed it back.
  - The byte holds the values you typed.
  - The writes came on the steps where you made the change.

  A button goes straight back to the step that fixes it.
- **The Write scan checks the recording itself.**
  - A Filtered Android log is named as such, with the exact setting to change. Before, it only said "no KeyLinker frames".
  - Android logs now carry the phone's clock, so a log from another time is caught on Android too, as it already was on iPhone. Each change is labelled with the step you were on, even when the phone logs local time.
  - A bug report's earlier and filtered logs are read too.

### Fixed

- **A Write scan report is never bigger than X20CTLADMIN can take.** The report site refuses anything over 4.5 MB, so the limit is now 4 MB, down from 8.

- **Sending from the ideas returns to the conversation**, so you see the answer.
- **When the agent can't do something, it says why in plain words.** For example: "Left stick dead zone: 0 to 40 only; 260 is outside." Before, it showed internal names.

### Linux

- **The Linux app opens with the Windows film, sound included.** Then the logo flies onto the header while the Controller Zone grows in around it, as on Windows. Esc, a click or any key skips it; M mutes it, and the choice is remembered.
- **The Linux app moves like the Windows one.**
  - Screens grow or shrink in as they change.
  - A player's card glides into the big spot.
  - Studio tabs slide in from their side, and the underline glides after them.
  - Buttons press in and spring back.
- **A bigger curve graph on Linux.** It spans the card, the stick buttons sit beside the stick's name, and the presets stand beside the graph.
- **Buttons and Curves fill the window on Linux**, and scroll when it is too short.
- **Every Studio tab shows on a narrow Linux window**, and notices wrap instead of being cut off.
- **Choosing a controller on Linux opens at once after the first time.**

## [V0.017.1] - 2026-10-19

Tagged `v0.017.1`. Everything in V0.017 is included.

### Changed

- **The setup is a small download.** `X20CTL-<version>-windows-x64-setup.exe` is now a small downloader (about 0.3 MB) instead of a copy of the whole app. The controller artwork inside the exe is recompressed without any change to the pictures, which takes about 13 MB off the download. It fetches the release exe, checks it against the release's `SHA256SUMS.txt`, runs the install wizard from it and removes the download afterwards. If the checksum is missing or wrong, nothing is run. The plain exe is still the no-install download.

## [V0.017] - 2026-10-19

Tagged `v0.017`. Everything in [V0.016](https://github.com/AmjadAAYD/x20ctl/blob/main/CHANGELOG.md#v0016---2026-10-18) is included.

### Changed

- **A new logo and app icon.** The blue winged controller with the orange spark replaces the hooked X everywhere: the app icon, the header, the setup, the tray, the Linux packages and the README. Every size is scaled from one master image (`tools/build_brand_mark.py`).
- **A new opening.** A bird draws the controller's outline in one line, the camera pulls back, the bird charges on the top edge and strikes, and the logo forms; then it flies to the header as the Zone appears. It is a 7-second film with its own sound, played by the app. Mute and Skip work as before, and reduced motion skips it with a short fade. The Linux app keeps its drawn opening for now.
- **The Tools page matches the rest of the app.** Flat navy tiles with a coloured icon chip each, a thin border that brightens on hover, and no glow. The layout and every button are unchanged.
- **PC mode installs its drivers by itself.** Switching PC mode on (in its tab, or by asking Agent XCTL) installs the virtual controller driver (ViGEmBus) and the controller hider (HidHide) from the copies carried inside the exe, checked against pinned hashes, with no Set up step first. It names both drivers and asks first, and Windows then asks for permission once; a refusal or a failure is reported and never retried in a loop. Setup has a *Set up PC mode drivers now* option that installs them during setup (silent installs: `--drivers`). A driver Windows has only marked for removal no longer counts as installed. Drivers already installed are left alone. *Set up PC mode* stays as the fallback if an install fails.

## [V0.016] - 2026-10-18

Tagged `v4.1.0-preview.7`, so copies up to V0.015 find it. Everything in [V0.015](https://github.com/AmjadAAYD/x20ctl/blob/main/CHANGELOG.md#v0015---2026-10-17) is included.

### Added

- **A setup wizard for Windows.** Each release carries `X20CTL-<version>-windows-x64-setup.exe` next to the plain exe. It is the same file: a copy with that name opens a wizard in the app's own look (welcome, license, options, install, finish) instead of the app. It installs for your account only, in `%LOCALAPPDATA%\Programs\X20CTL`, with a Start menu entry, an optional desktop shortcut and an entry in Windows Apps. The plain exe still runs from anywhere, with no install.
- **An uninstaller.** The entry in Windows Apps (or `X20Ctl.exe --uninstall`) removes the app, its shortcuts and its entry, and keeps your settings unless you tick *Also delete my settings*. The PC mode drivers stay installed. `--silent` skips the wizard for both setup and removal.

### Changed

- **New version names.** Releases are now numbered V0.016, V0.017 and so on, with fixes as V0.016.1. The first number turns to 1 at the first release that supports every controller. Earlier releases are renamed in the same way (4.1.0-preview.6 is V0.015) and keep their tags and downloads. There are no more previews. See [docs/versions.md](docs/versions.md).
- **Keep X20CTL up to date is on by default.** Anyone who switched it off keeps it off. The setup asks about it, and Tools still turns it off.

### Fixed

- **The in-app updater never installed anything in a release build.** The one-file exe was mistaken for a folder build, so it announced updates but sent you to GitHub. It now recognises itself and swaps in the new, checksum-verified exe.

## [V0.015] - 2026-10-17

Tagged `v4.1.0-preview.6`.

A pre-release. Everything in [V0.014](#v0014---2026-10-12) is included.

### Added

- **Agent XCTL**, a small assistant drawn as a controller, in the Studio title bar. Tell it in your own words how you play and it sets PC mode for you: stick and trigger curves (custom points, not just presets), dead zones, anti-dead zone, max output, hair triggers, turbo, remaps, mouse mode, and it measures stick drift before it picks a dead zone. Every request is checked against fixed rules first; each change is walked through on the page it lives on, with Undo. It answers questions about the app from these docs, and *Say HELP* lists what it can do. It runs on your PC with nothing to install and nothing you type leaves it. Spelling and grammar don't matter: it works out what you meant and shows *I read that as* when it rewrote your words.
- **Opening a controller scans it first**: a radar popup and a verdict, and a pad that names another model is not opened as this one.
- **Battery in the middle of the Studio title bar**: the level the controller reports (full, medium, low, empty or wired), a dash when it reports nothing. Click it for Device.
- **B resets the Buttons card**: with a control picked, B (or Esc) clears it back to *Select a control*; a second B leaves.
- **Every launch checks for a newer version**; a popup follows the intro only when there is one.
- **Your games**: Riot and GOG games, folders and games you add, their real icons; Play zooms in and the card shows *In game* while it runs.
- **Vibration preview** draws each controller's own grip edge, lit in its LED colours, with a moving light per motor.
- **X20 Pro macros on M5 and M6**, with macro slots that follow each controller.
- **Read scan, Write scan, send online, in one go.** When a Read scan finishes, *Continue to the Write scan* takes you straight on. The Write scan's warning says the Read scan comes along, and at the end *Send online* sends both together as one report. The Read scan's part is the same compact summary it already offered to send (controller names, IDs and results, no personal files). Starting the Write scan from Tools on its own works as before.
- **The Controller not working? card shows a computer scanning**, with the three steps (Read scan, Write scan, Send online), instead of a controller.

### Changed

- **A new intro.** The controller is drawn in one smooth stroke, the two halves of the X glide in and lock with a flash, then the slogan and the flight to the logo. It has an original score: piano, plucked strings and a soft pad in D minor, with each pluck placed where the line is drawing. Mute and skip work as before.
- **Buttons showcase** redrawn as the parts of a controller across the full width: a face-button diamond, a D-pad cross, triggers over bumpers, stick caps, View and Menu, Default and Disabled. The current output glows.
- **LB and RB** are shoulder-button keycaps on the tab line.
- **Controller Zone and Dashboard rebuilt**: matte navy surfaces, shared navigation, a header clock, a selected player shown large with the others beside it; Remove and Add controller stay inside their cards. Setups are kept per model.
- **Curves** draws exactly what games get, with handles and sliders for dead zone, full at, anti-dead zone and max output, and a live dot that follows the real stick.
- **Mouse, Diagnostics, Vibration and PC mode pages** refined; Diagnostics captures drift more reliably.
- Maximised and fullscreen windows stay above the taskbar.
- **New controller art for X20 Pro, X10, X15, D10 and Dune (D05)**, from the balanced product photographs. Each stick cap is its own layer with a dark socket left behind, so it really moves and tilts with the stick, and each trigger is lifted off the rear photo. Every button, D-pad arm, trigger and paddle outline was measured again on the new pictures. X20, X05 and X05 Pro keep their art.

### Linux

- **Scanning and connecting retry** instead of giving up after one pass, a paired pad is found through BlueZ when the scan can't see it, and `x20 scan --all` lists every Bluetooth device it hears. Not yet proven on a real pad: reports welcome.
- The Linux Controller Check and PC mode scripts ship with this release as before (`X20CTL-4.1.0-preview.6-linux-check.zip`).

### Removed

- The legacy PySide and web interfaces, the pywebview bridge, the WebView2 packaging and the old standalone Python scanner. The command line and its core no longer depend on any of them.

## [V0.014] - 2026-10-12

Tagged `v4.1.0-preview.5`.

A pre-release. Everything in [V0.013](#v0013---2026-10-10) is included.

### Added

- **The intro now hands you to the Controller Zone.** After the X's heartbeat, the slogan *Keep it easy, Keep it 20* comes into focus under it (in squared, slanted capitals, white to silver like the mark, with its glow and a run of light). Then the X flies from the middle into the logo tile at the top left, shrinking as it goes, and lands there with one small pulse while the Zone grows in around it: the header, then Players, the four cards one after another, the footer. It no longer fades to black first, and it is a little shorter (about 6 seconds). The dark behind it is no longer empty: a faint navy glow and slow drifting dust are there from the first moment.
- **Mute the intro, or skip it at launch.** A Mute button beside Skip (or M on the keyboard, Y on the controller) fades the sound out and is remembered. Tools has a new *Opening intro* bar: *At launch* (off opens X20CTL straight to your controllers; Replay Intro still plays it) and *Sound*.
- **PC mode** (Tools). Remaps, turbo, stick and trigger curves and macros for any controller, applied on the PC and handed to games as a virtual Xbox controller while the real one is hidden. Nothing is written to the controller. On by default but inert until a setting is changed; a one-time notice covers closing the app, online-game bans and the two drivers (ViGEmBus 1.22.0 and HidHide 1.5.230 by Nefarius, carried in the exe and checked against pinned SHA-256).
- **PC mode lives on every controller's tabs.** A new PC mode tab in each controller's Studio holds the switch, the per-game choice, turbo and sharing. While PC mode is on, that controller's own Buttons, Curves (with Advanced) and Macros tabs are PC mode's editor, for any model (the X05, X05 Pro and Dune included), and each change applies right away. Settings the tabs can't show (turbo, a remap of View, Menu or Home, a fifth macro) are kept. The editor that used to sit in Tools → PC mode is gone; Tools keeps the same compact page.
- **Macros in PC mode are played by a button you pick.** PC mode can't see a back paddle (EasySMX paddles only ever send ordinary buttons), so each Macros slot gets "In PC mode, played by", and that button plays the macro instead of its usual job.
- **PC mode for up to four controllers at once, each with its own settings**, and each with its own virtual Xbox controller, in the same order. Player N's settings (every-game and per-game) go to the Nth controller Windows sees, and each controller's Studio edits its own; Tools → PC mode has a Controller list. Settings saved before this are Player 1's. A controller plugged in later joins within a second.
- **PC mode settings per game.** *Settings for* picks *Every game* or a game; *Give a game its own settings* lists what's running and starts from your usual settings. X20CTL follows the window in front and switches within half a second, without dropping the virtual controllers.
- **Hold View + Menu for a second** to switch PC mode's settings off mid-game and back on, with a notification each time.
- **Share PC mode settings** as a `.x20pc` file or a code (`X20PC1.…`, compressed, safe to paste in a chat). Only the settings go in; the game name and anything about the PC stay out. Imports are size-capped and checked before anything is used. The Linux script has `export` and `import` with the same codes and files.
- **Mouse tab: the controller as a mouse.** Pointer on one stick, scrolling (both ways) on the other, every button free to pick: clicks, back/forward, Enter, Escape, Tab, arrows, Windows key, Task view, volume, on-screen keyboard, or a slow pointer while held. Speed, acceleration, dead zone and scroll speed are adjustable; held arrows repeat. Switch it from the tab, the tray, or by holding View + Y. While it's on, PC mode gives games a resting controller and X20CTL's own controller navigation pauses. Default layout after [Gopher360](https://github.com/Tylemagne/Gopher360) (GPL-3.0); written fresh. The Linux script has `mouse` too.
- **Use as dead zone** in Diagnostics: one click puts a stick's measured drift into PC mode's inner dead zone.
- **Battery in the tray:** the tooltip shows the controller's battery, and a notification warns once when it runs low.
- **Button chatter in the Controller Check**: presses shorter than 15 ms, which a finger can't make, are reported as a measured finding.
- **Curves → Advanced:** fine stick tools for PC mode, for any controller. Dead zones in 0.1% steps, anti-deadzone (an output floor so small moves register), max output, a square gate, up/down speed, and pure raw 1:1. Also one-click presets, copy to the other stick, a number table of what the game gets, and **Measure drift**, which sets each inner dead zone just past how far the stick wanders when left alone. The Linux script has the same settings (`set stick ... --anti --max --raw`).
- **Diagnostics tab** in the Studio, for every model. It reads the controller live and never writes to it. Each stick gets a range trace, roundness, resting drift (with the dead zone that would hide it) and step size. Triggers get travel and resolution; every button gets a press count, with chatter flagged for presses under 15 ms. The real report rate per controller comes from Raw Input, up to 8000 a second. **Copy report** puts the readings on the clipboard. While it is open it owns the controller's input, like the old Tester: hold B to leave.
- **Administrator permission at launch** for release builds, so PC mode can install its drivers.
- **Send feedback** (Tools): a bug, an idea or a note about your controller, straight to the X20CTL inbox, with a receipt.
- **Keep X20CTL up to date** (Tools, off by default): checks GitHub releases at launch, and the one-file exe downloads, verifies and swaps in the new version.
- **Controller Check 2.2:** Home, an M button step and a measured turbo test; the battery read every read-only way Windows offers (XInput, Bluetooth, Windows.Gaming.Input) plus a listen on the vendor channels; "Got instead" when a different button arrives, and "Start and Back look swapped".
- **The X05 has no back paddles**, confirmed by an owner; its scans no longer ask for M1 to M4.
- [PRIVACY.md](PRIVACY.md), and a release workflow that builds Windows and Linux together.
- **Controllers are only changed where that's been proven.** Every request is checked against a table of what has been measured on real hardware, per product, operating mode and firmware (`x20ctl/validation.py`), before it is sent. The X20 on firmware 1.23 keeps everything it had. An X20 on other firmware keeps the proven features, each still checked by reading it back. An X20 in an untested operating mode can be read but not changed. A controller that isn't in the table connects and is identified, nothing else is sent to it, and what it reports is saved to `unknown-controllers.json` so it can be validated. Lighting, turbo and mode switches are refused on every pad: none is proven.
- **The capability descriptor is kept whole.** Scans, status and reports carry the raw bytes and name every set bit nobody has traced, and the remap select-list bit (D6 bit 1) is decoded. Captures also keep the turbo, macro and per-slot macro-limit lists.
- **Macro slot limits are read and shown.** On a pad whose descriptor asks for it, each slot's limit comes from its own query (a list of 16-bit numbers, matched to slots by position). The X20 reports 42 for M1-M4. `status` prints it next to each slot and the desktop service returns it. It does not size writes: its relation to the 47-entry cap is unmeasured.
- **Write scan** (Tools), the partner of the Controller Check, which is now called the Read scan. The Read scan watches what a controller reports; the Write scan learns what KeyLinker writes to one. You change one setting in the official app on an Android phone while the phone's Bluetooth HCI snoop log records, then open the log or the bug report ZIP here. X20CTL never talks to the controller during it. It decodes KeyLinker frames when they're there and shows the controller's identity, what its capability descriptor offers (and any bits nobody has traced), how it compares with the recorded X20, and each byte the app changed, against the last value the controller reported. Recordings that aren't KeyLinker get their writes grouped by place and size, with the bytes that vary. A second recording can be compared message by message. A warning up front says it can take 20 to 40 minutes. The findings are saved to `Documents\X20CTLWriteScans` only when you press Save, and the raw log only if you tick the box.
- **The Write scan works with an iPhone too.** Apple's Bluetooth logging profile plus a sysdiagnose records the same traffic; X20CTL takes the PacketLogger log (`.pklg`) out of the sysdiagnose (`.tar.gz`) without unpacking anything else, and reads either byte order. The phone page asks which phone you have and gives that phone's steps.
- **Send to X20CTL** on the Write scan results: the summary, findings, timeline and decoded messages go to X20CTLADMIN as a research report (schema 2, with a manifest of every file and its SHA-256), only after you tick that you've read the summary. The Bluetooth packets go only if you tick that too, re-packed without the HCI events where device names and addresses travel. X20CTL asks the inbox first and sends nothing until it accepts Write scans.
- **Fixed:** the Write scan's guided test, and a second visit to its recording or results page, failed after the first step because a reused label was still attached to the old page.
- **The Write scan walks you through it, one step at a time.** Pick Samsung, Other Android or iPhone, and each step says what to do in a sentence or two, next to a drawing of the phone with the thing to tap marked ("Tap 7 times" on Build number, the snoop-log switch, the Bluetooth tile, the bug report notification). Samsung has its own steps, with One UI's menu names. Every step, and the test itself, has an "I'm stuck" button listing what usually goes wrong and what to do, with a Discord hand-off if none of it fits. The results page reminds you to turn the snoop log off, or remove Apple's profile.
- **A write is a request, not a stored setting.** The Write scan compared each write with the app's previous write as if the controller had taken it; it now compares with the last value the controller reported when read. Each write says whether the controller replied, and whether a later read in the recording held what was written ("read back as stored"), didn't, or wasn't read back.
- **Only the controller's connection is read.** A phone talks to its earbuds, watch and controller at the same time. The Write scan picks the controller's connection (the one with KeyLinker frames), reads only that one, and says how many others it left out. A shared trace holds that connection only, from just before the test to just after it.
- **The right log out of a sysdiagnose or bug report.** An archive can carry older Bluetooth logs beside the current one. The one with the controller's messages is chosen now, not the biggest, and the results say which of how many it was. An iPhone log whose clock doesn't cover the moment you made the change is flagged.
- **"Nothing decodes" is inconclusive**, not "the app speaks something else": a partial or filtered log looks the same. The iPhone steps no longer state as fact a restart, a few-day profile life or a ten-minute sysdiagnose; they point to Apple's own instructions.
- **`x20 state save` and `x20 state diff`**: read every setting record off a proven controller, change one setting in the official app or on the pad, read again, and see which record and bytes changed, with the field each byte is where that's known (curve points and deadzones, motor strength, idle shutdown, what each button sends). A third read after putting it back shows whether it returned to the start. Records the controller's descriptor switches off are not asked for. A comparison is void if the identity, firmware, mode, descriptor or button order changed between reads, and a record that came back incomplete is "not recorded", never "no change". It shows the stored state only: not the messages the app sent, and not whether a change survives a power-off.
- **`tools/compare_controllers.py`** compares what two controllers report (a capture, the unknown-controllers log or the recorded X20): generation and mode first, then identity, descriptor bits and lists.

### Fixed

- **Fullscreen no longer hides the bottom bar under the taskbar.** X20CTL covers the whole screen only while it is the window you are using, and tells Windows so, which makes the taskbar step back for certain. Click another window or screen and it fits above the taskbar instead, so the footer hints are never left underneath it.
- **A softer intro sound.** The three bright "ting" pings as the LEDs blink are now soft, rounded notes (D, F#, A, in the key of the swell), about a third as bright and a little quieter. The noisy "fwoosh" while the outline draws is now a short, soft warm pad, and the noise riser while the line becomes the X is a low, warm chord that only grows in volume (no rising pitch). The heartbeat has a small "lub-dub" you can hear on laptop speakers (the old one sat at 45 Hz). The deep "vmmm" drone as the X lands is gone: the X now has a heartbeat of its own, a "dug dug" you hear as the mark swells twice on screen.
- **Tools tiles cut off their words.** *Support X20CTL* and *Star our GitHub repo* wrapped their titles and lost the end of their description; the titles are a touch smaller and the star tile reads *Star on GitHub*.
- **The Controller Zone swap stutters less.** The hero card's glow was redrawn on every frame of the move; it now waits until the card lands and fades in (slow frames during a swap down by about 40%). The photos also scale more cheaply while moving.
- **The Tools hero's light sweep** ran at the full display rate forever; it is capped like every other endless animation.
- **The X20 Pro's M5 and M6** on the Macros photo could be clicked, and threw, since a macro draft holds M1 to M4.
- **An interrupted Zone swap while the window is hidden** left the cards' names faded out.
- **Only the official app's three address prefixes count as a controller.** The address filter accepted anything starting `98:B6:E`, which includes unrelated devices; it now takes `98:B6:E9`, `98:B6:EC` and `98:B6:ED`, as the app does. The `Xpert2` name still counts on its own.
- **The controller's identity was read one byte off.** The reply's size byte was parsed as data, so an X20 showed as `0710`/`1320` on firmware "9.01" and its flags were never decoded. It now reads `1013`/`2009`, firmware 1.23, operating mode 2, sensor 1, generation 2. The flags' low bits are named the operating mode (not a model), and the generation the official app routes on is derived from it.
- **"EQ" is gone from the supported features.** Descriptor byte 8 is not an equaliser: bit 1 tells the official app to ask each macro slot's limits. The field is now `engine`, with `new_macro_engine` for that bit.
- **Saving a scan with M paddle steps failed** ("Unknown evidence action"), which also made Back to Tools look broken.
- **Redo offered paddles a pad doesn't have.** When every paddle step comes back empty, Redo leaves them out.
- **The Controller Check could stay on "Finishing up" forever** after a redo. A failure now shows a message and leaves the screen usable.
- Tools tiles no longer let their icons overlap the titles.
- **A crash, or Windows shutting down, could leave the controller hidden from games** until X20CTL was opened again (HidHide's list survives a reboot). Every way out now gives it back.
- **PC mode could read its own virtual controller** as the real one after a reconnect. It now only reads the controller it started with.
- **A broken PC mode settings file stopped PC mode** until the app restarted. It's now refused with a message, and the rest keeps working.
- **Turning PC mode off and quickly on again could leave it stopped.**
- **The Controller Check scanned PC mode's virtual controller** when PC mode was active. PC mode now pauses while the check is open.
- **The macro recorder could record PC mode's virtual controller** instead of the real one.
- **New source during the turbo step could crash the app** (the turbo sampler kept reading a freed XInput library).
- **Finishing a scan froze the window for up to 2 seconds** while the vendor-channel listener shut down.
- **Home was missing from PC mode's button list**, and macro text saved only when the box lost focus (Enter now saves too).
- The PC mode page no longer stays subscribed after Tools closes.
- Linux: `--version` printed the help, a controller another program held leaked a file handle, and a device error ended in a traceback instead of a plain message.

### Faster

- **About a quarter of the CPU when idle.** The slow background motion (nebula drift, stars, status-dot pulses) asked for a redraw at the display's full rate, so the idle Studio used about 1.7 CPU cores on the test PC. It now asks for 24 frames a second, and the floating controller moves on a 30 Hz timer: about 0.4 cores, and it looks the same.
- **Almost nothing while in the tray or minimized** (about 3% of one core, down from up to 80%): nothing is drawn while nobody can see it, which matters while PC mode runs for hours.
- **Tools opens about 40% faster** (300 → 180 ms; 120 → 70 ms the second time): the PC mode and feedback pages are built the first time you open them.
- **Screens open 25–30% faster in the release exe**, which is now precompiled (ReadyToRun) for 2 MB more.
- `--review-timing FILE`, a development tool that measures startup, each screen and idle CPU.

### Not tested yet

- PC mode on a real controller: the virtual pad, the hiding, and whether a game sees one controller. A tester runs it next.
- PC mode on Linux (`x20ctl-pcmode.py`) on a real distro.
- Diagnostics and Measure drift on a real controller: the report-rate counter and the bounce flags have only run without a pad.
- The controller as a mouse, two or more controllers in PC mode, per-game switching and the View + Menu switch: all built and unit-tested, none run with a real controller yet. The Linux `mouse` command and View + Menu in `run` have not run on a real distro.

## [V0.013] - 2026-10-10

Tagged `v4.1.0-preview.4`.

A pre-release test build, made for the first owner scans of the EasySMX Dune 8K. Everything in [V0.012](#v0012---2026-10-10) is included; the changes below are all in the Controller Check (now scanner 2.1).

### Added

- **A countdown on every step.** Each step says exactly what to do and for how long: hold for 2 seconds, let go, hold again, let go. It also shows "Step 5 of 27", what comes next, and a live "Holding A · 1.4 s".
- **M paddle steps.** M1 to M6, as many as the model has (4 on the Dune and X20, 6 on the X20 Pro). The report shows what each paddle sends, for example "M1 → A · M2 → B", or "no input seen". This only reads; paddle settings are never changed.
- **Model and connection filled in automatically.** They come from the name the controller gives Windows, for example "EasySMX D15 Dune 8K", and from its Bluetooth interface, when you leave them on Unknown or Not sure.
- **Bluetooth battery.** The level Windows keeps for a controller paired over Bluetooth, read-only. Other paired devices never appear in a report.
- **Redo what wasn't seen.** The results list the controls that weren't seen, with a "Redo the N not seen" button that reruns just those steps and saves an updated report.
- **Dune 8K identity.** `413D:2131` is recognised, from an owner's two scans. Its two vendor channels (`0xFF70` and `0xFF10`) are listed; nothing is sent to them.
- `--review-scanner-replay ZIP OUT.json`, a development tool that replays a saved report through the current analysis.

### Fixed

- **A button held past the end of its step was marked "not seen".** It now counts as seen and is listed under "Held past the step". Replaying the Dune 8K owner scan, it now reads 23 of 23 instead of 22.
- **Uploads to X20ADMIN carry the real app and scanner versions** (`4.1.0-preview.4`, `2.1.0-local`) instead of `native-preview`.

### Not tested yet

- The countdown screens with a controller pressing through them, and the Bluetooth battery read on a real controller. This build is how they get tested.
- Listening to the vendor channels for battery data is not built yet, and the Linux scanner (`x20ctl-check.py`) doesn't have these changes.
## [V0.012] - 2026-10-10

Tagged `v4.1.0-preview.3`.

A pre-release. X20CTL is now a native Windows app, and everything below is new since Preview 2. The EasySMX X20 is the only verified controller; every other model is a read-only preview.

### Added

- **Native app.** The whole interface is rebuilt in WPF on .NET 10, and WebView2 is no longer needed. It ships as one self-contained `X20Ctl.exe`, beside the native input engine.
- **Brand and intro.** A new hooked-X mark, and a console-style opening that plays on every launch with its own original sound. You can replay it from Tools.
- **Controller Zone.** Four player cards. The selected card is the hero on the left and the other three wait on the right; swapping is animated. Press **+** on an empty card to choose a controller.
- **Eight controllers.** X20, X20 Pro, X15, X10, X05, X05 Pro, D10, and the new **Dune**, drawn from photos. The Dune's M1/M2 sit on the top and M3/M4 on the back.
- **Studio for every controller.** Dashboard, Buttons, Curves, Macros, Vibration, Device, Setups and Tester open for every model. On unverified models they are read-only previews, with a banner stating what is known about the controller, and where sources disagree.
- **Dashboard.** Controller status, draft and setup counts, and a game shelf. The shelf reads only the Steam and Epic launchers' local files, and you can launch a game from it.
- **Live controller art.** Sticks move and LT/RT press on every model, on the front and the back.
- **Clean controller outlines.** Each model has an outline traced from its own picture. The art is clipped to it, so the pixelated cut-out edges no longer show.
- **Vibration preview.** Each motor's outline pulses like a heartbeat, from 72 bpm at low strength to 180 bpm at full. Only the X20 Pro and the Dune have trigger motors; every other model has two grip motors.
- **Device page.** Battery card (four levels on the X20), plus firmware, hardware revision and transport. Gameplay and configuration are shown as separate connections, along with what X20CTL supports on the model.
- **Tools hub.** It replaces Settings and gathers the Controller Check, Replay Intro, About, Support X20CTL and Star on GitHub.
- **Controller Check.** A guided, read-only scan of every button, stick and trigger, with held times. It also reports stick resolution and report rate, battery, and what the controller reports to Windows. The report is a ZIP in `Documents\X20CTLInputReports`. Sending it is optional and chosen before the scan starts. The first step, **Confirm it's your controller** (press A, then pull LT and RT), proves the scan is reading the pad in your hands.
- **New receivers recognised.** The X15 (1A34:F517) and D10 (2345:E062) receivers, reported by owners, are known to the check and to the Linux script.
- **Linux Controller Check.** `x20ctl-check.py`, a standalone Python 3 script.
- **Gamepad navigation** throughout: LB/RB for tabs, A/B, and footer hints.
- **"Not affiliated with EasySMX"** notice in the About page, README and NOTICE.

### Changed

- **Licence:** GPL-3.0-or-later from this release. Releases up to 4.1.0 Preview 2 were MIT, and MIT-contributed code keeps its notice.
- Download size: one 111 MB ZIP holding everything the app needs.
- About credits ReynArts' ApexSenseBridge as the design inspiration ("Made by Amjad AAYD").

### Not included yet

- Writing settings to any controller other than the X20. Preview models never write or store anything as if it had been applied.
- RGB lighting, gyro, turbo and firmware updates. These stay untouched by design.
- A native Linux build of the full Studio. Only the Controller Check runs on Linux for now.
- Code signing: the app is unsigned, so check the ZIP against `SHA256SUMS.txt`.
## [V0.011] - 2026-10-08

Tagged `v4.1.0-preview.2`.

- Experimental selected-source input for D10, X15, X05 and X10, with guarded configuration and Known/Missing status.
- Extended guided evidence collection, model-specific BLE discovery hints and HCI trace/context import.
- Reject explicitly named non-X20 peripherals before the X20 settings writer.
- Direct EXE preview distribution; persistent remapping remains disabled for these four models.

## [V0.010] - 2026-10-07

Tagged `v4.1.0-preview.1`.

- Seven-model, four-player desktop workspace and shared Studio sections.
- Experimental X15 read-only input and integrated guided scanner.
- Controller/control artwork, vibration contours, compact macro editing and launch sizing updates.
- Preview hardware remains evidence-gated; native acceptance and receiver deployment remain pending.

## [V0.009.1] - 2026-09-28

Tagged `v4.0.1`.

### Fixed

- The orange vibration strength track now follows the selected value.
- Support X20ctl has a visible entry below the X20 studio navigation; the Ko-fi action is left aligned in its optional dialog.
- Removed the unfinished X20 Pro discovery footer link from the Controllers page. The Pro backend remains read-only, but its workspace is no longer offered in the visible navigation.
- Updated the desktop screenshots to reflect the current interface.

The paired scanner integrity check, report consent flow, and existing X20 functionality remain unchanged.

## [V0.009] - 2026-09-28

Tagged `v4.0.0`.

### What's added

- A fixed navy desktop interface with controller imagery, left navigation, and a compact status header.
- Keyboard and pointer controller preview for visual testing. Preview input does not change controller settings.
- Compatibility reports for a selected Bluetooth LE device. A separate scanner writes standard GATT diagnostics locally; X20ctl validates and previews the report, then uploads it to X20ADMIN only after explicit consent. Failed uploads retain the same ZIP and submission UUID for retry.
- SHA-256 verification of the release-paired scanner. A missing or modified helper fails closed.
- Optional Support X20ctl dialog opening the official Ko-fi page in the default browser.
- One Windows archive containing `x20ctl.exe` and `x20ctl-scanner.exe`, with their hash pairing verified after packaging.

### What's removed

- Theme Studio, the local color editor and metallic theme presets.
- The X20 Pro rear illustration/view. Its configuration controls remain unavailable.

### What's done so far

- Existing X20 configuration and saved setups remain available; X20 Pro discovery remains read-only.
- The 4.0.0 packaged desktop passed 28 automated smoke checks. Windows file and displayed version metadata both report 4.0.0.
- The scanner currently reads selected-device Bluetooth LE information only. USB/HID descriptors, input captures, new controller compatibility, and a live desktop-to-X20ADMIN report upload have not been verified for this release.

## [V0.008] - 2026-09-25

Tagged `v3.1.0`.

### Added

- Controllers entry page with separate X20 and X20 Pro workspaces.
- Dedicated front/rear X20 Pro SVG illustration, read-only BLE/GATT/HID discovery and an isolated Pro setup-store location. Pro configuration is explicitly unavailable until hardware validation.
- Four metallic theme presets, local advanced color editor, contrast checks, motion controls and reduced-motion support.
- Native missing-WebView2 dialog linking to Microsoft's installer, with Retry and Cancel.
- Model-isolation tests and packaged native-window checks for controller switching, Pro illustrations, themes and compact layouts.

### Changed

- Removed the private 308 MB WebView2 runtime from the download; the one-file Windows app now uses the serviced system runtime.
- XInput polling pauses outside live-input pages and when the X20 workspace is hidden. Unchanged samples no longer redraw the workspace.
- Pro hardware descriptions distinguish published features from observed data; smart-display image/GIF capabilities remain unverified.

### Fixed

- Backend operations now reject X20 profile exposure, import, reads and writes while Pro is selected; model changes disconnect the previous configuration link.
- Theme accent reaches controller drawings and primary interactive indicators instead of leaving them cyan under non-cyan palettes.
- Legacy X20 profiles retain their existing schema and location without migration.

### Removed

- Bundled fixed-version WebView2 fallback and its previous build/startup flags.

### Not included

- Pro configuration writes, macro uploads, lighting/display controls or GIF transfer. These require the physical X20 Pro, repeatable reads, backups and write/read-back verification before a later release.

## [V0.007] - 2026-09-19

Tagged `v3.0.0`.

### Added

- Added the metallic-grey studio shell with horizontal navigation, cyan state
  lighting, silver bevels and persistent read/apply controls.
- Added the saved-setup library, redesigned connection discovery, curve guide,
  help surfaces and themed confirmation dialogs.
- Added focus trapping, Escape handling and focus restoration for native dialogs.
- Added a 23-check native-window acceptance suite covering all seven pages,
  minimum-window layouts, persistence, cancellation and tray behavior.

### Changed

- Replaced the complete studio UI with metallic grey panels, silver bevels,
  cyan selections, horizontal navigation and persistent read/apply controls.
- Rebuilt the interactive controller drawing and button assignment inspector.
- Replaced stick and trigger curve editors, deadzone controls and live meters.
- Rebuilt M1-M4 macro sequencing and the piano-roll editor while preserving
  hardware limits, button chords, directional sticks and 5 ms timing.
- Redesigned vibration controls, battery/device information and auto-sleep.
- Rebuilt the input tester using actual XInput button, trigger and stick values.

### Fixed

- Unavailable gameplay input is explicitly distinct from a connected neutral
  reading throughout the redesigned instruments.
- In-app confirmation dialogs preserve the metallic theme, contain keyboard
  focus, support Escape, and restore focus to the triggering control.
- Expanded desktop acceptance to all seven pages, minimum-window layouts,
  setup replacement/deletion cancellation and saved-setup reload.
- Corrected the smoke harness to edit values inside the active dialog instead of
  accidentally targeting same-named controls behind the modal.

### Preserved

- Native Windows EXE packaging, BLE configuration, XInput, local setup files,
  category-scoped writes and read-back, tray behavior and optional updates.
- No invented hardware polling rate, latency, temperature, battery percentage,
  motor frequency or power-consumption displays. Battery remains four bars.
- The 2.0.1 firmware empty-macro-slot fix and previous controller compatibility.

### Removed

- Four unused synthetic JPG interface mockups left under `src/assets/images`.
  Product screenshots now come only from the running desktop application.

## [V0.006.1] - 2026-09-18

Tagged `v2.0.1`.

### Fixed

- Firmware 9.01 can report an unused macro slot as a neutral, zero-duration
  entry. Treat it as empty so saving a setup read from the controller succeeds.
  Preserve real initial delays and valid macro steps.
- Added regression coverage for both empty-slot saving and initial-delay preservation.

### Verified on hardware

- Connected to Xpert2 firmware 9.01 and read mappings, both stick/trigger curves,
  vibration, power, all four macro slots and battery without warnings.
- Changed the idle timer from 10 to 11 minutes, verified read-back, restored 10,
  and confirmed all settings matched the original snapshot.
- Detected live XInput button and left-stick changes. Saved the real setup in
  the connected desktop UI after the fix. Macro playback was not tested.

## [V0.006] - 2026-09-18

Tagged `v2.0.0`.

The first native desktop release of the new controller-studio interface.
Compared with 1.2.0, the primary UI changes while the proven Python protocol
engine remains. The intervening web/Tkinter prototype is not a shipped
hardware-capable release.

### Added

- Local React interface hosted in a native Windows window with a restricted
  Python bridge; downloadable self-contained x64 EXE.
- Button studio with selectable controller illustration, compact assignment
  panel and dark/light illustration styles.
- Curve editors for both sticks and triggers, independent deadzones and
  explicit illustration-only response previews.
- Macro piano roll, eight-way stick directions, loop intervals and real XInput
  recording into the M1 draft, using the hardware's 5 ms timing grid.
- Native JSON setup storage, import/export dialogs, saved-setup deletion and
  legacy 1.x migration that preserves which categories were actually present.
- Serialized native device operations, category-scoped writes, read-back
  verification, visible partial failures and explicit sent/unverified results.
- Single-instance desktop handling, close-to-tray/Open/Quit, optional quiet
  GitHub updates and a bundled Microsoft WebView2 fallback for offline startup.
- Genuine executable-window screenshots and repeatable native UI acceptance.
- Windows CI, pinned build dependencies, frontend checks and additional tests
  for bridge validation, persistence, device failures and macro fidelity.

### Fixed

- Updated the release environment to patched Pillow 12.3.0 and pytest 9.0.3 pins after GitHub security review.
- Restored the Python engine, CLI, hardware tools and regression tests deleted
  during the prototype rewrite.
- Macro recording no longer skips adjacent changed inputs or inserts unplayed
  default pauses. Zero-length pauses no longer consume wire entries.
- Missing remap replies no longer look like default mappings; strict macro
  reads distinguish empty slots from missing, corrupt or incomplete replies.
- Extended macro read assembly to cover the full supported record capacity.
- Corrected the 47-entry capacity and 5 ms timing validation, including pauses.
- Preserved unknown curve flags and converted UI percentages through native units.
- Fixed the native bridge readiness race before initial settings/profile loading.
- Removed unsupported Capture/Turbo remaps and Select/Start source remapping.
- Preserved drafts on connection, confirmed replacement of unsent edits, blocked
  duplicate operations and kept failed categories unsent.
- Rejected invalid profiles before persistence and preserved malformed stores
  instead of silently overwriting them.
- Improved controller/editor layout, readable labels and honest unavailable states.

### Removed

- Browser-only hardware configuration and unused, invented TypeScript packets.
- The separate Tkinter mock backend and fabricated macro recording.
- Fake polling-rate, latency/packet-loss and success claims.
- Synthetic marketing screenshots and undocumented preloaded hardware-like data.
- Invented hashes, nonexistent release links and false signed-attestation claims.
- Cross-platform/PWA support claims and misleading X05 selection.
- Unused ZIP/browser-Bluetooth dependencies, obsolete UI modules and stale lockfile.

### Verification limits

Software regression tests and native-window tests are required before publication.
No fresh physical-controller writes, macro replay or reset were tested with the
controller powered off. Intermediate battery levels and exact firmware curve
interpolation remain uncertain. X05 remains unsupported. The executable is
unsigned. Release hashes are generated from the actual final artifact, never
prewritten into documentation.

## [V0.005] - 2026-08-22

Tagged `v1.2.0`.

Adds a quiet update check at launch, and rules out the EasySMX X05 as a
KeyLinker device.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Investigated

- The EasySMX X05: checked against a physical unit and found to have no
  KeyLinker/pulsenet protocol at all, on any transport. BLE scan, XInput, and a
  full HID feature-report sweep all came back empty; the manual confirms RGB,
  macros and vibration are on-pad-only. Documented in
  [docs/00-findings.md](docs/00-findings.md#7-other-controllers-checked) rather
  than left unexplained.

### Changed

- The update check now runs once at launch instead of only on demand, on a
  bottom-left line: a spinner and "Searching for a new update...", then
  "Currently at the latest version" and the version number.
- It stays silent when there is nothing to report. A newer release pops a
  dialog offering Cancel or Update; Update opens the release page. Nobody is
  made to update.
- A GitHub that cannot be reached says so on that line and never opens a
  dialog.
- The check runs on a worker thread. The on-demand one called urllib with an
  eight-second timeout on the UI thread, which would have frozen the window at
  launch on a slow network.

## [V0.004.2] - 2026-08-16

Tagged `v1.1.2`.

A read-everything bug hunt over the whole codebase.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Fixed

- A controller that answered the stick query but not the trigger query crashed
  the load with `AttributeError`, leaving the workspace half filled. Reachable
  from a pad with no triggers and from a single read timing out, which the first
  query on a fresh link is known to do, so it was intermittent.
- The tray kept showing a battery reading after you left a controller, so with
  the window closed it could sit there stale or belong to a pad since switched
  off. It now clears.
- Macro slots were asked for by count rather than by bit position, so a pad with
  a gap in its macro bits would have been asked for the wrong slots.
- `transport.py` raised a `SyntaxWarning` on import from an unescaped device
  path in its docstring. That becomes an error in a future Python.
- `first_hide_to_tray` was created on first use rather than initialised.

### Verified, not changed

- Reading macros from inside the remapping callback is safe: the link releases
  before it calls back, so the chained read is not refused.
- `VibrationPage.load` takes a percentage and `client.vibration()` already
  converts from the raw 0-255 the pad stores. No unit mismatch.
- `transport.py` already documented the 2.4 GHz receiver as transparent, which
  independently confirms this release's dongle correction.

## [V0.004.1] - 2026-08-16

Tagged `v1.1.1`.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Fixed

- Closing the window quit the app and took the tray icon with it. It now hides
  and keeps running; the tray icon stays, clicking it reopens the window, and
  Quit is on its right-click menu.
- The first time the window hides, the tray says where it went.

## [V0.004] - 2026-08-16

Tagged `v1.1.0`.

Acts on the first round of user feedback against 1.0.1.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Added

- Taskbar tray icon showing battery, with Show and Quit
- Swap left and right sticks, on the Sticks page
- Swap L2 and R2, on the Triggers page
- Home button in the Test page, via XInputGetStateEx
- Macros now load from the controller on connect, all four at once

### Changed

- Power moved out of Advanced: a sleep timer needs no warning
- Power page says it is reading, instead of showing a value it has not read
- Battery readings carry the raw status byte, and `report.py` prints it

### Fixed

- Idle shutdown showed 10 minutes regardless of what the controller held
- Chunked `HOST_MENU` records silently truncated: continuations were sent as a
  bare index byte, which that opcode answers with silence
- `tools/ble_enum.py` read an OTA characteristic while claiming to skip OTA

### Protocol

- `HOST_MENU` kind 6 is macro step data; this pad reports 42 steps per slot
- Kind 7 is the device can-change list, and this pad does not answer it
- The full `SET_MODE` page table, `05 DF AB 00 <a> <b>`, including which pair
  enters firmware update
- `SET_MODE 03 DF AB 0A`–`0F` are a 5 ms uptime counter, not six features
- The USB bootloader's command set: opcode `0xF1` reads the same GUID as
  `HOST_GUID` does over BLE, and there is no bulk read at all
- Lighting writes are chunkable and do land; the LEDs ignore them regardless

### Known limitations

- Colour, brightness and RGB mode are on-pad only (`C` + `R3`, `C` + `L3`).
  `caps.lighting` is zero and KeyLinker cannot change them either.
- Battery has four steps because the controller reports four; the intermediate
  decode is unconfirmed against a discharging pad.
- Rear buttons cannot appear to Steam Input as their own buttons: the firmware
  replays them as existing buttons.
- The 2.4 GHz receiver carries no configuration channel.

## [V0.003] - 2026-08-16

Tagged `v1.0.1`.

First stable release. Verified on two EasySMX X20 controllers.

Download **x20ctl.exe** and run it. No install, no Python needed.

### Added

- Standalone Windows executable
- Controller roster: up to four controllers, one per player slot
- Per-controller save files, keyed by Bluetooth address
- Button remapping page
- Macro editor: piano-roll grid, all four slots, per-step timing, repeat
- Macro recording from live play
- Stick direction dial for macro steps, snapping to eight headings
- Saved macros page: whole-controller setups, open in editor or send to pad
- Trigger travel zones and response curves
- Idle shutdown timer, 1–30 minutes or never
- Sensor calibration
- Factory reset, behind a confirmation
- Battery level in the header
- Live connection state per controller, with a prompt when one goes quiet
- Vibration preview: the pad buzzes at the strength being set
- Update check against GitHub releases
- Simple and Advanced modes
- CLI: `sleep`, `remap`, `macro --read`, `calibrate`, `factory-reset`,
  `curve --gear`, `curve --preset`

### Changed

- App opens on the controller roster instead of the macro editor
- Sidebar navigation replaces the tab strip
- Every page that edits the controller has a Save button
- Vibration saves itself; no Apply needed
- Discovery returns every controller found, not just the first
- Higher-contrast text throughout
- Window opens at 1280×820

### Fixed

- Macros capped at 47 steps, where the chunk index runs out, instead of failing
  inside the packet builder
- Macro recording captures the left stick, not only buttons
- Key lists stop at their declared count instead of overrunning a repeated
  record
- Recording nothing no longer crashes
- Select and Start refused as remap sources; the controller accepts and ignores
  them

### Protocol

Decoded and verified on hardware:

- Idle shutdown timer lives inside the motor record, as 5 ms ticks
- Trigger zones are deadzone pairs; response curves are Hermite control points
- Sensor calibration is `SET_MODE 03 DF AB 10`
- Factory reset is `RECOVER 03 DF A9 02`; the wrong generation byte is ignored
  silently
- Macros read back via `HOST_MACRO`, not `READ_MACRO`
  (found by [chriss80](https://github.com/chriss80/x20ctl))
- Select and Start are key codes 93 and 94, not 9 and 10

### Known limitations

- Output mode (XInput / DirectInput) changes only by button combination on the
  controller
- Sensor calibration sends correctly; its effect is unverified
- Lighting and turbo are not configurable over this protocol
- Trigger settings are chosen by name, not drawn
- The executable is unsigned, so Windows warns that the publisher is unknown

## [V0.002.1]

Tagged `v0.2.1`.

Command line tool and single-controller GUI. Development releases.
