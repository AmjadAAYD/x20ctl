import { Crosshair, Gauge, SlidersHorizontal, Activity } from "lucide-react";
import { MetalDialog } from "./MetalDialog";
import "./metal-curves.css";

interface CurvesHelpModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function CurvesHelpModal({ isOpen, onClose }: CurvesHelpModalProps) {
  if (!isOpen) return null;
  return (
    <MetalDialog
      title="Response curve guide"
      subtitle="Signal shaping reference"
      closeLabel="Close curve guide"
      className="curve-help-dialog"
      onClose={onClose}
    >
      <div className="curve-help-grid">
        <article>
          <Crosshair size={20} />
          <div>
            <h3>Inner deadzone</h3>
            <p>
              The small area near the resting position where input is ignored.
              Increase it a little if the stick moves your character or camera
              while untouched.
            </p>
          </div>
        </article>
        <article>
          <Gauge size={20} />
          <div>
            <h3>Full-output threshold</h3>
            <p>
              The input level that reaches maximum output. A value of 90%
              reaches full output before the end of the travel. This is the
              controller's outer-deadzone setting.
            </p>
          </div>
        </article>
        <article>
          <Activity size={20} />
          <div>
            <h3>Choose a starting curve</h3>
            <dl>
              <dt>Linear</dt>
              <dd>Proportional response points.</dd>
              <dt>Aggressive</dt>
              <dd>Higher output earlier in the movement.</dd>
              <dt>Relaxed</dt>
              <dd>Lower output near the center for finer adjustments.</dd>
              <dt>Instant</dt>
              <dd>
                Earlier high output with a 2% inner deadzone and 75% full-output
                threshold. It remains an analog input.
              </dd>
            </dl>
          </div>
        </article>
        <article>
          <SlidersHorizontal size={20} />
          <div>
            <h3>Fine-tune P1 and P2</h3>
            <p>
              Use Edit Curve to adjust each point's input and output
              coordinates. P1 cannot move past P2 on the input axis.
            </p>
            <p>
              The drawn line illustrates the stored points. It is not a
              measurement of the controller's firmware interpolation or a game's
              response.
            </p>
          </div>
        </article>
      </div>
      <div className="curve-help-footer">
        <p>
          Edits stay in the current setup draft until you apply them to the
          controller.
        </p>
        <button className="curve-button is-active" onClick={onClose}>
          Back to curves
        </button>
      </div>
    </MetalDialog>
  );
}
