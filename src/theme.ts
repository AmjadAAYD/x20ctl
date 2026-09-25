export interface Appearance {
  id: string;
  background: string;
  surface: string;
  outline: string;
  text: string;
  accent: string;
  glow: string;
  motion: "off" | "subtle" | "vivid";
}

export const APPEARANCE_KEY = "x20ctl.appearance.v1";
export const THEMES: Appearance[] = [
  {
    id: "Graphite Cyan",
    background: "#101215",
    surface: "#23272c",
    outline: "#4b5259",
    text: "#edf0f2",
    accent: "#64dce4",
    glow: "#3fb7d4",
    motion: "subtle",
  },
  {
    id: "Titanium Amber",
    background: "#151514",
    surface: "#2b2b29",
    outline: "#605b52",
    text: "#f3f0e9",
    accent: "#ffbf68",
    glow: "#d98c3d",
    motion: "subtle",
  },
  {
    id: "Obsidian Violet",
    background: "#111118",
    surface: "#252532",
    outline: "#57536b",
    text: "#f0eef7",
    accent: "#bca5ff",
    glow: "#8b75db",
    motion: "subtle",
  },
  {
    id: "Silver Ice",
    background: "#282d31",
    surface: "#40484f",
    outline: "#78838a",
    text: "#ffffff",
    accent: "#a8eff4",
    glow: "#68cddf",
    motion: "subtle",
  },
];

const validColor = (value: unknown): value is string =>
  typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value);

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((offset) => {
    const channel = parseInt(hex.slice(offset, offset + 2), 16) / 255;
    return channel <= 0.04045
      ? channel / 12.92
      : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
}

export function contrast(first: string, second: string): number {
  const a = luminance(first);
  const b = luminance(second);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

export function validateAppearance(value: unknown): value is Appearance {
  if (!value || typeof value !== "object") return false;
  const theme = value as Record<string, unknown>;
  if (typeof theme.id !== "string" || theme.id.length > 40) return false;
  if (
    !["background", "surface", "outline", "text", "accent", "glow"].every(
      (key) => validColor(theme[key]),
    )
  )
    return false;
  if (!["off", "subtle", "vivid"].includes(String(theme.motion))) return false;
  return (
    contrast(theme.text as string, theme.surface as string) >= 4.5 &&
    contrast(theme.text as string, theme.background as string) >= 4.5 &&
    contrast(theme.accent as string, theme.surface as string) >= 3
  );
}

export function loadAppearance(): Appearance {
  try {
    const value = JSON.parse(localStorage.getItem(APPEARANCE_KEY) ?? "null");
    if (validateAppearance(value)) return value;
  } catch {
    // Invalid appearance settings cannot prevent the desktop from launching.
  }
  return THEMES[0];
}

export function saveAppearance(value: Appearance): void {
  if (!validateAppearance(value)) throw new Error("Theme colors need readable contrast.");
  localStorage.setItem(APPEARANCE_KEY, JSON.stringify(value));
}
