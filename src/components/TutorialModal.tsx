import React, { useState } from 'react';
import { BookOpen, X, Sliders, Music, Zap, Cpu, Radio, Check } from 'lucide-react';

interface TutorialModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const TutorialModal: React.FC<TutorialModalProps> = ({ isOpen, onClose }) => {
  const [activeTab, setActiveTab] = useState<'connect' | 'remap' | 'macros' | 'curves' | 'save'>('connect');

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in">
      <div className="w-full max-w-3xl bg-[#1B1817] border border-[#332C29] rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="p-4 border-b border-[#332C29] flex items-center justify-between bg-[#171413]">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-[#FF8A5B]/15 border border-[#FF8A5B]/30 text-[#FF8A5B]">
              <BookOpen className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-[#F4F0EB]">EasySMX Configurator Tutorial & Guide</h3>
              <p className="text-xs text-[#A79C92] mt-0.5">
                Learn how to customize your gamepad buttons, macros, curves, and wireless connections.
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

        {/* Tab Navigation */}
        <div className="flex items-center gap-1 px-4 pt-3 pb-2 bg-[#141211] border-b border-[#332C29] overflow-x-auto text-xs">
          <button
            onClick={() => setActiveTab('connect')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-colors ${
              activeTab === 'connect'
                ? 'bg-[#FF8A5B] text-[#131110] font-bold'
                : 'text-[#A79C92] hover:text-[#F4F0EB]'
            }`}
          >
            <Radio className="w-3.5 h-3.5" />
            <span>1. Connections</span>
          </button>

          <button
            onClick={() => setActiveTab('remap')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-colors ${
              activeTab === 'remap'
                ? 'bg-[#FF8A5B] text-[#131110] font-bold'
                : 'text-[#A79C92] hover:text-[#F4F0EB]'
            }`}
          >
            <Sliders className="w-3.5 h-3.5" />
            <span>2. Button Remapping</span>
          </button>

          <button
            onClick={() => setActiveTab('macros')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-colors ${
              activeTab === 'macros'
                ? 'bg-[#FF8A5B] text-[#131110] font-bold'
                : 'text-[#A79C92] hover:text-[#F4F0EB]'
            }`}
          >
            <Music className="w-3.5 h-3.5" />
            <span>3. Rear Macros & Piano Roll</span>
          </button>

          <button
            onClick={() => setActiveTab('curves')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-colors ${
              activeTab === 'curves'
                ? 'bg-[#FF8A5B] text-[#131110] font-bold'
                : 'text-[#A79C92] hover:text-[#F4F0EB]'
            }`}
          >
            <Zap className="w-3.5 h-3.5" />
            <span>4. Sticks & Triggers</span>
          </button>

          <button
            onClick={() => setActiveTab('save')}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg font-medium transition-colors ${
              activeTab === 'save'
                ? 'bg-[#FF8A5B] text-[#131110] font-bold'
                : 'text-[#A79C92] hover:text-[#F4F0EB]'
            }`}
          >
            <Cpu className="w-3.5 h-3.5" />
            <span>5. On-Board Profiles</span>
          </button>
        </div>

        {/* Tab Content */}
        <div className="p-6 overflow-y-auto flex-1 text-xs leading-relaxed text-[#D6CEC6] space-y-4">
          {activeTab === 'connect' && (
            <div className="space-y-3">
              <h4 className="text-sm font-bold text-[#F4F0EB]">Connecting Your Controller</h4>
              <p>The EasySMX app supports three high-speed connection methods:</p>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 pt-1">
                <div className="p-3.5 rounded-xl bg-[#141211] border border-[#332C29]">
                  <strong className="text-[#FF8A5B] block mb-1">🔌 Wired USB-C Cable</strong>
                  <p className="text-[#A79C92]">
                    Zero-latency connection. Plug your USB-C cable directly into your PC. Perfect for competitive gameplay and firmware flashing.
                  </p>
                </div>
                <div className="p-3.5 rounded-xl bg-[#141211] border border-[#332C29]">
                  <strong className="text-[#86C08A] block mb-1">📡 2.4GHz USB Dongle</strong>
                  <p className="text-[#A79C92]">
                    Insert the wireless USB dongle into your PC. Turn on your controller. The app detects the receiver automatically.
                  </p>
                </div>
                <div className="p-3.5 rounded-xl bg-[#141211] border border-[#332C29]">
                  <strong className="text-[#6BA0FA] block mb-1">📶 Bluetooth Wireless</strong>
                  <p className="text-[#A79C92]">
                    Hold Home + X (or pairing button) until LED flashes rapidly. Pair in your OS Bluetooth settings, then press any button to register.
                  </p>
                </div>
              </div>
            </div>
          )}

          {activeTab === 'remap' && (
            <div className="space-y-3">
              <h4 className="text-sm font-bold text-[#F4F0EB]">Remapping Buttons & Triggers</h4>
              <p>
                In the <strong>Buttons</strong> section:
              </p>
              <ol className="list-decimal list-inside space-y-2 pl-1 text-[#A79C92]">
                <li>Click on any button in the interactive 3D controller diagram or browse the remapping list.</li>
                <li>In the clean, centered <strong>&ldquo;Maps to&rdquo;</strong> row, choose the target key you want it to trigger.</li>
                <li>You can map standard face buttons, shoulder bumpers, triggers, or rear paddle switches.</li>
                <li>Press any physical button on your controller to see it instantly light up on the screen in real-time!</li>
              </ol>
            </div>
          )}

          {activeTab === 'macros' && (
            <div className="space-y-3">
              <h4 className="text-sm font-bold text-[#F4F0EB]">Rear Paddle Macros & Piano Roll</h4>
              <p>
                The EasySMX X20 features ergonomic rear paddles (M1, M2, M3, M4) capable of executing multi-button combos with millisecond timing:
              </p>
              <ul className="list-disc list-inside space-y-2 pl-1 text-[#A79C92]">
                <li>
                  <strong className="text-[#F4F0EB]">Step Sequencer:</strong> See every button press, hold duration, and pause gap in chronological order.
                </li>
                <li>
                  <strong className="text-[#FF8A5B]">Piano Roll Sequencer:</strong> Click the <em>&ldquo;🎹 Edit in Piano Roll&rdquo;</em> button to open the visual timeline view. You can see button tracks, time intervals, and test playback with a live moving playhead!
                </li>
                <li>
                  <strong className="text-[#F4F0EB]">Live Execution:</strong> Click &ldquo;Save & Apply&rdquo; to send the sequence to your controller paddle immediately.
                </li>
              </ul>
            </div>
          )}

          {activeTab === 'curves' && (
            <div className="space-y-3">
              <h4 className="text-sm font-bold text-[#F4F0EB]">Stick & Trigger Sensitivity Curves</h4>
              <p>
                Fine-tune your analog sticks and hall-effect triggers for your exact playstyle:
              </p>
              <div className="space-y-2 text-[#A79C92]">
                <p>
                  • <strong>Curve Editor:</strong> Select between Linear, Aggressive, Relaxed, or Instant presets, or drag the control dots to make your own custom curve.
                </p>
                <p>
                  • <strong>Live SVG Visualizers:</strong> Move your physical thumbstick or pull your triggers to see live coordinates, deflection angles, and pressure travel meters right beside the curve!
                </p>
                <p>
                  • <strong>Deadzones:</strong> Adjust the inner circle to eliminate drift, and adjust the outer circle to reach top speed effortlessly.
                </p>
              </div>
            </div>
          )}

          {activeTab === 'save' && (
            <div className="space-y-3">
              <h4 className="text-sm font-bold text-[#F4F0EB]">On-Board Profiles & Saving</h4>
              <p>
                Your changes are instantly stored into your active profile and can be committed to the gamepad&apos;s internal flash memory:
              </p>
              <div className="p-3.5 rounded-xl bg-[#141211] border border-[#332C29] space-y-2">
                <p>
                  1. Click <strong>&ldquo;Save to Device&rdquo;</strong> in the header or sidebar to flash settings onto the controller memory.
                </p>
                <p>
                  2. Your settings remain stored inside the controller even when moving between PC, Switch, or Android!
                </p>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-[#332C29] bg-[#171413] flex justify-end">
          <button
            onClick={onClose}
            className="flex items-center gap-1.5 px-4 py-2 text-xs font-bold rounded-lg bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] transition-colors"
          >
            <Check className="w-4 h-4" />
            <span>Close Guide</span>
          </button>
        </div>
      </div>
    </div>
  );
};
