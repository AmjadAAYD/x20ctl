using X20Ctl.Zone;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class AutoUpdateDefaultTests
{
    static string Temp() => Path.Combine(Path.GetTempPath(), "x20ctl-prefs-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public void Keeping_up_to_date_is_on_for_a_fresh_install()
    {
        var prefs = new ProductPreferences(Temp()); prefs.Load();
        Assert.True(prefs.AutoUpdate);
    }

    [Fact]
    public void An_older_file_with_no_choice_recorded_gets_the_new_default()
    {
        string path = Temp(); File.WriteAllText(path, "{\"version\":1,\"introSeen\":true}");
        try { var prefs = new ProductPreferences(path); prefs.Load(); Assert.True(prefs.AutoUpdate); } finally { File.Delete(path); }
    }

    [Fact]
    public void A_player_who_turned_it_off_stays_off()
    {
        string path = Temp(); File.WriteAllText(path, "{\"version\":1,\"autoUpdate\":false}");
        try { var prefs = new ProductPreferences(path); prefs.Load(); Assert.False(prefs.AutoUpdate); } finally { File.Delete(path); }
    }

    [Fact]
    public void Turning_it_off_is_remembered()
    {
        string path = Temp();
        try
        {
            var prefs = new ProductPreferences(path); prefs.Load(); prefs.SetAutoUpdate(false);
            var again = new ProductPreferences(path); again.Load(); Assert.False(again.AutoUpdate);
        }
        finally { foreach (string f in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(f); }
    }
}
