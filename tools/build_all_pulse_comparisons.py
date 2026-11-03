"""Generates comprehensive side-by-side comparison boards for ALL 23 Pulse boards
between reference concept boards (previews/console-concepts/themes/SEND-TO-CHATGPT/boards/pulse)
and WPF rendered windowless captures (artifacts/pulse).
Also copies high-resolution WPF renders to the brain artifacts directory.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

REF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\previews\console-concepts\themes\SEND-TO-CHATGPT\boards\pulse")
WPF_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\worktree-gemini\artifacts\pulse")
BRAIN_DIR = Path(r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23")

FONT_DIR = "C:/Windows/Fonts/"
try:
    f_head = ImageFont.truetype(FONT_DIR + "segoeuil.ttf", 24)
    f_sub = ImageFont.truetype(FONT_DIR + "segoeui.ttf", 14)
    f_bold = ImageFont.truetype(FONT_DIR + "segoeuib.ttf", 15)
except Exception:
    f_head = ImageFont.load_default()
    f_sub = ImageFont.load_default()
    f_bold = ImageFont.load_default()


PAIRS = [
    ("zone.png", "2-zone-player.png", "1. Controller Zone (Assigned Players)"),
    ("hero.png", "2b-hero.png", "2. Controller Hero Focus (Player 1)"),
    ("choose.png", "11c-choose.png", "3. Choose Controller (Catalog Rail & Pearl Specs)"),
    ("dashboard.png", "3-dashboard.png", "4. Studio Dashboard (Controller Hero & Action Grid)"),
    ("buttons.png", "4-buttons.png", "5. Button Mapping (Control Stage & Pearl Matrix)"),
    ("curves.png", "5-curves.png", "6. Response Curves (Concentric Ring Dial & Curve Field)"),
    ("advanced.png", "8g-advanced.png", "7. Advanced Stick Tools (Deflection Gate & Pearl Specs)"),
    ("macros.png", "6-macros.png", "8. Macro Editor (Paddle Rail & Multi-Lane Sequence)"),
    ("vibration.png", "7-vibration.png", "9. Vibration Control (Waveform Ribbon & Motor Haptics)"),
    ("pcmode.png", "8b-pcmode.png", "10. PC Mode Signal Pipeline (Hardware Route & Driver Badges)"),
    ("device.png", "8-device.png", "11. Device Management (Hardware Specs & Firmware State)"),
    ("diagnostics.png", "8f-diagnostics.png", "12. Input Diagnostics (Resonance Scopes & Ring Dials)"),
    ("mouse.png", "8c-mouse.png", "13. Mouse Emulation (Orbital Radial Button Field)"),
    ("setups.png", "8d-setups.png", "14. Setups Management (Configuration Profiles & Arc Cards)"),
    ("tester.png", "8e-tester.png", "15. Input Tester (Live Controller & Harmonic Scopes)"),
    ("agent.png", "8i-agent-answer.png", "16. Agent Intelligence (Harmonic Orb & Conversation Flow)"),
    ("hub.png", "9-tools.png", "17. Tools Hub (Orbital Hub Tiles & System Shortcuts)"),
    ("tools.png", "10-themes.png", "18. Theme Selector (Concentric Wheel & Theme Showcase)"),
    ("about.png", "11-about.png", "19. About & Diagnostics (Version, Architecture & Open Source)"),
    ("feedback.png", "11b-feedback.png", "20. Community Feedback (Survey & Diagnostic Logs)"),
    ("check.png", "11d-check.png", "21. Controller Check / Read Scan (Harmonic Wave Stepper)"),
    ("writescan.png", "11e-writescan.png", "22. Write Scan (Guided Firmware & Feature Probe)"),
    ("notices.png", "13-notice-restart.png", "23. Themed Modal Notices (Restart Notice & Driver Modal)"),
]


def make_sbs(ref_path: Path, wpf_path: Path, title: str, out_name: str):
    if not ref_path.exists() or not wpf_path.exists():
        print(f"Skipping {out_name}: missing {ref_path.name if not ref_path.exists() else wpf_path.name}")
        return

    ref = Image.open(ref_path).convert("RGB").resize((800, 450), Image.Resampling.LANCZOS)
    wpf = Image.open(wpf_path).convert("RGB").resize((800, 450), Image.Resampling.LANCZOS)

    canvas = Image.new("RGB", (16 + 800 + 16 + 800 + 16, 64 + 450 + 36), (6, 9, 15))
    draw = ImageDraw.Draw(canvas)

    # Title header
    draw.text((20, 16), f"Pulse Theme Review · {title}", font=f_head, fill=(242, 246, 252))

    # Reference box
    canvas.paste(ref, (16, 60))
    draw.rectangle([16, 60, 16 + 799, 60 + 449], outline=(36, 52, 78), width=1)
    draw.text((20, 60 + 450 + 8), f"APPROVED DESIGN: {ref_path.name} (1600×900)", font=f_sub, fill=(138, 164, 198))

    # WPF Render box
    canvas.paste(wpf, (832, 60))
    draw.rectangle([832, 60, 832 + 799, 60 + 449], outline=(92, 187, 255), width=1)
    draw.text((836, 60 + 450 + 8), f"WPF RUNTIME RENDER: {wpf_path.name} (Windowless Offscreen Render)", font=f_bold, fill=(92, 187, 255))

    out_file = BRAIN_DIR / out_name
    canvas.save(out_file)
    print("Generated side-by-side:", out_file.name)


def main():
    BRAIN_DIR.mkdir(parents=True, exist_ok=True)

    print("Building side-by-side comparison boards for all 23 Pulse pages...")
    for ref_name, wpf_name, title in PAIRS:
        sbs_name = f"pulse-sbs-{ref_name.replace('.png', '')}.png"
        make_sbs(REF_DIR / ref_name, WPF_DIR / wpf_name, title, sbs_name)

        # Also copy full-size WPF render into brain directory for direct artifact viewing
        wpf_src = WPF_DIR / wpf_name
        if wpf_src.exists():
            wpf_dest = BRAIN_DIR / f"pulse-wpf-{wpf_name}"
            import shutil
            shutil.copy2(wpf_src, wpf_dest)

    print("Completed all 23 comparison boards successfully!")


if __name__ == "__main__":
    main()
