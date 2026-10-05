# Threat model and limits

Assets: volunteers' device/privacy data, their controller configuration, local filesystem,
maintainers receiving evidence, and integrity of published scanner binaries.

| Threat | Controls | Remaining limits |
|---|---|---|
| Volunteer mistrusts developer/binary | Complete source, direct source execution, public build run, checksums, provenance | Attestation identifies a build; it does not prove code is harmless |
| Malicious/untrusted volunteer sends ZIP | Offline validation, filename/hash/schema checks, size/count limits, no extraction/execution | Data/model claims can be fabricated; use isolated analysis tools for raw captures |
| Accidental serial/path/address disclosure | Metadata allowlist, text redaction, no serial APIs, review prompt | Unknown raw payloads, names and user notes can contain identifiers |
| Nearby/unrelated device collection | Before/after selection, explicit target confirmation, selected-only export | Inventories/advertisements are observed transiently; user can select the wrong device |
| Wrong controller input source | Correlated XInput slot, gameplay-only HID selection, optional collection | Concurrent device changes can mislead correlation; isolate the controller |
| Archive traversal, symlink, bomb, nested executable | Strict names, reject links/duplicates/ZIP64, bounded reads and entry limits, manifest hashes | Parser/platform defects remain possible; no blanket guarantee for all file formats |
| Unreviewed packaging after cancellation | Tests require cancellation/declined review to leave only the folder | Volunteer must still inspect files before manually sharing them |
| Compromised dependency or build pipeline | Hash locks, action commit pins, no persistent checkout credentials, CodeQL, Defender, provenance | Dependencies, Python, GitHub runners, Windows and repository maintainers remain trusted |
| External capture/app experiment leaks data or changes setting | Separate opt-in mode, existing tools, bounded selected-file import, original-value/restore prompts | Owner/settings tool performs changes; capture contents and restoration are not independently verified |

No physical controller is exercised in automated build tests. Parser/self-check success
does not establish device compatibility or prove driver behavior. The kit cannot prevent
the user manually uploading private files elsewhere. Bit-for-bit reproducibility is not
established. AST guards are intentionally narrow regression controls, not a malicious-code
detector or substitute for review. SBOM scope is locked Python runtime/build distributions
and CPython; it is not a complete inventory of all native Windows/PyInstaller DLLs.
