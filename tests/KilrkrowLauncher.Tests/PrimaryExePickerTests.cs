using KilrkrowLauncher.Catalog;
using KilrkrowLauncher.Detection;

namespace KilrkrowLauncher.Tests;

public sealed class PrimaryExePickerTests
{
    [Fact]
    public void SideclipZipEntries_PreferSideclipOverCreatedump()
    {
        using var stream = new MemoryStream(FixtureZips.SideclipWithCreatedump());
        var entries = ZipExeInspector.EnumerateWindowsExes(ZipExeInspector.ListEntries(stream));
        Assert.Contains(entries, n => n.EndsWith("createdump.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(entries, n => n.EndsWith("Sideclip.exe", StringComparison.OrdinalIgnoreCase));

        var pick = PrimaryExePicker.Pick(
            entries,
            repo: "sideclip",
            displayName: "Sideclip",
            preferredFileNames: ["createdump.exe", "Sideclip.exe"],
            getLength: name => name.Contains("createdump", StringComparison.OrdinalIgnoreCase) ? 71_992 : 180_224);

        Assert.Equal("Sideclip.exe", Path.GetFileName(pick));
    }

    [Fact]
    public void CreatedumpIsNoise_EvenWhenPreferredFirst()
    {
        Assert.True(PrimaryExePicker.IsNoiseName("createdump.exe"));
        Assert.True(PrimaryExePicker.IsNoiseName(@"C:\apps\createdump.exe"));
        Assert.False(PrimaryExePicker.IsNoiseName("Sideclip.exe"));

        var pick = PrimaryExePicker.Pick(
            ["createdump.exe", "crashpad_handler.exe", "Sideclip.exe"],
            "sideclip",
            "Sideclip",
            preferredFileNames: ["createdump.exe"]);

        Assert.Equal("Sideclip.exe", pick);
    }

    [Fact]
    public void LargerUiExe_WinsWhenNamesAreTied()
    {
        var pick = PrimaryExePicker.Pick(
            ["HelperHost.exe", "ToolHost.exe"],
            repo: "other",
            displayName: "Other",
            getLength: name => name.Contains("Helper", StringComparison.OrdinalIgnoreCase) ? 8_000 : 180_000);

        Assert.Equal("ToolHost.exe", Path.GetFileName(pick));
    }

    [Fact]
    public void AllNoise_ReturnsNull()
    {
        var pick = PrimaryExePicker.Pick(
            ["createdump.exe", "uninstall.exe", "vcredist.exe"],
            "sideclip",
            "Sideclip");
        Assert.Null(pick);
    }
}
