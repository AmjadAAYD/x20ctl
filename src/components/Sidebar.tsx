import React, { useState } from 'react';
import { Profile, ControllerSlot } from '../types/gamepad';
import { Plus, Copy, Trash2, Edit3, Gamepad2, Info, Radio, Usb, Wifi, Scan } from 'lucide-react';
import { BatteryIndicator } from './BatteryIndicator';

interface SidebarProps {
  profiles: Profile[];
  activeProfileId: string;
  onSelectProfile: (id: string) => void;
  onCreateProfile: () => void;
  onDuplicateProfile: (id: string) => void;
  onDeleteProfile: (id: string) => void;
  onRenameProfile: (id: string, newName: string) => void;
  activeController: ControllerSlot;
  onOpenScanner: () => void;
}

export const Sidebar: React.FC<SidebarProps> = ({
  profiles,
  activeProfileId,
  onSelectProfile,
  onCreateProfile,
  onDuplicateProfile,
  onDeleteProfile,
  onRenameProfile,
  activeController,
  onOpenScanner,
}) => {
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState<string>('');

  const startRename = (profile: Profile, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingId(profile.id);
    setEditName(profile.name);
  };

  const handleSaveRename = (id: string) => {
    if (editName.trim()) {
      onRenameProfile(id, editName.trim());
    }
    setEditingId(null);
  };

  return (
    <aside className="w-64 border-r border-[#332C29] bg-[#1B1817] flex flex-col justify-between shrink-0 select-none">
      {/* Top: Brand and Profiles */}
      <div className="flex-1 overflow-y-auto p-4 space-y-6">
        {/* Brand */}
        <div className="px-1">
          <div className="flex items-center gap-2">
            <h1 className="text-xl font-black tracking-wider uppercase text-[#F4F0EB]">
              EasySMX
            </h1>
            <span className="text-[10px] uppercase font-mono px-1.5 py-0.5 rounded bg-[#241F1D] text-[#FF8A5B] border border-[#332C29] font-bold">
              Suite
            </span>
          </div>
          <p className="text-xs text-[#A79C92] mt-0.5">
            Gamepad configuration & tuning
          </p>
        </div>

        {/* Controller Roster - ONLY SHOWS CURRENT ACTIVE ONE */}
        <div>
          <div className="flex items-center justify-between mb-2 px-1">
            <span className="text-[10px] uppercase font-bold tracking-wider text-[#A79C92]">
              Active Controller
            </span>
            <button
              onClick={onOpenScanner}
              title="Open Controller Scanner"
              className="text-[11px] text-[#FF8A5B] hover:underline flex items-center gap-1 font-medium"
            >
              <Scan className="w-3 h-3" />
              <span>Scan</span>
            </button>
          </div>

          {/* Single Active Controller Card */}
          <div className="p-3.5 rounded-xl bg-[#141211] border border-[#332C29] shadow-sm space-y-2.5">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <div
                  className={`w-2 h-2 rounded-full ${
                    activeController.connected
                      ? 'bg-[#86C08A] shadow-[0_0_6px_rgba(134,192,138,0.7)]'
                      : 'bg-[#A79C92]/40'
                  }`}
                />
                <span className="text-xs font-bold text-[#F4F0EB] truncate max-w-[130px]">
                  {activeController.customName || activeController.name}
                </span>
              </div>
              <span className="text-[10px] font-mono text-[#A79C92] bg-[#241F1D] px-1.5 py-0.5 rounded border border-[#332C29]">
                P1
              </span>
            </div>

            {/* Connection Link Badge */}
            <div className="flex items-center justify-between text-[11px] text-[#A79C92]">
              <span className="flex items-center gap-1">
                {activeController.connectionMode === 'dongle' && (
                  <Radio className="w-3 h-3 text-[#FF8A5B]" />
                )}
                {activeController.connectionMode === 'wired' && (
                  <Usb className="w-3 h-3 text-[#86C08A]" />
                )}
                {activeController.connectionMode === 'bluetooth' && (
                  <Wifi className="w-3 h-3 text-[#6BA0FA]" />
                )}
                <span className="capitalize">{activeController.connectionMode}</span>
              </span>
              <span className="text-[10px] text-[#A79C92] font-mono">
                {activeController.firmwareVersion}
              </span>
            </div>

            {/* 4-level Battery Indicator */}
            <div className="pt-1 border-t border-[#241F1D]">
              <BatteryIndicator
                level={activeController.batteryLevel}
                isCharging={activeController.isCharging}
                showText={true}
                className="w-full justify-between bg-transparent border-0 px-0 py-0"
              />
            </div>
          </div>
        </div>

        {/* Save Files / Profiles (WITHOUT Download / Upload buttons) */}
        <div>
          <div className="flex items-center justify-between mb-2 px-1">
            <span className="text-[10px] uppercase font-bold tracking-wider text-[#A79C92]">
              Saved Profiles
            </span>
            <button
              onClick={onCreateProfile}
              title="Create New Profile"
              className="p-1 rounded hover:bg-[#241F1D] text-[#FF8A5B] transition-colors"
            >
              <Plus className="w-3.5 h-3.5" />
            </button>
          </div>

          <div className="space-y-1">
            {profiles.map((profile) => {
              const isActive = profile.id === activeProfileId;
              const isEditing = editingId === profile.id;

              return (
                <div
                  key={profile.id}
                  onClick={() => onSelectProfile(profile.id)}
                  className={`group relative flex items-center justify-between px-3 py-2 rounded-lg cursor-pointer transition-colors text-xs border ${
                    isActive
                      ? 'bg-[#241F1D] text-[#F4F0EB] border-[#FF8A5B]/50 font-semibold'
                      : 'bg-transparent hover:bg-[#141211] text-[#D6CEC6] border-transparent'
                  }`}
                >
                  {isEditing ? (
                    <input
                      type="text"
                      value={editName}
                      autoFocus
                      onChange={(e) => setEditName(e.target.value)}
                      onBlur={() => handleSaveRename(profile.id)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') handleSaveRename(profile.id);
                        if (e.key === 'Escape') setEditingId(null);
                      }}
                      className="bg-[#131110] border border-[#FF8A5B] rounded px-1.5 py-0.5 text-xs text-[#F4F0EB] outline-none w-32"
                    />
                  ) : (
                    <span className="truncate max-w-[140px]">{profile.name}</span>
                  )}

                  <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button
                      onClick={(e) => startRename(profile, e)}
                      title="Rename"
                      className="p-1 hover:text-[#FF8A5B] text-[#A79C92]"
                    >
                      <Edit3 className="w-3 h-3" />
                    </button>
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        onDuplicateProfile(profile.id);
                      }}
                      title="Duplicate"
                      className="p-1 hover:text-[#FF8A5B] text-[#A79C92]"
                    >
                      <Copy className="w-3 h-3" />
                    </button>
                    {profiles.length > 1 && (
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onDeleteProfile(profile.id);
                        }}
                        title="Delete"
                        className="p-1 hover:text-[#E5645E] text-[#A79C92]"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      </div>

      {/* Bottom Tip Panel */}
      <div className="p-3.5 m-3 rounded-xl bg-[#141211] border border-[#332C29] text-[11px] text-[#A79C92] leading-relaxed">
        <div className="flex items-center gap-1.5 text-[#D6CEC6] font-semibold mb-1">
          <Info className="w-3.5 h-3.5 text-[#FF8A5B]" />
          <span>Hardware Sync</span>
        </div>
        <p>
          Profiles store in controller memory. Hold <span className="text-[#FF8A5B] font-mono font-bold">M</span> button on the controller for 3s to quick-cycle profiles during gameplay.
        </p>
      </div>
    </aside>
  );
};
