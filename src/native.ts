interface NativeResult<T> {
  ok: boolean;
  data: T;
  error?: { code: string; message: string };
}
declare global {
  interface Window {
    pywebview?: {
      api: {
        request: <T>(
          operation: string,
          payload: object,
        ) => Promise<NativeResult<T>>;
      };
    };
  }
}

export async function request<T>(
  operation: string,
  payload: object = {},
): Promise<T> {
  if (!window.pywebview?.api)
    throw new Error("Open x20ctl.exe to use the desktop connection.");
  const result = await window.pywebview.api.request<T>(operation, payload);
  if (!result.ok)
    throw new Error(result.error?.message || "The desktop operation failed.");
  return result.data;
}

export function whenNativeReady(callback: () => void): () => void {
  if (typeof window.pywebview?.api?.request === "function") callback();
  else window.addEventListener("pywebviewready", callback, { once: true });
  return () => window.removeEventListener("pywebviewready", callback);
}
