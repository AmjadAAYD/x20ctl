"""Draw the application icon.

The mark is the same one the sidebar draws: a rounded gamepad body with two
stick wells. Generating it rather than shipping a binary means the icon and the
in-app wordmark can never drift apart, and the palette comes from one place.

    python tools/make_icon.py

Writes assets/x20ctl.ico plus a PNG for anywhere that wants one.
"""

from __future__ import annotations

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

os.environ.setdefault("QT_QPA_PLATFORM", "offscreen")

from PySide6.QtCore import QPointF, QRectF, Qt
from PySide6.QtGui import (
    QColor, QImage, QLinearGradient, QPainter, QPainterPath, QRadialGradient,
)
from PySide6.QtWidgets import QApplication

from x20ctl.gui import theme

# Windows picks whichever of these fits the context, so all of them matter.
SIZES = (16, 24, 32, 48, 64, 128, 256)

ASSETS = os.path.join(os.path.dirname(__file__), "..", "assets")


def draw(size: int) -> QImage:
    image = QImage(size, size, QImage.Format_ARGB32)
    image.fill(Qt.transparent)

    painter = QPainter(image)
    painter.setRenderHint(QPainter.Antialiasing)
    s = size / 256.0            # everything below is authored at 256

    # -- rounded tile, warm and slightly lit from the top left ------------
    tile = QRectF(8 * s, 8 * s, 240 * s, 240 * s)
    backdrop = QLinearGradient(tile.topLeft(), tile.bottomRight())
    backdrop.setColorAt(0.0, QColor("#2A2320"))
    backdrop.setColorAt(1.0, QColor("#141110"))
    path = QPainterPath()
    path.addRoundedRect(tile, 56 * s, 56 * s)
    painter.setPen(Qt.NoPen)
    painter.setBrush(backdrop)
    painter.drawPath(path)

    # A faint ember glow behind the mark gives the tile some depth without
    # resorting to a drop shadow, which reads poorly at 16 pixels.
    glow = QRadialGradient(QPointF(128 * s, 118 * s), 108 * s)
    glow.setColorAt(0.0, QColor(255, 138, 91, 60))
    glow.setColorAt(1.0, QColor(255, 138, 91, 0))
    painter.setBrush(glow)
    painter.drawPath(path)

    # -- the pad body -----------------------------------------------------
    body = QRectF(46 * s, 86 * s, 164 * s, 92 * s)
    ember = QLinearGradient(body.topLeft(), body.bottomRight())
    ember.setColorAt(0.0, QColor("#FFA173"))
    ember.setColorAt(1.0, QColor("#E8703F"))
    painter.setBrush(ember)
    painter.drawRoundedRect(body, 40 * s, 40 * s)

    # -- two stick wells --------------------------------------------------
    # Filled with the tile colour over punched through the alpha. A
    # destination-out punch would make them transparent all the way through the
    # icon, so they would show the desktop rather than the tile.
    #
    # Sized generously: at 16 pixels a well is barely two pixels across, and if
    # it closes up the mark stops reading as a controller at all.
    painter.setBrush(QColor("#221C19"))
    painter.setPen(Qt.NoPen)
    radius = 23 * s
    painter.drawEllipse(QPointF(93 * s, 132 * s), radius, radius)
    painter.drawEllipse(QPointF(163 * s, 132 * s), radius, radius)

    # A hairline lifts the tile off a dark taskbar.
    painter.setBrush(Qt.NoBrush)
    painter.setPen(QColor(255, 255, 255, 18))
    painter.drawPath(path)

    painter.end()
    return image


def main() -> None:
    app = QApplication.instance() or QApplication([])   # noqa: F841
    os.makedirs(ASSETS, exist_ok=True)

    png_paths = []
    for size in SIZES:
        image = draw(size)
        path = os.path.join(ASSETS, f"icon-{size}.png")
        image.save(path)
        png_paths.append(path)

    master = os.path.join(ASSETS, "x20ctl.png")
    draw(256).save(master)

    try:
        from PIL import Image
    except ImportError:
        print("Pillow not installed; wrote PNGs but no .ico")
        return

    ico = os.path.join(ASSETS, "x20ctl.ico")
    with Image.open(png_paths[-1]) as largest:
        largest.load()      # Pillow is lazy; force the read before the file goes
        largest.save(ico, format="ICO", sizes=[(s, s) for s in SIZES])

    # The .ico carries every size, so the intermediates are just clutter.
    for path in png_paths:
        try:
            os.remove(path)
        except OSError:
            pass

    print(f"wrote {ico}")
    print(f"wrote {master}")
    print("sizes:", ", ".join(str(s) for s in SIZES))


if __name__ == "__main__":
    main()
