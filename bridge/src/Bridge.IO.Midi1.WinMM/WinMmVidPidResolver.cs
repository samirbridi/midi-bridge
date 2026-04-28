using System.Runtime.InteropServices;
using Bridge.Core.Devices;

namespace Bridge.IO.Midi1.WinMM;

public sealed class WinMmVidPidResolver
{
    private readonly Lazy<IReadOnlyList<Entry>> _entries;

    public WinMmVidPidResolver()
    {
        _entries = new Lazy<IReadOnlyList<Entry>>(BuildEntries, isThreadSafe: true);
    }

    public bool TryResolve(string winMmDeviceName, out int vid, out int pid)
    {
        vid = 0;
        pid = 0;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(winMmDeviceName))
        {
            return false;
        }

        var name = Normalize(winMmDeviceName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        Entry? best = null;
        var bestScore = 0;

        foreach (var e in _entries.Value)
        {
            if (e.Vid is null || e.Pid is null)
            {
                continue;
            }

            var score = Score(name, e.NormalizedName);
            if (score > bestScore)
            {
                bestScore = score;
                best = e;
            }
        }

        if (best is null || bestScore <= 0)
        {
            return false;
        }

        vid = best.Vid!.Value;
        pid = best.Pid!.Value;
        return true;
    }

    private static int Score(string a, string b)
    {
        if (a.Contains(b, StringComparison.OrdinalIgnoreCase) || b.Contains(a, StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        var ta = Tokens(a);
        var tb = Tokens(b);
        if (ta.Count == 0 || tb.Count == 0)
        {
            return 0;
        }

        var hits = 0;
        foreach (var t in ta)
        {
            if (tb.Contains(t))
            {
                hits++;
            }
        }

        return hits;
    }

    private static HashSet<string> Tokens(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            var t = p.Trim();
            if (t.Length < 3)
            {
                continue;
            }
            set.Add(t);
        }

        return set;
    }

    private static string Normalize(string s)
    {
        var cleaned = s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned;
    }

    private static IReadOnlyList<Entry> BuildEntries()
    {
        var list = new List<Entry>();

        nint h = 0;
        try
        {
            h = SetupApiNative.SetupDiGetClassDevsW(0, null, 0, SetupApiNative.DIGCF_PRESENT | SetupApiNative.DIGCF_ALLCLASSES);
            if (h == 0 || h == -1)
            {
                return list;
            }

            for (uint i = 0; ; i++)
            {
                var data = new SetupApiNative.SpDevinfoData { cbSize = (uint)Marshal.SizeOf<SetupApiNative.SpDevinfoData>() };
                if (!SetupApiNative.SetupDiEnumDeviceInfo(h, i, ref data))
                {
                    break;
                }

                var name = TryGetStringProperty(h, ref data, SetupApiNative.SPDRP_FRIENDLYNAME)
                           ?? TryGetStringProperty(h, ref data, SetupApiNative.SPDRP_DEVICEDESC);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var hardwareIds = TryGetMultiStringProperty(h, ref data, SetupApiNative.SPDRP_HARDWAREID);
                int? vid = null;
                int? pid = null;
                foreach (var hid in hardwareIds)
                {
                    if (VidPidParser.TryParseVidPid(hid, out var v, out var p))
                    {
                        vid = v;
                        pid = p;
                        break;
                    }
                }

                list.Add(new Entry(Normalize(name), vid, pid));
            }
        }
        catch
        {
            return list;
        }
        finally
        {
            if (h != 0 && h != -1)
            {
                SetupApiNative.SetupDiDestroyDeviceInfoList(h);
            }
        }

        return list;
    }

    private static string? TryGetStringProperty(nint deviceInfoSet, ref SetupApiNative.SpDevinfoData data, uint prop)
    {
        var bytes = TryGetPropertyBytes(deviceInfoSet, ref data, prop, out _);
        if (bytes is null || bytes.Length == 0)
        {
            return null;
        }

        var s = System.Text.Encoding.Unicode.GetString(bytes);
        var nul = s.IndexOf('\0');
        if (nul >= 0)
        {
            s = s[..nul];
        }

        return s;
    }

    private static string[] TryGetMultiStringProperty(nint deviceInfoSet, ref SetupApiNative.SpDevinfoData data, uint prop)
    {
        var bytes = TryGetPropertyBytes(deviceInfoSet, ref data, prop, out _);
        if (bytes is null || bytes.Length == 0)
        {
            return Array.Empty<string>();
        }

        var s = System.Text.Encoding.Unicode.GetString(bytes);
        var parts = s.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return parts;
    }

    private static byte[]? TryGetPropertyBytes(nint deviceInfoSet, ref SetupApiNative.SpDevinfoData data, uint prop, out uint regType)
    {
        regType = 0;
        var buffer = new byte[1024];
        if (SetupApiNative.SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref data, prop, out regType, buffer, (uint)buffer.Length, out var required))
        {
            if (required == 0)
            {
                return Array.Empty<byte>();
            }

            Array.Resize(ref buffer, (int)required);
            return buffer;
        }

        var err = Marshal.GetLastWin32Error();
        if (required == 0 || err == 0)
        {
            return null;
        }

        buffer = new byte[required];
        if (!SetupApiNative.SetupDiGetDeviceRegistryPropertyW(deviceInfoSet, ref data, prop, out regType, buffer, (uint)buffer.Length, out required))
        {
            return null;
        }

        Array.Resize(ref buffer, (int)required);
        return buffer;
    }

    private sealed record Entry(string NormalizedName, int? Vid, int? Pid);
}

