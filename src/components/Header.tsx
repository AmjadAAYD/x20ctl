import React, { useState } from 'react';
import { ControllerSlot, Profile, ConnectionType } from '../types/gamepad';
import { BatteryIndicator } from './BatteryIndicator';
import {
  Gamepad2,
  Save,
  Check,
  Palette,
  BookOpen,
  Edit2,
  CheckCheck,
  Radio,
  Usb,
} from 'lucide-react';

interface HeaderProps {
  currentTab: 'settings' | 'curves' | 'tester';
  setCurrentTab: (tab: 'settings' | 'curves' | 'tester') => void;
  activeController: ControllerSlot;
  activeProfile: Profile;
  isModified: boolean;
  onApply: () => void;
  appliedToast: boolean;
  onOpenTutorial: () => void;
  onOpenTheme: () => void;
  onUpdateControllerName: (newName: string) => void;
  onChangeConnectionMode: (mode: ConnectionType) => void;
}

export const Header: React.FC<HeaderProps> = ({
  currentTab,
  setCurrentTab,
  activeController,
  isModified,
  onApply,
  appliedToast,
  onOpenTutorial,
  onOpenTheme,
  onUpdateControllerName,
  onChangeConnectionMode,
}) => {
  const [isEditingName, setIsEditingName] = useState(false);
  const [nameInput, setNameInput] = useState(activeController.customName || '');
  const [showModeMenu, setShowModeMenu] = useState(false);

  const handleSaveName = () => {
    const trimmed = nameInput.trim();
    onUpdateControllerName(trimmed);
    setIsEditingName(false);
  };

  const displayName = activeController.customName || activeController.name || '';

  return (
    <header className="h-16 border-b border-[#332C29] bg-[#1B1817] px-5 flex items-center justify-between z-20 shrink-0 select-none">
      {/* Left: Dynamic Controller Name & Status */}
      <div className="flex items-center gap-4">
        <div className="flex items-center gap-3">
          <div
            className={`w-2.5 h-2.5 rounded-full ${
              activeController.connected
                ? 'bg-[#86C08A] shadow-[0_0_8px_rgba(134,192,138,0.6)]'
                : 'bg-[#A79C92]/40'
            }`}
          />

          {/* Editable Controller Name */}
          <div className="flex flex-col justify-center">
            {isEditingName ? (
              <div className="flex items-center gap-1.5">
                <input
                  type="text"
                  value={nameInput}
                  onChange={(e) => setNameInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleSaveName();
                    if (e.key === 'Escape') setIsEditingName(false);
                  }}
                  autoFocus
                  placeholder="Enter controller name..."
                  className="bg-[#141211] border border-[#FF8A5B] text-xs font-semibold text-[#F4F0EB] px-2 py-0.5 rounded outline-none w-48"
                />
                <button
                  onClick={handleSaveName}
                  className="p-1 rounded bg-[#FF8A5B] text-[#131110] hover:bg-[#E77445]"
                  title="Save Name"
                >
                  <CheckCheck className="w-3.5 h-3.5" />
                </button>
              </div>
            ) : (
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    setNameInput(displayName === 'No Gamepad Connected' ? '' : displayName);
                    setIsEditingName(true);
                  }}
                  className="group flex items-center gap-1.5 text-sm font-bold text-[#F4F0EB] hover:text-[#FF8A5B] transition-colors"
                  title="Click to rename controller"
                >
                  <span>{displayName || 'Add Name for Controller'}</span>
                  <Edit2 className="w-3 h-3 text-[#A79C92] group-hover:text-[#FF8A5B] transition-colors" />
                </button>

                {/* Connection mode picker dropdown pill */}
                <div className="relative">
                  <button
                    onClick={() => setShowModeMenu(!showModeMenu)}
                    className="flex items-center gap-1 text-[10px] px-2 py-0.5 rounded bg-[#241F1D] text-[#D6CEC6] font-mono border border-[#332C29] hover:border-[#FF8A5B] transition-colors"
                    title="Click to switch connection mode (Dongle or Wired)"
                  >
                    {activeController.connectionMode === 'wired' ? (
                      <>
                        <Usb className="w-3 h-3 text-[#86C08A]" />
                        <span>Wired USB</span>
                      </>
                    ) : (
                      <>
                        <Radio className="w-3 h-3 text-[#FF8A5B]" />
                        <span>2.4G Dongle</span>
                      </>
                    )}
                  </button>

                  {showModeMenu && (
                    <div className="absolute left-0 mt-1 w-36 bg-[#1B1817] border border-[#332C29] rounded-lg shadow-xl py-1 z-30 text-xs">
                      <button
                        onClick={() => {
                          onChangeConnectionMode('dongle');
                          setShowModeMenu(false);
                        }}
                        className="w-full text-left px-3 py-1.5 hover:bg-[#241F1D] flex items-center gap-2 text-[#D6CEC6]"
                      >
                        <Radio className="w-3.5 h-3.5 text-[#FF8A5B]" />
                        <span>2.4G Dongle</span>
                      </button>
                      <button
                        onClick={() => {
                          onChangeConnectionMode('wired');
                          setShowModeMenu(false);
                        }}
                        className="w-full text-left px-3 py-1.5 hover:bg-[#241F1D] flex items-center gap-2 text-[#D6CEC6]"
                      >
                        <Usb className="w-3.5 h-3.5 text-[#86C08A]" />
                        <span>Wired USB-C</span>
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* 4-Level Hardware Battery Segment Indicator */}
        <BatteryIndicator
          level={activeController.batteryLevel}
          isCharging={activeController.isCharging}
        />
      </div>

      {/* Center: Navigation Tabs */}
      <div className="flex items-center gap-2">
        <div className="flex items-center p-1 rounded-lg bg-[#131110] border border-[#332C29]">
          <button
            id="nav-settings-btn"
            onClick={() => setCurrentTab('settings')}
            className={`px-3 py-1.5 rounded-md text-xs font-semibold transition-all ${
              currentTab === 'settings'
                ? 'bg-[#241F1D] text-[#FF8A5B] shadow-sm'
                : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
            }`}
          >
            Settings
          </button>
          <button
            id="nav-curves-btn"
            onClick={() => setCurrentTab('curves')}
            className={`px-3 py-1.5 rounded-md text-xs font-semibold transition-all ${
              currentTab === 'curves'
                ? 'bg-[#241F1D] text-[#FF8A5B] shadow-sm'
                : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
            }`}
          >
            Sticks & Triggers
          </button>
          <button
            id="nav-tester-btn"
            onClick={() => setCurrentTab('tester')}
            className={`px-3 py-1.5 rounded-md text-xs font-semibold transition-all flex items-center gap-1.5 ${
              currentTab === 'tester'
                ? 'bg-[#241F1D] text-[#FF8A5B] shadow-sm'
                : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
            }`}
          >
            <Gamepad2 className="w-3.5 h-3.5" />
            Input Tester
          </button>
        </div>

        {/* Action Buttons: Tutorial, Theme, Save */}
        <div className="flex items-center gap-2 ml-2">
          {/* Tutorial Button */}
          <button
            onClick={onOpenTutorial}
            id="header-tutorial-btn"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] hover:text-[#F4F0EB] border border-[#332C29] transition-colors"
            title="Open Configurator Tutorial & Guide"
          >
            <BookOpen className="w-3.5 h-3.5 text-[#FF8A5B]" />
            <span className="hidden sm:inline">Tutorial</span>
          </button>

          {/* Change Theme Button */}
          <button
            onClick={onOpenTheme}
            id="header-theme-btn"
            className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium rounded-md bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] hover:text-[#F4F0EB] border border-[#332C29] transition-colors"
            title="Change Theme & Aesthetics"
          >
            <Palette className="w-3.5 h-3.5 text-[#FF8A5B]" />
            <span className="hidden sm:inline">Theme</span>
          </button>

          {/* Write / Save Button */}
          <button
            id="header-apply-btn"
            onClick={onApply}
            className={`flex items-center gap-1.5 px-3.5 py-1.5 text-xs font-bold rounded-md transition-all ${
              appliedToast
                ? 'bg-[#86C08A] text-[#131110]'
                : isModified
                ? 'bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] shadow-[0_0_12px_rgba(255,138,91,0.25)]'
                : 'bg-[#241F1D] hover:bg-[#2C2624] text-[#D6CEC6] border border-[#332C29]'
            }`}
          >
            {appliedToast ? (
              <>
                <Check className="w-3.5 h-3.5" />
                <span>Saved!</span>
              </>
            ) : (
              <>
                <Save className="w-3.5 h-3.5" />
                <span>{isModified ? 'Apply Changes' : 'Write to Pad'}</span>
              </>
            )}
          </button>
        </div>
      </div>
    </header>
  );
};
