import { useId, useState } from "react";
import {
  Activity,
  HelpCircle,
  SlidersHorizontal,
  Crosshair,
  ArrowDownToLine,
} from "lucide-react";
import { curvePreview } from "../../curve";
import type { CurveConfig, LiveGamepadState } from "../../types/gamepad";
import { LiveStickModule } from "../LiveStickModule";
import { LiveTriggerModule } from "../LiveTriggerModule";
import { CurvesHelpModal } from "../CurvesHelpModal";
import "../metal-curves.css";

interface CurvesPageProps {
  stickCurves: { left: CurveConfig; right: CurveConfig };
  triggerCurves: { left: CurveConfig; right: CurveConfig };
  onUpdateStickCurve: (which: "left" | "right", config: CurveConfig) => void;
  onUpdateTriggerCurve: (which: "left" | "right", config: CurveConfig) => void;
  liveState: LiveGamepadState;
  liveConnected?: boolean;
}

type ChannelKey = "leftStick" | "rightStick" | "leftTrigger" | "rightTrigger";
const channels = [
  {
    key: "leftStick",
    label: "Left Stick",
    short: "LS",
    description: "Horizontal + vertical",
  },
  {
    key: "rightStick",
    label: "Right Stick",
    short: "RS",
    description: "Horizontal + vertical",
  },
  {
    key: "leftTrigger",
    label: "Left Trigger (LT)",
    short: "LT",
    description: "Analog travel",
  },
  {
    key: "rightTrigger",
    label: "Right Trigger (RT)",
    short: "RT",
    description: "Analog travel",
  },
] as const;

export function CurvesPage({
  stickCurves,
  triggerCurves,
  onUpdateStickCurve,
  onUpdateTriggerCurve,
  liveState,
  liveConnected,
}: CurvesPageProps) {
  const [activeChannel, setActiveChannel] = useState<ChannelKey>("leftStick");
  const [isHelpOpen, setIsHelpOpen] = useState(false);
  const [isEditCurveOpen, setIsEditCurveOpen] = useState(false);
  const chartId = useId().replace(/:/g, "");
  const channel = channels.find((item) => item.key === activeChannel)!;
  const isStick =
    activeChannel === "leftStick" || activeChannel === "rightStick";
  const which =
    activeChannel === "leftStick" || activeChannel === "leftTrigger"
      ? "left"
      : "right";
  const config = isStick ? stickCurves[which] : triggerCurves[which];
  const stick = which === "left" ? liveState.leftStick : liveState.rightStick;
  const input = isStick
    ? Math.min(1, Math.hypot(stick.x, stick.y))
    : which === "left"
      ? liveState.leftTrigger
      : liveState.rightTrigger;
  const update = (changes: Partial<CurveConfig>) =>
    (isStick ? onUpdateStickCurve : onUpdateTriggerCurve)(which, {
      ...config,
      ...changes,
    });

  const applyPreset = (preset: CurveConfig["preset"]) => {
    if (preset === "linear")
      update({
        preset,
        p1: { x: 100 / 3, y: 100 / 3 },
        p2: { x: 200 / 3, y: 200 / 3 },
      });
    if (preset === "aggressive")
      update({ preset, p1: { x: 25, y: 45 }, p2: { x: 65, y: 85 } });
    if (preset === "relaxed")
      update({ preset, p1: { x: 50, y: 25 }, p2: { x: 80, y: 65 } });
    if (preset === "instant")
      update({
        preset,
        p1: { x: 15, y: 70 },
        p2: { x: 45, y: 98 },
        innerDeadzone: 2,
        outerDeadzone: 75,
      });
  };
  const x = (value: number) => 46 + value * 3.15;
  const y = (value: number) => 235 - value * 2.05;
  const curvePath = Array.from(
    { length: 101 },
    (_, value) =>
      `${value ? "L" : "M"} ${x(value)} ${y(curvePreview(config, value))}`,
  ).join(" ");
  const display = (value: number) => Number(value.toFixed(1));

  return (
    <section className="curve-workbench" aria-label="Response curves workbench">
      <header className="curve-page-heading">
        <div>
          <span className="curve-eyebrow">Signal shaping</span>
          <h2>Response curves</h2>
          <p>
            Fine-tune each stick and trigger. Changes stay in your draft until
            applied.
          </p>
        </div>
        <button
          className="curve-button"
          id="curves-help-btn"
          onClick={() => setIsHelpOpen(true)}
        >
          <HelpCircle size={15} />
          Curve guide
        </button>
      </header>
      <div
        className="curve-channel-rail"
        role="group"
        aria-label="Input channel"
      >
        {channels.map((item) => (
          <button
            key={item.key}
            className={`curve-channel ${activeChannel === item.key ? "is-active" : ""}`}
            aria-pressed={activeChannel === item.key}
            onClick={() => setActiveChannel(item.key)}
          >
            <span className="curve-channel-mark">{item.short}</span>
            <span>
              <strong>{item.label}</strong>
              <small>{item.description}</small>
            </span>
            <span className="curve-channel-led" />
          </button>
        ))}
      </div>
      <div className="curve-console-grid">
        <section className="curve-panel curve-chart-panel">
          <header className="curve-panel-heading">
            <span>
              <Activity size={15} />
              Response editor
            </span>
            <span className="curve-tag">Draft preview</span>
          </header>
          <div className="curve-graph-wrap">
            <svg
              className="curve-graph"
              viewBox="0 0 400 270"
              role="img"
              aria-label={`${channel.label} response curve preview, not measured firmware output`}
            >
              <defs>
                <linearGradient id={chartId} x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#64dce4" stopOpacity=".2" />
                  <stop offset="100%" stopColor="#64dce4" stopOpacity=".015" />
                </linearGradient>
              </defs>
              {[0, 25, 50, 75, 100].map((tick) => (
                <g key={tick} className="curve-grid-line">
                  <line x1={x(tick)} x2={x(tick)} y1={y(100)} y2={y(0)} />
                  <line x1={x(0)} x2={x(100)} y1={y(tick)} y2={y(tick)} />
                  <text x={x(tick)} y={251} textAnchor="middle">
                    {tick}
                  </text>
                  <text x={34} y={y(tick) + 3} textAnchor="end">
                    {tick}
                  </text>
                </g>
              ))}
              <rect
                x={x(0)}
                y={y(100)}
                width={config.innerDeadzone * 3.15}
                height={205}
                fill="#b9c1c9"
                fillOpacity=".1"
              />
              <rect
                x={x(config.outerDeadzone)}
                y={y(100)}
                width={(100 - config.outerDeadzone) * 3.15}
                height={205}
                fill="#64dce4"
                fillOpacity=".07"
              />
              <line
                x1={x(0)}
                y1={y(0)}
                x2={x(100)}
                y2={y(100)}
                stroke="#77818b"
                strokeDasharray="4 5"
                opacity=".6"
              />
              <path
                d={`${curvePath} L ${x(100)} ${y(0)} Z`}
                fill={`url(#${chartId})`}
              />
              <path
                d={curvePath}
                fill="none"
                stroke="#64dce4"
                strokeWidth="2.5"
              />
              {(["p1", "p2"] as const).map((point, i) => (
                <g key={point}>
                  <circle
                    cx={x(config[point].x)}
                    cy={y(config[point].y)}
                    r="5"
                    fill="#dce5e9"
                    stroke="#0d171b"
                    strokeWidth="2"
                  />
                  <text
                    x={x(config[point].x) + 10}
                    y={y(config[point].y) - 8}
                    fill="#dce5e9"
                    fontSize="10"
                  >
                    P{i + 1}
                  </text>
                </g>
              ))}
              {liveConnected !== false && (
                <circle
                  cx={x(input * 100)}
                  cy={y(curvePreview(config, input * 100))}
                  r="5"
                  fill="#101519"
                  stroke="#64dce4"
                  strokeWidth="2"
                />
              )}
              <text x="46" y="16" className="curve-axis-label">
                OUTPUT %
              </text>
              <text
                x="361"
                y="267"
                textAnchor="end"
                className="curve-axis-label"
              >
                INPUT %
              </text>
            </svg>
            <div className="curve-chart-readouts">
              <span>
                Windows input{" "}
                <strong>
                  {liveConnected === false
                    ? "Unavailable"
                    : `${Math.round(input * 100)}%`}
                </strong>
              </span>
              <span>
                Curve <strong>{config.preset}</strong>
              </span>
            </div>
          </div>
          <div
            className="curve-presets"
            role="group"
            aria-label="Response presets"
          >
            {(["linear", "aggressive", "relaxed", "instant"] as const).map(
              (preset) => (
                <button
                  key={preset}
                  className={`curve-button ${config.preset === preset ? "is-active" : ""}`}
                  aria-pressed={config.preset === preset}
                  onClick={() => applyPreset(preset)}
                >
                  {preset}
                </button>
              ),
            )}
          </div>
          <p className="curve-caption">
            Illustration through stored P1 / P2 points. Firmware interpolation
            may differ.
          </p>
        </section>
        <div className="curve-side-stack">
          {isStick ? (
            <LiveStickModule
              label={channel.label}
              x={stick.x}
              y={stick.y}
              innerDeadzonePercent={config.innerDeadzone}
              outerDeadzonePercent={config.outerDeadzone}
              connected={liveConnected}
            />
          ) : (
            <LiveTriggerModule
              label={channel.label}
              value={input}
              hairTrigger={config.preset === "instant"}
              connected={liveConnected}
            />
          )}
          <section className="curve-panel curve-deadzones">
            <header className="curve-panel-heading">
              <span>
                <Crosshair size={15} />
                Travel limits
              </span>
              <span className="curve-tag">%</span>
            </header>
            <div className="curve-slider-field">
              <label htmlFor="curve-inner">
                Inner deadzone <output>{display(config.innerDeadzone)}%</output>
              </label>
              <input
                id="curve-inner"
                type="range"
                min="0"
                max="30"
                value={config.innerDeadzone}
                onChange={(event) =>
                  update({ innerDeadzone: Number(event.target.value) })
                }
              />
              <p>Ignore small movements near the center.</p>
            </div>
            <div className="curve-slider-field">
              <label htmlFor="curve-outer">
                Full-output threshold{" "}
                <output>{display(config.outerDeadzone)}%</output>
              </label>
              <input
                id="curve-outer"
                type="range"
                min="70"
                max="100"
                value={config.outerDeadzone}
                onChange={(event) =>
                  update({ outerDeadzone: Number(event.target.value) })
                }
              />
              <p>Reach maximum output at this input level.</p>
            </div>
          </section>
        </div>
      </div>
      <section className="curve-panel curve-point-editor">
        <header className="curve-panel-heading">
          <span>
            <SlidersHorizontal size={15} />
            Control points
          </span>
          <button
            className="curve-button curve-button-small"
            aria-expanded={isEditCurveOpen}
            aria-controls="curve-point-controls"
            onClick={() => setIsEditCurveOpen(!isEditCurveOpen)}
          >
            {isEditCurveOpen ? "Done Editing Curve" : "Edit Curve"}
          </button>
        </header>
        {isEditCurveOpen ? (
          <div id="curve-point-controls" className="curve-point-grid">
            {(["p1", "p2"] as const).map((point, i) => (
              <div key={point} className="curve-point">
                <h3>P{i + 1} Control Point</h3>
                {(["x", "y"] as const).map((axis) => (
                  <div className="curve-slider-field" key={axis}>
                    <label htmlFor={`curve-${point}-${axis}`}>
                      {axis === "x" ? "Input" : "Output"}{" "}
                      <output>{display(config[point][axis])}%</output>
                    </label>
                    <input
                      id={`curve-${point}-${axis}`}
                      type="range"
                      min="0"
                      max="100"
                      value={config[point][axis]}
                      onChange={(event) => {
                        let value = Number(event.target.value);
                        if (axis === "x")
                          value =
                            point === "p1"
                              ? Math.min(config.p2.x, value)
                              : Math.max(config.p1.x, value);
                        update({
                          [point]: { ...config[point], [axis]: value },
                          preset: "custom",
                        });
                      }}
                    />
                  </div>
                ))}
              </div>
            ))}
          </div>
        ) : (
          <div className="curve-point-summary">
            <span>
              P1{" "}
              <strong>
                {display(config.p1.x)} / {display(config.p1.y)}
              </strong>
            </span>
            <span>
              P2{" "}
              <strong>
                {display(config.p2.x)} / {display(config.p2.y)}
              </strong>
            </span>
            <p>
              <ArrowDownToLine size={13} />
              Coordinates are input / output percentages.
            </p>
          </div>
        )}
      </section>
      <CurvesHelpModal
        isOpen={isHelpOpen}
        onClose={() => setIsHelpOpen(false)}
      />
    </section>
  );
}
