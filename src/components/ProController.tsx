import { ControllerCanvas } from "./ControllerCanvas";
export function ProController() {
  return <section className="pro-controller-panel metal-panel">
    <div className="panel-heading"><h2>X20 Pro</h2><span className="panel-code">PREVIEW / INACTIVE</span></div>
    <ControllerCanvas model="x20_pro" disabled />
  </section>;
}
