import { useId, useState } from "react";

export function ProController() {
  const [side, setSide] = useState<"front" | "rear">("front");
  const id = useId().replace(/:/g, "");
  return (
    <section className="pro-controller-panel metal-panel">
      <div className="panel-heading">
        <h2>X20 Pro hardware canvas</h2>
        <span className="panel-code">ILLUSTRATION / {side.toUpperCase()}</span>
      </div>
      <div className="pro-view-switch" role="group" aria-label="Controller view">
        <button aria-pressed={side === "front"} onClick={() => setSide("front")}>Front</button>
        <button aria-pressed={side === "rear"} onClick={() => setSide("rear")}>Rear</button>
      </div>
      <svg className="pro-controller-svg" viewBox="0 0 760 480" role="img"
        aria-label={`Illustrated X20 Pro ${side} view with six programmable physical controls`}>
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
        {side === "front" ? (
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
        ) : (
          <g className="pro-rear">
            <rect x="174" y="159" width="145" height="52" rx="17" fill="#172027" stroke="var(--accent)" strokeWidth="2" />
            <rect x="441" y="159" width="145" height="52" rx="17" fill="#172027" stroke="var(--accent)" strokeWidth="2" />
            <rect x="202" y="258" width="118" height="64" rx="17" fill="#172027" stroke="#b6c1c8" strokeWidth="2" />
            <rect x="440" y="258" width="118" height="64" rx="17" fill="#172027" stroke="#b6c1c8" strokeWidth="2" />
            <text x="246" y="191" textAnchor="middle">REAR 1</text><text x="514" y="191" textAnchor="middle">REAR 2</text>
            <text x="261" y="291" textAnchor="middle">REMOVABLE 1</text><text x="499" y="291" textAnchor="middle">REMOVABLE 2</text>
            <text x="380" y="365" textAnchor="middle" className="pro-caption">FOUR REAR CONTROLS · TWO MINI SHOULDERS</text>
          </g>
        )}
      </svg>
      <p className="pro-illustration-note">Illustration based on published hardware features. Button names and live assignments have not been verified.</p>
    </section>
  );
}
