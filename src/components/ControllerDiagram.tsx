import React from 'react';
import { KeyName, LiveGamepadState, ControllerPreset } from '../types/gamepad';

interface ControllerDiagramProps {
  liveState: LiveGamepadState;
  onButtonClick?: (key: KeyName) => void;
  selectedKey?: KeyName | null;
  className?: string;
  themeAccent?: string;
  preset?: ControllerPreset;
}

export const ControllerDiagram: React.FC<ControllerDiagramProps> = ({
  liveState,
  onButtonClick,
  selectedKey,
  className = '',
  preset = 'x20-pro-black',
}) => {
  // Helpers for button press states
  const isPressed = (key: KeyName) => !!liveState.buttons[key];
  const isSelected = (key: KeyName) => selectedKey === key;

  // Sticks coordinates with instant 1:1 hardware deflection
  const lsX = (liveState.leftStick.x || 0) * 16;
  const lsY = (liveState.leftStick.y || 0) * 16;
  const rsX = (liveState.rightStick.x || 0) * 16;
  const rsY = (liveState.rightStick.y || 0) * 16;

  // Trigger values (0.0 to 1.0)
  const ltValue = Math.max(0, Math.min(1, liveState.leftTrigger || (liveState.buttons.LT ? 1 : 0)));
  const rtValue = Math.max(0, Math.min(1, liveState.rightTrigger || (liveState.buttons.RT ? 1 : 0)));

  // Preset flags
  const isProBlack = preset === 'x20-pro-black';
  const isProWhite = preset === 'x20-pro-white';
  const isX05 = preset === 'x05';

  return (
    <div className={`relative flex flex-col items-center select-none w-full ${className}`}>
      <svg
        viewBox="50 10 580 430"
        className="w-full h-auto filter drop-shadow-[0_16px_36px_rgba(0,0,0,0.7)] transition-all duration-200"
      >
        <defs>
          {/* ======================================================== */}
          {/* 1. GRADIENTS & SHADERS FOR CONTROLLER PRESETS */}
          {/* ======================================================== */}

          {/* Chassis Outer Body Gradients */}
          <radialGradient id="proBlackBodyGrad" cx="50%" cy="32%" r="75%">
            <stop offset="0%" stopColor="#2A2F3B" />
            <stop offset="45%" stopColor="#1C2029" />
            <stop offset="100%" stopColor="#101319" />
          </radialGradient>

          <radialGradient id="proWhiteBodyGrad" cx="50%" cy="32%" r="75%">
            <stop offset="0%" stopColor="#FFFFFF" />
            <stop offset="50%" stopColor="#E6EAF2" />
            <stop offset="100%" stopColor="#C6CEDC" />
          </radialGradient>

          <radialGradient id="x05BlackBodyGrad" cx="50%" cy="32%" r="75%">
            <stop offset="0%" stopColor="#2E333E" />
            <stop offset="50%" stopColor="#1E222A" />
            <stop offset="100%" stopColor="#0F1116" />
          </radialGradient>

          {/* Translucent Smoked Faceplate Gradients (Pro Models) */}
          <radialGradient id="smokedFaceplateBlack" cx="50%" cy="40%" r="68%">
            <stop offset="0%" stopColor="#3C4352" stopOpacity="0.88" />
            <stop offset="55%" stopColor="#252A35" stopOpacity="0.94" />
            <stop offset="100%" stopColor="#151820" stopOpacity="0.98" />
          </radialGradient>

          <radialGradient id="smokedFaceplateWhite" cx="50%" cy="40%" r="68%">
            <stop offset="0%" stopColor="#EFF2F8" stopOpacity="0.88" />
            <stop offset="55%" stopColor="#D4DBE7" stopOpacity="0.93" />
            <stop offset="100%" stopColor="#B4BDCE" stopOpacity="0.97" />
          </radialGradient>

          {/* Silver CNC Knurled Collar (Pro Models) */}
          <radialGradient id="silverKnurlGrad" cx="50%" cy="50%" r="50%">
            <stop offset="0%" stopColor="#B8C1CF" />
            <stop offset="35%" stopColor="#F5F8FD" />
            <stop offset="70%" stopColor="#8793A6" />
            <stop offset="100%" stopColor="#555E6D" />
          </radialGradient>

          {/* Glossy Black Collar (X05) */}
          <radialGradient id="glossyBlackKnurlGrad" cx="45%" cy="45%" r="50%">
            <stop offset="0%" stopColor="#4A5260" />
            <stop offset="60%" stopColor="#1E222A" />
            <stop offset="100%" stopColor="#0C0E12" />
          </radialGradient>

          {/* Stick Rubber Caps */}
          <radialGradient id="darkStickCapGrad" cx="45%" cy="45%" r="55%">
            <stop offset="0%" stopColor="#464E5C" />
            <stop offset="60%" stopColor="#242831" />
            <stop offset="100%" stopColor="#14161C" />
          </radialGradient>

          <radialGradient id="lightStickCapGrad" cx="45%" cy="45%" r="55%">
            <stop offset="0%" stopColor="#D2D9E4" />
            <stop offset="65%" stopColor="#A4AEBD" />
            <stop offset="100%" stopColor="#7E8897" />
          </radialGradient>

          {/* D-Pad Gradients */}
          <linearGradient id="proBlackDpadGrad" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#555E6F" />
            <stop offset="30%" stopColor="#373D49" />
            <stop offset="70%" stopColor="#242831" />
            <stop offset="100%" stopColor="#15171E" />
          </linearGradient>

          <linearGradient id="proWhiteDpadGrad" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FFFFFF" />
            <stop offset="35%" stopColor="#E2E7F0" />
            <stop offset="70%" stopColor="#B6C0CF" />
            <stop offset="100%" stopColor="#8D97A8" />
          </linearGradient>

          {/* RGB Lightpipe Gradients (Pro Models) */}
          <linearGradient id="proRgbPipeLeft" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stopColor="#00E5FF" />
            <stop offset="50%" stopColor="#2979FF" />
            <stop offset="100%" stopColor="#D500F9" />
          </linearGradient>

          <linearGradient id="proRgbPipeRight" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stopColor="#00E5FF" />
            <stop offset="50%" stopColor="#2979FF" />
            <stop offset="100%" stopColor="#D500F9" />
          </linearGradient>

          {/* X05 Top Rainbow Lightbar Gradient */}
          <linearGradient id="x05RainbowBarGrad" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stopColor="#FF007A" />
            <stop offset="25%" stopColor="#FF8A00" />
            <stop offset="50%" stopColor="#FFE600" />
            <stop offset="75%" stopColor="#00E5FF" />
            <stop offset="100%" stopColor="#B300FF" />
          </linearGradient>

          {/* X05 D-Pad Halo Rainbow Gradient */}
          <linearGradient id="x05DpadHaloGrad" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stopColor="#FF007A" />
            <stop offset="30%" stopColor="#FF8A00" />
            <stop offset="60%" stopColor="#FFE600" />
            <stop offset="85%" stopColor="#00E5FF" />
            <stop offset="100%" stopColor="#B300FF" />
          </linearGradient>

          {/* Trigger Travel Fill */}
          <linearGradient id="triggerTravelGrad" x1="0%" y1="100%" x2="0%" y2="0%">
            <stop offset="0%" stopColor="#FF8A5B" />
            <stop offset="100%" stopColor="#FFB020" />
          </linearGradient>

          {/* Grip Stipple Pattern */}
          <pattern id="gripStipple" x="0" y="0" width="8" height="8" patternUnits="userSpaceOnUse">
            <circle
              cx="4"
              cy="4"
              r="1.2"
              fill={isProWhite ? '#7D8899' : '#5A6375'}
              fillOpacity="0.4"
            />
          </pattern>

          {/* Mathematical Guide Curve for X20 PRO Badge TextPath */}
          {/* Exactly 18px inward concentric to the left RGB light-pipe */}
          <path
            id="proBadgeCurve"
            d="M 155 140 C 132 190, 136 260, 160 315"
            fill="none"
          />
        </defs>

        {/* ======================================================== */}
        {/* 1. TOP SHOULDER BUMPERS & TRIGGERS (LB, LT, RB, RT) */}
        {/* ======================================================== */}

        {/* LEFT TRIGGER (LT) */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('LT')}
          title="Left Trigger (LT) - Click to remap"
        >
          <path
            d="M 175 48 C 175 20, 198 8, 238 8 C 270 8, 282 20, 282 48 Z"
            fill={isPressed('LT') || isSelected('LT') ? '#2E201B' : isProWhite ? '#CED5E0' : '#1C2028'}
            stroke={isSelected('LT') ? '#FF8A5B' : isPressed('LT') ? '#FF8A5B' : '#454D5C'}
            strokeWidth={isSelected('LT') ? 3.5 : 2}
          />
          <path
            d="M 180 45 C 180 25, 200 13, 238 13 C 267 13, 277 25, 277 45 Z"
            fill="url(#triggerTravelGrad)"
            opacity={isSelected('LT') ? 0.9 : 0.15 + ltValue * 0.85}
            className="transition-all duration-75"
          />
          <text
            x="228"
            y="33"
            fill={isProWhite && ltValue === 0 ? '#262A32' : '#FFFFFF'}
            fontSize="12"
            fontWeight="900"
            textAnchor="middle"
            filter="drop-shadow(0 1px 2px rgba(0,0,0,0.8))"
          >
            LT {ltValue > 0 ? `${Math.round(ltValue * 100)}%` : ''}
          </text>
        </g>

        {/* RIGHT TRIGGER (RT) */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('RT')}
          title="Right Trigger (RT) - Click to remap"
        >
          <path
            d="M 398 48 C 398 20, 410 8, 442 8 C 482 8, 505 20, 505 48 Z"
            fill={isPressed('RT') || isSelected('RT') ? '#2E201B' : isProWhite ? '#CED5E0' : '#1C2028'}
            stroke={isSelected('RT') ? '#FF8A5B' : isPressed('RT') ? '#FF8A5B' : '#454D5C'}
            strokeWidth={isSelected('RT') ? 3.5 : 2}
          />
          <path
            d="M 403 45 C 403 25, 413 13, 442 13 C 480 13, 500 25, 500 45 Z"
            fill="url(#triggerTravelGrad)"
            opacity={isSelected('RT') ? 0.9 : 0.15 + rtValue * 0.85}
            className="transition-all duration-75"
          />
          <text
            x="452"
            y="33"
            fill={isProWhite && rtValue === 0 ? '#262A32' : '#FFFFFF'}
            fontSize="12"
            fontWeight="900"
            textAnchor="middle"
            filter="drop-shadow(0 1px 2px rgba(0,0,0,0.8))"
          >
            RT {rtValue > 0 ? `${Math.round(rtValue * 100)}%` : ''}
          </text>
        </g>

        {/* LEFT BUMPER (LB) */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('LB')}
          title="Left Bumper (LB) - Click to remap"
        >
          <path
            d="M 158 84 C 152 62, 178 44, 270 44 C 286 44, 290 60, 290 84 Z"
            fill={
              isPressed('LB') || isSelected('LB')
                ? '#FF8A5B'
                : isProWhite
                ? '#DFE4ED'
                : '#2A2F39'
            }
            stroke={isSelected('LB') ? '#FFFFFF' : isPressed('LB') ? '#FFB020' : '#495262'}
            strokeWidth={isSelected('LB') ? 3 : 2}
            className="transition-colors group-hover:brightness-110"
          />
          <text
            x="224"
            y="68"
            fill={
              isPressed('LB') || isSelected('LB')
                ? '#131110'
                : isProWhite
                ? '#242830'
                : '#F4F0EB'
            }
            fontSize="13"
            fontWeight="900"
            textAnchor="middle"
          >
            LB
          </text>
        </g>

        {/* RIGHT BUMPER (RB) */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('RB')}
          title="Right Bumper (RB) - Click to remap"
        >
          <path
            d="M 390 84 C 390 60, 394 44, 410 44 C 502 44, 528 62, 522 84 Z"
            fill={
              isPressed('RB') || isSelected('RB')
                ? '#FF8A5B'
                : isProWhite
                ? '#DFE4ED'
                : '#2A2F39'
            }
            stroke={isSelected('RB') ? '#FFFFFF' : isPressed('RB') ? '#FFB020' : '#495262'}
            strokeWidth={isSelected('RB') ? 3 : 2}
            className="transition-colors group-hover:brightness-110"
          />
          <text
            x="456"
            y="68"
            fill={
              isPressed('RB') || isSelected('RB')
                ? '#131110'
                : isProWhite
                ? '#242830'
                : '#F4F0EB'
            }
            fontSize="13"
            fontWeight="900"
            textAnchor="middle"
          >
            RB
          </text>
        </g>

        {/* ======================================================== */}
        {/* 2. MAIN CONTROLLER CHASSIS (BODY SHELL) */}
        {/* ======================================================== */}
        <path
          d="
            M 180 76
            C 136 76, 80 110, 56 195
            C 34 275, 66 370, 116 414
            C 146 438, 180 422, 210 365
            C 240 312, 275 304, 340 304
            C 405 304, 440 312, 470 365
            C 500 422, 534 438, 564 414
            C 614 370, 646 275, 624 195
            C 600 110, 544 76, 500 76
            C 470 76, 446 84, 340 84
            C 234 84, 210 76, 180 76 Z
          "
          fill={
            isProBlack
              ? 'url(#proBlackBodyGrad)'
              : isProWhite
              ? 'url(#proWhiteBodyGrad)'
              : 'url(#x05BlackBodyGrad)'
          }
          stroke={isProWhite ? '#A6B2C4' : isX05 ? '#363D4B' : '#464F60'}
          strokeWidth="3.5"
        />

        {/* Grip Micro-Stipple Textures on Hand Grips */}
        <path
          d="M 72 245 C 62 315, 84 380, 122 400 C 132 375, 137 335, 127 275 Z"
          fill="url(#gripStipple)"
        />
        <path
          d="M 608 245 C 618 315, 596 380, 558 400 C 548 375, 543 335, 553 275 Z"
          fill="url(#gripStipple)"
        />

        {/* ======================================================== */}
        {/* 3. MODEL-SPECIFIC FACEPLATES & RGB LIGHT ACCENTS */}
        {/* ======================================================== */}

        {/* === PRO BLACK & PRO WHITE: TRANSLUCENT EXPANDED FACEPLATE + REALISTIC RGB CURVES === */}
        {(isProBlack || isProWhite) && (
          <>
            {/* Smooth Translucent Polycarbonate Faceplate */}
            <path
              d="
                M 268 84
                L 412 84
                C 460 84, 516 102, 544 132
                C 568 185, 564 260, 538 315
                C 526 340, 508 355, 485 360
                C 450 368, 410 320, 340 320
                C 270 320, 230 368, 195 360
                C 172 355, 154 340, 142 315
                C 116 260, 112 185, 136 132
                C 164 102, 220 84, 268 84 Z
              "
              fill={isProBlack ? 'url(#smokedFaceplateBlack)' : 'url(#smokedFaceplateWhite)'}
              stroke={isProBlack ? '#444D5E' : '#9EA9BC'}
              strokeWidth="2.5"
            />

            {/* Continuous Left RGB Light-Pipe Following Grip Seam */}
            <path
              d="M 136 132 C 112 185, 116 260, 142 315 C 154 340, 172 355, 195 360"
              fill="none"
              stroke="url(#proRgbPipeLeft)"
              strokeWidth="5"
              strokeLinecap="round"
              filter="drop-shadow(0 0 6px #00E5FF)"
            />

            {/* Continuous Right RGB Light-Pipe Following Grip Seam */}
            <path
              d="M 544 132 C 568 185, 564 260, 538 315 C 526 340, 508 355, 485 360"
              fill="none"
              stroke="url(#proRgbPipeRight)"
              strokeWidth="5"
              strokeLinecap="round"
              filter="drop-shadow(0 0 6px #2979FF)"
            />

            {/* Gold "X20 PRO" Badge with Mathematical Curved TextPath Alignment */}
            <text
              fill="#FFB020"
              fontSize="11"
              fontWeight="900"
              letterSpacing="2.5"
              opacity="0.95"
              filter="drop-shadow(0 1px 2px rgba(0,0,0,0.8))"
            >
              <textPath href="#proBadgeCurve" startOffset="30%">
                X20 PRO
              </textPath>
            </text>
          </>
        )}

        {/* === X05: MATTE BLACK WITH TOP RAINBOW LIGHTBAR & RGB HALO D-PAD === */}
        {isX05 && (
          <>
            {/* Sculpted Metallic Top Arc below Bumpers */}
            <path
              d="M 264 78 C 300 70, 380 70, 416 78 L 422 116 C 382 122, 298 122, 258 116 Z"
              fill="#252933"
              stroke="#3E4554"
              strokeWidth="2"
            />
            {/* Top Continuous Rainbow RGB Lightbar - Follows Top Arc */}
            <path
              d="M 255 84 C 295 74, 385 74, 425 84"
              fill="none"
              stroke="url(#x05RainbowBarGrad)"
              strokeWidth="5.5"
              strokeLinecap="round"
              filter="drop-shadow(0 0 7px #00E5FF)"
            />

            {/* Glowing RGB Rainbow Halo Ring around the D-Pad Well */}
            <circle
              cx="262"
              cy="252"
              r="43"
              fill="none"
              stroke="url(#x05DpadHaloGrad)"
              strokeWidth="4.5"
              filter="drop-shadow(0 0 7px #FF007A)"
            />

            {/* Center Status Horizontal Cyan LED Bar */}
            <rect
              x="326"
              y="218"
              width="28"
              height="4"
              rx="2"
              fill="#00E5FF"
              filter="drop-shadow(0 0 4px #00E5FF)"
            />
          </>
        )}

        {/* ======================================================== */}
        {/* 4. TOP CENTER DISPLAY / LOGO SECTION */}
        {/* ======================================================== */}
        {/* For Pro Black and Pro White: Top OLED Display Trapezoid */}
        {!isX05 && (
          <g transform="translate(280, 78)">
            {/* Bezel */}
            <polygon
              points="10,0 110,0 120,46 0,46"
              fill="#0A0C10"
              stroke="#2B303C"
              strokeWidth="2.5"
            />
            {/* Screen Glass */}
            <polygon
              points="14,4 106,4 114,42 6,42"
              fill="#12151C"
            />

            {/* Pegasus / Knight Crest Logo in Gold */}
            <g transform="translate(24, 10)">
              <path
                d="M 12 3 C 7 3, 4 8, 4 14 C 4 19, 7 21, 11 21 C 10 17, 11 14, 13 12 C 12 10, 14 6, 17 6 C 15 4, 13 3, 12 3 Z"
                fill="#FFB020"
              />
              <path
                d="M 8 7 C 11 5, 15 8, 16 12 C 13 13, 11 15, 11 18 C 9 17, 7 13, 8 7 Z"
                fill="#FFE17D"
              />
            </g>

            {/* EasySMX Gold Wordmark */}
            <text
              x="48"
              y="27"
              fill="#FFB020"
              fontSize="12.5"
              fontWeight="bold"
              fontFamily="sans-serif"
              letterSpacing="0.5"
            >
              EasySMX
            </text>

            {/* 3 Status / Battery LED Dots */}
            <circle cx="50" cy="56" r="2.8" fill="#8AB4F8" filter="drop-shadow(0 0 3px #8AB4F8)" />
            <circle cx="60" cy="56" r="2.8" fill="#8AB4F8" filter="drop-shadow(0 0 3px #8AB4F8)" />
            <circle cx="70" cy="56" r="2.8" fill="#8AB4F8" filter="drop-shadow(0 0 3px #8AB4F8)" />
          </g>
        )}

        {/* For X05: Center Round Knight Crest Home Button */}
        {isX05 && (
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('CAPTURE')}
            transform="translate(340, 98)"
            title="Home / Crest Button"
          >
            <circle
              cx="0"
              cy="0"
              r="17"
              fill={isPressed('CAPTURE') || isSelected('CAPTURE') ? '#FF8A5B' : '#1C2028'}
              stroke={isSelected('CAPTURE') ? '#FFFFFF' : '#4E5769'}
              strokeWidth="2.5"
              className="group-hover:brightness-125 transition-all"
            />
            {/* White Knight Crest */}
            <path
              d="M 2 -8 C -3 -8, -6 -3, -6 3 C -6 8, -3 10, 1 10 C 0 6, 1 3, 3 1 C 2 -1, 4 -5, 7 -5 C 5 -7, 3 -8, 2 -8 Z"
              fill="#FFFFFF"
            />
          </g>
        )}

        {/* ======================================================== */}
        {/* 5. CENTER BUTTONS: SELECT, HOME, START, M */}
        {/* Clean icon-only design with no redundant wording underneath */}
        {/* ======================================================== */}

        {/* SELECT / VIEW Button */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('SELECT')}
          transform="translate(296, 142)"
          title="Select / View Button"
        >
          {isSelected('SELECT') && (
            <circle cx="0" cy="0" r="17" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
          )}
          <circle
            cx="0"
            cy="0"
            r="12.5"
            fill={
              isPressed('SELECT') || isSelected('SELECT')
                ? '#FF8A5B'
                : isProWhite
                ? '#E2E6EF'
                : '#252932'
            }
            stroke={isSelected('SELECT') ? '#FFFFFF' : '#4D5667'}
            strokeWidth="2"
            className="group-hover:brightness-125 transition-all"
          />
          <rect
            x="-5"
            y="-4"
            width="6.5"
            height="5.5"
            fill="none"
            stroke={
              isPressed('SELECT') || isSelected('SELECT')
                ? '#131110'
                : isProWhite
                ? '#353B47'
                : '#D1D6E2'
            }
            strokeWidth="1.3"
          />
          <rect
            x="-1.5"
            y="-1"
            width="6.5"
            height="5.5"
            fill="none"
            stroke={
              isPressed('SELECT') || isSelected('SELECT')
                ? '#131110'
                : isProWhite
                ? '#353B47'
                : '#D1D6E2'
            }
            strokeWidth="1.3"
          />
        </g>

        {/* HOME / CAPTURE Button (For Pro models) */}
        {!isX05 && (
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('CAPTURE')}
            transform="translate(340, 150)"
            title="Home / Capture Button"
          >
            {isSelected('CAPTURE') && (
              <circle cx="0" cy="0" r="18" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
            )}
            <circle
              cx="0"
              cy="0"
              r="13.5"
              fill={
                isPressed('CAPTURE') || isSelected('CAPTURE')
                  ? '#FF8A5B'
                  : isProWhite
                  ? '#E2E6EF'
                  : '#1F232B'
              }
              stroke={isSelected('CAPTURE') ? '#FFFFFF' : '#576072'}
              strokeWidth="2.5"
              className="group-hover:brightness-125 transition-all"
            />
            {/* Glowing Center Ring */}
            <circle
              cx="0"
              cy="0"
              r="6"
              fill="none"
              stroke={
                isPressed('CAPTURE') || isSelected('CAPTURE')
                  ? '#131110'
                  : '#FFB020'
              }
              strokeWidth="2"
            />
            <circle
              cx="0"
              cy="0"
              r="2"
              fill={
                isPressed('CAPTURE') || isSelected('CAPTURE')
                  ? '#131110'
                  : '#FFB020'
              }
            />
          </g>
        )}

        {/* START / MENU Button */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('START')}
          transform="translate(384, 142)"
          title="Start / Menu Button"
        >
          {isSelected('START') && (
            <circle cx="0" cy="0" r="17" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
          )}
          <circle
            cx="0"
            cy="0"
            r="12.5"
            fill={
              isPressed('START') || isSelected('START')
                ? '#FF8A5B'
                : isProWhite
                ? '#E2E6EF'
                : '#252932'
            }
            stroke={isSelected('START') ? '#FFFFFF' : '#4D5667'}
            strokeWidth="2"
            className="group-hover:brightness-125 transition-all"
          />
          {/* Hamburger 3-bar icon */}
          <line
            x1="-4.5"
            y1="-3"
            x2="4.5"
            y2="-3"
            stroke={
              isPressed('START') || isSelected('START')
                ? '#131110'
                : isProWhite
                ? '#353B47'
                : '#D1D6E2'
            }
            strokeWidth="1.6"
            strokeLinecap="round"
          />
          <line
            x1="-4.5"
            y1="0"
            x2="4.5"
            y2="0"
            stroke={
              isPressed('START') || isSelected('START')
                ? '#131110'
                : isProWhite
                ? '#353B47'
                : '#D1D6E2'
            }
            strokeWidth="1.6"
            strokeLinecap="round"
          />
          <line
            x1="-4.5"
            y1="3"
            x2="4.5"
            y2="3"
            stroke={
              isPressed('START') || isSelected('START')
                ? '#131110'
                : isProWhite
                ? '#353B47'
                : '#D1D6E2'
            }
            strokeWidth="1.6"
            strokeLinecap="round"
          />
        </g>

        {/* TURBO / M Button */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('TURBO')}
          transform="translate(328, 254)"
          title="M / Turbo Button"
        >
          {isSelected('TURBO') && (
            <rect x="-3" y="-3" width="30" height="22" rx="6" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
          )}
          <rect
            x="0"
            y="0"
            width="24"
            height="16"
            rx="4.5"
            fill={
              isPressed('TURBO') || isSelected('TURBO')
                ? '#FF8A5B'
                : isProWhite
                ? '#E2E6EF'
                : '#252932'
            }
            stroke={isSelected('TURBO') ? '#FFFFFF' : '#4E5768'}
            strokeWidth="1.6"
            className="group-hover:brightness-125 transition-all"
          />
          <text
            x="12"
            y="12"
            fill={
              isPressed('TURBO') || isSelected('TURBO')
                ? '#131110'
                : isProWhite
                ? '#242830'
                : '#F4F0EB'
            }
            fontSize="10"
            fontWeight="900"
            textAnchor="middle"
          >
            M
          </text>
        </g>

        {/* ======================================================== */}
        {/* 6. LEFT THUMBSTICK (HIGH OFFSET POSITION) */}
        {/* Clean, authentic cap with no cluttering "L3 (Click)" text */}
        {/* ======================================================== */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('L3')}
          transform="translate(210, 146)"
          title="Left Stick (L3) - Click to remap"
        >
          {isSelected('L3') && (
            <circle cx="0" cy="0" r="46" fill="none" stroke="#FF8A5B" strokeWidth="3" className="animate-pulse" />
          )}

          {/* Recessed Well */}
          <circle
            cx="0"
            cy="0"
            r="42"
            fill="#12141A"
            stroke={isProWhite ? '#A6B2C4' : '#39404E'}
            strokeWidth="2.6"
          />

          {/* Collar: Silver Knurled on Pro; Glossy Black on X05 */}
          <circle
            cx="0"
            cy="0"
            r="36"
            fill={isX05 ? 'url(#glossyBlackKnurlGrad)' : 'url(#silverKnurlGrad)'}
            stroke={isX05 ? '#404756' : '#727D8F'}
            strokeWidth="1.8"
          />
          {!isX05 && (
            <circle
              cx="0"
              cy="0"
              r="33"
              fill="none"
              stroke="#343B47"
              strokeWidth="2.8"
              strokeDasharray="2.5 3"
            />
          )}

          {/* Deflectable Thumbstick Cap (Instant 1:1 Response) */}
          <g transform={`translate(${lsX}, ${lsY})`}>
            <circle
              cx="0"
              cy="0"
              r="27"
              fill={isProWhite ? 'url(#lightStickCapGrad)' : 'url(#darkStickCapGrad)'}
              stroke={isPressed('L3') || isSelected('L3') ? '#FF8A5B' : '#555E6E'}
              strokeWidth="2.2"
            />
            {/* Concentric Grip Rings */}
            <circle
              cx="0"
              cy="0"
              r="20"
              fill="none"
              stroke={isProWhite ? '#86909E' : '#3E4654'}
              strokeWidth="1.5"
            />
            <circle
              cx="0"
              cy="0"
              r="13"
              fill={isProWhite ? '#98A3B2' : '#1C1F26'}
              stroke={isProWhite ? '#828D9D' : '#14161B'}
              strokeWidth="1.3"
            />
          </g>
        </g>

        {/* ======================================================== */}
        {/* 7. DIRECTIONAL PAD (D-PAD) */}
        {/* Positioned at (262, 252) with generous spacing from left stick */}
        {/* ======================================================== */}
        <g transform="translate(262, 252)">
          {/* Recessed well */}
          <circle
            cx="0"
            cy="0"
            r="39"
            fill="#12141A"
            stroke={isProWhite ? '#A8B4C5' : '#353B48'}
            strokeWidth="2.4"
          />

          {/* 8-point Faceted Disc */}
          <polygon
            points="0,-35 15,-27 27,-15 35,0 27,15 15,27 0,35 -15,27 -27,15 -35,0 -27,-15 -15,-27"
            fill={isProWhite ? 'url(#proWhiteDpadGrad)' : 'url(#proBlackDpadGrad)'}
            stroke={isProWhite ? '#8D97A7' : '#576070'}
            strokeWidth="1.6"
          />

          {/* UP Triangle */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('DPAD_UP')}
            title="D-Pad Up - Click to remap"
          >
            <polygon
              points="0,0 -15,-27 0,-35 15,-27"
              fill={isPressed('DPAD_UP') || isSelected('DPAD_UP') ? '#FF8A5B' : isProWhite ? '#FFFFFF' : '#3A404D'}
              stroke={isSelected('DPAD_UP') ? '#FFFFFF' : '#6A7485'}
              strokeWidth={isSelected('DPAD_UP') ? 2 : 1}
              className="transition-colors group-hover:brightness-110"
            />
            <text
              x="0"
              y="-15"
              fill={isPressed('DPAD_UP') || isSelected('DPAD_UP') ? '#131110' : isProWhite ? '#2A2F3A' : '#DDE2ED'}
              fontSize="11"
              fontWeight="900"
              textAnchor="middle"
            >
              ▲
            </text>
          </g>

          {/* RIGHT Triangle */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('DPAD_RIGHT')}
            title="D-Pad Right - Click to remap"
          >
            <polygon
              points="0,0 27,-15 35,0 27,15"
              fill={isPressed('DPAD_RIGHT') || isSelected('DPAD_RIGHT') ? '#FF8A5B' : isProWhite ? '#DEE3EC' : '#313642'}
              stroke={isSelected('DPAD_RIGHT') ? '#FFFFFF' : '#6A7485'}
              strokeWidth={isSelected('DPAD_RIGHT') ? 2 : 1}
              className="transition-colors group-hover:brightness-110"
            />
            <text
              x="17"
              y="4"
              fill={isPressed('DPAD_RIGHT') || isSelected('DPAD_RIGHT') ? '#131110' : isProWhite ? '#2A2F3A' : '#DDE2ED'}
              fontSize="11"
              fontWeight="900"
              textAnchor="middle"
            >
              ▶
            </text>
          </g>

          {/* DOWN Triangle */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('DPAD_DOWN')}
            title="D-Pad Down - Click to remap"
          >
            <polygon
              points="0,0 15,27 0,35 -15,27"
              fill={isPressed('DPAD_DOWN') || isSelected('DPAD_DOWN') ? '#FF8A5B' : isProWhite ? '#BCC5D2' : '#282C36'}
              stroke={isSelected('DPAD_DOWN') ? '#FFFFFF' : '#6A7485'}
              strokeWidth={isSelected('DPAD_DOWN') ? 2 : 1}
              className="transition-colors group-hover:brightness-110"
            />
            <text
              x="0"
              y="20"
              fill={isPressed('DPAD_DOWN') || isSelected('DPAD_DOWN') ? '#131110' : isProWhite ? '#2A2F3A' : '#DDE2ED'}
              fontSize="11"
              fontWeight="900"
              textAnchor="middle"
            >
              ▼
            </text>
          </g>

          {/* LEFT Triangle */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('DPAD_LEFT')}
            title="D-Pad Left - Click to remap"
          >
            <polygon
              points="0,0 -27,15 -35,0 -27,-15"
              fill={isPressed('DPAD_LEFT') || isSelected('DPAD_LEFT') ? '#FF8A5B' : isProWhite ? '#CBD3E0' : '#2D323E'}
              stroke={isSelected('DPAD_LEFT') ? '#FFFFFF' : '#6A7485'}
              strokeWidth={isSelected('DPAD_LEFT') ? 2 : 1}
              className="transition-colors group-hover:brightness-110"
            />
            <text
              x="-17"
              y="4"
              fill={isPressed('DPAD_LEFT') || isSelected('DPAD_LEFT') ? '#131110' : isProWhite ? '#2A2F3A' : '#DDE2ED'}
              fontSize="11"
              fontWeight="900"
              textAnchor="middle"
            >
              ◀
            </text>
          </g>

          {/* Center concave dish */}
          <circle cx="0" cy="0" r="10" fill={isProWhite ? '#B4BDCB' : '#22262F'} stroke="#667080" strokeWidth="1.2" />
        </g>

        {/* ======================================================== */}
        {/* 8. RIGHT THUMBSTICK (LOWER OFFSET POSITION) */}
        {/* Positioned at (418, 252) - 34px of clear gap from action buttons */}
        {/* ======================================================== */}
        <g
          className="cursor-pointer group"
          onClick={() => onButtonClick?.('R3')}
          transform="translate(418, 252)"
          title="Right Stick (R3) - Click to remap"
        >
          {isSelected('R3') && (
            <circle cx="0" cy="0" r="46" fill="none" stroke="#FF8A5B" strokeWidth="3" className="animate-pulse" />
          )}

          {/* Recessed Well */}
          <circle
            cx="0"
            cy="0"
            r="42"
            fill="#12141A"
            stroke={isProWhite ? '#A6B2C4' : '#39404E'}
            strokeWidth="2.6"
          />

          {/* Collar: Silver Knurled on Pro; Glossy Black on X05 */}
          <circle
            cx="0"
            cy="0"
            r="36"
            fill={isX05 ? 'url(#glossyBlackKnurlGrad)' : 'url(#silverKnurlGrad)'}
            stroke={isX05 ? '#404756' : '#727D8F'}
            strokeWidth="1.8"
          />
          {!isX05 && (
            <circle
              cx="0"
              cy="0"
              r="33"
              fill="none"
              stroke="#343B47"
              strokeWidth="2.8"
              strokeDasharray="2.5 3"
            />
          )}

          {/* Deflectable Thumbstick Cap (Instant 1:1 Response) */}
          <g transform={`translate(${rsX}, ${rsY})`}>
            <circle
              cx="0"
              cy="0"
              r="27"
              fill={isProWhite ? 'url(#lightStickCapGrad)' : 'url(#darkStickCapGrad)'}
              stroke={isPressed('R3') || isSelected('R3') ? '#FF8A5B' : '#555E6E'}
              strokeWidth="2.2"
            />
            <circle
              cx="0"
              cy="0"
              r="20"
              fill="none"
              stroke={isProWhite ? '#86909E' : '#3E4654'}
              strokeWidth="1.5"
            />
            <circle
              cx="0"
              cy="0"
              r="13"
              fill={isProWhite ? '#98A3B2' : '#1C1F26'}
              stroke={isProWhite ? '#828D9D' : '#14161B'}
              strokeWidth="1.3"
            />
          </g>
        </g>

        {/* ======================================================== */}
        {/* 9. ACTION BUTTONS (X, Y, A, B) */}
        {/* Positioned at (476, 142) - Fully separated with zero stick overlap */}
        {/* ======================================================== */}
        <g transform="translate(476, 142)">
          {/* Subtle glossy recessed bedding on the faceplate */}
          <circle
            cx="0"
            cy="0"
            r="44"
            fill={isProBlack ? '#181B22' : isProWhite ? '#CBD2E0' : '#171920'}
            fillOpacity={isProWhite ? 0.35 : 0.45}
            stroke={isProWhite ? '#B0BAC9' : '#343B48'}
            strokeWidth="1.2"
          />

          {/* Y BUTTON (TOP) */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('Y')}
            transform="translate(0, -25)"
            title="Y Button - Click to remap"
          >
            {isSelected('Y') && (
              <circle cx="0" cy="0" r="20" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
            )}
            <circle
              cx="0"
              cy="0"
              r="16"
              fill={
                isPressed('Y') || isSelected('Y')
                  ? '#FF8A5B'
                  : isProWhite
                  ? '#E2E6EF'
                  : '#232732'
              }
              stroke={isSelected('Y') ? '#FFFFFF' : isProWhite ? '#A2ACB9' : '#4E5769'}
              strokeWidth="2"
              filter="drop-shadow(0 2px 4px rgba(0,0,0,0.5))"
              className="group-hover:scale-105 transition-transform"
            />
            <text
              x="0"
              y="5.5"
              fill={
                isPressed('Y') || isSelected('Y')
                  ? '#131110'
                  : isProWhite
                  ? '#2B303C'
                  : '#FFFFFF'
              }
              fontSize="16"
              fontWeight="900"
              textAnchor="middle"
              fontFamily="sans-serif"
            >
              Y
            </text>
          </g>

          {/* X BUTTON (LEFT) */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('X')}
            transform="translate(-25, 0)"
            title="X Button - Click to remap"
          >
            {isSelected('X') && (
              <circle cx="0" cy="0" r="20" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
            )}
            <circle
              cx="0"
              cy="0"
              r="16"
              fill={
                isPressed('X') || isSelected('X')
                  ? '#FF8A5B'
                  : isProWhite
                  ? '#E2E6EF'
                  : '#232732'
              }
              stroke={isSelected('X') ? '#FFFFFF' : isProWhite ? '#A2ACB9' : '#4E5769'}
              strokeWidth="2"
              filter="drop-shadow(0 2px 4px rgba(0,0,0,0.5))"
              className="group-hover:scale-105 transition-transform"
            />
            <text
              x="0"
              y="5.5"
              fill={
                isPressed('X') || isSelected('X')
                  ? '#131110'
                  : isProWhite
                  ? '#2B303C'
                  : '#FFFFFF'
              }
              fontSize="16"
              fontWeight="900"
              textAnchor="middle"
              fontFamily="sans-serif"
            >
              X
            </text>
          </g>

          {/* B BUTTON (RIGHT) */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('B')}
            transform="translate(25, 0)"
            title="B Button - Click to remap"
          >
            {isSelected('B') && (
              <circle cx="0" cy="0" r="20" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
            )}
            <circle
              cx="0"
              cy="0"
              r="16"
              fill={
                isPressed('B') || isSelected('B')
                  ? '#FF8A5B'
                  : isProWhite
                  ? '#E2E6EF'
                  : '#232732'
              }
              stroke={isSelected('B') ? '#FFFFFF' : isProWhite ? '#A2ACB9' : '#4E5769'}
              strokeWidth="2"
              filter="drop-shadow(0 2px 4px rgba(0,0,0,0.5))"
              className="group-hover:scale-105 transition-transform"
            />
            <text
              x="0"
              y="5.5"
              fill={
                isPressed('B') || isSelected('B')
                  ? '#131110'
                  : isProWhite
                  ? '#2B303C'
                  : '#FFFFFF'
              }
              fontSize="16"
              fontWeight="900"
              textAnchor="middle"
              fontFamily="sans-serif"
            >
              B
            </text>
          </g>

          {/* A BUTTON (BOTTOM) */}
          <g
            className="cursor-pointer group"
            onClick={() => onButtonClick?.('A')}
            transform="translate(0, 25)"
            title="A Button - Click to remap"
          >
            {isSelected('A') && (
              <circle cx="0" cy="0" r="20" fill="none" stroke="#FF8A5B" strokeWidth="2.5" className="animate-pulse" />
            )}
            <circle
              cx="0"
              cy="0"
              r="16"
              fill={
                isPressed('A') || isSelected('A')
                  ? '#FF8A5B'
                  : isProWhite
                  ? '#E2E6EF'
                  : '#232732'
              }
              stroke={isSelected('A') ? '#FFFFFF' : isProWhite ? '#A2ACB9' : '#4E5769'}
              strokeWidth="2"
              filter="drop-shadow(0 2px 4px rgba(0,0,0,0.5))"
              className="group-hover:scale-105 transition-transform"
            />
            <text
              x="0"
              y="5.5"
              fill={
                isPressed('A') || isSelected('A')
                  ? '#131110'
                  : isProWhite
                  ? '#2B303C'
                  : '#FFFFFF'
              }
              fontSize="16"
              fontWeight="900"
              textAnchor="middle"
              fontFamily="sans-serif"
            >
              A
            </text>
          </g>
        </g>
      </svg>
    </div>
  );
};
