using X20Ctl.Linux;
namespace X20Ctl.Zone.Tests;
public class FlatpakUpdaterTests
{
    [Fact] public void Install_uses_argv_not_shell_and_user_scope()
    {
        var cmd = FlatpakUpdater.InstallCommand("/home/test/cache/a b.flatpak");
        Assert.Equal("flatpak-spawn", cmd.FileName);
        Assert.False(cmd.UseShellExecute);
        Assert.Equal(new[] { "--host", "flatpak", "install", "--user", "--noninteractive", "--or-update", "--bundle", "/home/test/cache/a b.flatpak" }, cmd.ArgumentList);
        Assert.Throws<ArgumentException>(() => FlatpakUpdater.InstallCommand("relative.flatpak"));
    }
    [Fact] public void Restart_pins_app_arch_and_branch_not_version()
    {
        Assert.Equal(new[] { "--host", "flatpak", "run", "--user", "--arch=x86_64", "--branch=stable", FlatpakUpdater.AppId }, FlatpakUpdater.RestartCommand().ArgumentList);
    }
    [Theory]
    [InlineData(0, true)] [InlineData(1, false)] [InlineData(127, false)]
    public async Task Host_install_failure_offers_fallback(int exit, bool expected)
    {
        bool invoked=false;
        var result=await FlatpakUpdater.InstallVerified("/home/test/cache/a.flatpak", _ => { invoked=true; return Task.FromResult(exit); });
        Assert.True(invoked); Assert.Equal(expected,result.Done);
        if(!expected) Assert.Contains("release page",result.Message);
    }
    [Fact] public async Task Host_denial_offers_fallback()
    {
        var result=await FlatpakUpdater.InstallVerified("/home/test/cache/a.flatpak", _ => throw new System.ComponentModel.Win32Exception("denied"));
        Assert.False(result.Done); Assert.Contains("release page",result.Message);
    }
    [Fact] public void System_install_does_not_get_shadow_user_update()
    {
        Assert.False(FlatpakUpdater.SameDeployment("[Instance]\napp-path=/var/lib/flatpak/app/id/x86_64/stable/abc", "/home/test/.local/share/flatpak/app/id/x86_64/stable/abc"));
        Assert.True(FlatpakUpdater.SameDeployment("[Instance]\napp-path=/home/test/.local/share/flatpak/app/id/x86_64/stable/abc\n", "/home/test/.local/share/flatpak/app/id/x86_64/stable/abc\n"));
        Assert.True(FlatpakUpdater.SameDeployment("[Instance]\napp-path=/home/test/.local/share/flatpak/app/id/x86_64/stable/abc/files\n", "/home/test/.local/share/flatpak/app/id/x86_64/stable/abc\n"));
        Assert.False(FlatpakUpdater.SameDeployment("", ""));
    }
}
