using System.Text;

namespace KilrkrowLauncher.Native;

/// <summary>
/// Minimal Shell Link parser for LocalBasePath. Enough for Start Menu detection.
/// </summary>
internal static class LnkReader
{
    public static string? TryReadTarget(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 0x4C || BitConverter.ToInt32(bytes, 0) != 0x4C)
                return null;

            var flags = BitConverter.ToInt32(bytes, 0x14);
            var pos = 0x4C;
            if ((flags & 0x01) != 0)
            {
                if (pos + 2 > bytes.Length)
                    return null;
                var idLen = BitConverter.ToUInt16(bytes, pos);
                pos += 2 + idLen;
            }

            if ((flags & 0x02) == 0 || pos + 0x1C > bytes.Length)
                return null;

            var linkInfoSize = BitConverter.ToInt32(bytes, pos);
            if (linkInfoSize < 0x1C || pos + linkInfoSize > bytes.Length)
                return null;

            var localBasePathOffset = BitConverter.ToInt32(bytes, pos + 0x10);
            if (localBasePathOffset <= 0 || localBasePathOffset >= linkInfoSize)
                return null;

            var start = pos + localBasePathOffset;
            var end = start;
            while (end < bytes.Length && bytes[end] != 0)
                end++;
            if (end <= start)
                return null;
            return Encoding.Default.GetString(bytes, start, end - start);
        }
        catch
        {
            return null;
        }
    }
}
