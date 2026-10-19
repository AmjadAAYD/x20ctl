"""Build every X20CTL icon and brand image from the logo master.

The master, desktop-dotnet/X20Ctl.Product/Brand/x20ctl-logo.png, is the finished logo on a transparent 1024 px square:
the same traced paths and textures the intro ends on, rendered by the intro project (logo-preview/intro-remotion,
composition X20CTLIcon). Every file below is scaled from it, so the icon, the header mark and the intro's last frame
are one picture.

  desktop-dotnet/X20Ctl.Product/Brand/x20ctl-mark.png   logo on transparent, 512 px, in-app brand tiles (Windows, Linux)
  desktop-dotnet/X20Ctl.Product/Brand/x20ctl-icon.png   the app icon as an image, 256 px, for the setup's header
  desktop-dotnet/X20Ctl.Product/Brand/x20ctl.ico        app icon, 16-256 px: the logo on a white rounded tile
  assets/x20ctl.png, assets/x20ctl.ico                  the same, for the repository and the legacy desktop app
  src/assets/brand-mark.png                             small mark for the legacy desktop app
  tools/linux-studio/x20ctl.png                         512 px, the Linux packages' icon (white tile)

The white tile follows the owner's white icon (19 Oct 2026): a near-white rounded square, corners 22% of the side, the
logo 86% of its width and a little above centre.

  python tools/build_brand_mark.py
"""
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
BRAND = ROOT / "desktop-dotnet" / "X20Ctl.Product" / "Brand"
MASTER = BRAND / "x20ctl-logo.png"
SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]


def square(master: Image.Image, size: int, fill: float = 1.0) -> Image.Image:
    """The logo centred on a transparent square, its longer side `fill` of the square."""
    box = master.getbbox()
    logo = master.crop(box)
    scale = size * fill / max(logo.size)
    logo = logo.resize((max(1, round(logo.width * scale)), max(1, round(logo.height * scale))), Image.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(logo, ((size - logo.width) // 2, (size - logo.height) // 2))
    return out


def tile(master: Image.Image, size: int) -> Image.Image:
    """The app icon: the logo on a white rounded tile, drawn at 4x and scaled down for smooth corners."""
    big = size * 4
    out = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    ImageDraw.Draw(out).rounded_rectangle((0, 0, big - 1, big - 1), radius=round(big * .22), fill=(249, 249, 249, 255))
    box = master.getbbox(); logo = master.crop(box)
    scale = big * .86 / logo.width
    logo = logo.resize((round(logo.width * scale), round(logo.height * scale)), Image.LANCZOS)
    out.alpha_composite(logo, ((big - logo.width) // 2, round(big * .47 - logo.height / 2)))
    return out.resize((size, size), Image.LANCZOS)


def ico(master: Image.Image, path: Path) -> None:
    icons = [tile(master, s) for s in SIZES]
    icons[-1].save(path, sizes=[(s, s) for s in SIZES], append_images=icons[:-1])


if __name__ == "__main__":
    master = Image.open(MASTER).convert("RGBA")
    square(master, 512, 0.96).save(BRAND / "x20ctl-mark.png")
    ico(master, BRAND / "x20ctl.ico")
    tile(master, 256).save(BRAND / "x20ctl-icon.png")
    tile(master, 256).save(ROOT / "assets" / "x20ctl.png")
    ico(master, ROOT / "assets" / "x20ctl.ico")
    small = square(master, 54, 1.0)
    flat = Image.new("RGB", small.size, (0, 0, 0))  # the legacy mark was RGB on black
    flat.paste(small, mask=small.split()[3])
    flat.crop((1, 0, 52, 54)).save(ROOT / "src" / "assets" / "brand-mark.png")
    tile(master, 512).save(ROOT / "tools" / "linux-studio" / "x20ctl.png")
    print("wrote the mark, the icon image, both .ico files, assets/x20ctl.png, src/assets/brand-mark.png, tools/linux-studio/x20ctl.png")
