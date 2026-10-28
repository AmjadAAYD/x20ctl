import os
from PIL import Image, ImageDraw, ImageFont

ARTIFACT_DIR = r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23"
CONCEPT_DIR = r"previews\console-concepts\themes\out\blossom"
WPF_DIR = r"artifacts\blossom"

pairs = [
    ("Dashboard (Grand Hanami Dashboard)", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview (Festival Lantern Wire)", "zone.png", "1-zone.png"),
    ("Hero View (Giant Full Moon & Canopy)", "hero.png", "2b-hero.png"),
    ("Buttons Remap (Shoji Lattice & Hanko Seals)", "buttons.png", "4-buttons.png"),
    ("Curves Workbench (Fire Beads & Lit Silk Cords)", "curves.png", "5-curves.png"),
    ("Macros Timeline (Floating River Lanterns)", "macros.png", "6-macros.png"),
    ("Vibration & Haptics (Still Pond Moon Ripples)", "vibration.png", "7-vibration.png"),
    ("PC Mode (Sacred Path with Stone Tōrō)", "pcmode.png", "8b-pcmode.png"),
    ("Device Info (Hanging Lantern & Tea Stall)", "device.png", "8-device.png"),
    ("Diagnostics (Celestial Moon Dials)", "diagnostics.png", "8f-diagnostics.png"),
    ("Mouse Mapping (Shrine Wall of Lanterns)", "mouse.png", "8c-mouse.png"),
    ("Setups Library (Festival Fans Uchiwa)", "setups.png", "8d-setups.png"),
    ("Input Tester (Ring of 18 Paper Lanterns)", "tester.png", "8e-tester.png"),
    ("Tools Hub (Yatai Festival Stalls)", "hub.png", "9-tools.png"),
    ("Themes Pavilion (Sliding Shoji Windows)", "tools.png", "10-themes.png"),
    ("About Scroll (Washi Scroll Under Moon)", "about.png", "11-about.png"),
    ("Feedback Shrine (Wooden Ema Plaque)", "feedback.png", "11b-feedback.png"),
]

for title, concept_file, wpf_file in pairs:
    src_wpf = os.path.join(WPF_DIR, wpf_file)
    if os.path.exists(src_wpf):
        base_name = f"blossom-{concept_file.replace('.png', '')}.png"
        dst = os.path.join(ARTIFACT_DIR, base_name)
        img = Image.open(src_wpf)
        img.save(dst)
        print(f"Saved {dst}")

hero_pairs = [
    ("Dashboard — Grand Hanami Under Full Moon & Cherry Canopy", "dashboard.png", "3-dashboard.png"),
    ("Zone Overview — Curved Festival Lantern Wire & 4 Bays", "zone.png", "1-zone.png"),
    ("Hero View — Massive 300px Full Moon & Washi Shoji Grid", "hero.png", "2b-hero.png"),
    ("Buttons — Shoji Lattice Mapping Rows & Samurai Hanko Seals", "buttons.png", "4-buttons.png"),
    ("Curves — Festive String of Fire Beads & Lit Silk Cord Sliders", "curves.png", "5-curves.png"),
    ("Macros — Dark River Sequencer with Floating Paper Lanterns", "macros.png", "6-macros.png"),
    ("Vibration — Night Pond Moonlight Ripples & Dual Haptic Motors", "vibration.png", "7-vibration.png"),
    ("PC Mode — Sacred Path of Carved Stone Tōrō Lanterns", "pcmode.png", "8b-pcmode.png"),
]

row_w = 1600
row_h = 460
header_h = 70
gap = 20
total_h = len(hero_pairs) * (row_h + header_h + gap) + 120
comp_img = Image.new("RGB", (row_w * 2 + gap * 3, total_h), (8, 4, 10))
draw = ImageDraw.Draw(comp_img)

try:
    font_title = ImageFont.truetype("arialbd.ttf", 36)
    font_sub = ImageFont.truetype("arial.ttf", 24)
    font_tag = ImageFont.truetype("arialbd.ttf", 26)
except Exception:
    font_title = font_sub = font_tag = None

draw.text((gap, 30), "Yozakura (Blossom) Theme: Concept Specification vs WPF Implementation", fill=(246, 233, 236), font=font_title)
draw.text((gap, 75), "Night Hanami aesthetic: Paper lanterns, weeping cherry canopy, full moon, still pond ripples, stone lanterns, samurai seals", fill=(244, 166, 189), font=font_sub)

y = 130
for title, concept_file, wpf_file in hero_pairs:
    concept_path = os.path.join(CONCEPT_DIR, concept_file)
    wpf_path = os.path.join(WPF_DIR, wpf_file)

    draw.rectangle([gap, y, row_w * 2 + gap * 2, y + 44], fill=(24, 12, 22))
    draw.text((gap + 16, y + 8), title, fill=(246, 233, 236), font=font_tag)
    y += 54

    if os.path.exists(concept_path):
        c_img = Image.open(concept_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(c_img, (gap, y))
        draw.rectangle([gap + 10, y + 10, gap + 300, y + 50], fill=(0, 0, 0, 200))
        draw.text((gap + 20, y + 16), "CONCEPT SPECIFICATION", fill=(244, 166, 189), font=font_tag)

    if os.path.exists(wpf_path):
        w_img = Image.open(wpf_path).resize((row_w, row_h), Image.Resampling.LANCZOS)
        comp_img.paste(w_img, (row_w + gap * 2, y))
        draw.rectangle([row_w + gap * 2 + 10, y + 10, row_w + gap * 2 + 320, y + 50], fill=(0, 0, 0, 200))
        draw.text((row_w + gap * 2 + 20, y + 16), "WPF IMPLEMENTATION", fill=(255, 211, 168), font=font_tag)

    y += row_h + gap

comp_dst = os.path.join(ARTIFACT_DIR, "blossom-comparison.png")
comp_img.save(comp_dst, quality=95)
print(f"Generated comparison: {comp_dst}")

grid_cols = 3
grid_rows = 4
cell_w = 533
cell_h = 300
grid_w = grid_cols * cell_w + (grid_cols + 1) * gap
grid_h = grid_rows * cell_h + (grid_rows + 1) * gap + 100

grid_img = Image.new("RGB", (grid_w, grid_h), (8, 4, 10))
draw_grid = ImageDraw.Draw(grid_img)
draw_grid.text((gap, 25), "Yozakura (Blossom) Theme: Complete Visual Showcase (12 Key Scenes)", fill=(246, 233, 236), font=font_title)
draw_grid.text((gap, 68), "Japanese Samurai Hanami: Glowing lanterns, weeping cherry canopy, still pond moonlight ripples, carved stone lanterns", fill=(244, 166, 189), font=font_sub)

showcase_cells = [
    ("Dashboard", "3-dashboard.png"),
    ("Zone Overview", "1-zone.png"),
    ("Hero Focused View", "2b-hero.png"),
    ("Buttons Mapping", "4-buttons.png"),
    ("Curves Workbench", "5-curves.png"),
    ("Macros River", "6-macros.png"),
    ("Vibration Night Pond", "7-vibration.png"),
    ("PC Mode Sacred Path", "8b-pcmode.png"),
    ("Device Overhead Lantern", "8-device.png"),
    ("Diagnostics Moon Plots", "8f-diagnostics.png"),
    ("Setups Festival Fans", "8d-setups.png"),
    ("Tools Festival Stalls", "9-tools.png")
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
        draw_grid.rectangle([x, y + cell_h - 36, x + cell_w, y + cell_h], fill=(16, 8, 18, 220))
        draw_grid.text((x + 12, y + cell_h - 28), label, fill=(246, 233, 236), font=font_sub)

grid_dst = os.path.join(ARTIFACT_DIR, "blossom-showcase-grid.png")
grid_img.save(grid_dst, quality=95)
print(f"Generated showcase grid: {grid_dst}")
