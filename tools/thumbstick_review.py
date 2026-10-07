"""Focused native WebView2 review of photo layers; no hardware access."""
from __future__ import annotations

import argparse
import json
import sys
import time
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
parser = argparse.ArgumentParser()
parser.add_argument("--output", required=True)
args = parser.parse_args()
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=True)


def exercise(window, directory, result, lifecycle):
    report = {"passed": False, "checks": [], "screenshots": []}

    def wait(expression):
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if window.evaluate_js(expression):
                return
            time.sleep(.1)
        raise AssertionError(expression)

    def click(label):
        assert window.evaluate_js(
            "(()=>{const b=[...document.querySelectorAll('button')].find(e=>"
            "(e.getAttribute('aria-label')===" + json.dumps(label) +
            "||e.textContent.trim()===" + json.dumps(label) +
            ")&&!e.disabled&&e.getClientRects().length);if(!b)return false;b.click();return true})()"
        ), label
        time.sleep(.3)

    def capture(name):
        from System import Action
        from System.IO import FileStream, FileMode
        from Microsoft.Web.WebView2.Core import CoreWebView2CapturePreviewImageFormat
        pending = {}
        path = output / f"{name}.png"
        window.evaluate_js("window.scrollTo(0,0);document.querySelector('.metal-workspace')?.scrollTo(0,0)")
        time.sleep(.3)

        def begin():
            pending["stream"] = FileStream(str(path), FileMode.Create)
            pending["task"] = window.native.browser.webview.CoreWebView2.CapturePreviewAsync(
                CoreWebView2CapturePreviewImageFormat.Png, pending["stream"]
            )
        window.native.Invoke(Action(begin))
        try:
            pending["task"].GetAwaiter().GetResult()
        finally:
            pending["stream"].Close()
        report["screenshots"].append(str(path))

    def move(side, x):
        # Dispatch through the actual React pointer-move handler. These are local
        # preview inputs, not trusted OS pointer events or a connected controller.
        key = "L3 (Left Stick)" if side == "left" else "R3 (Right Stick)"
        if x == 0:
            window.evaluate_js(
                "document.querySelector('.mapping-controller-stage [aria-label="
                + json.dumps("Select " + key) + "]')"
                ".dispatchEvent(new PointerEvent('pointercancel',{bubbles:true}))"
            )
            wait(f"document.querySelector('.mapping-controller-stage .controller-canvas').dataset.{side}X==='0.000'")
            return
        assert window.evaluate_js(
            "(()=>{const b=document.querySelector('.mapping-controller-stage [aria-label="
            + json.dumps("Select " + key) + "]');if(!b)return false;"
            "const r=b.getBoundingClientRect();b.dispatchEvent(new PointerEvent('pointermove',"
            "{bubbles:true,buttons:1,clientX:r.x+r.width/2+r.width/2*" + str(x) +
            ",clientY:r.y+r.height/2}));return true})()"
        )
        # DOM pointer coordinates are integer pixels. The normalized gate vector
        # can be slightly under one after rounding, especially on compact art.
        wait(f"Math.abs(Number(document.querySelector('.mapping-controller-stage .controller-canvas').dataset.{side}X)-({x}))<.02")

    def check_layers(model):
        assert window.evaluate_js(
            "(()=>{const c=document.querySelector('.mapping-controller-stage .controller-canvas');"
            "const p=c.querySelector('.controller-photo'),caps=[...c.querySelectorAll('.controller-stick-cap')];"
            "return p.dataset.artLayer==='stickless-base'&&p.complete&&p.naturalWidth===1536&&p.naturalHeight===1024"
            "&&p.src.includes('controller-base-')&&caps.length===2&&caps.every(e=>{const s=getComputedStyle(e);"
            "return s.opacity==='1'&&s.boxShadow==='none'&&s.filter==='none'&&s.transitionDuration==='0s'"
            "&&!s.backgroundImage.includes('controller-base-')})"
            "&&[...c.querySelectorAll('button')].every(e=>getComputedStyle(e).boxShadow==='none')})()"
        ), f"{model} base/cap layering or glow"
        assert window.evaluate_js(
            "[...document.querySelectorAll('.header-controller-art .controller-photo')].every(e=>"
            "e.dataset.artLayer==='complete'&&!e.src.includes('controller-base-'))"
        ), "Static header must retain complete controller"

    def check_centering(model, size):
        # Measured from the fixed rings in the background plates, independently
        # of the crop coordinates and live DOM positioning.
        measured = {
            "x20": [(400.67, 307.98), (932.75, 510.55)],
            "x20_pro": [(399, 283), (960, 460)],
        }
        metrics = window.evaluate_js(
            "(()=>{const c=document.querySelector('.mapping-controller-stage .controller-canvas');"
            "const rect=e=>{const r=e.getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}};"
            "return {image:rect(c.querySelector('.controller-photo')),"
            "sticks:[...c.querySelectorAll('.controller-stick')].map(e=>({button:rect(e),cap:rect(e.firstElementChild)}))}})()"
        )
        photo = metrics["image"]
        offsets = []
        for item, (x, y) in zip(metrics["sticks"], measured[model]):
            expected = (photo["x"] + x / 1536 * photo["w"], photo["y"] + y / 1024 * photo["h"])
            for layer in ("button", "cap"):
                rect = item[layer]
                delta = (rect["x"] + rect["w"] / 2 - expected[0], rect["y"] + rect["h"] / 2 - expected[1])
                assert max(abs(value) for value in delta) < .1, f"{model} {layer} centering at {size}: {delta}"
                offsets.append({"layer": layer, "offset_css_pixels": delta})
        report.setdefault("centering", []).append({"model": model, "window": size, "offsets": offsets})

    try:
        wait("document.querySelectorAll('.controller-card').length===4")
        for player, model, name in [(1, "x20", "X20"), (2, "x20_pro", "X20 Pro")]:
            click(f"Add controller for Player {player}")
            click(f"Select {name}")
            click(f"Enter Studio for Player {player}")
            wait(f"document.querySelector('.mapping-controller-stage .controller-canvas')?.dataset.model==='{model}'")
            check_layers(model)
            check_centering(model, "1400x940")
            capture(f"{model}-neutral")
            click("Keyboard preview")
            for direction in [-1, 1]:
                move("left", direction)
                move("right", direction)
                check_layers(model)
                capture(f"{model}-both-{'left' if direction < 0 else 'right'}")
            move("left", 0)
            move("right", 0)
            check_centering(model, "1400x940-after-preview")
            click("Select L3 (Left Stick)")
            check_layers(model)
            window.evaluate_js("document.querySelectorAll('.mapping-controller-stage button').forEach(e=>e.classList.add('is-pressed'))")
            check_layers(model)
            window.evaluate_js("document.querySelectorAll('.mapping-controller-stage button').forEach(e=>e.classList.remove('is-pressed'))")
            capture(f"{model}-selected-no-glow")
            window.resize(1060, 760)
            time.sleep(.4)
            check_layers(model)
            assert window.evaluate_js("document.documentElement.scrollWidth<=innerWidth+2")
            check_centering(model, "1060x760")
            capture(f"{model}-compact")
            window.resize(1400, 940)
            click("Switch Controller")
            wait("!!document.querySelector('.controller-hub')")
            assert window.evaluate_js(
                "[...document.querySelectorAll('.controller-hub .controller-photo')].every(e=>"
                "e.dataset.artLayer==='complete'&&!e.src.includes('controller-base-'))"
            )
            report["checks"].append(f"{name}: edited base, both caps neutral/full deflection, no overlay glow, resize and intact static art")
        report["passed"] = True
        result["code"] = 0
    except Exception:
        report["error"] = traceback.format_exc()
        result["code"] = 1
    finally:
        (output / "thumbstick-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        lifecycle["quit"]()


from x20ctl.desktop import launcher, smoke
smoke.exercise = exercise
sys.argv = ["app.py", "--smoke-test", str(output)]
sys.exit(launcher.main())
