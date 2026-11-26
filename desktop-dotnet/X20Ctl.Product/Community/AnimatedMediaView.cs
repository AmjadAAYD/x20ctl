using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace X20Ctl.Product.Community;

public sealed class AnimatedMediaView : Border
{
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _fallback = Kit.Text("", 13, "DS.TextSoft", FontWeights.SemiBold);
    private readonly List<(BitmapSource Frame, int DelayMs)> _frames = new();
    private DispatcherTimer? _timer;
    private int _frameIndex;

    public bool IsPixelArt { get; set; }

    public AnimatedMediaView(double width, double height, bool pixelArt = false, double cornerRadius = 8)
    {
        Width = width;
        Height = height;
        IsPixelArt = pixelArt;
        CornerRadius = new CornerRadius(cornerRadius);
        Background = Brushes.Black;
        ClipToBounds = true;

        RenderOptions.SetBitmapScalingMode(
            _image,
            pixelArt ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);

        _fallback.HorizontalAlignment = HorizontalAlignment.Center;
        _fallback.VerticalAlignment = VerticalAlignment.Center;
        var host = new Grid();
        host.Children.Add(_fallback);
        host.Children.Add(_image);
        Child = host;

        Unloaded += (_, _) => StopAnimation();
        Loaded += (_, _) =>
        {
            if (_frames.Count > 1 && _timer != null && !_timer.IsEnabled)
                _timer.Start();
        };
    }

    public void SetPlaceholder(string label, Color? tint = null)
    {
        StopAnimation();
        _frames.Clear();
        _image.Source = null;
        _fallback.Text = label;
        Background = tint.HasValue
            ? new SolidColorBrush(Color.FromArgb(40, tint.Value.R, tint.Value.G, tint.Value.B))
            : Brushes.Black;
    }

    public bool LoadBytes(byte[]? bytes, string? fallbackLabel = null)
    {
        StopAnimation();
        _frames.Clear();
        if (bytes == null || bytes.Length < 8)
        {
            if (fallbackLabel != null) SetPlaceholder(fallbackLabel);
            return false;
        }

        try
        {
            using var ms = new MemoryStream(bytes, writable: false);
            var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
            {
                if (fallbackLabel != null) SetPlaceholder(fallbackLabel);
                return false;
            }

            foreach (var frame in decoder.Frames)
            {
                int delayMs = 90;
                if (frame.Metadata is BitmapMetadata meta)
                {
                    try
                    {
                        if (meta.GetQuery("/grctlext/Delay") is ushort rawDelay)
                            delayMs = Math.Clamp(rawDelay * 10, 20, 5000);
                    }
                    catch
                    {
                        delayMs = 90;
                    }
                }
                if (!frame.IsFrozen && frame.CanFreeze) frame.Freeze();
                _frames.Add((frame, delayMs));
            }

            _fallback.Text = "";
            Background = Brushes.Black;
            _frameIndex = 0;
            _image.Source = _frames[0].Frame;

            if (_frames.Count > 1)
            {
                _timer = new DispatcherTimer(DispatcherPriority.Render)
                {
                    Interval = TimeSpan.FromMilliseconds(_frames[0].DelayMs)
                };
                _timer.Tick += (_, _) =>
                {
                    if (_frames.Count == 0) return;
                    _frameIndex = (_frameIndex + 1) % _frames.Count;
                    var next = _frames[_frameIndex];
                    _image.Source = next.Frame;
                    if (_timer != null)
                        _timer.Interval = TimeSpan.FromMilliseconds(next.DelayMs);
                };
                _timer.Start();
            }
            return true;
        }
        catch
        {
            if (fallbackLabel != null) SetPlaceholder(fallbackLabel);
            return false;
        }
    }

    public void StopAnimation()
    {
        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
        }
    }
}
