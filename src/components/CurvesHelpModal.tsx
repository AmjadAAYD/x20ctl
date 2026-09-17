import React from 'react';
import { HelpCircle, X, Compass, Zap, Target, Gauge, Check } from 'lucide-react';

interface CurvesHelpModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const CurvesHelpModal: React.FC<CurvesHelpModalProps> = ({ isOpen, onClose }) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-2xl bg-[#1B1817] border border-[#332C29] rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="p-4 border-b border-[#332C29] flex items-center justify-between bg-[#171413]">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-[#FF8A5B]/15 border border-[#FF8A5B]/30 text-[#FF8A5B]">
              <HelpCircle className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-[#F4F0EB]">
                Don&apos;t Understand What To Do?
              </h3>
              <p className="text-xs text-[#A79C92] mt-0.5">
                Simple, jargon-free explanations for sticks, triggers, and deadzones.
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

        {/* Content Body */}
        <div className="p-6 space-y-6 overflow-y-auto text-xs leading-relaxed text-[#D6CEC6]">
          {/* Card 1: Inner Deadzone */}
          <div className="p-4 rounded-xl bg-[#141211] border border-[#332C29] flex gap-3.5">
            <div className="p-2 rounded-lg bg-[#241F1D] text-[#FF8A5B] shrink-0 h-fit">
              <Compass className="w-5 h-5" />
            </div>
            <div>
              <h4 className="text-sm font-bold text-[#F4F0EB]">
                1. Inner Deadzone (Fixing Drift)
              </h4>
              <p className="mt-1 text-[#A79C92]">
                <strong className="text-[#FF8A5B]">What it is:</strong> A small invisible circle around the resting center of your thumbstick where nothing happens.
              </p>
              <p className="mt-1">
                <strong className="text-[#F4F0EB]">When to change it:</strong> If your game camera or character slowly creeps/drifts on its own when your fingers are completely off the stick, <em>turn this up slightly</em> (e.g. from 5% to 8%) until the creeping stops!
              </p>
            </div>
          </div>

          {/* Card 2: Outer Deadzone */}
          <div className="p-4 rounded-xl bg-[#141211] border border-[#332C29] flex gap-3.5">
            <div className="p-2 rounded-lg bg-[#241F1D] text-[#86C08A] shrink-0 h-fit">
              <Gauge className="w-5 h-5" />
            </div>
            <div>
              <h4 className="text-sm font-bold text-[#F4F0EB]">
                2. Outer Deadzone (Hitting Max Speed)
              </h4>
              <p className="mt-1 text-[#A79C92]">
                <strong className="text-[#86C08A]">What it is:</strong> The point where pushing the stick counts as 100% full sprint.
              </p>
              <p className="mt-1">
                <strong className="text-[#F4F0EB]">When to change it:</strong> If you feel like you have to push the stick super hard against the plastic edge to sprint, <em>lower this to around 85%–90%</em>. You will reach top speed earlier and easier!
              </p>
            </div>
          </div>

          {/* Card 3: Curve Presets */}
          <div className="p-4 rounded-xl bg-[#141211] border border-[#332C29] flex gap-3.5">
            <div className="p-2 rounded-lg bg-[#241F1D] text-[#FFB020] shrink-0 h-fit">
              <Target className="w-5 h-5" />
            </div>
            <div className="space-y-2">
              <h4 className="text-sm font-bold text-[#F4F0EB]">
                3. Response Curve Presets (How Aiming Feels)
              </h4>
              <ul className="space-y-1.5 pl-1">
                <li>
                  <strong className="text-[#F4F0EB]">Linear (Stock):</strong> Normal feel. 50% stick push gives exactly 50% movement speed. Great for all games.
                </li>
                <li>
                  <strong className="text-[#FF8A5B]">Aggressive (Fast Aim):</strong> Quick reaction. Small stick nudges turn your screen faster. Awesome for fast FPS shooters and 180° turns.
                </li>
                <li>
                  <strong className="text-[#86C08A]">Relaxed (Sniper Precision):</strong> Micro-aiming. Small stick movements are smoothed down and extra slow, so you can lock your crosshairs on a distant headshot without over-aiming.
                </li>
                <li>
                  <strong className="text-[#FFB020]">Instant (Hair Trigger):</strong> The instant you tap the trigger or stick, it acts like a mouse click! Best for LT/RT triggers to shoot immediately.
                </li>
              </ul>
            </div>
          </div>

          {/* Card 4: P1 & P2 Dots */}
          <div className="p-4 rounded-xl bg-[#141211] border border-[#332C29] flex gap-3.5">
            <div className="p-2 rounded-lg bg-[#241F1D] text-[#6BA0FA] shrink-0 h-fit">
              <Zap className="w-5 h-5" />
            </div>
            <div>
              <h4 className="text-sm font-bold text-[#F4F0EB]">
                4. What are the P1 and P2 dots on the graph?
              </h4>
              <p className="mt-1">
                Think of the curve like a rubber band. Dragging the P1 and P2 dots pulls and shapes the line. The higher the line arches, the quicker and more sensitive the controller will feel!
              </p>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-[#332C29] bg-[#171413] flex justify-end">
          <button
            onClick={onClose}
            className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] transition-colors"
          >
            <Check className="w-4 h-4" />
            <span>Got It, Thanks!</span>
          </button>
        </div>
      </div>
    </div>
  );
};
