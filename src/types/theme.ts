export interface ThemeDefinition {
  id: string;
  name: string;
  tagline: string;
  colors: {
    bg: string;
    sidebarBg: string;
    cardBg: string;
    cardBorder: string;
    textPrimary: string;
    textSecondary: string;
    textMuted: string;
    accent: string;
    accentHover: string;
    accentLight: string;
    accentGlow: string;
    success: string;
    danger: string;
    diagramShell: string;
    diagramPlate: string;
    diagramAccent: string;
  };
}

export const THEMES: ThemeDefinition[] = [
  {
    id: 'x20-frost',
    name: 'X20 Frost & Gold',
    tagline: 'Matches the white X20 PRO shell with amber and ice-blue glow',
    colors: {
      bg: '#141416',
      sidebarBg: '#1B1C20',
      cardBg: '#212228',
      cardBorder: '#34363F',
      textPrimary: '#F7F7F8',
      textSecondary: '#D1D3DC',
      textMuted: '#8E92A4',
      accent: '#FFB020',
      accentHover: '#E59D17',
      accentLight: '#FFECC7',
      accentGlow: 'rgba(255, 176, 32, 0.35)',
      success: '#4ADE80',
      danger: '#F87171',
      diagramShell: '#EDEDF0',
      diagramPlate: '#D9DBE2',
      diagramAccent: '#FFB020',
    },
  },
  {
    id: 'matte-obsidian',
    name: 'EasySMX Obsidian',
    tagline: 'Deep warm charcoal with brushed graphite and amber accents',
    colors: {
      bg: '#131110',
      sidebarBg: '#1B1817',
      cardBg: '#241F1D',
      cardBorder: '#3D3430',
      textPrimary: '#F4F0EB',
      textSecondary: '#D6CEC6',
      textMuted: '#A79C92',
      accent: '#FF8A5B',
      accentHover: '#E77445',
      accentLight: '#FFE3D6',
      accentGlow: 'rgba(255, 138, 91, 0.35)',
      success: '#86C08A',
      danger: '#E5645E',
      diagramShell: '#F0ECE6',
      diagramPlate: '#DDD6CE',
      diagramAccent: '#FF8A5B',
    },
  },
  {
    id: 'nordic-titanium',
    name: 'Nordic Titanium',
    tagline: 'Clean minimal slate grey, off-white, and copper warmth',
    colors: {
      bg: '#16181B',
      sidebarBg: '#1D2126',
      cardBg: '#252A31',
      cardBorder: '#3A424E',
      textPrimary: '#F1F4F8',
      textSecondary: '#C5CBD5',
      textMuted: '#8893A3',
      accent: '#E07A5F',
      accentHover: '#C9684F',
      accentLight: '#FBE8E3',
      accentGlow: 'rgba(224, 122, 95, 0.35)',
      success: '#52B788',
      danger: '#EF476F',
      diagramShell: '#E9ECF0',
      diagramPlate: '#D5DAE2',
      diagramAccent: '#E07A5F',
    },
  },
  {
    id: 'sage-forest',
    name: 'Sage & Sandstone',
    tagline: 'Organic muted pine, sage greens, and champagne cream accents',
    colors: {
      bg: '#121715',
      sidebarBg: '#19201D',
      cardBg: '#212A26',
      cardBorder: '#35433D',
      textPrimary: '#F0F5F2',
      textSecondary: '#C8D5CD',
      textMuted: '#899E92',
      accent: '#68B684',
      accentHover: '#539E6D',
      accentLight: '#D9F2E2',
      accentGlow: 'rgba(104, 182, 132, 0.35)',
      success: '#68B684',
      danger: '#E06D63',
      diagramShell: '#EFF5F1',
      diagramPlate: '#D9E4DD',
      diagramAccent: '#68B684',
    },
  },
  {
    id: 'warm-espresso',
    name: 'Warm Espresso',
    tagline: 'Deep roasted mocha, dark cocoa, and toasted honey glow',
    colors: {
      bg: '#151211',
      sidebarBg: '#1E1917',
      cardBg: '#27221E',
      cardBorder: '#3F3630',
      textPrimary: '#F7F3EE',
      textSecondary: '#D9CEBF',
      textMuted: '#9E9081',
      accent: '#E09F5A',
      accentHover: '#C98744',
      accentLight: '#FBEBDB',
      accentGlow: 'rgba(224, 159, 90, 0.35)',
      success: '#7BB57C',
      danger: '#D9655D',
      diagramShell: '#F3EFEA',
      diagramPlate: '#DFD8CE',
      diagramAccent: '#E09F5A',
    },
  },
  {
    id: 'midnight-pearl',
    name: 'Midnight Pearl',
    tagline: 'Ultra-pure OLED black with brushed silver and pearl luminescence',
    colors: {
      bg: '#0D0E11',
      sidebarBg: '#14161B',
      cardBg: '#1B1E24',
      cardBorder: '#2C303A',
      textPrimary: '#FFFFFF',
      textSecondary: '#CED3DF',
      textMuted: '#7E8698',
      accent: '#60A5FA',
      accentHover: '#3B82F6',
      accentLight: '#DBEAFE',
      accentGlow: 'rgba(96, 165, 250, 0.35)',
      success: '#34D399',
      danger: '#F87171',
      diagramShell: '#EAEFF8',
      diagramPlate: '#D4DCED',
      diagramAccent: '#60A5FA',
    },
  },
  {
    id: 'solar-sunset',
    name: 'Solar Sunset',
    tagline: 'Deep graphite chassis with vibrant terracotta and solar amber',
    colors: {
      bg: '#141214',
      sidebarBg: '#1C191D',
      cardBg: '#252027',
      cardBorder: '#3E3642',
      textPrimary: '#F7F1F5',
      textSecondary: '#D6CAD4',
      textMuted: '#998997',
      accent: '#F97316',
      accentHover: '#EA580C',
      accentLight: '#FFEDD5',
      accentGlow: 'rgba(249, 115, 22, 0.35)',
      success: '#4ADE80',
      danger: '#EF4444',
      diagramShell: '#F2EDF1',
      diagramPlate: '#DDD4DC',
      diagramAccent: '#F97316',
    },
  },
];
