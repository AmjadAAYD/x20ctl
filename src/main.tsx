import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { StartupIntro } from "./components/StartupIntro";
import "./index.css";
import "./rebrand.css";
import "./rebrand-polish.css";
import "./motion.css";
import "./startup-intro.css";
import "./controller-presentation.css";

const rootElement = document.getElementById("root");
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <StartupIntro />
      <App />
    </React.StrictMode>,
  );
}
