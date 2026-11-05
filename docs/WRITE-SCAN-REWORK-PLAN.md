# Write scan rework (5 Nov 2026)

Replaces the phone/KeyLinker-log Write scan with an in-app scanner that talks to the pad itself.

## Flow
1. **Choose** a controller (same list as the Read scan) and read a notice of what will happen.
2. **Identify** (safe, one click): ask 0x91 (identity), 0xB0 (capability menu), 0x93 (updater id). Verdict from the replies, not a model list: *answers like an X20*, *like an X20 Pro*, *answers differently*, *doesn't answer*.
3. **Read-back** (safe): only for pads that answer like the X20 family. Read remap, sticks, triggers and macro records; show them; save every record to a file.
4. **Write test** (visible "this sends a write" confirmation, only after 2 passed): per record, write back the exact bytes just read, read again. Learn: acknowledged? identical after a fresh connection? accepts the X20 command? Mismatch -> stop and restore from the saved bytes.
5. **Visual tests**: button map (XYAB layout), curves, triggers, vibration, macro reading. Each records pass/fail/notes.
6. **Package**: zip saved locally; sent to X20CTLADMIN only after review and explicit consent.

## Success criteria
- Old Write scan code (views, theme variants, report, KeyLinker-log tests, copy) is gone; no "KeyLinker log"/phone-recording text remains.
- Pure logic in X20Ctl.Zone with unit tests: verdict classification (4 outcomes), stage opcode allow-lists (identify cannot write, read-back cannot write, write only 0x31/32/35/36), write-back judge (acked+identical, no ack, mismatch -> restore needed, unreadable), report zip (manifest hashes, no MAC/PC name).
- `dotnet build` of Product and `dotnet test X20Ctl.Zone.Tests` pass.
- Offscreen render (no window on screen) of each step.
- Report separates tested from needs-a-real-pad: no BLE traffic is exercised without hardware.
