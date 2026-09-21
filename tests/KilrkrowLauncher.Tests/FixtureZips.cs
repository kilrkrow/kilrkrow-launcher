using System.IO.Compression;
using System.Text;

namespace KilrkrowLauncher.Tests;

internal static class FixtureZips
{
    public static byte[] SourceOnly()
    {
        return Build(zip =>
        {
            Write(zip, "README.md", "# source tree\n");
            Write(zip, "src/Program.cs", "class Program { static void Main() {} }\n");
            Write(zip, "Sideclip.csproj", "<Project />\n");
            Write(zip, "tools/helper.exe.config", "<configuration />\n");
        });
    }

    public static byte[] WithExe(string exePath = "WinServiceBuddy.App.exe")
    {
        return Build(zip =>
        {
            Write(zip, "README.md", "binaries\n");
            Write(zip, exePath, "MZ-fake");
        });
    }

    /// <summary>Sideclip-shaped payload: createdump listed first, then the real UI exe.</summary>
    public static byte[] SideclipWithCreatedump()
    {
        return Build(zip =>
        {
            WriteBytes(zip, "createdump.exe", new byte[72]);
            WriteBytes(zip, "Sideclip.exe", Enumerable.Repeat((byte)0x4D, 180).ToArray());
            Write(zip, "Sideclip.dll", "dll");
        });
    }

    public static byte[] WindowsNamedSourceOnly()
        => Build(zip =>
        {
            Write(zip, "src/win-x64/Program.cs", "// not a binary\n");
            Write(zip, "LICENSE", "MIT\n");
        });

    private static byte[] Build(Action<ZipArchive> populate)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            populate(zip);
        return buffer.ToArray();
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    private static void WriteBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        stream.Write(bytes);
    }
}
