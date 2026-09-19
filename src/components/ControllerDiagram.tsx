import { useId, type KeyboardEvent } from "react";
import {
  KeyName,
  LiveGamepadState,
  ControllerPreset,
  KEY_LABELS,
} from "../types/gamepad";
import "./metal-controls.css";

interface ControllerDiagramProps {
  liveState: LiveGamepadState;
  onButtonClick?: (key: KeyName) => void;
  selectedKey?: KeyName | null;
  className?: string;
  themeAccent?: string;
  preset?: ControllerPreset;
}

/** Hand-drawn X20 front view. Its moving elements use live XInput values. */
export function ControllerDiagram({
  liveState,
  onButtonClick,
  selectedKey,
  className = "",
  themeAccent = "#64dce4",
  preset = "x20-pro-black",
}: ControllerDiagramProps) {
  const id = useId().replace(/:/g, "");
  const paint = (name: string) => `url(#${id}-${name})`;
  const active = (key: KeyName) =>
    selectedKey === key || !!liveState.buttons[key];
  const interactive = (key: KeyName) => ({
    role: onButtonClick ? "button" : undefined,
    tabIndex: onButtonClick ? 0 : undefined,
    "aria-label": `Select ${KEY_LABELS[key]}`,
    "aria-pressed": selectedKey === key,
    className: "controller-hit",
    onClick: () => onButtonClick?.(key),
    onKeyDown: (event: KeyboardEvent<SVGGElement>) => {
      if (event.key === "Enter" || event.key === " ") {
        event.preventDefault();
        onButtonClick?.(key);
      }
    },
  });
  const stick = (
    key: "L3" | "R3",
    x: number,
    y: number,
    state: { x: number; y: number },
  ) => (
    <g transform={`translate(${x} ${y})`} {...interactive(key)}>
      <title>{KEY_LABELS[key]}</title>
      <circle
        r="49"
        fill="#111519"
        stroke={active(key) ? themeAccent : "#747e86"}
        strokeWidth="2"
      />
      <circle r="44" fill={paint("ring")} />
      <circle r="39" fill="#15191d" stroke="#080b0d" strokeWidth="2" />
      <circle
        r="36"
        fill="none"
        stroke="#69737c"
        strokeWidth="3"
        strokeDasharray="1 3"
      />
      <g transform={`translate(${state.x * 11} ${state.y * 11})`}>
        <circle
          r="31"
          fill={paint("cap")}
          stroke={active(key) ? themeAccent : "#89949c"}
          strokeWidth="1.5"
        />
        <circle r="25" fill="none" stroke="#101418" strokeWidth="2" />
        <circle r="21" fill="none" stroke="#616970" strokeWidth=".7" />
        <circle r="17" fill="#22282d" stroke="#1b2025" />
        <text
          y="4"
          textAnchor="middle"
          fill={active(key) ? themeAccent : "#b7c1c8"}
          fontSize="10"
          fontWeight="600"
        >
          {key}
        </text>
      </g>
      {selectedKey === key && (
        <circle
          r="54"
          fill="none"
          stroke={themeAccent}
          strokeWidth="1"
          strokeDasharray="4 4"
        />
      )}
    </g>
  );
  return (
    <div className={`controller-diagram ${className}`}>
      <svg
        viewBox="0 0 700 455"
        aria-label="EasySMX X20 controller illustration"
        className="controller-svg"
      >
        <defs>
          <linearGradient id={`${id}-body`} x1="0" y1="0" x2=".8" y2="1">
            <stop
              stopColor={preset === "x20-pro-white" ? "#bdc6cd" : "#677078"}
            />
            <stop
              offset=".26"
              stopColor={preset === "x20-pro-white" ? "#929da6" : "#3e464e"}
            />
            <stop
              offset=".62"
              stopColor={preset === "x20-pro-white" ? "#64727e" : "#292f35"}
            />
            <stop offset="1" stopColor="#171c21" />
          </linearGradient>
          <linearGradient id={`${id}-edge`} x1="0" y1="0" x2="1" y2="1">
            <stop stopColor="#b0bac1" />
            <stop offset=".33" stopColor="#65717a" />
            <stop offset=".66" stopColor="#242d35" />
            <stop offset="1" stopColor="#818d96" />
          </linearGradient>
          <linearGradient id={`${id}-ring`} x1="0" y1="0" x2="1" y2="1">
            <stop stopColor="#f0f3f5" />
            <stop offset=".2" stopColor="#808e99" />
            <stop offset=".48" stopColor="#2f3942" />
            <stop offset=".73" stopColor="#c7d0d6" />
            <stop offset="1" stopColor="#52616e" />
          </linearGradient>
          <radialGradient id={`${id}-cap`} cx=".35" cy=".25" r=".8">
            <stop stopColor="#5c666f" />
            <stop offset=".65" stopColor="#30383f" />
            <stop offset="1" stopColor="#161d23" />
          </radialGradient>
          <linearGradient id={`${id}-key`} x1="0" y1="0" x2="0" y2="1">
            <stop stopColor="#78838c" />
            <stop offset=".2" stopColor="#4e5862" />
            <stop offset="1" stopColor="#20272d" />
          </linearGradient>
          <pattern
            id={`${id}-grip`}
            width="5"
            height="5"
            patternUnits="userSpaceOnUse"
          >
            <path d="M0 5 5 0" stroke="#8d969d" strokeOpacity=".1" />
          </pattern>
          <filter
            id={`${id}-shadow`}
            x="-25%"
            y="-25%"
            width="150%"
            height="160%"
          >
            <feDropShadow
              dx="0"
              dy="14"
              stdDeviation="12"
              floodColor="#000"
              floodOpacity=".6"
            />
          </filter>
        </defs>
        <ellipse cx="350" cy="406" rx="242" ry="21" fill="#000" opacity=".28" />
        {(["LT", "RT"] as const).map((key, index) => (
          <g
            key={key}
            transform={`translate(${index ? 483 : 156} 31)`}
            {...interactive(key)}
          >
            <title>{KEY_LABELS[key]}</title>
            <path
              d="M0 30 5 5 Q32 -4 62 7 L69 45Z"
              fill={paint("key")}
              stroke={active(key) ? themeAccent : "#919da6"}
              strokeWidth="1.5"
            />
            <text
              x="34"
              y="22"
              textAnchor="middle"
              fill="#e5ebef"
              fontSize="11"
              fontWeight="600"
            >
              {key}
            </text>
            <path d="M12 34H57" stroke="#141a1f" strokeWidth="4" />
            <path
              d="M12 34H57"
              stroke={themeAccent}
              strokeWidth="2"
              pathLength="1"
              strokeDasharray={`${key === "LT" ? liveState.leftTrigger : liveState.rightTrigger} 1`}
            />
          </g>
        ))}
        {(["LB", "RB"] as const).map((key, index) => (
          <g
            key={key}
            transform={`translate(${index ? 455 : 126} 78)`}
            {...interactive(key)}
          >
            <title>{KEY_LABELS[key]}</title>
            <path
              d="M0 16 Q49 -17 121 3 L126 27H0Z"
              fill={paint("key")}
              stroke={active(key) ? themeAccent : "#89959e"}
              strokeWidth="1.4"
            />
            <text
              x="64"
              y="14"
              textAnchor="middle"
              fill="#e3e9ed"
              fontSize="10"
            >
              {key}
            </text>
          </g>
        ))}
        <g filter={paint("shadow")}>
          <path
            d="M180 94 C132 94 94 135 83 198 C69 266 92 371 130 400 C151 419 176 393 202 341 C223 300 257 293 350 293 C443 293 477 300 498 341 C524 393 549 419 570 400 C608 371 631 266 617 198 C606 135 568 94 520 94 C473 86 425 95 350 95 C275 95 227 86 180 94Z"
            fill={paint("body")}
            stroke={paint("edge")}
            strokeWidth="5"
          />
          <path
            d="M174 105 C126 119 104 161 99 218 C100 292 116 355 137 377 C151 382 173 342 185 314 C204 263 213 212 207 160Z M526 105 C574 119 596 161 601 218 C600 292 584 355 563 377 C549 382 527 342 515 314 C496 263 487 212 493 160Z"
            fill="#10171d"
            fillOpacity=".45"
          />
          <path
            d="M174 105 C126 119 104 161 99 218 C100 292 116 355 137 377 C151 382 173 342 185 314 C204 263 213 212 207 160Z M526 105 C574 119 596 161 601 218 C600 292 584 355 563 377 C549 382 527 342 515 314 C496 263 487 212 493 160Z"
            fill={paint("grip")}
          />
          <path
            d="M164 106 Q245 95 350 105 Q455 95 536 106"
            fill="none"
            stroke="#c0c8ce"
            strokeOpacity=".4"
          />
          <path
            d="M212 285 Q350 268 488 285"
            fill="none"
            stroke="#89959e"
            strokeOpacity=".25"
          />
        </g>
        <g transform="translate(350 132)">
          <circle r="17" fill={paint("key")} stroke="#828f99" />
          <path
            d="m-7-5 14 10m0-10L-7 5"
            stroke="#c8d4dc"
            strokeWidth="2"
            strokeLinecap="round"
          />
        </g>
        <text
          x="350"
          y="274"
          textAnchor="middle"
          fill="#a5b1bb"
          opacity=".7"
          fontSize="9"
          letterSpacing="4"
        >
          EASYSMX X20
        </text>
        {(["SELECT", "START"] as const).map((key, index) => (
          <g key={key} transform={`translate(${index ? 391 : 309} 179)`}>
            <title>{KEY_LABELS[key]}: indicator only</title>
            <rect
              x="-15"
              y="-9"
              width="30"
              height="18"
              rx="6"
              fill={liveState.buttons[key] ? themeAccent : paint("key")}
              stroke="#74828d"
            />
            {index ? (
              <path
                d="M-5-3H5M-5 0H5M-5 3H5"
                stroke="#e4edf3"
                strokeWidth="1.2"
              />
            ) : (
              <g fill="none" stroke="#e4edf3">
                <rect x="-5" y="-4" width="8" height="6" />
                <path d="M-2 2V4H6V-2H3" />
              </g>
            )}
          </g>
        ))}
        {stick("L3", 200, 179, liveState.leftStick)}
        {stick("R3", 431, 255, liveState.rightStick)}
        <g transform="translate(270 258)">
          <circle r="43" fill="#141a20" stroke="#7e8992" strokeWidth="1.5" />
          <circle
            r="37"
            fill={paint("key")}
            stroke="#9aa7b0"
            strokeWidth=".8"
          />
          {(
            [
              ["DPAD_UP", "M-11-32H11V-12L0-3-11-12Z", 0, -19, "↑"],
              ["DPAD_RIGHT", "M32-11V11H12L3 0 12-11Z", 21, 4, "→"],
              ["DPAD_DOWN", "M11 32H-11V12L0 3 11 12Z", 0, 26, "↓"],
              ["DPAD_LEFT", "M-32 11V-11H-12L-3 0-12 11Z", -21, 4, "←"],
            ] as const
          ).map(([key, path, x, y, label]) => (
            <g key={key} {...interactive(key)}>
              <title>{KEY_LABELS[key]}</title>
              <path
                d={path}
                fill={active(key) ? themeAccent : paint("key")}
                stroke={selectedKey === key ? "#dcfcff" : "#9aa6af"}
              />
              <text
                x={x}
                y={y}
                textAnchor="middle"
                fill={active(key) ? "#09252b" : "#e2eaf0"}
                fontSize="14"
              >
                {label}
              </text>
            </g>
          ))}
          <circle r="8" fill="#2b333b" stroke="#77838d" />
        </g>
        <g transform="translate(512 173)">
          <circle
            r="52"
            fill="#171e25"
            fillOpacity=".55"
            stroke="#66737e"
            strokeOpacity=".6"
          />
          {(
            [
              ["Y", 0, -30],
              ["X", -30, 0],
              ["B", 30, 0],
              ["A", 0, 30],
            ] as const
          ).map(([key, x, y]) => (
            <g
              key={key}
              transform={`translate(${x} ${y})`}
              {...interactive(key)}
            >
              <title>{KEY_LABELS[key]}</title>
              <circle
                r="18"
                fill="#11171c"
                stroke={active(key) ? themeAccent : "#9ca8b2"}
                strokeWidth="2"
              />
              <circle
                cy="-.5"
                r="15"
                fill={active(key) ? themeAccent : paint("key")}
                stroke="#bbc5cd"
                strokeOpacity=".2"
              />
              <text
                y="5"
                textAnchor="middle"
                fill={active(key) ? "#0d262b" : "#edf4f8"}
                fontSize="16"
                fontWeight="600"
              >
                {key}
              </text>
              {selectedKey === key && (
                <circle r="22" fill="none" stroke={themeAccent} />
              )}
            </g>
          ))}
        </g>
      </svg>
    </div>
  );
}
