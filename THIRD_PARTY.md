# Third-party components

x20ctl source is GPL-3.0-or-later (see LICENSE and NOTICE; releases up to 4.1.0 Preview 2 were MIT). That does not relicense its dependencies.

The Windows app is a .NET 10 WPF application published self-contained, so it carries Microsoft's .NET runtime (MIT). Its native input engine is built from `native-core` in this repository. PC mode's installers for ViGEmBus and HidHide are fetched at release time, pinned by SHA-256 (see `.github/workflows/release.yml`); they keep their own licences.

The command-line tool and protocol library in `x20ctl/` depend only on [Bleak](https://github.com/hbldh/bleak) (MIT).

No controller vendor firmware, proprietary application or decompiled vendor source is included.
