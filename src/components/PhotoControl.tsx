import type { ControlShape } from "../controller-controls";
import { CONTROL_SOURCE_SIZE } from "../controller-controls";

/** The target and contour share the photo's source-pixel rectangle. */
export function PhotoControl({ shape, label, selected, pressed = false, disabled, onSelect, className = "", text, slot }: {
  shape: ControlShape; label: string; selected: boolean; pressed?: boolean;
  disabled?: boolean; onSelect?: () => void; className?: string; text?: string; slot?: string;
}) {
  const [x, y, w, h] = shape.bounds;
  return <button type="button"
    className={`photo-control ${className} ${selected ? "is-selected" : ""} ${pressed ? "is-pressed" : ""}`}
    aria-label={label} aria-pressed={selected} disabled={disabled || !onSelect}
    data-slot={slot} title={label}
    style={{ left: `${x / CONTROL_SOURCE_SIZE[0] * 100}%`, top: `${y / CONTROL_SOURCE_SIZE[1] * 100}%`, width: `${w / CONTROL_SOURCE_SIZE[0] * 100}%`, height: `${h / CONTROL_SOURCE_SIZE[1] * 100}%` }}
    onClick={onSelect}>
    <svg viewBox={`${x} ${y} ${w} ${h}`} preserveAspectRatio="none" aria-hidden="true" focusable="false">
      <path className="photo-control-outline" d={shape.path} transform={shape.transform} vectorEffect="non-scaling-stroke" />
    </svg>
    {text && <span aria-hidden="true">{text}</span>}
  </button>;
}
