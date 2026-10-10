"""Build the X20CTL brand mark for the native app.

The mark is the hooked two-piece X chosen on 10 Oct 2026: two identical pieces, one turned half a turn about the
centre (the same points as `IntroView.Mark` in desktop-dotnet/X20Ctl.Product/ProductExperience.cs). The logo is the
mark in white; the intro shows it in red.

Writes:
  desktop-dotnet/X20Ctl.Product/Brand/x20ctl-mark.png  white mark on transparent, 512 px, for in-app brand tiles
  desktop-dotnet/X20Ctl.Product/Brand/x20ctl.ico       white mark on a near-black rounded tile, 16-256 px, app icon

Run from the repository root:  python tools/build_brand_mark.py
"""
from pathlib import Path

from PIL import Image, ImageDraw

PIECE = [(-53, -127), (-187, -127), (-64, -4), (-314, 258), (-20, -2), (-116, -98), (-66, -98), (8, -24), (29, -45)]
HALF_W, HALF_H = 314, 258  # the mark's half extents in its own units
OUT = Path(__file__).resolve().parents[1] / "desktop-dotnet" / "X20Ctl.Product" / "Brand"
SUPERSAMPLE = 8


def draw_mark(size: int, fill: float, colour=(255, 255, 255, 255), tile=None) -> Image.Image:
    """The mark centred in a size x size image, its larger extent filling `fill` of the side."""
    big = size * SUPERSAMPLE
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    if tile is not None:
        draw.rounded_rectangle((0, 0, big - 1, big - 1), radius=int(big * 0.22), fill=tile)
    scale = big * fill / (2 * max(HALF_W, HALF_H))
    centre = big / 2
    for sign in (1, -1):  # the piece, then its half-turn twin
        draw.polygon([(centre + sign * x * scale, centre + sign * y * scale) for x, y in PIECE], fill=colour)
    return image.resize((size, size), Image.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    draw_mark(512, 0.96).save(OUT / "x20ctl-mark.png")
    sizes = [16, 24, 32, 48, 64, 128, 256]
    icons = [draw_mark(s, 0.74, tile=(11, 12, 16, 255)) for s in sizes]
    icons[-1].save(OUT / "x20ctl.ico", sizes=[(s, s) for s in sizes], append_images=icons[:-1])
    print(f"wrote {OUT / 'x20ctl-mark.png'} and {OUT / 'x20ctl.ico'}")


if __name__ == "__main__":
    main()
