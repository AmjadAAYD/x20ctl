import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const catalog = JSON.parse(readFileSync(new URL('../x20ctl/controllers/catalog.json', import.meta.url)));
for (const model of catalog) {
  test(`${model.name} has artwork-sized contours for every declared motor zone`, () => {
    const folder = new URL(`../src/assets/controllers/${model.id}/`, import.meta.url);
    const geometry = JSON.parse(readFileSync(new URL('haptics-geometry.json', folder)));
    const image = readFileSync(new URL('controller.png', folder));
    const width = image.readUInt32BE(16), height = image.readUInt32BE(20);
    assert.deepEqual(geometry.viewBox, [0, 0, width, height]);
    assert.deepEqual(Object.keys(geometry.contours).sort(), model.visual.motors.map(zone => zone.id).sort());
    for (const [zone, contour] of Object.entries(geometry.contours)) {
      assert.match(contour, /^M[\d. ]+[LCQ]/, zone);
      assert.match(contour, /Z$/, `${zone} is closed so the effect can be clipped`);
      assert.ok(!/[AHVST]/i.test(contour), `${zone} uses paired coordinates for tracing`);
      const coordinates = contour.match(/\d+(?:\.\d+)?/g).map(Number);
      assert.ok(coordinates.length >= 12 && coordinates.length % 2 === 0, zone);
      for (let i = 0; i < coordinates.length; i += 2) {
        assert.ok(coordinates[i] >= 0 && coordinates[i] <= width, `${zone} x stays on the artwork`);
        assert.ok(coordinates[i + 1] >= 0 && coordinates[i + 1] <= height, `${zone} y stays on the artwork`);
      }
      if (zone.endsWith('trigger')) assert.ok(coordinates.filter((_, i) => i % 2).every(y => y < height * .2), `${zone} stays above the face controls`);
    }
  });
}
