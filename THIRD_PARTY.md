# Third-party components

x20ctl source is GPL-3.0-or-later (see LICENSE and NOTICE; releases up to 4.1.0 Preview 2 were MIT). That does not relicense its dependencies.

The Windows app is a .NET 10 WPF application published self-contained, so it carries Microsoft's .NET runtime (MIT). Its native input engine is built from `native-core` in this repository. It also uses:

- [Nefarius.ViGEm.Client](https://github.com/nefarius/ViGEm.NET) (MIT) for PC mode's virtual pad.
- System.IO.Ports (MIT) for the X20 Pro screen upload.
- [Microsoft WebView2 SDK](https://www.nuget.org/packages/Microsoft.Web.WebView2) (BSD-3-Clause, text in `licenses/MICROSOFT-WEBVIEW2.txt` and NOTICE) for trigger rumble.

PC mode's installers for ViGEmBus and HidHide are fetched at release time, pinned by SHA-256 (see `.github/workflows/release.yml`); they keep their own licences.

The Linux Studio uses [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT).

The X10's configuration protocol (`desktop-dotnet/X20Ctl.Zone/X10/Krly.cs`) is ported from Edgard's [EasyGamepad](https://github.com/edgardmessias/easygamepad) (GPL-3.0-or-later), who decoded it.

The command-line tool and protocol library in `x20ctl/` depend only on [Bleak](https://github.com/hbldh/bleak) (MIT).

No controller vendor firmware, proprietary application or decompiled vendor source is included.
