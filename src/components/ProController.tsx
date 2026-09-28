import { useId } from "react";
import proHero from "../assets/x20-pro-hero.png";

export function ProController() {
  const id = useId().replace(/:/g, "");
  return (
    <section className="pro-controller-panel metal-panel">
      <div className="panel-heading">
        <h2>X20 Pro hardware canvas</h2>
        <span className="panel-code">ILLUSTRATION / FRONT</span>
      </div>
      <img className="pro-controller-photo" src={proHero} alt="Front-facing illustration of the X20 Pro controller" />
      <svg className="pro-controller-svg" viewBox="0 0 760 480" role="img"
        aria-label="Illustrated X20 Pro front view with six programmable physical controls">
        <defs>
          <linearGradient id={`${id}-shell`} x1="0" x2="1" y1="0" y2="1">
            <stop stopColor="#6b747b" /><stop offset=".42" stopColor="#343a40" /><stop offset="1" stopColor="#1a1f24" />
          </linearGradient>
          <radialGradient id={`${id}-well`}><stop stopColor="#303942"/><stop offset="1" stopColor="#0d1115"/></radialGradient>
        </defs>
        <path className="pro-shell" fill={`url(#${id}-shell)`}
          d="M210 90 C255 70 312 89 380 89 C448 89 505 70 550 90 C613 113 658 176 670 261 C680 340 642 410 598 416 C558 422 539 370 516 330 C494 292 452 287 380 287 C308 287 266 292 244 330 C221 370 202 422 162 416 C118 410 80 340 90 261 C102 176 147 113 210 90Z" />
        <path fill="none" stroke="#8d9aa3" strokeWidth="3" opacity=".6"
          d="M209 99 C259 84 314 101 380 101 C446 101 501 84 551 99" />
          <g className="pro-front">
            <rect x="171" y="55" width="97" height="27" rx="12" fill="#202830" stroke="#8d9ba3" />
            <rect x="492" y="55" width="97" height="27" rx="12" fill="#202830" stroke="#8d9ba3" />
            <text x="219" y="73" textAnchor="middle">MINI SHOULDER 1</text>
            <text x="540" y="73" textAnchor="middle">MINI SHOULDER 2</text>
            <rect x="316" y="112" width="128" height="74" rx="10" fill="#0a131a" stroke="var(--accent)" strokeWidth="2" />
            <text x="380" y="139" textAnchor="middle" className="pro-display-label">SMART DISPLAY</text>
            <text x="380" y="162" textAnchor="middle" className="pro-display-note">Observed data only</text>
            <circle cx="251" cy="205" r="53" fill={`url(#${id}-well)`} stroke="#89969f" strokeWidth="3" />
            <circle cx="251" cy="205" r="27" fill="#333d45" stroke="var(--accent)" strokeWidth="2" />
            <circle cx="490" cy="261" r="53" fill={`url(#${id}-well)`} stroke="#89969f" strokeWidth="3" />
            <circle cx="490" cy="261" r="27" fill="#333d45" stroke="var(--accent)" strokeWidth="2" />
            <path d="M308 239 h25 v-20 h22 v20 h25 v22 h-25 v20 h-22 v-20 h-25Z" fill="#242b31" stroke="#97a4ae" strokeWidth="2" />
            {[[-1,0,"X"],[0,-1,"Y"],[1,0,"B"],[0,1,"A"]].map(([dx,dy,label]) => (
              <g key={label as string}><circle cx={545+(dx as number)*27} cy={193+(dy as number)*27} r="17" fill="#1c242a" stroke="#96a2aa" />
              <text x={545+(dx as number)*27} y={198+(dy as number)*27} textAnchor="middle">{label}</text></g>
            ))}
            <text x="379" y="352" textAnchor="middle" className="pro-caption">TMR STICKS · DUAL-MODE TRIGGERS · FOUR-MOTOR HAPTICS</text>
          </g>
      </svg>
      <p className="pro-illustration-note">Illustration based on published hardware features. Button names and live assignments have not been verified.</p>
    </section>
  );
}
