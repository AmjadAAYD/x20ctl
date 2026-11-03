import os
from PIL import Image, ImageDraw, ImageFont

ARTIFACT_DIR = r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23"
CONCEPT_DIR = r"previews\console-concepts\themes\out\dusk"
WPF_DIR = r"artifacts\dusk"

pairs = [
    ("Dashboard (Steam Deck Dark Home)", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview (Players & Docks)", "zone.png", "1-zone.png"),
    ("Hero View (Focused Controller)", "hero.png", "2b-hero.png"),
    ("Buttons remap & live pad visual", "buttons.png", "4-buttons.png"),
    ("Curves Workbench & S-curve graph", "curves.png", "5-curves.png"),
    ("Macros timeline & execution blocks", "macros.png", "6-macros.png"),
    ("Vibration & dual motor haptics", "vibration.png", "7-vibration.png"),
    ("PC Mode & signal routing", "pcmode.png", "8b-pcmode.png"),
    ("Setups library & loaded profile", "setups.png", "8d-setups.png"),
    ("Input Tester & latency gauges", "tester.png", "8e-tester.png"),
    ("Diagnostics & stick reticles", "diagnostics.png", "8f-diagnostics.png"),
    ("Tools Hub & maintenance", "hub.png", "9-tools.png"),
    ("Appearance & themes selector", "tools.png", "10-themes.png"),
    ("System Notice & Deck Modal", "notices.png", "13-notice-restart.png"),
]

# 1. Copy individual rendered images into artifact dir
for title, concept_file, wpf_file in pairs:
    src_wpf = os.path.join(WPF_DIR, wpf_file)
    if os.path.exists(src_wpf):
        base_name = f"dusk-{concept_file.replace('.png', '')}.png"
        dst = os.path.join(ARTIFACT_DIR, base_name)
        img = Image.open(src_wpf)
        img.save(dst)
        print(f"Copied {wpf_file} -> {dst}")

# 2. Build multi-row Side-by-side comparison for Dashboard, Zone, Hero, Buttons, Curves, Setups
hero_pairs = [
    ("Dashboard — Steam Deck Dark Home", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview — 2x2 Square Quadrant Bays", "zone.png", "2-zone-player.png"),
    ("Focused Hero View — X05 Pro", "hero.png", "2b-hero.png"),
    ("Buttons — Remap & Pad Callout", "buttons.png", "4-buttons.png"),
    ("Curves — Linear & S-Curve Graph", "curves.png", "5-curves.png"),
    ("Setups — Profiles & Action Band", "setups.png", "8d-setups.png"),
]

row_w = 1600
row_h = 460
header_h = 70
gap = 20
total_h = len(hero_pairs) * (row_h + header_h + gap) + 120
comp_img = Image.new("RGB", (row_w * 2 + gap * 3, total_h), (14, 18, 24))
draw = ImageDraw.Draw(comp_img)

try:
    font_title = ImageFont.truetype("arialbd.ttf", 36)
    font_sub = ImageFont.truetype("arial.ttf", 24)
    font_tag = ImageFont.truetype("arialbd.ttf", 26)
except Exception:
    font_title = font_sub = font_tag = None

draw.text((gap, 30), "Dusk Theme: Concept Specification vs WPF Implementation", fill=(255, 255, 255), font=font_title)
draw.text((gap, 75), "Authentic Steam Deck dark console home — Complete ground-up rebuild", fill=(139, 146, 154), font=font_sub)

y = 130
for title, concept_file, wpf_file in hero_pairs:
    concept_path = os.path.join(CONCEPT_DIR, concept_file)
    wpf_path = os.path.join(WPF_DIR, wpf_file)

    # Header
    draw.rectangle([gap, y, row_w * 2 + gap * 2, y + 44], fill=(27, 33, 41))
    draw.text((gap + 16, y + 8), title, fill=(255, 255, 255), font=font_tag)
    y += 54

    # Concept image
    if os.path.exists(concept_path):
        c_img = Image.open(concept_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(c_img, (gap, y))
        # Label overlay
        draw.rectangle([gap + 10, y + 10, gap + 280, y + 50], fill=(0, 0, 0, 200))
        draw.text((gap + 20, y + 16), "CONCEPT SPECIFICATION", fill=(89, 191, 64), font=font_tag)

    # WPF image
    if os.path.exists(wpf_path):
        w_img = Image.open(wpf_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(w_img, (row_w + gap * 2, y))
        # Label overlay
        draw.rectangle([row_w + gap * 2 + 10, y + 10, row_w + gap * 2 + 320, y + 50], fill=(0, 0, 0, 200))
        draw.text((row_w + gap * 2 + 20, y + 16), "WPF IMPLEMENTATION", fill=(26, 159, 255), font=font_tag)

    y += row_h + gap

comp_dst = os.path.join(ARTIFACT_DIR, "dusk-comparison.png")
comp_img.save(comp_dst, quality=95)
print(f"Generated comparison: {comp_dst}")

# 3. Build 3x4 showcase grid
grid_cols = 3
grid_rows = 4
cell_w = 533
cell_h = 300
grid_w = grid_cols * cell_w + (grid_cols + 1) * gap
grid_h = grid_rows * cell_h + (grid_rows + 1) * gap + 100
grid_img = Image.new("RGB", (grid_w, grid_h), (14, 18, 24))
draw_g = ImageDraw.Draw(grid_img)

draw_g.text((gap, 25), "Dusk Theme (Steam Deck Dark) — All Scenes Showcase", fill=(255, 255, 255), font=font_title)
draw_g.text((gap, 65), "WPF Live Rendered Review Shots (1600x900 native)", fill=(89, 191, 64), font=font_sub)

grid_shots = [
    ("Dashboard", "3-dashboard.png"),
    ("Zone (2x2 Grid)", "2-zone-player.png"),
    ("Hero View", "2b-hero.png"),
    ("Buttons", "4-buttons.png"),
    ("Stick Curves", "5-curves.png"),
    ("Macros Timeline", "6-macros.png"),
    ("Vibration Haptics", "7-vibration.png"),
    ("PC Mode Routing", "8b-pcmode.png"),
    ("Device Telemetry", "8-device.png"),
    ("Setups Library", "8d-setups.png"),
    ("Input Tester", "8e-tester.png"),
    ("System Notice", "13-notice-restart.png"),
]

for idx, (label, wpf_file) in enumerate(grid_shots):
    r = idx // grid_cols
    c = idx % grid_cols
    gx = gap + c * (cell_w + gap)
    gy = 100 + r * (cell_h + gap)

    wpf_path = os.path.join(WPF_DIR, wpf_file)
    if os.path.exists(wpf_path):
        cell_img = Image.open(wpf_path).resize((cell_w, cell_h), Image.Resampling.LANCZOS)
        grid_img.paste(cell_img, (gx, gy))

    draw_g.rectangle([gx, gy + cell_h - 36, gx + cell_w, gy + cell_h], fill=(14, 18, 24, 220))
    draw_g.text((gx + 12, gy + cell_h - 30), label, fill=(255, 255, 255), font=font_sub)

grid_dst = os.path.join(ARTIFACT_DIR, "dusk-showcase-grid.png")
grid_img.save(grid_dst, quality=95)
print(f"Generated showcase grid: {grid_dst}")
