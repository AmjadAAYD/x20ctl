"""The launch update check.

Asked for directly: search on open with a spinner in the bottom left, say
"Currently at the latest version" and the version when there is nothing, and
only pop a dialog — Cancel or Update — when there really is a newer release.
"""

from __future__ import annotations

from PySide6.QtWidgets import QApplication, QMessageBox

from x20ctl import __version__
from x20ctl.gui.updatebar import UpdateBar
from x20ctl.gui.updates import Release

app = QApplication.instance() or QApplication([])


def _pump(bar: UpdateBar, tries: int = 400) -> None:
    """Let the worker thread finish and deliver its queued signal."""
    for _ in range(tries):
        app.processEvents()
        if bar.spinner.running is False and bar._thread is None:
            return


def test_it_shows_the_version_before_anything_runs():
    bar = UpdateBar()
    assert __version__ in bar.label.text()
    assert bar.spinner.isHidden(), "no spinner until a check starts"


def test_searching_says_so_and_spins():
    bar = UpdateBar()
    bar.start(fetch=lambda: Release(version="v0.0.1"))
    assert "Searching" in bar.label.text()
    assert bar.spinner.running
    _pump(bar)


def test_up_to_date_names_the_version_and_does_not_interrupt():
    bar = UpdateBar()
    seen: list = []
    bar.finished.connect(lambda _m, r: seen.append(r))

    bar.start(fetch=lambda: Release(version=__version__))
    _pump(bar)

    assert seen and seen[-1] is None, "no release means nothing to offer"
    assert "latest version" in bar.label.text()
    assert __version__ in bar.label.text()
    assert not bar.spinner.running


def test_a_newer_release_is_reported():
    bar = UpdateBar()
    seen: list = []
    bar.finished.connect(lambda _m, r: seen.append(r))

    bar.start(fetch=lambda: Release(version="v99.0.0"))
    _pump(bar)

    assert seen and seen[-1] is not None
    assert "Update available" in bar.label.text()
    assert "99" in bar.label.text()


def test_an_unreachable_github_stays_quiet():
    """Not being able to reach GitHub is not the user's problem."""
    def boom():
        raise OSError("no network")

    bar = UpdateBar()
    seen: list = []
    bar.finished.connect(lambda _m, r: seen.append(r))

    bar.start(fetch=boom)
    _pump(bar)

    assert seen and seen[-1] is None, "a failure must never offer an update"
    assert "failed" in bar.label.text()
    assert not bar.spinner.running


def test_the_dialog_offers_cancel_as_well_as_update():
    bar = UpdateBar()
    box = bar.offer(Release(version="v9.9.9", notes="things"), show=False)
    labels = [b.text().replace("&", "") for b in box.buttons()]
    assert "Update" in labels and "Cancel" in labels
    assert box.defaultButton().text().replace("&", "") == "Update"
    assert "9.9.9" in box.text() and __version__ in box.text()


def test_two_checks_do_not_stack():
    bar = UpdateBar()
    bar.start(fetch=lambda: Release(version=__version__))
    bar.start(fetch=lambda: Release(version="v99.0.0"))   # ignored
    _pump(bar)
    assert "latest version" in bar.label.text()
