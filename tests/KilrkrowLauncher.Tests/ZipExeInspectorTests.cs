using System.IO.Compression;
using KilrkrowLauncher.Catalog;

namespace KilrkrowLauncher.Tests;

public sealed class ZipExeInspectorTests
{
    [Fact]
    public void SourceOnlyFixture_HasNoExe()
    {
        using var stream = new MemoryStream(FixtureZips.SourceOnly());
        var names = ZipExeInspector.ListEntries(stream);
        Assert.False(ZipExeInspector.ContainsWindowsExe(names));
        Assert.Contains(names, n => n.EndsWith("helper.exe.config", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WithExeFixture_IsDetected()
    {
        using var stream = new MemoryStream(FixtureZips.WithExe("tools/App.exe"));
        Assert.True(ZipExeInspector.StreamContainsWindowsExe(stream));
    }

    [Fact]
    public void CentralDirectory_RoundTripsNames()
    {
        var bytes = FixtureZips.WithExe("payload/Tool.exe");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var expected = zip.Entries.Select(e => e.FullName).ToArray();

        if (!ZipCentralDirectory.TryLocate(bytes, bytes.Length, 0, out var locator))
            throw new Xunit.Sdk.XunitException("EOCD should be in the full file bytes.");

        var cd = bytes.AsSpan((int)locator.CentralDirectoryOffset, locator.CentralDirectorySize);
        var names = ZipCentralDirectory.ParseNames(cd);
        Assert.Equal(expected, names);
        Assert.True(ZipExeInspector.ContainsWindowsExe(names));
    }
}
