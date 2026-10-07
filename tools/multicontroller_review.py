"""Isolated, hidden Windows WebView2 review; no screen capture or device writes.

Uses the real desktop launcher, production assets and bridge. DOM preview events
are synthetic, so this does not measure trusted OS input or physical hardware.
"""
from __future__ import annotations

import argparse
import json
import sys
import time
import traceback
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


def review(window, directory, result, lifecycle):
    report = {
        "passed": False,
        "mode": "hidden native Windows WebView2; real desktop bridge; no screen capture",
        "checks": [],
        "geometry": [],
        "screenshots": [],
    }

    def wait(expression):
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if window.evaluate_js(expression):
                return
            time.sleep(.05)
        raise AssertionError(expression)

    def click(label, scope="document"):
        assert window.evaluate_js(
            "(()=>{const b=[..." + scope + ".querySelectorAll('button')].find(e=>"
            "(e.getAttribute('aria-label')===" + json.dumps(label) +
            "||e.textContent.trim()===" + json.dumps(label) +
            "||e.querySelector('strong')?.textContent.trim()===" + json.dumps(label) +
            ")&&!e.disabled&&e.getClientRects().length);if(!b)return false;b.click();return true})()"
        ), label
        time.sleep(.1)

    def api(operation, payload=None):
        window.evaluate_js(
            "window.__reviewResult=null;window.pywebview.api.request("
            + json.dumps(operation) + "," + json.dumps(payload or {})
            + ").then(r=>window.__reviewResult=r)"
        )
        wait("window.__reviewResult!==null")
        return window.evaluate_js("window.__reviewResult")

    def check_geometry(model, size):
        # Independent measured source-art coordinates, not copied from the DOM.
        points = {
            "x20": [(400.67, 307.98), (932.75, 510.55), (1202, 315)],
            "x20_pro": [(399, 283), (960, 460), (1237, 290)],
        }
        wait("[...document.querySelectorAll('.mapping-controller-stage img')].every(i=>i.complete&&i.naturalWidth)")
        metrics = window.evaluate_js(
            "(()=>{const c=document.querySelector('.mapping-controller-stage .controller-canvas');"
            "const r=e=>{const b=e.getBoundingClientRect();return {x:b.x,y:b.y,w:b.width,h:b.height}};"
            "const i=c.querySelector('img'),p=c.querySelector('.controller-image-plane');"
            "return {canvas:r(c),plane:r(p),fit:getComputedStyle(i).objectFit,layer:i.dataset.artLayer,"
            "controls:['Select L3 (Left Stick)','Select R3 (Right Stick)','Select B'].map(label=>"
            "r([...c.querySelectorAll('button')].find(b=>b.getAttribute('aria-label')===label))),"
            "caps:[...c.querySelectorAll('.controller-stick-cap')].map(e=>({rect:r(e),"
            "opacity:getComputedStyle(e).opacity,shadow:getComputedStyle(e).boxShadow,filter:getComputedStyle(e).filter})),"
            "headerInside:[...document.querySelectorAll('.chassis-header button')].every(e=>"
            "e.getBoundingClientRect().right<=innerWidth+1),overflow:document.documentElement.scrollWidth-innerWidth}})()"
        )
        assert metrics["layer"] == "stickless-base"
        assert metrics["fit"] == "contain"
        canvas, plane = metrics["canvas"], metrics["plane"]
        assert abs(plane["w"] / canvas["w"] - .84) < .001
        assert plane["x"] >= canvas["x"] and plane["y"] >= canvas["y"]
        assert plane["x"] + plane["w"] <= canvas["x"] + canvas["w"] + 1
        assert plane["y"] + plane["h"] <= canvas["y"] + canvas["h"] + 1
        assert metrics["overflow"] <= 2 and metrics["headerInside"]
        assert len(metrics["caps"]) == 2
        for index, (x, y) in enumerate(points[model]):
            expected_x = plane["x"] + x / 1536 * plane["w"]
            expected_y = plane["y"] + y / 1024 * plane["h"]
            layers = [metrics["controls"][index]]
            if index < 2:
                cap = metrics["caps"][index]
                assert cap["opacity"] == "1" and cap["shadow"] == "none" and cap["filter"] == "none"
                layers.append(cap["rect"])
            for rect in layers:
                assert abs(rect["x"] + rect["w"] / 2 - expected_x) < .1
                assert abs(rect["y"] + rect["h"] / 2 - expected_y) < .1
        report["geometry"].append({"model": model, "size": size, "metrics": metrics})

    def check_slider_and_motors(model, count):
        click("Vibration", "document.querySelector('.chassis-nav')")
        click("Maximum")
        wait(f"document.querySelectorAll('.controller-motor').length==={count}")
        assert window.evaluate_js(
            "[...document.querySelectorAll('.controller-motor')].every(e=>"
            "getComputedStyle(e.querySelector('.motor-wave')).animationName==='motor-contour-travel')"
        )
        window.evaluate_js(
            "(()=>{const e=document.querySelector('[aria-label=\"Vibration strength\"]');"
            "e.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}));"
            "Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'30');"
            "e.dispatchEvent(new Event('input',{bubbles:true}))})()"
        )
        wait("document.querySelector('[aria-label=\"Vibration strength\"]').value==='30'")
        assert window.evaluate_js(
            "['.metal-slider-fill','.metal-slider-thumb'].every(s=>"
            "getComputedStyle(document.querySelector(s)).transitionDuration==='0s')"
        )
        window.evaluate_js(
            "document.querySelector('[aria-label=\"Vibration strength\"]').dispatchEvent(new PointerEvent('pointerup',{bubbles:true}))"
        )
        wait("!document.querySelector('.metal-slider.is-dragging')")
        assert window.evaluate_js(
            "parseFloat(getComputedStyle(document.querySelector('.metal-slider-fill')).transitionDuration)>0"
        )
        click("Off")
        wait("document.querySelectorAll('.controller-motor').length===0")
        report["checks"].append(f"{model}: localized motors and immediate dragging / animated preset sliders")

    try:
        wait("typeof window.pywebview?.api?.request==='function'&&!!document.querySelector('.controller-hub')")
        # Test-local preference only; prevent update-network traffic on entry.
        assert api("set_updates", {"enabled": False})["ok"]
        if window.evaluate_js("!!document.querySelector('.startup-skip')"):
            click("Skip intro Esc")
        wait("!document.querySelector('.startup-intro')")
        assert window.evaluate_js("document.querySelectorAll('.controller-card[data-controller=\"\"]').length") == 4
        assert not window.evaluate_js("!!document.querySelector('[data-studio-player]')")
        report["checks"].append("Launch starts with four unassigned players and no Studio")
        for player, model, name in [(1, "x20", "X20"), (2, "x20_pro", "X20 Pro")]:
            click(f"Add controller for Player {player}")
            assert window.evaluate_js(f"document.querySelector('.picker-note').textContent.includes('Player {player}')")
            if model == "x20_pro":
                assert window.evaluate_js("[...document.querySelectorAll('.controller-switch-row')].find(e=>e.querySelector('[aria-label=\"Select X20 Pro\"]')).lastElementChild.disabled")
            click(f"Select {name}", "document.querySelector('[role=dialog]')")
            wait(f"document.querySelector('[data-player=\"{player}\"]')?.dataset.controller==='{model}'")
        report["checks"].append("Player 1 X20 and Player 2 Pro assignments are independent and disconnected")
        for player, model, count in [(1, "x20", 2), (2, "x20_pro", 4)]:
            click(f"Enter Studio for Player {player}")
            wait(f"document.querySelector('.studio-player-label')?.textContent==='Player {player}'")
            click("Buttons", "document.querySelector('.chassis-nav')")
            for size in [(1400, 940), (1060, 760)]:
                window.resize(*size)
                time.sleep(.3)
                check_geometry(model, size)
            click("Keyboard preview")
            window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keydown',{key:'w',bubbles:true}))")
            wait("document.querySelector('.mapping-controller-stage .controller-canvas').dataset.leftY==='-1.000'")
            assert window.evaluate_js("document.querySelector('.controller-stick-cap').style.transform==='translate(0%, -18%)'")
            window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keyup',{key:'w',bubbles:true}))")
            wait("document.querySelector('.mapping-controller-stage .controller-canvas').dataset.leftY==='0.000'")
            click("Keyboard preview")
            report["checks"].append(f"{model}: neutral opaque caps, model geometry, containment and preview motion at both sizes")
            if model == "x20_pro":
                click("Macros", "document.querySelector('.chassis-nav')")
                assert window.evaluate_js("document.querySelectorAll('.macro-paddle').length") == 6
                for operation, payload in [("scan", {}), ("connect", {"address": "unverified"}), ("input", {}), ("apply", {"category": "vibration", "value": 30}), ("reset", {"confirm": True})]:
                    rejected = api(operation, payload)
                    assert not rejected["ok"] and "unavailable" in rejected["error"]["message"]
                report["checks"].append("Pro shows six controls and real native bridge rejects hardware operations")
            check_slider_and_motors(model, count)
            assert window.evaluate_js("[...document.querySelectorAll('.chassis-header button')].every(e=>e.textContent.trim()!=='Players')")
            click("Switch Controller", "document.querySelector('.chassis-header')")
            wait("!!document.querySelector('.controller-hub')")
            assert not window.evaluate_js("!!document.querySelector('.controller-picker')")
        report["checks"].append("Both Studios have no Players button; Switch Controller returns directly to the four-player Controller zone")
        click("Enter Studio for Player 2")
        click("Switch Controller", "document.querySelector('.chassis-header')")
        wait("!!document.querySelector('.controller-hub')")
        click("Choose controller for Player 2")
        wait("!!document.querySelector('.controller-picker')")
        assert window.evaluate_js("document.querySelector('.picker-note').textContent.includes('Player 2')")
        click("Close dialog")
        assert window.evaluate_js("document.querySelector('[data-player=\"2\"]').dataset.controller") == "x20_pro"
        click("Choose controller for Player 2")
        click("Select X20", "document.querySelector('[role=dialog]')")
        click("Enter Studio for Player 2")
        wait("document.querySelector('[data-studio-player=\"2\"]:not([hidden]) .studio-player-label')?.textContent==='Player 2'")
        assert window.evaluate_js("!!document.querySelector('[data-studio-player=\"2\"]:not([hidden]) .model-workspace')") is False
        click("Switch Controller", "document.querySelector('[data-studio-player=\"2\"]:not([hidden]) .chassis-header')")
        wait("!!document.querySelector('.controller-hub')")
        click("Choose controller for Player 2")
        click("Select X20 Pro", "document.querySelector('[role=dialog]')")
        click("Enter Studio for Player 2")
        wait("document.querySelector('.model-workspace')?.dataset.page==='rumble'")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').value") == "0"
        click("Switch Controller", "document.querySelector('.model-workspace .chassis-header')")
        wait("!!document.querySelector('.controller-hub')")
        assert window.evaluate_js("document.querySelector('[data-player=\"1\"]').dataset.controller") == "x20"
        assert window.evaluate_js("document.querySelector('[data-player=\"2\"]').dataset.controller") == "x20_pro"
        report["checks"].append("Controller zone switching uses Player 2, supports picker cancellation and preserves drafts / Player 1")
        for player, name in [(3, "X20"), (4, "X20 Pro")]:
            click(f"Add controller for Player {player}")
            click(f"Select {name}", "document.querySelector('[role=dialog]')")
        assert window.evaluate_js("document.querySelectorAll('.controller-card:not([data-controller=\"\"])').length") == 4
        report["checks"].append("All four players can be assigned independently")
        window.evaluate_js("location.reload()")
        wait("!!document.querySelector('.controller-hub')")
        if window.evaluate_js("!!document.querySelector('.startup-skip')"):
            click("Skip intro Esc")
        wait("!document.querySelector('.startup-intro')")
        assert window.evaluate_js("document.querySelectorAll('.controller-card[data-controller=\"\"]').length") == 4
        report["checks"].append("Fresh reload returns to four unassigned players")
        report["passed"] = True
        result["code"] = 0
    except Exception:
        report["error"] = traceback.format_exc()
        result["code"] = 1
    finally:
        (Path(directory) / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps({"passed": report["passed"], "checks": len(report["checks"]), "error": report.get("error")}), flush=True)
        lifecycle["quit"]()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    import webview
    from x20ctl.desktop import launcher, smoke

    original_create = webview.create_window

    def hidden_window(*values, **options):
        options["hidden"] = True
        return original_create(*values, **options)

    webview.create_window = hidden_window
    smoke.exercise = review
    directory = Path(args.output).resolve()
    directory.mkdir(parents=True, exist_ok=True)
    return launcher._run(argparse.Namespace(smoke_test=str(directory)))


if __name__ == "__main__":
    raise SystemExit(main())
