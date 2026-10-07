import test from 'node:test';
import assert from 'node:assert/strict';
import { createTriggerSolid, projectSolidPoint, unprojectSolidPhoto } from '../src/controller-trigger-solids.ts';
import catalog from '../x20ctl/controllers/catalog.json' with {type:'json'};

const distance = (a,b) => Math.hypot(...a.map((v,i)=>v-b[i]));
for(const model of catalog) test(`${model.name} rotates a thick beveled cap without flattening its walls`,()=>{
  for(const key of ['LT','RT']) {
    const solid=createTriggerSolid(model.id,key);
    try {
      assert.ok(solid.frame.thickness>=24,'cap cannot be a photographic sheet');
      assert.ok(solid.wallTriangles>100 && solid.faceTriangles>10,'both cap and rounded side walls must exist');
      const pos=solid.geometry.getAttribute('position');
      const z=Array.from({length:pos.count},(_,i)=>pos.getZ(i));
      assert.ok(Math.max(...z)-Math.min(...z)>24,'mesh vertices need nonzero solid depth');
      for(const photo of solid.contour) {
        const p=unprojectSolidPhoto(solid.frame,photo);
        assert.ok(distance(projectSolidPoint(solid.frame,p,0).photo,photo)<.0001,'released top contour must register to the source photo');
        const bottom=[p[0],p[1],p[2]-solid.frame.thickness];
        for(const press of [0,.25,.5,.75,1]) {
          const topPose=projectSolidPoint(solid.frame,p,press), bottomPose=projectSolidPoint(solid.frame,bottom,press);
          assert.ok(Math.abs(distance(topPose.camera,bottomPose.camera)-solid.frame.thickness)<.0001,'rotation must preserve cap thickness in 3D');
          assert.ok(topPose.w>.5,'cap must remain behind the camera');
        }
      }
      for(const press of [0,.5,1]) {
        assert.ok(distance(projectSolidPoint(solid.frame,[0,0,0],press).photo,solid.frame.hinge[0])<.0001,'fixed shaft');
        const tip=unprojectSolidPhoto(solid.frame,solid.contour.reduce((a,b)=>a[1]<b[1]?a:b));
        const rest=projectSolidPoint(solid.frame,tip,0),pressed=projectSolidPoint(solid.frame,tip,press);
        if(press>0) {
          assert.ok(pressed.photo[1]>rest.photo[1],'free edge must press down');
          assert.ok(pressed.camera[2]>rest.camera[2],'free edge must approach the viewer');
        }
      }
    } finally {solid.geometry.dispose();}
  }
});
