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
      <div className="ambient-dust" />
      <div className="ambient-stars" />
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
      const y =
        selected.getBoundingClientRect().top -
        nav.getBoundingClientRect().top -
        nav.clientTop +
        nav.scrollTop;
      indicator.style.transform = `translateY(${y}px)`;
      indicator.style.height = `${selected.offsetHeight}px`;
      indicator.dataset.ready = "true";
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(nav);
    return () => observer.disconnect();
  }, [active]);
  return (
    <nav ref={ref} className="chassis-nav motion-nav" aria-label="Workspace">
      <span ref={light} className="nav-active-light" aria-hidden="true" />
      {children}
    </nav>
  );
}
