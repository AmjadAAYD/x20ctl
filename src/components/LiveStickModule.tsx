import React from "react";

interface LiveStickModuleProps {
  label: string;
  x: number; // -1.0 to 1.0
  y: number; // -1.0 to 1.0
  innerDeadzonePercent: number; // e.g. 5
  outerDeadzonePercent: number; // e.g. 95
  className?: string;
}

export const LiveStickModule: React.FC<LiveStickModuleProps> = ({
  label,
  x,
  y,
  innerDeadzonePercent,
  outerDeadzonePercent,
  className = "",
}) => {
  // SVG radius is 80px (well radius = 70px)
  const wellRadius = 60;
  const maxDeflection = 40;

  const stickPxX = x * maxDeflection;
  const stickPxY = y * maxDeflection;

  const magnitude = Math.min(1, Math.sqrt(x * x + y * y));
  const innerDeadzoneRadius = (innerDeadzonePercent / 100) * wellRadius;
  const outerDeadzoneRadius = (outerDeadzonePercent / 100) * wellRadius;

  return (
    <div
      className={`p-4 rounded-xl bg-[#141211] border border-[#332C29] flex flex-col items-center select-none ${className}`}
    >
      <div className="w-full flex items-center justify-between text-xs mb-3">
        <span className="font-bold text-[#F4F0EB]">
          {label} Live Hardware View
        </span>
        <span className="font-mono text-[11px] text-[#FF8A5B] bg-[#241F1D] px-2 py-0.5 rounded border border-[#332C29]">
          Mag: {Math.round(magnitude * 100)}%
        </span>
      </div>

      {/* High Detail Interactive SVG */}
      <div className="relative w-48 h-48 flex items-center justify-center">
        <svg viewBox="-80 -80 160 160" className="w-full h-full">
          <defs>
            <radialGradient id="knurlCollar" cx="50%" cy="50%" r="50%">
              <stop offset="0%" stopColor="#8A92A0" />
              <stop offset="50%" stopColor="#D5D9E0" />
              <stop offset="85%" stopColor="#555D6C" />
              <stop offset="100%" stopColor="#2E333D" />
            </radialGradient>
            <radialGradient id="thumbCapGrad" cx="40%" cy="40%" r="60%">
              <stop offset="0%" stopColor="#555C69" />
              <stop offset="70%" stopColor="#242831" />
              <stop offset="100%" stopColor="#14161C" />
            </radialGradient>
          </defs>

          {/* Outer Housing Well */}
          <circle
            cx="0"
            cy="0"
            r={wellRadius + 10}
            fill="#181A20"
            stroke="#332C29"
            strokeWidth="2"
          />

          {/* Knurled Tension Ring Gear */}
          <circle
            cx="0"
            cy="0"
            r={wellRadius + 4}
            fill="url(#knurlCollar)"
            stroke="#4A5260"
            strokeWidth="1"
          />
          <circle
            cx="0"
            cy="0"
            r={wellRadius}
            fill="#121418"
            stroke="#30353E"
            strokeWidth="3"
            strokeDasharray="3 3"
          />

          {/* Crosshair guide lines */}
          <line
            x1={-wellRadius}
            y1="0"
            x2={wellRadius}
            y2="0"
            stroke="#2D3340"
            strokeWidth="1"
            strokeDasharray="2 2"
          />
          <line
            x1="0"
            y1={-wellRadius}
            x2="0"
            y2={wellRadius}
            stroke="#2D3340"
            strokeWidth="1"
            strokeDasharray="2 2"
          />

          {/* Outer Deadzone Ring (Sprint boundary) */}
          <circle
            cx="0"
            cy="0"
            r={outerDeadzoneRadius}
            fill="none"
            stroke="#86C08A"
            strokeWidth="1.2"
            strokeDasharray="4 3"
            opacity="0.6"
          />

          {/* Inner Deadzone Ring (Drift zone) */}
          <circle
            cx="0"
            cy="0"
            r={innerDeadzoneRadius}
            fill="#FF8A5B"
            fillOpacity="0.15"
            stroke="#FF8A5B"
            strokeWidth="1.2"
            strokeDasharray="2 2"
          />

          {/* Deflection Vector Line */}
          <line
            x1="0"
            y1="0"
            x2={stickPxX}
            y2={stickPxY}
            stroke="#FF8A5B"
            strokeWidth="2"
            strokeLinecap="round"
          />

          {/* Thumbstick Cap (Moves instantly with 1:1 deflection) */}
          <g transform={`translate(${stickPxX}, ${stickPxY})`}>
            {/* Rubber Cap Body */}
            <circle
              cx="0"
              cy="0"
              r="30"
              fill="url(#thumbCapGrad)"
              stroke="#FF8A5B"
              strokeWidth="2"
            />
            <circle
              cx="0"
              cy="0"
              r="24"
              fill="none"
              stroke="#484F5D"
              strokeWidth="1.5"
            />
            <circle
              cx="0"
              cy="0"
              r="16"
              fill="#1C1E24"
              stroke="#121418"
              strokeWidth="1"
            />

            {/* EasySMX Brand Mark */}
            <text
              x="0"
              y="3"
              fill="#B4BAC6"
              fontSize="7"
              fontWeight="bold"
              textAnchor="middle"
            >
              EasySMX
            </text>
            <circle cx="0" cy="0" r="2.5" fill="#FF8A5B" />
          </g>
        </svg>
      </div>

      {/* Coordinate & Deadzone Readout */}
      <div className="w-full mt-3 grid grid-cols-2 gap-2 text-[11px] font-mono text-[#A79C92] pt-2 border-t border-[#332C29]">
        <div className="flex justify-between">
          <span>X-Axis:</span>
          <span className="text-[#F4F0EB] font-bold">{x.toFixed(3)}</span>
        </div>
        <div className="flex justify-between">
          <span>Y-Axis:</span>
          <span className="text-[#F4F0EB] font-bold">{y.toFixed(3)}</span>
        </div>
      </div>
    </div>
  );
};
