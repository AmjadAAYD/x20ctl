"""Package the supplied x20ctl mark as the Windows/taskbar icon.

The source is the user-provided logo at src/assets/brand-mark.png. This
script only resizes it; it never redraws or invents the mark.
"""

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "src" / "assets" / "brand-mark.png"
ASSETS = ROOT / "assets"
SIZES = (16, 24, 32, 48, 64, 128, 256)


def main() -> None:
    with Image.open(SOURCE) as original:
        mark = original.convert("RGBA")
        # The source artwork is nearly square. Preserve its proportions and
        # center it on the original dark tile to avoid icon distortion.
        canvas = Image.new("RGBA", (256, 256), (10, 15, 30, 255))
        side = max(mark.size)
        scaled = mark.resize(
            (round(mark.width * 240 / side), round(mark.height * 240 / side)),
            Image.Resampling.LANCZOS,
        )
        canvas.alpha_composite(scaled, ((256 - scaled.width) // 2, (256 - scaled.height) // 2))
        ASSETS.mkdir(exist_ok=True)
        canvas.save(ASSETS / "x20ctl.png")
        canvas.save(ASSETS / "x20ctl.ico", format="ICO", sizes=[(n, n) for n in SIZES])


if __name__ == "__main__":
    main()
