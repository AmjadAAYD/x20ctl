import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { triggerPose, triggerStrength } from '../src/controller-triggers.ts';

const catalog = JSON.parse(readFileSync(new URL('../x20ctl/controllers/catalog.json', import.meta.url)));
const controls = JSON.parse(readFileSync(new URL('../src/assets/controllers/control-shapes.json', import.meta.url)));
const apply = ([a,b,c,d,e,f], [x,y]) => [a*x+c*y+e,b*x+d*y+f];
const distance = (a,b) => Math.hypot(a[0]-b[0],a[1]-b[1]);
test('analog trigger input clamps safely without inventing values', () => {
  for (const [input, expected] of [[-.3,0], [0,0], [.23,.23], [.73,.73], [1,1], [4,1], [NaN,0], [Infinity,0]])
    assert.equal(triggerStrength(input), expected);
});
for (const model of catalog) {
  test(`${model.name} keeps the shaft fixed while the free edge pivots independently`, () => {
    const folder = new URL(`../src/assets/controllers/${model.id}/`, import.meta.url);
    for (const name of ['controller-rear.png', 'controller-rear-trigger-base.png']) {
      const png = readFileSync(new URL(name, folder));
      assert.equal(png.readUInt32BE(16),1536); assert.equal(png.readUInt32BE(20),1024);
    }
    for (const key of ['LT','RT']) {
      const shape = controls.models[model.id].back[key];
      assert.equal(shape.transform,undefined,'cap paths must already be in photo coordinates');
      assert.notEqual(shape.path,controls.models[model.id].back[key==='LT'?'RT':'LT'].path,'two cutouts cannot overlap and cancel');
      const poses = [0,.23,.5,.73,1].map(value => triggerPose(model.id,key,value));
      assert.equal(new Set(poses.map(pose => pose.transform)).size,5);
      assert.deepEqual(poses.map(pose => pose.press),[0,.23,.5,.73,1]);
      assert.equal(triggerPose(model.id,key,NaN).transform,poses[0].transform);
      assert.equal(triggerPose(model.id,key,2).transform,poses[4].transform);
      for (const pose of poses) assert.ok(!/NaN|Infinity/.test(pose.transform));
      const [x,y,w,h] = shape.bounds;
      const tip = [x+w/2,y+h*.1];
      let previousTravel = -1;
      for (const pose of poses) {
        // Every point on the shaft stays fixed, unlike whole-cap translation.
        const [start,end] = pose.hinge;
        const fromShaft = point => Math.abs((end[0]-start[0])*(point[1]-start[1])-(end[1]-start[1])*(point[0]-start[0]))/distance(start,end);
        if (pose.press > 0) {
          assert.ok(fromShaft(apply(pose.matrix,tip)) < fromShaft(tip),'restore the downward press toward the body-side shaft');
          assert.ok(apply(pose.matrix,tip)[1] > tip[1],'free edge must move down in the rear photo');
          const towardViewer = (tip[0]-start[0])*pose.depth[0]+(tip[1]-start[1])*pose.depth[1];
          assert.ok(towardViewer > 0,'press must approach the viewer, not recede');
          assert.match(pose.cssTransform,/^perspective\(.+\) matrix3d\(/,'render camera depth rather than flatten it');
        }
        for (const point of [start,end,[(start[0]+end[0])/2,(start[1]+end[1])/2]])
        {
          assert.ok(distance(apply(pose.matrix,point),point)<1e-6,'hinge drift');
          assert.ok(Math.abs((point[0]-start[0])*pose.depth[0]+(point[1]-start[1])*pose.depth[1])<1e-6,'shaft must stay at its original camera depth');
        }
        const depth = (tip[0]-start[0])*pose.depth[0]+(tip[1]-start[1])*pose.depth[1];
        const w = 1-depth/pose.perspective;
        const flat = apply(pose.matrix,tip);
        const projected = [start[0]+(flat[0]-start[0])/w,start[1]+(flat[1]-start[1])/w];
        assert.ok(w > .5 && w <= 1,'near face must grow with perspective without crossing the camera');
        if(pose.press > 0) assert.ok(projected[1] > tip[1],'perspective must retain the downward press');
        const travel = distance(apply(pose.matrix,tip),tip);
        assert.ok(travel>previousTravel); previousTravel=travel;
        const nearShaft = [start[0]+(tip[0]-start[0])*.1,start[1]+(tip[1]-start[1])*.1];
        assert.ok(Math.abs(distance(apply(pose.matrix,nearShaft),nearShaft)-travel*.1)<1e-6,'free edge must move more than shaft end');
        assert.match(pose.transform,/^matrix\(/);
      }
      assert.ok(previousTravel>12,'free-edge travel must be visible');
      assert.ok(distance(apply(poses[0].matrix,tip),tip)<1e-6,'released cap registration');
    }
    const right = triggerPose(model.id,'RT',.73),left = triggerPose(model.id,'LT',.73);
    const testPoint = [controls.models[model.id].back.RT.bounds[0]+40,controls.models[model.id].back.RT.bounds[1]+15];
    const projectedRight = apply(right.matrix,testPoint);
    const projectedLeft = apply(left.matrix,[1536-testPoint[0],testPoint[1]]);
    assert.ok(distance(projectedLeft,[1536-projectedRight[0],projectedRight[1]])<1e-6,'mirrored shafts must pivot symmetrically');
    assert.ok(Math.abs(right.depth[0]+left.depth[0])<1e-9 && Math.abs(right.depth[1]-left.depth[1])<1e-9,'both mirrored caps approach the viewer');
  });
}
