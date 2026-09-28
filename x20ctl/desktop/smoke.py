"""Opt-in native-window acceptance checks and genuine Windows screenshots.

Never fabricates device state. Run with --smoke-test PATH in source or EXE.
"""

from __future__ import annotations

import json
import time
import traceback
from pathlib import Path


def exercise(window, directory, result, lifecycle=None):
    from PIL import Image, ImageStat
    from x20ctl import __version__

    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "checks": [], "screenshots": [], "layouts": []}
    pages = [
        ("Buttons", "buttons"),
        ("Response curves", "curves"),
        ("Macros", "macros"),
        ("Vibration", "vibration"),
        ("Power & device", "power"),
        ("Input tester", "tester"),
        ("Saved setups", "profiles-empty"),
    ]

    def wait_for(script, seconds=20):
        deadline = time.monotonic() + seconds
        while time.monotonic() < deadline:
            if window.evaluate_js(script):
                return
            time.sleep(0.1)
        raise AssertionError(f"Timed out: {script}")

    def click(text, scope="document"):
        found = window.evaluate_js(
            "(()=>{const root=" + scope + ";"
            "const b=[...root.querySelectorAll('button')].find(b=>"
            "(b.textContent.trim()===" + json.dumps(text)
            + "||b.getAttribute('aria-label')===" + json.dumps(text)
            + "||b.querySelector('strong')?.textContent.trim()===" + json.dumps(text)
            + ")&&!b.disabled&&b.getClientRects().length);"
            "if(!b)return false;b.focus();b.click();return true})()"
        )
        assert found, f"Enabled button not found: {text}"
        time.sleep(0.25)

    def api(operation, payload=None):
        window.evaluate_js(
            "window.__smokeResult=null; window.pywebview.api.request("
            + json.dumps(operation) + "," + json.dumps(payload or {})
            + ").then(r=>window.__smokeResult=r)"
        )
        wait_for("window.__smokeResult !== null", seconds=30)
        return window.evaluate_js("window.__smokeResult")

    def input_value(selector, value):
        assert window.evaluate_js(
            "(()=>{const root=document.querySelector('[role=dialog]')||document;"
            "const e=root.querySelector(" + json.dumps(selector)
            + ");if(!e||e.disabled)return false;"
            "Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value')"
            ".set.call(e," + json.dumps(value)
            + ");e.dispatchEvent(new Event('input',{bubbles:true}));return true})()"
        ), f"Enabled input not found: {selector}"
        time.sleep(0.2)

    def press_key(key, shift=False):
        window.evaluate_js(
            "document.activeElement.dispatchEvent(new KeyboardEvent('keydown',"
            + json.dumps({"key": key, "shiftKey": shift, "bubbles": True, "cancelable": True})
            + "))"
        )
        time.sleep(0.15)

    def select_value(label, value):
        assert window.evaluate_js(
            "(()=>{const e=[...document.querySelectorAll('select')].find(e=>"
            "e.getAttribute('aria-label')===" + json.dumps(label) + ");"
            "if(!e||e.disabled)return false;e.value=" + json.dumps(str(value)) + ";"
            "e.dispatchEvent(new Event('change',{bubbles:true}));return true})()"
        ), f"Enabled select not found: {label}"
        time.sleep(0.2)

    def check_dialog_keyboard():
        # This exercises the dialog's real DOM keyboard handlers and focus state.
        # It does not claim to synthesize a trusted native OS keyboard event.
        assert window.evaluate_js(
            "(()=>{const d=document.querySelector('[role=dialog]');"
            "if(!d||d.getAttribute('aria-modal')!=='true'||!d.contains(document.activeElement))return false;"
            "const title=document.getElementById(d.getAttribute('aria-labelledby'));"
            "if(!title?.textContent.trim())return false;"
            "window.__smokeFocusables=[...d.querySelectorAll("
            "'button:not(:disabled),input:not(:disabled),select:not(:disabled),a[href],[tabindex=\"0\"]'"
            ")].filter(e=>e.getClientRects().length);"
            "if(!window.__smokeFocusables.length)return false;"
            "window.__smokeFocusables[0].focus();return true})()"
        ), "Dialog is missing its name, modal state, or initial focus"
        press_key("Tab", shift=True)
        assert window.evaluate_js(
            "document.activeElement===window.__smokeFocusables[window.__smokeFocusables.length-1]"
        ), "Shift+Tab escaped the dialog"
        press_key("Tab")
        assert window.evaluate_js(
            "document.activeElement===window.__smokeFocusables[0]"
        ), "Tab escaped the dialog"

        assert window.evaluate_js(
            "(()=>{const d=document.querySelector('.metal-dialog-content');"
            "return d&&d.scrollWidth<=d.clientWidth+2})()"
        ), "Dialog body overflows horizontally; only its timeline may scroll"

    def check_layout(page, size):
        wait_for(
            "document.querySelector('.metal-workspace')?.clientWidth>0"
            "&&document.querySelector('.editor')?.clientWidth>0"
            "&&document.querySelector('.chassis-footer')?.getClientRects().length>0"
        )
        metrics = window.evaluate_js(
            "(()=>{const selectors=['html','body','.metal-app','.metal-workspace','.editor'];"
            "return {viewport:{width:innerWidth,height:innerHeight},elements:selectors.map(selector=>{"
            "const e=document.querySelector(selector);return {selector,width:e?.clientWidth||0,"
            "scrollWidth:e?.scrollWidth||0}})}})()"
        )
        report["layouts"].append({"page": page, "window": size, **metrics})
        failures = [
            item for item in metrics["elements"]
            if item["scrollWidth"] > item["width"] + 2
        ]
        assert not failures, f"Horizontal workspace overflow on {page} at {size}: {failures}"
        assert window.evaluate_js(
            "(()=>{const e=document.querySelector('.chassis-footer');"
            "return !!e&&e.getClientRects().length>0&&getComputedStyle(e).position!=='fixed'"
            "&&e.scrollHeight>0})()"
        ), f"Apply footer is missing or obscuring controls on {page} at {size}"

    def capture(name):
        from System import Action
        from System.IO import FileStream, FileMode
        from Microsoft.Web.WebView2.Core import CoreWebView2CapturePreviewImageFormat

        time.sleep(0.4)
        # CapturePreview copies the actual GPU-composited native browser surface.
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
            assert shot.width > 900 and shot.height > 600
            assert max(ImageStat.Stat(shot.convert("RGB")).stddev) > 10, "Blank screenshot"
        report["screenshots"].append(name)

    try:
        wait_for(
            "typeof window.pywebview?.api?.request === 'function' && !!document.querySelector('h1')"
        )
        wait_for("!document.body.innerText.includes('Desktop connection unavailable')")
        wait_for("!!document.querySelector('.rebranded-hub')")
        assert not window.evaluate_js("!!document.querySelector('[role=alert]')"), "Unexpected startup error"
        assert window.evaluate_js("document.querySelector('h1').textContent") == "Add a Controller"
        capture("controllers")
        window.resize(1060, 760)
        time.sleep(0.4)
        assert window.evaluate_js("document.documentElement.scrollWidth <= document.documentElement.clientWidth + 2")
        capture("controllers-compact")
        window.resize(1400, 940)
        time.sleep(0.4)
        wait_for("document.querySelectorAll('.controller-card').length===4")
        assert not window.evaluate_js("!!document.querySelector('[aria-label=Appearance]')")
        report["checks"].append("Four-slot Controllers entry opened with fixed rebrand palette and no Theme Studio")
        click("Open X20 studio")
        wait_for("document.querySelector('h1')?.textContent === 'Buttons'")
        assert window.evaluate_js(
            "(()=>{const rail=document.querySelector('.chassis-sidebar');"
            "const brand=document.querySelector('.chassis-brand');"
            "const nav=document.querySelector('.chassis-nav');"
            "if(!rail||!brand||!nav)return false;"
            "return getComputedStyle(rail).backgroundImage==='none'"
            "&&getComputedStyle(brand).backgroundImage!=='none'"
            "&&getComputedStyle(nav).backgroundImage!=='none'"
            "&&nav.getBoundingClientRect().top-brand.getBoundingClientRect().bottom>=8"
            "&&rail.getBoundingClientRect().bottom-nav.getBoundingClientRect().bottom>=24})()"
        ), "Sidebar brand and navigation are not separate panels"
        wait_for("document.querySelector('.version')?.textContent.includes(" + json.dumps(__version__) + ")")
        assert window.evaluate_js("!!document.querySelector('.photo-control-overlay')"), "Visible controller control layer missing"
        assert window.evaluate_js(
            "(()=>{const image=document.querySelector('.mapping-controller-photo');"
            "const stick=document.querySelector('.photo-stick-control');"
            "if(!image||!stick)return false;const box=image.getBoundingClientRect();"
            "const scale=Math.min(box.width/1536,box.height/1024);"
            "const expectedX=box.left+(box.width-1536*scale)/2+385*scale;"
            "const expectedY=box.top+(box.height-1024*scale)/2+302*scale;"
            "const matrix=stick.getScreenCTM();"
            "return !!matrix&&Math.hypot(matrix.e-expectedX,matrix.f-expectedY)<5})()"
        ), "Left SVG thumbstick is not centered on the photographed stick"
        assert window.evaluate_js(
            "(()=>{const button=document.querySelector('.photo-control-overlay [aria-label=\"Select B\"]');"
            "if(!button)return false;button.dispatchEvent(new MouseEvent('click',{bubbles:true}));return true})()"
        ), "Clicking a pictured button did not select it"
        wait_for("document.querySelector('.mapping-selected-control .mapping-key-large')?.textContent==='B'")
        click("Keyboard preview")
        assert window.evaluate_js("document.body.innerText.includes('Keyboard preview active')")
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keydown',{key:'w',bubbles:true}))")
        wait_for("document.querySelector('.photo-control-overlay')?.getAttribute('data-left-y')==='-1.000'")
        assert window.evaluate_js(
            "document.querySelector('.photo-stick-control g')?.getAttribute('transform')"
            "==='translate(0 -17)'"
        ), "WASD state did not visibly move the SVG stick cap"
        capture("buttons-keyboard-preview")
        assert window.evaluate_js("document.querySelector('.connection-lights')?.textContent.includes('Not Connected')"), "Preview impersonated connected hardware"
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keyup',{key:'w',bubbles:true}))")
        wait_for("document.querySelector('.photo-control-overlay')?.getAttribute('data-left-y')==='0.000'")
        click("Keyboard preview")
        report["checks"].append("Visible picture controls select mappings and opt-in WASD preview stays local")
        assert window.evaluate_js(
            "['Connection guide','New setup','Import setup','Export setup'].every(label=>"
            "!![...document.querySelectorAll('button')].find(b=>b.getAttribute('aria-label')===label))"
        ), "Icon toolbar actions need accessible names"
        report["checks"].append("Native bridge loaded bundled UI with named toolbar controls")

        for title, filename in pages:
            click(title, "document.querySelector('.chassis-nav')")
            assert window.evaluate_js("document.querySelector('h1').textContent") == title
            if title == "Vibration":
                assert window.evaluate_js(
                    "(()=>{const overlay=document.querySelector('.vibration-grip-overlay');"
                    "return !!overlay&&overlay.querySelectorAll('.grip-pulse').length===2"
                    "&&overlay.getAttribute('data-strength')==='70'})()"
                ), "Vibration page is missing its two-grip visual preview"
                assert window.evaluate_js(
                    "(()=>{const image=document.querySelector('.vibration-controller-frame img');"
                    "const rings=document.querySelectorAll('.grip-wave-outer');"
                    "if(!image||rings.length!==2)return false;"
                    "const art=image.getBoundingClientRect(),left=rings[0].getBoundingClientRect(),right=rings[1].getBoundingClientRect();"
                    "return left.left>=art.left+10&&right.right<=art.right-10})()"
                ), "Vibration contours extend beyond the controller art"
                assert window.evaluate_js(
                    "matchMedia('(prefers-reduced-motion: reduce)').matches"
                    "||getComputedStyle(document.querySelector('.grip-wave')).animationName.includes('grip-swell')"
                ), "Grip preview animation is not active"
            if title == "Power & device":
                assert window.evaluate_js(
                    "(()=>{const panel=document.querySelector('.battery-panel');"
                    "const content=panel?.querySelector('.battery-status-content');"
                    "if(!panel||!content||panel.querySelector('.power-controller-art'))return false;"
                    "const a=panel.getBoundingClientRect(),b=content.getBoundingClientRect();"
                    "return Math.abs((a.left+a.right)/2-(b.left+b.right)/2)<6"
                    "&&content.querySelectorAll('.battery-segments span').length===4})()"
                ), "Battery status is not centered without controller artwork"
            check_layout(title, "normal")
            capture(filename)
            report["checks"].append(f"Opened {title}")
        assert window.evaluate_js("document.body.innerText.includes('Your setups belong here')")

        normal_size = (window.width, window.height)
        try:
            window.resize(1060, 760)
            time.sleep(0.6)
            for title, _filename in pages:
                click(title, "document.querySelector('.chassis-nav')")
                check_layout(title, "1060x760")
            click("Buttons", "document.querySelector('.chassis-nav')")
            capture("buttons-compact")
        finally:
            window.resize(*normal_size)
            time.sleep(0.5)
        report["checks"].append("All seven pages fit normal and 1060x760 windows without horizontal workspace overflow")

        click("Macros", "document.querySelector('.chassis-nav')")
        click("Add Step")
        assert window.evaluate_js("document.body.innerText.includes('1 unsent change')")
        click("B, step 1")
        select_value("Left stick, step 1", 2)
        select_value("Right stick, step 1", 7)
        click("Edit in Piano Roll")
        assert window.evaluate_js("document.body.innerText.includes('M1 Piano-Roll')")
        input_value('[aria-label="Step 1 hold"]', "45")
        check_dialog_keyboard()
        capture("macro-editor")
        click("Update M1 draft")
        report["checks"].append("Edited macro chords, both stick directions and timing through the sequencer and piano roll")

        click("Buttons", "document.querySelector('.chassis-nav')")
        select_value("Remap A", "Y")
        assert window.evaluate_js("document.querySelector('.mapping-count').textContent") == "1 modified"
        report["checks"].append("Changed the A-to-Y assignment in the remap inspector")

        click("Vibration", "document.querySelector('.chassis-nav')")
        click("Off")
        assert "0%" in window.evaluate_js("document.querySelector('.haptics-master input[type=range]').style.background")
        wait_for("document.querySelector('.vibration-grip-overlay')?.getAttribute('data-strength')==='0'")
        assert window.evaluate_js(
            "getComputedStyle(document.querySelector('.vibration-grip-overlay')).opacity==='0'"
        ), "Zero vibration must hide the grip effect"
        click("Gentle")
        assert window.evaluate_js("document.querySelector('.large-value').textContent") == "30%"
        assert "30%" in window.evaluate_js("document.querySelector('.haptics-master input[type=range]').style.background")
        wait_for("document.querySelector('.vibration-grip-overlay')?.getAttribute('data-strength')==='30'")
        click("Support X20ctl", "document.querySelector('.chassis-sidebar')")
        assert window.evaluate_js("document.querySelector('[role=dialog]')?.textContent.includes('Support X20ctl on Ko-fi')")
        assert window.evaluate_js("getComputedStyle(document.querySelector('.support-primary-action')).justifyContent") == "flex-start"
        capture("support-dialog")
        click("Close", "document.querySelector('[role=dialog]')")
        assert not window.evaluate_js("!!document.querySelector('[role=dialog]')")
        click("Power & device", "document.querySelector('.chassis-nav')")
        click("Never")
        assert window.evaluate_js("document.querySelector('.preset-row button.selected').textContent") == "Never"
        report["checks"].append("Changed vibration and power drafts without hardware writes")

        click("Response curves", "document.querySelector('.chassis-nav')")
        click("aggressive")
        click("Edit Curve")
        assert window.evaluate_js("document.body.innerText.includes('P1 Control Point')")
        click("Left Trigger (LT)")
        click("instant")
        click("Curve guide")
        check_dialog_keyboard()
        capture("curve-guide")
        press_key("Escape")
        wait_for("!document.querySelector('[role=dialog]')")
        assert window.evaluate_js("document.activeElement.id==='curves-help-btn'")
        report["checks"].append("Edited stick and trigger drafts; curve guide traps focus and closes with Escape")

        input_value('[aria-label="Setup name"]', "Smoke test setup")
        click("Save")
        wait_for("document.body.innerText.includes('Setup saved on this computer.')")
        saved = api("bootstrap")
        assert saved["ok"] and len(saved["data"]["profiles"]) == 1
        saved_profile = saved["data"]["profiles"][0]
        assert saved_profile["remaps"]["A"] == "Y"
        assert saved_profile["macros"]["M1"][0]["buttons"] == ["A", "B"]
        assert saved_profile["macros"]["M1"][0]["leftStick"] == 2
        assert saved_profile["macros"]["M1"][0]["rightStick"] == 7
        assert saved_profile["macros"]["M1"][0]["durationMs"] == 45
        assert saved_profile["vibration"] == 30
        assert saved_profile["idleTimeoutMinutes"] == 0
        assert saved_profile["stickCurves"]["left"]["preset"] == "aggressive"
        assert saved_profile["triggerCurves"]["left"]["preset"] == "instant"
        report["checks"].append("Saved the edited setup through the UI and verified native storage")

        click("Saved setups", "document.querySelector('.chassis-nav')")
        assert window.evaluate_js("document.querySelector('.profile-card h3').textContent") == "Smoke test setup"
        capture("profiles")
        dirty_before = window.evaluate_js("document.querySelector('.draft-indicator').textContent")
        click("New setup", "document.querySelector('.toolbar-actions')")
        assert window.evaluate_js("document.querySelector('[role=dialog]').textContent.includes('Replace unsent changes?')")
        check_dialog_keyboard()
        capture("confirmation")
        click("Cancel", "document.querySelector('[role=dialog]')")
        wait_for("!document.querySelector('[role=dialog]')")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Setup name\"]').value") == "Smoke test setup"
        assert window.evaluate_js("document.querySelector('.draft-indicator').textContent") == dirty_before
        click("Delete Smoke test setup")
        assert window.evaluate_js("document.querySelector('[role=dialog]').textContent.includes('Delete saved setup?')")
        check_dialog_keyboard()
        press_key("Escape")
        wait_for("!document.querySelector('[role=dialog]')")
        assert window.evaluate_js("document.activeElement.getAttribute('aria-label')") == "Delete Smoke test setup"
        assert api("bootstrap")["data"]["profiles"] == saved["data"]["profiles"]
        report["checks"].append("New-draft Cancel and saved-setup Escape preserve draft and storage; dialog focus is contained and restored")

        click("New setup", "document.querySelector('.toolbar-actions')")
        click("Continue", "document.querySelector('[role=dialog]')")
        wait_for("document.body.innerText.includes('New local draft created.')")
        assert window.evaluate_js("document.querySelector('.draft-indicator').textContent") == "Editing offline draft"
        click("Load setup")
        wait_for("document.body.innerText.includes('Setup loaded into the editor. Review it before applying.')")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Setup name\"]').value") == "Smoke test setup"
        report["checks"].append("Created a fresh draft and loaded the saved setup from the library")

        assert not api("import_profile", {"profile": {"schemaVersion": 999}})["ok"]
        assert api("bootstrap")["data"]["profiles"] == saved["data"]["profiles"]
        report["checks"].append("Invalid import rejected without changing saved setups")

        click("Connection guide")
        assert window.evaluate_js("document.body.innerText.includes('Two connections. One controller.')")
        check_dialog_keyboard()
        capture("connection-guide")
        press_key("Escape")
        wait_for("!document.querySelector('[role=dialog]')")
        assert window.evaluate_js("document.activeElement.getAttribute('aria-label')") == "Connection guide"
        report["checks"].append("Connection guide traps focus, handles Escape and restores toolbar focus")

        click("Connect controller")
        wait_for("!document.body.innerText.includes('Scanning Bluetooth…')", seconds=20)
        assert window.evaluate_js(
            "document.body.innerText.includes('No supported controllers found')"
            "||!!document.querySelector('.device-result')||!!document.querySelector('.error-text')"
        )
        check_dialog_keyboard()
        capture("connection")
        click("Close devices")
        report["checks"].append("Ran real BLE discovery and displayed its result without connecting or writing")

        state = api("input")["data"]
        report["hardware"] = {"bleConnected": state["connected"], "xinputDetected": state["input"] is not None}
        if not state["connected"]:
            assert not api("apply", {"category": "vibration", "value": 30})["ok"]
            assert window.evaluate_js(
                "[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Apply changes').disabled"
            )
            report["checks"].append("Disconnected write rejected by UI and native API")

        window.evaluate_js(
            "window.pywebview.api.request('shell',{}).then(r=>window.__smokeRejected=!r.ok)"
        )
        wait_for("window.__smokeRejected === true")
        report["checks"].append("Unknown bridge operation rejected")
        click("Controllers")
        wait_for("document.querySelector('h1')?.textContent === 'Add a Controller'")
        time.sleep(0.2)
        window.evaluate_js(
            "window.__inputCalls=0;const original=window.pywebview.api.request;"
            "window.pywebview.api.request=function(operation,payload){"
            "if(operation==='input')window.__inputCalls++;return original.call(this,operation,payload)}"
        )
        time.sleep(0.25)
        assert window.evaluate_js("window.__inputCalls") == 0, "XInput polling continued in Controllers hub"
        report["checks"].append("XInput polling stops while the X20 workspace is hidden")
        assert not window.evaluate_js("document.body.innerText.includes('Explore X20 Pro read-only discovery')")
        assert api("select_model", {"model": "x20_pro"})["ok"]
        assert not api("bootstrap")["ok"]
        assert not api("apply", {"category": "vibration", "value": 30})["ok"]
        assert not api("import_profile", {"profile": saved_profile})["ok"]
        assert api("pro_hid")["ok"]
        report["checks"].append("Placeholder Pro link absent; Pro API remains read-only")
        click("Open X20 studio")
        wait_for("document.querySelector('.version')?.textContent.includes(" + json.dumps(__version__) + ")")
        assert api("bootstrap")["ok"]
        report["checks"].append("Switching back restores X20-only API access")
        original_url = window.evaluate_js("location.href")
        window.evaluate_js("location.href='https://example.invalid/'")
        time.sleep(0.5)
        assert window.evaluate_js("location.href") == original_url
        report["checks"].append("External navigation blocked in the native renderer")
        if lifecycle:
            assert lifecycle["tray"] is not None and lifecycle["tray"].visible
            window.destroy()
            time.sleep(0.3)
            assert not window.native.Visible, "Window close did not hide to tray"
            lifecycle["show"]()
            time.sleep(0.3)
            assert window.native.Visible, "Tray Open did not restore the window"
            report["checks"].append("Native tray close, hide and restore verified")
        report["passed"] = True
        result["code"] = 0
    except Exception:
        report["error"] = traceback.format_exc()
        result["code"] = 1
    finally:
        (directory / "smoke-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        if lifecycle:
            lifecycle["quit"]()
        else:
            window.destroy()
