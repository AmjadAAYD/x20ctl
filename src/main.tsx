import React from "react";
import ReactDOM from "react-dom/client";
import { App } from "./App";
import { StartupIntro } from "./components/StartupIntro";
import "./index.css";
import "./startup-intro.css";
import "./controller-presentation.css";
import "./studio-design.css";

const rootElement = document.getElementById("root");
if (rootElement) {
  ReactDOM.createRoot(rootElement).render(
    <React.StrictMode>
      <StartupIntro />
      <App />
    </React.StrictMode>,
  );
}
