/** Local-only controller illustration input. Never sent to the device bridge. */
export function keyboardStickVector(keys: ReadonlySet<string>): { x: number; y: number } {
  const x = Number(keys.has("d")) - Number(keys.has("a"));
  const y = Number(keys.has("s")) - Number(keys.has("w"));
  const length = Math.hypot(x, y);
  return length ? { x: x / length, y: y / length } : { x: 0, y: 0 };
}

export function pointerStickVector(
  x: number,
  y: number,
  centerX: number,
  centerY: number,
  radius: number,
): { x: number; y: number } {
  if (radius <= 0) return { x: 0, y: 0 };
  const dx = x - centerX;
  const dy = y - centerY;
  const length = Math.hypot(dx, dy);
  const scale = length > radius ? radius / length : 1;
  return { x: (dx * scale) / radius, y: (dy * scale) / radius };
}
