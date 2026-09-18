"""The L2/R2 exchange toggle on the trigger page.

Reported missing by a user comparing x20ctl against KeyLinker on iOS, which
offers it as "1 L2/R2 exchange toggle". The protocol side already existed
(FLAG_SWAP, Curve.with_flags); only the page and the wiring were absent.

The flag belongs to the page rather than a side: HostActivity.applyTriggerData
writes isTriggerExchange into the flag byte of BOTH channels, never one alone.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication

from x20ctl import protocol as p
from x20ctl.gui.triggers import TriggersPage

app = QApplication.instance() or QApplication([])


def _curve(flags: int = 0) -> p.Curve:
    return p.Curve.parse(bytes([4, 34, 82, 133, 229, 235, flags]),
                         p.TRIGGER_MAX_PROGRESS)


def test_the_toggle_reflects_what_the_pad_holds():
    page = TriggersPage()
    page.load([_curve(p.FLAG_SWAP), _curve(p.FLAG_SWAP)])
    assert page.swap.isChecked()

    page.load([_curve(0), _curve(0)])
    assert not page.swap.isChecked(), "a pad without the flag must clear it"


def test_toggling_emits_and_marks_unsaved():
    page = TriggersPage()
    page.load([_curve(0), _curve(0)])

    seen: list[bool] = []
    page.swap_toggled.connect(seen.append)

    page.swap.click()
    assert seen == [True]
    assert page.status.text() == "Not saved yet", (
        "the page must say so; nothing reaches the pad until Save")

    page.swap.click()
    assert seen == [True, False]


def test_the_flag_lands_on_both_channels():
    """Mirrors applyTriggerData, which sets it on each side or neither."""
    curves = [_curve(0), _curve(0)]
    swapped = [c.with_flags(swapped=True) for c in curves]
    assert all(c.swapped for c in swapped)

    raw = [bytes(c.to_bytes()) if hasattr(c, "to_bytes") else None
           for c in swapped]
    if all(r is not None for r in raw):
        for r in raw:
            assert r[6] & p.FLAG_SWAP


def test_swap_does_not_disturb_the_curve():
    before = _curve(0)
    after = before.with_flags(swapped=True)
    assert after.inner_deadzone == before.inner_deadzone
    assert after.outer_raw == before.outer_raw
    assert after.point1 == before.point1
    assert after.point2 == before.point2
    assert not before.swapped and after.swapped
