using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using X20Ctl.Simulation;

namespace X20Ctl.Desktop.ViewModels;

public record SourceRow(string Name, string Detail, string Status, string Color);
public sealed class WorkspaceModel : INotifyPropertyChanged
{
    private readonly PressTimeline timeline = new();
    private Scenario scenario;
    private int slot;
    private string selectedSegment = "Inputs";
    private bool compact;
    private bool navigationDemo;
    public InputFrame Frame { get; private set; } = Simulator.Read(0, Scenario.Standard);
    public bool IsScanner { get; }
    public string Title => IsScanner ? "See every input." : "Feel the signal.";
    public string Subtitle => IsScanner ? "Confirm a changing source before the guided checks." : "Buttons, sticks and triggers. One continuous view.";
    public string SectionLabel => IsScanner ? "CONTROLLER SCANNER / INPUTS" : "CONTROLLER STUDIO / INPUT TESTER";
    public string SourceLabel => Frame.SourceKey.Contains("raw-input") ? "Raw Input · simulated collection" : $"XInput · simulated slot {Frame.Slot}";
    public string ConnectionLabel => Frame.Connected ? "SIMULATED INPUT ACTIVE" : "SIMULATED SOURCE DISCONNECTED";
    public string ConnectionColor => Frame.Connected ? "#60DCB8" : "#EFA65F";
    public bool IsRaw => Frame.SourceKey.Contains("raw-input");
    public string PressedControl => !Frame.Connected ? "No signal" : timeline.Holds.Keys.FirstOrDefault(x => x != "LT" && x != "RT" && !x.StartsWith("Usage")) ?? "Released";
    public string HeldDuration => timeline.Holds.TryGetValue(PressedControl, out double duration) ? (duration / 1000).ToString("0.00") : "0.00";
    public string LastRelease => timeline.Interrupted ? "Unfinished hold · inconclusive" : timeline.LastDurationMs.HasValue ? $"Last release  {timeline.LastDurationMs:0} ms" : "Waiting for a completed press";
    public string InputState => !Frame.Connected ? "NO SIGNAL" : PressedControl == "Released" ? "RELEASED" : "HELD";
    public string SceneSource => IsRaw ? "Raw Input · collection 1" : $"XInput · Slot {Frame.Slot}";
    public string SceneStatus => Frame.Connected ? "INPUT ACTIVE" : "DISCONNECTED";
    public string ButtonNames => !Frame.Connected ? "No input source" : Frame.Buttons == Buttons.None ? "No digital buttons held" : IsRaw ? "Numbered usages · simulated" : Frame.Buttons.ToString();
    public string LX => Frame.LX.ToString("N0");
    public string LY => Frame.LY.ToString("N0");
    public string RX => Frame.RX.ToString("N0");
    public string RY => Frame.RY.ToString("N0");
    public string TimeLabel => $"{Frame.TimeMs / 1000:0.00} s simulated";
    public string Readiness => Frame.Connected ? "Changing source illustrated" : "No changing gameplay source";
    public string ReadinessDetail => Frame.Connected ? "This preview illustrates source feedback. Scanner 2.0.0 remains the capture baseline." : "Try another scenario or keep this incomplete example for review.";
    public ObservableCollection<SourceRow> Sources { get; } = [];
    public ObservableCollection<string> Events { get; } = [];
    public Scenario Scenario
    {
        get => scenario;
        set { if (scenario == value) return; scenario = value; Reset(); OnPropertyChanged(); }
    }
    public int Slot
    {
        get => slot;
        set { if (slot == value) return; slot = value; Reset(); OnPropertyChanged(); }
    }
    public bool Paused { get; set; }
    public bool CaptureOwnsInput { get; set; }
    public bool IsCompact { get => compact; set { if (compact == value) return; compact = value; OnPropertyChanged(); } }
    public bool NavigationDemo { get => navigationDemo; set { navigationDemo = value; OnPropertyChanged(nameof(NavigationHint)); } }
    public string NavigationHint => NavigationDemo ? "GAMEPAD DEMO · arrows focus · Enter activate · Q/E tabs · F6 exit" : "Ctrl+Tab inspector · F6 keyboard gamepad demo · No hardware accessed";
    public bool ReducedMotion { get; set; } = !System.Windows.SystemParameters.ClientAreaAnimation;
    public string SelectedSegment
    {
        get => selectedSegment;
        set { selectedSegment = value; OnPropertyChanged(); }
    }
    public string Reader { get; set; } = "XInput1_4.dll";
    public IReadOnlyList<string> Readers { get; } = ["XInput1_4.dll", "XInput9_1_0.dll", "XInput1_3.dll"];
    public IReadOnlyList<string> Slots { get; } = ["Slot 0 · Player 1", "Slot 1 · Player 2", "Slot 2 · Player 3", "Slot 3 · Player 4"];
    public IReadOnlyList<string> ScenarioNames { get; } = ["Standard input", "Held button", "Disconnected", "Raw Input fallback", "Four player slots", "Source change", "Incomplete session"];
    public WorkspaceModel(bool isScanner = false) { IsScanner = isScanner; Update(0); }
    public void Reset() { timeline.Reset(); Events.Clear(); Update(0); }
    public void Update(double time)
    {
        Frame = Simulator.Read(time, Scenario, Slot);
        foreach (var edge in timeline.Observe(Frame))
        {
            Events.Insert(0, $"{edge.TimeMs / 1000:0.00}s   {edge.Control} {(edge.Down ? "DOWN" : "UP")}" + (edge.DurationMs.HasValue ? $"  ·  {edge.DurationMs:0} ms" : ""));
            if (Events.Count > 24) Events.RemoveAt(Events.Count - 1);
        }
        Sources.Clear();
        Sources.Add(new($"XInput slot {Slot}", "Simulated standard state", Frame.Connected && !IsRaw ? "ACTIVE" : "UNAVAILABLE", Frame.Connected && !IsRaw ? "#60DCB8" : "#7F90AF"));
        Sources.Add(new("Raw Input", "Separate declared-usage stream", Frame.Connected && IsRaw ? "ACTIVITY" : "DETECTED", Frame.Connected && IsRaw ? "#60DCB8" : "#AABDDD"));
        Sources.Add(new("HID collection 1", "Metadata visible · input unchanged", "STATIC", "#7F90AF"));
        foreach (string name in new[] { nameof(Frame), nameof(SourceLabel), nameof(ConnectionLabel), nameof(ConnectionColor), nameof(PressedControl), nameof(HeldDuration), nameof(LastRelease), nameof(ButtonNames), nameof(LX), nameof(LY), nameof(RX), nameof(RY), nameof(TimeLabel), nameof(Readiness), nameof(ReadinessDetail), nameof(IsRaw), nameof(InputState), nameof(SceneSource), nameof(SceneStatus) }) OnPropertyChanged(name);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
