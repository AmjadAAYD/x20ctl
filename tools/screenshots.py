"""Render the app to PNGs for the README.

Runs the real window with representative data rather than a mockup, so a
screenshot can never drift from what the app actually looks like. Uses the real
platform plugin so system fonts resolve, but keeps the window off screen.

    python tools/screenshots.py
"""

from __future__ import annotations

import math
import os
import sys
import time

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QApplication

from x20ctl import protocol as p
from x20ctl.gui import theme
from x20ctl.gui.window import MainWindow
from x20ctl.profiles import MacroSpec, Profile

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..",
                   "assets", "screenshots")


def settle(app, ms: int = 700) -> None:
    """Let animations finish before grabbing, or they capture mid-fade."""
    end = time.time() + ms / 1000
    while time.time() < end:
        app.processEvents()
        time.sleep(0.005)


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    app = QApplication([])
    app.setStyleSheet(theme.STYLESHEET)

    # No auto-connect: a real controller answering mid-render would replace the
    # staged values with its own, and the picture would depend on whether the
    # pad happened to be switched on.
    window = MainWindow(auto_connect=False)
    window.resize(1000, 700)
    window.setAttribute(Qt.WA_DontShowOnScreen, True)
    window.show()

    # Representative content: two slots used, one looping, one empty, so the
    # screenshot shows every card state at once.
    profile = Profile(name="Save file 1")
    profile.macros["M1"] = MacroSpec(keys="A+B", hold_ms=100, gap_ms=60)
    profile.macros["M2"] = MacroSpec(keys="X:150/40, Y:80", hold_ms=100, gap_ms=60)
    profile.macros["M3"] = MacroSpec(keys="LS_UP+A", hold_ms=120, gap_ms=60,
                                     loop_ms=200)
    profile.vibration = 45
    window.load_into_form(profile)

    window.profile_list.clear()
    for name in ("Save file 1", "Save file 2", "Precision aim"):
        window.profile_list.addItem(name)
    window.profile_list.setCurrentRow(0)
    window.load_into_form(profile)

    settle(app, 300)

    window.dot.set_state("connected")
    # Through the same call the app uses, so the picture can't claim a name the
    # app would never show. It said "Xpert2" for a while after product_name
    # landed, purely because it was typed in here.
    window.device_name.setText(p.product_name("Xpert2", "0710", "1320"))
    window.device_detail.setText(window.describe_device("9.01", "98:B6:ED:E3:15:C4"))
    window.connect_button.setText("Reconnect")
    window.battery_label.setText("🔋 4/4")
    window.link_label.setText("playing over USB or 2.4 GHz receiver")
    window.notice.setText(
        "This controller does not expose lighting, turbo, gyro to the "
        "configuration protocol, so they are not shown. They are controlled by "
        "button combinations on the pad itself.")
    window.notice.show()
    window.set_status("connected", "Success")
    window.apply_button.setEnabled(True)

    settle(app, 250)
    window.grab().save(os.path.join(OUT, "settings.png"))
    print("wrote settings.png")

    # tester, with a stick trail and some buttons held
    window.tester_button.setChecked(True)
    window.toggle_tester()
    tester = window.tester
    settle(app, 300)
    tester.timer.stop()          # freeze so the sample data survives the grab

    tester.state.setText("XInput slot 0")
    tester.link_label.setText("USB or 2.4 GHz receiver")
    tester.rate_label.setText("1002")
    tester.peak_label.setText("peak 1011 Hz")
    tester.hint.setText("measuring")
    for key in (p.Key.A, p.Key.RB, p.Key.DPAD_LEFT):
        tester.lamps[key].set_lit(True)
    for i in range(48):
        angle = i / 48 * 2 * math.pi
        tester.left_stick.set_position(int(29000 * math.cos(angle)),
                                       int(29000 * math.sin(angle)))
    tester.right_stick.set_position(7000, -11000)
    tester.left_trigger.set_value(214)
    tester.right_trigger.set_value(52)

    settle(app, 400)
    window.grab().save(os.path.join(OUT, "tester.png"))
    print("wrote tester.png")

    # sticks and triggers, using the records a real X20 reports
    window.curves_button.setChecked(True)
    window.toggle_curves()
    window.curves.set_available(True, True)
    for kind, raw, scale in (
            ("sticks", "08085555aaaa00", p.STICK_MAX_PROGRESS),
            ("triggers", "04225285e5eb00", p.TRIGGER_MAX_PROGRESS)):
        curve = p.Curve.parse(bytes.fromhex(raw), scale)
        window.curves.load(kind, [curve, curve], baseline=True)
    # One channel edited, so the page shows both states side by side.
    window.curves.editors[("sticks", 0)].set_curve(
        p.Curve.parse(bytes.fromhex("08085555aaaa00"), p.STICK_MAX_PROGRESS)
        .with_deadzones(inner=4).with_points((100, 70), (190, 195)))
    window.curves._sync()
    window.curves.set_status("edited, not yet written", "Muted")

    settle(app, 400)
    window.grab().save(os.path.join(OUT, "curves.png"))
    print("wrote curves.png")

    window.bridge.shutdown()
    return 0


if __name__ == "__main__":
    sys.exit(main())
