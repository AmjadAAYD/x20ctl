# Privacy

This policy applies to `tools/controller-scan-kit`, not the desktop configurator or its
integrated helper. The standalone scanner implements no Internet upload or telemetry.

The kit reads Windows APIs after consent, correlates a controller using disconnected/connected
inventories and volunteer confirmation, and saves selected data under a local `results/scan-…`
folder. Windows device paths and BLE addresses are needed internally for correlation but are
excluded from normal exported metadata. Discovery can transiently observe other devices.

Input/BLE stages are optional. Raw HID collection requires a separate affirmative answer.
Raw protocol import is offered only in `--app-capture`, where the volunteer explicitly
chooses a local file and confirms inclusion. Neither raw HID nor protocol bytes are
guaranteed anonymous. Imported captures can contain private or unrelated data. The
manifest and review summary identify this exception. See [the complete table](DATA_COLLECTED.md).

After collection, the volunteer reviews `BEFORE_YOU_SEND.txt` and the actual files. A ZIP
is created only after an affirmative review/package prompt. Cancellation leaves the local
folder; no unreviewed ZIP is automatically created. Nothing is sent automatically. The
volunteer manually chooses whether to share it privately with Amjad.

The kit has no remote retention system or accounts. Local files remain until the owner
deletes them. Sending a ZIP elsewhere creates a copy held by its recipient; this tool
does not control that recipient's retention. Do not upload captures to public issues.

Sanitization is a best-effort metadata filter, not exhaustive anonymization. Owner text,
device-provided names and arbitrary raw payloads require human review. The Windows/Bluetooth
stack and separately chosen capture/settings tools have their own behavior and policies.
