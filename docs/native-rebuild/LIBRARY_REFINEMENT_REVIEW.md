# Setups library refinement owner review — 9 October 2026

Setups has been substantially revised; stop here for owner review. Device received hierarchy/copy polish. Tester retains its approved architecture with taller trigger meters. Earlier approved pages, persistence code and hardware boundaries remain intact.

## Changes

The library and selected details now share one workspace. Small libraries use larger cards; populated libraries fit four cards across the observed maximized window. Card heights remain 350 DIP for small libraries, 320 DIP for fuller libraries and 280 DIP in compact views. Width remains readable instead of shrinking to fit every item. Library scrolling snaps by rows and follows focused/selected cards. Smaller windows also scroll the detail workspace.

Cards show actual stored model, remap/changed-curve/macro counts and vibration strength. LOCAL, CURRENT, LOCAL CHANGES, OTHER MODEL and PRESERVED COPY are distinct. Current means loaded into the local editor, never hardware-applied. The metrics show the saved snapshot; a modified draft is identified separately and the saved copy remains intact. No modification dates or hardware values were invented.

Selected details sit immediately beneath the library: identity/compatibility, four saved metrics, Open/Duplicate/Rename/Export Setup, and explicit local-storage truth. Rename reveals a contextual editor. Import Setup/Export Setup retain JSON internally. Delete is inside More actions, with an explicit confirmation and Keep setup as the initial focus. Keyboard focus remains confined during confirmation. Card hover preserves selected fill; detached focus remains distinct from selection.

Device keeps the large controller and separate gameplay/configuration states. SUPPORTED BY X20CTL replaces developer-facing Model reference copy, with app/reference support explicitly distinguished from a connected device. DEVICE INFORMATION groups unavailable battery/firmware/revision/transport. Technical identity/authorization caveats are under Details; power settings remain unavailable. Minimum-size secondary details scroll.

Tester still uses Buttons' owned geometry, simultaneous hotspot illumination, analog visuals, capture ownership and deliberate exit. LT/RT meters increased from 20 to 28 DIP. Normal operation shows unavailable input; Review fixture appears only in development review simulation. No native input bridge or hardware telemetry was introduced.

## Verification

- Release build passed with zero warnings/errors.
- 68 domain tests passed. Persistence/domain implementations are unchanged.
- Final WPF review: 39 passed checks, 44 rendered states, zero binding errors.
- Covered zero/one/two/14 profiles, selected/current/modified/incompatible/legacy states, menu-driven deletion/cancel/confirmation, native/legacy round trips and card focus distinct from selection.
- All management pages include maximized/restored/minimum/reduced-motion captures. Populated Setups is included in 100/125/150% root-DPI renders.
- Return navigation verifies that Buttons, Curves, Macros and Vibration regain their approved separate body/shelf layout after leaving Setups.
- A 36-frame page-transition sequence is recorded; its Tester frames are labelled simulation.
- Six SHA-256 comparisons confirm unchanged SetupStore, SetupSnapshot, Curves, Macros, Vibration and actual legacy desktop profiles. See `artifacts/library-refinement/preservation.json`.
- The initial console-card sizing check failed against the previous 300×190 DIP cards, then passed after refinement.

Primary evidence: `artifacts/library-refinement/review-ready/review.json`. Tests: `artifacts/library-refinement/tests/library-domain.trx`. Root-DPI renders are detached WPF renders, not physical monitor scaling transitions. All captures use app-owned WPF rendering; no desktop input injection. Real hardware input/navigation, engine parity, motor output, physical multi-monitor transitions and couch-distance acceptance remain unverified. Apply remains disabled.

## Requested maximized screenshots

Device:

![Device](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/device-maximized.png)

Tester — explicitly simulated input:

![Tester simulation](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/tester-fixture-front.png)

Setups with two profiles:

![Two profiles](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/setups-library-maximized.png)

Setups with 14 profiles:

![Many profiles](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/setups-many-maximized.png)

Selected/current setup detail:

![Selected detail](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/setups-selected-detail.png)

Empty library:

![Empty library](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/setups-empty-maximized.png)

Delete confirmation:

![Delete confirmation](C:/Users/amjad/Desktop/Study/Projects/Personal/x20ctl/artifacts/library-refinement/review-ready/setups-delete-confirmation.png)

Additional evidence includes `setups-card-focus.png`, `setups-one-maximized.png`, `setups-incompatible-maximized.png`, `setups-many-last-selected.png`, `setups-many-minimum-detail.png`, the root-DPI matrix and `management-transitions-simulation.gif`.

No real profile was migrated or rewritten. No Scanner/protocol work, release, publish, push, tag or binary upload occurred.
