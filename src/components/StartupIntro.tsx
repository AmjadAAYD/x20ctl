import { useCallback, useEffect, useLayoutEffect, useRef, useState, type CSSProperties } from "react";
import { BrandX } from "./BrandX";

const INTRO_MS = 7000;
const SKIP_FADE_MS = 450;
const particles = Array.from({ length: 16 }, (_, index) => {
  const angle = (index / 16) * Math.PI * 2;
  return {
    "--dx": `${Math.cos(angle) * (23 + (index % 3) * 8)}vw`,
    "--dy": `${Math.sin(angle) * (22 + (index % 4) * 7)}vh`,
    "--delay": `${(index % 4) * 35}ms`,
  } as CSSProperties;
});

/** One launch sequence. The real app mounts underneath; no video or frame loop. */
export function StartupIntro() {
  const [visible, setVisible] = useState(
    () => !window.matchMedia("(prefers-reduced-motion: reduce)").matches,
  );
  const [exiting, setExiting] = useState(false);
  const intro = useRef<HTMLElement>(null);
  const exitStarted = useRef(false);
  const finish = useCallback(() => setVisible(false), []);
  const skip = useCallback(() => {
    if (!intro.current || exitStarted.current) return;
    // Continue from the current opacity, even during the natural final fade.
    intro.current.style.setProperty("--intro-exit-opacity", getComputedStyle(intro.current).opacity);
    exitStarted.current = true;
    setExiting(true);
  }, []);

  useEffect(() => {
    if (!visible || !exiting) return;
    // Release the input lock even if the host suspends animation end events.
    const fallback = window.setTimeout(finish, SKIP_FADE_MS + 150);
    return () => window.clearTimeout(fallback);
  }, [visible, exiting, finish]);

  useLayoutEffect(() => {
    if (!visible) return;
    const app = document.querySelector<HTMLElement>(".studio-root");
    const previousFocus = document.activeElement;
    const wasInert = app?.inert ?? false;
    const wasHidden = app?.getAttribute("aria-hidden");
    const lockup = document.querySelector<HTMLElement>(".startup-lockup");
    const title = document.querySelector<HTMLElement>(".startup-wordmark");
    let active = true;
    const centerTitle = () => {
      if (active && lockup && title) {
        // Re-measure after the bundled font loads, before the title's reveal.
        lockup.style.setProperty("--title-offset", `${(title.offsetWidth + 8.5) / 2}px`);
      }
    };
    centerTitle();
    void document.fonts.load('600 82px "X20CTL Wordmark"', "20CTL").then(centerTitle, () => {});
    if (app) {
      app.inert = true;
      app.setAttribute("aria-hidden", "true");
    }
    document.querySelector<HTMLButtonElement>(".startup-skip")?.focus();
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const onMotion = () => { if (motion.matches) finish(); };
    const onVisibility = () => { if (document.hidden) finish(); };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        skip();
      }
    };
    window.addEventListener("keydown", onKey);
    motion.addEventListener("change", onMotion);
    document.addEventListener("visibilitychange", onVisibility);
    // Fallback also clears the layer if WebView animation events are suspended.
    const fallback = window.setTimeout(finish, INTRO_MS + 300);
    return () => {
      active = false;
      window.clearTimeout(fallback);
      window.removeEventListener("keydown", onKey);
      motion.removeEventListener("change", onMotion);
      document.removeEventListener("visibilitychange", onVisibility);
      if (app) {
        app.inert = wasInert;
        if (wasHidden === null || wasHidden === undefined) app.removeAttribute("aria-hidden");
        else app.setAttribute("aria-hidden", wasHidden);
      }
      if (previousFocus instanceof HTMLElement && previousFocus !== document.body && previousFocus.isConnected) {
        previousFocus.focus({ preventScroll: true });
      }
    };
  }, [visible, finish, skip]);

  if (!visible) return null;
  return (
    <section
      ref={intro}
      className={`startup-intro${exiting ? " is-exiting" : ""}`}
      style={{ "--intro-exit-duration": `${SKIP_FADE_MS}ms` } as CSSProperties}
      aria-label="X20CTL startup"
      onAnimationEnd={(event) => {
        if (event.target === event.currentTarget && ["startup-reveal", "startup-skip-exit"].includes(event.animationName)) finish();
      }}
    >
      <div className="startup-space" aria-hidden="true">
        <div className="startup-nebula" />
        <div className="startup-stars" />
      </div>
      <div className="startup-lockup" aria-hidden="true">
        <div className="startup-emblem">
          <div className="startup-energy" />
          {particles.map((style, index) => <i className="startup-particle" key={index} style={style} />)}
          <div className="startup-wave" />
          <div className="startup-impact" />
          <BrandX animated />
        </div>
        <span className="startup-wordmark">20CTL</span>
      </div>
      <button className="startup-skip" onClick={skip} disabled={exiting}>Skip intro <kbd>Esc</kbd></button>
    </section>
  );
}
