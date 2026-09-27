# X20ctl submission receiver and private inbox

Date: 2026-09-27
Status: revised design for review

## Scope and boundary

This is the first sub-project in the broader X20ctl support work. Build and test the receiving end before adding any desktop scanner upload or “Send Report” control. The existing personal portfolio repository, its `/admin` route, database, deployment, and public pages are out of scope and must not be changed. No X20ctl release or receiver deployment is part of this sub-project without separate approval.

The receiver lives in its own Vercel project rooted at a new `receiver/` directory in the X20ctl repository. It has an authenticated private X20ctl submission inbox, not a portfolio page. Version one uses the inbox as the delivery destination; email is optional future work, so no sending domain or email API key is required now.

## Options considered

1. **Separate Vercel receiver and inbox, using private Vercel Blob (chosen).** Keeps the portfolio untouched, uses the Vercel account the developer already has, and avoids a database and email provider for version one. The trade-off is that the developer reviews an inbox rather than receiving email, and Vercel's function request-size limit requires small reports.
2. **Receiver inside the portfolio project.** Reuses its existing admin auth and Neon database, but violates the explicit boundary against touching the personal portfolio.
3. **Cloudflare Worker, R2, and an inbox.** Separates the projects, but adds another hosting account and more setup than the requested lightweight first version.

## Public API contract

The receiver exposes `POST /api/feedback` and `POST /api/controller-report`, plus `GET /api/health`. The public health response is exactly `{ "status": "ok" }`; it does not disclose storage, environment, credentials, version, or configuration details. The desktop app will later use one centrally configured API base URL. No shared secret or admin credential is embedded in the desktop app.

Every submission requires a client-generated UUIDv4 `clientSubmissionId`. The desktop app creates it once when composing a feedback message or local report and keeps it unchanged across retries. The receiver derives the stable `FB-` or `CR-` ID from the first 24 uppercase hexadecimal characters of SHA-256 over the submission type and normalized UUID. The random UUID supplies the unpredictability; the ID is never an authentication token. The receiver stores a SHA-256 content digest over canonically serialized, server-validated fields, excluding the UUID and server timestamp. For reports the digest also includes the ZIP's SHA-256, not multipart boundaries. A retry with the same UUID and digest returns the existing ID; the same UUID with different content returns HTTP 409. This makes a lost HTTP response safe to retry without creating a duplicate.

`POST /api/feedback` accepts JSON with that UUID, a category, message, optional diagnostic fields, app version, and client timestamp. The server validates categories and string lengths, discards unknown fields, adds a server timestamp, and creates one private JSON record without allowing overwrite. It returns HTTP 201 with `{ "success": true, "submissionId": "FB-..." }` only after storage. A valid duplicate may return HTTP 200 with the same response body.

`POST /api/controller-report` accepts multipart form data containing that UUID, bounded metadata, and one small ZIP. The server validates the metadata, compressed size, ZIP structure, entry count, entry names, uncompressed size, and CRCs. ZIP entries must have exact ASCII names from this allowlist: `device.json`, `usb-descriptors.txt`, `hid-report-descriptor.bin`, `hid-report-descriptor-parsed.txt`, `input-mapping.json`, `input-captures.json`, `system.json`, `scanner-version.txt`, and `README.txt`. No directories are needed. Reject absolute paths, drive-letter and UNC paths, forward- or backslash traversal, Unicode/path-normalization tricks, symbolic links, duplicate names, nested archives, executable files, encrypted entries, ZIP64, and malformed archives. Never execute uploaded content.

The report ZIP and private metadata use deterministic paths based on the stable ID, with overwrite disabled. The ZIP is written first, then committed metadata. If the metadata write fails, return a retryable error and leave the ZIP for recovery rather than risking a concurrent retry by deleting it. On retry, if the ZIP exists but metadata does not, compare its digest to the incoming ZIP and finish the metadata write only if they match. If committed metadata exists and its digest matches, return the original ID. Conflicting content returns HTTP 409. Orphan ZIPs are not shown as received submissions; the setup guide explains how to identify and manually remove old orphans after seven days. Return HTTP 201 only when both objects are present.

The ZIP cap is 2 MiB, leaving room for multipart overhead below Vercel's 4.5 MB function request limit. The total expanded size is capped at 8 MiB, with at most 20 files. The receiver counts bytes while reading the actual request stream and stops at a 16 KiB feedback-body cap or a 2.5 MiB total report-body cap; it does not trust `Content-Length`. ZIP entries are inspected and expanded only within the configured limits. A future larger-report flow must be designed separately. The desktop scanner must save its report locally and show a preview before any upload; it must keep the local report after upload failure.

Both endpoints return structured, non-sensitive JSON errors, use request timeouts, require correct content types, and reject bodies above their limits. They do not echo stack traces, storage paths, tokens, or internal errors. A failed request never produces a success-looking ID.

## Storage and inbox

Create one **private** Vercel Blob store connected only to the new receiver project. Feedback records, report metadata, and report ZIPs use separate prefixes. Stored filenames are generated by the server from IDs, not user-provided controller names. Original names are data fields only. The storage boundary is a small `StorageAdapter` with `saveFeedback`, `saveControllerReport`, `getSubmission`, `listSubmissions`, `getReportFile`, and `deleteSubmission`. Automated and local integration tests use an in-memory adapter; deployment uses a Vercel Blob adapter. API and admin logic must not import Vercel Blob directly.

The inbox is a small `/admin` interface belonging to the receiver project. It has Feedback and Controller Reports views with category, controller name where supplied, server time, ID, and “received” state. It supports viewing full text and downloading report ZIPs through an authenticated server route that streams `application/zip` with a server-generated filename such as `CR-A7F92B31.zip`. React renders submitted text as text, never raw HTML. It supports pagination and a clear empty state, with no charts or analytics. Report status editing and public status lookup are deferred.

An authenticated Delete action, guarded by a confirmation naming the submission ID, removes feedback metadata or both report ZIP and metadata. If one part of report deletion fails, the inbox shows an error and retains a retry path; it does not claim deletion succeeded. Deletion is permanent, so the UI must say so. No automatic retention or deletion is claimed.

The inbox uses `ADMIN_PASSWORD_HASH` with scrypt, a random salt, and documented work parameters, plus an independent 32-byte `ADMIN_SESSION_SECRET` in Vercel environment variables. There are no user accounts. A setup command generates the password hash and another generates the session secret; neither prints or stores plaintext passwords in source control. Password verification uses a timing-safe comparison. The session is an expiring, signed, `HttpOnly`, `Secure`, `SameSite=Strict` cookie. Every inbox page, data route, delete route, and download route verifies the session server-side. Login and other state-changing admin requests check origin. Private Blob URLs and tokens are never sent to an unauthenticated client. The login page is rate-limited as well as the public submission endpoints.

## Abuse and privacy controls

The receiver requests only information explicitly submitted by the user. The desktop scanner must not collect serial numbers by default, unrelated USB devices, files, browser data, keyboard input, or network traffic. The receiver does not intentionally persist raw IP addresses. Vercel may retain platform request logs; the setup documentation must disclose this.

The public endpoints use server-side schema validation, strict size limits, a simple honeypot field for feedback, and project-level Vercel Firewall rate-limit rules configured before production deployment. The setup guide includes exact rules and a verification step. Without those production rules, the local receiver can be tested but is not declared production-ready. Incoming report metadata and ZIP contents are treated as untrusted data throughout.

## Build and verification order

1. Add the isolated receiver project and its pure validation/storage logic.
2. Add automated tests for valid feedback, valid ZIP reports, same-ID/same-content retries, same-ID/different-content conflicts, concurrent retries, interrupted two-object report storage and recovery, malformed and oversized request streams with dishonest or absent `Content-Length`, every forbidden ZIP path/type above, decompression limits, storage failure, ID format, unauthenticated inbox access, authenticated download, and confirmed admin deletion.
3. Run the receiver locally with a test storage adapter and submit fake feedback and fake report fixtures. Verify that an inbox record exists and a valid ZIP can be downloaded; verify that rejected requests produce no record.
4. Add `docs/SUBMISSIONS_SETUP.md` with beginner-level instructions for creating a separate Vercel project, private Blob store, password-hash and session-secret generator commands, production Firewall rules, deployment URL, local test commands, inbox login, backup/deletion, and troubleshooting. The guide must explicitly say that the portfolio is not involved.
5. Only after the receiver passes those checks, begin the separate desktop feedback/scanner sub-project. First integrate feedback and verify a real receiver response; then add selected-controller diagnostics, local report preview, consent, and report upload.

Receiver acceptance is not the same as live delivery. Until the developer creates the Vercel project, connects private Blob, sets the environment variables, configures rate limiting, and approves deployment, only local behavior can be verified. No scanner upload UI is added before the receiver is tested.

## Follow-on sub-projects, not part of this receiver spec

The desktop work will preserve the existing UI and native X20/X20 Pro safety boundaries. It includes feedback, compatibility scanning, guided input capture, preview and consent, and explicit uploads. Separate focused work covers the compatibility database, Copy Support Information, local connection history and profile notes, advanced read-only HID inspection, health check, troubleshooting flow, update-check improvements and release notes, and the optional Ko-fi support entry. Public report-status lookup is deferred until there is a clear privacy-preserving status design. No feature implies new controller configuration support without device-specific evidence.
