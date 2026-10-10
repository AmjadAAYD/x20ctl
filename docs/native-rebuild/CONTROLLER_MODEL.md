# Controller capability and evidence model

Date: 8 October 2026. Target C++ contract; hardware implementation deferred.

ControllerSession separates Player/model selection, physical identity/confidence, gameplay sources, configuration transport, generation and connection state. XInput slot is an endpoint, not physical identity. Reconnect/reassignment invalidates authorization until reassociated; ambiguity remains explicit.

| Dimension | Values / data |
| --- | --- |
| availability | Unknown, Unsupported, Supported |
| readSupport | Unknown, Observed, Verified |
| writeSupport | None, Experimental, Verified |
| persistence | Unknown, Volatile, VerifiedPersistent |
| evidence | IDs/provenance linking VERIFIED_HARDWARE, CAPTURE_CONFIRMED, PUBLIC_DOCUMENTED, OWNER_REPORTED, EXPERIMENTAL, UNKNOWN |
| applicability | Exact model/family, known firmware range, hardware revision, transport, constraints/date |

Absent constraints do not prove all revisions. Production writes require engine-owned applicable verified command/payload/write evidence and current session. Shared GATT/owner claims/experimental data alone cannot authorize writes.

VerifiedPersistent requires feature-appropriate reconnect and safe power-cycle evidence; immediate readback never sets it. Retain unknown/trailing bytes with typed fields. Draft, last-read, pending/indeterminate and verified-readback states are separate. ACK-only operations remain sent/acknowledged, not verified persistent.

X20 is the regression anchor. Other models/placeholders do not inherit X20 commands. OTA/firmware and unknown HID output fuzzing excluded.
