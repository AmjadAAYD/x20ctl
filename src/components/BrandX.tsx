import { useId, type CSSProperties } from "react";
import vectorSource from "../assets/brand-x.svg?raw";

// One traced outline feeds the animated segments and the finished mark.
const paths = Array.from(vectorSource.matchAll(/\bd="([^"]+)"/g), (match) => match[1]);
const sparks = Array.from({ length: 6 }, (_, index) => ({
  "--sx": `${Math.cos(index * 1.05) * (3 + index % 2)}px`,
  "--sy": `${Math.sin(index * 1.05) * (3 + index % 2)}px`,
  "--spark-delay": `${index * 18}ms`,
}) as CSSProperties);

export function BrandX({ animated = false }: { animated?: boolean }) {
  const id = `startup-x-${useId().replace(/:/g, "")}`;
  const url = (name: string) => `url(#${id}-${name})`;
  if (!animated) return (
    <svg viewBox="0 0 51 54" aria-hidden="true">
      {paths.map((d, index) => <path key={index} d={d} fill="currentColor" fillRule="evenodd" />)}
    </svg>
  );
  return (
    <svg className="startup-logo startup-vector" viewBox="0 0 51 54" aria-hidden="true">
      <defs>
        <mask id={`${id}-rise`} maskUnits="userSpaceOnUse" x="0" y="0" width="51" height="54">
          <g transform="rotate(-40.4 14 39)">
            <rect className="startup-fill-rise" x="5" y="28" width="52" height="22" fill="white" />
          </g>
        </mask>
        <mask id={`${id}-fall`} maskUnits="userSpaceOnUse" x="0" y="0" width="51" height="54">
          <g transform="rotate(40.4 14 16)">
            <rect className="startup-fill-fall" x="5" y="5" width="52" height="22" fill="white" />
          </g>
        </mask>
        <clipPath id={`${id}-rise-clip`}><path d={paths[0]} clipRule="evenodd" /></clipPath>
        <clipPath id={`${id}-fall-clip`}><path d={paths.slice(1).join(" ")} clipRule="evenodd" /></clipPath>
        <clipPath id={`${id}-full-clip`}><path d={paths.join(" ")} clipRule="evenodd" /></clipPath>
        <linearGradient id={`${id}-edge`}>
          <stop offset="0" stopColor="#79bdff" stopOpacity="0" />
          <stop offset=".65" stopColor="white" />
          <stop offset="1" stopColor="white" stopOpacity="0" />
        </linearGradient>
      </defs>
      <g className="startup-forge-rise" mask={url("rise")}>
        <path className="startup-brand-path" d={paths[0]} fill="#eaf4ff" fillRule="evenodd" />
      </g>
      <g className="startup-forge-fall" mask={url("fall")}>
        {paths.slice(1).map((d, index) => <path className="startup-brand-path" key={index} d={d} fill="#f4f6ff" fillRule="evenodd" />)}
      </g>
      <g clipPath={url("rise-clip")}>
        <g transform="rotate(-40.4 14 39)">
          <rect className="startup-head-rise" x="5" y="28" width="8" height="22" fill={url("edge")} />
        </g>
      </g>
      <g clipPath={url("fall-clip")}>
        <g transform="rotate(40.4 14 16)">
          <rect className="startup-head-fall" x="5" y="5" width="8" height="22" fill={url("edge")} />
        </g>
      </g>
      <g clipPath={url("full-clip")}>
        <rect className="startup-finish-sheen" x="0" y="0" width="5" height="54" fill={url("edge")} />
      </g>
      {sparks.map((style, index) => (
        <g key={index} style={style}>
          <circle className="startup-spark startup-spark-rise" cx="40" cy="17" r={index % 2 ? ".12" : ".2"} />
          <circle className="startup-spark startup-spark-fall" cx="40" cy="38" r={index % 2 ? ".12" : ".2"} />
        </g>
      ))}
    </svg>
  );
}
