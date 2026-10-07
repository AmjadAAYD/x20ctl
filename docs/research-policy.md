# Public code and private controller research

Keep unpublished evidence in a separate local research workspace, outside this
public checkout. An initial workspace is `../x20ctl-protocol-research`; this is
local storage, not encryption or a configured private GitHub repository.
Do not add a remote or publish it without checking the destination's privacy
and every file's permissions. Back up raw evidence privately as well; files
ignored by Git are not protected by Git history.

## Boundaries

Public: the existing MIT-licensed application and X20 implementation, reviewed
UI/model metadata, tests and blank volunteer forms. No license is withdrawn
from previously distributed code. No proprietary driver bundle is introduced.

Private: tester submissions, personal identifiers, captures, unfinished command
experiments, conversation/permission evidence and maintainer research drafts.
The existing `docs/research/` working folder is ignored and its contents are
preserved. Do not depend on it from a public build; scanner kit templates live
in `docs/controller-scan-kit/templates/`.

The Git-index check runs in CI and recognizes private locations, raw capture
filenames, archives and environment files. It also catches force-added files.
It is not content scanning, access control, encryption or history removal. A
GitHub CI failure happens after a push, so run it locally before committing or
pushing. A public leak still requires a separate response; merely deleting the
latest file does not remove its previous copies.

## Intake and provenance

1. Give the case an opaque ID and the unit a non-identifying alias. Store the
   original received files privately, without executing or extracting unknown
   attachments. Record byte sizes and SHA-256 hashes before analysis.
2. Copy `templates/CASE.example.json` in the private workspace to
   `cases/<case-id>/record.json`. Record source, timestamps, model/revision,
   firmware, connection mode, collector version and evidence references. Use
   `unknown` or `not tested` when evidence is missing.
3. Record permissions separately for analysis, public derived implementation,
   raw publication, media publication and credit. Keep the permission evidence
   private. Upload consent does not substitute for those permissions. Record
   source licensing and third-party ownership; permission from a tester is not
   proof that they own another party's code or photographs.
4. Keep findings in draft until a repeatable test has demonstrated the claim.
   Distinguish discovered hardware capability, observed transport, tested read,
   tested write and verified read-back. State model/firmware/mode limits and
   negative results. Do not apply X20 commands to unverified models.

## Public promotion

Promote only deliberately selected, redacted findings or implementation changes
after privacy review, permission review, a licensing decision and appropriate
verification. Keep a private mapping from the public change to case/evidence IDs.
The public record can state test scope and limitations without publishing raw
captures or identifying the contributor. Publish credit only as agreed.

Private research does not make shipped commands secret. A signed pack would
verify an official pack's integrity/authenticity, not prevent extraction or
reuse. Any future split-driver licensing change needs a separate review of
ownership, contributor rights and component compatibility.
