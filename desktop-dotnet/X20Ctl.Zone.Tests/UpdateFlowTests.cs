using X20Ctl.Zone;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class UpdateFlowTests
{
    static UpdateFlow Open(bool canInstall = true) { var f = new UpdateFlow("V0.019", canInstall); f.Open(); return f; }

    [Fact]
    public void Clicking_the_notice_asks_first_and_nothing_downloads_until_Yes()
    {
        var f = Open();
        Assert.Equal(UpdateStep.Confirm, f.Step);
        Assert.Contains("V0.019 will be downloaded from GitHub and installed", f.Body);
        Assert.Contains("restarts", f.Body);
    }

    [Fact]
    public void No_and_Back_from_the_question_do_nothing_but_close_it()
    {
        var no = Open(); no.No(); Assert.Equal(UpdateStep.Hidden, no.Step);
        var back = Open(); back.Back(); Assert.Equal(UpdateStep.Hidden, back.Step);
        Assert.False(back.Yes()); // nothing is waiting for an answer any more
    }

    [Fact]
    public void Yes_starts_the_download_and_progress_is_clamped()
    {
        var f = Open(); Assert.True(f.Yes());
        Assert.Equal(UpdateStep.Downloading, f.Step);
        f.Report(.42); Assert.Equal(.42, f.Progress); Assert.Equal("42%", f.Body);
        f.Report(7); Assert.Equal(1, f.Progress);
    }

    [Fact]
    public void Cancel_and_Back_while_downloading_close_it()
    {
        var a = Open(); a.Yes(); a.Cancel(); Assert.Equal(UpdateStep.Hidden, a.Step);
        var b = Open(); b.Yes(); b.Back(); Assert.Equal(UpdateStep.Hidden, b.Step);
    }

    [Fact]
    public void A_failure_says_why_in_the_app_keeps_the_install_and_allows_try_again()
    {
        var f = Open(); f.Yes(); f.Fail("The download didn't match the release's checksum, so it was thrown away.");
        Assert.Equal(UpdateStep.Failed, f.Step); Assert.True(f.CanRetry);
        Assert.Contains("checksum", f.Body); Assert.Contains("Your current version is untouched.", f.Body);
        Assert.DoesNotContain("GitHub instead", f.Body);
        Assert.True(f.Retry()); Assert.Equal(UpdateStep.Downloading, f.Step); Assert.Equal(0, f.Progress);
    }

    [Fact]
    public void A_folder_build_gets_a_plain_message_with_no_try_again()
    {
        var f = Open(canInstall: false);
        Assert.Equal(UpdateStep.Failed, f.Step); Assert.False(f.CanRetry);
        Assert.Contains("can't update itself", f.Body);
        Assert.False(f.Retry()); Assert.False(f.Yes());
        f.Close(); Assert.Equal(UpdateStep.Hidden, f.Step);
    }

    [Fact]
    public void Back_closes_a_failure()
    {
        var f = Open(); f.Yes(); f.Fail("x"); f.Back(); Assert.Equal(UpdateStep.Hidden, f.Step);
    }

    [Fact]
    public void After_a_successful_install_it_restarts_and_a_restart_failure_cannot_retry()
    {
        var f = Open(); f.Yes(); f.Installed();
        Assert.Equal(UpdateStep.Restarting, f.Step); Assert.Equal(1, f.Progress);
        f.Back(); Assert.Equal(UpdateStep.Restarting, f.Step); // not cancellable once installed
        f.RestartFailed();
        Assert.Equal(UpdateStep.Failed, f.Step); Assert.False(f.CanRetry); Assert.Contains("open it again", f.Body);
    }

    [Fact]
    public void A_late_result_after_cancelling_is_ignored()
    {
        var f = Open(); f.Yes(); f.Cancel();
        f.Installed(); f.Report(.5); Assert.Equal(UpdateStep.Hidden, f.Step);
    }
}

public class WhatsNewTests
{
    const string Log = "# Changelog\n\n## [Unreleased]\n\n- not yet\n\n## [V0.019] - 2026-11-05\n\n### Added\n\n- **One-click update.**\n\n## [V0.018] - 2026-10-20\n\n- old\n\n## [V0.017.1] - 2026-10-19\n\n- a fix\n";

    [Theory]
    [InlineData("0.019")]
    [InlineData("V0.019")]
    [InlineData("v0.019.0")]
    public void Finds_the_entry_whatever_form_the_version_is_in(string version)
    {
        string entry = Changelog.Entry(Log, version)!;
        Assert.Contains("One-click update", entry); Assert.DoesNotContain("old", entry); Assert.DoesNotContain("not yet", entry);
    }

    [Fact]
    public void A_fix_release_matches_its_own_heading_and_unknown_or_unreleased_give_nothing()
    {
        Assert.Contains("a fix", Changelog.Entry(Log, "0.017.1")!);
        Assert.Null(Changelog.Entry(Log, "0.020"));
        Assert.Null(Changelog.Entry(Log, "Unreleased"));
    }

    static string Temp() => Path.Combine(Path.GetTempPath(), "x20ctl-whatsnew-" + Guid.NewGuid().ToString("N") + ".json");
    static void Clean(string path) { foreach (var f in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(f); }
    static ProductPreferences Fresh(string path) { var p = new ProductPreferences(path); p.Load(); return p; }

    [Fact]
    public void It_is_not_shown_on_a_fresh_install_or_a_normal_launch()
    {
        string path = Temp();
        try { Assert.False(Fresh(path).ShouldShowWhatsNew("0.019")); }
        finally { Clean(path); }
    }

    [Fact]
    public void It_shows_once_for_the_version_the_update_installed_and_not_for_another()
    {
        string path = Temp();
        try
        {
            Fresh(path).SetWhatsNewPending("0.019");
            var next = Fresh(path);
            Assert.True(next.ShouldShowWhatsNew("V0.019")); Assert.False(next.ShouldShowWhatsNew("0.018"));
            next.SetWhatsNewPending(null);
            Assert.False(Fresh(path).ShouldShowWhatsNew("0.019")); // shown once, then gone
        }
        finally { Clean(path); }
    }

    [Fact]
    public void Never_remind_me_again_is_remembered_across_later_updates_and_other_settings_survive()
    {
        string path = Temp();
        try
        {
            var p = Fresh(path); p.SetIntroOnLaunch(false); p.SetWhatsNewOff();
            var later = Fresh(path); later.SetWhatsNewPending("0.020");
            var after = Fresh(path);
            Assert.True(after.WhatsNewOff); Assert.False(after.ShouldShowWhatsNew("0.020")); Assert.False(after.IntroOnLaunch);
        }
        finally { Clean(path); }
    }
}
