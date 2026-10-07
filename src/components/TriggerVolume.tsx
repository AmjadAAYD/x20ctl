import { useEffect, useRef } from "react";
import { Camera, Color, DoubleSide, LinearFilter, Mesh, Scene, ShaderMaterial, SRGBColorSpace, TextureLoader, Vector2, WebGLRenderer } from "three";
import { createTriggerSolid } from "../controller-trigger-solids";
import { triggerStrength } from "../controller-triggers";
import type { ControllerId } from "../controllers";

const vertexShader = `
attribute vec3 photoCoord;
attribute float photoFace;
uniform vec2 hinge, axis, down;
uniform float angle, cameraDistance;
varying vec3 vPhoto, vNormal;
varying float vFace, vDepth;
void main() {
  float c=cos(angle),s=sin(angle);
  vec3 p=vec3(position.x,position.y*c+position.z*s,-position.y*s+position.z*c);
  float w=1.0-p.z/cameraDistance;
  vec2 photo=hinge+(axis*p.x+down*p.y)/w;
  vec2 clip=vec2(photo.x/768.0-1.0,1.0-photo.y/512.0);
  gl_Position=vec4(clip*w,-p.z/cameraDistance*w,w);
  vPhoto=photoCoord;
  vNormal=vec3(normal.x,normal.y*c+normal.z*s,-normal.y*s+normal.z*c);
  vFace=photoFace;
  vDepth=position.z;
}`;
const fragmentShader = `
uniform sampler2D photoTexture;
uniform vec3 wallColor;
uniform float strength, thickness;
varying vec3 vPhoto, vNormal;
varying float vFace, vDepth;
void main() {
  vec3 n=normalize(vNormal);
  vec3 light=normalize(vec3(-.3,-.45,.85));
  float diffuse=max(dot(n,light),0.0);
  float spec=pow(max(dot(reflect(-light,n),vec3(0.0,0.0,1.0)),0.0),28.0);
  vec3 face=texture2D(photoTexture,vPhoto.xy/vPhoto.z).rgb;
  face*=mix(1.0,.84+.2*diffuse,strength);
  face+=vec3(spec*.045*strength);
  float crease=mix(.55,1.0,clamp((vDepth+thickness)/thickness,0.0,1.0));
  vec3 side=wallColor*(.48+.52*diffuse)*crease+vec3(spec*.22);
  float alpha=vFace>.5 ? 1.0 : min(1.0,strength*8.0);
  if(alpha<.001) discard;
  gl_FragColor=vec4(vFace>.5 ? face : side,alpha);
  #include <colorspace_fragment>
}`;

/** Only the caps are 3D; the saved controller shell remains stationary. */
export function TriggerVolume({model,original,leftTrigger,rightTrigger,onReady}: {
  model: ControllerId; original: string; leftTrigger: number; rightTrigger: number; onReady: (ready: boolean)=>void;
}) {
  const canvasRef=useRef<HTMLCanvasElement>(null);
  const inputs=useRef({leftTrigger,rightTrigger}); inputs.current={leftTrigger,rightTrigger};
  const scheduleRef=useRef<()=>void>(()=>{});
  useEffect(()=>{
    const canvas=canvasRef.current!;
    onReady(false);
    let renderer:WebGLRenderer;
    try {renderer=new WebGLRenderer({canvas,alpha:true,antialias:true,preserveDrawingBuffer:true});}
    catch {canvas.dataset.status="unavailable";onReady(false);return;}
    const scene=new Scene(),camera=new Camera();
    let disposed=false,ready=false,frameId=0;
    const texture=new TextureLoader().load(original,()=>{
      if(disposed){texture.dispose();return;}
      ready=true;canvas.dataset.status="ready";onReady(true);schedule();
    },undefined,()=>{canvas.dataset.status="texture-error";onReady(false);});
    texture.colorSpace=SRGBColorSpace; texture.minFilter=LinearFilter;texture.magFilter=LinearFilter;
    const solids=(["LT","RT"] as const).map(key=>{
      const solid=createTriggerSolid(model,key),f=solid.frame;
      const material=new ShaderMaterial({vertexShader,fragmentShader,side:DoubleSide,transparent:true,
        uniforms:{photoTexture:{value:texture},hinge:{value:new Vector2(...f.hinge[0] as [number,number])},
          axis:{value:new Vector2(...f.axis as [number,number])},down:{value:new Vector2(...f.normal as [number,number])},
          angle:{value:f.restAngle*Math.PI/180},cameraDistance:{value:f.perspective},strength:{value:0},
          thickness:{value:f.thickness},wallColor:{value:new Color(model==="x20" ? "#d8dfeb" : "#252b35")}}});
      const mesh=new Mesh(solid.geometry,material);mesh.frustumCulled=false;scene.add(mesh);
      canvas.dataset[`${key.toLowerCase()}Depth`]=f.thickness.toFixed(3);
      canvas.dataset[`${key.toLowerCase()}Walls`]=String(solid.wallTriangles);
      return {...solid,key,material};
    });
    const render=()=>{
      frameId=0;if(disposed||!ready)return;
      const rect=canvas.parentElement!.getBoundingClientRect();
      if(rect.width<1||rect.height<1)return;
      renderer.setPixelRatio(Math.min(devicePixelRatio||1,2));renderer.setSize(rect.width,rect.height,false);
      for(const solid of solids) {
        const press=triggerStrength(solid.key==="LT" ? inputs.current.leftTrigger : inputs.current.rightTrigger);
        solid.material.uniforms.strength.value=press;
        solid.material.uniforms.angle.value=(solid.frame.restAngle+solid.frame.travelAngle*press)*Math.PI/180;
      }
      renderer.render(scene,camera);
      for(const solid of solids)canvas.dataset[`${solid.key.toLowerCase()}Rendered`]=String(solid.material.uniforms.strength.value);
    };
    function schedule(){if(!disposed&&!frameId)frameId=requestAnimationFrame(render);}
    scheduleRef.current=schedule;
    const resize=new ResizeObserver(schedule);resize.observe(canvas.parentElement!);
    const lost=(event:Event)=>{event.preventDefault();ready=false;canvas.dataset.status="lost";onReady(false);};
    const restored=()=>{ready=true;canvas.dataset.status="ready";onReady(true);schedule();};
    canvas.addEventListener("webglcontextlost",lost);canvas.addEventListener("webglcontextrestored",restored);
    return ()=>{
      disposed=true;cancelAnimationFrame(frameId);scheduleRef.current=()=>{};resize.disconnect();
      canvas.removeEventListener("webglcontextlost",lost);canvas.removeEventListener("webglcontextrestored",restored);
      for(const solid of solids){solid.geometry.dispose();solid.material.dispose();}
      texture.dispose();renderer.dispose();renderer.forceContextLoss();
    };
  },[model,original,onReady]);
  useEffect(()=>{scheduleRef.current();},[leftTrigger,rightTrigger]);
  return <canvas ref={canvasRef} className="controller-trigger-volume" data-status="loading" aria-hidden="true" />;
}
