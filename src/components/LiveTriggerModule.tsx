import React from "react";

interface LiveTriggerModuleProps {
  label: string; // "Left Trigger (LT)" | "Right Trigger (RT)"
  value: number; // 0.0 to 1.0
  hairTrigger: boolean;
  className?: string;
}

export const LiveTriggerModule: React.FC<LiveTriggerModuleProps> = ({
  label,
  value,
  hairTrigger,
  className = "",
}) => {
  const percent = Math.round(value * 100);
  const strokeDash = value * 180;

  // Visual trigger compression rotation angle (0 to 22 degrees)
  const rotAngle = value * 22;

  return (
    <div
      className={`p-4 rounded-xl bg-[#141211] border border-[#332C29] flex flex-col items-center select-none ${className}`}
    >
      <div className="w-full flex items-center justify-between text-xs mb-3">
        <span className="font-bold text-[#F4F0EB]">
          {label} Live Hardware View
        </span>
        <span
          className={`font-mono text-[11px] px-2 py-0.5 rounded border ${
            hairTrigger
              ? "bg-[#FF8A5B]/15 text-[#FF8A5B] border-[#FF8A5B]/30"
              : "bg-[#241F1D] text-[#86C08A] border-[#332C29]"
          }`}
        >
          {hairTrigger ? "Hair-Trigger Active" : "Linear Hall-Effect"}
        </span>
      </div>

      {/* 3D Trigger Visualizer */}
      <div className="w-48 h-48 relative flex items-center justify-center">
        <svg viewBox="0 0 160 160" className="w-full h-full">
          <defs>
            <linearGradient id="paddleGrad" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stopColor="#434A57" />
              <stop offset="50%" stopColor="#2A2F38" />
              <stop offset="100%" stopColor="#1B1E24" />
            </linearGradient>

            <linearGradient id="gaugeGrad" x1="0%" y1="100%" x2="0%" y2="0%">
              <stop offset="0%" stopColor="#86C08A" />
              <stop offset="60%" stopColor="#FFB020" />
              <stop offset="100%" stopColor="#FF8A5B" />
            </linearGradient>
          </defs>

          {/* Trigger Mount Chassis Hinge */}
          <rect
            x="25"
            y="20"
            width="110"
            height="24"
            rx="6"
            fill="#1C1F26"
            stroke="#332C29"
            strokeWidth="2"
          />
          <circle
            cx="80"
            cy="32"
            r="6"
            fill="#4B5362"
            stroke="#242831"
            strokeWidth="2"
          />
          <circle cx="80" cy="32" r="2.5" fill="#8A92A0" />

          {/* Curved Travel Gauge Arc */}
          <path
            d="M 30 135 A 65 65 0 0 1 130 135"
            fill="none"
            stroke="#241F1D"
            strokeWidth="10"
            strokeLinecap="round"
          />
          <path
            d="M 30 135 A 65 65 0 0 1 130 135"
            fill="none"
            stroke="url(#gaugeGrad)"
            strokeWidth="8"
            strokeLinecap="round"
            strokeDasharray="180"
            strokeDashoffset={180 - strokeDash}
            className="transition-all duration-75"
          />

          {/* Mechanical Trigger Paddle (Rotates on hinge based on pull value) */}
          <g
            transform={`rotate(${rotAngle}, 80, 32)`}
            className="transition-transform duration-75"
          >
            {/* Paddle Body */}
            <path
              d="
                M 56 32
                C 56 32, 50 80, 52 110
                C 54 125, 70 132, 80 132
                C 90 132, 106 125, 108 110
                C 110 80, 104 32, 104 32
                Z
              "
              fill="url(#paddleGrad)"
              stroke={value > 0.05 ? "#FF8A5B" : "#4F5868"}
              strokeWidth="2.5"
            />
            {/* Grip rib notches */}
            <line
              x1="62"
              y1="80"
              x2="98"
              y2="80"
              stroke="#16181D"
              strokeWidth="2"
            />
            <line
              x1="64"
              y1="92"
              x2="96"
              y2="92"
              stroke="#16181D"
              strokeWidth="2"
            />
            <line
              x1="66"
              y1="104"
              x2="94"
              y2="104"
              stroke="#16181D"
              strokeWidth="2"
            />
          </g>
        </svg>

        {/* Live % Badge in Center */}
        <div className="absolute bottom-2 font-mono text-base font-black text-[#F4F0EB] flex items-baseline gap-1">
          <span>{percent}</span>
          <span className="text-[10px] text-[#A79C92]">% Travel</span>
        </div>
      </div>

      <p className="text-[10px] text-[#A79C92] mt-3">
        Read-only XInput trigger state
      </p>
    </div>
  );
};
