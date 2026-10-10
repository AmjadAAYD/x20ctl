"""Standalone entry point, shipped unchanged in the auditable source kit."""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from controller_scan_kit.cli import main

if __name__ == "__main__": raise SystemExit(main())
