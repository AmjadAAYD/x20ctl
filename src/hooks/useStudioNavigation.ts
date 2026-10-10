import { useEffect, useRef } from 'react';
import type { LiveGamepadState } from '../types/gamepad';
import { navigationAction } from '../studio-navigation';

/** Reads existing samples; only marked navigation targets can activate. */
export function useStudioNavigation(input: LiveGamepadState, enabled: boolean, blocked: boolean, onBack: () => void) {
  const latest = useRef({ input, enabled, blocked, onBack });
  latest.current = { input, enabled, blocked, onBack };
  useEffect(() => {
    let previous: Partial<Record<string, boolean>> = {};
    let lastMove = 0;
    let wasBlocked = true;
    const timer = window.setInterval(() => {
      const state = latest.current;
      const app = document.querySelector<HTMLElement>('.rebranded-workspace');
      const focused = document.activeElement as HTMLElement | null;
      const editing = !!focused?.matches('input, select, textarea, [contenteditable="true"]');
      const suspended = !state.enabled || state.blocked || editing || document.hidden || !document.hasFocus() || !!document.querySelector('[role="dialog"]');
      const sample = { buttons: state.input.buttons, x: state.input.leftStick.x, y: state.input.leftStick.y };
      const action = navigationAction(sample, previous, suspended || wasBlocked);
      previous = { ...sample.buttons };
      wasBlocked = suspended;
      if (!app || !action) return;
      const targets = Array.from(app.querySelectorAll<HTMLElement>('[data-gamepad-action]:not(:disabled)')).filter(el => el.getClientRects().length);
      if (!targets.length) return;
      if (action === 'activate') {
        if (focused && targets.includes(focused)) focused.click();
      } else if (action === 'back') {
        const overview = app.querySelector<HTMLButtonElement>('[data-macro-back]');
        if (overview && !overview.disabled) overview.click();
        else state.onBack();
      } else if (action === 'next-tab' || action === 'previous-tab') {
        const tabs = targets.filter(el => el.closest('.chassis-nav'));
        const index = tabs.findIndex(el => el.getAttribute('aria-current') === 'page');
        const next = tabs[(index + (action === 'next-tab' ? 1 : -1) + tabs.length) % tabs.length];
        next?.focus(); next?.click();
      } else if (performance.now() - lastMove > 220) {
        lastMove = performance.now();
        const index = targets.indexOf(focused!);
        const offset = action === 'down' || action === 'right' ? 1 : -1;
        targets[index < 0 ? 0 : (index + offset + targets.length) % targets.length]?.focus();
      }
    }, 80);
    return () => window.clearInterval(timer);
  }, []);
}
