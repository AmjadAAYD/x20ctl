"""Build every X20CTL icon and brand image from the logo master.

The master, desktop-dotnet/X20Ctl.Product/Brand/x20ctl-logo.png, is the finished logo on a transparent 1024 px square:
the same traced paths and textures the intro ends on, rendered by the intro project (logo-preview/intro-remotion,
composition X20CTLIcon). Every file below is scaled from it, so the icon, the header mark and the intro's last frame
are one picture.

  desktop-dotnet/X20Ctl.Product/Brand/x20ctl-mark.png   logo on transparent, 512 px, in-app brand tiles (Windows, Linux)
  desktop-dotnet/X20Ctl.Product/Brand/x20ctl.ico        logo on transparent, 16-256 px, app icon
  assets/x20ctl.png, assets/x20ctl.ico                  the same, for the repository and the legacy desktop app
  src/assets/brand-mark.png                             small mark for the legacy desktop app
  tools/linux-studio/x20ctl.png                         512 px, the Linux packages' icon

  python tools/build_brand_mark.py
"""
from pathlib import Path
from PIL import Image

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


def ico(master: Image.Image, path: Path) -> None:
    # small sizes fill more of their square, so the mark stays legible at 16 px
    icons = [square(master, s, 1.0 if s <= 32 else 0.94) for s in SIZES]
    icons[-1].save(path, sizes=[(s, s) for s in SIZES], append_images=icons[:-1])


if __name__ == "__main__":
    master = Image.open(MASTER).convert("RGBA")
    square(master, 512, 0.96).save(BRAND / "x20ctl-mark.png")
    ico(master, BRAND / "x20ctl.ico")
    square(master, 256, 0.94).save(ROOT / "assets" / "x20ctl.png")
    ico(master, ROOT / "assets" / "x20ctl.ico")
    small = square(master, 54, 1.0)
    flat = Image.new("RGB", small.size, (0, 0, 0))  # the legacy mark was RGB on black
    flat.paste(small, mask=small.split()[3])
    flat.crop((1, 0, 52, 54)).save(ROOT / "src" / "assets" / "brand-mark.png")
    square(master, 512, 0.9).save(ROOT / "tools" / "linux-studio" / "x20ctl.png")
    print("wrote the mark, both .ico files, assets/x20ctl.png, src/assets/brand-mark.png, tools/linux-studio/x20ctl.png")
