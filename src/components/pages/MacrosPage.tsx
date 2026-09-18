import React, { useState } from "react";
import {
  MacroStep,
  KeyName,
  StickDirection,
  STICK_DIRECTION_NAMES,
  KEY_LABELS,
} from "../../types/gamepad";
import { Plus, Trash2, Music, CheckCircle2, Sliders } from "lucide-react";
import { PianoRollModal } from "../PianoRollModal";

interface MacrosPageProps {
  loops: Record<"M1" | "M2" | "M3" | "M4", number>;
  onUpdateLoop: (paddle: "M1" | "M2" | "M3" | "M4", value: number) => void;
  macros: {
    M1: MacroStep[];
    M2: MacroStep[];
    M3: MacroStep[];
    M4: MacroStep[];
  };
  onUpdateMacros: (
    paddle: "M1" | "M2" | "M3" | "M4",
    steps: MacroStep[],
  ) => void;
  onClearMacro: (paddle: "M1" | "M2" | "M3" | "M4") => void;
}

const PADDLES: Array<"M1" | "M2" | "M3" | "M4"> = ["M1", "M2", "M3", "M4"];

const TOGGLEABLE_BUTTONS: KeyName[] = [
  "A",
  "B",
  "X",
  "Y",
  "LB",
  "RB",
  "LT",
  "RT",
  "L3",
  "R3",
  "DPAD_UP",
  "DPAD_DOWN",
  "DPAD_LEFT",
  "DPAD_RIGHT",
];

// Interactive 8-way directional dial component
const StickDirectionDial: React.FC<{
  value: StickDirection;
  onChange: (dir: StickDirection) => void;
  label: string;
}> = ({ value, onChange, label }) => {
  const directions = [
    { dir: StickDirection.UP, angle: 0, x: 25, y: 5 },
    { dir: StickDirection.UP_RIGHT, angle: 45, x: 39, y: 11 },
    { dir: StickDirection.RIGHT, angle: 90, x: 45, y: 25 },
    { dir: StickDirection.DOWN_RIGHT, angle: 135, x: 39, y: 39 },
    { dir: StickDirection.DOWN, angle: 180, x: 25, y: 45 },
    { dir: StickDirection.DOWN_LEFT, angle: 225, x: 11, y: 39 },
    { dir: StickDirection.LEFT, angle: 270, x: 5, y: 25 },
    { dir: StickDirection.UP_LEFT, angle: 315, x: 11, y: 11 },
  ];

  return (
    <div className="flex flex-col items-center gap-1">
      <span className="text-[10px] text-[#A79C92]">{label}</span>
      <div className="relative w-[52px] h-[52px] rounded-full bg-[#131110] border border-[#332C29] p-1 flex items-center justify-center">
        {/* Neutral center */}
        <button
          type="button"
          onClick={() => onChange(StickDirection.NEUTRAL)}
          title="Neutral"
          className={`w-3.5 h-3.5 rounded-full z-10 transition-colors ${
            value === StickDirection.NEUTRAL
              ? "bg-[#FF8A5B] shadow-[0_0_6px_rgba(255,138,91,0.5)]"
              : "bg-[#241F1D] hover:bg-[#453B36]"
          }`}
        />

        {/* 8 direction dots */}
        {directions.map((d) => {
          const isSelected = value === d.dir;
          return (
            <button
              key={d.dir}
              type="button"
              onClick={() => onChange(d.dir)}
              title={STICK_DIRECTION_NAMES[d.dir]}
              style={{ left: `${d.x - 4}px`, top: `${d.y - 4}px` }}
              className={`absolute w-2.5 h-2.5 rounded-full transition-all ${
                isSelected
                  ? "bg-[#FF8A5B] scale-125 shadow-[0_0_6px_rgba(255,138,91,0.6)]"
                  : "bg-[#332C29] hover:bg-[#A79C92]"
              }`}
            />
          );
        })}
      </div>
      <span className="text-[10px] text-[#D6CEC6] font-mono h-3.5">
        {STICK_DIRECTION_NAMES[value]}
      </span>
    </div>
  );
};

export const MacrosPage: React.FC<MacrosPageProps> = ({
  loops,
  onUpdateLoop,
  macros,
  onUpdateMacros,
  onClearMacro,
}) => {
  const [selectedPaddle, setSelectedPaddle] = useState<
    "M1" | "M2" | "M3" | "M4"
  >("M1");
  const [isPianoRollOpen, setIsPianoRollOpen] = useState<boolean>(false);
  const [savedBanner, setSavedBanner] = useState<boolean>(false);

  const currentSteps = macros[selectedPaddle] || [];

  const handleAddStep = () => {
    const newStep: MacroStep = {
      id: Math.random().toString(36).substring(2, 9),
      buttons: ["A"],
      leftStick: StickDirection.NEUTRAL,
      rightStick: StickDirection.NEUTRAL,
      durationMs: 40,
      intervalMs: 20,
    };
    onUpdateMacros(selectedPaddle, [...currentSteps, newStep]);
  };

  const handleUpdateStep = (stepId: string, updates: Partial<MacroStep>) => {
    const updated = currentSteps.map((s) =>
      s.id === stepId ? { ...s, ...updates } : s,
    );
    onUpdateMacros(selectedPaddle, updated);
  };

  const handleDeleteStep = (stepId: string) => {
    const updated = currentSteps.filter((s) => s.id !== stepId);
    onUpdateMacros(selectedPaddle, updated);
  };

  const toggleButtonInStep = (stepId: string, btn: KeyName) => {
    const step = currentSteps.find((s) => s.id === stepId);
    if (!step) return;
    const exists = step.buttons.includes(btn);
    const newButtons = exists
      ? step.buttons.filter((b) => b !== btn)
      : [...step.buttons, btn];
    handleUpdateStep(stepId, { buttons: newButtons });
  };

  const handleSaveFromPianoRoll = (updatedSteps: MacroStep[]) => {
    onUpdateMacros(selectedPaddle, updatedSteps);
    setSavedBanner(true);
    setTimeout(() => setSavedBanner(false), 2500);
  };

  return (
    <div className="space-y-6 max-w-5xl mx-auto select-none">
      {/* Header text */}
      <div className="flex items-center justify-between pb-4 border-b border-[#332C29]">
        <div>
          <h2 className="text-base font-bold text-[#F4F0EB]">
            Rear Paddle Macros & Combos
          </h2>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Configure step sequences for M1, M2, M3, and M4 with 5 ms timing, or
            launch the interactive Piano-Roll editor.
          </p>
        </div>

        {/* Record & Clear Actions */}
        <div className="flex items-center gap-2">
          {currentSteps.length > 0 && (
            <button
              onClick={() => onClearMacro(selectedPaddle)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-[#241F1D] hover:bg-[#2C2624] text-[#A79C92] hover:text-[#E5645E] border border-[#332C29] transition-colors"
            >
              <Trash2 className="w-3.5 h-3.5" />
              <span>Clear Slot</span>
            </button>
          )}
        </div>
      </div>

      {/* Success Banner when saved from Piano Roll */}
      {savedBanner && (
        <div className="p-3 rounded-xl bg-[#86C08A]/15 border border-[#86C08A]/30 text-xs text-[#86C08A] flex items-center gap-2 animate-fade-in font-medium">
          <CheckCircle2 className="w-4 h-4" />
          <span>
            Piano Roll changes successfully compiled into {selectedPaddle}{" "}
            draft. Select Apply changes to send it.
          </span>
        </div>
      )}

      {/* Paddle Selector Tabs */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
        {PADDLES.map((paddle) => {
          const isSelected = selectedPaddle === paddle;
          const stepCount = (macros[paddle] || []).length;

          return (
            <button
              key={paddle}
              onClick={() => setSelectedPaddle(paddle)}
              className={`p-3.5 rounded-xl border text-left transition-all ${
                isSelected
                  ? "bg-[#241F1D] border-[#FF8A5B] shadow-md shadow-[#FF8A5B]/10"
                  : "bg-[#1B1817] border-[#332C29] hover:border-[#4A3F3B]"
              }`}
            >
              <div className="flex items-center justify-between">
                <span className="text-sm font-black text-[#F4F0EB]">
                  {paddle}
                </span>
                <span
                  className={`text-[10px] px-1.5 py-0.5 rounded font-mono font-bold ${
                    stepCount > 0
                      ? "bg-[#FF8A5B]/20 text-[#FF8A5B]"
                      : "bg-[#131110] text-[#A79C92]"
                  }`}
                >
                  {stepCount} {stepCount === 1 ? "step" : "steps"}
                </span>
              </div>
              <p className="text-[11px] text-[#A79C92] mt-1">
                {paddle === "M1" || paddle === "M3"
                  ? "Left rear paddle"
                  : "Right rear paddle"}
              </p>
            </button>
          );
        })}
      </div>

      {/* Piano-Roll / Steps Sequence */}
      <label className="flex items-center gap-3 text-xs text-[#A79C92]">
        Loop interval (0 = run once)
        <input
          aria-label="Macro loop interval"
          type="number"
          min="0"
          max="20475"
          step="5"
          value={loops[selectedPaddle]}
          onChange={(e) => onUpdateLoop(selectedPaddle, Number(e.target.value))}
          className="bg-[#241F1D] border border-[#332C29] rounded-md p-2 w-24"
        />{" "}
        ms
      </label>
      <div className="p-5 rounded-2xl bg-[#1B1817] border border-[#332C29] space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 pb-3 border-b border-[#332C29]">
          <div>
            <div className="flex items-center gap-2">
              <span className="text-sm font-bold text-[#F4F0EB]">
                {selectedPaddle} Step Sequence
              </span>
              <span className="text-[11px] text-[#A79C92]">
                (
                {currentSteps.reduce(
                  (n, s) => n + 1 + (s.intervalMs > 0 ? 1 : 0),
                  0,
                )}{" "}
                of 47 wire entries)
              </span>
            </div>
            <p className="text-xs text-[#A79C92] mt-0.5">
              Click &ldquo;Edit in Piano Roll&rdquo; to visually adjust hold
              times and notes on a timeline.
            </p>
          </div>

          <div className="flex items-center gap-2">
            {/* Direct Edit in Piano Roll Button */}
            <button
              onClick={() => setIsPianoRollOpen(true)}
              className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] font-bold transition-all shadow-sm"
              title="Open Piano Roll Visual Sequencer"
            >
              <Music className="w-3.5 h-3.5" />
              <span>🎹 Edit in Piano Roll</span>
            </button>

            <button
              onClick={handleAddStep}
              disabled={
                currentSteps.reduce(
                  (n, s) => n + 1 + (s.intervalMs > 0 ? 1 : 0),
                  0,
                ) > 45
              }
              className="flex items-center gap-1 px-3 py-1.5 text-xs rounded-lg bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] font-semibold border border-[#332C29] transition-colors disabled:opacity-50"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Add Step</span>
            </button>
          </div>
        </div>

        {currentSteps.length === 0 ? (
          <div className="py-14 text-center text-xs text-[#A79C92] bg-[#141211] rounded-xl border border-dashed border-[#332C29] space-y-2">
            <Music className="w-8 h-8 text-[#A79C92]/50 mx-auto" />
            <p className="font-semibold text-[#D6CEC6]">
              No sequence in this draft for {selectedPaddle}.
            </p>
            <p className="text-[11px] text-[#A79C92]">
              Click &ldquo;Edit in Piano Roll&rdquo; or &ldquo;Add Step&rdquo;
              to program your custom combo.
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            {currentSteps.map((step, idx) => (
              <div
                key={step.id}
                className="p-4 rounded-xl bg-[#141211] border border-[#332C29] flex flex-col md:flex-row items-start md:items-center justify-between gap-4 transition-all hover:border-[#4A3F3B]"
              >
                {/* Step Index & Sticks */}
                <div className="flex items-center gap-4 shrink-0">
                  <div className="w-8 h-8 rounded-lg bg-[#241F1D] border border-[#332C29] flex items-center justify-center text-xs font-mono font-black text-[#FF8A5B]">
                    #{idx + 1}
                  </div>

                  {/* Left Stick Direction */}
                  <StickDirectionDial
                    label="Left Stick"
                    value={step.leftStick}
                    onChange={(dir) =>
                      handleUpdateStep(step.id, { leftStick: dir })
                    }
                  />

                  {/* Right Stick Direction */}
                  <StickDirectionDial
                    label="Right Stick"
                    value={step.rightStick}
                    onChange={(dir) =>
                      handleUpdateStep(step.id, { rightStick: dir })
                    }
                  />
                </div>

                {/* Buttons in step */}
                <div className="flex-1 min-w-[200px]">
                  <span className="text-[10px] text-[#A79C92] block mb-1.5 font-medium">
                    Gamepad buttons active in this step:
                  </span>
                  <div className="flex flex-wrap gap-1">
                    {TOGGLEABLE_BUTTONS.map((btn) => {
                      const active = step.buttons.includes(btn);
                      return (
                        <button
                          key={btn}
                          type="button"
                          onClick={() => toggleButtonInStep(step.id, btn)}
                          className={`px-2 py-0.5 rounded text-[10px] font-mono transition-all border ${
                            active
                              ? "bg-[#FF8A5B] text-[#131110] border-[#FF8A5B] font-bold"
                              : "bg-[#1B1817] text-[#A79C92] border-[#332C29] hover:text-[#F4F0EB]"
                          }`}
                        >
                          {KEY_LABELS[btn]}
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Timing controls, Edit in Piano Roll, & Delete */}
                <div className="flex items-center gap-3 shrink-0">
                  <div className="text-right">
                    <span className="text-[10px] text-[#A79C92] block">
                      Hold (ms)
                    </span>
                    <input
                      type="number"
                      min="5"
                      max="1000"
                      step="5"
                      value={step.durationMs}
                      onChange={(e) =>
                        handleUpdateStep(step.id, {
                          durationMs: parseInt(e.target.value) || 20,
                        })
                      }
                      className="w-16 bg-[#241F1D] border border-[#332C29] text-xs font-mono text-[#F4F0EB] rounded-md px-1.5 py-1 text-center outline-none focus:border-[#FF8A5B]"
                    />
                  </div>

                  <div className="text-right">
                    <span className="text-[10px] text-[#A79C92] block">
                      Pause (ms)
                    </span>
                    <input
                      type="number"
                      min="0"
                      max="1000"
                      step="5"
                      value={step.intervalMs}
                      onChange={(e) =>
                        handleUpdateStep(step.id, {
                          intervalMs: parseInt(e.target.value) || 0,
                        })
                      }
                      className="w-16 bg-[#241F1D] border border-[#332C29] text-xs font-mono text-[#F4F0EB] rounded-md px-1.5 py-1 text-center outline-none focus:border-[#FF8A5B]"
                    />
                  </div>

                  {/* Step-specific Edit Button to launch Piano Roll */}
                  <button
                    onClick={() => setIsPianoRollOpen(true)}
                    title="Edit in Piano Roll"
                    className="p-2 rounded-lg bg-[#241F1D] hover:bg-[#2C2624] text-[#FF8A5B] border border-[#332C29] transition-colors"
                  >
                    <Sliders className="w-3.5 h-3.5" />
                  </button>

                  <button
                    onClick={() => handleDeleteStep(step.id)}
                    title="Remove Step"
                    className="p-2 rounded-lg text-[#A79C92] hover:text-[#E5645E] hover:bg-[#241F1D] transition-colors"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Piano Roll Modal */}
      <PianoRollModal
        isOpen={isPianoRollOpen}
        onClose={() => setIsPianoRollOpen(false)}
        paddle={selectedPaddle}
        steps={currentSteps}
        onSaveSteps={handleSaveFromPianoRoll}
      />
    </div>
  );
};
