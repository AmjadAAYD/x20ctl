import { useEffect, useLayoutEffect, useRef, type ReactNode } from "react";

/** Decorative CSS only. No render loop competes with the native input bridge. */
export function AmbientSpace() {
  useEffect(() => {
    const update = () => {
      document.documentElement.dataset.motionPaused = String(
        document.hidden || !document.hasFocus(),
      );
    };
    update();
    window.addEventListener("focus", update);
    window.addEventListener("blur", update);
    document.addEventListener("visibilitychange", update);
    return () => {
      window.removeEventListener("focus", update);
      window.removeEventListener("blur", update);
      document.removeEventListener("visibilitychange", update);
      delete document.documentElement.dataset.motionPaused;
    };
  }, []);
  return (
    <div className="ambient-space" aria-hidden="true">
      <div className="ambient-nebula" />
    </div>
  );
}

/** Move one decorative light; the navigation targets never move. */
export function MotionNav({
  active,
  children,
}: {
  active: string;
  children: ReactNode;
}) {
  const ref = useRef<HTMLElement>(null);
  const light = useRef<HTMLSpanElement>(null);
  useLayoutEffect(() => {
    const nav = ref.current;
    const indicator = light.current;
    if (!nav || !indicator) return;
    const update = () => {
      const selected = nav.querySelector<HTMLElement>('[aria-current="page"]');
      if (!selected) return;
      const bounds = selected.getBoundingClientRect();
      const frame = nav.getBoundingClientRect();
      indicator.style.transform = `translate(${bounds.left - frame.left - nav.clientLeft + nav.scrollLeft}px, ${bounds.bottom - frame.top - nav.clientTop + nav.scrollTop - 2}px)`;
      indicator.style.width = `${selected.offsetWidth}px`;
      indicator.style.height = '2px';
      indicator.dataset.ready = "true";
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(nav);
    return () => observer.disconnect();
  }, [active]);
  return (
    <nav ref={ref} className="chassis-nav motion-nav" aria-label="Workspace" onKeyDown={(event) => {
      if (!['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
      const buttons = Array.from(event.currentTarget.querySelectorAll<HTMLButtonElement>('button:not(:disabled)'));
      const index = buttons.indexOf(document.activeElement as HTMLButtonElement);
      if (index < 0) return;
      event.preventDefault();
      const next = event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1
        : (index + (['ArrowDown', 'ArrowRight'].includes(event.key) ? 1 : -1) + buttons.length) % buttons.length;
      buttons[next]?.focus();
    }}>
      <span ref={light} className="nav-active-light" aria-hidden="true" />
      {children}
    </nav>
  );
}
