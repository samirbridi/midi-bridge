using System.Buffers;

namespace Bridge.Core.Midi;

public readonly record struct Midi1Message(DateTimeOffset Timestamp, ReadOnlyMemory<byte> Data)
{
    public static Midi1Message Now(ReadOnlyMemory<byte> data) => new(DateTimeOffset.UtcNow, data);

    public byte Status => Data.Span.Length > 0 ? Data.Span[0] : (byte)0;

    public int Length => Data.Length;

    public bool IsShortMessage => Data.Length is 1 or 2 or 3;

    public bool IsSysEx => Data.Length >= 2 && Data.Span[0] == 0xF0 && Data.Span[^1] == 0xF7;

    public byte[] ToArray()
    {
        if (Data.IsEmpty)
        {
            return Array.Empty<byte>();
        }

        var arr = new byte[Data.Length];
        Data.CopyTo(arr);
        return arr;
    }

    public static Midi1Message FromArray(DateTimeOffset timestamp, byte[] data)
    {
        if (data.Length == 0)
        {
            throw new ArgumentException("MIDI message cannot be empty", nameof(data));
        }

        return new Midi1Message(timestamp, data);
    }

    public static IMemoryOwner<byte> RentAndCopy(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            throw new ArgumentException("MIDI message cannot be empty", nameof(data));
        }

        var owner = MemoryPool<byte>.Shared.Rent(data.Length);
        data.CopyTo(owner.Memory.Span[..data.Length]);
        return owner;
    }
}

