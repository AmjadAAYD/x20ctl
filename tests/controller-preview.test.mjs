import test from 'node:test';
import assert from 'node:assert/strict';
import { keyboardStickVector, pointerStickVector } from '../src/controller-preview.ts';

test('WASD previews a normalized left-stick direction and cancels opposing keys', () => {
  assert.deepEqual(keyboardStickVector(new Set(['w'])), { x: 0, y: -1 });
  const diagonal = keyboardStickVector(new Set(['a', 's']));
  assert.ok(Math.abs(diagonal.x + Math.SQRT1_2) < 1e-12);
  assert.ok(Math.abs(diagonal.y - Math.SQRT1_2) < 1e-12);
  assert.deepEqual(keyboardStickVector(new Set(['w', 's', 'd', 'a'])), { x: 0, y: 0 });
  assert.deepEqual(keyboardStickVector(new Set(['q'])), { x: 0, y: 0 });
});

test('pointer stick preview stays inside the stick gate', () => {
  assert.deepEqual(pointerStickVector(100, 100, 100, 100, 40), { x: 0, y: 0 });
  assert.deepEqual(pointerStickVector(140, 100, 100, 100, 40), { x: 1, y: 0 });
  assert.deepEqual(pointerStickVector(200, 100, 100, 100, 40), { x: 1, y: 0 });
  const diagonal = pointerStickVector(140, 140, 100, 100, 40);
  assert.ok(Math.abs(diagonal.x - Math.SQRT1_2) < 1e-12);
  assert.ok(Math.abs(diagonal.y - Math.SQRT1_2) < 1e-12);
});
