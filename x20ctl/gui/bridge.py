"""Bridge between Qt's event loop and asyncio.

bleak is asyncio; Qt has its own loop. Rather than pull in a third-party
integration, this runs an asyncio loop on a background thread and marshals
results back to the GUI thread through Qt signals, which is the only safe way to
touch widgets from other threads.

    bridge = AsyncBridge()
    bridge.run(lambda: pad.vibration(), on_done=..., on_error=...)
"""

from __future__ import annotations

import asyncio
import threading
import traceback
from typing import Any, Callable, Coroutine

from PySide6.QtCore import QObject, Signal


class _Result(QObject):
    """Carries one coroutine's outcome back to the GUI thread."""

    done = Signal(object)
    failed = Signal(str)


class AsyncBridge(QObject):
    """Owns a background asyncio loop for the lifetime of the app."""

    def __init__(self) -> None:
        super().__init__()
        self._loop = asyncio.new_event_loop()
        self._ready = threading.Event()
        self._thread = threading.Thread(target=self._run_loop, daemon=True,
                                        name="x20ctl-asyncio")
        self._thread.start()
        self._ready.wait(5)
        self._keepalive: list[_Result] = []

    def _run_loop(self) -> None:
        asyncio.set_event_loop(self._loop)
        self._loop.call_soon(self._ready.set)
        self._loop.run_forever()

    def run(
        self,
        factory: Callable[[], Coroutine[Any, Any, Any]],
        *,
        on_done: Callable[[Any], None] | None = None,
        on_error: Callable[[str], None] | None = None,
    ) -> None:
        """Schedule a coroutine and deliver its result on the GUI thread.

        `factory` is called on the asyncio thread, so coroutines are created in
        the loop that will await them.
        """
        result = _Result()
        if on_done:
            result.done.connect(on_done)
        if on_error:
            result.failed.connect(on_error)
        # Keep a reference; a garbage-collected QObject drops its signals.
        self._keepalive.append(result)

        async def wrapper() -> None:
            try:
                value = await factory()
            except Exception as exc:                     # noqa: BLE001
                detail = str(exc) or exc.__class__.__name__
                traceback.print_exc()
                result.failed.emit(detail)
            else:
                result.done.emit(value)
            finally:
                # Deferred so the signal is delivered before the ref is dropped.
                self._loop.call_later(1.0, self._forget, result)

        asyncio.run_coroutine_threadsafe(wrapper(), self._loop)

    def _forget(self, result: _Result) -> None:
        try:
            self._keepalive.remove(result)
        except ValueError:
            pass

    def shutdown(self) -> None:
        self._loop.call_soon_threadsafe(self._loop.stop)
        self._thread.join(timeout=2)
