# If Your Controller Isn't Working

This guide covers quick fixes for V0.025 on Windows and Linux.

## The tester shows no input

- **Check how the controller is connected:** Make sure the USB cable supports data (some charging cables do not) or the 2.4 GHz wireless receiver is seated directly in a PC USB port rather than an unpowered hub.
- **X20 Pro / X20 on 2.4 GHz dongle or DInput (`STK-7024X` / `0079:181C`):** If Steam or Windows reports a generic DInput pad (`STK-7024X`) and inputs do not appear in XInput tests, switch the physical controller back to **XInput mode** (PC mode on the controller hardware, not X20CTL's virtual PC Mode). Shared `0079:181C` DInput devices remain `Unconfirmed` because DInput descriptors are ambiguous across models.
- **Steam Input conflict:** Steam Input can intercept or double-read your pad when PC Mode is active. Turn off **Steam Input** for that game or for generic controllers in Steam Settings while testing.
- **Linux permissions / exclusive grab:** Ensure your user has read access to `/dev/input/event*` and, for PC Mode, write permission to `/dev/uinput` (see [`docs/linux-user-guide.md`](docs/linux-user-guide.md)).

## I closed the app, or it seems to be still running

- Clicking the window **X** button can minimize X20CTL to the **system tray** so active **PC Mode** profiles stay live while you game. Look for the X20CTL icon in the Windows notification area (system tray) or Linux tray bar, right-click it, and choose **Quit** to close completely.
- Closing or quitting X20CTL releases the virtual **PC Mode** controller (`ViGEmBus` / `uinput`) and restores normal hardware input immediately. If a game still sees a phantom controller after a crash, unplug and replug your physical pad or toggle HidHide/controller state.

## Nothing appears in the scan

- Power on the controller and verify Windows Settings -> **Bluetooth & devices** (or `joy.cpl` / `ls /dev/input/event*` on Linux) sees the hardware or dongle.
- **X20 Pro simultaneous 2.4 GHz dongle + BLE:** You can play over the 2.4 GHz receiver (`413D:2131`) while X20CTL configures RGB lighting and supported settings over simultaneous unpaired BLE — just make sure **PC Bluetooth is turned on** (no manual Windows Bluetooth pairing needed).
- **Incorrect model popup (`Detected <other>, not <chosen>`):** In V0.025, if Windows identifies another EasySMX model than the tile you picked, choose **Continue as \<chosen\>** only if you are certain of the physical hardware, **Enter Studio as \<detected\>** to switch, or **Cancel**. Native BLE writes still double-check the hardware's own identity before writing.

## A saved-profile error appeared

- If X20CTL reports a **saved-profile error**, the local JSON file for your draft or PC Mode profile could not be read cleanly (for example, after an interrupted write).
- X20CTL preserves the damaged file rather than silently overwriting it. Use **Reset selected profile** (under PC Mode / Dashboard) or **Reset** in **Tools**, or move the broken file out of `%LocalAppData%\X20CTL` (Windows) / `~/.config/x20ctl` (Linux) and re-save your setup.

## A write failed

- Move closer to the PC or use a wired USB connection / freshly paired Bluetooth link (`Tools -> Bluetooth manager`) before applying native changes on X20 or X20 Pro.
- Close competing controller configuration apps (official EasySMX software, KeyLinker, or active game updaters) that may lock the Bluetooth GATT or USB serial interface.
- Keep **PC Mode off** when saving native button/curve/macro/screen changes directly onto an X20 or X20 Pro. When PC Mode is on for any supported model, **Apply to PC mode** updates local software remapping instead of flashing controller memory.
- Note: Linux Studio currently reads native settings via the optional Python `x20` client and does not write native settings over Bluetooth.

## Reset and reporting

- **Hardware factory reset (X20 / X20 Pro):** Hold **C** on the controller for 5 seconds (see [`docs/00-findings.md`](docs/00-findings.md)), or re-save defaults cleanly in Studio. X20CTL never flashes firmware.
- **App reset:** Open **Tools** to reset local drafts/preferences or run **Controller Check** -> **Write scan** (read-only identify & read-back diagnostics).
- **Need help?** Open **Diagnostics -> Copy report** (or **Agent XCTL -> Check-up**), verify no personal note you typed contains private info, and submit via the **Feedback** tab or GitHub Issues.
