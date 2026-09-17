import React, { useState } from 'react';
import { Volume2, Play, Check } from 'lucide-react';

interface RumblePageProps {
  vibration: number;
  onUpdateVibration: (val: number) => void;
  onTriggerHaptic: (durationMs?: number, strong?: number, weak?: number) => Promise<boolean>;
}

export const RumblePage: React.FC<RumblePageProps> = ({
  vibration,
  onUpdateVibration,
  onTriggerHaptic,
}) => {
  const [testingRumble, setTestingRumble] = useState<boolean>(false);

  const testVibrationEffect = async () => {
    setTestingRumble(true);
    const mag = vibration / 100;
    await onTriggerHaptic(500, mag, mag * 0.7);
    setTimeout(() => {
      setTestingRumble(false);
    }, 550);
  };

  const presets = [
    { label: 'Off', val: 0 },
    { label: 'Gentle', val: 30 },
    { label: 'Standard', val: 70 },
    { label: 'Maximum', val: 100 },
  ];

  return (
    <div className="space-y-6 max-w-3xl mx-auto">
      <div className="flex items-center justify-between pb-4 border-b border-[#332C29]">
        <div>
          <h2 className="text-base font-semibold text-[#F4F0EB]">Haptic Vibration & Rumble</h2>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Adjust dual asymmetric eccentric rotating mass (ERM) motors inside the gamepad grips.
          </p>
        </div>

        <button
          onClick={testVibrationEffect}
          disabled={testingRumble || vibration === 0}
          className={`flex items-center gap-2 px-3.5 py-1.5 text-xs font-semibold rounded-md transition-all ${
            testingRumble
              ? 'bg-[#FF8A5B] text-[#171310] animate-pulse'
              : 'bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] hover:text-[#F4F0EB] border border-[#332C29]'
          } disabled:opacity-40`}
        >
          <Play className="w-3.5 h-3.5 text-[#FF8A5B]" />
          <span>{testingRumble ? 'Pulsing Motors...' : 'Test Vibration'}</span>
        </button>
      </div>

      <div className="p-6 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-6">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="p-2.5 rounded-lg bg-[#241F1D] border border-[#332C29] text-[#FF8A5B]">
              <Volume2 className="w-5 h-5" />
            </div>
            <div>
              <span className="text-sm font-semibold text-[#F4F0EB] block">
                Motor Intensity
              </span>
              <span className="text-xs text-[#A79C92]">
                Applied equally across both left (heavy) and right (light) vibration weights.
              </span>
            </div>
          </div>
          <span className="text-2xl font-mono font-bold text-[#FF8A5B]">
            {vibration}%
          </span>
        </div>

        <div>
          <input
            type="range"
            min="0"
            max="100"
            step="5"
            value={vibration}
            onChange={(e) => onUpdateVibration(parseInt(e.target.value))}
            className="w-full accent-[#FF8A5B] bg-[#131110] h-2 rounded-lg cursor-pointer"
          />
        </div>

        {/* Presets */}
        <div className="grid grid-cols-4 gap-3">
          {presets.map((preset) => (
            <button
              key={preset.label}
              onClick={() => onUpdateVibration(preset.val)}
              className={`py-2 px-3 rounded-lg border text-xs font-medium transition-all ${
                vibration === preset.val
                  ? 'bg-[#241F1D] text-[#FF8A5B] border-[#FF8A5B] font-semibold'
                  : 'bg-[#131110] text-[#D6CEC6] border-[#332C29] hover:border-[#453B36]'
              }`}
            >
              {preset.label} ({preset.val}%)
            </button>
          ))}
        </div>
      </div>
    </div>
  );
};
