import React, { useState } from 'react';
import {
  KeyName,
  KEY_LABELS,
  LiveGamepadState,
  ControllerPreset,
  CONTROLLER_PRESETS,
} from '../../types/gamepad';
import { ControllerDiagram } from '../ControllerDiagram';
import { RotateCcw, ArrowRight, Minus, Plus, SlidersHorizontal } from 'lucide-react';

interface ButtonsPageProps {
  remaps: Record<KeyName, KeyName>;
  onUpdateRemap: (source: KeyName, target: KeyName) => void;
  onResetRemaps: () => void;
  liveState: LiveGamepadState;
}

const ALL_REMAP_KEYS: KeyName[] = [
  'LT', 'RT',
  'LB', 'RB',
  'Y', 'X', 'B', 'A',
  'DPAD_UP', 'DPAD_DOWN', 'DPAD_LEFT', 'DPAD_RIGHT',
  'L3', 'R3',
  'SELECT', 'START',
  'CAPTURE', 'TURBO',
];

const AVAILABLE_TARGETS: KeyName[] = [
  'A', 'B', 'X', 'Y',
  'LB', 'RB', 'LT', 'RT',
  'L3', 'R3',
  'DPAD_UP', 'DPAD_DOWN', 'DPAD_LEFT', 'DPAD_RIGHT',
  'SELECT', 'START',
  'CAPTURE', 'TURBO',
];

export const ButtonsPage: React.FC<ButtonsPageProps> = ({
  remaps,
  onUpdateRemap,
  onResetRemaps,
  liveState,
}) => {
  const [selectedKey, setSelectedKey] = useState<KeyName | null>(null);
  const [filterCategory, setFilterCategory] = useState<'all' | 'triggers' | 'face' | 'dpad' | 'sticks'>('all');

  // SVG Size / Zoom scale state (default 68% for balanced proportion)
  const [svgScale, setSvgScale] = useState<number>(() => {
    try {
      const saved = localStorage.getItem('x20_svg_scale');
      return saved ? Math.min(100, Math.max(40, Number(saved))) : 68;
    } catch {
      return 68;
    }
  });

  // Controller Preset Model state
  const [activePreset, setActivePreset] = useState<ControllerPreset>(() => {
    try {
      const saved = localStorage.getItem('x20_controller_preset');
      if (saved && ['x20-pro-black', 'x20-pro-white', 'x05'].includes(saved)) {
        return saved as ControllerPreset;
      }
      return 'x20-pro-black';
    } catch {
      return 'x20-pro-black';
    }
  });

  const handleScaleChange = (newScale: number) => {
    const clamped = Math.min(100, Math.max(40, newScale));
    setSvgScale(clamped);
    try {
      localStorage.setItem('x20_svg_scale', clamped.toString());
    } catch {
      // ignore
    }
  };

  const handlePresetSelect = (presetId: ControllerPreset) => {
    setActivePreset(presetId);
    try {
      localStorage.setItem('x20_controller_preset', presetId);
    } catch {
      // ignore
    }
  };

  const filteredKeys = ALL_REMAP_KEYS.filter((k) => {
    if (filterCategory === 'triggers') return ['LT', 'RT', 'LB', 'RB'].includes(k);
    if (filterCategory === 'face') return ['A', 'B', 'X', 'Y'].includes(k);
    if (filterCategory === 'dpad') return k.startsWith('DPAD');
    if (filterCategory === 'sticks') return ['L3', 'R3'].includes(k);
    return true;
  });

  return (
    <div className="space-y-6 max-w-5xl mx-auto select-none">
      {/* Header text */}
      <div className="flex items-center justify-between pb-4 border-b border-[#332C29]">
        <div>
          <h2 className="text-base font-bold text-[#F4F0EB]">Hardware Button Remapping</h2>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Click any button on the diagram or choose from the list below to configure remaps.
          </p>
        </div>
        <button
          onClick={onResetRemaps}
          className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] hover:text-[#F4F0EB] border border-[#332C29] transition-colors"
        >
          <RotateCcw className="w-3.5 h-3.5" />
          <span>Reset Stock Layout</span>
        </button>
      </div>

      {/* Controller Diagram Container with Toolbar */}
      <div className="p-4 md:p-6 rounded-2xl bg-[#1B1817] border border-[#332C29] shadow-xl flex flex-col items-center w-full space-y-4">
        {/* Model Presets & SVG Size Slider Bar */}
        <div className="w-full flex flex-col md:flex-row md:items-center justify-between gap-3 pb-3 border-b border-[#2A2421]">
          {/* 4 Model Presets */}
          <div className="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0 scrollbar-none">
            <span className="text-[11px] font-bold text-[#A79C92] uppercase tracking-wider mr-1 shrink-0">
              Preset:
            </span>
            {CONTROLLER_PRESETS.map((p) => {
              const isActive = activePreset === p.id;
              return (
                <button
                  key={p.id}
                  onClick={() => handlePresetSelect(p.id)}
                  className={`flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-semibold transition-all shrink-0 ${
                    isActive
                      ? 'bg-[#FF8A5B] text-[#131110] shadow-[0_0_12px_rgba(255,138,91,0.3)] font-bold'
                      : 'bg-[#241F1D] text-[#D6CEC6] hover:bg-[#2F2926] hover:text-[#F4F0EB] border border-[#332C29]'
                  }`}
                  title={p.description}
                >
                  {/* Preset Swatch Dot */}
                  <span
                    className={`w-2.5 h-2.5 rounded-full shrink-0 border ${
                      p.id === 'x20-pro-black'
                        ? 'bg-[#1C2028] border-[#555E6E]'
                        : p.id === 'x20-pro-white'
                        ? 'bg-[#F0F3F8] border-[#99A4B8]'
                        : 'bg-[#12141A] border-[#00D2FF]'
                    }`}
                  />
                  <span>{p.name}</span>
                </button>
              );
            })}
          </div>

          {/* SVG Scale / Size Slider Controls */}
          <div className="flex items-center gap-2.5 self-end md:self-auto shrink-0 bg-[#141211] px-3 py-1.5 rounded-xl border border-[#2F2926]">
            <div className="flex items-center gap-1.5 text-xs text-[#A79C92]">
              <SlidersHorizontal className="w-3.5 h-3.5 text-[#FF8A5B]" />
              <span className="text-[11px] font-medium hidden sm:inline">Size</span>
            </div>

            <button
              onClick={() => handleScaleChange(svgScale - 5)}
              className="p-1 rounded bg-[#241F1D] hover:bg-[#332C29] text-[#A79C92] hover:text-[#F4F0EB] transition-colors"
              title="Zoom out"
            >
              <Minus className="w-3 h-3" />
            </button>

            <input
              type="range"
              min="40"
              max="100"
              step="2"
              value={svgScale}
              onChange={(e) => handleScaleChange(Number(e.target.value))}
              className="w-24 sm:w-28 h-1.5 bg-[#2A2421] rounded-lg appearance-none cursor-pointer accent-[#FF8A5B]"
              title={`SVG scale: ${svgScale}%`}
            />

            <button
              onClick={() => handleScaleChange(svgScale + 5)}
              className="p-1 rounded bg-[#241F1D] hover:bg-[#332C29] text-[#A79C92] hover:text-[#F4F0EB] transition-colors"
              title="Zoom in"
            >
              <Plus className="w-3 h-3" />
            </button>

            <span className="font-mono text-xs text-[#FF8A5B] font-bold min-w-[34px] text-right">
              {svgScale}%
            </span>

            <button
              onClick={() => handleScaleChange(68)}
              className="text-[10px] px-2 py-0.5 rounded bg-[#241F1D] hover:bg-[#2E2825] text-[#A79C92] hover:text-[#F4F0EB] border border-[#332C29] transition-colors font-medium ml-1"
              title="Reset size to 68%"
            >
              Reset
            </button>
          </div>
        </div>

        {/* Status Line */}
        <div className="w-full flex items-center justify-between text-xs text-[#A79C92] px-1">
          <span className="font-medium text-[#F4F0EB]">
            {CONTROLLER_PRESETS.find((p) => p.id === activePreset)?.name || 'EasySMX Controller'}
            <span className="text-[11px] font-normal text-[#A79C92] ml-2 hidden sm:inline">
              ({CONTROLLER_PRESETS.find((p) => p.id === activePreset)?.badge})
            </span>
          </span>
          <div className="flex items-center gap-3">
            <span className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-[#FF8A5B] shadow-[0_0_6px_#FF8A5B]" />
              <span className="text-[11px]">Real-time Live Input</span>
            </span>
            <span className="text-[11px] text-[#A79C92]">LB, LT, RB, RT Included</span>
          </div>
        </div>

        {/* Dynamically Scaled Controller Diagram */}
        <div
          className="flex justify-center items-center w-full transition-all duration-200 py-2"
          style={{ width: `${svgScale}%`, maxWidth: '100%' }}
        >
          <ControllerDiagram
            liveState={liveState}
            selectedKey={selectedKey}
            onButtonClick={(k) => setSelectedKey(k)}
            preset={activePreset}
          />
        </div>
      </div>

      {/* Centered Remapping Section */}
      <div className="space-y-4">
        {/* Category Filters */}
        <div className="flex items-center justify-between">
          <span className="text-xs font-bold uppercase tracking-wider text-[#A79C92]">
            Remapping Table
          </span>

          <div className="flex items-center gap-1.5 p-1 rounded-lg bg-[#1B1817] border border-[#332C29] text-xs">
            {(['all', 'triggers', 'face', 'dpad', 'sticks'] as const).map((cat) => (
              <button
                key={cat}
                onClick={() => setFilterCategory(cat)}
                className={`px-2.5 py-1 rounded-md capitalize font-medium transition-all ${
                  filterCategory === cat
                    ? 'bg-[#FF8A5B] text-[#131110] font-bold shadow-sm'
                    : 'text-[#A79C92] hover:text-[#F4F0EB]'
                }`}
              >
                {cat}
              </button>
            ))}
          </div>
        </div>

        {/* Remapping Rows with "Maps to" Centered Based on Window */}
        <div className="space-y-2">
          {filteredKeys.map((sourceKey) => {
            const targetKey = remaps[sourceKey] || sourceKey;
            const isLivePressed = !!liveState.buttons[sourceKey];
            const isCurrentSelected = selectedKey === sourceKey;
            const isRemapped = targetKey !== sourceKey;

            return (
              <div
                key={sourceKey}
                onClick={() => setSelectedKey(sourceKey)}
                className={`flex items-center justify-between p-3.5 rounded-xl border transition-all cursor-pointer ${
                  isCurrentSelected
                    ? 'bg-[#241F1D] border-[#FF8A5B] shadow-[0_0_12px_rgba(255,138,91,0.2)]'
                    : 'bg-[#1B1817] border-[#332C29] hover:border-[#4A3F3B]'
                }`}
              >
                {/* Left side: Physical Input Button */}
                <div className="flex-1 flex items-center justify-start gap-3">
                  <div
                    className={`w-2.5 h-2.5 rounded-full shrink-0 transition-colors ${
                      isLivePressed
                        ? 'bg-[#FF8A5B] shadow-[0_0_8px_#FF8A5B]'
                        : 'bg-[#332C29]'
                    }`}
                  />
                  <span className="text-sm font-bold text-[#F4F0EB]">
                    {KEY_LABELS[sourceKey] || sourceKey}
                  </span>
                  {isRemapped && (
                    <span className="text-[10px] px-1.5 py-0.5 rounded bg-[#FF8A5B]/15 text-[#FF8A5B] font-mono font-semibold border border-[#FF8A5B]/30">
                      Modified
                    </span>
                  )}
                </div>

                {/* EXACT CENTER: "Maps to" Based on Window Width */}
                <div className="w-44 flex items-center justify-center shrink-0">
                  <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-[#141211] border border-[#332C29] shadow-inner text-xs font-mono font-bold text-[#D6CEC6]">
                    <span className="text-[#A79C92]">Maps to</span>
                    <ArrowRight className="w-3.5 h-3.5 text-[#FF8A5B]" />
                  </div>
                </div>

                {/* Right side: Target Output Action Dropdown */}
                <div className="flex-1 flex items-center justify-end">
                  <select
                    value={targetKey}
                    onChange={(e) => onUpdateRemap(sourceKey, e.target.value as KeyName)}
                    onClick={(e) => e.stopPropagation()}
                    className="bg-[#141211] border border-[#332C29] focus:border-[#FF8A5B] text-xs font-semibold text-[#F4F0EB] rounded-lg px-3 py-2 outline-none cursor-pointer hover:bg-[#1E1A18] transition-colors w-40 text-right"
                  >
                    {AVAILABLE_TARGETS.map((target) => (
                      <option key={target} value={target}>
                        {KEY_LABELS[target] || target}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
