"""Generates side-by-side comparison boards between the reference PNGs (out/pulse/zone.png, out/pulse/hero.png)
and the windowless WPF renders (artifacts/review-pulse/1-zone.png, 2-zone-player.png, 2b-hero.png).
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

REF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\previews\console-concepts\themes\out\pulse")
REV_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\worktree-gemini\artifacts\review-pulse")
BRAIN_DIR = Path(r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23")

FONT_DIR = "C:/Windows/Fonts/"
f_head = ImageFont.truetype(FONT_DIR + "segoeuil.ttf", 26)
f_sub = ImageFont.truetype(FONT_DIR + "segoeui.ttf", 16)


def make_sbs(ref_path: Path, wpf_path: Path, title: str, ref_label: str, wpf_label: str, out_name: str):
    ref = Image.open(ref_path).convert("RGB").resize((800, 450), Image.LANCZOS)
    wpf = Image.open(wpf_path).convert("RGB").resize((800, 450), Image.LANCZOS)
    canvas = Image.new("RGB", (16 + 800 + 16 + 800 + 16, 72 + 450 + 36), (8, 11, 18))
    draw = ImageDraw.Draw(canvas)
    draw.text((20, 18), title, font=f_head, fill=(238, 242, 248))
    canvas.paste(ref, (16, 68))
    canvas.paste(wpf, (16 + 800 + 16, 68))
    draw.rectangle([16, 68, 16 + 799, 68 + 449], outline=(46, 58, 82), width=1)
    draw.rectangle([832, 68, 832 + 799, 68 + 449], outline=(46, 58, 82), width=1)
    draw.text((20, 72 + 450), ref_label, font=f_sub, fill=(168, 204, 255))
    draw.text((836, 72 + 450), wpf_label, font=f_sub, fill=(168, 204, 255))
    out_file = REV_DIR / out_name
    canvas.save(out_file)
    canvas.save(BRAIN_DIR / out_name)
    print("Saved:", out_file)


def main():
    BRAIN_DIR.mkdir(parents=True, exist_ok=True)
    make_sbs(
        REF_DIR / "zone.png",
        REV_DIR / "2-zone-player.png",
        "Pulse · Controller Zone — Reference Board vs. Windowless WPF Render",
        "Reference: previews/console-concepts/themes/out/pulse/zone.png (1600×900)",
        "WPF Render: artifacts/review-pulse/2-zone-player.png (--review-themes pulse, 1600×900)",
        "sbs-pulse-zone.png",
    )
    make_sbs(
        REF_DIR / "hero.png",
        REV_DIR / "2b-hero.png",
        "Pulse · Selected-Controller Hero — Reference Board vs. Windowless WPF Render",
        "Reference: previews/console-concepts/themes/out/pulse/hero.png (1600×900)",
        "WPF Render: artifacts/review-pulse/2b-hero.png (--review-themes pulse, 1600×900)",
        "sbs-pulse-hero.png",
    )


if __name__ == "__main__":
    main()
