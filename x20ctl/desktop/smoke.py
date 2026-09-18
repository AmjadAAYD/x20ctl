"""Opt-in native-window acceptance checks and genuine Windows screenshots.

Never fabricates device state. Run with --smoke-test PATH in source or EXE.
"""

from __future__ import annotations

import json
import time
import traceback
from pathlib import Path


def exercise(window, directory, result):
    from PIL import Image, ImageStat
    from x20ctl import __version__

    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "checks": [], "screenshots": []}

    def wait_for(script, seconds=20):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if window.evaluate_js(script):
                return
            time.sleep(0.1)
        raise AssertionError(f"Timed out: {script}")

    def click(text):
        found = window.evaluate_js(
            "(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==="
            + json.dumps(text)
            + ");if(!b||b.disabled)return false;b.click();return true})()"
        )
        assert found, f"Enabled button not found: {text}"
        time.sleep(0.25)

    def api(operation, payload=None):
        window.evaluate_js("window.__smokeResult=null; window.pywebview.api.request("+json.dumps(operation)+","+json.dumps(payload or {})+").then(r=>window.__smokeResult=r)")
        wait_for("window.__smokeResult !== null", seconds=30)
        return window.evaluate_js("window.__smokeResult")

    def input_value(selector, value):
        window.evaluate_js("(()=>{const e=document.querySelector("+json.dumps(selector)+");Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,"+json.dumps(value)+");e.dispatchEvent(new Event('input',{bubbles:true}));})()")
        time.sleep(0.2)

    def capture(name):
        from System import Action
        from System.IO import FileStream, FileMode
        from Microsoft.Web.WebView2.Core import CoreWebView2CapturePreviewImageFormat

        time.sleep(0.4)
        # PrintWindow cannot capture GPU-composited WebView2 content reliably.
        # CapturePreview copies the actual native browser surface, not a mockup.
        pending = {}
        path = (directory / f"{name}.png").resolve()

        def begin():
            pending["stream"] = FileStream(str(path), FileMode.Create)
            pending["task"] = (
                window.native.browser.webview.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png, pending["stream"]
                )
            )

        window.native.Invoke(Action(begin))
        try:
            pending["task"].GetAwaiter().GetResult()
        finally:
            pending["stream"].Close()
        with Image.open(path) as shot:
            assert shot.width > 900 and shot.height > 650
            assert (
                max(ImageStat.Stat(shot.convert("RGB")).stddev) > 10
            ), "Blank screenshot"
        report["screenshots"].append(name)

    try:
        wait_for(
            "typeof window.pywebview?.api?.request === 'function' && !!document.querySelector('h1')"
        )
        wait_for("!document.body.innerText.includes('Desktop connection unavailable')")
        wait_for(
            "document.querySelector('.version')?.textContent.includes("+json.dumps(__version__)+")"
        )
        assert not window.evaluate_js(
            "!!document.querySelector('[role=alert]')"
        ), "Unexpected startup error"
        assert (
            window.evaluate_js("document.querySelector('h1').textContent") == "Buttons"
        )
        capture("buttons")
        report["checks"].append("Native bridge loaded bundled UI")
        for label, title, filename in [
            ("Response curves", "Response curves", "curves"),
            ("Macros", "Macros", "macros"),
            ("Vibration", "Vibration", "vibration"),
            ("Power & device", "Power & device", "power"),
            ("Input tester", "Input tester", "tester"),
        ]:
            click(label)
            assert (
                window.evaluate_js("document.querySelector('h1').textContent") == title
            )
            capture(filename)
            report["checks"].append(f"Opened {title}")
        click("Macros")
        click("Add Step")
        assert window.evaluate_js("document.body.innerText.includes('1 unsent change')")
        click("🎹 Edit in Piano Roll")
        assert window.evaluate_js("document.body.innerText.includes('M1 Piano-Roll')")
        capture("macro-editor")
        click("Update M1 draft")
        report["checks"].append("Edited a macro draft in the native window")
        click("Vibration")
        click("Gentle")
        assert window.evaluate_js("document.querySelector('.large-value').textContent") == "30%"
        click("Power & device")
        click("Never")
        assert window.evaluate_js("document.querySelector('.preset-row button.selected').textContent") == "Never"
        report["checks"].append("Changed vibration and power drafts without hardware writes")
        click("Response curves")
        click("aggressive")
        click("Edit Curve")
        assert window.evaluate_js("document.body.innerText.includes('P1 Control Point')")
        click("Left Trigger (LT)")
        click("instant")
        report["checks"].append("Edited stick and trigger curves and presets")
        input_value('[aria-label="Setup name"]', "Smoke test setup")
        click("Save")
        wait_for("document.body.innerText.includes('Setup saved on this computer.')")
        saved = api("bootstrap")
        assert saved["ok"] and len(saved["data"]["profiles"]) == 1
        assert saved["data"]["profiles"][0]["vibration"] == 30
        assert saved["data"]["profiles"][0]["idleTimeoutMinutes"] == 0
        report["checks"].append("Saved an edited setup through the UI and reread native storage")
        assert not api("import_profile", {"profile":{"schemaVersion":999}})["ok"]
        assert len(api("bootstrap")["data"]["profiles"]) == 1
        report["checks"].append("Invalid import rejected without changing saved setups")
        click("Connection guide")
        assert window.evaluate_js("document.body.innerText.includes('Two connections. One controller.')")
        window.evaluate_js("document.querySelector('[aria-label=\"Close guide\"]').click()")
        click("Connect controller")
        wait_for("!document.body.innerText.includes('Scanning Bluetooth…')", seconds=20)
        assert window.evaluate_js("document.body.innerText.includes('No supported controllers found') || !!document.querySelector('.device-result') || !!document.querySelector('.error-text')")
        capture("connection")
        window.evaluate_js("document.querySelector('[aria-label=\"Close devices\"]').click()")
        report["checks"].append("Ran real BLE discovery and displayed its result")
        state = api("input")["data"]
        report["hardware"] = {"bleConnected":state["connected"], "xinputDetected":state["input"] is not None}
        if not state["connected"]:
            assert not api("apply", {"category":"vibration", "value":30})["ok"]
            assert window.evaluate_js("[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Apply changes').disabled")
            report["checks"].append("Disconnected write rejected by UI and native API")
        # Bridge calls are exercised through the same JS interface as the UI.
        window.evaluate_js(
            "window.pywebview.api.request('shell',{}).then(r=>window.__smokeRejected=!r.ok)"
        )
        wait_for("window.__smokeRejected === true")
        report["checks"].append("Unknown bridge operation rejected")
        original_url = window.evaluate_js("location.href")
        window.evaluate_js("location.href='https://example.invalid/'")
        time.sleep(0.5)
        assert window.evaluate_js("location.href") == original_url
        report["checks"].append("External navigation blocked in the native renderer")
        report["passed"] = True
        result["code"] = 0
    except Exception:
        report["error"] = traceback.format_exc()
        result["code"] = 1
    finally:
        (directory / "smoke-report.json").write_text(
            json.dumps(report, indent=2), encoding="utf-8"
        )
        window.destroy()
