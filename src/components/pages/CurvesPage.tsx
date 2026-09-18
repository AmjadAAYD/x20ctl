import { curvePreview } from "../../curve";
import React, { useState } from "react";
import { CurveConfig, LiveGamepadState } from "../../types/gamepad";
import {
  Sliders,
  RotateCcw,
  Zap,
  Activity,
  HelpCircle,
  Edit3,
  Check,
} from "lucide-react";
import { LiveStickModule } from "../LiveStickModule";
import { LiveTriggerModule } from "../LiveTriggerModule";
import { CurvesHelpModal } from "../CurvesHelpModal";

interface CurvesPageProps {
  stickCurves: {
    left: CurveConfig;
    right: CurveConfig;
  };
  triggerCurves: {
    left: CurveConfig;
    right: CurveConfig;
  };
  onUpdateStickCurve: (which: "left" | "right", config: CurveConfig) => void;
  onUpdateTriggerCurve: (which: "left" | "right", config: CurveConfig) => void;
  liveState: LiveGamepadState;
}

type ChannelKey = "leftStick" | "rightStick" | "leftTrigger" | "rightTrigger";

export const CurvesPage: React.FC<CurvesPageProps> = ({
  stickCurves,
  triggerCurves,
  onUpdateStickCurve,
  onUpdateTriggerCurve,
  liveState,
}) => {
  const [activeChannel, setActiveChannel] = useState<ChannelKey>("leftStick");
  const [isHelpOpen, setIsHelpOpen] = useState<boolean>(false);
  const [isEditCurveOpen, setIsEditCurveOpen] = useState<boolean>(false);

  const getChannelConfig = (ch: ChannelKey): CurveConfig => {
    switch (ch) {
      case "leftStick":
        return stickCurves.left;
      case "rightStick":
        return stickCurves.right;
      case "leftTrigger":
        return triggerCurves.left;
      case "rightTrigger":
        return triggerCurves.right;
    }
  };

  const updateChannelConfig = (
    ch: ChannelKey,
    updates: Partial<CurveConfig>,
  ) => {
    const current = getChannelConfig(ch);
    const updated: CurveConfig = { ...current, ...updates };

    if (ch === "leftStick") onUpdateStickCurve("left", updated);
    else if (ch === "rightStick") onUpdateStickCurve("right", updated);
    else if (ch === "leftTrigger") onUpdateTriggerCurve("left", updated);
    else if (ch === "rightTrigger") onUpdateTriggerCurve("right", updated);
  };

  const currentConfig = getChannelConfig(activeChannel);

  // Live input magnitude (0.0 to 1.0)
  let liveMagnitude = 0;
  if (activeChannel === "leftStick") {
    liveMagnitude = Math.min(
      1,
      Math.hypot(liveState.leftStick.x, liveState.leftStick.y),
    );
  } else if (activeChannel === "rightStick") {
    liveMagnitude = Math.min(
      1,
      Math.hypot(liveState.rightStick.x, liveState.rightStick.y),
    );
  } else if (activeChannel === "leftTrigger") {
    liveMagnitude = liveState.leftTrigger;
  } else {
    liveMagnitude = liveState.rightTrigger;
  }

  // Presets applicator
  const applyPreset = (preset: CurveConfig["preset"]) => {
    if (preset === "linear") {
      updateChannelConfig(activeChannel, {
        preset: "linear",
        p1: { x: 100 / 3, y: 100 / 3 },
        p2: { x: 200 / 3, y: 200 / 3 },
      });
    } else if (preset === "instant") {
      updateChannelConfig(activeChannel, {
        preset: "instant",
        p1: { x: 15, y: 70 },
        p2: { x: 45, y: 98 },
        innerDeadzone: 2,
        outerDeadzone: 75,
      });
    } else if (preset === "relaxed") {
      updateChannelConfig(activeChannel, {
        preset: "relaxed",
        p1: { x: 50, y: 25 },
        p2: { x: 80, y: 65 },
      });
    } else if (preset === "aggressive") {
      updateChannelConfig(activeChannel, {
        preset: "aggressive",
        p1: { x: 25, y: 45 },
        p2: { x: 65, y: 85 },
      });
    }
  };

  // SVG dimensions for curve
  const svgSize = 260;
  const padding = 28;
  const plotSize = svgSize - padding * 2;

  const toSvgX = (xPct: number) => padding + (xPct / 100) * plotSize;
  const toSvgY = (yPct: number) => padding + plotSize - (yPct / 100) * plotSize;

  const p0 = { x: toSvgX(0), y: toSvgY(0) };
  const p1 = { x: toSvgX(currentConfig.p1.x), y: toSvgY(currentConfig.p1.y) };
  const p2 = { x: toSvgX(currentConfig.p2.x), y: toSvgY(currentConfig.p2.y) };
  const p3 = { x: toSvgX(100), y: toSvgY(100) };

  const liveX = liveMagnitude * 100;
  const liveY = curvePreview(currentConfig, liveX);
  const curvePath = Array.from(
    { length: 101 },
    (_, x) =>
      `${x ? "L" : "M"} ${toSvgX(x)} ${toSvgY(curvePreview(currentConfig, x))}`,
  ).join(" ");

  const isStick =
    activeChannel === "leftStick" || activeChannel === "rightStick";

  return (
    <div className="space-y-6 max-w-5xl mx-auto select-none">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-4 border-b border-[#332C29]">
        <div>
          <h2 className="text-base font-bold text-[#F4F0EB]">
            Sticks & Triggers Response Curves
          </h2>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Edit stored control points. The curve is an illustration, not
            measured firmware output.
          </p>
        </div>

        {/* Action Buttons: "Don't understand what to do?" + Channel Selector */}
        <div className="flex items-center gap-2">
          {/* ELIF Simplified Guide Button */}
          <button
            onClick={() => setIsHelpOpen(true)}
            id="curves-help-btn"
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs font-bold rounded-lg bg-[#FF8A5B]/15 hover:bg-[#FF8A5B]/25 text-[#FF8A5B] border border-[#FF8A5B]/40 transition-all shadow-sm"
            title="How response curves work"
          >
            <HelpCircle className="w-3.5 h-3.5" />
            <span>Curve guide</span>
          </button>
        </div>
      </div>

      {/* Channel Switcher */}
      <div className="flex items-center justify-between">
        <div className="flex rounded-xl bg-[#141211] p-1 border border-[#332C29]">
          {(
            [
              ["leftStick", "Left Stick"],
              ["rightStick", "Right Stick"],
              ["leftTrigger", "Left Trigger (LT)"],
              ["rightTrigger", "Right Trigger (RT)"],
            ] as const
          ).map(([key, label]) => (
            <button
              key={key}
              onClick={() => setActiveChannel(key)}
              className={`px-3.5 py-1.5 text-xs rounded-lg font-bold transition-all ${
                activeChannel === key
                  ? "bg-[#241F1D] text-[#FF8A5B] shadow-sm border border-[#FF8A5B]/30"
                  : "text-[#A79C92] hover:text-[#F4F0EB]"
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* Edit Curve Toggle Button */}
        <button
          onClick={() => setIsEditCurveOpen(!isEditCurveOpen)}
          className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-bold rounded-lg transition-all border ${
            isEditCurveOpen
              ? "bg-[#FF8A5B] text-[#131110] border-[#FF8A5B]"
              : "bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] border-[#332C29]"
          }`}
        >
          <Edit3 className="w-3.5 h-3.5" />
          <span>{isEditCurveOpen ? "Done Editing Curve" : "Edit Curve"}</span>
        </button>
      </div>

      {/* Main Two-Column Layout: Response Curve on Left, Live SVG Visualizer on Right */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Column (Col 7): Curve illustration & Controls */}
        <div className="lg:col-span-7 p-5 rounded-2xl bg-[#1B1817] border border-[#332C29] shadow-lg flex flex-col items-center">
          <div className="w-full flex items-center justify-between mb-3">
            <span className="text-xs font-bold text-[#F4F0EB]">
              Response Curve Graph
            </span>
            <div className="flex items-center gap-2 text-xs">
              <span className="text-[#A79C92]">Preview:</span>
              <span className="font-mono text-[#86C08A] font-bold">
                {(liveY / 100).toFixed(2)} ({Math.round(liveMagnitude * 100)}%
                In)
              </span>
            </div>
          </div>

          {/* Curve SVG */}
          <div className="relative select-none">
            <svg
              width={svgSize}
              height={svgSize}
              className="bg-[#131110] rounded-xl border border-[#332C29]"
            >
              {/* Grid lines */}
              <line
                x1={padding}
                y1={padding + plotSize / 2}
                x2={padding + plotSize}
                y2={padding + plotSize / 2}
                stroke="#241F1D"
                strokeWidth="1"
                strokeDasharray="4"
              />
              <line
                x1={padding + plotSize / 2}
                y1={padding}
                x2={padding + plotSize / 2}
                y2={padding + plotSize}
                stroke="#241F1D"
                strokeWidth="1"
                strokeDasharray="4"
              />

              {/* Deadzone regions */}
              <rect
                x={padding}
                y={padding}
                width={(currentConfig.innerDeadzone / 100) * plotSize}
                height={plotSize}
                fill="#FF8A5B"
                fillOpacity="0.08"
              />
              <rect
                x={padding + (currentConfig.outerDeadzone / 100) * plotSize}
                y={padding}
                width={(1 - currentConfig.outerDeadzone / 100) * plotSize}
                height={plotSize}
                fill="#86C08A"
                fillOpacity="0.08"
              />

              {/* Linear reference diagonal */}
              <line
                x1={toSvgX(0)}
                y1={toSvgY(0)}
                x2={toSvgX(100)}
                y2={toSvgY(100)}
                stroke="#453B36"
                strokeWidth="1.5"
                strokeDasharray="4 4"
              />

              {/* Curve path */}
              <path
                d={curvePath}
                fill="none"
                stroke="#FF8A5B"
                strokeWidth="3"
              />

              {/* Control Points P1 and P2 */}
              <circle
                cx={p1.x}
                cy={p1.y}
                r="6.5"
                fill="#FF8A5B"
                stroke="#131110"
                strokeWidth="2"
                className="cursor-pointer hover:scale-125 transition-transform"
              />
              <circle
                cx={p2.x}
                cy={p2.y}
                r="6.5"
                fill="#FF8A5B"
                stroke="#131110"
                strokeWidth="2"
                className="cursor-pointer hover:scale-125 transition-transform"
              />

              {/* Live position tracking point */}
              <circle
                cx={toSvgX(liveX)}
                cy={toSvgY(liveY)}
                r="7.5"
                fill="#86C08A"
                stroke="#131110"
                strokeWidth="2"
                className="shadow-sm"
              />

              {/* Labels */}
              <text x={padding} y={svgSize - 8} fill="#A79C92" fontSize="9">
                0% Center
              </text>
              <text
                x={svgSize - padding}
                y={svgSize - 8}
                fill="#A79C92"
                fontSize="9"
                textAnchor="end"
              >
                100% Edge
              </text>
            </svg>
          </div>

          {/* Presets Row */}
          <div className="w-full mt-4 flex items-center justify-center gap-2">
            {(["linear", "aggressive", "relaxed", "instant"] as const).map(
              (pr) => (
                <button
                  key={pr}
                  onClick={() => applyPreset(pr)}
                  className={`px-2.5 py-1 text-xs rounded-lg capitalize font-semibold transition-all border ${
                    currentConfig.preset === pr
                      ? "bg-[#FF8A5B] text-[#131110] border-[#FF8A5B]"
                      : "bg-[#141211] text-[#A79C92] border-[#332C29] hover:text-[#F4F0EB]"
                  }`}
                >
                  {pr}
                </button>
              ),
            )}
          </div>

          {/* Edit Curve Fine Tuning Controls (revealed when Edit Curve is active) */}
          {isEditCurveOpen && (
            <div className="w-full mt-4 pt-4 border-t border-[#332C29] grid grid-cols-2 gap-4 text-xs">
              <div className="p-3 rounded-xl bg-[#141211] border border-[#332C29] space-y-2">
                <span className="font-bold text-[#FF8A5B] block">
                  P1 Control Point
                </span>
                <div className="flex items-center justify-between text-[11px] text-[#A79C92]">
                  <span>X: {Number(currentConfig.p1.x.toFixed(1))}%</span>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={currentConfig.p1.x}
                    onChange={(e) =>
                      updateChannelConfig(activeChannel, {
                        p1: {
                          ...currentConfig.p1,
                          x: Math.min(
                            currentConfig.p2.x,
                            parseInt(e.target.value),
                          ),
                        },
                        preset: "custom",
                      })
                    }
                    className="w-24 accent-[#FF8A5B]"
                  />
                </div>
                <div className="flex items-center justify-between text-[11px] text-[#A79C92]">
                  <span>Y: {Number(currentConfig.p1.y.toFixed(1))}%</span>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={currentConfig.p1.y}
                    onChange={(e) =>
                      updateChannelConfig(activeChannel, {
                        p1: {
                          ...currentConfig.p1,
                          y: parseInt(e.target.value),
                        },
                        preset: "custom",
                      })
                    }
                    className="w-24 accent-[#FF8A5B]"
                  />
                </div>
              </div>

              <div className="p-3 rounded-xl bg-[#141211] border border-[#332C29] space-y-2">
                <span className="font-bold text-[#FF8A5B] block">
                  P2 Control Point
                </span>
                <div className="flex items-center justify-between text-[11px] text-[#A79C92]">
                  <span>X: {Number(currentConfig.p2.x.toFixed(1))}%</span>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={currentConfig.p2.x}
                    onChange={(e) =>
                      updateChannelConfig(activeChannel, {
                        p2: {
                          ...currentConfig.p2,
                          x: Math.max(
                            currentConfig.p1.x,
                            parseInt(e.target.value),
                          ),
                        },
                        preset: "custom",
                      })
                    }
                    className="w-24 accent-[#FF8A5B]"
                  />
                </div>
                <div className="flex items-center justify-between text-[11px] text-[#A79C92]">
                  <span>Y: {Number(currentConfig.p2.y.toFixed(1))}%</span>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={currentConfig.p2.y}
                    onChange={(e) =>
                      updateChannelConfig(activeChannel, {
                        p2: {
                          ...currentConfig.p2,
                          y: parseInt(e.target.value),
                        },
                        preset: "custom",
                      })
                    }
                    className="w-24 accent-[#FF8A5B]"
                  />
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Right Column (Col 5): Live Hardware Test Visualizer & Deadzone Sliders */}
        <div className="lg:col-span-5 space-y-4">
          {/* Hardware Live SVG Test Section */}
          {isStick ? (
            <LiveStickModule
              label={
                activeChannel === "leftStick" ? "Left Stick" : "Right Stick"
              }
              x={
                activeChannel === "leftStick"
                  ? liveState.leftStick.x
                  : liveState.rightStick.x
              }
              y={
                activeChannel === "leftStick"
                  ? liveState.leftStick.y
                  : liveState.rightStick.y
              }
              innerDeadzonePercent={currentConfig.innerDeadzone}
              outerDeadzonePercent={currentConfig.outerDeadzone}
            />
          ) : (
            <LiveTriggerModule
              label={
                activeChannel === "leftTrigger"
                  ? "Left Trigger (LT)"
                  : "Right Trigger (RT)"
              }
              value={
                activeChannel === "leftTrigger"
                  ? liveState.leftTrigger
                  : liveState.rightTrigger
              }
              hairTrigger={currentConfig.preset === "instant"}
            />
          )}

          {/* Deadzone Sliders */}
          <div className="p-4 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-3">
            <span className="text-xs font-bold text-[#F4F0EB] block">
              Hardware Deadzones
            </span>

            {/* Inner deadzone slider */}
            <div className="space-y-1">
              <div className="flex justify-between text-xs">
                <span className="text-[#A79C92]">Inner Deadzone (Slack)</span>
                <span className="font-mono text-[#FF8A5B] font-bold">
                  {Number(currentConfig.innerDeadzone.toFixed(1))}%
                </span>
              </div>
              <input
                type="range"
                min="0"
                max="30"
                value={currentConfig.innerDeadzone}
                onChange={(e) =>
                  updateChannelConfig(activeChannel, {
                    innerDeadzone: parseInt(e.target.value),
                  })
                }
                className="w-full accent-[#FF8A5B] h-1.5 bg-[#241F1D] rounded cursor-pointer"
              />
            </div>

            {/* Outer deadzone slider */}
            <div className="space-y-1">
              <div className="flex justify-between text-xs">
                <span className="text-[#A79C92]">
                  Outer Deadzone (Max Push)
                </span>
                <span className="font-mono text-[#86C08A] font-bold">
                  {Number(currentConfig.outerDeadzone.toFixed(1))}%
                </span>
              </div>
              <input
                type="range"
                min="70"
                max="100"
                value={currentConfig.outerDeadzone}
                onChange={(e) =>
                  updateChannelConfig(activeChannel, {
                    outerDeadzone: parseInt(e.target.value),
                  })
                }
                className="w-full accent-[#86C08A] h-1.5 bg-[#241F1D] rounded cursor-pointer"
              />
            </div>
          </div>
        </div>
      </div>

      {/* ELIF Guide Modal */}
      <CurvesHelpModal
        isOpen={isHelpOpen}
        onClose={() => setIsHelpOpen(false)}
      />
    </div>
  );
};
