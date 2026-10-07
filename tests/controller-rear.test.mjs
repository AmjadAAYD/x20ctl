import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const catalog = JSON.parse(readFileSync(new URL('../x20ctl/controllers/catalog.json', import.meta.url)));
const shapes = JSON.parse(readFileSync(new URL('../src/assets/controllers/control-shapes.json', import.meta.url)));
const geometries = new Map();
for (const model of catalog) {
  const folder = new URL(`../src/assets/controllers/${model.id}/`, import.meta.url);
  const geometry = JSON.parse(readFileSync(new URL('rear-geometry.json', folder)));
  geometries.set(model.id, geometry);
  test(`${model.name} rear artwork and clickable outlines match its capability definition`, () => {
    const png = readFileSync(new URL(geometry.asset, folder));
    assert.equal(png.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
    assert.equal(png.readUInt32BE(16), 1536);
    assert.equal(png.readUInt32BE(20), 1024);
    assert.equal(geometry.aspect, png.readUInt32BE(16) / png.readUInt32BE(20));
    assert.deepEqual(geometry.controls.map(control => control.slot).sort(), [...model.macroSlots].sort());
    const controls = shapes.models[model.id];
    assert.deepEqual(Object.keys(controls.macros).sort(), [...model.macroSlots].sort());
    assert.deepEqual(Object.keys(controls.back).sort(), ['LB','LT','RB','RT']);
    for (const key of ['LB','LT','RB','RT']) assert.equal(key in controls.front, false);
    for (const [key, shape] of Object.entries({...controls.front, ...controls.back, ...controls.macros})) {
      const [x,y,w,h] = shape.bounds;
      assert.ok(shape.bounds.every(Number.isFinite));
      assert.ok(x >= 0 && y >= 0 && w > 5 && h > 5 && x+w <= 1536 && y+h <= 1024, key);
      assert.match(shape.path, /^M.*Z$/);
    }
  });
}

test('rear labels retain the manufacturer reference orientation', () => {
  const center = (model, slot) => {
    const [x,y,w,h] = shapes.models[model].macros[slot].bounds;
    return { x: (x+w/2)/1536, y: (y+h/2)/1024 };
  };
  for (const [model, left, right] of [['x20_pro', 'M6', 'M5'], ['x05_pro', 'M2', 'M1'], ['d10', 'M2', 'M1'], ['x10', 'M1', 'M2'], ['x15', 'M2', 'M1']]) {
    assert.ok(center(model, left).x < .5 && center(model, right).x > .5, model);
  }
  assert.ok(center('x20_pro', 'M5').y < .2 && center('x20_pro', 'M6').y < .2);
  assert.match(geometries.get('x05_pro').viewLabel, /tilt/i);
});
