import { ArrowRight, Gamepad2, Layers3, Palette } from "lucide-react";

export function ControllerHub({ onX20, onPro, onTheme }: { onX20: () => void; onPro: () => void; onTheme: () => void }) {
  return <div className="metal-app controller-hub"><header className="chassis-header"><div className="chassis-brand"><div className="brand-emblem"><Gamepad2 size={24}/></div><div><strong>x20ctl</strong><span>CONTROLLER STUDIO</span></div><span className="edition-tag">3.1</span></div><div className="header-actions"><button className="button secondary" onClick={onTheme}><Palette size={16}/>Themes</button></div></header>
    <main id="workspace" className="metal-workspace"><div className="hub-intro"><span className="engraved">YOUR HARDWARE / YOUR SPACE</span><h1>Controllers</h1><p>Choose a workspace. Each model keeps its own connection, capabilities and setups.</p></div>
      <div className="controller-cards"><button className="controller-card metal-panel" aria-label="Open X20 studio" onClick={onX20}><span className="controller-card-index">01 / SUPPORTED</span><div className="controller-card-icon"><Gamepad2 size={37}/></div><h2>EasySMX X20</h2><p>Open the complete mapping, curves, macros and device studio.</p><span className="controller-card-action">Open X20 studio <ArrowRight size={18}/></span></button>
        <button className="controller-card metal-panel" aria-label="Explore X20 Pro" onClick={onPro}><span className="controller-card-index">02 / DISCOVERY</span><div className="controller-card-icon"><Layers3 size={37}/></div><h2>EasySMX X20 Pro</h2><p>Explore the distinct six-control hardware and its read-only investigation tools.</p><span className="controller-card-action">Explore X20 Pro <ArrowRight size={18}/></span></button></div>
      <div className="hub-footnote"><span className="led on"/> X20 settings are available. Pro configuration remains locked until verified on real hardware.</div>
    </main></div>;
}
