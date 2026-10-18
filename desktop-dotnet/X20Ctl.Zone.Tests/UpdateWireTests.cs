using X20Ctl.Linux;
using Xunit;

namespace X20Ctl.Zone.Tests;

public class UpdateWireTests
{
    const string Sums = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  X20CTL-4.1.0-linux-x86_64.AppImage\n"
        + "FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210FEDCBA9876543210 *X20CTL-4.1.0-windows-x64.exe\n"
        + "tooshort  X20CTL-4.1.0-linux-x64.tar.gz\n";

    [Fact]
    public void Finds_the_hash_listed_for_a_file()
    {
        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", UpdateWire.Checksum(Sums, "X20CTL-4.1.0-linux-x86_64.AppImage"));
        Assert.Equal("fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210", UpdateWire.Checksum(Sums, "X20CTL-4.1.0-windows-x64.exe")); // binary-mode star, upper case
    }

    [Fact]
    public void A_missing_file_or_a_malformed_hash_gives_nothing()
    {
        Assert.Null(UpdateWire.Checksum(Sums, "X20CTL-4.1.0-linux-musl-x64.tar.gz"));
        Assert.Null(UpdateWire.Checksum(Sums, "X20CTL-4.1.0-linux-x64.tar.gz"));
        Assert.Null(UpdateWire.Checksum(Sums, "AppImage")); // a name that is only the end of another file's name is not a match
    }

    [Fact]
    public void Only_the_projects_own_release_downloads_are_trusted()
    {
        Assert.True(UpdateWire.Trusted("https://github.com/AmjadAAYD/x20ctl/releases/download/v4.1.0/X20CTL-4.1.0-linux-x86_64.AppImage"));
        Assert.False(UpdateWire.Trusted("https://github.com/someone-else/x20ctl/releases/download/v4.1.0/a.AppImage"));
        Assert.False(UpdateWire.Trusted("http://github.com/AmjadAAYD/x20ctl/releases/download/v4.1.0/a.AppImage"));
        Assert.False(UpdateWire.Trusted("https://github.com/AmjadAAYD/x20ctl/releases/download.evil.example/a"));
    }

    [Fact]
    public void Picks_the_x86_64_appimage_and_not_its_zsync_or_the_tarball()
    {
        string[] assets = ["X20CTL-4.1.0-linux-x64.tar.gz", "X20CTL-4.1.0-linux-x86_64.AppImage.zsync", "X20CTL-4.1.0-linux-x86_64.AppImage", "SHA256SUMS.txt"];
        Assert.Equal("X20CTL-4.1.0-linux-x86_64.AppImage", UpdateWire.AppImageName(assets));
        Assert.Null(UpdateWire.AppImageName(["X20CTL-4.1.0-linux-x64.tar.gz"]));
    }
}
