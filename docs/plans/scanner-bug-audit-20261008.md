# Scanner bug audit - 8 October 2026

Scope: offline scanner input classification, source consistency, capability import regression, and synthetic XInput structure/read verification. Preserve all existing UI and capability changes. No controller/screen access, configuration writes, build of release binaries, or publication.

1. Add regressions for stick drift mistaken for button/trigger activity and mixed input sources.
2. Correct shared summaries in integrated and standalone collectors.
3. Ensure failed/inconclusive captures do not receive observed coverage.
4. Repair the capability import test setup and undefined test reference.
5. Verify relevant scanner tests and record evidence/remaining hardware uncertainty.
