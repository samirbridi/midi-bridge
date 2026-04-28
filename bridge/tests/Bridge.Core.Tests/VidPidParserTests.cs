using Bridge.Core.Devices;

namespace Bridge.Core.Tests;

public class VidPidParserTests
{
    [Theory]
    [InlineData(@"USB\VID_09E8&PID_0001", 0x09E8, 0x0001)]
    [InlineData(@"usb\vid_1234&pid_abcd", 0x1234, 0xABCD)]
    [InlineData(@"HID\VID_18D1&PID_4EE7", 0x18D1, 0x4EE7)]
    public void Parses_VidPid(string s, int vid, int pid)
    {
        Assert.True(VidPidParser.TryParseVidPid(s, out var v, out var p));
        Assert.Equal(vid, v);
        Assert.Equal(pid, p);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NO_VID_PID")]
    [InlineData(@"USB\VID_09E8")]
    [InlineData(@"USB\PID_0001")]
    public void Returns_False_When_Missing(string s)
    {
        Assert.False(VidPidParser.TryParseVidPid(s, out _, out _));
    }
}

