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
            "&&(document.querySelector('.editor')||document.querySelector('.model-workspace'))?.clientWidth>0"
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

        window.evaluate_js("window.scrollTo(0,0);document.querySelector('.metal-workspace')?.scrollTo(0,0)")
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

    def check_controller_geometry(model):
        # Independently measured control centers in the 1536x1024 source artwork.
        points = {
            "x20": {"Select L3 (Left Stick)": (400.67, 307.98), "Select R3 (Right Stick)": (932.75, 510.55), "Select B": (1202, 315)},
            "x20_pro": {"Select L3 (Left Stick)": (399, 283), "Select R3 (Right Stick)": (960, 460), "Select B": (1237, 290)},
        }
        metrics = window.evaluate_js(
            "(()=>{const canvas=document.querySelector('.mapping-controller-stage .controller-canvas');"
            "const image=canvas?.querySelector('img');if(!image)return null;"
            "const rect=e=>{const r=e.getBoundingClientRect();return {x:r.x,y:r.y,w:r.width,h:r.height}};"
            "return {canvas:rect(canvas),stage:rect(canvas.parentElement),panel:rect(canvas.closest('.mapping-canvas-panel')),image:rect(image),fit:getComputedStyle(image).objectFit,"
            "caps:[...canvas.querySelectorAll('.controller-stick-cap')].map(e=>Number(getComputedStyle(e).opacity)),"
            "controls:Object.fromEntries([...canvas.querySelectorAll('button')].map(e=>[e.getAttribute('aria-label'),rect(e)]))}})()"
        )
        assert metrics, "Controller art missing"
        art, frame = metrics["image"], metrics["canvas"]
        for container in (metrics["stage"], metrics["panel"]):
            assert art["x"] >= container["x"] and art["x"] + art["w"] <= container["x"] + container["w"] + 1, "Controller grips are clipped by the canvas panel"
        assert metrics["fit"] == "contain" and abs(art["w"] / art["h"] - 1.5) < .01
        assert art["w"] <= frame["w"] * .85, "Controller lacks comfortable breathing room"
        assert all(value >= .98 for value in metrics["caps"]) and len(metrics["caps"]) == 2
        for label, (x, y) in points[model].items():
            rect = metrics["controls"][label]
            expected_x, expected_y = art["x"] + x / 1536 * art["w"], art["y"] + y / 1024 * art["h"]
            assert abs(rect["x"] + rect["w"] / 2 - expected_x) < 2, label
            assert abs(rect["y"] + rect["h"] / 2 - expected_y) < 2, label
        report["checks"].append(f"{model} art is contained and opaque stick/button overlays align at {frame['w']:.0f}px")

    try:
        wait_for(
            "typeof window.pywebview?.api?.request === 'function' && !!document.querySelector('h1')"
        )
        wait_for("!document.body.innerText.includes('Desktop connection unavailable')")
        wait_for("!!document.querySelector('.rebranded-hub')&&!document.querySelector('.startup-intro')")
        assert not window.evaluate_js("!!document.querySelector('[role=alert]')"), "Unexpected startup error"
        assert window.evaluate_js("document.querySelector('h1').textContent") == "Controller zone"
        assert window.evaluate_js("[...document.querySelectorAll('.controller-card')].every(e=>!e.dataset.controller)")
        assert not window.evaluate_js("!!document.querySelector('.rebranded-workspace')")
        capture("controllers")
        window.resize(1060, 760)
        time.sleep(0.4)
        assert window.evaluate_js("document.documentElement.scrollWidth <= document.documentElement.clientWidth + 2")
        capture("controllers-compact")
        window.resize(1400, 940)
        time.sleep(0.4)
        wait_for("document.querySelectorAll('.controller-card').length===4&&!document.querySelector('.startup-intro')")
        assert not window.evaluate_js("!!document.querySelector('[aria-label=Appearance]')")
        report["checks"].append("Four-slot Controllers entry opened with fixed rebrand palette and no Theme Studio")
        click("Add controller for Player 1")
        wait_for("document.querySelector('[role=dialog] h2')?.textContent === 'Switch Controller'")
        assert window.evaluate_js("document.querySelectorAll('.controller-switch-row').length") == 6
        click("Select X20", "document.querySelector('[role=dialog]')")
        wait_for("document.querySelector('[data-player=\"1\"]')?.dataset.controller==='x20'")
        assert window.evaluate_js("document.querySelector('[data-player=\"2\"]').dataset.controller") == ""
        capture("player-one-assigned")
        click("Enter Studio for Player 1")
        wait_for("document.querySelector('h1')?.textContent === 'Buttons'")
        check_controller_geometry("x20")
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
        assert window.evaluate_js("!!document.querySelector('.mapping-controller-stage .controller-canvas')"), "Visible controller control layer missing"
        assert window.evaluate_js(
            "(()=>{const image=document.querySelector('.mapping-controller-stage .controller-photo');"
            "const stick=document.querySelector('.mapping-controller-stage .controller-stick');"
            "if(!image||!stick)return false;const box=image.getBoundingClientRect();"
            "const scale=Math.min(box.width/1536,box.height/1024);"
            "const expectedX=box.left+(box.width-1536*scale)/2+400.67*scale;"
            "const expectedY=box.top+(box.height-1024*scale)/2+307.98*scale;"
            "const rect=stick.getBoundingClientRect();"
            "return Math.hypot((rect.left+rect.right)/2-expectedX,(rect.top+rect.bottom)/2-expectedY)<5})()"
        ), "Left HTML thumbstick is not centered on the photographed stick"
        assert window.evaluate_js(
            "(()=>{const button=document.querySelector('.mapping-controller-stage [aria-label=\"Select B\"]');"
            "if(!button)return false;button.dispatchEvent(new MouseEvent('click',{bubbles:true}));return true})()"
        ), "Clicking a pictured button did not select it"
        wait_for("document.querySelector('.mapping-selected-control .mapping-key-large')?.textContent==='B'")
        click("Keyboard preview")
        assert window.evaluate_js("document.body.innerText.includes('Keyboard preview active')")
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keydown',{key:'w',bubbles:true}))")
        wait_for("document.querySelector('.mapping-controller-stage .controller-canvas')?.getAttribute('data-left-y')==='-1.000'")
        assert window.evaluate_js(
            "document.querySelector('.mapping-controller-stage .controller-stick-cap')?.style.transform"
            "==='translate(0%, -18%)'"
        ), "WASD state did not visibly move the opaque photographic stick cap"
        capture("buttons-keyboard-preview")
        assert window.evaluate_js("document.querySelector('.connection-lights')?.textContent.includes('Not Connected')"), "Preview impersonated connected hardware"
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keyup',{key:'w',bubbles:true}))")
        wait_for("document.querySelector('.mapping-controller-stage .controller-canvas')?.getAttribute('data-left-y')==='0.000'")
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
                    "(()=>{const overlay=document.querySelector('.vibration-controller-frame .controller-canvas');"
                    "return !!overlay&&overlay.querySelectorAll('.controller-motor').length===2"
                    "&&overlay.getAttribute('data-motor-power')==='70'})()"
                ), "Vibration page is missing its two-grip visual preview"
                assert window.evaluate_js(
                    "(()=>{const image=document.querySelector('.vibration-controller-frame img');"
                    "const rings=document.querySelectorAll('.vibration-controller-frame .controller-motor');"
                    "if(!image||rings.length!==2)return false;"
                    "const art=image.getBoundingClientRect(),left=rings[0].getBoundingClientRect(),right=rings[1].getBoundingClientRect();"
                    "return left.left>=art.left+10&&right.right<=art.right-10})()"
                ), "Vibration contours extend beyond the controller art"
                assert window.evaluate_js(
                    "matchMedia('(prefers-reduced-motion: reduce)').matches"
                    "||(()=>{const zone=document.querySelector('.controller-motor');"
                    "return getComputedStyle(zone,'::before').animationName!=='none'"
                    "&&parseFloat(getComputedStyle(zone,'::before').animationDuration)>0"
                    "&&getComputedStyle(document.querySelector('.controller-photo')).animationName==='none'})()"
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
            check_controller_geometry("x20")
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
        assert window.evaluate_js("document.querySelector('.haptics-master .metal-slider').style.getPropertyValue('--range-ratio')") == "0"
        wait_for("document.querySelector('.vibration-controller-frame .controller-canvas')?.getAttribute('data-motor-power')==='0'")
        assert window.evaluate_js(
            "!document.querySelector('.vibration-controller-frame .controller-motor')"
        ), "Zero vibration must hide the grip effect"
        click("Gentle")
        assert window.evaluate_js("document.querySelector('.large-value').textContent") == "30%"
        assert window.evaluate_js("document.querySelector('.haptics-master .metal-slider').style.getPropertyValue('--range-ratio')") == "0.3"
        wait_for("document.querySelector('.vibration-controller-frame .controller-canvas')?.getAttribute('data-motor-power')==='30'")
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
        click("Switch Controller", "document.querySelector('.chassis-header')")
        wait_for("document.querySelector('h1')?.textContent === 'Controller zone'")
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
        click("Add controller for Player 2")
        check_dialog_keyboard()
        capture("picker-player-two")
        click("Select X20 Pro", "document.querySelector('[role=dialog]')")
        wait_for("document.querySelector('[data-player=\"2\"]')?.dataset.controller==='x20_pro'")
        assert window.evaluate_js("document.querySelector('[data-player=\"1\"]').dataset.controller") == "x20"
        capture("players-one-and-two-assigned")
        click("Enter Studio for Player 2")
        wait_for("document.querySelector('.model-workspace')?.getAttribute('data-model')==='x20_pro'")
        check_controller_geometry("x20_pro")
        capture("pro-buttons")
        click("Keyboard preview")
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keydown',{key:'d',bubbles:true}))")
        wait_for("document.querySelector('.mapping-controller-stage .controller-canvas')?.dataset.leftX==='1.000'")
        window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keyup',{key:'d',bubbles:true}))")
        click("Keyboard preview")
        window.resize(1060, 760)
        time.sleep(.5)
        check_controller_geometry("x20_pro")
        check_layout("X20 Pro Buttons", "1060x760")
        capture("pro-buttons-compact")
        window.resize(1400, 940)
        time.sleep(.4)
        assert not api("bootstrap")["ok"]
        assert not api("apply", {"category": "vibration", "value": 30})["ok"]
        assert not api("import_profile", {"profile": saved_profile})["ok"]
        assert not api("pro_hid")["ok"]
        assert not api("pro_scan")["ok"]
        assert not api("controller_scan")["ok"]
        assert window.evaluate_js("[...document.querySelectorAll('.model-workspace button')].find(e=>e.textContent.trim()==='Apply to controller').disabled")
        select_value("Remap A", "Y")
        click("Response curves", "document.querySelector('.model-workspace .chassis-nav')")
        input_value('#curve-inner', '15')
        assert window.evaluate_js("document.querySelector('#curve-inner').value") == "15"
        click("Macros", "document.querySelector('.model-workspace .chassis-nav')")
        assert window.evaluate_js("document.querySelectorAll('.model-workspace .macro-paddle').length") == 6
        click("M6")
        click("Add Step")
        capture("pro-macros-six-slots")
        click("Vibration", "document.querySelector('.model-workspace .chassis-nav')")
        assert window.evaluate_js("document.querySelectorAll('.model-workspace .controller-motor').length") == 4
        input_value('[aria-label="Vibration strength"]', '30')
        assert window.evaluate_js("document.querySelector('.model-feature .controller-canvas').dataset.motorPower") == "30"
        assert window.evaluate_js(
            "(()=>{const art=document.querySelector('.model-haptics-body .controller-canvas').getBoundingClientRect();"
            "const controls=document.querySelector('.model-haptics-body .haptics-master').getBoundingClientRect();"
            "return Math.abs((art.top+art.bottom)/2-(controls.top+controls.bottom)/2)<5})()"
        ), "Pro vibration art and controls must share the same row"
        window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}))")
        assert window.evaluate_js("getComputedStyle(document.querySelector('.metal-slider-fill')).transitionDuration") == "0s"
        window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').dispatchEvent(new PointerEvent('pointerup',{bubbles:true}))")
        click("Off")
        assert window.evaluate_js("document.querySelectorAll('.model-workspace .controller-motor').length") == 0
        click("Standard")
        capture("pro-vibration")
        window.resize(1060, 760)
        time.sleep(.4)
        check_layout("X20 Pro Vibration", "1060x760")
        capture("pro-vibration-compact")
        window.resize(1400, 940)
        time.sleep(.4)
        report["checks"].append("Pro draft mappings, curves, six macro slots, shared sliders and four localized motor visuals work without device access")
        click("Switch Controller", "document.querySelector('.model-workspace .chassis-header')")
        wait_for("!!document.querySelector('.controller-hub')")
        click("Choose controller for Player 2")
        click("Select X20", "document.querySelector('[role=dialog]')")
        click("Enter Studio for Player 2")
        wait_for("document.querySelector('.studio-player-label')?.textContent==='Player 2'")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Setup name\"]').value") == "Untitled setup"
        click("Switch Controller", "document.querySelector('.chassis-header')")
        wait_for("!!document.querySelector('.controller-hub')")
        click("Choose controller for Player 2")
        click("Select X20 Pro", "document.querySelector('[role=dialog]')")
        click("Enter Studio for Player 2")
        wait_for("document.querySelector('.model-workspace')?.dataset.page==='rumble'")
        click("Buttons", "document.querySelector('.chassis-nav')")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Remap A\"]').value") == "Y"
        click("Switch Controller", "document.querySelector('.chassis-header')")
        wait_for("!!document.querySelector('.controller-hub')")
        assert window.evaluate_js("document.querySelector('[data-player=\"1\"]').dataset.controller") == "x20"
        assert window.evaluate_js("document.querySelector('[data-player=\"2\"]').dataset.controller") == "x20_pro"
        for player, model in [(3, "X20"), (4, "X20 Pro")]:
            click(f"Add controller for Player {player}")
            click(f"Select {model}", "document.querySelector('[role=dialog]')")
            click(f"Enter Studio for Player {player}")
            wait_for(f"document.querySelector('.studio-player-label')?.textContent==='Player {player}'")
            click("Vibration", "document.querySelector('.chassis-nav')")
            assert window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').value") == "70"
            input_value('[aria-label="Vibration strength"]', '0')
            click("Switch Controller", "document.querySelector('.chassis-header')")
            wait_for("!!document.querySelector('.controller-hub')")
        capture("four-players-assigned")
        report["checks"].append("All four cards assign and enter their own Studio; Studio switches modify only their player and preserve separate drafts")
        click("Enter Studio for Player 1")
        wait_for("document.querySelector('.version')?.textContent.includes(" + json.dumps(__version__) + ")")
        click("Vibration", "document.querySelector('.chassis-nav')")
        assert window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').value") == "30"
        assert api("bootstrap")["ok"]
        report["checks"].append("Switching back restores X20-only API access")
        original_url = window.evaluate_js("location.href")
        window.evaluate_js("location.href='https://example.invalid/'")
        time.sleep(0.5)
        assert window.evaluate_js("location.href") == original_url
        report["checks"].append("External navigation blocked in the native renderer")
        window.evaluate_js("setTimeout(()=>location.reload(),0);true")
        wait_for("!!document.querySelector('.controller-hub')&&document.querySelectorAll('.controller-card').length===4&&!document.querySelector('.startup-intro')")
        assert window.evaluate_js("[...document.querySelectorAll('.controller-card')].every(e=>!e.dataset.controller)")
        assert not window.evaluate_js("!!document.querySelector('.rebranded-workspace')")
        capture("fresh-launch-players")
        report["checks"].append("Fresh app reload always opens four unassigned players, never a Studio")
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
