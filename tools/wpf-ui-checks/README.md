# Isolated WPF UI checks

From the private lab root on Windows/.NET10:

    dotnet run --project tools/wpf-ui-checks/UiChecks.csproj -v q

Optional offline 30fps intro frame export:

    dotnet run --project tools/wpf-ui-checks/UiChecks.csproj -v q -- --preview

Checks Dashboard, original controller Agent, navigation and intro without opening a Window, launching games, playing audio, preparing models, measuring drift or writing hardware. It uses reflection for private WPF integration boundaries. Artwork checks use a GUID fixture identity and remove its own file. A read-only scan can render local installed-game artwork with explicitly offline input state. Render artifacts go to artifacts/intro-steam-inspired. The 30fps export is not a runtime performance measurement.


Scoped Controller Zone surface/layout checks and renders: dotnet run --project tools/wpf-ui-checks/UiChecks.csproj -v q -- --zone-surfaces

