import React from 'react';
import { BatteryCharging, Zap } from 'lucide-react';

interface BatteryIndicatorProps {
  level: number; // 1 to 4 bars (or 0-100 mapped into 1-4)
  isCharging?: boolean;
  showText?: boolean;
  className?: string;
  themeAccent?: string;
}

export const BatteryIndicator: React.FC<BatteryIndicatorProps> = ({
  level,
  isCharging = false,
  showText = true,
  className = '',
  themeAccent = '#FF8A5B',
}) => {
  // Normalize to 1 - 4 bars
  let bars = Math.round(level);
  if (level > 4) {
    // If passed 0-100, cut it into 4 levels
    if (level <= 25) bars = 1;
    else if (level <= 50) bars = 2;
    else if (level <= 75) bars = 3;
    else bars = 4;
  }
  bars = Math.max(1, Math.min(4, bars));

  const levelLabels: Record<number, string> = {
    1: 'Level 1 (Low / 25%)',
    2: 'Level 2 (50%)',
    3: 'Level 3 (75%)',
    4: 'Level 4 (Full / 100%)',
  };

  return (
    <div
      className={`inline-flex items-center gap-2 px-2.5 py-1 rounded-md bg-[#241F1D] border border-[#332C29] ${className}`}
      title={isCharging ? 'Battery Charging (EasySMX X20 4-LED Indicator)' : `EasySMX Battery: ${levelLabels[bars]}`}
    >
      {isCharging ? (
        <Zap className="w-3.5 h-3.5 text-[#86C08A] animate-pulse" />
      ) : (
        <span className="text-[10px] uppercase font-mono text-[#A79C92] font-semibold">BAT</span>
      )}

      {/* 4 Discrete Hardware Segment Bars */}
      <div className="flex items-center gap-1">
        {[1, 2, 3, 4].map((barIdx) => {
          const isLit = barIdx <= bars;
          return (
            <div
              key={barIdx}
              className={`w-2.5 h-3.5 rounded-[2px] transition-all duration-300 ${
                isCharging
                  ? 'bg-[#86C08A] animate-pulse'
                  : isLit
                  ? barIdx === 1 && bars === 1
                    ? 'bg-[#E5645E] shadow-[0_0_6px_rgba(229,100,94,0.6)]'
                    : 'bg-[#FF8A5B] shadow-[0_0_6px_rgba(255,138,91,0.5)]'
                  : 'bg-[#131110] border border-[#332C29]'
              }`}
            />
          );
        })}
      </div>

      {showText && (
        <span className="text-xs font-mono font-medium text-[#D6CEC6] ml-0.5">
          {isCharging ? 'Charging' : `Lvl ${bars}/4`}
        </span>
      )}
    </div>
  );
};
