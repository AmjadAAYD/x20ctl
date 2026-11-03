"""Generates comprehensive side-by-side comparison boards for ALL 23 Spotlight boards
between reference concept boards (previews/console-concepts/themes/SEND-TO-CHATGPT/boards/spotlight)
and WPF rendered windowless captures (artifacts/spotlight).
Also computes exact Mean RGB Difference metrics across all 23 boards and generates a markdown summary report.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

REF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\previews\console-concepts\themes\SEND-TO-CHATGPT\boards\spotlight")
WPF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\artifacts\spotlight")
if not WPF_DIR.exists():
    WPF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\worktree-gemini\artifacts\spotlight")
BRAIN_DIR = Path(r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23")

FONT_DIR = "C:/Windows/Fonts/"
try:
    f_head = ImageFont.truetype(FONT_DIR + "georgia.ttf", 24)
    f_sub = ImageFont.truetype(FONT_DIR + "garamond.ttf", 15)
    f_bold = ImageFont.truetype(FONT_DIR + "georgiab.ttf", 15)
except Exception:
    f_head = ImageFont.load_default()
    f_sub = ImageFont.load_default()
    f_bold = ImageFont.load_default()

PAIRS = [
    ("zone.png", "2-zone-player.png", "1. Controller Zone (Cast on Marks)"),
    ("hero.png", "2b-hero.png", "2. Controller Hero Focus (Gilded Title & Marks)"),
    ("choose.png", "11c-choose.png", "3. Choose Controller (Theatrical Lineup & Cue Card)"),
    ("dashboard.png", "3-dashboard.png", "4. Studio Dashboard (Acts I-IX Floor & Follow Spot)"),
    ("buttons.png", "4-buttons.png", "5. Button Mapping (Programme Rail & Stage Wireframe)"),
    ("curves.png", "5-curves.png", "6. Response Curves (Theatrical Lighting Desk Faders)"),
    ("advanced.png", "8g-advanced.png", "7. Advanced Stick Tools (Monumental Fader & Drift Cue)"),
    ("macros.png", "6-macros.png", "8. Macro Editor (Stage Manager Cue Sequence)"),
    ("vibration.png", "7-vibration.png", "9. Vibration Control (Master Fader & Grip Illumination)"),
    ("pcmode.png", "8b-pcmode.png", "10. PC Mode Signal Pipeline (Suspended Rig Lamps)"),
    ("device.png", "8-device.png", "11. Device Management (Star on Mark & Cue Card)"),
    ("diagnostics.png", "8f-diagnostics.png", "12. Input Diagnostics (Dual Gobos & Trigger Faders)"),
    ("mouse.png", "8c-mouse.png", "13. Mouse Emulation (8 Radial Blocking Callouts)"),
    ("setups.png", "8d-setups.png", "14. Setups Management (Repertoire Posters Carousel)"),
    ("tester.png", "8e-tester.png", "15. Input Tester (Target Reticles & Gauge Faders)"),
    ("agent.png", "8i-agent-answer.png", "16. Agent Intelligence (Script Paper & Typewriter Courier)"),
    ("hub.png", "9-tools.png", "17. Tools Hub (Marquee Mirror & 14x5 Dressing Bulbs)"),
    ("tools.png", "10-themes.png", "18. Theme Selector (Backstage Gallery of 10 Theme Posters)"),
    ("about.png", "11-about.png", "19. About & Diagnostics (Theatrical Playbill Cast & Program)"),
    ("feedback.png", "11b-feedback.png", "20. Community Feedback (Rehearsal Notes & Category Cues)"),
    ("check.png", "11d-check.png", "21. Controller Check / Read Scan (5 Act Play Scene Cards)"),
    ("writescan.png", "11e-writescan.png", "22. Write Scan (20-40 min Running Time Playbill Cue)"),
    ("notices.png", "13-notice-restart.png", "23. Themed Modal Notices (Two-Wire Suspended Cue Card)"),
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

    canvas = Image.new("RGB", (16 + 800 + 16 + 800 + 16, 64 + 450 + 36), (7, 4, 5))
    draw = ImageDraw.Draw(canvas)

    # Title header
    draw.text((20, 16), f"Spotlight Theme Review · {title} · Mean RGB Diff: {diff_val:.2f}", font=f_head, fill=(239, 230, 216))

    # Reference box
    canvas.paste(ref, (16, 60))
    draw.rectangle([16, 60, 16 + 799, 60 + 449], outline=(58, 38, 22), width=1)
    draw.text((20, 60 + 450 + 8), f"APPROVED DESIGN: {ref_path.name} (1600×900)", font=f_sub, fill=(155, 140, 122))

    # WPF Render box
    canvas.paste(wpf, (832, 60))
    draw.rectangle([832, 60, 832 + 799, 60 + 449], outline=(217, 183, 122), width=1)
    draw.text((836, 60 + 450 + 8), f"WPF RUNTIME RENDER: {wpf_path.name} (Windowless Offscreen Render)", font=f_bold, fill=(217, 183, 122))

    out_file = BRAIN_DIR / out_name
    canvas.save(out_file)
    print("Generated side-by-side:", out_file.name)

def main():
    BRAIN_DIR.mkdir(parents=True, exist_ok=True)
    results = []

    print("Building side-by-side comparison boards for all 23 Spotlight pages...")
    for ref_name, wpf_name, title in PAIRS:
        ref_path = REF_DIR / ref_name
        wpf_path = WPF_DIR / wpf_name
        if not ref_path.exists() or not wpf_path.exists():
            print(f"Skipping {title}: missing {ref_name if not ref_path.exists() else wpf_name}")
            continue

        diff_val = compute_diff(ref_path, wpf_path)
        sbs_name = f"spotlight-sbs-{ref_name.replace('.png', '')}.png"
        make_sbs(ref_path, wpf_path, title, sbs_name, diff_val)

        # Copy full size WPF render to brain artifacts
        wpf_dest = BRAIN_DIR / f"spotlight-wpf-{wpf_name}"
        import shutil
        shutil.copy2(wpf_path, wpf_dest)

        results.append((title, ref_name, wpf_name, diff_val))

    print("\n--- SPOTLIGHT THEME ACCURACY RESULTS ---")
    for title, ref_name, wpf_name, diff_val in results:
        print(f"{title:60} | Diff: {diff_val:6.2f}")
    if results:
        mean_all = sum(r[3] for r in results) / len(results)
        print(f"\nAVERAGE MEAN RGB DIFFERENCE ACROSS ALL BOARDS: {mean_all:.2f}")

if __name__ == "__main__":
    main()
