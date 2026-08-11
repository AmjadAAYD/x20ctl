# If your controller isn't working

Work down this list. Most problems are one of the first three. If none of it helps,
the last section tells you how to send me something I can actually act on.

---

## 1. Bluetooth is off, or the pad isn't paired

This catches most people, and it isn't obvious.

**Settings travel over Bluetooth, even if you play wired or on the 2.4GHz receiver.**
The pad exposes its configuration on a separate Bluetooth connection from the one it
plays over. Both can be live at the same time. So:

- Turn Bluetooth on in Windows
- Pair the controller if you never have
- Playing over USB or the dongle is fine, leave it as it is

The input tester works without Bluetooth. Everything else needs it.

## 2. The pad is asleep

It drops the Bluetooth link when it's been idle. Press any button on it and try again.

If the app remembered an address from an earlier session and the pad has since gone
quiet, you'll get "controller not found". Same fix.

## 3. It's connected to something else

Windows will happily pair the pad while your phone still holds it. Disconnect it from
the phone, or turn the phone's Bluetooth off, and try again.

---

## 4. The app opens but never finds anything

Run this and see what comes back:

```bash
x20 scan
```

It looks for a Bluetooth peripheral advertising as `Xpert2`, or one with a MAC in the
vendor's range. If it finds nothing at all, the pad isn't advertising, so go back to
step 1.

If you know the address already, skip the search:

```bash
x20 status --address AA:BB:CC:DD:EE:FF
```

## 5. It connects, but writing a setting does nothing

Read the setting back and see whether the pad took it:

```bash
x20 status
```

Every write in this app re-reads the record afterwards and tells you what the
controller reports, rather than assuming it worked. If the value didn't change, the pad
rejected it. That's worth reporting, and section 7 explains how.

## 6. Windows or your antivirus blocked the exe

It's an unsigned one-file build, which is enough to trip antivirus heuristics by
itself, with nothing actually wrong. See
[SECURITY.md](SECURITY.md) for how to check the download against its published hash, or
skip the binary entirely:

```bash
pip install -e ".[gui]"
```

That runs the same app from source.

---

## 7. None of that worked

Then I need to see what your controller actually says about itself, because everything
in this project was decoded from one pad and yours may differ.

```bash
python tools/report.py
```

**It only reads.** No setting is changed, no firmware is touched, and the file it writes
is plain text you can read before sending it anywhere. Bluetooth addresses are cut down
to the vendor prefix, and other people's devices nearby are counted rather than named.

It produces `controller-report.txt`. Open it, check you're happy with what's in it, then
open an issue and paste it:

https://github.com/AmjadAAYD/x20ctl/issues

Say what you tried and what happened. "It didn't connect" plus that file is genuinely
enough to work from.

---

## Things that are not faults

- **Lighting, turbo and gyro don't appear.** An X20 reports zero for those in its own
  capability descriptor. They exist in the hardware but run off button combinations on
  the pad, and aren't reachable through this protocol on that model. Another controller
  may report them as available, in which case they will simply show up.
- **A macro can't do a specific stick angle.** Sticks store eight compass directions at
  full deflection. That's the format, not a bug. See
  [docs/01-protocol.md](docs/01-protocol.md).
- **Read-back can't confirm a macro.** The pad returns zeros for macro slots whether or
  not one is written. Only pressing the button proves it.

## If everything breaks

Hold `C` for five seconds. That's a factory reset of the settings, and it undoes
anything this app can do. Firmware is never involved.
