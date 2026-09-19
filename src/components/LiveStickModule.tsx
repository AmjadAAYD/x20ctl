import { useId } from "react";
import { Crosshair } from "lucide-react";
import "./metal-curves.css";

interface LiveStickModuleProps {
  label: string;
  x: number;
  y: number;
  innerDeadzonePercent: number;
  outerDeadzonePercent: number;
  className?: string;
  connected?: boolean;
}

export function LiveStickModule({
  label,
  x,
  y,
  innerDeadzonePercent,
  outerDeadzonePercent,
  className = "",
  connected,
}: LiveStickModuleProps) {
  const id = useId().replace(/:/g, "");
  const radius = 68;
  const magnitude = Math.min(1, Math.hypot(x, y));
  const clamp = (value: number) => Math.max(-1, Math.min(1, value));
  return (
    <section
      className={`instrument-module ${className}`}
      aria-label={`${label} live input`}
    >
      <header className="curve-panel-heading">
        <span>
          <Crosshair size={15} />
          {label}
        </span>
        <span
          className={`curve-tag ${connected === false ? "" : "instrument-ready"}`}
        >
          {connected === false ? "No XInput" : "XInput"}
        </span>
      </header>
      <div className="instrument-stick-layout">
        <svg
          className="instrument-stick"
          viewBox="-84 -84 168 168"
          role="img"
          aria-label={
            connected === false
              ? "No live controller input"
              : `Stick coordinates X ${x.toFixed(3)}, Y ${y.toFixed(3)}`
          }
        >
          <defs>
            <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
              <stop stopColor="#d4dadd" />
              <stop offset=".35" stopColor="#606971" />
              <stop offset=".65" stopColor="#272d33" />
              <stop offset="1" stopColor="#b2bdc5" />
            </linearGradient>
          </defs>
          <circle
            r="77"
            fill={`url(#${id})`}
            stroke="#070a0d"
            strokeWidth="2"
          />
          <circle r="72" fill="#12171c" stroke="#070a0d" strokeWidth="2" />
          {[0.25, 0.5, 0.75, 1].map((scale) => (
            <circle
              key={scale}
              r={radius * scale}
              fill="none"
              stroke="#35414a"
              strokeWidth=".7"
            />
          ))}
          {[0, 45, 90, 135].map((angle) => (
            <line
              key={angle}
              x1={-radius}
              x2={radius}
              stroke="#35414a"
              strokeWidth=".7"
              transform={`rotate(${angle})`}
            />
          ))}
          <circle
            r={(Math.max(0, innerDeadzonePercent) / 100) * radius}
            fill="#b9c4cb"
            fillOpacity=".15"
            stroke="#7b8790"
            strokeWidth=".8"
            strokeDasharray="2 2"
          />
          <circle
            r={(Math.max(0, outerDeadzonePercent) / 100) * radius}
            fill="none"
            stroke="#64dce4"
            strokeOpacity=".55"
            strokeWidth="1"
            strokeDasharray="3 3"
          />
          {connected !== false && (
            <g>
              <line
                x1="0"
                y1="0"
                x2={clamp(x) * radius}
                y2={clamp(y) * radius}
                stroke="#64dce4"
                strokeWidth="1.5"
              />
              <circle
                cx={clamp(x) * radius}
                cy={clamp(y) * radius}
                r="6"
                fill="#d4ffff"
                stroke="#1d727c"
                strokeWidth="2"
              />
            </g>
          )}
          <text x="0" y="-59" textAnchor="middle" fill="#788791" fontSize="6">
            Y
          </text>
          <text x="61" y="3" fill="#788791" fontSize="6">
            X
          </text>
        </svg>
        <div className="instrument-readout-stack">
          <div>
            <span>Deflection</span>
            <strong>
              {connected === false ? "--" : Math.round(magnitude * 100)}
              <small>%</small>
            </strong>
          </div>
          <dl>
            <div>
              <dt>X axis</dt>
              <dd>{connected === false ? "--" : x.toFixed(3)}</dd>
            </div>
            <div>
              <dt>Y axis</dt>
              <dd>{connected === false ? "--" : y.toFixed(3)}</dd>
            </div>
          </dl>
          <p>
            {connected === false
              ? "Connect a Windows gamepad to view its input."
              : "Windows stick position"}
          </p>
        </div>
      </div>
    </section>
  );
}
