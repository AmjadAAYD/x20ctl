# Scanner bug audit - local validation, 8 October 2026

Changes are local and not part of public Preview 2. No controller or screen access, device commands, release build, or publication occurred.

## Fixed

- Integrated and standalone XInput summaries now check the requested standard button/trigger/stick instead of treating arbitrary stick movement as a successful button test. Unrelated changes retain their raw fields and are explicitly labelled. Existing remaps can produce a different logical button, so this is a standard-output check, not proof that a physical button failed.
- Mixed APIs or XInput slots cannot be summarized as one controller action.
- Failed, cancelled, empty, unchanged, unrelated, and source-changing action captures do not receive observed coverage. A quiet neutral capture remains valid. Later successes retain earlier category limitations.
- Capability-discovery import/removal regression now initializes the attachment scope normally created during scan start and imports its referenced discover function.

## Evidence

121 focused Python tests passed across scanner input regressions, capability discovery, standalone collector, integrated scanner and partial-input phase 1. Scoped Ruff F checks and git diff --check passed.

Synthetic native Windows input bytes decoded correctly in both reader implementations, with 12-byte gamepad and 16-byte state structures. No live DLL/device read was performed.

Offline replay of the supplied receiver capture now labels the requested control tests as unrelated changes rather than confirming button/trigger activity. Existing evidence files were read only.

Pre-existing modified files were backed up under C:/Users/amjad/AppData/Local/Temp/x20ctl-scanner-audit-ewgnat_3. Untargeted backed-up files remained byte-identical. Intentional UI/capability-discovery work was preserved.

## Remaining uncertainty

The owner captures still do not establish why buttons/triggers were zero. Hardware input acceptance, native GUI behaviour, packaged EXE behaviour, and the upcoming wired capture were not tested. Unknown HID byte changes still cannot establish specific controls without a verified layout. Remapping/configuration backends remain disabled.
