using X20Ctl.Mobile.Core;

namespace X20Ctl.Mobile;

public sealed class HomePage : ContentPage
{
    public HomePage()
    {
        Ui.Page(this, "X20CTL", Ui.Column(
            Ui.Heading("X20CTL for Android"),
            Ui.Body("Check your controller from your phone. This version only looks and listens: it never changes anything on the controller."),
            Ui.Card(
                new Label { Text = GattPolicy.WritesAllowed ? "Sending to the controller: on" : "Sending to the controller: locked", TextColor = Ui.Locked, FontAttributes = FontAttributes.Bold, FontSize = 15 },
                Ui.Note("Settings changes stay on the PC app until the phone's Bluetooth path is checked on a real X20. The firmware update service is never touched.")),
            Ui.Card(Ui.Body("Input"), Ui.Note("Pair the controller with the phone as a gamepad, then test every button, stick and trigger. Shows drift, range and repeated presses.")),
            Ui.Card(Ui.Body("Bluetooth"), Ui.Note("Lists nearby and paired Bluetooth LE devices and runs a read scan: services, battery and device information. Nothing is written.")),
            Ui.Card(Ui.Body("Write scan"), Ui.Note("Open the phone's own Bluetooth log of the official app changing one setting, and see what it sent. X20CTL sends nothing.")),
            Ui.Note($"Version {AppInfo.Current.VersionString}. Results stay on this phone unless you share them.")));
    }
}
