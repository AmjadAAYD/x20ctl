import { useId } from 'react';
import { controller, type ControllerId, type Point } from '../controllers';
import { controllerSilhouette, LIGHTING_VIEWBOX } from '../controller-lighting';
import { controlShapes, CONTROL_SOURCE_SIZE } from '../controller-controls';
import type { KeyName } from '../types/gamepad';

/** Transparent model-specific illustration; never establishes detected identity. */
export function ControllerOutline({ model, view = 'front', buttons = {}, leftStick = { x: 0, y: 0 }, rightStick = { x: 0, y: 0 }, selectedKey, selectedMacro, leftTrigger = 0, rightTrigger = 0 }: {
  model: ControllerId; view?: 'front' | 'back'; buttons?: Partial<Record<KeyName, boolean>>;
  leftStick?: Point; rightStick?: Point; selectedKey?: KeyName | null; selectedMacro?: string;
  leftTrigger?: number; rightTrigger?: number;
}) {
  const id = useId().replace(/:/g, '');
  const profile = controller(model), shapes = controlShapes(model);
  const [width, height] = CONTROL_SOURCE_SIZE;
  const palette: Record<string, string> = { A: '#27e4a2', B: '#ff6b92', X: '#25cafa', Y: '#edcf64' };
  const controls = view === 'front' ? shapes.front : { ...shapes.back, ...shapes.macros };
  return <svg className="controller-outline" viewBox={LIGHTING_VIEWBOX} aria-hidden="true" focusable="false" data-outline-model={model} data-outline-view={view}>
    <defs>
      <linearGradient id={`${id}-shell`} x1="0" x2="1" y1="0" y2="1"><stop stopColor="#6ba9ff" /><stop offset=".5" stopColor="#568bcc" /><stop offset="1" stopColor="#90bded" /></linearGradient>
      <filter id={`${id}-light`} x="-70%" y="-70%" width="240%" height="240%"><feGaussianBlur stdDeviation="8" /></filter>
    </defs>
    <path className="outline-shell-light" d={controllerSilhouette(model, view)} stroke={`url(#${id}-shell)`} fill="none" filter={`url(#${id}-light)`} />
    <path className="outline-shell" d={controllerSilhouette(model, view)} stroke={`url(#${id}-shell)`} fill="#071a34" fillOpacity=".3" />
    {Object.entries(controls).map(([key, shape]) => {
      if (!shape) return null;
      const [x, y, w, h] = shape.bounds;
      const pressed = !!buttons[key as KeyName], selected = selectedKey === key || selectedMacro === key;
      const trigger = key === 'LT' ? leftTrigger : key === 'RT' ? rightTrigger : 0;
      const color = palette[key] ?? '#78afff';
      const label = key.startsWith('DPAD') ? '' : key === 'SELECT' ? 'View' : key === 'START' ? 'Menu' : key;
      return <g key={key} className={`outline-control ${pressed ? 'is-pressed' : ''} ${selected ? 'is-selected' : ''}`} data-outline-control={key}>
        {(pressed || selected || trigger > .01) && <path d={shape.path} transform={shape.transform} stroke={color} strokeWidth="16" fill="none" opacity=".45" filter={`url(#${id}-light)`} />}
        <path d={shape.path} transform={shape.transform} stroke={color} strokeWidth={pressed || selected ? 4 : 2} fill={color} fillOpacity={pressed ? .3 : selected ? .15 : trigger * .22} />
        {label && <text x={x + w / 2} y={y + h / 2} textAnchor="middle" dominantBaseline="central" fill={color} fontSize={label.length > 3 ? 19 : 42} fontFamily="Segoe UI, sans-serif">{label}</text>}
      </g>;
    })}
    {view === 'front' && (['left', 'right'] as const).map(side => {
      const geometry = profile.visual.sticks[side], vector = side === 'left' ? leftStick : rightStick;
      const key = side === 'left' ? 'L3' : 'R3';
      const cx = geometry.x * width, cy = geometry.y * height, radius = geometry.radius * width;
      return <g key={side} data-outline-stick={side}>
        <circle cx={cx} cy={cy} r={radius} fill="#173455" fillOpacity=".28" stroke="#8fb6e6" strokeWidth="2" />
        <circle cx={cx} cy={cy} r={radius * .82} fill="none" stroke="#336292" strokeWidth="2" />
        <circle cx={cx + vector.x * radius * .24} cy={cy + vector.y * radius * .24} r={radius * .58} fill="#153452" fillOpacity=".8" stroke={buttons[key] || selectedKey === key ? '#e3f4ff' : '#5289c4'} strokeWidth="3" />
        <circle cx={cx + vector.x * radius * .24} cy={cy + vector.y * radius * .24} r="9" fill="#c6efff" />
      </g>;
    })}
    <text x={width * .5} y={height * .19} textAnchor="middle" fill="#92bff7" fontFamily="Segoe UI, sans-serif" fontSize="25" letterSpacing="3">{profile.name.toUpperCase()}</text>
  </svg>;
}
