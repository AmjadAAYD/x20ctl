"""Macros load on connect instead of needing a press per slot.

Reported: "Each macro needs an individual Read from controller press". They are
now read as ONE operation chained after the remapping read, because this link
refuses overlapping work -- four separate read_macro calls would have three
rejected.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication

app = QApplication.instance() or QApplication([])


class FakeLink:
    """Records what was asked for, and refuses overlaps like the real one."""

    def __init__(self, programs=None) -> None:
        self.calls: list[tuple] = []
        self.busy = False
        self._programs = programs or {}

    def read_macros(self, slots, on_done):
        if self.busy:
            return False
        self.calls.append(("read_macros", list(slots)))
        on_done({s: self._programs.get(s) for s in slots})
        return True

    def read_macro(self, slot, on_done):
        self.calls.append(("read_macro", slot))
        return True


def _workspace(link):
    from x20ctl.gui.shell import Workspace

    ws = Workspace()
    ws.link = link
    return ws


def test_all_four_slots_are_requested_in_one_call():
    link = FakeLink()
    ws = _workspace(link)
    ws._read_all_macros()

    assert link.calls == [("read_macros", [1, 2, 3, 4])], (
        "one operation, not four: the link rejects overlapping work")


def test_empty_slots_do_not_claim_to_have_loaded():
    link = FakeLink(programs={1: None, 2: None, 3: None, 4: None})
    ws = _workspace(link)
    said: list[str] = []
    ws.say = said.append

    ws._read_all_macros()
    assert said and "No macros" in said[-1]


def test_nothing_happens_without_a_link():
    ws = _workspace(None)
    ws._read_all_macros()          # must not raise


def test_a_refused_read_is_not_treated_as_success():
    link = FakeLink()
    link.busy = True
    ws = _workspace(link)
    ws._read_all_macros()
    assert link.calls == [], "a busy link should record no work"
