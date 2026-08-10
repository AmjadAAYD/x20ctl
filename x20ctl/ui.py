"""Terminal styling.

Small and self-contained: enables ANSI on Windows consoles, degrades to plain
text when output is redirected, and provides the few primitives the CLI needs.
"""

from __future__ import annotations

import os
import sys

_ENABLED = False


def _enable_ansi() -> bool:
    """Turn on virtual terminal processing on Windows; no-op elsewhere.

    Under pythonw.exe, which is what a windowed entry point runs, sys.stdout is
    None rather than a stream. Touching it raises and takes the whole app down
    before it draws anything, so guard before asking about the terminal.
    """
    stream = getattr(sys, "stdout", None)
    if stream is None or not hasattr(stream, "isatty"):
        return False
    try:
        if not stream.isatty():
            return False
    except (ValueError, OSError):
        return False
    if os.name != "nt":
        return True
    try:
        import ctypes

        kernel32 = ctypes.windll.kernel32
        # -11 is STD_OUTPUT_HANDLE, 0x0004 is ENABLE_VIRTUAL_TERMINAL_PROCESSING
        handle = kernel32.GetStdHandle(-11)
        mode = ctypes.c_ulong()
        if not kernel32.GetConsoleMode(handle, ctypes.byref(mode)):
            return False
        return bool(kernel32.SetConsoleMode(handle, mode.value | 0x0004))
    except Exception:
        return False


_ENABLED = _enable_ansi()


def _code(seq: str) -> str:
    return seq if _ENABLED else ""


RESET = _code("\033[0m")
BOLD = _code("\033[1m")
DIM = _code("\033[2m")

WHITE = _code("\033[97m")
GREY = _code("\033[90m")
CYAN = _code("\033[36m")
GREEN = _code("\033[32m")
YELLOW = _code("\033[33m")
RED = _code("\033[31m")
BLUE = _code("\033[34m")
MAGENTA = _code("\033[35m")

WIDTH = 62


def title(text: str) -> str:
    return f"{BOLD}{WHITE}{text}{RESET}"


def label(text: str) -> str:
    return f"{GREY}{text}{RESET}"


def value(text: str) -> str:
    return f"{WHITE}{text}{RESET}"


def ok(text: str) -> str:
    return f"{GREEN}{text}{RESET}"


def warn(text: str) -> str:
    return f"{YELLOW}{text}{RESET}"


def bad(text: str) -> str:
    return f"{RED}{text}{RESET}"


def accent(text: str) -> str:
    return f"{CYAN}{text}{RESET}"


def rule(char: str = "─") -> str:
    return f"{GREY}{char * WIDTH}{RESET}"


def header(text: str, subtitle: str = "") -> str:
    """A section heading with a rule beneath it."""
    out = [f"\n{BOLD}{CYAN}{text}{RESET}"]
    if subtitle:
        out.append(f"{GREY}{subtitle}{RESET}")
    out.append(rule())
    return "\n".join(out)


def field(name: str, val: str, width: int = 16) -> str:
    return f"  {GREY}{name.ljust(width)}{RESET}{WHITE}{val}{RESET}"


def bar(percent: int, width: int = 24) -> str:
    """A proportion bar. Colour tracks magnitude rather than good/bad."""
    percent = max(0, min(100, percent))
    filled = round(width * percent / 100)
    colour = GREY if percent == 0 else (GREEN if percent < 67 else YELLOW)
    return (f"{colour}{'█' * filled}{RESET}{GREY}{'░' * (width - filled)}{RESET} "
            f"{WHITE}{percent}%{RESET}")


def bullet(text: str, marker: str = "•") -> str:
    return f"  {CYAN}{marker}{RESET} {text}"


def slot_line(slot: str, description: str | None) -> str:
    if description is None:
        return f"  {GREY}{slot}{RESET}  {GREY}empty{RESET}"
    return f"  {BOLD}{MAGENTA}{slot}{RESET}  {WHITE}{description}{RESET}"
