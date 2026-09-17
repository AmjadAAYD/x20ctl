import React, { useState } from 'react';
import { Power, ShieldAlert, AlertTriangle, RefreshCw, CheckCircle2 } from 'lucide-react';

interface PowerPageProps {
  idleTimeout: number;
  onUpdateIdleTimeout: (mins: number) => void;
  onFactoryReset: () => void;
  firmwareVersion: string;
}

export const PowerPage: React.FC<PowerPageProps> = ({
  idleTimeout,
  onUpdateIdleTimeout,
  onFactoryReset,
  firmwareVersion,
}) => {
  const [showResetConfirm, setShowResetConfirm] = useState<boolean>(false);
  const [resetSuccess, setResetSuccess] = useState<boolean>(false);

  const handleReset = () => {
    onFactoryReset();
    setShowResetConfirm(false);
    setResetSuccess(true);
    setTimeout(() => setResetSuccess(false), 3000);
  };

  const timeoutOptions = [
    { label: '5 Minutes', val: 5 },
    { label: '10 Minutes', val: 10 },
    { label: '15 Minutes', val: 15 },
    { label: '30 Minutes', val: 30 },
    { label: 'Never (Always On)', val: 0 },
  ];

  return (
    <div className="space-y-6 max-w-3xl mx-auto">
      {/* Header */}
      <div className="pb-4 border-b border-[#332C29]">
        <h2 className="text-base font-semibold text-[#F4F0EB]">Power & Hardware Safety</h2>
        <p className="text-xs text-[#A79C92] mt-0.5">
          Manage sleep timer, verify device firmware details, and perform safe resets.
        </p>
      </div>

      {/* Sleep timeout */}
      <div className="p-5 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-4">
        <div className="flex items-center gap-3">
          <div className="p-2.5 rounded-lg bg-[#241F1D] border border-[#332C29] text-[#FF8A5B]">
            <Power className="w-5 h-5" />
          </div>
          <div>
            <span className="text-sm font-semibold text-[#F4F0EB] block">
              Idle Shutdown Timer
            </span>
            <span className="text-xs text-[#A79C92]">
              Controller automatically powers off when no button or stick activity is detected.
            </span>
          </div>
        </div>

        <div className="grid grid-cols-2 sm:grid-cols-5 gap-2 pt-2">
          {timeoutOptions.map((opt) => (
            <button
              key={opt.val}
              onClick={() => onUpdateIdleTimeout(opt.val)}
              className={`p-2.5 rounded-lg border text-xs text-center transition-all ${
                idleTimeout === opt.val
                  ? 'bg-[#241F1D] text-[#FF8A5B] border-[#FF8A5B] font-semibold'
                  : 'bg-[#131110] text-[#D6CEC6] border-[#332C29] hover:border-[#453B36]'
              }`}
            >
              {opt.label}
            </button>
          ))}
        </div>
      </div>

      {/* Device Info */}
      <div className="p-5 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-3">
        <span className="text-xs font-semibold text-[#F4F0EB] block">
          Hardware & Protocol Verification
        </span>
        <div className="grid grid-cols-2 gap-4 text-xs">
          <div className="p-3 rounded-lg bg-[#131110] border border-[#332C29]">
            <span className="text-[#A79C92] block text-[11px]">Hardware Model</span>
            <span className="font-semibold text-[#F4F0EB] mt-0.5 block">EasySMX X20 / KeyLinker</span>
          </div>
          <div className="p-3 rounded-lg bg-[#131110] border border-[#332C29]">
            <span className="text-[#A79C92] block text-[11px]">Reported Firmware</span>
            <span className="font-semibold text-[#F4F0EB] mt-0.5 block">{firmwareVersion}</span>
          </div>
          <div className="p-3 rounded-lg bg-[#131110] border border-[#332C29]">
            <span className="text-[#A79C92] block text-[11px]">Command Channel</span>
            <span className="font-semibold text-[#86C08A] mt-0.5 block">BLE GATT (Safe Configuration)</span>
          </div>
          <div className="p-3 rounded-lg bg-[#131110] border border-[#332C29]">
            <span className="text-[#A79C92] block text-[11px]">Bootloader Status</span>
            <span className="font-semibold text-[#D6CEC6] mt-0.5 block">Permanently Out of Scope</span>
          </div>
        </div>
      </div>

      {/* Factory Reset */}
      <div className="p-5 rounded-xl bg-[#1B1817] border border-[#332C29] space-y-4">
        <div className="flex items-start gap-3">
          <div className="p-2 rounded-lg bg-[#E5645E]/15 border border-[#E5645E]/30 text-[#E5645E] mt-0.5">
            <AlertTriangle className="w-5 h-5" />
          </div>
          <div className="flex-1">
            <span className="text-sm font-semibold text-[#F4F0EB] block">
              Factory Reset Controller
            </span>
            <p className="text-xs text-[#A79C92] mt-1 leading-relaxed">
              Clears all on-device remaps, custom macros, response curves, and restores default factory calibration.
              You can also perform this hardware-side anytime by <strong className="text-[#F4F0EB]">holding the C button for 5 seconds</strong>.
            </p>

            {resetSuccess && (
              <div className="flex items-center gap-2 text-xs text-[#86C08A] mt-3">
                <CheckCircle2 className="w-4 h-4" />
                <span>Controller reset to factory defaults successfully.</span>
              </div>
            )}

            {!showResetConfirm ? (
              <button
                onClick={() => setShowResetConfirm(true)}
                className="mt-4 px-3.5 py-1.5 text-xs font-semibold rounded-md bg-[#241F1D] hover:bg-[#E5645E]/20 text-[#D6CEC6] hover:text-[#E5645E] border border-[#332C29] hover:border-[#E5645E]/40 transition-colors"
              >
                Reset Controller Settings
              </button>
            ) : (
              <div className="mt-4 p-3 rounded-lg bg-[#131110] border border-[#E5645E]/40 flex items-center justify-between">
                <span className="text-xs text-[#E5645E] font-medium">
                  Are you sure? This will wipe all slots on the controller.
                </span>
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setShowResetConfirm(false)}
                    className="px-2.5 py-1 text-xs rounded bg-[#241F1D] text-[#D6CEC6]"
                  >
                    Cancel
                  </button>
                  <button
                    onClick={handleReset}
                    className="px-3 py-1 text-xs font-bold rounded bg-[#E5645E] text-[#131110]"
                  >
                    Confirm Reset
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
