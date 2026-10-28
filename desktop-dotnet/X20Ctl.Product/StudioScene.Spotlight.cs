using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using X20Ctl.Product.Spotlight;
using X20Ctl.Zone;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool IsSpotlight => ThemeCatalog.Current.Id == "spotlight";

    private Grid? spotlightLayer;
    private Canvas? spotlightStage;
    private Canvas? spotlightPageHost;
    private bool spotlightQueued;
    private string currentSpotlightPage = "Dashboard";

    // Dashboard State
    private bool spotlightDashboardRear = false;
    private GameEntry? spotlightSelectedGame = null;
    private IReadOnlyList<GameEntry> spotlightGamesList = [];
    private bool spotlightGamesScanning = false;

    private void BuildSpotlightStudio()
    {
        if (!IsSpotlight || spotlightLayer != null) return;

        RootLayout.Visibility = Visibility.Collapsed;
        StudioBackdrop.Visibility = Visibility.Collapsed;

        spotlightStage = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        spotlightPageHost = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };
        Canvas.SetLeft(spotlightPageHost, 0);
        Canvas.SetTop(spotlightPageHost, 0);
        spotlightStage.Children.Add(spotlightPageHost);

        spotlightLayer = new Grid
        {
            Background = SpotlightKit.HouseBrush,
            Children = { new Viewbox { Stretch = Stretch.Uniform, Child = spotlightStage } }
        };
        Panel.SetZIndex(spotlightLayer, 30);
        StudioShell.Children.Add(spotlightLayer);

        ShowManagement("Dashboard");
    }

    private void SpotlightLater()
    {
        if (!IsSpotlight || spotlightQueued) return;
        spotlightQueued = true;
        Dispatcher.InvokeAsync(() =>
        {
            spotlightQueued = false;
            SpotlightShow();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void SpotlightSelectPage(string target)
    {
        switch (target.ToLowerInvariant())
        {
            case "buttons":
                ShowButtons();
                break;
            case "curves":
                ShowCurves(true);
                break;
            case "macros":
                ShowMacros(true);
                break;
            case "vibration":
                ShowVibration(true);
                break;
            case "pcmode":
                ShowManagement("PcMode");
                break;
            case "device":
                ShowManagement("Device");
                break;
            case "mouse":
                ShowManagement("Mouse");
                break;
            case "setups":
                ShowManagement("Setups");
                break;
            case "tester":
                ShowManagement("Tester");
                break;
            case "diagnostics":
                ShowManagement("Diagnostics");
                break;
            case "tools":
                ToolsRequested?.Invoke();
                break;
            case "dashboard":
            default:
                ShowManagement("Dashboard");
                break;
        }
    }

    private void SpotlightShow()
    {
        if (!IsSpotlight || spotlightPageHost == null) return;

        string activePage = CurrentPage;
        currentSpotlightPage = activePage;
        switch (activePage)
        {
            case "Buttons":
                SpotlightShowButtons();
                break;
            case "Curves":
                SpotlightShowCurves();
                break;
            case "Macros":
                SpotlightShowMacros();
                break;
            case "Vibration":
                SpotlightShowVibration();
                break;
            case "PcMode":
                SpotlightShowPcMode();
                break;
            case "Device":
                SpotlightShowDevice();
                break;
            case "Mouse":
                SpotlightShowMouse();
                break;
            case "Setups":
                SpotlightShowSetups();
                break;
            case "Tester":
                SpotlightShowTester();
                break;
            case "Diagnostics":
                SpotlightShowDiagnostics();
                break;
            case "Dashboard":
            default:
                currentSpotlightPage = "Dashboard";
                SpotlightShowDashboard();
                break;
        }
    }

    private void SpotlightShowDashboard()
    {
        spotlightPageHost!.Children.Clear();

        // 1. Populate / Scan Games Shelf
        if (spotlightGamesList.Count == 0 && !spotlightGamesScanning)
        {
            if (ReviewSandbox.HideLibrary || ReviewSandbox.Active)
            {
                spotlightGamesList = new GameEntry[]
                {
                    new("hk", "Hollow Knight", "Steam", null, null, null, DateTime.Now.AddDays(-1), "steam://run/367520"),
                    new("er", "Elden Ring", "Steam", null, null, null, DateTime.Now.AddDays(-2), "steam://run/1245620"),
                    new("cp", "Cyberpunk 2077", "GOG", null, null, null, DateTime.Now.AddDays(-5), "goggalaxy://launch/1097893765"),
                    new("sf", "Starfield", "Xbox", null, null, null, DateTime.Now.AddDays(-7), "xbox://launch"),
                    new("ds", "Dark Souls III", "Steam", null, null, null, DateTime.Now.AddDays(-10), "steam://run/374320")
                };
                if (spotlightSelectedGame == null) spotlightSelectedGame = spotlightGamesList[0];
            }
            else
            {
                spotlightGamesScanning = true;
                _ = Task.Run(async () =>
                {
                    var list = await GameLibrary.ScanAsync();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        spotlightGamesScanning = false;
                        spotlightGamesList = list;
                        if (spotlightSelectedGame == null && list.Count > 0)
                            spotlightSelectedGame = list[0];
                        if (currentSpotlightPage == "Dashboard")
                            SpotlightShowDashboard();
                    });
                });
            }
        }

        // 2. Theatrical Stage Background (floor divides upper telemetry/hero from lower game shelf at y = 510)
        var stage = SpotlightKit.TheatricalStage(floorY: 510, legs: true, valance: true, leftLeg: false, rightLegW: 80);
        spotlightPageHost.Children.Add(stage);

        // 3. Programme Rail down the left: ★ DASHBOARD is actively illuminated with golden follow-spot glow
        var rail = SpotlightParts.ProgrammeRail("dashboard", SpotlightSelectPage);
        spotlightPageHost.Children.Add(rail);

        // 4. Header Bar at (280, 24)
        var headerStack = new StackPanel { Orientation = Orientation.Vertical };
        headerStack.Children.Add(new TextBlock
        {
            Text = "DASHBOARD · SALLE DES CONSOLES",
            FontFamily = SpotlightKit.Display,
            FontSize = 28,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.B("#f3e7d3")
        });
        headerStack.Children.Add(new TextBlock
        {
            Text = $"Command centre, hardware telemetry & games library · {Model.Name}",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 14.5,
            Foreground = SpotlightKit.B("#b3a48e"),
            Margin = new Thickness(2, 2, 0, 0)
        });
        Canvas.SetLeft(headerStack, 280);
        Canvas.SetTop(headerStack, 22);
        spotlightPageHost.Children.Add(headerStack);

        // Top-right status header
        var status = SpotlightParts.StatusHeader(right: 40, top: 22);
        Canvas.SetRight(status, 40);
        Canvas.SetTop(status, 22);
        spotlightPageHost.Children.Add(status);

        // =========================================================================
        // 5. TOP LEFT FLANK: CONTROLLER STATUS CUE CARD (x: 280, y: 84, w: 460, h: 412)
        // =========================================================================
        var statusCardContent = new StackPanel { Width = 432, Margin = new Thickness(14, 12, 14, 12) };

        // Status indicator row
        bool isOnline = (gameplayConnected == true);
        var statusDotRow = new StackPanel { Orientation = Orientation.Horizontal };
        statusDotRow.Children.Add(new Ellipse
        {
            Width = 9, Height = 9,
            Fill = isOnline ? SpotlightKit.B("#2ecc71") : SpotlightKit.B("#e67e22"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });
        statusDotRow.Children.Add(new TextBlock
        {
            Text = isOnline ? "CONTROLLER ONLINE" : "CONTROLLER STANDBY",
            FontFamily = SpotlightKit.Display,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = isOnline ? SpotlightKit.B("#2ecc71") : SpotlightKit.B("#e67e22"),
            VerticalAlignment = VerticalAlignment.Center
        });
        statusCardContent.Children.Add(statusDotRow);

        // Active setup name
        string activeSetupName = activeSetup != null
            ? setupStore.Items.FirstOrDefault(x => x.Id == activeSetup)?.Name ?? "Current working settings"
            : "Current working settings";

        statusCardContent.Children.Add(new TextBlock
        {
            Text = activeSetupName,
            FontFamily = SpotlightKit.Body,
            FontWeight = FontWeights.SemiBold,
            FontSize = 20,
            Foreground = SpotlightKit.B("#fff6e6"),
            Margin = new Thickness(0, 4, 0, 0)
        });

        statusCardContent.Children.Add(new TextBlock
        {
            Text = Draft.PcMode ? "PC Mode active · Settings apply on this PC" : "Local working settings · Hardware memory intact",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 12.5,
            Foreground = SpotlightKit.B("#a4957f"),
            Margin = new Thickness(0, 2, 0, 10)
        });

        // 2x2 Telemetry Grid
        var telemGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        telemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        telemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        telemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        telemGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        telemGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        telemGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });

        string connStr = currentTransport ?? (isOnline ? "Detected · 2.4 GHz" : "Not detected");
        string battStr = currentBatteryLevel switch
        {
            "full" => "100% · Full",
            "medium" => "74% · Good",
            "low" => "22% · Low",
            "wired" => "Wired · Charging",
            _ => "100% · Standby"
        };
        string fwStr = currentFirmware ?? (currentTransport == "2.4 GHz receiver" ? "Not readable (XInput)" : "v1.0.8");
        string chgStr = Draft.UnsentChanges > 0 ? $"{Draft.UnsentChanges} unsaved" : "0 unsaved";

        // Plaque helper
        Border MakePlaque(string label, string val, Action? onClick = null, string? tip = null)
        {
            var pPanel = new StackPanel { Margin = new Thickness(10, 6, 10, 6) };
            pPanel.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontFamily = SpotlightKit.Display,
                FontSize = 9.5,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.MuteBrush
            });
            pPanel.Children.Add(new TextBlock
            {
                Text = val,
                FontFamily = SpotlightKit.Body,
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = SpotlightKit.ChalkBrush,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            });
            var b = new Border
            {
                Background = SpotlightKit.B(SpotlightKit.A(SpotlightKit.C("#050203"), 0.85)),
                BorderBrush = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.22)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = pPanel
            };
            if (onClick != null)
            {
                b.Cursor = Cursors.Hand;
                b.ToolTip = tip;
                b.MouseLeftButtonDown += (_, _) => onClick();
            }
            return b;
        }

        var plqConn = MakePlaque("Connection", connStr);
        Grid.SetColumn(plqConn, 0); Grid.SetRow(plqConn, 0); telemGrid.Children.Add(plqConn);

        var plqBatt = MakePlaque("Battery", battStr, () =>
        {
            _ = PadBridge.Status(Model.Id).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result.Ok && t.Result.Telemetry != null)
                {
                    Dispatcher.InvokeAsync(() =>
                    {
                        ApplyTelemetry(t.Result.Telemetry);
                        if (t.Result.Telemetry.Battery != null) currentBatteryLevel = t.Result.Telemetry.Battery;
                        if (t.Result.Telemetry.Firmware != null) currentFirmware = t.Result.Telemetry.Firmware;
                        SpotlightShowDashboard();
                    });
                }
            });
        }, "Click to refresh battery over Bluetooth");
        Grid.SetColumn(plqBatt, 2); Grid.SetRow(plqBatt, 0); telemGrid.Children.Add(plqBatt);

        var plqFw = MakePlaque("Firmware", fwStr);
        Grid.SetColumn(plqFw, 0); Grid.SetRow(plqFw, 2); telemGrid.Children.Add(plqFw);

        var plqChg = MakePlaque("Changes", chgStr);
        Grid.SetColumn(plqChg, 2); Grid.SetRow(plqChg, 2); telemGrid.Children.Add(plqChg);

        statusCardContent.Children.Add(telemGrid);

        // Status Card Bottom Buttons
        var statusBtnsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var editSetupBtn = SpotlightKit.TheatricalButton("Edit Setup  →", primary: false, key: "X", onClick: () => SpotlightSelectPage("buttons"), height: 38, fontSize: 11);
        editSetupBtn.Margin = new Thickness(0, 0, 10, 0);
        statusBtnsRow.Children.Add(editSetupBtn);

        var pcModeBtn = SpotlightKit.TheatricalButton("Open PC Mode  →", primary: false, onClick: () => SpotlightSelectPage("pcmode"), height: 38, fontSize: 11);
        statusBtnsRow.Children.Add(pcModeBtn);
        statusCardContent.Children.Add(statusBtnsRow);

        var statusCard = SpotlightKit.CueCard(statusCardContent, 460, padding: new Thickness(0));
        Canvas.SetLeft(statusCard, 280);
        Canvas.SetTop(statusCard, 84);
        spotlightPageHost.Children.Add(statusCard);

        // =========================================================================
        // 6. TOP RIGHT FLANK: HERO CONTROLLER STAGE (cx: 1140, floor: 470, w: 520)
        // =========================================================================
        double heroCx = 1140;
        double heroFloor = 470;
        double heroW = 520;
        double heroH = 320;

        // Radiant Overhead Follow-Spot on controller
        var heroSpot = SpotlightKit.FollowSpot(heroCx, heroFloor, 270, -40, 0.35);
        spotlightPageHost.Children.Add(heroSpot);

        // Stage Pedestal Floor Ring
        var heroPedestal = new Ellipse
        {
            Width = heroW * 0.88,
            Height = 32,
            Stroke = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.45)),
            StrokeThickness = 1.5,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops =
                {
                    new(SpotlightKit.A(SpotlightKit.C("#ffe6c4"), 0.35), 0.0),
                    new(SpotlightKit.A(SpotlightKit.C("#d9b77a"), 0.15), 0.60),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(heroPedestal, heroCx - (heroW * 0.88) / 2);
        Canvas.SetTop(heroPedestal, heroFloor - 16);
        spotlightPageHost.Children.Add(heroPedestal);

        // Contact Shadow
        var heroShadow = new Ellipse
        {
            Width = heroW * 0.72,
            Height = 14,
            Fill = new RadialGradientBrush
            {
                Center = new(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5,
                GradientStops = { new(SpotlightKit.A(Colors.Black, 0.80), 0.0), new(Colors.Transparent, 1.0) }
            }
        };
        Canvas.SetLeft(heroShadow, heroCx - (heroW * 0.72) / 2);
        Canvas.SetTop(heroShadow, heroFloor - 7);
        spotlightPageHost.Children.Add(heroShadow);

        // Controller Visual Container (Front or Rear based on spotlightDashboardRear)
        var heroPadContainer = new Grid
        {
            Width = heroW,
            Height = heroH,
            Cursor = Cursors.Hand,
            ToolTip = "Click to configure controller buttons"
        };
        heroPadContainer.MouseLeftButtonDown += (_, _) => SpotlightSelectPage("buttons");

        if (!spotlightDashboardRear)
        {
            // Front view: High-res controller with golden rim aura
            var rimBmp = SpotlightParts.GetControllerRimBitmap(Model.Id);
            if (rimBmp != null)
            {
                var rimImg = new Image
                {
                    Source = rimBmp,
                    Width = heroW,
                    Height = heroH,
                    Stretch = Stretch.Uniform,
                    Opacity = 0.85,
                    Effect = new DropShadowEffect { Color = SpotlightKit.C("#ffd27a"), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.75 }
                };
                RenderOptions.SetBitmapScalingMode(rimImg, BitmapScalingMode.HighQuality);
                heroPadContainer.Children.Add(rimImg);
            }

            var padArt = SpotlightParts.CreateControllerArt(Model.Id, heroW, heroH);
            padArt.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 16, Opacity = 0.70 };
            heroPadContainer.Children.Add(padArt);
        }
        else
        {
            // Rear view: PhotoController rear showing paddles and mode switches
            var rearPad = new PhotoController(Model.Id, true);
            var rearVb = new Viewbox { Stretch = Stretch.Uniform, Child = rearPad, Width = heroW, Height = heroH };
            rearVb.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 24, ShadowDepth = 16, Opacity = 0.70 };
            heroPadContainer.Children.Add(rearVb);
        }

        Canvas.SetLeft(heroPadContainer, heroCx - heroW / 2);
        Canvas.SetTop(heroPadContainer, heroFloor - heroH);
        spotlightPageHost.Children.Add(heroPadContainer);

        // Theatrical "Flip Controller ↺" Brass Action Pill below pedestal
        var flipBtn = SpotlightKit.TheatricalButton(
            spotlightDashboardRear ? "Show Front View  ↺" : "Flip Controller (Rear)  ↺",
            primary: false,
            onClick: () =>
            {
                spotlightDashboardRear = !spotlightDashboardRear;
                SpotlightShowDashboard();
            },
            height: 34,
            fontSize: 11
        );
        Canvas.SetLeft(flipBtn, heroCx - 85);
        Canvas.SetTop(flipBtn, heroFloor + 4);
        spotlightPageHost.Children.Add(flipBtn);

        // =========================================================================
        // 7. BOTTOM HALF: GAMES & PROFILES PLAYBILL SHELF (x: 280, y: 504, w: 1280)
        // =========================================================================

        // Theatrical Gold Divider Rule
        var shelfRule = new Rectangle
        {
            Width = 1280, Height = 1,
            Fill = new LinearGradientBrush
            {
                StartPoint = new(0, 0), EndPoint = new(1, 0),
                GradientStops =
                {
                    new(Colors.Transparent, 0.0),
                    new(SpotlightKit.Gold, 0.20),
                    new(SpotlightKit.GoldHi, 0.50),
                    new(SpotlightKit.Gold, 0.80),
                    new(Colors.Transparent, 1.0)
                }
            }
        };
        Canvas.SetLeft(shelfRule, 280);
        Canvas.SetTop(shelfRule, 506);
        spotlightPageHost.Children.Add(shelfRule);

        // Shelf Header: "GAMES & PROFILES", count badge, "+ Add folder", "+ Add game", "↺ Rescan"
        var shelfHeader = new DockPanel { Width = 1280 };
        var leftHeaderStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        leftHeaderStack.Children.Add(new TextBlock
        {
            Text = "GAMES & PROFILES",
            FontFamily = SpotlightKit.Display,
            FontSize = 16,
            FontWeight = FontWeights.Light,
            Foreground = SpotlightKit.GoldBrush,
            VerticalAlignment = VerticalAlignment.Center
        });
        leftHeaderStack.Children.Add(new TextBlock
        {
            Text = $" · {spotlightGamesList.Count} AVAILABLE",
            FontFamily = SpotlightKit.Body,
            FontSize = 13.5,
            Foreground = SpotlightKit.B("#a4957f"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });
        DockPanel.SetDock(leftHeaderStack, Dock.Left);
        shelfHeader.Children.Add(leftHeaderStack);

        var rightBtnsStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var addFolderBtn = SpotlightKit.TheatricalButton("+ Add folder", primary: false, onClick: () =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder",
                Title = "Add Game Library Folder"
            };
            if (ofd.ShowDialog() == true)
            {
                string? dir = System.IO.Path.GetDirectoryName(ofd.FileName);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    GameLibrary.AddFolder(dir);
                    spotlightGamesList = [];
                    SpotlightShowDashboard();
                }
            }
        }, height: 32, fontSize: 11);
        addFolderBtn.Margin = new Thickness(0, 0, 8, 0);
        rightBtnsStack.Children.Add(addFolderBtn);

        var addGameBtn = SpotlightKit.TheatricalButton("+ Add game", primary: false, onClick: () =>
        {
            var ofd = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Game Executables (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Add Game Executable"
            };
            if (ofd.ShowDialog() == true)
            {
                GameLibrary.AddGame(ofd.FileName);
                spotlightGamesList = [];
                SpotlightShowDashboard();
            }
        }, height: 32, fontSize: 11);
        addGameBtn.Margin = new Thickness(0, 0, 8, 0);
        rightBtnsStack.Children.Add(addGameBtn);

        var rescanBtn = SpotlightKit.TheatricalButton("↺ Rescan", primary: false, key: "Y", onClick: () =>
        {
            spotlightGamesList = [];
            spotlightGamesScanning = false;
            SpotlightShowDashboard();
        }, height: 32, fontSize: 11);
        rightBtnsStack.Children.Add(rescanBtn);

        shelfHeader.Children.Add(rightBtnsStack);

        Canvas.SetLeft(shelfHeader, 280);
        Canvas.SetTop(shelfHeader, 514);
        spotlightPageHost.Children.Add(shelfHeader);

        // Playbill Games Carousel ScrollViewer (y: 548, w: 1280, h: 215)
        var gamesCarousel = new StackPanel { Orientation = Orientation.Horizontal };

        foreach (var g in spotlightGamesList)
        {
            bool isSelected = (spotlightSelectedGame?.Id == g.Id);

            var gameCard = new Border
            {
                Width = 144, Height = 196,
                Margin = new Thickness(0, 0, 14, 0),
                CornerRadius = new CornerRadius(8),
                Background = SpotlightKit.B(SpotlightKit.A(SpotlightKit.C("#0d0608"), 0.90)),
                BorderBrush = SpotlightKit.B(isSelected ? SpotlightKit.GoldHi : SpotlightKit.A(SpotlightKit.Gold, 0.28)),
                BorderThickness = new Thickness(isSelected ? 2.0 : 1.0),
                Cursor = Cursors.Hand
            };
            if (isSelected)
            {
                gameCard.Effect = new DropShadowEffect
                {
                    Color = SpotlightKit.C("#ffc878"),
                    BlurRadius = 16,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }

            var cardStack = new StackPanel();

            // Cover Image Box (140x140)
            var coverBox = new Grid { Width = 140, Height = 140, ClipToBounds = true, Margin = new Thickness(1, 1, 1, 0) };
            string? coverPath = GameArtwork.PathFor(g);
            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath))
            {
                try
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(coverPath, UriKind.Absolute);
                    bi.DecodePixelWidth = 280;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    bi.Freeze();

                    var coverImg = new Image { Source = bi, Stretch = Stretch.UniformToFill };
                    RenderOptions.SetBitmapScalingMode(coverImg, BitmapScalingMode.HighQuality);
                    coverBox.Children.Add(coverImg);
                }
                catch { }
            }
            else
            {
                // Fallback Playbill Monogram Plaque
                var fallbackGrid = new Grid { Background = SpotlightKit.B(SpotlightKit.C("#180a0e")) };
                fallbackGrid.Children.Add(new TextBlock
                {
                    Text = g.Name.Length > 0 ? g.Name.Substring(0, Math.Min(3, g.Name.Length)).ToUpperInvariant() : "X20",
                    FontFamily = SpotlightKit.Display,
                    FontSize = 26,
                    FontWeight = FontWeights.Light,
                    Foreground = SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.45)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                coverBox.Children.Add(fallbackGrid);
            }

            // Store Badge Pill on top-right of cover
            var storeBadge = new Border
            {
                Background = SpotlightKit.B(SpotlightKit.A(Colors.Black, 0.75)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0),
                Child = new TextBlock
                {
                    Text = g.Store.ToUpperInvariant(),
                    FontFamily = SpotlightKit.Display,
                    FontSize = 8.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = SpotlightKit.GoldHiBrush
                }
            };
            coverBox.Children.Add(storeBadge);
            cardStack.Children.Add(coverBox);

            // Game Title Line
            var titleBlock = new TextBlock
            {
                Text = g.Name,
                FontFamily = SpotlightKit.Body,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                FontSize = 13,
                Foreground = isSelected ? SpotlightKit.B("#fff6e6") : SpotlightKit.B("#b3a48e"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(6, 8, 6, 0)
            };
            cardStack.Children.Add(titleBlock);

            gameCard.Child = cardStack;

            var chosenGame = g;
            gameCard.MouseLeftButtonDown += (_, _) =>
            {
                spotlightSelectedGame = chosenGame;
                SpotlightShowDashboard();
            };

            gamesCarousel.Children.Add(gameCard);
        }

        var shelfScroll = new ScrollViewer
        {
            Width = 1280, Height = 210,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = gamesCarousel
        };
        Canvas.SetLeft(shelfScroll, 280);
        Canvas.SetTop(shelfScroll, 550);
        spotlightPageHost.Children.Add(shelfScroll);

        // =========================================================================
        // 8. SELECTED GAME COMMAND ROW (x: 280, y: 772, w: 1280, h: 74)
        // =========================================================================
        var commandRow = new Grid { Width = 1280, Height = 74 };
        commandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        commandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        commandRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

        if (spotlightSelectedGame != null)
        {
            var curGame = spotlightSelectedGame;

            // Col 0: Game Details
            var gameMetaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            gameMetaStack.Children.Add(new TextBlock
            {
                Text = curGame.Name,
                FontFamily = SpotlightKit.Body,
                FontSize = 21,
                FontWeight = FontWeights.SemiBold,
                Foreground = SpotlightKit.B("#fff6e6"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            gameMetaStack.Children.Add(new TextBlock
            {
                Text = $"{curGame.Store} Library · Last played {(curGame.LastPlayed.HasValue ? curGame.LastPlayed.Value.ToShortDateString() : "Never")}",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 13,
                Foreground = SpotlightKit.B("#a4957f"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(gameMetaStack, 0);
            commandRow.Children.Add(gameMetaStack);

            // Col 1: Setup Dropdown + Load / Edit / Change Icon actions
            var setupActionsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            setupActionsStack.Children.Add(new TextBlock
            {
                Text = "SETUP:",
                FontFamily = SpotlightKit.Display,
                FontSize = 10.5,
                FontWeight = FontWeights.Light,
                Foreground = SpotlightKit.GoldBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            var availableSetups = setupStore.Items.Where(x => x.ModelId == Model.Id).ToArray();
            var setupCombo = new ComboBox
            {
                Width = 190,
                Height = 36,
                ItemsSource = availableSetups,
                DisplayMemberPath = "Name",
                Margin = new Thickness(0, 0, 8, 0)
            };
            if (availableSetups.Length > 0) setupCombo.SelectedIndex = 0;
            setupActionsStack.Children.Add(setupCombo);

            var loadSetupBtn = SpotlightKit.TheatricalButton("Load setup", primary: false, onClick: () =>
            {
                if (setupCombo.SelectedItem is SavedSetup chosen)
                {
                    try
                    {
                        LoadSetup(setupStore.NativeSnapshot(chosen.Id));
                        activeSetup = chosen.Id;
                        SpotlightShowDashboard();
                    }
                    catch { }
                }
            }, height: 36, fontSize: 11);
            loadSetupBtn.Margin = new Thickness(0, 0, 8, 0);
            setupActionsStack.Children.Add(loadSetupBtn);

            var editSetupsBtn = SpotlightKit.TheatricalButton("Edit", primary: false, onClick: () => SpotlightSelectPage("setups"), height: 36, fontSize: 11);
            editSetupsBtn.Margin = new Thickness(0, 0, 8, 0);
            setupActionsStack.Children.Add(editSetupsBtn);

            var changeIconBtn = SpotlightKit.TheatricalButton("Change icon", primary: false, onClick: () =>
            {
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp",
                    Title = $"Select Icon for {curGame.Name}"
                };
                if (ofd.ShowDialog() == true)
                {
                    try
                    {
                        GameArtwork.Save(curGame, ofd.FileName);
                        SpotlightShowDashboard();
                    }
                    catch { }
                }
            }, height: 36, fontSize: 11);
            setupActionsStack.Children.Add(changeIconBtn);

            Grid.SetColumn(setupActionsStack, 1);
            commandRow.Children.Add(setupActionsStack);

            // Col 2: Premier Play Action Button
            var playBtn = SpotlightKit.TheatricalButton("▶  PLAY", primary: true, key: "A", onClick: () =>
            {
                try { GameLibrary.Launch(curGame); } catch { }
            }, height: 46, fontSize: 14);
            Grid.SetColumn(playBtn, 2);
            commandRow.Children.Add(playBtn);
        }
        else
        {
            var noSelBlock = new TextBlock
            {
                Text = "Select an installed game from the shelf to launch or link a custom controller setup.",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 15,
                Foreground = SpotlightKit.B("#a4957f"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumnSpan(noSelBlock, 3);
            commandRow.Children.Add(noSelBlock);
        }

        Canvas.SetLeft(commandRow, 280);
        Canvas.SetTop(commandRow, 770);
        spotlightPageHost.Children.Add(commandRow);

        // Hints
        var hints = SpotlightParts.HintsBar(new[]
        {
            ("A", "Select / Play"),
            ("X", "Edit Setup"),
            ("Y", "Rescan Games"),
            ("B", "Controller Zone")
        });
        Canvas.SetRight(hints, 40);
        Canvas.SetTop(hints, 856);
        spotlightPageHost.Children.Add(hints);
    }

    private void SpotlightShowButtons()
    {
        spotlightPageHost!.Children.Clear();
        var canvas = new Canvas { Width = 1600, Height = 900, ClipToBounds = true };

        // Tag: Local draft
        var draftTag = SpotlightKit.Tag(Draft.UnsentChanges > 0 ? "Local draft" : "Synchronized");
        Canvas.SetLeft(draftTag, 302);
        Canvas.SetTop(draftTag, 144);
        canvas.Children.Add(draftTag);

        var noteText = new TextBlock
        {
            Text = "Not written to controller memory.",
            FontFamily = SpotlightKit.Body,
            FontStyle = FontStyles.Italic,
            FontSize = 17,
            Foreground = SpotlightKit.B("#b3a48e")
        };
        Canvas.SetLeft(noteText, 410);
        Canvas.SetTop(noteText, 146);
        canvas.Children.Add(noteText);

        // Tabs: Face · Shoulders · Sticks · System
        var tabs = SpotlightKit.Tabs(new[] { "Face", "Shoulders", "Sticks", "System" }, 0);
        Canvas.SetLeft(tabs, 302);
        Canvas.SetTop(tabs, 196);
        canvas.Children.Add(tabs);

        // Button mapping list
        var listStack = new StackPanel { Width = 520 };
        string[] controls = { "A", "B", "X", "Y" };
        foreach (var c in controls)
        {
            bool on = (c == "A");
            string target = (c == "A") ? "B" : c;

            var r = new Border
            {
                Height = 58,
                BorderBrush = on ? Brushes.Transparent : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.12)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Background = on
                    ? new RadialGradientBrush
                    {
                        Center = new(0.3, 0.5), RadiusX = 0.7, RadiusY = 1.6,
                        GradientStops = { new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.22), 0.0), new(SpotlightKit.A(SpotlightKit.C("#ffdcaf"), 0.05), 0.7), new(Colors.Transparent, 1.0) }
                    }
                    : Brushes.Transparent
            };
            if (on)
            {
                r.BorderThickness = new Thickness(3, 0, 0, 0);
                r.BorderBrush = SpotlightKit.GoldBrush;
            }

            var g = new Grid { Margin = new Thickness(20, 0, 20, 0) };
            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (on)
            {
                var lamp = new System.Windows.Shapes.Path { Data = Geometry.Parse("M3 4L13 4L11 11L5 11Z M5 11L1 18H15L11 11"), Fill = SpotlightKit.GoldBrush, Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
                leftStack.Children.Add(lamp);
            }
            leftStack.Children.Add(SpotlightKit.BrassKey(c, on, 26));

            var sendsText = new TextBlock
            {
                Text = "sends",
                FontFamily = SpotlightKit.Body,
                FontStyle = FontStyles.Italic,
                FontSize = 16,
                Foreground = SpotlightKit.MuteBrush,
                Margin = new Thickness(14, 0, 14, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(sendsText);
            leftStack.Children.Add(SpotlightKit.BrassKey(target, false, 26));
            g.Children.Add(leftStack);

            if (on)
            {
                var modText = new TextBlock
                {
                    Text = "Modified",
                    FontFamily = SpotlightKit.Body,
                    FontSize = 15.5,
                    Foreground = SpotlightKit.GoldHiBrush,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                g.Children.Add(modText);
            }

            r.Child = g;
            listStack.Children.Add(r);
        }
        Canvas.SetLeft(listStack, 292);
        Canvas.SetTop(listStack, 248);
        canvas.Children.Add(listStack);

        // Controller star on right in follow spot
        double px = 1170, baseFloor = 600, w = 560;
        var spot = SpotlightKit.FollowSpot(px, 640, 280, -60, 0.26);
        canvas.Children.Add(spot);

        var star = SpotlightParts.SpotlightStar(Model.Id, px, baseFloor, w, reflection: 0.18);
        canvas.Children.Add(star);

        // Highlight ring on A button
        var aRing = SpotlightParts.AButtonHighlight(1290, 420, 22);
        canvas.Children.Add(aRing);

        // Cue card at bottom: "Cue · A"
        var cardInner = new StackPanel();
        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var bigKeys = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bigKeys.Children.Add(SpotlightKit.BrassKey("A", true, 54));
        bigKeys.Children.Add(new TextBlock
        {
            Text = "→",
            FontFamily = SpotlightKit.Bodoni,
            FontSize = 30,
            Foreground = SpotlightKit.GildBrush,
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        bigKeys.Children.Add(SpotlightKit.BrassKey("B", false, 54));
        Grid.SetColumn(bigKeys, 0);
        topRow.Children.Add(bigKeys);

        var cueTitle = new StackPanel { Margin = new Thickness(34, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        cueTitle.Children.Add(new TextBlock { Text = "A sends B", FontFamily = SpotlightKit.Body, FontSize = 30, Foreground = SpotlightKit.ChalkBrush });
        cueTitle.Children.Add(new TextBlock { Text = "Modified · Not written to controller memory.", FontFamily = SpotlightKit.Body, FontStyle = FontStyles.Italic, FontSize = 14.5, Foreground = SpotlightKit.MuteBrush, Margin = new Thickness(0, 4, 0, 0) });
        Grid.SetColumn(cueTitle, 1);
        topRow.Children.Add(cueTitle);

        var actionBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        actionBtns.Children.Add(SpotlightKit.TheatricalButton("Reset this control", false, null, height: 42, fontSize: 12));
        var testBtn = SpotlightKit.TheatricalButton("Test mapping", false, null, height: 42, fontSize: 12);
        testBtn.Margin = new Thickness(12, 0, 0, 0);
        actionBtns.Children.Add(testBtn);
        Grid.SetColumn(actionBtns, 2);
        topRow.Children.Add(actionBtns);
        cardInner.Children.Add(topRow);

        // Choices row
        var choicesRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 22, 0, 0) };
        string[] outputs = { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS" };
        foreach (var o in outputs)
        {
            bool chosen = (o == "B");
            var box = new Border
            {
                MinWidth = 44, Height = 44,
                Padding = new Thickness(8, 0, 8, 0),
                BorderBrush = chosen ? SpotlightKit.GoldBrush : SpotlightKit.B(SpotlightKit.A(SpotlightKit.Gold, 0.25)),
                BorderThickness = new Thickness(1),
                Background = chosen ? SpotlightKit.B("#e8cf9c") : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0)
            };
            box.Child = new TextBlock
            {
                Text = o,
                FontFamily = SpotlightKit.Body,
                FontSize = 16,
                FontWeight = chosen ? FontWeights.Bold : FontWeights.Normal,
                Foreground = chosen ? SpotlightKit.B("#1a0c08") : SpotlightKit.ChalkDimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            choicesRow.Children.Add(box);
        }
        cardInner.Children.Add(choicesRow);

        var cueCard = SpotlightKit.CueCard(cardInner, 1250, label: "Cue · A", padding: new Thickness(30, 26, 30, 26));
        Canvas.SetLeft(cueCard, 292);
        Canvas.SetTop(cueCard, 640);
        canvas.Children.Add(cueCard);

        var frame = SpotlightToolFrame.WrapWithProgramme(
            currentKey: "buttons",
            title: "Buttons",
            lead: "",
            backLabel: "",
            onBack: () => SpotlightSelectPage("Dashboard"),
            onNavigate: SpotlightSelectPage,
            bodyStage: canvas,
            hints: new[] { ("A", "Assign"), ("Y", "All assignments"), ("B", "Back") });

        spotlightPageHost.Children.Add(frame);
    }
}
