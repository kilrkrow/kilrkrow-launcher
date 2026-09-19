using System.Text;

namespace KilrkrowLauncher.Catalog;

/// <summary>
/// Reads ZIP central-directory names from a tail slice (Range request) so catalog
/// filtering does not download a 40MB asset just to see if it contains an .exe.
/// </summary>
public static class ZipCentralDirectory
{
    private static readonly byte[] EocdSignature = [0x50, 0x4B, 0x05, 0x06];
    private static readonly byte[] CdSignature = [0x50, 0x4B, 0x01, 0x02];

    public readonly record struct Locator(long CentralDirectoryOffset, int CentralDirectorySize, int EntryCount);

    public static bool TryLocate(ReadOnlySpan<byte> tail, long fileLength, long tailStartOffset, out Locator locator)
    {
        locator = default;
        var idx = LastIndexOf(tail, EocdSignature);
        if (idx < 0 || idx + 22 > tail.Length)
            return false;

        var cdSize = BitConverter.ToInt32(tail.Slice(idx + 12, 4));
        var cdOffset = BitConverter.ToUInt32(tail.Slice(idx + 16, 4));
        var entries = BitConverter.ToUInt16(tail.Slice(idx + 10, 2));
        if (cdSize < 0 || cdOffset > (ulong)fileLength)
            return false;

        locator = new Locator((long)cdOffset, cdSize, entries);
        // Caller must fetch the CD if it is not fully inside this tail.
        _ = tailStartOffset;
        return true;
    }

    public static IReadOnlyList<string> ParseNames(ReadOnlySpan<byte> centralDirectory)
    {
        var names = new List<string>();
        var pos = 0;
        while (pos + 46 <= centralDirectory.Length)
        {
            if (!centralDirectory.Slice(pos, 4).SequenceEqual(CdSignature))
                break;

            var nameLen = BitConverter.ToUInt16(centralDirectory.Slice(pos + 28, 2));
            var extraLen = BitConverter.ToUInt16(centralDirectory.Slice(pos + 30, 2));
            var commentLen = BitConverter.ToUInt16(centralDirectory.Slice(pos + 32, 2));
            var nameStart = pos + 46;
            var nameEnd = nameStart + nameLen;
            if (nameEnd > centralDirectory.Length)
                break;

            names.Add(Encoding.UTF8.GetString(centralDirectory.Slice(nameStart, nameLen)));
            pos = nameEnd + extraLen + commentLen;
        }

        return names;
    }

    private static int LastIndexOf(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = haystack.Length - needle.Length; i >= 0; i--)
        {
            if (haystack.Slice(i, needle.Length).SequenceEqual(needle))
                return i;
        }

        return -1;
    }
}
