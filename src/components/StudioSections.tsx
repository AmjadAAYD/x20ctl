import {
  Activity,
  FolderOpen,
  Gamepad2,
  Layers3,
  Power,
  SlidersHorizontal,
  Volume2,
} from "lucide-react";
import { MetalDialog } from "./MetalDialog";

export const STUDIO_SECTIONS = [
  { id: "buttons", label: "Buttons", icon: Gamepad2 },
  { id: "curves", label: "Response curves", icon: SlidersHorizontal },
  { id: "macros", label: "Macros", icon: Layers3 },
  { id: "rumble", label: "Vibration", icon: Volume2 },
  { id: "power", label: "Power & device", icon: Power },
  { id: "tester", label: "Input tester", icon: Activity },
  { id: "profiles", label: "Saved setups", icon: FolderOpen },
];

export function studioSectionLabel(id: string) {
  return ({ curves: 'Curves', power: 'Device', tester: 'Tester', profiles: 'Setups' } as Record<string, string>)[id];
}

export function MissingControllerData({
  model,
  section,
  onLater,
  onScan,
}: {
  model: string;
  section: string;
  onLater: () => void;
  onScan: () => void;
}) {
  return (
    <MetalDialog
      title={`${section} · data needed`}
      subtitle={model}
      onClose={onLater}
    >
      <p>
        There is not enough verified controller data to make{" "}
        {section.toLowerCase()} work for {model} yet.
      </p>
      <p>
        Scan now opens the built-in guided collector. After you review the
        report, it can be sent to X20CTLADMIN to help add support. Scan later
        keeps this section available as a preview.
      </p>
      <div className="dialog-actions">
        <button className="button primary" onClick={onScan}>
          Scan now
        </button>
        <button className="button secondary" onClick={onLater}>
          Scan later
        </button>
      </div>
    </MetalDialog>
  );
}
