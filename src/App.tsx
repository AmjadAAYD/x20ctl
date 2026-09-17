import React, { useState, useEffect } from 'react';
import { Header } from './components/Header';
import { Sidebar } from './components/Sidebar';
import { ButtonsPage } from './components/pages/ButtonsPage';
import { MacrosPage } from './components/pages/MacrosPage';
import { CurvesPage } from './components/pages/CurvesPage';
import { RumblePage } from './components/pages/RumblePage';
import { PowerPage } from './components/pages/PowerPage';
import { TesterPage } from './components/pages/TesterPage';
import { IntroScanner } from './components/IntroScanner';
import { TutorialModal } from './components/TutorialModal';
import { ThemeSelector } from './components/ThemeSelector';
import { useGamepad } from './hooks/useGamepad';
import { THEMES } from './types/theme';
import {
  Profile,
  ControllerSlot,
  ConnectionType,
} from './types/gamepad';
import {
  loadProfiles,
  saveProfiles,
  getActiveProfileId,
  setActiveProfileId,
  DEFAULT_KEY_REMAPS,
  DEFAULT_CURVE_CONFIG,
} from './data/defaultProfiles';
import { Settings, Sliders, Volume2, Power } from 'lucide-react';

export function App() {
  const [profiles, setProfiles] = useState<Profile[]>(loadProfiles);
  const [activeProfileId, setActiveProfileIdState] = useState<string>(getActiveProfileId);
  const [currentTab, setCurrentTab] = useState<'settings' | 'curves' | 'tester'>('settings');
  const [settingsSubTab, setSettingsSubTab] = useState<'buttons' | 'macros' | 'rumble' | 'power'>('buttons');
  const [isModified, setIsModified] = useState<boolean>(false);
  const [appliedToast, setAppliedToast] = useState<boolean>(false);

  // Modals state
  const [isTutorialOpen, setIsTutorialOpen] = useState<boolean>(false);
  const [isThemeOpen, setIsThemeOpen] = useState<boolean>(false);
  const [currentThemeId, setCurrentThemeId] = useState<string>('matte-obsidian');
  const [showScanner, setShowScanner] = useState<boolean>(false);

  // Active Controller state (EasySMX X20 Pro)
  const [activeController, setActiveController] = useState<ControllerSlot>({
    slot: 0,
    connected: true,
    name: 'EasySMX X20 PRO Gamepad',
    customName: 'EasySMX X20 PRO',
    batteryLevel: 75, // 3/4 bars
    isCharging: false,
    firmwareVersion: 'v1.4.2',
    hardwareType: 'EasySMX X20 PRO',
    connectionMode: 'dongle',
    activeProfileId: profiles[0]?.id || 'default-stock',
  });

  const activeProfile = profiles.find((p) => p.id === activeProfileId) || profiles[0];

  const {
    liveState,
    hardwareDetected,
    controllerName,
    triggerHaptic,
    setSimulatedInput,
    setSimulatedStick,
    setSimulatedTrigger,
  } = useGamepad(true);

  // Hardware detection sync
  useEffect(() => {
    if (hardwareDetected && controllerName) {
      setActiveController((prev) => ({
        ...prev,
        connected: true,
        name: controllerName,
        customName: prev.customName || controllerName,
      }));
    }
  }, [hardwareDetected, controllerName]);

  // Profile management
  const handleSelectProfile = (id: string) => {
    setActiveProfileIdState(id);
    setActiveProfileId(id);
    setIsModified(false);
  };

  const handleUpdateActiveProfile = (updater: (prev: Profile) => Profile) => {
    setProfiles((prev) => {
      const next = prev.map((p) => (p.id === activeProfile.id ? updater(p) : p));
      saveProfiles(next);
      return next;
    });
    setIsModified(true);
  };

  const handleCreateProfile = () => {
    const newId = 'profile-' + Math.random().toString(36).substring(2, 9);
    const newProfile: Profile = {
      id: newId,
      name: `Custom Preset ${profiles.length + 1}`,
      createdAt: Date.now(),
      remaps: { ...DEFAULT_KEY_REMAPS },
      macros: { M1: [], M2: [], M3: [], M4: [] },
      vibration: 70,
      idleTimeoutMinutes: 10,
      stickCurves: {
        left: { ...DEFAULT_CURVE_CONFIG },
        right: { ...DEFAULT_CURVE_CONFIG },
      },
      triggerCurves: {
        left: { ...DEFAULT_CURVE_CONFIG },
        right: { ...DEFAULT_CURVE_CONFIG },
      },
    };
    const next = [...profiles, newProfile];
    setProfiles(next);
    saveProfiles(next);
    handleSelectProfile(newId);
  };

  const handleDuplicateProfile = (id: string) => {
    const source = profiles.find((p) => p.id === id);
    if (!source) return;
    const newId = 'profile-' + Math.random().toString(36).substring(2, 9);
    const duplicated: Profile = {
      ...JSON.parse(JSON.stringify(source)),
      id: newId,
      name: `${source.name} (Copy)`,
      createdAt: Date.now(),
    };
    const next = [...profiles, duplicated];
    setProfiles(next);
    saveProfiles(next);
    handleSelectProfile(newId);
  };

  const handleDeleteProfile = (id: string) => {
    if (profiles.length <= 1) return;
    const next = profiles.filter((p) => p.id !== id);
    setProfiles(next);
    saveProfiles(next);
    if (activeProfileId === id) {
      handleSelectProfile(next[0].id);
    }
  };

  const handleRenameProfile = (id: string, newName: string) => {
    setProfiles((prev) => {
      const next = prev.map((p) => (p.id === id ? { ...p, name: newName } : p));
      saveProfiles(next);
      return next;
    });
  };

  const handleApplyToPad = () => {
    setIsModified(false);
    setAppliedToast(true);
    triggerHaptic(200, 0.5, 0.3);
    setTimeout(() => {
      setAppliedToast(false);
    }, 2500);
  };

  const handleUpdateControllerName = (newName: string) => {
    setActiveController((prev) => ({
      ...prev,
      customName: newName || prev.name,
    }));
  };

  const handleChangeConnectionMode = (mode: ConnectionType) => {
    setActiveController((prev) => ({
      ...prev,
      connectionMode: mode,
    }));
  };

  // Get active theme colors
  const activeTheme = THEMES.find((t) => t.id === currentThemeId) || THEMES[1];

  // If user opened the scanner view
  if (showScanner) {
    return (
      <IntroScanner
        onConnect={(name, mode) => {
          setActiveController((prev) => ({
            ...prev,
            connected: true,
            name,
            customName: name,
            connectionMode: mode,
          }));
          setShowScanner(false);
        }}
        onSkip={() => setShowScanner(false)}
      />
    );
  }

  return (
    <div
      className="flex h-screen w-screen overflow-hidden text-[#F4F0EB] select-none"
      style={{
        backgroundColor: activeTheme.colors.bg,
        color: activeTheme.colors.textPrimary,
      }}
    >
      {/* Left Sidebar (Only shows current active controller in roster, no download/upload buttons) */}
      <Sidebar
        profiles={profiles}
        activeProfileId={activeProfileId}
        onSelectProfile={handleSelectProfile}
        onCreateProfile={handleCreateProfile}
        onDuplicateProfile={handleDuplicateProfile}
        onDeleteProfile={handleDeleteProfile}
        onRenameProfile={handleRenameProfile}
        activeController={activeController}
        onOpenScanner={() => setShowScanner(true)}
      />

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Top Header */}
        <Header
          currentTab={currentTab}
          setCurrentTab={setCurrentTab}
          activeController={activeController}
          activeProfile={activeProfile}
          isModified={isModified}
          onApply={handleApplyToPad}
          appliedToast={appliedToast}
          onOpenTutorial={() => setIsTutorialOpen(true)}
          onOpenTheme={() => setIsThemeOpen(true)}
          onUpdateControllerName={handleUpdateControllerName}
          onChangeConnectionMode={handleChangeConnectionMode}
        />

        {/* Workspace Body */}
        <main className="flex-1 overflow-y-auto p-6 md:p-8">
          {currentTab === 'settings' && (
            <div className="space-y-6 max-w-5xl mx-auto">
              {/* Settings Subtabs bar */}
              <div className="flex items-center gap-2 border-b border-[#332C29] pb-3">
                <button
                  onClick={() => setSettingsSubTab('buttons')}
                  className={`flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg transition-all ${
                    settingsSubTab === 'buttons'
                      ? 'bg-[#241F1D] text-[#FF8A5B] font-bold border border-[#FF8A5B]/30'
                      : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
                  }`}
                >
                  <Settings className="w-3.5 h-3.5" />
                  <span>Button Remapping</span>
                </button>

                <button
                  onClick={() => setSettingsSubTab('macros')}
                  className={`flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg transition-all ${
                    settingsSubTab === 'macros'
                      ? 'bg-[#241F1D] text-[#FF8A5B] font-bold border border-[#FF8A5B]/30'
                      : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
                  }`}
                >
                  <Sliders className="w-3.5 h-3.5" />
                  <span>Rear Paddle Macros</span>
                </button>

                <button
                  onClick={() => setSettingsSubTab('rumble')}
                  className={`flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg transition-all ${
                    settingsSubTab === 'rumble'
                      ? 'bg-[#241F1D] text-[#FF8A5B] font-bold border border-[#FF8A5B]/30'
                      : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
                  }`}
                >
                  <Volume2 className="w-3.5 h-3.5" />
                  <span>Vibration & Haptics</span>
                </button>

                <button
                  onClick={() => setSettingsSubTab('power')}
                  className={`flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg transition-all ${
                    settingsSubTab === 'power'
                      ? 'bg-[#241F1D] text-[#FF8A5B] font-bold border border-[#FF8A5B]/30'
                      : 'text-[#D6CEC6] hover:text-[#F4F0EB]'
                  }`}
                >
                  <Power className="w-3.5 h-3.5" />
                  <span>Power & Safety</span>
                </button>
              </div>

              {/* Subtab content */}
              {settingsSubTab === 'buttons' && (
                <ButtonsPage
                  remaps={activeProfile.remaps}
                  onUpdateRemap={(source, target) => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      remaps: { ...p.remaps, [source]: target },
                    }));
                  }}
                  onResetRemaps={() => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      remaps: { ...DEFAULT_KEY_REMAPS },
                    }));
                  }}
                  liveState={liveState}
                />
              )}

              {settingsSubTab === 'macros' && (
                <MacrosPage
                  macros={activeProfile.macros}
                  onUpdateMacros={(paddle, steps) => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      macros: { ...p.macros, [paddle]: steps },
                    }));
                  }}
                  onClearMacro={(paddle) => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      macros: { ...p.macros, [paddle]: [] },
                    }));
                  }}
                />
              )}

              {settingsSubTab === 'rumble' && (
                <RumblePage
                  vibration={activeProfile.vibration}
                  onUpdateVibration={(val) => {
                    handleUpdateActiveProfile((p) => ({ ...p, vibration: val }));
                  }}
                  onTriggerHaptic={triggerHaptic}
                />
              )}

              {settingsSubTab === 'power' && (
                <PowerPage
                  idleTimeout={activeProfile.idleTimeoutMinutes}
                  onUpdateIdleTimeout={(mins) => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      idleTimeoutMinutes: mins,
                    }));
                  }}
                  onFactoryReset={() => {
                    handleUpdateActiveProfile((p) => ({
                      ...p,
                      remaps: { ...DEFAULT_KEY_REMAPS },
                      macros: { M1: [], M2: [], M3: [], M4: [] },
                      vibration: 70,
                      idleTimeoutMinutes: 10,
                      stickCurves: {
                        left: { ...DEFAULT_CURVE_CONFIG },
                        right: { ...DEFAULT_CURVE_CONFIG },
                      },
                      triggerCurves: {
                        left: { ...DEFAULT_CURVE_CONFIG },
                        right: { ...DEFAULT_CURVE_CONFIG },
                      },
                    }));
                  }}
                  firmwareVersion={activeController.firmwareVersion}
                />
              )}
            </div>
          )}

          {currentTab === 'curves' && (
            <CurvesPage
              stickCurves={activeProfile.stickCurves}
              triggerCurves={activeProfile.triggerCurves}
              onUpdateStickCurve={(which, cfg) => {
                handleUpdateActiveProfile((p) => ({
                  ...p,
                  stickCurves: { ...p.stickCurves, [which]: cfg },
                }));
              }}
              onUpdateTriggerCurve={(which, cfg) => {
                handleUpdateActiveProfile((p) => ({
                  ...p,
                  triggerCurves: { ...p.triggerCurves, [which]: cfg },
                }));
              }}
              liveState={liveState}
              onSimulateStickMove={(which, x, y) => setSimulatedStick(which, x, y)}
              onSimulateTriggerPull={(which, val) => setSimulatedTrigger(which, val)}
            />
          )}

          {currentTab === 'tester' && (
            <TesterPage
              liveState={liveState}
              onSimulateButton={setSimulatedInput}
              onSimulateStick={setSimulatedStick}
              onSimulateTrigger={setSimulatedTrigger}
            />
          )}
        </main>
      </div>

      {/* Tutorial Guide Modal */}
      <TutorialModal
        isOpen={isTutorialOpen}
        onClose={() => setIsTutorialOpen(false)}
      />

      {/* Theme Selector Modal */}
      <ThemeSelector
        isOpen={isThemeOpen}
        onClose={() => setIsThemeOpen(false)}
        currentThemeId={currentThemeId}
        onSelectTheme={(themeId) => setCurrentThemeId(themeId)}
      />
    </div>
  );
}

export default App;
