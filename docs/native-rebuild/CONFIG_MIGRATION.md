# Configuration migration contract

Updated: 9 October 2026. Setups now implements separate native storage and explicit opaque legacy copying. Actual legacy stores were inventoried/read only for preservation; no automatic migration or original-file modification occurred. CRUD, backups and idempotent copying were verified with isolated fixtures. See DEVICE_TESTER_SETUPS_REVIEW.md. Semantic conversion of legacy fields remains deferred.

## Audited legacy stores

- profiles.py: default %APPDATA%/x20ctl/profiles/*.json, home fallback/configurable store directory.
- desktop/service.py: %APPDATA%/x20ctl/desktop/profiles.json, preferences.json, x20_pro/profiles.json, configurable service directory.
- Imported files/session drafts/other stores need complete inventory before migration. Four-player UI does not imply four simultaneous BLE writers.

Preserve names/mappings/macros/curves/vibration/power/preferences/assignments and unknown fields. No format change solely because language changes.

## Atomic/idempotent sequence

Locate/version/read without mutating source -> unique exact-byte backup + manifest -> validate/convert in memory -> temporary destination in destination directory -> flush/reopen/validate -> same-filesystem atomic publication -> completion record with source/destination digests/schema/setup identities.

Never overwrite legacy source or corrupt stores. Use stable migration identity to avoid duplicate setups. Crash before publication preserves source; crash after publication/before marker requires validation of existing destination. Changed source/destination is a reported conflict, never silent overwrite. Retain backups indefinitely unless user deletes them.

## Future checks

All known/unknown fields, schema versions, corrupt JSON, duplicate identities/names, unicode, unavailable/read-only destination, crash at each boundary, repeated run, changed source, backup hashes and reopen/round-trip. Use copies; no success claim before verification.
