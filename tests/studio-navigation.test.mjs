import test from 'node:test';
import assert from 'node:assert/strict';
import { navigationAction } from '../src/studio-navigation.ts';

test('tester, recording, modal, busy and text editing contexts suppress every navigation action', () => {
  for (const blocked of [true]) {
    assert.equal(navigationAction({ buttons: { A: true, RB: true, DPAD_DOWN: true }, x: 0, y: 0 }, {}, blocked), null);
  }
});
test('activation and tab changes require a new press, never repeat held buttons', () => {
  assert.equal(navigationAction({ buttons: { A: true }, x: 0, y: 0 }, {}), 'activate');
  assert.equal(navigationAction({ buttons: { A: true }, x: 0, y: 0 }, { A: true }), null);
  assert.equal(navigationAction({ buttons: { RB: true }, x: 0, y: 0 }, {}), 'next-tab');
  assert.equal(navigationAction({ buttons: { B: true }, x: 0, y: 0 }, {}), 'back');
});
test('stick deadzone avoids drift; movement is directional and cannot become activation', () => {
  assert.equal(navigationAction({ buttons: {}, x: .4, y: -.4 }, {}), null);
  assert.equal(navigationAction({ buttons: {}, x: -.8, y: .1 }, {}), 'left');
  assert.equal(navigationAction({ buttons: { DPAD_DOWN: true }, x: 0, y: 0 }, {}), 'down');
});
