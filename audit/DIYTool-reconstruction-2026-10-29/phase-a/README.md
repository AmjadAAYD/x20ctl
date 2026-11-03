# Phase A evidence

The independent derivation script uses either the retained research captures or the committed synthetic packet/asset fixtures. It imports no Screen implementation.

The fidelity script needs the private original-tool cropped-output tree. Run `python measure_fidelity.py <offscreen-review-directory> <research-root>` with that root containing `ground-truth/outputs`. Both scripts are read-only with respect to input captures; the fidelity report writes its own JSON/contact sheet under `phase-a`.

Original vendor executables, DLLs, firmware, package archives and manufacturer artwork are outside this checkout. The data here is a derived flash inventory, independent checksum comparison and synthetic-image fidelity measurements. The offscreen render PNGs stay local under `artifacts/screen-feature`; they are not release assets.
