import {
  ArrowRight,
  Bluetooth,
  CircleHelp,
  Gamepad2,
  Plus,
  Radio,
  Usb,
} from "lucide-react";
import brandMark from "../assets/brand-mark.png";
import { controller, controllers, type ControllerId } from "../controllers";
import { ControllerCanvas } from "./ControllerCanvas";

interface ControllerHubProps {
  players: (ControllerId | null)[];
  busy: boolean;
  onChoose: (player: number) => void;
  onEnter: (player: number) => void;
  onScan: () => void;
}

export function ControllerHub({
  players,
  busy,
  onChoose,
  onEnter,
  onScan,
}: ControllerHubProps) {
  return (
    <div className="metal-app controller-hub rebranded-hub">
      <header className="hub-header">
        <div className="chassis-brand">
          <img className="brand-emblem" src={brandMark} alt="" />
          <strong>x20ctl</strong>
        </div>
        <button className="button secondary" onClick={onScan}>Controller scanner</button>
      </header>
      <main id="workspace" className="hub-main">
        <div className="hub-intro">
          <h1>Controller zone</h1>
          <p>Choose a model for each player, then open their Studio.</p>
          <div className="hub-guidance">
            <CircleHelp size={19} /> Model assignments are local. Connect
            hardware separately inside the Studio.
          </div>
        </div>
        <div className="controller-cards">
          {players.map((model, index) => {
            const number = index + 1;
            const selected = model ? controller(model) : null;
            return (
              <article
                className={`controller-card metal-panel ${selected ? "featured" : "empty"}`}
                key={number}
                aria-label={`Player ${number}`}
                data-player={number}
                data-controller={model ?? ""}
              >
                <span className="controller-card-index">Player {number}</span>
                <button
                  className="controller-card-icon"
                  disabled={busy}
                  onClick={() => onChoose(index)}
                  aria-label={`Choose controller for Player ${number}`}
                >
                  {model ? (
                    <ControllerCanvas model={model} disabled />
                  ) : (
                    <span className="controller-empty-art">
                      <Gamepad2 size={58} strokeWidth={1} />
                      <Plus size={20} />
                    </span>
                  )}
                </button>
                <h2>{selected?.name ?? "Choose a controller"}</h2>
                <p className="controller-slot-status">
                  <i className="led" />
                  {selected ? "Assigned · not connected" : "No model assigned"}
                </p>
                <small className="controller-card-detail">
                  {selected ? (
                    <>
                      {selected.availability === "unavailable" ? "Not available yet" : selected.availability === "input_experimental" ? "Gameplay input · experimental" : selected.macroSlots.length > 0
                        ? `${selected.macroSlots.length} programmable controls`
                        : `${selected.hardware.sticks} sticks`}
                      {selected.availability === "preview" && " · preview Studio"}
                    </>
                  ) : (
                    `Choose from ${controllers.filter((model) => model.visible).length} EasySMX models`
                  )}
                </small>
                <button
                  className={`button ${selected ? "primary" : "secondary"}`}
                  disabled={busy}
                  aria-label={`${selected ? "Enter Studio" : "Add controller"} for Player ${number}`}
                  onClick={() => (selected ? onEnter(index) : onChoose(index))}
                >
                  {selected ? <ArrowRight size={18} /> : <Plus size={18} />}
                  {selected ? "Enter the Studio" : "Add Controller"}
                </button>
              </article>
            );
          })}
        </div>
        <div className="hub-connection-guide">
          <div>
            <Usb size={28} />
            <span>
              <strong>USB Connection</strong>
              <small>Connect via USB cable for gameplay input.</small>
            </span>
          </div>
          <div>
            <Radio size={28} />
            <span>
              <strong>Wireless Receiver</strong>
              <small>Use the receiver in XInput mode.</small>
            </span>
          </div>
          <div>
            <Bluetooth size={28} />
            <span>
              <strong>Bluetooth Configuration</strong>
              <small>Discover a supported X20 in the studio.</small>
            </span>
          </div>
        </div>
      </main>
    </div>
  );
}
