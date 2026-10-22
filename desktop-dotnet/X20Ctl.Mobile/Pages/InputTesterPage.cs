using X20Ctl.Mobile.Core;
using X20Ctl.Zone.Diagnostics;

namespace X20Ctl.Mobile;

/// <summary>Live gamepad input from a controller paired with the phone. Ordinary gameplay input, read-only.</summary>
public sealed class InputTesterPage : ContentPage
{
    readonly GraphicsView view = new() { HeightRequest = 340, Drawable = new PadDrawing(Gamepad.Pad) };
    readonly Label summary = Ui.Mono();
    IDispatcherTimer? timer;

    public InputTesterPage()
    {
        Ui.Page(this, "Input", Ui.Column(
            Ui.Heading("Input tester"),
            Ui.Note("Pair the controller in Android's Bluetooth settings (or connect it by USB), then press buttons and move the sticks. While this page is open, the controller's buttons are measured here instead of moving around the app."),
            Ui.Card(view),
            Ui.Card(summary),
            Ui.Action("Reset", (_, _) => Gamepad.Pad.Reset(), primary: false),
            Ui.Action("Share results", async (_, _) => await Ui.ShareText("x20ctl-input.txt", Gamepad.Pad.Summary(Gamepad.Now)), primary: false),
            Ui.Note("Android's event rate is not the controller's report rate. Buttons X20CTL can't see here (such as rear buttons that send nothing to the phone) are not tested.")));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Gamepad.Active = true;
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += (_, _) => { view.Invalidate(); summary.Text = Gamepad.Pad.Summary(Gamepad.Now); };
        timer.Start();
    }

    protected override void OnDisappearing()
    {
        Gamepad.Active = false;
        timer?.Stop(); timer = null;
        base.OnDisappearing();
    }
}

/// <summary>Both sticks with their reach traces, the triggers as bars, and every button as a chip that lights while held.</summary>
sealed class PadDrawing(PadSession pad) : IDrawable
{
    public void Draw(ICanvas canvas, RectF rect)
    {
        float w = rect.Width, r = Math.Min(w / 4.6f, 70);
        Stick(canvas, pad.LeftStats, w * .25f, 24 + r, r, "L");
        Stick(canvas, pad.RightStats, w * .75f, 24 + r, r, "R");
        float barTop = 24 + 2 * r + 20;
        Bar(canvas, 16, barTop, w / 2 - 24, "LT", pad.LeftTrigger.Value);
        Bar(canvas, w / 2 + 8, barTop, w / 2 - 24, "RT", pad.RightTrigger.Value);
        float x = 16, y = barTop + 40, chip = 50;
        foreach (var (mask, name) in PadSession.Buttons)
        {
            if (x + chip > w - 8) { x = 16; y += 32; }
            bool down = (pad.State & mask) != 0;
            canvas.FillColor = down ? Ui.Accent : Ui.Background;
            canvas.FillRoundedRectangle(x, y, chip - 6, 26, 8);
            canvas.FontColor = down ? Ui.Background : (pad.Presses[mask].Presses > 0 ? Ui.Good : Ui.Muted);
            canvas.FontSize = 12;
            canvas.DrawString(name, x, y, chip - 6, 26, HorizontalAlignment.Center, VerticalAlignment.Center);
            x += chip;
        }
    }

    static void Stick(ICanvas canvas, StickStats s, float cx, float cy, float r, string name)
    {
        canvas.StrokeColor = Ui.Line; canvas.StrokeSize = 1.5f;
        canvas.DrawCircle(cx, cy, r);
        canvas.DrawLine(cx - r, cy, cx + r, cy); canvas.DrawLine(cx, cy - r, cx, cy + r);
        // the furthest reach in each 5° direction
        var path = new PathF();
        for (int i = 0; i < StickStats.Sectors; i++)
        {
            double reach = Math.Min(s.Reach[i], 1.2), angle = (i + .5) / StickStats.Sectors * 2 * Math.PI - Math.PI;
            float px = cx + (float)(Math.Cos(angle) * reach * r), py = cy - (float)(Math.Sin(angle) * reach * r);
            if (i == 0) path.MoveTo(px, py); else path.LineTo(px, py);
        }
        path.Close();
        canvas.StrokeColor = Ui.Good; canvas.StrokeSize = 1;
        canvas.DrawPath(path);
        canvas.FillColor = Ui.Accent;
        canvas.FillCircle(cx + s.X / 32767f * r, cy - s.Y / 32767f * r, 7);
        canvas.FontColor = Ui.Muted; canvas.FontSize = 12;
        canvas.DrawString(name, cx - r, cy - r - 18, 20, 16, HorizontalAlignment.Left, VerticalAlignment.Top);
    }

    static void Bar(ICanvas canvas, float x, float y, float width, string name, byte value)
    {
        canvas.FillColor = Ui.Background; canvas.FillRoundedRectangle(x + 28, y, width - 28, 14, 7);
        canvas.FillColor = Ui.Accent; canvas.FillRoundedRectangle(x + 28, y, (width - 28) * value / 255f, 14, 7);
        canvas.FontColor = Ui.Muted; canvas.FontSize = 12;
        canvas.DrawString(name, x, y - 1, 26, 16, HorizontalAlignment.Left, VerticalAlignment.Center);
    }
}
