import { useEffect, useId, useRef, type ReactNode } from "react";
import { X } from "lucide-react";

interface Props {
  title: string;
  subtitle?: string;
  closeLabel?: string;
  className?: string;
  onClose: () => void;
  children: ReactNode;
}

export function MetalDialog({
  title,
  subtitle,
  closeLabel = "Close dialog",
  className = "",
  onClose,
  children,
}: Props) {
  const ref = useRef<HTMLElement>(null);
  const titleId = useId();
  const close = useRef(onClose);
  close.current = onClose;
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    ref.current?.focus();
    const keydown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        event.preventDefault();
        close.current();
      }
      if (event.key !== "Tab") return;
      const elements = [
        ...(ref.current?.querySelectorAll<HTMLElement>(
          "button:not(:disabled), input:not(:disabled), select:not(:disabled), a[href], [tabindex='0']",
        ) ?? []),
      ].filter((el) => el.getClientRects().length);
      const first = elements[0],
        last = elements[elements.length - 1];
      if (!first) {
        event.preventDefault();
        return;
      }
      if (
        event.shiftKey &&
        (document.activeElement === first ||
          document.activeElement === ref.current)
      ) {
        event.preventDefault();
        last.focus();
      } else if (
        !event.shiftKey &&
        (document.activeElement === last ||
          document.activeElement === ref.current)
      ) {
        event.preventDefault();
        first.focus();
      }
    };
    document.addEventListener("keydown", keydown);
    return () => {
      document.removeEventListener("keydown", keydown);
      previous?.focus();
    };
  }, []);
  return (
    <div className="metal-modal-backdrop">
      <section
        ref={ref}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className={`metal-dialog ${className}`}
      >
        <header className="metal-dialog-header">
          <div>
            {subtitle && <span className="engraved">{subtitle}</span>}
            <h2 id={titleId}>{title}</h2>
          </div>
          <button
            className="icon-button"
            aria-label={closeLabel}
            onClick={onClose}
          >
            <X size={19} />
          </button>
        </header>
        <div className="metal-dialog-content">{children}</div>
      </section>
    </div>
  );
}
