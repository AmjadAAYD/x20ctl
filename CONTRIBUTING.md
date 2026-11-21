# Contributing to X20CTL

Thank you for helping improve **X20CTL**. This guide covers the V0.025 codebase layout, local verification checks, and safe protocol/controller contributions.

## Repository layout

| Path | Purpose |
|---|---|
| `desktop-dotnet/X20Ctl.Product/` | Windows desktop app (.NET 10 WPF) and native C++ input helper (`tools/web-setup`, native engine) |
| `desktop-dotnet/X20Ctl.Linux/` | Linux Studio frontend (.NET 10 Avalonia) sharing core logic from `X20Ctl.Zone` |
| `desktop-dotnet/X20Ctl.Linux.Review/` | Headless Avalonia screen renderer and input-ownership verification harness |
| `desktop-dotnet/X20Ctl.Zone/` | Shared cross-platform core: drafts, protocols (X20, X20 Pro, experimental X10), offline Agent XCTL, docs search |
| `desktop-dotnet/X20Ctl.Simulation/` | Deterministic simulation fixtures for UI & controller flows |
| `desktop-dotnet/X20Ctl.Zone.Tests/` | xUnit suite covering protocol frames, docs search, offline Agent, updater, and profiles |
| `x20ctl/` | Python CLI (`x20`), model catalog (`x20ctl/controllers/catalog.json`), and protocol helpers |
| `tools/controller-scan-kit/` | Read-only Windows Controller Scan Kit (`x20ctl-scanner`) for hardware evidence collection |
| `tools/linux_controller_check/` | Standalone Python 3 scripts for Linux Controller Check and PC Mode |
| `tools/linux-studio/` | Linux tarball, AppImage, and Flatpak packaging scripts & manifests |
| `tools/wpf-ui-checks/` | Headless WPF UI invariant checks (`UiChecks`) |
| `src/assets/controllers/` | Layered controller artwork, finishes, hit regions, and haptics contours |

## Local checks

Run these before committing changes:

```powershell
# Research boundary guard (no private captures/binaries in Git)
python tools/check_research_boundary.py

# Python regression suite (root + scan kit)
python -m pytest -q -p no:cacheprovider

# Core .NET tests (protocol, Agent, docs, release versions)
dotnet test desktop-dotnet/X20Ctl.Zone.Tests/X20Ctl.Zone.Tests.csproj

# Windows WPF Studio + UI checks + Linux Studio builds (Windows host)
dotnet build desktop-dotnet/X20Ctl.Product/X20Ctl.Product.csproj
dotnet build tools/wpf-ui-checks/UiChecks.csproj
dotnet build desktop-dotnet/X20Ctl.Linux/X20Ctl.Linux.csproj
dotnet build desktop-dotnet/X20Ctl.Linux.Review/X20Ctl.Linux.Review.csproj
```

## Contributing controller support & protocol research

- Follow [`docs/research-policy.md`](docs/research-policy.md). Never upload proprietary manufacturer DLLs, APKs, raw captures, or personal device identifiers (serials, MAC addresses) to public issues or PRs.
- Use **Controller Check** / **Diagnostics** inside X20CTL, or the templates under [`docs/controller-scan-kit/templates/`](docs/controller-scan-kit/templates/), when reporting a controller layout or capture.
- Never guess or claim unverified hardware writes. New native configuration commands require read-back verification and explicit documentation in [`docs/01-protocol.md`](docs/01-protocol.md).

## Pull requests & code style

- Keep changes focused, tested, and honest about what was actually run on hardware vs. fixtures/offscreen renders.
- Source code is licensed under **GPL-3.0-or-later** (see [`LICENSE`](LICENSE) and [`NOTICE`](NOTICE)).
