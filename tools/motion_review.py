"""Isolated, read-only native motion/performance review; no simulated hardware."""
import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--idle', type=int, default=120)
parser.add_argument('--baseline', action='store_true')
parser.add_argument('--review-seconds', type=int, default=0)
args = parser.parse_args()
output = Path(args.output).resolve()
output.mkdir(parents=True, exist_ok=True)

def cpu_sample():
    script = '''$rows = Get-CimInstance Win32_Process | Select-Object ProcessId,ParentProcessId,Name
    $ids = [System.Collections.Generic.HashSet[int]]::new()
    [void]$ids.Add(ROOT_PID)
    do {
        $added = $false
        foreach ($row in $rows) {
            if ($ids.Contains([int]$row.ParentProcessId) -and $ids.Add([int]$row.ProcessId)) { $added = $true }
        }
    } while ($added)
    Get-Process -Id @($ids) -ErrorAction SilentlyContinue | Select-Object Id,ProcessName,CPU,WorkingSet64 | ConvertTo-Json -Compress
    '''.replace('ROOT_PID', str(os.getpid()))
    data = json.loads(subprocess.check_output(['powershell', '-NoProfile', '-Command', script], text=True))
    return {str(row['Id']): row for row in (data if isinstance(data, list) else [data]) if row['ProcessName'] not in ('powershell', 'conhost')}

def exercise(window, directory, result, lifecycle):
    report = {'passed': False, 'baseline': args.baseline, 'screenshots': [], 'checks': []}
    def wait(expression):
        deadline = time.monotonic() + 25
        while time.monotonic() < deadline:
            if window.evaluate_js(expression): return
            time.sleep(.1)
        raise AssertionError(expression)
    def capture(name):
        from System import Action
        from System.IO import FileStream, FileMode
        from Microsoft.Web.WebView2.Core import CoreWebView2CapturePreviewImageFormat
        pending = {}
        def begin():
            pending['stream'] = FileStream(str(output / (name + '.png')), FileMode.Create)
            pending['task'] = window.native.browser.webview.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, pending['stream'])
        window.native.Invoke(Action(begin))
        try: pending['task'].GetAwaiter().GetResult()
        finally: pending['stream'].Close()
        report['screenshots'].append(name)
    def click(label, scope='document'):
        assert window.evaluate_js("(()=>{const root=" + scope + ";const b=[...root.querySelectorAll('button')].find(e=>(e.getAttribute('aria-label')===" + json.dumps(label) + "||e.textContent.trim()===" + json.dumps(label) + ")&&!e.disabled&&e.getClientRects().length);if(!b)return false;b.click();return true})()"), label
        time.sleep(.35)
    def media(reduced):
        from System import Action
        pending = {}
        def begin():
            pending['task'] = window.native.browser.webview.CoreWebView2.CallDevToolsProtocolMethodAsync('Emulation.setEmulatedMedia', json.dumps({'features': [{'name': 'prefers-reduced-motion', 'value': 'reduce' if reduced else 'no-preference'}]}))
        window.native.Invoke(Action(begin))
        pending['task'].GetAwaiter().GetResult()
        time.sleep(.35)
    try:
        wait("document.querySelectorAll('.controller-card').length===4&&!document.querySelector('.startup-intro')")
        window.evaluate_js("window.__reviewFocusLog=[];['focus','blur'].forEach(kind=>window.addEventListener(kind,()=>setTimeout(()=>window.__reviewFocusLog.push({kind,visible:document.visibilityState,paused:document.documentElement.dataset.motionPaused}),100)));true")
        time.sleep(1)
        capture('landing')
        report['checks'].append('Actual Windows WebView2 landing rendered')
        report['start_motion'] = window.evaluate_js("({focused:document.hasFocus(),visible:document.visibilityState,paused:document.documentElement.dataset.motionPaused,animations:document.getAnimations().length})")
        start = time.monotonic()
        before = cpu_sample()
        for remaining in range(args.idle, 0, -10):
            print(f"Native idle: {remaining}s remaining", flush=True)
            time.sleep(min(10, remaining))
        after = cpu_sample()
        elapsed = time.monotonic() - start
        cpu = sum(max(0, row['CPU'] - before[pid]['CPU']) for pid, row in after.items() if pid in before)
        report['idle'] = {'seconds': elapsed, 'cpu_seconds': cpu, 'one_core_percent': cpu / elapsed * 100, 'working_set_mb': sum(row['WorkingSet64'] for row in after.values()) / 1024**2, 'processes': after}
        capture('landing-idle')
        report['checks'].append(f'{args.idle}-second idle capture completed')
        window.minimize()
        time.sleep(1)
        report['minimized_motion'] = window.evaluate_js("({focused:document.hasFocus(),visible:document.visibilityState,paused:document.documentElement.dataset.motionPaused,animations:document.getAnimations().map(a=>({name:a.animationName,state:a.playState}))})")
        if not args.baseline:
            assert report['minimized_motion']['paused'] == 'true'
            assert all(a['state'] == 'paused' for a in report['minimized_motion']['animations'])
            report['checks'].append('Native minimize pauses all perpetual decorative animation')
        start = time.monotonic()
        before = cpu_sample()
        time.sleep(20)
        after = cpu_sample()
        elapsed = time.monotonic() - start
        cpu = sum(max(0, row['CPU'] - before[pid]['CPU']) for pid, row in after.items() if pid in before)
        report['minimized'] = {'seconds': elapsed, 'cpu_seconds': cpu, 'one_core_percent': cpu / elapsed * 100}
        window.restore()
        time.sleep(1)
        capture('landing-restored')
        if not args.baseline:
            wait("document.documentElement.dataset.motionPaused==='false'")
            report['checks'].append('Native restore resumes ambient motion')
            for player, model in [(1, 'X20'), (2, 'X20 Pro')]:
                click(f'Add controller for Player {player}')
                capture(f'picker-{model}')
                click(f'Select {model}')
                capture(f'assigned-{model}')
                click(f'Enter Studio for Player {player}')
                wait(f"document.querySelector('.studio-player-label')?.textContent==='Player {player}'")
                for label in ['Buttons', 'Response curves', 'Macros', 'Vibration', 'Power & device', 'Input tester', 'Saved setups'] + (['Lighting', 'Display'] if model == 'X20 Pro' else []):
                    click(label, "document.querySelector('.chassis-nav')")
                    assert window.evaluate_js("(()=>{const nav=document.querySelector('.chassis-nav'),b=nav.querySelector('[aria-current=page]'),light=nav.querySelector('.nav-active-light');const a=b.getBoundingClientRect(),c=light.getBoundingClientRect();return Math.abs(a.top-c.top)<2&&Math.abs(a.height-c.height)<2})()"), f'{model} {label} navigation light alignment'
                    assert window.evaluate_js("document.documentElement.scrollWidth<=innerWidth+2"), f'{model} {label} overflow'
                    capture(f'{model}-{label}')
                click('Buttons', "document.querySelector('.chassis-nav')")
                click('Keyboard preview')
                window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keydown',{key:'w',bubbles:true}))")
                wait("document.querySelector('.mapping-controller-stage .controller-canvas').dataset.leftY==='-1.000'")
                assert window.evaluate_js("(()=>{const cap=document.querySelector('.mapping-controller-stage .controller-stick-cap');return cap.style.transform==='translate(0%, -18%)'&&getComputedStyle(cap).transitionDuration==='0s'&&getComputedStyle(cap).opacity==='1'})()")
                capture(f'{model}-local-stick-preview')
                window.evaluate_js("window.dispatchEvent(new KeyboardEvent('keyup',{key:'w',bubbles:true}))")
                click('Keyboard preview')
                click('Vibration', "document.querySelector('.chassis-nav')")
                window.evaluate_js("window.__motionPage=document.querySelector('.motion-page')")
                assert window.evaluate_js("document.querySelectorAll('.controller-motor').length===" + ('4' if model == 'X20 Pro' else '2'))
                input_selector = '[aria-label="Vibration strength"]'
                window.evaluate_js("(()=>{const e=document.querySelector(" + json.dumps(input_selector) + ");Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(e,'30');e.dispatchEvent(new Event('input',{bubbles:true}));e.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}));return true})()")
                time.sleep(.1)
                assert window.evaluate_js("getComputedStyle(document.querySelector('.metal-slider-fill')).transitionDuration==='0s'&&getComputedStyle(document.querySelector('.metal-slider-thumb')).transitionDuration==='0s'")
                assert window.evaluate_js("window.__motionPage===document.querySelector('.motion-page')"), 'Editing must not restart page entrance'
                window.evaluate_js("document.querySelector('[aria-label=\"Vibration strength\"]').dispatchEvent(new PointerEvent('pointerup',{bubbles:true}))")
                media(True)
                assert window.evaluate_js("matchMedia('(prefers-reduced-motion: reduce)').matches")
                assert window.evaluate_js("document.getAnimations().length===0&&[...document.querySelectorAll('.motor-wave')].every(e=>getComputedStyle(e).animationName==='none')&&getComputedStyle(document.querySelector('.motion-page')).opacity==='1'")
                capture(f'{model}-reduced-motion')
                click('Switch Controller', "document.querySelector('.chassis-header')")
                wait("!!document.querySelector('.controller-hub')")
                click(f'Choose controller for Player {player}')
                assert window.evaluate_js("getComputedStyle(document.querySelector('.metal-dialog')).animationName==='none'")
                capture(f'{model}-reduced-picker')
                window.evaluate_js("document.activeElement.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true,cancelable:true}))")
                time.sleep(.2)
                assert window.evaluate_js("!document.querySelector('[role=dialog]')")
                click(f'Enter Studio for Player {player}')
                media(False)
                window.resize(1060, 760)
                time.sleep(.5)
                capture(f'{model}-compact-vibration')
                assert window.evaluate_js("document.documentElement.scrollWidth<=innerWidth+2")
                window.resize(1400, 940)
                time.sleep(.4)
                click('Switch Controller', "document.querySelector('.chassis-header')")
                wait("!!document.querySelector('.controller-hub')")
                report['checks'].append(f'{model}: all sections, moving navigation, exact opaque local stick preview, direct sliders, resize, reduced motion and picker verified')
            capture('two-players-assigned')
            click('Enter Studio for Player 1')
            click('Vibration', "document.querySelector('.chassis-nav')")
            report['checks'].append('Presentation checks do not simulate a hardware connection or hardware input')
        for remaining in range(args.review_seconds, 0, -10):
            print(f'Interactive native review: {remaining}s remaining', flush=True)
            time.sleep(min(10, remaining))
        report['focus_events'] = window.evaluate_js('window.__reviewFocusLog')
        report['passed'] = True
        result['code'] = 0
    except Exception:
        import traceback
        report['error'] = traceback.format_exc()
        result['code'] = 1
    finally:
        (output / 'motion-report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        lifecycle['quit']()

from x20ctl.desktop import launcher, smoke
smoke.exercise = exercise
sys.argv = ['app.py', '--smoke-test', str(output)]
sys.exit(launcher.main())
