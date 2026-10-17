using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using X20Ctl.Zone;
namespace X20Ctl.Linux;

/// <summary>Sequences for the back paddles: each step holds buttons for a time, then waits. In PC mode a button you pick plays the macro.</summary>
internal sealed class MacrosPage : UserControl
{
    readonly StudioState state; string slot; int step = -1;
    public MacrosPage(StudioState state)
    {
        this.state = state;
        slot = state.Macros.SlotNames.FirstOrDefault() ?? "M1";
        Build();
    }

    void Build()
    {
        var m = state.Macros;
        var slots = Ui.Row();
        foreach (string s in m.SlotNames) { string name = s; slots.Children.Add(Ui.Chip(name + (m[name].Count > 0 ? " · " + m[name].Count : ""), name == slot, () => { slot = name; step = -1; Build(); })); }

        var list = new StackPanel { Spacing = 8 };
        var events = m[slot];
        if (events.Count == 0) list.Children.Add(Ui.Label("No steps yet. Add one to start " + slot + ".", 13, Ui.Quiet));
        for (int i = 0; i < events.Count; i++)
        {
            int index = i; var e = events[i];
            string what = e.Buttons.Count == 0 ? "Wait" : string.Join(" + ", e.Buttons);
            var row = new Border
            {
                Background = index == step ? Ui.Accent : Ui.Raised, CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 9),
                Child = new Grid { ColumnDefinitions = new("*,Auto"), Children = { Ui.Label((index + 1) + "  " + what, 14), Right(e) } }
            };
            row.PointerPressed += (_, _) => { step = index; Build(); };
            list.Children.Add(row);
        }
        var actions = Ui.Row(
            Ui.Chip("Add step", false, () => { try { m.Add(slot); step = m[slot].Count - 1; } catch (InvalidOperationException) { } Build(); }),
            Ui.Chip("Clear " + slot, false, () => { m.Clear(slot); step = -1; Build(); }));
        var left = Ui.Column(slots, Ui.Label(m.EntryCount(slot) + " entries · " + m.Duration(slot) + " ms", 12, Ui.Quiet), list, actions);

        var right = new StackPanel { Spacing = 12, Width = 380 };
        right.Children.Add(Ui.Label(step < 0 ? "Select a step" : "Step " + (step + 1), 18, null, FontWeight.SemiBold));
        if (step >= 0 && step < events.Count) Editor(right, events[step]);
        else right.Children.Add(Ui.Label("Pick a step to change its buttons and timing.", 13, Ui.Quiet));
        right.Children.Add(Trigger());

        var grid = new Grid { ColumnDefinitions = new("*,Auto") };
        grid.Children.Add(Ui.Panel(left));
        var side = Ui.Panel(right); side.Margin = new Thickness(14, 0, 0, 0); Grid.SetColumn(side, 1); grid.Children.Add(side);
        Content = new ScrollViewer { Content = grid };
    }

    static Control Right(MacroEvent e) { var t = Ui.Label(e.HoldMs + " ms + " + e.PauseMs + " ms", 12, Ui.Quiet); t.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(t, 1); return t; }

    void Editor(StackPanel host, MacroEvent e)
    {
        var buttons = new WrapPanel();
        foreach (string input in MacroDraft.Inputs)
        {
            string b = input; bool on = e.Buttons.Contains(b);
            var chip = Ui.Chip(b, on, () => { var next = on ? e.Buttons.Where(x => x != b).ToList() : [.. e.Buttons, b]; Update(e with { Buttons = next }); });
            chip.Margin = new Thickness(0, 0, 6, 6); buttons.Children.Add(chip);
        }
        host.Children.Add(buttons);
        host.Children.Add(Number("Hold (ms)", e.HoldMs, 10, 2000, v => Update(e with { HoldMs = v })));
        host.Children.Add(Number("Pause after (ms)", e.PauseMs, 0, 2000, v => Update(e with { PauseMs = v })));
        host.Children.Add(Ui.Row(
            Ui.Chip("Duplicate", false, () => { state.Macros.Duplicate(slot, step); Build(); }),
            Ui.Chip("Remove", false, () => { state.Macros.Remove(slot, step); step = Math.Min(step, state.Macros[slot].Count - 1); Build(); })));
    }

    void Update(MacroEvent next) { try { state.Macros.Update(slot, step, next); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { } Build(); }

    Control Number(string label, int value, int min, int max, Action<int> set)
    {
        var box = new NumericUpDown { Minimum = min, Maximum = max, Value = value, Increment = 10, FormatString = "0", Width = 160, HorizontalAlignment = HorizontalAlignment.Left };
        box.ValueChanged += (_, _) => { int v = (int)(box.Value ?? value); if (v != value) set(v); };
        return Ui.Column(Ui.Label(label, 12, Ui.Quiet), box);
    }

    Control Trigger()
    {
        var m = state.Macros; string? current = m.Trigger(slot);
        var wrap = new WrapPanel();
        foreach (string input in MacroDraft.TriggerInputs.Prepend("None"))
        {
            string b = input; bool on = (current ?? "None") == b;
            var chip = Ui.Chip(b, on, () => { try { m.SetTrigger(slot, b == "None" ? null : b); } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { } Build(); });
            chip.Margin = new Thickness(0, 0, 6, 6); wrap.Children.Add(chip);
        }
        return Ui.Column(Ui.Label("Played by", 12, Ui.Quiet), wrap);
    }
}
