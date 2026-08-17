"""The power page must not claim a timeout before the pad has been read.

A user reported: "It doesn't seem to be reading the existing config (I have
timeout set to 30 minutes, but it shows 10 minutes)". The cause was PowerPage
ending __init__ with load(10), so the page asserted a value the pad had never
given it. The timeout itself was always available: it lives in the motor record,
which Snapshot already reads.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication

from x20ctl import protocol as p
from x20ctl.client import Snapshot
from x20ctl.gui.panels import PowerPage

app = QApplication.instance() or QApplication([])


def _snapshot(motor: bytes | None) -> Snapshot:
    return Snapshot(
        device=None, capabilities=None, name="Xpert2", vibration=(76, 76),
        sticks=[], triggers=[], battery=None,
        raw={"motor": motor} if motor is not None else {},
    )


def test_page_makes_no_claim_before_the_pad_answers():
    page = PowerPage()
    assert not page.loaded
    assert page.readout.text() == "reading..."
    assert "Reading" in page.status.text()


def test_loading_marks_it_read_and_clears_the_notice():
    page = PowerPage()
    page.load(30)
    assert page.loaded
    assert "30 minute" in page.readout.text()
    assert page.status.text() == ""


def test_never_round_trips():
    page = PowerPage()
    page.load(None)
    assert page.never.isChecked()
    assert page.readout.text() == "never"
    assert page.value() is None


def test_snapshot_exposes_the_timeout_from_the_motor_record():
    """The record as READ is [L1, L2, R1, R2, t0..t3]; the length byte is
    already stripped by unwrap, so the timeout starts at offset 4."""
    ticks = (30 * p.TICKS_PER_MINUTE).to_bytes(4, "little")
    motor = bytes([0xB2, 0xB2, 0, 0]) + ticks
    assert _snapshot(motor).shutdown == 30

    never = bytes([0xB2, 0xB2, 0, 0]) + b"\xff\xff\xff\xff"
    assert _snapshot(never).shutdown is None


def test_the_pads_factory_record_reads_as_ten_minutes():
    """Verbatim from an X20 on firmware 9.01, via tools/report.py."""
    assert _snapshot(bytes.fromhex("b2b20000c0d40100")).shutdown == 10


def test_snapshot_without_a_motor_record_says_nothing():
    assert _snapshot(None).shutdown is None
    assert _snapshot(b"\xb2\xb2").shutdown is None, (
        "a short record has no field; that must not raise into a settings page")
