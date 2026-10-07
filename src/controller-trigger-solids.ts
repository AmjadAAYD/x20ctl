import { ExtrudeGeometry, Float32BufferAttribute, Shape, Vector2 } from "three";
import contours from "./assets/controllers/trigger-solids.json" with { type: "json" };
import motion from "./assets/controllers/trigger-motion.json" with { type: "json" };
import type { ControllerId } from "./controllers";
import { triggerStrength, type TriggerKey } from "./controller-triggers.ts";

export type SolidFrame = ReturnType<typeof triggerSolidFrame>;
export function triggerSolidFrame(model: ControllerId, key: TriggerKey) {
  const config = motion.models[model][key];
  const [[x,y],[endX,endY]]=config.hinge;
  const length=Math.hypot(endX-x,endY-y), ux=(endX-x)/length, uy=(endY-y)/length;
  const normal=key==="RT" ? [-uy,ux] : [uy,-ux];
  const contour=contours.models[model][key];
  const width=Math.max(...contour.map(p=>p[0]))-Math.min(...contour.map(p=>p[0]));
  return {...config,axis:[ux,uy],normal,perspective:1100,thickness:Math.max(26,Math.min(32,width*.17))};
}

/** Inverse of the release camera: the photo contour sits on a tilted 3D plane. */
export function unprojectSolidPhoto(frame: SolidFrame, [x,y]: number[]) {
  const dx=x-frame.hinge[0][0],dy=y-frame.hinge[0][1];
  const u=dx*frame.axis[0]+dy*frame.axis[1],v=dx*frame.normal[0]+dy*frame.normal[1];
  const angle=frame.restAngle*Math.PI/180;
  const w=1/(1-v*Math.tan(angle)/frame.perspective);
  return [u*w,v*w/Math.cos(angle),0];
}

/** Rigid rotation in 3D before perspective division; no cap compression. */
export function projectSolidPoint(frame: SolidFrame, [u,v,z]: number[], value: number) {
  const angle=(frame.restAngle+frame.travelAngle*triggerStrength(value))*Math.PI/180;
  const y=v*Math.cos(angle)+z*Math.sin(angle),depth=-v*Math.sin(angle)+z*Math.cos(angle);
  const w=1-depth/frame.perspective;
  return {camera:[u,y,depth],w,photo:[
    frame.hinge[0][0]+(frame.axis[0]*u+frame.normal[0]*y)/w,
    frame.hinge[0][1]+(frame.axis[1]*u+frame.normal[1]*y)/w,
  ]};
}

export function createTriggerSolid(model: ControllerId,key: TriggerKey) {
  const frame=triggerSolidFrame(model,key),contour=contours.models[model][key];
  const shape=new Shape(contour.map(photo=>{const [u,v]=unprojectSolidPhoto(frame,photo);return new Vector2(u,v);}));
  const bevel=3;
  const geometry=new ExtrudeGeometry(shape,{depth:frame.thickness-bevel*2,steps:1,
    bevelEnabled:true,bevelThickness:bevel,bevelSize:bevel,bevelOffset:-bevel,bevelSegments:5});
  geometry.translate(0,0,-frame.thickness+bevel);
  const pos=geometry.getAttribute("position"),normals=geometry.getAttribute("normal");
  const photoCoord:number[]=[],photoFace:number[]=[];
  for(let i=0;i<pos.count;i++) {
    const projected=projectSolidPoint(frame,[pos.getX(i),pos.getY(i),0],0);
    photoCoord.push(projected.photo[0]/1536*projected.w,(1-projected.photo[1]/1024)*projected.w,projected.w);
    photoFace.push(normals.getZ(i)>.999 ? 1 : 0);
  }
  geometry.setAttribute("photoCoord",new Float32BufferAttribute(photoCoord,3));
  geometry.setAttribute("photoFace",new Float32BufferAttribute(photoFace,1));
  const faceTriangles=photoFace.filter(v=>v===1).length/3;
  return {frame,contour,geometry,faceTriangles,wallTriangles:pos.count/3-faceTriangles};
}

export function solidTriggerOutline(model: ControllerId,key: TriggerKey,value: number) {
  const frame=triggerSolidFrame(model,key);
  return contours.models[model][key].map((point,i)=>{
    const [x,y]=projectSolidPoint(frame,unprojectSolidPhoto(frame,point),value).photo;
    return `${i?"L":"M"}${x.toFixed(3)} ${y.toFixed(3)}`;
  }).join(" ")+" Z";
}
