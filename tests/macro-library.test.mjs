import test from 'node:test';
import assert from 'node:assert/strict';
import { cloneMacroSteps, countWireEntries, setMacroPauses, validateSavedMacro } from '../src/macro-library.ts';

const step = (gap = 0) => ({id: 'original', buttons: ['A', 'RT'], leftStick: 8, rightStick: 0, durationMs: 40, intervalMs: gap});
const saved = (steps = [step()]) => ({format: 'x20ctl-macro', version: 1, name: 'Combo', sourceModel: 'x20', loopMs: 0, steps});

test('valid simultaneous inputs and directions round-trip without sharing arrays', () => {
  const original = saved([step(15)]);
  const clean = validateSavedMacro(original);
  assert.deepEqual(clean.steps, original.steps);
  clean.steps[0].buttons.push('B');
  assert.deepEqual(original.steps[0].buttons, ['A', 'RT']);
});

test('encoded pauses count toward the 47-entry limit', () => {
  assert.equal(countWireEntries(Array.from({length: 23}, () => step(5))), 46);
  validateSavedMacro(saved(Array.from({length: 47}, () => step())));
  assert.throws(() => validateSavedMacro(saved(Array.from({length: 24}, () => step(5)))), /47 entries/);
});

test('import rejects unsupported execution modes, inputs and malformed timing', () => {
  for (const patch of [{format: 'script'}, {version: 2}, {loopMs: 1}, {loopMs: Infinity}, {name: ''}, {mode: 'toggle'}]) assert.throws(() => validateSavedMacro({...saved(), ...patch}));
  for (const patch of [{buttons: ['HOME']}, {buttons: ['A', 'A']}, {durationMs: 0}, {intervalMs: 7}, {durationMs: 327680}, {leftStick: 9}, {rightStick: true}]) assert.throws(() => validateSavedMacro(saved([{...step(), ...patch}])));
});

test('copy creates independent steps; pause edits preserve hold times and input states', () => {
  const original = [step(50)];
  const copy = cloneMacroSteps(original);
  assert.notEqual(copy[0].id, original[0].id);
  copy[0].buttons.pop();
  assert.equal(original[0].buttons.length, 2);
  assert.equal(setMacroPauses(original, 0)[0].durationMs, 40);
  assert.equal(setMacroPauses(original, 100)[0].intervalMs, 100);
  assert.equal(original[0].intervalMs, 50);
  assert.throws(() => setMacroPauses(Array.from({length: 47}, () => step()), 5), /47 entries/);
});
