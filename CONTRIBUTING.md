# Contributing to x20ctl

UI, fixes, tests, blank collection templates and reviewed model metadata can be
proposed publicly. Existing code remains under the repository's MIT license.
Only X20 currently has a verified configuration backend; preview artwork and
capabilities do not establish a working protocol for another model.

## Controller research

Send raw captures, diagnostics, identifying photos and unfinished experiments
through an agreed private maintainer channel, never a public issue or pull
request. The existing desktop report upload still requires its own consent;
this policy neither changes that upload nor grants extra permissions.

Use the [blank intake form](docs/controller-scan-kit/templates/INTAKE.md).
Indicate separately whether private analysis, a public implementation, public
photos and public credit are permitted. Unknown or unanswered permission means
the maintainer must ask before that use. Submitting a report is not automatic
permission to publish its contents or license somebody else's material.

Do not include device serials, unique Bluetooth addresses, account details,
unrelated devices or private conversations in public contributions. Protocol
claims need their model, firmware, connection mode, reproducible evidence and
verification limits. Keep the full evidence record private.

Before proposing a change, run:

```powershell
python tools/check_research_boundary.py
```

The check examines staged/tracked filenames. It cannot identify every secret
inside an ordinary text file; review the staged diff too. Maintainer workflow:
[research policy](docs/research-policy.md).
