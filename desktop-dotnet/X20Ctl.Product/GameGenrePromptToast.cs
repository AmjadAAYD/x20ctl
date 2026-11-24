using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using X20Ctl.Zone.Lighting;

namespace X20Ctl.Product;

/// <summary>
/// Non-focus-stealing top-right PS5-style dark-glass banner displayed when an unknown game
/// launches for the first time, letting the user bind its haptic genre without minimizing the game.
/// </summary>
internal static class GameGenrePromptToast
{
    const int GwlExStyle = -20;
    const int WsExNoActivate = 0x08000000;
    const int WsExToolWindow = 0x00000080;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    static Window? activeToast;

    public static void Show(string exe, string displayName, Action<HapticsGenreProfile?> onChosen)
    {
        if (ReviewSandbox.Active) return;
        if (Application.Current?.Dispatcher is not { } disp) return;

        disp.BeginInvoke(() =>
        {
            try
            {
                activeToast?.Close();
            }
            catch { }

            var win = new Window
            {
                Width = 420,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Focusable = false,
                ResizeMode = ResizeMode.NoResize,
            };

            win.SourceInitialized += (_, _) =>
            {
                var hwnd = new WindowInteropHelper(win).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int exStyle = GetWindowLong(hwnd, GwlExStyle);
                    SetWindowLong(hwnd, GwlExStyle, exStyle | WsExNoActivate | WsExToolWindow);
                }
            };

            var card = new Border
            {
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0xA2, 0xFF)),
                Background = new SolidColorBrush(Color.FromArgb(242, 11, 18, 34)),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(4),
            };

            var content = new StackPanel();
            content.Children.Add(Kit.Text($"New game detected: {displayName}", 13.5, "DS.Text", FontWeights.SemiBold)
                .Also(t => t.TextTrimming = TextTrimming.CharacterEllipsis));
            content.Children.Add(Kit.Text($"Choose haptic style for {exe} (saved for next time)", 11.5, "DS.TextSoft")
                .Also(t => t.Margin = new(0, 2, 0, 10)));

            bool handled = false;
            var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };

            void Finish(HapticsGenreProfile? picked)
            {
                if (handled) return;
                handled = true;
                closeTimer.Stop();
                activeToast = null;
                try { win.Close(); } catch { }
                onChosen(picked);
            }

            closeTimer.Tick += (_, _) => Finish(null);

            var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new(-3, 0, -3, 0) };
            (string Label, HapticsGenreProfile? Profile)[] choices =
            [
                ("Racing", HapticsGenreProfile.Racing),
                ("FPS / Action", HapticsGenreProfile.FpsAction),
                ("RPG / Melee", HapticsGenreProfile.RpgMelee),
                ("Music", HapticsGenreProfile.Music),
                ("Cinema", HapticsGenreProfile.Cinema),
                ("Ignore", null),
            ];

            foreach (var (label, prof) in choices)
            {
                var p = prof;
                var btn = new Button
                {
                    Content = label,
                    Style = DS.Style("DS.ConsoleBtn"),
                    Margin = new(3),
                    Padding = new(8, 5, 8, 5),
                    MinHeight = 30,
                    FontSize = 12,
                };
                btn.Click += (_, _) => Finish(p);
                grid.Children.Add(btn);
            }

            content.Children.Add(grid);
            card.Child = content;
            win.Content = card;

            var work = SystemParameters.WorkArea;
            win.Left = Math.Max(work.Left + 12, work.Right - win.Width - 18);
            win.Top = work.Top + 18;
            win.Closed += (_, _) => closeTimer.Stop();

            activeToast = win;
            closeTimer.Start();
            win.Show();
        });
    }
}
