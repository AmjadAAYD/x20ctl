import React, { useState, useEffect } from 'react';
import { Radio, Usb, Wifi, Play, RefreshCw, CheckCircle2, Gamepad2, ArrowRight } from 'lucide-react';
import { ConnectionType } from '../types/gamepad';

interface IntroScannerProps {
  onEnterApp?: (connectionMode: ConnectionType) => void;
  hardwareDetected?: boolean;
  controllerName?: string;
  onConnect?: (name: string, connectionMode: ConnectionType) => void;
  onSkip?: () => void;
}

export const IntroScanner: React.FC<IntroScannerProps> = ({
  onEnterApp,
  hardwareDetected = false,
  controllerName = 'EasySMX X20 PRO Gamepad',
  onConnect,
  onSkip,
}) => {
  const [scanning, setScanning] = useState<boolean>(true);
  const [scanProgress, setScanProgress] = useState<number>(0);
  const [selectedMode, setSelectedMode] = useState<ConnectionType>('dongle');

  useEffect(() => {
    // Simulated progressive scanning sweep
    setScanning(true);
    setScanProgress(10);
    const t1 = setTimeout(() => setScanProgress(45), 400);
    const t2 = setTimeout(() => setScanProgress(80), 900);
    const t3 = setTimeout(() => {
      setScanProgress(100);
      setScanning(false);
    }, 1400);

    return () => {
      clearTimeout(t1);
      clearTimeout(t2);
      clearTimeout(t3);
    };
  }, []);

  const handleRescan = () => {
    setScanning(true);
    setScanProgress(10);
    setTimeout(() => setScanProgress(55), 500);
    setTimeout(() => {
      setScanProgress(100);
      setScanning(false);
    }, 1100);
  };

  return (
    <div className="fixed inset-0 z-50 bg-[#131110] text-[#F4F0EB] flex flex-col items-center justify-center p-6 select-none overflow-hidden">
      {/* Subtle Background Glows & Ambient Lighting */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[350px] bg-[#FF8A5B]/10 rounded-full blur-[120px] pointer-events-none" />
      <div className="absolute bottom-10 left-10 w-[300px] h-[300px] bg-[#FFB020]/5 rounded-full blur-[90px] pointer-events-none" />

      {/* Main Container */}
      <div className="relative z-10 max-w-xl w-full flex flex-col items-center text-center space-y-6 animate-fade-in">
        {/* EasySMX Emblem & Slogan */}
        <div className="flex flex-col items-center space-y-3">
          <div className="w-16 h-16 rounded-2xl bg-[#1B1817] border border-[#332C29] flex items-center justify-center shadow-xl relative overflow-hidden group">
            <div className="absolute inset-0 bg-gradient-to-br from-[#FF8A5B]/20 to-transparent" />
            <Gamepad2 className="w-8 h-8 text-[#FF8A5B] relative z-10" />
          </div>

          <div>
            <div className="flex items-center justify-center gap-2">
              <span className="text-2xl font-black tracking-wider uppercase text-[#F4F0EB]">
                EasySMX
              </span>
              <span className="px-2 py-0.5 rounded bg-[#FF8A5B] text-[#131110] text-[11px] font-extrabold uppercase tracking-wider">
                X20 PRO
              </span>
            </div>
            <p className="text-xs tracking-widest text-[#FF8A5B] font-semibold uppercase mt-1">
              Precision Engineered · Unbound Performance
            </p>
          </div>
        </div>

        {/* Controller Radar Scanner Visual */}
        <div className="relative w-72 h-44 my-2 flex items-center justify-center">
          {/* Radar Waves */}
          <div
            className={`absolute inset-0 rounded-full border border-[#FF8A5B]/20 transition-all duration-1000 ${
              scanning ? 'animate-ping opacity-30' : 'opacity-10 scale-90'
            }`}
          />
          <div
            className={`absolute w-48 h-32 rounded-full border border-[#FF8A5B]/40 transition-all duration-700 ${
              scanning ? 'animate-pulse' : 'opacity-20'
            }`}
          />

          {/* Central Gamepad Silhouette Graphic */}
          <div className="relative z-10 p-5 rounded-2xl bg-[#1B1817] border border-[#332C29] shadow-2xl flex flex-col items-center">
            <div className="w-24 h-14 relative flex items-center justify-center">
              <svg viewBox="0 0 120 70" className="w-full h-full filter drop-shadow">
                <path
                  d="M 25 15 C 20 15, 10 20, 8 35 C 5 50, 12 65, 22 68 C 28 70, 35 65, 42 55 C 48 48, 52 46, 60 46 C 68 46, 72 48, 78 55 C 85 65, 92 70, 98 68 C 108 65, 115 50, 112 35 C 110 20, 100 15, 95 15 C 90 15, 85 17, 60 17 C 35 17, 30 15, 25 15 Z"
                  fill="#241F1D"
                  stroke="#FF8A5B"
                  strokeWidth="2"
                />
                <circle cx="34" cy="30" r="6" fill="#FF8A5B" opacity="0.8" />
                <circle cx="70" cy="42" r="6" fill="#FF8A5B" opacity="0.8" />
                <circle cx="86" cy="30" r="6" fill="#FF8A5B" opacity="0.8" />
                <rect x="42" y="38" width="10" height="10" rx="2" fill="#FF8A5B" opacity="0.8" />
              </svg>

              {/* Scanning Sweep Line */}
              {scanning && (
                <div className="absolute inset-y-0 w-1 bg-gradient-to-b from-transparent via-[#FF8A5B] to-transparent animate-[pulse_1s_infinite]" />
              )}
            </div>

            <div className="mt-2 text-[11px] font-mono font-medium text-[#A79C92]">
              {scanning ? (
                <span className="flex items-center gap-1.5 text-[#FF8A5B]">
                  <RefreshCw className="w-3 h-3 animate-spin" />
                  Scanning Ports ({scanProgress}%)...
                </span>
              ) : hardwareDetected ? (
                <span className="flex items-center gap-1.5 text-[#86C08A]">
                  <CheckCircle2 className="w-3.5 h-3.5" />
                  Hardware Gamepad Detected
                </span>
              ) : (
                <span className="flex items-center gap-1.5 text-[#D6CEC6]">
                  <CheckCircle2 className="w-3.5 h-3.5 text-[#86C08A]" />
                  X20 PRO Ready To Configure
                </span>
              )}
            </div>
          </div>
        </div>

        {/* Connection Type Selection Tabs */}
        <div className="w-full space-y-2">
          <span className="text-[11px] uppercase tracking-wider text-[#A79C92] font-semibold block">
            Select Active Connection Link
          </span>
          <div className="grid grid-cols-3 gap-2.5">
            <button
              onClick={() => setSelectedMode('dongle')}
              className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition-all ${
                selectedMode === 'dongle'
                  ? 'bg-[#241F1D] border-[#FF8A5B] text-[#F4F0EB] shadow-[0_0_12px_rgba(255,138,91,0.2)]'
                  : 'bg-[#1B1817] border-[#332C29] text-[#A79C92] hover:text-[#D6CEC6]'
              }`}
            >
              <Radio className="w-5 h-5 text-[#FF8A5B]" />
              <span className="text-xs font-bold">2.4G Dongle</span>
              <span className="text-[9px] text-[#A79C92]">Wireless USB</span>
            </button>

            <button
              onClick={() => setSelectedMode('wired')}
              className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition-all ${
                selectedMode === 'wired'
                  ? 'bg-[#241F1D] border-[#FF8A5B] text-[#F4F0EB] shadow-[0_0_12px_rgba(255,138,91,0.2)]'
                  : 'bg-[#1B1817] border-[#332C29] text-[#A79C92] hover:text-[#D6CEC6]'
              }`}
            >
              <Usb className="w-5 h-5 text-[#86C08A]" />
              <span className="text-xs font-bold">Wired USB-C</span>
              <span className="text-[9px] text-[#A79C92]">Zero-Latency Cable</span>
            </button>

            <button
              onClick={() => setSelectedMode('bluetooth')}
              className={`p-3 rounded-xl border flex flex-col items-center gap-1.5 transition-all ${
                selectedMode === 'bluetooth'
                  ? 'bg-[#241F1D] border-[#FF8A5B] text-[#F4F0EB] shadow-[0_0_12px_rgba(255,138,91,0.2)]'
                  : 'bg-[#1B1817] border-[#332C29] text-[#A79C92] hover:text-[#D6CEC6]'
              }`}
            >
              <Wifi className="w-5 h-5 text-[#6BA0FA]" />
              <span className="text-xs font-bold">Bluetooth</span>
              <span className="text-[9px] text-[#A79C92]">Wireless BLE</span>
            </button>
          </div>
        </div>

        {/* Enter Configurator CTA */}
        <div className="w-full flex flex-col sm:flex-row items-center gap-3 pt-2">
          <button
            onClick={handleRescan}
            className="w-full sm:w-auto px-4 py-3 rounded-xl bg-[#1B1817] hover:bg-[#241F1D] border border-[#332C29] text-[#D6CEC6] text-xs font-semibold flex items-center justify-center gap-2 transition-colors"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${scanning ? 'animate-spin' : ''}`} />
            <span>Re-Scan Devices</span>
          </button>

          {onSkip && (
            <button
              onClick={onSkip}
              className="w-full sm:w-auto px-4 py-3 rounded-xl bg-[#1B1817] hover:bg-[#241F1D] border border-[#332C29] text-[#D6CEC6] text-xs font-semibold flex items-center justify-center gap-2 transition-colors"
            >
              <span>Skip Scanner</span>
            </button>
          )}

          <button
            onClick={() => {
              const name = controllerName || 'EasySMX X20 PRO Gamepad';
              if (onConnect) onConnect(name, selectedMode);
              if (onEnterApp) onEnterApp(selectedMode);
              if (!onConnect && !onEnterApp && onSkip) onSkip();
            }}
            className="w-full flex-1 py-3 px-6 rounded-xl bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] font-bold text-sm flex items-center justify-center gap-2 shadow-lg shadow-[#FF8A5B]/25 transition-all duration-200 transform hover:scale-[1.01]"
          >
            <span>Launch EasySMX Suite</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>

        {/* Footnote */}
        <p className="text-[11px] text-[#A79C92]">
          Compatible with EasySMX X20, X20 PRO, KeyLinker & all upcoming EasySMX hardware.
        </p>
      </div>
    </div>
  );
};
