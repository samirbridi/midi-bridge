namespace Bridge.Core.Devices;

public static class VidPidParser
{
    public static bool TryParseVidPid(string hardwareId, out int vid, out int pid)
    {
        vid = 0;
        pid = 0;

        if (string.IsNullOrWhiteSpace(hardwareId))
        {
            return false;
        }

        var span = hardwareId.AsSpan();
        if (!TryFindHexAfterToken(span, "VID_", out vid))
        {
            return false;
        }

        if (!TryFindHexAfterToken(span, "PID_", out pid))
        {
            return false;
        }

        return true;
    }

    private static bool TryFindHexAfterToken(ReadOnlySpan<char> span, ReadOnlySpan<char> token, out int value)
    {
        value = 0;
        var idx = IndexOfIgnoreCase(span, token);
        if (idx < 0)
        {
            return false;
        }

        var start = idx + token.Length;
        if (start + 4 > span.Length)
        {
            return false;
        }

        var hex = span.Slice(start, 4);
        return int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static int IndexOfIgnoreCase(ReadOnlySpan<char> span, ReadOnlySpan<char> value)
    {
        for (var i = 0; i <= span.Length - value.Length; i++)
        {
            var ok = true;
            for (var j = 0; j < value.Length; j++)
            {
                if (char.ToUpperInvariant(span[i + j]) != char.ToUpperInvariant(value[j]))
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                return i;
            }
        }

        return -1;
    }
}

