using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using X20Ctl.Product.Rebel;

namespace X20Ctl.Product;

public partial class StudioScene
{
    private bool hybridDeviceWired;
    private void HybridDevicePage()
    {
        var device = Device; if (hybridDevice == null || device == null) return;
        if (!hybridDeviceWired) { hybridDeviceWired = true; device.Watch(() => { if (ManagementPage == "Device") HybridLater(); }); }
        hybridDevice.Children.Clear(); var board = new Canvas { Width = 1600, Height = 848, Background = HBrush("#17151c"), ClipToBounds = true }; hybridDevice.Children.Add(board);
        HAt(board, new Rectangle { Width = 156, Height = 848, Fill = HBrush("#cbb5e4") }, 0, 0); HAt(board, new Rectangle { Width = 140, Height = 848, Fill = HBrush("#29262f") }, 1460, 0);
        HAt(board, new Rectangle { Width = 3, Height = 670, Fill = HBrush("#52dcff") }, 846, 108);
        HAt(board, HText("Device", 38, "#f5f1fa", FontWeights.ExtraBold), 216, 14); HAt(board, HText("Assigned model · Gameplay and configuration remain separate.", 14, "#bdb4c8", FontWeights.SemiBold), 218, 64);
        var pages = new (string label, string key)[] { ("Buttons", "buttons"), ("Curves", "curves"), ("Macros", "macros"), ("Vibration", "vibration"), ("PC mode", "pcmode"), ("Device", "device"), ("Mouse", "mouse"), ("Setups", "setups"), ("Tools", "tools") };
        for (int i = 0; i < pages.Length; i++) { var (label, key) = pages[i]; var nav = HybridButton(label, "hybrid:nav:" + key, key == "device", 120, 50); nav.Click += (_, _) => HybridGo(key); HAt(board, nav, 18, 88 + i * 67); }
        HAt(board, Pieces.Pad(Model.Id, PadColours.For(Player, Model.Id), 540), 230, 108); HAt(board, HText($"PLAYER {Player + 1} · {Model.Name}", 17, "#f5f1fa", FontWeights.Bold), 230, 478);
        var profile = ModelProfiles.For(Model.Id);
        var capabilities = new (string label, string value)[] { ("Sticks", profile.Sticks), ("Triggers", profile.Triggers), ("Rear controls", profile.Paddles.Length == 0 ? "None listed" : string.Join(" · ", profile.Paddles)), ("Motors", profile.Motors), ("Motion", profile.Motion) };
        HAt(board, HText("MODEL CAPABILITIES", 13, "#bdb4c8", FontWeights.Bold), 230, 530);
        for (int i = 0; i < capabilities.Length; i++) { var (label, value) = capabilities[i]; var tile = new Border { Width = 272, Height = 60, Background = HBrush("#29242f"), CornerRadius = new(12), Padding = new(12, 6, 12, 4), Child = new StackPanel { Children = { HText(label, 12, "#bdb4c8", FontWeights.Normal), HText(value, 15, "#f5f1fa", FontWeights.SemiBold) } } }; HAt(board, tile, 230 + i % 2 * 284, 553 + i / 2 * 68); }
        var facts = new (string label, string value)[] { ("Connection", device.ConnectionText), ("Battery", device.BatteryText), ("Firmware", device.FirmwareText), ("Hardware revision", device.RevisionText), ("Transport", device.TransportText) };
        for (int i = 0; i < facts.Length; i++) { var (label, value) = facts[i]; var row = new Grid { Width = 492, Height = 55, Background = HBrush("#29242f"), Margin = new(0) }; var l = HText(label, 16, "#f5f1fa", FontWeights.SemiBold); l.Margin = new(16, 0, 0, 0); l.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(l); var v = HText(value, 14, "#bdb4c8", FontWeights.Normal); v.HorizontalAlignment = HorizontalAlignment.Right; v.VerticalAlignment = VerticalAlignment.Center; v.Margin = new(0, 0, 16, 0); row.Children.Add(v); HAt(board, row, 892, 108 + i * 64); }
        var diagnostics = HybridButton("Open Diagnostics", "hybrid:device:diagnostics", true, 240, 50); diagnostics.Click += (_, _) => device.OpenDiagnostics(); HAt(board, diagnostics, 892, 485);
        if (device.CanRefreshBattery) { var refresh = HybridButton("Refresh battery", "hybrid:device:refresh", false, 210, 50); refresh.Click += (_, _) => device.RefreshBattery(); HAt(board, refresh, 1145, 485); }
        HAt(board, HText("Live input and configuration access are checked separately.", 14, "#bdb4c8", FontWeights.Normal), 892, 560);
    }
}
