# Macros — purpose-built sequence workspace

The owner approved Controller Zone, Buttons and Curves, directing the next milestone to Macros with a timeline/sequence workspace rather than copying the Curves split. Preserve the shared native frame, typography, atmosphere, contextual footer, detached focus and reduced-motion policy.

## Composition

- A compact slot rail selects M1–M4; an owned rear-art thumbnail illuminates the exact selected paddle contour. The timeline is the primary editing object, spanning the workspace width.
- A horizontal sequence shelf uses readable event targets. Below it, separate lanes show held buttons, left-stick direction, right-stick direction and release/pause intervals against a common time ruler. Selected event and preview playhead are distinct.
- A contextual dock edits one selected event: held-button chord, two stick directions, hold and following pause. Add, remove and move events without table rows or per-event forms.
- Empty slots show a clear create-sequence action. A local timeline preview animates the playhead only; it never injects keyboard/controller input. Recording remains unavailable until native input and capture ownership exist.

## Reference and truth

Use the existing React macro model and Python MacroStep/limits as separate UI/protocol references. A UI event contains buttons, explicit neutral or directional sticks, hold duration and following pause. The reference uses 5 ms timing units and a 47-entry payload ceiling; nonzero pauses consume release entries. Validate the local draft accordingly without implementing a C# packet encoder.

Keep four independent, in-session local drafts per player/model. Hardware contents remain unknown; Apply remains disabled. The rear UI label order is owner-approved artwork identity, not proof of wire slot ordering. Preserve neutral stick semantics; never encode a zero button mask as a verified release. Protocol framing, engine parity, hardware recording/replay, persistence and import/export remain later work.

## Review

Check empty and populated sequences, every slot, chords, directions, timing, add/remove/reorder, capacity rejection, local preview versus hardware state, draft retention and model guards. Capture minimum/maximized, selection/focus, reduced motion and timed sequence motion. Stop for owner review before the next page.
