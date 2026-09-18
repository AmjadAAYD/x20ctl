# Third-party components

x20ctl source is MIT-licensed. That does not relicense its dependencies.

The Windows executable bundles Python and libraries including pywebview, pythonnet, Bleak, pystray, Pillow and their runtime dependencies. React and Lucide are compiled into the local frontend. Original dependency copyright/license files are collected into `THIRD_PARTY_LICENSES.txt` during the build and shipped inside the executable; the same notice file accompanies the release.

The full build also contains **Microsoft Edge WebView2 Fixed Version Runtime 153.0.4234.32 x64**, distributed under Microsoft's runtime terms, not the x20ctl MIT license. All files from Microsoft's runtime package are retained. Source: [Microsoft WebView2 downloads](https://developer.microsoft.com/en-us/microsoft-edge/webview2/). Deployment details: [Microsoft's distribution documentation](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution).

The pinned runtime download and SHA-256 are in `tools/runtime.py`. The build verifies a valid Microsoft signature on its executable. Microsoft retains all rights to that runtime. Its own third-party notices are accessible through the runtime's included resources. The bundled fixed version requires maintenance updates from this project; the separately serviced system runtime is preferred when present.

WebView2 includes **Microsoft Defender SmartScreen**, which collects and sends information to Microsoft as described in [Microsoft's privacy statement](https://aka.ms/privacy) and the [Edge privacy whitepaper](https://learn.microsoft.com/en-us/microsoft-edge/privacy-whitepaper#smartscreen). Runtime diagnostics and security services are Microsoft's behavior, separate from x20ctl's optional GitHub update check.

pystray is LGPL-3.0 licensed. Its source is available from [pystray upstream](https://github.com/moses-palmer/pystray/tree/v0.19.5) and the release's source archive. The application can be rebuilt with a modified library using the documented build process. No project restriction is imposed on reverse engineering for debugging modifications to LGPL components.

No controller vendor firmware, proprietary application or decompiled vendor source is included.
