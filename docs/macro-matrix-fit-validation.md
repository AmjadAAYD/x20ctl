# Compact macro sequencer — 2026-10-02

The shared MacrosPage matrix now fits every step into its available width. The input column is explicitly bounded, short sequences use compact step columns, and long sequences shrink equally. All 18 input rows remain visible without an internal vertical scrollbar. Active button cells use filled markers; stick cells use direction arrows. Tooltips and accessible labels retain input names, step numbers and timing. Selecting a step continues to expose its complete inspector below the matrix. Piano Roll remains available for detailed time editing.

At smaller window widths, sequences longer than 12 steps use the whole workspace width. Toolbar actions wrap rather than compete with the title.

## Verification

- Before the change, a one-step sequence at 1920px had a 675px input column and 208px of hidden vertical table content.
- Type check and production frontend build passed.
- `tools/macro_matrix_review.cjs` passed 120 isolated production UI cases: six macro-capable models × 1/2/23/47 steps × 1920/1400/1100/1000/800px windows.
- Each case checks all 18 rows, every step column, no internal horizontal/vertical overflow, bounded input labels, and controls remaining inside the matrix.
- Per-model input toggling, last-step selection, inspector timing and stick editing passed. The 23-step fixture contains pauses and uses 46 encoded entries; the 47-step fixture contains no pauses.
- Rendered screenshots for two-step and 47-step X20 Pro tables were inspected. Evidence is in `artifacts/macro-matrix-fit/`.

This changes the shared sequencer for X20, X20 Pro, X05 Pro, X10, D10 and X15. X05 has no macro controls. It does not modify macro storage, limits, hardware commands, or controller artwork. Dense sequences show compact markers and timing on selection/hover, rather than cramming full timing text into every column.

The Windows package is saved separately as `dist/local-macro-matrix-fit-20261002/x20ctl.exe`. The actual native window was not opened or inspected, and no controller was accessed. No release, push or publication was performed.
