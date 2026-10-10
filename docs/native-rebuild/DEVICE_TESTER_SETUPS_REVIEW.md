# Device, Tester and Setups owner review — 9 October 2026

Historical initial review. Owner accepted Device/Tester directions but rejected the initial Setups presentation. See LIBRARY_REFINEMENT_REVIEW.md for the subsequent visual revision and current evidence.

The three requested native UI pages are implemented. Stop here for consolidated owner review. Earlier approved pages retain their compositions; this milestone adds management pages and shared navigation/capture/setup integration. No Scanner, controller-protocol or release work.

## Device

Large artwork anchors the assigned model/player overview. Gameplay and configuration each have a separate offline status. Battery, firmware, hardware revision, transport and idle settings remain unavailable. Model-reference knowledge is distinguished from a physical identity and native session. The actions open Tester and My setups without hardware commands. X15 remains a preview/research target and does not inherit X20 commands or native setup editing.

## Tester

Buttons and Tester both use ControlRegions.Owned; no alternate coordinate map was introduced. Review fixtures demonstrate actual A/D-pad/stick regions, stick positions and LT/RT meters. Independent paddle illumination requires source exposure: the M1 fixture without independent exposure stays unlit.

Normal operation waits for native gameplay input. The C++ input bridge is not connected: all input evidence is labelled SIMULATION. Hardware input, physical gamepad navigation and latency remain unverified. Tester owns its capture context; frames do not dispatch UI navigation. Keyboard A/B/Y cannot navigate while capturing; Enter on Exit Tester or Escape provides deliberate keyboard exit. Its footer uses keyboard glyphs. Leaving clears frames and ownership. No scanner workflow is exposed.

## Setups

Library cards support create, save current draft, select, duplicate, rename, confirmed deletion, native JSON import/export and opening a compatible setup in the local editor. Open setup belongs to the current player/model for this app session. Editor modifications remain distinct from the saved copy. Opening is never hardware application. Native editing is limited to X20.

Delete confirmation initially focuses Keep setup. Keyboard navigation stays within its choices, unrelated actions are suppressed, Escape cancels, and explicit confirmation removes only the native entry. Original legacy files and backups remain.

Separate native storage: `%APPDATA%/x20ctl/native/setups.v1.json`. Mutations validate, acquire an exclusive lock, check the baseline hash, back up/verify previous bytes, flush/reopen/validate a same-directory temporary file, then atomically replace. Future/malformed formats and stale-instance conflicts preserve originals. Unknown library/setup/snapshot fields are retained. Identical native identities/payloads import idempotently; changed payloads conflict.

Legacy import is an explicit opaque copy, never automatic semantic conversion. It retains original JSON, exact-byte source backups and provenance manifests, uses stable source identity/hash, and rejects changed sources. Unverified legacy semantics cannot load into the editor; original-payload export remains available. Semantic migration of legacy mappings/curves/macros/preferences is deferred. Crash-injection/power-loss acceptance at every transaction boundary is not claimed.

Actual desktop profiles were read only for inventory/hash preservation. SHA-256 still equals the pre-work checkpoint: `F01F5573D76768794ACD86C0AE1299EA7FDD9D23C0CCBFDC0C4D064F1C6038B2`. CRUD/migration review operations used isolated fixture libraries. No original user file was migrated, deleted or rewritten.

## Executed evidence

| Check | Result |
| --- | --- |
| Latest Release WPF build | Passed; 0 warnings, 0 errors |
| Native domain suite | 68 passed, 0 failed |
| Management WPF review | 26 passed checks, 33 rendered states; zero binding errors |
| Frozen-page responsive regression | 52 passed checks, 54 rendered states; zero binding errors |
| New-page layouts | Maximized, restored, minimum, reduced motion and focus captured |
| Root-DPI matrix | All three pages at 100/125/150%, 1920×1040 render dimensions |
| Motion | 36 WPF frames at 40 ms intervals; Device → simulation Tester → Setups |
| Preservation hashes | 43 original desktop-dotnet entries, 3 Zone/theme files, CurveWorkbench and original desktop profiles: 48 matches |
| Tracked diff whitespace | git diff --check passed |

The original desktop-dotnet entries include metadata and retained authored prototypes. The older approved Macros checkpoint predates subsequent approved responsive/47-event fixes, so it is not a byte-identical current baseline; that historical difference is recorded separately. MacroWorkbench and VibrationWorkbench were not edited in this management milestone.

Primary bundle: `artifacts/management-native/owner-review`:

- `device-maximized.png`
- `tester-offline-maximized.png`
- `tester-fixture-front.png` / `tester-fixture-rear.png` — simulation
- `setups-library-maximized.png`, `setups-modified.png`, `setups-delete-confirmation.png`
- `setups-legacy-preserved.png`
- `management-transitions-simulation.gif`
- `review.json` and `bindings.txt`

Domain report: `artifacts/management-native/tests/management-domain.trx`.
Shared regression: `artifacts/management-native/frozen-regression/review.json`.
Preservation: `artifacts/management-native/preservation.json`.

Captures are shown app-owned WPF client renders plus labelled detached root-DPI renders. No OS input injection or system scaling changes were used. Work-area startup remains verified on the available single monitor; physical cross-monitor/DPI transitions and couch-distance owner acceptance remain unverified.

Apply remains disabled. No physical controller access, C++ engine parity, hardware read/write, motor output, independent paddle discovery, Scanner port, public upload, commit, push, tag or release occurred.
