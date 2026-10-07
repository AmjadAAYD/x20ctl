"""Native launch bounds; placement math is independent of display APIs."""

from dataclasses import dataclass
import logging
import sys


PREFERRED_SIZE = (1583, 1147)
MINIMUM_SIZE = (1060, 760)
EDGE_MARGIN = 16


@dataclass(frozen=True)
class LaunchBounds:
    x: int
    y: int
    width: int
    height: int
    min_size: tuple[int, int]


def launch_bounds(x: int, y: int, width: int, height: int) -> LaunchBounds:
    """Center the preferred outer size inside the available monitor work area."""
    if width <= 0 or height <= 0:
        raise ValueError("Work area dimensions must be positive")

    def fit(preferred, available):
        return preferred if preferred <= available else max(1, available - 2 * EDGE_MARGIN)

    fitted_width = fit(PREFERRED_SIZE[0], width)
    fitted_height = fit(PREFERRED_SIZE[1], height)
    return LaunchBounds(
        x + (width - fitted_width) // 2,
        y + (height - fitted_height) // 2,
        fitted_width,
        fitted_height,
        (min(MINIMUM_SIZE[0], fitted_width), min(MINIMUM_SIZE[1], fitted_height)),
    )


def prepare_launch_bounds(window) -> None:
    """Install on pywebview.before_show, which runs on the native UI thread."""
    if sys.platform != "win32":
        return

    from System.Drawing import Rectangle, Size
    from System.Windows.Forms import FormStartPosition, Screen

    form = window.native
    # Default CenterScreen can overwrite a manual location on first Show.
    form.StartPosition = FormStartPosition.Manual

    def on_load(_sender, _event):
        # WinForms scales initial Size and MinimumSize from 96 DPI. Set final
        # outer bounds after that scaling, in the same units as WorkingArea,
        # before the first frame. Avoid pywebview.move's extra DPI multiplier.
        form.PerformAutoScale()
        area = Screen.FromControl(form).WorkingArea
        bounds = launch_bounds(area.X, area.Y, area.Width, area.Height)
        form.MinimumSize = Size(*bounds.min_size)
        form.Bounds = Rectangle(bounds.x, bounds.y, bounds.width, bounds.height)
        logging.info(
            "Desktop launch bounds: %sx%s at %s,%s; work area: %sx%s at %s,%s",
            bounds.width, bounds.height, bounds.x, bounds.y,
            area.Width, area.Height, area.X, area.Y,
        )

    form.Load += on_load
