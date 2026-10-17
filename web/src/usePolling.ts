import { useEffect, useRef } from 'react';

/** Calls `callback` now and then every `intervalMs` while `enabled`. */
export function usePolling(callback: () => void | Promise<void>, intervalMs: number, enabled = true) {
  const saved = useRef(callback);
  saved.current = callback;

  useEffect(() => {
    if (!enabled) return;
    void saved.current();
    const id = setInterval(() => void saved.current(), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs, enabled]);
}
