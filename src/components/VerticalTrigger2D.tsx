import React from 'react';

interface VerticalTrigger2DProps {
  label: string;
  side: 'left' | 'right';
  value: number; // 0.0 to 1.0
  onSimulate?: (val: number) => void;
  className?: string;
  hairTrigger?: boolean;
}

export const VerticalTrigger2D: React.FC<VerticalTrigger2DProps> = ({
  label,
  side,
  value,
  onSimulate,
  className = '',
  hairTrigger = false,
}) => {
  const clampedVal = Math.max(0, Math.min(1, value));
  // Total vertical travel distance in SVG pixels: 0px (top rest) to 90px (bottom out)
  const maxTravelPx = 90;
  const currentTravelPx = clampedVal * maxTravelPx;
  const travelMm = (clampedVal * 8.5).toFixed(2); // 8.5mm standard full mechanical travel

  const handlePointerMove = (e: React.PointerEvent<SVGSVGElement>) => {
    if (!onSimulate || (e.buttons !== 1 && e.type !== 'pointerdown')) return;
    const rect = e.currentTarget.getBoundingClientRect();
    const offsetY = e.clientY - rect.top;
    // Map top 30px to 130px -> 0 to 1
    const normalized = (offsetY - 25) / (rect.height - 50);
    onSimulate(Math.max(0, Math.min(1, normalized)));
  };

  const handlePointerUp = () => {
    if (onSimulate) {
      onSimulate(0);
    }
  };

  return (
    <div
      className={`p-4 rounded-xl bg-[#141211] border border-[#332C29] flex flex-col items-center select-none ${className}`}
    >
      {/* Header Readout */}
      <div className="w-full flex items-center justify-between text-xs mb-2">
        <div className="flex items-center gap-2">
          <span className="font-bold text-[#F4F0EB]">{label}</span>
          <span className="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded bg-[#241F1D] text-[#FF8A5B] border border-[#332C29]">
            2D Hall Travel
          </span>
        </div>
        <div className="flex items-center gap-2 font-mono">
          <span className="text-[11px] text-[#A79C92]">{travelMm} mm</span>
          <span className="text-sm font-bold text-[#FF8A5B]">
            {Math.round(clampedVal * 100)}%
          </span>
        </div>
      </div>

      {/* 2D Vertical Mechanical Trigger SVG */}
      <div className="relative w-full max-w-[200px] h-[190px] flex items-center justify-center">
        <svg
          viewBox="0 0 160 170"
          className="w-full h-full cursor-ns-resize overflow-visible"
          onPointerDown={handlePointerMove}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
        >
          <defs>
            {/* Lever Shader */}
            <linearGradient id={`leverGrad-${side}`} x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#3E4452" />
              <stop offset="25%" stopColor="#2A2F3A" />
              <stop offset="70%" stopColor="#1C2028" />
              <stop offset="100%" stopColor="#101217" />
            </linearGradient>

            {/* Plunger Metal Shaft */}
            <linearGradient id="plungerShaft" x1="0%" y1="0%" x2="100%" y2="0%">
              <stop offset="0%" stopColor="#9CA3AF" />
              <stop offset="50%" stopColor="#E5E7EB" />
              <stop offset="100%" stopColor="#6B7280" />
            </linearGradient>

            {/* Magnetic Field Glow */}
            <radialGradient id={`magnetGlow-${side}`} cx="50%" cy="50%" r="50%">
              <stop offset="0%" stopColor="#FF8A5B" stopOpacity={0.2 + clampedVal * 0.7} />
              <stop offset="60%" stopColor="#FF8A5B" stopOpacity={0.05 + clampedVal * 0.3} />
              <stop offset="100%" stopColor="#FF8A5B" stopOpacity="0" />
            </radialGradient>
          </defs>

          {/* 1. Vertical Guide Chassis & Track */}
          <rect
            x="20"
            y="15"
            width="120"
            height="145"
            rx="12"
            fill="#181B22"
            stroke="#2F3644"
            strokeWidth="1.5"
          />

          {/* Calibrated Vertical MM Ruler */}
          <g transform="translate(118, 25)" className="font-mono text-[8px] fill-[#6B7280]">
            {/* 0% Top */}
            <line x1="0" y1="0" x2="10" y2="0" stroke="#4B5563" strokeWidth="1.5" />
            <text x="14" y="3" fill="#9CA3AF">0%</text>

            {/* 25% */}
            <line x1="0" y1="22.5" x2="6" y2="22.5" stroke="#374151" strokeWidth="1" />

            {/* 50% Mid */}
            <line x1="0" y1="45" x2="8" y2="45" stroke="#4B5563" strokeWidth="1.5" />
            <text x="14" y="48" fill="#9CA3AF">50%</text>

            {/* 75% */}
            <line x1="0" y1="67.5" x2="6" y2="67.5" stroke="#374151" strokeWidth="1" />

            {/* 100% Bottom */}
            <line x1="0" y1="90" x2="10" y2="90" stroke="#FF8A5B" strokeWidth="1.5" />
            <text x="14" y="93" fill="#FF8A5B" fontWeight="bold">100%</text>
          </g>

          {/* Vertical Travel Guide Slot */}
          <rect
            x="64"
            y="25"
            width="12"
            height="100"
            rx="6"
            fill="#0F1116"
            stroke="#262C38"
            strokeWidth="1.2"
          />

          {/* Mechanical Internal Spring (compresses as trigger moves down) */}
          <g transform="translate(70, 25)">
            <line
              x1="0"
              y1="0"
              x2="0"
              y2={Math.max(8, currentTravelPx)}
              stroke="#E5E7EB"
              strokeWidth="2.5"
              strokeDasharray="3 3"
              strokeLinecap="round"
            />
          </g>

          {/* Hall-Effect Magnet Sensor Chip (at bottom of travel) */}
          <g transform="translate(70, 134)">
            {/* Radiating flux glow */}
            <circle cx="0" cy="0" r={16 + clampedVal * 12} fill={`url(#magnetGlow-${side})`} />
            <rect
              x="-14"
              y="-6"
              width="28"
              height="12"
              rx="3"
              fill="#1F2430"
              stroke="#FF8A5B"
              strokeWidth="1.5"
            />
            <text
              x="0"
              y="2.5"
              fill="#FF8A5B"
              fontSize="7"
              fontFamily="monospace"
              fontWeight="bold"
              textAnchor="middle"
            >
              HALL
            </text>
          </g>

          {/* ========================================================= */}
          {/* 2. THE MOVING TRIGGER LEVER (PLUNGES VERTICALLY DOWNWARD) */}
          {/* Instant 1:1 hardware tracking without CSS lag */}
          {/* ========================================================= */}
          <g transform={`translate(0, ${currentTravelPx})`}>
            {/* Plunger Guide Pin */}
            <rect
              x="66"
              y="20"
              width="8"
              height="14"
              rx="2"
              fill="url(#plungerShaft)"
              stroke="#374151"
              strokeWidth="0.8"
            />

            {/* Permanent Neodymium Magnet Pill */}
            <rect
              x="63"
              y="28"
              width="14"
              height="6"
              rx="2"
              fill="#E11D48"
              stroke="#BE123C"
              strokeWidth="0.8"
            />

            {/* Ergonomic Curved 2D Trigger Paddle Body */}
            <path
              d={
                side === 'left'
                  ? 'M 35,16 C 50,14 85,14 105,16 C 110,24 108,36 102,44 C 92,54 52,54 40,44 C 34,36 32,24 35,16 Z'
                  : 'M 35,16 C 55,14 90,14 105,16 C 108,24 106,36 100,44 C 88,54 48,54 38,44 C 32,36 30,24 35,16 Z'
              }
              fill={`url(#leverGrad-${side})`}
              stroke={clampedVal > 0.02 ? '#FF8A5B' : '#475163'}
              strokeWidth="2"
              filter="drop-shadow(0 4px 6px rgba(0,0,0,0.5))"
            />

            {/* Anti-slip grip ridges on trigger face */}
            <line x1="50" y1="28" x2="90" y2="28" stroke="#525C6E" strokeWidth="1.5" strokeLinecap="round" />
            <line x1="53" y1="34" x2="87" y2="34" stroke="#525C6E" strokeWidth="1.5" strokeLinecap="round" />
            <line x1="57" y1="40" x2="83" y2="40" stroke="#525C6E" strokeWidth="1.5" strokeLinecap="round" />

            {/* Center Active Indicator LED */}
            <circle
              cx="70"
              cy="23"
              r="2.5"
              fill={clampedVal > 0.05 ? '#FF8A5B' : '#374151'}
              filter={clampedVal > 0.05 ? 'drop-shadow(0 0 4px #FF8A5B)' : 'none'}
            />
          </g>

          {/* Bottom Mechanical Stop / Cushion */}
          <rect
            x="58"
            y="126"
            width="24"
            height="4"
            rx="1.5"
            fill="#374151"
            stroke="#1F2937"
            strokeWidth="0.8"
          />
        </svg>
      </div>

      {/* Footer Info / Travel Limits */}
      <div className="w-full mt-2 pt-2 border-t border-[#332C29] flex items-center justify-between text-[11px] text-[#A79C92]">
        <span>Zero Stop: <strong className="text-[#F4F0EB]">0.0 mm</strong></span>
        <span>Bottom Stop: <strong className="text-[#FF8A5B]">8.5 mm</strong></span>
      </div>
    </div>
  );
};
