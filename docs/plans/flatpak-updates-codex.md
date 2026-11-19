# Flatpak bundle updates

Route: versioned x86_64 .flatpak release asset and SHA256SUMS, using existing verified download. Host install via flatpak-spawn; only per-user installs self-update. System installs fall back rather than create a shadow user install. Download to the app cache (host-visible absolute path), never sandbox /tmp. Use argv, no shell. Disable cancel during the host installation transaction; failed install/restart opens release page. Keep AppImage flow.

Build: reusable Linux bundle script with staging manifest and self-contained publish. Network plus org.freedesktop.Flatpak permission explicitly declared. Existing Linux release job produces the bundle before publication. Provide workflow_dispatch dry-run input with publication disabled, usable after an authorized push; no push or dispatch performed here.

Criteria: fake-HTTP prerequisite tests; Flatpak asset selection, safe argv, failure and cancellation tests; Product/Linux compilation; offscreen states; YAML/build-script validation. A real Linux Flatpak build/install/update/restart and branch CI remain unverified unless actually run. No existing research distro modified.
