"""Builds the overhauled Pulse 1920x1080 H.264 yuv420p silent intro film (Brand/themes/pulse/intro.mp4):
"High-Impact 3D Sonic-Boom & Heartbeat Forge" — Supersonic Cardiac ECG trace -> 3D perspective cymatic grid &
volumetric light shafts -> 96-bar radial spectrum analyzer & counter-rotating gyroscopic HUD rings ->
3 prismatic bass-drop shockwaves forging the brushed-platinum Pulse mark with anamorphic star-spark lens flares.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path
import imageio_ffmpeg
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont
from playwright.sync_api import sync_playwright

THEMES_DIR = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\previews\console-concepts\themes")
sys.path.insert(0, str(THEMES_DIR))
import intros as I
from kit import page_html
from logos import DST, SIZE, uri as logo_uri

WT = Path(r"C:\Users\amjad\Desktop\Study\Projects\Personal\x20ctl\worktree-gemini")
BRAND_PULSE = WT / "desktop-dotnet" / "X20Ctl.Product" / "Brand" / "themes" / "pulse"
WORK = WT / "artifacts" / "pulse-intro-work"
REVIEW_DIR = WT / "artifacts" / "review-pulse"
BRAIN_DIR = Path(r"C:\Users\amjad\.gemini\antigravity\brain\d753778d-ab43-44ed-ac83-50d30c90bd23")

FPS = 30
SCALE = 1.2  # 1600x900 stage -> 1920x1080 film
CX, CY, W = 800, 430, 560
CUT = 2.74
LR = SIZE[1] / SIZE[0]


def build_pulse_scene():
    h = W * LR
    R = 385
    N = 3400
    rnd = np.random.default_rng(7)
    names = ["wing-l", "wing-r", "body-l", "body-r"]
    al = I.composite("pulse", names).split()[3]
    a, e = np.array(al), np.array(al.filter(ImageFilter.FIND_EDGES))
    ys, xs = np.nonzero(a > 128)
    eys, exs = np.nonzero(e > 70)
    ne = int(N * 0.58)
    ie, fi = rnd.integers(0, len(exs), ne), rnd.integers(0, len(xs), N - ne)
    lx = np.concatenate([exs[ie], xs[fi]]) + rnd.uniform(-0.5, 0.5, N)
    ly = np.concatenate([eys[ie], ys[fi]]) + rnd.uniform(-0.5, 0.5, N)
    logo = np.stack([CX - W / 2 + lx / SIZE[0] * W, CY - h / 2 + ly / SIZE[1] * h], 1)

    def chladni(n, m, sign, eps):
        out = []
        while len(out) < N:
            p = rnd.uniform(-1, 1, (N * 6, 2))
            p = p[(p ** 2).sum(1) < 0.97]
            x, y = p[:, 0], p[:, 1]
            f = np.cos(n * np.pi * x) * np.cos(m * np.pi * y) + sign * np.cos(m * np.pi * x) * np.cos(n * np.pi * y)
            out.extend(p[np.abs(f) < eps].tolist())
        return np.array(out[:N]) * R + [CX, CY]

    r0 = np.sqrt(rnd.uniform(0.01, 0.96, N)) * R
    t0 = rnd.uniform(0, 2 * np.pi, N)
    scatter = np.stack([CX + r0 * np.cos(t0), CY + r0 * np.sin(t0)], 1)
    stages = [scatter, chladni(1, 4, -1, 0.048), chladni(2, 5, 1, 0.034), logo]
    for i, s in enumerate(stages):
        stages[i] = s[np.argsort(np.arctan2(s[:, 1] - CY, s[:, 0] - CX))]
    P = [[round(float(v), 1) for v in s.flatten()] for s in stages]

    full = logo_uri("pulse", "full")
    sx, sy = I.centre_of("pulse", "spark", CX, CY, W)

    css = I.BASE + f"""
#bg{{position:absolute;inset:0;background:
 radial-gradient(58% 48% at 50% 36%,rgba(56,136,255,.20),transparent 68%),
 radial-gradient(45% 42% at 18% 68%,rgba(216,120,255,.08),transparent 65%),
 radial-gradient(40% 36% at 82% 22%,rgba(255,208,136,.08),transparent 65%),
 linear-gradient(180deg,#091428 0%,#050b16 52%,#020409 100%)}}
#reactor{{position:absolute;left:{CX - R - 28}px;top:{CY - R - 28}px;width:{2 * R + 56}px;height:{2 * R + 56}px;border-radius:50%;
 background:radial-gradient(closest-side,rgba(48,110,210,.26),rgba(12,24,48,.62) 82%,rgba(120,180,255,.0) 99%);
 box-shadow:inset 0 0 0 1.5px rgba(124,226,255,.32),inset 0 0 70px rgba(0,0,0,.7),0 0 40px rgba(56,150,255,.22)}}
#sand{{position:absolute;left:0;top:0;filter:drop-shadow(0 0 5px rgba(124,226,255,.75))}}
#fx{{position:absolute;left:0;top:0;overflow:visible}}
#lg{{position:absolute;inset:0;transform-origin:0 0}}
#lg img{{opacity:1!important}}
#set{{position:absolute;inset:0}}
#ex,#e1,#e2{{position:absolute;inset:0;transform-origin:{CX}px {CY}px;opacity:0}}
#sk{{position:absolute;inset:0;opacity:0}}
#sheen{{position:absolute;left:{CX - W / 2:.0f}px;top:{CY - h / 2:.0f}px;width:{W}px;height:{h:.0f}px;-webkit-mask:url({full}) 0 0/{W}px {h:.0f}px;mask:url({full}) 0 0/{W}px {h:.0f}px;opacity:0}}
#sg{{position:absolute;width:480px;height:480px;border-radius:50%;background:radial-gradient(closest-side,rgba(255,245,220,.96),rgba(240,212,156,.42) 34%,rgba(124,226,255,.18) 58%,transparent 75%);opacity:0}}
#sxh{{position:absolute;left:{sx - 340}px;top:{sy - 2}px;width:680px;height:4px;border-radius:50%;background:linear-gradient(90deg,transparent,rgba(124,226,255,.75) 28%,rgba(255,250,235,1) 50%,rgba(255,140,198,.75) 72%,transparent);filter:blur(.6px);opacity:0}}
#sxv{{position:absolute;left:{sx - 2}px;top:{sy - 170}px;width:4px;height:340px;border-radius:50%;background:linear-gradient(180deg,transparent,rgba(255,245,220,.95) 50%,transparent);filter:blur(.6px);opacity:0}}
"""
    body = (
        '<div id=scene class=a style="inset:0"><div id=bg></div>'
        '<svg id=fx width="1600" height="900"><g id=shafts></g><g id=grid3d></g><g id=gyro></g><g id=spec></g><g id=waves></g><g id=rings></g><g id=res></g></svg>'
        '<div id=reactor></div><canvas id=sand width=1600 height=900></canvas></div>'
        + f'<div id=lg><div id=set>{I.parts("pulse", names, CX, CY, W)}</div><i id=sheen></i>'
        + f'<div id=e2>{I.parts("pulse", ["extra-l", "extra-r"], CX, CY, W)}</div><div id=e1>{I.parts("pulse", ["extra-l", "extra-r"], CX, CY, W)}</div>'
        + f'<div id=ex>{I.parts("pulse", ["extra-l", "extra-r"], CX, CY, W)}</div>'
        + f'<i id=sg style="left:{sx - 240}px;top:{sy - 240}px"></i><i id=sxh></i><i id=sxv></i>'
        + f'<div id=sk>{I.parts("pulse", ["spark"], CX, CY, W)}</div></div>'
    )
    js = r"""
const P=%P%, N=%N%, CX=%CX%, CY=%CY%, R=%R%;
const T=[.40,.92,1.44], MOVE=.45;
const ctx=$('sand').getContext('2d');
const RN=rng(19), G=[...Array(N)].map(()=>({
 d:RN()*.08, r:.85+RN()*1.25, a:.62+RN()*.38, p:RN()*6.28, q:RN()*6.28,
 c:RN()<.10?'#f0d49c':(RN()<.22?'#ff8cc6':(RN()<.52?'#7ce2ff':'#f2f6fc'))
}));
const NS='http://www.w3.org/2000/svg';
function svgEl(tag,parent,attrs){
 const e=document.createElementNS(NS,tag);
 for(const k in attrs)e.setAttribute(k,attrs[k]);
 $(parent).appendChild(e);return e;
}

// 1. Volumetric light shafts & 3D perspective cymatic floor grid (matching PulseKit.VoidBackdrop)
const SHAFTS=[
 [180,-40,380,620,740,900,'rgba(104,176,255,.065)'],
 [680,-40,920,980,1280,900,'rgba(255,136,200,.045)'],
 [1020,-40,1280,760,1520,900,'rgba(255,212,148,.055)']
].map(([x0,y0,x1,bx0,bx1,y1,col])=>svgEl('polygon','shafts',{points:`${x0},${y0} ${x1},${y0} ${bx1},${y1} ${bx0},${y1}`,fill:col}));

const GRID_LINES=[];
for(let i=-12;i<=12;i++){
 const major=i%3===0;
 GRID_LINES.push(svgEl('line','grid3d',{
  x1:CX+i*18,y1:488,x2:CX+i*135,y2:900,
  stroke:major?'rgba(168,204,255,.24)':'rgba(104,168,255,.14)',
  'stroke-width':major?1.0:0.6,opacity:1-Math.abs(i)/14
 }));
}
const GRID_ELLIPSES=[];
for(let r=1;r<=8;r++){
 const t=Math.pow(r/8,1.65), y=494+t*382, rx=220+t*1180, ry=18+t*115;
 const attrs={cx:CX,cy:y,rx,ry,fill:'none',stroke:r%2===0?'rgba(168,204,255,.24)':'rgba(104,168,255,.14)','stroke-width':r%2===0?1.1:0.7};
 if(r%3===0)attrs['stroke-dasharray']='4 6';
 GRID_ELLIPSES.push(svgEl('ellipse','grid3d',attrs));
}

// 2. 72-Tick Gyroscopic HUD Ring & 80-Bar Radial Frequency Spectrum Analyzer
const GYRO_TICKS=[...Array(72)].map((_,i)=>{
 const cardinal=i%18===0, major=i%6===0;
 return {i,cardinal,major,e:svgEl('line','gyro',{
  stroke:cardinal?'#f0d49c':(major?'#dce9ff':'#7ce2ff'),
  'stroke-width':cardinal?2.2:(major?1.4:0.9),
  'stroke-linecap':'round'
 })};
});
const SPEC_BARS=[...Array(80)].map((_,i)=>{
 const col=i%6===0?'#f0d49c':(i%3===0?'#ff8cc6':'#7ce2ff');
 return {i,e:svgEl('line','spec',{stroke:col,'stroke-width':2.4,'stroke-linecap':'round'})};
});

// 3. Full-Screen Multi-Sine & Cardiac ECG Waveform Traces
const SINE1=svgEl('path','waves',{fill:'none',stroke:'rgba(255,140,198,.42)','stroke-width':1.5});
const SINE2=svgEl('path','waves',{fill:'none',stroke:'rgba(240,212,156,.36)','stroke-width':1.4});
const ECG_GLOW=svgEl('path','waves',{fill:'none',stroke:'rgba(86,184,255,.65)','stroke-width':5,'stroke-linejoin':'round',style:'filter:blur(6px)'});
const ECG_CORE=svgEl('path','waves',{fill:'none',stroke:'#ffffff','stroke-width':2.1,'stroke-linejoin':'round'});

// 4. Prismatic Sonic-Boom Shockwave Rings & Paired Resonance Wings
const RINGS=T.map(()=>[0,1,2].map(j=>svgEl('circle','rings',{
 cx:CX,cy:CY,fill:'none',
 stroke:j===0?'#7ce2ff':(j===1?'#ff8cc6':'#f0d49c'),
 'stroke-width':j===0?3.0:(j===1?1.8:1.4),
 ...(j===2?{'stroke-dasharray':'6 6'}:{})
})));

function arcD(cx,cy,r,a0,a1){
 const r0=(a0-90)*Math.PI/180, r1=(a1-90)*Math.PI/180;
 const x0=cx+r*Math.cos(r0), y0=cy+r*Math.sin(r0), x1=cx+r*Math.cos(r1), y1=cy+r*Math.sin(r1);
 return `M ${x0.toFixed(1)} ${y0.toFixed(1)} A ${r} ${r} 0 0 1 ${x1.toFixed(1)} ${y1.toFixed(1)}`;
}
const RES=T.map(()=>[-1,1].map(side=>[0,1,2].map(j=>({
 side,j,p:svgEl('path','res',{
  fill:'none',stroke:j===0?'#ffffff':(j===1?'#7ce2ff':'#ff8cc6'),
  'stroke-linecap':'round','stroke-width':j===0?3.0:(j===1?2.0:1.3),
  style:'filter:drop-shadow(0 0 10px rgba(124,226,255,.8))'
 })
}))).flat());

function frame(t){
 let hum=0;
 T.forEach((p,k)=>{
  const u=seg(t,p,p+.92);hum=Math.max(hum,bump(seg(t,p,p+.32)));
  RINGS[k].forEach((c,j)=>{
   const rr=40+1080*eo(seg(t,p+j*.05,p+j*.05+.92));
   c.setAttribute('r',rr);
   const baseO=j===0?.78:(j===1?.52:.40);
   c.setAttribute('opacity',t<p+j*.05?0:baseO*(1-u)*(1-seg(t,2.38,2.66)));
  });
  RES[k].forEach(({p:el,side,j})=>{
   const u2=seg(t,p+j*.04,p+j*.04+.74);
   const rr=R+22+j*28+210*eo(u2);
   const span=30-j*4, mid=side>0?90:270;
   el.setAttribute('d',arcD(CX,CY,rr,mid-span,mid+span));
   el.setAttribute('opacity',t<p+j*.04?0:(j===0?.85:(j===1?.55:.38))*bump(u2)*(1-seg(t,2.38,2.66)));
  });
 });

 // Fade out the reactor rings, spectrum, and ECG traces cleanly between 2.36s and 2.68s so final frame matches Zone void backdrop
 const hudFade=seg(t,.08,.35)*(1-eio(seg(t,2.34,2.66)));
 S('reactor',{o:hudFade,f:`brightness(${1+1.1*hum})`,s:1+.012*Math.sin(t*130)*hum});

 // Rotating Gyroscopic Ticks
 const rot=t*22;
 GYRO_TICKS.forEach(({i,cardinal,major,e})=>{
  const ang=(i*5+rot)*Math.PI/180;
  const rOut=R+22+(cardinal?10:0), rIn=rOut-(cardinal?18:(major?10:5));
  e.setAttribute('x1',CX+rIn*Math.cos(ang));e.setAttribute('y1',CY+rIn*Math.sin(ang));
  e.setAttribute('x2',CX+rOut*Math.cos(ang));e.setAttribute('y2',CY+rOut*Math.sin(ang));
  e.setAttribute('opacity',hudFade*(cardinal?.9:(major?.6:.3)));
 });

 // 80-Bar Radial Frequency Spectrum Analyzer
 SPEC_BARS.forEach(({i,e})=>{
  const ang=(i*4.5-rot*.6)*Math.PI/180;
  const wv=.45*Math.abs(Math.cos(2*ang+t*6))+.35*Math.abs(Math.sin(4*ang-t*8))+.20*Math.abs(Math.cos(8*ang+t*10));
  const len=(8+(38+46*hum)*wv)*seg(t,.35,.65);
  const r0=R-4, r1=r0+len;
  e.setAttribute('x1',CX+r0*Math.cos(ang));e.setAttribute('y1',CY+r0*Math.sin(ang));
  e.setAttribute('x2',CX+r1*Math.cos(ang));e.setAttribute('y2',CY+r1*Math.sin(ang));
  e.setAttribute('opacity',hudFade*(.35+.65*wv));
 });

 // Supersonic Cardiac ECG & Sine Waveforms across y = CY
 const scanX=lerp(0,1600,seg(t,0,.44));
 let d1='', d2='', dE='';
 for(let x=0;x<=1600;x+=6){
  const env=Math.sin(Math.PI*x/1600);
  const y1=CY+Math.sin(x*.010+t*9)*28*env*(.4+.8*hum);
  const y2=CY+Math.cos(x*.008-t*7)*34*env*(.4+.8*hum);
  d1+=(x===0?'M':'L')+x+' '+y1.toFixed(1);
  d2+=(x===0?'M':'L')+x+' '+y2.toFixed(1);
  if(x<=scanX){
   const u=(x-CX)/68;
   const pqrst=(-0.25*Math.exp(-Math.pow((u+.72)/.18,2))+.35*Math.exp(-Math.pow((u+.24)/.08,2))-1.0*Math.exp(-Math.pow(u/.09,2))+.60*Math.exp(-Math.pow((u-.22)/.09,2))-.35*Math.exp(-Math.pow((u-.68)/.22,2)));
   const ye=CY+Math.sin(x*.015+t*11)*8*env+pqrst*(65+55*hum);
   dE+=(x===0?'M':'L')+x+' '+ye.toFixed(1);
  }
 }
 SINE1.setAttribute('d',d1);SINE2.setAttribute('d',d2);
 ECG_GLOW.setAttribute('d',dE);ECG_CORE.setAttribute('d',dE);
 SINE1.setAttribute('opacity',hudFade);SINE2.setAttribute('opacity',hudFade);
 ECG_GLOW.setAttribute('opacity',hudFade*.9);ECG_CORE.setAttribute('opacity',hudFade*.95);

 // 3,400 Cymatic Energy Particles
 const sandO=seg(t,.05,.32)*(1-seg(t,2.02,2.32));
 ctx.clearRect(0,0,1600,900);
 if(sandO>0.001){
  for(let i=0;i<N;i++){
   const g=G[i];let st=0,pp=0;
   for(let k=0;k<3;k++){if(t>=T[k]+g.d){st=k;pp=eo(seg(t,T[k]+g.d,T[k]+g.d+MOVE))}}
   const s0=t<T[0]+g.d?0:st, s1=t<T[0]+g.d?0:st+1, f=t<T[0]+g.d?0:pp;
   let x=lerp(P[s0][2*i],P[s1][2*i],f), y=lerp(P[s0][2*i+1],P[s1][2*i+1],f);
   let air=0;for(let k=0;k<3;k++)air=Math.max(air,bump(seg(t,T[k]+g.d,T[k]+g.d+.32)));
   const live=t<1.9?1:.25;
   x+=air*9*Math.sin(t*95+g.p)*live+.4*Math.sin(t*37+g.q);
   y+=air*9*Math.cos(t*87+g.q)*live-air*6+.4*Math.cos(t*41+g.p);
   ctx.globalAlpha=sandO*g.a*(.78+.22*air);
   ctx.fillStyle=g.c;
   ctx.beginPath();ctx.arc(x,y,g.r*(1+.85*air),0,6.283);ctx.fill();
  }
 }

 // Center-outward Prismatic Fusion into Brushed Platinum
 const rv=seg(t,1.95,2.35), rr=lerp(0,460,eio(rv));
 const m=`radial-gradient(circle at ${CX}px ${CY}px,#000 ${rr}px,transparent ${rr+70}px)`;
 $('set').style.webkitMaskImage=$('set').style.maskImage=m;
 S('set',{o:t<1.95?0:1,f:`brightness(${1+.85*(1-seg(t,2.1,2.58))})`});
 const sh=seg(t,2.18,2.60);
 const pos=lerp(-30,115,sh);
 $('sheen').style.background=`linear-gradient(105deg,transparent ${pos-18}%,rgba(255,140,198,.48) ${pos-6}%,rgba(255,255,255,.95) ${pos}%,rgba(124,226,255,.55) ${pos+6}%,transparent ${pos+18}%)`;
 S('sheen',{o:sh>0&&sh<1?.85*bump(sh):0});

 // Champagne Star-Spark Ignition & Anamorphic Horizontal Lens Flare
 const ig=seg(t,2.16,2.44);
 const spkGlow=bump(seg(t,2.16,2.66));
 S('sk',{o:t<2.16?0:1,f:`brightness(${1+1.5*spkGlow}) drop-shadow(0 0 ${22*spkGlow}px #f0d49c)`});
 A('p-spark',{s:t>=2.52?1:(.2+.8*back(ig))});
 const sgB=bump(seg(t,2.18,2.66));
 S('sg',{o:.92*sgB,s:.3+.95*eo(seg(t,2.18,2.66))});
 const flB=bump(seg(t,2.18,2.60));
 S('sxh',{o:.95*flB,sx:.2+1.1*flB});
 S('sxv',{o:.85*flB,sy:.2+.9*flB});

 // Paired Resonance Arcs & Sonic-Boom Echoes
 const xr=seg(t,2.30,2.50), hw=lerp(0,520,eo(xr));
 S('ex',{o:t<2.30?0:1,c:`polygon(${CX-hw}px 0,${CX+hw}px 0,${CX+hw}px 900px,${CX-hw}px 900px)`,f:`brightness(${1+.85*bump(xr)})`});
 [['e1',2.38,.26],['e2',2.45,.22]].forEach(([id,t0,dur])=>{
  const u=seg(t,t0,t0+dur);
  S(id,{o:t<t0||u>=1?0:.75*(1-u),s:1+.55*eo(u),f:'blur(.5px)'});
 });
}
"""
    js = js.replace("%P%", json.dumps(P)).replace("%N%", str(N)).replace("%CX%", str(CX)).replace("%CY%", str(CY)).replace("%R%", str(R))
    return css, body, js


def main():
    shutil.rmtree(WORK, ignore_errors=True)
    WORK.mkdir(parents=True, exist_ok=True)
    BRAND_PULSE.mkdir(parents=True, exist_ok=True)
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    BRAIN_DIR.mkdir(parents=True, exist_ok=True)

    css, body, js = build_pulse_scene()
    page_file = WORK / "page.html"
    page_file.write_text(page_html(css, body + f"<script>{I.RUNTIME}{js}</script>", "pulse intro"), encoding="utf-8")

    n_frames = round(CUT * FPS)
    with sync_playwright() as p:
        b = p.chromium.launch(channel="chrome", headless=True)
        pg = b.new_page(viewport={"width": 1600, "height": 900}, device_scale_factor=SCALE)
        pg.goto(page_file.as_uri())
        pg.wait_for_timeout(500)
        for i in range(n_frames + 1):
            pg.evaluate("t => frame(t)", i / FPS)
            pg.screenshot(path=str(WORK / f"{i:04d}.jpg"), type="jpeg", quality=95)
        b.close()

    ff = imageio_ffmpeg.get_ffmpeg_exe()
    mp4_path = BRAND_PULSE / "intro.mp4"
    subprocess.run([
        ff, "-hide_banner", "-loglevel", "error", "-y",
        "-framerate", str(FPS),
        "-i", str(WORK / "%04d.jpg"),
        "-c:v", "libx264",
        "-pix_fmt", "yuv420p",
        "-crf", "18",
        "-preset", "slow",
        "-movflags", "+faststart",
        "-an",
        str(mp4_path)
    ], check=True)

    logo_src = DST / "pulse" / "full.png"
    logo_dst = BRAND_PULSE / "logo.png"
    logo = Image.open(logo_src).convert("RGBA")
    logo.save(logo_dst, optimize=True)

    ink = logo.split()[3].getbbox()
    k = W / SIZE[0] * SCALE
    left = (CX - W / 2) * SCALE
    top = (CY - W * SIZE[1] / SIZE[0] / 2) * SCALE
    film = (left + ink[0] * k, top + ink[1] * k, (ink[2] - ink[0]) * k, (ink[3] - ink[1]) * k)

    spec = dict(
        theme="pulse",
        filmEnd=round(CUT, 2),
        coverAt=round(CUT - 0.05, 2),
        flyAt=round(CUT + 0.10, 2),
        filmLogo=[round(v, 2) for v in film],
        image=list(logo.size),
        ink=[ink[0], ink[1], ink[2] - ink[0], ink[3] - ink[1]],
    )
    (BRAND_PULSE / "spec.json").write_text(json.dumps(spec, indent=2), encoding="utf-8")
    print("IntroSpec:", json.dumps(spec))

    # Build an 8-frame contact sheet from the rendered 1920x1080 frames
    beat_times = [0.24, 0.54, 0.84, 1.34, 1.74, 2.16, 2.38, 2.74]
    beat_labels = [
        "0.24s · Supersonic ECG & 3D Reactor",
        "0.54s · Sonic Boom I & 80-Bar Spectrum",
        "0.84s · Chladni Harmonic Mandala I",
        "1.34s · Sonic Boom II & Chladni Lattice II",
        "1.74s · Magnetic Vortex Particle Forge",
        "2.16s · Platinum Fusion & Laser Strike",
        "2.38s · Anamorphic Spark Supernova",
        "2.74s · Settled Final Frame (Chamber Pose)",
    ]
    fw, fh = 460, 259
    cols, rows = 4, 2
    sheet = Image.new("RGB", (16 + cols * (fw + 12), 76 + rows * (fh + 38)), (5, 9, 18))
    draw = ImageDraw.Draw(sheet)
    font_dir = "C:/Windows/Fonts/"
    f_title = ImageFont.truetype(font_dir + "segoeuil.ttf", 27)
    f_sub = ImageFont.truetype(font_dir + "segoeui.ttf", 14)
    draw.text((20, 18), "Pulse Overhaul · 1920×1080 Cinematic Intro — 3D Sonic-Boom & Heartbeat Forge", font=f_title, fill=(242, 246, 252))
    draw.text((980, 26), "ECG trace · 3D Cymatic Grid · 80-Bar Spectrum · Anamorphic Spark", font=f_sub, fill=(124, 226, 255))
    for idx, (bt, lbl) in enumerate(zip(beat_times, beat_labels)):
        f_idx = min(n_frames, round(bt * FPS))
        im = Image.open(WORK / f"{f_idx:04d}.jpg").convert("RGB").resize((fw, fh), Image.LANCZOS)
        c, r = idx % cols, idx // cols
        x = 14 + c * (fw + 12)
        y = 70 + r * (fh + 38)
        sheet.paste(im, (x, y))
        draw.rectangle([x, y, x + fw - 1, y + fh - 1], outline=(56, 110, 180), width=1)
        draw.text((x + 4, y + fh + 6), lbl, font=f_sub, fill=(200, 218, 240))
    contact_path = REVIEW_DIR / "pulse-intro-contact-sheet.png"
    sheet.save(contact_path)
    sheet.save(BRAIN_DIR / "pulse-intro-contact-sheet.png")
    shutil.copyfile(WORK / f"{n_frames:04d}.jpg", BRAIN_DIR / "pulse-intro-final-frame.jpg")
    print("Saved contact sheet:", contact_path)


if __name__ == "__main__":
    main()
