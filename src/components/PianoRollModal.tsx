import React, { useState, useEffect, useRef } from 'react';
import { MacroStep, KeyName, KEY_LABELS, StickDirection } from '../types/gamepad';
import { Play, Pause, RotateCcw, Check, Plus, Trash2, X, Music, Clock } from 'lucide-react';

interface PianoRollModalProps {
  isOpen: boolean;
  onClose: () => void;
  paddle: 'M1' | 'M2' | 'M3' | 'M4';
  steps: MacroStep[];
  onSaveSteps: (updatedSteps: MacroStep[]) => void;
}

const PIANO_TRACKS: KeyName[] = [
  'Y', 'X', 'B', 'A',
  'RB', 'RT', 'LB', 'LT',
  'DPAD_UP', 'DPAD_RIGHT', 'DPAD_DOWN', 'DPAD_LEFT',
  'L3', 'R3',
];

export const PianoRollModal: React.FC<PianoRollModalProps> = ({
  isOpen,
  onClose,
  paddle,
  steps,
  onSaveSteps,
}) => {
  const [localSteps, setLocalSteps] = useState<MacroStep[]>([]);
  const [isPlaying, setIsPlaying] = useState<boolean>(false);
  const [playbackTimeMs, setPlaybackTimeMs] = useState<number>(0);
  const [zoomMs, setZoomMs] = useState<number>(1); // pixels per ms: 1ms = 0.5px or 1px
  const playheadTimerRef = useRef<number | null>(null);

  // Sync on open
  useEffect(() => {
    if (isOpen) {
      setLocalSteps(JSON.parse(JSON.stringify(steps)));
      setIsPlaying(false);
      setPlaybackTimeMs(0);
    }
  }, [isOpen, steps]);

  if (!isOpen) return null;

  // Calculate cumulative timeline positions for each step
  let cumulativeTime = 0;
  const stepPositions = localSteps.map((s) => {
    const start = cumulativeTime;
    const hold = s.durationMs;
    const gap = s.intervalMs;
    cumulativeTime += hold + gap;
    return {
      step: s,
      startMs: start,
      endMs: start + hold,
      totalEndMs: start + hold + gap,
    };
  });

  const totalDurationMs = Math.max(500, cumulativeTime);

  // Playhead loop
  const handleTogglePlay = () => {
    if (isPlaying) {
      setIsPlaying(false);
      if (playheadTimerRef.current) clearInterval(playheadTimerRef.current);
    } else {
      setIsPlaying(true);
      const startTime = Date.now() - (playbackTimeMs >= totalDurationMs ? 0 : playbackTimeMs);
      playheadTimerRef.current = window.setInterval(() => {
        const elapsed = Date.now() - startTime;
        if (elapsed >= totalDurationMs) {
          setIsPlaying(false);
          setPlaybackTimeMs(0);
          if (playheadTimerRef.current) clearInterval(playheadTimerRef.current);
        } else {
          setPlaybackTimeMs(elapsed);
        }
      }, 16);
    }
  };

  const handleAddStep = (targetBtn?: KeyName) => {
    const newStep: MacroStep = {
      id: 'step-' + Math.random().toString(36).substring(2, 9),
      buttons: targetBtn ? [targetBtn] : ['A'],
      leftStick: StickDirection.NEUTRAL,
      rightStick: StickDirection.NEUTRAL,
      durationMs: 50,
      intervalMs: 25,
    };
    setLocalSteps((prev) => [...prev, newStep]);
  };

  const handleUpdateStepDuration = (stepId: string, durationMs: number) => {
    setLocalSteps((prev) =>
      prev.map((s) => (s.id === stepId ? { ...s, durationMs: Math.max(10, durationMs) } : s))
    );
  };

  const handleUpdateStepInterval = (stepId: string, intervalMs: number) => {
    setLocalSteps((prev) =>
      prev.map((s) => (s.id === stepId ? { ...s, intervalMs: Math.max(0, intervalMs) } : s))
    );
  };

  const toggleKeyOnStep = (stepId: string, key: KeyName) => {
    setLocalSteps((prev) =>
      prev.map((s) => {
        if (s.id !== stepId) return s;
        const exists = s.buttons.includes(key);
        const newButtons = exists ? s.buttons.filter((b) => b !== key) : [...s.buttons, key];
        return { ...s, buttons: newButtons.length > 0 ? newButtons : [key] };
      })
    );
  };

  const handleDeleteStep = (stepId: string) => {
    setLocalSteps((prev) => prev.filter((s) => s.id !== stepId));
  };

  const handleSave = () => {
    onSaveSteps(localSteps);
    onClose();
  };

  // Convert ms to pixel offset
  const msToPx = (ms: number) => ms * 0.8;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-5xl bg-[#1B1817] border border-[#332C29] rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Modal Top Bar */}
        <div className="p-4 border-b border-[#332C29] flex items-center justify-between bg-[#171413]">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-[#241F1D] border border-[#332C29] text-[#FF8A5B]">
              <Music className="w-5 h-5" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-[#F4F0EB]">
                  {paddle} Piano-Roll Macro Sequencer
                </h3>
                <span className="text-[10px] px-2 py-0.5 rounded bg-[#FF8A5B]/15 text-[#FF8A5B] font-mono font-semibold border border-[#FF8A5B]/30">
                  {localSteps.length} Steps · {totalDurationMs} ms Total
                </span>
              </div>
              <p className="text-xs text-[#A79C92] mt-0.5">
                Visual timeline editor. Each row represents a gamepad button. Adjust hold timers and pause gaps.
              </p>
            </div>
          </div>

          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-[#A79C92] hover:text-[#F4F0EB] hover:bg-[#241F1D] transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Toolbar */}
        <div className="px-5 py-2.5 bg-[#141211] border-b border-[#332C29] flex items-center justify-between text-xs">
          <div className="flex items-center gap-2">
            <button
              onClick={handleTogglePlay}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-md font-semibold transition-all ${
                isPlaying
                  ? 'bg-[#E5645E] text-[#131110]'
                  : 'bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] hover:text-[#F4F0EB] border border-[#332C29]'
              }`}
            >
              {isPlaying ? <Pause className="w-3.5 h-3.5" /> : <Play className="w-3.5 h-3.5 text-[#FF8A5B]" />}
              <span>{isPlaying ? 'Pause' : 'Test Playback'}</span>
            </button>

            <button
              onClick={() => {
                setIsPlaying(false);
                setPlaybackTimeMs(0);
                if (playheadTimerRef.current) clearInterval(playheadTimerRef.current);
              }}
              className="p-1.5 rounded-md bg-[#241F1D] hover:bg-[#2C2624] text-[#A79C92] hover:text-[#F4F0EB] border border-[#332C29]"
              title="Reset Timeline"
            >
              <RotateCcw className="w-3.5 h-3.5" />
            </button>

            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-[#241F1D] border border-[#332C29] font-mono text-[11px] text-[#A79C92]">
              <Clock className="w-3 h-3 text-[#FF8A5B]" />
              <span>Time: {Math.round(playbackTimeMs)}ms</span>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => handleAddStep()}
              disabled={localSteps.length >= 42}
              className="flex items-center gap-1 px-3 py-1.5 rounded-md bg-[#241F1D] hover:bg-[#2C2624] text-[#FF8A5B] font-semibold border border-[#332C29] transition-colors disabled:opacity-40"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Add Step Block</span>
            </button>
          </div>
        </div>

        {/* Sequencer Grid Scrollable Body */}
        <div className="flex-1 overflow-y-auto p-4 select-none">
          <div className="flex min-w-[750px]">
            {/* Left Keys Label Column */}
            <div className="w-28 shrink-0 pr-3 space-y-1 pt-6 border-r border-[#332C29]">
              {PIANO_TRACKS.map((key) => {
                // Check if key is currently active in playback
                const isCurrentlyActive = stepPositions.some(
                  (sp) =>
                    sp.step.buttons.includes(key) &&
                    playbackTimeMs >= sp.startMs &&
                    playbackTimeMs <= sp.endMs
                );

                return (
                  <div
                    key={key}
                    className={`h-7 px-2 rounded flex items-center justify-between text-[11px] font-mono transition-colors ${
                      isCurrentlyActive
                        ? 'bg-[#FF8A5B] text-[#131110] font-bold shadow-[0_0_8px_rgba(255,138,91,0.5)]'
                        : 'bg-[#141211] text-[#A79C92] border border-[#2A2421]'
                    }`}
                  >
                    <span>{KEY_LABELS[key] || key}</span>
                    <span className="text-[9px] opacity-70">
                      {key.startsWith('DPAD') ? 'DIR' : 'KEY'}
                    </span>
                  </div>
                );
              })}
            </div>

            {/* Right Timeline Area */}
            <div className="flex-1 overflow-x-auto relative pl-4">
              {/* Time ruler bar */}
              <div
                className="h-6 relative border-b border-[#332C29] mb-1 font-mono text-[10px] text-[#A79C92]"
                style={{ width: `${Math.max(650, msToPx(totalDurationMs) + 60)}px` }}
              >
                {Array.from({ length: Math.ceil(totalDurationMs / 100) + 1 }).map((_, i) => {
                  const ms = i * 100;
                  return (
                    <div
                      key={ms}
                      className="absolute top-0 flex flex-col items-start"
                      style={{ left: `${msToPx(ms)}px` }}
                    >
                      <span className="border-l border-[#332C29] pl-1 h-3">{ms}ms</span>
                    </div>
                  );
                })}
              </div>

              {/* Tracks Container with Playhead */}
              <div
                className="relative space-y-1"
                style={{ width: `${Math.max(650, msToPx(totalDurationMs) + 60)}px` }}
              >
                {/* Moving Playhead Line */}
                <div
                  className="absolute top-0 bottom-0 w-[2px] bg-[#FF8A5B] z-30 pointer-events-none transition-all shadow-[0_0_8px_#FF8A5B]"
                  style={{ left: `${msToPx(playbackTimeMs)}px` }}
                />

                {/* Track Rows */}
                {PIANO_TRACKS.map((trackKey) => (
                  <div
                    key={trackKey}
                    className="h-7 bg-[#141211] rounded border border-[#241F1D] relative overflow-hidden"
                  >
                    {/* Render blocks that contain this key */}
                    {stepPositions.map((sp, idx) => {
                      if (!sp.step.buttons.includes(trackKey)) return null;

                      const leftPx = msToPx(sp.startMs);
                      const widthPx = Math.max(24, msToPx(sp.step.durationMs));

                      return (
                        <div
                          key={sp.step.id}
                          style={{ left: `${leftPx}px`, width: `${widthPx}px` }}
                          className="absolute top-0.5 bottom-0.5 rounded bg-gradient-to-r from-[#FF8A5B] to-[#FFB020] text-[#131110] font-mono text-[10px] font-bold flex items-center justify-between px-1.5 shadow-[0_1px_3px_rgba(0,0,0,0.4)] hover:brightness-110 cursor-pointer"
                          title={`Step ${idx + 1}: ${trackKey} (Hold: ${sp.step.durationMs}ms, Pause: ${sp.step.intervalMs}ms)`}
                        >
                          <span className="truncate">{trackKey}</span>
                          <span className="text-[8px] opacity-80">{sp.step.durationMs}ms</span>
                        </div>
                      );
                    })}
                  </div>
                ))}
              </div>
            </div>
          </div>

          {/* Steps Detail Cards below piano roll */}
          <div className="mt-6 pt-4 border-t border-[#332C29]">
            <span className="text-xs font-semibold text-[#F4F0EB] block mb-2.5">
              Step Timing Fine Tuning (Hold & Pause)
            </span>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3">
              {localSteps.map((step, idx) => (
                <div
                  key={step.id}
                  className="p-3 rounded-lg bg-[#141211] border border-[#332C29] space-y-2 text-xs"
                >
                  <div className="flex items-center justify-between">
                    <span className="font-mono font-bold text-[#FF8A5B]">Step #{idx + 1}</span>
                    <button
                      onClick={() => handleDeleteStep(step.id)}
                      className="text-[#A79C92] hover:text-[#E5645E]"
                    >
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>

                  <div className="flex flex-wrap gap-1">
                    {PIANO_TRACKS.map((k) => {
                      const active = step.buttons.includes(k);
                      return (
                        <button
                          key={k}
                          onClick={() => toggleKeyOnStep(step.id, k)}
                          className={`px-1.5 py-0.5 text-[9px] rounded font-mono border ${
                            active
                              ? 'bg-[#FF8A5B] text-[#131110] border-[#FF8A5B] font-bold'
                              : 'bg-[#1B1817] text-[#A79C92] border-[#332C29]'
                          }`}
                        >
                          {k}
                        </button>
                      );
                    })}
                  </div>

                  <div className="grid grid-cols-2 gap-2 pt-1">
                    <div>
                      <span className="text-[10px] text-[#A79C92] block">Hold (ms)</span>
                      <input
                        type="number"
                        min="10"
                        max="1500"
                        step="10"
                        value={step.durationMs}
                        onChange={(e) =>
                          handleUpdateStepDuration(step.id, parseInt(e.target.value) || 20)
                        }
                        className="w-full bg-[#241F1D] border border-[#332C29] text-xs font-mono text-[#F4F0EB] rounded px-1.5 py-1 outline-none"
                      />
                    </div>
                    <div>
                      <span className="text-[10px] text-[#A79C92] block">Pause (ms)</span>
                      <input
                        type="number"
                        min="0"
                        max="1500"
                        step="10"
                        value={step.intervalMs}
                        onChange={(e) =>
                          handleUpdateStepInterval(step.id, parseInt(e.target.value) || 0)
                        }
                        className="w-full bg-[#241F1D] border border-[#332C29] text-xs font-mono text-[#F4F0EB] rounded px-1.5 py-1 outline-none"
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>

        {/* Modal Footer */}
        <div className="p-4 border-t border-[#332C29] bg-[#171413] flex items-center justify-between">
          <span className="text-xs text-[#A79C92]">
            Changes made here directly compile into {paddle}&apos;s on-device macro sequence.
          </span>

          <div className="flex items-center gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-xs font-medium rounded-lg bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={handleSave}
              className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] transition-colors shadow-md"
            >
              <Check className="w-4 h-4" />
              <span>Save & Apply to {paddle}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
