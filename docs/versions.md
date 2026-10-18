# Version names

X20CTL versions read **V0.016**, **V0.016.1**, **V1.000**.

| Part | Meaning |
|---|---|
| First number | The super release. It is 0 until the first release that supports every controller, which is V1.000. Then the count starts again. |
| Three digits | The release. Each new release adds one: V0.016, V0.017, V0.018. |
| Last number | A bug fix to that release: V0.016.1, V0.016.2. A release without fixes has none. |

Releases are tagged the same way (`v0.017`, `v0.017.1`, `v1.000`). There are no previews: every release is a full release.

## Earlier releases

Releases before 18 Oct 2026 used other numbers. Their tags and downloads are unchanged. Only their names are new. The Controller Scan Kit (`scanner-v1.1.0`) is a separate tool and keeps its name.

| Tag | Name |
|---|---|
| v0.1.0 | V0.001 |
| v0.2.0 | V0.002 |
| v0.2.1 | V0.002.1 |
| v1.0.1 | V0.003 |
| v1.1.0 | V0.004 |
| v1.1.1 | V0.004.1 |
| v1.1.2 | V0.004.2 |
| v1.2.0 | V0.005 |
| v2.0.0 | V0.006 |
| v2.0.1 | V0.006.1 |
| v3.0.0 | V0.007 |
| v3.1.0 | V0.008 |
| v4.0.0 | V0.009 |
| v4.0.1 | V0.009.1 |
| v4.1.0-preview.1-exe | V0.010 |
| v4.1.0-preview.2 | V0.011 |
| v4.1.0-preview.3, v4.1.0-preview.3-exe | V0.012 |
| v4.1.0-preview.4 | V0.013 |
| v4.1.0-preview.5 | V0.014 |
| v4.1.0-preview.6 | V0.015 |
| v4.1.0-preview.7 | V0.016 |

V0.016 still has an old-style tag. Copies of X20CTL up to V0.015 only understand the old numbers, so they need that tag to find the update. From V0.017 on, tags use the new form.

## For maintainers

- [RELEASES/versions.json](../RELEASES/versions.json) lists the old tags. `desktop-dotnet/X20Ctl.Zone/ReleaseVersions.cs` holds the same list, and a test keeps the two equal.
- `python tools/version_name.py <tag>` prints a tag's name. `--numeric` prints the form .NET needs (`v0.017.1` gives `0.17.1`).
- CHANGELOG sections are headed by the name (`## [V0.017] - date`). `tools/release_notes.py <tag>` finds the section.
- The app orders versions with `ReleaseVersions.Compare`. Any new-style version is newer than every old one.
