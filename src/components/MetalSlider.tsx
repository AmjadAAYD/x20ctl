import { useState, type CSSProperties, type InputHTMLAttributes } from "react";
import "./metal-slider.css";

type Props = Omit<
  InputHTMLAttributes<HTMLInputElement>,
  "type" | "value" | "min" | "max" | "onChange"
> & {
  value: number;
  min?: number;
  max?: number;
  onValueChange: (value: number) => void;
  tone?: "blue" | "amber";
};

export function MetalSlider({
  value,
  min = 0,
  max = 100,
  onValueChange,
  tone = "blue",
  disabled,
  ...props
}: Props) {
  const [dragging, setDragging] = useState(false);
  const ratio = Math.max(0, Math.min(1, (value - min) / (max - min || 1)));
  return (
    <div
      className={`metal-slider ${dragging ? "is-dragging" : ""} ${disabled ? "is-disabled" : ""}`}
      data-tone={tone}
      style={{ "--range-ratio": ratio } as CSSProperties}
    >
      <span className="metal-slider-rail" aria-hidden="true">
        <span className="metal-slider-fill" />
      </span>
      <span className="metal-slider-thumb" aria-hidden="true" />
      <input
        {...props}
        disabled={disabled}
        type="range"
        min={min}
        max={max}
        value={value}
        onChange={(event) => onValueChange(Number(event.target.value))}
        onPointerDown={(event) => {
          if (event.nativeEvent.isTrusted)
            event.currentTarget.setPointerCapture(event.pointerId);
          setDragging(true);
        }}
        onPointerUp={() => setDragging(false)}
        onPointerCancel={() => setDragging(false)}
        onLostPointerCapture={() => setDragging(false)}
        onBlur={() => setDragging(false)}
      />
    </div>
  );
}
