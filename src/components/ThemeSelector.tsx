import React from 'react';
import { THEMES, ThemeDefinition } from '../types/theme';
import { Palette, Check, X } from 'lucide-react';

interface ThemeSelectorProps {
  isOpen: boolean;
  onClose: () => void;
  currentThemeId: string;
  onSelectTheme: (themeId: string) => void;
}

export const ThemeSelector: React.FC<ThemeSelectorProps> = ({
  isOpen,
  onClose,
  currentThemeId,
  onSelectTheme,
}) => {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm animate-fade-in select-none">
      <div className="w-full max-w-2xl bg-[#1B1817] border border-[#332C29] rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
        {/* Header */}
        <div className="p-4 border-b border-[#332C29] flex items-center justify-between bg-[#171413]">
          <div className="flex items-center gap-3">
            <div className="p-2 rounded-lg bg-[#FF8A5B]/15 border border-[#FF8A5B]/30 text-[#FF8A5B]">
              <Palette className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-[#F4F0EB]">Change Theme</h3>
              <p className="text-xs text-[#A79C92] mt-0.5">
                Select your preferred color aesthetic. Handcrafted with warm, natural neutrals and zero cyberpunk cliché.
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

        {/* Theme List Grid */}
        <div className="p-5 overflow-y-auto grid grid-cols-1 sm:grid-cols-2 gap-3">
          {THEMES.map((theme: ThemeDefinition) => {
            const isSelected = theme.id === currentThemeId;
            return (
              <div
                key={theme.id}
                onClick={() => onSelectTheme(theme.id)}
                className={`p-3.5 rounded-xl border cursor-pointer transition-all ${
                  isSelected
                    ? 'bg-[#241F1D] border-[#FF8A5B] shadow-[0_0_12px_rgba(255,138,91,0.25)]'
                    : 'bg-[#141211] border-[#332C29] hover:border-[#4A3F3B] hover:bg-[#1E1A18]'
                }`}
              >
                <div className="flex items-center justify-between mb-2">
                  <span className="text-sm font-bold text-[#F4F0EB] flex items-center gap-2">
                    {theme.name}
                    {isSelected && (
                      <span className="p-0.5 rounded-full bg-[#FF8A5B] text-[#131110]">
                        <Check className="w-3 h-3 stroke-[3]" />
                      </span>
                    )}
                  </span>
                </div>

                <p className="text-[11px] text-[#A79C92] mb-3 leading-snug">
                  {theme.tagline}
                </p>

                {/* Color Swatch Preview Bar */}
                <div className="flex items-center gap-1.5 p-1.5 rounded-lg bg-[#141211] border border-[#2A2421]">
                  <div
                    className="w-5 h-5 rounded-md border border-white/10"
                    style={{ backgroundColor: theme.colors.bg }}
                    title="Background"
                  />
                  <div
                    className="w-5 h-5 rounded-md border border-white/10"
                    style={{ backgroundColor: theme.colors.cardBg }}
                    title="Surface Card"
                  />
                  <div
                    className="w-5 h-5 rounded-md border border-white/10"
                    style={{ backgroundColor: theme.colors.accent }}
                    title="Accent Color"
                  />
                  <div
                    className="w-5 h-5 rounded-md border border-white/10"
                    style={{ backgroundColor: theme.colors.diagramShell }}
                    title="Controller Shell"
                  />
                  <div
                    className="w-5 h-5 rounded-md border border-white/10"
                    style={{ backgroundColor: theme.colors.success }}
                    title="Success Indicator"
                  />
                </div>
              </div>
            );
          })}
        </div>

        {/* Footer */}
        <div className="p-4 border-t border-[#332C29] bg-[#171413] flex justify-end">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-bold rounded-lg bg-[#FF8A5B] hover:bg-[#E77445] text-[#131110] transition-colors"
          >
            Done
          </button>
        </div>
      </div>
    </div>
  );
};
