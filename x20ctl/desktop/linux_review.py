"""Packaged GTK acceptance in a container/Xvfb display, never the host screen."""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path


def exercise(window, directory, result):
    output = Path(directory)
    output.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "renderer": "GTK 3 / WebKitGTK 4.1", "mode": "packaged ELF in container/Xvfb" if getattr(sys, "frozen", False) else "source GTK host in container/Xvfb",
              "hardwareAccess": False, "checks": []}

    def wait(expression, label, timeout=15):
        end = time.monotonic() + timeout
        while time.monotonic() < end:
            if window.evaluate_js(expression):
                report["checks"].append(label)
                return
            time.sleep(0.1)
        raise AssertionError(f"Timed out: {label}")

    def click(label):
        encoded = json.dumps(label)
        success = window.evaluate_js(f"""(() => {{
          const b = [...document.querySelectorAll('button')].find(b => b.getClientRects().length &&
            (b.getAttribute('aria-label') === {encoded} || b.textContent.trim() === {encoded}));
          if (!b || b.disabled) return false;
          b.click(); return true;
        }})()""")
        assert success, f"Missing/enabled button: {label}"

    try:
        assert window.events.loaded.wait(40), "Packaged frontend did not load"
        wait("!!window.pywebview?.api?.request", "native bridge initialized")
        # Xvfb has no window manager to focus the window. Focus only this
        # container display; the host screen is not mounted or accessed.
        from gi.repository import GLib
        from webview.platforms.gtk import BrowserView
        from Xlib import X, display

        def focus_review_window():
            BrowserView.instances[window.uid].webview.grab_focus()
            connection = display.Display()
            try:
                for native in connection.screen().root.query_tree().children:
                    if native.get_wm_name() == "x20ctl" and native.get_attributes().map_state == X.IsViewable:
                        native.set_input_focus(X.RevertToParent, X.CurrentTime)
                        connection.sync()
                        break
            finally:
                connection.close()
            return False

        GLib.idle_add(focus_review_window)
        wait("document.hasFocus() && document.documentElement.dataset.motionPaused === 'false'", "isolated native window has focus")
        window.evaluate_js("document.querySelector('.startup-skip')?.click()")
        wait("!document.querySelector('.startup-skip')", "startup intro can be skipped")
        wait("document.querySelectorAll('.controller-card').length === 4 && !document.querySelector('.editor')",
             "startup opens four-player landing")
        click("Add controller for Player 1")
        wait("!!document.querySelector('[role=dialog]')", "controller picker opens")
        click("Select X20")
        wait("document.querySelector('[data-player=\"1\"]').dataset.controller === 'x20'", "Player 1 assigned X20")
        click("Enter Studio for Player 1")
        wait("!!document.querySelector('.editor') && document.body.textContent.includes('Gameplay input')",
             "X20 Studio opens through real bridge")
        click("Back View")
        wait("document.querySelectorAll('.controller-macro-hotspot').length === 4", "X20 rear macro buttons render")
        click("Open M2 macro")
        wait("!!document.querySelector('.macro-studio')", "rear macro click opens editor")
        assert window.evaluate_js("!document.querySelector('.macro-studio .controller-view-switch') && !document.querySelector('.macro-studio .controller-photo') && document.querySelector('.macro-paddle.is-selected').textContent.includes('M2')"), "Macros must use rear view and selected M2"
        report["checks"].append("Macros keeps rear view only")
        click("Vibration")
        wait("document.querySelectorAll('.motor-wave-glow').length === 4", "X20 layered vibration renders")
        window.evaluate_js("[...document.querySelectorAll('.strength-presets button')].find(b => b.textContent.includes('Maximum')).click()")
        wait("getComputedStyle(document.querySelector('.motor-wave')).animationDuration === '0.4s'", "maximum vibration uses 0.4-second motion")
        time_before = window.evaluate_js("document.querySelector('.motor-wave').getAnimations()[0].currentTime")
        wait(f"document.querySelector('.motor-wave').getAnimations()[0].currentTime > {time_before}", "native WebKit vibration animation advances", timeout=8)
        window.evaluate_js("""(() => {
          const slider = document.querySelector('.vibration-controller-art').closest('.rumble-console')?.querySelector('input[type=range]')
            || document.querySelector('.editor input[type=range]');
          Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(slider,'30');
          slider.dispatchEvent(new Event('input',{bubbles:true}));
        })()""")
        wait("document.querySelector('.editor input[type=range]').value === '30' && document.querySelector('.editor .metal-slider').style.getPropertyValue('--range-ratio') === '0.3'", "shared slider updates value and fill in WebKit")
        report["checks"].append("X20 maximum strength preview")
        window.resize(1060, 760)
        time.sleep(0.5)
        geometry = window.evaluate_js("""(() => {
          const p = document.querySelector('.vibration-controller-art .controller-image-plane').getBoundingClientRect();
          const o = document.querySelector('.vibration-controller-art .controller-haptics-overlay').getBoundingClientRect();
          return {offset:Math.max(Math.abs(p.x-o.x),Math.abs(p.y-o.y),Math.abs(p.width-o.width),Math.abs(p.height-o.height)),
                  overflow:document.documentElement.scrollWidth > innerWidth + 2};
        })()""")
        assert geometry["offset"] < 0.2 and not geometry["overflow"], geometry
        report["checks"].append("compact native window keeps haptics aligned")
        click("Switch Controller")
        wait("document.querySelectorAll('.controller-card').length === 4", "return to players")
        click("Add controller for Player 2")
        wait("!!document.querySelector('[role=dialog]')", "Player 2 picker opens")
        click("Select X20 Pro")
        wait("document.querySelector('[data-player=\"2\"]').dataset.controller === 'x20_pro' && document.querySelector('[data-player=\"1\"]').dataset.controller === 'x20'",
             "independent Player 2 assignment")
        click("Enter Studio for Player 2")
        wait("!!document.querySelector('.model-workspace[data-model=x20_pro]')", "X20 Pro preview Studio opens")
        click("Back View")
        wait("document.querySelectorAll('.model-workspace[data-model=x20_pro] .controller-macro-hotspot').length === 6", "X20 Pro six rear macro buttons")
        click("Open M6 macro")
        wait("!!document.querySelector('.model-workspace[data-model=x20_pro] .macro-studio')", "X20 Pro M6 editor opens")
        click("Vibration")
        wait("document.querySelectorAll('.model-workspace[data-model=x20_pro] .motor-wave-glow').length === 8", "X20 Pro four layered haptics zones")
        click("Switch Controller")
        wait("document.querySelectorAll('.controller-card').length === 4", "final four-player landing")
        # Other preview models share the same native renderer and keep their own
        # artwork/motor geometry. Model changes remain local assignments.
        for model, name, slots, zones in (("x05", "X05", 0, 2), ("x05_pro", "X05 Pro", 2, 4),
                                          ("x10", "X10", 2, 2), ("d10", "D10", 2, 2)):
            label = "Add controller for Player 3" if model == "x05" else "Choose controller for Player 3"
            click(label)
            wait("!!document.querySelector('[role=dialog]')", f"{name} picker opens")
            click(f"Select {name}")
            wait(f"document.querySelector('[data-player=\"3\"]').dataset.controller === '{model}'", f"{name} assigned to Player 3")
            click("Enter Studio for Player 3")
            prefix = f".model-workspace[data-model={model}]"
            wait(f"!!document.querySelector('{prefix}')", f"{name} preview opens")
            click("Back View")
            wait(f"document.querySelectorAll('{prefix} .controller-macro-hotspot').length === {slots}", f"{name} rear controls match capabilities")
            click("Vibration")
            wait(f"document.querySelectorAll('{prefix} .motor-wave-glow').length === {zones * 2}", f"{name} layered haptics render")
            click("Switch Controller")
            wait("document.querySelectorAll('.controller-card').length === 4", f"{name} returns to players")
        current = window.get_current_url()
        window.evaluate_js("window.location.href = 'http://127.0.0.1:9/forbidden.html'")
        time.sleep(0.5)
        assert window.get_current_url() == current, "Foreign navigation escaped bundled entry"
        assert window.evaluate_js("document.querySelectorAll('.controller-card').length === 4")
        report["checks"].append("native navigation guard blocks foreign document")
        report["passed"] = True
    except Exception as exc:
        result["code"] = 1
        report["error"] = str(exc)
        try:
            report["motionState"] = window.evaluate_js("({paused:document.documentElement.dataset.motionPaused,focus:document.hasFocus(),hidden:document.hidden})")
            report["animations"] = window.evaluate_js("[...document.querySelectorAll('.motor-wave')].map(e=>({rect:e.getBoundingClientRect().toJSON(),play:getComputedStyle(e).animationPlayState,offset:getComputedStyle(e).strokeDashoffset,timeline:document.timeline.currentTime,animation:e.getAnimations().map(a=>({time:a.currentTime,start:a.startTime,state:a.playState,pending:a.pending}))}))")
            report["visibleButtons"] = window.evaluate_js("[...document.querySelectorAll('button')].map(b => ({label:b.getAttribute('aria-label'),text:b.textContent.trim(),disabled:b.disabled}))")
            report["body"] = window.evaluate_js("document.body.textContent.slice(0,10000)")
        except Exception:
            pass
    finally:
        (output / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        window.destroy()
