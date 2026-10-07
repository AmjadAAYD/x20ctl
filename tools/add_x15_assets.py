"""Register measured X15 image coordinates. Does not edit source photographs."""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "src/assets/controllers"
GENERATED = Path(r"C:\Users\amjad\.codex\generated_images\01a0f523-969d-71f1-a640-4b821af7520d")


def write(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")


def polygon(points):
    xs, ys = zip(*points)
    return {"bounds": [min(xs), min(ys), max(xs)-min(xs), max(ys)-min(ys)],
            "path": "M" + " L".join(f"{x} {y}" for x, y in points) + " Z"}


def ellipse(x, y, rx, ry):
    return {"bounds": [x-rx, y-ry, rx*2, ry*2], "path": f"M{x-rx} {y} A{rx} {ry} 0 1 0 {x+rx} {y} A{rx} {ry} 0 1 0 {x-rx} {y} Z"}


def main():
    folder = ASSETS / "x15"
    folder.mkdir(exist_ok=True)
    files = {"controller.png": "exec-50b08ad4-e960-4208-bdd6-3d0b0c67dc7d.png",
             "controller-base.png": "exec-dcb06e84-50b9-4305-9869-367a5b86bedb.png",
             "controller-rear.png": "exec-476d86bf-d5bb-4e4e-ad6c-df27c240efcf.png"}
    for name, source in files.items():
        if not (folder / name).exists():
            shutil.copyfile(GENERATED / source, folder / name)
    front = {
        "Y": ellipse(1101, 252, 43, 42), "X": ellipse(1014, 331, 44, 41),
        "B": ellipse(1182, 332, 42, 42), "A": ellipse(1099, 414, 45, 44),
        "SELECT": polygon([(670,294),(758,294),(756,316),(730,332),(697,326),(679,311)]),
        "START": polygon([(773,292),(863,292),(860,307),(837,326),(806,330),(776,316)]),
        "DPAD_UP": polygon([(566,438),(626,438),(626,479),(614,492),(578,492),(566,479)]),
        "DPAD_DOWN": polygon([(578,542),(614,542),(626,555),(626,598),(566,598),(566,555)]),
        "DPAD_LEFT": polygon([(518,488),(557,488),(574,500),(574,536),(557,548),(518,548)]),
        "DPAD_RIGHT": polygon([(622,500),(638,488),(676,488),(676,548),(638,548),(622,536)]),
    }
    trigger = [(313,162),(313,140),(329,103),(350,79),(385,69),(419,69),(448,91),(463,119),(475,143),(451,139),(420,144),(388,157),(350,170)]
    mirror = lambda points: [(1536-x,y) for x,y in points]
    back = {
        "RT": polygon(trigger), "LT": polygon(mirror(trigger)),
        "RB": polygon([(482,146),(507,143),(592,143),(597,148),(597,164),(500,168),(483,159)]),
        "LB": polygon(mirror([(482,146),(507,143),(592,143),(597,148),(597,164),(500,168),(483,159)])),
    }
    paddles = {
        "M2": [(418,510),(543,499),(555,505),(585,583),(582,596),(449,624),(436,618),(405,536),(407,521)],
        "M1": [(994,500),(1118,510),(1130,521),(1129,536),(1098,617),(1086,623),(952,596),(949,583),(981,505)],
    }
    shapes_path = ASSETS / "control-shapes.json"
    shapes = json.loads(shapes_path.read_text())
    shapes["models"]["x15"] = {"front": front, "back": back, "macros": {key: polygon(points) for key, points in paddles.items()}}
    write(shapes_path, shapes)
    write(folder / "rear-geometry.json", {"asset": "controller-rear.png", "aspect": 1.5, "viewLabel": "Back view", "controls": [
        {"slot": key, "printedLabel": True, "points": [{"x": x/1536, "y": y/1024} for x,y in points]} for key,points in paddles.items()]})
    front_shell = "M594 95 L602 119 L929 119 L937 95 L1012 95 Q1134 92 1185 145 Q1220 172 1244 219 C1291 307 1328 443 1353 569 C1382 697 1401 822 1372 890 Q1354 935 1307 958 Q1268 969 1239 931 L1099 770 Q1067 719 1004 704 L532 704 Q468 719 435 770 L294 931 Q265 969 226 958 Q179 935 161 890 C132 822 150 697 179 569 C205 443 244 307 289 219 Q313 172 350 145 Q398 92 519 95 Z"
    back_shell = "M478 144 Q462 112 444 88 Q414 56 374 71 Q333 79 313 122 L303 174 Q275 190 252 229 C208 311 164 430 115 552 C74 655 44 748 60 822 Q75 920 137 943 Q200 966 257 912 L438 767 Q470 746 520 746 L1016 746 Q1066 746 1098 767 L1279 912 Q1336 966 1399 943 Q1461 920 1476 822 C1492 748 1462 655 1421 552 C1372 430 1328 311 1284 229 Q1261 190 1233 174 L1223 122 Q1203 79 1162 71 Q1122 56 1092 88 Q1074 112 1058 144 L828 144 L827 127 L704 127 L703 144 Z"
    lighting_path = ASSETS / "lighting.json"
    lighting = json.loads(lighting_path.read_text())
    lighting["models"]["x15"] = {"front": front_shell, "back": back_shell}
    write(lighting_path, lighting)
    framing_path = ASSETS / "framing.json"
    framing = json.loads(framing_path.read_text())
    framing["models"]["x15"] = {"front": [128, 93, 1260, 868], "back": [56, 67, 1424, 884]}
    write(framing_path, framing)
    write(folder / "haptics-geometry.json", {"viewBox": [0,0,1536,1024], "contours": {
        "left_grip": "M194 493 Q254 566 326 578 L418 744 Q395 783 360 823 L277 922 Q248 951 215 927 C146 876 143 824 153 747 Q164 613 194 493 Z",
        "right_grip": "M1342 493 Q1282 566 1210 578 L1118 744 Q1141 783 1176 823 L1259 922 Q1288 951 1321 927 C1390 876 1393 824 1383 747 Q1372 613 1342 493 Z"}})
    buttons = {key: {"x": (shape["bounds"][0]+shape["bounds"][2]/2)/1536, "y": (shape["bounds"][1]+shape["bounds"][3]/2)/1024} for key,shape in front.items()}
    visual = {"asset": "x15/controller.png", "baseAsset": "x15/controller-base.png", "aspect": 1.5, "viewLabel": "Front view",
        "sticks": {
            "left": {"x": 437/1536, "y": 326/1024, "radius": 91/1536, "cap": {"x": 435/1536, "y": 301/1024, "w": 128/1536, "h": 128/1024}},
            "right": {"x": 932/1536, "y": 505/1024, "radius": 89/1536, "cap": {"x": 933/1536, "y": 490/1024, "w": 126/1536, "h": 126/1024}}},
        "buttons": buttons, "motors": [{"id": "left_grip", "x": .18, "y": .75, "kind": "grip"}, {"id": "right_grip", "x": .82, "y": .75, "kind": "grip"}],
        "rgb": [{"x": 597/1536, "y": 520/1024, "w": 210/1536, "h": 215/1024}], "screen": None}
    write(folder / "geometry.json", visual)
    catalog_path = ROOT / "x20ctl/controllers/catalog.json"
    catalog = json.loads(catalog_path.read_text())
    profile = {"id": "x15", "name": "X15", "macroSlots": ["M1", "M2"], "backend": None, "visible": True,
        "placeholder": True, "softwareControl": "unverified", "connectionModes": ["USB", "2.4 GHz", "Bluetooth"], "macroPlacement": "rear",
        "sources": ["https://www.easysmx.com/products/easysmx-x15-pc-controller-with-rgb-light-and-hall-joysticks", "https://www.easysmx.com/pages/support-about-easysmx-x15-controller"],
        "hardware": {"rgb": True, "vibration": True, "triggerHaptics": False, "motion": None, "display": False, "sticks": "Hall", "triggers": "Hall"}, "visual": visual}
    catalog = [item for item in catalog if item["id"] != "x15"] + [profile]
    write(catalog_path, catalog)
    print("Registered X15 with measured front/rear controls and no hardware backend")


if __name__ == "__main__":
    main()
