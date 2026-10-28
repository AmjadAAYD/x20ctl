import os
from PIL import Image, ImageDraw, ImageFont

ARTIFACT_DIR = r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23"
CONCEPT_DIR = r"previews\console-concepts\themes\out\rebel"
WPF_DIR = r"app-console-themes-current\review\rebel-full-final-20261027"

pairs = [
    ("Dashboard (Phantom Thieves Hideout)", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview (Wanted Posters Wall)", "zone.png", "1-zone.png"),
    ("Hero View (Battle Command Cluster)", "hero.png", "2b-hero.png"),
    ("Buttons Remap (Calling Card Styling)", "buttons.png", "4-buttons.png"),
    ("Curves Workbench (Angular Slanted Graph)", "curves.png", "5-curves.png"),
    ("Macros Timeline (Comic Strip Layout)", "macros.png", "6-macros.png"),
    ("Vibration & Haptics (Comic Action Burst)", "vibration.png", "7-vibration.png"),
    ("PC Mode (Heist Map Strategy)", "pcmode.png", "8b-pcmode.png"),
    ("Setups Library (Calling Cards Spread)", "setups.png", "8d-setups.png"),
    ("Input Tester (Comic Impact Burst)", "tester.png", "8e-tester.png"),
    ("Diagnostics (Halftone Telemetry)", "diagnostics.png", "8f-diagnostics.png"),
    ("Tools Hub (Planning Board with Red String)", "hub.png", "9-tools.png"),
    ("Themes (Calling Card Showcase)", "tools.png", "10-themes.png"),
    ("System Notices (Calling Card Alert)", "notices.png", "13-restart-notice.png"),
]

for title, concept_file, wpf_file in pairs:
    src_wpf = os.path.join(WPF_DIR, wpf_file)
    if os.path.exists(src_wpf):
        base_name = f"rebel-{concept_file.replace('.png', '')}.png"
        dst = os.path.join(ARTIFACT_DIR, base_name)
        img = Image.open(src_wpf)
        img.save(dst)
        print(f"Saved {dst}")

hero_pairs = [
    ("Dashboard — Phantom Thieves Hideout", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview — Wanted Posters Wall", "zone.png", "2-zone-player.png"),
    ("Hero View — Battle Command Proposal", "hero.png", "2b-hero.png"),
    ("Buttons — Calling Card & Pad Mapping", "buttons.png", "4-buttons.png"),
    ("Curves — Slanted Axis & Response Graph", "curves.png", "5-curves.png"),
    ("Macros — Comic Strip Sequence", "macros.png", "6-macros.png"),
]

row_w = 1600
row_h = 460
header_h = 70
gap = 20
total_h = len(hero_pairs) * (row_h + header_h + gap) + 120
comp_img = Image.new("RGB", (row_w * 2 + gap * 3, total_h), (11, 10, 12))
draw = ImageDraw.Draw(comp_img)

try:
    font_title = ImageFont.truetype("arialbd.ttf", 36)
    font_sub = ImageFont.truetype("arial.ttf", 24)
    font_tag = ImageFont.truetype("arialbd.ttf", 26)
except Exception:
    font_title = font_sub = font_tag = None

draw.text((gap, 30), "Rebel Theme: Concept Specification vs WPF Implementation", fill=(247, 243, 238), font=font_title)
draw.text((gap, 75), "Persona 5 style: Black, bone, crimson red, ransom note typography and skewed slabs", fill=(163, 155, 158), font=font_sub)

y = 130
for title, concept_file, wpf_file in hero_pairs:
    concept_path = os.path.join(CONCEPT_DIR, concept_file)
    wpf_path = os.path.join(WPF_DIR, wpf_file)

    draw.rectangle([gap, y, row_w * 2 + gap * 2, y + 44], fill=(22, 20, 23))
    draw.text((gap + 16, y + 8), title, fill=(247, 243, 238), font=font_tag)
    y += 54

    if os.path.exists(concept_path):
        c_img = Image.open(concept_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(c_img, (gap, y))
        draw.rectangle([gap + 10, y + 10, gap + 300, y + 50], fill=(0, 0, 0, 200))
        draw.text((gap + 20, y + 16), "CONCEPT SPECIFICATION", fill=(227, 18, 45), font=font_tag)

    if os.path.exists(wpf_path):
        w_img = Image.open(wpf_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(w_img, (row_w + gap * 2, y))
        draw.rectangle([row_w + gap * 2 + 10, y + 10, row_w + gap * 2 + 320, y + 50], fill=(0, 0, 0, 200))
        draw.text((row_w + gap * 2 + 20, y + 16), "WPF IMPLEMENTATION", fill=(255, 255, 255), font=font_tag)

    y += row_h + gap

comp_dst = os.path.join(ARTIFACT_DIR, "rebel-comparison.png")
comp_img.save(comp_dst, quality=95)
print(f"Generated comparison: {comp_dst}")

grid_cols = 3
grid_rows = 4
cell_w = 533
cell_h = 300
grid_w = grid_cols * cell_w + (grid_cols + 1) * gap
grid_h = grid_rows * cell_h + (grid_rows + 1) * gap + 100

grid_img = Image.new("RGB", (grid_w, grid_h), (11, 10, 12))
draw_grid = ImageDraw.Draw(grid_img)
draw_grid.text((gap, 25), "Rebel Theme: Complete Visual Showcase (12 Key Scenes)", fill=(247, 243, 238), font=font_title)
draw_grid.text((gap, 68), "Persona 5 aesthetic: Ransom letters, comic strip layouts, calling cards, wanted posters wall", fill=(227, 18, 45), font=font_sub)

showcase_cells = [
    ("Dashboard", "3-dashboard.png"),
    ("Zone Overview", "1-zone.png"),
    ("Hero Focused View", "2b-hero.png"),
    ("Buttons Mapping", "4-buttons.png"),
    ("Curves Workbench", "5-curves.png"),
    ("Macros Comic Strip", "6-macros.png"),
    ("Vibration Action Burst", "7-vibration.png"),
    ("PC Mode Heist Map", "8b-pcmode.png"),
    ("Setups Calling Cards", "8d-setups.png"),
    ("Input Tester Burst", "8e-tester.png"),
    ("Halftone Diagnostics", "8f-diagnostics.png"),
    ("Tools Planning Board", "9-tools.png")
]

for idx, (label, wpf_file) in enumerate(showcase_cells):
    c = idx % grid_cols
    r = idx // grid_cols
    x = gap + c * (cell_w + gap)
    y = 110 + r * (cell_h + gap)

    wpf_path = os.path.join(WPF_DIR, wpf_file)
    if os.path.exists(wpf_path):
        cell_img = Image.open(wpf_path).resize((cell_w, cell_h), Image.Resampling.LANCZOS)
        grid_img.paste(cell_img, (x, y))
        draw_grid.rectangle([x, y + cell_h - 36, x + cell_w, y + cell_h], fill=(22, 20, 23, 220))
        draw_grid.text((x + 12, y + cell_h - 28), label, fill=(247, 243, 238), font=font_sub)

grid_dst = os.path.join(ARTIFACT_DIR, "rebel-showcase-grid.png")
grid_img.save(grid_dst, quality=95)
print(f"Generated showcase grid: {grid_dst}")
