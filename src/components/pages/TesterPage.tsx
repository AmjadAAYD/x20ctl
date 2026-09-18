import React from "react";
import { LiveGamepadState, KEY_LABELS, KeyName } from "../../types/gamepad";
import { Activity, Gauge, Zap } from "lucide-react";

interface TesterPageProps {
  liveState: LiveGamepadState;
}

export const TesterPage: React.FC<TesterPageProps> = ({ liveState }) => {
  const stickBoxSize = 160;
  const stickRadius = 60;
  const center = stickBoxSize / 2;

  return (
    <div className="space-y-6 max-w-5xl mx-auto">
      {/* Header with Hardware rate unavailable */}
      <div className="flex items-center justify-between pb-4 border-b border-[#332C29]">
        <div>
          <h2 className="text-base font-semibold text-[#F4F0EB]">
            Live Input Tester
          </h2>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Samples buttons, analog sticks with 2D history trails, triggers, and
            native XInput state.
          </p>
        </div>

        <span className="text-xs text-[#A79C92]">
          XInput states · hardware polling rate not measured
        </span>
      </div>

      {/* Main Tester Visualizer Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Left Stick Visualizer */}
        <div className="p-5 rounded-xl bg-[#1B1817] border border-[#332C29] flex flex-col items-center">
          <div className="w-full flex items-center justify-between mb-3 text-xs">
            <span className="font-semibold text-[#F4F0EB]">
              Left Stick (LS)
            </span>
            <span className="font-mono text-[#A79C92]">
              X: {liveState.leftStick.x.toFixed(3)} | Y:{" "}
              {liveState.leftStick.y.toFixed(3)}
            </span>
          </div>

          <div className="relative">
            <svg
              width={stickBoxSize}
              height={stickBoxSize}
              className="bg-[#131110] rounded-full border border-[#332C29]"
            >
              {/* Center crosshairs */}
              <line
                x1={center}
                y1={10}
                x2={center}
                y2={stickBoxSize - 10}
                stroke="#241F1D"
                strokeWidth="1"
              />
              <line
                x1={10}
                y1={center}
                x2={stickBoxSize - 10}
                y2={center}
                stroke="#241F1D"
                strokeWidth="1"
              />
              <circle
                cx={center}
                cy={center}
                r={stickRadius}
                fill="none"
                stroke="#241F1D"
                strokeWidth="1"
              />
              <circle
                cx={center}
                cy={center}
                r={stickRadius * 0.5}
                fill="none"
                stroke="#241F1D"
                strokeWidth="1"
                strokeDasharray="2 2"
              />

              {/* Trail */}
              {liveState.trail.left.map((pt, i) => (
                <circle
                  key={i}
                  cx={center + pt.x * stickRadius}
                  cy={center + pt.y * stickRadius}
                  r="2"
                  fill="#FF8A5B"
                  fillOpacity={((i + 1) / liveState.trail.left.length) * 0.4}
                />
              ))}

              {/* Current stick position */}
              <circle
                cx={center + liveState.leftStick.x * stickRadius}
                cy={center + liveState.leftStick.y * stickRadius}
                r="10"
                fill={liveState.buttons.L3 ? "#FF8A5B" : "#241F1D"}
                stroke={liveState.buttons.L3 ? "#171310" : "#FF8A5B"}
                strokeWidth="2"
              />
            </svg>
          </div>

          <div className="mt-3 flex items-center gap-2">
            <span
              className={`text-[10px] px-2 py-0.5 rounded font-mono border ${
                liveState.buttons.L3
                  ? "bg-[#FF8A5B] text-[#171310] border-[#FF8A5B] font-bold"
                  : "bg-[#131110] text-[#A79C92] border-[#332C29]"
              }`}
            >
              L3 Stick Click
            </span>
          </div>
        </div>

        {/* Right Stick Visualizer */}
        <div className="p-5 rounded-xl bg-[#1B1817] border border-[#332C29] flex flex-col items-center">
          <div className="w-full flex items-center justify-between mb-3 text-xs">
            <span className="font-semibold text-[#F4F0EB]">
              Right Stick (RS)
            </span>
            <span className="font-mono text-[#A79C92]">
              X: {liveState.rightStick.x.toFixed(3)} | Y:{" "}
              {liveState.rightStick.y.toFixed(3)}
            </span>
          </div>

          <div className="relative">
            <svg
              width={stickBoxSize}
              height={stickBoxSize}
              className="bg-[#131110] rounded-full border border-[#332C29]"
            >
              {/* Center crosshairs */}
              <line
                x1={center}
                y1={10}
                x2={center}
                y2={stickBoxSize - 10}
                stroke="#241F1D"
                strokeWidth="1"
              />
              <line
                x1={10}
                y1={center}
                x2={stickBoxSize - 10}
                y2={center}
                stroke="#241F1D"
                strokeWidth="1"
              />
              <circle
                cx={center}
                cy={center}
                r={stickRadius}
                fill="none"
                stroke="#241F1D"
                strokeWidth="1"
              />
              <circle
                cx={center}
                cy={center}
                r={stickRadius * 0.5}
                fill="none"
                stroke="#241F1D"
                strokeWidth="1"
                strokeDasharray="2 2"
              />

              {/* Trail */}
              {liveState.trail.right.map((pt, i) => (
                <circle
                  key={i}
                  cx={center + pt.x * stickRadius}
                  cy={center + pt.y * stickRadius}
                  r="2"
                  fill="#FF8A5B"
                  fillOpacity={((i + 1) / liveState.trail.right.length) * 0.4}
                />
              ))}

              {/* Current stick position */}
              <circle
                cx={center + liveState.rightStick.x * stickRadius}
                cy={center + liveState.rightStick.y * stickRadius}
                r="10"
                fill={liveState.buttons.R3 ? "#FF8A5B" : "#241F1D"}
                stroke={liveState.buttons.R3 ? "#171310" : "#FF8A5B"}
                strokeWidth="2"
              />
            </svg>
          </div>

          <div className="mt-3 flex items-center gap-2">
            <span
              className={`text-[10px] px-2 py-0.5 rounded font-mono border ${
                liveState.buttons.R3
                  ? "bg-[#FF8A5B] text-[#171310] border-[#FF8A5B] font-bold"
                  : "bg-[#131110] text-[#A79C92] border-[#332C29]"
              }`}
            >
              R3 Stick Click
            </span>
          </div>
        </div>

        {/* Analog Triggers Travel */}
        <div className="md:col-span-2 p-5 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-4">
          <span className="text-xs font-semibold text-[#F4F0EB] block">
            Analog Triggers Travel
          </span>

          <div className="grid grid-cols-1 sm:grid-cols-2 gap-6">
            {/* Left Trigger */}
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-[#D6CEC6]">
                  LT (Left Trigger)
                </span>
                <span className="font-mono text-[#FF8A5B] font-bold">
                  {(liveState.leftTrigger * 100).toFixed(1)}%
                </span>
              </div>
              <div className="h-4 rounded-full bg-[#131110] border border-[#332C29] overflow-hidden p-0.5">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-[#FF8A5B] to-[#FF8A5B] transition-all duration-75"
                  style={{
                    width: `${Math.min(100, liveState.leftTrigger * 100)}%`,
                  }}
                />
              </div>
            </div>

            {/* Right Trigger */}
            <div className="space-y-2">
              <div className="flex items-center justify-between text-xs">
                <span className="font-semibold text-[#D6CEC6]">
                  RT (Right Trigger)
                </span>
                <span className="font-mono text-[#FF8A5B] font-bold">
                  {(liveState.rightTrigger * 100).toFixed(1)}%
                </span>
              </div>
              <div className="h-4 rounded-full bg-[#131110] border border-[#332C29] overflow-hidden p-0.5">
                <div
                  className="h-full rounded-full bg-gradient-to-r from-[#FF8A5B] to-[#FF8A5B] transition-all duration-75"
                  style={{
                    width: `${Math.min(100, liveState.rightTrigger * 100)}%`,
                  }}
                />
              </div>
            </div>
          </div>
        </div>

        {/* Buttons State Matrix */}
        <div className="md:col-span-2 p-5 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-3">
          <span className="text-xs font-semibold text-[#F4F0EB] block">
            Button Press Matrix
          </span>

          <div className="grid grid-cols-2 sm:grid-cols-4 md:grid-cols-6 gap-2">
            {(Object.keys(liveState.buttons) as KeyName[]).map((btnKey) => {
              const pressed = liveState.buttons[btnKey];
              return (
                <button
                  key={btnKey}
                  type="button"
                  className={`p-2.5 rounded-lg border text-xs flex items-center justify-between font-mono transition-all ${
                    pressed
                      ? "bg-[#FF8A5B] text-[#171310] border-[#FF8A5B] font-bold shadow-[0_0_10px_rgba(255,138,91,0.4)]"
                      : "bg-[#131110] text-[#D6CEC6] border-[#332C29] hover:border-[#453B36]"
                  }`}
                >
                  <span className="truncate">{KEY_LABELS[btnKey]}</span>
                  <div
                    className={`w-2 h-2 rounded-full ${
                      pressed ? "bg-[#171310]" : "bg-[#241F1D]"
                    }`}
                  />
                </button>
              );
            })}
          </div>
          <p className="text-[11px] text-[#A79C92] pt-1">
            Press buttons on your connected gamepad to verify response. The
            tester reads native XInput; it does not measure USB polling rate.
          </p>
        </div>
      </div>
    </div>
  );
};
