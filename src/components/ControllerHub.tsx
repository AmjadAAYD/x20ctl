import { ArrowRight, Bluetooth, CircleHelp, Plus, Radio, Usb } from "lucide-react";
import brandMark from "../assets/brand-mark.png";
import x20Hero from "../assets/x20-hero.png";

interface ControllerHubProps {
  onX20: () => void;
  onPro: () => void;
  onSupport: () => void;
}

export function ControllerHub({ onX20, onPro, onSupport }: ControllerHubProps) {
  return (
    <div className="metal-app controller-hub rebranded-hub">
      <header className="hub-header">
        <div className="chassis-brand">
          <img className="brand-emblem" src={brandMark} alt="" />
          <strong>x20ctl</strong>
        </div>
      </header>
      <main id="workspace" className="hub-main">
        <div className="hub-intro">
          <h1>Add a Controller</h1>
          <p>Connect an X20 to open its configuration studio. Windows can show up to four XInput player slots.</p>
          <div className="hub-guidance"><CircleHelp size={19} /> Connect your controller via USB, a wireless receiver, or a supported XInput mode.</div>
        </div>
        <div className="controller-cards">
          {[1, 2, 3, 4].map((number) => (
            <article className={`controller-card metal-panel ${number === 1 ? "featured" : "empty"}`} key={number}>
              <span className="controller-card-index">{number}</span>
              <div className="controller-card-icon"><img src={x20Hero} alt={number === 1 ? "Front illustration of the X20 controller" : ""} /></div>
              <h2>{number === 1 ? "X20 Controller" : `Controller ${number}`}</h2>
              <p className="controller-slot-status"><i className="led" /> Not connected</p>
              {number === 1 ? (
                <button className="button primary" onClick={onX20}><Plus size={21} /> Open X20 studio <ArrowRight size={17} /></button>
              ) : (
                <button className="button secondary" disabled title="Additional independent controller sessions are not supported yet"><Plus size={21} /> Add Controller</button>
              )}
            </article>
          ))}
        </div>
        <div className="hub-connection-guide">
          <div><Usb size={28} /><span><strong>USB Connection</strong><small>Connect via USB cable for gameplay input.</small></span></div>
          <div><Radio size={28} /><span><strong>Wireless Receiver</strong><small>Use the receiver in XInput mode.</small></span></div>
          <div><Bluetooth size={28} /><span><strong>Bluetooth Configuration</strong><small>Discover a supported X20 in the studio.</small></span></div>
        </div>
        <div className="hub-footer-links">
          <button className="hub-pro-link" onClick={onPro}>Explore X20 Pro read-only discovery <ArrowRight size={15} /></button>
          <button className="hub-pro-link" onClick={onSupport}>Support X20ctl</button>
        </div>
      </main>
    </div>
  );
}
