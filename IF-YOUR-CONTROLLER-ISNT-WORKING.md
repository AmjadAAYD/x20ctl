# If Your Controller Isn't Working

Work down this list. Most problems are one of the first three. If none of it helps, the last section tells you how to report what your controller reports.

---

## 1. Bluetooth Is Off, or the Controller Isn't Paired

**Settings travel over Bluetooth Low Energy (BLE), even if you play wired or on the 2.4 GHz receiver.**
The controller exposes its configuration channel on a separate Bluetooth peripheral (`Xpert2`) from the game input channel. Both can be active at the same time:

- Make sure Bluetooth is enabled on your computer or device.
- Pair the controller via Bluetooth if you have never done so.
- If using the **Web Suite** in Google Chrome or Microsoft Edge, ensure Web Bluetooth is allowed when prompted by the browser.
- Playing over USB or the 2.4 GHz wireless dongle is completely supported; the live input tester functions over any connection type.

## 2. The Controller Has Gone to Sleep

The controller drops its Bluetooth link after its idle timeout (default 10 minutes). Press any button on the controller to wake it up and reconnect.

## 3. The Controller Is Connected to Another Device

If the gamepad was previously paired to your mobile phone or another computer, it may connect there first. Disconnect it or turn off Bluetooth on that device so your computer can claim the link.

---

## 4. Browser Web Bluetooth Permissions (Web Suite)

When connecting through the Web Suite:
- Use a browser supporting Web Bluetooth (Chrome, Edge, Opera, Brave).
- Click **"Scan for Controller"** in the top navigation or introductory scanner.
- Select your controller (`Xpert2` or `KeyLinker`) in the browser's pairing dialog and click **Pair**.
- Note: Safari and Firefox currently do not support the Web Bluetooth API standard natively; use Chrome or Edge for BLE configuration.

## 5. It Connects, but a Written Setting Doesn't Change

Every write in the app validates against the controller's confirmed packet protocol. If a value does not take effect:
- Check if your controller model exposes that feature (e.g. RGB lighting and gyro are not exposed by the X20 firmware).
- Check the battery level; low battery can cause the controller to reject configuration writes to conserve power.

---

## 6. How to Report an Issue

If you encounter unexpected behavior:
1. Note your controller model, firmware version, and connection mode.
2. Note your operating system and browser version (if using the Web Suite).
3. Open an issue on GitHub:
   https://github.com/AmjadAAYD/x20ctl/issues

---

## Emergency Reset: If Everything Breaks

Hold the **`C` button** on the controller for **5 seconds**.
This executes an instantaneous hardware factory reset of all configuration settings back to manufacturer defaults. The firmware is never altered, so a factory reset always restores factory behavior.
