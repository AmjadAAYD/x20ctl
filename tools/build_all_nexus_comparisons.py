"""Generates comprehensive side-by-side comparison boards for ALL 23 Nexus boards
between reference concept boards (previews/console-concepts/themes/SEND-TO-CHATGPT/boards/nexus)
and WPF rendered windowless captures (artifacts/nexus).
Also computes exact Mean RGB Difference metrics across all 23 boards and generates a markdown summary report.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

REF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\previews\console-concepts\themes\SEND-TO-CHATGPT\boards\nexus")
WPF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\artifacts\nexus")
if not WPF_DIR.exists():
    WPF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\worktree-gemini\artifacts\nexus")
BRAIN_DIR = Path(r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23")

FONT_DIR = "C:/Windows/Fonts/"
try:
    f_head = ImageFont.truetype(FONT_DIR + "segoeui.ttf", 22)
    f_sub = ImageFont.truetype(FONT_DIR + "segoeui.ttf", 14)
    f_bold = ImageFont.truetype(FONT_DIR + "segoeuib.ttf", 15)
except Exception:
    f_head = ImageFont.load_default()
    f_sub = ImageFont.load_default()
    f_bold = ImageFont.load_default()

PAIRS = [
    ("zone.png", "2-zone-player.png", "1. Controller Zone (4 Player Tiles & Top Bar)"),
    ("hero.png", "2b-hero.png", "2. Controller Hero Focus (Centered Pad & Stats Column)"),
    ("choose.png", "11c-choose.png", "3. Choose Controller (212x174 Focused Tile & Neighbor Carousel)"),
    ("dashboard.png", "3-dashboard.png", "4. Studio Dashboard (Key Art Band, Top Bar & Focused Buttons Tile)"),
    ("buttons.png", "4-buttons.png", "5. Button Mapping (560x560 Panel, Dropdowns & Glowing A-Button)"),
    ("curves.png", "5-curves.png", "6. Response Curves (860x580 SVG Plot & 580x580 Green Faders)"),
    ("advanced.png", "8g-advanced.png", "7. Advanced Stick Tools (Dead Zones & Straight 1:1 Curve)"),
    ("macros.png", "6-macros.png", "8. Macro Editor (Timeline Lane & Event Steps)"),
    ("vibration.png", "7-vibration.png", "9. Vibration Control (Strength Slider & Grip Outline)"),
    ("pcmode.png", "8b-pcmode.png", "10. PC Mode Signal Pipeline (Signal Route Diagram & Steps)"),
    ("device.png", "8-device.png", "11. Device Management (Hardware Specs & Wireless Dongle Card)"),
    ("diagnostics.png", "8f-diagnostics.png", "12. Input Diagnostics (Dual Stick Plots & 1000 Hz Telemetry)"),
    ("mouse.png", "8c-mouse.png", "13. Mouse Emulation (Action Assignments & Clicks Category)"),
    ("setups.png", "8d-setups.png", "14. Setups Management (Angled Pad, Action Buttons & Saved Setup Carousel)"),
    ("tester.png", "8e-tester.png", "15. Input Tester (Centered Controller & Bottom Gauges Shelf)"),
    ("agent.png", "8i-agent-answer.png", "16. Agent Guide (Xbox Guide Slide-in Panel & Chat Bubbles)"),
    ("hub.png", "9-tools.png", "17. Tools Hub (Hero Tile, 6 Feature Tiles & Feedback Tile)"),
    ("tools.png", "10-themes.png", "18. Theme Selector (10 Theme Tiles & Nexus Apply Card)"),
    ("about.png", "11-about.png", "19. About Page (System Version, Credits & Independent Studio Card)"),
    ("feedback.png", "11b-feedback.png", "20. Community Feedback (Dropdowns, Focused Message & Quote Box)"),
    ("check.png", "11d-check.png", "21. Controller Check / Read Scan (Step Rows & Pad Visual)"),
    ("writescan.png", "11e-writescan.png", "22. Write Scan (Information Steps & Planning Notice Card)"),
    ("notices.png", "13-notice-restart.png", "23. Themed Modal Notices (Xbox Guide Dialog & Stacked Toasts)"),
]

def compute_diff(img1_path: Path, img2_path: Path):
    im1 = Image.open(img1_path).convert("RGB").resize((1600, 900), Image.Resampling.LANCZOS)
    im2 = Image.open(img2_path).convert("RGB").resize((1600, 900), Image.Resampling.LANCZOS)
    arr1 = np.array(im1, dtype=np.float32)
    arr2 = np.array(im2, dtype=np.float32)
    diff = np.abs(arr1 - arr2)
    mean_diff = float(np.mean(diff))
    return mean_diff

def make_sbs(ref_path: Path, wpf_path: Path, title: str, out_name: str, diff_val: float):
    ref = Image.open(ref_path).convert("RGB").resize((800, 450), Image.Resampling.LANCZOS)
    wpf = Image.open(wpf_path).convert("RGB").resize((800, 450), Image.Resampling.LANCZOS)

    canvas = Image.new("RGB", (16 + 800 + 16 + 800 + 16, 64 + 450 + 36), (16, 17, 19))
    draw = ImageDraw.Draw(canvas)

    # Title header
    draw.text((20, 16), f"Nexus Theme Review · {title} · Mean RGB Diff: {diff_val:.2f}", font=f_head, fill=(255, 255, 255))

    # Side-by-side images
    canvas.paste(ref, (16, 60))
    canvas.paste(wpf, (16 + 800 + 16, 60))

    # Sub-labels
    draw.text((20, 60 + 450 + 8), "Approved Concept Reference (1600x900)", font=f_sub, fill=(108, 198, 68))
    draw.text((16 + 800 + 20, 60 + 450 + 8), "WPF Native Output (--review-themes 1600x900)", font=f_sub, fill=(108, 198, 68))

    out_file = BRAIN_DIR / f"nexus-sbs-{out_name}.png"
    canvas.save(out_file)
    return out_file

def main():
    print(f"{'#':<3} | {'Screen / Board':<42} | {'Concept Reference':<16} | {'WPF Capture':<22} | {'Mean RGB Diff':<13} | {'Status'}")
    print("-" * 115)

    diffs = []
    results = []

    for idx, (ref_name, wpf_name, title) in enumerate(PAIRS, 1):
        ref_p = REF_DIR / ref_name
        wpf_p = WPF_DIR / wpf_name

        if not ref_p.exists():
            print(f"{idx:<3} | {title:<42} | Missing Ref: {ref_name}")
            continue
        if not wpf_p.exists():
            print(f"{idx:<3} | {title:<42} | Missing WPF: {wpf_name}")
            continue

        diff_val = compute_diff(ref_p, wpf_p)
        diffs.append(diff_val)

        stem = ref_name.replace(".png", "")
        sbs_path = make_sbs(ref_p, wpf_p, title, stem, diff_val)

        status = "EXCELLENT" if diff_val < 18 else ("GOOD" if diff_val < 32 else "ACCEPTABLE")
        print(f"{idx:<3} | {title:<42} | {ref_name:<16} | {wpf_name:<22} | {diff_val:>11.2f}   | {status}")
        results.append((idx, title, ref_name, wpf_name, diff_val, status, sbs_path.name))

    print("-" * 115)
    mean_all = float(np.mean(diffs)) if diffs else 0
    print(f"OVERALL MEAN RGB DIFFERENCE ACROSS ALL 23 NEXUS BOARDS: {mean_all:.2f}")

if __name__ == "__main__":
    main()
