import {
  Activity,
  Crosshair,
  Gamepad2,
  Radio,
  SlidersHorizontal,
} from "lucide-react";
import { LiveGamepadState, KEY_LABELS, KeyName } from "../../types/gamepad";
import "../metal-controls.css";

interface TesterPageProps {
  liveState: LiveGamepadState;
  inputConnected?: boolean;
  inputSlot?: number | null;
}
const TEST_KEYS: KeyName[] = [
  "LB",
  "RB",
  "DPAD_UP",
  "Y",
  "DPAD_LEFT",
  "DPAD_RIGHT",
  "X",
  "B",
  "DPAD_DOWN",
  "A",
  "L3",
  "R3",
  "SELECT",
  "START",
];
const KEY_SYMBOLS: Partial<Record<KeyName, string>> = {
  DPAD_UP: "↑",
  DPAD_DOWN: "↓",
  DPAD_LEFT: "←",
  DPAD_RIGHT: "→",
  SELECT: "BACK",
};

function StickRadar({
  label,
  value,
  trail,
  pressed,
  connected,
}: {
  label: string;
  value: { x: number; y: number };
  trail: Array<{ x: number; y: number }>;
  pressed: boolean;
  connected: boolean;
}) {
  return (
    <div className="tester-stick-module">
      <header>
        <h3>{label}</h3>
        <span
          className={`tester-click-indicator ${connected && pressed ? "is-on" : ""}`}
        >
          {connected ? (pressed ? "CLICKED" : "RELEASED") : "NO SIGNAL"}
        </span>
      </header>
      <svg
        viewBox="0 0 250 250"
        role="img"
        aria-label={`${label}: ${connected ? `X ${value.x.toFixed(3)}, Y ${value.y.toFixed(3)}` : "unavailable"}`}
        className="tester-stick-radar"
      >
        <circle cx="125" cy="125" r="113" className="tester-radar-rim" />
        <circle cx="125" cy="125" r="106" className="tester-radar-well" />
        {[25, 50, 75, 100].map((r) => (
          <circle
            key={r}
            cx="125"
            cy="125"
            r={r}
            fill="none"
            className="tester-radar-ring"
          />
        ))}
        <path
          d="M25 125H225M125 25V225M54.3 54.3 195.7 195.7M54.3 195.7 195.7 54.3"
          className="tester-radar-crosshair"
        />
        <text x="125" y="20" textAnchor="middle">
          Y
        </text>
        <text x="235" y="129" textAnchor="middle">
          X
        </text>
        {connected &&
          trail.map((point, index) => (
            <circle
              key={index}
              cx={125 + point.x * 100}
              cy={125 + point.y * 100}
              r="2.3"
              fill="var(--accent, #64dce4)"
              opacity={((index + 1) / trail.length) * 0.45}
            />
          ))}
        {connected && (
          <g>
            <line
              x1="125"
              y1="125"
              x2={125 + value.x * 100}
              y2={125 + value.y * 100}
              className="tester-radar-vector"
            />
            <circle
              cx={125 + value.x * 100}
              cy={125 + value.y * 100}
              r={pressed ? 8 : 6}
              className="tester-radar-cursor"
            />
          </g>
        )}
        {!connected && (
          <text
            x="125"
            y="130"
            textAnchor="middle"
            className="tester-radar-offline"
          >
            WAITING FOR INPUT
          </text>
        )}
      </svg>
      <div className="tester-coordinate-strip">
        <span>
          X <strong>{connected ? value.x.toFixed(3) : "—"}</strong>
        </span>
        <span>
          Y <strong>{connected ? value.y.toFixed(3) : "—"}</strong>
        </span>
        <span>
          TRAVEL{" "}
          <strong>
            {connected
              ? `${Math.round(Math.min(1, Math.hypot(value.x, value.y)) * 100)}%`
              : "—"}
          </strong>
        </span>
      </div>
    </div>
  );
}

export function TesterPage({
  liveState,
  inputConnected = false,
  inputSlot,
}: TesterPageProps) {
  const pressed = TEST_KEYS.filter(
    (key) => inputConnected && liveState.buttons[key],
  );
  return (
    <div className="tester-console">
      <section className="tester-signal-bar">
        <div>
          <Radio size={18} />
          <span>
            XINPUT SIGNAL
            <strong>
              {inputConnected
                ? `Player ${(inputSlot ?? 0) + 1} detected`
                : "Waiting for a gamepad"}
            </strong>
          </span>
        </div>
        <span className={`mapping-status ${inputConnected ? "is-live" : ""}`}>
          <i />
          {inputConnected ? "LIVE" : "NO SIGNAL"}
        </span>
        <p>
          {inputConnected
            ? "Move the sticks, squeeze the triggers and press a button."
            : "Connect your controller to Windows by USB, receiver or a supported XInput mode."}
        </p>
      </section>
      <div className="tester-primary-grid">
        <section className="tester-panel tester-stick-panel">
          <header className="mapping-panel-heading">
            <div>
              <Crosshair size={17} />
              <h2>Stick coordinates</h2>
            </div>
            <span className="tester-heading-detail">X / Y AXES</span>
          </header>
          <div className="tester-stick-pair">
            <StickRadar
              label="Left stick"
              value={liveState.leftStick}
              trail={liveState.trail.left}
              pressed={!!liveState.buttons.L3}
              connected={inputConnected}
            />
            <StickRadar
              label="Right stick"
              value={liveState.rightStick}
              trail={liveState.trail.right}
              pressed={!!liveState.buttons.R3}
              connected={inputConnected}
            />
          </div>
          <footer className="tester-panel-caption">
            The fading trace shows recent stick positions. Stick clicks light
            the center point.
          </footer>
        </section>
        <section className="tester-panel tester-buttons-panel">
          <header className="mapping-panel-heading">
            <div>
              <Gamepad2 size={17} />
              <h2>Button actuation</h2>
            </div>
            <span className="mapping-count">{pressed.length} active</span>
          </header>
          <div className="tester-button-matrix">
            {TEST_KEYS.map((key) => (
              <div
                key={key}
                className={`tester-button-indicator ${inputConnected && liveState.buttons[key] ? "is-pressed" : ""}`}
                title={KEY_LABELS[key]}
                aria-label={`${KEY_LABELS[key]}: ${inputConnected ? (liveState.buttons[key] ? "pressed" : "released") : "unavailable"}`}
              >
                <span>{KEY_SYMBOLS[key] ?? key}</span>
                <i />
              </div>
            ))}
          </div>
          <p className="tester-buttons-caption">
            {inputConnected
              ? pressed.length
                ? `Pressed: ${pressed.map((key) => KEY_LABELS[key]).join(", ")}`
                : "All buttons released"
              : "Button state unavailable"}
          </p>
        </section>
      </div>
      <div className="tester-secondary-grid">
        <section className="tester-panel tester-trigger-panel">
          <header className="mapping-panel-heading">
            <div>
              <SlidersHorizontal size={17} />
              <h2>Analog trigger travel</h2>
            </div>
            <span className="tester-heading-detail">0–100%</span>
          </header>
          <div className="tester-trigger-pair">
            {(
              [
                ["LT", liveState.leftTrigger],
                ["RT", liveState.rightTrigger],
              ] as const
            ).map(([label, value]) => (
              <div key={label} className="tester-trigger-module">
                <div className="tester-trigger-label">
                  <span>
                    {label}
                    <small>
                      {label === "LT" ? "LEFT TRIGGER" : "RIGHT TRIGGER"}
                    </small>
                  </span>
                  <strong>
                    {inputConnected ? (value * 100).toFixed(1) : "—"}
                    <small>{inputConnected ? "%" : ""}</small>
                  </strong>
                </div>
                <div
                  className="tester-trigger-track"
                  role="meter"
                  aria-label={`${label} travel`}
                  aria-valuemin={0}
                  aria-valuemax={100}
                  aria-valuenow={inputConnected ? value * 100 : undefined}
                  aria-valuetext={
                    inputConnected
                      ? `${(value * 100).toFixed(1)} percent`
                      : "Unavailable"
                  }
                >
                  <i
                    style={{
                      width: `${inputConnected ? Math.max(0, Math.min(100, value * 100)) : 0}%`,
                    }}
                  />
                </div>
                <div className="tester-trigger-scale">
                  <span>0</span>
                  <span>25</span>
                  <span>50</span>
                  <span>75</span>
                  <span>100</span>
                </div>
              </div>
            ))}
          </div>
        </section>
        <section className="tester-panel tester-signal-details">
          <header className="mapping-panel-heading">
            <div>
              <Activity size={17} />
              <h2>Signal details</h2>
            </div>
          </header>
          <dl>
            <div>
              <dt>Input source</dt>
              <dd>Windows XInput</dd>
            </div>
            <div>
              <dt>Player slot</dt>
              <dd>
                {inputConnected ? `P${(inputSlot ?? 0) + 1}` : "Unavailable"}
              </dd>
            </div>
            <div>
              <dt>Hardware polling rate</dt>
              <dd>Not measured</dd>
            </div>
          </dl>
          <p>
            Gameplay input is separate from the Bluetooth configuration link.
          </p>
        </section>
      </div>
    </div>
  );
}
