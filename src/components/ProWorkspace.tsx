import { ModelWorkspace } from "./ModelWorkspace";
import { useState } from "react";
import { ControllerResearchScanner } from "./ControllerResearchScanner";
export function ProWorkspace({
  onBack,
  onSupport,
}: {
  onBack: () => void;
  onSupport: () => void;
}) {
  const [scan, setScan] = useState(false);
  return (
    <>
      <ModelWorkspace
        model="x20_pro"
        active
        player={1}
        onSwitch={onBack}
        onSupport={onSupport}
        onScan={() => setScan(true)}
      />
      {scan && (
        <ControllerResearchScanner
          model="X20 Pro"
          onClose={() => setScan(false)}
        />
      )}
    </>
  );
}
