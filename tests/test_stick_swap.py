"""The left/right stick swap toggle on the curves page.

KeyLinker offers reverse X, reverse Y and a swap; x20ctl had the first two and
not the third. The flag was already decoded (FLAG_SWAP); only the control was
missing.

Swap belongs to the page, not a channel card: HostActivity.applyRockData writes
the same `rock_jiaohuan` bit into the flag byte of BOTH halves of the record.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication

from x20ctl import protocol as p
from x20ctl.gui.curves import CurvesPage

app = QApplication.instance() or QApplication([])


def _sticks(flags: int = 0) -> list[p.Curve]:
    raw = bytes([8, 5, 85, 85, 170, 170, flags])
    return [p.Curve.parse(raw, p.STICK_MAX_PROGRESS) for _ in range(2)]


def test_toggle_reflects_the_pad():
    page = CurvesPage()
    page.load("sticks", _sticks(p.FLAG_SWAP), baseline=True)
    assert page.swap_sticks.isChecked()

    page.load("sticks", _sticks(0), baseline=True)
    assert not page.swap_sticks.isChecked()


def test_the_flag_reaches_both_channels_on_write():
    page = CurvesPage()
    page.load("sticks", _sticks(0), baseline=True)
    assert all(not c.swapped for c in page.values("sticks"))

    page.swap_sticks.setChecked(True)
    written = page.values("sticks")
    assert len(written) == 2
    assert all(c.swapped for c in written), (
        "the pad stores the bit on both halves or neither")


def test_swapping_counts_as_a_change():
    page = CurvesPage()
    page.load("sticks", _sticks(0), baseline=True)
    assert "sticks" not in page.changed_kinds()

    page.swap_sticks.setChecked(True)
    assert "sticks" in page.changed_kinds(), (
        "an unsaved swap must show as pending, or Write looks like a no-op")


def test_swap_leaves_the_curve_alone():
    page = CurvesPage()
    page.load("sticks", _sticks(0), baseline=True)
    before = page.values("sticks")
    page.swap_sticks.setChecked(True)
    after = page.values("sticks")
    for a, b in zip(before, after):
        assert a.inner_deadzone == b.inner_deadzone
        assert a.point1 == b.point1 and a.point2 == b.point2


def test_triggers_are_untouched_by_the_stick_swap():
    page = CurvesPage()
    page.load("triggers", [p.Curve.parse(bytes([4, 34, 82, 133, 229, 235, 0]),
                                         p.TRIGGER_MAX_PROGRESS)
                           for _ in range(2)], baseline=True)
    page.swap_sticks.setChecked(True)
    assert all(not c.swapped for c in page.values("triggers")), (
        "L2/R2 exchange is a separate control on the Triggers page")
