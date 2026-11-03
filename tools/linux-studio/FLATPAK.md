# Flatpak bundle packaging and updates

Implemented locally; a real Linux Flatpak build, install, update and restart have NOT been verified. Do not describe this as shipped support until the Linux dry-run and end-to-end checks pass.

## Packaging

`bash tools/linux-studio/build.sh VERSION OUTDIR INFO NAME` publishes self-contained Linux binaries, tarballs, AppImage and `X20CTL-NAME-linux-x86_64.flatpak`. VERSION/INFO/NAME come from the release tag in CI, never a fixed app version. The helper `build-flatpak.sh PUBLISH ABSOLUTE_OUTPUT.flatpak` stages the manifest and published files, installs build dependencies from Flathub, exports a repository and creates a single-file bundle. It requires Linux, flatpak and flatpak-builder, network access and usable user namespaces. Runtime/SDK: org.freedesktop 25.08; stable app branch. The desktop icon is renamed to match the app ID.

The Linux release job builds the bundle before publication. The publication job requires it in SHA256SUMS.txt and uploads it while the release is still a draft. Nothing is added to an immutable release afterward. Bundle integrity uses GitHub HTTPS plus the release checksum list, not a separate signing key.

For first installation on a Linux test machine:

```sh
flatpak --user remote-add --if-not-exists flathub https://flathub.org/repo/flathub.flatpakrepo
flatpak install --user ./X20CTL-NAME-linux-x86_64.flatpak
flatpak run --user io.github.AmjadAAYD.x20ctl
```

Replace NAME with the downloaded asset's actual name. These are planned instructions, not a record of execution.

## In-app update

The GitHub release query supplies the newest eligible release version. Flatpak selection requires the exact x86_64 bundle suffix, this project's trusted release URLs, and SHA256SUMS.txt. The existing download kit checks API size and SHA-256, reports progress and removes failed partial downloads.

After a click, the updater confirms the running deployment matches `flatpak info --user --show-location`, downloads into host-visible XDG_CACHE_HOME, then calls `flatpak-spawn --host flatpak install --user --noninteractive --or-update --bundle ABSOLUTE_PATH` through an argument list, without a shell. Cancel applies to download/preparation; it is disabled once the installation transaction starts. A successful transaction offers/attempts restart via host `flatpak run --user --arch=x86_64 --branch=stable APP_ID`. An immediate launch error keeps the current process open and offers the release page. A delayed startup failure cannot be proven absent by this short launch check.

System/custom deployments fall back rather than creating a shadow per-user installation. Missing assets, denied host execution and host install errors also fall back. The updater itself never replaces /app files. Flatpak manages its deployment transaction; interrupted host transactions still need real Linux acceptance testing.

The manifest grants network access and `--talk-name=org.freedesktop.Flatpak`. The latter permits broad host execution, not just updating this app. This is the explicit permission tradeoff for the requested host-command route. No broad host filesystem permission is added. Flatpak's own command reference documents this requirement: https://docs.flatpak.org/en/latest/flatpak-command-reference.html#flatpak-spawn

## Safe CI dry-run and remaining acceptance

After an independently authorized push, dispatch Release on that branch with a valid version tag input and `dry_run=true` (the default). It checks out the branch for Linux Studio, produces build artifacts, and skips Windows, Controller Check and release publication. No push or dispatch was performed during this task.

1. Build the bundle in the branch dry-run and inspect the artifact and desktop metadata.
2. Install an older bundle per-user on Linux; start it and offer a newer GitHub release bundle.
3. Check progress/cancel; install, restart, and independently read the running version.
4. Deny host permission and simulate missing/corrupt bundles; verify fallback and intact existing deployment.
5. Test system installation, absent runtimes, spaces in home path, host failure and restart failure.

No real Flatpak bundle exists as an outcome of the Windows-only checks in this task.
