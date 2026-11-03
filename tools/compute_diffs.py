from pathlib import Path
from PIL import Image, ImageChops, ImageStat

wpf_dir = Path("worktree-gemini/artifacts/pulse")
ref_dir = Path("previews/console-concepts/themes/SEND-TO-CHATGPT/boards/pulse")

pairs = [
    ("zone.png", "2-zone-player.png"),
    ("hero.png", "2b-hero.png"),
    ("choose.png", "11c-choose.png"),
    ("dashboard.png", "3-dashboard.png"),
    ("buttons.png", "4-buttons.png"),
    ("curves.png", "5-curves.png"),
    ("advanced.png", "8g-advanced.png"),
    ("macros.png", "6-macros.png"),
    ("vibration.png", "7-vibration.png"),
    ("pcmode.png", "8b-pcmode.png"),
    ("device.png", "8-device.png"),
    ("diagnostics.png", "8f-diagnostics.png"),
    ("mouse.png", "8c-mouse.png"),
    ("setups.png", "8d-setups.png"),
    ("tester.png", "8e-tester.png"),
    ("agent.png", "8i-agent-answer.png"),
    ("hub.png", "9-tools.png"),
    ("tools.png", "10-themes.png"),
    ("about.png", "11-about.png"),
    ("feedback.png", "11b-feedback.png"),
    ("check.png", "11d-check.png"),
    ("writescan.png", "11e-writescan.png"),
    ("notices.png", "13-notice-restart.png"),
]

print(f"{'REF NAME':<16} | {'WPF FILE':<22} | {'EXISTS':<6} | {'MEAN RGB DIFF':<14}")
print("-" * 65)

for ref_f, wpf_f in pairs:
    rp = ref_dir / ref_f
    wp = wpf_dir / wpf_f
    if not rp.exists():
        print(f"{ref_f:<16} | {wpf_f:<22} | REF MISSING")
        continue
    if not wp.exists():
        print(f"{ref_f:<16} | {wpf_f:<22} | WPF MISSING")
        continue

    img_r = Image.open(rp).convert("RGB").resize((800, 450))
    img_w = Image.open(wp).convert("RGB").resize((800, 450))
    diff = ImageChops.difference(img_r, img_w)
    stat = ImageStat.Stat(diff)
    mean_diff = sum(stat.mean) / 3.0
    print(f"{ref_f:<16} | {wpf_f:<22} | OK     | {mean_diff:.2f}")
