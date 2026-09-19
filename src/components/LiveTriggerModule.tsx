import { useId } from "react";
import { Gauge } from "lucide-react";
import "./metal-curves.css";

interface LiveTriggerModuleProps {
  label: string;
  value: number;
  hairTrigger: boolean;
  className?: string;
  connected?: boolean;
}

export function LiveTriggerModule({
  label,
  value,
  hairTrigger,
  className = "",
  connected,
}: LiveTriggerModuleProps) {
  const id = useId().replace(/:/g, "");
  const travel = Math.max(0, Math.min(1, value));
  return (
    <section
      className={`instrument-module ${className}`}
      aria-label={`${label} live input`}
    >
      <header className="curve-panel-heading">
        <span>
          <Gauge size={15} />
          {label}
        </span>
        <span
          className={`curve-tag ${connected === false ? "" : "instrument-ready"}`}
        >
          {connected === false ? "No XInput" : "XInput"}
        </span>
      </header>
      <div className="instrument-trigger-layout">
        <svg
          className="instrument-trigger"
          viewBox="0 0 180 170"
          role="img"
          aria-label={
            connected === false
              ? "No live controller input"
              : `Trigger travel ${Math.round(travel * 100)} percent`
          }
        >
          <defs>
            <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
              <stop stopColor="#a0a9b0" />
              <stop offset=".25" stopColor="#525c65" />
              <stop offset=".75" stopColor="#242c33" />
              <stop offset="1" stopColor="#515b63" />
            </linearGradient>
          </defs>
          <path d="M25 34 H98 V48 H25 Z" fill="#313a42" stroke="#75838d" />
          <circle cx="61" cy="40" r="8" fill="#0e141a" stroke="#919ca4" />
          <circle cx="61" cy="40" r="3" fill="#77838c" />
          <g transform={`rotate(${travel * 18} 61 40)`}>
            <path
              d="M40 44 H82 L96 106 Q98 129 83 141 Q69 149 52 141 L43 124 Z"
              fill={`url(#${id})`}
              stroke={
                connected !== false && travel > 0.05 ? "#64dce4" : "#8b969f"
              }
              strokeWidth="1.5"
            />
            {[83, 93, 103, 113].map((position) => (
              <line
                key={position}
                x1="52"
                y1={position}
                x2="81"
                y2={position + 2}
                stroke="#151d24"
                strokeWidth="2"
              />
            ))}
          </g>
          <rect
            x="132"
            y="25"
            width="17"
            height="126"
            rx="7"
            fill="#0c1115"
            stroke="#56616b"
          />
          {connected !== false && (
            <rect
              x="135"
              y={148 - travel * 120}
              width="11"
              height={travel * 120}
              rx="4"
              fill="#64dce4"
            />
          )}
          {[0, 25, 50, 75, 100].map((tick) => (
            <g key={tick}>
              <line
                x1="153"
                x2="157"
                y1={148 - tick * 1.2}
                y2={148 - tick * 1.2}
                stroke="#66747f"
              />
              <text x="161" y={151 - tick * 1.2} fill="#94a1aa" fontSize="7">
                {tick}
              </text>
            </g>
          ))}
        </svg>
        <div className="instrument-readout-stack">
          <div>
            <span>Trigger travel</span>
            <strong>
              {connected === false ? "--" : Math.round(travel * 100)}
              <small>%</small>
            </strong>
          </div>
          <span className="curve-tag">
            {hairTrigger ? "Instant draft" : "Analog input"}
          </span>
          <p>
            {connected === false
              ? "Connect a Windows gamepad to view its input."
              : "Windows trigger position"}
          </p>
        </div>
      </div>
    </section>
  );
}
