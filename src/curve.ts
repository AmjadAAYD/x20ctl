import type { CurveConfig } from "./types/gamepad";

// A monotone Hermite illustration through the controller's two stored points.
// Firmware interpolation is unknown. This is never presented as measured output.
export function curvePreview(config: CurveConfig, x: number): number {
  const knots = [{ x: 0, y: 0 }, config.p1, config.p2, { x: 100, y: 100 }]
    .sort((a, b) => a.x - b.x)
    .filter((p, i, a) => i === a.length - 1 || p.x !== a[i + 1].x);
  const deltas = knots
    .slice(1)
    .map((p, i) => (p.y - knots[i].y) / (p.x - knots[i].x));
  const slopes = knots.map((_, i) =>
    i === 0
      ? deltas[0]
      : i === knots.length - 1
        ? deltas[i - 1]
        : deltas[i - 1] * deltas[i] <= 0
          ? 0
          : (deltas[i - 1] + deltas[i]) / 2,
  );
  deltas.forEach((d, i) => {
    if (d === 0) {
      slopes[i] = slopes[i + 1] = 0;
      return;
    }
    const a = slopes[i] / d,
      b = slopes[i + 1] / d;
    if (a * a + b * b > 9) {
      const scale = 3 / Math.hypot(a, b);
      slopes[i] = scale * a * d;
      slopes[i + 1] = scale * b * d;
    }
  });
  if (x <= knots[0].x) return knots[0].y;
  if (x >= knots[knots.length - 1].x) return knots[knots.length - 1].y;
  const i = knots.findIndex(
    (p, j) => j < knots.length - 1 && x >= p.x && x < knots[j + 1].x,
  );
  const span = knots[i + 1].x - knots[i].x,
    t = (x - knots[i].x) / span,
    t2 = t * t,
    t3 = t2 * t;
  return (
    (2 * t3 - 3 * t2 + 1) * knots[i].y +
    (t3 - 2 * t2 + t) * span * slopes[i] +
    (-2 * t3 + 3 * t2) * knots[i + 1].y +
    (t3 - t2) * span * slopes[i + 1]
  );
}
