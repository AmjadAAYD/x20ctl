import test from 'node:test';
import assert from 'node:assert/strict';
import {curvePreview} from '../src/curve.ts';

test('curve illustration passes through the stored points',()=>{
  const cfg={p1:{x:25,y:45},p2:{x:65,y:85}};
  for(const point of [{x:0,y:0},cfg.p1,cfg.p2,{x:100,y:100}])
    assert.ok(Math.abs(curvePreview(cfg,point.x)-point.y)<1e-9);
  const points=Array.from({length:101},(_,x)=>curvePreview(cfg,x));
  assert.ok(points.every((y,i)=>y>=0&&y<=100&&(i===0||y>=points[i-1])));
});

test('coincident control points remain finite',()=>{
  for(const cfg of [{p1:{x:0,y:0},p2:{x:0,y:50}},{p1:{x:100,y:90},p2:{x:100,y:100}}])
    for(let x=0;x<=100;x++)assert.ok(Number.isFinite(curvePreview(cfg,x)));
});
