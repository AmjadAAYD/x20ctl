type Buttons = Partial<Record<string, boolean>>;
export function navigationAction(input: { buttons: Buttons; x: number; y: number }, previous: Buttons, blocked = false) {
  if (blocked) return null;
  for (const [key, action] of [['A', 'activate'], ['B', 'back'], ['LB', 'previous-tab'], ['RB', 'next-tab']] as const) {
    if (input.buttons[key] && !previous[key]) return action;
  }
  if (input.buttons.DPAD_UP) return 'up';
  if (input.buttons.DPAD_DOWN) return 'down';
  if (input.buttons.DPAD_LEFT) return 'left';
  if (input.buttons.DPAD_RIGHT) return 'right';
  if (Math.max(Math.abs(input.x), Math.abs(input.y)) < .6) return null;
  return Math.abs(input.x) > Math.abs(input.y) ? input.x > 0 ? 'right' : 'left' : input.y > 0 ? 'down' : 'up';
}
